// The surfboard, built the way the web game builds it: a loft of rings from nose to tail
// with the outline, thickness, rocker and rail profile of a 5'10 egg, painted per vertex
// with the Dawn Patrol pour (gold at the nose through coral and magenta into indigo), a cream
// pinline just inside the rail, the 17 over the back foot, a grip pad over the back third and
// a 2+1 fin set. Numbers are the web spec for the Astro Original, verbatim, in feet.
using System.Collections.Generic;
using UnityEngine;

public static class BoardMesh
{
    // ---- the Astro Original ----
    const int NU = 230, NR = 62;
    const float L = 5.83f, W = 0.696f, T = 0.187f, NoseA = 0.586f, TailA = 1.433f, NoseW = 0.050f, TailW = 0.535f;
    const float RockN = 0.375f, NoseR = 0.330f, RockT = 0.167f, TailR = 0.260f, DeckPow = 0.66f, HullPow = 0.46f, HullK = 0.60f;
    const float PadU0 = 0.735f, PadU1 = 0.945f, PadW = 0.56f;
    const float FinToe = 0.048f, FinCantMax = 0.132f;
    struct Fin { public float x, z, c, d, t, rake, cant; }
    static readonly Fin[] Fins = {
        new Fin { x = 0.000f, z = 2.061f, c = 0.563f, d = 0.523f, t = 0.031f, rake = 0.17f },
        new Fin { x = -0.285f, z = 1.789f, c = 0.302f, d = 0.251f, t = 0.022f, rake = 0.08f, cant = 0.16f },
        new Fin { x = 0.32f, z = 1.789f, c = 0.302f, d = 0.251f, t = 0.022f, rake = 0.08f, cant = -0.16f },
    };

    // ---- colour helpers, as the web has them ----
    static Color Hex(uint h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);
    /// push saturation, then into linear light: the web's vivid()
    static Color Vivid(uint hex, float boost = 1f)
    {
        var c = Hex(hex); float avg = (c.r + c.g + c.b) / 3f, k = 0.30f * boost;
        c.r = Mathf.Clamp01(avg + (c.r - avg) * (1 + k)); c.g = Mathf.Clamp01(avg + (c.g - avg) * (1 + k)); c.b = Mathf.Clamp01(avg + (c.b - avg) * (1 + k));
        return c.linear;
    }
    static Color Mix(Color c, uint hex, float k) => Color.Lerp(c, Vivid(hex, 1.25f), Mathf.Clamp01(k));
    static float Sm(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
    static float Hash(float x, float y) { double n = System.Math.Sin(x * 127.1 + y * 311.7) * 43758.5453; return (float)(n - System.Math.Floor(n)); }
    static float Noise(float x, float y)
    {
        float xi = Mathf.Floor(x), yi = Mathf.Floor(y), xf = x - xi, yf = y - yi;
        float a = xf * xf * (3 - 2 * xf), b = yf * yf * (3 - 2 * yf);
        return (Hash(xi, yi) * (1 - a) + Hash(xi + 1, yi) * a) * (1 - b) + (Hash(xi, yi + 1) * (1 - a) + Hash(xi + 1, yi + 1) * a) * b;
    }
    static float Fbm(float x, float y) { float s = 0, m = 0.5f, f = 1; for (int i = 0; i < 4; i++) { s += Noise(x * f, y * f) * m; f *= 2.07f; m *= 0.5f; } return s; }

    static readonly (float, Color)[] Dawn = {
        (0.00f, Vivid(0xffd046, 1.75f)), (0.20f, Vivid(0xff9016, 1.75f)), (0.40f, Vivid(0xf83a1c, 1.75f)),
        (0.62f, Vivid(0xd0117a, 1.75f)), (0.82f, Vivid(0x5c1a96, 1.75f)), (1.00f, Vivid(0x120c4e, 1.75f)) };
    static Color Ramp((float, Color)[] stops, float t)
    {
        t = Mathf.Clamp01(t); int i = 0; while (i < stops.Length - 2 && t > stops[i + 1].Item1) i++;
        var a = stops[i]; var b = stops[i + 1];
        return Color.Lerp(a.Item2, b.Item2, (t - a.Item1) / Mathf.Max(1e-6f, b.Item1 - a.Item1));
    }

    // the paint: deck scheme, then the pour over both faces, then the stringer on the deck
    static Color Paint(float u, float v, bool top)
    {
        const float sw = 0.030f, ra = 0.86f; float s = Mathf.Abs(v);
        Color c;
        if (top)
        {
            c = Vivid(0xff8a3d, 1.35f);
            if (s < sw) c = Vivid(0xf7ead0, 1.3f);
            if (s > ra) c = Mix(c, 0xf7ead0, Sm(ra, 1f, s));
        }
        else
        {
            c = Vivid(0xfff0dc, 1.2f);
            if (s < sw) c = Vivid(0xf7ead0, 1.3f);
            if (s > ra - 0.08f && s < ra + 0.02f) c = Mix(c, 0xf7ead0, 1 - Mathf.Abs(s - (ra - 0.03f)) / 0.05f);
            if (s > ra + 0.02f) c = Mix(c, 0xf7ead0, Sm(ra + 0.02f, 1f, s));
        }
        // DAWN PATROL, poured rather than sprayed
        float wob = (Fbm(u * 3.2f, v * 1.8f + 4) - 0.5f) * 0.09f;
        c = Ramp(Dawn, u + wob);
        if (!top) c = Mix(c, 0xfff3e0, 0.60f);
        float a2 = Mathf.Abs(v);
        if (a2 > 0.79f && a2 < 0.845f) c = Mix(c, 0xf7ead0, 0.88f);
        if (top)
        {
            if (Mathf.Abs(u - 0.662f) < 0.070f && Mathf.Abs(v + 0.250f) < 0.050f) c = Mix(c, 0xf7ead0, 1);   // the 1
            if (Mathf.Abs(u - 0.600f) < 0.017f && Mathf.Abs(v + 0.312f) < 0.038f) c = Mix(c, 0xf7ead0, 1);   // its flag
            if (Mathf.Abs(u - 0.598f) < 0.017f && Mathf.Abs(v - 0.150f) < 0.160f) c = Mix(c, 0xf7ead0, 1);   // the 7's top bar
            if (u > 0.598f && u < 0.732f && Mathf.Abs(v - (0.300f - (u - 0.598f) * 2.05f)) < 0.050f) c = Mix(c, 0xf7ead0, 1);
            if (s < sw) c = Vivid(0xf7ead0, 1.3f);
        }
        return c;
    }

    // ---- the shape ----
    static float Up => NoseA / (NoseA + TailA);
    static float Peak => Mathf.Pow(Up, NoseA) * Mathf.Pow(1 - Up, TailA);
    static float Beta(float u) => Mathf.Pow(Mathf.Max(0, u), NoseA) * Mathf.Pow(Mathf.Max(0, 1 - u), TailA) / Peak;
    static float InvBeta(float t, float lo, float hi) { for (int k = 0; k < 44; k++) { float m = (lo + hi) * 0.5f; if (Beta(m) < t) lo = m; else hi = m; } return (lo + hi) * 0.5f; }
    static float Round(float d, float r) => d >= r ? 1 : Mathf.Sqrt(Mathf.Max(0, 1 - (1 - d / r) * (1 - d / r)));
    static float TipR => Mathf.Min(0.055f, Mathf.Max(0.010f, T * 0.62f / L));
    static float Close(float u) => Round(u, TipR) * Round(1 - u, TipR);
    static float Ends(float u) { float capN = W * NoseW, capT = W * TailW; return (capN > 0 ? Round(u * L, capN) : 1) * (capT > 0 ? Round((1 - u) * L, capT) : 1); }
    static float uN, uT;
    static float Hw(float u) => W * Beta(uN + (uT - uN) * u) * Ends(u);
    static float Th(float u) => Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)), 0.5f) * Close(u);
    static float Rock(float u) => RockN * Mathf.Pow(Mathf.Max(0, (NoseR - u) / NoseR), 2.2f) + RockT * Mathf.Pow(Mathf.Max(0, (u - 1 + TailR) / TailR), 2.2f);

    static Material glass, padMat, finMat;

    /// the whole board as one object: the deck mesh, the pad and the fins, nose toward -z
    public static GameObject Build(string name = "board")
    {
        uN = NoseW > 0 ? InvBeta(NoseW, 0, Up) : 0; uT = TailW > 0 ? InvBeta(TailW, 1, Up) : 1;
        var root = new GameObject(name);
        if (glass == null)
        {
            var sh = Shader.Find("Surf/Glass") ?? Shader.Find("Universal Render Pipeline/Lit");
            glass = new Material(sh);
            padMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); padMat.SetColor("_BaseColor", Vivid(0x2a1d38)); padMat.SetFloat("_Smoothness", 0.22f);
            finMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); finMat.SetColor("_BaseColor", Vivid(0xf7ead0)); finMat.SetFloat("_Smoothness", 0.86f);
        }
        // the loft
        var pos = new List<Vector3>(); var col = new List<Color>(); var uvs = new List<Vector2>(); var idx = new List<int>();
        for (int i = 0; i <= NU; i++)
        {
            float u = i / (float)NU, w = Hw(u), t = Th(u), r = Rock(u);
            for (int j = 0; j < NR; j++)
            {
                float phi = j / (float)NR * Mathf.PI * 2f;
                float v = Mathf.Sin(phi), sAbs = Mathf.Abs(v); bool top = Mathf.Cos(phi) >= 0;
                float prof = Mathf.Pow(Mathf.Max(0, 1 - sAbs * sAbs), top ? DeckPow : HullPow);
                pos.Add(new Vector3(w * v, r + (top ? T * t * prof : -T * HullK * t * prof), (u - 0.5f) * L));
                col.Add(Paint(u, v, top)); uvs.Add(new Vector2(j / (float)NR, u));
            }
        }
        for (int i = 0; i < NU; i++) for (int j = 0; j < NR; j++)
        {
            int j2 = (j + 1) % NR, A = i * NR + j, B = i * NR + j2, C = (i + 1) * NR + j, D = (i + 1) * NR + j2;
            // the web winds A,C,B for a right-handed view; Unity wants the other way round
            idx.Add(A); idx.Add(B); idx.Add(C); idx.Add(B); idx.Add(D); idx.Add(C);
        }
        foreach (var (ring, fwd) in new[] { (0, false), (NU, true) })
        {
            float u = ring / (float)NU, cy = Rock(u), cz = (u - 0.5f) * L;
            for (int j = 0; j < NR; j++)
            {
                int j2 = (j + 1) % NR; float phi = (j + 0.5f) / NR * Mathf.PI * 2f; float cv = Mathf.Sin(phi); bool ctop = Mathf.Cos(phi) >= 0;
                int b = pos.Count; pos.Add(new Vector3(0, cy, cz)); col.Add(Paint(u, cv, ctop)); uvs.Add(new Vector2(phi / (Mathf.PI * 2), u));
                if (fwd) { idx.Add(b); idx.Add(ring * NR + j2); idx.Add(ring * NR + j); } else { idx.Add(b); idx.Add(ring * NR + j); idx.Add(ring * NR + j2); }
            }
        }
        var m = new Mesh { name = "deck" }; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(pos); m.SetColors(col); m.SetUVs(0, uvs); m.SetTriangles(idx, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var deck = new GameObject("deck", typeof(MeshFilter), typeof(MeshRenderer)); deck.transform.SetParent(root.transform, false);
        deck.GetComponent<MeshFilter>().sharedMesh = m; deck.GetComponent<MeshRenderer>().sharedMaterial = glass;

        // the grip pad: a thin skin lying on the deck over the back third, its ends rounded
        {
            const int PU = 34, PV = 18; var pp = new List<Vector3>(); var pi = new List<int>();
            for (int i = 0; i <= PU; i++)
            {
                float f = i / (float)PU, d = Mathf.Min(f, 1 - f) / 0.12f;
                float e = d >= 1 ? 1 : Mathf.Max(0.06f, Mathf.Sqrt(Mathf.Max(0, 1 - (1 - d) * (1 - d))));
                float u = PadU0 + (PadU1 - PadU0) * i / PU, k = PadW * e, w = Hw(u) * k, t = Th(u), r = Rock(u);
                for (int j = 0; j <= PV; j++)
                {
                    float v = -1 + 2f * j / PV, sv = k * v;
                    float prof = Mathf.Pow(Mathf.Max(0, 1 - sv * sv), DeckPow);
                    pp.Add(new Vector3(w * v, r + T * t * prof + 0.010f, (u - 0.5f) * L));
                }
            }
            for (int i = 0; i < PU; i++) for (int j = 0; j < PV; j++)
            {
                int A = i * (PV + 1) + j, B = A + 1, C = A + PV + 1, D = C + 1;
                pi.Add(A); pi.Add(B); pi.Add(C); pi.Add(B); pi.Add(D); pi.Add(C);
            }
            var pm = new Mesh { name = "pad" }; pm.SetVertices(pp); pm.SetTriangles(pi, 0); pm.RecalculateNormals();
            var pad = new GameObject("pad", typeof(MeshFilter), typeof(MeshRenderer)); pad.transform.SetParent(root.transform, false);
            pad.GetComponent<MeshFilter>().sharedMesh = pm; pad.GetComponent<MeshRenderer>().sharedMaterial = padMat;
        }

        // the fins: a raked foil hung off the hull, the side pair canted outward
        foreach (var f in Fins)
        {
            float ct = Mathf.Sign(f.cant) * Mathf.Min(Mathf.Abs(f.cant), FinCantMax);
            var fm = FinMesh(f.c, f.d, f.t, f.rake);
            var fg = new GameObject("fin", typeof(MeshFilter), typeof(MeshRenderer)); fg.transform.SetParent(root.transform, false);
            fg.GetComponent<MeshFilter>().sharedMesh = fm; fg.GetComponent<MeshRenderer>().sharedMaterial = finMat;
            float u = f.z / L + 0.5f, v = Mathf.Clamp(f.x / Mathf.Max(1e-4f, Hw(u)), -0.95f, 0.95f);
            float hullY = Rock(u) - T * HullK * Th(u) * Mathf.Pow(Mathf.Max(0, 1 - v * v), HullPow);
            fg.transform.localPosition = new Vector3(f.x, hullY + 0.004f, f.z);
            // cant leans the tip outward; toe points the leading edge a touch inward
            fg.transform.localRotation = Quaternion.Euler(0, f.x == 0 ? 0 : -Mathf.Sign(f.x) * FinToe * Mathf.Rad2Deg, -ct * Mathf.Rad2Deg);
        }
        return root;
    }

    // the fin outline is two bezier curves from the leading edge down to the raked tip and
    // back up the trailing edge, drawn in the board's length (z) and depth (y) and extruded
    // across x by its thickness
    static Mesh FinMesh(float cL, float d, float ft, float rk)
    {
        var o = new List<Vector2>();
        Vector2 p0 = new Vector2(-cL * 0.5f, 0), p1 = new Vector2(-cL * 0.30f, -d * 0.44f), p2 = new Vector2(-cL * 0.02f + rk, -d * 0.82f), p3 = new Vector2(cL * 0.14f + rk, -d);
        Vector2 q1 = new Vector2(cL * 0.30f + rk * 0.5f, -d * 0.66f), q2 = new Vector2(cL * 0.42f, -d * 0.30f), q3 = new Vector2(cL * 0.5f, 0);
        for (int i = 0; i <= 26; i++) o.Add(Bez(p0, p1, p2, p3, i / 26f));
        for (int i = 1; i <= 26; i++) o.Add(Bez(p3, q1, q2, q3, i / 26f));
        int n = o.Count; var v = new List<Vector3>(); var idx = new List<int>();
        float h = ft * 0.5f;
        // two faces
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? -h : h; int b = v.Count;
            v.Add(new Vector3(x, -d * 0.45f, (p0.x + q3.x) * 0.5f + rk * 0.3f));     // a centre to fan from
            for (int i = 0; i < n; i++) v.Add(new Vector3(x, o[i].y, o[i].x));
            for (int i = 0; i < n; i++)
            {
                int a = b + 1 + i, c = b + 1 + (i + 1) % n;
                if (side == 0) { idx.Add(b); idx.Add(c); idx.Add(a); } else { idx.Add(b); idx.Add(a); idx.Add(c); }
            }
        }
        // the edge wall
        int w0 = v.Count;
        for (int i = 0; i < n; i++) { v.Add(new Vector3(-h, o[i].y, o[i].x)); v.Add(new Vector3(h, o[i].y, o[i].x)); }
        for (int i = 0; i < n; i++)
        {
            int a = w0 + i * 2, b2 = a + 1, c = w0 + ((i + 1) % n) * 2, d2 = c + 1;
            idx.Add(a); idx.Add(c); idx.Add(b2); idx.Add(b2); idx.Add(c); idx.Add(d2);
        }
        var m = new Mesh { name = "fin" }; m.SetVertices(v); m.SetTriangles(idx, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    static Vector2 Bez(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float s = 1 - t; return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d;
    }
}
