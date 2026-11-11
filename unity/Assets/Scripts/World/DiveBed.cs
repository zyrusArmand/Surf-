// The sea bed under a dive: the web's dunes (Dive.BedH), seventeen feet under mean water and
// scrolling past at the rider's own speed, so a prop standing on it stays on its patch of
// sand. The heights are a function of the GROUND coordinate (z minus the distance travelled),
// so the mesh is built in ground space over a window and the whole thing is moved along by the
// distance; when the rider has gone far enough that the window would run out, the heights are
// filled in again for a fresh window. Textured with the island's own sand; the dive fog does
// the rest of the colour.
using UnityEngine;

public class DiveBed : MonoBehaviour
{
    const float X0 = -160f, X1 = 160f, ZNear = 100f, ZFar = -380f, Slack = 40f;
    const int Cols = 64, Rows = 120;
    Mesh mesh; Vector3[] v; float builtD = float.NaN; bool built;

    void Build()
    {
        built = true;
        var mf = gameObject.AddComponent<MeshFilter>(); var mr = gameObject.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "divebed" }; mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        v = new Vector3[(Cols + 1) * (Rows + 1)];
        var uv = new Vector2[v.Length];
        for (int r = 0; r <= Rows; r++) for (int c = 0; c <= Cols; c++)
        {
            float x = Mathf.Lerp(X0, X1, c / (float)Cols), z = Mathf.Lerp(ZNear + Slack, ZFar - Slack, r / (float)Rows);
            v[r * (Cols + 1) + c] = new Vector3(x, -Dive.BedDepth, z);
            uv[r * (Cols + 1) + c] = new Vector2(x / 14f, z / 14f);
        }
        var idx = new int[Cols * Rows * 6]; int n = 0;
        for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
        {
            int a = r * (Cols + 1) + c, b = a + 1, d = a + Cols + 1, e = d + 1;
            idx[n++] = a; idx[n++] = d; idx[n++] = b; idx[n++] = b; idx[n++] = d; idx[n++] = e;
        }
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = idx; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(lit != null ? lit : Shader.Find("Standard"));
        m.SetColor("_BaseColor", new Color(0.80f, 0.78f, 0.68f)); m.SetFloat("_Smoothness", 0.05f);
        var baseTex = Resources.Load<Texture2D>("Models/sand_base"); var nrm = Resources.Load<Texture2D>("Models/sand_normal");
        if (baseTex != null) { baseTex.wrapMode = TextureWrapMode.Repeat; m.SetTexture("_BaseMap", baseTex); }
        if (nrm != null) { nrm.wrapMode = TextureWrapMode.Repeat; m.EnableKeyword("_NORMALMAP"); m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1.2f); }
        mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    public void Show(bool on)
    {
        if (!built && !on) return;
        if (!built) Build();
        if (gameObject.activeSelf != on) gameObject.SetActive(on);
    }

    /// move the bed along with the distance travelled (metres, as the rider counts it) and refill
    /// the heights when the window has slid far enough that the edge would show
    public void Place(float distM)
    {
        if (!built) Build();
        float d = distM * Dive.BedDrift;
        if (float.IsNaN(builtD) || d - builtD > Slack || d < builtD)
        {
            // baked for the world as it stands at this distance: local z IS world z at the
            // moment of the fill, and the mesh then slides along by whatever he travels after
            builtD = d;
            for (int r = 0; r <= Rows; r++) for (int c = 0; c <= Cols; c++)
            {
                int i = r * (Cols + 1) + c;
                float x = v[i].x, zl = Mathf.Lerp(ZNear + Slack, ZFar - Slack, r / (float)Rows);
                v[i] = new Vector3(x, Dive.BedH(x, zl, distM), zl);
            }
            mesh.vertices = v; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        transform.position = new Vector3(0f, 0f, d - builtD);   // the sand goes past at his speed
    }
}
