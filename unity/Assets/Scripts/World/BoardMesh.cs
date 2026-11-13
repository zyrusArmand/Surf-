// A surfboard, built the way the web game builds every board in its shop: a loft of rings from
// nose to tail with the outline, thickness, rocker and rail profile of that board's spec,
// painted per vertex from the web's own paint (baked under Resources/Boards, one file per
// board, in exactly this vertex order), a grip pad over the back third and its fin set.
// Numbers come from BoardCatalog, generated from the web's BOARDS list.
using System.Collections.Generic;
using UnityEngine;

public static class BoardMesh
{
    const float FinToe = 0.048f, FinCantMax = 0.132f;

    static Color Hex(uint h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);
    static Color Vivid(uint hex, float boost = 1f)
    {
        var c = Hex(hex); float avg = (c.r + c.g + c.b) / 3f, k = 0.30f * boost;
        c.r = Mathf.Clamp01(avg + (c.r - avg) * (1 + k)); c.g = Mathf.Clamp01(avg + (c.g - avg) * (1 + k)); c.b = Mathf.Clamp01(avg + (c.b - avg) * (1 + k));
        return c.linear;
    }
    static float Sm(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
    static float Round(float d, float r) => d >= r ? 1 : Mathf.Sqrt(Mathf.Max(0, 1 - (1 - d / r) * (1 - d / r)));

    // ---- the shape, from the spec ----
    class Shape
    {
        public BoardCatalog.Board S; float up, peak, uN, uT, tipR;
        public Shape(BoardCatalog.Board b)
        {
            S = b; up = b.noseA / (b.noseA + b.tailA); peak = Mathf.Pow(up, b.noseA) * Mathf.Pow(1 - up, b.tailA);
            tipR = Mathf.Min(0.055f, Mathf.Max(0.010f, b.T * 0.62f / b.L * b.tipK));
            uN = b.noseW > 0 ? InvBeta(b.noseW, 0, up) : 0; uT = b.tailW > 0 ? InvBeta(b.tailW, 1, up) : 1;
        }
        float Beta(float u) => Mathf.Pow(Mathf.Max(0, u), S.noseA) * Mathf.Pow(Mathf.Max(0, 1 - u), S.tailA) / peak;
        float InvBeta(float t, float lo, float hi) { for (int k = 0; k < 44; k++) { float m = (lo + hi) * 0.5f; if (Beta(m) < t) lo = m; else hi = m; } return (lo + hi) * 0.5f; }
        float Close(float u) => Round(u, tipR) * Round(1 - u, tipR);
        float Ends(float u) { float capN = S.W * S.noseW, capT = S.swallow > 0 ? 0 : S.W * S.tailW; return (capN > 0 ? Round(u * S.L, capN) : 1) * (capT > 0 ? Round((1 - u) * S.L, capT) : 1); }
        public float Hw(float u) => S.W * Beta(uN + (uT - uN) * u) * Ends(u);
        public float Th(float u) => Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(u), S.thSkew)), S.thPow) * Close(u);
        public float Rock(float u) => S.rockN * Mathf.Pow(Mathf.Max(0, (S.noseR - u) / S.noseR), 2.2f) + S.rockT * Mathf.Pow(Mathf.Max(0, (u - 1 + S.tailR) / S.tailR), 2.2f);
        public float AsW(float v) => v < 0 ? S.asymW : 1f;
        public float Notch(float u, float v)
        {
            float n = S.swallow > 0 ? -S.swallow * Sm(0.80f, 1f, u) * (1 - Mathf.Pow(Mathf.Abs(v), 1.7f)) : 0f;
            if (S.asymZ != 0 && v < 0) n += -S.asymZ * Sm(0.62f, 1f, u) * Mathf.Abs(v);
            return n;
        }
    }

    static Material glass; static readonly Dictionary<uint, Material> flat = new Dictionary<uint, Material>();
    static Material Flat(uint hex, float smooth)
    {
        if (flat.TryGetValue(hex, out var m)) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", Vivid(hex)); m.SetFloat("_Smoothness", smooth);
        flat[hex] = m; return m;
    }

    // ---- the grip pad's grooves ----
    // The web's pad is a flat matte patch in its colour (8047: roughness 0.78); here it keeps that
    // colour and finish, and carries a diamond-groove relief, so it reads as foam traction rather
    // than a hole in the deck. A small normal map made once, tiled every half foot.
    static Texture2D grooves; static readonly Dictionary<uint, Material> padMats = new Dictionary<uint, Material>();
    static Texture2D Grooves()
    {
        if (grooves != null) return grooves;
        const int N = 128;
        var px = new Color[N * N]; var h = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // two families of grooves at plus and minus 45 degrees, four of each across a tile,
                // with rounded shoulders, so the cells between them are diamonds
                float a = Mathf.Repeat((x + y) / (float)N * 4f, 1f), b = Mathf.Repeat((x - y) / (float)N * 4f, 1f);
                float ga = Mathf.SmoothStep(0f, 1f, Mathf.Abs(a - 0.5f) * 2f), gb = Mathf.SmoothStep(0f, 1f, Mathf.Abs(b - 0.5f) * 2f);
                h[y * N + x] = Mathf.Min(1f - Mathf.Pow(1f - ga, 6f), 1f - Mathf.Pow(1f - gb, 6f));
            }
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N], dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                var n = new Vector3(-dx * 1.6f, -dy * 1.6f, 1f).normalized;
                // alpha 1: URP's unpacking takes x from red times alpha on desktop and from red on
                // phones, so a plain RGB normal with alpha 1 reads the same on both
                px[y * N + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        grooves = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "padGrooves", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        grooves.SetPixels(px); grooves.Apply(true, true);
        return grooves;
    }
    static Material PadMat(uint hex)
    {
        if (padMats.TryGetValue(hex, out var m)) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", Vivid(hex)); m.SetFloat("_Smoothness", 0.22f);
        m.SetTexture("_BumpMap", Grooves()); m.SetFloat("_BumpScale", 0.9f); m.EnableKeyword("_NORMALMAP");
        padMats[hex] = m; return m;
    }

    public static GameObject Build(string id) => Build(BoardCatalog.Get(id));

    /// the whole board as one object: the deck mesh, the pad and the fins, nose toward -z
    public static GameObject Build(BoardCatalog.Board S)
    {
        var sh = new Shape(S); int NU = S.nu, NR = S.nr; float L = S.L, T = S.T;
        var root = new GameObject("board:" + S.id);
        if (glass == null)
        {
            var g = Shader.Find("Surf/Glass");
            if (g == null) { Debug.LogWarning("Surf/Glass shader not found; the board falls back to Lit"); g = Shader.Find("Universal Render Pipeline/Lit"); }
            glass = new Material(g);
        }
        // the paint, baked from the web
        var paintAsset = Resources.Load<TextAsset>("Boards/" + S.id);
        byte[] paint = paintAsset != null ? paintAsset.bytes : null;
        int need = ((NU + 1) * NR + 2 * NR) * 3;
        if (paint != null && paint.Length != need) { Debug.LogWarning("board paint size mismatch for " + S.id); paint = null; }
        int pi = 0;
        Color Next()
        {
            if (paint == null) return new Color(0.9f, 0.9f, 0.85f);
            var c = new Color(paint[pi] / 255f, paint[pi + 1] / 255f, paint[pi + 2] / 255f); pi += 3; return c;
        }
        // the loft
        var pos = new List<Vector3>(); var col = new List<Color>(); var uvs = new List<Vector2>(); var idx = new List<int>();
        for (int i = 0; i <= NU; i++)
        {
            float u = i / (float)NU, w = sh.Hw(u), t = sh.Th(u), r = sh.Rock(u);
            for (int j = 0; j < NR; j++)
            {
                float phi = j / (float)NR * Mathf.PI * 2f;
                float v = Mathf.Sin(phi), sAbs = Mathf.Abs(v); bool top = Mathf.Cos(phi) >= 0;
                float prof = Mathf.Pow(Mathf.Max(0, 1 - sAbs * sAbs), top ? S.deckPow : S.hullPow);
                pos.Add(new Vector3(w * v * sh.AsW(v), r + (top ? T * t * prof : -T * S.hullK * t * prof), (u - 0.5f) * L + sh.Notch(u, v)));
                col.Add(Next()); uvs.Add(new Vector2(j / (float)NR, u));
            }
        }
        for (int i = 0; i < NU; i++) for (int j = 0; j < NR; j++)
        {
            int j2 = (j + 1) % NR, A = i * NR + j, B = i * NR + j2, C = (i + 1) * NR + j, D = (i + 1) * NR + j2;
            // wound so the shell faces OUT in Unity: (A, B, C) as three.js orders it put the deck's
            // faces looking down into the board, which culled the deck from above and showed the
            // inside of the hull — the board read as a hollow canoe with the sea in it
            idx.Add(A); idx.Add(C); idx.Add(B); idx.Add(B); idx.Add(C); idx.Add(D);
        }
        foreach (var (ring, fwd) in new[] { (0, false), (NU, true) })
        {
            float u = ring / (float)NU, cy = sh.Rock(u), cz = (u - 0.5f) * L + sh.Notch(u, 0);
            for (int j = 0; j < NR; j++)
            {
                int j2 = (j + 1) % NR; float phi = (j + 0.5f) / NR * Mathf.PI * 2f;
                int b = pos.Count; pos.Add(new Vector3(0, cy, cz)); col.Add(Next()); uvs.Add(new Vector2(phi / (Mathf.PI * 2), u));
                if (fwd) { idx.Add(b); idx.Add(ring * NR + j2); idx.Add(ring * NR + j); } else { idx.Add(b); idx.Add(ring * NR + j); idx.Add(ring * NR + j2); }
            }
        }
        var m = new Mesh { name = "deck" }; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(pos); m.SetColors(col); m.SetUVs(0, uvs); m.SetTriangles(idx, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var deck = new GameObject("deck", typeof(MeshFilter), typeof(MeshRenderer)); deck.transform.SetParent(root.transform, false);
        deck.GetComponent<MeshFilter>().sharedMesh = m; deck.GetComponent<MeshRenderer>().sharedMaterial = glass;

        // the grip pad(s): a thin skin on the deck, ends rounded; a twin-tip carries two
        if (S.pad)
        {
            var ranges = S.pads ?? new float[][] { new float[] { S.padU0, S.padU1 } };
            foreach (var rg in ranges)
            {
                const int PU = 34, PV = 18; var pp = new List<Vector3>(); var pix = new List<int>(); var puv = new List<Vector2>();
                for (int i = 0; i <= PU; i++)
                {
                    float f = i / (float)PU, d = Mathf.Min(f, 1 - f) / 0.12f;
                    float e = d >= 1 ? 1 : Mathf.Max(0.06f, Mathf.Sqrt(Mathf.Max(0, 1 - (1 - d) * (1 - d))));
                    float u = rg[0] + (rg[1] - rg[0]) * i / PU, k = S.padW * e, w = sh.Hw(u) * k, t = sh.Th(u), r = sh.Rock(u);
                    for (int j = 0; j <= PV; j++)
                    {
                        float v = -1 + 2f * j / PV, sv = k * v;
                        float prof = Mathf.Pow(Mathf.Max(0, 1 - sv * sv), S.deckPow);
                        // flat on the deck, as the web lays it: a raised kick at the tail was tried, and seen
                        // from behind the board it stood up as a dark box; the grooves carry the texture
                        pp.Add(new Vector3(w * v * sh.AsW(v), r + T * t * prof + 0.010f, (u - 0.5f) * L + sh.Notch(u, sv)));
                        puv.Add(new Vector2((v + 1f) * 0.5f * (w * 2f) / 0.5f, f * Mathf.Abs(rg[1] - rg[0]) * L / 0.5f));   // in half-feet, so the grooves keep one size on every board
                    }
                }
                for (int i = 0; i < PU; i++) for (int j = 0; j < PV; j++)
                {
                    int A = i * (PV + 1) + j, B = A + 1, C = A + PV + 1, D = C + 1;
                    pix.Add(A); pix.Add(C); pix.Add(B); pix.Add(B); pix.Add(C); pix.Add(D);   // facing up out of the deck in Unity, as the shell now does
                }
                var pm = new Mesh { name = "pad" }; pm.SetVertices(pp); pm.SetUVs(0, puv); pm.SetTriangles(pix, 0); pm.RecalculateNormals(); pm.RecalculateTangents(); pm.RecalculateBounds();
                var pad = new GameObject("pad", typeof(MeshFilter), typeof(MeshRenderer)); pad.transform.SetParent(root.transform, false);
                pad.GetComponent<MeshFilter>().sharedMesh = pm; pad.GetComponent<MeshRenderer>().sharedMaterial = PadMat(S.padCol);
            }
        }

        // the fins: a raked foil hung off the hull, side fins canted outward and toed in
        var finMat = Flat(S.finCol, 0.86f);
        foreach (var f in S.fins)
        {
            float ct = Mathf.Sign(f.cant) * Mathf.Min(Mathf.Abs(f.cant), FinCantMax);
            var fm = FinMesh(f.c, f.d, f.t, f.rake);
            var fg = new GameObject("fin", typeof(MeshFilter), typeof(MeshRenderer)); fg.transform.SetParent(root.transform, false);
            fg.GetComponent<MeshFilter>().sharedMesh = fm; fg.GetComponent<MeshRenderer>().sharedMaterial = finMat;
            float u = Mathf.Clamp01(f.z / L + 0.5f), v = Mathf.Clamp(f.x / Mathf.Max(1e-4f, sh.Hw(u)), -0.95f, 0.95f);
            float hullY = sh.Rock(u) - T * S.hullK * sh.Th(u) * Mathf.Pow(Mathf.Max(0, 1 - v * v), S.hullPow);
            fg.transform.localPosition = new Vector3(f.x, hullY + 0.004f, f.z);
            fg.transform.localRotation = Quaternion.Euler(0, f.x == 0 ? 0 : -Mathf.Sign(f.x) * FinToe * Mathf.Rad2Deg, -ct * Mathf.Rad2Deg);
        }
        // a board may wear a model over its loft: the tube, the jet ski and the kite twin-tip
        if (!string.IsNullOrEmpty(S.model)) Dress(root, S);
        return root;
    }

    // ---- a board may wear a MODEL over its loft (the web's boardArtFor) ----
    // The loft is built regardless: it is the box the file is fitted into, and it is the board
    // that is there when the file is not -- a board whose art depends on a file is a board that
    // is sometimes invisible, so the loft is never skipped. With the file in, the loft's deck,
    // pads and fins go dark and the file stands in their place, fitted the way the web's
    // fitToBox fits it: a quarter turn lays its longest side along the box's longest side, it is
    // scaled so that side matches, and it is centred on the box. The web's rider lines the decks
    // up to one slab height, which the port does not, so the box is where the loft is.
    // ---- and a board may say how big its model is, in feet ----
    // For a lofted board the box is not the published L: the outline runs to +/-W about the
    // centreline, so the slab is L long by about 1.65 W ACROSS, and for anything wider than it is
    // long the width is what the model would be fitted to. The tube found this: W 1.18 on a ring
    // meant to be two feet came out at the slab's width. modelD says the fitted size outright,
    // in feet: the box is scaled about its centre so its longest side is modelD, and the model
    // is fitted to that. The slab keeps its own size and stays the collision box.
    static void Dress(GameObject root, BoardCatalog.Board S)
    {
        if (Resources.Load<GameObject>("Models/" + S.model) == null) { Debug.LogWarning("board model " + S.model + " not found; " + S.id + " keeps its loft"); return; }
        var frame = root.transform;
        var box = LocalBox(root, frame);
        if (box.size.sqrMagnitude < 1e-8f) return;
        if (S.modelD > 0f)
        {
            float k = S.modelD / Mathf.Max(1e-6f, Mathf.Max(box.size.x, box.size.y, box.size.z));
            box = new Bounds(box.center, box.size * k);
        }
        var go = Models.Spawn(S.model, frame, 1f, PrimitiveType.Cube);
        FitBox(go, frame, box);
        // it drives bow-first: the ride points -z the way it travels, and half the time a fitted
        // model's long axis lands the other way round (the web's skiBowAt, run on every file)
        if (BowAt(go, frame) > 0)
        {
            go.transform.localRotation = Quaternion.Euler(0, 180f, 0) * go.transform.localRotation;
            var b = LocalBox(go, frame);
            go.transform.localPosition += box.center - b.center;
        }
        // the loft goes dark: the deck, the pads and the fins, the model in their place
        for (int i = 0; i < frame.childCount; i++) { var c = frame.GetChild(i); if (c != go.transform) c.gameObject.SetActive(false); }
    }

    /// the box of everything drawn under go, in frame's own axes (the corners of each mesh's
    /// box run through the transforms, so a turned fin or a spun model still measures right)
    static Bounds LocalBox(GameObject go, Transform frame)
    {
        bool any = false; var b = new Bounds();
        var toFrame = frame.worldToLocalMatrix;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh; if (m == null) continue;
            var M = toFrame * mf.transform.localToWorldMatrix; var mb = m.bounds;
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3((c & 1) == 0 ? mb.min.x : mb.max.x, (c & 2) == 0 ? mb.min.y : mb.max.y, (c & 4) == 0 ? mb.min.z : mb.max.z);
                var p = M.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    // the web's longAxis, ties broken the same way: x first, then y
    static int LongAxis(Vector3 v) => (v.x >= v.y && v.x >= v.z) ? 0 : (v.y >= v.z ? 1 : 2);

    /// the web's fitToBox: a quarter turn that lays the model's longest side along the target's
    /// longest side (a board exported down Blender's x still lands nose to tail), the scale that
    /// matches those two sides, and the model's box centred on the target's. go's parent is frame,
    /// so its local position, turn and scale are in the frame's axes; each turn is applied on the
    /// left, so it moves the frame's axes whatever the model's own are.
    static void FitBox(GameObject go, Transform frame, Bounds target)
    {
        var b = LocalBox(go, frame);
        int a = LongAxis(b.size), t = LongAxis(target.size);
        if (a != t)
        {
            Quaternion q = Quaternion.identity;
            if (a == 0 && t == 1) q = Quaternion.Euler(0, 0, 90); else if (a == 1 && t == 0) q = Quaternion.Euler(0, 0, -90);
            else if (a == 0 && t == 2) q = Quaternion.Euler(0, 90, 0); else if (a == 2 && t == 0) q = Quaternion.Euler(0, -90, 0);
            else if (a == 1 && t == 2) q = Quaternion.Euler(-90, 0, 0); else if (a == 2 && t == 1) q = Quaternion.Euler(90, 0, 0);
            go.transform.localRotation = q * go.transform.localRotation;
            b = LocalBox(go, frame);
        }
        float k = Mathf.Max(target.size.x, target.size.y, target.size.z) / Mathf.Max(1e-6f, Mathf.Max(b.size.x, b.size.y, b.size.z));
        go.transform.localScale = go.transform.localScale * k;
        b = LocalBox(go, frame);
        go.transform.localPosition += target.center - b.center;
    }

    /// Which end of a hull is the bow, as a sign: +1 if it points down +z, -1 if down -z (the
    /// web's skiBowAt). A bow is narrow and carries the height; a stern is wide, low and square.
    /// Comparing the two halves on both counts is steadier than either alone -- some hulls are
    /// barely tapered, some barely stepped -- so the two votes are summed and the sign of the
    /// total is the answer. A ring or a twin-tip is the same both ways, and lands on -1.
    /// Read off the vertices in the frame's axes; a mesh that cannot be read here says -1.
    static int BowAt(GameObject go, Transform frame)
    {
        var pts = new List<Vector3>();
        try
        {
            var toFrame = frame.worldToLocalMatrix;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; if (m == null || !m.isReadable) continue;
                var M = toFrame * mf.transform.localToWorldMatrix; var v = m.vertices;
                for (int i = 0; i < v.Length; i += 3) pts.Add(M.MultiplyPoint3x4(v[i]));
            }
        }
        catch { pts.Clear(); }
        if (pts.Count == 0) return -1;
        float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
        foreach (var p in pts) { if (p.z < lo) lo = p.z; if (p.z > hi) hi = p.z; }
        if (!(hi > lo)) return -1;
        float mid = (lo + hi) * 0.5f, wN = 0, wP = 0, hN = float.NegativeInfinity, hP = float.NegativeInfinity;
        foreach (var p in pts)
        {
            float w = Mathf.Abs(p.x);
            if (p.z < mid) { if (w > wN) wN = w; if (p.y > hN) hN = p.y; }
            else { if (w > wP) wP = w; if (p.y > hP) hP = p.y; }
        }
        // the narrower half is the bow; the taller half holds the console, which sits forward of centre
        return ((wP < wN ? 1 : -1) + (hP > hN ? 1 : -1)) >= 0 ? 1 : -1;
    }

    static Mesh FinMesh(float cL, float d, float ft, float rk)
    {
        var o = new List<Vector2>();
        Vector2 p0 = new Vector2(-cL * 0.5f, 0), p1 = new Vector2(-cL * 0.30f, -d * 0.44f), p2 = new Vector2(-cL * 0.02f + rk, -d * 0.82f), p3 = new Vector2(cL * 0.14f + rk, -d);
        Vector2 q1 = new Vector2(cL * 0.30f + rk * 0.5f, -d * 0.66f), q2 = new Vector2(cL * 0.42f, -d * 0.30f), q3 = new Vector2(cL * 0.5f, 0);
        for (int i = 0; i <= 26; i++) o.Add(Bez(p0, p1, p2, p3, i / 26f));
        for (int i = 1; i <= 26; i++) o.Add(Bez(p3, q1, q2, q3, i / 26f));
        int n = o.Count; var v = new List<Vector3>(); var idx = new List<int>();
        float h = Mathf.Max(0.012f, ft) * 0.5f;
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? -h : h; int b = v.Count;
            v.Add(new Vector3(x, -d * 0.45f, (p0.x + q3.x) * 0.5f + rk * 0.3f));
            for (int i = 0; i < n; i++) v.Add(new Vector3(x, o[i].y, o[i].x));
            for (int i = 0; i < n; i++)
            {
                int a = b + 1 + i, c = b + 1 + (i + 1) % n;
                if (side == 0) { idx.Add(b); idx.Add(c); idx.Add(a); } else { idx.Add(b); idx.Add(a); idx.Add(c); }
            }
        }
        int w0 = v.Count;
        for (int i = 0; i < n; i++) { v.Add(new Vector3(-h, o[i].y, o[i].x)); v.Add(new Vector3(h, o[i].y, o[i].x)); }
        for (int i = 0; i < n; i++)
        {
            int a = w0 + i * 2, b2 = a + 1, c = w0 + ((i + 1) % n) * 2, d2 = c + 1;
            idx.Add(a); idx.Add(c); idx.Add(b2); idx.Add(b2); idx.Add(c); idx.Add(d2);
        }
        // the outline was laid out for three.js's winding: every face turned round so it faces out in Unity
        for (int i = 0; i + 2 < idx.Count; i += 3) { int t0 = idx[i + 1]; idx[i + 1] = idx[i + 2]; idx[i + 2] = t0; }
        var m = new Mesh { name = "fin" }; m.SetVertices(v); m.SetTriangles(idx, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    static Vector2 Bez(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) { float s = 1 - t; return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d; }
}
