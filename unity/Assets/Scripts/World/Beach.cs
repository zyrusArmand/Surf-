// The title screen's beach, laid out from the web game's actual scene: every position, size
// and turn below was read off the running web game at phone size (430 by 932), in its own
// world units, which are the same feet and the same axes this project uses (-z is out to
// sea). The camera stands almost on the sand four feet from the signpost with a 92-degree
// lens looking slightly up, which is why the sign, the board and the palm tower over it.
using System.Collections.Generic;
using UnityEngine;

public class Beach : MonoBehaviour
{
    [Header("The shot, as measured")]
    // The title as laid out for the surf shop (worked out in a stand-in of this scene with the same
    // lens): the eye a foot and a half higher and tipped a little down, so the sea shows over the
    // dunes; turned a little right, toward the open water, with the shop on the left, the palm
    // framing the top left, the signpost bottom right and the rider and chest bottom left.
    // (The web's own shot was 4.84 up, turned 19.8 up and 13.6 left, at 92 degrees.)
    // (after the mockup: the eye 8.4 ft up looking level, so the sea sits mid-screen and the near
    // things sit low in the frame, as the painting has them)
    public Vector3 camAt = new Vector3(-11.75f, 8.4f, -40.69f);
    public Vector3 camTurn = new Vector3(1f, -5f, 0f);          // pitch up, yaw, roll (web YXZ)
    public float camFov = 88f;
    public float focusDist = 7f;                               // the lens's focus: the signs near, the shop far, both sharp

    [Header("The props (world feet)")]
    public Vector3 palmFoot = new Vector3(-20.87f, -0.44f, -59.53f);           // the web's, 8 ft left and 8 back: it frames the shop from behind
    public Vector3 palmBoxCentre = new Vector3(-22.97f, 0, -59.8f);
    public Vector3 palm2At = new Vector3(0f, 0f, -68f); public float palm2Height = 17f; public Vector3 palm2Turn = new Vector3(0f, 60f, -8f);   // a second palm, right, by the water
    public float palmHeight = 19.7f; public Vector3 palmTurn = new Vector3(0, 173.1f, 7.8f);
    public Vector3 signAt = new Vector3(-14.06f, 8.90f, -50.77f); public Vector3 signTurn = new Vector3(19.8f, 13.6f, -1.4f); public float signHeight = 1.35f;
    public Vector3 postAt = new Vector3(-10.4f, 6.3f, -44.4f); public Vector3 postTurn = new Vector3(3.39f, -6.82f, -6.2f); public float postHeight = 1.7f;
    public Vector3 chestBottom = new Vector3(-13.3f, 3.0f, -44.8f); public float chestTurn = 35f, chestHeight = 1.4f;
    public Vector3 pugAt = new Vector3(-12.4f, 3.0f, -46.4f); public float pugFace = 64f, pugScale = 0.95f;   // pugFace: his yaw in Unity, toward the lens
    public Vector3 boardAt = new Vector3(-14.62f, 4.84f, -50.92f); public Vector3 boardTurn = new Vector3(72.5f, -54.3f, -20.9f);
    public Vector3 shopAt = new Vector3(-17f, 3.8f, -65f); public Vector3 shopTurn = new Vector3(0f, 40f, 0f); public float shopHeight = 12f;   // the surf shop: where its foot stands, its turn, how tall
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
        // measured off the palm as it stands now, so a palm moved by hand (or by a saved layout)
        // takes its sign and its board with it
        if (palmGo != null && trunkOk) { trunkA = palmGo.transform.TransformPoint(Vector3.zero); trunkB = palmGo.transform.TransformPoint(new Vector3(0f, 0.5f, 0f)); }
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
        BarkTrunk(palm);
        palmGo = palm;
        var sign = Prop("Sign", Models.Spawn("sign", root.transform, signHeight, PrimitiveType.Cube), R(signTurn), flipSign);
        PaintSign(sign);
        var sAt = OnTrunk(8.797f, new Vector3(0.045f, 0.116f, 0.842f));
        Settle(sign, sAt.x, sAt.y, sAt.z, true);
        sign.transform.SetParent(palm.transform, true);                 // moves with the palm
        // the board propped into the crook of the trunk
        SetBoard(SaveData.S.equipped);
        // a second palm, on the right by the water, framing the sea as the mockup's does
        var palm2 = Models.Spawn("palm2", root.transform, palm2Height, PrimitiveType.Cylinder);
        if (palm2 != null && !palm2.name.EndsWith("(stand-in)"))
        {
            Prop("Palm2", palm2, R(palm2Turn), false);
            Settle(palm2, -palm2At.x, GroundTop(-palm2At.x, palm2At.z) - 0.3f, palm2At.z, false);
            BarkTrunk(palm2);
        }
        else if (palm2 != null) Destroy(palm2);
        // the surf shop, standing on the sand on the left, its own patch of sand sunk a little into the beach's
        var shop = Models.Spawn("surfshop", root.transform, shopHeight, PrimitiveType.Cube);
        if (shop != null && !shop.name.EndsWith("(stand-in)"))
        {
            Prop("SurfShop", shop, R(shopTurn), false);
            Settle(shop, -shopAt.x, GroundTop(-shopAt.x, shopAt.z) - 0.15f, shopAt.z, false);
        }
        else if (shop != null) Destroy(shop);
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
            arm.transform.SetParent(post, true);                         // the tap areas go wherever the post goes
        }
        LiftPost(postGo);
        LoadLayout();
        EditHandles();
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
            cocos[i] = go.transform; cocoHome[i] = palmGo != null ? palmGo.transform.InverseTransformPoint(go.transform.position) : go.transform.position; cocoTurn[i] = turn; cocoPhase[i] = a2;
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
            cocos[i].position = (palmGo != null ? palmGo.transform.TransformPoint(cocoHome[i]) : cocoHome[i]) + new Vector3(-amp * (s1 * 0.80f + s2 * 0.30f), -amp * Mathf.Abs(s1) * 0.22f, amp * (s2 * 0.70f - s1 * 0.25f));
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
        var tris = srcMesh.triangles;
        if (rel.determinant < 0) for (int i = 0; i < tris.Length; i += 3) { int a0 = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = a0; }

        // ---- the scanned edge is cut off, and the cut edge pressed down (web sandPrepare 11390-11477) ----
        // A scanned slab does not end at a clean line: its outer band curls up into a lip, with
        // near-vertical sides. Laid as it came, every tile's lip and side stood proud of whatever
        // it overlapped, and the beach was covered in pale flat planks with straight edges crossing
        // at angles -- the grey slabs behind the props. As the web does: the outer eighth is cut
        // away (a triangle is kept only if all three corners are inside, so nothing hangs off), and
        // the outer band of what is left is lowered, hard at the rim and easing to nothing inland,
        // so every tile is a shallow dish and every rim lies buried under its neighbour's middle.
        {
            const float Edge = 0.125f;
            float ix = (mx.x - mn.x) * Edge, iz = (mx.z - mn.z) * Edge;
            float lx0 = mn.x + ix, lx1 = mx.x - ix, lz0 = mn.z + iz, lz1 = mx.z - iz;
            bool Inside(int k) => v[k].x >= lx0 && v[k].x <= lx1 && v[k].z >= lz0 && v[k].z <= lz1;
            var keep = new List<int>(tris.Length);
            for (int t3 = 0; t3 + 2 < tris.Length; t3 += 3)
                if (Inside(tris[t3]) && Inside(tris[t3 + 1]) && Inside(tris[t3 + 2])) { keep.Add(tris[t3]); keep.Add(tris[t3 + 1]); keep.Add(tris[t3 + 2]); }
            if (keep.Count >= 3)
            {
                tris = keep.ToArray();
                // measured off the vertices still in use, not the whole file
                var used = new bool[v.Length];
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                foreach (int k in tris) { if (used[k]) continue; used[k] = true; var q = v[k]; x0 = Mathf.Min(x0, q.x); x1 = Mathf.Max(x1, q.x); y0 = Mathf.Min(y0, q.y); y1 = Mathf.Max(y1, q.y); z0 = Mathf.Min(z0, q.z); z1 = Mathf.Max(z1, q.z); }
                float fcx = (x0 + x1) * 0.5f, fcz = (z0 + z1) * 0.5f, yDrop = (y1 - y0) * 1.15f;
                // once per vertex: a welded vertex belongs to five or six triangles
                for (int k = 0; k < v.Length; k++)
                {
                    if (!used[k]) continue;
                    float tx = Mathf.Abs(v[k].x - fcx) / Mathf.Max(1e-4f, (x1 - x0) * 0.5f);
                    float tz = Mathf.Abs(v[k].z - fcz) / Mathf.Max(1e-4f, (z1 - z0) * 0.5f);
                    float tt = Mathf.Max(tx, tz);
                    if (tt <= 0.55f) continue;
                    float kk = (tt - 0.55f) / 0.45f, e = kk * kk * (3f - 2f * kk);
                    v[k].y -= yDrop * e;
                }
                // and the box measured again, after the feather, over what is in use
                mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue); mx = -mn;
                foreach (int k in tris) { mn = Vector3.Min(mn, v[k]); mx = Vector3.Max(mx, v[k]); }
            }
        }

        float cx = (mn.x + mx.x) * 0.5f, cz = (mn.z + mx.z) * 0.5f, sy = 0f;
        for (int i = 0; i < v.Length; i++) v[i] = new Vector3(v[i].x - cx, v[i].y, v[i].z - cz);
        { var seen = new bool[v.Length]; int nUsed = 0; foreach (int k in tris) if (!seen[k]) { seen[k] = true; sy += v[k].y; nUsed++; } sy /= Mathf.Max(1, nUsed); }
        float mid = sy, maxY = mx.y;
        mn.x -= cx; mx.x -= cx; mn.z -= cz; mx.z -= cz;
        var mesh = new Mesh { name = "sandTile", indexFormat = srcMesh.indexFormat };
        mesh.vertices = v; mesh.uv = srcMesh.uv;
        if (nrmIn != null && nrmIn.Length == v.Length) mesh.normals = nrmIn;
        if (tanIn != null && tanIn.Length == v.Length) mesh.tangents = tanIn;
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
            float reach = tileR * Mathf.Abs(t.lossyScale.x) * 1.45f;     // a tile turned 45 degrees reaches out to root 2 of its radius along an axis
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
        if (palmGo != null) leanBoard.transform.SetParent(palmGo.transform, true);
    }

    GameObject Prop(string n, GameObject go, Vector3 turn, bool flip)
    {
        go.name = n;
        go.transform.rotation = Quaternion.Euler(turn) * (flip ? Quaternion.Euler(0, 180f, 0) : Quaternion.identity);
        return go;
    }

    // ---- the palm's trunk, in bark of its own ----
    // The palm's one baked texture gives the trunk only a few small brown patches, so close to the
    // lens it was a blur. Its triangles (the thin tube below the crown: under 56% of the model's
    // height and within 0.065 of its axis, as measured on the model) come out into a mesh of their
    // own, wrapped round the trunk with a bark made here: the ring scars a coconut palm's old fronds
    // leave, each with a raised lip, wobbling a little round the trunk, fibrous streaks between them,
    // weathered grey-brown, with a normal map from the same relief. The crown keeps its texture.
    static Texture2D barkTex, barkNrm;
    static readonly Dictionary<Mesh, (Mesh crown, Mesh trunk)> barkMade = new Dictionary<Mesh, (Mesh, Mesh)>();
    void BarkTrunk(GameObject palm)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit"); if (lit == null) return;
        foreach (var mf in palm.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>(); var src = mf.sharedMesh;
            if (mr == null || src == null || !src.isReadable || src.subMeshCount != 1) continue;
            if (barkMade.TryGetValue(src, out var made)) { AttachTrunk(mf, mr, made.crown, made.trunk, lit); continue; }   // a second palm of the same mesh: the same split
            var v = src.vertices; var nrm = src.normals; var tri = src.triangles; var bb = src.bounds;
            float H = bb.size.y, y0 = bb.min.y;
            var trunk = new List<int>(); var rest = new List<int>(tri.Length);
            float top = y0;
            for (int t = 0; t < tri.Length; t += 3)
            {
                var c = (v[tri[t]] + v[tri[t + 1]] + v[tri[t + 2]]) / 3f;
                bool isTrunk = (c.y - y0) < 0.56f * H && new Vector2(c.x, c.z).magnitude < 0.065f * H;
                var into = isTrunk ? trunk : rest;
                into.Add(tri[t]); into.Add(tri[t + 1]); into.Add(tri[t + 2]);
                if (isTrunk) top = Mathf.Max(top, c.y);
            }
            if (trunk.Count < 60 * 3) continue;                                  // not the palm this was measured on
            // the trunk on its own, every triangle with vertices of its own so the wrap can be mended
            int n = trunk.Count;
            var tv = new Vector3[n]; var tn = new Vector3[n]; var tuv = new Vector2[n]; var tt = new int[n];
            float span = Mathf.Max(1e-4f, top - y0);
            for (int k = 0; k < n; k += 3)
            {
                for (int q = 0; q < 3; q++)
                {
                    var p = v[trunk[k + q]];
                    tv[k + q] = p; tn[k + q] = nrm != null && nrm.Length == v.Length ? nrm[trunk[k + q]] : new Vector3(p.x, 0f, p.z).normalized;
                    float ang = Mathf.Atan2(p.z, p.x) / (2f * Mathf.PI) + 0.5f;
                    tuv[k + q] = new Vector2(ang * 2f, (p.y - y0) / span * 4f);    // twice round, four times up: some thirty rings
                    tt[k + q] = k + q;
                }
                // a triangle across the wrap: its low side taken round once more
                float a = tuv[k].x, b = tuv[k + 1].x, c2 = tuv[k + 2].x;
                if (Mathf.Max(a, Mathf.Max(b, c2)) - Mathf.Min(a, Mathf.Min(b, c2)) > 1f)
                    for (int q = 0; q < 3; q++) if (tuv[k + q].x < 1f) tuv[k + q].x += 2f;
            }
            var tm = new Mesh { name = "palm trunk" };
            if (n > 65000) tm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            tm.vertices = tv; tm.normals = tn; tm.uv = tuv; tm.triangles = tt;
            tm.RecalculateTangents(); tm.RecalculateBounds();
            var crown = Instantiate(src); crown.name = src.name + " (crown)"; crown.SetTriangles(rest, 0);
            barkMade[src] = (crown, tm);
            AttachTrunk(mf, mr, crown, tm, lit);
        }
    }

    static Material barkMat;
    static void AttachTrunk(MeshFilter mf, MeshRenderer mr, Mesh crown, Mesh trunk, Shader lit)
    {
        mf.sharedMesh = crown;
        var go = new GameObject("trunk", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(mf.transform, false);
        go.layer = mf.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = trunk;
        if (barkTex == null) Bark(out barkTex, out barkNrm);
        if (barkMat == null)
        {
            barkMat = new Material(lit) { name = "palm bark" };
            barkMat.SetTexture("_BaseMap", barkTex); barkMat.SetColor("_BaseColor", Color.white);
            barkMat.SetTexture("_BumpMap", barkNrm); barkMat.SetFloat("_BumpScale", 1.2f); barkMat.EnableKeyword("_NORMALMAP");
            barkMat.SetFloat("_Smoothness", 0.12f);
        }
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = barkMat;
        r.shadowCastingMode = mr.shadowCastingMode; r.receiveShadows = mr.receiveShadows;
    }

    // the bark: 512 square, seamless both ways, eight ring scars to a repeat
    static void Bark(out Texture2D col, out Texture2D nrm)
    {
        const int N = 512, Rings = 8;
        var h = new float[N * N]; var c = new Color32[N * N]; var nn = new Color32[N * N];
        // value noise on a lattice that wraps after px cells across and py up, so the bark tiles both ways
        System.Func<int, int, int, int, int, float> hs = (i, j, px, py, seed) =>
        {
            i = ((i % px) + px) % px; j = ((j % py) + py) % py;
            uint q = (uint)(i * 374761393 + j * 668265263 + seed * unchecked((int)2246822519u));
            q = (q ^ (q >> 13)) * 1274126177u; return ((q ^ (q >> 16)) & 0xffff) / 65535f;
        };
        System.Func<float, float, int, int, int, float> vn = (x, y, px, py, seed) =>
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float l1 = Mathf.Lerp(hs(x0, y0, px, py, seed), hs(x0 + 1, y0, px, py, seed), fx);
            float l2 = Mathf.Lerp(hs(x0, y0 + 1, px, py, seed), hs(x0 + 1, y0 + 1, px, py, seed), fx);
            return Mathf.Lerp(l1, l2, fy);
        };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, w = y / (float)N;
                // the scars wobble round the trunk; each band between two of them is its own shade
                float wob = (vn(u * 6f, w * Rings, 6, Rings, 3) - 0.5f) * 0.45f + (vn(u * 17f, w * Rings * 2f, 17, Rings * 2, 9) - 0.5f) * 0.16f;
                float rr = w * Rings + wob; int ring = Mathf.FloorToInt(rr); float r = rr - ring;
                float ringTone = hs(ring, 0, Rings, 1, 21) - 0.5f;
                float groove = Mathf.Exp(-Mathf.Pow((r - 0.10f) / 0.06f, 2f));      // the scar: a wide, soft cut
                float lip = Mathf.Exp(-Mathf.Pow((r - 0.22f) / 0.09f, 2f));         // and a rough raised band above it
                float fibre = vn(u * 140f, w * 6f, 140, 6, 5) * 0.6f + vn(u * 60f, w * 3f, 60, 3, 7) * 0.4f;   // long streaks up the trunk
                float crack = Mathf.Pow(Mathf.Max(0f, vn(u * 90f, w * Rings * 1.5f, 90, 12, 13) - 0.72f) / 0.28f, 1.5f);   // short vertical cracks
                float rough = vn(u * 40f, w * 40f, 40, 40, 17), blot = vn(u * 5f, w * 5f, 5, 5, 11);
                h[y * N + x] = 0.55f + lip * 0.30f - groove * 0.6f + (fibre - 0.5f) * 0.22f - crack * 0.35f + (rough - 0.5f) * 0.12f * lip;
                float tone = 0.86f + (fibre - 0.5f) * 0.26f + (blot - 0.5f) * 0.16f + ringTone * 0.16f + lip * 0.08f - groove * 0.36f - crack * 0.30f + (rough - 0.5f) * 0.14f * lip;
                var bc = new Color(0.50f, 0.43f, 0.35f) * tone;                      // weathered grey-brown
                c[y * N + x] = new Color(Mathf.Clamp01(bc.r), Mathf.Clamp01(bc.g), Mathf.Clamp01(bc.b), 1f);
            }
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
                float dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                var v3 = new Vector3(-dx * 6f, -dy * 6f, 1f).normalized;
                nn[y * N + x] = new Color(v3.x * 0.5f + 0.5f, v3.y * 0.5f + 0.5f, v3.z * 0.5f + 0.5f, 1f);   // alpha 1: read as plain xy everywhere
            }
        col = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { name = "palm bark", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        col.SetPixels32(c); col.Apply(true, true);
        nrm = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "palm bark relief", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        nrm.SetPixels32(nn); nrm.Apply(true, true);
    }

    // ---- the Surf sign on the palm, made to read ----
    // Its letters are carved into the plank, the same wood as the rest, so in the palm's shade the
    // word all but vanished. They are painted the signpost's yellow here: the triangles of the cut
    // (between the face and the floor of the carving, inside the plank's margin -- measured on the
    // model, they are exactly the word) go to a second part of the mesh with a painted material,
    // and the wood itself is lifted a little so the plank reads as the signposts' wood does.
    static void PaintSign(GameObject sign)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var mf in sign.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>(); var src = mf.sharedMesh;
            if (mr == null || src == null || !src.isReadable || src.subMeshCount != 1) continue;
            foreach (var m in mr.materials)
                foreach (var p in new[] { "_BaseColor", "baseColorFactor", "_Color" })
                    if (m.HasProperty(p)) { var c = m.GetColor(p); m.SetColor(p, new Color(Mathf.Min(1f, c.r * 1.18f), Mathf.Min(1f, c.g * 1.14f), Mathf.Min(1f, c.b * 1.08f), c.a)); }
            if (lit == null) continue;
            var v = src.vertices; var tri = src.triangles; var bb = src.bounds; var ext = bb.size;
            int thin = ext.x < ext.y ? (ext.x < ext.z ? 0 : 2) : (ext.y < ext.z ? 1 : 2);
            int a0 = (thin + 1) % 3, a1 = (thin + 2) % 3; if (ext[a1] > ext[a0]) { int s = a0; a0 = a1; a1 = s; }
            // the carving is cut in from one face: whichever side finds the word is that face
            var front = new List<int>(); var back = new List<int>(); var rest = new List<int>();
            var side = new int[tri.Length / 3];
            for (int t = 0; t < tri.Length; t += 3)
            {
                var c = (v[tri[t]] + v[tri[t + 1]] + v[tri[t + 2]]) / 3f;
                float dn = (c[thin] - bb.min[thin]) / Mathf.Max(1e-5f, ext[thin]);
                float u0 = (c[a0] - bb.min[a0]) / Mathf.Max(1e-5f, ext[a0]), u1 = (c[a1] - bb.min[a1]) / Mathf.Max(1e-5f, ext[a1]);
                bool inside = u0 > 0.14f && u0 < 0.86f && u1 > 0.12f && u1 < 0.90f;
                side[t / 3] = !inside ? 0 : (dn >= 0.5f && dn <= 0.93f) ? 1 : (dn >= 0.07f && dn < 0.5f) ? 2 : 0;
                if (side[t / 3] == 1) front.Add(t); else if (side[t / 3] == 2) back.Add(t);
            }
            int pick = front.Count >= back.Count ? 1 : 2;
            if ((pick == 1 ? front.Count : back.Count) < 200) continue;          // not the carved sign after all
            var letters = new List<int>();
            for (int t = 0; t < tri.Length; t += 3)
            {
                var into = side[t / 3] == pick ? letters : rest;
                into.Add(tri[t]); into.Add(tri[t + 1]); into.Add(tri[t + 2]);
            }
            var mesh = Instantiate(src); mesh.name = src.name + " (painted)";
            mesh.subMeshCount = 2; mesh.SetTriangles(rest, 0); mesh.SetTriangles(letters, 1);
            mf.sharedMesh = mesh;
            var paint = new Material(lit) { name = "sign letters" };
            paint.SetColor("_BaseColor", new Color(1f, 0.84f, 0.30f)); paint.SetFloat("_Smoothness", 0.35f);
            // a little light of its own, so the word still reads in the shade of the crown
            paint.EnableKeyword("_EMISSION"); paint.SetColor("_EmissionColor", new Color(0.22f, 0.16f, 0.03f));
            mr.sharedMaterials = new[] { mr.sharedMaterial, paint };
        }
    }

    // ---- a layout set by hand ----
    // In the editor, while the game plays: move the shop, the signpost, the chest, the palm (its sign,
    // nuts and board go with it) or PugSpot (where the rider stands) with the scene view's tools, and
    // set the lens here in the inspector (camAt, camTurn, camFov, focusDist: the shot follows live).
    // Then "Save layout" from this component's menu writes it all to Resources/beach_layout.json, which
    // is loaded on every build of the beach, the phone's included. Commit the file to keep it.
    const string LayoutFile = "beach_layout";
    static readonly string[] Movable = { "SurfShop", "Signpost", "Chest", "Palm", "PugSpot", "Palm2" };
    const int LayoutVersion = 2;        // a layout saved against an older set of defaults is not loaded over newer ones
    [System.Serializable] class LayoutItem { public string name; public Vector3 pos, rot, scale; }
    [System.Serializable] class Layout { public int version; public List<LayoutItem> items = new List<LayoutItem>(); public Vector3 camAt, camTurn; public float camFov, focusDist, pugFace, pugScale; }

    const string LayoutPref = "beach_layout";
    void LoadLayout()
    {
        // this device's own arrangement first (moved by hand on it), else the project's
        string json = PlayerPrefs.GetString(LayoutPref, "");
        if (string.IsNullOrEmpty(json)) { var ta = Resources.Load<TextAsset>(LayoutFile); if (ta == null) return; json = ta.text; }
        Layout l; try { l = JsonUtility.FromJson<Layout>(json); } catch { return; }
        if (l == null || l.version != LayoutVersion) return;
        if (l.camFov > 1f) { camAt = l.camAt; camTurn = l.camTurn; camFov = l.camFov; }
        if (l.focusDist > 0f) focusDist = l.focusDist;
        if (l.pugScale > 0f) { pugFace = l.pugFace; pugScale = l.pugScale; }
        if (l.items != null)
            foreach (var it in l.items)
            {
                var t = root.transform.Find(it.name); if (t == null) continue;
                t.position = it.pos; t.eulerAngles = it.rot; if (it.scale != Vector3.zero) t.localScale = it.scale;
            }
        if (palmGo != null && leanBoard != null) SetBoard(SaveData.S.equipped);   // the board back against the trunk where it now is
    }

    // ---- held and dragged (BeachEdit) ----
    // Each movable thing gets a grab box on the Ignore Raycast layer: the signpost's own taps use
    // the default mask and never meet them, and only a held finger looks for them.
    const int GrabLayer = 2;
    void EditHandles()
    {
        foreach (var n in Movable)
        {
            var t = root.transform.Find(n); if (t == null) continue;
            Bounds b;
            if (n == "PugSpot") b = new Bounds(t.position + Vector3.up * 1.4f * pugScale, new Vector3(1.6f, 2.8f, 1.6f) * pugScale);
            else if (n == "Palm" || n == "Palm2") b = new Bounds(t.position + Vector3.up * 4.5f, new Vector3(2.0f, 9f, 2.0f));   // the trunk, not the whole crown
            else
            {
                var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            }
            var h = new GameObject("grab", typeof(BoxCollider)); h.layer = GrabLayer;
            h.transform.SetParent(t, false);
            h.transform.position = b.center; h.transform.rotation = Quaternion.identity;
            var s = t.lossyScale; float k = Mathf.Max(1e-4f, Mathf.Abs(s.x));
            h.GetComponent<BoxCollider>().size = b.size / k;
        }
    }

    static readonly string[] Labels = { "the shop", "the signpost", "the chest", "the palm", "the surfer", "the far palm" };
    /// what a held finger is on, if it is something that can be moved
    public Transform EditTarget(Camera cam, Vector2 screen, out string label)
    {
        label = null;
        if (root == null || !Physics.Raycast(cam.ScreenPointToRay(screen), out var hit, 300f, 1 << GrabLayer)) return null;
        var t = hit.collider.transform.parent; if (t == null) return null;
        int i = System.Array.IndexOf(Movable, t.name); if (i < 0) return null;
        label = Labels[i]; return t;
    }

    [ContextMenu("Save layout")]
    public void SaveLayout()
    {
        if (root == null) { Debug.LogWarning("Surf: press Play first, then set the beach out and save it"); return; }
        var l = new Layout { version = LayoutVersion, camAt = camAt, camTurn = camTurn, camFov = camFov, focusDist = focusDist, pugFace = pugFace, pugScale = pugScale };
        foreach (var n in Movable)
        {
            var t = root.transform.Find(n); if (t == null) continue;
            l.items.Add(new LayoutItem { name = n, pos = t.position, rot = t.eulerAngles, scale = t.localScale });
        }
        var json = JsonUtility.ToJson(l, true);
        PlayerPrefs.SetString(LayoutPref, json); PlayerPrefs.Save();             // kept on this device
#if UNITY_EDITOR
        // and in the editor, into the project, so it can be committed and every build gets it
        System.IO.Directory.CreateDirectory("Assets/Resources");
        System.IO.File.WriteAllText("Assets/Resources/" + LayoutFile + ".json", json);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("Surf: beach layout saved to Assets/Resources/" + LayoutFile + ".json -- commit it in GitHub Desktop to keep it");
#endif
    }

    [ContextMenu("Forget saved layout")]
    public void ForgetLayout()
    {
        PlayerPrefs.DeleteKey(LayoutPref); PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.DeleteAsset("Assets/Resources/" + LayoutFile + ".json");
        Debug.Log("Surf: saved beach layout removed; the next Play uses the built-in one");
#endif
    }

    // ---- the signpost stands clear of the sand ----
    // Placed at the web's height, its bottom arm went into the dune here (the sand under it stands
    // higher than the web's did). The arms are the whole post, so it is lifted, colliders and all,
    // until the bottom of the lowest arm clears the highest sand under it.
    void LiftPost(GameObject postGo)
    {
        var b = Models.Bounds(postGo);
        float top = float.MinValue;
        for (int i = 0; i <= 4; i++)
            for (int j = 0; j <= 2; j++)
                top = Mathf.Max(top, GroundTop(Mathf.Lerp(b.min.x, b.max.x, i / 4f), Mathf.Lerp(b.min.z, b.max.z, j / 2f)));
        float lift = top + 0.12f - b.min.y;
        if (lift > 0f && lift <= 3f)                                              // (more and the sand read is not to be trusted)
        {
            postGo.transform.position += Vector3.up * lift;          // its tap areas are its children
            postAt.y += lift;
            b = Models.Bounds(postGo);
        }
        // and if it stands well clear of the sand (set high in the frame), its pole is carried on
        // down into the sand, so it is planted rather than floating
        float ground = GroundTop(b.center.x, b.center.z);
        if (b.min.y - ground > 0.15f)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pole.name = "pole";
            Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(postGo.transform, true);
            float len = b.min.y - ground + 0.6f, r = Mathf.Max(0.06f, postHeight * 0.045f);
            pole.transform.position = new Vector3(b.center.x, ground - 0.4f + len * 0.5f, b.center.z);
            pole.transform.rotation = Quaternion.identity;
            var ls = postGo.transform.lossyScale; float k = Mathf.Max(1e-4f, Mathf.Abs(ls.x));
            pole.transform.localScale = new Vector3(r * 2f, len * 0.5f, r * 2f) / k;   // a cylinder is two units tall
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null) { var m = new Material(lit) { name = "post wood" }; m.SetColor("_BaseColor", new Color(0.36f, 0.24f, 0.14f)); m.SetFloat("_Smoothness", 0.15f); pole.GetComponent<Renderer>().sharedMaterial = m; }
        }
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
