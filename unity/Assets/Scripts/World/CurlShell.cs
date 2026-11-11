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
