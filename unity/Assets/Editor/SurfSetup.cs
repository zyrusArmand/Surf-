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

    /// the same set-up, run straight through and allowed to fail loudly: for a build on a machine
    /// that has never opened the project (SurfCI), where nothing waits for the editor's delayCall
    public static void Prepare()
    {
        EnsurePipeline();
        EnsureAmbientOcclusion();
        EnsureShaderKeep();
        EnsurePlayerSettings();
        EnsureScene();
        EnsureModels();
        EnsureTextures();
    }

    [MenuItem("Surf/Set up project")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            EnsurePipeline();
            EnsureAmbientOcclusion();
            EnsureShaderKeep();
            EnsurePlayerSettings();
            EnsureScene();
            // after a crash Unity reopens on an empty Untitled scene; put the game's own back
            var act = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(act.path) && !act.isDirty && File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            EnsureModels();
            EnsureTextures();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Surf setup: " + e.Message + " (open Surf > Set up project to retry)");
        }
    }

    // Screen-space ambient occlusion on the renderer: the soft darkening where the palm meets the
    // sand, under the rider's arms, round the coconuts and the shop's shelves. Half resolution and
    // a short radius so it grounds things without costing a phone its frame rate. The sea sits in
    // the transparent queue, so it is never touched by it.
    static void EnsureAmbientOcclusion()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null) return;
        foreach (var f in data.rendererFeatures) if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") return;
        var t = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
        if (t == null) return;
        var ssao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(t);
        ssao.name = "SSAO";
        AssetDatabase.AddObjectToAsset(ssao, data);
        var so = new SerializedObject(ssao);
        void F(string n, float v) { var p = so.FindProperty("m_Settings." + n); if (p != null) p.floatValue = v; }
        void B(string n, bool v) { var p = so.FindProperty("m_Settings." + n); if (p != null) p.boolValue = v; }
        F("Intensity", 1.1f); F("Radius", 0.32f); F("DirectLightingStrength", 0.22f); F("Falloff", 140f);
        B("Downsample", true); B("AfterOpaque", false);
        so.ApplyModifiedPropertiesWithoutUndo();
        data.rendererFeatures.Add(ssao);
        EditorUtility.SetDirty(ssao); EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    // ---- the shaders a build must carry ----
    // Everything here is built at runtime and finds its shaders by name (Shader.Find). A player
    // build only packs the shaders some asset uses, and nothing in the project used these, so on a
    // phone the sea, the sky, the sand, the board and the effects would come up missing or pink.
    // One small material per shader (and per keyword set the code switches on at runtime) in a
    // Resources folder makes the build carry them. Safe to run again: it only adds what is absent.
    const string KeepDir = "Assets/Resources/ShaderKeep";
    static void EnsureShaderKeep()
    {
        const string Lit = "Universal Render Pipeline/Lit", Unlit = "Universal Render Pipeline/Unlit", PUnlit = "Universal Render Pipeline/Particles/Unlit";
        var want = new (string shader, string[] kw, bool clear)[] {
            ("Surf/Ocean", null, false), ("Surf/Sky", null, false), ("Surf/Sand", null, false), ("Surf/Glass", null, false),
            ("Surf/Cumulus", null, false), ("Surf/Puff", null, false), ("Surf/Cloud", null, false), ("Surf/Curl", null, false), ("Surf/FoamBank", null, false),
            ("Surf/Beam", null, false), ("Surf/Whirl", null, false), ("Surf/Character", null, false), ("Surf/TractorBeam", null, false), ("Surf/BeamPool", null, false),
            (Lit, null, false), (Lit, new[] { "_NORMALMAP" }, false), (Lit, new[] { "_EMISSION" }, false), (Lit, new[] { "_NORMALMAP", "_EMISSION" }, false),
            (Lit, new[] { "_NORMALMAP", "_ENVIRONMENTREFLECTIONS_OFF" }, false), (Lit, new[] { "_SPECULARHIGHLIGHTS_OFF" }, false),
            (Lit, new[] { "_SURFACE_TYPE_TRANSPARENT" }, true), (Lit, new[] { "_SURFACE_TYPE_TRANSPARENT", "_EMISSION" }, true),
            (Unlit, null, false), (Unlit, new[] { "_SURFACE_TYPE_TRANSPARENT" }, true),
            (PUnlit, new[] { "_SURFACE_TYPE_TRANSPARENT" }, true), (PUnlit, new[] { "_SURFACE_TYPE_TRANSPARENT", "_SOFTPARTICLES_ON" }, true),
            // the spray's drops, mist and glints also fade out toward the lens (Fx.NearFade)
            (PUnlit, new[] { "_SURFACE_TYPE_TRANSPARENT", "_SOFTPARTICLES_ON", "_FADING_ON" }, true), (PUnlit, new[] { "_SURFACE_TYPE_TRANSPARENT", "_FADING_ON" }, true),
        };
        bool made = false;
        foreach (var w in want)
        {
            string name = (w.shader + (w.kw != null ? "_" + string.Join("_", w.kw) : "")).Replace('/', '_').Replace(' ', '_');
            string path = KeepDir + "/" + name + ".mat";
            if (File.Exists(path)) continue;
            var sh = Shader.Find(w.shader);
            if (sh == null) continue;
            Directory.CreateDirectory(KeepDir);
            var m = new Material(sh) { name = name };
            if (w.kw != null) foreach (var k in w.kw) m.EnableKeyword(k);
            // the particle shaders' own switches for those keywords, so the editor's material checks
            // keep the keywords on instead of clearing them
            if (w.kw != null && System.Array.IndexOf(w.kw, "_SOFTPARTICLES_ON") >= 0 && m.HasProperty("_SoftParticlesEnabled")) m.SetFloat("_SoftParticlesEnabled", 1f);
            if (w.kw != null && System.Array.IndexOf(w.kw, "_FADING_ON") >= 0 && m.HasProperty("_CameraFadingEnabled")) m.SetFloat("_CameraFadingEnabled", 1f);
            if (w.clear)
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(m, path); made = true;
        }
        if (made) AssetDatabase.SaveAssets();
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
