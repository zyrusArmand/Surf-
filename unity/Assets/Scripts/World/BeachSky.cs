// Sky furniture over the title screen's beach: the web game's clouds and gulls.
//
// CLOUDS are the web's sixteen, laid out by its own rule (sizes, heights, distances, drift),
// but each is a lit cumulus from SkyKit's atlas rather than a flat cut-out: billboarded to the
// lens and lit by the title's sun and sky every frame (Surf/Cumulus). The web's cloudTexture()
// is still here, pixel for pixel, for the shop's preview stage that shows one.
//
// GULLS are models/bird_rigged.glb, which carries a flap and a glide clip on its skeleton;
// each bird blends between the two on its own timer, the way the web's beatGull does, and
// the flocks (a V, a loose bunch, a solo) circle slowly out over the sea. Without the file,
// a drawn silhouette flies the same paths with the web's own asymmetric wingbeat. On top of
// those, now and then a stray -- one, two or three gulls -- crosses the whole view at its own
// height and pace, so the sky is never the same four flocks going round.
using System.Collections.Generic;
using UnityEngine;

public class BeachSky
{
    class Cloud { public Transform t; public float v; }
    class Flock
    {
        public Transform t; public readonly List<SkyKit.Gull> birds = new List<SkyKit.Gull>();
        public Vector3 c; public float rx, rz, w, th, bob, dir;
    }
    // a stray crossing the view: its birds come out of the spare pool and go back when it is done
    class Stray
    {
        public Transform t; public readonly List<SkyKit.Gull> birds = new List<SkyKit.Gull>();
        public float v, bob;
    }

    public float birdYaw = 90f;     // the model faces +z; a flock flies along its own +x
    public float birdSpan = 3.0f;   // feet, wingtip to wingtip, before the per-bird and per-flock scales

    readonly List<Cloud> clouds = new List<Cloud>();
    readonly List<Flock> flocks = new List<Flock>();
    readonly List<Stray> strays = new List<Stray>();
    readonly List<SkyKit.Gull> spare = new List<SkyKit.Gull>();
    Transform root, gulls, pool; float t, strayT = 4f;
    System.Random rng;
    float R() => (float)rng.NextDouble();

    const float CloudWrap = 400f;   // the web wraps at 340 in its own units; the measured set is a shade wider

    public void Build(Transform parent, int seed)
    {
        rng = new System.Random(seed);
        root = new GameObject("Sky").transform; root.SetParent(parent, false);
        BuildClouds();
        BuildBirds();
    }

    // ---------------------------------------------------------------- clouds
    // One quad per cloud faced to the lens, lit by Surf/Cumulus; they all share one material, which
    // SkyKit.LightClouds sets from the day cycle (on the title, the title's own sun and sky).
    void BuildClouds()
    {
        if (SkyKit.CloudMaterial() == null) return;
        var holder = new GameObject("Clouds").transform; holder.SetParent(root, false);
        // the web's rule exactly (web 12187-12195): sixteen of them, 34 to 96 feet across, 56 to
        // 140 up and 240 to 460 out. Its opacity (half to nine tenths) was for a soft cut-out; a
        // lit cloud is solid in the core and soft at the rim on its own, so the range sits higher.
        for (int i = 0; i < 16; i++)
        {
            float w = 34f + R() * 62f, op = 0.62f + R() * 0.36f;
            var go = SkyKit.Cloud(holder, w, (int)(R() * SkyKit.Shapes), op);
            if (go == null) return;
            go.transform.position = new Vector3((R() - 0.5f) * 2f * CloudWrap, 56f + R() * 84f, -240f - R() * 220f);
            clouds.Add(new Cloud { t = go.transform, v = 0.9f + R() * 2.1f });
        }
    }

    static Mesh Quad()
    {
        var m = new Mesh { name = "cloud quad" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static float Fbm(float x, float y)
    {
        float s = 0, m = 0.5f, f = 1f;
        for (int i = 0; i < 4; i++) { s += Mathf.PerlinNoise(x * f, y * f) * m; f *= 2.07f; m *= 0.5f; }
        return s;
    }

    /// the web's cloudTexture(seed): the same lumps, the same shade, the same erosion
    public static Texture2D CloudTexture(int seed)
    {
        const int W = 384, H = 192; const float BASE = 132f;
        long n = seed * 9301L + 49297L;
        System.Func<float> rnd = () => { n = (n * 9301L + 49297L) % 233280L; return n / 233280f; };
        var px = new Color[W * H];     // canvas coordinates: row 0 at the top, straight alpha
        int lumps = 9 + (int)(rnd() * 6);
        for (int i = 0; i < lumps; i++)
        {
            float tt = i / (float)(lumps - 1);
            float rad = 24f + rnd() * 20f + 18f * (1f - Mathf.Abs(tt - 0.45f) * 1.6f);
            float cx = 76f + tt * 232f + (rnd() - 0.5f) * 26f;
            float cy = BASE - rad * 0.55f - rnd() * 26f * (1f - Mathf.Abs(tt - 0.5f) * 1.4f);
            float top = Mathf.Round(238f + rnd() * 17f) / 255f;
            // the canvas's two-circle radial gradient: focus a quarter radius above the centre
            float fx = cx, fy = cy - rad * 0.25f, r0 = rad * 0.12f, r1 = rad;
            float ex = cx - fx, ey = cy - fy, dr = r1 - r0;
            float qa = ex * ex + ey * ey - dr * dr;
            int x0 = Mathf.Max(0, (int)(cx - rad) - 1), x1 = Mathf.Min(W - 1, (int)(cx + rad) + 1);
            int y0 = Mathf.Max(0, (int)(cy - rad) - 1), y1 = Mathf.Min(H - 1, (int)(cy + rad) + 1);
            for (int py = y0; py <= y1; py++)
            for (int pxl = x0; pxl <= x1; pxl++)
            {
                float ox = pxl + 0.5f - cx, oy = py + 0.5f - cy;
                if (ox * ox + oy * oy > rad * rad) continue;
                float dx = pxl + 0.5f - fx, dy = py + 0.5f - fy;
                float qb = -2f * (dx * ex + dy * ey + r0 * dr), qc = dx * dx + dy * dy - r0 * r0;
                float g;
                if (Mathf.Abs(qa) < 1e-6f) g = Mathf.Abs(qb) < 1e-6f ? 0f : -qc / qb;
                else
                {
                    float disc = qb * qb - 4f * qa * qc;
                    if (disc < 0f) g = 1f;
                    else
                    {
                        float sq = Mathf.Sqrt(disc);
                        float g1 = (-qb + sq) / (2f * qa), g2 = (-qb - sq) / (2f * qa);
                        g = Mathf.Max(g1, g2);
                        if (r0 + g * dr < 0f) g = Mathf.Min(g1, g2);
                    }
                }
                g = Mathf.Clamp01(g);
                // stops: white 0.95 -> (top, 0.60) -> (214,220,230, 0)
                Color s;
                if (g < 0.5f) { float k = g / 0.5f; s = new Color(Mathf.Lerp(1f, top, k), Mathf.Lerp(1f, top, k), Mathf.Lerp(1f, top, k), Mathf.Lerp(0.95f, 0.60f, k)); }
                else { float k = (g - 0.5f) / 0.5f; s = new Color(Mathf.Lerp(top, 214f / 255f, k), Mathf.Lerp(top, 220f / 255f, k), Mathf.Lerp(top, 230f / 255f, k), Mathf.Lerp(0.60f, 0f, k)); }
                // source-over
                int id = py * W + pxl; var d = px[id];
                float oa = s.a + d.a * (1f - s.a);
                if (oa > 1e-5f)
                {
                    float r = (s.r * s.a + d.r * d.a * (1f - s.a)) / oa, gg = (s.g * s.a + d.g * d.a * (1f - s.a)) / oa, b = (s.b * s.a + d.b * d.a * (1f - s.a)) / oa;
                    px[id] = new Color(r, gg, b, oa);
                }
            }
        }
        // shade the underside (source-atop: colour only, where there is cloud)
        for (int py = 0; py < H; py++)
        {
            float k = Mathf.Clamp01((py - (BASE - 62f)) / 66f);
            float sa = 0.36f * k;
            if (sa <= 0f) continue;
            var sc = Color.Lerp(new Color(150f / 255f, 163f / 255f, 185f / 255f), new Color(138f / 255f, 152f / 255f, 177f / 255f), k);
            for (int pxl = 0; pxl < W; pxl++)
            {
                int id = py * W + pxl; var d = px[id];
                if (d.a <= 0f) continue;
                px[id] = new Color(sc.r * sa + d.r * (1f - sa), sc.g * sa + d.g * (1f - sa), sc.b * sa + d.b * (1f - sa), d.a);
            }
        }
        // and break it up: re-cut the silhouette against noise, lift and drop the body with it
        var outPx = new Color32[W * H];
        for (int py = 0; py < H; py++)
        for (int pxl = 0; pxl < W; pxl++)
        {
            int id = py * W + pxl; var d = px[id]; float a = d.a;
            if (a > 0.004f)
            {
                float nz = Fbm(pxl * 0.055f + seed * 31.7f, py * 0.055f + seed * 11.3f) * 0.62f
                         + Fbm(pxl * 0.155f + seed * 7.1f, py * 0.155f + seed * 5.9f) * 0.38f;
                float th = 0.30f + (nz - 0.5f) * 0.46f;
                float e = Mathf.Clamp01((a - th) / 0.26f);
                a = a * e * e * (3f - 2f * e);
                float lift = 0.84f + 0.32f * nz;
                d = new Color(Mathf.Min(1f, d.r * lift), Mathf.Min(1f, d.g * lift), Mathf.Min(1f, d.b * lift), a);
            }
            else d = new Color(0.9f, 0.92f, 0.95f, 0f);
            // the texture's row 0 is the bottom
            outPx[(H - 1 - py) * W + pxl] = new Color32((byte)Mathf.RoundToInt(d.r * 255f), (byte)Mathf.RoundToInt(d.g * 255f), (byte)Mathf.RoundToInt(d.b * 255f), (byte)Mathf.RoundToInt(d.a * 255f));
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "cloud " + seed, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(outPx); tex.Apply(true, true);
        return tex;
    }

    // ---------------------------------------------------------------- gulls
    // the web's FLIGHTS: a V of five, a loose three, a solo, another V; (the fifth flock, a
    // second solo, is left out to keep the skinned count down)
    static readonly int[] FlightN = { 5, 3, 1, 4 };
    static readonly bool[] FlightV = { true, false, false, true };
    const int SpareN = 6;           // the strays' pool: at most this many crossing at once

    void BuildBirds()
    {
        gulls = new GameObject("Gulls").transform; gulls.SetParent(root, false);
        for (int f = 0; f < FlightN.Length; f++)
        {
            var fl = new Flock();
            var fg = new GameObject("flock " + f).transform; fg.SetParent(gulls, false); fl.t = fg;
            float beat = 1.7f + R() * 2.0f;            // the flock's rate; its birds take it with a little jitter
            for (int i = 0; i < FlightN[f]; i++)
            {
                var b = SkyKit.MakeGull(fg, birdSpan, 0.8f + R() * 0.5f, beat, birdYaw, R);
                b.t.localPosition = SkyKit.FlightSpot(i, FlightN[f], FlightV[f], R);
                fl.birds.Add(b);
            }
            // the web beach's flocks hang 14..60 up and 110..220 out, scattered across 260 of x;
            // here each circles a slow loop about that spot instead of sliding across and wrapping
            fl.c = new Vector3((R() - 0.5f) * 280f, 16f + R() * 52f, -126f - R() * 126f);
            fl.rx = 35f + R() * 45f; fl.rz = 12f + R() * 22f;
            float speed = 4f + R() * 4f;
            fl.w = speed / fl.rx; fl.dir = R() < 0.5f ? -1f : 1f;
            fl.th = R() * Mathf.PI * 2f; fl.bob = R() * Mathf.PI * 2f;
            fg.localScale = Vector3.one * (0.7f + R() * 0.9f);
            flocks.Add(fl);
        }
        PlaceFlocks();
        // the strays' birds, made now (a gull is a skinned model with its own clip graph, too much
        // to build in the middle of a frame) and put away until one is wanted
        pool = new GameObject("spare gulls").transform; pool.SetParent(gulls, false);
        for (int i = 0; i < SpareN; i++)
        {
            var b = SkyKit.MakeGull(pool, birdSpan, 0.8f + R() * 0.5f, 1.7f + R() * 2.0f, birdYaw, R);
            SkyKit.Show(b, false);
            spare.Add(b);
        }
    }

    // A stray: one, two or three gulls in from one side of the bay at a height of their own,
    // straight across and out of the other, in the web beach's band (14 to 60 up, 110 to 220 out)
    // and now and then higher, where a lone bird rides the air with its wings still.
    void LaunchStray()
    {
        float q = R();
        int n = Mathf.Min(spare.Count, q < 0.5f ? 1 : q < 0.8f ? 2 : 3);
        if (n <= 0) return;
        var st = new Stray();
        st.t = new GameObject("stray").transform; st.t.SetParent(gulls, false);
        float dir = R() < 0.5f ? -1f : 1f, beat = 1.7f + R() * 2.0f;
        bool high = n == 1 && R() < 0.4f;
        st.v = dir * (high ? 3f + R() * 3f : 5f + R() * 5f);
        st.bob = R() * Mathf.PI * 2f;
        st.t.position = new Vector3(-dir * 205f, high ? 64f + R() * 50f : 14f + R() * 46f, -110f - R() * 130f);
        st.t.rotation = Quaternion.LookRotation(new Vector3(dir, 0f, 0f), Vector3.up) * Quaternion.Euler(0, -90f, 0);
        st.t.localScale = Vector3.one * (0.7f + R() * 0.9f);
        for (int i = 0; i < n; i++)
        {
            var b = spare[spare.Count - 1]; spare.RemoveAt(spare.Count - 1);
            b.t.SetParent(st.t, false);
            b.t.localPosition = SkyKit.FlightSpot(i, n, false, R);
            SkyKit.Rebeat(b, beat, R);
            if (high) { b.glide = true; b.gT = 3f + R() * 4f; }
            SkyKit.Show(b, true);
            st.birds.Add(b);
        }
        strays.Add(st);
    }

    void StepStrays(float dt)
    {
        strayT -= dt;
        if (strayT <= 0f) { strayT = 5f + R() * 9f; if (strays.Count < 3) LaunchStray(); }
        for (int k = strays.Count - 1; k >= 0; k--)
        {
            var st = strays[k];
            var p = st.t.position; p.x += st.v * dt; p.y += Mathf.Sin(t * 0.7f + st.bob) * dt * 1.5f;
            st.t.position = p;
            foreach (var b in st.birds) SkyKit.Beat(b, t, dt, R);
            if (Mathf.Abs(p.x) > 210f)
            {
                foreach (var b in st.birds) { SkyKit.Show(b, false); b.t.SetParent(pool, false); spare.Add(b); }
                Object.Destroy(st.t.gameObject);
                strays.RemoveAt(k);
            }
        }
    }

    void PlaceFlocks()
    {
        foreach (var fl in flocks)
        {
            float cs = Mathf.Cos(fl.th), sn = Mathf.Sin(fl.th);
            var p = fl.c + new Vector3(fl.rx * cs, Mathf.Sin(t * 0.7f + fl.bob) * 1.5f, fl.rz * sn);
            var d = new Vector3(-fl.rx * sn, 0, fl.rz * cs) * fl.dir;
            fl.t.position = p;
            if (d.sqrMagnitude > 1e-6f) fl.t.rotation = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0, -90f, 0);   // local +x along the flight
        }
    }

    // ---------------------------------------------------------------- each frame on the beach
    public void Animate(Camera cam, float dt)
    {
        t += dt;
        if (cam != null)
        {
            var look = cam.transform.rotation;
            SkyKit.LightClouds();
            foreach (var c in clouds)
            {
                var p = c.t.position; p.x += c.v * dt;
                if (p.x > CloudWrap) p.x -= CloudWrap * 2f; else if (p.x < -CloudWrap) p.x += CloudWrap * 2f;
                c.t.position = p; c.t.rotation = look;
            }
        }
        foreach (var fl in flocks)
        {
            fl.th += fl.w * fl.dir * dt;
            foreach (var b in fl.birds) SkyKit.Beat(b, t, dt, R);
        }
        PlaceFlocks();
        StepStrays(dt);
    }

    /// the beach is hidden: hold the wingbeats so nothing animates off screen
    public void Pause(bool on)
    {
        foreach (var fl in flocks) foreach (var b in fl.birds) if (b.clips != null && b.clips.Valid) b.clips.Pause(on);
        foreach (var st in strays) foreach (var b in st.birds) if (b.clips != null && b.clips.Valid) b.clips.Pause(on);
    }
}
