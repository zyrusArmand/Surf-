// The jet ski, the web game's encounter number for number (web 29842-30405, the reset at
// 27106/27178, the set wave's guard at 32347, the jetpack's at 42014, the ride's wake rules at
// 45313-45327 and 45455-45457, the shader's ridge at 4255-4266, the foam limit at 46757).
//
// A ski crosses the lane a hundred-odd feet ahead, side on, and where it crosses your line it
// leaves a wake: not a prop on the water but a ridge IN it (WaveField.SkiWakeH, which the sea
// shader carries as _SkiA), a gaussian eighteen feet deep along the lane and eased off over
// sixty-four either side, that rolls in at you and swells the whole way. The board, the buoys
// and the ski itself ride it because the water under them is genuinely higher. When its crest
// reaches him he is thrown off the top of it: WAKE LAUNCH, +120, a long low-gravity hang
// (BoardController.wakeAir) with the landing held off for a beat while the ridge slides out
// from under him (wakeGrace).
//
//   CLOCK:  16 s into a run (26 after a revive), then 62-100 s between them, and never before
//           480 m. Not while the set wave is up at any stage (it waits at least 8 s after), not
//           while the island has him (3 s after), not under the water or on the launch.
//   CROSS:  from 36 ft off to one side, 104-126 ft ahead, at 15 ft/s across, drifting in at
//           0.35 of the run's speed. Gone 48 ft past him. JET SKI!
//   WAKE:   born 2 ft beyond the ski as it comes within 6 ft of his line (WAKE INCOMING!), rolls
//           in at 0.28 of the run's speed plus 7.5 ft/s, 22 s to live, from a 0.055 standing
//           bump to 21 ft on the square of how far it has come (all of it by 112 ft), fading
//           over its last 1.4 s, gone once 20 ft behind him.
//
// The web set wave does not start while the ski is out (SetWave.ski), nor does the jetpack float
// in (Dive.ski); nothing else waits for it, and it waits for nothing else -- the whirlpool, the
// saucer, the boat and the chute are not in either list there, so they are not here.
//
// Public API (Bootstrap owns the wiring): Reset() at Play/Menu, Revive() on a spare life, Step(dt)
// in the running branch after the ride has stepped, summon for the stage, On for the guards.
using System.Collections.Generic;
using UnityEngine;

public class JetSki : MonoBehaviour
{
    // ---- the web's numbers ----
    public const float FirstT = 16f, ReviveT = 26f, GapT = 62f, GapR = 38f, MinDist = 480f;
    public const float AfterSet = 8f, AfterIsle = 3f;
    public const float SpawnX = 36f, SpawnZ = -104f, SpawnZR = 22f, LeaveX = 48f;
    public const float Across = 15f, DriftK = 0.35f;
    public const float WakeAtX = 6f, WakeLife = 22f, WakeFade = 1.4f, WakeGrow = 112f, WakeTop = 21f, WakeEnd = 20f;
    public const float WakeRollK = 0.28f, WakeRoll0 = 7.5f;
    public const float LaunchVy = 9.6f, LaunchPop = 1.6f, LaunchGrace = 0.34f;   // web WAKE_LAUNCH_VY, the pop clear of the crest, wakeGrace
    public const int LaunchPts = 120;
    // ---- the ski's frame (web 29970-29973 and the fit at 30000-30021) ----
    // The web fits the model into the box of the ski it replaces (SKI_BOX: 5.46 long, its centre
    // 0.40 up and 0.42 toward the bow, in a frame scaled by 0.92), then turns it end for end about
    // the ski's own origin, which carries that centre to 0.42 toward the STERN. Kept as it lands.
    const float Scale = 0.92f, BoxLen = 5.4602f;
    static readonly Vector3 BoxC = new Vector3(0f, 0.4017f, 0.421f);
    // The pug driving it (web seatSkiCrew, SKI_CREW 0.34, SKI_FEET -0.10), measured off jetski.glb
    // the way the web measures it: down the centreline, the console is the tallest of 32 slices,
    // and he stands a fifth of the boat aft of it, on the deck there (0.60), 1.02 aft of centre.
    const float CrewK = 0.34f, CrewDeck = 0.6036f, CrewFeet = -0.10f, CrewZ = 1.018f;

    public BoardController rider;
    public SetWave setWave;                    // the ski waits out the set wave
    public Dive dive;                          // and the dive; and is muffled while he is under
    public Island island;                      // and the island fork
    public System.Action<Vector3, int> onSplash;
    public bool summon;                        // the stage button: bring it now

    // ---- public state (web skiT, skiOn, skiDir, skiWake) ----
    public float timer = FirstT;
    public bool On => on;                      // the web's skiOn: the set wave and the jetpack wait
    public bool WakeOn => wakeOn;
    public float x, z;
    public int dir = 1;

    bool on, wakeOn, wakeHit;
    float wakeZ, wakeZ0, wakeLifeT, wakeX;
    Transform root; GameObject glint; Material glintMat; GltfClips crewClips;
    ParticleSystem rooster, foamPs; float roosterAcc, foamAcc;

    // the churned strip it leaves (web bfPush / updateBoatFoam, 30502-30584): packets ride the sea
    struct Foam { public float x, z, age, vx, w, rot, spin; }
    const int FoamMax = 240; const float FoamLife = 3.0f, FoamR0 = 0.38f, FoamR1 = 1.95f;
    readonly List<Foam> foam = new List<Foam>();
    ParticleSystem.Particle[] foamOut;

    // =====================================================================================
    /// a new run (web 27106): nothing on the water, the first one sixteen seconds out
    public void Reset() { Clear(); timer = FirstT; summon = false; }
    /// a spare life (web 27178): the same, twenty-six seconds out
    public void Revive() { Clear(); timer = ReviveT; }

    void Clear()
    {
        on = false; wakeOn = false; wakeHit = false;
        WaveField.SkiA = 0f;
        if (root != null) root.gameObject.SetActive(false);
        if (glint != null) glint.SetActive(false);
        foam.Clear(); foamAcc = 0f; roosterAcc = 0f;
        if (foamPs != null) foamPs.Clear();
        if (rooster != null) rooster.Clear();
    }

    // web spawnJetSki
    void Spawn()
    {
        var r = rider;
        Build();
        on = true;
        dir = Random.value < 0.5f ? 1 : -1;
        // close enough to actually see it go past; the wake still has fifty feet to build
        x = r.px - dir * SpawnX; z = SpawnZ - Random.value * SpawnZR;
        root.gameObject.SetActive(true);
        wakeOn = false;
        r.Say("JET SKI!");
    }

    // =====================================================================================
    // web updateJetSki
    public void Step(float dt)
    {
        var r = rider; if (r == null) return;
        float eff = r.eff, now = Time.time;
        if (!on)
        {
            timer -= dt;
            // Not during the set wave, at any stage of it: its launch would fire while the ring
            // owns him. The clock keeps running and it comes once the wave is done.
            bool setUp = setWave != null && setWave.ph != SetWave.Phase.Idle;
            bool isle = island != null && island.On;
            bool under = dive != null && dive.Busy;
            if (setUp) timer = Mathf.Max(timer, AfterSet);
            if (isle) timer = Mathf.Max(timer, AfterIsle);
            if (((timer <= 0f && r.dist > MinDist) || summon) && !setUp && !isle && !under)
            {
                timer = GapT + Random.value * GapR; summon = false; Spawn();
            }
        }
        else
        {
            // rips across the field, slowly enough to be on screen long enough to see
            x += dir * Across * dt;
            z += eff * dt * DriftK;
            float sy = WaveField.Height(x, WaveField.Scroll + z, r.t);
            root.position = new Vector3(x, sy + 0.34f + Mathf.Sin(now * 9f) * 0.05f, z);
            // the web's Euler (XYZ): roll about its own length, turned to drive bow first (the bow
            // is its local -z), then -0.10 about the world's x. Both rolls end up about the line
            // it drives along, so what shows is a heel of about seven degrees toward the lens.
            float yaw = -dir * Mathf.PI * 0.5f, roll = -dir * 0.22f + Mathf.Sin(now * 7f) * 0.05f;
            root.rotation = Quaternion.AngleAxis(-0.10f * Mathf.Rad2Deg, Vector3.right)
                          * Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up)
                          * Quaternion.AngleAxis(roll * Mathf.Rad2Deg, Vector3.forward);
            Glint(sy, now);
            // a foam cloud behind it, the same stuff the boat lays (web 30316-30331)
            foamAcc += dt * 46f;
            int n = 0;
            while (foamAcc >= 1f && n++ < 8)
            {
                foamAcc -= 1f;
                float a = 1.5f + Random.value * Random.value * 34f;          // astern of it, packed near it
                if (foam.Count >= FoamMax) continue;
                foam.Add(new Foam
                {
                    x = x - dir * a * 0.10f + (Random.value * 2f - 1f) * (1.1f + a * 0.055f), z = z + 1.6f + a,
                    vx = (Random.value * 2f - 1f) * 0.55f, w = 0.72f * (0.88f + Random.value * 0.34f),
                    rot = Random.value * 360f, spin = (Random.value - 0.5f) * 0.5f * Mathf.Rad2Deg
                });
            }
            // the rooster tail: four drops a frame at the web's sixty (web 30332-30340)
            Rooster(sy, eff, dt);
            // once it has crossed in front of you, it leaves a wake wave rolling your way
            if (!wakeOn && Mathf.Abs(x - r.px) < WakeAtX)
            {
                wakeOn = true; wakeHit = false;
                wakeZ = wakeZ0 = z - 2f; wakeLifeT = WakeLife; wakeX = x;
                r.Say("WAKE INCOMING!");
            }
            // heard where it is, panned across and falling off with distance (web 30348-30359)
            if (r.running) Sfx.Ski(x - r.px, z - r.pz, dive != null && dive.Depth > 0.5f);
            if (Mathf.Abs(x - r.px) > LeaveX) { on = false; root.gameObject.SetActive(false); glint.SetActive(false); foamAcc = 0f; }
        }
        // the wake rolls in as a ridge in the ocean itself (web 30362-30404)
        if (wakeOn)
        {
            wakeLifeT -= dt;
            wakeZ += (eff * WakeRollK + WakeRoll0) * dt;       // a long roll in, so the wall has time to grow on you
            // from where it was born, squared: a small standing bump the moment he passes, then
            // swelling the whole way in
            float p = Mathf.Clamp01((wakeZ - wakeZ0) / WakeGrow);
            float grow = 0.055f + 0.945f * p * p;
            float fade = Mathf.Clamp01(wakeLifeT / WakeFade);
            WaveField.SkiA = WakeTop * grow * fade;
            WaveField.SkiZ = WaveField.Scroll + wakeZ;
            WaveField.SkiX = wakeX;
            // He leaves at the top and nowhere else: the crest reaching him is the peak, he is
            // travelling with the water there, so the launch only adds to what he has. Not while
            // something else owns his height (the barrel's ring, the wing, the dive, the island,
            // the helicopter's beats), which in the web is the barrel's guard and the tow's.
            bool owned = r.glideOn || r.diveOn || r.isleOn || r.heliPh >= 0 || (setWave != null && setWave.tubeRide);
            if (!wakeHit && wakeZ >= r.pz && !r.airborne && !owned && r.running)
            {
                wakeHit = true;
                r.vy = Mathf.Max(r.vy, LaunchVy);
                // he is standing on the crest: popped clear, and the landing test held off while
                // the ridge slides out from under him, or he "lands" the frame he takes off
                r.py = Mathf.Max(r.py, r.waterY + LaunchPop);
                r.wakeGrace = LaunchGrace;
                r.airborne = true; r.wakeAir = true;
                r.score += LaunchPts; r.boost = Mathf.Min(16f, r.boost + 4f);
                r.Say("WAKE LAUNCH! +" + LaunchPts);
                Sfx.Buzz(30);                                  // the web shakes the camera (shake 1.8)
                onSplash?.Invoke(new Vector3(r.px, r.py, r.pz), 26);
            }
            if (wakeLifeT <= 0f || wakeZ > WakeEnd) { wakeOn = false; WaveField.SkiA = 0f; }
        }
        else if (WaveField.SkiA > 0f) WaveField.SkiA = Mathf.Max(0f, WaveField.SkiA - dt * 3f);
        FoamStep(dt, eff);
    }

    // ---- the halo behind it (web skiGlint, 29975-29999 and 30304-30315) ----
    // Low sun off the water around the hull rather than a twinkle on it: set behind the hull so
    // the ski hides its middle, faced at the lens, sized off its distance so it holds the same
    // size on screen a hundred feet out or going by, and swelling slowly.
    void Glint(float sy, float now)
    {
        var cam = Camera.main;
        if (glint == null || cam == null) return;
        glint.SetActive(true);
        var gt = glint.transform;
        gt.position = new Vector3(x, sy + 0.95f, z - 1.8f);
        gt.rotation = cam.transform.rotation;
        float gd = Vector3.Distance(cam.transform.position, root.position);
        gt.localScale = Vector3.one * (gd * 0.30f * (0.94f + 0.10f * Mathf.Sin(now * 1.5f) + 0.045f * Mathf.Sin(now * 2.6f)));
        glintMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.58f + 0.14f * Mathf.Sin(now * 1.5f)));
    }

    // web 30332-30340: from just behind the jet nozzle, thrown back the way it came and up
    void Rooster(float sy, float eff, float dt)
    {
        if (rooster == null) return;
        roosterAcc += dt * 240f;
        int n = 0;
        while (roosterAcc >= 1f && n++ < 8)
        {
            roosterAcc -= 1f;
            var ep = new ParticleSystem.EmitParams
            {
                position = new Vector3(x - dir * 1.8f, sy + 0.2f, z + 0.4f),
                // the web also carries every drop toward the lens at 0.4 of the run's speed
                velocity = new Vector3(-dir * (3f + Random.value * 5f), 3f + Random.value * 6f, 1.5f + Random.value * 3f + eff * 0.4f),
                startSize = 1.1f + Random.value * 1.6f,
                startLifetime = 0.6f + Random.value * 0.4f,
                startColor = Color.white
            };
            rooster.Emit(ep, 1);
        }
        if (n >= 8) roosterAcc = 0f;
    }

    // the foam packets: carried at the run's speed, lying on the sea, opening fast and thinning
    // out over three seconds, and gone before they reach the lens (web updateBoatFoam)
    void FoamStep(float dt, float eff)
    {
        if (foamPs == null) return;
        var cam = Camera.main; var r = rider;
        if (foamOut == null) foamOut = new ParticleSystem.Particle[FoamMax];
        int n = 0;
        for (int i = foam.Count - 1; i >= 0; i--)
        {
            var p = foam[i];
            p.age += dt;
            float k = p.age / FoamLife;
            if (k >= 1f) { foam.RemoveAt(i); continue; }
            p.x += p.vx * dt; p.z += eff * dt;
            foam[i] = p;
            var at = new Vector3(p.x, WaveField.Height(p.x, WaveField.Scroll + p.z, r.t) + 0.26f, p.z);
            float a = Mathf.Min(1f, k * 8f) * Mathf.Pow(1f - k, 1.30f) * 0.95f;
            if (cam != null) a *= Mathf.Clamp01((Vector3.Distance(cam.transform.position, at) - 20f) / 20f);
            float size = (FoamR0 + (FoamR1 - FoamR0) * Mathf.Pow(k, 0.58f)) * p.w;
            if (n >= foamOut.Length) continue;
            foamOut[n++] = new ParticleSystem.Particle
            {
                position = at, velocity = Vector3.zero, startSize = size,
                rotation = p.rot + p.spin * p.age,
                startColor = new Color(1f, 1f, 1f, a * 0.70f),
                remainingLifetime = 10f, startLifetime = 10f
            };
        }
        foamPs.SetParticles(foamOut, n);
    }

    // =====================================================================================
    void Build()
    {
        if (root != null) return;
        root = new GameObject("JetSki").transform;
        root.localScale = Vector3.one * Scale;
        BuildHull();
        BuildCrew();
        root.gameObject.SetActive(false);

        // a unit quad of its own, white at every corner: the particle shader it is drawn with
        // multiplies by the vertex colour, which the stock quad does not carry
        glint = new GameObject("SkiGlint", typeof(MeshFilter), typeof(MeshRenderer));
        var qm = new Mesh { name = "skiGlint" };
        qm.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        qm.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        qm.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        qm.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        qm.RecalculateNormals(); qm.RecalculateBounds();
        glint.GetComponent<MeshFilter>().sharedMesh = qm;
        var gr = glint.GetComponent<Renderer>();
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; gr.receiveShadows = false;
        glintMat = Bootstrap.SoftParticles(Gradient(160, new[] {
            new Vector2(0.00f, 0f), new Vector2(0.16f, 1f), new Vector2(0.36f, 2f), new Vector2(0.62f, 3f), new Vector2(0.84f, 4f), new Vector2(1.00f, 5f) },
            new[] { new Color(1f, 0.988f, 0.933f, 0.80f), new Color(1f, 0.933f, 0.769f, 0.46f), new Color(1f, 0.839f, 0.580f, 0.20f),
                    new Color(1f, 0.769f, 0.471f, 0.070f), new Color(1f, 0.722f, 0.431f, 0.018f), new Color(1f, 0.706f, 0.412f, 0f) }, "skiGlint"));
        if (glintMat != null)
        {
            // added, as light is: the web's AdditiveBlending
            glintMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glintMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (glintMat.HasProperty("_Cull")) glintMat.SetFloat("_Cull", 0f);
            Fx.Soften(glintMat, 1.5f);               // and no hard line where the sea cuts through it
            gr.sharedMaterial = glintMat;
        }
        glint.SetActive(false);

        rooster = MakeRooster();
        foamPs = MakeFoam();
    }

    void BuildHull()
    {
        var fit = new GameObject("hull").transform; fit.SetParent(root, false);
        var prefab = Resources.Load<GameObject>("Models/jetski");
        if (prefab == null) { StandInHull(fit); return; }
        var m = Instantiate(prefab, fit); m.name = "jetski"; Models.Repair(m);
        // fitToBox: the long side down the lane, scaled until it is the box's length
        var b = Models.Bounds(m);
        if (b.size.x >= b.size.y && b.size.x >= b.size.z) m.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * m.transform.localRotation;
        else if (b.size.y >= b.size.x && b.size.y >= b.size.z) m.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * m.transform.localRotation;
        b = Models.Bounds(m);
        m.transform.localScale *= BoxLen / Mathf.Max(1e-4f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        // ---- and BOW FIRST (web skiBowAt) ----
        // The file's long side is its x, the narrow low end at -x with the console just aft of it
        // and the wide flat platform at +x: the bow is at -x. The importer mirrors x, so the
        // quarter turn above sends that end to -z, which is where the ride wants it, and no end
        // for end turn is needed. Measured again here when the mesh can be read, in case the file
        // is ever swapped.
        if (BowAt(m) > 0) m.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * m.transform.localRotation;
        b = Models.Bounds(m);
        m.transform.localPosition += BoxC - b.center;
        foreach (var rd in m.GetComponentsInChildren<Renderer>(true)) { rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; rd.receiveShadows = true; }
    }

    // +1 when the bow is at +z: the narrower half, and the taller half (the console sits forward
    // of centre), each get a vote and the sign of the sum is the answer (web skiBowAt)
    int BowAt(GameObject go)
    {
        var pts = new List<Vector3>();
        try
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var ms = mf.sharedMesh; if (ms == null || !ms.isReadable) continue;
                var M = root.worldToLocalMatrix * mf.transform.localToWorldMatrix; var v = ms.vertices;
                for (int i = 0; i < v.Length; i += 3) pts.Add(M.MultiplyPoint3x4(v[i]));
            }
        }
        catch { pts.Clear(); }
        if (pts.Count == 0) return -1;                 // unreadable: what the file says, above
        float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
        foreach (var p in pts) { if (p.z < lo) lo = p.z; if (p.z > hi) hi = p.z; }
        if (!(hi > lo)) return -1;
        float mid = (lo + hi) * 0.5f, wN = 0f, wP = 0f, hN = -1e9f, hP = -1e9f;
        foreach (var p in pts)
        {
            float w = Mathf.Abs(p.x);
            if (p.z < mid) { if (w > wN) wN = w; if (p.y > hN) hN = p.y; }
            else { if (w > wP) wP = w; if (p.y > hP) hP = p.y; }
        }
        return ((wP < wN ? 1 : -1) + (hP > hN ? 1 : -1)) >= 0 ? 1 : -1;
    }

    // ---- and a PUG is driving it (web 30023-30111) ----
    // The roster's own pug, his own copy, in the file's standing pose held at its first frame (the
    // web plays Stand_and_Chat once at time 0 and never touches it again). Sized to the BOAT: a
    // third of the hull's length tall, his soles a tenth into the deck. The file faces him down
    // its +z (head to muzzle), which the mirror leaves alone, so half a turn squares him to the bow.
    void BuildCrew()
    {
        if (Resources.Load<GameObject>("Models/pug") == null) return;   // no stand-in capsule on the seat
        var crew = new GameObject("crew").transform; crew.SetParent(root, false);
        crew.localPosition = new Vector3(0f, CrewDeck + CrewFeet, CrewZ);
        crew.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var pug = Models.Spawn("pug", crew, BoxLen * CrewK);
        foreach (var a in pug.GetComponentsInChildren<Animation>(true)) { a.Stop(); a.enabled = false; }
        foreach (var a in pug.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        foreach (var rd in pug.GetComponentsInChildren<Renderer>(true)) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        crewClips = GltfClips.Start(pug.transform, "pug", new[] { "Stand_and_Chat" });
        if (crewClips != null) { crewClips.SetTime(0, 0f); crewClips.SetSpeed(0, 0f); }
    }

    // the ski when jetski.glb is missing: the web's built hull, in the same frame (web buildJetSki)
    void StandInHull(Transform fit)
    {
        var blue = Lit(0x1f6fd0, 0.85f); var blueDk = Lit(0x123f7d, 0.85f); var white = Lit(0xf6f8fa, 0.84f); var dark = Lit(0x191d22, 0.6f);
        Part(fit, PrimitiveType.Sphere, new Vector3(0f, -0.06f, 0f), new Vector3(1.24f, 0.84f, 4.10f), blue);
        Part(fit, PrimitiveType.Sphere, new Vector3(0f, -0.30f, 0f), new Vector3(1.04f, 0.60f, 3.90f), blueDk);
        Part(fit, PrimitiveType.Sphere, new Vector3(0f, 0.30f, 0f), new Vector3(1.00f, 0.32f, 3.10f), white);
        Part(fit, PrimitiveType.Sphere, new Vector3(0f, 0.50f, 0.55f), new Vector3(0.76f, 0.40f, 2.10f), dark);
        Part(fit, PrimitiveType.Sphere, new Vector3(0f, 0.42f, -1.25f), new Vector3(0.92f, 0.68f, 1.90f), blue);
        Part(fit, PrimitiveType.Sphere, new Vector3(0f, 0.66f, -1.35f), new Vector3(0.68f, 0.32f, 1.24f), white);
        Part(fit, PrimitiveType.Cube, new Vector3(0f, 0.86f, -1.05f), new Vector3(1.02f, 0.10f, 0.10f), dark);
        Part(fit, PrimitiveType.Cube, new Vector3(0f, 0.02f, 2.05f), new Vector3(0.95f, 0.10f, 0.70f), white);
    }

    // =====================================================================================
    ParticleSystem MakeRooster()
    {
        var go = new GameObject("SkiRooster"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = 320; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 14f / 32.2f;                     // the web's drops fall at 14 against the world's 32.2
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        // each drop shrinks to 0.55 of itself and fades out over its last 0.42 s (web 46692-46706)
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.55f, 1f), new Keyframe(1f, 0.55f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0.70f, 0.15f), new GradientAlphaKey(0.70f, 0.45f), new GradientAlphaKey(0.30f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = Bootstrap.SoftParticles(Bootstrap.DropTex());
        if (m != null) { Fx.Soften(m, 0.5f); r.material = m; }
        ps.Play();
        return ps;
    }

    ParticleSystem MakeFoam()
    {
        var go = new GameObject("SkiFoam"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startSpeed = 0f; main.maxParticles = FoamMax; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;           // the web's sprites
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        // web puffTexture: flat white out past half the radius, then a soft rim, so overlapping
        // lumps read as one mass rather than as dots
        var tex = Gradient(64, new[] { new Vector2(0f, 0f), new Vector2(0.52f, 1f), new Vector2(0.74f, 2f), new Vector2(0.90f, 3f), new Vector2(1f, 4f) },
            new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.98f), new Color(0.973f, 0.988f, 1f, 0.70f), new Color(0.941f, 0.980f, 1f, 0.24f), new Color(0.925f, 0.973f, 1f, 0f) }, "skiFoam");
        var m = Bootstrap.SoftParticles(tex);
        if (m != null) { Fx.Soften(m, 0.6f); r.material = m; }
        ps.Play();
        return ps;
    }

    // a radial gradient, as the web's canvases draw them: (stop, index) pairs into the colours
    static Texture2D Gradient(int S, Vector2[] stops, Color[] cols, string name)
    {
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = name };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int xx = 0; xx < S; xx++)
        {
            float dx = (xx + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            Color c = cols[cols.Length - 1];
            for (int i = 1; i < stops.Length; i++)
            {
                if (r > stops[i].x) continue;
                float u = Mathf.InverseLerp(stops[i - 1].x, stops[i].x, r);
                c = Color.Lerp(cols[(int)stops[i - 1].y], cols[(int)stops[i].y], u);
                break;
            }
            px[y * S + xx] = c;
        }
        tex.SetPixels(px); tex.Apply(true, false);
        return tex;
    }

    static Color Col(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

    static Material Lit(uint hex, float smooth)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Col(hex));
        if (m.HasProperty("_Color")) m.SetColor("_Color", Col(hex));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Material m)
    {
        var p = GameObject.CreatePrimitive(type);
        var col = p.GetComponent<Collider>(); if (col) Destroy(col);
        p.transform.SetParent(parent, false); p.transform.localPosition = pos; p.transform.localScale = size;
        p.GetComponent<Renderer>().sharedMaterial = m;
    }
}
