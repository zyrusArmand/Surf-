// The title screen's beach: a straight shore looking out to sea, dry sand in the foreground
// rising to a berm, a palm with the Surf sign hung in it, the chest with a board, the pug
// standing by it, and the signpost whose arms are the menu. The same props the web game's
// beach is built from. Everything is placed on a public field so it can be nudged in the
// Inspector while looking at it, which is how the web game's beach was composed too.
using UnityEngine;

public class Beach : MonoBehaviour
{
    [Header("Where things stand (feet; -z is out to sea)")]
    public Vector3 palmAt = new Vector3(-2.5f, 0, -13f);
    public float palmHeight = 27f, palmLean = 4f, palmTurn = 20f;
    public Vector3 signOnPalm = new Vector3(0.95f, 10.5f, 1.1f);   // relative to the palm's foot
    public float signHeight = 1.4f, signTurn = -8f;
    public Vector3 chestAt = new Vector3(-7f, 0, -1.5f);
    public float chestSize = 2.6f, chestTurn = 28f;
    public Vector3 pugAt = new Vector3(-3.8f, 0, 2.2f);
    public float pugTurn = 196f;
    public Vector3 postAt = new Vector3(7.2f, 0, 5.5f);
    public float postHeight = 9.5f, postTurn = -14f;
    public Vector3 boardAt = new Vector3(0.9f, 0, -10.5f);
    public float boardLean = 22f, boardTurn = -30f;
    [Header("The shot")]
    public Vector3 camAt = new Vector3(0.6f, 6.6f, 22f);
    public Vector3 camAim = new Vector3(-0.4f, 5.0f, -8f);
    public float camFov = 58f;

    public System.Action<string> onSign;       // "Play", "Shop", "Quests"

    GameObject root; Transform palm, post, pugSpot; GameObject leanBoard;
    Mesh sandMesh; float t;
    static readonly string[] Arms = { "Quests", "Shop", "Play" };   // bottom to top

    // the beach profile: under water a long way out, rising to dry sand with a berm at the tide line
    static float SS(float a, float b, float x) { float k = Mathf.Clamp01((x - a) / (b - a)); return k * k * (3f - 2f * k); }
    public static float SandY(float x, float z)
    {
        float h = 9.0f * SS(-180f, 10f, z) - 4.6f;
        float d = SS(-68f, -52f, z); h += d * d * 1.6f;
        // dunes, only where the sand is dry enough for the wind to shape it
        float dry = SS(-60f, -46f, z);
        float n = Mathf.PerlinNoise(x * 0.055f + 3.1f, z * 0.075f) - 0.5f;
        float n2 = Mathf.PerlinNoise(x * 0.21f, z * 0.26f + 7.7f) - 0.5f;
        h += dry * (n * 1.9f + n2 * 0.45f);
        return h;
    }

    public void Build(Transform parent)
    {
        root = new GameObject("Beach"); root.transform.SetParent(parent, false);
        // the sand
        var sandGo = new GameObject("Sand", typeof(MeshFilter), typeof(MeshRenderer)); sandGo.transform.SetParent(root.transform, false);
        sandMesh = BuildSand(760f, 460f, -100f, 190, 230);
        sandGo.GetComponent<MeshFilter>().sharedMesh = sandMesh;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var sm = new Material(lit); sm.SetColor("_BaseColor", new Color(0.78f, 0.70f, 0.56f)); sm.SetFloat("_Smoothness", 0.12f);
        var grain = Resources.Load<Texture2D>("Models/sandgrain");
        if (grain != null) { sm.EnableKeyword("_NORMALMAP"); sm.SetTexture("_BumpMap", grain); sm.SetFloat("_BumpScale", 0.9f); sm.SetTextureScale("_BumpMap", new Vector2(90f, 55f)); }
        var smr = sandGo.GetComponent<MeshRenderer>(); smr.sharedMaterial = sm; smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // the palm, with the sign hung in it and a board leaning on the trunk
        palm = Spot("Palm", palmAt);
        var palmGo = Models.Spawn("palmlod", palm, palmHeight, PrimitiveType.Cylinder);
        palm.localRotation = Quaternion.Euler(0, palmTurn, 0) * Quaternion.Euler(0, 0, -palmLean);
        var signSpot = new GameObject("SignSpot").transform; signSpot.SetParent(palm, false);
        signSpot.localPosition = signOnPalm; signSpot.localRotation = Quaternion.Euler(0, signTurn, 0);
        Models.Spawn("sign", signSpot, signHeight, PrimitiveType.Cube);
        var boardSpot = Spot("BoardSpot", boardAt);
        leanBoard = Models.Spawn("board", boardSpot, 0.3f, PrimitiveType.Capsule);
        Models.FitLength(leanBoard, BoardController.BoardHalfL * 2f);
        boardSpot.localRotation = Quaternion.Euler(0, boardTurn, 0) * Quaternion.Euler(90f - boardLean, 0, 0);
        boardSpot.localPosition += new Vector3(0, BoardController.BoardHalfL * Mathf.Cos(boardLean * Mathf.Deg2Rad), 0);

        // the chest
        var chestSpot = Spot("Chest", chestAt);
        Models.Spawn("chest", chestSpot, chestSize, PrimitiveType.Cube);
        chestSpot.localRotation = Quaternion.Euler(0, chestTurn, 0);

        // where the pug stands
        pugSpot = Spot("PugSpot", pugAt);

        // the signpost: its arms are the menu, so each third of it gets a collider to tap
        post = Spot("Signpost", postAt);
        var postGo = Models.Spawn("menusign", post, postHeight, PrimitiveType.Cube);
        post.localRotation = Quaternion.Euler(0, postTurn, 0);
        var b = Models.Bounds(postGo);
        for (int i = 0; i < 3; i++)
        {
            var arm = new GameObject("arm:" + Arms[i]); arm.transform.SetParent(post, false);
            var bc = arm.AddComponent<BoxCollider>();
            float h = b.size.y / 3f;
            bc.center = new Vector3(b.center.x, b.min.y + h * (i + 0.5f), b.center.z);
            bc.size = new Vector3(b.size.x, h, Mathf.Max(b.size.z, 1f));
        }
        Show(false);
    }

    Transform Spot(string n, Vector3 at)
    {
        var s = new GameObject(n).transform; s.SetParent(root.transform, false);
        s.localPosition = new Vector3(at.x, SandY(at.x, at.z) + at.y, at.z);
        return s;
    }

    Mesh BuildSand(float w, float d, float zc, int nx, int nz)
    {
        var m = new Mesh { name = "sand" }; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var v = new Vector3[(nx + 1) * (nz + 1)]; var uv = new Vector2[v.Length];
        for (int j = 0; j <= nz; j++)
        for (int i = 0; i <= nx; i++)
        {
            float x = (i / (float)nx - 0.5f) * w, z = zc + (j / (float)nz - 0.5f) * d;
            v[j * (nx + 1) + i] = new Vector3(x, SandY(x, z), z);
            uv[j * (nx + 1) + i] = new Vector2(i / (float)nx, j / (float)nz);
        }
        var idx = new int[nx * nz * 6]; int n = 0;
        for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++)
        {
            int a = j * (nx + 1) + i, bq = a + 1, c = a + nx + 1, e = c + 1;
            idx[n++] = a; idx[n++] = c; idx[n++] = bq; idx[n++] = bq; idx[n++] = c; idx[n++] = e;
        }
        m.vertices = v; m.uv = uv; m.triangles = idx; m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    public void Show(bool on) { if (root != null) root.SetActive(on); }
    public bool Showing => root != null && root.activeSelf;

    /// the pug and his board go to their spots on the sand; the ride's board is hidden under him
    public void PlaceRider(Transform player, Transform rig, GameObject rideBoard)
    {
        player.position = pugSpot.position; player.rotation = Quaternion.Euler(0, pugTurn, 0);
        rig.localRotation = Quaternion.identity;
        if (rideBoard != null) rideBoard.SetActive(false);
    }

    /// the shot, with a slow breath so it is not a photograph
    public void PlaceCamera(Camera cam, float dt)
    {
        t += dt;
        var at = camAt + new Vector3(Mathf.Sin(t * 0.21f) * 0.25f, Mathf.Sin(t * 0.17f + 1f) * 0.12f, 0);
        cam.transform.position = at; cam.transform.LookAt(camAim, Vector3.up); cam.fieldOfView = camFov;
    }

    /// a tap on the signpost: which arm was hit
    public void Tap(Camera cam, Vector2 screen)
    {
        var ray = cam.ScreenPointToRay(screen);
        if (Physics.Raycast(ray, out var hit, 200f) && hit.collider.name.StartsWith("arm:")) onSign?.Invoke(hit.collider.name.Substring(4));
    }
}
