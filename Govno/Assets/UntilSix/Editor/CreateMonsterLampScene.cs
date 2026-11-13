using System.IO;
using UntilSix.Monster.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UntilSix.Monster.Editor
{
    public static class CreateMonsterLampScene
    {
        public const string ScenePath = "Assets/UntilSix/Demo/MonsterLampTest.unity";
        private const string MaterialsPath = "Assets/UntilSix/Demo/Materials";

        public static void Build()
        {
            Directory.CreateDirectory(MaterialsPath);
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material ceilingMaterial = CreateMaterial("Ceiling", new Color(0.22f, 0.27f, 0.34f));
            Material intactMaterial = CreateMaterial("IntactLamp", new Color(1f, 0.78f, 0.35f));
            Material brokenMaterial = CreateMaterial("BrokenLamp", new Color(0.10f, 0.12f, 0.15f));
            Material monsterMaterial = CreateMaterial("Monster", new Color(0.56f, 0.18f, 0.24f));

            GameObject ceiling = CreatePrimitive("Ceiling", PrimitiveType.Cube, null,
                new Vector3(0f, 3.2f, 0f), new Vector3(13f, 0.16f, 9f), ceilingMaterial);
            GameObject lampsRoot = new GameObject("Lamps");
            var lamps = new LampTarget[6];
            for (int index = 0; index < lamps.Length; index++)
            {
                Vector3 position = new Vector3((index % 3 - 1) * 4f, 3.02f, index < 3 ? -2.5f : 2.5f);
                lamps[index] = CreateLamp(index, position, lampsRoot.transform, intactMaterial, brokenMaterial);
            }

            var monster = new GameObject("Monster");
            monster.transform.position = new Vector3(0f, 0f, -0.5f);
            CreatePrimitive("Body", PrimitiveType.Sphere, monster.transform,
                new Vector3(0f, 0.75f, 0f), new Vector3(0.8f, 1.5f, 0.8f), monsterMaterial);
            DirectMonsterMovement movement = monster.AddComponent<DirectMonsterMovement>();
            LampHunter hunter = monster.AddComponent<LampHunter>();
            var serializedHunter = new SerializedObject(hunter);
            serializedHunter.FindProperty("movement").objectReferenceValue = movement;
            SerializedProperty lampList = serializedHunter.FindProperty("lamps");
            lampList.arraySize = lamps.Length;
            for (int index = 0; index < lamps.Length; index++)
                lampList.GetArrayElementAtIndex(index).objectReferenceValue = lamps[index];
            serializedHunter.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Overview Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            camera.orthographic = true;
            camera.orthographicSize = 5.9f;
            camera.transform.position = new Vector3(8f, -8f, -14f);
            camera.transform.LookAt(new Vector3(0f, 1.9f, 0f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save " + ScenePath);

            AssetDatabase.SaveAssets();
            Debug.Log($"Monster lamp test saved with {lamps.Length} lamps and ceiling {ceiling.name}.");
        }

        private static LampTarget CreateLamp(
            int index, Vector3 position, Transform parent, Material intactMaterial, Material brokenMaterial)
        {
            var lampObject = new GameObject($"Lamp {index + 1:00}");
            lampObject.transform.SetParent(parent, false);
            lampObject.transform.localPosition = position;
            LampTarget lamp = lampObject.AddComponent<LampTarget>();
            var approachPoint = new GameObject("Approach Point");
            approachPoint.transform.SetParent(lampObject.transform, false);
            approachPoint.transform.localPosition = Vector3.down * position.y;
            var serializedLamp = new SerializedObject(lamp);
            serializedLamp.FindProperty("approachPoint").objectReferenceValue = approachPoint.transform;
            serializedLamp.ApplyModifiedPropertiesWithoutUndo();

            Vector3 panelScale = new Vector3(0.95f, 0.2f, 0.95f);
            GameObject intact = CreatePrimitive("Intact", PrimitiveType.Cube, lampObject.transform,
                Vector3.zero, panelScale, intactMaterial);
            GameObject broken = CreatePrimitive("Broken", PrimitiveType.Cube, lampObject.transform,
                Vector3.zero, panelScale, brokenMaterial);
            broken.SetActive(false);

            LampDemoView view = lampObject.AddComponent<LampDemoView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("lamp").objectReferenceValue = lamp;
            serializedView.FindProperty("intactPanel").objectReferenceValue = intact;
            serializedView.FindProperty("brokenPanel").objectReferenceValue = broken;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            return lamp;
        }

        private static GameObject CreatePrimitive(
            string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(primitive.GetComponent<Collider>());
            return primitive;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("UntilSix/Demo Color"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
