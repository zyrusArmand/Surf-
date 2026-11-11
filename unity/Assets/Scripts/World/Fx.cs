// The water's small effects, on top of the web's spray: a ring spreading on the surface where
// he lands or goes in, a trail of foam blotches peeling off the tail of the board, mist hanging
// in the barrel while the ring has him, and drops of water on the lens after a wipeout or when
// the camera comes back up out of a dive. All particles, all soft (they fade where they meet the
// sea instead of cutting through it), and all carried toward the camera at the run's speed,
// because the world scrolls past the rider rather than him moving through it.
using UnityEngine;
using UnityEngine.UI;

public class Fx : MonoBehaviour
{
    public BoardController rider;
    public SetWave setWave;
    public Dive dive;
    ParticleSystem rings, trail, mist;
    RectTransform lens; RawImage[] drops; float[] dropT, dropV; float lensT;
    bool wasUnder; float trailAcc, mistAcc;

    public void Build()
    {
        rings = Make("SplashRings", RingTex(), 120, true);
        trail = Make("FoamTrail", FoamTex(), 500, true);
        mist = Make("BarrelMist", Bootstrap.DropTex(), 260, false);
        BuildLens();
    }

    // ---- the systems ----
    ParticleSystem Make(string name, Texture2D tex, int max, bool flat)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = max; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = Bootstrap.SoftParticles(tex);
        if (m != null) { Soften(m, flat ? 0.25f : 0.9f); r.material = m; }
        // grow and fade over the life
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, flat ? new AnimationCurve(new Keyframe(0, 0.35f), new Keyframe(1, 1f))
                                                       : new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 1.25f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        return ps;
    }

    /// soft particles: fade out over the last few inches before they meet whatever is behind them
    public static void Soften(Material m, float far)
    {
        m.EnableKeyword("_SOFTPARTICLES_ON");
        m.SetFloat("_SoftParticlesEnabled", 1f);
        m.SetFloat("_SoftParticlesNearFadeDistance", 0f); m.SetFloat("_SoftParticlesFarFadeDistance", far);
        m.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / Mathf.Max(0.01f, far), 0f, 0f));
    }

    /// a ring spreading on the water where something hit it; bigger for a bigger splash
    public void Ring(Vector3 p, int n)
    {
        if (rings == null) return;
        float k = Mathf.Clamp01(n / 60f);
        var ep = new ParticleSystem.EmitParams
        {
            position = new Vector3(p.x, p.y + 0.06f, p.z),
            velocity = new Vector3(0f, 0f, Flow()),
            startSize = 3.2f + 5.5f * k,
            startLifetime = 0.9f + 0.6f * k,
            startColor = new Color(1f, 1f, 1f, 0.55f + 0.35f * k),
            rotation = Random.value * 360f
        };
        rings.Emit(ep, 1);
        if (n >= 60) Splash();            // a wipeout: the lens takes some of it
    }

    float Flow() => rider != null && rider.running ? rider.eff : 0f;

    public void Step(float dt, Color tint)
    {
        if (rider == null) return;
        SetTint(rings, tint); SetTint(trail, tint); SetTint(mist, tint);
        // the foam trail off the tail, while he is on the water and moving
        bool onWater = rider.running && !rider.airborne && !rider.wipe && !rider.Submerged && (dive == null || !dive.On);
        if (onWater)
        {
            float rate = Mathf.Lerp(10f, 34f, Mathf.InverseLerp(14f, 50f, rider.eff));
            trailAcc += dt * rate;
            while (trailAcc >= 1f)
            {
                trailAcc -= 1f;
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(rider.px + (Random.value - 0.5f) * 0.6f, rider.waterY + 0.05f, rider.pz + 2.2f + Random.value * 0.4f),
                    velocity = new Vector3((Random.value - 0.5f) * 0.6f, 0f, rider.eff * 0.96f),
                    startSize = 0.9f + Random.value * 0.9f,
                    startLifetime = 1.1f + Random.value * 0.8f,
                    startColor = new Color(1f, 1f, 1f, 0.45f + Random.value * 0.25f),
                    rotation = Random.value * 360f
                };
                trail.Emit(ep, 1);
            }
        }
        else trailAcc = 0f;
        // mist hanging in the barrel while the ring has him
        if (setWave != null && setWave.tubeRide)
        {
            mistAcc += dt * 26f;
            while (mistAcc >= 1f)
            {
                mistAcc -= 1f;
                float side = Random.value < 0.5f ? -1f : 1f;
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(rider.px + side * (1.5f + Random.value * 4f), rider.waterY + 1.5f + Random.value * 5f, rider.pz - 4f - Random.value * 14f),
                    velocity = new Vector3((Random.value - 0.5f) * 1.2f, 0.4f + Random.value * 0.6f, Flow() * 0.85f + 2f),
                    startSize = 2.2f + Random.value * 3.2f,
                    startLifetime = 1.0f + Random.value * 0.9f,
                    startColor = new Color(1f, 1f, 1f, 0.10f + Random.value * 0.10f)
                };
                mist.Emit(ep, 1);
            }
        }
        else mistAcc = 0f;
        // coming back up out of a dive leaves the lens wet too
        bool under = dive != null && dive.On;
        if (wasUnder && !under) Splash();
        wasUnder = under;
        StepLens(Time.unscaledDeltaTime);
        Ride(rings); Ride(trail);
    }

    // what lies flat on the water rides its swell: each one put back on the surface where it now is
    ParticleSystem.Particle[] buf;
    void Ride(ParticleSystem ps)
    {
        if (ps == null || ps.particleCount == 0) return;
        if (buf == null || buf.Length < ps.main.maxParticles) buf = new ParticleSystem.Particle[ps.main.maxParticles];
        int n = ps.GetParticles(buf);
        float t = rider.t, fz0 = WaveField.Scroll;
        for (int i = 0; i < n; i++)
        {
            var q = buf[i].position;
            q.y = WaveField.Height(q.x, fz0 + q.z, t) + 0.06f;
            buf[i].position = q;
        }
        ps.SetParticles(buf, n);
    }

    static void SetTint(ParticleSystem ps, Color c)
    {
        if (ps == null) return;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r != null && r.sharedMaterial != null) r.sharedMaterial.SetColor("_BaseColor", c);
    }

    // ---- drops on the lens ----
    void BuildLens()
    {
        var cgo = new GameObject("LensDrops", typeof(Canvas), typeof(CanvasScaler));
        var cv = cgo.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = -5;   // under the HUD
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(430, 932); sc.matchWidthOrHeight = 0f;
        lens = (RectTransform)cgo.transform;
        var tex = LensDropTex();
        const int N = 16;
        drops = new RawImage[N]; dropT = new float[N]; dropV = new float[N];
        for (int i = 0; i < N; i++)
        {
            var go = new GameObject("drop" + i, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(lens, false);
            var ri = go.GetComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = Vector2.zero;
            go.SetActive(false); drops[i] = ri;
        }
    }

    /// water thrown at the lens: a scatter of drops that sit, slide a little and dry off
    public void Splash()
    {
        if (drops == null) return;
        var size = lens.rect.size; if (size.x < 1f) size = new Vector2(430, 932);
        int n = 7 + Random.Range(0, 6);
        for (int i = 0; i < drops.Length && n > 0; i++)
        {
            if (drops[i].gameObject.activeSelf && dropT[i] < 1.2f) continue;
            n--;
            var rt = drops[i].rectTransform;
            float s = 14f + Random.value * Random.value * 46f;
            rt.sizeDelta = new Vector2(s, s * (1.0f + Random.value * 0.25f));
            rt.anchoredPosition = new Vector2(Random.value * size.x, size.y * (0.12f + Random.value * 0.86f));
            rt.localRotation = Quaternion.Euler(0, 0, (Random.value - 0.5f) * 20f);
            dropT[i] = 1.6f + Random.value * 1.6f;              // how long it hangs there
            dropV[i] = s > 34f ? 8f + Random.value * 22f : 0f;   // the big ones run down the glass
            drops[i].color = new Color(1, 1, 1, 0.95f);
            drops[i].gameObject.SetActive(true);
        }
        lensT = 3.4f;
    }

    void StepLens(float dt)
    {
        if (drops == null || lensT <= 0f) return;
        lensT -= dt;
        for (int i = 0; i < drops.Length; i++)
        {
            if (!drops[i].gameObject.activeSelf) continue;
            dropT[i] -= dt;
            var rt = drops[i].rectTransform;
            rt.anchoredPosition += new Vector2(0f, -dropV[i] * dt);
            float a = Mathf.Clamp01(dropT[i] / 0.8f);
            drops[i].color = new Color(1, 1, 1, 0.95f * a);
            if (dropT[i] <= 0f) drops[i].gameObject.SetActive(false);
        }
    }

    // ---- textures, made here ----
    static Texture2D ringTex, foamTex, lensTex;

    // a soft ring with a torn edge: the spreading lip of a splash seen from above
    static Texture2D RingTex()
    {
        if (ringTex != null) return ringTex;
        const int N = 128; ringTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "ring" };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f, ang = Mathf.Atan2(dy, dx);
            float tear = 0.5f + 0.5f * Mathf.PerlinNoise(Mathf.Cos(ang) * 3f + 5f, Mathf.Sin(ang) * 3f + 5f);
            float band = Mathf.Exp(-Mathf.Pow((r - 0.80f) / (0.07f + 0.05f * tear), 2f));
            float inner = Mathf.Exp(-Mathf.Pow((r - 0.55f) / 0.12f, 2f)) * 0.25f;
            float a = Mathf.Clamp01((band * (0.55f + 0.45f * tear) + inner) * (r < 1f ? 1f : 0f));
            px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        ringTex.SetPixels32(px); ringTex.Apply(true, true);
        return ringTex;
    }

    // a foam blotch: a few soft lumps with holes torn in them
    static Texture2D FoamTex()
    {
        if (foamTex != null) return foamTex;
        const int N = 96; foamTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "foam" };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float body = Mathf.Clamp01(1f - r); body = body * body * (3f - 2f * body);
            float n1 = Mathf.PerlinNoise(u * 6f + 11f, v * 6f + 3f), n2 = Mathf.PerlinNoise(u * 15f + 2f, v * 15f + 9f);
            float lace = Mathf.Clamp01((n1 * 0.65f + n2 * 0.45f - 0.42f) * 3.2f);
            float a = body * lace;
            px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
        }
        foamTex.SetPixels32(px); foamTex.Apply(true, true);
        return foamTex;
    }

    // a water drop on glass: nearly clear inside, a darker refracting rim, a bright catchlight
    static Texture2D LensDropTex()
    {
        if (lensTex != null) return lensTex;
        const int N = 64; lensTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "lensdrop" };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            if (r >= 1f) { px[y * N + x] = new Color32(0, 0, 0, 0); continue; }
            float rim = Mathf.Pow(r, 6f) * 0.55f;
            float lo = Mathf.Exp(-(Mathf.Pow(dx + 0.05f, 2f) + Mathf.Pow(dy + 0.20f, 2f)) / 0.012f) * 0.30f;   // light through the bottom of the drop
            float hi = Mathf.Exp(-(Mathf.Pow(dx + 0.16f, 2f) + Mathf.Pow(dy - 0.18f, 2f)) / 0.0035f);          // the catchlight, top left
            float edge = Mathf.Clamp01((1f - r) * 14f);
            float lum = Mathf.Clamp01(0.62f - rim * 0.5f + hi * 0.4f + lo);
            float a = Mathf.Clamp01((0.10f + rim + hi * 0.85f + lo) * edge);
            byte l = (byte)Mathf.RoundToInt(lum * 255f);
            px[y * N + x] = new Color32(l, (byte)Mathf.Min(255, l + 8), (byte)Mathf.Min(255, l + 12), (byte)Mathf.RoundToInt(a * 255f));
        }
        lensTex.SetPixels32(px); lensTex.Apply(true, true);
        return lensTex;
    }
}
