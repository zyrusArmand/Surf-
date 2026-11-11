// Runs once when the project is opened in the Unity editor. It does the set-up a person would
// otherwise click through: a Universal Render Pipeline asset assigned to the project, linear
// colour, portrait orientation, and a Main scene holding the one object that builds everything
// else at runtime. Safe to run again: every step checks whether it has already been done.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class SurfSetup
{
    const string PipelinePath = "Assets/Settings/SurfURP.asset";
    const string RendererPath = "Assets/Settings/SurfRenderer.asset";
    const string ScenePath = "Assets/Scenes/Main.unity";

    static SurfSetup()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Surf/Set up project")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            EnsurePipeline();
            EnsurePlayerSettings();
            EnsureScene();
            EnsureModels();
            EnsureTextures();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Surf setup: " + e.Message + " (open Surf > Set up project to retry)");
        }
    }

    static void EnsurePipeline()
    {
        if (GraphicsSettings.defaultRenderPipeline != null) return;
        Directory.CreateDirectory("Assets/Settings");
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (asset == null)
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
            asset = UniversalRenderPipelineAsset.Create(renderer);
            asset.supportsHDR = true;
            asset.msaaSampleCount = 4;
            asset.shadowDistance = 60f;
            asset.renderScale = 1f;
            AssetDatabase.CreateAsset(asset, PipelinePath);
            AssetDatabase.SaveAssets();
        }
        GraphicsSettings.defaultRenderPipeline = asset;
        QualitySettings.renderPipeline = asset;
        Debug.Log("Surf setup: URP assigned");
    }

    static void EnsurePlayerSettings()
    {
        PlayerSettings.companyName = "Surf";
        PlayerSettings.productName = "Surf";
        PlayerSettings.bundleVersion = "10.98";   // the web build this port tracks; the title prints it
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.surf.pug");
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.surf.pug");
        // the scripts read the classic Input class; make sure the project answers it (2 = both systems)
        var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (ps != null && ps.Length > 0)
        {
            var so = new SerializedObject(ps[0]);
            var ih = so.FindProperty("activeInputHandler");
            if (ih != null && ih.intValue == 1) { ih.intValue = 2; so.ApplyModifiedProperties(); }
        }
    }

    // The .glb files are imported the moment the project first opens, a few seconds before
    // EnsurePipeline switches the project to URP, so their materials come out built for the
    // wrong renderer and draw pink. Import them once more now that URP is in place. The marker
    // lives in Library, which is per machine and never committed, so this runs once per clone.
    const string ModelsMarker = "Library/surf-models-urp";
    static void EnsureModels()
    {
        if (GraphicsSettings.defaultRenderPipeline == null) return;
        if (File.Exists(ModelsMarker)) return;
        ReimportModels();
        File.WriteAllText(ModelsMarker, "done");
    }

    // the sand's bump map has to be imported AS a normal map, every load, since the file can
    // arrive after the one-time model pass has already run
    static void EnsureTextures()
    {
        var ti = AssetImporter.GetAtPath("Assets/Resources/Models/sand_normal.jpg") as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); Debug.Log("Surf setup: sand bump map set to normal map"); }
        // the sea's chop map is read as raw numbers, not colour: no sRGB, tiled, mipmapped
        var tc = AssetImporter.GetAtPath("Assets/Resources/Models/waternormals.png") as TextureImporter;
        if (tc != null && (tc.sRGBTexture || tc.wrapMode != TextureWrapMode.Repeat)) { tc.sRGBTexture = false; tc.wrapMode = TextureWrapMode.Repeat; tc.mipmapEnabled = true; tc.SaveAndReimport(); Debug.Log("Surf setup: chop map set to linear"); }
    }

    [MenuItem("Surf/Reimport models")]
    public static void ReimportModels()
    {
        // the sand grain is a normal map, which the importer has to be told
        var ti = AssetImporter.GetAtPath("Assets/Resources/Models/sand_normal.jpg") as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        AssetDatabase.ImportAsset("Assets/Resources/Models", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log("Surf setup: models imported for the current render pipeline");
    }

    static void EnsureScene()
    {
        if (File.Exists(ScenePath)) return;
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("Surf");
        go.AddComponent<Bootstrap>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("Surf setup: Main scene created. Open it and press Play.");
    }
}
