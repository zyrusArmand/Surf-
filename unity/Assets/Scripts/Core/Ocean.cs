// The sea mesh: a flat grid wide enough to reach the horizon in portrait, finer near the rider
// where the chop is looked at, displaced on the GPU by the same wave list the CPU rides.
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Ocean : MonoBehaviour
{
    public Material material;
    public float width = 300f, nearZ = 60f, farZ = -900f;
    public int cols = 400, rows = 340;      // across: three quarters of a foot apart, the web's density, so no crest is carried through the board

    readonly Vector4[] wa = new Vector4[7], wb = new Vector4[7];

    bool built;

    /// called by Bootstrap once the material exists; builds the grid and feeds it the wave list
    public void Init(Material m)
    {
        material = m;
        if (!built) Build();
        var mr = GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        WaveField.FillShaderArrays(wa, wb);
        material.SetVectorArray("_WaveA", wa);
        material.SetVectorArray("_WaveB", wb);
    }

    void Build()
    {
        built = true;
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        var m = new Mesh { name = "ocean" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var v = new Vector3[(cols + 1) * (rows + 1)];
        var uv = new Vector2[v.Length];
        for (int r = 0; r <= rows; r++)
        {
            // rows are packed toward the rider: a quadratic in z puts most of the triangles
            // in the forty feet that fill the bottom of the frame
            float t = (float)r / rows;
            float z = Mathf.Lerp(nearZ, farZ, t * t * 0.85f + t * 0.15f);
            for (int c = 0; c <= cols; c++)
            {
                float x = Mathf.Lerp(-width / 2, width / 2, (float)c / cols);
                v[r * (cols + 1) + c] = new Vector3(x, 0, z);
                uv[r * (cols + 1) + c] = new Vector2((float)c / cols, t);
            }
        }
        var idx = new int[cols * rows * 6]; int n = 0;
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
        {
            int a = r * (cols + 1) + c, b = a + 1, d = a + cols + 1, e = d + 1;
            idx[n++] = a; idx[n++] = d; idx[n++] = b; idx[n++] = b; idx[n++] = d; idx[n++] = e;
        }
        m.vertices = v; m.uv = uv; m.triangles = idx;
        m.RecalculateNormals();
        m.bounds = new Bounds(new Vector3(0, 0, (nearZ + farZ) / 2), new Vector3(width, 40f, nearZ - farZ));
        mf.sharedMesh = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    public void Tick(float t, float dist, float amp, float wakeX, float wakeZ, float wakeA, float lift = 0f, float shore = 100000f)
    {
        material.SetFloat("_Lift", lift);
        material.SetFloat("_Calm", WaveField.Calm);
        material.SetFloat("_FallW", WaveField.FallW); material.SetFloat("_FallZF", WaveField.FallZF); material.SetFloat("_FallT", WaveField.FallT); material.SetFloat("_FallLane", WaveField.FallLane);
        material.SetFloat("_WhX", WaveField.WhX); material.SetFloat("_WhZ", WaveField.WhZ); material.SetFloat("_WhA", WaveField.WhA);
        material.SetFloat("_SwA", WaveField.SetA); material.SetFloat("_SwX", WaveField.SetX); material.SetFloat("_SwS", WaveField.SetS);
        material.SetFloat("_Shore", shore);
        material.SetFloat("_T", t);
        material.SetFloat("_Dist", dist);
        material.SetFloat("_Amp", amp);
        material.SetFloat("_Peak", 1.58f * amp);
        material.SetFloat("_WakeX", wakeX);
        material.SetFloat("_WakeZ", wakeZ);
        material.SetFloat("_WakeA", wakeA);
    }
}
