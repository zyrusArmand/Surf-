// The curling lip of the set wave: the web game's shell, number for number. A ledge thrown
// off the crest that arcs over the pocket, thins toward its tip and falls a curtain into the
// water, with a skirt below the weld so the swell never opens daylight under it. Built once
// in the shell's own frame (local +x is "forward off the crest", the side flips with the
// transform's x scale) and placed each frame by SetWave.
using UnityEngine;

public static class CurlShell
{
    public const float R = 7.0f;          // the radius the lip throws through
    public const float Alpha = 1.42f;     // matched to the face's own slope at the crest
    public const float Sweep = 4.05f;     // how far it carries over before it falls
    public const float LipT = 0.22f;      // slab thickness, a fraction of the throw radius
    public const float Fall = 1.30f;      // how far the curtain falls off the tip, in radii
    public const int NZ = 104, NA = 64;
    public const float Z0 = -124f, Z1 = 34f;
    const float Skirt = 3.2f, SK = 0.05f, Cap = 0.12f;
    const float Span = (1.0f - 0.12f - 2.0f * SK) / 2.0f;

    struct Arc { public float f, y, nf, ny, tf, ty; }

    static Arc ArcAt(float th)
    {
        float sa = Mathf.Sin(Alpha), ca = Mathf.Cos(Alpha), p = Alpha - th;
        return new Arc { f = sa - Mathf.Sin(p), y = Mathf.Cos(p) - ca, nf = Mathf.Sin(p), ny = -Mathf.Cos(p), tf = Mathf.Cos(p), ty = Mathf.Sin(p) };
    }
    static float Thick(float q) => LipT * Mathf.Min(1f, q / 0.26f) * (1 - 0.34f * q);

    public static Mesh Build()
    {
        int nv = (NZ + 1) * (NA + 1);
        var pos = new Vector3[nv]; var uv = new Vector2[nv]; var uv2 = new Vector2[nv];
        var idx = new int[NZ * NA * 6];
        int n = 0;
        for (int i = 0; i <= NZ; i++)
        {
            float zt = i / (float)NZ, z = Z0 + (Z1 - Z0) * zt;
            float pa = WaveField.PeelA(z), pb = WaveField.PeelB(z);
            float rad = R * (0.16f + 0.84f * pa) * (1 - 0.55f * pb);
            float swp = Sweep * (0.30f + 0.70f * pa);
            float fold = 1 - 0.38f * pb;
            float drop = -WaveField.SwPeak * 0.55f * pb / Mathf.Max(0.05f, fold);
            float FALL = Fall * pa, QA = 1f / (1f + FALL * 0.85f);
            Arc Seg(float q)
            {
                if (FALL <= 1e-4f || q <= QA) return ArcAt(swp * Mathf.Min(1f, q / QA));
                var a = ArcAt(swp); float v = (q - QA) / (1 - QA), w = Mathf.Min(1f, v / 0.35f);
                float nf = a.nf * (1 - w) - w, ny = a.ny * (1 - w), nl = Mathf.Max(1e-6f, Mathf.Sqrt(nf * nf + ny * ny));
                float tf = a.tf * (1 - w), ty = a.ty * (1 - w) - w, tl = Mathf.Max(1e-6f, Mathf.Sqrt(tf * tf + ty * ty));
                return new Arc { f = a.f, y = a.y - FALL * v, nf = nf / nl, ny = ny / nl, tf = tf / tl, ty = ty / tl };
            }
            float FallFrac(float q) => (FALL <= 1e-4f || q <= QA) ? 0f : (q - QA) / (1 - QA);
            float gu = Mathf.Clamp01((pa - 0.25f) / 0.30f), gate = gu * gu * (3 - 2 * gu);
            for (int j = 0; j <= NA; j++)
            {
                float s = j / (float)NA, q, f, y, fv = 0f;
                if (s < SK) { q = 0; f = 0; y = -Skirt * (1 - s / SK) * fold; }
                else if (s <= SK + Span)
                {
                    q = (s - SK) / Span; var a = Seg(q);
                    f = a.f * rad; y = a.y * rad * fold; fv = FallFrac(q);
                }
                else if (s < SK + Span + Cap)
                {
                    q = 1; fv = 1;
                    var a = Seg(1); float T = Thick(1) * rad, ph = Mathf.PI * (s - SK - Span) / Cap;
                    float cf = a.f * rad - a.nf * T * 0.5f, cy = a.y * rad - a.ny * T * 0.5f;
                    f = cf + (T * 0.5f) * (Mathf.Cos(ph) * a.nf + Mathf.Sin(ph) * a.tf);
                    y = (cy + (T * 0.5f) * (Mathf.Cos(ph) * a.ny + Mathf.Sin(ph) * a.ty)) * fold;
                }
                else if (s <= SK + Span + Cap + Span)
                {
                    q = 1 - (s - SK - Span - Cap) / Span;
                    var a = Seg(q); float T = Thick(q) * rad;
                    f = a.f * rad - a.nf * T; y = (a.y * rad - a.ny * T) * fold; fv = FallFrac(q);
                }
                else { q = 0; f = 0; y = -Skirt * ((s - SK - Span - Cap - Span) / SK) * fold; }
                int k = i * (NA + 1) + j;
                pos[k] = new Vector3(f, y + drop, z); uv[k] = new Vector2(q, zt); uv2[k] = new Vector2(fv * gate, 0);
            }
        }
        for (int i = 0; i < NZ; i++) for (int j = 0; j < NA; j++)
        {
            int a = i * (NA + 1) + j, b = a + NA + 1;
            idx[n++] = a; idx[n++] = b; idx[n++] = a + 1; idx[n++] = a + 1; idx[n++] = b; idx[n++] = b + 1;
        }
        var m = new Mesh { name = "curl", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.vertices = pos; m.uv = uv; m.uv2 = uv2; m.triangles = idx;
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // ---- the foam bank: puffs on the tube's own cross-section, filling the open sector from the
    // curtain's tip round the bottom, climbing as the section nears the break so the O closes
    // just behind the rider and the far mouth stays open (the web's buildFoamGeom) ----
    static float H(float a, float b) { float x = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }

    public static Mesh BuildFoam()
    {
        const int M = 7, NZf = 110;
        float sa = Mathf.Sin(Alpha), ca = Mathf.Cos(Alpha);
        var pos = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var uv2 = new System.Collections.Generic.List<Vector2>();
        var idx = new System.Collections.Generic.List<int>();
        for (int i = 0; i <= NZf; i++)
        {
            float zt = i / (float)NZf, z = Z0 + (Z1 - Z0) * zt;
            float pa = WaveField.PeelA(z), pb = WaveField.PeelB(z);
            float rad = R * (0.16f + 0.84f * pa) * (1 - 0.55f * pb);
            float swp = Sweep * (0.30f + 0.70f * pa);
            float fold = 1 - 0.38f * pb;
            float drop = -WaveField.SwPeak * 0.55f * pb / Mathf.Max(0.05f, fold);
            float thB = Alpha + Mathf.PI * 0.80f;
            float gate = Mathf.Clamp01((pa - 0.22f) / 0.30f);
            float cu = Mathf.Clamp01((z + 18f) / 20f), climb = cu * cu * (3 - 2 * cu);
            for (int k = 0; k < M; k++)
            {
                float tt = 0.16f + 0.84f * k / (M - 1);
                float keep = Mathf.Clamp01((tt - (1 - climb - 0.24f)) / 0.30f);
                float R0 = rad * (0.16f + 0.20f * pa) * (1 + 1.0f * pb) * gate * keep * (0.55f + 0.45f * Mathf.Sin(Mathf.PI * tt));
                if (R0 < 0.30f) continue;
                float th = swp + (thB - swp) * tt, pp = Alpha - th;
                float cf = (sa - Mathf.Sin(pp)) * rad, cy = (Mathf.Cos(pp) - ca) * rad;
                float dx = sa * rad - cf, dy = -ca * rad - cy, L = Mathf.Max(1e-6f, Mathf.Sqrt(dx * dx + dy * dy));
                cf += dx / L * R0 * 0.45f; cy += dy / L * R0 * 0.45f;
                var c = new Vector3(cf + (H(i, k) - 0.5f) * R0 * (0.8f + 1.1f * climb), cy + drop + (H(k, i) - 0.5f) * R0 * 0.5f, z + (H(i * 3, k * 5) - 0.5f) * 1.2f);
                float size = R0 * (1.15f + 0.55f * H(i * 1.3f, k * 2.7f)) * (1 + 0.35f * climb);
                float seed = H(i * 7.1f, k * 9.3f);
                int b0 = pos.Count;
                pos.Add(c); pos.Add(c); pos.Add(c); pos.Add(c);
                var sz = new Vector2(size, seed); uv.Add(sz); uv.Add(sz); uv.Add(sz); uv.Add(sz);
                uv2.Add(new Vector2(-1, -1)); uv2.Add(new Vector2(1, -1)); uv2.Add(new Vector2(1, 1)); uv2.Add(new Vector2(-1, 1));
                idx.Add(b0); idx.Add(b0 + 1); idx.Add(b0 + 2); idx.Add(b0); idx.Add(b0 + 2); idx.Add(b0 + 3);
            }
        }
        var m = new Mesh { name = "foambank", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(pos); m.SetUVs(0, uv); m.SetUVs(1, uv2); m.SetTriangles(idx, 0);
        m.bounds = new Bounds(new Vector3(0, 0, (Z0 + Z1) * 0.5f), new Vector3(60f, 60f, Z1 - Z0 + 20f));
        return m;
    }

    public static GameObject MakeFoam(out Material mat)
    {
        var go = new GameObject("FoamBank", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = BuildFoam();
        var sh = Shader.Find("Surf/FoamBank");
        mat = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit"));
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        go.SetActive(false);
        return go;
    }

    /// the shell, drawn with Surf/Curl, hidden until the set wave stands it up
    public static GameObject Make(out Material mat)
    {
        var go = new GameObject("Curl", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = Build();
        var sh = Shader.Find("Surf/Curl");
        mat = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit"));
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        go.SetActive(false);
        return go;
    }
}
