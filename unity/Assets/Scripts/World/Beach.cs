// The title screen's beach, laid out from the web game's actual scene: every position, size
// and turn below was read off the running web game at phone size (430 by 932), in its own
// world units, which are the same feet and the same axes this project uses (-z is out to
// sea). The camera stands almost on the sand four feet from the signpost with a 92-degree
// lens looking slightly up, which is why the sign, the board and the palm tower over it.
using UnityEngine;

public class Beach : MonoBehaviour
{
    [Header("The shot, as measured")]
    public Vector3 camAt = new Vector3(-11.75f, 4.84f, -40.69f);
    public Vector3 camTurn = new Vector3(19.8f, 13.6f, 0f);     // pitch up, yaw, roll (web YXZ)
    public float camFov = 92f;

    [Header("The props (world feet)")]
    public Vector3 palmFoot = new Vector3(-12.87f, -0.44f, -51.53f);
    public Vector3 palmBoxCentre = new Vector3(-14.97f, 0, -51.8f);
    public float palmHeight = 19.7f; public Vector3 palmTurn = new Vector3(0, 173.1f, 7.8f);
    public Vector3 signAt = new Vector3(-14.06f, 8.90f, -50.77f); public Vector3 signTurn = new Vector3(19.8f, 13.6f, -1.4f); public float signHeight = 1.15f;
    public Vector3 postAt = new Vector3(-11.88f, 4.37f, -44.75f); public Vector3 postTurn = new Vector3(3.39f, -6.82f, -6.2f); public float postHeight = 1.48f;
    public Vector3 chestBottom = new Vector3(-17.02f, 2.95f, -50.22f); public float chestTurn = 37.4f, chestHeight = 2.65f;
    public Vector3 pugAt = new Vector3(-15.29f, 2.85f, -49.62f); public float pugFace = 64f, pugScale = 1.32f;   // pugFace: his yaw in Unity, toward the lens
    public Vector3 boardAt = new Vector3(-14.62f, 4.84f, -50.92f); public Vector3 boardTurn = new Vector3(72.5f, -54.3f, -20.9f);
    [Header("Mirror switches, in case a model imports facing the other way")]
    public bool flipSign, flipPost, flipPalm, flipChest;
    [Header("The small stuff")]
    public float cocoHeight = 1.17f;      // the nuts in the crown, feet tall
    public float bowlHeight = 0.46f;      // the split shells on the sand
    public float birdYaw = 90f;           // the gull model faces +z; its flock flies along +x. 270 if they fly backwards.

    [Header("The light and the sea")]
    public Vector3 keyFrom = new Vector3(0.943f, 0.117f, 0.31f);    // toward the sun, from the set (already mirrored)
    public float seaLevel = 1.21f;

    public System.Action<string> onSign;       // "Play", "Shop", "Quests"

    // The web camera looks down -z with +x on its right; Unity's, looking the same way, has +x on
    // its left. So the measured scene is mirrored across x on the way in: positions flip x, and
    // turns flip their yaw and roll. Text on the signs stays readable because a mirror of the
    // layout is not a mirror of the models.
    static Vector3 M(Vector3 p) => new Vector3(-p.x, p.y, p.z);

    // ---- placed on the trunk the palm actually has ----
    // The sign, the coconuts and the board were set at the web's absolute coordinates, which are
    // only right if the palm stands exactly as the web's does; any difference in how it stands and
    // the trunk was no longer behind them (the sign off the trunk, the nuts below the crown, the
    // board leaning on air). Each is now placed where it sits RELATIVE to the trunk on the web --
    // read off the web's own trunk line in world space (its trunkLine, sampled on the live page):
    // the sign 0.84 ft in front of the trunk at 8.8 ft, the nuts round it at 10.9 ft, the board
    // against it at 4.6 ft -- and measured on the palm drawn here: the model's trunk runs straight
    // up its own axis from its foot, through 0.5 of its height before the crown.
    Vector3 trunkA, trunkB; float trunkK = 1f; bool trunkOk;
    const float WebFootY = -0.44f, WebPalmH = 19.67f;
    void MeasureTrunk(GameObject palm)
    {
        trunkOk = false; if (palm == null || palm.name.EndsWith("(stand-in)")) return;
        trunkA = palm.transform.TransformPoint(Vector3.zero);
        trunkB = palm.transform.TransformPoint(new Vector3(0f, 0.5f, 0f));
        var b = Models.Bounds(palm);
        trunkK = Mathf.Max(0.5f, b.size.y / WebPalmH);
        trunkOk = Mathf.Abs(trunkB.y - trunkA.y) > 0.5f;
    }
    /// where something sits that, on the web, sits `webOff` (web world axes) from its trunk at web height webY
    Vector3 OnTrunk(float webY, Vector3 webOff)
    {
        var off = new Vector3(-webOff.x, webOff.y, webOff.z) * trunkK;      // the set is mirrored across x here
        if (!trunkOk) return new Vector3(0f, webY, 0f) + off;
        float y = trunkA.y + (webY - WebFootY) * trunkK;
        float t = (y - trunkA.y) / (trunkB.y - trunkA.y);
        return Vector3.LerpUnclamped(trunkA, trunkB, t) + off;
    }
    static Vector3 R(Vector3 e) => new Vector3(e.x, -e.y, -e.z);

    GameObject root; Transform post, pugSpot; Mesh sandMesh; float t;
    static readonly string[] Arms = { "Quests", "Shop", "Play" };   // bottom to top
    // the sky, the nuts and the shells, and the rider's own standing clip
    BeachSky sky;
    Transform[] cocos = new Transform[0]; Vector3[] cocoHome = new Vector3[0]; Quaternion[] cocoTurn = new Quaternion[0]; float[] cocoPhase = new float[0];
    Transform[] bowls = new Transform[0]; Quaternion[] bowlTurn = new Quaternion[0];
    GltfClips idle; TitleGesture gesture;
    Camera shotCam; float savedFar; bool farSaved;

    // the beach profile along z, read off the web sand with a ray every five feet
    static readonly float[] ProfZ = { 20, 0, -20, -25, -30, -35, -40, -45, -50, -55, -60, -65, -70, -75, -80, -85, -90, -100, -120, -160, -300 };
    static readonly float[] ProfY = { 7.2f, 7.0f, 6.7f, 6.6f, 6.0f, 4.9f, 4.3f, 4.1f, 3.7f, 3.9f, 4.2f, 3.5f, 2.4f, 1.8f, 1.3f, 0.6f, 0.0f, -1.2f, -3.0f, -4.6f, -6f };

    /// the web's sand material (sand.glb's own, retuned): a warm tint of 1.04 / 0.99 / 0.95, rough to at
    /// least 0.82, never metal, the normal map at 1.35, and almost none of the sky's reflection (0.14
    /// against everything else's 0.55), which is what kept it from going pale and blue
    static Material SandLook(Material m, bool tint = true)
    {
        if (tint)
        {
            foreach (var p in new[] { "_BaseColor", "baseColorFactor", "_Color" })
                if (m.HasProperty(p)) { var c = m.GetColor(p); m.SetColor(p, new Color(c.r * 1.04f, c.g * 0.99f, c.b * 0.95f, c.a)); }
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.18f));
        if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", Mathf.Max(m.GetFloat("roughnessFactor"), 0.82f));
        foreach (var p in new[] { "_Metallic", "metallicFactor" }) if (m.HasProperty(p)) m.SetFloat(p, 0f);
        foreach (var p in new[] { "_BumpScale", "normalTexture_scale" }) if (m.HasProperty(p)) m.SetFloat(p, 1.35f);
        if (m.HasProperty("_EnvironmentReflections")) { m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
        return m;
    }

    /// the beach's own shader (Surf/Sand) on the textures of whatever material the file brought;
    /// falls back to the old look if the shader is not there
    static Material SandShader(Material src, Texture baseT = null, Texture nrmT = null)
    {
        var sh = Shader.Find("Surf/Sand");
        if (sh == null) return SandLook(src);
        if (src != null)
        {
            foreach (var p in new[] { "baseColorTexture", "_BaseMap", "_MainTex" }) if (baseT == null && src.HasProperty(p)) baseT = src.GetTexture(p);
            foreach (var p in new[] { "normalTexture", "_BumpMap" }) if (nrmT == null && src.HasProperty(p)) nrmT = src.GetTexture(p);
        }
        var m = new Material(sh) { name = "Sand" };
        if (baseT != null) m.SetTexture("_BaseTex", Sharp(baseT));
        if (nrmT != null) m.SetTexture("_NrmTex", Sharp(nrmT));
        return m;
    }

    // filtered properly at the grazing angle the title looks along the beach, instead of going to mush
    static Texture Sharp(Texture t) { if (t != null) { t.anisoLevel = 16; t.filterMode = FilterMode.Trilinear; } return t; }

    public static float SandY(float x, float z)
    {
        float h;
        if (z >= ProfZ[0]) h = ProfY[0];
        else if (z <= ProfZ[ProfZ.Length - 1]) h = ProfY[ProfY.Length - 1];
        else
        {
            int i = 0; while (z < ProfZ[i + 1]) i++;
            float k = (z - ProfZ[i]) / (ProfZ[i + 1] - ProfZ[i]); k = k * k * (3f - 2f * k);
            h = Mathf.Lerp(ProfY[i], ProfY[i + 1], k);
        }
        // broad dunes where the sand is dry (the fine relief is in the tiles), and the flat disc the set stands on
        float dry = Mathf.Clamp01((z + 85f) / 20f);
        float n = (Mathf.PerlinNoise(x * 0.05f + 3.1f, z * 0.06f) - 0.5f) * 0.7f;
        h += dry * n;
        float d = Vector2.Distance(new Vector2(x, z), new Vector2(15.3f, -50f));
        float flat = 1f - Mathf.Clamp01((d - 4f) / 4f); flat = flat * flat * (3f - 2f * flat);
        return Mathf.Lerp(h, 2.85f, flat);
    }

    public void Build(Transform parent)
    {
        root = new GameObject("Beach"); root.transform.SetParent(parent, false);
        // the sand, textured with the web beach's own sand
        var sandGo = new GameObject("Sand", typeof(MeshFilter), typeof(MeshRenderer)); sandGo.transform.SetParent(root.transform, false);
        sandMesh = BuildSand(-70f, 70f, 24f, -110f, 140, 134);
        sandGo.GetComponent<MeshFilter>().sharedMesh = sandMesh;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var sm = new Material(lit); sm.SetColor("_BaseColor", new Color(1f, 0.97f, 0.9f)); sm.SetFloat("_Smoothness", 0.08f); SandLook(sm, false);
        var baseTex = Resources.Load<Texture2D>("Models/sand_base"); var nrm = Resources.Load<Texture2D>("Models/sand_normal");
        if (baseTex != null) sm.SetTexture("_BaseMap", baseTex);
        if (nrm != null) { sm.EnableKeyword("_NORMALMAP"); sm.SetTexture("_BumpMap", nrm); sm.SetFloat("_BumpScale", 1.35f); }
        if (Shader.Find("Surf/Sand") != null) { sm = SandShader(null, baseTex, nrm); sm.SetTextureScale("_BaseTex", Vector2.one); }
        var smr = sandGo.GetComponent<MeshRenderer>(); smr.sharedMaterial = sm; smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sandGo.transform.position = new Vector3(0, -0.35f, 0);     // a floor under the tiles, for the gaps
        SandTiles();

        // the palm, leaning out to the right with the Surf sign hung in it
        var palm = Prop("Palm", Models.Spawn("palm2", root.transform, palmHeight, PrimitiveType.Cylinder), R(palmTurn), flipPalm);
        Settle(palm, -palmBoxCentre.x, palmFoot.y, palmBoxCentre.z, false);
        MeasureTrunk(palm);
        palmGo = palm;
        var sign = Prop("Sign", Models.Spawn("sign", root.transform, signHeight, PrimitiveType.Cube), R(signTurn), flipSign);
        var sAt = OnTrunk(8.797f, new Vector3(0.045f, 0.116f, 0.842f));
        Settle(sign, sAt.x, sAt.y, sAt.z, true);
        // the board propped into the crook of the trunk
        SetBoard(SaveData.S.equipped);
        // the chest
        var chest = Prop("Chest", Models.Spawn("chest", root.transform, chestHeight, PrimitiveType.Cube), new Vector3(0, -chestTurn, 0), flipChest);
        Settle(chest, -chestBottom.x, SandY(-chestBottom.x, chestBottom.z) - 0.04f, chestBottom.z, false);
        // where the pug stands
        pugSpot = new GameObject("PugSpot").transform; pugSpot.SetParent(root.transform, false); pugSpot.position = new Vector3(-pugAt.x, SandY(-pugAt.x, pugAt.z), pugAt.z);
        // the signpost: four feet from the lens, its arms are the menu, so each third gets a collider
        var postGo = Prop("Signpost", Models.Spawn("menusign", root.transform, postHeight, PrimitiveType.Cube), R(postTurn), flipPost);
        Settle(postGo, -postAt.x, postAt.y, postAt.z, true);
        post = postGo.transform;
        var b = Models.Bounds(postGo);
        for (int i = 0; i < 3; i++)
        {
            var arm = new GameObject("arm:" + Arms[i]); arm.transform.SetParent(root.transform, false);
            arm.transform.position = new Vector3(-postAt.x, postAt.y - postHeight / 2f + postHeight * (i + 0.5f) / 3f, postAt.z);
            arm.transform.rotation = post.rotation;
            var bc = arm.AddComponent<BoxCollider>(); bc.size = new Vector3(Mathf.Max(b.size.x, 1.2f), postHeight / 3f, Mathf.Max(b.size.z, 0.8f));
        }
        // the coconuts in the crown and the split shells on the sand
        Coconuts();
        // the clouds and the gulls, far out over the water
        sky = new BeachSky { birdYaw = birdYaw };
        sky.Build(root.transform, 20250823);
        Show(false);
    }

    // ---- the coconuts: three in the crown, nodding on the same gust as the fronds, and two
    // half shells on the sand, at the spots measured off the web title screen ----
    // The web's coconut.glb is five and a half megabytes of mesh for a brown ball ten feet up,
    // so the nut is a sphere with a fibrous husk baked into a texture and a normal map instead.
    // The shells are the web's own cocobowl.glb.
    void Coconuts()
    {
        var brng = new System.Random(4171);
        System.Func<float> rnd = () => (float)brng.NextDouble();
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        // on the tree: measured world positions (web frame), each ~1.17 feet tall
        // round the trunk where the crown starts, as on the web (offsets from its trunk at 10.9 ft)
        var nutAt = new[] { new Vector3(0.693f, 0.074f, 0.797f), new Vector3(-0.727f, 0.069f, 0.647f), new Vector3(0.113f, 0.069f, -0.513f) };
        cocos = new Transform[nutAt.Length]; cocoHome = new Vector3[nutAt.Length]; cocoTurn = new Quaternion[nutAt.Length]; cocoPhase = new float[nutAt.Length];
        Material nutMat = null;
        if (lit != null)
        {
            CoconutTextures(out var baseTex, out var nrmTex);
            nutMat = new Material(lit); nutMat.name = "coconut";
            nutMat.SetTexture("_BaseMap", baseTex); nutMat.SetColor("_BaseColor", Color.white); nutMat.SetFloat("_Smoothness", 0.24f);
            nutMat.EnableKeyword("_NORMALMAP"); nutMat.SetTexture("_BumpMap", nrmTex); nutMat.SetFloat("_BumpScale", 1.2f);
        }
        for (int i = 0; i < nutAt.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Coconut";
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.SetParent(root.transform, false);
            if (nutMat != null) go.GetComponent<MeshRenderer>().sharedMaterial = nutMat;
            // the web's turn for the i-th nut round the trunk, mirrored with the set
            float a2 = i / (float)nutAt.Length * Mathf.PI * 2f + 0.7f;
            var turn = Quaternion.Euler(R(new Vector3(0.3f + a2 * 0.11f, a2 * 1.7f, 0.2f * Mathf.Sin(a2 * 2.3f)) * Mathf.Rad2Deg));
            go.transform.rotation = turn;
            go.transform.localScale = new Vector3(cocoHeight * 0.86f, cocoHeight, cocoHeight * 0.86f);   // a husked nut is an egg, not a ball
            go.transform.position = OnTrunk(10.891f, nutAt[i]);
            cocos[i] = go.transform; cocoHome[i] = go.transform.position; cocoTurn[i] = turn; cocoPhase[i] = a2;
        }
        // on the sand: two half shells, one open to the sky and one mouth down, as the web lays them
        var bowlAt = new[] { new Vector3(-15.42f, 3.58f, -46.89f), new Vector3(-12.55f, 3.76f, -46.06f) };
        bool haveBowl = Resources.Load<GameObject>("Models/cocobowl") != null;
        bowls = new Transform[bowlAt.Length]; bowlTurn = new Quaternion[bowlAt.Length];
        for (int i = 0; i < bowlAt.Length; i++)
        {
            GameObject go;
            if (haveBowl) { go = Models.Spawn("cocobowl", root.transform, 1f, PrimitiveType.Sphere); go.name = "Bowl"; }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Bowl (stand-in)";
                var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
                go.transform.SetParent(root.transform, false); go.transform.localScale = new Vector3(1f, 0.68f, 1f);
                if (nutMat != null) go.GetComponent<MeshRenderer>().sharedMaterial = nutMat;
            }
            // The shell is modelled standing on its cut edge with its axis along z (its box is
            // 0.99 x 1.00 x 0.68), so a quarter turn about x stands it up; then dome up or dome
            // down, at any yaw, with a couple of degrees of tilt for the sand under it.
            float ty = rnd() * 360f, up = i == 0 ? 0f : 180f, tx = (rnd() - 0.5f) * 9f, tz = (rnd() - 0.5f) * 9f;
            var turn = Quaternion.Euler(0, ty, 0) * Quaternion.Euler(-90f + up + tx, 0, 0) * Quaternion.Euler(0, 0, tz);
            go.transform.rotation = turn;
            var b = Models.Bounds(go);
            if (b.size.y > 1e-4f) go.transform.localScale = go.transform.localScale * (bowlHeight / b.size.y);
            Settle(go, -bowlAt[i].x, bowlAt[i].y, bowlAt[i].z, true);
            bowls[i] = go.transform; bowlTurn[i] = turn;
        }
    }

    /// a husk: fibres running pole to pole, three faint ridges, darker and smoother at the ends
    public static void CoconutTextures(out Texture2D baseTex, out Texture2D nrmTex)
    {
        const int S = 256;
        var h = new float[S * S]; var cols = new Color32[S * S]; var nrm = new Color32[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float u = x / (float)S, v = y / (float)S;
            float fib = Mathf.PerlinNoise(u * 48f + 7.3f, v * 6f + 1.1f) * 0.6f + Mathf.PerlinNoise(u * 110f + 3.7f, v * 14f + 9.2f) * 0.4f;
            float sm = Mathf.Abs(Mathf.Repeat(u * 3f, 1f) - 0.5f);
            float seam = Mathf.Clamp01(1f - sm / 0.07f);
            float pole = Mathf.Clamp01((Mathf.Abs(v - 0.5f) - 0.34f) / 0.16f);
            h[y * S + x] = fib * (1f - pole * 0.6f) - seam * 0.3f;
            float l = 0.72f + 0.6f * (fib - 0.5f);
            var c = new Color(0.47f * l, 0.30f * l, 0.14f * l, 1f);
            c = Color.Lerp(c, new Color(0.22f, 0.13f, 0.06f, 1f), Mathf.Clamp01(seam * 0.55f + pole * 0.5f));
            cols[y * S + x] = c;
        }
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float hx = h[y * S + (x + 1) % S] - h[y * S + (x + S - 1) % S];
            float hy = h[((y + 1) % S) * S + x] - h[((y + S - 1) % S) * S + x];
            var nv = new Vector3(-hx * 6f, -hy * 6f, 1f).normalized;
            nrm[y * S + x] = new Color32((byte)Mathf.RoundToInt((nv.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((nv.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((nv.z * 0.5f + 0.5f) * 255f), 255);
        }
        baseTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "coconut", wrapMode = TextureWrapMode.Repeat };
        baseTex.SetPixels32(cols); baseTex.Apply(true, true);
        nrmTex = new Texture2D(S, S, TextureFormat.RGBA32, true, true) { name = "coconut normal", wrapMode = TextureWrapMode.Repeat };
        nrmTex.SetPixels32(nrm); nrmTex.Apply(true, true);
    }

    /// the same slow gust as the web's fronds: the nuts nod on their stems and the shells rock on the sand
    void Animate(Camera cam, float dt)
    {
        if (sky != null) sky.Animate(cam, dt);
        float gu = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 0.23f + 0.6f));
        for (int i = 0; i < cocos.Length; i++)
        {
            if (cocos[i] == null) continue;
            float a2 = cocoPhase[i];
            float s1 = Mathf.Sin(t * 0.83f + a2 * 2.1f), s2 = Mathf.Sin(t * 1.45f + a2 * 3.7f);
            float amp = cocoHeight * 0.15f * gu;
            // the web's offsets, with x and the z-turn mirrored like everything else on the set
            cocos[i].position = cocoHome[i] + new Vector3(-amp * (s1 * 0.80f + s2 * 0.30f), -amp * Mathf.Abs(s1) * 0.22f, amp * (s2 * 0.70f - s1 * 0.25f));
            cocos[i].rotation = Quaternion.Euler(0.11f * gu * s1 * Mathf.Rad2Deg, 0, -0.11f * gu * s2 * Mathf.Rad2Deg) * cocoTurn[i];
        }
        for (int i = 0; i < bowls.Length; i++)
        {
            if (bowls[i] == null) continue;
            float ph = i * 2.3f + 0.7f;
            bowls[i].rotation = bowlTurn[i] * Quaternion.Euler(0.045f * gu * Mathf.Sin(t * 1.63f + ph * 1.4f) * Mathf.Rad2Deg, 0, 0.055f * gu * Mathf.Sin(t * 1.10f + ph) * Mathf.Rad2Deg);
        }
    }

    /// On the beach the rider stands in his own stance and, if his file has 'Stand_and_Chat',
    /// plays it every few seconds on his back and head only (TitleGesture, the web's loiter); a
    /// rider whose file has a 'rest' clip loops that instead. Off stops it and leaves the Animator switched off again,
    /// which is how BuildRider wants it for the ride, where RiderRig poses the bones by hand.
    /// Safe to call every frame; it only starts the clip once per rider.
    public void IdleAnim(Transform pug, bool on)
    {
        if (on && !Showing) on = false;      // the beach only
        if (idle != null && (!on || pug == null || idle.root != pug || !idle.Valid)) { idle.Stop(); idle = null; }
        if (gesture != null && (!on || pug == null || gesture.transform != pug)) { Object.Destroy(gesture); gesture = null; }
        if (!on || pug == null || idle != null || gesture != null) return;
        // a file with the chat gets the web's loiter: the chat now and then, on his back and head only
        var chat = GltfClips.Find(pug.name, "Stand_and_Chat");
        if (chat != null) { gesture = TitleGesture.Attach(pug, chat); return; }
        // a file drawn for its own body with a 'rest' clip plays that, looping
        idle = GltfClips.Start(pug, pug.name, new[] { "rest" });
        if (idle == null) return;
        idle.SetSpeed(0, 0.9f);
        idle.SetTime(0, Random.value * idle.Length(0));
    }

    // The web beach is eighty-odd copies of one modelled sand tile with real dune relief, laid
    // on a hexagonal grid at about half a tile's spacing so they overlap, each turned, scaled
    // and tilted to the beach's slope. The same tile, the same layout.
    void SandTiles()
    {
        var prefab = Resources.Load<GameObject>("Models/sand");
        if (prefab == null) return;
        var src = Object.Instantiate(prefab); src.SetActive(false);
        src.transform.position = Vector3.zero; src.transform.rotation = Quaternion.identity; src.transform.localScale = Vector3.one;
        var mf = src.GetComponentInChildren<MeshFilter>(true); var mr = src.GetComponentInChildren<MeshRenderer>(true);
        if (mf == null || mr == null) { Object.Destroy(src); return; }
        Models.Repair(src);
        var mat = SandShader(mr.sharedMaterial);
        // ---- the piece, as the web prepares it (sandPrepare) ----
        // whatever the file wrapped the mesh in is baked into it, then it is centred in x and z.
        // The file's own node sits it almost entirely BELOW its origin; leaving that out stood
        // every tile two and a half feet proud of the beach, right in front of the title's lens.
        var srcMesh = mf.sharedMesh;
        var rel = src.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = srcMesh.vertices; var nrmIn = srcMesh.normals; var tanIn = srcMesh.tangents;
        for (int i = 0; i < v.Length; i++) v[i] = rel.MultiplyPoint3x4(v[i]);
        if (nrmIn != null && nrmIn.Length == v.Length) for (int i = 0; i < nrmIn.Length; i++) nrmIn[i] = rel.MultiplyVector(nrmIn[i]).normalized;
        if (tanIn != null && tanIn.Length == v.Length) for (int i = 0; i < tanIn.Length; i++) { var t3 = rel.MultiplyVector(new Vector3(tanIn[i].x, tanIn[i].y, tanIn[i].z)).normalized; tanIn[i] = new Vector4(t3.x, t3.y, t3.z, tanIn[i].w); }
        Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
        foreach (var p in v) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
        float cx = (mn.x + mx.x) * 0.5f, cz = (mn.z + mx.z) * 0.5f, sy = 0f;
        for (int i = 0; i < v.Length; i++) { v[i] = new Vector3(v[i].x - cx, v[i].y, v[i].z - cz); sy += v[i].y; }
        float mid = sy / Mathf.Max(1, v.Length), maxY = mx.y;
        var mesh = new Mesh { name = "sandTile", indexFormat = srcMesh.indexFormat };
        mesh.vertices = v; mesh.uv = srcMesh.uv;
        if (nrmIn != null && nrmIn.Length == v.Length) mesh.normals = nrmIn;
        if (tanIn != null && tanIn.Length == v.Length) mesh.tangents = tanIn;
        var tris = srcMesh.triangles;
        if (rel.determinant < 0) for (int i = 0; i < tris.Length; i += 3) { int a0 = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = a0; }
        mesh.triangles = tris;
        if (nrmIn == null || nrmIn.Length != v.Length) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        float spanX = Mathf.Max(1e-4f, mx.x - mn.x), spanZ = Mathf.Max(1e-4f, mx.z - mn.z);

        // ---- the field, the web's numbers (SAND_TILE 14, GAP 0.54, JIT 0.34, RELIEF 0.32, SINK 0.62,
        // TILT 0.9, DIP 0.34; the grid x +-34, z 14..-68, all at the beach's 1.15) ----
        const float Tile = 14f * 1.15f, Gap = 0.54f, Jit = 0.34f, Relief = 0.32f, Sink = 0.62f, TiltK = 0.9f, DipK = 0.34f, MbZ = -44f;
        float baseK = Tile / Mathf.Max(spanX, spanZ);
        float stepX = Mathf.Max(0.6f, Mathf.Min(spanX, spanZ) * baseK * Gap), stepZ = stepX * 0.866f;
        float X = 34f * 1.15f, Z0 = 14f * 1.15f, Z1 = -68f * 1.15f;
        float gx0 = -X - stepX * 0.5f, gz0 = Z1 - stepZ * 0.5f;
        int nx = Mathf.CeilToInt((X * 2 + stepX) / stepX) + 1, nz = Mathf.CeilToInt((Z0 - Z1 + stepZ) / stepZ) + 1;
        long rs = 20250823; System.Func<float> R = () => { rs = (rs * 1103515245L + 12345L) & 0x7fffffff; return rs / 2147483647f; };
        System.Func<float, float, float, float> Ss = (a, b, x0) => { float u = Mathf.Clamp01((x0 - a) / (b - a)); return u * u * (3 - 2 * u); };
        var holder = new GameObject("SandTiles"); holder.transform.SetParent(root.transform, false);
        tileHolder = holder.transform; tileV = v; tileT = tris; tileR = Mathf.Max(spanX, spanZ) * 0.5f;
        float dd = Mathf.Max(0.6f, Tile * 0.35f);
        for (int ix = 0; ix < nx; ix++) for (int iz = 0; iz < nz; iz++)
        {
            float x = gx0 + (ix + 0.5f + ((iz & 1) == 1 ? 0.5f : 0f)) * stepX + (R() - 0.5f) * stepX * Jit;
            float z = gz0 + (iz + 0.5f) * stepZ + (R() - 0.5f) * stepZ * Jit;
            float k = baseK * (0.90f + R() * 0.26f), kyU = k * Relief;
            // flattened round the props, so nothing stands in a hollow or on a hump
            float dO = Mathf.Sqrt(x * x + z * z), dS = Mathf.Sqrt(x * x + (z - MbZ) * (z - MbZ));
            float fw = Mathf.Min(Ss(3f, 24f, dO), Ss(4f, 24f, dS));
            float h0 = SandY(x, z);
            float gx = (SandY(x + dd, z) - SandY(x - dd, z)) / (2 * dd), gz = (SandY(x, z + dd) - SandY(x, z - dd)) / (2 * dd);
            var nrm = new Vector3(-gx, 1f, -gz).normalized;
            float h = h0 - DipK * (1 - fw);
            var q = Quaternion.Euler(0, R() * 360f, 0);
            var axis = Vector3.Cross(Vector3.up, nrm); float s2 = axis.magnitude;
            if (s2 > 1e-4f) q = Quaternion.AngleAxis(Mathf.Asin(Mathf.Min(1f, s2)) * TiltK * Mathf.Rad2Deg, axis / s2) * q;
            var t = new GameObject("tile", typeof(MeshFilter), typeof(MeshRenderer)); t.transform.SetParent(holder.transform, false);
            t.GetComponent<MeshFilter>().sharedMesh = mesh; var tr = t.GetComponent<MeshRenderer>(); tr.sharedMaterial = mat;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = true;
            t.transform.position = new Vector3(x, h - (mid + Sink * (maxY - mid)) * kyU, z);
            t.transform.rotation = q;
            t.transform.localScale = new Vector3(k, kyU, k);
        }
        Object.Destroy(src);
    }

    // ---- the drawn sand, for things that stand on it away from the set ----
    // The tiles carry the beach's real relief and lie over the analytic profile by a varying few
    // inches, so a prop stood on SandY alone can hover or sink. This asks the tiles themselves,
    // as the web's groundTopOver asks its sand field: a ray straight down through every tile near
    // the point, the highest hit wins, and the floor under the tiles is the fallback. It walks
    // triangles, so it is for placing something once, not for every frame.
    GameObject palmGo;
    Transform tileHolder; Vector3[] tileV; int[] tileT; float tileR;
    public Transform Root => root != null ? root.transform : null;
    /// the title's palm, for scenery that is a copy of it
    public GameObject PalmGo => palmGo;
    public float GroundTop(float x, float z)
    {
        float best = SandY(x, z) - 0.35f;                        // the floor mesh under the tiles
        if (tileHolder == null || tileV == null || tileT == null) return best;
        var from = new Vector3(x, 200f, z);
        for (int c = 0; c < tileHolder.childCount; c++)
        {
            var t = tileHolder.GetChild(c);
            float reach = tileR * Mathf.Abs(t.lossyScale.x) * 1.15f;
            if (Mathf.Abs(t.position.x - x) > reach || Mathf.Abs(t.position.z - z) > reach) continue;
            var o = t.InverseTransformPoint(from); var d = t.InverseTransformVector(Vector3.down);
            for (int i = 0; i + 2 < tileT.Length; i += 3)
            {
                Vector3 a = tileV[tileT[i]], e1 = tileV[tileT[i + 1]] - a, e2 = tileV[tileT[i + 2]] - a;
                var p = Vector3.Cross(d, e2); float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1f / det; var s = o - a;
                float u = Vector3.Dot(s, p) * inv; if (u < 0f || u > 1f) continue;
                var q = Vector3.Cross(s, e1);
                float w = Vector3.Dot(d, q) * inv; if (w < 0f || u + w > 1f) continue;
                float k = Vector3.Dot(e2, q) * inv; if (k < 0f) continue;
                float y = t.TransformPoint(o + d * k).y;
                if (y > best) best = y;
            }
        }
        return best;
    }

    GameObject leanBoard;
    /// the board propped against the trunk is the one equipped
    public void SetBoard(string id)
    {
        if (root == null) return;
        if (leanBoard != null) Destroy(leanBoard);
        leanBoard = BoardMesh.Build(id); leanBoard.transform.SetParent(root.transform, false);
        leanBoard.transform.rotation = Quaternion.Euler(R(boardTurn));
        var bAt = OnTrunk(4.625f, new Vector3(-1.186f, 0.215f, 0.653f));   // propped against the trunk, as on the web
        Settle(leanBoard, bAt.x, bAt.y, bAt.z, true);
    }

    GameObject Prop(string n, GameObject go, Vector3 turn, bool flip)
    {
        go.name = n;
        go.transform.rotation = Quaternion.Euler(turn) * (flip ? Quaternion.Euler(0, 180f, 0) : Quaternion.identity);
        return go;
    }

    // move a prop so its world box sits where the web one was: centred in x and z, and either
    // centred or stood on its bottom in y
    static void Settle(GameObject go, float cx, float y, float cz, bool centreY)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { go.transform.position = new Vector3(cx, y, cz); return; }
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var want = new Vector3(cx, centreY ? y : y + b.extents.y, cz);
        go.transform.position += want - b.center;
    }

    Mesh BuildSand(float x0, float x1, float z0, float z1, int nx, int nz)
    {
        var m = new Mesh { name = "sand" }; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var v = new Vector3[(nx + 1) * (nz + 1)]; var uv = new Vector2[v.Length];
        for (int j = 0; j <= nz; j++)
        for (int i = 0; i <= nx; i++)
        {
            float x = Mathf.Lerp(x0, x1, i / (float)nx), z = Mathf.Lerp(z0, z1, j / (float)nz);
            v[j * (nx + 1) + i] = new Vector3(x, SandY(x, z), z);
            uv[j * (nx + 1) + i] = new Vector2(x / 16f, z / 16f);     // one sand tile every sixteen feet, as the web beach tiles it
        }
        var idx = new int[nx * nz * 6]; int n = 0;
        for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++)
        {
            int a = j * (nx + 1) + i, bq = a + 1, c = a + nx + 1, e = c + 1;
            // clockwise seen from above, which is the side Unity draws
            idx[n++] = a; idx[n++] = bq; idx[n++] = c; idx[n++] = bq; idx[n++] = e; idx[n++] = c;
        }
        m.vertices = v; m.uv = uv; m.triangles = idx; m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    public void Show(bool on)
    {
        if (root != null) root.SetActive(on);
        if (sky != null) sky.Pause(!on);
        if (!on)
        {
            IdleAnim(null, false);                                             // the ride poses him by hand from here
            if (shotCam != null && farSaved) shotCam.farClipPlane = savedFar;   // the lens back to the ride's reach
        }
    }
    public bool Showing => root != null && root.activeSelf;

    /// the pug stands by the chest, a little bigger than he rides, with the ride's board hidden
    public void PlaceRider(Transform player, Transform rig, GameObject rideBoard)
    {
        player.position = pugSpot.position; player.rotation = Quaternion.Euler(0, pugFace, 0);
        player.localScale = Vector3.one * pugScale;
        rig.localRotation = Quaternion.identity;
        if (rideBoard != null) rideBoard.SetActive(false);
        // and he stands the way his file stands, not frozen: the rider is the thing under the rig
        // that carries the RiderRig, whichever file the shop put there
        if ((idle == null || !idle.Valid) && gesture == null)
        {
            var rr = rig.GetComponentInChildren<RiderRig>(true);
            if (rr != null) IdleAnim(rr.transform, true);
        }
    }

    /// the measured shot, with a slow breath so it is not a photograph
    public void PlaceCamera(Camera cam, float dt)
    {
        t += dt;
        var at = M(camAt) + new Vector3(Mathf.Sin(t * 0.21f) * 0.06f, Mathf.Sin(t * 0.17f + 1f) * 0.04f, 0);
        // the web camera looks down its -z; its YXZ turn applied to that is the direction we want
        var ct = R(camTurn);
        var dir = Quaternion.Euler(ct.x, ct.y, ct.z) * new Vector3(0, 0, -1);
        cam.transform.position = at; cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up); cam.fieldOfView = camFov;
        // the clouds hang up to five hundred feet out and the ride's lens stops there, so the
        // beach reaches further and hands the old distance back when it goes (see Show)
        shotCam = cam;
        if (!farSaved) { savedFar = cam.farClipPlane; farSaved = true; }
        if (cam.farClipPlane < 1000f) cam.farClipPlane = 1000f;
        Animate(cam, dt);
    }

    /// a tap on the signpost: which arm was hit
    public void Tap(Camera cam, Vector2 screen)
    {
        var ray = cam.ScreenPointToRay(screen);
        if (Physics.Raycast(ray, out var hit, 200f) && hit.collider.name.StartsWith("arm:")) onSign?.Invoke(hit.collider.name.Substring(4));
    }
}
