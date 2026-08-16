using UnityEngine;

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
        return h * waveAmp;
    }

    // the long swell alone — what a floating thing rides, with the chop filtered out
    public static float SwellH(float x, float z, float t)
    {
        float h = 0f;
        for (int i = 0; i < 2; i++) h += Component(WAVES[i], x, z, t);
        return h * waveAmp * 1.06f;
    }
}


public class SurfBoot : MonoBehaviour
{
    // ---- units: ONE UNITY UNIT IS ONE FOOT ----
    const float RIDER_FT = 2.20f;
    const float BOARD_L  = 5.83f, BOARD_W = 1.42f, BOARD_T = 0.25f;
    const float BOARD_HALF_L = 2.45f;
    const float BUOY_H = 3.17f, LOG_L = 5.10f;

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

    Transform[] props;
    float[] propZ;    // each prop's distance ahead, which is what actually moves

    void Awake()
    {
        BuildCamera();
        BuildSun();
        BuildSea();

        board = Box("Board", new Vector3(BOARD_W, BOARD_T, BOARD_L),
                    new Color(0.96f, 0.55f, 0.30f)).transform;

        var rider = Prim("Rider", PrimitiveType.Capsule, new Color(0.85f, 0.69f, 0.45f));
        rider.transform.SetParent(board, false);
        rider.transform.localPosition = new Vector3(0f, (BOARD_T * 0.5f + RIDER_FT * 0.5f) / BOARD_T, 0f);
        rider.transform.localScale = new Vector3(RIDER_FT * 0.25f / BOARD_W,
                                                 RIDER_FT * 0.5f  / BOARD_T,
                                                 RIDER_FT * 0.25f / BOARD_L);

        // A handful of things to pass, so the motion has something to be measured
        // against. Water with nothing in it does not read as moving at all.
        props = new Transform[10];
        propZ = new float[10];
        for (int i = 0; i < props.Length; i++)
        {
            GameObject g;
            if (i % 2 == 0)
            {
                g = Prim("Buoy", PrimitiveType.Cylinder, new Color(0.85f, 0.20f, 0.18f));
                g.transform.localScale = new Vector3(1.6f, BUOY_H * 0.5f, 1.6f);
            }
            else
            {
                g = Box("Log", new Vector3(1.0f, 1.0f, LOG_L), new Color(0.45f, 0.27f, 0.14f));
            }
            props[i] = g.transform;
            propZ[i] = 25f + i * 16f;
            g.transform.position = new Vector3(Random.Range(-14f, 14f), 0f, propZ[i]);
        }

        Debug.Log("[Surf] hold A / D (or the arrow keys) to carve. " +
                  "Speed builds with distance — it starts at 16.2 ft/s and climbs.");
    }

    void Update()
    {
        float dt = Time.deltaTime;
        clock += dt;

        // ---- forward speed ----
        speed = Mathf.Min(SPEED_CAP + BS_top, (SPEED_BASE + dist / SPEED_RAMP) * BS_speed + BS_top);

        // ---- steering ----
        float sx = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  sx -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) sx += 1f;
        vx += sx * TURN_ACCEL * BS_turn * dt;
        vx -= vx * GRIP * BS_grip * dt;
        px += vx * dt;
        if (px < -LANE_LIMIT) { px = -LANE_LIMIT; vx = 0f; }
        if (px >  LANE_LIMIT) { px =  LANE_LIMIT; vx = 0f; }

        // Carving costs nothing and gains a little: crossing the face adds to the
        // ground he covers, which is why a run that weaves outruns one that does not.
        eff = Mathf.Max(SPEED_BASE, speed + Mathf.Abs(vx) * 0.3f);
        dist += eff * dt;

        // ---- the sea scrolls ----
        // This is the whole trick. The vertices never move in x or z; the swell is
        // sampled at an offset that grows with distance, so the water slides past
        // underneath a grid that is standing still.
        float drift = dist * SurfWater.WAVE_DRIFT;
        int n = SEA_DIV + 1;
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

        // ---- the world comes to him ----
        for (int i = 0; i < props.Length; i++)
        {
            propZ[i] -= eff * dt;
            if (propZ[i] < -20f)                       // past him: recycle it out front
            {
                propZ[i] += props.Length * 16f;
                var q = props[i].position;
                q.x = Random.Range(-14f, 14f);
                props[i].position = q;
            }
            var p = props[i].position;
            p.z = propZ[i];
            p.y = SurfWater.SwellH(p.x, p.z + drift, clock) + 0.35f;
            props[i].position = p;
        }

        // ---- and the camera sits behind him ----
        camX += (px - camX) * Mathf.Min(1f, dt * 7f);
        float waterY = SurfWater.SwellH(px, drift, clock);
        cam.transform.position = new Vector3(camX,
                                             Mathf.Max(waterY + CAM_Y, waterY + CAM_FLOOR),
                                             CAM_Z);
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
