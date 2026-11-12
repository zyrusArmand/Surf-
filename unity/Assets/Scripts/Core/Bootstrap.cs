// The one object in the scene. It builds everything else at runtime -- lights, sky, sea, the
// board and the rider, the spawner, the camera, the HUD -- so the project needs no hand-made
// prefabs or scene wiring to run, and every change here is a code change rather than a click.
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    BoardController rider; Spawner spawner; ChaseCamera chase; Hud hud; DayCycle day; Ocean ocean;
    Fx fx;
    ParticleSystem spray; Transform rig, pug; RiderRig riderRig; ProcRig procRig; float wakeA;
    Beach beach; Camera cam; GameObject boardGo; SetWave setWave; Whirlpool whirl; Crate crate; Shop shop; Coconuts cocos; Ufo ufo; Dive dive; Island island; Chute chute;
    Quests quests; StatsPanel stats; SettingsPanel settings; SchoolPanel school; ProfilePanel profile; GuidePanel guide;
    WheelPanel wheel; LevelPanel levelInfo; PukaPanel puka; SecretPanel secret; int bankedDist, bankedScore, bankedCoins;
    bool over; float runSecs; bool palmSaid;
    Boat boat;                                        // the tow boat and its paraglide, on demand only (web 15187)
    JetSki ski;                                       // the jet ski and the wake it throws at you (web 29842-30405)
    Vector3 boardBasePos; Quaternion boardBaseRot;   // where the board sits in the rig when no board trick has it
    readonly System.Collections.Generic.List<Vector2> wakeHist = new System.Collections.Generic.List<Vector2>();   // (time, x) of the board, the last few seconds
    readonly float[] wakeP = new float[8];
    float WakeXAt(float tt)
    {
        int n = wakeHist.Count;
        if (n == 0) return rider.px;
        if (tt >= wakeHist[n - 1].x) return wakeHist[n - 1].y;
        for (int i = n - 1; i > 0; i--)
        {
            var a = wakeHist[i - 1]; var b = wakeHist[i];
            if (tt >= a.x) return Mathf.Lerp(a.y, b.y, (tt - a.x) / Mathf.Max(1e-4f, b.x - a.x));
        }
        return wakeHist[0].y;
    }
    float riderReflShift;
    Vector3 pugBase = Vector3.one; GltfClips riderClips;   // the rider's fitted scale; the clip a rig-less rider plays
    Vector3 hullMin, hullMax; bool hullOk; float hullRise, hullThick = 0.2f, floatLift;
    // where the hull is checked against the sea: the middle, both rails, the rails fore and aft, the tail
    static readonly Vector2[] FloatPts = { new Vector2(0f, 0f), new Vector2(0.9f, 0f), new Vector2(-0.9f, 0f), new Vector2(0.75f, 0.45f), new Vector2(-0.75f, 0.45f), new Vector2(0.75f, -0.45f), new Vector2(-0.75f, -0.45f), new Vector2(0f, 0.8f) }; PlanarReflection reflect;
    UnityEngine.Rendering.Universal.DepthOfField dof; UnityEngine.Rendering.Universal.ScreenSpaceLensFlare flare;      // the board's own box, in its frame, for the sea's dry-hull rule

    // Whatever scene is open when Play is pressed, the game builds itself: an empty or untitled
    // scene (what Unity opens after a crash) gets the Surf object too, and its stock camera and
    // light are switched off so they cannot fight the game's own.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (FindFirstObjectByType<Bootstrap>() != null) return;
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
        new GameObject("Surf").AddComponent<Bootstrap>();
    }

    // Start is a coroutine for one reason: to put the loading screen up and let it draw before
    // the second or two the whole world takes to build. Nothing runs in Update until it is done.
    bool ready;
    System.Collections.IEnumerator Start()
    {
        Application.targetFrameRate = 60;
        Physics.gravity = new Vector3(0f, -32.2f, 0f);      // the world is in feet
        QualitySettings.vSyncCount = 0;
        var load = Loading.Show(); load.Set(0.12f);
        FpsMeter.Install();             // a Development Build shows how it is running, for the phone test
        yield return null;

        // light
        var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.75f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        SkyReflection();
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 120f; RenderSettings.fogEndDistance = 330f;

        // sky
        var skyMat = new Material(Shader.Find("Surf/Sky"));
        RenderSettings.skybox = skyMat;

        // sea
        var oceanGo = new GameObject("Ocean"); oceanGo.layer = PlanarReflection.WaterLayer;   // the reflection does not draw the sea into itself
        ocean = oceanGo.AddComponent<Ocean>();
        ocean.Init(new Material(Shader.Find("Surf/Ocean")));
        var chop = Resources.Load<Texture2D>("Models/waternormals");
        // Filtered as the web filters it (10246): trilinear and anisotropic. Bilinear, the sea is seen
        // at a grazing angle almost everywhere, so each step from one mip to the next fell on a hard
        // line straight across the water at a fixed distance from the lens -- bands of different
        // ripple running left to right through the sea, and moving as the camera bobbed.
        if (chop != null) { chop.wrapMode = TextureWrapMode.Repeat; chop.filterMode = FilterMode.Trilinear; chop.anisoLevel = 8; ocean.material.SetTexture("_Chop", chop); ocean.material.SetFloat("_ChopK", 1f); }

        day = gameObject.AddComponent<DayCycle>();
        day.sky = skyMat; day.ocean = ocean.material; day.sun = sun;

        // the board and the rider
        var player = new GameObject("Player"); rider = player.AddComponent<BoardController>();
        rig = new GameObject("rig").transform; rig.SetParent(player.transform, false);
        BuildRider();

        // the water
        spawner = new GameObject("Spawner").AddComponent<Spawner>(); spawner.rider = rider;
        setWave = new GameObject("SetWave").AddComponent<SetWave>(); setWave.rider = rider; setWave.day = day; spawner.setWave = setWave;
        whirl = new GameObject("Whirlpool").AddComponent<Whirlpool>(); whirl.rider = rider; whirl.spawner = spawner; whirl.setWave = setWave;
        crate = new GameObject("Crate").AddComponent<Crate>(); crate.rider = rider; crate.setWave = setWave;
        cocos = new GameObject("Coconuts").AddComponent<Coconuts>(); cocos.rider = rider; cocos.setWave = setWave;
        dive = new GameObject("Dive").AddComponent<Dive>(); dive.rider = rider; dive.setWave = setWave; setWave.dive = dive; whirl.dive = dive;
        ufo = new GameObject("Ufo").AddComponent<Ufo>(); ufo.rider = rider; ufo.day = day; ufo.setWave = setWave; ufo.rig = rig; ufo.onRebuild = BuildRider; setWave.ufo = ufo;
        island = new GameObject("Island").AddComponent<Island>(); island.rider = rider; island.spawner = spawner; island.setWave = setWave;
        chute = new GameObject("Chute").AddComponent<Chute>(); chute.rider = rider; chute.spawner = spawner; chute.setWave = setWave; chute.whirl = whirl;
        island.chute = chute; chute.island = island; setWave.island = island; setWave.chute = chute; whirl.island = island; whirl.chute = chute; dive.island = island; dive.chute = chute;
        crate.island = island; cocos.island = island; spawner.island = island;
        ski = new GameObject("JetSki").AddComponent<JetSki>(); ski.rider = rider; ski.setWave = setWave; ski.dive = dive; ski.island = island; setWave.ski = ski; dive.ski = ski;

        // spray
        spray = BuildSpray();
        fx = new GameObject("Fx").AddComponent<Fx>(); fx.rider = rider; fx.setWave = setWave; fx.dive = dive; fx.Build();
        // every splash in the game: drops, mist, foam and a ring of wave in the sea itself; when it is
        // the board slapping down, the ring starts from the board's own outline, centred on the board
        rider.onSplash = (p, n) =>
        {
            bool board = n >= 6 && hullOk && boardGo != null && rider.running && Mathf.Abs(p.x - rider.px) < 1.5f && Mathf.Abs(p.z - rider.pz) < 2.5f;
            if (!board) { fx.Hit(p, n); return; }
            var bt = boardGo.transform; var sc = bt.lossyScale;
            var c = bt.TransformPoint(new Vector3((hullMin.x + hullMax.x) * 0.5f, 0f, (hullMin.z + hullMax.z) * 0.5f));
            float yaw = bt.eulerAngles.y * Mathf.Deg2Rad;
            var shape = new Vector4((hullMax.x - hullMin.x) * 0.5f * Mathf.Abs(sc.x), (hullMax.z - hullMin.z) * 0.5f * Mathf.Abs(sc.z), Mathf.Sin(yaw), Mathf.Cos(yaw));
            fx.Hit(new Vector3(c.x, p.y, c.z), n, shape);
        };
        spawner.onSplash = rider.onSplash; setWave.onSplash = rider.onSplash; whirl.onSplash = rider.onSplash; crate.onSplash = rider.onSplash; cocos.onSplash = rider.onSplash; ufo.onSplash = rider.onSplash; dive.onSplash = rider.onSplash; island.onSplash = rider.onSplash; chute.onSplash = rider.onSplash;
        ski.onSplash = rider.onSplash;
        rider.onWipeout = () => { over = false; Sfx.Buzz(60, 50, 80); };

        // camera
        var camGo = new GameObject("Camera"); cam = camGo.AddComponent<Camera>(); camGo.tag = "MainCamera";
        cam.nearClipPlane = 0.5f; cam.farClipPlane = 500f; cam.fieldOfView = 62f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        Sfx.Init();
        var ucam = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        ucam.renderPostProcessing = true; ucam.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; cam.allowMSAA = true;   // the pipeline's 4x MSAA: crisp edges, no FXAA smearing the texture detail
        ucam.requiresDepthTexture = true;          // the sea reads what is under it for its contact foam and shallows
        reflect = camGo.AddComponent<PlanarReflection>(); reflect.main = cam;
        // for the reflection's own render only: the floating things and the rider seated on the mirror,
        // each by the water under it, and put straight back afterwards
        reflect.beforeRender = y =>
        {
            spawner.SeatForReflection(y);
            riderReflShift = rider.running ? y - rider.waterY : 0f;
            if (riderReflShift != 0f) rider.transform.position += new Vector3(0f, riderReflShift, 0f);
        };
        reflect.afterRender = () =>
        {
            spawner.UnseatAfterReflection();
            if (riderReflShift != 0f) { rider.transform.position -= new Vector3(0f, riderReflShift, 0f); riderReflShift = 0f; }
        };
        chase = camGo.AddComponent<ChaseCamera>(); chase.rider = rider; chase.cam = cam; chase.dive = dive; chase.island = island; chase.chute = chute; island.chase = chase; chute.chase = chase;

        // post: bloom, a touch of vignette, ACES
        var volGo = new GameObject("Volume"); var vol = volGo.AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); vol.profile = prof;
        var bloom = prof.Add<UnityEngine.Rendering.Universal.Bloom>(); bloom.intensity.Override(0.55f); bloom.threshold.Override(1.0f); bloom.scatter.Override(0.68f);   // only what is brighter than white: the sun, the glints
        var vig = prof.Add<UnityEngine.Rendering.Universal.Vignette>(); vig.intensity.Override(0.16f);
        // a soft flare off the sun, built from the bloom's own bright pixels, so it is only ever where the sun is
        flare = prof.Add<UnityEngine.Rendering.Universal.ScreenSpaceLensFlare>(); flare.intensity.Override(0.22f);
        // the title's near-sand blur (web #nearBlur) as real depth of field: focused on the set, the sand at the lens soft
        dof = prof.Add<UnityEngine.Rendering.Universal.DepthOfField>();
        dof.mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh);
        dof.focalLength.Override(50f); dof.aperture.Override(3.5f); dof.focusDistance.Override(10.6f);
        dof.active = false;
        // no tone mapping: the web's sea and sky are drawn raw and encoded once, and that is
        // where their colour comes from; a filmic curve on top is what made the sea grey
        var tone = prof.Add<UnityEngine.Rendering.Universal.Tonemapping>(); tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.None);

        // the title screen's beach
        beach = gameObject.AddComponent<Beach>(); beach.Build(null);
        // every arm answers the finger as the web's dial does: buzz(12) for Play, buzz(8) for the rest
        beach.onSign = s => { Sfx.Buzz(s == "Play" ? 12 : 8); if (s == "Play") Play(); else if (s == "Shop") shop.Show(true); else if (s == "Quests") quests.Show(true); else school.Show(true); };
        beach.SetBoard(SaveData.S.equipped);

        // HUD
        hud = new GameObject("HUD").AddComponent<Hud>(); hud.rider = rider; hud.cocos = cocos; hud.chase = chase; hud.island = island; hud.dive = dive;
        hud.onPlay = Play; hud.onMenu = Menu; hud.onShop = () => shop.Show(true);
        spawner.onSplat = c => hud.Splat(c);
        hud.Build();
        boat = new GameObject("Boat").AddComponent<Boat>(); boat.rider = rider; boat.spawner = spawner; boat.chase = chase; boat.dive = dive;
        boat.rig = rig; boat.ui = hud.UiRoot; boat.sea = ocean.material; boat.onSplash = rider.onSplash;
        shop = new GameObject("Shop").AddComponent<Shop>(); shop.Build(hud.UiRoot);
        shop.onChange = () => { BuildRider(); if (beach.Showing) beach.PlaceRider(rider.transform, rig, boardGo); hud.ShowMenu(); };
        // the other panes: quests, stats, settings, the trick school, the profile
        quests = new GameObject("Quests").AddComponent<Quests>(); quests.Build(hud.UiRoot);
        stats = new GameObject("Stats").AddComponent<StatsPanel>(); stats.Build(hud.UiRoot);
        settings = new GameObject("Settings").AddComponent<SettingsPanel>(); settings.Build(hud.UiRoot);
        school = new GameObject("School").AddComponent<SchoolPanel>(); school.Build(hud.UiRoot);
        profile = new GameObject("Profile").AddComponent<ProfilePanel>(); profile.Build(hud.UiRoot);
        guide = new GameObject("Guide").AddComponent<GuidePanel>(); guide.Build(hud.UiRoot);
        guide.onChange = shop.onChange; guide.onShop = () => shop.Show(true);
        shop.onGuide = () => guide.Show(true);
        wheel = new GameObject("Wheel").AddComponent<WheelPanel>(); wheel.Build(hud.UiRoot);
        levelInfo = new GameObject("LevelInfo").AddComponent<LevelPanel>(); levelInfo.Build(hud.UiRoot);
        puka = new GameObject("Puka").AddComponent<PukaPanel>(); puka.Build(hud.UiRoot);
        // the secret panel: five taps on the version
        secret = new GameObject("Secret").AddComponent<SecretPanel>(); secret.Build(hud.UiRoot);
        hud.onSecret = () => secret.Show(true);
        secret.onChange = shop.onChange;
        secret.onTest = what =>
        {
            Play();
            switch (what)
            {
                case "wave": setWave.summon = true; break;
                case "whirl": whirl.summon = true; break;
                case "ufo": ufo.summon = true; break;
                case "fork": island.summon = true; break;
                case "dive": dive.summon = true; break;
                case "drop": chute.summon = true; break;
                case "boat": boat.summon = true; break;
                case "ski": ski.summon = true; break;
            }
        };
        hud.onWheel = () => wheel.Show(true); hud.onLevel = () => levelInfo.Show(true); hud.onPuka = () => puka.Show(true);
        wheel.onChange = shop.onChange;      // a loan or gear won shows at once; the purse refreshes
        // a spare life from the wheel: back on the board where he went in, the water ahead cleared
        hud.onRevive = () =>
        {
            bankedDist = Mathf.RoundToInt(rider.dist); bankedScore = rider.score; bankedCoins = rider.coins;
            for (int i = spawner.obstacles.Count - 1; i >= 0; i--) if (spawner.obstacles[i].z > -48f) spawner.Remove(i);
            spawner.eater = null; setWave.Reset(); whirl.Reset();
            ski.Revive();                                    // the ski and its wake put away, the next one 26 s out (web 27178)
            rider.Revive(); over = false; hud.ShowRide(); chase.Snap();
        };
        hud.onQuests = () => quests.Show(true); hud.onSettings = () => settings.Show(true);
        hud.onProfile = () => profile.Show(true); hud.onSchool = () => school.Show(true);
        profile.onStats = () => stats.Show(true);
        Quests.onToast = hud.Toast;
        settings.onChange = () => { Sfx.Refresh(); ApplyGfx(); };
        quests.onClose = settings.onClose = profile.onClose = stats.onClose = school.onClose = guide.onClose = wheel.onClose = levelInfo.onClose = puka.onClose = secret.onClose = () => { if (!rider.running && !rider.wipe) hud.ShowMenu(); };
        Menu();
        ApplyGfx();
        ready = true; load.Hide();
    }

    /// the rider's (and what he wears') opaque materials onto Surf/Character, keeping their
    /// texture and colour; anything see-through (lenses, glass) is left as it is
    static void Character(GameObject go)
    {
        var sh = Shader.Find("Surf/Character");
        if (sh == null || go == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials; bool changed = false;
            for (int i = 0; i < ms.Length; i++)
            {
                var src = ms[i];
                if (src == null || src.shader == sh || src.renderQueue >= 2450) continue;
                var m = new Material(sh) { name = src.name + " (character)" };
                Texture t = null; foreach (var p in new[] { "baseColorTexture", "_BaseMap", "_MainTex" }) if (t == null && src.HasProperty(p)) t = src.GetTexture(p);
                Color c = Color.white; foreach (var p in new[] { "baseColorFactor", "_BaseColor", "_Color" }) if (src.HasProperty(p)) { c = src.GetColor(p); break; }
                if (t != null) m.SetTexture("_BaseMap", t);
                m.SetColor("_BaseColor", c);
                ms[i] = m; changed = true;
            }
            if (changed) r.sharedMaterials = ms;
        }
    }

    // the board's box in its own frame: every visible mesh's corners brought into the board root
    void MeasureHull()
    {
        hullOk = false; if (boardGo == null) return;
        var root = boardGo.transform; Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
        foreach (var mf in boardGo.GetComponentsInChildren<MeshFilter>(false))
        {
            if (mf.sharedMesh == null) continue;
            var mr = mf.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled) continue;
            if (mf.name == "fin") continue;              // the fin hangs below the hull; the hull is what floats
            var b = mf.sharedMesh.bounds; var toRoot = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                var p = toRoot.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z));
                mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
            }
        }
        if (mn.x <= mx.x) { hullMin = mn; hullMax = mx; hullOk = true; }
        // the rocker: how far the underside rises from the middle of the board to its ends, read
        // off the hull's own vertices (a board built here is always readable; a file may not be)
        hullRise = 0f;
        if (!hullOk) return;
        float cz = (mn.z + mx.z) * 0.5f, hl = Mathf.Max(1e-3f, (mx.z - mn.z) * 0.5f), botC = float.MaxValue, botE = float.MaxValue, topC = float.MinValue;
        foreach (var mf in boardGo.GetComponentsInChildren<MeshFilter>(false))
        {
            if (mf.sharedMesh == null || mf.name == "fin" || !mf.sharedMesh.isReadable) continue;
            var toRoot = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (var v0 in mf.sharedMesh.vertices)
            {
                var p = toRoot.MultiplyPoint3x4(v0); float q = Mathf.Abs(p.z - cz) / hl;
                if (q < 0.15f) { botC = Mathf.Min(botC, p.y); topC = Mathf.Max(topC, p.y); } else if (q > 0.80f && q < 0.92f) botE = Mathf.Min(botE, p.y);
            }
        }
        if (botC < float.MaxValue && botE < float.MaxValue) hullRise = Mathf.Max(0f, botE - botC) / (0.86f * 0.86f);
        hullThick = (botC < float.MaxValue && topC > botC) ? topC - botC : Mathf.Max(0.08f, (mx.y - mn.y) * 0.4f);
    }

    // ---- what the models see reflected: the web's baked environment (bakeEnv) ----
    // A sky that runs from the horizon's pale blue to the zenith's deep one, dark sea below the
    // horizon and the sun as a warm ball, at 55% (what PBR() gives every material). Without it the
    // boards, the chest and the buoys reflected nothing at all, which is the matte look.
    static void SkyReflection()
    {
        const int N = 64;
        var cube = new Cubemap(N, TextureFormat.RGBAHalf, true) { name = "SkyEnv", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        Color top = Raw(0x2f7fc4), hor = Raw(0x9fd0e8), ground = Raw(0x14567f), sun = Raw(0xfff0cf);
        var sunDir = new Vector3(22f, 26f, -18f).normalized;          // the web's (-22, 26, -18), mirrored with the set
        float sunCos = Mathf.Cos(Mathf.Asin(3.4f / 38.5f));
        var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
        var px = new Color[N * N];
        foreach (var f in faces)
        {
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                Vector3 d;
                switch (f)
                {
                    case CubemapFace.PositiveX: d = new Vector3(1, -v, -u); break;
                    case CubemapFace.NegativeX: d = new Vector3(-1, -v, u); break;
                    case CubemapFace.PositiveY: d = new Vector3(u, 1, v); break;
                    case CubemapFace.NegativeY: d = new Vector3(u, -1, -v); break;
                    case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1); break;
                    default: d = new Vector3(-u, -v, -1); break;
                }
                d.Normalize();
                Color c = d.y >= -0.02f ? Color.Lerp(hor, top, Mathf.Pow(Mathf.Clamp01(d.y), 0.55f)) : ground;
                if (Vector3.Dot(d, sunDir) > sunCos) c = sun;
                px[y * N + x] = c;
            }
            cube.SetPixels(px, f);
        }
        cube.Apply(true, false);
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = cube;
        RenderSettings.reflectionIntensity = 0.55f;
    }
    static Color Raw(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f);   // the web's raw numbers, no conversion

    // the board is a solid thing: a box round its hull on a kinematic body, so what floats and
    // tumbles in the water knocks against it
    void Solidify()
    {
        if (boardGo == null || !hullOk) return;
        var box = boardGo.GetComponent<BoxCollider>(); if (box == null) box = boardGo.AddComponent<BoxCollider>();
        box.center = (hullMin + hullMax) * 0.5f; box.size = hullMax - hullMin;
        var rb = boardGo.GetComponent<Rigidbody>(); if (rb == null) rb = boardGo.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None;   // carried by the rig: interpolating it left the board a step behind (and under) him
    }

    // the equipped board and rider, from the save; called again when the shop changes them
    void BuildRider()
    {
        if (boardGo != null) Destroy(boardGo);
        if (riderClips != null) { riderClips.Stop(); riderClips = null; }
        if (pug != null) Destroy(pug.gameObject);
        // a loan (the wheel's, or the saucer's cow) outranks the equipped pair
        var b = BoardCatalog.Get(string.IsNullOrEmpty(SaveData.S.wheelLoan) ? SaveData.S.equipped : SaveData.S.wheelLoan);
        var board = BoardMesh.Build(b); board.transform.SetParent(rig, false); boardGo = board; Hull.Board = board; Hull.ClearMarks();
        boardBasePos = board.transform.localPosition; boardBaseRot = board.transform.localRotation;
        MeasureHull(); Solidify();
        rider.ApplyStats(RideStats.For(b.id, string.IsNullOrEmpty(SaveData.S.wheelRider) ? SaveData.S.rider : SaveData.S.wheelRider));
        // the rider: a file when there is one, else one of the web's eleven built from primitives
        var rid = string.IsNullOrEmpty(SaveData.S.wheelRider) ? SaveData.S.rider : SaveData.S.wheelRider;
        bool proc = Resources.Load<GameObject>("Models/" + rid) == null && ProcRider.Has(rid);
        pug = (proc ? ProcRider.Build(rid, rig) : Models.Spawn(rid, rig, 1.9f, PrimitiveType.Capsule)).transform;
        pug.localPosition = new Vector3(0, 0.19f, 0.2f); pug.localRotation = Quaternion.Euler(0, -70f, 0);
        if (pug.name.EndsWith("(stand-in)")) { pug.localScale = new Vector3(0.9f, 0.95f, 0.9f); pug.localPosition = new Vector3(0, 1.0f, 0.2f); pug.GetComponent<Renderer>().material.color = new Color(0.85f, 0.75f, 0.6f); }
        // the file's own clips would overwrite the bones every frame; the rig poses him instead
        foreach (var a in pug.GetComponentsInChildren<Animation>(true)) { a.Stop(); a.enabled = false; }
        foreach (var a in pug.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        riderRig = null; procRig = null;
        pugBase = pug.localScale;                      // its fitted scale: the fallback squash works relative to this
        if (proc) { procRig = pug.gameObject.AddComponent<ProcRig>(); procRig.Init(rider); }
        else
        {
            riderRig = pug.gameObject.AddComponent<RiderRig>(); riderRig.Init(rider);
            riderRig.SetBoard(b);          // the ring and the ski are sat in, not stood on
            Gear.Dress(pug, SaveData.S);   // what the save says he wears; a rider without the bones is skipped
            // a body the rig cannot pose plays its own file's standing clip instead of standing stiff
            if (!riderRig.Ready) riderClips = GltfClips.Start(pug, rid, new[] { "rest", "Stand_and_Chat", "walk" });
            // (Surf/Character is not applied: lit by its own Lambert it came out far darker than URP Lit,
            // which also takes the sky's reflection and the scene's GI; the rider stays on URP Lit)
        }
        if (beach != null) beach.SetBoard(b.id);
    }

    void Play()
    {
        spawner.Clear(); setWave.Reset(); whirl.Reset(); crate.Reset(); cocos.Reset(); ufo.Reset(); dive.Reset(); island.Reset(); chute.Reset(); boat.Reset();
        ski.Reset();
        Quests.Roll();
        if (Wheel.RunStart()) BuildRider(); if (Wheel.ConsumeChest()) crate.Soon(); bankedDist = bankedScore = bankedCoins = 0;
        rider.Begin(); runSecs = 0;
        rider.transform.rotation = Quaternion.identity; rig.localRotation = Quaternion.identity;
        over = false;
        beach.Show(false); boardGo.SetActive(true); day.pin = -1f; day.keyFrom = Vector3.zero; day.beachLook = false;
        rider.transform.localScale = Vector3.one;
        hud.ShowRide();
        chase.Snap();
    }

    /// the title: the ride stops, he floats in the lane, the dial comes up
    void Menu()
    {
        if (Wheel.RunEnd()) BuildRider();        // a loan from the wheel is given back
        spawner.Clear(); setWave.Reset(); whirl.Reset(); crate.Reset(); cocos.Reset(); ufo.Reset(); dive.Reset(); island.Reset(); chute.Reset(); boat.Reset();
        ski.Reset();
        rider.Idle(0f);
        rider.transform.rotation = Quaternion.identity; rig.localRotation = Quaternion.identity;
        over = false;
        beach.Show(true); day.pin = 0.47f; day.keyFrom = beach.keyFrom; day.beachLook = true;
        WaveField.Amp = 0.78f;      // the web menu's starting swell, so the sea stays off the sand
        beach.PlaceRider(rider.transform, rig, boardGo);
        hud.ShowMenu();
    }

    // the web's drop: a soft round sprite, its alpha (1-r)^1.9 * (0.55 + 0.45 (1-r)^5), white,
    // tinted per splash. A particle with no texture is a square, which is what the spray was.
    static Texture2D dropTex;
    public static Texture2D DropTex()
    {
        if (dropTex != null) return dropTex;
        const int N = 64; dropTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "drop" };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f, r = Mathf.Min(1f, Mathf.Sqrt(dx * dx + dy * dy) * 2f);
            float a = Mathf.Pow(1 - r, 1.9f) * (0.55f + 0.45f * Mathf.Pow(1 - r, 5f));
            px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
        }
        dropTex.SetPixels32(px); dropTex.Apply(true, true);
        return dropTex;
    }

    /// a URP particle material that is actually see-through: the keyword and the blend, not just the slider
    public static Material SoftParticles(Texture2D tex)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return null;
        var m = new Material(sh);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex); m.SetColor("_BaseColor", Color.white);
        return m;
    }

    ParticleSystem BuildSpray()
    {
        var go = new GameObject("Spray"); var ps = go.AddComponent<ParticleSystem>();
        // the web's splash: scale (0.34 + r^3 * 1.9) * 0.34, out at +-3 a second across, 2 to 6 up,
        // living 0.34 to 0.64 s under a gravity of 14
        var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.64f); main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.76f);
        main.gravityModifier = 14f / 32.2f; main.maxParticles = 600;   // gravity: the web's 14 against the world's 32.2 feet
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
        main.startColor = new Color(1, 1, 1, 1f);
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-3f, 3f); vel.y = new ParticleSystem.MinMaxCurve(2f, 6f); vel.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        // the drop shrinks to 0.55 of itself as it goes and fades out at the end
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(0.6f, 0.9f), new Keyframe(1, 0.55f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                                         new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        var m = SoftParticles(DropTex()); if (m != null) { Fx.Soften(m, 0.5f); r.material = m; }   // soft: drops fade into the board and the rider instead of cutting through
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    // the renderer's ambient occlusion follows Settings > Graphics; it is an asset, so it is put
    // back on when the game closes rather than left switched off in the project
    static void Ssao(bool on)
    {
        foreach (var f in Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Universal.ScriptableRendererFeature>())
            if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") f.SetActive(on);
    }
    void ApplyGfx() { Ssao(!SaveData.S.fastFx); }
    void OnApplicationQuit() { Ssao(true); }

    void Update()
    {
        if (!ready) return;
        float dt = Mathf.Min(0.033f, Time.deltaTime);
        Sfx.MusicTick(!rider.running);       // the title track whenever a run is not in progress
        if (rider.running)
        {
            rider.Step(dt); runSecs += dt;
            Sfx.RideBed(rider.speed, rider.carve, rider.airborne, Mathf.Max(dive.Sub, rider.Submerged ? 1f : 0f), ufo.Duck);
            spawner.Step(dt);
            setWave.Step(dt);
            dive.Step(dt);                                   // after the set wave: the launch assigns pz over its trim
            island.Step(dt); chute.Step(dt);
            whirl.Step(dt); crate.Step(dt); cocos.Step(dt); ufo.Step(dt);
            ski.Step(dt);                                    // after the ride, as the web's updateJetSki: the launch it fires is his next frame's
            boat.Step(dt);                                   // after the spawner: the bow shoves what it has just placed
            if (Input.GetKeyDown(KeyCode.U)) ufo.summon = true;           // debug: the saucer now
            if (Input.GetKeyDown(KeyCode.N)) dive.summon = true;          // debug: a jetpack now
            if (Input.GetKeyDown(KeyCode.I)) island.summon = true;        // debug: the island fork now
            if (Input.GetKeyDown(KeyCode.F)) chute.summon = true;         // debug: the chute now
            rider.transform.localScale = Vector3.one * cocos.K;      // the coconut charge shrinks him
            Quests.Scan(rider, dt);
            if (Input.GetKeyDown(KeyCode.B)) setWave.summon = true;      // debug: bring a set now
            if (Input.GetKeyDown(KeyCode.V)) whirl.summon = true;        // debug: a whirlpool now
            chase.swCam = setWave.cam; chase.hold = setWave.tubeRide;
            // the carve's spray: off the TAIL, from the outside rail, flung up and out (Fx.Carve)
            float cv = Mathf.Abs(rider.carve);
            if (!rider.airborne && !rider.wipe && cv > 0.22f && boardGo != null && hullOk)
            {
                float outS = -Mathf.Sign(rider.carve);
                float hcx = (hullMin.x + hullMax.x) * 0.5f, hcz = (hullMin.z + hullMax.z) * 0.5f, hhw = (hullMax.x - hullMin.x) * 0.5f, hhl = (hullMax.z - hullMin.z) * 0.5f;
                var tail = boardGo.transform.TransformPoint(new Vector3(hcx, hullMin.y + hullRise * 0.5f, hcz + hhl * 0.78f));
                tail.x += outS * hhw * 0.85f; tail.y = Mathf.Max(tail.y, rider.waterY - 0.05f);
                fx.Carve(tail, outS, Mathf.InverseLerp(0.22f, 1f, cv), dt);
            }
            wakeA = Mathf.Lerp(wakeA, rider.airborne ? 0f : 0.35f, dt * 4f);
        }
        else if (rider.wipe)
        {
            chase.hold = false;
            if (spawner.eater != null && spawner.eater.at < Spawner.ShkChew) { rider.wipeT += dt; spawner.Step(dt); }   // in the shark's mouth
            else { rider.WipeStep(dt); spawner.Step(dt); }
            wakeA = Mathf.Lerp(wakeA, 0f, dt * 4f);
            if (rider.wipeT >= 0.95f && !over)
            {
                over = true; ufo.RunEnd();
                // the card, as the web fills it: what this run (since any revive) earned, doubled by the
                // wheel if it said so, the levels crossed and what they opened, the goals
                int d = Mathf.RoundToInt(rider.dist), dd = d - bankedDist, ds = rider.score - bankedScore;
                if (rider.score > SaveData.Best) SaveData.Best = rider.score;
                if (d > SaveData.BestDist) SaveData.BestDist = d;
                if (bankedDist == 0) SaveData.S.runs++;
                int goalPay = Quests.RunEnd(d, rider.score);      // distance and trick-point goals
                int earned = (rider.coins - bankedCoins) + goalPay; bool dbl = Wheel.ConsumeX2(); if (dbl) earned *= 2;
                SaveData.Coins += earned;
                Stats.Bump("dist", dd); Stats.Bump("score", ds); Stats.Bump("earned", earned); Stats.Bump("secs", Mathf.RoundToInt(runSecs));
                int was = Progress.Level, xpGain = dd + Mathf.RoundToInt(ds / 10f), lvPay = Progress.AddXp(xpGain), now = Progress.Level;
                int rkPay = Rungs.RankPayBetween(was, now); SaveData.Coins += rkPay;
                var rc = new Hud.RunCard { dist = d, score = rider.score, earned = earned, doubled = dbl, lvUp = now - was, level = now, pay = lvPay + rkPay, rankUp = Rungs.RankIdx(now) > Rungs.RankIdx(was), rider = Hud.RiderName() };
                for (int L = was + 1; L <= now; L++) rc.unlocked.AddRange(Rungs.UnlocksAt(L));
                Wheel.RunEnd(); SaveData.Flush();
                hud.ShowCard(true, rc);
            }
        }
        else
        {
            rider.Idle(dt);
            if (Random.value < dt / 11.5f) Sfx.Gull(40f, 210f, true);
            float gust = 0.10f + 0.95f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.time * 0.21f), 1.5f);
            if (gust < 0.42f) palmSaid = false; else if (gust > 0.62f && !palmSaid) { palmSaid = true; Sfx.Palm(); }
            beach.PlaceRider(rider.transform, rig, boardGo);
            // a tap on the signpost, away from the pills
            bool over2 = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            bool paneOpen = shop.Open || quests.Open || stats.Open || settings.Open || school.Open || profile.Open || guide.Open || wheel.Open || levelInfo.Open || puka.Open || secret.Open;
            if (Input.GetMouseButtonDown(0) && !over2 && !paneOpen) beach.Tap(cam, Input.mousePosition);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var tc = Input.GetTouch(i);
                bool overT = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(tc.fingerId);
                if (tc.phase == TouchPhase.Began && !overT && !paneOpen) beach.Tap(cam, tc.position);
            }
        }
        // ---- the board rides ON the water ----
        // The ride floats him on the waves' height under a handful of points, then leans the whole
        // rider up to forty degrees into a carve about the board's centreline, which drove the
        // inside rail a third of a foot under. Here the hull's underside is checked against the
        // sea itself, at the middle, nose, tail and both rails, and if any of it is deeper than a
        // real board sits (a couple of inches; more on the rails the harder he carves, which is
        // the rail biting) the whole rider is lifted onto it. Up at once, down gently.
        if (rider.running)
        {
            float need = 0f;
            bool onWater = !rider.airborne && !rider.wipe && !rider.Submerged && !setWave.tubeRide && !(island != null && island.Ashore) && !(dive != null && dive.On) && hullOk && boardGo != null;
            if (onWater)
            {
                var bt = boardGo.transform; float tw = rider.t, fz0 = WaveField.Scroll;
                float hcx = (hullMin.x + hullMax.x) * 0.5f, hcz = (hullMin.z + hullMax.z) * 0.5f, hhw = (hullMax.x - hullMin.x) * 0.5f, hhl = (hullMax.z - hullMin.z) * 0.5f;
                float dC = 0.06f, dR = 0.06f + 0.10f * Mathf.Abs(rider.carve);
                for (int i = 0; i < FloatPts.Length; i++)
                {
                    float lx = FloatPts[i].x, lz = FloatPts[i].y, q = Mathf.Abs(lz);      // across and along, as fractions of the half-width and half-length (+z the tail)
                    var p = bt.TransformPoint(new Vector3(hcx + lx * hhw, hullMin.y + hullRise * q * q, hcz + lz * hhl));
                    float sea = WaveField.Height(p.x, fz0 + p.z, tw);
                    need = Mathf.Max(need, sea - (lx != 0f ? dR : dC) - p.y);
                }
                // the nose, last: ahead of him and up on the rocker
                {
                    var p = bt.TransformPoint(new Vector3(hcx, hullMin.y + hullRise * 0.64f, hcz - 0.8f * hhl));
                    need = Mathf.Max(need, WaveField.Height(p.x, fz0 + p.z, tw) - dC - p.y);
                }
            }
            need = Mathf.Clamp(need, 0f, 1.2f);
            floatLift = need > floatLift ? need : Mathf.MoveTowards(floatLift, need, dt * 1.5f);
            rider.transform.position += Vector3.up * floatLift;
        }
        else floatLift = 0f;
        // a board trick: the board turns under him and drops a hand's width while he tucks (web 27016-27026).
        // glTF turns are mirrored into Unity, so the yaw and the roll change sign
        if (boardGo != null)
        {
            boardGo.transform.localPosition = boardBasePos - new Vector3(0f, rider.kfTuck * 0.16f, 0f);
            boardGo.transform.localRotation = boardBaseRot * Quaternion.Euler(0f, -rider.kfYaw * Mathf.Rad2Deg, -rider.kfRoll * Mathf.Rad2Deg);
        }
        if (riderRig != null) riderRig.tuckW = rider.kfTuck;
        if (procRig != null) procRig.tuckW = rider.kfTuck;
        // the pug's stance: the skeleton is posed when the file has one, else the stand-in squashes
        if (procRig != null && procRig.Ready)
        {
            if (rider.running || rider.wipe) procRig.Stance(dt, 1f); else procRig.Idle(dt, Mathf.Min(1f, dt * 4f));
        }
        else if (riderRig != null && riderRig.Ready)
        {
            if (rider.running || rider.wipe) riderRig.Stance(dt, 1f); else riderRig.Idle(dt, Mathf.Min(1f, dt * 4f));
        }
        else
        {
            // a body the rig cannot pose (a stand-in, or the cow, whose skeleton is a quadruped's)
            // squashes with the ride instead -- relative to its OWN scale: the cow's file is tiny
            // and she is fitted up some six hundred times, and easing her height toward 1 flattened
            // her to nothing within a second, which is why she never came down off the saucer
            float crouch = 0.3f + rider.poseCrouch * 0.5f + rider.poseAir * 0.35f + rider.poseLand * 0.45f - rider.poseExt * 0.25f + Mathf.Abs(rider.carve) * 0.15f;
            pug.localScale = new Vector3(pugBase.x, Mathf.Lerp(pug.localScale.y, pugBase.y * (1f - (crouch - 0.3f) * 0.18f), dt * 12f), pugBase.z);
        }
        rig.localRotation = Quaternion.Euler(0, 0, -rider.carve * 8f);

        // the sea is told where the hull is, so no crest comes up through it; not while he is
        // under the water, on the barrel's wall, ashore, or going over
        bool dry = hullOk && boardGo != null && boardGo.activeInHierarchy && !beach.Showing && rider.running && !rider.Submerged && !rider.airborne
                   && Vector3.Dot(boardGo.transform.up, Vector3.up) > 0.6f
                   && !setWave.tubeRide && !(island != null && island.Ashore);
        if (dry)
        {
            var bt = boardGo.transform;
            // the plan about its own centre: the board's pivot is not always in the middle of it
            float hw = (hullMax.x - hullMin.x) * 0.5f, hl = (hullMax.z - hullMin.z) * 0.5f;
            ocean.material.SetMatrix("_HullW2L", bt.worldToLocalMatrix);
            ocean.material.SetVector("_HullS", new Vector4(hw * 0.96f, hl * 0.97f, hullMin.y, 1f));
            ocean.material.SetVector("_HullC", new Vector4((hullMin.x + hullMax.x) * 0.5f, (hullMin.z + hullMax.z) * 0.5f, hullRise, hullThick));
        }
        else ocean.material.SetVector("_HullS", Vector4.zero);
        bool fast = SaveData.S.fastFx;
        if (dof != null) { dof.active = beach.Showing && !fast; if (beach.Showing) dof.focusDistance.value = Vector3.Distance(beach.camAt, beach.boardAt); }
        if (flare != null) flare.active = !beach.Showing && !fast;
        if (reflect != null) { reflect.planeY = beach.Showing ? beach.seaLevel : 0f; reflect.on = !fast && !(dive != null && dive.On); }
        // where the board has been across the lane, so the wake follows the path he drew (Surf/Ocean _WakeP)
        if (rider.running) { wakeHist.Add(new Vector2(Time.time, rider.px)); while (wakeHist.Count > 2 && wakeHist[1].x < Time.time - 4f) wakeHist.RemoveAt(0); }
        else wakeHist.Clear();
        {
            float sp = Mathf.Max(6f, rider.eff);        // the water runs back past him at the run's speed
            for (int i = 0; i < wakeP.Length; i++) wakeP[i] = WakeXAt(Time.time - i * 3f / sp);
            ocean.material.SetFloatArray("_WakeP", wakeP); ocean.material.SetFloat("_WakeC", rider.carve);
        }
        ocean.Tick(rider.t, rider.dist * WaveField.Drift, WaveField.Amp, rider.px, rider.pz + BoardController.BoardHalfL, wakeA, beach.Showing ? beach.seaLevel : 0f, beach.Showing ? -88f : 100000f);   // a shoreline far behind the rider keeps the open sea at full swell
        day.Apply(rider.dist);
        var pm = spray.main; pm.startColor = Color.Lerp(Color.white, day.glow, day.glowAmt * 0.45f);
        // where the board's tail meets the water, for the whitewater it sheds
        fx.tailOk = hullOk && boardGo != null && rider.running;
        if (fx.tailOk)
        {
            var tp = boardGo.transform.TransformPoint(new Vector3((hullMin.x + hullMax.x) * 0.5f, hullMin.y + hullRise * 0.5f, (hullMin.z + hullMax.z) * 0.5f + (hullMax.z - hullMin.z) * 0.5f * 0.82f));
            fx.tail = new Vector3(tp.x, Mathf.Max(tp.y, rider.waterY - 0.05f), tp.z);
            var bt = boardGo.transform;
            fx.tailRight = new Vector3(bt.right.x, 0f, bt.right.z).normalized; fx.tailFwd = new Vector3(-bt.forward.x, 0f, -bt.forward.z).normalized;   // the board's nose is its -z
            fx.tailHalfW = (hullMax.x - hullMin.x) * 0.5f * Mathf.Abs(bt.lossyScale.x);
        }
        fx.Step(dt, pm.startColor.color);
        if (beach.Showing) beach.PlaceCamera(cam, dt); else chase.Step(dt);
    }
}
