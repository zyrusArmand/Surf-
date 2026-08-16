using UnityEngine;

// ---------------------------------------------------------------------------
//  Surf — Unity port, step 2: the sea, and everything floating on it.
//
//  Step 1 proved the scale. This adds the thing the whole game stands on: the
//  wave height field. Everything else in Surf is downstream of it — the board
//  rides it, the buoys and logs bob on it, the camera height follows it, the
//  spray dies into it. Nothing else can be ported until this is right, which is
//  why it comes before the rider, the boards, the obstacles or the UI.
//
//  The maths below is copied verbatim from the browser game rather than
//  reinvented. Those seven wave components and their envelope were tuned over a
//  long time, and re-deriving them by eye would throw all of that away.
// ---------------------------------------------------------------------------


// ---- the sea itself, as pure maths ----
// No Unity types in here on purpose: it is a function of position and time and
// nothing else, so it can be called from the mesh, from the board, from an
// obstacle or from a test without any of them needing a scene.
public static class SurfWater
{
    // One component of the swell. dx/dz is the direction it travels, k is how
    // tightly packed it is, a is how tall, and ek/ep drive a second slow wave
    // that swells and flattens the first one as it passes — which is what stops
    // the sea reading as a repeating corrugation.
    struct Comp
    {
        public float dx, dz, k, a, ek, ep, w;
        public Comp(float dx, float dz, float k, float a, float ek, float ep)
        {
            this.dx = dx; this.dz = dz; this.k = k; this.a = a; this.ek = ek; this.ep = ep;
            // deep-water dispersion: longer waves travel faster, which is the
            // whole reason a real sea never repeats
            this.w = Mathf.Sqrt(WAVE_G * k) * WAVE_TSCALE;
        }
    }

    const float WAVE_G = 9.8f, WAVE_TSCALE = 1.0f;
    const float ENV_BASE = 0.32f, ENV_SWING = 0.68f;  // envelope floor, and how far it breathes
    const float ZSTRETCH = 1.0f;

    public const float WAVE_DRIFT = 0.46f;            // how fast the swell rolls past
    public static float waveAmp = 0.78f;              // current swell height, varies set to set

    static readonly Comp[] WAVES = {
        new Comp( 0.00f, 1.00f, 0.190f, 1.000f, 0.38f, 0.00f),
        new Comp( 0.60f, 0.80f, 0.310f, 0.520f, 0.42f, 2.10f),
        new Comp(-0.74f, 0.67f, 0.470f, 0.260f, 0.50f, 4.20f),
        new Comp( 0.40f,-0.92f, 0.720f, 0.110f, 0.58f, 1.30f),
        new Comp( 1.00f, 0.00f, 1.050f, 0.050f, 0.66f, 5.40f),
        new Comp( 0.86f, 0.51f, 1.700f, 0.030f, 0.72f, 3.10f),   // chop
        new Comp(-0.35f, 0.94f, 2.600f, 0.016f, 0.80f, 0.70f),   // finer chop
    };

    static float Component(in Comp W, float x, float z, float t)
    {
        float u = W.dx * x + W.dz * z * ZSTRETCH;
        float env = ENV_BASE + ENV_SWING * Mathf.Sin(u * W.k * W.ek - t * W.w * W.ek * 0.5f + W.ep);
        return W.a * env * Mathf.Sin(u * W.k - t * W.w);
    }

    // The full sea, chop and all. This is what you SEE.
    public static float WaveH(float x, float z, float t)
    {
        float h = 0f;
        for (int i = 0; i < WAVES.Length; i++) h += Component(WAVES[i], x, z, t);
        return h * waveAmp;
    }

    // The long-period part only — what a floating object actually rides. The
    // short chop is filtered out so that buoys glide over the swell instead of
    // vibrating on it, and because they track the big waves they never end up
    // buried by a crest.
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
    const float BOARD_HALF_L = 2.45f;          // the hull's own footprint, for trim
    const float BUOY_H = 3.17f, LOG_L = 5.10f;

    // ---- the ride camera ----
    const float FOV = 62f, NEAR = 0.5f, FAR = 500f;
    const float CAM_Y = 4.9f, CAM_Z = -11f, CAM_PITCH = 8.76f;
    const float CAM_FLOOR = 2.3f;              // never let a building face swallow it

    // ---- the water mesh ----
    // Two feet between vertices over two hundred feet of sea. The finest chop in
    // the height field has a two-and-a-half foot wavelength, so it will not be
    // fully resolved at this spacing — the swell is what reads at this distance
    // and the chop is detail the shader will carry later. Doing it on the CPU at
    // all is a stepping stone: it is here so the waves can be SEEN and floated
    // on now, and it moves to the GPU before this ever runs on a phone.
    const int   SEA_DIV  = 100;
    const float SEA_SIZE = 200f;

    Camera cam;
    Transform board, rider;
    Transform[] floaters;                      // things that bob but do not steer
    Mesh seaMesh;
    Vector3[] seaVerts;
    float clock;

    void Awake()
    {
        BuildCamera();
        BuildSun();
        BuildSea();

        board = Box("Board", new Vector3(BOARD_W, BOARD_T, BOARD_L),
                    new Color(0.96f, 0.55f, 0.30f)).transform;

        rider = Prim("Rider", PrimitiveType.Capsule, new Color(0.85f, 0.69f, 0.45f)).transform;
        rider.localScale = new Vector3(RIDER_FT * 0.25f, RIDER_FT * 0.5f, RIDER_FT * 0.25f);
        rider.SetParent(board, false);
        // the rider stands on the deck, and from here on he simply goes where the
        // board goes — which is what being parented to it means
        rider.localPosition = new Vector3(0f, (BOARD_T * 0.5f + RIDER_FT * 0.5f) / BOARD_T, 0f);
        rider.localScale = new Vector3(RIDER_FT * 0.25f / BOARD_W,
                                       RIDER_FT * 0.5f  / BOARD_T,
                                       RIDER_FT * 0.25f / BOARD_L);

        var buoy = Prim("Buoy (3.17ft)", PrimitiveType.Cylinder, new Color(0.85f, 0.20f, 0.18f));
        buoy.transform.localScale = new Vector3(1.6f, BUOY_H * 0.5f, 1.6f);
        buoy.transform.position = new Vector3(-6f, 0f, 14f);

        var log = Box("Log (5.1ft)", new Vector3(1.0f, 1.0f, LOG_L), new Color(0.45f, 0.27f, 0.14f));
        log.transform.position = new Vector3(6.5f, 0f, 20f);

        floaters = new[] { buoy.transform, log.transform };

        Debug.Log("[Surf] sea is live. 7 wave components, deep-water dispersion. " +
                  "Board and props float on the swell; the camera rides it.");
    }

    void Update()
    {
        clock += Time.deltaTime;

        // ---- the sea surface ----
        int n = SEA_DIV + 1;
        for (int j = 0, v = 0; j < n; j++)
            for (int i = 0; i < n; i++, v++)
            {
                float x = seaVerts[v].x, z = seaVerts[v].z;
                seaVerts[v].y = SurfWater.WaveH(x, z, clock);
            }
        seaMesh.vertices = seaVerts;
        seaMesh.RecalculateNormals();

        // ---- the board ----
        // Its height comes from the SWELL, not the full sea: a hull this long
        // bridges the chop rather than following every ripple of it. And its
        // pitch is read from the water under its own nose and tail, so it lies
        // ALONG the wave it is on instead of sitting flat on top of one.
        float bx = board.position.x, bz = board.position.z;
        float yNose = SurfWater.SwellH(bx, bz + BOARD_HALF_L, clock);
        float yTail = SurfWater.SwellH(bx, bz - BOARD_HALF_L, clock);
        board.position = new Vector3(bx, (yNose + yTail) * 0.5f, bz);
        // atan of rise over run, which is the angle of the line joining its ends
        board.rotation = Quaternion.Euler(
            -Mathf.Atan2(yNose - yTail, BOARD_HALF_L * 2f) * Mathf.Rad2Deg, 0f, 0f);

        foreach (var f in floaters)
        {
            var p = f.position;
            p.y = SurfWater.SwellH(p.x, p.z, clock) + 0.35f;
            f.position = p;
        }

        // ---- and the camera rides the sea too ----
        float waterY = SurfWater.SwellH(0f, 0f, clock);
        var c = cam.transform.position;
        c.y = Mathf.Max(waterY + CAM_Y, waterY + CAM_FLOOR);
        cam.transform.position = c;
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
                tris[t++] = v0;         tris[t++] = v0 + n;     tris[t++] = v0 + 1;
                tris[t++] = v0 + 1;     tris[t++] = v0 + n;     tris[t++] = v0 + n + 1;
            }

        seaMesh = new Mesh { name = "Sea" };
        // over 65k vertices needs the wider index format, and this grid is close
        // enough to it that raising the limit now saves a confusing failure later
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
