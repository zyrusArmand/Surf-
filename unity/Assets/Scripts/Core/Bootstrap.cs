// The one object in the scene. It builds everything else at runtime -- lights, sky, sea, the
// board and the rider, the spawner, the camera, the HUD -- so the project needs no hand-made
// prefabs or scene wiring to run, and every change here is a code change rather than a click.
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    BoardController rider; Spawner spawner; ChaseCamera chase; Hud hud; DayCycle day; Ocean ocean;
    ParticleSystem spray; Transform rig, pug; RiderRig riderRig; ProcRig procRig; float wakeA;
    Beach beach; Camera cam; GameObject boardGo; SetWave setWave; Whirlpool whirl; Crate crate; Shop shop; Coconuts cocos; Ufo ufo; Dive dive; Island island; Chute chute;
    Quests quests; StatsPanel stats; SettingsPanel settings; SchoolPanel school; ProfilePanel profile; GuidePanel guide;
    WheelPanel wheel; LevelPanel levelInfo; PukaPanel puka; int bankedDist, bankedScore, bankedCoins;
    bool over; float runSecs; bool palmSaid;

    void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        // light
        var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.75f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 120f; RenderSettings.fogEndDistance = 330f;

        // sky
        var skyMat = new Material(Shader.Find("Surf/Sky"));
        RenderSettings.skybox = skyMat;

        // sea
        var oceanGo = new GameObject("Ocean");
        ocean = oceanGo.AddComponent<Ocean>();
        ocean.Init(new Material(Shader.Find("Surf/Ocean")));
        var chop = Resources.Load<Texture2D>("Models/waternormals");
        if (chop != null) { chop.wrapMode = TextureWrapMode.Repeat; ocean.material.SetTexture("_Chop", chop); ocean.material.SetFloat("_ChopK", 1f); }

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

        // spray
        spray = BuildSpray();
        rider.onSplash = (p, n) => { spray.transform.position = p; spray.Emit(n); };
        spawner.onSplash = rider.onSplash; setWave.onSplash = rider.onSplash; whirl.onSplash = rider.onSplash; crate.onSplash = rider.onSplash; cocos.onSplash = rider.onSplash; ufo.onSplash = rider.onSplash; dive.onSplash = rider.onSplash; island.onSplash = rider.onSplash; chute.onSplash = rider.onSplash;
        rider.onWipeout = () => { over = false; Sfx.Buzz(60, 50, 80); };

        // camera
        var camGo = new GameObject("Camera"); cam = camGo.AddComponent<Camera>(); camGo.tag = "MainCamera";
        cam.nearClipPlane = 0.5f; cam.farClipPlane = 500f; cam.fieldOfView = 62f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        Sfx.Init();
        var ucam = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        ucam.renderPostProcessing = true; ucam.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
        chase = camGo.AddComponent<ChaseCamera>(); chase.rider = rider; chase.cam = cam; chase.dive = dive; chase.island = island; chase.chute = chute; island.chase = chase; chute.chase = chase;

        // post: bloom, a touch of vignette, ACES
        var volGo = new GameObject("Volume"); var vol = volGo.AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); vol.profile = prof;
        var bloom = prof.Add<UnityEngine.Rendering.Universal.Bloom>(); bloom.intensity.Override(0.28f); bloom.threshold.Override(1.04f);
        var vig = prof.Add<UnityEngine.Rendering.Universal.Vignette>(); vig.intensity.Override(0.16f);
        // no tone mapping: the web's sea and sky are drawn raw and encoded once, and that is
        // where their colour comes from; a filmic curve on top is what made the sea grey
        var tone = prof.Add<UnityEngine.Rendering.Universal.Tonemapping>(); tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.None);

        // the title screen's beach
        beach = gameObject.AddComponent<Beach>(); beach.Build(null);
        beach.onSign = s => { if (s == "Play") Play(); else if (s == "Shop") shop.Show(true); else if (s == "Quests") quests.Show(true); else school.Show(true); };
        beach.SetBoard(SaveData.S.equipped);

        // HUD
        hud = new GameObject("HUD").AddComponent<Hud>(); hud.rider = rider; hud.cocos = cocos; hud.chase = chase; hud.island = island; hud.dive = dive;
        hud.onPlay = Play; hud.onMenu = Menu; hud.onShop = () => shop.Show(true);
        spawner.onSplat = c => hud.Splat(c);
        hud.Build();
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
        hud.onWheel = () => wheel.Show(true); hud.onLevel = () => levelInfo.Show(true); hud.onPuka = () => puka.Show(true);
        wheel.onChange = shop.onChange;      // a loan or gear won shows at once; the purse refreshes
        // a spare life from the wheel: back on the board where he went in, the water ahead cleared
        hud.onRevive = () =>
        {
            bankedDist = Mathf.RoundToInt(rider.dist); bankedScore = rider.score; bankedCoins = rider.coins;
            for (int i = spawner.obstacles.Count - 1; i >= 0; i--) if (spawner.obstacles[i].z > -48f) spawner.Remove(i);
            spawner.eater = null; setWave.Reset(); whirl.Reset();
            rider.Revive(); over = false; hud.ShowRide(); chase.Snap();
        };
        hud.onQuests = () => quests.Show(true); hud.onSettings = () => settings.Show(true);
        hud.onProfile = () => profile.Show(true); hud.onSchool = () => school.Show(true);
        profile.onStats = () => stats.Show(true);
        Quests.onToast = hud.Toast;
        settings.onChange = () => Sfx.Refresh();
        quests.onClose = settings.onClose = profile.onClose = stats.onClose = school.onClose = guide.onClose = wheel.onClose = levelInfo.onClose = puka.onClose = () => { if (!rider.running && !rider.wipe) hud.ShowMenu(); };
        Menu();
    }

    // the equipped board and rider, from the save; called again when the shop changes them
    void BuildRider()
    {
        if (boardGo != null) Destroy(boardGo);
        if (pug != null) Destroy(pug.gameObject);
        // a loan (the wheel's, or the saucer's cow) outranks the equipped pair
        var b = BoardCatalog.Get(string.IsNullOrEmpty(SaveData.S.wheelLoan) ? SaveData.S.equipped : SaveData.S.wheelLoan);
        var board = BoardMesh.Build(b); board.transform.SetParent(rig, false); boardGo = board; Hull.Board = board; Hull.ClearMarks();
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
        if (proc) { procRig = pug.gameObject.AddComponent<ProcRig>(); procRig.Init(rider); }
        else
        {
            riderRig = pug.gameObject.AddComponent<RiderRig>(); riderRig.Init(rider);
            riderRig.SetBoard(b);          // the ring and the ski are sat in, not stood on
            Gear.Dress(pug, SaveData.S);   // what the save says he wears; a rider without the bones is skipped
        }
        if (beach != null) beach.SetBoard(b.id);
    }

    void Play()
    {
        spawner.Clear(); setWave.Reset(); whirl.Reset(); crate.Reset(); cocos.Reset(); ufo.Reset(); dive.Reset(); island.Reset(); chute.Reset();
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
        spawner.Clear(); setWave.Reset(); whirl.Reset(); crate.Reset(); cocos.Reset(); ufo.Reset(); dive.Reset(); island.Reset(); chute.Reset();
        rider.Idle(0f);
        rider.transform.rotation = Quaternion.identity; rig.localRotation = Quaternion.identity;
        over = false;
        beach.Show(true); day.pin = 0.47f; day.keyFrom = beach.keyFrom; day.beachLook = true;
        WaveField.Amp = 0.78f;      // the web menu's starting swell, so the sea stays off the sand
        beach.PlaceRider(rider.transform, rig, boardGo);
        hud.ShowMenu();
    }

    ParticleSystem BuildSpray()
    {
        var go = new GameObject("Spray"); var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startLifetime = 0.5f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.5f);
        main.gravityModifier = 0.6f; main.maxParticles = 600; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
        main.startColor = new Color(1, 1, 1, 0.85f);
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.9f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-3f, 3f); vel.y = new ParticleSystem.MinMaxCurve(2f, 6f); vel.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                                         new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        var sh2 = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh2 != null) { r.material = new Material(sh2); r.material.SetFloat("_Surface", 1); r.material.SetFloat("_Blend", 0); r.material.renderQueue = 3000; }
        return ps;
    }

    void Update()
    {
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
            if (Input.GetKeyDown(KeyCode.U)) ufo.summon = true;           // debug: the saucer now
            if (Input.GetKeyDown(KeyCode.N)) dive.summon = true;          // debug: a jetpack now
            if (Input.GetKeyDown(KeyCode.I)) island.summon = true;        // debug: the island fork now
            if (Input.GetKeyDown(KeyCode.F)) chute.summon = true;         // debug: the chute now
            rider.transform.localScale = Vector3.one * cocos.K;      // the coconut charge shrinks him
            Quests.Scan(rider, dt);
            if (Input.GetKeyDown(KeyCode.B)) setWave.summon = true;      // debug: bring a set now
            if (Input.GetKeyDown(KeyCode.V)) whirl.summon = true;        // debug: a whirlpool now
            chase.swCam = setWave.cam; chase.hold = setWave.tubeRide;
            // rail spray while carving
            float cv = Mathf.Abs(rider.carve);
            if (!rider.airborne && cv > 0.22f && Random.value < dt * 30f * cv)
            {
                spray.transform.position = new Vector3(rider.px - Mathf.Sign(rider.carve) * 0.75f, rider.waterY + 0.15f, rider.pz + 0.5f);
                spray.Emit(Mathf.CeilToInt(cv * 4));
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
            bool paneOpen = shop.Open || quests.Open || stats.Open || settings.Open || school.Open || profile.Open || guide.Open || wheel.Open || levelInfo.Open || puka.Open;
            if (Input.GetMouseButtonDown(0) && !over2 && !paneOpen) beach.Tap(cam, Input.mousePosition);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var tc = Input.GetTouch(i);
                bool overT = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(tc.fingerId);
                if (tc.phase == TouchPhase.Began && !overT && !paneOpen) beach.Tap(cam, tc.position);
            }
        }
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
            float crouch = 0.3f + rider.poseCrouch * 0.5f + rider.poseAir * 0.35f + rider.poseLand * 0.45f - rider.poseExt * 0.25f + Mathf.Abs(rider.carve) * 0.15f;
            pug.localScale = new Vector3(pug.localScale.x, Mathf.Lerp(pug.localScale.y, 1f - crouch * 0.18f, dt * 12f), pug.localScale.z);
        }
        rig.localRotation = Quaternion.Euler(0, 0, -rider.carve * 8f);

        ocean.Tick(rider.t, rider.dist * WaveField.Drift, WaveField.Amp, rider.px, rider.pz + BoardController.BoardHalfL, wakeA, beach.Showing ? beach.seaLevel : 0f, beach.Showing ? -88f : 100000f);   // a shoreline far behind the rider keeps the open sea at full swell
        day.Apply(rider.dist);
        var pm = spray.main; pm.startColor = Color.Lerp(Color.white, day.glow, day.glowAmt * 0.45f);
        if (beach.Showing) beach.PlaceCamera(cam, dt); else chase.Step(dt);
    }
}
