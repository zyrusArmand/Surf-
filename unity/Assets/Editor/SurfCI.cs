// The automatic build (.github/workflows/unity-webgl.yml, through GameCI's unity-builder): a WebGL
// player of the game, so it can be run and photographed in a headless browser at sunrise, noon
// and sunset without anyone opening Unity. The clone it runs in has no ProjectSettings and an
// empty Library, so the project's own set-up runs first, straight through (SurfSetup.Prepare).
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SurfCI
{
    public static void BuildWebGL()
    {
        SurfSetup.Prepare();
        // compressed, and unpacked by the page itself, so any plain file server can host it
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = false;
        string path = Arg("-customBuildPath") ?? "build/WebGL/surf";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = path,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });
        var sum = report.summary;
        Debug.Log($"SurfCI: {sum.result}, {sum.totalErrors} errors, {sum.totalSize / (1024 * 1024)} MB, {sum.totalTime} -> {path}");
        if (sum.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    static string Arg(string name)
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }
}
