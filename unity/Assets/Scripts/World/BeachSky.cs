// Sky furniture over the title screen's beach: the web game's clouds and gulls.
//
// CLOUDS are the web's cloudTexture() brought across pixel for pixel: a pile of soft radial
// lumps on a flat base, shaded underneath, then re-cut against a wobbling noise threshold so
// the edge goes cauliflower rather than reading as nine circles. Sixteen of them on quads,
// far out over the water, billboarded to the lens and drifting slowly across.
//
// GULLS are models/bird_rigged.glb, which carries a flap and a glide clip on its skeleton;
// each bird blends between the two on its own timer, the way the web's beatGull does, and
// the flocks (a V, a loose bunch, a solo) circle slowly out over the sea. Without the file,
// a drawn silhouette flies the same paths with the web's own asymmetric wingbeat.
using System.Collections.Generic;
using UnityEngine;

public class BeachSky
{
    class Cloud { public Transform t; public float v; }
    class Bird
    {
        public Transform t; public GltfClips clips;         // the modelled gull
        public Transform wingL, wingR;                       // or the drawn one
        public float f, p, up, dn, dwK;                      // the drawn beat
        public bool glide; public float gT, wF, flapRate, glideRate;
    }
    class Flock
    {
        public Transform t; public readonly List<Bird> birds = new List<Bird>();
        public Vector3 c; public float rx, rz, w, th, bob, dir;
    }

    public float birdYaw = 90f;     // the model faces +z; a flock flies along its own +x
    public float birdSpan = 3.0f;   // feet, wingtip to wingtip, before the per-bird and per-flock scales

    readonly List<Cloud> clouds = new List<Cloud>();
    readonly List<Flock> flocks = new List<Flock>();
    Transform root; float t;
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
    // Cumulus: each cloud a pile of lit puffs (Surf/CloudPuff), heaped highest in the middle and
    // spread flat along a base, one mesh and one draw each. Where the web cut its clouds out of a
    // painted canvas, these are lit by the title's own sun and sky, so they have a bright crown, a
    // cool underside and a silver edge, and go gold with the sunset.
    Material puffMat; Light sunLight;
    void BuildClouds()
    {
        var sh = Shader.Find("Surf/CloudPuff");
        if (sh == null) return;
        puffMat = new Material(sh) { name = "cloud puffs" };
        puffMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 20;   // after the opaque world, which covers it by depth
        var holder = new GameObject("Clouds").transform; holder.SetParent(root, false);
        // the web's rule (sixteen of them, far out and high, 50..90% opaque), at the sizes and
        // distances measured off the running title screen, a little fuller
        for (int i = 0; i < 16; i++)
        {
            float w = 58f + R() * 70f, op = 0.62f + R() * 0.34f;
            var go = new GameObject("cloud", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(holder, false);
            go.GetComponent<MeshFilter>().sharedMesh = Cumulus(w, op);
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = puffMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            go.transform.position = new Vector3((R() - 0.5f) * 2f * CloudWrap, 70f + R() * 110f, -277f - R() * 219f);
            clouds.Add(new Cloud { t = go.transform, v = 0.9f + R() * 2.1f });
        }
    }

    Mesh Cumulus(float W, float op)
    {
        int n = 16 + (int)(R() * 14f);
        float H = W * (0.30f + R() * 0.14f);
        var cs = new List<Vector3>(); var rs = new List<float>(); var hs = new List<float>();
        // the base: a row of wide, low puffs that makes the flat underside
        int nb = 5 + (int)(R() * 3f);
        for (int k = 0; k < nb; k++)
        {
            float x = Mathf.Lerp(-0.42f, 0.42f, (k + 0.5f) / nb) * W + (R() - 0.5f) * W * 0.06f;
            float r = W * (0.11f + 0.04f * R()) * (1f - 0.35f * Mathf.Abs(x / (W * 0.5f)));
            cs.Add(new Vector3(x, r * 0.55f, (R() - 0.5f) * W * 0.10f)); rs.Add(r); hs.Add(0.05f);
        }
        // the heap: puffs piled toward the middle, smaller as they climb
        for (int k = 0; k < n; k++)
        {
            float u = (R() + R() + R()) / 3f - 0.5f;                       // bunched to the middle
            float x = u * W * 0.9f;
            float top = H * Mathf.Pow(Mathf.Max(0f, 1f - (2f * x / W) * (2f * x / W)), 0.7f);
            float y = Mathf.Pow(R(), 0.8f) * top * 0.78f + W * 0.06f;
            float r = W * (0.085f + 0.09f * R()) * (1f - 0.40f * y / Mathf.Max(1f, H));
            cs.Add(new Vector3(x, y, (R() - 0.5f) * W * 0.22f)); rs.Add(r); hs.Add(Mathf.Clamp01((y + r * 0.6f) / Mathf.Max(1f, H)));
        }
        // far first, so the pile blends back to front from the title's lens
        var order = new List<int>(); for (int k = 0; k < cs.Count; k++) order.Add(k);
        order.Sort((a, b) => cs[a].z.CompareTo(cs[b].z));
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var uv3 = new List<Vector2>(); var idx = new List<int>();
        foreach (int k in order)
        {
            int b0 = v.Count; float seed = R();
            for (int c = 0; c < 4; c++)
            {
                v.Add(cs[k]); uv.Add(new Vector2(rs[k], seed)); uv3.Add(new Vector2(hs[k], op));
            }
            uv2.Add(new Vector2(-1, -1)); uv2.Add(new Vector2(1, -1)); uv2.Add(new Vector2(1, 1)); uv2.Add(new Vector2(-1, 1));
            idx.Add(b0); idx.Add(b0 + 2); idx.Add(b0 + 1); idx.Add(b0); idx.Add(b0 + 3); idx.Add(b0 + 2);
        }
        var m = new Mesh { name = "cumulus" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetUVs(1, uv2); m.SetUVs(2, uv3); m.SetTriangles(idx, 0);
        m.bounds = new Bounds(new Vector3(0, H * 0.5f, 0), new Vector3(W * 1.4f, H * 1.8f, W * 0.6f));
        return m;
    }

    // the clouds take the live sky's colours and the sun's, every frame, so they turn with the light
    void LightClouds()
    {
        if (puffMat == null) return;
        if (sunLight == null)
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.name == "Sun") { sunLight = l; break; }
        var sky = RenderSettings.skybox;
        if (sky != null && sky.HasProperty("_Top")) { puffMat.SetVector("_Sky", sky.GetVector("_Mid")); puffMat.SetVector("_Hz", sky.GetVector("_Horizon")); }
        if (sky != null && sky.HasProperty("_Glow")) puffMat.SetVector("_SunCol", Vector4.Lerp(new Vector4(1f, 0.97f, 0.92f, 1f), sky.GetVector("_Glow"), 0.55f));
        if (sunLight != null) puffMat.SetVector("_SunDir", -sunLight.transform.forward);
        puffMat.SetFloat("_T", t);
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

    void BuildBirds()
    {
        var prefab = Resources.Load<GameObject>("Models/bird_rigged");
        var holder = new GameObject("Gulls").transform; holder.SetParent(root, false);
        Material silhouette = null;
        for (int f = 0; f < FlightN.Length; f++)
        {
            var fl = new Flock();
            var fg = new GameObject("flock " + f).transform; fg.SetParent(holder, false); fl.t = fg;
            float beat = 1.7f + R() * 2.0f;            // the flock's rate; its birds take it with a little jitter
            for (int i = 0; i < FlightN[f]; i++)
            {
                Vector3 at;
                if (FlightV[f])
                {
                    int r = (i + 1) / 2; float sd = (i % 2) == 1 ? 1f : -1f;
                    at = new Vector3(-r * (2.6f + R() * 0.42f), (R() - 0.5f) * 0.6f, r > 0 ? sd * r * (2.0f + R() * 0.3f) : 0f);
                }
                else if (FlightN[f] == 1) at = Vector3.zero;
                else at = new Vector3((R() - 0.5f) * 7f, (R() - 0.5f) * 2.4f, (R() - 0.5f) * 7f);
                float sz = 0.8f + R() * 0.5f;
                var b = new Bird();
                if (prefab != null)
                {
                    var go = Models.Spawn("bird_rigged", fg, 1f, PrimitiveType.Sphere);
                    var bb = Models.Bounds(go);
                    float span = Mathf.Max(1e-3f, Mathf.Max(bb.size.x, bb.size.z));
                    go.transform.localScale = go.transform.localScale * (birdSpan / span * sz);
                    go.transform.localRotation = Quaternion.Euler(0, birdYaw, 0);
                    go.transform.localPosition = at;
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
                    b.t = DrawnGull(fg, silhouette, out b.wingL, out b.wingR);
                    b.t.localPosition = at; b.t.localScale = Vector3.one * sz;
                }
                // the drawn beat's own numbers, per bird (used when there is no clip to play)
                b.f = 10f + R() * 8.5f; b.p = R() * Mathf.PI * 2f;
                b.up = 0.92f + R() * 0.42f; b.dn = -0.52f - R() * 0.34f; b.dwK = 0.82f + R() * 0.40f;
                b.glide = R() < 0.35f; b.gT = 1.5f + R() * 4.5f; b.wF = b.glide ? 0f : 1f;
                if (b.clips != null) { b.clips.SetWeight(0, b.wF); b.clips.SetWeight(1, 1f - b.wF); }
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

    // the web's gullAngle: the downstroke is the fast half, the recovery takes half again as long
    static float GullAngle(float w, Bird u)
    {
        float dw = Mathf.Clamp(0.38f * u.dwK, 0.12f, 0.88f);
        return w < dw
            ? u.up - (u.up - u.dn) * (0.5f - 0.5f * Mathf.Cos(Mathf.PI * (w / dw)))
            : u.dn + (u.up - u.dn) * (0.5f - 0.5f * Mathf.Cos(Mathf.PI * ((w - dw) / (1f - dw))));
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
            LightClouds();
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
            foreach (var b in fl.birds)
            {
                // a gull glides most of the time and flaps in bursts; the change of habit is a crossfade
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
        }
        PlaceFlocks();
    }

    /// the beach is hidden: hold the wingbeats so nothing animates off screen
    public void Pause(bool on)
    {
        foreach (var fl in flocks) foreach (var b in fl.birds) if (b.clips != null && b.clips.Valid) b.clips.Pause(on);
    }
}
