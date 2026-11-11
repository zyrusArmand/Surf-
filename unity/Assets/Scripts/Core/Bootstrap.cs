// The one object in the scene. It builds everything else at runtime -- lights, sky, sea, the
// board and the rider, the spawner, the camera, the HUD -- so the project needs no hand-made
// prefabs or scene wiring to run, and every change here is a code change rather than a click.
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    BoardController rider; Spawner spawner; ChaseCamera chase; Hud hud; DayCycle day; Ocean ocean;
    ParticleSystem spray; Transform rig, pug; RiderRig riderRig; float wakeA;
    Beach beach; Camera cam; GameObject boardGo;
    bool over;

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

        day = gameObject.AddComponent<DayCycle>();
        day.sky = skyMat; day.ocean = ocean.material; day.sun = sun;

        // the board and the rider
        var player = new GameObject("Player"); rider = player.AddComponent<BoardController>();
        rig = new GameObject("rig").transform; rig.SetParent(player.transform, false);
        var board = Models.Spawn("board", rig, 0.3f, PrimitiveType.Capsule); boardGo = board;
        Models.FitLength(board, BoardController.BoardHalfL * 2f);
        if (board.name.EndsWith("(stand-in)")) { board.transform.localScale = new Vector3(1.1f, 0.18f, 4.9f); board.transform.localRotation = Quaternion.identity; board.transform.localPosition = Vector3.zero; board.GetComponent<Renderer>().material.color = new Color(0.95f, 0.45f, 0.55f); }
        // the board floats with its underside just under the surface and its deck above it;
        // FitLength centred it on the water line, which hid half of it under an opaque sea
        var bb = Models.Bounds(board);
        board.transform.localPosition += new Vector3(0, -0.12f - bb.min.y, 0);
        bb = Models.Bounds(board);
        pug = Models.Spawn("pug", rig, 1.9f, PrimitiveType.Capsule).transform;
        pug.localPosition = new Vector3(0, bb.max.y - 0.02f, 0.2f); pug.localRotation = Quaternion.Euler(0, -70f, 0);
        if (pug.name.EndsWith("(stand-in)")) { pug.localScale = new Vector3(0.9f, 0.95f, 0.9f); pug.localPosition = new Vector3(0, 1.0f, 0.2f); pug.GetComponent<Renderer>().material.color = new Color(0.85f, 0.75f, 0.6f); }
        // the file's own clips would overwrite the bones every frame; the rig poses him instead
        foreach (var a in pug.GetComponentsInChildren<Animation>(true)) { a.Stop(); a.enabled = false; }
        foreach (var a in pug.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        riderRig = pug.gameObject.AddComponent<RiderRig>(); riderRig.Init(rider);

        // the water
        spawner = new GameObject("Spawner").AddComponent<Spawner>(); spawner.rider = rider;

        // spray
        spray = BuildSpray();
        rider.onSplash = (p, n) => { spray.transform.position = p; spray.Emit(n); };
        spawner.onSplash = rider.onSplash;
        rider.onWipeout = () => { over = false; };

        // camera
        var camGo = new GameObject("Camera"); cam = camGo.AddComponent<Camera>(); camGo.tag = "MainCamera";
        cam.nearClipPlane = 0.5f; cam.farClipPlane = 500f; cam.fieldOfView = 62f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        var ucam = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        ucam.renderPostProcessing = true; ucam.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
        chase = camGo.AddComponent<ChaseCamera>(); chase.rider = rider; chase.cam = cam;

        // post: bloom, a touch of vignette, ACES
        var volGo = new GameObject("Volume"); var vol = volGo.AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); vol.profile = prof;
        var bloom = prof.Add<UnityEngine.Rendering.Universal.Bloom>(); bloom.intensity.Override(0.28f); bloom.threshold.Override(1.04f);
        var vig = prof.Add<UnityEngine.Rendering.Universal.Vignette>(); vig.intensity.Override(0.16f);
        var tone = prof.Add<UnityEngine.Rendering.Universal.Tonemapping>(); tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);

        // the title screen's beach
        beach = gameObject.AddComponent<Beach>(); beach.Build(null);
        beach.onSign = s => { if (s == "Play") Play(); else hud.Toast(s + " is coming in the next round"); };

        // HUD
        hud = new GameObject("HUD").AddComponent<Hud>(); hud.rider = rider;
        hud.onPlay = Play; hud.onMenu = Menu;
        hud.Build();
        Menu();
    }

    void Play()
    {
        spawner.Clear();
        rider.Begin();
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
        spawner.Clear();
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
        if (rider.running)
        {
            rider.Step(dt);
            spawner.Step(dt);
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
            rider.WipeStep(dt);
            spawner.Step(dt);
            wakeA = Mathf.Lerp(wakeA, 0f, dt * 4f);
            if (rider.wipeT >= 0.95f && !over)
            {
                over = true;
                int d = Mathf.RoundToInt(rider.dist);
                if (rider.score > SaveData.Best) SaveData.Best = rider.score;
                if (d > SaveData.BestDist) SaveData.BestDist = d;
                SaveData.Coins += rider.coins; SaveData.Flush();
                hud.ShowCard(true, d, rider.score, SaveData.Best);
            }
        }
        else
        {
            rider.Idle(dt);
            beach.PlaceRider(rider.transform, rig, boardGo);
            // a tap on the signpost, away from the pills
            bool over2 = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0) && !over2) beach.Tap(cam, Input.mousePosition);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var tc = Input.GetTouch(i);
                bool overT = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(tc.fingerId);
                if (tc.phase == TouchPhase.Began && !overT) beach.Tap(cam, tc.position);
            }
        }
        // the pug's stance: the skeleton is posed when the file has one, else the stand-in squashes
        if (riderRig != null && riderRig.Ready)
        {
            if (rider.running || rider.wipe) riderRig.Stance(dt, 1f); else riderRig.Idle(dt, Mathf.Min(1f, dt * 4f));
        }
        else
        {
            float crouch = 0.3f + rider.poseCrouch * 0.5f + rider.poseAir * 0.35f + rider.poseLand * 0.45f - rider.poseExt * 0.25f + Mathf.Abs(rider.carve) * 0.15f;
            pug.localScale = new Vector3(pug.localScale.x, Mathf.Lerp(pug.localScale.y, 1f - crouch * 0.18f, dt * 12f), pug.localScale.z);
        }
        rig.localRotation = Quaternion.Euler(0, 0, -rider.carve * 8f);

        ocean.Tick(rider.t, rider.dist * WaveField.Drift, WaveField.Amp, rider.px, rider.pz + BoardController.BoardHalfL, wakeA, beach.Showing ? beach.seaLevel : 0f);
        day.Apply(rider.dist);
        var pm = spray.main; pm.startColor = Color.Lerp(Color.white, day.glow, day.glowAmt * 0.45f);
        if (beach.Showing) beach.PlaceCamera(cam, dt); else chase.Step(dt);
    }
}
