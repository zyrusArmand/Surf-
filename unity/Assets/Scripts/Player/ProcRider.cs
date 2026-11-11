// The riders the web game builds instead of loading: one shared skeleton, dressed per animal
// from its CHARS entry (web index.html ~17405) exactly as buildChar (~17803) does. The body is
// the web's deformed sphere (torsoForm, 16134), the limbs are tubes swept through the joints
// every frame (sweepLimb, 18934), and everything that hangs off the head and the tail is the
// same sphere, box, cone or cylinder at the same place and in the same colour. The numbers
// here are the web's, in its pre-house units; HOUSE (17773) is applied once, in Housed().
//
// Frame: the body faces -z, +x is its shoulder line, the front foot is on the -x side, as in
// the web. Bootstrap turns the root by its stance yaw, so what is built here is only the body.
// Unity's rotation matrices are the same formulas as three.js's, so every position and every
// angle copies straight across; only the Euler ORDER has to be composed by hand (XYZ, and
// YXZ for the leg joints), which Rot() does.
using System.Collections.Generic;
using UnityEngine;

public static class ProcRider
{
    // ---- a character, as the web's CHARS spells it ----
    public class Spec
    {
        public string id, body, face = "none", ears = "none", tail = "none", eyes = "side";
        public int fur, dark, nose;
        public int muzzle = -1, belly = -1, bib = -1, blaze = -1, cheekCol = -1, earCol = -1, inner = -1, dewlapCol = -1, iris = -1, crest = -1,
                   drum = -1, band = -1, beak = -1, comb = -1, tailCol = -1, quillCol = -1, quillTip = -1, clawCol = -1, limbCol = -1, footCol = -1, handCol = -1;
        public float bellyW = 0.80f, bibS = 1f, earS = 1f, tailTilt = float.NaN, gloss = float.NaN, metal = 0f, beakLen = 0.32f, beakR = 0.10f;
        public bool whiskers, cheeks, dewlap, quills, spines, flipper, headPatch = true;
        public int claws, toes, fingers;
        public float[] build, headS, footShape, handShape, legProf, armProf;
        public float tall = 1.32f, neckLen = 1f, bodyW = 1f, bodyD = 1f, limb = 1f, limbLen = 1f, arm = float.NaN, armLen = 1f, foot = 1f, hand = float.NaN;
        public float legBlend = 0.16f, armBlend = 0.16f, socket = 1f, neckW = 1.02f;
    }

    // The eleven without a file of their own, transcribed from CHARS (web 17511-17752).
    static readonly Spec[] Specs = {
        new Spec { id = "corgi", body = "deep", fur = 0xd2842f, dark = 0xf6ecd8, nose = 0x201810, face = "long", ears = "corgi", tail = "bushy",
                   muzzle = 0xf6ecd8, bib = 0xf9f2e2, bibS = 0.84f, blaze = 0xf9f2e2, bodyD = 1.06f, tall = 1.16f, neckLen = 0.86f,
                   build = new[] { 1.0f, 0.92f, 1.0f }, limb = 1.04f, limbLen = 0.52f, arm = 1.00f, armLen = 0.56f, foot = 1.02f,
                   footCol = 0xf9f2e2, handCol = 0xf9f2e2, headS = new[] { 0.90f, 0.88f, 1.00f } },
        new Spec { id = "bunny", body = "upright", fur = 0xeee3cf, dark = 0xd5bfa0, nose = 0xdd8794, muzzle = 0xf7f1e4, belly = 0xfaf6ec,
                   face = "bunny", ears = "long", tail = "puff", whiskers = true, cheeks = true, cheekCol = 0xf7f1e4,
                   tall = 1.22f, neckLen = 0.70f, build = new[] { 0.92f, 0.98f, 0.96f }, limb = 0.94f, limbLen = 1.04f, arm = 0.70f, armLen = 0.72f,
                   foot = 1.30f, footShape = new[] { 0.90f, 0.85f, 1.42f }, hand = 0.76f, headS = new[] { 0.764f, 0.798f, 0.832f } },
        new Spec { id = "panda", body = "barrel", fur = 0xf4f1e6, dark = 0x191920, nose = 0x121218, face = "long", ears = "round", tail = "puff",
                   eyes = "patch", band = 0x191920, muzzle = 0xf4f1e6, tall = 1.24f, neckLen = 0.72f,
                   build = new[] { 1.10f, 0.98f, 1.12f }, limb = 1.16f, limbLen = 0.84f, arm = 1.10f, armLen = 0.86f, foot = 1.06f,
                   limbCol = 0x191920, footCol = 0x141419, handCol = 0x141419, headS = new[] { 1.178f, 1.138f, 1.036f } },
        new Spec { id = "otter", body = "lean", fur = 0x6f4b2e, dark = 0xd6c5a4, nose = 0x1e1610, bib = 0xd8c8a8, bibS = 0.70f, muzzle = 0xd8c8a8,
                   face = "long", ears = "round", earCol = 0x6a472b, earS = 0.62f, tail = "flat", whiskers = true, cheeks = true, cheekCol = 0xd8c8a8,
                   belly = 0x9c7854, tall = 1.50f, neckLen = 1.04f, build = new[] { 0.80f, 1.08f, 0.88f }, limb = 0.88f, limbLen = 0.80f, arm = 0.80f, armLen = 0.90f,
                   foot = 1.30f, footShape = new[] { 1.34f, 0.72f, 1.16f }, hand = 0.86f, headS = new[] { 0.696f, 0.666f, 0.725f } },
        new Spec { id = "raccoon", body = "lean", fur = 0x8b8375, dark = 0x2a2620, nose = 0x121216, muzzle = 0xe4ddd0, belly = 0xc8c0b0,
                   face = "long", ears = "round", tail = "ringed", eyes = "mask", whiskers = true, cheeks = true, cheekCol = 0xe8e2d6,
                   tall = 1.28f, neckLen = 0.92f, build = new[] { 0.94f, 0.98f, 1.02f }, limb = 0.92f, limbLen = 0.94f, arm = 0.88f, armLen = 1.02f, foot = 0.98f,
                   footCol = 0x26282f, handCol = 0x26282f, headS = new[] { 0.74f, 0.696f, 0.799f } },
        new Spec { id = "sloth", body = "lean", fur = 0x554836, dark = 0x322818, nose = 0x261e17, muzzle = 0xdccaa6, belly = 0x8e7a58,
                   face = "long", ears = "tuft", tail = "stub", eyes = "patch", cheeks = true, cheekCol = 0xd6c4a2,
                   tall = 1.22f, neckLen = 0.64f, build = new[] { 0.98f, 0.96f, 1.00f }, limb = 0.80f, limbLen = 0.92f, arm = 0.78f, armLen = 1.62f,
                   foot = 0.94f, hand = 0.70f, claws = 3, clawCol = 0x35291d, headS = new[] { 0.72f, 0.735f, 0.705f } },
        new Spec { id = "lizard", body = "deep", fur = 0x2b7a4c, dark = 0x155238, nose = 0x0d3524, iris = 0xe8bf28, crest = 0xdc9a1e, drum = 0x1c6446,
                   face = "lizard", ears = "hole", tail = "long", eyes = "reptile", tailTilt = 1.05f, gloss = 0.44f,
                   dewlap = true, dewlapCol = 0x2c8a58, belly = 0x63a86a, bellyW = 0.44f,
                   tall = 1.08f, neckLen = 0.94f, build = new[] { 0.90f, 0.84f, 1.10f }, limb = 0.92f, limbLen = 0.86f, arm = 0.84f, armLen = 0.94f, foot = 1.14f,
                   footShape = new[] { 1.28f, 0.76f, 1.24f }, hand = 1.06f, handShape = new[] { 1.20f, 0.76f, 1.20f },
                   claws = 4, clawCol = 0x123f2e, toes = 4, fingers = 4, headS = new[] { 0.764f, 0.747f, 0.967f } },
        new Spec { id = "penguin", body = "upright", fur = 0x20242c, dark = 0xf6f6f2, nose = 0xf2a01b, beak = 0xf2a01b, bib = 0xf7f5ee,
                   face = "beak", ears = "none", tail = "stub", eyes = "beady",
                   tall = 1.46f, neckLen = 0.66f, build = new[] { 0.98f, 1.08f, 1.00f }, limb = 0.84f, limbLen = 0.58f, arm = 0.88f, armLen = 1.02f, foot = 1.32f, hand = 1.0f,
                   footShape = new[] { 1.26f, 0.70f, 1.30f }, flipper = true, footCol = 0xf2a01b, headS = new[] { 0.807f, 0.825f, 0.842f } },
        new Spec { id = "porcupine", body = "barrel", fur = 0x4a3a28, dark = 0x2b2118, nose = 0x1a120c, muzzle = 0x6b5540, belly = 0x6d5742,
                   face = "long", ears = "small", tail = "stub", eyes = "beady", whiskers = true,
                   quills = true, quillCol = 0x241d14, quillTip = 0xf4e8ca,
                   tall = 1.14f, neckLen = 0.58f, headS = new[] { 1.098f, 1.098f, 1.366f },
                   build = new[] { 1.04f, 0.94f, 1.06f }, limb = 1.00f, limbLen = 0.74f, arm = 0.92f, armLen = 0.76f, foot = 1.02f,
                   footCol = 0x241d14, handCol = 0x241d14, claws = 3, clawCol = 0x1c150e },
        new Spec { id = "rex", body = "deep", fur = 0x4c7a3a, dark = 0x2f5226, nose = 0x1c3418, crest = 0xd8892a,
                   face = "rex", ears = "none", tail = "thick", eyes = "reptile", iris = 0xe0532a, spines = true, belly = 0xa8bd7a,
                   tall = 1.20f, neckLen = 0.80f, build = new[] { 0.96f, 0.98f, 1.10f }, limb = 1.20f, limbLen = 0.94f, arm = 0.58f, armLen = 0.42f, foot = 1.10f,
                   footShape = new[] { 1.14f, 0.88f, 1.30f }, claws = 3, clawCol = 0xe8e0cc, headS = new[] { 0.807f, 0.791f, 0.904f } },
        new Spec { id = "chicken", body = "barrel", fur = 0xf6efe2, dark = 0xd8c4a4, nose = 0xf0a51c, beak = 0xf0a51c, comb = 0xd8342c,
                   face = "beak", beakLen = 0.19f, beakR = 0.095f, headPatch = false, ears = "none",
                   tail = "fan", tailCol = 0xcfa871, eyes = "wide",
                   tall = 1.14f, neckLen = 1.26f, build = new[] { 0.98f, 1.00f, 1.00f }, limb = 0.84f, limbLen = 0.96f, arm = 0.92f, armLen = 0.80f, foot = 1.14f, hand = 1.0f,
                   footShape = new[] { 1.22f, 0.55f, 1.34f }, flipper = true, footCol = 0xf0a51c, claws = 3, clawCol = 0xf0a51c,
                   headS = new[] { 1.025f, 1.049f, 1.049f } },
    };

    public static bool Has(string id) { return Find(id) != null; }
    static Spec Find(string id) { foreach (var s in Specs) if (s.id == id) return s; return null; }

    // ---- the house proportions (web 17773-17802): head up by a third, torso down, limbs shorter and thicker ----
    static float Toward(float v, float t, float k) { return v + (t - v) * k; }
    static Spec Housed(Spec c)
    {
        var h = Clone(c);
        float[] b = c.build ?? new[] { 1f, 1f, 1f }, hs = c.headS ?? new[] { 1f, 1f, 1f };
        h.build = new[] { b[0], b[1], b[2] * 0.72f };
        h.headS = new[] { hs[0] * 1.34f, hs[1] * 1.34f, hs[2] * 1.34f };
        h.tall = Toward(c.tall, 1.05f, 0.55f);
        h.limb = Toward(c.limb, 1.35f, 0.55f);
        h.limbLen = c.limbLen * 0.88f;
        h.arm = Toward(float.IsNaN(c.arm) ? c.limb : c.arm, 1.10f, 0.55f);
        h.armLen = c.armLen * 0.90f;
        h.foot = c.foot * 1.15f;
        h.hand = float.IsNaN(c.hand) ? float.NaN : c.hand * 1.20f;
        return h;
    }
    static Spec Clone(Spec s)
    {
        return new Spec
        {
            id = s.id, body = s.body, face = s.face, ears = s.ears, tail = s.tail, eyes = s.eyes, fur = s.fur, dark = s.dark, nose = s.nose,
            muzzle = s.muzzle, belly = s.belly, bib = s.bib, blaze = s.blaze, cheekCol = s.cheekCol, earCol = s.earCol, inner = s.inner, dewlapCol = s.dewlapCol,
            iris = s.iris, crest = s.crest, drum = s.drum, band = s.band, beak = s.beak, comb = s.comb, tailCol = s.tailCol, quillCol = s.quillCol, quillTip = s.quillTip,
            clawCol = s.clawCol, limbCol = s.limbCol, footCol = s.footCol, handCol = s.handCol, bellyW = s.bellyW, bibS = s.bibS, earS = s.earS, tailTilt = s.tailTilt,
            gloss = s.gloss, metal = s.metal, beakLen = s.beakLen, beakR = s.beakR, whiskers = s.whiskers, cheeks = s.cheeks, dewlap = s.dewlap, quills = s.quills,
            spines = s.spines, flipper = s.flipper, headPatch = s.headPatch, claws = s.claws, toes = s.toes, fingers = s.fingers, build = s.build, headS = s.headS,
            footShape = s.footShape, handShape = s.handShape, legProf = s.legProf, armProf = s.armProf, tall = s.tall, neckLen = s.neckLen, bodyW = s.bodyW, bodyD = s.bodyD,
            limb = s.limb, limbLen = s.limbLen, arm = s.arm, armLen = s.armLen, foot = s.foot, hand = s.hand, legBlend = s.legBlend, armBlend = s.armBlend,
            socket = s.socket, neckW = s.neckW,
        };
    }

    // ---- the body form (web BODY 16124, torsoForm 16134) ----
    static readonly Dictionary<string, float[][]> Body = new Dictionary<string, float[][]> {
        { "barrel",  new[] { new[] { 1.30f, 1.16f, 1.34f, 0.86f }, new[] { 1.26f, 1.14f, 1.32f, 0.88f }, new[] { 0f, 0f, -0.02f, -0.03f }, new[] { 0.02f } } },
        { "deep",    new[] { new[] { 1.06f, 0.84f, 1.06f, 0.72f }, new[] { 1.20f, 0.98f, 1.44f, 0.94f }, new[] { 0.05f, 0.01f, -0.08f, -0.12f }, new[] { 0.05f } } },
        { "lean",    new[] { new[] { 1.00f, 0.76f, 1.06f, 0.66f }, new[] { 1.10f, 0.86f, 1.22f, 0.76f }, new[] { 0.03f, 0f, -0.05f, -0.08f }, new[] { 0.03f } } },
        { "squat",   new[] { new[] { 1.22f, 1.06f, 1.32f, 0.92f }, new[] { 0.96f, 0.86f, 1.02f, 0.74f }, new[] { 0f, 0f, -0.03f, -0.04f }, new[] { 0.01f } } },
        { "upright", new[] { new[] { 0.98f, 0.82f, 1.04f, 0.72f }, new[] { 1.04f, 0.90f, 1.18f, 0.82f }, new[] { 0.02f, 0f, -0.05f, -0.07f }, new[] { 0.03f } } },
    };
    static readonly float[] TorsoDef = { 1.06f, 1.03f, 0.935f, 0.76f };
    const float SkinSag = 0.0072949f;   // 1 - cos(pi/26)

    static float Smt(float u) { return u * u * (3 - 2 * u); }
    static float ProfAt(float[] A, float t)
    {
        int n = A.Length - 1;
        float g = Mathf.Min(n - 1e-4f, Mathf.Max(0f, t) * n);
        int i = Mathf.FloorToInt(g); float f = Smt(g - i);
        return A[i] * (1 - f) + A[i + 1] * f;
    }

    public class Form
    {
        public float TL, K; public float[] W, D, Z;
        public float w(float t) { return ProfAt(W, t); }
        public float d(float t) { return ProfAt(D, t); }
        public float zc(float t) { return ProfAt(Z, t); }
        public float y(float t) { return (t - 0.5f) * 0.92f * TL + 0.258f * TL + t * t * t * 0.06f; }
        public float r0(float t) { return 0.92f * Mathf.Sqrt(Mathf.Max(0f, t * (1 - t))); }
        public float hw(float t) { return r0(t) * w(t); }
        public float hd(float t) { return r0(t) * d(t); }
        public float tAt(float halfW) { for (float t = 0.55f; t <= 1.0f; t += 0.01f) if (hw(t) <= halfW) return t; return 1f; }
        // a point on the base sphere of radius 0.46 -> the body
        public Vector3 Put(float x, float yy, float z, out float t)
        {
            t = Mathf.Clamp01((yy + 0.46f) / 0.92f);
            float front = Mathf.Max(0f, -z / 0.46f);
            return new Vector3(x * w(t), y(t), z * d(t) + zc(t) - K * front * front * (1 - t * 0.55f));
        }
    }
    static Form TorsoForm(Spec C)
    {
        var F = new Form { TL = C.tall };
        float[][] A = Body.ContainsKey(C.body ?? "") ? Body[C.body] : null;
        if (A != null)
        {
            F.W = new float[4]; F.D = new float[4];
            for (int i = 0; i < 4; i++) { F.W[i] = A[0][i] * C.bodyW; F.D[i] = A[1][i] * C.bodyD; }
            F.Z = A[2]; F.K = A[3][0];
        }
        else
        {
            F.W = new float[4]; F.D = new float[4];
            for (int i = 0; i < 4; i++) { F.W[i] = TorsoDef[i] * 1.14f; F.D[i] = TorsoDef[i] * 0.86f; }
            F.Z = new[] { 0f, 0f }; F.K = 0f;
        }
        return F;
    }

    // ---- what Build hands back, and what ProcRig drives ----
    public class Leg { public Transform hip, knee, ankle, toe; public LimbTube tube; }
    public class Arm { public Transform sh, el, wr, tip; public LimbTube tube; }
    public class Ear { public Transform t; public Vector3 baseEuler; public float side; }
    public class Built
    {
        public Transform root, body, pelvis, spine, chest, neck, head, tail, hipMass;
        public Leg legF, legB; public Arm armF, armB;
        public readonly List<Ear> ears = new List<Ear>();
        public readonly List<LimbTube> tubes = new List<LimbTube>();
        public float legS = 1f; public float[] hipBuild = { 1f, 1f, 1f };
        // the seat, laid along whatever line the two hips are on (web layHips 16068)
        public void LayHips()
        {
            Vector3 hf = legF.hip.localPosition, hb = legB.hip.localPosition; float[] B = hipBuild;
            float dx = hb.x - hf.x, dz = hb.z - hf.z, span = Mathf.Sqrt(dx * dx + dz * dz);
            hipMass.localPosition = new Vector3((hf.x + hb.x) * 0.5f, (hf.y + hb.y) * 0.5f + 0.045f, (hf.z + hb.z) * 0.5f);
            hipMass.localRotation = Quaternion.AngleAxis(Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, Vector3.up);
            hipMass.localScale = new Vector3(0.245f * B[0], 0.235f * B[1], span * 0.5f + 0.115f * B[2]) * 2f;   // a unit sphere; the primitive is half that
        }
        public void Sweep() { foreach (var t in tubes) t.Sweep(); }
    }

    static readonly Dictionary<GameObject, Built> registry = new Dictionary<GameObject, Built>();
    public static Built Of(GameObject root) { Built b; return registry.TryGetValue(root, out b) ? b : null; }
    public static void Forget(GameObject root) { registry.Remove(root); }

    // ---- primitives, materials, rotations ----
    static Mesh sphereMesh, cubeMesh;
    static Mesh Prim(PrimitiveType t, ref Mesh cache)
    {
        if (cache != null) return cache;
        var tmp = GameObject.CreatePrimitive(t);
        cache = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tmp);
        return cache;
    }
    static Shader lit;
    static Shader Lit()
    {
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");
        return lit;
    }
    // the web's hex, through the same saturation push vivid() gives it (web 5495)
    public static Color Col(int hex, float vivid)
    {
        var c = new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
        float avg = (c.r + c.g + c.b) / 3f, k = 0.30f * vivid;
        c.r = Mathf.Clamp01(avg + (c.r - avg) * (1 + k)); c.g = Mathf.Clamp01(avg + (c.g - avg) * (1 + k)); c.b = Mathf.Clamp01(avg + (c.b - avg) * (1 + k));
        return c;
    }
    /// the web's PBR(): colour, roughness, metalness
    public static Material Mat(int hex, float rough, float vivid = 1f, float metal = 0.03f)
    {
        var m = new Material(Lit());
        m.SetColor("_BaseColor", Col(hex, vivid)); m.SetColor("_Color", Col(hex, vivid));
        m.SetFloat("_Smoothness", 1f - rough); m.SetFloat("_Metallic", metal);
        return m;
    }
    /// the web's mat(c, shininess)
    static Material MatSh(int hex, float sh) { return Mat(hex, Mathf.Max(0.06f, 1f - Mathf.Min(1f, sh / 150f))); }

    static Quaternion Rot(float x, float y, float z)   // three.js XYZ order
    {
        return Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);
    }
    public static Quaternion RotXYZ(float x, float y, float z) { return Rot(x, y, z); }
    public static Quaternion RotYXZ(float x, float y, float z)   // the leg joints' order
    {
        return Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);
    }

    static Transform Joint(Transform parent, string name, float x, float y, float z)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = new Vector3(x, y, z); return g.transform;
    }
    static Transform Part(Transform parent, string name, Mesh mesh, Material m, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = rot;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g.transform;
    }
    static Transform Sphere(Transform p, string n, float r, Material m, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        return Part(p, n, Prim(PrimitiveType.Sphere, ref sphereMesh), m, pos, scale * (2f * r), rot);
    }
    static Transform Sphere(Transform p, string n, float r, Material m, Vector3 pos, Vector3 scale) { return Sphere(p, n, r, m, pos, scale, Quaternion.identity); }
    static Transform Sphere(Transform p, string n, float r, Material m, Vector3 pos) { return Sphere(p, n, r, m, pos, Vector3.one, Quaternion.identity); }
    static Transform Box(Transform p, string n, Vector3 size, Material m, Vector3 pos, Quaternion rot)
    {
        return Part(p, n, Prim(PrimitiveType.Cube, ref cubeMesh), m, pos, size, rot);
    }
    /// the web's CylinderGeometry(rTop, rBottom, height): y is the axis, centred
    static Transform Cyl(Transform p, string n, float rTop, float rBot, float h, int seg, Material m, Vector3 pos, Quaternion rot)
    {
        return Part(p, n, Frustum(rTop, rBot, h, seg), m, pos, Vector3.one, rot);
    }
    static Transform Cone(Transform p, string n, float r, float h, int seg, Material m, Vector3 pos, Quaternion rot) { return Cyl(p, n, 0f, r, h, seg, m, pos, rot); }

    static Mesh Frustum(float rTop, float rBot, float h, int seg)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; v.Add(new Vector3(rTop * Mathf.Sin(a), h * 0.5f, rTop * Mathf.Cos(a))); }
        for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; v.Add(new Vector3(rBot * Mathf.Sin(a), -h * 0.5f, rBot * Mathf.Cos(a))); }
        int top = 0, bot = seg + 1;
        for (int i = 0; i < seg; i++)
        {
            tri.Add(bot + i); tri.Add(bot + i + 1); tri.Add(top + i);
            tri.Add(bot + i + 1); tri.Add(top + i + 1); tri.Add(top + i);
        }
        int ct = v.Count; v.Add(new Vector3(0, h * 0.5f, 0));
        int cb = v.Count; v.Add(new Vector3(0, -h * 0.5f, 0));
        for (int i = 0; i < seg; i++)
        {
            tri.Add(ct); tri.Add(top + i); tri.Add(top + i + 1);
            tri.Add(cb); tri.Add(bot + i + 1); tri.Add(bot + i);
        }
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // The torso: the web's 32x26 sphere pushed through the form (shapeTorso 16169), with the
    // smooth normal of a non-uniformly scaled sphere, as bodySkin computes it.
    static Mesh TorsoMesh(Form F)
    {
        const int W = 32, H = 26; const float R = 0.46f;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        for (int iy = 0; iy <= H; iy++)
        {
            float phi = iy / (float)H * Mathf.PI;
            for (int ix = 0; ix <= W; ix++)
            {
                float th = ix / (float)W * Mathf.PI * 2f;
                float x = -R * Mathf.Cos(th) * Mathf.Sin(phi), y = R * Mathf.Cos(phi), z = R * Mathf.Sin(th) * Mathf.Sin(phi);
                float t; v.Add(F.Put(x, y, z, out t));
                var nn = new Vector3(x / Mathf.Max(0.02f, F.w(t)), y / F.TL, z / Mathf.Max(0.02f, F.d(t)));
                n.Add(nn.sqrMagnitude > 1e-8f ? nn.normalized : Vector3.up);
            }
        }
        for (int iy = 0; iy < H; iy++) for (int ix = 0; ix < W; ix++)
        {
            int a = iy * (W + 1) + ix + 1, b = iy * (W + 1) + ix, c = (iy + 1) * (W + 1) + ix, d = (iy + 1) * (W + 1) + ix + 1;
            if (iy != 0) { tri.Add(a); tri.Add(b); tri.Add(d); }
            if (iy != H - 1) { tri.Add(b); tri.Add(c); tri.Add(d); }
        }
        var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0); m.RecalculateBounds();
        return m;
    }
    // A marking cut from the body's own skin (web bodyPatch 17909): the same surface between
    // two heights and within an angle of the belly line, a hair proud of it. The web feathers
    // the edge through vertex alpha; this one is cut clean.
    static Mesh PatchMesh(Form F, float t0, float t1, float halfAng)
    {
        const int Rows = 12, Cols = 28; const float R = 0.46f;
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int iy = 0; iy <= Rows; iy++)
        {
            float t = Mathf.Lerp(t0, t1, iy / (float)Rows), ys = t * 0.92f - R, r0 = Mathf.Sqrt(Mathf.Max(0f, R * R - ys * ys));
            for (int ix = 0; ix <= Cols; ix++)
            {
                float ang = Mathf.Lerp(-halfAng, halfAng, ix / (float)Cols);
                float tt; v.Add(F.Put(Mathf.Sin(ang) * r0, ys, -Mathf.Cos(ang) * r0, out tt));
            }
        }
        for (int iy = 0; iy < Rows; iy++) for (int ix = 0; ix < Cols; ix++)
        {
            int p00 = iy * (Cols + 1) + ix, p10 = p00 + 1, p01 = p00 + Cols + 1, p11 = p01 + 1;
            tri.Add(p00); tri.Add(p01); tri.Add(p10);
            tri.Add(p10); tri.Add(p01); tri.Add(p11);
        }
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // ---- the build ----
    const float PelvisY = 0.475f, StanceRot = -1.20f;
    public const float RiderFt = 1.9f;   // what Models.Spawn fits the files to

    public static GameObject Build(string id, Transform parent)
    {
        var spec = Find(id); if (spec == null) return null;
        var C = Housed(spec);
        var F = TorsoForm(C);
        float[] B = C.build, HS = C.headS;
        float L = C.limb, A = C.arm, K = C.socket;

        var rootGo = new GameObject(id); rootGo.transform.SetParent(parent, false);
        var bodyGo = new GameObject("Body"); bodyGo.transform.SetParent(rootGo.transform, false);
        var R = new Built { root = rootGo.transform, body = bodyGo.transform, legS = C.limbLen, hipBuild = B };
        Transform body = R.body;

        // ---- materials (web 16002, 17910-17913, 18049-18051) ----
        var fur = Mat(C.fur, float.IsNaN(C.gloss) ? 0.86f : C.gloss, 1.5f, C.metal);
        var dark = Mat(C.dark, 0.72f, 1.0f);
        var noseM = MatSh(C.nose, 90);
        var inner = Mat(C.inner >= 0 ? C.inner : 0xe79aa6, 0.6f);
        var lm = C.limbCol >= 0 ? Mat(C.limbCol, 0.66f, 1.2f) : fur;
        var footM = C.footCol >= 0 ? Mat(C.footCol, 0.45f, 1.5f) : lm;
        var handM = C.handCol >= 0 ? Mat(C.handCol, 0.45f, 1.5f) : lm;

        // ---- the skeleton (web 16054-16080, 16208) ----
        R.pelvis = Joint(body, "Pelvis", 0, PelvisY, 0.02f);
        R.hipMass = Sphere(R.pelvis, "HipMass", 1f, fur, Vector3.zero);
        R.spine = Joint(R.pelvis, "Spine", 0, 0.16f, 0);
        R.chest = Joint(R.spine, "Chest", 0, 0.30f, -0.04f);
        R.neck = Joint(R.chest, "Neck", 0, 0.30f, -0.06f);
        R.head = Joint(R.neck, "Head", 0, 0.20f, 0);
        Part(R.spine, "Torso", TorsoMesh(F), fur, Vector3.zero, new Vector3(B[0], B[1], B[2]), Quaternion.identity);
        Sphere(R.head, "Collar", 0.22f, fur, new Vector3(0, -0.16f, 0), new Vector3(1.15f * B[0], 1.0f * B[1], 1.1f * B[2]));
        Sphere(R.head, "Skull", 0.40f, fur, Vector3.zero);
        var neckMass = Sphere(R.neck, "NeckMass", 1f, fur, Vector3.zero);
        R.tail = Joint(R.pelvis, "Tail", 0, 0.22f, 0.30f);
        R.tail.localRotation = Quaternion.AngleAxis(-1.16f * Mathf.Rad2Deg, Vector3.up);

        // the feet in board space, turned into the side-on body's (web 19033-19059)
        float cs = Mathf.Cos(-StanceRot), sn = Mathf.Sin(-StanceRot);
        float fx0 = -0.04f * cs + -0.10f * sn, fz0 = 0.04f * sn + -0.10f * cs;
        float bx0 = 0.04f * cs + 0.50f * sn, bz0 = -0.04f * sn + 0.50f * cs;
        R.legF = MakeLeg(R, "F", fx0, fz0, fur, lm); R.legB = MakeLeg(R, "B", bx0, bz0, fur, lm);
        R.armF = MakeArm(R, "F", -0.44f, -0.10f, fur, lm); R.armB = MakeArm(R, "B", 0.44f, 0.06f, fur, lm);

        // ---- proportions (web 17918-17983) ----
        float legS = C.limbLen;
        foreach (var lg in new[] { R.legF, R.legB })
        {
            lg.knee.localPosition = new Vector3(0, -0.185f * legS, 0);
            lg.ankle.localPosition = new Vector3(0, -0.155f * legS, 0);
            lg.tube.prof = C.legProf ?? LimbTube.LegProfile; lg.tube.thick = L; lg.tube.blend = C.legBlend; lg.tube.Material = lm;
        }
        float FL = C.flipper ? 1.55f : 1f, FT = C.flipper ? 0.42f : 1f;
        float Fo = C.foot, H = float.IsNaN(C.hand) ? A : C.hand, AL = C.armLen;
        float[] FS = C.footShape ?? new[] { 1f, 1f, 1f }, HSh = C.handShape ?? new[] { 1f, 1f, 1f };
        var footMeshes = new List<Transform>(); var handMeshes = new List<Transform>();
        foreach (var lg in new[] { R.legF, R.legB })
        {
            var f = lg.ankle.Find("Foot");
            f.localScale = new Vector3(0.94f * L * Fo * FS[0], 0.62f * L * Fo * FS[1], 1.24f * L * Fo * FS[2]) * (2f * 0.152f);
            f.GetComponent<MeshRenderer>().sharedMaterial = footM; footMeshes.Add(f);
        }
        foreach (var ar in new[] { R.armF, R.armB })
        {
            var p = ar.wr.Find("Paw");
            p.localScale = new Vector3(1.0f * H * FL * HSh[0] / AL, 0.96f * H * HSh[1] / AL, 1.08f * H * FT * HSh[2] / AL) * (2f * 0.128f);
            p.GetComponent<MeshRenderer>().sharedMaterial = handM; handMeshes.Add(p);
            ar.sh.localScale = Vector3.one * AL;
            ar.tube.prof = C.armProf ?? LimbTube.ArmProfile; ar.tube.thick = A; ar.tube.blend = C.armBlend;
            ar.tube.flat = C.flipper ? new Vector2(0.44f, 1.50f) : Vector2.one; ar.tube.Material = lm;
        }
        // shoulders follow the body's width; hips stay where the feet land (web 17968-17979)
        float headHW = 0.40f * HS[0];
        float tSh = F.tAt(headHW * 1.30f);
        float shX = Mathf.Max(0.12f, F.hw(tSh) * B[0] * 0.86f) * C.socket;
        R.armF.sh.localPosition = new Vector3(-shX, 0.14f, -0.10f * B[2]);
        R.armB.sh.localPosition = new Vector3(shX, 0.14f, 0.06f * B[2]);
        R.legF.hip.localPosition = new Vector3(fx0 * K, 0.02f, fz0 * K);
        R.legB.hip.localPosition = new Vector3(bx0 * K, 0.02f, bz0 * K);
        R.LayHips();
        // the head on the shoulder line, and the throat filling what that leaves (web 17992-18023)
        float chestHW = 0f;
        for (float t = 0.30f; t <= 0.85f; t += 0.02f) chestHW = Mathf.Max(chestHW, F.hw(t));
        float tHead = F.tAt(chestHW * 0.36f);
        float headR = 0.40f * HS[1];
        float shoulderY = F.y(tHead) * B[1];
        float headY = shoulderY + headR * (0.90f + 0.20f * C.neckLen);
        R.neck.localPosition = new Vector3(0, Mathf.Max(0.06f, headY - 0.50f), -0.06f);
        {
            float nTop = 0.20f - headR * 0.80f, nBot = shoulderY - (0.30f + R.neck.localPosition.y) - 0.06f;
            float len = Mathf.Max(0.05f, nTop - nBot);
            neckMass.localPosition = new Vector3(0, (nTop + nBot) * 0.5f, 0);
            neckMass.localScale = new Vector3(0.225f * HS[0] * C.neckW * B[0], Mathf.Max(0.03f, len * 0.62f), 0.225f * HS[2] * C.neckW * B[2]) * 2f;
        }
        R.head.localScale = new Vector3(HS[0], HS[1], HS[2]);

        var hd = R.head;
        System.Func<float, float> headRZ = yy => 0.40f * HS[2] * Mathf.Sqrt(Mathf.Max(0.04f, 1 - Mathf.Pow(Mathf.Min(0.99f, Mathf.Abs(yy) / (0.40f * HS[1])), 2)));
        System.Func<float, float> bodyT = yy => Mathf.Clamp01((yy / B[1] - 0.258f * F.TL) / (0.92f * F.TL) + 0.5f);
        System.Action<float, float, Material, float, float> bodyPatch = (t0, t1, m, grow, halfAng) =>
            Part(R.spine, "Patch", PatchMesh(F, t0, t1, halfAng), m, Vector3.zero, new Vector3(B[0], B[1], B[2]) * grow, Quaternion.identity);

        KitEyes(C, hd, fur, dark);

        // ---- chest bib (web 18107-18125) ----
        if (C.bib >= 0)
        {
            var bm = Mat(C.bib, 0.62f, 1.2f);
            float bs = C.bibS, by = 0.30f + (1 - bs) * 0.22f, bh = 0.30f * 1.55f * bs;
            bodyPatch(bodyT(by - bh), bodyT(by + bh), bm, 1 + SkinSag + 0.012f, 0.86f);
            float hz = headRZ(-0.15f);
            Sphere(hd, "Bib", 1f, bm, new Vector3(0, -0.15f, -hz * 0.50f),
                   new Vector3(Mathf.Min(0.30f * 0.80f * bs, 0.40f * HS[0] * 0.70f), 0.30f * 0.58f * bs, hz * 0.54f));
        }
        // ---- quills (web 18130-18157) ----
        if (C.quills)
        {
            var qm = Mat(C.quillCol >= 0 ? C.quillCol : 0x2b2419, 0.42f, 1.1f);
            var tm = Mat(C.quillTip >= 0 ? C.quillTip : 0xf2e6c8, 0.5f, 1.2f);
            for (int ring = 0; ring < 6; ring++)
            {
                float t2 = 0.20f + ring * 0.135f;
                float y0 = t2 * 0.92f - 0.46f, r0 = Mathf.Sqrt(Mathf.Max(0.02f, 0.46f * 0.46f - y0 * y0));
                float yq = F.y(t2) * B[1];
                int n = 7 + ring * 2;
                for (int i = 0; i < n; i++)
                {
                    float a3 = (i / (float)(n - 1) - 0.5f) * Mathf.PI * 1.45f;
                    float tt; var q0 = F.Put(Mathf.Sin(a3) * r0, y0, Mathf.Cos(a3) * r0, out tt);
                    float x = q0.x * B[0], z = q0.z * B[2];
                    float L2 = 0.26f + Mathf.Sin(t2 * Mathf.PI) * 0.26f;
                    var dir = new Vector3(x, 0.30f, z).normalized;
                    var rot = Quaternion.FromToRotation(Vector3.up, dir);
                    var at = new Vector3(x * 0.94f, yq, z * 0.94f);
                    Cone(R.spine, "Quill", 0.030f, L2, 6, qm, at, rot);
                    Cone(R.spine, "QuillTip", 0.021f, L2 * 0.36f, 6, tm, at + dir * (L2 * 0.44f), rot);
                }
            }
        }
        // ---- toes, fingers, claws (web 18188-18215) ----
        if (C.toes > 0) foreach (var f in footMeshes) for (int i = 0; i < C.toes; i++)
        {
            float u = i - (C.toes - 1) / 2f;
            Sphere(f, "Toe", 0.062f, f.GetComponent<MeshRenderer>().sharedMaterial, new Vector3(u * 0.105f, 0.01f, -0.125f), new Vector3(0.85f, 1.0f, 1.5f));
        }
        if (C.fingers > 0) foreach (var h in handMeshes) for (int i = 0; i < C.fingers; i++)
        {
            float u = i - (C.fingers - 1) / 2f;
            Sphere(h, "Finger", 0.052f, h.GetComponent<MeshRenderer>().sharedMaterial, new Vector3(u * 0.086f, 0f, -0.100f), new Vector3(0.80f, 0.95f, 1.15f));
        }
        if (C.claws > 0)
        {
            var cm = Mat(C.clawCol >= 0 ? C.clawCol : 0x3b2f24, 0.38f, 1.1f);
            foreach (var h in handMeshes) for (int i = 0; i < C.claws; i++)
            {
                float a2 = (i - (C.claws - 1) / 2f) * 0.62f;
                Cone(h, "Claw", 0.036f, 0.300f, 7, cm, new Vector3(Mathf.Sin(a2) * 0.078f, -0.080f, -Mathf.Cos(a2) * 0.078f), Rot(-2.30f, 0, a2 * 0.35f));
            }
        }

        // ---- the face (web 18218-18404) ----
        if (C.face == "bunny")
        {
            Sphere(hd, "Muzzle", 0.16f, fur, new Vector3(0, -0.11f, -0.31f), new Vector3(1.2f, 0.80f, 0.95f));
            Sphere(hd, "Nose", 0.045f, noseM, new Vector3(0, -0.05f, -0.45f));
            var tooth = MatSh(0xfffdf6, 45);
            foreach (var sx in new[] { -1f, 1f }) Box(hd, "Tooth", new Vector3(0.045f, 0.07f, 0.03f), tooth, new Vector3(sx * 0.028f, -0.155f, -0.42f), Quaternion.identity);
        }
        else if (C.face == "lizard")
        {
            Sphere(hd, "Muzzle", 0.20f, fur, new Vector3(0, -0.06f, -0.42f), new Vector3(0.86f, 0.72f, 1.65f));
            Box(hd, "Jaw", new Vector3(0.26f, 0.045f, 0.44f), Mat(0x2c2118, 0.55f), new Vector3(0, -0.135f, -0.46f), Quaternion.identity);
            var nm = MatSh(0x1a2418, 60);
            foreach (var sx in new[] { -1f, 1f }) Sphere(hd, "Nostril", 0.024f, nm, new Vector3(sx * 0.05f, -0.02f, -0.66f));
            var crest = Mat(C.crest >= 0 ? C.crest : 0xf2a71b, 0.5f, 1.5f);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                Cone(hd, "Crest", 0.038f - 0.018f * t, 0.11f - 0.04f * t, 6, crest, new Vector3(0, 0.34f - t * 0.10f, -0.02f - t * 0.30f), Rot(-0.35f - t * 0.25f, 0, 0));
            }
        }
        else if (C.face == "rex")
        {
            Sphere(hd, "Skull2", 0.24f, fur, new Vector3(0, 0.02f, -0.44f), new Vector3(0.92f, 0.86f, 1.75f));
            Sphere(hd, "Jaw", 0.19f, fur, new Vector3(0, -0.16f, -0.46f), new Vector3(0.86f, 0.5f, 1.6f), Rot(0.13f, 0, 0));
            Box(hd, "Gum", new Vector3(0.30f, 0.05f, 0.60f), Mat(0x6d2a34, 0.55f), new Vector3(0, -0.085f, -0.52f), Quaternion.identity);
            var tooth = MatSh(0xfdfaf0, 70);
            for (int i = 0; i < 7; i++)
            {
                float z = -0.28f - i * 0.075f;
                foreach (var sx in new[] { -1f, 1f })
                {
                    Cone(hd, "ToothUp", 0.026f, 0.075f, 6, tooth, new Vector3(sx * 0.13f, -0.115f, z), Rot(Mathf.PI, 0, 0));
                    Cone(hd, "ToothLo", 0.022f, 0.062f, 6, tooth, new Vector3(sx * 0.12f, -0.145f, z), Quaternion.identity);
                }
            }
            Sphere(hd, "Brow", 0.10f, dark, new Vector3(0, 0.19f, -0.30f), new Vector3(1.8f, 0.5f, 0.9f));
        }
        else if (C.face == "beak")
        {
            float bl = C.beakLen, br = C.beakR;
            Cone(hd, "Beak", br, bl, 10, Mat(C.beak >= 0 ? C.beak : 0xf2a01b, 0.35f, 1.6f), new Vector3(0, -0.04f, -0.34f - bl * 0.5f), Rot(-Mathf.PI / 2f, 0, 0));
            if (C.headPatch) Sphere(hd, "HeadPatch", 0.17f, dark, new Vector3(0, 0.06f, -0.28f), new Vector3(1.25f, 1.05f, 0.55f));
        }
        else   // 'long': a proper snout
        {
            Sphere(hd, "Muzzle", 0.19f, C.muzzle >= 0 ? Mat(C.muzzle, 0.64f, 1.15f) : fur, new Vector3(0, -0.09f, -0.36f), new Vector3(1.0f, 0.82f, 1.25f));
            Sphere(hd, "Nose", 0.062f, noseM, new Vector3(0, -0.045f, -0.56f));
        }
        // ---- blaze, cheeks, dewlap (web 18430-18456) ----
        if (C.blaze >= 0)
        {
            var bm = Mat(C.blaze, 0.62f, 1.2f);
            Sphere(hd, "Blaze", 0.115f, bm, new Vector3(0, 0.02f, -0.375f), new Vector3(0.52f, 1.85f, 0.62f));
            Sphere(hd, "Blaze2", 0.10f, bm, new Vector3(0, -0.135f, -0.44f), new Vector3(1.25f, 0.70f, 1.05f));
        }
        if (C.cheeks)
        {
            var cmm = C.cheekCol >= 0 ? Mat(C.cheekCol, 0.66f, 1.2f) : fur;
            foreach (var sx in new[] { -1f, 1f })
                Sphere(hd, "Cheek", 0.145f, cmm, new Vector3(sx * 0.40f * HS[0] * 0.76f, -0.075f, -headRZ(-0.075f) * 0.33f), new Vector3(0.74f, 1.00f, 0.86f));
        }
        if (C.dewlap)
            Sphere(hd, "Dewlap", 0.20f, Mat(C.dewlapCol >= 0 ? C.dewlapCol : C.dark, 0.45f, 1.3f), new Vector3(0, -0.30f, -0.28f), new Vector3(0.30f, 1.05f, 0.85f), Rot(-0.30f, 0, 0));
        // ---- a lighter underside, and the panda's saddle (web 18483-18506) ----
        if (C.belly >= 0) bodyPatch(0.16f, 0.90f, Mat(C.belly, 0.64f, 1.2f), 1 + SkinSag + 0.012f, C.bellyW);
        if (C.band >= 0) bodyPatch(0.52f, 0.92f, Mat(C.band, 0.60f, 1.0f), 1 + SkinSag + 0.012f, Mathf.PI);
        // ---- comb and wattles (web 18511-18524) ----
        if (C.comb >= 0)
        {
            var cm = Mat(C.comb, 0.50f, 1.4f);
            for (int i = 0; i < 5; i++)
            {
                float t2 = i / 4f, rise = Mathf.Sin(t2 * Mathf.PI);
                Sphere(hd, "Comb", 0.048f + rise * 0.052f, cm, new Vector3(0, 0.355f + rise * 0.070f, -0.30f + t2 * 0.46f), new Vector3(0.40f, 1.30f, 0.95f));
            }
            foreach (var sx in new[] { -1f, 1f }) Sphere(hd, "Wattle", 0.062f, cm, new Vector3(sx * 0.052f, -0.215f, -0.325f), new Vector3(0.52f, 1.40f, 0.72f));
        }
        // ---- ears (web 18528-18629): registered so the rig can flap them ----
        foreach (var sx in new[] { -1f, 1f })
        {
            if (C.ears == "corgi")
            {
                EarAt(R, sx, Sphere(hd, "Ear", 0.135f, fur, new Vector3(0.230f, 0.455f, -0.01f), new Vector3(1.26f, 1.46f, 0.42f)), 0, 0, -0.17f);
                EarAt(R, sx, Sphere(hd, "EarIn", 0.135f, inner, new Vector3(0.230f, 0.432f, -0.050f), new Vector3(0.78f, 0.98f, 0.30f)), 0, 0, -0.17f);
            }
            else if (C.ears == "long")
            {
                EarAt(R, sx, Sphere(hd, "Ear", 0.085f, fur, new Vector3(0.15f, 0.60f, 0f), new Vector3(0.75f, 3.5f, 0.55f)), 0, 0, -0.13f);
                EarAt(R, sx, Sphere(hd, "EarIn", 0.055f, inner, new Vector3(0.15f, 0.60f, -0.048f), new Vector3(0.75f, 3.2f, 0.35f)), 0, 0, -0.13f);
            }
            else if (C.ears == "tuft")
                EarAt(R, sx, Sphere(hd, "Ear", 0.10f, fur, new Vector3(0.30f, 0.20f, 0.06f), new Vector3(0.75f, 1.15f, 0.75f)), 0, 0, 0);
            else if (C.ears == "hole")
            {
                var drum = Mat(C.drum >= 0 ? C.drum : 0x3f6a2a, 0.45f);
                EarAt(R, sx, Cyl(hd, "Ear", 0.075f, 0.075f, 0.004f, 16, drum, new Vector3(0.30f, 0.10f, -0.05f), Quaternion.identity), 0, 0, Mathf.PI / 2f);   // a disc facing out the side
            }
            else if (C.ears == "small")
                EarAt(R, sx, Cone(hd, "Ear", 0.10f, 0.18f, 4, dark, new Vector3(0.22f, 0.34f, -0.02f), Quaternion.identity), 0.55f, 0, -0.30f);
            else if (C.ears == "round")
            {
                var em = C.earCol >= 0 ? Mat(C.earCol, 0.70f, 1.4f) : dark;
                EarAt(R, sx, Sphere(hd, "Ear", 0.135f * C.earS, em, new Vector3(0.29f, 0.33f, 0.03f), new Vector3(1, 1, 0.55f)), 0, 0, 0);
            }
            // 'none': nothing
        }
        if (C.whiskers)
        {
            var wm = MatSh(0xfdf6e6, 45);
            foreach (var sx in new[] { -1f, 1f }) for (int i = 0; i < 3; i++)
                Cyl(hd, "Whisker", 0.005f, 0.003f, 0.30f, 5, wm, new Vector3(sx * 0.13f, -0.10f + i * 0.045f, -0.42f), Rot(0, 0, sx * (Mathf.PI / 2f - 0.20f + i * 0.16f)));
        }

        // ---- the tail (web 18643-18751) ----
        var tl = R.tail;
        if (C.tail == "bushy")
            Sphere(tl, "TailBush", 0.11f, fur, new Vector3(0, 0.13f, 0.02f), new Vector3(1, 1.9f, 1), Rot(-0.4f, 0, 0));
        else if (C.tail == "puff")
            Sphere(tl, "TailPuff", 0.125f, fur, new Vector3(0, 0.02f, 0.05f));
        else if (C.tail == "flat")
            Sphere(tl, "TailFlat", 0.13f, fur, new Vector3(0, 0.16f, 0.02f), new Vector3(0.9f, 2.1f, 0.42f), Rot(-0.55f, 0, 0));
        else if (C.tail == "stub")
            Sphere(tl, "TailStub", 0.085f, fur, new Vector3(0, 0.03f, 0.03f), new Vector3(1, 1.2f, 0.8f));
        else if (C.tail == "ringed")
        {
            var par = TailRoot(tl, float.IsNaN(C.tailTilt) ? 0.95f : C.tailTilt);
            for (int i = 0; i < 7; i++)
            {
                Cyl(par, "TailSeg", 0.058f - i * 0.004f, 0.066f - i * 0.004f, 0.16f, 12, (i % 2 == 1) ? dark : fur, new Vector3(0, 0.08f, 0), Quaternion.identity);
                var nx = Joint(par, "TailJ", 0, 0.16f, 0); nx.localRotation = Rot(-0.34f + i * 0.10f, 0, 0); par = nx;
            }
        }
        else if (C.tail == "long")
        {
            var par = TailRoot(tl, float.IsNaN(C.tailTilt) ? 0.85f : C.tailTilt);
            for (int i = 0; i < 7; i++)
            {
                Cyl(par, "TailSeg", 0.055f - i * 0.005f, 0.062f - i * 0.005f, 0.15f, 10, fur, new Vector3(0, 0.075f, 0), Quaternion.identity);
                var nx = Joint(par, "TailJ", 0, 0.15f, 0); nx.localRotation = Rot(-0.30f + i * 0.09f, 0, 0); par = nx;
            }
        }
        else if (C.tail == "thick")
        {
            var par = TailRoot(tl, float.IsNaN(C.tailTilt) ? 1.35f : C.tailTilt);
            var spineM = C.spines ? Mat(C.crest >= 0 ? C.crest : 0xd8892a, 0.5f, 1.4f) : null;
            for (int i = 0; i < 8; i++)
            {
                float r0 = 0.165f - i * 0.018f, r1 = 0.158f - i * 0.018f;
                var seg = Cyl(par, "TailSeg", Mathf.Max(0.012f, r1), Mathf.Max(0.016f, r0), 0.20f, 14, fur, new Vector3(0, 0.10f, 0), Quaternion.identity);
                var nx = Joint(par, "TailJ", 0, 0.20f, 0); nx.localRotation = Rot(-0.09f + i * 0.012f, 0, 0); par = nx;
                if (C.spines && i < 6) Cone(seg, "Spine", 0.035f, 0.09f, 6, spineM, new Vector3(0, 0.02f, -r0 - 0.02f), Rot(-Mathf.PI / 2f, 0, 0));
            }
        }
        else if (C.tail == "fan")
        {
            var yaw = Joint(tl, "TailYaw", 0, 0, 0); yaw.localRotation = Quaternion.AngleAxis(1.16f * Mathf.Rad2Deg, Vector3.up);
            var rootJ = Joint(yaw, "TailRoot", 0, 0, 0); rootJ.localRotation = Rot(float.IsNaN(C.tailTilt) ? -0.62f : C.tailTilt, 0, 0);
            var bs = Joint(rootJ, "TailBase", 0, 0.11f, 0.05f);
            var fm = C.tailCol >= 0 ? Mat(C.tailCol, 0.58f, 1.15f) : fur;
            for (int i = 0; i < 7; i++)
            {
                float t2 = i / 6f, off = (t2 - 0.5f) * 2f, len = 0.40f - Mathf.Abs(off) * 0.12f;
                var g2 = Joint(bs, "Feather", 0, 0, 0); g2.localRotation = Rot(-0.10f - Mathf.Abs(off) * 0.30f, 0, off * 0.46f);
                Sphere(g2, "FeatherBlade", len, fm, new Vector3(0, len * 0.94f, 0), new Vector3(0.155f, 1.0f, 0.42f));
            }
        }

        // ---- stood up to be measured, as sizeRider does through portraitPose (web 12480, 18874) ----
        foreach (var lg in new[] { R.legF, R.legB })
        {
            lg.hip.localRotation = RotYXZ(-0.05f, -StanceRot, 0); lg.knee.localRotation = RotYXZ(0.13f, 0, 0); lg.ankle.localRotation = RotYXZ(-0.08f, 0, 0);
        }
        R.armF.sh.localRotation = Rot(0.16f, 0, -0.30f); R.armF.el.localRotation = Rot(0.72f, 0, 0);
        R.armB.sh.localRotation = Rot(0.16f, 0, 0.30f); R.armB.el.localRotation = Rot(0.72f, 0, 0);
        R.Sweep();
        float drop = MeasureLegDrop(R);
        R.pelvis.localPosition = new Vector3(0, PelvisY + drop * (1 - 1 / R.legS), 0.02f);
        R.Sweep();
        Models.Fit(bodyGo, RiderFt);   // the same height the files are stood at, feet on the root's floor
        registry[rootGo] = R;
        return rootGo;
    }

    /// how far the ankles hang below the hips, in the body's frame, averaged over both legs (web updateLimbs 18989)
    public static float MeasureLegDrop(Built R)
    {
        float d2 = 0f;
        foreach (var lg in new[] { R.legF, R.legB })
            d2 += R.body.InverseTransformPoint(lg.hip.position).y - R.body.InverseTransformPoint(lg.ankle.position).y;
        return d2 * 0.5f;
    }

    static Transform TailRoot(Transform tailJ, float tilt)
    {
        if (tilt == 0f) return tailJ;
        var g = Joint(tailJ, "TailTilt", 0, 0, 0); g.localRotation = Rot(tilt, 0, 0); return g;
    }

    static void EarAt(Built R, float sx, Transform e, float rx, float ry, float rz)
    {
        var p = e.localPosition; p.x *= sx; e.localPosition = p;
        var eu = new Vector3(rx, ry * sx, rz * sx);
        e.localRotation = Rot(eu.x, eu.y, eu.z);
        R.ears.Add(new Ear { t = e, baseEuler = eu, side = sx });
    }

    // legs: hip -> knee -> ankle -> toe, one soft stub for a foot (web makeLeg 19004)
    static Leg MakeLeg(Built R, string tag, float xoff, float zoff, Material fur, Material lm)
    {
        var lg = new Leg();
        lg.hip = Joint(R.pelvis, "Hip" + tag, xoff, 0.02f, zoff);
        lg.knee = Joint(lg.hip, "Knee" + tag, 0, -0.185f, 0);
        lg.ankle = Joint(lg.knee, "Ankle" + tag, 0, -0.155f, 0);
        lg.toe = Joint(lg.ankle, "Toe" + tag, 0, -0.055f, -0.020f);
        Sphere(lg.ankle, "Foot", 0.152f, fur, new Vector3(0, -0.040f, -0.028f), new Vector3(0.94f, 0.62f, 1.24f));
        lg.tube = new LimbTube(R.body, "Leg" + tag, new[] { lg.hip, lg.knee, lg.ankle, lg.toe }, lm);
        R.tubes.Add(lg.tube);
        return lg;
    }
    // arms: shoulder -> elbow -> wrist -> tip, a mitten for a paw (web makeArm 19060)
    static Arm MakeArm(Built R, string tag, float xoff, float zoff, Material fur, Material lm)
    {
        var ar = new Arm();
        ar.sh = Joint(R.chest, "Shoulder" + tag, xoff, 0.14f, zoff);
        ar.el = Joint(ar.sh, "Elbow" + tag, 0, -0.225f, 0);
        ar.wr = Joint(ar.el, "Wrist" + tag, 0, -0.200f, 0);
        ar.tip = Joint(ar.wr, "Tip" + tag, 0, -0.060f, 0);
        Sphere(ar.wr, "Paw", 0.128f, fur, new Vector3(0, -0.030f, 0), new Vector3(1.0f, 0.96f, 1.08f));
        ar.tube = new LimbTube(R.body, "Arm" + tag, new[] { ar.sh, ar.el, ar.wr, ar.tip }, lm);
        R.tubes.Add(ar.tube);
        return ar;
    }

    // ---- eyes (web kitEyes 17302) ----
    static void KitEyes(Spec C, Transform hd, Material fur, Material darkMat)
    {
        var dark = MatSh(0x0d0d10, 90); var white = MatSh(0xf8f8f4, 60); var spark = MatSh(0xffffff, 120);
        float[] D = C.headS; float k = (D[0] + D[1] + D[2]) / 3f;
        foreach (var sx in new[] { -1f, 1f })
        {
            if (C.eyes == "mask")
            {
                Sphere(hd, "Mask", 0.155f, darkMat, new Vector3(sx * 0.095f, 0.075f, -0.305f), new Vector3(1.30f, 0.60f, 0.55f), Rot(0, 0, sx * 0.14f));
                Sphere(hd, "Brow", 0.085f, MatSh(0xe9e2d4, 40), new Vector3(sx * 0.155f, 0.215f, -0.300f), new Vector3(1.0f, 0.36f, 0.34f));
                Sphere(hd, "Eye", 0.058f, dark, new Vector3(sx * 0.145f, 0.075f, -0.375f));
                Sphere(hd, "Glint", 0.017f, MatSh(0xffffff, 120), new Vector3(sx * 0.127f, 0.105f, -0.395f));
            }
            else if (C.eyes == "patch")
            {
                Sphere(hd, "EyePatch", 0.145f, darkMat, new Vector3(sx * 0.175f, 0.055f, -0.315f), new Vector3(1.05f, 0.85f, 0.45f), Rot(0, 0, sx * 0.30f));
                Sphere(hd, "Eye", 0.055f, dark, new Vector3(sx * 0.175f, 0.055f, -0.365f));
            }
            else if (C.eyes == "reptile")
            {
                Sphere(hd, "Iris", 0.085f, Mat(C.iris >= 0 ? C.iris : 0xe8c33a, 0.28f, 1.4f, 0.1f), new Vector3(sx * 0.20f, 0.115f, -0.30f));
                Sphere(hd, "Pupil", 0.052f, dark, new Vector3(sx * 0.215f, 0.115f, -0.355f), new Vector3(0.34f, 1.25f, 1f));
            }
            else if (C.eyes == "beady")
                Sphere(hd, "Eye", 0.056f, dark, new Vector3(sx * 0.135f, 0.135f, -0.345f));
            else if (C.eyes == "wide")
            {
                Sphere(hd, "Sclera", 0.086f * k, white, new Vector3(sx * 0.125f * D[0], 0.10f * D[1], -0.30f), new Vector3(0.9f, 1.12f, 0.86f));
                Sphere(hd, "Pupil", 0.050f * k, dark, new Vector3(sx * 0.128f * D[0], 0.096f * D[1], -0.30f - 0.040f * k));
                Sphere(hd, "Glint", 0.016f * k, spark, new Vector3(sx * 0.108f * D[0], 0.128f * D[1], -0.30f - 0.062f * k));
            }
            else   // 'side'
                Sphere(hd, "Eye", 0.072f, dark, new Vector3(sx * 0.16f, 0.06f, -0.35f));
        }
    }
}

// A limb is ONE mesh swept along the joint chain every frame (web sweepLimb 18934): a tube
// through a Catmull-Rom curve from a point pushed up inside the trunk, through hip, knee and
// ankle, into the foot, with a radius profile that thickens at the thigh and nips at the knee.
public class LimbTube
{
    public const int LR = 15, LRAD = 12;
    public static readonly float[] LegProfile = { 0.205f, 0.190f, 0.152f, 0.163f, 0.126f, 0.104f };
    public static readonly float[] ArmProfile = { 0.180f, 0.152f, 0.122f, 0.133f, 0.104f, 0.088f };

    public float[] prof = LegProfile; public float thick = 1f, blend = 0.16f; public Vector2 flat = Vector2.one;
    public Material Material { set { renderer.sharedMaterial = value; } }

    readonly Mesh mesh; readonly MeshRenderer renderer; readonly Transform space; readonly Transform[] chain;
    readonly Vector3[] v = new Vector3[LR * LRAD + 2];
    readonly Vector3[] p = new Vector3[5];

    public LimbTube(Transform space, string name, Transform[] chain, Material m)
    {
        this.space = space; this.chain = chain;
        var go = new GameObject(name); go.transform.SetParent(space, false);
        mesh = new Mesh(); mesh.MarkDynamic();
        var idx = new List<int>();
        for (int i = 0; i < LR - 1; i++) for (int k = 0; k < LRAD; k++)
        {
            int a = i * LRAD + k, b = i * LRAD + (k + 1) % LRAD;
            idx.Add(a); idx.Add(a + LRAD); idx.Add(b);
            idx.Add(b); idx.Add(a + LRAD); idx.Add(b + LRAD);
        }
        int capA = LR * LRAD, capB = capA + 1, o = (LR - 1) * LRAD;
        for (int k = 0; k < LRAD; k++)
        {
            idx.Add(capA); idx.Add(k); idx.Add((k + 1) % LRAD);
            idx.Add(capB); idx.Add(o + (k + 1) % LRAD); idx.Add(o + k);
        }
        mesh.vertices = v; mesh.SetTriangles(idx, 0);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = m;
    }

    static float Smooth(float u) { return u * u * (3 - 2 * u); }
    static float Sample(float[] P, float t)
    {
        float g = Mathf.Min(P.Length - 1.001f, t * (P.Length - 1)); int i = Mathf.FloorToInt(g); float f = Smooth(g - i);
        return P[i] * (1 - f) + P[i + 1] * f;
    }
    // uniform Catmull-Rom through the five points, the ends reflected as three.js does
    Vector3 Curve(float t)
    {
        float g = Mathf.Clamp01(t) * 4f; int i = Mathf.Min(3, Mathf.FloorToInt(g)); float u = g - i;
        Vector3 p0 = i > 0 ? p[i - 1] : p[0] * 2f - p[1], p1 = p[i], p2 = p[i + 1], p3 = i + 2 <= 4 ? p[i + 2] : p[4] * 2f - p[3];
        return 0.5f * ((2f * p1) + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (u * u) + (-p0 + 3f * p1 - 3f * p2 + p3) * (u * u * u));
    }

    public void Sweep()
    {
        for (int i = 0; i < 4; i++) p[i + 1] = space.InverseTransformPoint(chain[i].position);
        p[0] = p[1] + (p[1] - p[2]).normalized * blend;
        var n = new Vector3(1, 0, 0);
        for (int i = 0; i < LR; i++)
        {
            float t = i / (float)(LR - 1);
            var w = Curve(t);
            var tg = (Curve(t + 1e-3f) - Curve(t - 1e-3f));
            if (tg.sqrMagnitude < 1e-12f) tg = Vector3.down; else tg.Normalize();
            n -= tg * Vector3.Dot(n, tg);
            if (n.sqrMagnitude < 1e-6f) n = new Vector3(0, 0, 1) - tg * tg.z;
            n.Normalize();
            var b = Vector3.Cross(tg, n);
            float edge = Mathf.Min(1f, t * 9f + 0.18f) * Mathf.Min(1f, (1 - t) * 4.2f + 0.72f);
            float r = Sample(prof, t) * thick * edge;
            for (int k = 0; k < LRAD; k++)
            {
                float a = k / (float)LRAD * Mathf.PI * 2f, c = Mathf.Cos(a) * r * flat.x, d = Mathf.Sin(a) * r * flat.y;
                v[i * LRAD + k] = w + n * c + b * d;
            }
        }
        v[LR * LRAD] = Curve(0f); v[LR * LRAD + 1] = Curve(1f);
        mesh.vertices = v;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
