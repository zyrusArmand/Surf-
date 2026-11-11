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
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Surf setup: " + e.Message + " (open Surf > Set up project to retry)");
        }
    }

    static void EnsurePipeline()
    {
        if (GraphicsSettings.renderPipelineAsset != null) return;
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
        GraphicsSettings.renderPipelineAsset = asset;
        QualitySettings.renderPipeline = asset;
        Debug.Log("Surf setup: URP assigned");
    }

    static void EnsurePlayerSettings()
    {
        PlayerSettings.companyName = "Surf";
        PlayerSettings.productName = "Surf";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.surf.pug");
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.surf.pug");
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
