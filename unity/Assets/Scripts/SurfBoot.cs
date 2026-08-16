using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ---------------------------------------------------------------------------
//  Surf — Unity port, step 3: the ride moves.
//
//  Step 1 put the scale on screen, step 2 put the sea under it. This adds the
//  motion: forward speed that builds with distance, steering across the face,
//  a camera that sits behind and follows, and a sea that scrolls past.
//
//  The one idea worth understanding before reading any of it: THE RIDER NEVER
//  GOES ANYWHERE. He stays at the origin and the world is moved past him. That
//  is how the browser game works and it is why the sea mesh can be a fixed grid
//  at the origin and never need to chase anything — the swell is sampled at an
//  offset that grows with distance travelled, so the water slides underneath
//  while the vertices stay put.
// ---------------------------------------------------------------------------


public static class SurfWater
{
    struct Comp
    {
        public float dx, dz, k, a, ek, ep, w;
        public Comp(float dx, float dz, float k, float a, float ek, float ep)
        {
            this.dx = dx; this.dz = dz; this.k = k; this.a = a; this.ek = ek; this.ep = ep;
            this.w = Mathf.Sqrt(WAVE_G * k) * WAVE_TSCALE;
        }
    }

    const float WAVE_G = 9.8f, WAVE_TSCALE = 1.0f;
    const float ENV_BASE = 0.32f, ENV_SWING = 0.68f;
    const float ZSTRETCH = 1.0f;

    public const float WAVE_DRIFT = 0.46f;   // how fast the swell rolls past as you travel
    public static float waveAmp = 0.78f;

    // ---- the set wave ----
    // The ambient swell above is only ever about four inches. THIS is the water the
    // game is actually about: a wall that arrives from off to one side, stands up,
    // and peels. Everything here is the browser game's own geometry.
    public const float SW_PEAK = 7.0f;    // how tall the crest stands
    public const float SW_FACE = 10.0f;   // how far the steep front face runs
    public const float SW_BACK = 34.0f;   // and the long shoulder behind the crest
    const float SW_BRK  = 30.0f;          // world z of the break — well behind the camera
    const float SW_COLL = 26.0f;          // and how far behind it takes to fall to whitewater
    public const float SW_CRASH_X = 46f;  // how far out it starts
    public const float SW_HOLD_X  = 9.0f; // and how close it comes in

    public static float dist;             // how far the rider has travelled, in feet
    public static float swA, swX;         // crest height and crest position in x
    public static float swS = 1f;         // which side it came from

    // 0 at the break, 1 well behind it, where the section has collapsed to whitewater
    static float PeelB(float z)
    {
        float u = Mathf.Clamp01((z - SW_BRK) / SW_COLL);
        return u * u * (3f - 2f * u);
    }

    // d > 0 is behind the crest — the wave's own water. d < 0 is the trough you ride in.
    public static float SetWaveH(float x, float z)
    {
        if (swA <= 0.0015f) return 0f;
        float d = (x - swX) * swS;
        float f;
        if (d <= 0f)
        {
            // A concave power curve, not a smoothstep. A smoothstep flattens at its
            // top, so the face arrived at the crest with ZERO slope — a rounded hump
            // — and the lip then left it at nearly a right angle. That crease read as
            // a flat slab with a separate spiral stuck on it. This is flat water, then
            // a wall still steepening as it reaches the lip, which is what a wave about
            // to barrel actually does.
            float k = Mathf.Max(0f, 1f + d / SW_FACE);
            f = Mathf.Pow(k, 3.2f);
        }
        else
        {
            float k = Mathf.Max(0f, 1f - d / SW_BACK);
            f = 0.34f + 0.66f * k * k * (3f - 2f * k);
        }
        // z does NOT arrive in world coordinates — every caller passes the scrolled
        // frame the swell is written in, because the swell has to roll past as you
        // travel. The set wave does not scroll: it STANDS STILL in the world while you
        // ride along it. So the scroll comes back off here before the peel is read.
        //
        // In the browser game getting this wrong cost a rendering that looked perfect
        // and a board that sank: the visible wave stood at full height while the water
        // the board actually rode had collapsed to 45% underneath it.
        float wz = z - dist * WAVE_DRIFT;
        // Ahead of the break the wall stands at full height — an unbroken wave is not a
        // smaller wave. Only behind it does the crest drop away.
        return swA * (1f - 0.55f * PeelB(wz)) * f;
    }

    static readonly Comp[] WAVES = {
        new Comp( 0.00f, 1.00f, 0.190f, 1.000f, 0.38f, 0.00f),
        new Comp( 0.60f, 0.80f, 0.310f, 0.520f, 0.42f, 2.10f),
        new Comp(-0.74f, 0.67f, 0.470f, 0.260f, 0.50f, 4.20f),
        new Comp( 0.40f,-0.92f, 0.720f, 0.110f, 0.58f, 1.30f),
        new Comp( 1.00f, 0.00f, 1.050f, 0.050f, 0.66f, 5.40f),
        new Comp( 0.86f, 0.51f, 1.700f, 0.030f, 0.72f, 3.10f),
        new Comp(-0.35f, 0.94f, 2.600f, 0.016f, 0.80f, 0.70f),
    };

    static float Component(in Comp W, float x, float z, float t)
    {
        float u = W.dx * x + W.dz * z * ZSTRETCH;
        float env = ENV_BASE + ENV_SWING * Mathf.Sin(u * W.k * W.ek - t * W.w * W.ek * 0.5f + W.ep);
        return W.a * env * Mathf.Sin(u * W.k - t * W.w);
    }

    public static float WaveH(float x, float z, float t)
    {
        float h = 0f;
        for (int i = 0; i < WAVES.Length; i++) h += Component(WAVES[i], x, z, t);
        return h * waveAmp + SetWaveH(x, z);
    }

    // the long swell alone — what a floating thing rides, with the chop filtered out
    public static float SwellH(float x, float z, float t)
    {
        float h = 0f;
        for (int i = 0; i < 2; i++) h += Component(WAVES[i], x, z, t);
        return h * waveAmp * 1.06f + SetWaveH(x, z);
    }
}


public class SurfBoot : MonoBehaviour
{
    // ---- units: ONE UNITY UNIT IS ONE FOOT ----
    const float RIDER_FT = 2.20f;
    const float BOARD_L  = 5.83f, BOARD_W = 1.42f, BOARD_T = 0.25f;
    const float BOARD_HALF_L = 2.45f;

    // ---- the board's stats ----
    // In the full game these come from whichever board is equipped and every one
    // of them differs. A default board is all ones, which is what this is.
    const float BS_speed = 1f, BS_turn = 1f, BS_grip = 1f, BS_top = 0f;

    // ---- how it moves ----
    // Straight from the browser game. Speed BUILDS with distance rather than
    // being handed to you: 16.2 ft/s off the start, climbing by one for every
    // 210 ft travelled, so a long run is genuinely faster than a short one.
    const float SPEED_BASE = 16.2f, SPEED_RAMP = 210f, SPEED_CAP = 100f;
    // Steering is momentum, not position. The stick adds sideways acceleration
    // and GRIP bleeds it off — one constant setting both how far the board runs
    // across the face and how long it takes to stop. It is most of why a gun and
    // a fish feel like different boards.
    const float TURN_ACCEL = 55f, GRIP = 3.4f;
    const float LANE_LIMIT = 17f;            // slide out to the far lanes and stop there

    // ---- the ride camera ----
    const float FOV = 62f, NEAR = 0.5f, FAR = 500f;
    const float CAM_Y = 4.9f, CAM_Z = -11f, CAM_PITCH = 8.76f;
    const float CAM_FLOOR = 2.3f;

    const int   SEA_DIV  = 100;
    const float SEA_SIZE = 200f;

    Camera cam;
    Transform board;
    Mesh seaMesh;
    Vector3[] seaVerts;

    // ---- the state of the ride ----
    float clock;      // wave time
    float dist;       // how far he has travelled, in feet — drives speed AND the swell offset
    float speed, eff; // base speed, and the effective speed after steering
    float px, vx;     // where he is across the face, and how fast he is going across it
    float camX;       // the camera's own lateral position, which lags his

    // ---- what is in the water ----
    // A kind is its two half-widths and how tall it stands. hx and hz are separate on
    // purpose: a log is a plank across your path — long and thin — and giving it one
    // radius for both made it kill you from a distance equal to its LENGTH in the
    // direction you were coming from. The weight is how often it turns up, and the
    // last number is how far into a run it starts appearing at all.
    struct Kind
    {
        public string name; public float hx, hz, h, size; public Color col;
        public int weight; public float from;
        public Kind(string n, float hx, float hz, float h, float size, Color c, int w, float from)
        { name = n; this.hx = hx; this.hz = hz; this.h = h; this.size = size; col = c; weight = w; this.from = from; }
    }

    static readonly Kind[] KINDS = {
        new Kind("buoy",   1.05f, 1.05f, 1.60f, 3.17f, new Color(0.85f,0.20f,0.18f), 22,   0f),
        new Kind("log",    2.50f, 0.62f, 1.10f, 5.10f, new Color(0.45f,0.27f,0.14f), 17,   0f),
        new Kind("jelly",  1.15f, 1.15f, 1.30f, 2.03f, new Color(0.85f,0.45f,0.85f), 20, 340f),
        new Kind("bigfin", 1.70f, 0.50f, 4.20f, 4.00f, new Color(0.32f,0.38f,0.46f), 11, 560f),
    };

    class Ob
    {
        public Transform t; public int kind;
        public float x, z, lx;      // lx is where it was last frame, for the sweep
        public bool passed;
    }

    readonly System.Collections.Generic.List<Ob> obs = new System.Collections.Generic.List<Ob>();
    float nextSpawn = 40f;
    int lastKind = -1, sameRun;

    // ---- the state of the run ----
    bool running = true;
    float overT;                    // how long the wipeout card has been up
    int score, shells;
    string lastMsg = "";
    float msgT;

    // ---- where the set is in its life ----
    // The browser game runs a full phase machine here — arming, riding, the tube, the
    // exit, the scoring — and none of that is ported yet. This is the ARRIVAL only:
    // a wave builds, stands, and fades, from the far side each time. It is enough to
    // put the real water on screen and ride it, and the phases go on top of it later.
    float setT;
    int   setPhase;   // 0 waiting, 1 building, 2 standing, 3 fading
    const float SET_WAIT = 9f, SET_BUILD = 4.5f, SET_STAND = 11f, SET_FADE = 4f;

    void Awake()
    {
        BuildCamera();
        BuildSun();
        BuildSea();

        board = Box("Board", new Vector3(BOARD_W, BOARD_T, BOARD_L),
                    new Color(0.96f, 0.55f, 0.30f)).transform;

        BuildPug();

        Debug.Log("[Surf] carve with A / D, the arrow keys, or by holding the mouse on " +
                  "the left or right half of the screen. Speed builds with distance. " +
                  "A set arrives every twenty seconds or so — carve into the face.");
    }

    void Update()
    {
        float dt = Time.deltaTime;
        clock += dt;
        msgT = Mathf.Max(0f, msgT - dt);

        if (!running)
        {
            // The run is over. Hold the card up for a moment and then start again —
            // there is no menu yet, and a game you have to restart by hand is a game
            // nobody plays for long enough to find the next bug in.
            overT += dt;
            if (overT > 2.2f) Restart();
            return;
        }

        // ---- forward speed ----
        speed = Mathf.Min(SPEED_CAP + BS_top, (SPEED_BASE + dist / SPEED_RAMP) * BS_speed + BS_top);

        // ---- steering ----
        float sx = Steer();
        vx += sx * TURN_ACCEL * BS_turn * dt;
        vx -= vx * GRIP * BS_grip * dt;
        px += vx * dt;
        if (px < -LANE_LIMIT) { px = -LANE_LIMIT; vx = 0f; }
        if (px >  LANE_LIMIT) { px =  LANE_LIMIT; vx = 0f; }

        // Carving costs nothing and gains a little: crossing the face adds to the
        // ground he covers, which is why a run that weaves outruns one that does not.
        eff = Mathf.Max(SPEED_BASE, speed + Mathf.Abs(vx) * 0.3f);
        dist += eff * dt;
        SurfWater.dist = dist;      // the set wave needs it to undo the swell's scroll

        UpdateSet(dt);

        // ---- the sea scrolls ----
        // This is the whole trick. The vertices never move in x or z; the swell is
        // sampled at an offset that grows with distance, so the water slides past
        // underneath a grid that is standing still.
        float drift = dist * SurfWater.WAVE_DRIFT;
        for (int v = 0; v < seaVerts.Length; v++)
            seaVerts[v].y = SurfWater.WaveH(seaVerts[v].x, seaVerts[v].z + drift, clock);
        seaMesh.vertices = seaVerts;
        seaMesh.RecalculateNormals();

        // ---- the board ----
        float yNose = SurfWater.SwellH(px, BOARD_HALF_L + drift, clock);
        float yTail = SurfWater.SwellH(px, -BOARD_HALF_L + drift, clock);
        board.position = new Vector3(px, (yNose + yTail) * 0.5f, 0f);
        // it lies ALONG the wave, not flat on top of one: pitch is the angle of the
        // line joining the water under its nose to the water under its tail
        float pitch = -Mathf.Atan2(yNose - yTail, BOARD_HALF_L * 2f) * Mathf.Rad2Deg;
        // and it banks into the turn, which is the whole of what carving looks like
        board.rotation = Quaternion.Euler(pitch, 0f, Mathf.Clamp(-vx * 1.6f, -38f, 38f));

        UpdateObstacles(dt, drift);

        // ---- and the camera sits behind him ----
        camX += (px - camX) * Mathf.Min(1f, dt * 7f);
        float waterY = SurfWater.SwellH(px, drift, clock);
        cam.transform.position = new Vector3(camX,
                                             Mathf.Max(waterY + CAM_Y, waterY + CAM_FLOOR),
                                             CAM_Z);
    }

    // ---- Astro ----
    // Every measurement below is a fraction of his standing height, and every one of
    // those fractions comes from the browser game's rig rather than from taste. They
    // are worth stating because they are the whole character: the head is 37% of him,
    // the body is 30% wide against 26% deep — broader than it is thick, like anything
    // that walks on two legs — and the legs are a quarter of him. Those numbers took a
    // long argument to arrive at and re-guessing them here would throw it away.
    //
    // He is built as a plain hierarchy under one root, and the root is parented to the
    // board, so from here on he simply goes wherever the board goes.
    void BuildPug()
    {
        var fur   = new Color(0.863f, 0.694f, 0.451f);   // 0xdcb173, a fawn pug
        var dark  = new Color(0.200f, 0.157f, 0.122f);   // 0x33281f, the mask and the ears
        var nose  = new Color(0.078f, 0.067f, 0.094f);   // 0x141118
        var belly = new Color(0.914f, 0.784f, 0.576f);   // 0xe9c893, a paler chest
        var eyeC  = new Color(0.05f, 0.05f, 0.06f);
        var glint = new Color(1f, 1f, 1f);

        float H = RIDER_FT;

        var root = new GameObject("Astro").transform;
        root.SetParent(board, false);
        // undo the board's own scale, so his numbers are in feet and not in board-lengths
        root.localScale = new Vector3(1f / BOARD_W, 1f / BOARD_T, 1f / BOARD_L);
        root.localPosition = new Vector3(0f, BOARD_T * 0.5f / BOARD_T, 0f);
        // side-on to the board, which is how anybody stands on one
        root.localRotation = Quaternion.Euler(0f, -68f, 0f);

        // ---- the body, bottom up ----
        Part(root, "Hips",  fur,  new Vector3(0f, 0.29f * H, 0f),
             new Vector3(0.28f * H, 0.22f * H, 0.25f * H));
        Part(root, "Torso", fur,  new Vector3(0f, 0.47f * H, 0f),
             new Vector3(0.30f * H, 0.40f * H, 0.26f * H));
        // a paler chest panel. Two shades off the coat, not a cream bib: it is there to
        // separate the chest from the hips from the legs, which from behind are otherwise
        // one continuous fawn mass with nothing to read.
        Part(root, "Chest", belly, new Vector3(0f, 0.47f * H, -0.10f * H),
             new Vector3(0.20f * H, 0.28f * H, 0.10f * H));
        Part(root, "Neck",  fur,  new Vector3(0f, 0.635f * H, -0.01f * H),
             new Vector3(0.14f * H, 0.12f * H, 0.14f * H));

        // ---- the head ----
        var head = Part(root, "Head", fur, new Vector3(0f, 0.795f * H, -0.01f * H),
                        new Vector3(0.37f * H, 0.375f * H, 0.35f * H));

        // The MASK is the pug. On a real one the black covers the whole lower face and
        // runs up either side of the bridge; the fawn is only the crown and the cheeks.
        Part(head, "Muzzle", dark, new Vector3(0f, -0.30f, -0.62f), new Vector3(0.62f, 0.50f, 0.52f));
        Part(head, "Bridge", dark, new Vector3(0f, -0.05f, -0.70f), new Vector3(0.26f, 0.42f, 0.30f));
        Part(head, "Nose",   nose, new Vector3(0f, -0.24f, -0.92f), new Vector3(0.20f, 0.15f, 0.14f));

        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? -1f : 1f;
            // a ring of coat-dark around each eye, which is what makes them read as big
            // at a distance where the eye itself is a few pixels across
            Part(head, "EyeRing", dark,  new Vector3(sx * 0.42f, 0.22f, -0.62f),
                 new Vector3(0.34f, 0.32f, 0.20f));
            Part(head, "Eye",    eyeC,  new Vector3(sx * 0.41f, 0.23f, -0.72f),
                 new Vector3(0.24f, 0.24f, 0.24f));
            Part(head, "Glint",  glint, new Vector3(sx * 0.35f, 0.31f, -0.83f),
                 new Vector3(0.07f, 0.07f, 0.07f));
            // small dark ears folded flat against the top corners of the skull. Standing
            // them away from the head is the one detail that turns a round-headed animal
            // into a bear.
            Part(head, "Ear", dark, new Vector3(sx * 0.72f, 0.52f, 0.10f),
                 new Vector3(0.26f, 0.44f, 0.40f));
        }

        // ---- limbs ----
        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? -1f : 1f;
            Part(root, "Leg",  fur, new Vector3(sx * 0.075f * H, 0.135f * H, 0f),
                 new Vector3(0.12f * H, 0.25f * H, 0.13f * H));
            Part(root, "Foot", fur, new Vector3(sx * 0.075f * H, 0.025f * H, -0.02f * H),
                 new Vector3(0.13f * H, 0.06f * H, 0.19f * H));
            // arms out along the board — one down the line, one back — because that is
            // what a surfer's arms do and it is the only way they read from astern
            Part(root, "Arm", fur, new Vector3(sx * 0.15f * H, 0.50f * H, sx * 0.14f * H),
                 new Vector3(0.11f * H, 0.11f * H, 0.30f * H));
            Part(root, "Paw", fur, new Vector3(sx * 0.16f * H, 0.49f * H, sx * 0.29f * H),
                 new Vector3(0.13f * H, 0.13f * H, 0.13f * H));
        }

        // the curl on the rump — from astern it is the single thing that says "pug"
        Part(root, "Tail", fur, new Vector3(0f, 0.40f * H, 0.15f * H),
             new Vector3(0.13f * H, 0.16f * H, 0.10f * H));
    }

    // one rounded lump of him. Everything is a sphere: it is what the browser rig is
    // made of too, and a soft body reads better than a faceted one at this size.
    Transform Part(Transform parent, string name, Color c, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.GetComponent<Renderer>().sharedMaterial = Mat(c);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        return go.transform;
    }

    // ---- everything in the water ----
    void UpdateObstacles(float dt, float drift)
    {
        // spawn when the run has covered enough ground since the last one
        if (dist >= nextSpawn)
        {
            Spawn();
            // The gap closes as the run gets faster, but never past the point where a
            // board travelling at speed cannot physically get round the next thing.
            nextSpawn = dist + Mathf.Max(26f, 62f - dist / 90f) * Random.Range(0.75f, 1.35f);
        }

        for (int i = obs.Count - 1; i >= 0; i--)
        {
            var o = obs[i];
            float z0 = o.z;                  // where it was a frame ago
            o.z -= eff * dt;

            if (o.z < -22f)                  // well past him, and gone
            {
                Destroy(o.t.gameObject);
                obs.RemoveAt(i);
                continue;
            }

            var K = KINDS[o.kind];
            o.t.position = new Vector3(o.x, SurfWater.SwellH(o.x, o.z + drift, clock) + K.h * 0.20f, o.z);

            // ---- the hit ----
            // The board is a five foot plank, not a point, so it is tested as a shape
            // running nose to tail and SWEPT over the ground the obstacle covered this
            // frame. Without the sweep a thing closing fast steps straight over the
            // board between two frames — you watch it pass through you and nothing
            // happens. It is an ellipse and not a circle: hx is a sideways clearance so
            // it takes the board's half-width, hz does not, because the board's own
            // length is already handled by testing its nose and tail.
            float RX = K.hx + BOARD_HALF_W;
            float RZ = K.hz;
            float xlo = Mathf.Min(o.lx, o.x), xhi = Mathf.Max(o.lx, o.x);
            float dxo = px < xlo ? xlo - px : (px > xhi ? px - xhi : 0f);
            o.lx = o.x;

            bool contact = false;
            if (dxo < RX)
            {
                float reach = RZ * Mathf.Sqrt(Mathf.Max(0f, 1f - (dxo / RX) * (dxo / RX)));
                float nose = -BOARD_HALF_L, tail = BOARD_HALF_L;
                contact = (Mathf.Min(z0, o.z) - reach) < tail && (Mathf.Max(z0, o.z) + reach) > nose;
            }

            if (contact) { Wipeout(K.name); return; }

            // ---- the near miss ----
            // It slid past the tail without ever touching, and it went past CLOSE — a
            // margin you could put a hand in. Fear converted into score.
            if (!o.passed && o.z < -BOARD_HALF_L)
            {
                o.passed = true;
                float margin = dxo - RX;
                if (margin > 0f && margin < 1.6f)
                {
                    shells++; score += 25;
                    Msg("CLOSE! +25");
                }
            }
        }
    }

    void Spawn()
    {
        // Weighted, and gated on how far into the run you are — a jellyfish does not
        // turn up in the first hundred feet. Streaks are broken up deliberately: three
        // of the same thing running is a pattern, and a pattern stops being an obstacle.
        int pick = 0;
        for (int tries = 0; tries < 8; tries++)
        {
            int total = 0;
            for (int i = 0; i < KINDS.Length; i++) if (dist >= KINDS[i].from) total += KINDS[i].weight;
            int roll = Random.Range(0, total), cand = 0;
            for (int i = 0; i < KINDS.Length; i++)
            {
                if (dist < KINDS[i].from) continue;
                roll -= KINDS[i].weight;
                if (roll <= 0) { cand = i; break; }
            }
            if (cand == lastKind && sameRun >= 2) continue;
            pick = cand; break;
        }
        sameRun = pick == lastKind ? sameRun + 1 : 1;
        lastKind = pick;

        var K = KINDS[pick];
        var o = new Ob { kind = pick, x = Random.Range(-LANE_LIMIT, LANE_LIMIT), z = 150f };
        o.lx = o.x;

        GameObject g;
        if (K.name == "log")
            g = Box("log", new Vector3(1.0f, 1.0f, K.size), K.col);
        else if (K.name == "bigfin")
        {
            g = Box("bigfin", new Vector3(0.35f, K.size, 1.6f), K.col);
        }
        else
        {
            g = Prim(K.name, PrimitiveType.Sphere, K.col);
            g.transform.localScale = Vector3.one * K.size * 0.6f;
        }
        o.t = g.transform;
        obs.Add(o);
    }

    void Wipeout(string what)
    {
        running = false;
        overT = 0f;
        lastMsg = "WIPEOUT — " + what;
        msgT = 99f;
    }

    void Restart()
    {
        foreach (var o in obs) if (o.t != null) Destroy(o.t.gameObject);
        obs.Clear();
        running = true;
        dist = 0f; px = 0f; vx = 0f; camX = 0f;
        SurfWater.dist = 0f; SurfWater.swA = 0f;
        setPhase = 0; setT = 0f;
        nextSpawn = 40f; lastKind = -1; sameRun = 0;
        score = 0; shells = 0; lastMsg = ""; msgT = 0f;
    }

    void Msg(string m) { lastMsg = m; msgT = 1.4f; }

    // ---- the numbers, on screen ----
    // Unity's immediate-mode GUI, which is not what a shipped game uses — it is here
    // because it needs no canvas, no prefabs and no layout work, and the point right
    // now is to be able to READ the run rather than to style it.
    void OnGUI()
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        st.normal.textColor = Color.white;
        GUI.Label(new Rect(16, 12, 600, 30),
                  $"DIST {dist / 3.281f:0} m     MPH {eff * 0.682f:0}     SHELLS {shells}     SCORE {score}", st);

        if (msgT > 0f && lastMsg.Length > 0)
        {
            var mid = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold,
                                                     alignment = TextAnchor.MiddleCenter };
            mid.normal.textColor = running ? Color.yellow : new Color(1f, 0.4f, 0.35f);
            GUI.Label(new Rect(0, Screen.height * 0.34f, Screen.width, 50), lastMsg, mid);
        }
    }

    // ---- the set arriving ----
    void UpdateSet(float dt)
    {
        setT += dt;
        switch (setPhase)
        {
            case 0:
                SurfWater.swA = 0f;
                if (setT >= SET_WAIT) { setPhase = 1; setT = 0f; SurfWater.swS = -SurfWater.swS; }
                break;
            case 1:
            {
                // It comes in from a long way out and grows as it closes. Eased rather
                // than linear, so it settles into position instead of arriving and
                // stopping dead.
                float k = Mathf.Clamp01(setT / SET_BUILD);
                float e = k * k * (3f - 2f * k);
                SurfWater.swA = SurfWater.SW_PEAK * e;
                SurfWater.swX = SurfWater.swS *
                    Mathf.Lerp(SurfWater.SW_CRASH_X, SurfWater.SW_HOLD_X, e);
                if (setT >= SET_BUILD) { setPhase = 2; setT = 0f; }
                break;
            }
            case 2:
                SurfWater.swA = SurfWater.SW_PEAK;
                SurfWater.swX = SurfWater.swS * SurfWater.SW_HOLD_X;
                if (setT >= SET_STAND) { setPhase = 3; setT = 0f; }
                break;
            case 3:
                SurfWater.swA = SurfWater.SW_PEAK * (1f - Mathf.Clamp01(setT / SET_FADE));
                if (setT >= SET_FADE) { setPhase = 0; setT = 0f; }
                break;
        }
    }

    // ---- what the player is asking for, whichever way they are asking ----
    // Unity 6 projects use the Input System package, and the old UnityEngine.Input
    // class throws outright when it is active rather than quietly returning
    // nothing — which, called from the top of Update, takes the whole frame down
    // with it. Both paths are compiled here so this runs on either setting.
    //
    // Touch and mouse are the same gesture as the phone game: a finger on the left
    // half of the screen carves left, the right half carves right. That also makes
    // it playable in the editor with nothing but the mouse.
    float Steer()
    {
        float sx = 0f;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.aKey.isPressed || k.leftArrowKey.isPressed)  sx -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) sx += 1f;
        }
        var t = Touchscreen.current;
        if (t != null && t.primaryTouch.press.isPressed)
            sx += t.primaryTouch.position.ReadValue().x < Screen.width * 0.5f ? -1f : 1f;
        var m = Mouse.current;
        if (m != null && m.leftButton.isPressed)
            sx += m.position.ReadValue().x < Screen.width * 0.5f ? -1f : 1f;
#else
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  sx -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) sx += 1f;
        if (Input.GetMouseButton(0))
            sx += Input.mousePosition.x < Screen.width * 0.5f ? -1f : 1f;
#endif
        return Mathf.Clamp(sx, -1f, 1f);
    }

    // ---------------------------------------------------------------------
    void BuildSea()
    {
        int n = SEA_DIV + 1;
        seaVerts = new Vector3[n * n];
        var uv = new Vector2[n * n];
        var tris = new int[SEA_DIV * SEA_DIV * 6];
        float step = SEA_SIZE / SEA_DIV, half = SEA_SIZE * 0.5f;

        for (int j = 0, v = 0; j < n; j++)
            for (int i = 0; i < n; i++, v++)
            {
                seaVerts[v] = new Vector3(i * step - half, 0f, j * step - half);
                uv[v] = new Vector2(i / (float)SEA_DIV, j / (float)SEA_DIV);
            }

        for (int j = 0, t = 0; j < SEA_DIV; j++)
            for (int i = 0; i < SEA_DIV; i++)
            {
                int v0 = j * n + i;
                tris[t++] = v0;     tris[t++] = v0 + n; tris[t++] = v0 + 1;
                tris[t++] = v0 + 1; tris[t++] = v0 + n; tris[t++] = v0 + n + 1;
            }

        seaMesh = new Mesh { name = "Sea" };
        seaMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        seaMesh.vertices = seaVerts;
        seaMesh.uv = uv;
        seaMesh.triangles = tris;
        seaMesh.RecalculateNormals();

        var go = new GameObject("Sea");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = seaMesh;
        var mat = Mat(new Color(0.07f, 0.40f, 0.60f));
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.62f);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    void BuildCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.transform.position = new Vector3(0f, CAM_Y, CAM_Z);
        cam.transform.rotation = Quaternion.Euler(CAM_PITCH, 0f, 0f);
        cam.fieldOfView = FOV;
        cam.nearClipPlane = NEAR;
        cam.farClipPlane = FAR;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.60f, 0.76f, 0.88f);
    }

    void BuildSun()
    {
        var go = new GameObject("Sun");
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1.0f, 0.95f, 0.88f);
        l.intensity = 1.5f;
        go.transform.rotation = Quaternion.Euler(38f, -155f, 0f);
    }

    GameObject Prim(string name, PrimitiveType type, Color c)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(transform, false);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.GetComponent<Renderer>().sharedMaterial = Mat(c);
        return go;
    }

    GameObject Box(string name, Vector3 size, Color c)
    {
        var go = Prim(name, PrimitiveType.Cube, c);
        go.transform.localScale = size;
        return go;
    }

    Material Mat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        return m;
    }
}
