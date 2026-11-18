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

    // ---- the same player, built from the editor into the repo's play/ folder: GitHub Pages
    // serves the whole repo, so once it is committed and pushed it is at <site>/play/ for a phone
    [MenuItem("Surf/Build web version for phone (GitHub Pages)")]
    public static void BuildPages()
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../play"));
        if (!EditorUtility.DisplayDialog("Build the web version",
            "Builds the game for the web into\n" + path + "\n\nThe first time, Unity switches to the Web platform, which takes a while. Then commit and push the play folder, and it opens on a phone at your GitHub Pages address + /play/",
            "Build", "Cancel")) return;
        // without the Web module installed nothing can be built: say how to add it
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            EditorUtility.DisplayDialog("Web building isn't installed",
                "Unity Hub > Installs > the gear on " + Application.unityVersion + " > Add modules > tick \"Web Build Support\" > Install. Then restart Unity and build again.", "OK");
            return;
        }
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;           // GitHub Pages sends no Content-Encoding: the page unpacks it
        PlayerSettings.WebGL.dataCaching = false;                    // the data can come in parts joined on the page, which a cache can't key
        if (System.IO.Directory.Exists(path)) System.IO.Directory.Delete(path, true);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = path,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });
        var sum = report.summary;
        if (sum.result != BuildResult.Succeeded)
        {
            // the first few errors, said here rather than left in the Console
            string why = "";
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if ((m.type == LogType.Error || m.type == LogType.Exception) && why.Split('\n').Length < 5)
                        why += "\n- " + (m.content.Length > 200 ? m.content.Substring(0, 200) : m.content);
            EditorUtility.DisplayDialog("Build failed", (why.Length > 0 ? why.TrimStart('\n') : "No error was reported; see the Console.") + "\n\n(" + sum.result + ")", "OK");
            Debug.LogError("Web build failed: " + sum.result + why);
            return;
        }
        // GitHub refuses any one file over 100 MB: the game's data is cut into parts under that, and
        // the page fetches them and joins them back before the game starts
        string build = System.IO.Path.Combine(path, "Build");
        string split = SplitData(build, path);
        string big = "";
        foreach (var f in System.IO.Directory.GetFiles(path, "*", System.IO.SearchOption.AllDirectories))
            if (new System.IO.FileInfo(f).Length > 95L * 1024 * 1024) big += "\n" + System.IO.Path.GetFileName(f) + " (" + new System.IO.FileInfo(f).Length / (1024 * 1024) + " MB)";
        if (big.Length > 0)
        {
            System.IO.Directory.Delete(path, true);
            EditorUtility.DisplayDialog("Too big for GitHub", "A file is still over GitHub's 100 MB limit:" + big + "\n\nThe build was removed so it can't be committed by mistake.", "OK");
            return;
        }
        long all = 0; foreach (var f in System.IO.Directory.GetFiles(path, "*", System.IO.SearchOption.AllDirectories)) all += new System.IO.FileInfo(f).Length;
        EditorUtility.DisplayDialog("Built", "Web version built: " + all / (1024 * 1024) + " MB to download" + split + ".\n\nNow commit and push the play folder in GitHub Desktop.", "OK");
        EditorUtility.RevealInFinder(path);
    }

    // the data file, if it is over the limit, in parts of 90 MB, and the page taught to put them together
    static string SplitData(string build, string path)
    {
        const long Part = 90L * 1024 * 1024;
        if (!System.IO.Directory.Exists(build)) return "";
        foreach (var f in System.IO.Directory.GetFiles(build, "*.data*"))
        {
            var info = new System.IO.FileInfo(f);
            if (info.Length <= 95L * 1024 * 1024 || f.Contains(".part")) continue;
            var bytes = System.IO.File.ReadAllBytes(f);
            var names = new System.Collections.Generic.List<string>();
            for (long o = 0, n = 0; o < bytes.LongLength; o += Part, n++)
            {
                long len = System.Math.Min(Part, bytes.LongLength - o);
                var chunk = new byte[len]; System.Array.Copy(bytes, o, chunk, 0, len);
                string name = info.Name + ".part" + n;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(build, name), chunk);
                names.Add("\"Build/" + name + "\"");
            }
            System.IO.File.Delete(f);
            string page = System.IO.Path.Combine(path, "index.html");
            string html = System.IO.File.ReadAllText(page);
            const string Hook = "document.body.appendChild(script);";
            int at = html.LastIndexOf(Hook);
            if (at < 0) { Debug.LogError("Web build: the page has no '" + Hook + "' to hook the data parts onto"); return " (could not hook the data parts into the page)"; }
            string join =
                "(function () {\n" +
                "        var parts = [" + string.Join(", ", names) + "];\n" +
                "        Promise.all(parts.map(function (p) { return fetch(p).then(function (r) { if (!r.ok) throw new Error(p + \": \" + r.status); return r.arrayBuffer(); }); }))\n" +
                "          .then(function (bufs) { config.dataUrl = URL.createObjectURL(new Blob(bufs)); document.body.appendChild(script); })\n" +
                "          .catch(function (e) { alert(\"Couldn't load the game's data: \" + e); });\n" +
                "      })();";
            html = html.Substring(0, at) + join + html.Substring(at + Hook.Length);
            System.IO.File.WriteAllText(page, html);
            return ", its data in " + names.Count + " parts";
        }
        return "";
    }

    static string Arg(string name)
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }
}
