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
        Mark((Random.value - 0.5f) * 0.6f, Mathf.Clamp((Random.value - 0.5f) * 3.2f, -2.1f, 2.1f));
        if (Cracks >= Lives) { r.Wipeout("snap"); r.Say("SNAPPED!"); return; }
        r.Say("CRACKED! " + (Lives - Cracks) + " LEFT");
    }

    public static void ClearMarks() { foreach (var m in marks) if (m != null) Object.Destroy(m); marks.Clear(); }

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
