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

    [Header("The light and the sea")]
    public Vector3 keyFrom = new Vector3(0.943f, 0.117f, 0.31f);    // toward the sun, from the set (already mirrored)
    public float seaLevel = 1.21f;

    public System.Action<string> onSign;       // "Play", "Shop", "Quests"

    // The web camera looks down -z with +x on its right; Unity's, looking the same way, has +x on
    // its left. So the measured scene is mirrored across x on the way in: positions flip x, and
    // turns flip their yaw and roll. Text on the signs stays readable because a mirror of the
    // layout is not a mirror of the models.
    static Vector3 M(Vector3 p) => new Vector3(-p.x, p.y, p.z);
    static Vector3 R(Vector3 e) => new Vector3(e.x, -e.y, -e.z);

    GameObject root; Transform post, pugSpot; Mesh sandMesh; float t;
    static readonly string[] Arms = { "Quests", "Shop", "Play" };   // bottom to top

    // the beach profile along z, read off the web sand with a ray every five feet
    static readonly float[] ProfZ = { 20, 0, -20, -25, -30, -35, -40, -45, -50, -55, -60, -65, -70, -75, -80, -85, -90, -100, -120, -160, -300 };
    static readonly float[] ProfY = { 7.2f, 7.0f, 6.7f, 6.6f, 6.0f, 4.9f, 4.3f, 4.1f, 3.7f, 3.9f, 4.2f, 3.5f, 2.4f, 1.8f, 1.3f, 0.6f, 0.0f, -1.2f, -3.0f, -4.6f, -6f };

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
        // dunes where the sand is dry, and the flat disc the set stands on
        float dry = Mathf.Clamp01((z + 85f) / 20f);
        float n = (Mathf.PerlinNoise(x * 0.07f + 3.1f, z * 0.09f) - 0.5f) * 0.9f + (Mathf.PerlinNoise(x * 0.23f, z * 0.27f + 7.7f) - 0.5f) * 0.25f;
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
        var sm = new Material(lit); sm.SetColor("_BaseColor", new Color(1f, 0.97f, 0.9f)); sm.SetFloat("_Smoothness", 0.08f);
        var baseTex = Resources.Load<Texture2D>("Models/sand_base"); var nrm = Resources.Load<Texture2D>("Models/sand_normal");
        if (baseTex != null) sm.SetTexture("_BaseMap", baseTex);
        if (nrm != null) { sm.EnableKeyword("_NORMALMAP"); sm.SetTexture("_BumpMap", nrm); sm.SetFloat("_BumpScale", 1.35f); }
        var smr = sandGo.GetComponent<MeshRenderer>(); smr.sharedMaterial = sm; smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // the palm, leaning out to the right with the Surf sign hung in it
        var palm = Prop("Palm", Models.Spawn("palm2", root.transform, palmHeight, PrimitiveType.Cylinder), R(palmTurn), flipPalm);
        Settle(palm, -palmBoxCentre.x, palmFoot.y, palmBoxCentre.z, false);
        var sign = Prop("Sign", Models.Spawn("sign", root.transform, signHeight, PrimitiveType.Cube), R(signTurn), flipSign);
        Settle(sign, -signAt.x, signAt.y, signAt.z, true);
        // the board propped into the crook of the trunk
        var board = BoardMesh.Build("board"); board.transform.SetParent(root.transform, false);
        board.transform.rotation = Quaternion.Euler(R(boardTurn)); Settle(board, -boardAt.x, boardAt.y, boardAt.z, true);
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
        Show(false);
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

    public void Show(bool on) { if (root != null) root.SetActive(on); }
    public bool Showing => root != null && root.activeSelf;

    /// the pug stands by the chest, a little bigger than he rides, with the ride's board hidden
    public void PlaceRider(Transform player, Transform rig, GameObject rideBoard)
    {
        player.position = pugSpot.position; player.rotation = Quaternion.Euler(0, pugFace, 0);
        player.localScale = Vector3.one * pugScale;
        rig.localRotation = Quaternion.identity;
        if (rideBoard != null) rideBoard.SetActive(false);
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
    }

    /// a tap on the signpost: which arm was hit
    public void Tap(Camera cam, Vector2 screen)
    {
        var ray = cam.ScreenPointToRay(screen);
        if (Physics.Raycast(ray, out var hit, 200f) && hit.collider.name.StartsWith("arm:")) onSign?.Invoke(hit.collider.name.Substring(4));
    }
}
