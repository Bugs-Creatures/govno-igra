using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class SetUpRecoveryBox
{
    [MenuItem("Tools/Inspect Recovery Box Scene State")]
    private static void Inspect()
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, "Assets/RecoveryBoxSceneSnapshot.unity", true);
    }

    [MenuItem("Tools/Set Up Recovery Bulb Box")]
    private static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Expected SampleScene in Edit mode.");
        const string path = "Assets/UntilSix/Cottage/Recovery Bulb Box.prefab";
        var original = Object.FindAnyObjectByType<BulbBox>();
        var interaction = Object.FindAnyObjectByType<LampInteraction>();
        var copy = Object.Instantiate(original.gameObject);
        copy.name = "Recovery Bulb Box";
        copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        GameObject prefab;
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(copy))
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var data = new SerializedObject(copy.GetComponent<BulbBox>());
            var bulbs = data.FindProperty("bulbs");
            GameObject spare = (GameObject)bulbs.GetArrayElementAtIndex(0).objectReferenceValue;
            for (int i = 1; i < bulbs.arraySize; i++)
            {
                var item = (GameObject)bulbs.GetArrayElementAtIndex(i).objectReferenceValue;
                if (item != null && item.transform.IsChildOf(copy.transform))
                    Object.DestroyImmediate(item);
            }
            bulbs.arraySize = 1;
            bulbs.GetArrayElementAtIndex(0).objectReferenceValue = spare;
            data.ApplyModifiedPropertiesWithoutUndo();
            prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
        }
        finally
        {
            Object.DestroyImmediate(copy);
        }

        if (prefab == null)
            throw new InvalidOperationException("Could not save recovery box prefab.");
        var player = new SerializedObject(interaction);
        player.FindProperty("recoveryBoxPrefab").objectReferenceValue = prefab.GetComponent<BulbBox>();
        player.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save SampleScene.");
        File.WriteAllText("Temp/RecoveryBoxSetup.txt", $"prefab={prefab.name} original={original.name}");
    }
}
