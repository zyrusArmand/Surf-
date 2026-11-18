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
    // serves the whole repo, so once it is committed and pushed it is at <site>/play/ for a phone.
    //
    // The models' textures are packed inside their .glb files, which Unity's glTF importer unpacks
    // raw (731 MB of them), far too much for a web page. So for the length of this build only, each
    // model file is swapped for a copy whose textures are shrunk to 512 (WebMax): the originals are
    // kept in Library/SurfWebBackup and put back the moment the build is done -- or, if Unity stops
    // in the middle, the next time it opens (RestoreIfInterrupted). The game's own files never change.
    const int WebMax = 512;
    static string ModelsDir => System.IO.Path.Combine(Application.dataPath, "Resources/Models");
    static string BackupDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Library/SurfWebBackup"));

    [MenuItem("Surf/Build web version for phone (GitHub Pages)")]
    public static void BuildPages()
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../play"));
        if (!EditorUtility.DisplayDialog("Build the web version",
            "Builds the game for the web into\n" + path + "\n\nFor the web build only, the models' textures are shrunk; your game's own files are put back exactly as they were afterwards. It takes a while. Then commit and push the play folder, and it opens on a phone at your GitHub Pages address + /play/",
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
        PlayerSettings.WebGL.dataCaching = false;                    // the data may come in parts joined on the page, which a cache can't key
        if (System.IO.Directory.Exists(path)) System.IO.Directory.Delete(path, true);

        BuildReport report = null; int shrunk = 0;
        try
        {
            RestoreOriginals();                                       // anything left from an interrupted build first
            shrunk = SwapInWebModels();
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = path,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
        }
        catch (System.Exception ex) { Debug.LogException(ex); }
        finally
        {
            EditorUtility.DisplayProgressBar("Web build", "Putting your models back", 1f);
            RestoreOriginals();
            EditorUtility.ClearProgressBar();
        }
        if (report == null) { EditorUtility.DisplayDialog("Build failed", "Something went wrong before the build; see the Console. Your models are back as they were.", "OK"); return; }
        var sum = report.summary;
        if (sum.result != BuildResult.Succeeded)
        {
            // the first few errors, said here rather than left in the Console
            string why = "";
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if ((m.type == LogType.Error || m.type == LogType.Exception) && why.Split('\n').Length < 5)
                        why += "\n- " + (m.content.Length > 200 ? m.content.Substring(0, 200) : m.content);
            EditorUtility.DisplayDialog("Build failed", (why.Length > 0 ? why.TrimStart('\n') : "No error was reported; see the Console.") + "\n\n(" + sum.result + ")\n\nYour models are back as they were.", "OK");
            Debug.LogError("Web build failed: " + sum.result + why);
            return;
        }
        // GitHub refuses any one file over 100 MB: the game's data is cut into parts under that, and
        // the page fetches them and joins them back before the game starts
        string split = SplitData(System.IO.Path.Combine(path, "Build"), path);
        PhoneResolution(path);
        string big = "";
        long all = 0;
        foreach (var f in System.IO.Directory.GetFiles(path, "*", System.IO.SearchOption.AllDirectories))
        {
            long n = new System.IO.FileInfo(f).Length; all += n;
            if (n > 95L * 1024 * 1024) big += "\n" + System.IO.Path.GetFileName(f) + " (" + n / (1024 * 1024) + " MB)";
        }
        if (big.Length > 0)
        {
            System.IO.Directory.Delete(path, true);
            EditorUtility.DisplayDialog("Too big for GitHub", "A file is still over GitHub's 100 MB limit:" + big + "\n\nThe build was removed so it can't be committed by mistake.", "OK");
            return;
        }
        EditorUtility.DisplayDialog("Built", "Web version built: " + all / (1024 * 1024) + " MB to download" + split + " (" + shrunk + " model textures shrunk for the web).\n\nYour models are back as they were. Now commit and push the play folder in GitHub Desktop.", "OK");
        EditorUtility.RevealInFinder(path);
    }

    // ---- the swap ----
    [System.Serializable] class GImage { public int bufferView = -1; public string mimeType; public string uri; }
    [System.Serializable] class GView { public int buffer; public int byteOffset; public int byteLength; }
    [System.Serializable] class GDoc { public GImage[] images; public GView[] bufferViews; }

    // every model whose textures can be made smaller is backed up and overwritten with the smaller
    // copy; returns how many textures were shrunk
    static int SwapInWebModels()
    {
        System.IO.Directory.CreateDirectory(BackupDir);
        var files = System.IO.Directory.GetFiles(ModelsDir, "*.glb");
        int count = 0;
        var changed = new System.Collections.Generic.List<string>();
        for (int i = 0; i < files.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Web build", "Shrinking textures for the web: " + System.IO.Path.GetFileName(files[i]), i / (float)files.Length);
            var bytes = System.IO.File.ReadAllBytes(files[i]);
            int n = ShrinkGlb(bytes);
            if (n == 0) continue;
            string keep = System.IO.Path.Combine(BackupDir, System.IO.Path.GetFileName(files[i]));
            if (!System.IO.File.Exists(keep)) System.IO.File.Copy(files[i], keep);
            System.IO.File.WriteAllBytes(files[i], bytes);
            changed.Add(files[i]); count += n;
        }
        EditorUtility.DisplayProgressBar("Web build", "Importing the web models", 1f);
        Reimport(changed);
        EditorUtility.ClearProgressBar();
        return count;
    }

    // the originals back over the copies, if there are any to put back
    static void RestoreOriginals()
    {
        if (!System.IO.Directory.Exists(BackupDir)) return;
        var kept = System.IO.Directory.GetFiles(BackupDir, "*.glb");
        var back = new System.Collections.Generic.List<string>();
        foreach (var k in kept)
        {
            string to = System.IO.Path.Combine(ModelsDir, System.IO.Path.GetFileName(k));
            System.IO.File.Copy(k, to, true);
            System.IO.File.Delete(k);
            back.Add(to);
        }
        Reimport(back);
    }

    [InitializeOnLoadMethod]
    static void RestoreIfInterrupted()
    {
        if (!System.IO.Directory.Exists(BackupDir) || System.IO.Directory.GetFiles(BackupDir, "*.glb").Length == 0) return;
        // a web build stopped half way: put the real models back as soon as the editor is ready
        EditorApplication.delayCall += () =>
        {
            if (BuildPipeline.isBuildingPlayer) return;
            RestoreOriginals();
            Debug.Log("Web build: your models were put back after an interrupted build.");
        };
    }

    static void Reimport(System.Collections.Generic.List<string> full)
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")).Replace('\\', '/') + "/";
        foreach (var f in full)
        {
            string rel = System.IO.Path.GetFullPath(f).Replace('\\', '/');
            if (rel.StartsWith(root)) rel = rel.Substring(root.Length);
            AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }

    // A .glb's jpg and png textures larger than WebMax re-encoded smaller, each written back over its
    // own bytes in the binary chunk (the rest zeroed, which the decoders stop short of), so not one
    // offset or length in the file changes. Returns how many were shrunk; 0 leaves the bytes alone.
    static int ShrinkGlb(byte[] d)
    {
        if (d.Length < 28 || d[0] != (byte)'g' || d[1] != (byte)'l' || d[2] != (byte)'T' || d[3] != (byte)'F') return 0;
        int jsonLen = System.BitConverter.ToInt32(d, 12);
        if (System.BitConverter.ToInt32(d, 16) != 0x4E4F534A) return 0;
        string json = System.Text.Encoding.UTF8.GetString(d, 20, jsonLen);
        int binAt = 20 + jsonLen;
        if (binAt + 8 > d.Length || System.BitConverter.ToInt32(d, binAt + 4) != 0x004E4942) return 0;
        int bin = binAt + 8;
        GDoc doc;
        try { doc = JsonUtility.FromJson<GDoc>(json); } catch { return 0; }
        if (doc == null || doc.images == null || doc.bufferViews == null) return 0;
        int n = 0;
        foreach (var im in doc.images)
        {
            if (im == null || !string.IsNullOrEmpty(im.uri) || im.bufferView < 0 || im.bufferView >= doc.bufferViews.Length) continue;
            bool png = im.mimeType == "image/png", jpg = im.mimeType == "image/jpeg";
            if (!png && !jpg) continue;
            var v = doc.bufferViews[im.bufferView];
            int at = bin + v.byteOffset, len = v.byteLength;
            if (at < bin || at + len > d.Length) continue;
            var src = new byte[len]; System.Array.Copy(d, at, src, 0, len);
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!t.LoadImage(src) || Mathf.Max(t.width, t.height) <= WebMax) continue;
                float k = WebMax / (float)Mathf.Max(t.width, t.height);
                int nw = Mathf.Max(1, Mathf.RoundToInt(t.width * k)), nh = Mathf.Max(1, Mathf.RoundToInt(t.height * k));
                var small = Box(t.GetPixels32(), t.width, t.height, nw, nh);
                var o = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
                o.SetPixels32(small); o.Apply();
                byte[] enc = png ? o.EncodeToPNG() : o.EncodeToJPG(85);
                Object.DestroyImmediate(o);
                if (enc == null || enc.Length > len) continue;                      // only ever smaller, in its own place
                System.Array.Copy(enc, 0, d, at, enc.Length);
                System.Array.Clear(d, at + enc.Length, len - enc.Length);
                n++;
            }
            finally { Object.DestroyImmediate(t); }
        }
        return n;
    }

    // each new pixel the average of the block of old ones it covers
    static Color32[] Box(Color32[] s, int w, int h, int nw, int nh)
    {
        var o = new Color32[nw * nh];
        for (int y = 0; y < nh; y++)
        {
            int y0 = y * h / nh, y1 = Mathf.Max(y0 + 1, (y + 1) * h / nh);
            for (int x = 0; x < nw; x++)
            {
                int x0 = x * w / nw, x1 = Mathf.Max(x0 + 1, (x + 1) * w / nw);
                int r = 0, g = 0, b = 0, a = 0, c = 0;
                for (int yy = y0; yy < y1; yy++)
                {
                    int row = yy * w;
                    for (int xx = x0; xx < x1; xx++) { var p = s[row + xx]; r += p.r; g += p.g; b += p.b; a += p.a; c++; }
                }
                o[y * nw + x] = new Color32((byte)(r / c), (byte)(g / c), (byte)(b / c), (byte)(a / c));
            }
        }
        return o;
    }

    // a phone draws the page at 1x rather than its 3x screen: far fewer pixels, and the game keeps up
    static void PhoneResolution(string path)
    {
        string page = System.IO.Path.Combine(path, "index.html");
        if (!System.IO.File.Exists(page)) return;
        string html = System.IO.File.ReadAllText(page);
        const string Off = "// config.devicePixelRatio = 1;";
        if (html.Contains(Off)) System.IO.File.WriteAllText(page, html.Replace(Off, "config.devicePixelRatio = 1;"));
        else Debug.LogWarning("Web build: the page had no devicePixelRatio line to switch on");
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
            int hook = html.LastIndexOf(Hook);
            if (hook < 0) { Debug.LogError("Web build: the page has no '" + Hook + "' to hook the data parts onto"); return " (could not hook the data parts into the page)"; }
            string join =
                "(function () {\n" +
                "        var parts = [" + string.Join(", ", names) + "];\n" +
                "        Promise.all(parts.map(function (p) { return fetch(p).then(function (r) { if (!r.ok) throw new Error(p + \": \" + r.status); return r.arrayBuffer(); }); }))\n" +
                "          .then(function (bufs) { config.dataUrl = URL.createObjectURL(new Blob(bufs)); document.body.appendChild(script); })\n" +
                "          .catch(function (e) { alert(\"Couldn't load the game's data: \" + e); });\n" +
                "      })();";
            html = html.Substring(0, hook) + join + html.Substring(hook + Hook.Length);
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
