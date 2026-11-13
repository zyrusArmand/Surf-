// What the two skies share: the ride's (SkyLife) and the title beach's (BeachSky).
//
// CLOUDS. The web draws each cumulus as a canvas cut-out with one colour on it, tinted per
// cloud by the hour. That reads as a sticker at any distance: there is nothing in it for the
// light to do. Here a cloud is one tile of a pair of baked textures (Resources/Clouds): eight
// real 3D cumulus, raymarched offline and lit from six directions (unity/Tools~/cloudbake), so
// Surf/Cumulus can light each as a volume every frame from the day cycle -- sun side and shade
// side wherever the sun is, a silver lining only toward the sun, flat grey bases, the
// undersides burning at either end of the day. Each is also used mirrored, so sixteen shapes.
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

    // ---------------------------------------------------------------- the cumulus maps
    // Two 512 square textures, eight 256 x 128 tiles each (two across, four down), baked by
    // unity/Tools~/cloudbake and imported as linear data. Per pixel, in the sprite's frame:
    //   pos: R G B the light arriving from +X +Y +Z, A the cloud's coverage
    //   neg: R G B the light arriving from -X -Y -Z, A the light straight through from behind
    public const int TileW = 256, TileH = 128, Cols = 2, Rows = 4, Baked = Cols * Rows;
    public const int Shapes = Baked * 2;     // and each mirrored
    static Texture2D mapPos, mapNeg;

    /// the baked lightmaps, or false when they are not in the build
    public static bool CloudMaps(out Texture2D pos, out Texture2D neg)
    {
        if (mapPos == null) mapPos = Resources.Load<Texture2D>("Clouds/cumulus_pos");
        if (mapNeg == null) mapNeg = Resources.Load<Texture2D>("Clouds/cumulus_neg");
        pos = mapPos; neg = mapNeg;
        return pos != null && neg != null;
    }

    static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

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
        if (sh == null || !CloudMaps(out var pos, out var neg)) return null;
        cloudMat = new Material(sh) { name = "cumulus" };
        cloudMat.SetTexture("_CloudPos", pos); cloudMat.SetTexture("_CloudNeg", neg);
        cloudMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 20;   // after the opaque world, which covers it by depth
        return cloudMat;
    }

    /// A cloud: a quad w across and w/2 high (the web's PlaneGeometry(w, w*0.5)) centred on its
    /// object, mapped onto one tile of the maps (shapes from Baked up are the same tiles
    /// mirrored, marked by a negative tile width so the shader swaps the light's left and right).
    /// Opacity rides in the vertex colour so a fade is a colour write and every cloud can share
    /// the one material.
    public static GameObject Cloud(Transform parent, float w, int shape, float opacity, string name = "cloud")
    {
        var mat = CloudMaterial();
        if (mat == null) return null;
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.layer = PlanarReflection.SkyLayer;                 // seen by the lens, not by the sea's mirror
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var m = new Mesh { name = "cumulus" };
        Fill(m, w, shape, opacity);
        go.GetComponent<MeshFilter>().sharedMesh = m;
        return go;
    }

    /// give a cloud made by Cloud() a new size and shape, so a pool of them can be reused
    public static void Reshape(GameObject cloud, float w, int shape, float opacity)
    {
        if (cloud == null) return;
        var mf = cloud.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return;
        Fill(mf.sharedMesh, w, shape, opacity);
    }

    static readonly Vector3[] fillV = new Vector3[4];
    static readonly Vector2[] fillUV = new Vector2[4];
    static readonly List<Vector4> fillTile = new List<Vector4> { Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero };
    static readonly int[] fillTri = { 0, 2, 1, 1, 2, 3 };

    static void Fill(Mesh m, float w, int shape, float opacity)
    {
        shape = ((shape % Shapes) + Shapes) % Shapes;
        bool mirror = shape >= Baked; int k = shape % Baked;
        float us = 1f / Cols, vs = 1f / Rows, u0 = (k % Cols) * us, v0 = (k / Cols) * vs;
        float uL = mirror ? u0 + us : u0, uR = mirror ? u0 : u0 + us;
        float hw = w * 0.5f, hh = w * 0.25f;
        fillV[0] = new Vector3(-hw, -hh, 0); fillV[1] = new Vector3(hw, -hh, 0); fillV[2] = new Vector3(-hw, hh, 0); fillV[3] = new Vector3(hw, hh, 0);
        fillUV[0] = new Vector2(uL, v0); fillUV[1] = new Vector2(uR, v0); fillUV[2] = new Vector2(uL, v0 + vs); fillUV[3] = new Vector2(uR, v0 + vs);
        var tile = new Vector4(u0, v0, mirror ? -us : us, vs);
        for (int i = 0; i < 4; i++) fillTile[i] = tile;
        m.vertices = fillV;
        m.uv = fillUV;
        m.SetUVs(1, fillTile);
        var c = new Color(1f, 1f, 1f, opacity);
        opBuf[0] = opBuf[1] = opBuf[2] = opBuf[3] = c;
        m.colors = opBuf;
        if (m.triangles.Length != 6) m.triangles = fillTri;
        m.RecalculateBounds();
    }

    /// set a cloud's opacity (the fade at the ends of its drift)
    static readonly Color[] opBuf = new Color[4];
    public static void SetOpacity(GameObject cloud, float a)
    {
        if (cloud == null) return;
        var mf = cloud.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return;
        var c = new Color(1f, 1f, 1f, a);
        opBuf[0] = opBuf[1] = opBuf[2] = opBuf[3] = c;
        mf.sharedMesh.colors = opBuf;                 // copied in by the mesh, so one buffer serves every cloud
    }

    static DayCycle day;

    /// the clouds take the light the day cycle worked out for them, every frame, so they turn
    /// with the sky: one call does every cumulus in the game, since they share the material
    public static void LightClouds()
    {
        var m = CloudMaterial(); if (m == null) return;
        if (day == null) day = Object.FindFirstObjectByType<DayCycle>();
        if (day == null) return;
        m.SetVector("_SunDir", day.cloudKey.normalized);
        // the day cycle's raw colours turned to light here, once, rather than in every pixel
        Vector3 sun = Lin(day.cloudSun);
        SkyLight(sun, 1f, out var up, out var dn);
        m.SetVector("_SunLin", V4(sun, 1f));
        m.SetVector("_AmbUp", V4(up, 1f));
        m.SetVector("_AmbDn", V4(dn, 1f));
        m.SetVector("_UnderLin", V4(Lin(day.cloudUnder), day.cloudUnder.w));
        m.SetVector("_HzLin", V4(Lin(day.skyHz), 1f));
    }

    /// The whitewater puffs a carve throws and the rails leave on the water (Surf/Puff, Fx.cs)
    /// take the clouds' light from the same day cycle, every frame, so the sun side and shade
    /// side of each turn with the sky. But they are at sea level: the sun leaves them as it
    /// leaves the sea, not a while later as it leaves the clouds overhead. And foam scatters
    /// back more of the sky's light than a cloud's flank does, so its shade stays a soft grey.
    public static void LightPuffs(Material m)
    {
        if (m == null) return;
        if (day == null) day = Object.FindFirstObjectByType<DayCycle>();
        if (day == null) return;
        m.SetVector("_SunDir", day.cloudKey.normalized);
        float seaSun = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.04f, 0.10f, day.sunDir.y));
        Vector3 sun = Lin(day.cloudSun) * (0.3f + 0.7f * seaSun);
        SkyLight(sun, 1.25f, out var up, out var dn);
        m.SetVector("_SunLin", V4(sun, 1f));
        m.SetVector("_AmbUp", V4(up, 1f));
        m.SetVector("_AmbDn", V4(dn, 1f));
        m.SetVector("_HzLin", V4(Lin(day.skyHz), 1f));
    }

    // the sky's light on something white out in the open, as linear colour: from the dome above
    // and off the sea below, scaled by how much light there is about
    static void SkyLight(Vector3 sun, float boost, out Vector3 up, out Vector3 dn)
    {
        Vector3 top = day.skyTop, mid = day.skyMid, hz = day.skyHz;
        // how low and golden the sun is (the underside glow's amount): the sky round a cloud is
        // brightest and warmest then, though the day's light is at its least
        float low = day.cloudUnder.w;
        float amb = (0.40f + 0.60f * day.cloudLight + 0.45f * low) * 0.55f * boost;
        // the dome above a cloud: mostly the blue overhead, a little greyed, and a touch of the
        // sun's own scatter; with the sun low, more of the warm glow low down in it
        Vector3 sky = Vector3.Lerp(Vector3.Lerp(mid, top, 0.75f - 0.45f * low), hz, 0.55f * low);
        up = (Lin(Vector3.Lerp(sky, new Vector3(0.86f, 0.88f, 0.92f), 0.25f * (1f - low))) * 0.85f + sun * 0.06f) * amb;
        // and below it the sea, darker, with the horizon's colour in it
        dn = Lin(Vector3.Lerp(hz, new Vector3(0.10f, 0.34f, 0.50f), 0.55f)) * 0.45f * amb;
    }

    static Vector3 Lin(Vector4 c) => new Vector3(Mathf.Pow(Mathf.Max(0f, c.x), 2.2f), Mathf.Pow(Mathf.Max(0f, c.y), 2.2f), Mathf.Pow(Mathf.Max(0f, c.z), 2.2f));
    static Vector3 Lin(Vector3 c) => Lin(new Vector4(c.x, c.y, c.z, 1f));
    static Vector4 V4(Vector3 v, float w) => new Vector4(v.x, v.y, v.z, w);

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
            // skinned only while on screen: two dozen gulls skinned every frame, most of them behind
            // the lens or off its edge, is wasted work on a phone. The bounds are padded to cover the
            // wings' full beat, so a bird is never culled while a wingtip is still in shot.
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            { smr.updateWhenOffscreen = false; var lb = smr.localBounds; lb.Expand(lb.size.magnitude * 1.5f); smr.localBounds = lb; }
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
        // out of the sea's mirror with the clouds: a gull crossing low over the swell, mirrored,
        // was a dark shape sliding across the water that read as something moving in the sea
        if (b.t != null) foreach (var tr in b.t.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = PlanarReflection.SkyLayer;
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
