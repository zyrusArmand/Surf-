// The water's small effects: every splash (Hit: a crown of drops, a mist, foam left on the water
// and a real ring of wave running out through the sea's own surface), a trail of foam blotches peeling off the tail of the board, mist hanging
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
    ParticleSystem trail, mist, carve, carveMist, clumps, sparkle;
    public Vector3 tail, tailRight = Vector3.right, tailFwd = Vector3.forward; public float tailHalfW = 0.55f; public bool tailOk;   // the board's tail at the water, its axes and half width, set each frame by Bootstrap
    float clumpAcc, fizzAcc;
    readonly Vector4[] rip = new Vector4[4];        // the rings of wave in the sea: x, field z, age, height (Surf/Ocean _Rip)
    readonly Vector4[] ripS = new Vector4[4];       // and the shape each starts from: half width, half length, sin and cos of its heading (_RipS)
    static readonly int RipId = Shader.PropertyToID("_Rip"), RipSId = Shader.PropertyToID("_RipS");
    static readonly Vector4 Round = new Vector4(0.35f, 0.35f, 0f, 1f);   // anything that is not the board: a small round splash
    RectTransform lens; RawImage[] drops; float[] dropT, dropV; float lensT;
    bool wasUnder; float trailAcc, mistAcc;

    public void Build()
    {
        trail = Make("FoamTrail", FoamTex(), 500, true);
        mist = Make("BarrelMist", Bootstrap.DropTex(), 260, false);
        carve = MakeCarve();
        carveMist = MakeCarveMist();
        clumps = MakeClumps();
        sparkle = MakeSparkle();
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

    // The sheet a carve throws: fine drops off the tail, flung up and out from the outside rail,
    // each drawn stretched along its own flight so it reads as spray, not as a cloud of dots.
    ParticleSystem MakeCarve()
    {
        var go = new GameObject("CarveSpray"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = 1600; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.85f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.6f; drag.limit = 1000f;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.8f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0.95f, 0), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.005f; r.lengthScale = 1.0f;   // drops, only a little drawn out by their speed: more read as dashes
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = Bootstrap.SoftParticles(Bootstrap.DropTex());
        if (m != null) { Soften(m, 0.3f); r.material = m; }
        return ps;
    }

    float carveAcc;
    /// a carve, this frame: `tail` is the outside rail at the tail, `outward` the side the spray goes,
    /// k how hard he is turning (0..1)
    public void Carve(Vector3 tail, float outward, float k, float dt)
    {
        if (carve == null) return;
        // A carve throws a curtain off the rail: dense and white at its root, fanning up, out and
        // BACK (toward the lens) into a sheet of drops, the finest of them catching the sun. Fine
        // drops make the sheet and a fifth are heavy ones that arc higher and longer; clumps of
        // whitewater make its solid core; a mist hangs in it; glints sparkle through it.
        float back = (rider != null ? rider.eff : 20f) * 0.22f;     // the water he leaves runs back past the lens with the run
        carveAcc += dt * 420f * Mathf.Pow(k, 1.5f);
        while (carveAcc >= 1f)
        {
            carveAcc -= 1f;
            float fan = Random.value, heavy = Random.value < 0.2f ? 1f : 0f;
            // the fan: from mostly out-and-up at one edge to mostly back-and-up at the other
            float outV = (2.0f + 7.0f * (1f - fan)) * (0.55f + 0.45f * k);
            float backV = back + (2.0f + 10.0f * fan) * (0.6f + 0.4f * k);
            float upV = (4.0f + 9.0f * Random.value) * (0.5f + 0.5f * k) * (1f + 0.3f * heavy);
            var vel = new Vector3(outward * outV, upV, backV);
            var ep = new ParticleSystem.EmitParams
            {
                position = tail + new Vector3(outward * Random.value * 0.35f, Random.value * 0.12f, (Random.value - 0.3f) * 1.0f),
                velocity = vel,
                startSize = heavy > 0f ? 0.14f + 0.16f * Random.value : 0.04f + 0.10f * Random.value * Random.value,
                startLifetime = 0.6f + 0.6f * Random.value + 0.3f * heavy,
                startColor = new Color(1f, 1f, 1f, 0.8f + 0.2f * Random.value)
            };
            carve.Emit(ep, 1);
            float r = Random.value;
            if (clumps != null && r < 0.20f)
            {
                // the curtain's solid white core: whitewater thrown with it, slower, bigger, nearer the rail
                var ec = new ParticleSystem.EmitParams
                {
                    position = tail + new Vector3(outward * (0.2f + Random.value * 0.6f), 0.15f + Random.value * 0.5f, Random.value * 0.6f),
                    velocity = vel * (0.35f + 0.25f * Random.value),
                    startSize = 0.3f + 0.45f * k * Random.value + 0.15f,
                    startLifetime = 0.4f + 0.45f * Random.value,
                    startColor = new Color(1f, 1f, 1f, 0.55f),
                    rotation = Random.value * 360f
                };
                clumps.Emit(ec, 1);
            }
            if (sparkle != null && r > 0.85f)
            {
                var es = new ParticleSystem.EmitParams
                {
                    position = ep.position, velocity = vel * (0.9f + 0.3f * Random.value),
                    startSize = 0.02f + 0.04f * Random.value,
                    startLifetime = 0.35f + 0.4f * Random.value,
                    startColor = Color.white
                };
                sparkle.Emit(es, 1);
            }
            if (carveMist != null && r > 0.20f && r < 0.30f)
            {
                var em = new ParticleSystem.EmitParams
                {
                    position = tail + new Vector3(outward * (0.2f + Random.value * 0.6f), 0.2f + Random.value * 0.5f, Random.value * 0.8f),
                    velocity = new Vector3(outward * outV * 0.35f, upV * 0.35f, backV * 0.7f),
                    startSize = 0.7f + 1.1f * Random.value * k,
                    startLifetime = 0.7f + 0.6f * Random.value,
                    startColor = new Color(1f, 1f, 1f, 0.18f + 0.12f * k)
                };
                carveMist.Emit(em, 1);
            }
        }
    }

    // torn clumps of whitewater: the trail's churn and a carve's core. Four shapes in one texture,
    // each puff picks one; shaded bright on top and grey-blue beneath so it reads as foam with
    // volume, not a flat sticker; soft where it meets the water
    ParticleSystem MakeClumps()
    {
        var go = new GameObject("Whitewater"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = 700; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.25f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.8f; drag.limit = 1000f;
        var ts = ps.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 2; ts.numTilesY = 2;
        ts.animation = ParticleSystemAnimationType.WholeSheet;
        ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);   // a random tile per puff, held for its life (normalised over the sheet)
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 1.5f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var rr = go.GetComponent<ParticleSystemRenderer>();
        // drawn out along their flight: torn streaks of white water, not round puffs
        rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = 0.05f; rr.lengthScale = 1.4f;
        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
        var m = Bootstrap.SoftParticles(FoamClumpTex());
        if (m != null) { Soften(m, 0.6f); rr.material = m; }
        return ps;
    }

    // glints: the finest drops in a carve, brighter than white so the bloom catches them
    ParticleSystem MakeSparkle()
    {
        var go = new GameObject("Glints"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = 600; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.85f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.6f; drag.limit = 1000f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;   // points of light, not streaks
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = Bootstrap.SoftParticles(Bootstrap.DropTex());
        if (m != null) { m.SetColor("_BaseColor", new Color(2.6f, 2.5f, 2.3f, 1f)); r.material = m; }
        return ps;
    }

    // the mist in a carve's spray: soft, growing, drifting back and fading
    ParticleSystem MakeCarveMist()
    {
        var go = new GameObject("CarveMist"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = 300; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.12f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 2.4f; drag.limit = 1000f;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 1.9f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = Bootstrap.SoftParticles(Bootstrap.DropTex());
        if (m != null) { Soften(m, 0.8f); r.material = m; }
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

    // ---- a splash, as water makes one ----
    // Whatever hit the sea (a landing, a bounce, something knocked in, a drip off his paws): a
    // crown of small drops thrown out and up, wider across than along for a flat thing slapping
    // down; a mist hanging in it for anything big; torn foam left where it went in, riding the
    // swell and carried off with the water; and a real ring of wave running out through the sea's
    // own surface (Surf/Ocean _Rip), lit like the rest of it. No flat ring drawn on top, no
    // round blobs: those read as a sticker and as glitter.
    /// `shape` is the board's outline when the board itself hit the water (half width, half length,
    /// sin and cos of its heading, centred on p); left out, the splash is round
    public void Hit(Vector3 p, int n, Vector4 shape = default)
    {
        float k = Mathf.Clamp01(n / 60f), flow = Flow();
        float sea = rider != null ? WaveField.Height(p.x, WaveField.Scroll + p.z, rider.t) : p.y;
        bool atSea = Mathf.Abs(p.y - sea) < 1.2f;
        if (p.y < sea - 1.0f) return;     // under the water (the dive's own splashes): nothing is thrown into the air
        // the drops: a drip (the handstand's and the helicopter's, thirty and more a second) is
        // only its own few; a real splash is a crown
        int count = n < 6 ? n : Mathf.Min(220, 4 + Mathf.RoundToInt(n * 2.6f));
        if (carve != null)
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f, cx = Mathf.Cos(a), cz = Mathf.Sin(a) * 0.55f;
                float hs = (1.2f + 5.0f * Random.value) * (0.35f + 0.65f * k);
                float up = 1.0f + (1.5f + 5.5f * Random.value * Random.value) * (0.3f + 0.7f * k);
                var ep = new ParticleSystem.EmitParams
                {
                    position = p + new Vector3(cx * (0.35f + 0.25f * Random.value), 0.05f, cz * 1.6f + (Random.value - 0.5f) * 0.4f),
                    velocity = new Vector3(cx * hs, up, cz * hs + flow * 0.15f),
                    startSize = 0.035f + 0.10f * Random.value * Random.value + 0.08f * k * Random.value,
                    startLifetime = 0.4f + 0.5f * Random.value,
                    startColor = new Color(1f, 1f, 1f, 0.7f + 0.3f * Random.value)
                };
                carve.Emit(ep, 1);
            }
        // the mist, for anything bigger than a drip
        if (carveMist != null && n >= 10)
            for (int i = 0, m = 2 + n / 6; i < m; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = p + new Vector3((Random.value - 0.5f) * 1.6f, 0.2f + Random.value * 0.4f, (Random.value - 0.5f) * 2.2f),
                    velocity = new Vector3((Random.value - 0.5f) * 2f, 0.8f + Random.value * 1.5f * k, flow * 0.5f),
                    startSize = 0.6f + 1.2f * k * Random.value + 0.3f,
                    startLifetime = 0.7f + 0.6f * Random.value,
                    startColor = new Color(1f, 1f, 1f, 0.12f + 0.12f * k)
                };
                carveMist.Emit(ep, 1);
            }
        if (atSea && n >= 6)
        {
            // the foam it leaves
            if (trail != null)
                for (int i = 0, m = 2 + n / 7; i < m; i++)
                {
                    float a = Random.value * Mathf.PI * 2f, rr = (0.4f + 1.4f * k) * Mathf.Sqrt(Random.value);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = new Vector3(p.x + Mathf.Cos(a) * rr * 1.3f, sea + 0.05f, p.z + Mathf.Sin(a) * rr),
                        velocity = new Vector3((Random.value - 0.5f) * 0.4f, 0f, flow * 0.96f),
                        startSize = 0.8f + 1.3f * k * Random.value + 0.4f * Random.value,
                        startLifetime = 1.4f + 1.2f * Random.value,
                        startColor = new Color(1f, 1f, 1f, 0.45f + 0.25f * k),
                        rotation = Random.value * 360f
                    };
                    trail.Emit(ep, 1);
                }
            // and the ring of wave
            AddRipple(p.x, WaveField.Scroll + p.z, 0.04f + 0.16f * k, shape.x > 0f ? shape : Round);
        }
        if (n >= 60) Splash();            // a wipeout: the lens takes some of it
    }

    void AddRipple(float x, float fz, float amp, Vector4 shape)
    {
        int slot = 0; float oldest = -1f;
        for (int i = 0; i < rip.Length; i++)
        {
            if (rip[i].w <= 0f) { slot = i; break; }
            if (rip[i].z > oldest) { oldest = rip[i].z; slot = i; }
        }
        rip[slot] = new Vector4(x, fz, 0f, amp); ripS[slot] = shape;
    }

    float Flow() => rider != null && rider.running ? rider.eff : 0f;

    public void Step(float dt, Color tint)
    {
        if (rider == null) return;
        SetTint(trail, tint); SetTint(mist, tint); SetTint(carve, tint); SetTint(carveMist, tint); SetTint(clumps, tint);
        if (sparkle != null) { var sr = sparkle.GetComponent<ParticleSystemRenderer>(); if (sr.sharedMaterial != null) sr.sharedMaterial.SetColor("_BaseColor", tint * 2.6f); }   // past white: the bloom picks them out as glints
        // the foam trail off the tail, while he is on the water and moving
        bool onWater = rider.running && !rider.airborne && !rider.wipe && !rider.Submerged && (dive == null || !dive.On);
        if (onWater && tailOk)
        {
            // ---- the white water a board sheds, as real ones do: off the RAILS, not its middle ----
            // The water the hull pushes aside peels off both rails over the back third of the board
            // as a thin lacy sheet, low and drifting back, and settles behind as lace along each
            // side. Round puffs out of the centre of the tail read as smoke; this reads as water.
            float sp = Mathf.InverseLerp(14f, 50f, rider.eff), cv = Mathf.Abs(rider.carve);
            // the lace it leaves on the surface, along both rails
            trailAcc += dt * Mathf.Lerp(14f, 40f, sp);
            while (trailAcc >= 1f)
            {
                trailAcc -= 1f;
                float side = Random.value < 0.5f ? -1f : 1f;
                var at = tail + tailRight * side * tailHalfW * (0.8f + 0.3f * Random.value) + tailFwd * Random.value * 1.4f;   // along the back third of the rail, toward the nose
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(at.x, rider.waterY + 0.05f, at.z),
                    velocity = new Vector3(side * (0.3f + 0.6f * Random.value), 0f, rider.eff * 0.96f),
                    startSize = 0.45f + Random.value * 0.6f,
                    startLifetime = 1.2f + Random.value * 1.0f,
                    startColor = new Color(1f, 1f, 1f, 0.35f + Random.value * 0.25f),
                    rotation = Random.value * 360f
                };
                trail.Emit(ep, 1);
            }
            // the sheet off the rails: fine drops, low, peeling outward and back
            if (carve != null)
            {
                fizzAcc += dt * (20f + 50f * sp + 40f * cv);
                while (fizzAcc >= 1f)
                {
                    fizzAcc -= 1f;
                    float side = Random.value < 0.5f ? -1f : 1f;
                    var at = tail + tailRight * side * tailHalfW * 0.95f + tailFwd * Random.value * 1.6f;
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = new Vector3(at.x, rider.waterY + 0.04f, at.z),
                        velocity = new Vector3(side * (0.8f + 1.8f * Random.value), 0.7f + 1.6f * Random.value * (0.5f + sp), rider.eff * 0.85f + Random.value * 1.5f),
                        startSize = 0.03f + 0.06f * Random.value,
                        startLifetime = 0.3f + 0.35f * Random.value,
                        startColor = new Color(1f, 1f, 1f, 0.6f + 0.3f * Random.value)
                    };
                    carve.Emit(ep, 1);
                }
            }
        }
        else { trailAcc = 0f; fizzAcc = 0f; }
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
        Ride(trail);
        // the rings of wave age and die away; the sea reads them every frame
        for (int i = 0; i < rip.Length; i++)
            if (rip[i].w > 0f) { rip[i].z += dt; if (rip[i].z > 1.6f) rip[i] = Vector4.zero; }
        Shader.SetGlobalVectorArray(RipId, rip); Shader.SetGlobalVectorArray(RipSId, ripS);
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
    static Texture2D foamTex, lensTex, clumpTex;

    static float H21(float a, float b) { float s = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
    static float Vn(float u, float v)
    {
        float i = Mathf.Floor(u), j = Mathf.Floor(v), f = u - i, g = v - j, F = f * f * (3 - 2 * f), G = g * g * (3 - 2 * g);
        return (H21(i, j) * (1 - F) + H21(i + 1, j) * F) * (1 - G) + (H21(i, j + 1) * (1 - F) + H21(i + 1, j + 1) * F) * G;
    }

    // four torn whitewater clumps in a 2x2 sheet: a handful of overlapping soft lumps eroded by
    // noise, holed through in places, shaded as a lump lit from above (worked out in a browser
    // mock of this same code before it went in)
    static Texture2D FoamClumpTex()
    {
        if (clumpTex != null) return clumpTex;
        const int N = 128;
        clumpTex = new Texture2D(N * 2, N * 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "whitewater" };
        var px = new Color32[N * N * 4];
        for (int variant = 0; variant < 4; variant++)
        {
            long sd = variant * 17 + 3; System.Func<float> R = () => { sd = (sd * 16807) % 2147483647; return sd / 2147483647f; };
            var L = new Vector3[11];
            for (int k = 0; k < L.Length; k++) { float a = R() * 6.283f, rr = Mathf.Sqrt(R()) * 0.28f; L[k] = new Vector3(0.5f + Mathf.Cos(a) * rr, 0.52f + Mathf.Sin(a) * rr * 0.75f, 0.10f + R() * 0.12f); }
            int ox = (variant % 2) * N, oy = (variant / 2) * N;
            for (int yy = 0; yy < N; yy++) for (int xx = 0; xx < N; xx++)
            {
                float u = (xx + 0.5f) / N, v = (yy + 0.5f) / N, dens = 0f, hy = 0f, top = 0f;
                foreach (var l in L)
                {
                    float dx = (u - l.x) / l.z, dy = (v - l.y) / l.z, q = 1f - (dx * dx + dy * dy);
                    if (q > 0f) { float w = q * q; dens += w; hy += dy * w; top += Mathf.Sqrt(q) * w; }
                }
                float n1 = Vn(u * 6 + variant * 3, v * 6), n2 = Vn(u * 13, v * 13 + variant), n3 = Vn(u * 26 + variant, v * 26);
                float al = Mathf.Clamp01((dens - 0.28f + (n1 - 0.5f) * 0.50f + (n2 - 0.5f) * 0.30f + (n3 - 0.5f) * 0.12f) * 2.0f);
                al *= 0.62f + 0.38f * Mathf.Clamp01((n2 - 0.25f) * 2.0f);
                // the texture's v runs up where the mock's y ran down: the lump's top is lit
                float lit = Mathf.Clamp01(0.58f + hy / (dens + 1e-3f) * 0.40f + top / (dens + 1e-3f) * 0.22f + (n2 - 0.5f) * 0.14f + (n3 - 0.5f) * 0.06f);
                px[(oy + yy) * N * 2 + ox + xx] = new Color32((byte)(255 * (0.70f + 0.30f * lit)), (byte)(255 * (0.74f + 0.26f * lit)), (byte)(255 * (0.80f + 0.20f * lit)), (byte)(255 * al));
            }
        }
        clumpTex.SetPixels32(px); clumpTex.Apply(true, true);
        return clumpTex;
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
