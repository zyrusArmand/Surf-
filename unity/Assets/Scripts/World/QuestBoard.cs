// The quest board on the title beach, ported from the web (index.html 24726-24912, 25067-25174,
// 49935-49970, 50180-50216, 13927-14096). Quests is a place, not a panel: pressing the sign walks
// the lens ninety-odd degrees round to a notice board standing on the sand, and the three goals
// are written on its pinned sheets. A tap on the board walks back; a tap on a sheet steps in to
// read it, and a tap anywhere then steps back out. The title beach is a strip of three stops --
// board, title, shop -- and a flick across the sand steps along it, as the web's swipe does.
//
// Everything is placed in the web's world and mirrored across x on the way in, exactly as Beach
// places the set (see Beach.M and Beach.R): the board, its two palms and the fixed pose the lens
// walks to were read off the running web game with the board pinned where it was staged.
// If the model is not in the build, Go() says so and the flat Quests panel is used instead.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class QuestBoard : MonoBehaviour
{
    // ---- where the board stands, in the web's world (its group, centred on the model's box) ----
    // menuProps is at (-13.5136, 3.0, -54.941) turned 153.6 degrees and scaled 1.15; the board's
    // pin in it is p (-6.513, 4.309, -16.887), r (-0.9, -42.6, -1.0), s 2.331 (MB_QB_PIN). The
    // product, read off the live scene, is this.
    static readonly Vector3 WebAt = new Vector3(-15.4237f, 7.9553f, -34.2145f);
    static readonly Quaternion WebTurn = new Quaternion(-0.0061f, 0.8243f, 0.0028f, 0.5661f);
    const float WebScale = 2.6806f;
    const float Sink = 0.010f;                 // MB_QB_SINK: barely dug in, this has feet

    // ---- the lens's fixed pose over there (QB_CAM): eased onto, not offset from the title shot ----
    static readonly Vector3 CamAt = new Vector3(-11.66f, 8.29f, -35.66f);
    const float CamYaw = 110.8f, CamPitch = 3.6f, Secs = 0.95f;
    // ...and in onto one sheet (QB_ZOOM_D, QB_ZOOM_SECS)
    const float ZoomD = 2.85f, ZoomSecs = 0.42f;

    // ---- the five ruled panels, as fractions of the board's own box (QB_SLOTS) ----
    // cx, cy (v runs down from the top), half-width, half-height, tilt in degrees
    static readonly float[,] Slots = {
        { 0.3250f, 0.2543f, 0.1327f, 0.0554f, 0.0f },
        { 0.7033f, 0.2867f, 0.1467f, 0.0606f, 0.0f },
        { 0.3257f, 0.4903f, 0.1370f, 0.0550f, 0.4f },
        { 0.7018f, 0.6291f, 0.1477f, 0.0579f, 0.5f },
        { 0.3246f, 0.7306f, 0.1465f, 0.0523f, 0.0f },
    };

    // ---- the two palms behind it: clones of the title's palm (MB_QB_TREES), as they stood ----
    // Kept as the web's world poses of the title palm and of each clone, so each clone is placed
    // by the same move that takes the title palm to it on the web -- whatever frame the palm model
    // happens to have here, the clone inherits it.
    static readonly Vector3 WebPalmAt = new Vector3(-12.87f, 0.0801f, -51.53f);
    static readonly Quaternion WebPalmTurn = new Quaternion(0.0679f, 0.9959f, 0.0041f, 0.0597f);
    const float WebPalmScale = 2.3f;
    static readonly Vector3[] WebTreeAt = { new Vector3(-28.7483f, 4.718f, -43.7551f), new Vector3(-30.3277f, 5.1671f, -34.8959f) };
    static readonly Quaternion[] WebTreeTurn = { new Quaternion(0f, 0.9759f, 0f, -0.2182f), new Quaternion(0.0555f, -0.4784f, -0.0642f, 0.874f) };
    static readonly float[] WebTreeScale = { 2.346f, 2.806f };
    const float TreeSink = 0.42f;              // MB_QB_TREE_SINK: feet of trunk below the sand

    // ---- the gestures, in the HUD's CSS pixels (430 across) ----
    const float TapSlop = 14f, SwipeMin = 56f, SwipeSecs = 0.70f, VerStrip = 60f;

    // the web world is mirrored across x here; see Beach.M / Beach.R
    static Vector3 M(Vector3 p) => new Vector3(-p.x, p.y, p.z);
    static Quaternion Rq(Quaternion q) => Quaternion.Normalize(new Quaternion(q.x, -q.y, -q.z, q.w));

    public System.Func<bool> blocked;          // a pane is open over the beach, or a run is on
    public System.Action onShop;               // the leftward flick from the title
    public System.Action onFallback;           // no board in the build: the flat panel instead

    Beach beach;
    ResourceRequest req; bool missing;
    GameObject board, trees; Transform inner;
    readonly List<Renderer> rends = new List<Renderer>();
    readonly List<Material[]> solid = new List<Material[]>(), fading = new List<Material[]>();
    bool clearNow = true; float inA;
    Vector3 camTo; Quaternion camTurnTo; float camDy, dyNow;
    readonly Transform[] slot = new Transform[5];
    readonly float[] slotHw = new float[5], slotHh = new float[5];

    // the walk (questCamGo / questCamE) and the sheet being read (qbFocus / qbZoomE)
    bool go; float e; int focus = -1; float zoomE;
    /// how far the lens has walked toward the board, 0 at the title and 1 there
    public float Away => e;
    /// the lens is moving, which is when the web drops its depth of field
    public bool Panning => (e > 0.002f && e < 0.998f) || (zoomE > 0.002f && zoomE < 0.998f);

    public void Init(Beach b) { beach = b; }

    /// the sign's Quests arm: walk over. False when the board cannot be built, so the caller
    /// opens the flat panel instead.
    public bool Go()
    {
        if (missing) return false;
        Want();
        go = true;
        DrawNotes(true);
        return true;
    }

    /// a run starting puts everything back where the title expects it
    public void Reset()
    {
        go = false; e = 0f; focus = -1; zoomE = 0f;
        swLive = false; tapArm = false; noteArm = -1;
        if (board != null) board.SetActive(false);
        if (trees != null) trees.SetActive(false);
    }

    // asked for on the first press and never before, as the web's wantBoard: five megabytes nobody
    // has walked over to are not loaded with the title
    void Want()
    {
        if (req != null || board != null || missing) return;
        req = Resources.LoadAsync<GameObject>("Models/questboard");
    }

    /// called every frame the beach is showing, after Beach.PlaceCamera has put the title shot
    public void Step(Camera cam, float dt)
    {
        if (req != null && req.isDone)
        {
            var prefab = req.asset as GameObject; req = null;
            if (prefab == null || !Build(prefab))
            {
                missing = true;
                if (go) { go = false; onFallback?.Invoke(); }
            }
        }
        // the clocks: clamped steps, so the move takes Secs whatever the frame rate does
        e = go ? Mathf.Min(1f, e + dt / Secs) : Mathf.Max(0f, e - dt / Secs);
        bool wantZ = go && focus >= 0;
        zoomE = wantZ ? Mathf.Min(1f, zoomE + dt / ZoomSecs) : Mathf.Max(0f, zoomE - dt / ZoomSecs);
        if (zoomE <= 0f && focus >= 0 && !go) focus = -1;

        // ---- the lens: eased onto the fixed pose, smoothstepped at both ends ----
        // The seat offset is walked in rather than applied at once, because the board can land
        // halfway through the walk and a lens that jumps by it would read as a cut.
        dyNow = Mathf.MoveTowards(dyNow, camDy, dt * 1.5f);
        CamPose();
        if (e > 0.0001f)
        {
            float s = e * e * (3f - 2f * e);
            cam.transform.position = Vector3.Lerp(cam.transform.position, camTo, s);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, camTurnTo, s);
        }
        // ...and in onto one sheet, by moving rather than turning, so it lands square and centred
        if (zoomE > 0.0001f && focus >= 0 && slot[focus] != null)
        {
            float z = zoomE * zoomE * (3f - 2f * zoomE);
            var w = slot[focus].position;
            cam.transform.position = Vector3.Lerp(cam.transform.position, w - cam.transform.forward * ZoomD, z);
        }

        Fade(dt);
        if (e > 0.0001f) DrawNotes(false);
        LayoutNotes(cam);
        Pointer(cam);
    }

    void CamPose()
    {
        camTo = M(CamAt) + new Vector3(0f, dyNow, 0f);
        camTurnTo = Quaternion.LookRotation(Quaternion.Euler(CamPitch, -CamYaw, 0f) * Vector3.back, Vector3.up);
    }

    // =====================================================================================
    // ---- building it ----
    bool Build(GameObject prefab)
    {
        var root = beach != null ? beach.Root : null;
        if (root == null) return false;
        board = new GameObject("QuestBoard"); board.transform.SetParent(root, false);
        board.transform.localPosition = Vector3.zero; board.transform.localRotation = Quaternion.identity; board.transform.localScale = Vector3.one;
        var go0 = Instantiate(prefab, board.transform); go0.name = "questboard"; Models.Repair(go0);
        inner = go0.transform;
        rends.Clear(); rends.AddRange(go0.GetComponentsInChildren<Renderer>(true));
        if (rends.Count == 0) { Destroy(board); board = null; return false; }
        // recentred on its own box, so the group's origin is the middle of the board and its local
        // coordinates run from -size/2 to +size/2, which is what the slots are measured in
        var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
        inner.localPosition -= board.transform.InverseTransformPoint(b.center);
        var size = b.size;
        // ---- not the hut's treatment: painted wood already mid-range, with its own roughness map.
        // Never metal; the sky fill is the scene's 0.55 already. Cast and take shadows.
        foreach (var r in rends)
        {
            var mats = r.materials;
            foreach (var m in mats) if (m != null) foreach (var p in new[] { "metallicFactor", "_Metallic" }) if (m.HasProperty(p)) m.SetFloat(p, 0f);
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true;
            solid.Add(mats);
            var f = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++) f[i] = FadeOf(mats[i]);
            fading.Add(f);
        }
        // ---- the five panels get a marker each: a transform, not a mesh ----
        // The model faces +z, so the markers sit a hair proud of the front face. x is mirrored with
        // the rest of the world, and so is the sheet's tilt about the board's normal.
        for (int i = 0; i < 5; i++)
        {
            var g = new GameObject("sheet" + i).transform; g.SetParent(board.transform, false);
            g.localPosition = new Vector3(-(Slots[i, 0] - 0.5f) * size.x, (0.5f - Slots[i, 1]) * size.y, size.z * 0.5f + size.z * 0.02f);
            g.localRotation = Quaternion.Euler(0f, 0f, Slots[i, 4]);
            slotHw[i] = Slots[i, 2] * size.x; slotHh[i] = Slots[i, 3] * size.y;
            slot[i] = g;
        }
        // ---- stood where it was staged, then seated on THIS sand ----
        board.transform.position = M(WebAt); board.transform.rotation = Rq(WebTurn); board.transform.localScale = Vector3.one * WebScale;
        // The sand here is not the web's to the inch, so the feet are put on the drawn tiles under
        // them (the web's own rule for an unpinned board: the ground, less a hundredth of its height),
        // and the lens's pose moves with it, so the shot is framed on the board exactly as staged.
        float bottom = float.MaxValue, top = float.MinValue;
        foreach (var r in rends) { bottom = Mathf.Min(bottom, r.bounds.min.y); top = Mathf.Max(top, r.bounds.max.y); }
        float ground = float.MinValue;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz++)
            {
                var p = board.transform.TransformPoint(new Vector3(sx * 0.40f * size.x, -0.5f * size.y, sz * 0.30f * size.z));
                ground = Mathf.Max(ground, beach.GroundTop(p.x, p.z));
            }
        camDy = (ground - Sink * (top - bottom)) - bottom;
        board.transform.position += new Vector3(0f, camDy, 0f);
        CamPose();
        Palms(root);
        BuildNotes();
        board.SetActive(false);
        return true;
    }

    void Palms(Transform root)
    {
        var palm = beach.PalmGo;
        if (palm == null || palm.name.EndsWith("(stand-in)")) return;
        trees = new GameObject("QuestPalms"); trees.transform.SetParent(root, false);
        var qp = Rq(WebPalmTurn); var pt = palm.transform;
        for (int i = 0; i < WebTreeAt.Length; i++)
        {
            var qt = Rq(WebTreeTurn[i]); var turn = qt * Quaternion.Inverse(qp); float k = WebTreeScale[i] / WebPalmScale;
            var c = Instantiate(palm, trees.transform); c.name = "palm" + i;
            c.transform.position = M(WebTreeAt[i]) + turn * ((pt.position - M(WebPalmAt)) * k);
            c.transform.rotation = turn * pt.rotation;
            c.transform.localScale = pt.localScale * k;
            // dug in as on the web: a few inches of trunk under the sand, measured against the sand
            // that is actually drawn here rather than the web's
            var w = c.transform.position;
            float webSand = WebTreeAt[i].y + TreeSink;
            c.transform.position = new Vector3(w.x, w.y + (beach.GroundTop(w.x, w.z) - webSand), w.z);
            foreach (var r in c.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
        }
        trees.SetActive(false);
    }

    /// a see-through stand-in for the half second the board fades in: the web turns its own
    /// materials transparent for that and opaque again the moment it has arrived
    static Material FadeOf(Material src)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null || src == null) return null;
        var m = new Material(lit) { name = src.name + " (fading)" };
        Texture t = null; foreach (var k in new[] { "baseColorTexture", "_BaseMap", "_MainTex" }) if (t == null && src.HasProperty(k)) t = src.GetTexture(k);
        Color c = Color.white; foreach (var k in new[] { "baseColorFactor", "_BaseColor", "_Color" }) if (src.HasProperty(k)) { c = src.GetColor(k); break; }
        if (t != null) m.SetTexture("_BaseMap", t);
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.25f);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    // ---- shown only while the lens is over there, faded with the walk (boardShow) ----
    // It has its own short ramp too, for a board that lands after the walk has already finished.
    void Fade(float dt)
    {
        bool on = e > 0.004f;
        if (trees != null && trees.activeSelf != on) trees.SetActive(on);
        if (board == null) return;
        if (board.activeSelf != on) board.SetActive(on);
        if (!on) return;
        inA = Mathf.Min(1f, inA + dt / 0.55f);
        float o = inA * Mathf.Clamp01((e - 0.05f) / 0.45f);
        bool clear = o >= 0.999f;
        if (clear != clearNow)
        {
            clearNow = clear;
            for (int i = 0; i < rends.Count; i++)
            {
                bool can = clear; foreach (var f in fading[i]) if (f == null) can = true;
                rends[i].sharedMaterials = can ? solid[i] : fading[i];
            }
        }
        if (!clear)
            foreach (var fs in fading) foreach (var f in fs) if (f != null) { var c = f.GetColor("_BaseColor"); c.a = o; f.SetColor("_BaseColor", c); }
    }

    // =====================================================================================
    // ---- the words on the five sheets ----
    // Ink on paper: brown, not black, no plate of any kind -- the paper the model draws is the
    // background. The goal's line drawing fills the ruled rectangle inked on the sheet, and the
    // words take the strip of paper under it. Drawn on a canvas lying on each sheet, so they
    // foreshorten and travel with the board the way the web's projected notes do.
    // Canvas units are CSS pixels at the size the sheet is drawn in the fixed shot, times K, so
    // the web's px and em sizes carry straight over and the font sizes have room to be exact.
    const float K = 4f;
    static readonly Color Ink = Solid.Rgba(0x6b, 0x4a, 0x25), InkDone = Solid.Rgba(0x4f, 0x7a, 0x33), InkSoon = Solid.Rgba(0x8a, 0x6b, 0x45);
    static readonly Color Glow = Solid.Rgba(255, 248, 228, 0.55f);
    class Note
    {
        public RectTransform rt; public Canvas cv; public CanvasScaler sc; public CanvasGroup cg;
        public Image icon; public RectTransform cap; public Text qt, qn; public RectTransform bar; public Image track, fill;
        public string qk = ""; public float bh;
        public bool soon;
    }
    readonly Note[] notes = new Note[5];
    string drawn = "", sizedFor = "";

    void BuildNotes()
    {
        var trackSp = Solid.Rounded(64, 6, 3);
        for (int i = 0; i < 5; i++)
        {
            var n = new Note();
            var go1 = new GameObject("words", typeof(RectTransform));
            go1.transform.SetParent(slot[i], false);
            n.cv = go1.AddComponent<Canvas>(); n.cv.renderMode = RenderMode.WorldSpace;
            n.sc = go1.AddComponent<CanvasScaler>();
            n.cg = go1.AddComponent<CanvasGroup>(); n.cg.interactable = false; n.cg.blocksRaycasts = false;
            n.rt = (RectTransform)go1.transform;
            // a canvas is read from its -z side; the board faces +z, so it is turned to face out
            n.rt.localPosition = Vector3.zero; n.rt.localRotation = Quaternion.Euler(0f, 180f, 0f); n.rt.pivot = new Vector2(0.5f, 0.5f);
            // the drawing, in the ruled rectangle: 3% in from each side, 16.3% down, 64.1% tall
            n.icon = UiKit.Pic(n.rt, "icon", null, new Vector2(0.03f, 0.196f), new Vector2(0.97f, 0.837f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            n.icon.preserveAspect = true; n.icon.raycastTarget = false;
            // the caption strip under the rule, from 80.5% down to the foot, clipped like the web's
            n.cap = UiKit.Rect(n.rt, "caption", new Vector2(0f, 0f), new Vector2(1f, 0.195f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            n.cap.gameObject.AddComponent<RectMask2D>();
            n.qt = Words(n.cap, "title"); n.qt.horizontalOverflow = HorizontalWrapMode.Wrap;
            n.qn = Words(n.cap, "count");
            n.bar = UiKit.Rect(n.cap, "bar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            n.track = Solid.Pic(n.bar, "track", trackSp, Solid.Rgba(107, 74, 37, 0.20f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            n.fill = Solid.Pic(n.bar, "fill", null, Color.white, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            go1.SetActive(false);
            notes[i] = n;
        }
        drawn = ""; sizedFor = "";
    }

    static float lineK = -1f;
    Text Words(Transform parent, string name)
    {
        var t = UiKit.Label(parent, "", 10, Ink, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, TextAnchor.UpperCenter, 0f);
        t.name = name;
        // CSS line-height 1.12, against whatever this font's own natural spacing is
        if (lineK < 0f) { var f = t.font; lineK = (f != null && f.fontSize > 0 && f.lineHeight > 0) ? Mathf.Clamp(1.12f / (f.lineHeight / (float)f.fontSize), 0.8f, 1.2f) : 1f; }
        t.lineSpacing = lineK;
        // text-shadow 0 1px 0: a pale line under the ink, which is what makes it read as written on paper
        var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = Glow; sh.effectDistance = new Vector2(0f, -K);
        return t;
    }

    /// drawQuestNotes: rebuilt only when what would be written actually differs
    void DrawNotes(bool force)
    {
        if (notes[0] == null) return;
        var gs = Quests.Ensure();
        var key = new System.Text.StringBuilder();
        foreach (var g in gs) key.Append(g.id).Append(':').Append(g.n).Append(':').Append(g.done ? 1 : 0).Append('|');
        string k = key.ToString();
        if (!force && k == drawn) return;
        drawn = k;
        for (int i = 0; i < 5; i++)
        {
            var n = notes[i];
            if (i >= gs.Count)
            {
                // the board has five pins and the game deals three goals; the spare sheets say what
                // they are, with the lookout, rather than look like something failed to load
                n.soon = true; n.qk = "soon";
                n.icon.sprite = UiKit.Icon("goal_tower"); n.icon.enabled = n.icon.sprite != null;
                n.icon.color = new Color(InkSoon.r, InkSoon.g, InkSoon.b, 0.55f);
                n.qt.text = "Next quest\nafter a run"; n.qt.fontStyle = FontStyle.BoldAndItalic; n.qt.color = InkSoon;
                n.qn.gameObject.SetActive(false); n.bar.gameObject.SetActive(false);
                n.cg.alpha = 0.6f;
                continue;
            }
            var gl = gs[i]; var d = Quests.Def(gl.id);
            string t = System.Text.RegularExpressions.Regex.Replace(d.text, @"\s+in (?:one|a) run$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            int cnt = Mathf.Min(gl.n, d.target); float pct = Mathf.Clamp01(cnt / (float)Mathf.Max(1, d.target));
            var ink = gl.done ? InkDone : Ink;
            n.soon = false; n.qk = gl.id + ":" + cnt + ":" + (gl.done ? 1 : 0);
            n.icon.sprite = UiKit.Icon("goal_" + d.icon); n.icon.enabled = n.icon.sprite != null;
            n.icon.color = new Color(ink.r, ink.g, ink.b, 0.92f);
            n.qt.text = t; n.qt.fontStyle = FontStyle.Bold; n.qt.color = ink;
            n.qn.gameObject.SetActive(true); n.bar.gameObject.SetActive(true);
            n.qn.text = gl.done ? "DONE  +" + Quests.Pay : Pane.N(cnt) + " / " + Pane.N(d.target);
            n.qn.color = new Color(ink.r, ink.g, ink.b, 0.86f);
            n.fill.sprite = gl.done ? Solid.Gradient(64, 6, 3, Solid.Rgba(0x5c, 0x9a, 0x3c), Solid.Rgba(0x8f, 0xd0, 0x6a))
                                    : Solid.Gradient(64, 6, 3, Solid.Rgba(0xe0, 0x8a, 0x2c), Solid.Rgba(0xf6, 0xbf, 0x4d));
            n.fill.rectTransform.anchorMax = new Vector2(gl.done ? 1f : pct, 1f);
            n.fill.enabled = (gl.done ? 1f : pct) > 0f;
            n.cg.alpha = 1f;
        }
        sizedFor = "";        // refit everything against the new words
    }

    // ---- layoutQuestNotes: up at 0.72, with the words sized to the sheet as it is drawn ----
    // The web sets the font from the panel's projected height every frame -- a quarter of it,
    // floored at 9px and capped at 20 -- and then shrinks it until the caption fits its strip.
    // Here the sheet is sized once for the fixed shot on this screen and the canvas scales with
    // the board through the walk, so the fit only reruns when the screen or the words change.
    void LayoutNotes(Camera cam)
    {
        if (notes[0] == null) return;
        bool live = board != null && board.activeSelf && e >= 0.72f;
        foreach (var n in notes) if (n.rt.gameObject.activeSelf != live) n.rt.gameObject.SetActive(live);
        // measured while live: a Text finds its canvas, and so its scale, only when it is active
        string sk = Screen.width + "x" + Screen.height;
        if (live && sk != sizedFor) { sizedFor = sk; Size(); }
    }

    void Size()
    {
        float cssH = Screen.height * 430f / Mathf.Max(1, Screen.width);          // the HUD's match-width scale
        float tanH = Mathf.Tan((beach != null ? beach.camFov : 92f) * 0.5f * Mathf.Deg2Rad);
        var fwd = camTurnTo * Vector3.forward;
        // rasterise the glyphs at about the device's own resolution, so they stay crisp without
        // shimmering when the board is small
        float dpu = Mathf.Clamp(Screen.width / 430f * 1.25f / K, 0.3f, 3f);
        for (int i = 0; i < 5; i++)
        {
            var n = notes[i];
            float sc = slot[i].lossyScale.y, hh = slotHh[i], hw = slotHw[i];
            float depth = Mathf.Max(0.1f, Vector3.Dot(slot[i].position - camTo, fwd));
            float bh = (2f * hh * sc) / (2f * depth * tanH) * cssH;              // the panel's height in CSS px
            n.bh = bh;
            float upl = bh * K / (2f * hh);                                         // canvas units per board unit
            n.rt.localScale = Vector3.one / upl;
            n.rt.sizeDelta = new Vector2(2f * hw * 0.90f, 2f * hh * 0.92f) * upl;
            n.sc.dynamicPixelsPerUnit = dpu;
            Fit(n);
        }
    }

    // the fit loop: from a quarter of the panel's height, down by 0.88 a step until the caption fits
    // its strip, never under 7px
    void Fit(Note n)
    {
        float want = Mathf.Clamp(n.bh * 0.245f, 9f, 20f), f = want;
        float capH = n.rt.sizeDelta.y * 0.195f, w = n.rt.sizeDelta.x;
        for (int t = 0; t < 14 && Need(n, f, w) > capH + K; t++)
        {
            f *= 0.88f;
            if (f < 7f) { f = 7f; break; }
        }
        Place(n, f, w);
    }

    float Need(Note n, float f, float w)
    {
        n.qt.fontSize = Mathf.Max(1, Mathf.RoundToInt(0.80f * f * K));
        n.qt.rectTransform.sizeDelta = new Vector2(0f, 0f);
        n.qt.rectTransform.anchorMin = new Vector2(0f, 1f); n.qt.rectTransform.anchorMax = new Vector2(1f, 1f);
        float h = n.qt.preferredHeight;
        if (n.soon) return h;
        n.qn.fontSize = Mathf.Max(1, Mathf.RoundToInt(0.60f * f * K));
        return h + 0.06f * f * K + n.qn.preferredHeight + 0.06f * f * K + 0.13f * f * K;
    }

    // stacked from the top of the strip, centred, with the web's .06em gaps between the lines
    void Place(Note n, float f, float w)
    {
        Need(n, f, w);
        float y = 0f, gap = 0.06f * f * K;
        float hq = n.qt.preferredHeight;
        Stack(n.qt.rectTransform, ref y, hq); y += gap;
        if (n.soon) return;
        float hn = n.qn.preferredHeight;
        Stack(n.qn.rectTransform, ref y, hn); y += gap;
        n.bar.anchoredPosition = new Vector2(0f, -y); n.bar.sizeDelta = new Vector2(w * 0.62f, 0.13f * f * K);
    }

    static void Stack(RectTransform rt, ref float y, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y); rt.sizeDelta = new Vector2(0f, h);
        y += h;
    }

    // =====================================================================================
    // ---- the gestures (13936-14053) ----
    // A tap on the board (not on a sheet, not on the top bar, not on the version strip at the foot
    // of the screen where the five taps live) walks home, or steps back out of a sheet first. A tap
    // on a sheet steps in to read it, and a second tap on the same sheet steps back. A quick flick
    // across the beach steps along board - title - shop, one stop at a time.
    bool swLive, tapArm; int noteArm = -1; Vector2 swAt, tapAt; float swT;

    void Pointer(Camera cam)
    {
        bool down, up; Vector2 pos; bool overUi;
        var es = EventSystem.current;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0); pos = t.position;
            down = t.phase == TouchPhase.Began; up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            overUi = es != null && es.IsPointerOverGameObject(t.fingerId);
        }
        else
        {
            pos = Input.mousePosition; down = Input.GetMouseButtonDown(0); up = Input.GetMouseButtonUp(0);
            overUi = es != null && es.IsPointerOverGameObject();
        }
        if (!down && !up) return;
        bool shut = blocked != null && blocked();
        float css = Screen.width / 430f;
        if (down)
        {
            swLive = false; tapArm = false; noteArm = -1;
            if (shut || overUi) return;
            bool strip = pos.y <= VerStrip * css;
            int note = NoteAt(cam, pos);
            bool here = go && e >= 0.55f;
            if (here && note >= 0) noteArm = note;
            if (here && note < 0 && !strip) { tapArm = true; tapAt = pos; }
            // a flick that starts on a sign arm or a sheet belongs to that, not to the beach
            if (note < 0 && !strip && !OnSign(cam, pos)) { swLive = true; swAt = pos; swT = Time.unscaledTime; }
            return;
        }
        // up
        if (shut) { swLive = false; tapArm = false; noteArm = -1; return; }
        if (tapArm)
        {
            tapArm = false;
            if ((pos - tapAt).magnitude <= TapSlop * css)
            {
                Sfx.Buzz(6);
                // zoomed in, the way out of the sheet is the way you came; only from the whole
                // board does a tap leave the room
                if (focus >= 0) focus = -1; else go = false;
            }
        }
        if (swLive)
        {
            swLive = false;
            float dx = pos.x - swAt.x, dy = pos.y - swAt.y;
            if (Time.unscaledTime - swT <= SwipeSecs && Mathf.Abs(dx) >= SwipeMin * css && Mathf.Abs(dx) >= Mathf.Abs(dy) * 1.5f)
            {
                if (dx < 0f)
                {
                    if (go) { Sfx.Buzz(6); focus = -1; go = false; }
                    else onShop?.Invoke();
                }
                else if (!go) { Sfx.Buzz(8); if (!Go()) onFallback?.Invoke(); }
            }
        }
        if (noteArm >= 0)
        {
            int was = noteArm; noteArm = -1;
            if (NoteAt(cam, pos) == was && go && e >= 0.55f) { Sfx.Buzz(8); focus = focus == was ? -1 : was; }
        }
    }

    /// which sheet's words are under a screen point, the words being the hit area as on the web
    int NoteAt(Camera cam, Vector2 screen)
    {
        if (notes[0] == null || !notes[0].rt.gameObject.activeInHierarchy) return -1;
        var ray = cam.ScreenPointToRay(screen);
        int best = -1; float bestD = float.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            var rt = notes[i].rt;
            var pl = new Plane(rt.forward, rt.position);
            if (!pl.Raycast(ray, out float d) || d >= bestD) continue;
            var l = rt.InverseTransformPoint(ray.GetPoint(d));
            if (Mathf.Abs(l.x) <= rt.sizeDelta.x * 0.5f && Mathf.Abs(l.y) <= rt.sizeDelta.y * 0.5f) { best = i; bestD = d; }
        }
        return best;
    }

    static bool OnSign(Camera cam, Vector2 screen)
    {
        return Physics.Raycast(cam.ScreenPointToRay(screen), out var hit, 200f) && hit.collider.name.StartsWith("arm:");
    }
}
