// What the rider wears, built the way the web game's kitGear builds it. Three pieces are files
// (shades.glb, cowboyhat.glb, bandana.glb under Resources/Models) fitted to the skull that is
// actually wearing them, measured off the vertices the Head bone drives, exactly as the web
// measures them; the other hats are lofted from primitives in the procedural skull's own units
// and hung off a wrapper on the Head bone (the web's gearHeadParent); and a suit is CUT from the
// rider's own skinned triangles, pushed a hair out along their normals, hemmed, collared and
// buttoned, then bound to the same bones (the web's cutGarment) -- or, on the pug, the authored
// garments from garments.glb re-skinned onto his bones (wearAuthored). Every print is painted
// to a texture from the catalogue's description (garmentTex), so an outfit is numbers, not art.
//
// Dress(riderRoot, save) takes off whatever is on and puts on what the save says is worn, in the
// four slots. A rider without the bones it needs is left as he is.
using System.Collections.Generic;
using UnityEngine;

public static class Gear
{
    // ---- the web's sweepable fit numbers ----
    const float HatW = 0.92f, HatUp = 0.42f, HatFwd = 0.80f;                               // the modelled hat
    const float ShdW = 0.92f, ShdSep = 1.35f, ShdOut = 0.95f, ShdUp = 0.0f, ShdTilt = 0f;  // the shades
    const float BndR = 0.78f, BndY = -0.10f, BndTilt = 48f;                                // the bandana
    const float SkullD = 0.80f;        // the procedural skull is a 0.40 sphere: the unit the lofted hats were drawn in
    // glTF is right-handed and Unity is not: the importer mirrors every file across x, so a model
    // whose front is +X in the file has it at -X here. Flip this if a modelled piece faces backward.
    const bool ImportMirrorX = true;
    // the garment cutter's numbers (web GARM)
    const float GLift = 0.020f, GShirtLift = 0.024f, GHang = 0.6f, GWaist = 0.10f, GTuck = 0.08f, GCollar = 1.0f,
                GShortHem = 0.42f, GLongHem = 0.06f, GRimT = 0.012f, GHemW = 0.022f, GCollarW = 0.045f, GWaistW = 0.035f, GCuffW = 0.02f, GBtnR = 0.013f;

    // =====================================================================================
    /// takes off what is on and puts on what the save says is worn
    public static void Dress(Transform riderRoot, Save s)
    {
        if (riderRoot == null || s == null) return;
        Undress(riderRoot);
        var E = GearCatalog.Worn(s, "eyes"); var H = GearCatalog.Worn(s, "head");
        var B = GearCatalog.Worn(s, "neck"); var S = GearCatalog.Worn(s, "body");
        if (E == null && H == null && B == null && S == null) return;
        var bones = new Dictionary<string, Transform>();
        foreach (var t in riderRoot.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
        Transform head; bones.TryGetValue("Head", out head);
        if (head != null && (E != null || H != null || B != null))
        {
            Frame F = null;
            try { F = Measure(riderRoot, bones, head); } catch (System.Exception ex) { Debug.LogWarning("Gear: could not measure the head: " + ex.Message); }
            if (F != null)
            {
                if (E != null) try { Shades(F, E); } catch (System.Exception ex) { Debug.LogWarning("Gear: shades: " + ex.Message); }
                if (B != null) try { Bandana(F, B); } catch (System.Exception ex) { Debug.LogWarning("Gear: bandana: " + ex.Message); }
                if (H != null) try { Hat(F, H); } catch (System.Exception ex) { Debug.LogWarning("Gear: hat: " + ex.Message); }
            }
        }
        if (S != null) try { Suit(riderRoot, S, s.rider); } catch (System.Exception ex) { Debug.LogWarning("Gear: suit: " + ex.Message); }
    }

    /// removes every piece Dress put on
    public static void Undress(Transform riderRoot)
    {
        if (riderRoot == null) return;
        var gone = new List<GameObject>();
        foreach (var t in riderRoot.GetComponentsInChildren<Transform>(true)) if (t != riderRoot && t.name.StartsWith("gear:")) gone.Add(t.gameObject);
        foreach (var g in gone) if (g != null) { g.SetActive(false); Object.Destroy(g); }
    }

    // =====================================================================================
    // ---- the head that is wearing it (web headBoxLocal + riderEyeFit + gearHeadParent) ----
    class Frame
    {
        public Transform H;                      // the Head bone
        public Vector3 mid, right, up, fwd;      // the eye line, in the bone's own space
        public float sep, r;                     // eye spacing and radius
        public float lo, hi, bot, top, bk, fr;   // the skull's reach from mid along right, up and fwd
        public Transform J;                      // the headJ wrapper: +Y up, -Z out of the face, the procedural skull's units
        public float unit;
    }

    static int IndexOf(Transform[] bl, string name) { for (int i = 0; i < bl.Length; i++) if (bl[i] != null && bl[i].name == name) return i; return -1; }

    static Frame Measure(Transform root, Dictionary<string, Transform> bones, Transform head)
    {
        var F = new Frame { H = head };
        var box = new Bounds(); bool any = false; int n = 0;
        Vector3 hipP = Vector3.zero, frontP = Vector3.zero; bool haveHip = false, haveFront = false, haveDir = false;
        Vector3[] edir = null; float headR = 0f; var far = new[] { float.NegativeInfinity, float.NegativeInfinity };
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = smr.sharedMesh; var bl = smr.bones;
            if (mesh == null || bl == null) continue;
            int bi = System.Array.IndexOf(bl, head); if (bi < 0) continue;
            Vector3[] verts; BoneWeight[] bw; Matrix4x4[] bind;
            try { if (!mesh.isReadable) continue; verts = mesh.vertices; bw = mesh.boneWeights; bind = mesh.bindposes; } catch { continue; }
            if (verts == null || bw == null || bw.Length != verts.Length || bind == null || bind.Length != bl.Length) continue;
            // a vertex the head drives sits at bindpose * v in the bone's own frame: no pose in it
            var toHL = bind[bi];
            if (!haveHip) { int hi_ = IndexOf(bl, "Hips"); if (hi_ >= 0) { hipP = toHL.MultiplyPoint3x4(bind[hi_].inverse.MultiplyPoint3x4(Vector3.zero)); haveHip = true; } }
            if (!haveFront) { int fi = IndexOf(bl, "headfront"); if (fi < 0) fi = IndexOf(bl, "head_end"); if (fi >= 0) { frontP = toHL.MultiplyPoint3x4(bind[fi].inverse.MultiplyPoint3x4(Vector3.zero)); haveFront = true; } }
            if (!haveDir && haveHip && haveFront) haveDir = Directions(F, hipP, frontP, out headR, out edir);
            for (int i = 0; i < verts.Length; i++)
            {
                var w = bw[i]; float ww = 0f;
                if (w.boneIndex0 == bi) ww += w.weight0; if (w.boneIndex1 == bi) ww += w.weight1;
                if (w.boneIndex2 == bi) ww += w.weight2; if (w.boneIndex3 == bi) ww += w.weight3;
                if (ww < 0.6f) continue;                              // dominated by the head, not the neck
                var v = toHL.MultiplyPoint3x4(verts[i]);
                if (!any) { box = new Bounds(v, Vector3.zero); any = true; } else box.Encapsulate(v);
                n++;
                if (edir == null) continue;
                float len = v.magnitude; if (!(len > 1e-9f) || len > headR * 1.30f) continue;
                for (int e = 0; e < 2; e++) { float t = Vector3.Dot(v, edir[e]); if (t > far[e] && t / len > 0.80f) far[e] = t; }
            }
        }
        if (!haveDir)
        {
            // no readable skin: the same frame off the live bones, in the head's own space
            Transform hips, front;
            bones.TryGetValue("Hips", out hips);
            if (!bones.TryGetValue("headfront", out front)) bones.TryGetValue("head_end", out front);
            var hp = hips != null ? head.InverseTransformPoint(hips.position) : -Vector3.up;
            var fp = front != null ? head.InverseTransformPoint(front.position) : head.InverseTransformDirection(root.forward) * hp.magnitude * 0.3f;
            haveDir = Directions(F, hp, fp, out headR, out edir);
            if (!haveDir) { F.up = Vector3.up; F.fwd = Vector3.forward; F.right = Vector3.Cross(F.up, F.fwd); headR = Mathf.Max(1e-3f, hp.magnitude * 0.25f); }
        }
        if (n <= 40)
        {
            // the mesh could not be read: a skull about the muzzle's reach, centred on the bone
            float hr = Mathf.Max(1e-3f, headR * 0.6f);
            box = new Bounds(F.up * hr * 0.3f, Vector3.one * hr * 2f);
        }
        if (edir != null && !float.IsInfinity(far[0]) && !float.IsInfinity(far[1]))
        {
            F.r = 0.19f * headR;
            var a0 = edir[0] * (far[0] - F.r * 0.86f); var a1 = edir[1] * (far[1] - F.r * 0.86f);
            F.mid = (a0 + a1) * 0.5f; F.sep = Vector3.Distance(a0, a1);
        }
        else
        {
            // no face to read: the eyes sit high on the front of the box
            Extents(F, box, box.center);
            F.r = 0.19f * Mathf.Max(headR, 1e-3f);
            F.mid = box.center + F.fwd * (F.fr * 0.6f) + F.up * (F.top * 0.3f);
            F.sep = (F.hi - F.lo) * 0.5f;
        }
        Extents(F, box, F.mid);
        if (!(F.sep > 1e-6f)) F.sep = Mathf.Max(1e-4f, (F.hi - F.lo) * 0.5f);
        // the wrapper the lofted hats are built in: the procedural skull's units, sat so its top is the skull's top
        float width = Mathf.Max(1e-4f, F.hi - F.lo);
        F.unit = width / SkullD;
        var J = new GameObject("gear:J").transform; J.SetParent(head, false);
        J.localRotation = Quaternion.LookRotation(-F.fwd, F.up);
        J.localPosition = F.mid + F.right * ((F.lo + F.hi) * 0.5f) + F.up * (F.top - 0.40f * F.unit) + F.fwd * ((F.bk + F.fr) * 0.5f);
        J.localScale = Vector3.one * F.unit;
        F.J = J;
        return F;
    }

    // up is hips-to-head; forward is written down by the headfront bone; the eyes look mostly
    // forward, high on the front of the skull, close either side of the bridge
    static bool Directions(Frame F, Vector3 hipP, Vector3 frontP, out float headR, out Vector3[] edir)
    {
        headR = 0f; edir = null;
        if (hipP.sqrMagnitude <= 1e-12f || frontP.sqrMagnitude <= 1e-12f) return false;
        var up = (-hipP).normalized;
        var fwd = frontP - up * Vector3.Dot(frontP, up);
        if (fwd.sqrMagnitude <= 1e-12f) return false;
        fwd.Normalize();
        var right = Vector3.Cross(up, fwd).normalized;
        F.up = up; F.fwd = fwd; F.right = right;
        headR = frontP.magnitude * 1.42f;
        edir = new[] { (fwd * 0.80f + up * 0.60f + right * 0.30f).normalized, (fwd * 0.80f + up * 0.60f - right * 0.30f).normalized };
        return true;
    }

    static void Extents(Frame F, Bounds box, Vector3 origin)
    {
        F.lo = F.bot = F.bk = float.PositiveInfinity; F.hi = F.top = F.fr = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var p = new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z) - origin;
            float t = Vector3.Dot(p, F.right), u = Vector3.Dot(p, F.up), f = Vector3.Dot(p, F.fwd);
            if (t < F.lo) F.lo = t; if (t > F.hi) F.hi = t; if (u < F.bot) F.bot = u; if (u > F.top) F.top = u; if (f < F.bk) F.bk = f; if (f > F.fr) F.fr = f;
        }
    }

    // =====================================================================================
    // ---- materials, the web's PBR() in URP Lit ----
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Shader lit;
    static Shader Lit() { if (lit == null) { lit = Shader.Find("Universal Render Pipeline/Lit"); if (lit == null) lit = Shader.Find("Standard"); } return lit; }
    static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);
    /// the web's vivid(): push each channel away from the average, then to linear
    static Color Vivid(int hex, float boost = 1f)
    {
        var c = Hex(hex); float avg = (c.r + c.g + c.b) / 3f, k = 0.30f * boost;
        c.r = Mathf.Clamp01(avg + (c.r - avg) * (1 + k)); c.g = Mathf.Clamp01(avg + (c.g - avg) * (1 + k)); c.b = Mathf.Clamp01(avg + (c.b - avg) * (1 + k));
        return c.linear;
    }
    static Material Mat(int hex, float rough, float metal, float vivid, bool twoSided = false)
    {
        string key = hex + ":" + rough + ":" + metal + ":" + vivid + ":" + twoSided;
        Material m;
        if (mats.TryGetValue(key, out m) && m != null) return m;
        m = new Material(Lit()); m.SetColor("_BaseColor", Vivid(hex, vivid)); m.SetFloat("_Smoothness", 1f - rough); m.SetFloat("_Metallic", metal);
        if (twoSided) m.SetFloat("_Cull", 0f);
        mats[key] = m; return m;
    }
    /// a see-through one (the helmet's bubble)
    static Material Glass(int hex, float rough, float metal, float opacity)
    {
        var m = new Material(Lit()); var c = Vivid(hex, 1f); c.a = opacity;
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 1f - rough); m.SetFloat("_Metallic", metal);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }
    /// tint a loaded model's material the way the web clones and recolours it
    static void Tint(Renderer r, Color col, float metal, float rough)
    {
        var m = r.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col); else if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - rough);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // ---- the shapes three.js hands out, as meshes ----
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static Mesh sphereMesh, cubeMesh;
    static Mesh Prim(PrimitiveType t) { var go = GameObject.CreatePrimitive(t); var m = go.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(go); return m; }
    static Mesh Sphere() { if (sphereMesh == null) sphereMesh = Prim(PrimitiveType.Sphere); return sphereMesh; }
    static Mesh Cube() { if (cubeMesh == null) cubeMesh = Prim(PrimitiveType.Cube); return cubeMesh; }

    // a triangle is front-facing here when Cross(p1-p0, p2-p0) points out, so every builder
    // checks each face against the direction it should show and turns it if it does not
    static Mesh Finish(string key, List<Vector3> V, List<int> T, System.Func<Vector3, Vector3> outward)
    {
        for (int t = 0; t < T.Count; t += 3)
        {
            var p0 = V[T[t]]; var p1 = V[T[t + 1]]; var p2 = V[T[t + 2]];
            var nrm = Vector3.Cross(p1 - p0, p2 - p0);
            if (Vector3.Dot(nrm, outward((p0 + p1 + p2) / 3f)) < 0f) { int k = T[t + 1]; T[t + 1] = T[t + 2]; T[t + 2] = k; }
        }
        var m = new Mesh { name = key }; m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        meshes[key] = m; return m;
    }
    /// three's CylinderGeometry(rTop, rBot, h, seg, 1, open): centred, y up; a cone is rTop = 0
    static Mesh Lathe(float rTop, float rBot, float h, int seg, bool open)
    {
        string key = "L" + rTop + "," + rBot + "," + h + "," + seg + "," + open;
        Mesh m; if (meshes.TryGetValue(key, out m) && m != null) return m;
        var V = new List<Vector3>(); var T = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            V.Add(new Vector3(cs * rTop, h * 0.5f, sn * rTop)); V.Add(new Vector3(cs * rBot, -h * 0.5f, sn * rBot));
        }
        for (int i = 0; i < seg; i++) { int a = i * 2, b = a + 1, c = a + 2, d = a + 3; T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d); }
        if (!open)
        {
            foreach (var cap in new[] { 1, -1 })
            {
                float r = cap > 0 ? rTop : rBot; if (r <= 0f) continue;
                int c0 = V.Count; V.Add(new Vector3(0, cap * h * 0.5f, 0));
                for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; V.Add(new Vector3(Mathf.Cos(a) * r, cap * h * 0.5f, Mathf.Sin(a) * r)); }
                for (int i = 0; i < seg; i++) { T.Add(c0); T.Add(c0 + 1 + i); T.Add(c0 + 2 + i); }
            }
        }
        // a cap's triangles sit entirely on an end plane; a side's centroid never does
        return Finish(key, V, T, p => Mathf.Abs(Mathf.Abs(p.y) - h * 0.5f) < 1e-5f ? new Vector3(0, p.y, 0) : new Vector3(p.x, 0, p.z));
    }
    /// three's TorusGeometry: in the XY plane about z, like the original, so rotation.x = pi/2 lays it flat
    static Mesh Torus(float R, float r, int radSeg, int tubSeg)
    {
        string key = "T" + R + "," + r + "," + radSeg + "," + tubSeg;
        Mesh m; if (meshes.TryGetValue(key, out m) && m != null) return m;
        var V = new List<Vector3>(); var T = new List<int>();
        for (int j = 0; j <= radSeg; j++)
            for (int i = 0; i <= tubSeg; i++)
            {
                float u = i / (float)tubSeg * Mathf.PI * 2f, v = j / (float)radSeg * Mathf.PI * 2f;
                V.Add(new Vector3((R + r * Mathf.Cos(v)) * Mathf.Cos(u), (R + r * Mathf.Cos(v)) * Mathf.Sin(u), r * Mathf.Sin(v)));
            }
        for (int j = 0; j < radSeg; j++)
            for (int i = 0; i < tubSeg; i++)
            {
                int a = j * (tubSeg + 1) + i, b = a + 1, c = a + tubSeg + 1, d = c + 1;
                T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d);
            }
        return Finish(key, V, T, p => { var ring = new Vector3(p.x, p.y, 0).normalized * R; return p - ring; });
    }
    /// three's CircleGeometry: a disc in the XY plane facing +z
    static Mesh Disc(int seg)
    {
        string key = "D" + seg;
        Mesh m; if (meshes.TryGetValue(key, out m) && m != null) return m;
        var V = new List<Vector3> { Vector3.zero }; var T = new List<int>();
        for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; V.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0)); }
        for (int i = 0; i < seg; i++) { T.Add(0); T.Add(1 + i); T.Add(2 + i); }
        return Finish(key, V, T, p => Vector3.forward);
    }
    /// one triangle (the bandana stand-in's drape), drawn from both sides
    static Mesh Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        var m = new Mesh { name = "tri" }; m.SetVertices(new List<Vector3> { a, b, c }); m.SetTriangles(new List<int> { 0, 1, 2 }, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    // a part under its parent at the web's position, scale and rotation (radians, three's axes:
    // the frames here are the mirror of the web's across x, so y and z turns change sign)
    static GameObject Part(Transform p, Mesh mesh, Material m, Vector3 pos, Vector3 scale, float rx = 0f, float ry = 0f, float rz = 0f)
    {
        var go = new GameObject("part"); go.transform.SetParent(p, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(rx * Mathf.Rad2Deg, -ry * Mathf.Rad2Deg, -rz * Mathf.Rad2Deg);
        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return go;
    }
    static GameObject Sph(Transform p, float r, float x, float y, float z, Material m, float sx = 1f, float sy = 1f, float sz = 1f, float rx = 0f)
        => Part(p, Sphere(), m, new Vector3(x, y, z), new Vector3(2 * r * sx, 2 * r * sy, 2 * r * sz), rx);
    static GameObject Cyl(Transform p, float rTop, float rBot, float h, int seg, bool open, float x, float y, float z, Material m, float rx = 0f)
        => Part(p, Lathe(rTop, rBot, h, seg, open), m, new Vector3(x, y, z), Vector3.one, rx);
    static GameObject Tor(Transform p, float R, float r, int radSeg, int tubSeg, float x, float y, float z, Material m, float rx = 0f, float ry = 0f)
        => Part(p, Torus(R, r, radSeg, tubSeg), m, new Vector3(x, y, z), Vector3.one, rx, ry);
    static GameObject Box(Transform p, float w, float h, float d, float x, float y, float z, Material m, float rz = 0f)
        => Part(p, Cube(), m, new Vector3(x, y, z), new Vector3(w, h, d), 0f, 0f, rz);

    /// a model's box in its own root frame, with that root's transform cleared
    static Bounds LocalBounds(Transform root)
    {
        var b = new Bounds(); bool any = false;
        var w2l = root.worldToLocalMatrix;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds; var m = w2l * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }
    static GameObject LoadModel(string name)
    {
        var prefab = Resources.Load<GameObject>("Models/" + name); if (prefab == null) return null;
        var o = Object.Instantiate(prefab); o.name = name; Models.Repair(o);
        o.transform.SetParent(null, false);
        o.transform.position = Vector3.zero; o.transform.rotation = Quaternion.identity; o.transform.localScale = Vector3.one;
        foreach (var r in o.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        foreach (var c in o.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        return o;
    }

    // =====================================================================================
    // ---- the shades: the modelled pair fitted to the face, else the web's lofted bar ----
    static void Shades(Frame F, GearCatalog.Gear E)
    {
        var holder = new GameObject("gear:eyes").transform; holder.SetParent(F.H, false);
        // fitted by WIDTH to the skull's extent along the eye line, or the eye spacing, whichever is more
        float wide = Mathf.Max(F.sep * ShdSep, (F.hi - F.lo) * ShdW);
        var o = LoadModel("shades");
        if (o != null)
        {
            foreach (var r in o.GetComponentsInChildren<Renderer>(true)) Tint(r, Hex(E.frame), Mathf.Min(1f, 0.35f + E.mirror * 0.65f), E.mirror > 0.5f ? 0.12f : 0.34f);
            // the file runs +X across, +Y up and +Z out of the lenses
            var bb = LocalBounds(o.transform); var sz = bb.size; var c = bb.center;
            float k = wide / Mathf.Max(1e-4f, sz.x);
            float depth = sz.z * k, lensOut = Mathf.Max(F.r * 0.95f, F.fr * ShdOut) - depth * 0.5f;
            holder.localPosition = F.mid; holder.localRotation = Quaternion.LookRotation(F.fwd, F.up); holder.localScale = Vector3.one * k;
            o.transform.SetParent(holder, false);
            o.transform.localRotation = ShdTilt != 0f ? Quaternion.AngleAxis(ShdTilt, Vector3.right) : Quaternion.identity;
            // the lens face just proud of the eyes, the arms running back past them
            o.transform.localPosition = new Vector3(-c.x, -c.y + F.sep * ShdUp / k, -c.z + lensOut / k);
        }
        else
        {
            // the lofted pair: a dark bar across the face, a brow bar, two arms -- 0.503 across as
            // authored, sat on the measured eye line the way the modelled pair is
            float k = wide / 0.503f;
            float depth = 0.148f * k, lensOut = Mathf.Max(F.r * 0.95f, F.fr * ShdOut) - depth * 0.5f;
            holder.localPosition = F.mid + F.up * (F.sep * ShdUp) + F.fwd * lensOut;
            holder.localRotation = Quaternion.LookRotation(-F.fwd, F.up); holder.localScale = Vector3.one * k;
            var lensM = Mat(E.lens, E.mirror > 0.5f ? 0.10f : 0.28f, Mathf.Min(1f, 0.25f + E.mirror * 0.75f), 1.0f + E.mirror * 0.5f);
            var frameM = Mat(E.frame, 0.34f, 0.55f, 1.25f);
            Sph(holder, 0.185f, 0f, 0f, 0f, lensM, 1.36f, 0.44f, 0.40f);              // web (0, 0.105, -0.320)
            Sph(holder, 0.185f, 0f, 0.063f, 0.005f, frameM, 1.40f, 0.13f, 0.40f);     // web (0, 0.168, -0.315)
            foreach (var sx in new[] { -1f, 1f }) Sph(holder, 0.150f, sx * 0.235f, 0.030f, 0.240f, frameM, 0.16f, 0.13f, 1.55f);   // web (±0.235, 0.135, -0.080)
        }
    }

    // ---- the bandana: its own model, the collar sized to the neck and seated under the jaw ----
    static void Bandana(Frame F, GearCatalog.Gear B)
    {
        float skull = F.hi - F.lo;
        var neck = F.mid + F.up * (F.bot + skull * BndY);
        var q = Quaternion.LookRotation(F.fwd, F.up);            // the file's +Z is the drape, +Y up
        if (BndTilt != 0f) q = q * Quaternion.AngleAxis(BndTilt, Vector3.right);   // leaned back onto the chest
        var o = LoadModel("bandana");
        if (o != null)
        {
            o.name = "gear:neck";
            // the collar is the top 30% of the mesh: its centre and width are what fit the neck
            Vector3 bmin = Vector3.positiveInfinity, bmax = Vector3.negativeInfinity; var pts = new List<Vector3>();
            var w2l = o.transform.worldToLocalMatrix;
            foreach (var mf in o.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Vector3[] vs = null; try { if (mf.sharedMesh.isReadable) vs = mf.sharedMesh.vertices; } catch { vs = null; }
                var rel = w2l * mf.transform.localToWorldMatrix;
                if (vs != null) { for (int i = 0; i < vs.Length; i += 3) { var p = rel.MultiplyPoint3x4(vs[i]); bmin = Vector3.Min(bmin, p); bmax = Vector3.Max(bmax, p); pts.Add(p); } }
                else
                {
                    var mb = mf.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++) { var p = rel.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z)); bmin = Vector3.Min(bmin, p); bmax = Vector3.Max(bmax, p); pts.Add(p); }
                }
            }
            if (pts.Count == 0) { Object.Destroy(o); o = null; }
            else
            {
                float bh = Mathf.Max(1e-4f, bmax.y - bmin.y);
                var cc = Vector3.zero; int cn = 0; float cx0 = float.PositiveInfinity, cx1 = float.NegativeInfinity;
                foreach (var p in pts) if (p.y > bmax.y - bh * 0.30f) { cc += p; cn++; if (p.x < cx0) cx0 = p.x; if (p.x > cx1) cx1 = p.x; }
                if (cn > 0) cc /= cn; else { cc = (bmin + bmax) * 0.5f; cx0 = bmin.x; cx1 = bmax.x; }
                float collarW = Mathf.Max(1e-4f, cx1 - cx0);
                float k = skull * BndR / collarW;
                o.transform.SetParent(F.H, false);
                o.transform.localScale = Vector3.one * k; o.transform.localRotation = q;
                // the collar's centre lands on the neck point; the origin goes wherever that puts it
                o.transform.localPosition = neck - q * (cc * k);
            }
        }
        if (o == null)
        {
            // no file: a teal collar and a point hanging down the chest
            var holder = new GameObject("gear:neck").transform; holder.SetParent(F.H, false);
            holder.localPosition = neck; holder.localRotation = Quaternion.LookRotation(-F.fwd, F.up); holder.localScale = Vector3.one * skull;
            var teal = Mat(0x1f9aa8, 0.70f, 0.02f, 1.0f, true);
            Tor(holder, BndR * 0.5f, 0.045f, 10, 26, 0f, 0f, 0f, teal, Mathf.PI / 2f);
            Part(holder, Tri(new Vector3(-BndR * 0.45f, -0.02f, -BndR * 0.42f), new Vector3(BndR * 0.45f, -0.02f, -BndR * 0.42f), new Vector3(0f, -0.72f, -BndR * 0.25f)), teal, Vector3.zero, Vector3.one);
        }
    }

    // ---- the hats: the Cattleman is a file, the rest are lofted in the skull's own units ----
    static void Hat(Frame F, GearCatalog.Gear H)
    {
        bool crown = H.kind == "crown";
        var m = Mat(H.col, crown ? 0.22f : 0.72f, crown ? 0.85f : 0.05f, crown ? 1.6f : 1.05f);
        if (H.kind == "stetson" && Stetson(F, H)) return;
        var GP = F.J;
        switch (H.kind)
        {
            case "cap":
                Sph(GP, 0.355f, 0f, 0.175f, 0.015f, m, 1.06f, 0.78f, 1.04f);
                Sph(GP, 0.235f, 0f, 0.150f, -0.330f, m, 1.02f, 0.14f, 0.92f, -0.16f);
                break;
            case "brim":
                Sph(GP, 0.315f, 0f, 0.235f, 0f, m, 1.00f, 0.86f, 1.00f);
                Cyl(GP, 0.56f, 0.60f, 0.035f, 24, false, 0f, 0.115f, -0.02f, m, 0.06f);
                break;
            case "beanie":
                Sph(GP, 0.375f, 0f, 0.150f, 0f, m, 1.05f, 0.86f, 1.05f);
                Tor(GP, 0.335f, 0.058f, 10, 22, 0f, 0.060f, 0f, m, Mathf.PI / 2f);
                break;
            case "toque":
            {
                // the chef's hat: a stiff band, and a puffed crown that is mostly air
                Cyl(GP, 0.345f, 0.335f, 0.14f, 22, true, 0f, 0.235f, 0f, m);
                Sph(GP, 0.36f, 0f, 0.46f, 0f, m, 1.0f, 0.78f, 1.0f);
                for (int i = 0; i < 6; i++) { float a2 = i / 6f * Mathf.PI * 2f; Sph(GP, 0.15f, Mathf.Sin(a2) * 0.27f, 0.50f, Mathf.Cos(a2) * 0.27f, m); }
                break;
            }
            case "tophat":
                Cyl(GP, 0.50f, 0.50f, 0.03f, 26, false, 0f, 0.165f, 0f, m);
                Cyl(GP, 0.325f, 0.31f, 0.44f, 24, false, 0f, 0.395f, 0f, m);
                Cyl(GP, 0.335f, 0.335f, 0.07f, 24, true, 0f, 0.215f, 0f, Mat(H.band >= 0 ? H.band : 0x2a1a10, 0.5f, 0.05f, 1.0f));
                break;
            case "wizard":
            {
                Cyl(GP, 0.54f, 0.56f, 0.03f, 26, false, 0f, 0.165f, 0f, m);
                Cyl(GP, 0f, 0.31f, 0.80f, 20, false, 0f, 0.56f, -0.04f, m, -0.16f);
                // a scatter of stars, which is what makes a cone a wizard
                var gold = Mat(0xf4d35e, 0.4f, 0.3f, 1.2f);
                for (int i = 0; i < 7; i++)
                {
                    float a2 = i * 2.4f, y = 0.30f + i * 0.075f, r = 0.31f * (1f - (y - 0.16f) / 0.80f) + 0.01f;
                    Sph(GP, 0.03f, Mathf.Sin(a2) * r, y, Mathf.Cos(a2) * r - 0.04f, gold);
                }
                break;
            }
            case "headband":
                Tor(GP, 0.345f, 0.048f, 10, 26, 0f, 0.150f, 0f, m, Mathf.PI / 2f);
                // the knot at the back, and its two tails
                Sph(GP, 0.075f, 0f, 0.150f, 0.345f, m);
                foreach (var sx in new[] { -1f, 1f }) Box(GP, 0.06f, 0.22f, 0.02f, sx * 0.06f, 0.03f, 0.36f, m, sx * 0.35f);
                break;
            case "astro":
                // a bubble over the whole head, and the collar ring it seals to
                Sph(GP, 0.50f, 0f, 0.06f, -0.02f, Glass(0xdff3ff, 0.05f, 0.1f, 0.22f));
                Cyl(GP, 0.44f, 0.46f, 0.10f, 26, false, 0f, -0.40f, -0.02f, Mat(0xf2f2f0, 0.45f, 0.15f, 1.0f));
                break;
            case "aviator":
            {
                Sph(GP, 0.36f, 0f, 0.16f, 0f, m, 1.06f, 0.82f, 1.06f);
                foreach (var sx in new[] { -1f, 1f }) Box(GP, 0.10f, 0.22f, 0.16f, sx * 0.33f, -0.02f, 0.02f, m);
                Cyl(GP, 0.37f, 0.37f, 0.05f, 24, true, 0f, 0.235f, 0f, Mat(0x2a1a10, 0.5f, 0.05f, 1.0f));
                var gm = Mat(0x8a6b3a, 0.35f, 0.4f, 1.1f); var lens = Mat(0x9fd4e8, 0.08f, 0.2f, 1.0f, true);
                foreach (var sx in new[] { -1f, 1f })
                {
                    Tor(GP, 0.11f, 0.028f, 10, 20, sx * 0.13f, 0.25f, -0.33f, gm, 0f, sx * 0.25f);
                    Part(GP, Disc(20), lens, new Vector3(sx * 0.13f, 0.25f, -0.335f), Vector3.one * 0.10f, 0f, sx * 0.25f);
                }
                break;
            }
            case "samurai":
            {
                Sph(GP, 0.375f, 0f, 0.17f, 0f, m, 1.08f, 0.74f, 1.08f);
                Cyl(GP, 0.40f, 0.55f, 0.16f, 24, true, 0f, 0.02f, 0.02f, m);
                var gold = Mat(0xd4af37, 0.3f, 0.7f, 1.2f);
                foreach (var sx in new[] { -1f, 1f }) Box(GP, 0.05f, 0.30f, 0.02f, sx * 0.10f, 0.40f, -0.30f, gold, -sx * 0.55f);
                Cyl(GP, 0.07f, 0.07f, 0.02f, 16, false, 0f, 0.28f, -0.34f, gold, Mathf.PI / 2f);
                break;
            }
            case "stetson":
            {
                // the file did not load: a creased felt crown, a rolled brim, the band and its buckle
                Cyl(GP, 0.60f, 0.62f, 0.03f, 26, false, 0f, 0.165f, 0f, m, 0.04f);
                Cyl(GP, 0.30f, 0.335f, 0.42f, 24, false, 0f, 0.38f, 0f, m);
                Cyl(GP, 0.345f, 0.345f, 0.07f, 24, true, 0f, 0.21f, 0f, Mat(H.band >= 0 ? H.band : 0x3d2a1c, 0.55f, 0.04f, 1.0f));
                Box(GP, 0.07f, 0.06f, 0.02f, 0f, 0.21f, -0.345f, Mat(H.buckle >= 0 ? H.buckle : 0xc9a227, 0.28f, 0.90f, 1.0f));
                break;
            }
            default:
            {
                // crown: the one worn by exactly one surfer
                Cyl(GP, 0.335f, 0.345f, 0.145f, 22, true, 0f, 0.330f, 0f, m);
                for (int i = 0; i < 7; i++) { float a2 = i / 7f * Mathf.PI * 2f; Cyl(GP, 0f, 0.062f, 0.20f, 8, false, Mathf.Sin(a2) * 0.325f, 0.475f, Mathf.Cos(a2) * 0.325f, m); }
                break;
            }
        }
    }

    /// the Cattleman from its file, sized by its own 175 mm head opening to the skull's width and
    /// set down by its underside on the crown (web 17079-17139)
    static bool Stetson(Frame F, GearCatalog.Gear H)
    {
        var o = LoadModel("cowboyhat"); if (o == null) return false;
        o.name = "gear:head";
        foreach (var r in o.GetComponentsInChildren<Renderer>(true))
        {
            string nm = (r.name + "|" + (r.transform.parent != null ? r.transform.parent.name : "") + "|" + (r.sharedMaterial != null ? r.sharedMaterial.name : "")).ToLowerInvariant();
            bool brass = nm.Contains("buckle") || nm.Contains("brass"), leather = nm.Contains("band") || nm.Contains("leather");
            Tint(r, Hex(brass ? H.buckle : leather ? H.band : H.col), brass ? 0.90f : 0.04f, brass ? 0.28f : (leather ? 0.55f : 0.76f));
        }
        var hbb = LocalBounds(o.transform); var hc = hbb.center;
        const float OPEN = 0.175f;                                 // the model's own head opening, side to side
        float kk = (F.hi - F.lo) * HatW / OPEN;                    // slightly under the widest part of the skull, deliberately
        // the file's +X is the front and +Y up; the importer's mirror puts that front at -X
        var front = ImportMirrorX ? -F.fwd : F.fwd;
        o.transform.SetParent(F.H, false);
        o.transform.localScale = Vector3.one * kk;
        o.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(front, F.up), F.up);
        // placed by its UNDERSIDE, a fraction of the skull's height up, and walked back toward the skull's middle
        float fore = (ImportMirrorX ? hc.x : -hc.x) * kk;
        o.transform.localPosition = F.mid + F.up * (F.top * HatUp - hbb.min.y * kk) + F.fwd * ((F.fr + F.bk) * 0.5f * HatFwd + fore);
        return true;
    }

    // =====================================================================================
    // ---- the suit, cut from the body that wears it (web wearSuit / cutGarment / wearAuthored) ----
    class GD { public string part, len, sleeves; public bool tile, buttons; public int trim = -1, btn = -1; public float rough = -1f; }
    static readonly HashSet<string> TILE = new HashSet<string> { "stripes", "check", "plaid", "stars", "denim", "leather", "brocade", "hawaii" };
    static readonly HashSet<string> BTN = new HashSet<string> { "chef", "button", "brocade" };

    static void Suit(Transform root, GearCatalog.Gear S, string riderId)
    {
        var F = GarmentFrame(root); if (F == null) return;
        bool authored = riderId == "pug" && Resources.Load<GameObject>("Models/garments") != null;
        if (S.pants != null)
        {
            var sp = S.pants;
            var D = new GD { part = "pants", len = S.len ?? "shorts", tile = TILE.Contains(sp.k ?? "solid") && !sp.belt, trim = sp.trim >= 0 ? sp.trim : S.hem, rough = S.rough };
            var tex = GarmentTex(sp);
            if (!(authored && WearAuthored(F, D.len == "long" ? "pants_long" : "pants_short", D, tex))) CutGarment(F, D, tex);
        }
        if (S.shirt != null)
        {
            var sp = S.shirt; string sl = S.sleeves ?? "short";
            var D = new GD { part = "shirt", sleeves = sl, tile = TILE.Contains(sp.k ?? "solid") && !sp.belt, trim = sp.trim >= 0 ? sp.trim : S.hem, rough = S.rough, buttons = BTN.Contains(sp.k ?? "solid"), btn = sp.btn };
            var tex = GarmentTex(sp);
            if (!(authored && WearAuthored(F, sl == "none" ? "vest" : (sl == "long" ? "shirt_long" : "shirt_short"), D, tex))) CutGarment(F, D, tex);
        }
    }

    // the rider's skinned mesh and the landmarks a garment is cut against, in the mesh's own
    // bind space, the one space nothing animates
    class GF
    {
        public SkinnedMeshRenderer sk; public Mesh mesh;
        public Vector3[] pos, nrm; public BoneWeight[] bw; public int[] tri; public Matrix4x4[] bind;
        public Dictionary<string, int> bi = new Dictionary<string, int>();
        public Vector3 up, fwd, right;
        public float hips, shoulder, neck, knee, ankle, uparm, elbow, wrist;   // heights along up; NaN where the rig has no such bone
        public float axisR, axisF, tall;
    }
    class Acc { public float s, lo = float.PositiveInfinity, hi = float.NegativeInfinity; public int n; }

    static GF GarmentFrame(Transform root)
    {
        SkinnedMeshRenderer sk = null; int best = -1;
        foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (s.sharedMesh == null || s.bones == null) continue;
            if (IndexOf(s.bones, "Hips") < 0 || IndexOf(s.bones, "Head") < 0) continue;
            if (s.sharedMesh.vertexCount > best) { best = s.sharedMesh.vertexCount; sk = s; }
        }
        if (sk == null) return null;
        var F = new GF { sk = sk, mesh = sk.sharedMesh };
        try
        {
            if (!F.mesh.isReadable) { Debug.LogWarning("Gear: the rider's mesh is not readable, so no suit can be cut from it"); return null; }
            F.pos = F.mesh.vertices; F.nrm = F.mesh.normals; F.bw = F.mesh.boneWeights; F.tri = F.mesh.triangles; F.bind = F.mesh.bindposes;
        }
        catch { return null; }
        if (F.pos == null || F.nrm == null || F.nrm.Length != F.pos.Length || F.bw == null || F.bw.Length != F.pos.Length || F.bind == null || F.tri == null) return null;
        var bl = sk.bones;
        for (int i = 0; i < bl.Length; i++) if (bl[i] != null && !F.bi.ContainsKey(bl[i].name)) F.bi[bl[i].name] = i;
        // a bone's bind position in mesh space
        Vector3 hipsB, headB, frontB;
        if (!BoneAt(F, "Hips", out hipsB) || !BoneAt(F, "Head", out headB)) return null;
        bool hasFront = BoneAt(F, "headfront", out frontB) || BoneAt(F, "head_end", out frontB);
        F.up = (headB - hipsB).normalized;
        var fwd = hasFront ? frontB - headB : Vector3.back;
        fwd -= F.up * Vector3.Dot(fwd, F.up); if (fwd.sqrMagnitude < 1e-9f) fwd = Vector3.back; fwd.Normalize();
        F.fwd = fwd; F.right = Vector3.Cross(F.up, fwd).normalized;
        // positions come from the vertices: a landmark is the mean of the vertices its bone owns
        var acc = new Dictionary<string, Acc>();
        var CORE = new HashSet<int>(); foreach (var n in new[] { "Hips", "Spine", "Spine01", "Spine02", "Spine1", "Spine2", "LeftShoulder", "RightShoulder" }) { int i; if (F.bi.TryGetValue(n, out i)) CORE.Add(i); }
        float cxs = 0f, czs = 0f; int cn = 0; float minH = float.PositiveInfinity, maxH = float.NegativeInfinity;
        for (int v = 0; v < F.pos.Length; v++)
        {
            var p = F.pos[v]; var w = F.bw[v];
            int b0 = w.boneIndex0; float w0 = w.weight0;
            if (w.weight1 > w0) { w0 = w.weight1; b0 = w.boneIndex1; } if (w.weight2 > w0) { w0 = w.weight2; b0 = w.boneIndex2; } if (w.weight3 > w0) { w0 = w.weight3; b0 = w.boneIndex3; }
            string nm = (b0 >= 0 && b0 < bl.Length && bl[b0] != null) ? bl[b0].name : "?";
            float hh = Vector3.Dot(p, F.up);
            Acc e; if (!acc.TryGetValue(nm, out e)) { e = new Acc(); acc[nm] = e; }
            e.s += hh; e.n++; if (hh < e.lo) e.lo = hh; if (hh > e.hi) e.hi = hh;
            if (CORE.Contains(b0)) { cxs += Vector3.Dot(p, F.right); czs += Vector3.Dot(p, F.fwd); cn++; }
            if (hh < minH) minH = hh; if (hh > maxH) maxH = hh;
        }
        System.Func<string, float> mean = n => { Acc e; return acc.TryGetValue(n, out e) ? e.s / e.n : float.NaN; };
        System.Func<string, float> lo = n => { Acc e; return acc.TryGetValue(n, out e) ? e.lo : float.NaN; };
        System.Func<string, float> hi = n => { Acc e; return acc.TryGetValue(n, out e) ? e.hi : float.NaN; };
        System.Func<System.Func<string, float>, string, string, float> two = (f, a, b) => { float A = f(a), B = f(b); return !float.IsNaN(A) && !float.IsNaN(B) ? (A + B) * 0.5f : (!float.IsNaN(A) ? A : B); };
        F.hips = mean("Hips"); if (float.IsNaN(F.hips)) return null;
        F.shoulder = two(mean, "LeftShoulder", "RightShoulder");
        // the collar is where the head begins: the head's lowest vertex, the neck's middle or the
        // shoulders' top, whichever is highest
        float nk = float.NegativeInfinity;
        float v1 = lo("Head"); if (!float.IsNaN(v1) && v1 > nk) nk = v1;
        float v2 = mean("neck"); if (float.IsNaN(v2)) v2 = mean("Neck"); if (!float.IsNaN(v2) && v2 > nk) nk = v2;
        float v3 = two(hi, "LeftShoulder", "RightShoulder"); if (!float.IsNaN(v3) && v3 > nk) nk = v3;
        F.neck = float.IsInfinity(nk) ? F.hips + 0.5f : nk;
        F.knee = two(mean, "LeftLeg", "RightLeg"); F.ankle = two(mean, "LeftFoot", "RightFoot");
        F.uparm = two(mean, "LeftArm", "RightArm"); F.elbow = two(mean, "LeftForeArm", "RightForeArm"); F.wrist = two(mean, "LeftHand", "RightHand");
        if (float.IsNaN(F.shoulder)) F.shoulder = F.hips + 0.3f; if (float.IsNaN(F.knee)) F.knee = F.hips - 0.3f; if (float.IsNaN(F.ankle)) F.ankle = F.knee - 0.2f;
        F.axisR = cn > 0 ? cxs / cn : 0f; F.axisF = cn > 0 ? czs / cn : 0f;
        F.tall = Mathf.Max(1e-4f, maxH - minH);
        return F;
    }
    static bool BoneAt(GF F, string n, out Vector3 p)
    {
        int i; p = Vector3.zero;
        if (!F.bi.TryGetValue(n, out i) || i >= F.bind.Length) return false;
        p = F.bind[i].inverse.MultiplyPoint3x4(Vector3.zero); return true;
    }

    // the twelve corners of an icosahedron and its twenty faces: a button
    static readonly float PHI = (1f + Mathf.Sqrt(5f)) / 2f;
    static readonly Vector3[] ICO = { new Vector3(-1, PHI, 0), new Vector3(1, PHI, 0), new Vector3(-1, -PHI, 0), new Vector3(1, -PHI, 0), new Vector3(0, -1, PHI), new Vector3(0, 1, PHI), new Vector3(0, -1, -PHI), new Vector3(0, 1, -PHI), new Vector3(PHI, 0, -1), new Vector3(PHI, 0, 1), new Vector3(-PHI, 0, -1), new Vector3(-PHI, 0, 1) };
    static readonly int[] ICOF = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };

    /// one garment: the rider's own triangles that the garment's bones own and that sit between
    /// its hem and its neckline, welded, pushed out along their normals, hemmed and buttoned
    static void CutGarment(GF F, GD D, Texture2D tex)
    {
        var pos = F.pos; var nrm = F.nrm; var bw = F.bw; int nv = pos.Length;
        System.Func<string[], HashSet<int>> set = names => { var s = new HashSet<int>(); foreach (var n in names) { int i; if (F.bi.TryGetValue(n, out i)) s.Add(i); } return s; };
        // the throat is the neck bone's, and without it the neckline fell into a V at the front
        var CORE = set(new[] { "Hips", "Spine", "Spine01", "Spine02", "Spine1", "Spine2", "LeftShoulder", "RightShoulder", "neck", "Neck" });
        var ARMS = set(new[] { "LeftArm", "RightArm", "LeftForeArm", "RightForeArm", "LeftHand", "RightHand" });
        var LEGS = set(new[] { "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase" });
        System.Func<int, HashSet<int>, float> own = (v, S) => { var w = bw[v]; float s = 0f; if (S.Contains(w.boneIndex0)) s += w.weight0; if (S.Contains(w.boneIndex1)) s += w.weight1; if (S.Contains(w.boneIndex2)) s += w.weight2; if (S.Contains(w.boneIndex3)) s += w.weight3; return s; };
        bool shirt = D.part == "shirt";
        float torso = Mathf.Max(1e-4f, F.shoulder - F.hips), thigh = Mathf.Max(1e-4f, F.hips - F.knee), shin = Mathf.Max(1e-4f, F.knee - F.ankle);
        float waist = F.hips + GWaist * torso;
        float lo, hi, lift;
        if (shirt) { lo = waist - GTuck * torso; hi = F.hips + GCollar * (F.neck - F.hips); lift = GShirtLift; }
        else { hi = waist + 0.02f * torso; lift = GLift; lo = D.len == "long" ? F.ankle + GLongHem * shin : F.knee + GShortHem * thigh; }
        string sleeves = D.sleeves ?? "none";
        // a sleeve ends at a HEIGHT down the arm, not at a bone, so the cuff is a straight line
        float ua = !float.IsNaN(F.uparm) ? F.uparm : F.shoulder - 0.1f, el = !float.IsNaN(F.elbow) ? F.elbow : ua - 0.1f, wr = !float.IsNaN(F.wrist) ? F.wrist : el - 0.1f;
        float cuffH = sleeves == "long" ? wr + 0.25f * (el - wr) : ua - 0.2f * (ua - el);
        // keep: 0 not this garment's, 1 inside, 2 on the right bones but past a hem (its corners
        // slide to the hem line, so the edge is a line and not a sawtooth)
        var keep = new byte[nv]; var wcore = new byte[nv]; var snapLo = new float[nv]; var snapHi = new float[nv];
        for (int v = 0; v < nv; v++)
        {
            float hh = Vector3.Dot(pos[v], F.up); byte k = 0;
            float wc = own(v, CORE); wcore[v] = (byte)(wc >= 0.5f ? 1 : 0);
            if (shirt)
            {
                float wa = sleeves != "none" ? own(v, ARMS) : 0f;
                if (wc + wa >= 0.5f)
                {
                    if (wa > wc) { k = (byte)(hh >= cuffH ? 1 : 2); snapLo[v] = cuffH; snapHi[v] = float.PositiveInfinity; }
                    else { k = (byte)((hh >= lo && hh <= hi) ? 1 : 2); snapLo[v] = lo; snapHi[v] = hi; }
                }
            }
            else
            {
                float wl = own(v, LEGS);
                if (wc + wl >= 0.5f)
                {
                    if (wl > wc) { k = (byte)(hh >= lo ? 1 : 2); snapLo[v] = lo; snapHi[v] = float.PositiveInfinity; }
                    else { k = (byte)((hh >= lo && hh <= hi) ? 1 : 2); snapLo[v] = lo; snapHi[v] = hi; }
                }
            }
            keep[v] = k;
        }
        var tris = new List<int>();
        var src = F.tri;
        for (int t = 0; t + 2 < src.Length; t += 3)
        {
            int a = src[t], b = src[t + 1], c = src[t + 2];
            byte ka = keep[a], kb = keep[b], kc = keep[c];
            if (ka != 0 && kb != 0 && kc != 0 && (ka == 1 || kb == 1 || kc == 1)) { tris.Add(a); tris.Add(b); tris.Add(c); }
        }
        if (tris.Count < 3) { Debug.LogWarning("Gear: nothing to cut for the " + D.part); return; }
        // weld by position before offsetting, or the seams the file split drift apart as shards
        float eps = Mathf.Max(1e-6f, F.tall * 5e-4f);
        var kmap = new Dictionary<string, int>(); var order = new List<int>(); var NS = new List<Vector3>(); var mIdx = new int[nv];
        for (int i = 0; i < nv; i++) mIdx[i] = -1;
        foreach (var v in tris)
        {
            if (mIdx[v] >= 0) continue;
            var p = pos[v];
            string key = Mathf.RoundToInt(p.x / eps) + "," + Mathf.RoundToInt(p.y / eps) + "," + Mathf.RoundToInt(p.z / eps);
            int i;
            if (!kmap.TryGetValue(key, out i)) { i = order.Count; kmap[key] = i; order.Add(v); NS.Add(Vector3.zero); }
            mIdx[v] = i; NS[i] = NS[i] + nrm[v];
        }
        int n0 = order.Count;
        var P = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var BW = new List<BoneWeight>(); var rr = new List<float>();
        float liftAbs = lift * F.tall;
        for (int i = 0; i < n0; i++)
        {
            int v = order[i]; var p = pos[v]; var nn = NS[i];
            if (nn.sqrMagnitude < 1e-8f) nn = nrm[v]; nn.Normalize();
            if (keep[v] == 2) { float h0 = Vector3.Dot(p, F.up); p += F.up * (Mathf.Min(snapHi[v], Mathf.Max(snapLo[v], h0)) - h0); }
            // a shirt hangs: snug at the shoulders, further off the body towards the hem
            float vf = Mathf.Clamp01((Vector3.Dot(p, F.up) - lo) / Mathf.Max(1e-4f, hi - lo));
            p += nn * (liftAbs * (shirt ? (1f + GHang * (1f - vf)) : 1f));
            P.Add(p); N.Add(nn);
            float hh = Vector3.Dot(p, F.up), ang = Mathf.Atan2(Vector3.Dot(p, F.right) - F.axisR, Vector3.Dot(p, F.fwd) - F.axisF);
            UV.Add(new Vector2(ang / (Mathf.PI * 2f) + 0.5f, (hh - lo) / Mathf.Max(1e-4f, hi - lo)));   // 0.5 is dead centre of the chest; 1 at the collar
            if (keep[v] == 1 && wcore[v] == 1) rr.Add(Mathf.Sqrt(Sq(Vector3.Dot(p, F.right) - F.axisR) + Sq(Vector3.Dot(p, F.fwd) - F.axisF)));
            BW.Add(bw[v]);
        }
        // the back seam: a triangle astride u=0|1 gets its low-side corners copied at u+1
        var alt = new Dictionary<int, int>(); var index = new List<int>(); var baseOf = new Dictionary<int, int>();
        System.Func<int, int> wrap = i => { int j; if (!alt.TryGetValue(i, out j)) { j = P.Count; alt[i] = j; baseOf[j] = i; P.Add(P[i]); N.Add(N[i]); UV.Add(new Vector2(UV[i].x + 1f, UV[i].y)); BW.Add(BW[i]); } return j; };
        for (int t = 0; t < tris.Count; t += 3)
        {
            int a = mIdx[tris[t]], b = mIdx[tris[t + 1]], c = mIdx[tris[t + 2]];
            if (a == b || b == c || a == c) continue;               // welded flat
            float ua2 = UV[a].x, ub = UV[b].x, uc = UV[c].x;
            if (Mathf.Max(ua2, Mathf.Max(ub, uc)) - Mathf.Min(ua2, Mathf.Min(ub, uc)) > 0.5f) { index.Add(ua2 < 0.5f ? wrap(a) : a); index.Add(ub < 0.5f ? wrap(b) : b); index.Add(uc < 0.5f ? wrap(c) : c); }
            else { index.Add(a); index.Add(b); index.Add(c); }
        }
        // a seamless pattern is tiled round the body as many times as it is wider than the garment is tall
        if (D.tile && rr.Count > 0)
        {
            float r = 0f; foreach (var x in rr) r += x; r /= rr.Count;
            int tile = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * r / Mathf.Max(1e-4f, hi - lo)));
            for (int i = 0; i < UV.Count; i++) UV[i] = new Vector2(UV[i].x * tile, UV[i].y);
        }
        // ---- edges, or it is paint: a rim of cloth thickness on every open edge, and a folded band at the hems ----
        int clothCount = index.Count; float tall = F.tall;
        float rimT = GRimT * tall, tol = 0.03f * tall;
        var ecount = new Dictionary<long, int>(); const long EK = 1L << 22;
        System.Func<int, int> bs = i => { int b; return baseOf.TryGetValue(i, out b) ? b : i; };
        for (int t = 0; t < clothCount; t += 3)
        {
            int[] q = { bs(index[t]), bs(index[t + 1]), bs(index[t + 2]) };
            for (int e = 0; e < 3; e++) { int a = q[e], b = q[(e + 1) % 3]; long k = (long)Mathf.Min(a, b) * EK + Mathf.Max(a, b); int c; ecount.TryGetValue(k, out c); ecount[k] = c + 1; }
        }
        var ring1 = new Dictionary<int, int>(); var ring2 = new Dictionary<int, int>(); var up = F.up;
        System.Func<int, Vector3, int> copyV = (i, at) => { int j = P.Count; P.Add(at); N.Add(N[i]); UV.Add(UV[i]); BW.Add(BW[i]); return j; };
        System.Func<int, string> kind = i => { float h = Vector3.Dot(P[i], up); return Mathf.Abs(h - lo) < tol ? "hem" : (Mathf.Abs(h - hi) < tol ? "top" : ((shirt && Mathf.Abs(h - cuffH) < tol) ? "cuff" : "edge")); };
        System.Func<string, float> bandW = kd => shirt ? (kd == "hem" ? GHemW : kd == "top" ? GCollarW : GCuffW) : (kd == "hem" ? GHemW : GWaistW);
        System.Func<int, int> r1 = i => { int j; if (!ring1.TryGetValue(i, out j)) { j = copyV(i, P[i] + N[i] * rimT); ring1[i] = j; } return j; };
        System.Func<int, int> r2 = i =>
        {
            int j; if (ring2.TryGetValue(i, out j)) return j;
            string kd = kind(i); if (kd == "edge") return -1;
            float w = bandW(kd) * tall * (kd == "top" ? -1f : 1f); int a = r1(i);
            j = copyV(i, P[a] + up * w); ring2[i] = j; return j;
        };
        for (int t = 0; t < clothCount; t += 3)
        {
            int[] q = { bs(index[t]), bs(index[t + 1]), bs(index[t + 2]) };
            for (int e = 0; e < 3; e++)
            {
                int a = q[e], b = q[(e + 1) % 3]; long k = (long)Mathf.Min(a, b) * EK + Mathf.Max(a, b);
                int c; if (!ecount.TryGetValue(k, out c) || c != 1) continue;
                int a1 = r1(a), b1 = r1(b);
                index.Add(a); index.Add(b); index.Add(b1); index.Add(a); index.Add(b1); index.Add(a1);
                int a2 = r2(a), b2 = r2(b);
                if (a2 >= 0 && b2 >= 0) { index.Add(a1); index.Add(b1); index.Add(b2); index.Add(a1); index.Add(b2); index.Add(a2); }
            }
        }
        int trimCount = index.Count - clothCount;
        int btnCount = 0;
        if (D.buttons && shirt)
        {
            // centre front is u=0.5 by construction; five buttons from just under the collar to the hem
            var cand = new List<int>(); for (int i = 0; i < n0; i++) if (Mathf.Abs(UV[i].x - 0.5f) < 0.04f && wcore[order[i]] == 1) cand.Add(i);
            float R = GBtnR * tall;
            for (int bnum = 0; bnum < 5 && cand.Count > 0; bnum++)
            {
                float vt = 0.86f - bnum * 0.17f; int bestI = -1; float bd = 1e9f;
                foreach (var i in cand) { float d = Mathf.Abs(UV[i].y - vt); if (d < bd) { bd = d; bestI = i; } }
                if (bestI < 0 || bd > 0.08f) continue;
                var c = P[bestI] + N[bestI] * (rimT + R * 0.6f);
                int b0 = P.Count;
                foreach (var q in ICO) { var d = q.normalized; P.Add(c + d * R); N.Add(d); UV.Add(new Vector2(0.5f, 0.5f)); BW.Add(BW[bestI]); }
                foreach (var f in ICOF) index.Add(b0 + f);
                btnCount += ICOF.Length;
            }
        }
        var mesh = new Mesh { name = "garment:" + D.part };
        if (P.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(P); mesh.SetNormals(N); mesh.SetUVs(0, UV);
        mesh.boneWeights = BW.ToArray(); mesh.bindposes = F.bind;
        mesh.subMeshCount = btnCount > 0 ? 3 : 2;
        mesh.SetTriangles(index.GetRange(0, clothCount), 0);
        mesh.SetTriangles(index.GetRange(clothCount, trimCount), 1);
        if (btnCount > 0) mesh.SetTriangles(index.GetRange(clothCount + trimCount, btnCount), 2);
        mesh.RecalculateBounds();
        var mats = GarmentMats(D, tex, btnCount > 0);
        Wear(F, mesh, F.sk.bones, mats, D.part);
    }
    static float Sq(float x) => x * x;

    // the cloth (the print, white underneath), its trim, and its buttons
    static Material[] GarmentMats(GD D, Texture2D tex, bool buttons)
    {
        var m = new Material(Lit()); m.SetColor("_BaseColor", Color.white); m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", 1f - (D.rough >= 0f ? D.rough : 0.66f)); m.SetFloat("_Metallic", 0.02f); m.SetFloat("_Cull", 0f);
        int trimC = D.trim >= 0 ? D.trim : GarmentAvg(tex, 0.72f);
        var mt = Mat(trimC, 0.72f, 0.02f, 1.0f, true);
        if (!buttons) return new[] { m, mt };
        float lum = ((trimC >> 16) & 255) * 0.3f + ((trimC >> 8) & 255) * 0.59f + (trimC & 255) * 0.11f;
        var mb = Mat(D.btn >= 0 ? D.btn : (lum < 110f ? 0xf2ead8 : 0x2a2622), 0.35f, 0.05f, 1.0f);
        return new[] { m, mt, mb };
    }

    // same parent, same local transform, same bones and bind -- so it is the same surface, a hair out
    static void Wear(GF F, Mesh mesh, Transform[] bones, Material[] mats, string part)
    {
        var go = new GameObject("gear:body:" + part);
        go.transform.SetParent(F.sk.transform.parent, false);
        go.transform.localPosition = F.sk.transform.localPosition; go.transform.localRotation = F.sk.transform.localRotation; go.transform.localScale = F.sk.transform.localScale;
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = F.sk.rootBone; smr.sharedMaterials = mats;
        smr.updateWhenOffscreen = true; smr.quality = F.sk.quality;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = true;
    }

    /// the pug's authored wardrobe (garments.glb): the piece of that name re-skinned onto his
    /// bones by name, its print tiled round by the file's own circumference-over-height
    static bool WearAuthored(GF F, string name, GD D, Texture2D tex)
    {
        var prefab = Resources.Load<GameObject>("Models/garments"); if (prefab == null) return false;
        var inst = Object.Instantiate(prefab); inst.SetActive(false);
        bool any = false;
        try
        {
            Transform hips = null; int hi; if (F.bi.TryGetValue("Hips", out hi)) hips = F.sk.bones[hi];
            // the file's extras: pants 12.376, shirts and the vest 3.702 (circumference over height)
            float circ = name.StartsWith("pants") ? 12.376f : 3.702f;
            int tile = D.tile ? Mathf.Max(1, Mathf.RoundToInt(circ)) : 1;
            foreach (var g in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // the importer may split a mesh with three materials into children under a group of the name
                bool match = false;
                for (Transform t = g.transform, stop = inst.transform; t != null && t != stop; t = t.parent)
                    if (t.name == name || t.name.StartsWith(name + "_") || t.name.StartsWith(name + ".")) { match = true; break; }
                if (!match || g.sharedMesh == null || g.bones == null) continue;
                var gb = g.bones; var mapped = new Transform[gb.Length];
                for (int i = 0; i < gb.Length; i++) { int bi; mapped[i] = (gb[i] != null && F.bi.TryGetValue(gb[i].name, out bi)) ? F.sk.bones[bi] : (hips != null ? hips : F.sk.bones[0]); }
                var mesh = g.sharedMesh;
                if (tile > 1)
                {
                    try { var copy = Object.Instantiate(mesh); var uv = copy.uv; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(uv[i].x * tile, uv[i].y); copy.uv = uv; mesh = copy; }
                    catch { mesh = g.sharedMesh; }
                }
                var base3 = GarmentMats(D, tex, true);
                var sm = g.sharedMaterials; var mats = new Material[Mathf.Max(1, sm.Length)];
                for (int i = 0; i < mats.Length; i++)
                {
                    string mn = (i < sm.Length && sm[i] != null) ? sm[i].name.Split('.')[0].Split(' ')[0] : "";
                    if (mn == "trim") mats[i] = base3[1];
                    else if (mn == "button") mats[i] = D.buttons ? base3[2] : base3[0];
                    else if (mn == "cloth") mats[i] = base3[0];
                    else mats[i] = i == 1 ? base3[1] : i == 2 ? (D.buttons ? base3[2] : base3[0]) : base3[0];
                }
                Wear(F, mesh, mapped, mats, D.part);
                any = true;
            }
        }
        finally { Object.Destroy(inst); }
        return any;
    }

    // =====================================================================================
    // ---- the prints, painted from the description (web garmentTex) ----
    static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<Texture2D, int> texAvg = new Dictionary<Texture2D, int>();

    class Paint
    {
        public int W, H; public Color[] px; System.Random rnd = new System.Random(7);
        public Paint(int w, int h) { W = w; H = h; px = new Color[w * h]; }
        public float R() => (float)rnd.NextDouble();
        // canvas rows run down from the top; a texture's run up
        int At(int x, int y) => (H - 1 - y) * W + x;
        public void Fill(Color c) { for (int i = 0; i < px.Length; i++) px[i] = c; }
        void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = At(x, y); if (a >= 1f) { px[i] = c; return; }
            var d = px[i]; px[i] = new Color(d.r + (c.r - d.r) * a, d.g + (c.g - d.g) * a, d.b + (c.b - d.b) * a, 1f);
        }
        public void Rect(float x, float y, float w, float h, Color c, float a = 1f)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), x1 = Mathf.Max(x0 + 1, Mathf.CeilToInt(x + w)), y1 = Mathf.Max(y0 + 1, Mathf.CeilToInt(y + h));
            for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) Blend(xx, yy, c, a);
        }
        public void Circle(float cx, float cy, float r, Color c, float a = 1f)
        {
            for (int yy = Mathf.FloorToInt(cy - r); yy <= Mathf.CeilToInt(cy + r); yy++)
                for (int xx = Mathf.FloorToInt(cx - r); xx <= Mathf.CeilToInt(cx + r); xx++)
                    if (Sq(xx + 0.5f - cx) + Sq(yy + 0.5f - cy) <= r * r) Blend(xx, yy, c, a);
        }
        public void Line(float x0, float y0, float x1, float y1, float width, Color c, float a = 1f, float dashOn = 0f, float dashOff = 0f)
        {
            float len = Mathf.Sqrt(Sq(x1 - x0) + Sq(y1 - y0)); int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
            float rad = Mathf.Max(0.5f, width * 0.5f), period = dashOn + dashOff;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps, d = t * len;
                if (period > 0f && (d % period) >= dashOn) continue;
                Circle(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, rad, c, a);
            }
        }
        public void Poly(Vector2[] pts, Color c, float a = 1f)
        {
            float mnx = float.PositiveInfinity, mny = float.PositiveInfinity, mxx = float.NegativeInfinity, mxy = float.NegativeInfinity;
            foreach (var p in pts) { mnx = Mathf.Min(mnx, p.x); mny = Mathf.Min(mny, p.y); mxx = Mathf.Max(mxx, p.x); mxy = Mathf.Max(mxy, p.y); }
            for (int yy = Mathf.FloorToInt(mny); yy <= Mathf.CeilToInt(mxy); yy++)
                for (int xx = Mathf.FloorToInt(mnx); xx <= Mathf.CeilToInt(mxx); xx++)
                {
                    float px_ = xx + 0.5f, py = yy + 0.5f; bool inside = false;
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                        if ((pts[i].y > py) != (pts[j].y > py) && px_ < (pts[j].x - pts[i].x) * (py - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
                    if (inside) Blend(xx, yy, c, a);
                }
        }
        public void Star(float px_, float py, float r, Color c)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++) { float rr = (i & 1) != 0 ? r * 0.45f : r, a = i / 10f * Mathf.PI * 2f - Mathf.PI / 2f; pts[i] = new Vector2(px_ + Mathf.Cos(a) * rr, py + Mathf.Sin(a) * rr); }
            Poly(pts, c);
        }
    }
    static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);
    static Color Hsl(float h, float s, float l)
    {
        h = ((h % 360f) + 360f) % 360f / 360f;
        if (s <= 0f) return new Color(l, l, l);
        float q = l < 0.5f ? l * (1f + s) : l + s - l * s, p = 2f * l - q;
        System.Func<float, float> ch = t => { if (t < 0f) t += 1f; if (t > 1f) t -= 1f; if (t < 1f / 6f) return p + (q - p) * 6f * t; if (t < 0.5f) return q; if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f; return p; };
        return new Color(ch(h + 1f / 3f), ch(h), ch(h - 1f / 3f));
    }

    static Texture2D GarmentTex(GearCatalog.Cloth sp)
    {
        string key = sp.k + "|" + sp.c + "|" + sp.a + "|" + sp.b + "|" + sp.n + "|" + sp.f + "|" + sp.belt + "|" + sp.lanyard + "|" + sp.text + "|" + sp.sub + "|" + sp.hue;
        Texture2D tex;
        if (texCache.TryGetValue(key, out tex) && tex != null) return tex;
        const int W = 256, H = 256; const float s = W / 512f;       // the web paints at 512; pixel sizes scale with it
        var x = new Paint(W, H);
        string k = sp.k ?? "solid";
        switch (k)
        {
            case "stripes":
            {
                // horizontal bands; n is how many pairs across the height
                x.Fill(Hex(sp.a)); int n = sp.n > 0 ? sp.n : 8; float hh = H / (float)n;
                for (int i = 0; i < n; i++) x.Rect(0, i * hh, W, hh * (sp.f > 0f ? sp.f : 0.5f), Hex(sp.b));
                break;
            }
            case "check":
            {
                x.Fill(Hex(sp.a)); int n = sp.n > 0 ? sp.n : 10; float sz = W / (float)n;
                for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) if (((i + j) & 1) != 0) x.Rect(i * sz, j * sz, sz + 0.5f, sz + 0.5f, Hex(sp.b));
                break;
            }
            case "plaid":
            {
                x.Fill(Hex(sp.a)); int n = sp.n > 0 ? sp.n : 5; float sz = W / (float)n;
                for (int i = 0; i < n; i++) { x.Rect(i * sz, 0, sz * 0.45f, H, Hex(sp.b), 0.55f); x.Rect(0, i * sz, W, sz * 0.45f, Hex(sp.b), 0.55f); }
                var c2 = Hex(sp.c >= 0 ? sp.c : sp.b);
                for (int i = 0; i < n; i++) { x.Rect(i * sz + sz * 0.18f, 0, sz * 0.08f, H, c2, 0.9f); x.Rect(0, i * sz + sz * 0.18f, W, sz * 0.08f, c2, 0.9f); }
                break;
            }
            case "tiedye":
            {
                // a spiral of hue out of the centre, which is what a tie-dye is (the lettering on the
                // chest needs a font to raster and is not drawn here)
                float cx = W * 0.5f, cy = H * 0.55f;
                for (int yy = 0; yy < H; yy++) for (int xx = 0; xx < W; xx++)
                {
                    float dx = xx - cx, dy = yy - cy, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                    float t = r / W * 3.2f + a / (Mathf.PI * 2f) * 2.0f; t = t - Mathf.Floor(t);
                    x.Rect(xx, yy, 1, 1, Hsl(Mathf.Floor(t * 360f + sp.hue), 0.82f, (58f + Mathf.Sin(r / s * 0.09f) * 10f) / 100f));
                }
                break;
            }
            case "denim":
            {
                x.Fill(Hex(sp.c >= 0 ? sp.c : 0x3b5a8a));
                // weave noise, then the stitch lines that say "jeans"
                int nn = Mathf.RoundToInt(9000 * s * s);
                for (int i = 0; i < nn; i++) x.Rect(x.R() * W, x.R() * H, Mathf.Max(1f, 2 * s), Mathf.Max(1f, 1 * s), Color.white, x.R() * 0.10f);
                var st = Rgb(0xd9, 0xa4, 0x41);
                x.Line(W * 0.5f, 0, W * 0.5f, H, 2 * s, st, 1f, 6 * s, 5 * s);
                x.Line(0, H * 0.06f, W, H * 0.06f, 2 * s, st, 1f, 6 * s, 5 * s);
                if (sp.belt)
                {
                    x.Rect(0, 0, W, H * 0.09f, Rgb(0x4a, 0x2f, 0x16)); x.Rect(W * 0.47f, H * 0.015f, W * 0.06f, H * 0.06f, Rgb(0xc9, 0xa2, 0x27));
                    // a couple of leather pouches hanging off it
                    x.Rect(W * 0.12f, H * 0.08f, W * 0.14f, H * 0.14f, Rgb(0x6b, 0x44, 0x23)); x.Rect(W * 0.74f, H * 0.08f, W * 0.14f, H * 0.14f, Rgb(0x6b, 0x44, 0x23));
                }
                break;
            }
            case "stars":
            {
                x.Fill(Hex(sp.c >= 0 ? sp.c : 0x1c2a6b));
                for (int i = 0; i < 46; i++) x.Star(x.R() * W, x.R() * H, (6f + x.R() * 12f) * s, i % 3 != 0 ? Rgb(0xf4, 0xd3, 0x5e) : Color.white);
                for (int i = 0; i < 120; i++) x.Rect(x.R() * W, x.R() * H, Mathf.Max(1f, 2 * s), Mathf.Max(1f, 2 * s), Color.white, 0.7f);
                break;
            }
            case "chef":
            {
                x.Fill(Hex(0xf7f7f4));
                x.Rect(W * 0.5f - 1 * s, 0, 2 * s, H, Rgb(0xe6, 0xe6, 0xe0));                         // the front seam
                foreach (var bx in new[] { W * 0.40f, W * 0.60f }) for (int i = 0; i < 5; i++) x.Circle(bx, H * 0.16f + i * H * 0.15f, W * 0.022f, Rgb(0xd9, 0xd9, 0xd2));   // double-breasted buttons
                break;
            }
            case "armor":
            {
                // lacquered lames: red bands laced with dark cord
                x.Fill(Hex(sp.c >= 0 ? sp.c : 0x8a1c1c)); int n = 9; float hh = H / (float)n;
                for (int i = 0; i < n; i++)
                {
                    x.Rect(0, i * hh + hh * 0.72f, W, hh * 0.28f, Color.black, 0.42f);
                    for (int j = 0; j < 12; j++) x.Rect(j * W / 12f + W / 24f - 2 * s, i * hh + hh * 0.30f, 4 * s, hh * 0.40f, Rgb(0xe0, 0xb2, 0x3a));
                }
                break;
            }
            case "space":
            {
                x.Fill(Hex(0xf2f2f0)); var ln = Rgb(0xb9, 0xbd, 0xc3);
                foreach (var yy in new[] { 0.22f, 0.48f, 0.74f }) x.Line(0, H * yy, W, H * yy, 3 * s, ln);
                x.Line(W * 0.5f, 0, W * 0.5f, H, 3 * s, ln);
                // mission patch on the chest, flag on the arm
                x.Circle(W * 0.36f, H * 0.30f, W * 0.06f, Rgb(0x1f, 0x4e, 0x9c));
                x.Rect(W * 0.60f, H * 0.26f, W * 0.10f, H * 0.07f, Rgb(0xd2, 0x3c, 0x3c));
                break;
            }
            case "track":
            {
                // an eighties tracksuit: three loud panels on the diagonal
                x.Fill(Hex(sp.a >= 0 ? sp.a : 0xe94b9c));
                x.Poly(new[] { new Vector2(0, H * 0.30f), new Vector2(W, H * 0.55f), new Vector2(W, H * 0.72f), new Vector2(0, H * 0.47f) }, Hex(sp.b >= 0 ? sp.b : 0x2ec4b6));
                x.Poly(new[] { new Vector2(0, H * 0.50f), new Vector2(W, H * 0.75f), new Vector2(W, H * 0.82f), new Vector2(0, H * 0.57f) }, Hex(sp.c >= 0 ? sp.c : 0xffd23f));
                x.Rect(W * 0.5f - 3 * s, 0, 6 * s, H, Color.white);                                    // the zip
                break;
            }
            case "button":
            {
                x.Fill(Hex(sp.c >= 0 ? sp.c : 0xbfd7ee));
                x.Rect(W * 0.46f, 0, W * 0.08f, H, Color.white, 0.35f);                               // the placket
                for (int i = 0; i < 6; i++) x.Circle(W * 0.5f, H * 0.12f + i * H * 0.15f, W * 0.016f, Color.white);
                if (sp.lanyard)
                {
                    var dk = Rgb(0x1d, 0x1d, 0x1d);
                    x.Line(W * 0.40f, 0, W * 0.47f, H * 0.42f, 5 * s, dk); x.Line(W * 0.60f, 0, W * 0.53f, H * 0.42f, 5 * s, dk);
                    x.Rect(W * 0.44f, H * 0.42f, W * 0.12f, H * 0.16f, Color.white);
                    x.Rect(W * 0.44f, H * 0.42f, W * 0.12f, H * 0.04f, Rgb(0xd2, 0x3c, 0x3c));
                }
                break;
            }
            case "leather":
            {
                x.Fill(Hex(sp.c >= 0 ? sp.c : 0x5a3a1e));
                foreach (var xx in new[] { 0.20f, 0.50f, 0.80f }) x.Line(W * xx, 0, W * xx, H, 3 * s, Color.black, 0.45f);
                x.Line(0, H * 0.30f, W, H * 0.30f, 3 * s, Color.black, 0.45f);
                x.Rect(W * 0.32f, H * 0.14f, W * 0.10f, H * 0.06f, Rgb(0xc9, 0xa2, 0x27));          // wings badge
                x.Rect(W * 0.40f, 0, W * 0.20f, H * 0.10f, Rgb(0xe9, 0xe0, 0xc8));                   // the scarf at the collar
                break;
            }
            case "brocade":
            {
                x.Fill(Hex(sp.c >= 0 ? sp.c : 0x6b4a1e)); float s8 = W / 8f; var gold = Rgb(212, 175, 55);
                for (int i = 0; i < 8; i++) for (int j = 0; j < 8; j++)
                {
                    float cx = i * s8 + s8 / 2 + ((j & 1) != 0 ? s8 / 2 : 0), cy = j * s8 + s8 / 2;
                    x.Poly(new[] { new Vector2(cx, cy - s8 * 0.32f), new Vector2(cx + s8 * 0.22f, cy), new Vector2(cx, cy + s8 * 0.32f), new Vector2(cx - s8 * 0.22f, cy) }, gold, 0.75f);
                }
                x.Rect(W * 0.40f, 0, W * 0.20f, H * 0.12f, Rgb(0xf4, 0xef, 0xe4));                   // the cravat
                for (int i = 0; i < 4; i++) x.Circle(W * 0.5f, H * 0.20f + i * H * 0.17f, W * 0.02f, Rgb(0xc9, 0xa2, 0x27));
                break;
            }
            case "hawaii":
            {
                x.Fill(Hex(sp.c >= 0 ? sp.c : 0xf3e6c8));
                for (int i = 0; i < 22; i++)
                {
                    float px_ = x.R() * W, py = x.R() * H, r = W * 0.045f;
                    var petal = i % 2 != 0 ? Rgb(0xd9, 0x4a, 0x4a) : Rgb(0x2f, 0x8f, 0x6b);
                    for (int p = 0; p < 5; p++) { float a = p / 5f * Mathf.PI * 2f; x.Circle(px_ + Mathf.Cos(a) * r, py + Mathf.Sin(a) * r, r * 0.7f, petal); }
                    x.Circle(px_, py, r * 0.45f, Rgb(0xf4, 0xd3, 0x5e));
                }
                break;
            }
            default:
                x.Fill(Hex(sp.c >= 0 ? sp.c : 0x333333));
                break;
        }
        tex = new Texture2D(W, H, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4, name = "garment:" + k };
        tex.SetPixels(x.px); tex.Apply(true, false);
        // the mean of the map, for a trim colour when the outfit names none
        float r2 = 0f, g2 = 0f, b2 = 0f; for (int i = 0; i < x.px.Length; i += 16) { r2 += x.px[i].r; g2 += x.px[i].g; b2 += x.px[i].b; }
        int cnt = (x.px.Length + 15) / 16;
        texAvg[tex] = (Mathf.RoundToInt(r2 / cnt * 255f) << 16) | (Mathf.RoundToInt(g2 / cnt * 255f) << 8) | Mathf.RoundToInt(b2 / cnt * 255f);
        texCache[key] = tex; return tex;
    }
    static int GarmentAvg(Texture2D tex, float mul)
    {
        int avg; if (tex == null || !texAvg.TryGetValue(tex, out avg)) return 0x333333;
        System.Func<int, int> f = v => Mathf.Clamp(Mathf.RoundToInt(v * mul), 0, 255);
        return (f((avg >> 16) & 255) << 16) | (f((avg >> 8) & 255) << 8) | f(avg & 255);
    }
}
