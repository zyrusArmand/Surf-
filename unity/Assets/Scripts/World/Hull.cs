// The board's hull takes damage before it breaks, as the web's does: dropping onto the top
// of something cracks it and kicks you back up, and only the last crack snaps it. A board's
// lives come off its toughness (two to four), a rider's perk can add one. Driving into the
// side of a thing is still the end of the run. Kept here, off the controller, so the spawner
// can call it and the run card can read it.
using UnityEngine;

public static class Hull
{
    public static int Cracks, Lives = 2;
    public static GameObject Board;            // the board in use, for the marks (Bootstrap sets it)
    static readonly System.Collections.Generic.List<GameObject> marks = new System.Collections.Generic.List<GameObject>();
    static Material markMat;

    public static void Reset()
    {
        ClearMarks();
        Cracks = 0;
        Lives = RideStats.For(string.IsNullOrEmpty(SaveData.S.wheelLoan) ? SaveData.S.equipped : SaveData.S.wheelLoan,
                              string.IsNullOrEmpty(SaveData.S.wheelRider) ? SaveData.S.rider : SaveData.S.wheelRider).lives;
    }

    /// came down on top of it: a crack, the momentum gone, and a kick up off the thing
    public static void Hit(BoardController r, float topY)
    {
        if (r.wipe || !r.running) return;
        Cracks++;
        r.boost = 0f;
        r.vy = Mathf.Max(6.4f, -r.vy * 0.35f + 5.2f);
        r.airborne = true;
        r.py = Mathf.Max(r.py, topY + 0.28f);
        r.poseLand = 0.8f;
        Sfx.Land(1.4f); Sfx.Buzz(40);
        float lz = Mathf.Clamp((Random.value - 0.5f) * 3.2f, -2.1f, 2.1f);
        Mark((Random.value - 0.5f) * 0.6f, lz);
        // it breaks where the last one landed, pulled off the very ends so neither half is a sliver (web 27828-27830)
        SnapZ = Mathf.Clamp(lz, -1.5f, 1.5f);
        if (Cracks >= Lives) { r.Wipeout("snap"); return; }          // "BOARD SNAPPED!", the web's own line for it
        r.Say("CRACKED! " + (Lives - Cracks) + " LEFT");
    }

    public static void ClearMarks() { foreach (var m in marks) if (m != null) Object.Destroy(m); marks.Clear(); Unsnap(); SnapZ = 0f; }

    // ---- the snap (web 27731-27811, 28417-28434) ----
    // Split the board in two across the middle. Every triangle is moved into the board's own frame
    // first, so a fin hung under the tail with its own transform is cut by the same plane the hull
    // is, and goes to the half its centre lies on. A piece that does not straddle the cut -- the
    // fins, the pads, a decal -- moves across whole and exactly as it was. The originals are only
    // hidden, and only those, so a variant's own switched-off parts stay off when it is put back.
    public static float SnapZ;                                     // where it breaks, along the board (its own z)
    static Transform[] halves;
    static readonly Vector3[] halfRot = new Vector3[2];
    static readonly System.Collections.Generic.List<Renderer> snapHidden = new System.Collections.Generic.List<Renderer>();
    static readonly System.Collections.Generic.List<Mesh> snapMeshes = new System.Collections.Generic.List<Mesh>();

    public static void Snap()
    {
        if (Board == null || halves != null) return;
        var root = Board.transform;
        var parts = Board.GetComponentsInChildren<MeshRenderer>(false);    // before the halves exist
        halves = new Transform[2];
        for (int k = 0; k < 2; k++)
        {
            halves[k] = new GameObject(k == 0 ? "half nose" : "half tail").transform;
            halves[k].SetParent(root, false);
            halfRot[k] = Vector3.zero;
        }
        var toRoot = root.worldToLocalMatrix;
        foreach (var mr in parts)
        {
            if (mr == null || !mr.enabled) continue;
            if (marks.Contains(mr.gameObject)) { mr.enabled = false; snapHidden.Add(mr); continue; }   // the cracks are not cut; they go
            var mf = mr.GetComponent<MeshFilter>(); var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) continue;
            var M = toRoot * mr.transform.localToWorldMatrix;
            var b = mesh.bounds; float zMin = float.PositiveInfinity, zMax = float.NegativeInfinity;
            for (int c = 0; c < 8; c++)
            {
                float z = M.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z)).z;
                zMin = Mathf.Min(zMin, z); zMax = Mathf.Max(zMax, z);
            }
            bool whole = zMin >= SnapZ || zMax <= SnapZ || !mesh.isReadable;      // a mesh that cannot be read goes whole, by its middle
            if (whole)
            {
                int k = (zMin >= SnapZ || (zMax > SnapZ && (zMin + zMax) * 0.5f >= SnapZ)) ? 1 : 0;
                var go = new GameObject(mr.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(halves[k], false);
                go.transform.localPosition = M.GetColumn(3); go.transform.localRotation = M.rotation; go.transform.localScale = M.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                CopyLook(mr, go.GetComponent<MeshRenderer>());
            }
            else Cut(mesh, M, mr);
            mr.enabled = false; snapHidden.Add(mr);
        }
    }

    /// every attribute the mesh carries goes with the triangle, not just its position: a half
    /// that loses its colours or its texture coordinates goes black (web 27774-27776)
    static void Cut(Mesh mesh, Matrix4x4 M, MeshRenderer src)
    {
        var v = mesh.vertices; var n = mesh.normals; var c = mesh.colors; var u0 = mesh.uv; var u1 = mesh.uv2; var tg = mesh.tangents;
        bool hasN = n.Length == v.Length, hasC = c.Length == v.Length, hasU0 = u0.Length == v.Length, hasU1 = u1.Length == v.Length, hasT = tg.Length == v.Length;
        var NM = M.inverse.transpose;
        bool flip = M.determinant < 0f;                                    // a mirrored transform turns the winding over
        int subs = mesh.subMeshCount;
        for (int k = 0; k < 2; k++)
        {
            var remap = new int[v.Length]; for (int i = 0; i < remap.Length; i++) remap[i] = -1;
            var pv = new System.Collections.Generic.List<Vector3>(); var pn = new System.Collections.Generic.List<Vector3>(); var pc = new System.Collections.Generic.List<Color>();
            var p0 = new System.Collections.Generic.List<Vector2>(); var p1 = new System.Collections.Generic.List<Vector2>(); var pt = new System.Collections.Generic.List<Vector4>();
            var tris = new System.Collections.Generic.List<int>[subs]; int count = 0;
            for (int s = 0; s < subs; s++)
            {
                tris[s] = new System.Collections.Generic.List<int>();
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                var idx = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    float cz = (M.MultiplyPoint3x4(v[idx[i]]).z + M.MultiplyPoint3x4(v[idx[i + 1]]).z + M.MultiplyPoint3x4(v[idx[i + 2]]).z) / 3f;
                    if ((cz < SnapZ ? 0 : 1) != k) continue;
                    for (int j = 0; j < 3; j++)
                    {
                        int o = idx[i + (flip && j > 0 ? 3 - j : j)];
                        if (remap[o] < 0)
                        {
                            remap[o] = pv.Count;
                            pv.Add(M.MultiplyPoint3x4(v[o]));
                            if (hasN) pn.Add(NM.MultiplyVector(n[o]).normalized);
                            if (hasC) pc.Add(c[o]);
                            if (hasU0) p0.Add(u0[o]);
                            if (hasU1) p1.Add(u1[o]);
                            if (hasT) { var d = M.MultiplyVector(new Vector3(tg[o].x, tg[o].y, tg[o].z)).normalized; pt.Add(new Vector4(d.x, d.y, d.z, tg[o].w)); }
                        }
                        tris[s].Add(remap[o]); count++;
                    }
                }
            }
            if (count == 0) continue;
            var m = new Mesh { name = mesh.name + (k == 0 ? " nose" : " tail") };
            if (pv.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(pv);
            if (hasC) m.SetColors(pc);
            if (hasU0) m.SetUVs(0, p0);
            if (hasU1) m.SetUVs(1, p1);
            m.subMeshCount = subs;
            for (int s = 0; s < subs; s++) m.SetTriangles(tris[s], s);
            if (hasN) m.SetNormals(pn); else m.RecalculateNormals();
            if (hasT) m.SetTangents(pt);
            m.RecalculateBounds();
            snapMeshes.Add(m);
            var go = new GameObject(src.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(halves[k], false);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            CopyLook(src, go.GetComponent<MeshRenderer>());
        }
    }

    static void CopyLook(MeshRenderer from, MeshRenderer to)
    {
        to.sharedMaterials = from.sharedMaterials;
        to.shadowCastingMode = from.shadowCastingMode; to.receiveShadows = from.receiveShadows;
    }

    /// the halves part while the board is in the air, and once it is in the water they stop
    /// turning and lie down: a half standing on its end in open water is the one thing the sea
    /// would never allow. t is the crash's clock, held from the moment the board landed.
    public static void SnapStep(float t, bool landed, float dt)
    {
        if (halves == null) return;
        for (int i = 0; i < 2; i++)
        {
            var h = halves[i]; if (h == null) continue;
            float sgn = i == 1 ? 1f : -1f;
            h.localPosition = new Vector3(sgn * 0.10f * t, 0f, sgn * 0.85f * t);
            var e = halfRot[i];
            if (landed)
            {
                e.x += (0f - e.x) * Mathf.Min(1f, dt * 2.6f);
                e.z += (0f - e.z) * Mathf.Min(1f, dt * 2.6f);
                e.y += (sgn * 0.35f - e.y) * Mathf.Min(1f, dt * 1.6f);
            }
            else e = new Vector3(sgn * -0.7f * t, 0f, sgn * 1.05f * t);
            halfRot[i] = e;
            h.localRotation = BoardController.Xyz(e.x, e.y, e.z);
        }
    }

    /// the board back in one piece (web clearCracks): the halves gone, and only what Snap hid shown again
    static void Unsnap()
    {
        if (halves != null) foreach (var h in halves) if (h != null) Object.Destroy(h.gameObject);
        halves = null;
        foreach (var r in snapHidden) if (r != null) r.enabled = true;
        snapHidden.Clear();
        foreach (var m in snapMeshes) if (m != null) Object.Destroy(m);
        snapMeshes.Clear();
    }

    /// the web's crack (27685): four jagged strands walking out from the hit, thinning as they go,
    /// laid ON the deck and on the hull under it (a crack goes through a board; on the deck alone it
    /// hides under the rider from where the camera sits). Each point is dropped onto the shell by a
    /// ray straight down through the board, as the web does it, so a strand follows the rocker.
    /// It used to be laid flat at the top of the board's bounding box -- the nose's rocker tip --
    /// which over the middle of the deck left it hanging in the air around his feet, where it read
    /// as marks drawn on him rather than cut into the board.
    static void Mark(float lx, float lz)
    {
        if (Board == null) return;
        var local = Board.transform;
        MeshFilter shell = null;
        foreach (var f in Board.GetComponentsInChildren<MeshFilter>())
            if (f.sharedMesh != null && f.name == "deck") { shell = f; break; }
        if (shell == null) return;
        var mesh = shell.sharedMesh; var mv = mesh.vertices; var mt = mesh.triangles;
        // into the board's frame (the shell normally sits at its identity, but do not assume)
        var toB = local.worldToLocalMatrix * shell.transform.localToWorldMatrix;
        var bv = new Vector3[mv.Length]; for (int i = 0; i < mv.Length; i++) bv[i] = toB.MultiplyPoint3x4(mv[i]);
        var v = new System.Collections.Generic.List<Vector3>(); var idx = new System.Collections.Generic.List<int>();
        if (!DeckAt(bv, mt, lx, lz, out _, out _)) return;            // the hit missed the hull
        for (int sIdx = 0; sIdx < 4; sIdx++)
        {
            float a = Random.value * Mathf.PI * 2f, x = lx, z = lz;
            int segs = 5 + Random.Range(0, 3); float step = 0.28f + Random.value * 0.22f;
            var prevA = new Vector3?[2]; var prevB = new Vector3?[2];
            for (int i = 0; i <= segs; i++)
            {
                if (!DeckAt(bv, mt, x, z, out var top, out var bot)) break;   // off the rail: the strand stops
                float t = i / (float)segs, w = 0.070f * (1 - t) + 0.008f;
                float nx = Mathf.Cos(a + Mathf.PI / 2f) * w, nz = Mathf.Sin(a + Mathf.PI / 2f) * w;
                for (int f = 0; f < 2; f++)
                {
                    var P = f == 0 ? top : bot;
                    var A = new Vector3(P.x - nx, P.y, P.z - nz); var B = new Vector3(P.x + nx, P.y, P.z + nz);
                    if (prevA[f].HasValue)
                    {
                        int b0 = v.Count; v.Add(prevA[f].Value); v.Add(prevB[f].Value); v.Add(A); v.Add(B);
                        idx.Add(b0); idx.Add(b0 + 2); idx.Add(b0 + 1); idx.Add(b0 + 1); idx.Add(b0 + 2); idx.Add(b0 + 3);
                        idx.Add(b0); idx.Add(b0 + 1); idx.Add(b0 + 2); idx.Add(b0 + 1); idx.Add(b0 + 3); idx.Add(b0 + 2);
                    }
                    prevA[f] = A; prevB[f] = B;
                }
                a += (Random.value - 0.5f) * 1.2f;
                x += Mathf.Cos(a) * step; z += Mathf.Sin(a) * step;
            }
        }
        if (v.Count < 4) return;
        var m = new Mesh { name = "crack" }; m.SetVertices(v); m.SetTriangles(idx, 0); m.RecalculateBounds();
        if (markMat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");     // never throw here: the crack is drawn before the snap is decided
            markMat = new Material(sh); markMat.SetColor("_BaseColor", new Color(0.078f, 0.063f, 0.051f, 0.9f));
            markMat.SetFloat("_Surface", 1); markMat.SetFloat("_Blend", 0); markMat.renderQueue = 3001;
            markMat.SetInt("_ZWrite", 0); markMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            markMat.SetFloat("_Cull", 0f);
        }
        var go = new GameObject("crack", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(local, false);
        go.GetComponent<MeshFilter>().sharedMesh = m;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = markMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        marks.Add(go);
    }

    /// where a vertical line through (x, z) in the board's frame meets the shell: the highest hit is
    /// the deck, the lowest the hull. Each is lifted 0.012 ft off its face, along the face's normal,
    /// so it stays proud of a curved surface without hovering over it.
    static bool DeckAt(Vector3[] bv, int[] tri, float x, float z, out Vector3 top, out Vector3 bot)
    {
        top = bot = Vector3.zero; float yTop = float.NegativeInfinity, yBot = float.PositiveInfinity;
        Vector3 nTop = Vector3.up, nBot = Vector3.down;
        for (int i = 0; i + 2 < tri.Length; i += 3)
        {
            Vector3 p0 = bv[tri[i]], p1 = bv[tri[i + 1]], p2 = bv[tri[i + 2]];
            // barycentrics of (x, z) in the triangle's plan
            float d = (p1.z - p2.z) * (p0.x - p2.x) + (p2.x - p1.x) * (p0.z - p2.z);
            if (Mathf.Abs(d) < 1e-9f) continue;
            float w0 = ((p1.z - p2.z) * (x - p2.x) + (p2.x - p1.x) * (z - p2.z)) / d;
            float w1 = ((p2.z - p0.z) * (x - p2.x) + (p0.x - p2.x) * (z - p2.z)) / d;
            float w2 = 1f - w0 - w1;
            if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
            float y = w0 * p0.y + w1 * p1.y + w2 * p2.y;
            var n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            if (y > yTop) { yTop = y; nTop = n.y >= 0f ? n : -n; }
            if (y < yBot) { yBot = y; nBot = n.y <= 0f ? n : -n; }
        }
        if (float.IsNegativeInfinity(yTop)) return false;
        top = new Vector3(x, yTop, z) + nTop * 0.012f;
        bot = new Vector3(x, yBot, z) + nBot * 0.012f;
        return true;
    }
}
