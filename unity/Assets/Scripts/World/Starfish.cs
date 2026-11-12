// The octopus's starfish, built as the web builds it (buildStarfish, index.html ~43230): a
// plump five-armed body with rounded, bevelled edges, rows of pale bumps down each arm on both
// faces, two black eyes with a gleam, and a grin -- in one of three colours (orange, pink, gold).
// The same little character is what lands on the lens: Splat shows it pressed against the
// screen, squashing as it hits and then sliding down and off (web splatOnScreen / updateSplat).
using UnityEngine;

public static class Starfish
{
    // the web's three tints, before its vivid(1.7) push
    static readonly uint[] Tints = { 0xff8a3d, 0xff5f8a, 0xffb037 };
    static Mesh body, sphere;
    static Material[] skins; static Material dot, black;

    public static int Variants => Tints.Length;

    /// the tint of a variant, as the sRGB colour the material shows (vivid 1.7)
    public static Color Tint(int variant) => Vivid(Tints[((variant % 3) + 3) % 3], 1.7f);

    // web vivid(): each channel pushed away from the grey of its own average
    static Color Vivid(uint hex, float boost)
    {
        float r = ((hex >> 16) & 255) / 255f, g = ((hex >> 8) & 255) / 255f, b = (hex & 255) / 255f;
        float avg = (r + g + b) / 3f, k = 0.30f * boost;
        return new Color(Mathf.Clamp01(avg + (r - avg) * (1 + k)), Mathf.Clamp01(avg + (g - avg) * (1 + k)), Mathf.Clamp01(avg + (b - avg) * (1 + k)));
    }

    static Material Lit(Color c, float rough)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh);
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 1f - rough); m.SetFloat("_Metallic", 0f);
        return m;
    }

    static void Ensure()
    {
        if (body != null) return;
        body = BodyMesh();
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere = tmp.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(tmp);
        skins = new Material[Tints.Length];
        for (int i = 0; i < Tints.Length; i++) skins[i] = Lit(Tint(i), 0.52f);
        dot = Lit(Vivid(0xfff1de, 1.2f), 0.40f);
        black = Lit(new Color(0x1a / 255f, 0x10 / 255f, 0x16 / 255f), 0.18f);
    }

    static Mesh whole;     // body, bumps, eyes and grin in one mesh: submesh 0 the skin, 1 the pale dots, 2 the black

    /// one starfish, its face toward +z, about 1.1 ft tip to tip (the web's 0.92 scale). One object
    /// and one mesh, three materials: built from forty-odd parts once, then shared, so a throw is
    /// one GameObject rather than forty.
    public static GameObject Build(int variant, Transform parent, Material skinOverride = null)
    {
        Ensure();
        if (whole == null) whole = Bake();
        var g = new GameObject("starfish", typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(parent, false);
        g.GetComponent<MeshFilter>().sharedMesh = whole;
        var mr = g.GetComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { skinOverride != null ? skinOverride : skins[((variant % 3) + 3) % 3], dot, black };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.localScale = Vector3.one * 0.92f;
        return g;
    }

    static Mesh Bake()
    {
        var skinP = new System.Collections.Generic.List<CombineInstance>();
        var dotP = new System.Collections.Generic.List<CombineInstance>();
        var blackP = new System.Collections.Generic.List<CombineInstance>();
        void Add(System.Collections.Generic.List<CombineInstance> to, Mesh m, Vector3 pos, Vector3 scale)
            => to.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, Quaternion.identity, scale) });
        Add(skinP, body, Vector3.zero, Vector3.one);
        const int N = 5;
        for (int i = 0; i < N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f - Mathf.PI / 2f;     // the web's: one arm straight down, the face between the upper two
            for (int k = 0; k < 3; k++)
            {
                float d = 0.27f + k * 0.135f, rr = 0.050f - k * 0.009f;
                foreach (float zz in new[] { 0.185f, -0.185f })        // the body's faces sit at |z| 0.18: these read as bumps
                    Add(dotP, sphere, new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, zz), new Vector3(rr * 2f, rr * 2f, rr));
            }
        }
        foreach (float sx in new[] { -1f, 1f })
        {
            Add(blackP, sphere, new Vector3(sx * 0.125f, 0.115f, 0.200f), new Vector3(0.066f * 2f * 0.88f, 0.066f * 2f * 1.1f, 0.066f * 2f * 0.42f));
            Add(dotP, sphere, new Vector3(sx * 0.148f, 0.150f, 0.224f), new Vector3(0.042f, 0.042f, 0.021f));
        }
        // the grin: the lower half of a small ring, drawn as a row of beads
        for (int i = 0; i <= 8; i++)
        {
            float a = Mathf.PI + i / 8f * Mathf.PI;
            Add(blackP, sphere, new Vector3(Mathf.Cos(a) * 0.088f, -0.010f + Mathf.Sin(a) * 0.088f, 0.203f), new Vector3(0.042f, 0.042f, 0.019f));
        }
        Mesh Merge(System.Collections.Generic.List<CombineInstance> parts)
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(parts.ToArray(), true, true);
            return m;
        }
        var subs = new[] { Merge(skinP), Merge(dotP), Merge(blackP) };
        var outM = new Mesh { name = "starfish", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        var ci = new CombineInstance[3];
        for (int i = 0; i < 3; i++) ci[i] = new CombineInstance { mesh = subs[i], transform = Matrix4x4.identity };
        outM.CombineMeshes(ci, false, false);                        // three submeshes, one per material
        outM.RecalculateBounds();
        return outM;
    }

    // The body: the web's outline (each arm a pair of quadratic curves through a valley pulled out
    // by 1.34, tips at 0.62, valleys at 0.335), extruded 0.14 with a 0.10 bevel each side reaching
    // 0.062 out -- here as a pillow: the rim at the bevelled outline, rounding over onto two flat
    // faces 0.17 out, so the light runs round its edge the way it runs round the web's.
    static Mesh BodyMesh()
    {
        const int N = 5, K = 7, Rings = 7; const float R = 0.62f, rv = 0.335f, half = 0.17f, bevel = 0.062f;
        var outline = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i < N; i++)
        {
            float a0 = i / (float)N * Mathf.PI * 2f - Mathf.PI / 2f, a1 = a0 + Mathf.PI / N, a2 = a0 + 2f * Mathf.PI / N;
            var tip = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * R;
            var vly = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rv;
            var nxt = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * R;
            var c = vly * 1.34f;
            for (int k = 0; k < K; k++) { float t = k / (float)K; outline.Add((1 - t) * (1 - t) * tip + 2 * (1 - t) * t * c + t * t * vly); }
            for (int k = 0; k < K; k++) { float t = k / (float)K; outline.Add((1 - t) * (1 - t) * vly + 2 * (1 - t) * t * c + t * t * nxt); }
        }
        int M = outline.Count;
        var v = new System.Collections.Generic.List<Vector3>(); var idx = new System.Collections.Generic.List<int>();
        // ring 0 is the rim (z 0); rings 1..Rings climb onto each face; then a centre point per face
        for (int face = 0; face < 2; face++)
        {
            float sz = face == 0 ? 1f : -1f; int baseV = v.Count;
            for (int ring = 0; ring <= Rings; ring++)
            {
                float t = ring / (float)Rings;                         // 0 at the rim, 1 well onto the face
                float inward = t * 0.55f;                              // how far toward the middle this ring is
                float z = half * Mathf.Sin(Mathf.Min(1f, t * 1.6f) * Mathf.PI * 0.5f);
                for (int j = 0; j < M; j++)
                {
                    var o = outline[j]; float len = o.magnitude;
                    var rim = len > 1e-4f ? o * ((len + bevel * (1f - Mathf.Min(1f, t * 1.6f))) / len) : o;
                    var p = rim * (1f - inward);
                    v.Add(new Vector3(p.x, p.y, z * sz));
                }
            }
            int centre = v.Count; v.Add(new Vector3(0, 0, half * sz));
            for (int ring = 0; ring < Rings; ring++)
                for (int j = 0; j < M; j++)
                {
                    int j2 = (j + 1) % M, A = baseV + ring * M + j, B = baseV + ring * M + j2, C = A + M, D = B + M;
                    // wound clockwise as seen from outside each face (Unity's front), the outline running anticlockwise
                    if (face == 0) { idx.Add(A); idx.Add(B); idx.Add(C); idx.Add(B); idx.Add(D); idx.Add(C); }
                    else { idx.Add(A); idx.Add(C); idx.Add(B); idx.Add(B); idx.Add(C); idx.Add(D); }
                }
            int last = baseV + Rings * M;
            for (int j = 0; j < M; j++)
            {
                int j2 = (j + 1) % M;
                if (face == 0) { idx.Add(last + j); idx.Add(last + j2); idx.Add(centre); }
                else { idx.Add(last + j); idx.Add(centre); idx.Add(last + j2); }
            }
        }
        var mesh = new Mesh { name = "starfish" };
        mesh.SetVertices(v); mesh.SetTriangles(idx, 0); mesh.RecalculateNormals();
        // Each face has its own copy of the rim ring, so the normals there were each face's own and
        // met in a crease round the edge. Set both copies to the edge's own outward direction (in
        // the plane of the star, across the outline), so the light rolls round the edge instead.
        var nrm = mesh.normals;
        for (int face = 0; face < 2; face++)
        {
            int baseV = face * ((Rings + 1) * M + 1);
            for (int j = 0; j < M; j++)
            {
                Vector2 prev = outline[(j - 1 + M) % M], next = outline[(j + 1) % M];
                Vector2 tan = next - prev; var outw = new Vector2(tan.y, -tan.x);   // the outline runs anticlockwise: right of it is outward
                if (outw.sqrMagnitude < 1e-10f) outw = outline[j];
                outw.Normalize();
                nrm[baseV + j] = new Vector3(outw.x, outw.y, 0f);
            }
        }
        mesh.normals = nrm; mesh.RecalculateBounds();
        return mesh;
    }
}

/// The starfish that hits the lens (web splatOnScreen / updateSplat, ~44030-44060): pressed
/// against the screen a little off centre, flattening for a moment as it lands, then sliding down
/// and off over 2.4 s, wobbling as it goes. It lives just in front of the camera, so the world's
/// own light falls on it. One at a time: the spawner holds the next off until this one is gone.
public class StarSplat : MonoBehaviour
{
    public Camera cam;
    GameObject star; Material skin; float t, spin, sx;
    const float Dur = 2.4f, D = 1.05f;
    public bool Busy => t > 0f;

    public void Show(Color tint)
    {
        if (cam == null) return;
        if (star == null)
        {
            skin = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            skin.SetFloat("_Smoothness", 0.48f); skin.SetFloat("_Metallic", 0f);
            star = Starfish.Build(0, cam.transform, skin);
            star.layer = cam.gameObject.layer;
        }
        skin.SetColor("_BaseColor", tint);
        t = Dur; spin = (Random.value - 0.5f) * 0.9f; sx = (Random.value - 0.5f) * 0.28f;
        star.SetActive(true);
        Place();
    }

    public void Hide() { t = 0f; if (star != null) star.SetActive(false); }

    void LateUpdate() { if (t > 0f) { t -= Time.deltaTime; if (t <= 0f) Hide(); else Place(); } }

    void Place()
    {
        float k = Mathf.Clamp01(t / Dur), age = 1f - k;
        float squash = 1f + Mathf.Max(0f, 0.20f - age * 2.6f);
        float vh = 2f * D * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f), vw = vh * cam.aspect;
        float S = Mathf.Min(vw * 0.82f, vh * 0.62f) / 1.09f / 0.92f;   // 1.09 tip to tip at scale 1; the build's own 0.92 taken back out
        var tr = star.transform;
        tr.localPosition = new Vector3(sx * vw * 0.16f + Mathf.Sin(age * 7f) * 0.02f, (0.10f - age * age * 1.45f) * vh * 0.85f, D);
        tr.localRotation = Quaternion.Euler(0.10f * Mathf.Sin(age * 4f) * Mathf.Rad2Deg, 180f + 0.16f * Mathf.Sin(age * 3f) * Mathf.Rad2Deg, (spin + age * 0.55f) * Mathf.Rad2Deg);
        tr.localScale = new Vector3(S * squash, S / squash, S) * 0.92f;
    }
}
