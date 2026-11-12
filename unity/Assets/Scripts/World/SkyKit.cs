// What the two skies share: the ride's (SkyLife) and the title beach's (BeachSky).
//
// CLOUDS. The web draws each cumulus as a canvas cut-out with one colour on it, tinted per
// cloud by the hour. That reads as a sticker at any distance: there is nothing in it for the
// light to do. Here a cloud is one tile of a baked atlas holding what a lit cloud needs --
// how solid it is, how thick it is front to back, and the slope of its front surface -- and
// Surf/Cumulus lights that every frame from the day cycle: the sun marched through it for
// self-shadow, a silver lining when it is backlit, grey flat bases, the underside burning at
// either end of the day. The atlas is built once at start from a heap of overlapping spheres
// per cloud (summed thickness), cut flat at the base and billowed with noise.
//
// The sky dome gets its own high layer, a veil of cirrus (Surf/Sky HighCloud), read off a
// tiling noise made here.
//
// GULLS. models/bird_rigged.glb with its flap and glide clips crossfaded on a per-bird timer,
// or the web's drawn silhouette with its asymmetric wingbeat when the file is not there.
using System.Collections.Generic;
using UnityEngine;

public static class SkyKit
{
    // ---------------------------------------------------------------- noise
    static float HashU(int x, int y, int s)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)s * 1442695041u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h / 4294967296f;
        }
    }

    static float VNoise(float x, float y, int s)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = HashU(ix, iy, s), b = HashU(ix + 1, iy, s), c = HashU(ix, iy + 1, s), d = HashU(ix + 1, iy + 1, s);
        float ab = a + (b - a) * fx, cd = c + (d - c) * fx;
        return ab + (cd - ab) * fy;
    }

    // value noise whose lattice wraps every p cells, so the texture made of it tiles
    static float VNoiseP(float x, float y, int p, int s)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int x0 = ((ix % p) + p) % p, x1 = (((ix + 1) % p) + p) % p, y0 = ((iy % p) + p) % p, y1 = (((iy + 1) % p) + p) % p;
        float a = HashU(x0, y0, s), b = HashU(x1, y0, s), c = HashU(x0, y1, s), d = HashU(x1, y1, s);
        float ab = a + (b - a) * fx, cd = c + (d - c) * fx;
        return ab + (cd - ab) * fy;
    }

    static float Fbm(float x, float y, int s, int oct)
    {
        float v = 0f, a = 0.5f, n = 0f;
        for (int k = 0; k < oct; k++) { v += a * VNoise(x, y, s + k * 17); n += a; x = x * 2.03f + 11.7f; y = y * 2.03f + 5.3f; a *= 0.5f; }
        return v / n;
    }

    static float SStep(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    /// a small xorshift, so a shape comes out the same on every device
    class Rng
    {
        uint st;
        public Rng(int seed) { unchecked { st = (uint)seed * 2654435761u + 1013904223u; } if (st == 0) st = 1; }
        public float Next() { st ^= st << 13; st ^= st >> 17; st ^= st << 5; return st / 4294967296f; }
    }

    // ---------------------------------------------------------------- the cumulus atlas
    public const int TileW = 256, TileH = 128, Cols = 2, Rows = 4, Shapes = Cols * Rows;
    const int AtlasW = TileW * Cols, AtlasH = TileH * Rows;
    static Texture2D atlas;

    /// eight cumulus, two across and four down; R solid, G thick, B and A the front surface's slope
    public static Texture2D CloudAtlas()
    {
        if (atlas != null) return atlas;
        var px = new Color32[AtlasW * AtlasH];
        for (int k = 0; k < Shapes; k++)
        {
            var s = Shape(k + 1);
            int ox = (k % Cols) * TileW, oy = (k / Cols) * TileH;
            for (int y = 0; y < TileH; y++)
                System.Array.Copy(s, y * TileW, px, (oy + y) * AtlasW + ox, TileW);
        }
        // linear: these are numbers for the shader, not colours
        atlas = new Texture2D(AtlasW, AtlasH, TextureFormat.RGBA32, true, true)
        { name = "cumulus atlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 0 };
        atlas.SetPixels32(px); atlas.Apply(true, true);
        return atlas;
    }

    // One cumulus: a heap of overlapping spheres whose summed thickness is the cloud. The body is
    // big spheres filling the space under a profile of one or two towers, the crowns are medium
    // puffs standing proud of that profile (which is what makes the cauliflower edge), and a row
    // of wide lumps makes the base, which is then cut flat. Noise billows the rim.
    static Color32[] Shape(int seed)
    {
        var rng = new Rng(seed); System.Func<float> R = rng.Next;
        int kind = seed % 4;
        const float bas = 24f;
        float cw = TileW * (0.64f + R() * 0.12f);
        float hk = kind == 0 ? 0.30f : kind == 1 ? 0.40f : kind == 2 ? 0.46f : 0.24f;
        float ch = Mathf.Min(cw * hk, TileH - bas - 12f);
        float cx = TileW / 2f + (R() - 0.5f) * 12f;
        var tw = new List<Vector3>();   // (x, height, half width)
        if (kind == 2) { float o = cw * (0.15f + R() * 0.07f); tw.Add(new Vector3(cx - o, ch, cw * 0.30f)); tw.Add(new Vector3(cx + o, ch * (0.66f + R() * 0.2f), cw * 0.28f)); }
        else tw.Add(new Vector3(cx + (R() - 0.5f) * cw * 0.20f, ch, cw * (0.44f + R() * 0.08f)));
        tw.Add(new Vector3(cx, ch * 0.42f, cw * 0.52f));        // the shoulders, so the ends are not bare
        System.Func<float, float> prof = x =>
        {
            float m = 0f;
            foreach (var t in tw) { float q = (x - t.x) / t.z; m = Mathf.Max(m, t.y * Mathf.Pow(Mathf.Max(0f, 1f - q * q), 0.55f)); }
            return m;
        };
        var S = new List<Vector3>();    // (x, y, r)
        for (int i = 0; i < 26; i++)
        {
            float u = (R() + R()) / 2f - 0.5f, x = cx + u * cw * 0.85f;
            float top = prof(x); if (top < 6f) continue;
            float r = Mathf.Max(8f, Mathf.Min(top * 0.62f, cw * (0.10f + 0.05f * R())));
            float y = bas + R() * Mathf.Max(0f, top - r * 1.05f);
            S.Add(new Vector3(x, y, r));
        }
        int nc = 18 + (int)(R() * 8f);
        for (int i = 0; i < nc; i++)
        {
            float x = cx + (R() - 0.5f) * cw * 0.92f, top = prof(x); if (top < 5f) continue;
            float r = Mathf.Max(7f, cw * (0.04f + 0.045f * R()) * (0.6f + 0.4f * top / ch));
            S.Add(new Vector3(x, bas + top - r * (0.70f + 0.25f * R()), r));
        }
        int nb = 6 + (int)(R() * 3f);
        for (int k = 0; k < nb; k++)
        {
            float t = (k + 0.5f) / nb * 2f - 1f, x = cx + t * cw * 0.40f + (R() - 0.5f) * 6f;
            float r = cw * (0.08f + 0.03f * R()) * (1f - 0.30f * Mathf.Abs(t));
            S.Add(new Vector3(x, bas + r * 0.25f, r));
        }
        const int N = TileW * TileH;
        var T = new float[N];
        // each sphere adds its own thickness, over its own box only
        foreach (var s in S)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(s.x - s.z)), x1 = Mathf.Min(TileW - 1, Mathf.CeilToInt(s.x + s.z));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(s.y - s.z)), y1 = Mathf.Min(TileH - 1, Mathf.CeilToInt(s.y + s.z));
            float r2 = s.z * s.z;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - s.x, dy = y + 0.5f - s.y, q = 1f - (dx * dx + dy * dy) / r2;
                if (q > 0f) T[y * TileW + x] += s.z * Mathf.Sqrt(q);
            }
        }
        int sd = seed * 7 + 3;
        for (int y = 0; y < TileH; y++)
        for (int x = 0; x < TileW; x++)
        {
            int i = y * TileW + x;
            // the flat base, soft and a little ragged: a knife-straight cut drew a hard line across
            // whatever cloud stood behind it
            float jb = (Fbm(x / 9f, 3.7f, sd + 90, 2) - 0.5f) * 7f;
            float t = T[i] * SStep(bas - 5f + jb, bas + 13f + jb, y + 0.5f);
            if (t > 0f)
            {
                float b1 = 1f - Mathf.Abs(2f * Fbm(x / 11f, y / 11f, sd, 3) - 1f);   // rounded lumps
                float n2 = Fbm(x / 4.5f, y / 4.5f, sd + 50, 2);
                float edge = 1f - SStep(8f, 30f, t);
                t = t * (0.86f + 0.22f * b1) + (b1 - 0.6f) * 7f * edge + (n2 - 0.5f) * 6f * edge;
                t = Mathf.Max(0f, t);
            }
            T[i] = t;
        }
        // the slope off a blurred copy: the big shapes, not every sphere's rim
        var Ts = Blur(Blur(T, 2), 2);
        var outPx = new Color32[N];
        for (int y = 0; y < TileH; y++)
        for (int x = 0; x < TileW; x++)
        {
            int i = y * TileW + x; float t = T[i];
            float a = SStep(2.5f, 13f, t), g = Mathf.Min(1f, t / 70f);
            float xl = Ts[y * TileW + Mathf.Max(0, x - 1)], xr = Ts[y * TileW + Mathf.Min(TileW - 1, x + 1)];
            float yd = Ts[Mathf.Max(0, y - 1) * TileW + x], yu = Ts[Mathf.Min(TileH - 1, y + 1) * TileW + x];
            var n = new Vector3(-(xr - xl) * 0.5f * 0.55f, -(yu - yd) * 0.5f * 0.55f, 1f).normalized;
            outPx[i] = new Color32(B(a), B(g), B(n.x * 0.5f + 0.5f), B(n.y * 0.5f + 0.5f));
        }
        return outPx;
    }

    static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

    static float[] Blur(float[] T, int r)
    {
        var o = new float[T.Length]; var o2 = new float[T.Length];
        for (int y = 0; y < TileH; y++)
        for (int x = 0; x < TileW; x++)
        {
            float s = 0f; int n = 0;
            for (int i = -r; i <= r; i++) { int xx = x + i; if (xx < 0 || xx >= TileW) continue; s += T[y * TileW + xx]; n++; }
            o[y * TileW + x] = s / n;
        }
        for (int y = 0; y < TileH; y++)
        for (int x = 0; x < TileW; x++)
        {
            float s = 0f; int n = 0;
            for (int i = -r; i <= r; i++) { int yy = y + i; if (yy < 0 || yy >= TileH) continue; s += o[yy * TileW + x]; n++; }
            o2[y * TileW + x] = s / n;
        }
        return o2;
    }

    // ---------------------------------------------------------------- the high cloud's noise
    static Texture2D skyNoise;

    /// 256 square and tiling: R five octaves of value noise, G three coarser ones for the warp
    public static Texture2D SkyNoise()
    {
        if (skyNoise != null) return skyNoise;
        const int N = 256;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float v = 0f, a = 0.5f, n = 0f, f = 4f / N; int p = 4;
            for (int k = 0; k < 5; k++) { v += a * VNoiseP(x * f, y * f, p, 91 + k); n += a; a *= 0.5f; p *= 2; f *= 2f; }
            float w = 0f, n2 = 0f; a = 0.5f; p = 8; f = 8f / N;
            for (int k = 0; k < 3; k++) { w += a * VNoiseP(x * f, y * f, p, 301 + k); n2 += a; a *= 0.5f; p *= 2; f *= 2f; }
            px[y * N + x] = new Color32(B(v / n), B(w / n2), 0, 255);
        }
        skyNoise = new Texture2D(N, N, TextureFormat.RGBA32, true, true)
        { name = "high cloud noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        skyNoise.SetPixels32(px); skyNoise.Apply(true, true);
        return skyNoise;
    }

    // ---------------------------------------------------------------- cloud sprites
    static Material cloudMat;

    /// the one material every cumulus in the game shares; LightClouds sets it each frame
    public static Material CloudMaterial()
    {
        if (cloudMat != null) return cloudMat;
        var sh = Shader.Find("Surf/Cumulus");
        if (sh == null) return null;
        cloudMat = new Material(sh) { name = "cumulus" };
        cloudMat.SetTexture("_Atlas", CloudAtlas());
        cloudMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 20;   // after the opaque world, which covers it by depth
        return cloudMat;
    }

    /// A cloud: a quad w across and w/2 high (the web's PlaneGeometry(w, w*0.5)) centred on its
    /// object, mapped onto one atlas tile. Opacity rides in the vertex colour so a fade is a
    /// colour write and every cloud can share the one material.
    public static GameObject Cloud(Transform parent, float w, int shape, float opacity, string name = "cloud")
    {
        var mat = CloudMaterial();
        if (mat == null) return null;
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        shape = ((shape % Shapes) + Shapes) % Shapes;
        float us = 1f / Cols, vs = 1f / Rows, u0 = (shape % Cols) * us, v0 = (shape / Cols) * vs;
        float hw = w * 0.5f, hh = w * 0.25f;
        var m = new Mesh { name = "cumulus" };
        m.vertices = new[] { new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0), new Vector3(-hw, hh, 0), new Vector3(hw, hh, 0) };
        m.uv = new[] { new Vector2(u0, v0), new Vector2(u0 + us, v0), new Vector2(u0, v0 + vs), new Vector2(u0 + us, v0 + vs) };
        var tile = new Vector4(u0, v0, us, vs);
        m.SetUVs(1, new List<Vector4> { tile, tile, tile, tile });
        var c = new Color(1f, 1f, 1f, opacity);
        m.colors = new[] { c, c, c, c };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = m;
        return go;
    }

    /// set a cloud's opacity (the fade at the ends of its drift)
    public static void SetOpacity(GameObject cloud, float a)
    {
        if (cloud == null) return;
        var mf = cloud.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return;
        var c = new Color(1f, 1f, 1f, a);
        mf.sharedMesh.colors = new[] { c, c, c, c };
    }

    static DayCycle day;

    /// the clouds take the light the day cycle worked out for them, every frame, so they turn
    /// with the sky: one call does every cumulus in the game, since they share the material
    public static void LightClouds()
    {
        var m = CloudMaterial(); if (m == null) return;
        if (day == null) day = Object.FindFirstObjectByType<DayCycle>();
        if (day == null) return;
        m.SetVector("_SunDir", day.cloudKey);
        m.SetVector("_SunCol", day.cloudSun);
        m.SetVector("_Top", day.skyTop); m.SetVector("_Mid", day.skyMid); m.SetVector("_Hz", day.skyHz);
        m.SetVector("_Under", day.cloudUnder);
        m.SetFloat("_Light", day.cloudLight);
    }

    // ---------------------------------------------------------------- gulls
    public class Gull
    {
        public Transform t; public GltfClips clips;          // the modelled gull
        public Transform wingL, wingR;                        // or the drawn one
        public Renderer[] rends;
        public float f, p, up, dn, dwK;                       // the drawn beat
        public bool glide; public float gT, wF, flapRate, glideRate;
        public bool shown = true;
    }

    static Material silhouette;

    /// One gull of span `span` feet (times sz), parented under `parent`. The flap rate is the
    /// flock's (`beat`), each bird taking it with a few per cent of jitter, as the web does.
    public static Gull MakeGull(Transform parent, float span, float sz, float beat, float yaw, System.Func<float> R)
    {
        var b = new Gull();
        var prefab = Resources.Load<GameObject>("Models/bird_rigged");
        if (prefab != null)
        {
            var go = Models.Spawn("bird_rigged", parent, 1f, PrimitiveType.Sphere);
            var bb = Models.Bounds(go);
            float s = Mathf.Max(1e-3f, Mathf.Max(bb.size.x, bb.size.z));
            go.transform.localScale = go.transform.localScale * (span / s * sz);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var rr in go.GetComponentsInChildren<Renderer>(true)) { rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false; }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            b.t = go.transform;
            b.clips = GltfClips.Start(go.transform, "bird_rigged", new[] { "flap", "glide" });
            if (b.clips != null)
            {
                b.flapRate = beat * (0.94f + R() * 0.12f);
                b.glideRate = 0.5f + R() * 0.4f;
                b.clips.SetSpeed(0, b.flapRate); b.clips.SetSpeed(1, b.glideRate);
                b.clips.SetTime(0, R() * b.clips.Length(0)); b.clips.SetTime(1, R() * b.clips.Length(1));
            }
        }
        else
        {
            if (silhouette == null)
            {
                var ush = Shader.Find("Universal Render Pipeline/Unlit");
                silhouette = ush != null ? new Material(ush) : null;
                if (silhouette != null) { silhouette.SetColor("_BaseColor", new Color(0.93f, 0.95f, 0.98f)); silhouette.SetFloat("_Cull", 0f); }
            }
            b.t = DrawnGull(parent, silhouette, out b.wingL, out b.wingR);
            b.t.localScale = Vector3.one * sz;
        }
        b.rends = b.t.GetComponentsInChildren<Renderer>(true);
        // the drawn beat's own numbers, per bird (used when there is no clip to play)
        b.f = 10f + R() * 8.5f; b.p = R() * Mathf.PI * 2f;
        b.up = 0.92f + R() * 0.42f; b.dn = -0.52f - R() * 0.34f; b.dwK = 0.82f + R() * 0.40f;
        b.glide = R() < 0.35f; b.gT = 1.5f + R() * 4.5f; b.wF = b.glide ? 0f : 1f;
        if (b.clips != null) { b.clips.SetWeight(0, b.wF); b.clips.SetWeight(1, 1f - b.wF); }
        return b;
    }

    /// a new flock rate for a bird moved into another flock (its clip keeps its own phase)
    public static void Rebeat(Gull b, float beat, System.Func<float> R)
    {
        if (b.clips == null || !b.clips.Valid) return;
        b.flapRate = beat * (0.94f + R() * 0.12f);
        b.clips.SetSpeed(0, b.flapRate);
    }

    /// shown or put away: a hidden gull neither draws nor animates
    public static void Show(Gull b, bool on)
    {
        if (b == null || b.shown == on) return;
        b.shown = on;
        if (b.rends != null) foreach (var r in b.rends) if (r != null) r.enabled = on;
        if (b.clips != null && b.clips.Valid) b.clips.Pause(!on);
    }

    /// The web's layFlight: a skein (pairs dropping back off a leader, odd and at least five),
    /// a loose knot, or a lone bird, in the flock's own frame: it flies along +x, so behind is -x
    /// and a V opens backwards in z.
    public static Vector3 FlightSpot(int i, int n, bool vee, System.Func<float> R)
    {
        if (vee)
        {
            int r = (i + 1) / 2; float sd = (i % 2) == 1 ? 1f : -1f;
            return new Vector3(-r * (2.6f + R() * 0.42f), (R() - 0.5f) * 0.6f, r > 0 ? sd * r * (2.0f + R() * 0.3f) : 0f);
        }
        if (n == 1) return Vector3.zero;
        return new Vector3((R() - 0.5f) * 7f, (R() - 0.5f) * 2.4f, (R() - 0.5f) * 7f);
    }

    /// One frame of a gull's wings. It glides most of the time and flaps in bursts; the change of
    /// habit is a crossfade between the clips (or the drawn beat shrinking to a tremor).
    public static void Beat(Gull b, float t, float dt, System.Func<float> R)
    {
        if (b == null || !b.shown) return;
        b.gT -= dt;
        if (b.gT <= 0f) { b.glide = !b.glide; b.gT = (b.glide ? 1.2f : 2.2f) + R() * 4.5f; }
        b.wF = Mathf.MoveTowards(b.wF, b.glide ? 0f : 1f, dt / 0.55f);
        if (b.clips != null && b.clips.Valid)
        {
            b.clips.SetWeight(0, b.wF);
            if (b.clips.Count > 1) b.clips.SetWeight(1, 1f - b.wF);
        }
        else if (b.wingL != null && b.wingR != null)
        {
            float w = Mathf.Repeat((t * b.f + b.p) / (Mathf.PI * 2f), 1f);
            // positive about x drops a tip here, and the web's angle is positive raised
            float a = -GullAngle(w, b) * Mathf.Lerp(0.12f, 1f, b.wF) * Mathf.Rad2Deg;
            b.wingL.localRotation = Quaternion.Euler(a, 0, 0);
            b.wingR.localRotation = Quaternion.Euler(a, 180f, 0);
        }
    }

    // the web's gullAngle: the downstroke is the fast half, the recovery takes half again as long
    static float GullAngle(float w, Gull u)
    {
        float dw = Mathf.Clamp(0.38f * u.dwK, 0.12f, 0.88f);
        return w < dw
            ? u.up - (u.up - u.dn) * (0.5f - 0.5f * Mathf.Cos(Mathf.PI * (w / dw)))
            : u.dn + (u.up - u.dn) * (0.5f - 0.5f * Mathf.Cos(Mathf.PI * ((w - dw) / (1f - dw))));
    }

    /// the web's drawn gull: two wing triangles on a flat body, hinged at the shoulders
    static Transform DrawnGull(Transform parent, Material mat, out Transform wingL, out Transform wingR)
    {
        var g = new GameObject("gull").transform; g.SetParent(parent, false);
        var wing = new Mesh { name = "wing" };
        // the root at the shoulder, the tip 1.5 feet out along +z, swept back along -x
        wing.vertices = new[] { new Vector3(0, 0, 0), new Vector3(-0.42f, 0, 0.05f), new Vector3(-0.55f, 0, 1.5f) };
        wing.triangles = new[] { 0, 1, 2 };
        wing.RecalculateNormals(); wing.RecalculateBounds();
        wingL = Part(g, "wingL", wing, mat, Quaternion.identity);
        wingR = Part(g, "wingR", wing, mat, Quaternion.Euler(0, 180f, 0));
        var body = new Mesh { name = "body" };
        body.vertices = new[] { new Vector3(0.28f, 0, 0), new Vector3(-0.2f, 0, 0.14f), new Vector3(-0.2f, 0, -0.14f) };
        body.triangles = new[] { 0, 1, 2 };
        body.RecalculateNormals(); body.RecalculateBounds();
        Part(g, "body", body, mat, Quaternion.identity);
        return g;
    }

    static Transform Part(Transform parent, string n, Mesh m, Material mat, Quaternion rot)
    {
        var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = m;
        var mr = go.GetComponent<MeshRenderer>(); if (mat != null) mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        go.transform.localRotation = rot;
        return go.transform;
    }
}
