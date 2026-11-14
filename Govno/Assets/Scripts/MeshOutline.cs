using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class MeshOutline
{
    private static readonly int CenterId = Shader.PropertyToID("_OutlineCenter");
    private static Mesh cube;
    private readonly List<MeshRenderer> renderers = new();
    private readonly List<MeshRenderer> maskRenderers = new();
    private Material assignedMaterial;

    private MeshOutline() { }

    public static MeshOutline ForBox(BoxCollider collider)
    {
        if (cube == null)
        {
            cube = new Mesh { name = "Closed box outline" };
            cube.vertices = new Vector3[]
            {
                new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f),
                new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f),
                new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f),
                new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)
            };
            cube.triangles = new[]
            {
                0, 3, 2, 0, 2, 1, 4, 5, 6, 4, 6, 7,
                0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5,
                0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2
            };
        }

        // Use the closed silhouette so the interior dividers cannot produce outlines.
        var outline = new MeshOutline();
        MeshRenderer renderer = AddRenderer(collider.transform, cube);
        outline.renderers.Add(renderer);
        Transform shell = renderer.transform;
        shell.localPosition = collider.center;
        shell.localScale = collider.size;
        return outline;
    }

    public MeshOutline(IEnumerable<MeshFilter> filters, Material silhouetteMask)
    {
        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

            // Submit the mask and the expanded mesh as separate renderers, ordered by render queue.
            renderers.Add(AddRenderer(filter.transform, filter.sharedMesh));
            var mask = AddRenderer(filter.transform, filter.sharedMesh);
            var passes = new Material[filter.sharedMesh.subMeshCount];
            System.Array.Fill(passes, silhouetteMask);
            mask.sharedMaterials = passes;
            maskRenderers.Add(mask);
        }
    }

    private static MeshRenderer AddRenderer(Transform parent, Mesh mesh)
    {
        var child = new GameObject("Outline") { layer = parent.gameObject.layer };
        child.transform.SetParent(parent, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var properties = new MaterialPropertyBlock();
        properties.SetVector(CenterId, mesh.bounds.center);
        renderer.SetPropertyBlock(properties);
        return renderer;
    }

    public void SetMaterial(Material material)
    {
        bool materialChanged = material != null && assignedMaterial != material;
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (materialChanged)
            {
                int subMeshCount = renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                var materials = new Material[subMeshCount];
                System.Array.Fill(materials, material);
                renderer.sharedMaterials = materials;
            }
            renderer.enabled = material != null;
        }
        if (materialChanged)
            assignedMaterial = material;
        foreach (MeshRenderer mask in maskRenderers)
            if (mask != null)
                mask.enabled = material != null;
    }
}
