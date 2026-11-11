// The shop's photographs, taken the way the web takes them (snapshot ~12355, previewSetup ~10165):
// the item alone on a small stage -- a cyclorama in the beach's own colours, sand sweeping up
// into sky with no horizon, a soft round shadow under the subject, the web's three lights --
// shot once by a dedicated camera into a 164x250 (x2) texture, read back, and kept. A board is
// stood on its tail (x a quarter turn, YXZ) and yawed -0.34; a rider faces the lens (yaw pi),
// stood at ease. Nothing is photographed until a tile asks for it, and one item is shot a frame:
// it is built on one frame (so a skinned body has been skinned once) and shot on the next.
//
// The stage is parked far under the sea on a layer of its own. Its lights are off except for the
// length of its own render, and every other light is off for that length, so the stage lights
// never touch the game and the sun never touches the stage. The full-screen viewer builds a
// second stage of the same kind for itself (Viewer.cs).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class Previews
{
    public const int W = 164, H = 250;                  // the web's PREV_W x PREV_H, rendered at 2x
    public const int Layer = 30;                        // the stages' own layer; nothing else uses it
    const float Fov = 32f, Lift = 0.07f;                // prevCam's fov; how far above the middle the subject sits
    public static readonly Vector3 ThumbHome = new Vector3(0f, -3000f, 0f);
    public static readonly Vector3 ViewHome = new Vector3(3000f, -3000f, 0f);

    // the cardSet's colours
    static readonly Color SAND = Hex(0xd9bd8e), PALE = Hex(0xe8dcc2), HZ = Hex(0xd6ecf8), SKY = Hex(0x7cc6ee), SHADE = Hex(0x9a825c);
    public static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);
    static float Ss(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

    // =====================================================================================
    /// a stage: the backdrop, the shadow, the key and the fill, and an `items` anchor at its origin
    public sealed class Stage
    {
        public readonly Transform root, items, set, shadow;
        public readonly Light key, fill;
        readonly Texture2D shadowTex; float shadowA = -1f;

        public Stage(string name, Vector3 at)
        {
            var go = new GameObject(name); root = go.transform; root.position = at;
            items = Child(root, "items"); set = Child(root, "set");
            // the backdrop: 46 x 26, its foot 1.2 under the subject's base, 15 behind it; PALE ->
            // HZ over the first 13%, -> SKY from 10% to 34% of its height
            var back = FlatQuad(set, "back", Ramp(BackColor));
            back.localPosition = new Vector3(0f, 26f * 0.5f - 1.2f, -15f); back.localRotation = Quaternion.Euler(0f, 180f, 0f); back.localScale = new Vector3(46f, 26f, 1f);
            // the floor, 46 x 34, meeting the backdrop in exactly its own colour so the join disappears
            var floor = FlatQuad(set, "floor", Ramp(FloorColorV));
            floor.localPosition = new Vector3(0f, -1.2f, 2f); floor.localRotation = Quaternion.Euler(90f, 0f, 0f); floor.localScale = new Vector3(46f, 34f, 1f);
            // the shadow: a disc of 0x9a825c over the sand; drawn opaque at the colour the web's
            // transparent one comes out at, which spares the stage a transparent pass
            shadowTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var sgo = GameObject.CreatePrimitive(PrimitiveType.Cylinder); sgo.name = "shadow";
            var col = sgo.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
            sgo.transform.SetParent(set, false);
            sgo.GetComponent<MeshRenderer>().sharedMaterial = Flat(shadowTex);
            shadow = sgo.transform; shadow.localPosition = new Vector3(0f, -1.17f, 0f);
            SetShadow(0.40f);
            // hemisphere 0xffffff / 0x8fa6bd at 1.05 goes on as the ambient during a render (Render);
            // key 0xfff3e0 1.8 from (-1.4, 2.2, 2.6), fill 0xbfd8ff 0.55 from (2.0, 0.7, -1.8)
            key = NewLight(root, "key", Hex(0xfff3e0), 1.8f, new Vector3(-1.4f, 2.2f, 2.6f));
            fill = NewLight(root, "fill", Hex(0xbfd8ff), 0.55f, new Vector3(2.0f, 0.7f, -1.8f));
            Layerize(go);
        }

        /// a camera for this stage, off (it is rendered by hand), seeing only the stage layer
        public Camera NewCamera(float fov, float far, Color clear)
        {
            var go = new GameObject("cam"); go.transform.SetParent(root, false); go.layer = Layer;
            var c = go.AddComponent<Camera>();
            c.enabled = false; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = clear;
            c.cullingMask = 1 << Layer; c.fieldOfView = fov; c.nearClipPlane = 0.05f; c.farClipPlane = far;
            c.allowHDR = false; c.depth = -20f;
            var u = go.AddComponent<UniversalAdditionalCameraData>();
            u.renderPostProcessing = false; u.renderShadows = false; u.volumeLayerMask = 0;
            u.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            return c;
        }

        /// lays the cyclorama under a subject: scaled by k (the web's d/9), its sand at `bottom`,
        /// the shadow r across (in the set's units) at the given strength
        public void Backdrop(float k, float bottom, float r, float alpha)
        {
            set.localScale = Vector3.one * k;
            set.localPosition = new Vector3(0f, bottom + 1.2f * k, 0f);
            shadow.localScale = new Vector3(2f * r, 0.002f, 2f * r);
            SetShadow(alpha);
        }

        void SetShadow(float a)
        {
            if (Mathf.Abs(a - shadowA) < 1e-4f) return;
            shadowA = a;
            // the sand right under the subject (z = 0 on a floor centred at z = 2) with the shade over it
            shadowTex.SetPixel(0, 0, Color.Lerp(FloorColorV(0.5f - 2f / 34f), SHADE, a)); shadowTex.Apply(false);
        }

        public void Destroy() { if (root != null) Object.Destroy(root.gameObject); }
    }

    static Transform Child(Transform p, string n) { var g = new GameObject(n); g.transform.SetParent(p, false); return g.transform; }

    static Color BackColor(float t) { var c = Color.Lerp(PALE, HZ, Ss(0.00f, 0.13f, t)); return Color.Lerp(c, SKY, Ss(0.10f, 0.34f, t)); }
    // the floor quad's v runs toward the lens once it is laid down; the web's t runs away from it
    static Color FloorColorV(float v) { float t2 = 1f - v; return Color.Lerp(SAND, PALE, Ss(0.35f, 1.0f, t2)); }

    static Texture2D Ramp(System.Func<float, Color> f)
    {
        const int n = 128;
        var tex = new Texture2D(1, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n];
        for (int i = 0; i < n; i++) px[i] = f((i + 0.5f) / n);
        tex.SetPixels(px); tex.Apply(false, true);
        return tex;
    }

    static Shader flatShader;
    static Material Flat(Texture2D tex)
    {
        if (flatShader == null) flatShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (flatShader == null) flatShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (flatShader == null) flatShader = Shader.Find("Unlit/Texture");
        var m = new Material(flatShader);
        m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
        return m;
    }

    static Transform FlatQuad(Transform p, string n, Texture2D tex)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = n;
        var col = go.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
        go.transform.SetParent(p, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = Flat(tex);
        return go.transform;
    }

    static Light NewLight(Transform p, string n, Color c, float intensity, Vector3 from)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        go.transform.localRotation = Quaternion.LookRotation(-from.normalized, Vector3.up);   // a three.js light at `from` aims at the origin
        var l = go.AddComponent<Light>();
        l.type = UnityEngine.LightType.Directional; l.color = c; l.intensity = intensity; l.shadows = UnityEngine.LightShadows.None;
        l.cullingMask = 1 << Layer; l.enabled = false;
        return l;
    }

    /// puts a built object on the stage layer, out of every shadow, and keeps a skinned body skinned off-screen
    public static void Layerize(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;
    }

    // =====================================================================================
    // ---- the subjects ----
    /// builds one item on a stage, at its origin, under a holder that is the axis it turns on:
    /// a board stood on its tail (nose up, deck toward +z at yaw 0); a rider facing -z at yaw 0,
    /// stood at ease and wearing what is worn; a piece of gear worn alone by the rider you play
    public static GameObject Spawn(Stage s, string kind, string id)
    {
        var holder = new GameObject("item:" + kind + ":" + id);
        holder.transform.SetParent(s.items, false);
        if (kind == "board")
        {
            var b = BoardMesh.Build(id);
            b.transform.SetParent(holder.transform, false);
            b.transform.localPosition = Vector3.zero;
            b.transform.localRotation = Quaternion.AngleAxis(90f, Vector3.right);   // rotation.x = pi/2: the nose (-z) goes up
        }
        else if (kind == "gear")
        {
            var g = GearCatalog.Get(id);
            string rid = GearRider();
            var dress = new Save(); dress.rider = rid;
            if (g != null) GearCatalog.SetOn(dress, g.slot, id);
            BuildRider(holder.transform, rid, dress, g != null ? id : null);
        }
        else
        {
            var w = SaveData.S;
            var dress = new Save { rider = id, gearEyes = w.gearEyes, gearHead = w.gearHead, gearNeck = w.gearNeck, gearBody = w.gearBody };
            BuildRider(holder.transform, id, dress, null);
        }
        Layerize(holder);
        return holder;
    }

    /// the web's gearPreview wears the piece on the rider you play; the port dresses only the
    /// riders with a model file (the procedural ones ride bare), so those fall back to the pug
    static string GearRider()
    {
        var r = SaveData.S.rider;
        return !string.IsNullOrEmpty(r) && Resources.Load<GameObject>("Models/" + r) != null ? r : "pug";
    }

    static void BuildRider(Transform holder, string rid, Save dress, string grant)
    {
        bool proc = Resources.Load<GameObject>("Models/" + rid) == null && ProcRider.Has(rid);
        var go = proc ? ProcRider.Build(rid, holder) : Models.Spawn(rid, holder, ProcRider.RiderFt, PrimitiveType.Capsule);
        if (go == null) return;
        foreach (var a in go.GetComponentsInChildren<Animation>(true)) { a.Stop(); a.enabled = false; }
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        if (proc) { Portrait(go); return; }
        // riderPortrait: the standing pose he uses between runs, at full weight
        var rig = go.AddComponent<RiderRig>(); rig.Init(null);
        if (rig.Ready) { rig.SetBoard(null); rig.Idle(1f, 1f); }
        // the piece being photographed is worn whether or not the rank has granted it yet (the
        // web's gearPreviewAll); the grant is lent for the length of the dressing and taken back
        bool lent = false;
        if (grant != null && !GearCatalog.Have(grant)) { SaveData.S.ownedGear.Add(grant); lent = true; }
        try { global::Gear.Dress(go.transform, dress); }
        catch (System.Exception ex) { Debug.LogWarning("Previews: could not dress " + rid + ": " + ex.Message); }
        finally { if (lent) SaveData.S.ownedGear.Remove(grant); }
        Face(go.transform);
    }

    /// portraitPose for the procedural riders: the hips side by side, square to the body, a touch of knee
    static void Portrait(GameObject go)
    {
        var R = ProcRider.Of(go); if (R == null) return;
        float fx0 = -0.04f * Mathf.Cos(1.2f) - 0.10f * Mathf.Sin(1.2f);      // the front hip's socket before the build's scale
        float k = R.legF.hip.localPosition.x / fx0;
        if (!(k > 0.05f && k < 20f)) k = 1f;
        R.legF.hip.localPosition = new Vector3(-0.165f * k, 0.02f, 0f);
        R.legB.hip.localPosition = new Vector3(0.165f * k, 0.02f, 0f);
        R.legF.hip.localRotation = ProcRider.RotYXZ(-0.05f, 0f, 0f);
        R.legB.hip.localRotation = ProcRider.RotYXZ(-0.05f, 0f, 0f);
        R.LayHips(); R.Sweep();
    }

    /// a model whose face points the other way from the procedural riders' (-z) is turned round
    /// about the holder's axis, so yaw pi faces every rider into the lens
    static void Face(Transform go)
    {
        Transform head = null, front = null;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (head == null && t.name == "Head") head = t;
            if (front == null && t.name == "headfront") front = t;
        }
        if (head == null || front == null || go.parent == null) return;
        var f = go.parent.InverseTransformDirection(front.position - head.position); f.y = 0f;
        if (f.sqrMagnitude < 1e-8f) return;
        if (Mathf.Abs(Vector3.SignedAngle(f, Vector3.back, Vector3.up)) < 90f) return;
        var q = Quaternion.AngleAxis(180f, Vector3.up);
        go.localPosition = q * go.localPosition; go.localRotation = q * go.localRotation;
    }

    /// the web's frameObj: the vertical centre and half height of what is visible, the reach from
    /// the axis it turns on to the furthest corner (which holds at every yaw), and its base
    public static void Measure(GameObject holder, out float cy, out float hy, out float hr, out float bottom)
    {
        bool any = false; var b = new Bounds();
        foreach (var r in holder.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        var o = holder.transform.position;
        if (!any) b = new Bounds(o + Vector3.up, Vector3.one);
        Vector3 mn = b.min - o, mx = b.max - o;
        cy = (mn.y + mx.y) * 0.5f; hy = Mathf.Max(0.05f, (mx.y - mn.y) * 0.5f); bottom = mn.y;
        hr = 0f;
        float[] xs = { mn.x, mx.x }, zs = { mn.z, mx.z };
        for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) hr = Mathf.Max(hr, Mathf.Sqrt(xs[i] * xs[i] + zs[j] * zs[j]));
        hr = Mathf.Max(0.05f, hr);
    }

    /// takes a subject off a stage, with the meshes a board lofted for it
    public static void Despawn(GameObject holder)
    {
        if (holder == null) return;
        foreach (Transform c in holder.transform) ProcRider.Forget(c.gameObject);
        foreach (var mf in holder.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m != null && (m.name == "deck" || m.name == "pad" || m.name == "fin")) Object.Destroy(m);   // BoardMesh's own, made per build
        }
        holder.SetActive(false);
        Object.Destroy(holder);
    }

    // =====================================================================================
    // ---- a render, with the stage's light and only the stage's light ----
    static Light[] sceneLights; static float sceneAt = -99f; static bool[] was = new bool[0];
    public static void Render(Stage s, Camera c, RenderTexture target)
    {
        if (sceneLights == null || Time.unscaledTime - sceneAt > 0.5f) { sceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None); sceneAt = Time.unscaledTime; }
        if (was.Length < sceneLights.Length) was = new bool[sceneLights.Length];
        for (int i = 0; i < sceneLights.Length; i++)
        {
            var l = sceneLights[i]; if (l == null) continue;
            was[i] = l.enabled;
            if (l != s.key && l != s.fill) l.enabled = false;
        }
        s.key.enabled = true; s.fill.enabled = true;
        var mode = RenderSettings.ambientMode; Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, gr = RenderSettings.ambientGroundColor;
        bool fog = RenderSettings.fog;
        try
        {
            // the hemisphere light: white from above, 0x8fa6bd from below, at 1.05
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var up = Color.white * 1.05f; var dn = Hex(0x8fa6bd) * 1.05f;
            up.a = 1f; dn.a = 1f;
            RenderSettings.ambientSkyColor = up; RenderSettings.ambientGroundColor = dn; RenderSettings.ambientEquatorColor = Color.Lerp(up, dn, 0.5f);
            RenderSettings.fog = false;
            c.targetTexture = target;
            var req = new RenderPipeline.StandardRequest();
            req.destination = target;
            if (RenderPipeline.SupportsRenderRequest(c, req)) RenderPipeline.SubmitRenderRequest(c, req);
            else c.Render();
        }
        finally
        {
            RenderSettings.ambientMode = mode; RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gr;
            RenderSettings.fog = fog;
            s.key.enabled = false; s.fill.enabled = false;
            for (int i = 0; i < sceneLights.Length; i++) { var l = sceneLights[i]; if (l != null && l != s.key && l != s.fill) l.enabled = was[i]; }
        }
    }

    // =====================================================================================
    // ---- the thumbnails ----
    static Stage thumb; static Camera tcam; static RenderTexture trt;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite> muted = new Dictionary<string, Sprite>();
    static readonly List<string> queue = new List<string>();
    static GameObject staged; static string stagedKind, stagedId; static int stagedFrame;

    public static Sprite Board(string id) => Get("board", id);
    public static Sprite Rider(string id) => Get("rider", id);
    public static Sprite Gear(string id) => Get("gear", id);

    /// the photograph of an item, or null while it is still to be taken (asking queues it)
    public static Sprite Get(string kind, string id)
    {
        string k = kind + ":" + id;
        if (cache.TryGetValue(k, out var sp)) return sp;
        bool onStage = staged != null && stagedKind == kind && stagedId == id;
        if (!onStage && !queue.Contains(k)) queue.Add(k);
        return null;
    }

    /// the same photograph with the colour taken down, the web's `.card.lk .thumb { saturate(.55) }`
    public static Sprite Muted(string kind, string id)
    {
        var sp = Get(kind, id);
        if (sp == null) return null;
        return muted.TryGetValue(kind + ":" + id, out var m) && m != null ? m : sp;
    }

    /// true once the item has been photographed, or has failed to be
    public static bool Done(string kind, string id) => cache.ContainsKey(kind + ":" + id);

    /// forgets what was asked for and not yet taken: the shelf it was asked for has gone
    public static void Drop() { queue.Clear(); }

    /// every rider card wears what is worn, so a change of gear makes them all wrong
    public static void ForgetRiders() { Forget("rider:"); }
    /// every gear card is worn by the rider you play, so a change of rider makes them all wrong
    public static void ForgetGear() { Forget("gear:"); }

    static void Forget(string prefix)
    {
        var gone = new List<string>();
        foreach (var k in cache.Keys) if (k.StartsWith(prefix)) gone.Add(k);
        foreach (var k in gone)
        {
            Sprite a; if (cache.TryGetValue(k, out a) && a != null) { Object.Destroy(a.texture); Object.Destroy(a); }
            Sprite b; if (muted.TryGetValue(k, out b) && b != null) { Object.Destroy(b.texture); Object.Destroy(b); }
            cache.Remove(k); muted.Remove(k);
        }
    }

    /// one step of the queue: shoot what was built last frame, then build the next. Called every
    /// frame by whatever is showing tiles.
    public static void Pump()
    {
        if (staged != null && Time.frameCount > stagedFrame) Shoot();
        while (staged == null && queue.Count > 0)
        {
            string k = queue[0]; queue.RemoveAt(0);
            if (cache.ContainsKey(k)) continue;
            int c = k.IndexOf(':');
            Prepare(k.Substring(0, c), k.Substring(c + 1));
        }
    }

    static void EnsureThumb()
    {
        if (thumb != null && thumb.root != null) return;
        thumb = new Stage("PreviewStage", ThumbHome);
        tcam = thumb.NewCamera(Fov, 90f, SKY);
        trt = new RenderTexture(W * 2, H * 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "previews" };
        trt.Create();
    }

    static void Prepare(string kind, string id)
    {
        EnsureThumb();
        try { staged = Spawn(thumb, kind, id); stagedKind = kind; stagedId = id; stagedFrame = Time.frameCount; }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Previews: could not build " + kind + " " + id + ": " + ex.Message);
            cache[kind + ":" + id] = null; staged = null;
        }
    }

    static void Shoot()
    {
        var holder = staged; staged = null;
        string kind = stagedKind, id = stagedId, k = kind + ":" + id;
        try
        {
            bool board = kind == "board";
            holder.transform.localRotation = Quaternion.identity;        // framed stood up and square, then turned
            Measure(holder, out float cy, out float hy, out float hr, out float bottom);
            float aspect = W / (float)H, t = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            // a board is framed to fit whole; a surfer to be seen, taller in the tile and allowed past the sides
            float d = board ? Mathf.Max(hy / (t * (0.86f - Lift)), hr / (aspect * t * 0.84f))
                            : Mathf.Max(hy / (t * (0.92f - Lift)), hr / (aspect * t * 1.16f));
            d = Mathf.Max(d, 0.2f);
            float ks = d / 9f;
            thumb.Backdrop(ks, bottom, Mathf.Clamp(hr * 1.6f / ks, 0.9f, 2.6f), 0.40f);
            holder.transform.localRotation = Quaternion.AngleAxis((board ? -0.34f : Mathf.PI) * Mathf.Rad2Deg, Vector3.up);
            float lift = d * t * Lift;
            tcam.transform.localPosition = new Vector3(0f, cy + lift, d);
            tcam.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);   // level, at the lifted height
            Render(thumb, tcam, trt);        // the target is 328 x 500, so the lens takes the tile's aspect from it
            cache[k] = Read(trt, kind == "gear", k);
        }
        catch (System.Exception ex) { Debug.LogWarning("Previews: could not shoot " + k + ": " + ex.Message); cache[k] = null; }
        finally { Despawn(holder); }
    }

    static Sprite Read(RenderTexture rt, bool mute, string k)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false) { name = k, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
        RenderTexture.active = prev;
        if (mute)
        {
            // saturate(.55), the W3C matrix, on the sRGB bytes as a browser does it
            const float s = 0.55f;
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float r = px[i].r, g = px[i].g, b = px[i].b;
                float r2 = (0.213f + 0.787f * s) * r + (0.715f - 0.715f * s) * g + (0.072f - 0.072f * s) * b;
                float g2 = (0.213f - 0.213f * s) * r + (0.715f + 0.285f * s) * g + (0.072f - 0.072f * s) * b;
                float b2 = (0.213f - 0.213f * s) * r + (0.715f - 0.715f * s) * g + (0.072f + 0.928f * s) * b;
                px[i] = new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(r2), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(g2), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(b2), 0, 255), 255);
            }
            var mt = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false) { name = k + " muted", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            mt.SetPixels32(px); mt.Apply(false, true);
            muted[k] = Sprite.Create(mt, new Rect(0, 0, mt.width, mt.height), new Vector2(0.5f, 0.5f), 100f);
        }
        tex.Apply(false, true);          // to the GPU, and the CPU copy let go
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
