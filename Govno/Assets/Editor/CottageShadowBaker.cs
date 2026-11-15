using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class CottageShadowBaker
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DepthPath = "Assets/Scenes/CottageShadowDepth.asset";
    private const string DefaultMaterialPath = "Assets/Materials/Cottage Lit.mat";
    private const string RecoveryBoxPath = "Assets/UntilSix/Cottage/Recovery Bulb Box.prefab";
    private const int Resolution = 256;
    // Rendering layers 2–5 carry the light index to the shader; layer 6 selects dynamic door casters.
    private const int LampIndexShift = 2;
    private const uint LampIndexMask = 15u << LampIndexShift;
    private const uint DoorShadowLayer = 1u << 6;

    private static readonly Vector3[] Forward =
        { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
    private static readonly Vector3[] Right =
        { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
    private static readonly Vector3[] Up =
        { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };

    [MenuItem("Tools/Cottage/Bake Lamp Shadows")]
    public static void Bake()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open SampleScene in Edit Mode to bake its lamp shadows.");

        GameObject[] roots = scene.GetRootGameObjects();
        GameObject lampsRoot = roots.Single(root => root.name == "Cottage Lamps");
        Light[] lights = lampsRoot.GetComponentsInChildren<CeilingLamp>(true)
            .OrderBy(lamp => lamp.name, StringComparer.Ordinal)
            .Select(lamp => (Light)new SerializedObject(lamp).FindProperty("lampLight").objectReferenceValue)
            .ToArray();
        if (lights.Length is 0 or > 15 || lights.Any(light => light == null || light.type != LightType.Point))
            throw new InvalidOperationException("The cottage bake supports one to fifteen point lights.");

        Shader cottageLit = Shader.Find("Until Six/Cottage Lit");
        if (cottageLit == null || ShaderUtil.ShaderHasError(cottageLit))
            throw new InvalidOperationException("Cottage Lit must compile before baking.");

        MeshRenderer[] casters = roots
            .Where(root => root.name is "Do_Shesti_Cottage" or "Victorian Props" or "Ground")
            .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>())
            .Where(renderer => renderer.GetComponentInParent<CottageDoor>() == null)
            .Concat(lampsRoot.GetComponentsInChildren<MeshRenderer>().Where(renderer => renderer.name == "Socket and Wires"))
            .Where(renderer => renderer.enabled && renderer.shadowCastingMode != ShadowCastingMode.Off)
            .ToArray();
        if (casters.Length == 0)
            throw new InvalidOperationException("The scene has no static cottage shadow casters.");

        Texture2DArray depth = SaveDepth(BakeDepth(lights, casters));

        for (int i = 0; i < lights.Length; i++)
        {
            UniversalAdditionalLightData data = lights[i].GetUniversalAdditionalLightData();
            data.renderingLayers = ((uint)data.renderingLayers & ~LampIndexMask) | (uint)(i + 1) << LampIndexShift;
            data.shadowRenderingLayers = DoorShadowLayer;
            data.customShadowLayers = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lights[i]);
        }

        foreach (CottageDoor door in roots.SelectMany(root => root.GetComponentsInChildren<CottageDoor>(true)))
        foreach (Renderer renderer in door.GetComponentsInChildren<Renderer>(true))
        {
            renderer.renderingLayerMask |= DoorShadowLayer;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        foreach (BulbBox box in roots.SelectMany(root => root.GetComponentsInChildren<BulbBox>(true)))
            DisableBoxShadows(box.gameObject);

        using (var prefab = new PrefabUtility.EditPrefabContentsScope(RecoveryBoxPath))
            DisableBoxShadows(prefab.prefabContentsRoot);

        foreach (Renderer renderer in roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
            AssignCottageShader(renderer, cottageLit);

        if (!lampsRoot.TryGetComponent(out CottageBakedShadows bakedShadows))
            bakedShadows = lampsRoot.AddComponent<CottageBakedShadows>();
        var settings = new SerializedObject(bakedShadows);
        settings.FindProperty("depth").objectReferenceValue = depth;
        settings.ApplyModifiedPropertiesWithoutUndo();
        bakedShadows.enabled = false;
        bakedShadows.enabled = true;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the cottage scene.");
        Debug.Log($"Baked {lights.Length} point-light shadow maps from {casters.Length} static renderers.");
    }

    private static Texture2DArray SaveDepth(Texture2DArray depth)
    {
        Texture2DArray existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(DepthPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(depth, DepthPath);
            return depth;
        }

        EditorUtility.CopySerialized(depth, existing);
        Object.DestroyImmediate(depth);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Texture2DArray BakeDepth(Light[] lights, MeshRenderer[] casters)
    {
        // A separate physics scene lets the rays use visible geometry and ignore gameplay colliders.
        Scene bakeScene = EditorSceneManager.NewPreviewScene();
        var meshes = new List<Mesh>(casters.Length);
        var result = new Texture2DArray(Resolution, Resolution, lights.Length * Forward.Length,
            TextureFormat.RHalf, false, true)
        {
            name = "Cottage Shadow Depth",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        try
        {
            foreach (MeshRenderer renderer in casters)
                AddCollider(bakeScene, renderer, meshes);
            Physics.SyncTransforms();
            PhysicsScene physics = bakeScene.GetPhysicsScene();
            var query = new QueryParameters(Physics.AllLayers, false, QueryTriggerInteraction.Ignore, true);
            using var rayStorage = new NativeArray<RaycastCommand>(Resolution * Resolution, Allocator.TempJob);
            var rays = rayStorage;
            using var hits = new NativeArray<RaycastHit>(rays.Length, Allocator.TempJob);
            using var pixelStorage = new NativeArray<ushort>(rays.Length, Allocator.Temp);
            var pixels = pixelStorage;

            for (int lamp = 0; lamp < lights.Length; lamp++)
            {
                Light light = lights[lamp];
                Vector3 center = light.transform.position;
                float near = light.shadowNearPlane;
                int blockers = 0;
                EditorUtility.DisplayProgressBar("Baking cottage shadows", light.transform.parent.name, (float)lamp / lights.Length);
                for (int face = 0; face < Forward.Length; face++)
                {
                    for (int y = 0; y < Resolution; y++)
                    for (int x = 0; x < Resolution; x++)
                    {
                        float u = ((x + 0.5f) * 2f / Resolution) - 1f;
                        float v = ((y + 0.5f) * 2f / Resolution) - 1f;
                        Vector3 direction = (Forward[face] + Right[face] * u + Up[face] * v).normalized;
                        rays[y * Resolution + x] = new RaycastCommand(physics, center + direction * near, direction, query, light.range - near);
                    }

                    hits.AsSpan().Clear();
                    RaycastCommand.ScheduleBatch(rays, hits, 128).Complete();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        float distance = hits[i].distance;
                        if (distance > 0)
                            blockers++;
                        pixels[i] = Mathf.FloatToHalf(distance > 0 ? distance + near : light.range);
                    }
                    result.SetPixelData(pixels, 0, lamp * Forward.Length + face);
                }
                if (blockers == 0)
                    throw new InvalidOperationException($"No occlusion was found for {light.transform.parent.name}.");
            }

            result.Apply(false, true);
            return result;
        }
        catch
        {
            Object.DestroyImmediate(result);
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            EditorSceneManager.ClosePreviewScene(bakeScene);
            foreach (Mesh mesh in meshes)
                Object.DestroyImmediate(mesh);
        }
    }

    private static void AddCollider(Scene scene, MeshRenderer renderer, List<Mesh> meshes)
    {
        Mesh source = renderer.GetComponent<MeshFilter>().sharedMesh;
        if (source == null)
            return;

        using Mesh.MeshDataArray sourceData = MeshUtility.AcquireReadOnlyMeshData(source);
        Mesh.MeshData data = sourceData[0];
        var triangles = new List<int>();
        Material[] materials = renderer.sharedMaterials;
        if (materials.Length == 0)
            return;

        for (int i = 0; i < data.subMeshCount; i++)
        {
            SubMeshDescriptor subMesh = data.GetSubMesh(i);
            Material material = materials[Mathf.Min(i, materials.Length - 1)];
            if (subMesh.topology != MeshTopology.Triangles || material == null ||
                !material.GetShaderPassEnabled("ShadowCaster") || material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                continue;

            if (material.IsKeywordEnabled("_ALPHATEST_ON"))
                throw new InvalidOperationException($"The shadow bake requires opaque geometry. Check {material.name} on {renderer.name}.");

            using var indices = new NativeArray<int>(subMesh.indexCount, Allocator.Temp);
            data.GetIndices(indices, i);
            triangles.AddRange(indices);
        }
        if (triangles.Count == 0)
            return;

        using var vertexStorage = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
        var vertices = vertexStorage;
        data.GetVertices(vertices);
        Matrix4x4 localToWorld = renderer.localToWorldMatrix;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = localToWorld.MultiplyPoint3x4(vertices[i]);

        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        meshes.Add(mesh);
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);

        var colliderObject = new GameObject(renderer.name);
        SceneManager.MoveGameObjectToScene(colliderObject, scene);
        colliderObject.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static void DisableBoxShadows(GameObject box)
    {
        foreach (Renderer renderer in box.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    private static void AssignCottageShader(Renderer renderer, Shader shader)
    {
        Material[] materials = renderer.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || material.shader.name != "Universal Render Pipeline/Lit")
                continue;

            if (AssetDatabase.GetAssetPath(material).StartsWith("Packages/", StringComparison.Ordinal))
            {
                Material replacement = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
                if (replacement == null)
                {
                    replacement = new Material(material);
                    SetCottageShader(replacement, shader);
                    AssetDatabase.CreateAsset(replacement, DefaultMaterialPath);
                }
                materials[i] = replacement;
                changed = true;
            }
            else
            {
                SetCottageShader(material, shader);
                EditorUtility.SetDirty(material);
            }
        }
        if (changed)
        {
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    private static void SetCottageShader(Material material, Shader shader)
    {
        int renderQueue = material.rawRenderQueue;
        bool motionVectors = material.GetShaderPassEnabled("MotionVectors");
        material.shader = shader;
        material.renderQueue = renderQueue;
        material.SetShaderPassEnabled("MotionVectors", motionVectors);
    }
}
