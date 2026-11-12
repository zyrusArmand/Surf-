// The dive, the web game's set piece under the water, number for number.
//   IN:   past DIVE_PICK_DIST a jetpack floats down the lane the way the chest does. Ride
//         through it and the launch fires: a crouch, then a ballistic arc up and down the
//         lane, piking over the top and going in head first. The dive begins on entry.
//   DOWN: twenty to fifty seconds of air, drawn fresh each time. A kick down for the first
//         0.9 s, then he hangs: the vertical is the swipe (or W/S, or the jump button held)
//         at nine feet a second. Pearls hang in the lane and pay puka on the spot. Swim hard
//         at the surface and he breaches: out in an arc and back in, the dive still on.
//   OUT:  when the clock runs out the pack lifts him up through the surface and over an arc
//         back onto the board. Running out of air never ends the run; it surfaces him.
//
// It owns rider.py outright while it runs, as the helicopter beats do, and re-places his
// transform after BoardController.Step has had its say. In this port the board stays with him
// through the arc (the web leaves it floating and lifts the rider off it), so the lift is
// simply inside py. The look (fog, the drowned sky, the sea bed) is applied in LateUpdate,
// after DayCycle has written the day's fog colour.
using System.Collections.Generic;
using UnityEngine;

public class Dive : MonoBehaviour
{
    public BoardController rider;
    public ChaseCamera chase;
    public SetWave setWave;                            // the pack waits for a quiet lane
    public System.Action<Vector3, int> onSplash;

    // ---- the web's numbers ----
    public const float LenMin = 20f, LenMax = 50f;    // seconds under, drawn per dive
    public const float ExitT = 2.0f, ExitUp = 6.2f;   // the arc out: seconds surface to surface, feet of air
    public const float LandGrace = 0.9f;              // seconds after he lands before the lane can hit him
    public const float ExitRiseV = 12.5f;             // ft/s the pack drives him up at under the water
    public const int PearlN = 18, PearlPay = 8; public const float PearlR = 1.7f;
    public const float PickDist = 900f;               // metres before the first pack can float by
    public const float PickFirstA = 6f, PickFirstB = 18f, PickGapA = 75f, PickGapB = 140f;
    public const float Sink = 0.0f, Ease0 = 3.4f;     // he hangs; how fast the plunge answers
    public const float ObstDraft = 1.9f;              // the draft of everything that floats (the spawner's)
    public const float Top = ObstDraft + 0.45f;       // how far under the surface counts as still under
    public const float SwimV = 9.0f, SwimEase = 4.6f; // feet a second, full stick; how fast it answers
    public const float Plunge = 0.90f, PlungeV = 5.6f;
    public const float Clear = 2.2f;                  // how far above the sand he is held
    public const float LaunchT = 2.30f, LaunchUp = 7.2f, LaunchFwd = 19.0f, LaunchSide = 3.4f;
    public const float PikeA = 0.28f, PikeB = 0.90f, LaunchDive = 0.72f, FaceA = 0.02f, FaceB = 0.34f;
    public const float LaunchLoad = 0.46f;            // seconds of crouch before the pack fires
    public const float Born = 138f, Behind = 30f;     // pearls are laid past the fog and taken back behind
    public const float BedDepth = 17f, BedDrift = 1.0f;
    static readonly float[][] BedOct = {
        new[] { 0.00420f, 0.00385f, 2.50f, 0.00f },
        new[] { 0.00840f, -0.00510f, 1.45f, 2.31f },
        new[] { 0.01930f, 0.01410f, 0.42f, 4.02f },
        new[] { 0.15400f, -0.11300f, 0.11f, 5.44f } };
    // the look
    public const float FogNear = 2.0f, FogFar = 118.0f, FogOff = 4000f;
    public static readonly Color FogCol = new Color(0x1d / 255f, 0x5f / 255f, 0x7a / 255f);
    public static readonly Color SkyCol = new Color(0x11 / 255f, 0x40 / 255f, 0x5a / 255f);

    // ---- public state (the HUD and the camera read these) ----
    public bool On;                // under the water (web diveOn)
    public float T;                // seconds under
    public float Depth;            // feet under the surface, 0 when he is not
    public float Left, Len;        // air: seconds left, and this dive's draw
    public int Pearls;             // taken this dive
    public int Bonus;              // puka the pearls paid this run
    public float Launch;           // seconds into the launch; 0 when there is none
    public float Exit;             // seconds into the ride up and out; 0 when there is none
    public bool Breach;
    public float Rise;             // vertical speed under the water, ft/s
    public float Lift;             // feet of air the arc gives (already inside rider.py)
    public float Pike, Face, Load; // the launch's fold, turn and crouch, 0..1
    public float Pose;             // 0 riding, 1 flat out face-down (the rig's blend)
    public float JetOn;            // the pack's thrust, 0 idle .. 1 full
    public bool PickOn;            // a jetpack is floating in the lane
    public Island island; public Chute chute;   // optional: no pack while either has the lane
    public JetSki ski;                          // optional: nor while a jet ski is crossing (web 42014 skiOn)
    public float steerY;           // -1..1 from the HUD, if it wants to drive the vertical
    public bool jumpHeld;          // the jump button held: climbs
    public bool summon;            // debug: launch now
    public bool Busy => On || Launch > 0f;                    // the set wave and the whirlpool wait
    public bool Shielded => rider != null && rider.diveShield;
    /// how far under he is for the riding bed's lid (web _sub): 0 dry .. 1 fully under
    public float Sub => (On || Depth > 0.5f) ? Mathf.Clamp01(Mathf.Max(0.25f, Depth / 2.5f)) : 0f;

    // ---- private ----
    float breachVY; int breachN; bool warned, jetGone, exitAir, jetWant; float exitT, landT = -99f, launchVY, launchZ0, side = 1f;
    float clock, pickT = -1f, pickBob, dragY, dragY0; int dragId = -1;
    Transform pick, pickHolder; Material ringMat; Light pickGlow;
    Transform pack; Light packGlow; Material[] nozzleMats;
    GameObject pearlRoot; Transform[] pearls; float[] pearlH, pearlPh; bool[] pearlGot;
    GameObject dome; Material domeMat; DiveBed bed; DiveScenery scenery;
    Color[] packGlowBase;
    bool fogSet; float fog0Start, fog0End; FogMode fog0Mode;
    public const float JetTall = 0.55f, JetWide = 1.72f;   // feet up his spine; the tanks fattened across

    // =====================================================================================
    public void Reset()
    {
        On = false; T = 0; Rise = 0; Depth = 0; Launch = 0; Exit = 0; Breach = false; breachVY = 0; breachN = 0;
        Left = 0; Len = 0; warned = false; jetGone = false; exitAir = false; exitT = 0; landT = -99f;
        Pearls = 0; Bonus = 0; Lift = Pike = Face = Load = Pose = 0; launchVY = 0; JetOn = 0; jetWant = false;
        dragY = 0; dragId = -1; steerY = 0; jumpHeld = false; clock = 0; summon = false;
        PickOn = false; pickT = -1f;
        if (pick != null) pick.gameObject.SetActive(false);
        if (pearlRoot != null) pearlRoot.SetActive(false);
        if (pack != null) pack.gameObject.SetActive(false);
        if (scenery != null) { scenery.Relay(); scenery.Show(false); }
        if (rider != null) { rider.diveOn = false; rider.diveShield = false; rider.divePitch = 0; rider.divePose = 0; rider.divePike = 0; rider.diveLoad = 0; rider.diveLift = 0; }
        Sfx.LoopStop("jet", 180);
        ApplyLook();
    }

    /// the web's diveGo(true): asking to go under starts the LAUNCH, not the dive
    public void Go()
    {
        var r = rider;
        if (r == null || !r.running || r.wipe || On || Launch > 0f) return;
        Launch = 1e-6f; jetWant = true; Load = 0; launchZ0 = r.pz;
        // toward the middle of the lane if he is out on one side of it; a coin toss down the middle
        side = r.px > 1f ? -1f : (r.px < -1f ? 1f : (Random.value < 0.5f ? -1f : 1f));
        r.Say("JETPACK!"); Sfx.Buzz(14, 26, 14);
    }

    /// how far out of the dive shot and into the swim shot the camera is (web diveShot)
    public float Shot()
    {
        if (!On) return 0f;
        float t = Mathf.Clamp01(T / 2.0f);
        return t * t * (3f - 2f * t);
    }

    /// the sea bed under (x, z): dunes scrolling past at his own speed (web bedH)
    public static float BedH(float x, float z, float distM)
    {
        float zz = z - distM * BedDrift, h = -BedDepth;
        for (int i = 0; i < BedOct.Length; i++) { var o = BedOct[i]; h += Mathf.Sin(x * o[0] + zz * o[1] + o[3]) * o[2]; }
        return h;
    }

    // =====================================================================================
    public void Step(float dt)
    {
        var r = rider; if (r == null) return;
        clock += dt;
        if (summon) { summon = false; Go(); }
        ReadDrag();
        PickStep(dt);
        if (Launch <= 0f && Pike > 0f) Pike = Mathf.Max(0f, Pike - dt * 1.9f);   // bleeds off under the water and after
        if (Launch > 0f) LaunchStep(dt);
        else if (On) DiveStep(dt);
        JetUpdate(dt);
        // the sea bed's rocks, kelp and animals: shown whenever he is under (web diveScenery)
        bool show = On || Depth > 0.5f;
        if (show && scenery == null) { scenery = new GameObject("DiveSceneryRoot").AddComponent<DiveScenery>(); scenery.rider = r; }
        if (scenery != null)
        {
            float camZ = chase != null ? chase.transform.position.z : 11f;
            scenery.Step(dt, show, On, r.eff, camZ, fogSet ? RenderSettings.fogEndDistance : FogOff);
        }
        // the swim pose the rig reads: the fold IS the pose arriving, the dive's own ease owns it after
        bool want = On && !(Exit > 0f && exitAir);
        Pose += ((want ? 1f : 0f) - Pose) * Mathf.Min(1f, dt * (On ? 7f : 5f));
        if (Pike > 0f) Pose = Mathf.Max(Pose, Pike);
        // what the ride and the spawner read off him
        bool owned = Launch > 0f || On;
        r.diveOn = owned;
        r.diveShield = Depth > ObstDraft || ((Launch > 0f || Exit > 0f) && Lift > 0.9f) || (clock - landT < LandGrace);
        r.divePose = Pose; r.divePike = Pike; r.diveLoad = Load; r.diveLift = Lift;
        if (owned) r.transform.position = new Vector3(r.px, r.py, r.pz);   // BoardController.Step placed him before py was written
    }

    // ---- the launch (web diveLaunchStep) ----
    void LaunchStep(float dt)
    {
        var r = rider; float waterY = r.waterY;
        Launch += dt;
        if (Launch < LaunchLoad)                     // the crouch, before any of the arc exists
        {
            Load = Mathf.Min(1f, Launch / (LaunchLoad * 0.70f));
            Lift = 0; Pike = 0; Face = 0; launchVY = 0;
            r.py = waterY; r.airborne = false; r.vy = 0;
            r.divePitch += (0f - r.divePitch) * Mathf.Min(1f, dt * 6f);
            return;
        }
        float tt = Launch - LaunchLoad;
        Load = Mathf.Max(0f, 1f - tt / 0.22f);      // and it springs straight as he leaves
        float k = Mathf.Clamp01(tt / LaunchT);
        Lift = 4f * LaunchUp * k * (1f - k);         // ballistic, not a sine
        launchVY = 4f * LaunchUp * (1f - 2f * k) / LaunchT;
        float pk = Mathf.Clamp01((k - PikeA) / (PikeB - PikeA)); Pike = pk * pk * (3f - 2f * pk);
        float fc = Mathf.Clamp01((k - FaceA) / (FaceB - FaceA)); Face = fc * fc * (3f - 2f * fc);
        r.pz = launchZ0 - LaunchFwd * k;             // assigned: the lane's own trim would pull it back
        r.px += side * LaunchSide * dt / LaunchT;
        r.py = waterY + Lift;                        // the board goes with him in this port
        r.airborne = true; r.vy = 0;
        r.divePitch += (0f - r.divePitch) * Mathf.Min(1f, dt * 6f);
        if (k >= 1f) { Launch = 0; Lift = 0; Face = 0; Load = 0; Under(true); }
    }

    // ---- in and out (web diveUnder) ----
    void Under(bool on)
    {
        var r = rider;
        if (on == On) return;
        On = on; T = 0;
        if (On)
        {
            // he carries his speed through the surface: the arc's own final velocity, else the plunge's
            Rise = launchVY < -0.5f ? launchVY : -PlungeV * 0.6f;
            launchVY = 0;
            Len = LenMin + Random.value * (LenMax - LenMin); Left = Len; warned = false;
            Exit = 0; Breach = false; jetGone = false; Pearls = 0; exitAir = false; exitT = 0;
            if (pack != null) pack.localScale = Vector3.one;
            BuildPearls(); for (int i = 0; i < PearlN; i++) LayPearl(i, true);
            pearlRoot.SetActive(true);
            SaveData.S.dives++;
            r.Say("UNDER!");
            onSplash?.Invoke(new Vector3(r.px, WaveField.Height(r.px, WaveField.Scroll + r.pz, r.t), r.pz), 26);
        }
        else
        {
            Rise = 0; Depth = 0; Launch = 0; jetWant = false;
            if (pearlRoot != null) pearlRoot.SetActive(false);
        }
    }

    // ---- under the water (web diveStep) ----
    void DiveStep(float dt)
    {
        var r = rider; float waterY = r.waterY;
        T += dt;
        PearlStep(dt, r.eff, waterY);
        if (Exit > 0f) { ExitStep(dt); return; }
        if (Breach) { BreachStep(dt); return; }
        // the clock
        if (T > Plunge && !r.wipe)
        {
            Left -= dt;
            if (!warned && Left <= 8f) { warned = true; r.Say("8 SECONDS OF AIR"); Sfx.Buzz(12); }
            if (Left <= 0f) { Exit = 1e-3f; exitAir = false; exitT = 0; jetWant = true; r.Say("SURFACING!"); Sfx.Buzz(12, 30); return; }
        }
        bool plunge = T < Plunge;
        float sy = SteerY();
        float want = plunge ? -PlungeV : (sy * SwimV - Sink);
        Rise += (want - Rise) * Mathf.Min(1f, dt * (plunge ? Ease0 : SwimEase));
        r.py += Rise * dt;
        float floorY = BedH(r.px, r.pz, r.dist) + Clear;          // the sand has dunes in it
        if (r.py < floorY) { r.py = floorY; Rise = Mathf.Max(Rise, 0f); }
        r.divePitch += ((Rise * 0.045f) - r.divePitch) * Mathf.Min(1f, dt * 3.2f);
        r.airborne = true; r.vy = 0;
        float ceil = waterY - Top;
        if (r.py > ceil)
        {
            // arriving at the ceiling with speed takes him through: the dolphin's move
            if (!plunge && sy > 0.5f && Rise > SwimV * 0.55f)
            {
                Breach = true; breachVY = Mathf.Max(Rise * 1.4f, 17.0f); breachN++;
                r.score += 150; r.Say("BREACH! +150"); Sfx.Buzz(8, 14, 8);
                onSplash?.Invoke(new Vector3(r.px, waterY, r.pz), 20);
                return;
            }
            r.py = ceil; Rise = Mathf.Min(Rise, 0f);
        }
        Depth = Mathf.Max(0f, waterY - r.py);       // measured where he ended up
        if ((On || Depth > 0.5f) && !Breach) Sfx.Under(Depth);
    }

    // ---- the breach (web breachStep) ----
    void BreachStep(float dt)
    {
        var r = rider; float waterY = r.waterY;
        r.py += breachVY * dt; breachVY -= 32.2f * dt;
        Depth = 0;
        r.divePitch += ((breachVY * 0.05f) - r.divePitch) * Mathf.Min(1f, dt * 5f);
        r.airborne = true; r.vy = 0;
        if (breachVY < 0f && r.py < waterY - 0.5f)
        {
            Breach = false; Rise = breachVY * 0.75f;
            onSplash?.Invoke(new Vector3(r.px, waterY, r.pz), 18);
        }
    }

    // ---- the way out (web diveExitStep) ----
    void ExitStep(float dt)
    {
        var r = rider; float waterY = r.waterY;
        Exit += dt;
        if (!exitAir)
        {
            // under: the pack drives him up
            Rise += (ExitRiseV - Rise) * Mathf.Min(1f, dt * 3.5f);
            r.py += Rise * dt;
            Depth = Mathf.Max(0f, waterY - r.py);
            r.divePitch += ((Rise * 0.045f) - r.divePitch) * Mathf.Min(1f, dt * 3.2f);
            r.airborne = true; r.vy = 0;
            if (r.py >= waterY - 0.05f)
            {
                exitAir = true; exitT = 0;
                onSplash?.Invoke(new Vector3(r.px, waterY, r.pz), 30);
                Sfx.Buzz(10, 20, 10);
            }
            return;
        }
        // in the air: a ballistic arc from the surface back to it
        exitT += dt;
        float k = Mathf.Clamp01(exitT / ExitT);
        Depth = 0; Rise = 0;
        Lift = 4f * ExitUp * k * (1f - k);
        launchVY = 4f * ExitUp * (1f - 2f * k) / ExitT;
        r.py = waterY + Lift;
        r.divePitch += (0f - r.divePitch) * Mathf.Min(1f, dt * 6f);
        r.airborne = true; r.vy = 0;
        // the pack goes over the top of the arc and is gone before he is down
        if (pack != null) { float f = 1f - Mathf.Clamp01((k - 0.42f) / 0.38f); pack.localScale = Vector3.one * Mathf.Max(0.001f, f); }
        if (k >= 0.80f && !jetGone) { jetGone = true; Sfx.LoopStop("jet", 250); }
        if (k >= 1f)
        {
            Lift = 0; launchVY = 0; exitAir = false; exitT = 0; Exit = 0; landT = clock;
            if (pack != null) pack.localScale = Vector3.one;
            Under(false);
            r.airborne = true; r.vy = 0; r.py = waterY;      // the ride's own landing takes it from here
            r.Say(Pearls > 0 ? "BACK ON THE BOARD! +" + (Pearls * PearlPay) + " PUKA" : "BACK ON THE BOARD!");
            SaveData.Flush();
        }
    }

    // ---- the vertical: keys, the jump button held, the HUD's number, a finger up or down the glass ----
    float SteerY()
    {
        float sy = steerY + dragY
                 + ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f)
                 - ((Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f);
        if (jumpHeld || Input.GetKey(KeyCode.Space)) sy = 1f;     // the button still climbs
        return Mathf.Clamp(sy, -1f, 1f);
    }

    // the same finger that steers, read up and down while he is under (web 23645-23655): hold
    // the swipe at the stop and the anchor follows, so a long climb does not run out of glass
    void ReadDrag()
    {
        if (!On) { dragY = 0; dragId = -1; return; }
        float rangeY = Screen.height * 0.13f;
        var es = UnityEngine.EventSystems.EventSystem.current;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var tc = Input.GetTouch(i);
            bool overUi = es != null && es.IsPointerOverGameObject(tc.fingerId);
            if (tc.phase == TouchPhase.Began && !overUi && dragId < 0) { dragId = tc.fingerId; dragY0 = tc.position.y; }
            if (tc.fingerId == dragId)
            {
                if (tc.phase == TouchPhase.Ended || tc.phase == TouchPhase.Canceled) { dragId = -1; dragY = 0; }
                else dragY = Rebase(tc.position.y, rangeY);
            }
        }
        if (Input.touchCount == 0)
        {
            if (dragId >= 0 && dragId != 99) { dragId = -1; dragY = 0; }
            bool overUi = es != null && es.IsPointerOverGameObject();
            if (Input.GetMouseButton(0) && !overUi)
            {
                if (dragId != 99) { dragId = 99; dragY0 = Input.mousePosition.y; }
                dragY = Rebase(Input.mousePosition.y, rangeY);
            }
            else if (dragId == 99) { dragId = -1; dragY = 0; }
        }
    }
    float Rebase(float y, float rangeY)
    {
        float w = (y - dragY0) / rangeY;
        if (w > 1f) { dragY0 = y - rangeY; w = 1f; }
        if (w < -1f) { dragY0 = y + rangeY; w = -1f; }
        return w;
    }

    // =====================================================================================
    // ---- the pearls (web buildPearls / pearlLay / pearlStep) ----
    void BuildPearls()
    {
        if (pearlRoot != null) return;
        pearlRoot = new GameObject("Pearls"); pearlRoot.SetActive(false);
        pearls = new Transform[PearlN]; pearlH = new float[PearlN]; pearlPh = new float[PearlN]; pearlGot = new bool[PearlN];
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(lit != null ? lit : Shader.Find("Standard"));
        mat.SetColor("_BaseColor", new Color(0xf4 / 255f, 0xf7 / 255f, 1f));
        mat.SetFloat("_Smoothness", 0.75f); mat.SetFloat("_Metallic", 0.1f);
        mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(0x8f / 255f, 0xd8 / 255f, 1f) * 0.55f);
        for (int i = 0; i < PearlN; i++)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = "pearl"; var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.SetParent(pearlRoot.transform, false);
            g.transform.localScale = Vector3.one * 0.60f;              // radius 0.30
            var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pearls[i] = g.transform; pearlPh[i] = Random.value * 6.28f;
            LayPearl(i, true);
        }
    }

    void LayPearl(int i, bool first)
    {
        var p = pearls[i].position;
        p.z = -(Born + Random.value * (first ? 110f : 70f));
        p.x = (Random.value - 0.5f) * 11f;
        pearls[i].position = p;
        pearlH[i] = 0.25f + Random.value * 0.75f;                     // where in the column, 0 bed .. 1 ceiling
        pearls[i].gameObject.SetActive(true); pearlGot[i] = false;
    }

    void PearlStep(float dt, float eff, float waterY)
    {
        var r = rider;
        BuildPearls();
        bool vis = On && !r.wipe;
        pearlRoot.SetActive(vis);
        if (!vis) return;
        float ceil = waterY - Top - 0.6f;
        for (int i = 0; i < PearlN; i++)
        {
            var g = pearls[i]; var p = g.position;
            p.z += eff * dt;
            if (p.z > Behind) { g.position = p; LayPearl(i, false); continue; }
            float lo = BedH(p.x, p.z, r.dist) + Clear + 0.3f;
            p.y = lo + (ceil - lo) * pearlH[i] + Mathf.Sin(clock * 1.6f + pearlPh[i]) * 0.12f;
            g.position = p;
            g.Rotate(0f, dt * 0.8f * Mathf.Rad2Deg, 0f, Space.World);
            if (pearlGot[i] || Exit > 0f || Breach) continue;
            float dx = r.px - p.x, dy = r.py - p.y, dz = r.pz - p.z;
            if (dx * dx + dy * dy + dz * dz < PearlR * PearlR)
            {
                pearlGot[i] = true; g.gameObject.SetActive(false); Pearls++;
                Bonus += PearlPay; r.bonusCoins += PearlPay; r.score += 40; SaveData.S.pearls++;
                Sfx.Buzz(8); Sfx.Pearl();
                r.Say("PEARL! +" + PearlPay);
            }
        }
    }

    // =====================================================================================
    // ---- the pack floating in the lane (web jetPickStep) ----
    void PickStep(float dt)
    {
        var r = rider; float eff = r.eff;
        if (!r.running || r.wipe) { if (pick != null) pick.gameObject.SetActive(false); PickOn = false; return; }
        if (!PickOn)
        {
            if (On || Launch > 0f || r.dist < PickDist) return;
            if (pickT < 0f) pickT = PickFirstA + Random.value * (PickFirstB - PickFirstA);
            bool busy = (setWave != null && setWave.ph != SetWave.Phase.Idle) || WaveField.WhA > 0.001f || (island != null && island.Busy) || (chute != null && chute.Busy)
                        || r.glideOn || r.hanging                      // and no pack while he is on the paraglide or the plane's rope (web 42014)
                        || (ski != null && ski.On);                    // or a jet ski is crossing (web 42014)
            if (busy) return;
            pickT -= dt;
            if (pickT > 0f) return;
            BuildPick();
            PickOn = true; pick.gameObject.SetActive(true); pickBob = 0;
            pick.position = new Vector3(Mathf.Clamp(r.px + (Random.value - 0.5f) * 12f, -12f, 12f), 0f, -170f);
            r.Say("JETPACK AHEAD!");
            return;
        }
        pickBob += dt;
        var p = pick.position; p.z += eff * dt;
        float sea = WaveField.Swell(p.x, WaveField.Scroll + p.z, r.t);
        p.y = sea + 0.55f + Mathf.Sin(pickBob * 1.9f) * 0.16f;
        pick.position = p;
        pickHolder.localRotation = Quaternion.Euler(0f, pickBob * 1.4f * Mathf.Rad2Deg, 0f);
        float ringA = 0.55f + 0.3f * Mathf.Sin(pickBob * 2.8f);
        if (ringMat != null) { var c = ringMat.GetColor("_BaseColor"); c.a = ringA; ringMat.SetColor("_BaseColor", c); }
        if (pickGlow != null) pickGlow.intensity = 1.4f + 1.0f * Mathf.Sin(pickBob * 2.8f + 1f);
        if (p.z > 14f) { PickOn = false; pick.gameObject.SetActive(false); pickT = PickGapA * 0.5f + Random.value * 30f; return; }
        if (!On && Launch <= 0f && Mathf.Abs(p.z - r.pz) < 2.4f && Mathf.Abs(p.x - r.px) < 2.4f && Mathf.Abs(r.py - p.y) < 2.4f)
        {
            PickOn = false; pick.gameObject.SetActive(false);
            pickT = PickGapA + Random.value * (PickGapB - PickGapA);
            onSplash?.Invoke(new Vector3(r.px, r.py + 0.3f, r.pz), 12);
            Go();
        }
    }

    void BuildPick()
    {
        if (pick != null) return;
        var go = new GameObject("JetPick"); pick = go.transform;
        var spin = new GameObject("holder").transform; spin.SetParent(pick, false); spin.localPosition = new Vector3(0, 1.3f, 0);
        pickHolder = spin;
        BuildPack(spin, JetTall * 4.6f, out _, out _, out _);     // big enough to read from thirty feet down the lane
        // the ring on the water under it, and a glow where the web has a halo sprite
        var ring = new GameObject("ring", typeof(MeshFilter), typeof(MeshRenderer)); ring.transform.SetParent(pick, false);
        ring.GetComponent<MeshFilter>().sharedMesh = Torus(1.6f, 0.10f, 48, 10);
        ringMat = Unlit(new Color(0x7f / 255f, 0xe9 / 255f, 1f, 0.75f), true, false, true);
        var rr = ring.GetComponent<MeshRenderer>(); rr.sharedMaterial = ringMat; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pickGlow = go.AddComponent<Light>(); pickGlow.type = LightType.Point; pickGlow.color = new Color(0x9f / 255f, 0xf0 / 255f, 1f); pickGlow.range = 9f; pickGlow.intensity = 1.8f;
        go.SetActive(false);
    }

    // ---- the pack on his back (web jetUpdate): shown while he is under, thrust from the stick ----
    void JetUpdate(float dt)
    {
        var r = rider;
        bool want = On || Depth > 0.5f || Launch > 0f || jetWant;
        if (want && pack == null)
        {
            var holder = new GameObject("JetPack").transform; holder.SetParent(r.transform, false);
            holder.localPosition = new Vector3(0f, 1.25f, 0.45f);
            BuildPack(holder, JetTall, out packGlow, out nozzleMats, out packGlowBase);
            pack = holder;
        }
        if (pack != null) pack.gameObject.SetActive(want && !jetGone);
        if (!want) { JetOn = 0; Sfx.LoopStop("jet", 180); return; }
        // the thrust reads on the trim before the bubbles: swimming hard counts as thrust,
        // and it is full through the launch
        float swim = Mathf.Min(1f, Mathf.Sqrt(r.steer * r.steer + SteerYRaw() * SteerYRaw()));
        float th = Launch > 0f ? 1f : Mathf.Max((jumpHeld || Input.GetKey(KeyCode.Space)) ? 1f : 0f, swim);
        JetOn += (th - JetOn) * Mathf.Min(1f, dt * 8f);
        if (packGlow != null) packGlow.intensity = 0.6f + JetOn * 2.2f;
        // the file's glow strip answers the thrust (web 1.5 + 3.2 * thrust); the built pods' nozzles likewise
        if (nozzleMats != null) for (int i = 0; i < nozzleMats.Length; i++)
        {
            var m = nozzleMats[i]; if (m == null) continue;
            if (packGlowBase != null && i < packGlowBase.Length) m.SetColor("_EmissionColor", packGlowBase[i] * (1.5f + JetOn * 3.2f));
            else m.SetColor("_EmissionColor", new Color(0x7f / 255f, 0xe9 / 255f, 1f) * (0.35f + JetOn * 1.5f));
        }
        if (!jetGone) Sfx.Jet(JetOn);
    }
    float SteerYRaw() { return Mathf.Clamp(steerY + dragY, -1f, 1f); }

    // The web's jetpack.glb, worn as the web wears it: its longest axis (the tanks) up the spine,
    // the two tanks abreast across his back and fattened by JetWide, the thin way out of his back.
    // Models.FitLength lays the longest side along z and the thinnest along y, so a quarter turn
    // about x stands the tanks up. Which end the exhausts are on is the file's own business.
    // Without the file, two pods and a pair of nozzles are built instead.
    static void BuildPack(Transform parent, float tall, out Light glow, out Material[] glowMats, out Color[] glowBase)
    {
        glowBase = null;
        var h = new GameObject("holder").transform; h.SetParent(parent, false);
        var go = Models.Spawn("jetpack", h, 1f, PrimitiveType.Capsule);
        if (!go.name.EndsWith("(stand-in)"))
        {
            Models.FitLength(go, tall);
            h.localScale = new Vector3(JetWide, 1f, 1f);
            h.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var gm = new List<Material>(); var gb = new List<Color>();
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mats = rend.materials;      // instanced, so the glow can be driven per pack
                foreach (var m in mats)
                {
                    if (m == null || m.name.IndexOf("glow", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    m.EnableKeyword("_EMISSION");
                    var e = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                    if (e.maxColorComponent < 0.05f) e = new Color(0x7f / 255f, 0xe9 / 255f, 1f);
                    gm.Add(m); gb.Add(e);
                }
            }
            glowMats = gm.ToArray(); glowBase = gb.ToArray();
            glow = parent.gameObject.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(0x7f / 255f, 0xe9 / 255f, 1f); glow.range = 4f * tall / JetTall; glow.intensity = 0.6f;
            return;
        }
        Destroy(go);
        float scale = tall / JetTall;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var body = new Material(lit != null ? lit : Shader.Find("Standard")); body.SetColor("_BaseColor", new Color(0.78f, 0.80f, 0.84f)); body.SetFloat("_Metallic", 0.8f); body.SetFloat("_Smoothness", 0.7f);
        var noz = new Material(lit != null ? lit : Shader.Find("Standard")); noz.SetColor("_BaseColor", new Color(0.25f, 0.28f, 0.32f)); noz.EnableKeyword("_EMISSION");
        noz.SetColor("_EmissionColor", new Color(0x7f / 255f, 0xe9 / 255f, 1f) * 0.35f);
        glowMats = new[] { noz };
        for (int s = -1; s <= 1; s += 2)
        {
            var pod = GameObject.CreatePrimitive(PrimitiveType.Capsule); var c1 = pod.GetComponent<Collider>(); if (c1 != null) Destroy(c1);
            pod.transform.SetParent(parent, false); pod.transform.localPosition = new Vector3(s * 0.19f * scale, 0f, 0f);
            pod.transform.localScale = new Vector3(0.22f, 0.34f, 0.22f) * scale;
            pod.GetComponent<MeshRenderer>().sharedMaterial = body; pod.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var nz = GameObject.CreatePrimitive(PrimitiveType.Cylinder); var c2 = nz.GetComponent<Collider>(); if (c2 != null) Destroy(c2);
            nz.transform.SetParent(parent, false); nz.transform.localPosition = new Vector3(s * 0.19f * scale, -0.40f * scale, 0f);
            nz.transform.localScale = new Vector3(0.10f, 0.06f, 0.10f) * scale;
            nz.GetComponent<MeshRenderer>().sharedMaterial = noz; nz.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        glow = parent.gameObject.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(0x7f / 255f, 0xe9 / 255f, 1f); glow.range = 4f * scale; glow.intensity = 0.6f;
    }

    static Mesh Torus(float R, float r, int seg, int ring)
    {
        var v = new Vector3[(seg + 1) * (ring + 1)]; var n = new Vector3[v.Length]; var idx = new int[seg * ring * 6]; int k = 0;
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f, cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            for (int j = 0; j <= ring; j++)
            {
                float b = j / (float)ring * Mathf.PI * 2f, cb = Mathf.Cos(b), sb = Mathf.Sin(b);
                v[i * (ring + 1) + j] = new Vector3((R + r * cb) * cx, r * sb, (R + r * cb) * cz);
                n[i * (ring + 1) + j] = new Vector3(cb * cx, sb, cb * cz);
            }
        }
        for (int i = 0; i < seg; i++) for (int j = 0; j < ring; j++)
        {
            int A = i * (ring + 1) + j, B = A + ring + 1, C = A + 1, D = B + 1;
            idx[k++] = A; idx[k++] = B; idx[k++] = C; idx[k++] = C; idx[k++] = B; idx[k++] = D;
        }
        var m = new Mesh { name = "torus" }; m.vertices = v; m.normals = n; m.triangles = idx; m.RecalculateBounds();
        return m;
    }

    /// a URP Unlit material, transparent if asked, culling front faces or nothing if asked
    public static Material Unlit(Color c, bool transparent, bool cullFront = false, bool cullOff = false)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh);
        m.SetColor("_BaseColor", c);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        m.SetFloat("_Cull", cullOff ? 0f : (cullFront ? 1f : 2f));
        return m;
    }

    // =====================================================================================
    // ---- the look (web diveFog): the fog closes in with depth, the sky drowns, the bed shows ----
    void LateUpdate() { ApplyLook(); }

    void ApplyLook()
    {
        float depth = Depth;
        if (depth <= 0.05f)
        {
            if (fogSet) { fogSet = false; RenderSettings.fogMode = fog0Mode; RenderSettings.fogStartDistance = fog0Start; RenderSettings.fogEndDistance = fog0End; }
            if (dome != null) dome.SetActive(false);
            if (bed != null) bed.Show(On);
            return;
        }
        if (!fogSet) { fogSet = true; fog0Mode = RenderSettings.fogMode; fog0Start = RenderSettings.fogStartDistance; fog0End = RenderSettings.fogEndDistance; }
        // the far plane is DIVIDED by the depth: two feet down you can see a long way, twelve feet down sixty
        float k = Mathf.Clamp(depth / 12f, 0.02f, 1f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = FogNear; RenderSettings.fogEndDistance = Mathf.Min(FogOff, FogFar / k);
        RenderSettings.fogColor = FogCol;
        // the sky goes under with him, full by three feet: a dome round the lens, drawn over the skybox
        if (dome == null)
        {
            dome = GameObject.CreatePrimitive(PrimitiveType.Sphere); dome.name = "DiveDome";
            var col = dome.GetComponent<Collider>(); if (col != null) Destroy(col);
            dome.transform.localScale = Vector3.one * 900f;
            domeMat = Unlit(SkyCol, true, true);
            var mr = dome.GetComponent<MeshRenderer>(); mr.sharedMaterial = domeMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        }
        dome.SetActive(true);
        var cam = chase != null && chase.cam != null ? chase.cam.transform : (Camera.main != null ? Camera.main.transform : null);
        if (cam != null) dome.transform.position = cam.position;
        var dc = SkyCol; dc.a = Mathf.Min(1f, depth / 3.0f); domeMat.SetColor("_BaseColor", dc);
        if (bed == null) { bed = new GameObject("DiveBed").AddComponent<DiveBed>(); }
        bed.Show(true);
        if (rider != null) bed.Place(rider.dist);
    }
}
