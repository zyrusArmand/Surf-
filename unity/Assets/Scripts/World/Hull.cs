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

    /// the web's crack: four jagged strands walking out from the hit, thinning as they go, laid
    /// on the deck's top (0x14100d at 0.9, drawn over the paint)
    static void Mark(float lx, float lz)
    {
        if (Board == null) return;
        var rend = Board.GetComponentInChildren<MeshRenderer>(); if (rend == null) return;
        var bb = rend.bounds; var local = Board.transform;
        float topY = local.InverseTransformPoint(new Vector3(bb.center.x, bb.max.y, bb.center.z)).y + 0.012f;
        float halfW = Mathf.Max(0.3f, local.InverseTransformVector(new Vector3(bb.extents.x, 0, 0)).magnitude);
        float halfL = Mathf.Max(1.5f, local.InverseTransformVector(new Vector3(0, 0, bb.extents.z)).magnitude);
        var v = new System.Collections.Generic.List<Vector3>(); var idx = new System.Collections.Generic.List<int>();
        for (int sIdx = 0; sIdx < 4; sIdx++)
        {
            float a = Random.value * Mathf.PI * 2f, x = lx, z = lz;
            int segs = 5 + Random.Range(0, 3); float step = 0.28f + Random.value * 0.22f;
            Vector3? pa = null, pb = null;
            for (int i = 0; i <= segs; i++)
            {
                // off the deck's own plan: a strand stops at the rail
                if (Mathf.Abs(x) > halfW * (1f - 0.35f * Mathf.Abs(z) / halfL) || Mathf.Abs(z) > halfL) break;
                float t = i / (float)segs, w = 0.070f * (1 - t) + 0.008f;
                float nx = Mathf.Cos(a + Mathf.PI / 2f) * w, nz = Mathf.Sin(a + Mathf.PI / 2f) * w;
                var A = new Vector3(x - nx, topY, z - nz); var B = new Vector3(x + nx, topY, z + nz);
                if (pa.HasValue)
                {
                    int b0 = v.Count; v.Add(pa.Value); v.Add(pb.Value); v.Add(A); v.Add(B);
                    idx.Add(b0); idx.Add(b0 + 2); idx.Add(b0 + 1); idx.Add(b0 + 1); idx.Add(b0 + 2); idx.Add(b0 + 3);
                    idx.Add(b0); idx.Add(b0 + 1); idx.Add(b0 + 2); idx.Add(b0 + 1); idx.Add(b0 + 3); idx.Add(b0 + 2);
                }
                pa = A; pb = B;
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
        }
        var go = new GameObject("crack", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(local, false);
        go.GetComponent<MeshFilter>().sharedMesh = m;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = markMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        marks.Add(go);
    }
}
