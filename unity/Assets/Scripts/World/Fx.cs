// The water's small effects: every splash (Hit: a crown of drops, a mist, foam left on the water
// and a real ring of wave running out through the sea's own surface), the white water a board
// throws, mist hanging in the barrel while the ring has him, and drops of water on the lens after
// a wipeout or when the camera comes back up out of a dive. All particles, all soft (they fade
// where they meet the sea instead of cutting through it), and all carried toward the camera at the
// run's speed, because the world scrolls past the rider rather than him moving through it.
//
// What a carve throws, as real ones do (worked out in a browser mock of this same code):
//   - whitewater shed off the back of the rails onto the water: lit, cloud-like puffs (Surf/Puff,
//     its atlas baked by unity/Tools~/cloudbake --puffs), many and overlapping, soft-edged so they
//     run together, left behind with the water as two lines along the rails' tracks (the arms of
//     the V, the outside one heavier and opening wider), swelling and spreading flat as they go;
//   - off it, a curtain of spray: fine drops drawn as thin streaks along their own flight, every
//     one thrown on one fan-shaped sheet (out-and-up at one edge, back-and-up at the other) so they
//     make a curtain rather than a cloud of dots, arcing under gravity and blown back by the air
//     the run carries past; a soft mist through the same sheet; a few glints. Drops and mist are
//     lit by the sky about them and glow when the lens looks toward the sun, as water does.
// Behind the board, a light froth of foam lace lies on the water along each rail, over the foam
// the sea itself draws there (Surf/Ocean). Nothing is drawn as a flat lit sprite on the water or
// as a hard-edged clump: from the chase camera those read as scattered paper flakes.
// Without the puff shader or its atlas the whitewater falls back to soft round blobs.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Fx : MonoBehaviour
{
    public BoardController rider;
    public SetWave setWave;
    public Dive dive;
    ParticleSystem trail, mist, carve, carveMist, sparkle;
    public Vector3 tail, tailRight = Vector3.right, tailFwd = Vector3.forward; public float tailHalfW = 0.55f; public bool tailOk;   // the board's tail at the water, its axes and half width, set each frame by Bootstrap
    float fizzAcc;
    readonly Vector4[] rip = new Vector4[4];        // the rings of wave in the sea: x, field z, age, height (Surf/Ocean _Rip)
    readonly Vector4[] ripS = new Vector4[4];       // and the shape each starts from: half width, half length, sin and cos of its heading (_RipS)
    static readonly int RipId = Shader.PropertyToID("_Rip"), RipSId = Shader.PropertyToID("_RipS");
    static readonly Vector4 Round = new Vector4(0.35f, 0.35f, 0f, 1f);   // anything that is not the board: a small round splash
    RectTransform lens; RawImage[] drops; float[] dropT, dropV; float lensT;
    bool wasUnder; float trailAcc, mistAcc;
    ParticleSystem puffs;                           // the whitewater at the rail and up the curtain's root
    Material puffMat, puffFallback; bool fastFx, softSet;
    float frothAcc, rootAcc, dropAcc, sheetAcc; readonly float[] wakeAcc = new float[2];
    AnimationCurve puffSize;                        // their swell over a life, kept to read sizes back each frame
    readonly List<Vector4> puffData = new List<Vector4>();
    ParticleSystem.Particle[] pbuf;
    DayCycle day;
    const int PuffMax = 300, PuffMaxFast = 130, DropMax = 1600, DropMaxFast = 700, MistMax = 260, MistMaxFast = 120, LaceMax = 500, LaceMaxFast = 260;
    // the drag of the air on a drop and on mist, toward the air's own drift (the run's speed, back
    // past him); the same numbers go to the systems' drag and to the push that stands in for the wind
    const float DropDrag = 0.9f, MistDrag = 2.4f;
    // What a carve throws is drawn by the sea itself now: the V of foam along both rails (Surf/Ocean).
    // Every particle a carve used to add on top of it is off -- the whitewater puffs and the lace
    // patches lay on the water as round blobs, the spray curtain's drops read as streaks of sparks,
    // its mist hung on the side of the board as a white smear, and the glints, rail fizz and root
    // puffs were more of the same. Each is one flag here, so any can be brought back.
    static readonly bool CarveParticles = false, RailLace = false;
    static readonly bool Glints = false, RailFizz = false, RootPuffs = false, ExtraLace = false;   // readonly, not const: no unreachable-code warnings

    public void Build()
    {
        trail = Make("FoamTrail", FoamTex(), LaceMax, true);
        mist = Make("BarrelMist", Bootstrap.DropTex(), 260, false);
        carve = MakeCarve();
        carveMist = MakeCarveMist();
        sparkle = MakeSparkle();
        puffMat = MakePuffMaterial();
        puffSize = new AnimationCurve(new Keyframe(0, 0.45f), new Keyframe(0.35f, 0.8f), new Keyframe(1, 1f));
        puffs = MakePuffs("Whitewater", PuffMax, puffSize,
            new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.8f, 0.45f), new GradientAlphaKey(0f, 1) });
        SetFast(SaveData.S.fastFx);
        BuildLens();
    }

    /// Settings > Graphics: Fast keeps fewer of everything a carve throws alive, and throws fewer
    public void SetFast(bool fast)
    {
        fastFx = fast;
        if (puffs != null) { var m = puffs.main; m.maxParticles = fast ? PuffMaxFast : PuffMax; }
        if (carve != null) { var m = carve.main; m.maxParticles = fast ? DropMaxFast : DropMax; }
        if (carveMist != null) { var m = carveMist.main; m.maxParticles = fast ? MistMaxFast : MistMax; }
        if (trail != null) { var m = trail.main; m.maxParticles = fast ? LaceMaxFast : LaceMax; }
    }

    // ---- the whitewater puffs ----
    // the lit-cloud material every puff shares; null when the shader or its baked atlas is missing
    static Material MakePuffMaterial()
    {
        var sh = Shader.Find("Surf/Puff");
        var pos = Resources.Load<Texture2D>("Clouds/puff_pos");
        var neg = Resources.Load<Texture2D>("Clouds/puff_neg");
        if (sh == null || !sh.isSupported || pos == null || neg == null) return null;
        var m = new Material(sh) { name = "whitewater" };
        m.SetTexture("_PuffPos", pos); m.SetTexture("_PuffNeg", neg);
        m.SetVector("_Albedo", new Vector4(0.93f, 0.96f, 1.0f, 1f));
        // x: soft against the solid world over 0.6 ft; y, z: gone half a foot from the lens,
        // whole by two; w: the forward-scatter glow toward the sun
        m.SetVector("_Fade", new Vector4(0.6f, 0.5f, 1.5f, 1f));
        // fading evenly rather than eroding into torn pieces; the outline mostly from a mip two
        // steps blurrier and rounded off well inside the tile, so the puffs melt into one mass;
        // the shading evened out a good way toward all-round light, as whitewater's is
        // spray, not cloud: lit nearly evenly from all round (water scatters light; a cloud's dark
        // underside on each puff made a row of little cumulus), and its thin parts wisping away as it fades
        m.SetVector("_Edge", new Vector4(0.45f, 2f, 0.8f, 1f));
        m.SetFloat("_Lift", 0.9f);
        m.SetVector("_Tiles", new Vector4(4f, 2f, 0f, 0f));
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    // A billboard per puff, turned square to the lens (the shader lights it in the camera's own
    // frame, so it is never rotated or flipped), wider than tall, one of the eight tiles of the
    // atlas at random, held for its life. Moved by Settle each frame rather than by the system's own
    // gravity and drag, since the drag that matters is toward the water's speed, not toward
    // standing still. Without the lit shader, the same puffs as soft round blobs.
    ParticleSystem MakePuffs(string name, int max, AnimationCurve size, GradientAlphaKey[] alpha)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        // seen by the lens, not by the sea's mirror: that camera keeps no depth to soften them against
        go.layer = PlanarReflection.SkyLayer;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = max; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0f;
        main.startRotation = 0f; main.startSize3D = true;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, size);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, alpha);
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.View;
        r.sortMode = ParticleSystemSortMode.Distance; r.maxParticleSize = 0.6f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (puffMat != null)
        {
            var ts = ps.textureSheetAnimation; ts.enabled = true; ts.mode = ParticleSystemAnimationMode.Grid;
            ts.numTilesX = 4; ts.numTilesY = 2; ts.animation = ParticleSystemAnimationType.WholeSheet; ts.cycleCount = 1;
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);   // a random tile per puff, held for its life (normalised over the sheet)
            r.material = puffMat;
            // custom1.xy: the sea's height under the puff and the band over it the puff thins out through
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV, ParticleSystemVertexStream.Custom1XY });
        }
        else
        {
            puffFallback = Bootstrap.SoftParticles(Bootstrap.DropTex());
            if (puffFallback == null) { Destroy(go); return null; }
            Soften(puffFallback, 0.6f); r.material = puffFallback;
        }
        return ps;
    }

    // Each frame, every puff: those in the air slow toward the drift of the air the run carries
    // past (two thirds of the water's speed), fall, and settle onto the sea, each told the sea's
    // height under it so it thins out into the water rather than being cut off by it; once on the
    // water a puff spreads wider and lower, foam running out flat, and goes with the water.
    void Settle(ParticleSystem ps, AnimationCurve size, float dt)
    {
        if (ps == null || rider == null) return;
        int n = ps.particleCount; if (n == 0) return;
        if (pbuf == null || pbuf.Length < ps.main.maxParticles) pbuf = new ParticleSystem.Particle[PuffMax];
        n = ps.GetParticles(pbuf);
        puffData.Clear();
        float flow = Flow(), t = rider.t, fz0 = WaveField.Scroll;
        float drag = Mathf.Exp(-1.3f * dt), lift = Mathf.Exp(-1.2f * dt), land = Mathf.Exp(-4f * dt);
        float spread = Mathf.Exp(0.6f * dt), squash = Mathf.Exp(-1.6f * dt);
        for (int i = 0; i < n; i++)
        {
            var q = pbuf[i];
            float f = 1f - q.remainingLifetime / Mathf.Max(1e-3f, q.startLifetime);
            Vector3 s3 = q.startSize3D;
            float h = s3.y * size.Evaluate(f);
            Vector3 p = q.position, v = q.velocity;
            float sea = WaveField.Height(p.x, fz0 + p.z, t);
            // low over the sea a puff is still in the water's skin and goes with it; higher, the air's
            float with = p.y - sea < 1.2f ? flow : flow * 0.65f;
            v.x *= drag; v.z = with + (v.z - with) * drag;
            v.y = v.y * lift - 7f * dt;
            float rest = sea + h * 0.2f;
            if (p.y <= rest && v.y <= 0f)
            {
                p.y = rest; v.y = 0f; v.x *= land; v.z = flow + (v.z - flow) * land;
                s3.x *= spread; s3.y *= squash; q.startSize3D = s3;
                // and melts into it, into the churned foam the sea draws there, rather than lying on it as a blob
                q.remainingLifetime = Mathf.Min(q.remainingLifetime, 0.15f);
            }
            q.position = p; q.velocity = v; pbuf[i] = q;
            puffData.Add(new Vector4(sea, h * 0.32f, 0f, 0f));
        }
        ps.SetParticles(pbuf, n);
        ps.SetCustomParticleData(puffData, ParticleSystemCustomData.Custom1);
    }

    // soft against the board and the rider only where the lens keeps a depth texture to read
    void PuffSoft()
    {
        if (softSet || puffMat == null) return;
        var cam = Camera.main; if (cam == null) return;
        softSet = true;
        // (the getter falls back to the pipeline's own setting when the camera does not say)
        var ud = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        bool depth = ud != null && ud.requiresDepthTexture;
        if (depth) puffMat.EnableKeyword("_PUFF_SOFT"); else puffMat.DisableKeyword("_PUFF_SOFT");
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

    // Drag toward the air rather than toward standing still: linear drag on the velocity, and a
    // steady push back past him of drag times the run's speed (set each frame by Step), which
    // together bring anything thrown up to the air's own drift
    static void AirDrag(ParticleSystem ps, float drag)
    {
        var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = drag; lv.limit = 1000f;
        lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;
        var fo = ps.forceOverLifetime; fo.enabled = true; fo.space = ParticleSystemSimulationSpace.World;
        fo.x = new ParticleSystem.MinMaxCurve(0f); fo.y = new ParticleSystem.MinMaxCurve(0f); fo.z = new ParticleSystem.MinMaxCurve(0f);
    }

    static void Wind(ParticleSystem ps, float drag, float flow)
    {
        if (ps == null) return;
        var fo = ps.forceOverLifetime; fo.z = new ParticleSystem.MinMaxCurve(drag * flow);
    }

    // The drops: every splash's, the rails' fizz and the carve's curtain. Fine streaks, each drawn
    // out along its own flight (a camera's shutter sees a flying drop as a short line, and a sheet
    // of them as a curtain), fading out before they reach the lens.
    ParticleSystem MakeCarve()
    {
        var go = new GameObject("CarveSpray"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = DropMax; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.85f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        AirDrag(ps, DropDrag);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.8f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.7f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0.95f, 0), new GradientAlphaKey(0.8f, 0.45f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;   // round droplets: a stretched bright streak reads as a spark
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = Bootstrap.SoftParticles(Bootstrap.DropTex());
        if (m != null) { Soften(m, 0.3f); NearFade(m, 3f, 9f); r.material = m; }
        return ps;
    }

    // the shape of a carve's curtain: `fan` 0..1 runs across it from its outer edge (more out) to its
    // back edge (more back, toward the lens), always steeply up. Drops, mist and the white root all
    // fly along it, so together they make one sheet.
    Vector3 FanDir(float fan, float outward, Vector3 fwd)
    {
        float elev = 1.0f + 0.3f * fan, outW = 0.95f - 0.55f * fan, backW = 0.35f + 0.6f * fan;
        float hl = Mathf.Sqrt(outW * outW + backW * backW), hz = Mathf.Cos(elev);
        Vector3 across = new Vector3(outward * outW / hl, 0f, 0f) - fwd * (backW / hl);
        return new Vector3(across.x * hz, Mathf.Sin(elev), across.z * hz);
    }

    /// a carve, this frame: `tail` is the outside rail at the tail, `outward` the side the spray goes,
    /// k how hard he is turning (0..1)
    public void Carve(Vector3 tail, float outward, float k, float dt)
    {
        if (rider == null) return;
        if (!CarveParticles) { Spray(tail, outward, k, dt); return; }
        float flow = Flow(), sp = Mathf.InverseLerp(14f, 50f, flow);
        float power = k * (0.55f + 0.45f * sp);              // how much water the rail throws
        float reach = 0.4f + 0.6f * power;                   // and how high and far
        float rate = fastFx ? 0.5f : 1f;
        Vector3 fwd = tailFwd.sqrMagnitude > 0.5f ? tailFwd.normalized : Vector3.forward * -1f;   // toward the nose
        // ---- the whitewater: overlapping puffs shed off the back of the rails onto the water. Foam
        // stays with the water, and the water runs back past him at the run's speed, so what the
        // rails shed is left behind as two lines along their tracks -- the arms of the V, the
        // outside one the heavier and opening the wider -- never a heap riding along beside him ----
        if (puffs != null)
        {
            frothAcc += dt * rate * 170f * (0.35f + 0.65f * k) * (0.7f + 0.3f * sp);
            float kk = 0.4f + 0.6f * k;
            Vector3 rOut = tailRight.sqrMagnitude > 0.01f ? tailRight.normalized : Vector3.right;
            if (Mathf.Sign(rOut.x) != Mathf.Sign(outward)) rOut = -rOut;   // toward the outside of the turn
            float railGap = 2f * tailHalfW * 0.85f;                          // the outside rail to the inside one
            while (frothAcc >= 1f)
            {
                frothAcc -= 1f;
                bool inner = Random.value < 0.3f;
                Vector3 side = inner ? -rOut : rOut;
                float along = -0.3f + 1.3f * Random.value;                  // just off the tail, up the back of the rail
                float w = (0.9f + 0.7f * Random.value) * (0.7f + 0.45f * k) * (inner ? 0.75f : 1f) * (fastFx ? 1.25f : 1f);
                // the arms open outward as they run back, the outside one the more the harder he turns
                float drift = flow * (0.05f + 0.07f * Random.value) * (inner ? 0.7f : 1f + 0.8f * k);
                var at = (inner ? tail - rOut * railGap : tail) + fwd * along + side * (0.25f * Random.value);
                // spread through the frame: the water has carried each on since it left the rail
                at.z += flow * dt * Random.value;
                puffs.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(at.x, Mathf.Max(at.y, rider.waterY) + 0.05f + 0.1f * Random.value, at.z),
                    velocity = side * drift + new Vector3(0f, (0.3f + 0.8f * Random.value) * kk, flow),
                    startSize3D = new Vector3(w, w * (0.55f + 0.2f * Random.value), 1f),   // wider than tall: foam lying on the water
                    startLifetime = (inner ? 0.5f : 0.7f) + 0.6f * Random.value,
                    startColor = new Color(1f, 1f, 1f, (inner ? 0.6f : 0.85f) + 0.15f * Random.value),
                    rotation = 0f
                }, 1);
            }
            // and the curtain's white root: smaller, fainter puffs rising a little way up it, kept slow
            // and short-lived so none breaks away as a ball of its own; they thin out into its mist
            if (RootPuffs) rootAcc += dt * rate * 30f * Mathf.Pow(power, 1.2f);
            while (rootAcc >= 1f)
            {
                rootAcc -= 1f;
                float spd = (1.5f + 3.5f * Random.value) * reach, along = 1.4f * Random.value * Random.value;
                float w = (0.7f + 0.5f * Random.value) * (0.7f + 0.4f * power) * (fastFx ? 1.25f : 1f);
                puffs.Emit(new ParticleSystem.EmitParams
                {
                    position = tail + fwd * along + new Vector3(outward * 0.15f, 0.25f + 0.2f * Random.value, 0f),
                    velocity = FanDir(Random.value, outward, fwd) * spd + new Vector3(0f, 0f, flow * 0.45f),   // blown back along the trail
                    startSize3D = new Vector3(w, w, 1f),
                    startLifetime = 0.3f + 0.2f * Random.value,
                    startColor = new Color(1f, 1f, 1f, 0.5f + 0.2f * Random.value),
                    rotation = 0f
                }, 1);
            }
        }
        // ---- the curtain: fine drops, each on the fan at its own speed, a few heavier; glints ----
        if (carve != null)
        {
            dropAcc += dt * (fastFx ? 0.45f : 1f) * 750f * Mathf.Pow(power, 1.2f);
            while (dropAcc >= 1f)
            {
                dropAcc -= 1f;
                bool heavy = Random.value < 0.08f;
                float spd = (3.5f + 18.0f * Mathf.Pow(Random.value, 0.75f)) * reach * (heavy ? 1.2f : 1f);
                float along = 1.7f * Random.value * Random.value;
                var ep = new ParticleSystem.EmitParams
                {
                    position = tail + fwd * along + new Vector3(outward * Random.value * 0.15f, -0.01f + Random.value * 0.12f, 0f),
                    velocity = FanDir(Random.value + (Random.value - 0.5f) * 0.15f, outward, fwd) * spd + new Vector3(0f, 0f, flow * 0.06f),
                    startSize = heavy ? 0.04f + 0.03f * Random.value : 0.014f + 0.022f * Random.value,
                    startLifetime = 0.45f + 0.4f * Random.value + (heavy ? 0.2f : 0f),
                    startColor = new Color(1f, 1f, 1f, 0.35f + 0.25f * Random.value)
                };
                carve.Emit(ep, 1);
                if (Glints && sparkle != null && Random.value < 0.05f)
                    sparkle.Emit(new ParticleSystem.EmitParams
                    {
                        position = ep.position, velocity = ep.velocity * (0.9f + 0.2f * Random.value),
                        startSize = 0.035f + 0.04f * Random.value,
                        startLifetime = 0.3f + 0.4f * Random.value,
                        startColor = Color.white
                    }, 1);
            }
        }
        // ---- the mist the curtain is made of, close about its root ----
        if (carveMist != null)
        {
            sheetAcc += dt * rate * 70f * Mathf.Pow(power, 1.1f);
            while (sheetAcc >= 1f)
            {
                sheetAcc -= 1f;
                float spd = (2.5f + 9.0f * Random.value) * reach;
                carveMist.Emit(new ParticleSystem.EmitParams
                {
                    position = tail + new Vector3(outward * (0.1f + 0.3f * Random.value), 0.2f + 0.4f * Random.value * reach, 0f) + fwd * ((Random.value - 0.3f) * -1.0f),
                    velocity = FanDir(Random.value, outward, fwd) * spd + new Vector3(0f, 0f, flow * 0.08f),
                    startSize = (1.1f + 1.6f * Random.value) * (0.6f + 0.5f * power),
                    startLifetime = 0.45f + 0.4f * Random.value,
                    startColor = new Color(1f, 1f, 1f, (0.4f + 0.2f * Random.value) * (0.5f + 0.5f * power))
                }, 1);
            }
        }
    }

    // ---- the spray of a hard turn, as reference footage shows it ----
    // A dense white mass bursting off the tail, thick and lumpy like cloud: many lit puffs packed so
    // close they read as one body, flung up and out along the fan and blown back with the air, gone
    // before they come down (one that does reach the water melts into it at once, into the churned
    // arc the sea draws there, rather than lying on it as a blob). Round its edges, fine round
    // droplets, dots at the brightness of the sky, not streaks. Only on a hard turn: a gentle one
    // throws nothing, and the spray grows in with how hard he is turning.
    float sprayAcc, dotAcc;
    void Spray(Vector3 tail, float outward, float k, float dt)
    {
        float flow = Flow(), sp = Mathf.InverseLerp(14f, 50f, flow);
        float power = k * (0.55f + 0.45f * sp);
        float m = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, power));
        if (m <= 0.001f) { sprayAcc = dotAcc = 0f; return; }
        float reach = 0.5f + 0.5f * power, rate = fastFx ? 0.5f : 1f;
        Vector3 fwd = tailFwd.sqrMagnitude > 0.5f ? tailFwd.normalized : Vector3.forward * -1f;
        if (puffs != null)
        {
            sprayAcc += dt * rate * 380f * m;
            while (sprayAcc >= 1f)
            {
                sprayAcc -= 1f;
                // most of it in the middle of the fan, thinning to its edges
                float fan = Mathf.Clamp01(0.5f + (Random.value + Random.value - 1f) * 0.6f);
                float spd = (2f + 8f * Random.value) * reach;
                // mostly small, a few big: a mass with a ragged edge, not a row of even balls
                float rs = Random.value;
                float w = (0.45f + 1.1f * rs * rs) * (0.7f + 0.5f * m) * (fastFx ? 1.25f : 1f);
                var jit = Random.insideUnitSphere * 0.45f;
                puffs.Emit(new ParticleSystem.EmitParams
                {
                    position = tail + fwd * (-0.2f + 0.8f * Random.value) + jit + new Vector3(outward * 0.3f * Random.value, 0.15f + 0.3f * Random.value, flow * dt * Random.value),
                    velocity = FanDir(fan, outward, fwd) * spd + jit * 2f + new Vector3(0f, 0f, flow * 0.55f),
                    startSize3D = new Vector3(w, w * (0.85f + 0.2f * Random.value), 1f),
                    startLifetime = 0.3f + 0.25f * Random.value,
                    startColor = new Color(1f, 1f, 1f, 0.45f + 0.3f * Random.value),   // see-through: dense only where many overlap
                    rotation = 0f
                }, 1);
            }
        }
        if (carve != null)
        {
            dotAcc += dt * rate * 420f * m;
            while (dotAcc >= 1f)
            {
                dotAcc -= 1f;
                // thrown off the outside of the mass, faster and further than it goes
                float fan = Random.value;
                float spd = (6f + 10f * Random.value) * reach;
                carve.Emit(new ParticleSystem.EmitParams
                {
                    position = tail + fwd * (0.6f * Random.value) + new Vector3(outward * 0.3f * Random.value, 0.2f + 0.4f * Random.value, flow * dt * Random.value),
                    velocity = FanDir(fan, outward, fwd) * spd + new Vector3(0f, 0f, flow * 0.3f),
                    startSize = 0.035f + 0.045f * Random.value,
                    startLifetime = 0.5f + 0.4f * Random.value,
                    startColor = new Color(1f, 1f, 1f, 0.55f + 0.3f * Random.value)
                }, 1);
            }
        }
    }

    // The rails' froth: foam lace lying on the water along each rail, from just ahead of the tail,
    // over the band of foam the sea itself draws along the track that rail took (Surf/Ocean). Big,
    // soft, faint patches overlapping, each a network of bubble walls rather than a blot, laid
    // where the rail is and carried with the water, drifting outward so each band widens behind
    // (the outside rail of a carve the more). Stronger with speed and on the outside of a carve, a
    // slow noise per rail varying it so it is never a regular chain.
    void RailPuffs(float dt)
    {
        if (trail == null || !RailLace) return;
        float flow = Flow(), sp = Mathf.InverseLerp(14f, 50f, flow), cv = Mathf.Abs(rider.carve), outS = -Mathf.Sign(rider.carve);
        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1f : 1f;
            Vector3 rail = tailRight * side;
            bool outer = cv > 0.05f && Mathf.Sign(rail.x) == outS;
            float str = (0.5f + 0.8f * sp) * (1f + cv * (outer ? 1.2f : -0.3f));
            float gate = 0.6f + 0.7f * Mathf.PerlinNoise(Time.time * 1.9f, 20f + side * 7.3f);
            wakeAcc[s] += dt * flow * (fastFx ? 0.5f : 0.9f) * str * gate;
            while (wakeAcc[s] >= 1f)
            {
                wakeAcc[s] -= 0.4f + 1.2f * Random.value;          // uneven spacing
                float jw = tailHalfW * (0.85f + 0.2f * Random.value) + 0.35f * Random.value * Random.value, ja = Random.value * 0.8f - 0.1f;
                // when in the frame it left the rail: the water has carried it on since, so a
                // frame's worth do not all start in one clump
                var at = tail + rail * jw + tailFwd * ja + new Vector3(0f, 0f, flow * dt * Random.value);
                float drift = flow * (0.02f + 0.08f * Random.value) * (outer ? 1f + 1.2f * cv : 1f - 0.4f * cv);
                trail.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(at.x, rider.waterY + 0.05f, at.z),
                    velocity = new Vector3(rail.x * drift, 0f, flow),
                    startSize = (2.4f + 2.0f * Random.value) * (fastFx ? 1.2f : 1f),
                    startLifetime = Mathf.Clamp((18f + 17f * Random.value) / Mathf.Max(flow, 8f), 0.4f, 3f),
                    startColor = new Color(1f, 1f, 1f, (0.35f + 0.25f * Random.value) * Mathf.Clamp01(0.6f + 0.4f * str)),
                    rotation = Random.value * 360f
                }, 1);
            }
        }
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
        AirDrag(ps, DropDrag);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;   // points of light, not streaks
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = Bootstrap.SoftParticles(Bootstrap.DropTex());
        if (m != null) { m.SetColor("_BaseColor", new Color(2.6f, 2.5f, 2.3f, 1f)); NearFade(m, 3f, 9f); r.material = m; }
        return ps;
    }

    // the mist in a carve's curtain (and a splash's): soft, swelling, blown back and gone quickly,
    // and gone before it reaches the lens rather than passing over it as a grey blot
    ParticleSystem MakeCarveMist()
    {
        var go = new GameObject("CarveMist"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = MistMax; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.12f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        AirDrag(ps, MistDrag);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.45f), new Keyframe(1, 1.8f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.45f, 0.5f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = Bootstrap.SoftParticles(Bootstrap.DropTex());
        if (m != null) { Soften(m, 0.8f); NearFade(m, 4f, 12f); r.material = m; }
        return ps;
    }

    // The light on the drops and the mist: what a cloud of water drops shows is the sky round it
    // (the horizon's colour, as bright as the day is) and, when the lens looks toward the sun, the
    // sun's own glow many times over, since water scatters light mostly forward. Set as linear
    // light and handed to the materials, which take colours as sRGB.
    void SprayLight(Color tint)
    {
        if (day == null) day = FindFirstObjectByType<DayCycle>();
        Color drop = tint.linear, haze = tint.linear; float hazeA = 0.7f;
        if (day != null)
        {
            Color hz = new Color(day.skyHz.x, day.skyHz.y, day.skyHz.z, 1f).linear, glow = day.glow.linear;
            float sky = 0.35f + 0.65f * day.daylight, back = 0f;
            var cam = Camera.main;
            if (cam != null)
            {
                float into = Mathf.Clamp01(Vector3.Dot(cam.transform.forward, day.sunDir));
                back = into * into * into * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.12f, day.sunDir.y));
            }
            drop = hz * (1.1f * sky) + glow * (1.2f * back);
            // never past white: brighter, and the bloom made sparks of them
            float dm = Mathf.Max(drop.r, Mathf.Max(drop.g, drop.b)); if (dm > 1.0f) drop /= dm;
            haze = hz * (0.95f * (0.6f + 0.4f * day.daylight)) + glow * (1.4f * back);   // mist sees the whole sky, never as dim as the drops
            hazeA = Mathf.Min(1f, 0.7f + 0.5f * back);
        }
        Color glint = (drop * 2.6f).gamma;      // past white: the bloom picks them out as glints
        drop = drop.gamma; haze = haze.gamma;
        drop.a = 1f; haze.a = hazeA; glint.a = 1f;
        SetTint(carve, drop);
        SetTint(carveMist, haze);
        SetTint(sparkle, glint);
    }

    // URP's own fade toward the lens, on a particle material: gone nearer than `near`, whole by `far`
    public static void NearFade(Material m, float near, float far)
    {
        m.EnableKeyword("_FADING_ON");
        m.SetFloat("_CameraFadingEnabled", 1f);
        m.SetFloat("_CameraNearFadeDistance", near); m.SetFloat("_CameraFarFadeDistance", far);
        m.SetVector("_CameraFadeParams", new Vector4(near, 1f / Mathf.Max(0.01f, far - near), 0f, 0f));
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
            // and the ring of wave -- but not while the jet ski's wake is up: the launch off it and
            // the landing after both splash, and their rings sat on the face of that ridge as a set
            // of little circles round the board
            if (WaveField.SkiA <= 0.02f)
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
        SetTint(trail, tint); SetTint(mist, tint);
        if (puffMat == null) SetTint(puffs, tint);          // the soft stand-ins, when the lit puffs are missing
        SprayLight(tint);
        // the air runs back past him at the run's speed: what is thrown up drifts back with it
        float flow = Flow();
        Wind(carve, DropDrag, flow); Wind(sparkle, DropDrag, flow); Wind(carveMist, MistDrag, flow);
        // the foam trail off the tail, while he is on the water and moving
        bool onWater = rider.running && !rider.airborne && !rider.wipe && !rider.Submerged && (dive == null || !dive.On);
        if (onWater && tailOk)
        {
            // ---- the white water a board sheds, as real ones do: off the RAILS, not its middle ----
            // The water the hull pushes aside peels off both rails over the back third of the board
            // as a thin sheet, low and drifting back, and settles behind as lace along each side.
            float sp = Mathf.InverseLerp(14f, 50f, rider.eff), cv = Mathf.Abs(rider.carve);
            // the lace it leaves on the surface, along both rails: few, big, faint and overlapping
            if (ExtraLace) trailAcc += dt * Mathf.Lerp(8f, 22f, sp) * (fastFx ? 0.6f : 1f);
            while (trailAcc >= 1f)
            {
                trailAcc -= 1f;
                float side = Random.value < 0.5f ? -1f : 1f;
                var at = tail + tailRight * side * tailHalfW * (0.8f + 0.3f * Random.value) + tailFwd * Random.value * 1.4f;   // along the back third of the rail, toward the nose
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(at.x, rider.waterY + 0.05f, at.z),
                    velocity = new Vector3(side * (0.3f + 0.6f * Random.value), 0f, rider.eff * 0.96f),
                    startSize = 1.6f + Random.value * 1.4f,
                    startLifetime = 1.2f + Random.value * 1.0f,
                    startColor = new Color(1f, 1f, 1f, 0.25f + Random.value * 0.2f),
                    rotation = Random.value * 360f
                };
                trail.Emit(ep, 1);
            }
            // the sheet off the rails: a few fine drops, low, peeling outward and back
            if (RailFizz && carve != null)
            {
                fizzAcc += dt * (12f + 30f * sp + 30f * cv) * (fastFx ? 0.6f : 1f);
                while (fizzAcc >= 1f)
                {
                    fizzAcc -= 1f;
                    float side = Random.value < 0.5f ? -1f : 1f;
                    var at = tail + tailRight * side * tailHalfW * 0.95f + tailFwd * Random.value * 1.6f;
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = new Vector3(at.x, rider.waterY + 0.04f, at.z),
                        velocity = new Vector3(side * (0.8f + 1.8f * Random.value), 0.7f + 1.6f * Random.value * (0.5f + sp), rider.eff * 0.85f + Random.value * 1.5f),
                        startSize = 0.02f + 0.03f * Random.value,
                        startLifetime = 0.3f + 0.35f * Random.value,
                        startColor = new Color(1f, 1f, 1f, 0.3f + 0.3f * Random.value)
                    };
                    carve.Emit(ep, 1);
                }
            }
        }
        // the rails' foam itself is drawn into the sea (Surf/Ocean, along the wake's path); this is
        // only a light froth of lace over it
        if (onWater && tailOk) RailPuffs(dt);
        else { trailAcc = 0f; fizzAcc = 0f; wakeAcc[0] = wakeAcc[1] = 0f; }
        if (!onWater || !tailOk || Mathf.Abs(rider.carve) <= 0.22f) frothAcc = rootAcc = dropAcc = sheetAcc = 0f;   // a carve starts fresh
        if (puffMat != null) { SkyKit.LightPuffs(puffMat); PuffSoft(); }
        Settle(puffs, puffSize, dt);
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
    static Texture2D foamTex, lensTex;

    static float H21(float a, float b) { float s = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
    static float Vn(float u, float v)
    {
        float i = Mathf.Floor(u), j = Mathf.Floor(v), f = u - i, g = v - j, F = f * f * (3 - 2 * f), G = g * g * (3 - 2 * g);
        return (H21(i, j) * (1 - F) + H21(i + 1, j) * F) * (1 - G) + (H21(i, j + 1) * (1 - F) + H21(i + 1, j + 1) * F) * G;
    }

    // a patch of foam lace: the walls between bubbles, a network of bright ridges (where a noise
    // crosses its middle), warped a little so the cells are uneven, over a soft round body that
    // dies away well inside the square; never torn right through, so a patch is a veil of froth
    // rather than a blot with holes in it, and many of them overlap into one
    static Texture2D FoamTex()
    {
        if (foamTex != null) return foamTex;
        const int N = 128; foamTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "foam lace" };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Min(1f, Mathf.Sqrt(dx * dx + dy * dy) * 2f);
            float body = Mathf.Exp(-r * r * 3.2f) * (1f - r * r);
            float wu = u + Vn(u * 3f + 4.1f, v * 3f + 1.3f) * 0.18f, wv = v + Vn(u * 3f + 9.7f, v * 3f + 6.2f) * 0.18f;
            float n1 = Vn(wu * 7f + 2.3f, wv * 7f + 5.9f), n2 = Vn(wu * 16f + 3.1f, wv * 16f + 7.7f);
            float ridge = Mathf.Pow(1f - Mathf.Abs(n1 * 2f - 1f), 2.5f) * 0.65f + Mathf.Pow(1f - Mathf.Abs(n2 * 2f - 1f), 3f) * 0.45f;
            float lace = Mathf.Clamp01(1.6f * (ridge - 0.25f)) * 0.8f + 0.2f;
            px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(body * lace) * 255f));
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
