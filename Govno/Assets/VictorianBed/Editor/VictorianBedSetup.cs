// Editor-only helper, limited to the VictorianBed asset folder.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class VictorianBedSetup
{
    static bool running;
    static readonly string[] Names = { "VB_Iron", "VB_Wood", "VB_Mattress", "VB_Pillows", "VB_Piping" };
    static readonly float[] Tiling = { 2, 1, 5, 2, 3 };
    static VictorianBedSetup() { EditorApplication.delayCall += AutoSetup; }
    public static void Queue() { EditorApplication.delayCall -= AutoSetup; EditorApplication.delayCall += AutoSetup; }
    static void AutoSetup() { Setup(false); }

    [MenuItem("Tools/Victorian Bed/Setup materials")]
    static void ManualSetup() { Setup(true); }

    static Texture2D Texture(string root, string name, string suffix, bool normal = false)
    {
        string path = root + "/Textures/" + name + suffix;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
        bool srgb = suffix.Contains("BaseColor");
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.sRGBTexture != srgb || importer.alphaSource != TextureImporterAlphaSource.FromInput)
        {
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Setup(bool force)
    {
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating) { if (!running) Queue(); return; }
        running = true;
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("Victorian_Bed t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/VictorianBed/Victorian_Bed.fbx", StringComparison.Ordinal)) continue;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || (!force && importer.userData == "VictorianBed_PBR_v2")) continue;
                string root = Path.GetDirectoryName(path).Replace('\\', '/');
                var pipeline = GraphicsSettings.currentRenderPipeline;
                string kind = pipeline == null ? "Standard" : pipeline.GetType().Name.Contains("HDRender") ? "HDRP/Lit" : "Universal Render Pipeline/Lit";
                Shader shader = Shader.Find(kind);
                if (shader == null) throw new InvalidOperationException("Shader unavailable: " + kind);
                if (!AssetDatabase.IsValidFolder(root + "/Materials")) AssetDatabase.CreateFolder(root, "Materials");
                for (int i = 0; i < Names.Length; i++)
                {
                    string name = Names[i], matPath = root + "/Materials/" + name + ".mat";
                    var color = Texture(root, name, "_BaseColor.png");
                    var normal = Texture(root, name, "_Normal.jpg", true);
                    var mask = Texture(root, name, "_MetallicSmoothness.png");
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
                    mat.shader = shader;
                    if (kind == "HDRP/Lit")
                    {
                        mat.SetTexture("_BaseColorMap", color); mat.SetColor("_BaseColor", Color.white);
                        mat.SetTexture("_NormalMap", normal); mat.SetFloat("_NormalScale", .4f);
                        mat.SetTexture("_MaskMap", mask); mat.SetFloat("_Metallic", 1);
                        mat.SetFloat("_SmoothnessRemapMin", 0); mat.SetFloat("_SmoothnessRemapMax", 1);
                        mat.EnableKeyword("_NORMALMAP_TANGENT_SPACE"); mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_MASKMAP");
                        mat.SetTextureScale("_BaseColorMap", Vector2.one * Tiling[i]);
                    }
                    else
                    {
                        string baseSlot = kind == "Standard" ? "_MainTex" : "_BaseMap";
                        mat.SetTexture(baseSlot, color); mat.SetColor(kind == "Standard" ? "_Color" : "_BaseColor", Color.white);
                        mat.SetTextureScale(baseSlot, Vector2.one * Tiling[i]);
                        mat.SetTexture("_BumpMap", normal); mat.SetFloat("_BumpScale", .4f); mat.EnableKeyword("_NORMALMAP");
                        mat.SetTexture("_MetallicGlossMap", mask); mat.SetFloat("_Metallic", 1);
                        mat.SetFloat("_GlossMapScale", 1); mat.SetFloat("_Smoothness", 1);
                        mat.EnableKeyword(kind == "Standard" ? "_METALLICGLOSSMAP" : "_METALLICSPECGLOSSMAP");
                        if (mat.HasProperty("_WorkflowMode")) mat.SetFloat("_WorkflowMode", 1);
                    }
                    EditorUtility.SetDirty(mat);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                }
                importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                importer.userData = "VictorianBed_PBR_v2";
                AssetDatabase.SaveAssets(); importer.SaveAndReimport();
                Debug.Log("Victorian Bed: PBR materials configured (" + kind + "). Drag Victorian_Bed.fbx into your scene.");
            }
        }
        catch (Exception ex) { Debug.LogError("Victorian Bed setup: " + ex.Message + ". Retry Tools > Victorian Bed > Setup materials."); }
        finally { running = false; }
    }
}

public class VictorianBedImportHook : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
            if (path.EndsWith("/VictorianBed/Victorian_Bed.fbx", StringComparison.Ordinal)) { VictorianBedSetup.Queue(); break; }
    }
}
