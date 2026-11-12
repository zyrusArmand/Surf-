// The seaplane's two propellers, made to turn.
//
// plane.glb is a single mesh: the twin engines' four-bladed props are baked into it with the
// rest of the airframe, so on its own nothing on it can spin. The blades are cut out of it here,
// once, into a child of their own per engine, pivoted on the hub and turned about the
// fuselage's axis every frame (web: prop.rotation.z += dt*40 on the tow, dt*(40-32k) as the
// set wave's plane goes down).
//
// The cut is by place, measured off the file. Each prop is a thin disc of blades just ahead of
// its nacelle, 0.417 to 0.434 from the model's middle toward the nose and 0.262 out to either
// side; a triangle goes with the blades when all three corners lie in that disc, within 0.15 of
// the hub, and it is not part of the spinner in the middle. The spinner stays where it is: it is
// round, so it looks the same turning or not, and leaving it means no seam where blade meets cowl.
//
// A faint disc sits over each, the blur the web's own built propeller carries (#d8dee4 at 0.2),
// so the turn reads as speed rather than four blades stepping round.
//
// A mesh the build left unreadable cannot be cut. Then a plain pair of blades is turned over
// each hub instead, so there is still a prop that spins.
using System.Collections.Generic;
using UnityEngine;

public class PlaneProps : MonoBehaviour
{
    public float rate = 40f;                   // radians a second

    // measured off plane.glb, in the file's own units (the mesh's local frame)
    const float SlabNear = 0.417f, SlabFar = 0.434f;          // the blades' disc, from the middle toward the nose
    const float HubX = 0.426f, HubY = 0.0125f, HubZ = 0.262f;  // the hubs, either side of the fuselage
    const float BladeIn = 0.04f, BladeOut = 0.15f;             // inside BladeIn is the spinner; past BladeOut is the wing
    const float BlurR = 0.145f;
    const int MinTris = 12;                                    // fewer than this in a disc and it is not this model

    readonly List<Transform> hubs = new List<Transform>();
    float ang;
    static Mesh blurMesh; static Material blurMat, bladeMat;

    /// cut the props out of a freshly spawned plane model and set them turning
    public static PlaneProps Attach(GameObject model)
    {
        if (model == null || model.name.EndsWith("(stand-in)")) return null;     // a box has no props
        var pp = model.GetComponent<PlaneProps>();
        if (pp != null) return pp;
        pp = model.AddComponent<PlaneProps>();
        MeshFilter host = null;
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
            if (host == null) host = mf;
            if (pp.Cut(mf)) return pp;
        }
        if (host != null) pp.StandIn(host.transform);
        return pp;
    }

    void Update()
    {
        if (hubs.Count == 0) return;
        ang = Mathf.Repeat(ang + rate * Mathf.Min(0.05f, Time.deltaTime), Mathf.PI * 2f);
        var q = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, Vector3.right);   // the mesh's x is the fuselage's axis
        foreach (var h in hubs) if (h != null) h.localRotation = q;
    }

    // ---- the cut ----
    bool Cut(MeshFilter mf)
    {
        var src = mf.sharedMesh;
        if (!src.isReadable) return false;
        Vector3[] v;
        try { v = src.vertices; } catch { return false; }
        if (v == null || v.Length == 0) return false;
        // Which end is the nose: the fin is the tallest thing on the airframe, and it is at the
        // tail. Found rather than assumed, because the importer mirrors the file's x.
        int top = 0;
        for (int i = 1; i < v.Length; i++) if (v[i].y > v[top].y) top = i;
        float nose = v[top].x > 0f ? -1f : 1f;

        int subs = src.subMeshCount;
        var keep = new List<int>[subs];
        var cut = new List<int>[2, subs];
        int[] count = new int[2];
        for (int s = 0; s < subs; s++)
        {
            keep[s] = new List<int>();
            cut[0, s] = new List<int>(); cut[1, s] = new List<int>();
            var tri = src.GetTriangles(s);
            for (int t = 0; t + 2 < tri.Length; t += 3)
            {
                int a = tri[t], b = tri[t + 1], c = tri[t + 2];
                int side = Blade(v[a], v[b], v[c], nose);
                if (side < 0) { keep[s].Add(a); keep[s].Add(b); keep[s].Add(c); }
                else { cut[side, s].Add(a); cut[side, s].Add(b); cut[side, s].Add(c); count[side]++; }
            }
        }
        if (count[0] < MinTris || count[1] < MinTris) return false;

        // the airframe without its blades
        var body = Object.Instantiate(src);
        body.name = src.name + " (props cut)";
        for (int s = 0; s < subs; s++) body.SetTriangles(keep[s], s);
        mf.sharedMesh = body;

        var mr = mf.GetComponent<MeshRenderer>();
        var n = src.normals; var uv = src.uv; var tg = src.tangents; var col = src.colors;
        for (int side = 0; side < 2; side++)
        {
            var hub = Hub(side, nose);
            var piv = new GameObject("prop").transform;
            piv.SetParent(mf.transform, false); piv.localPosition = hub;
            // the blades re-centred on the hub, so turning the pivot turns them about it
            var map = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var ntg = new List<Vector4>(); var ncol = new List<Color>();
            var ntri = new List<int>[subs];
            for (int s = 0; s < subs; s++)
            {
                ntri[s] = new List<int>();
                foreach (int i in cut[side, s])
                {
                    if (!map.TryGetValue(i, out int j))
                    {
                        j = nv.Count; map[i] = j;
                        nv.Add(v[i] - hub);
                        if (n.Length == v.Length) nn.Add(n[i]);
                        if (uv.Length == v.Length) nuv.Add(uv[i]);
                        if (tg.Length == v.Length) ntg.Add(tg[i]);
                        if (col.Length == v.Length) ncol.Add(col[i]);
                    }
                    ntri[s].Add(j);
                }
            }
            var bm = new Mesh { name = "propeller" };
            bm.SetVertices(nv);
            if (nn.Count == nv.Count) bm.SetNormals(nn);
            if (nuv.Count == nv.Count) bm.SetUVs(0, nuv);
            if (ntg.Count == nv.Count) bm.SetTangents(ntg);
            if (ncol.Count == nv.Count) bm.SetColors(ncol);
            bm.subMeshCount = subs;
            for (int s = 0; s < subs; s++) bm.SetTriangles(ntri[s], s);
            if (nn.Count != nv.Count) bm.RecalculateNormals();
            bm.RecalculateBounds();
            var go = new GameObject("blades", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(piv, false);
            go.GetComponent<MeshFilter>().sharedMesh = bm;
            var br = go.GetComponent<MeshRenderer>();
            br.sharedMaterials = mr.sharedMaterials;
            br.shadowCastingMode = mr.shadowCastingMode; br.receiveShadows = mr.receiveShadows;
            Blur(piv, nose);
            hubs.Add(piv);
        }
        return true;
    }

    // which prop's blades a triangle belongs to (0 one side, 1 the other), or -1 for the airframe
    static int Blade(Vector3 a, Vector3 b, Vector3 c, float nose)
    {
        for (int side = 0; side < 2; side++)
        {
            var hub = Hub(side, nose);
            if (!InDisc(a, hub, nose) || !InDisc(b, hub, nose) || !InDisc(c, hub, nose)) continue;
            var m = (a + b + c) / 3f;
            if (Radial(m, hub) > BladeIn) return side;
        }
        return -1;
    }

    static bool InDisc(Vector3 p, Vector3 hub, float nose)
    {
        float d = p.x * nose;
        return d > SlabNear && d < SlabFar && Radial(p, hub) < BladeOut;
    }

    static float Radial(Vector3 p, Vector3 hub) { float dy = p.y - hub.y, dz = p.z - hub.z; return Mathf.Sqrt(dy * dy + dz * dz); }

    static Vector3 Hub(int side, float nose) => new Vector3(nose * HubX, HubY, side == 0 ? HubZ : -HubZ);

    // ---- the blur: a faint disc just ahead of the blades ----
    static void Blur(Transform piv, float nose)
    {
        if (blurMesh == null)
        {
            const int N = 26;
            var v = new Vector3[N + 1]; var idx = new int[N * 3];
            for (int i = 0; i < N; i++) { float a = i / (float)N * Mathf.PI * 2f; v[i + 1] = new Vector3(0f, Mathf.Cos(a) * BlurR, Mathf.Sin(a) * BlurR); }
            for (int i = 0; i < N; i++) { idx[i * 3] = 0; idx[i * 3 + 1] = 1 + i; idx[i * 3 + 2] = 1 + (i + 1) % N; }
            blurMesh = new Mesh { name = "propBlur", vertices = v, triangles = idx };
            blurMesh.RecalculateNormals(); blurMesh.RecalculateBounds();
        }
        if (blurMat == null) blurMat = Dive.Unlit(new Color(0xd8 / 255f, 0xde / 255f, 0xe4 / 255f, 0.2f), true, false, true);
        var go = new GameObject("blur", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(piv, false);
        go.transform.localPosition = new Vector3(nose * 0.006f, 0f, 0f);
        go.GetComponent<MeshFilter>().sharedMesh = blurMesh;
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = blurMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // ---- an unreadable mesh: two plain blades over each hub ----
    // The importer mirrors the file's x, which puts the nose at +x in the mesh.
    void StandIn(Transform host)
    {
        if (bladeMat == null)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            bladeMat = new Material(lit != null ? lit : Shader.Find("Standard"));
            bladeMat.SetColor("_BaseColor", new Color(0x2b / 255f, 0x2f / 255f, 0x35 / 255f));
            if (bladeMat.HasProperty("_Smoothness")) bladeMat.SetFloat("_Smoothness", 0.4f);
        }
        for (int side = 0; side < 2; side++)
        {
            var piv = new GameObject("prop").transform;
            piv.SetParent(host, false); piv.localPosition = Hub(side, 1f);
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var bc = b.GetComponent<Collider>(); if (bc != null) Destroy(bc);
            b.name = "blades"; b.transform.SetParent(piv, false);
            b.transform.localScale = new Vector3(0.008f, BladeOut * 1.85f, 0.022f);
            var r = b.GetComponent<MeshRenderer>(); r.sharedMaterial = bladeMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Blur(piv, 1f);
            hubs.Add(piv);
        }
    }
}
