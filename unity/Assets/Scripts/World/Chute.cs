// The drop: the web game's chute of falling water, number for number (web 3815-3966, 4074,
// 15363-15990, 44898-44900, 45175-45178, 46176-46216, 47168-47193). A test-only set piece
// there, armed from the workshop menu and nowhere else, and summoned here the same way.
//   IN:   four seconds. The look comes up over the first 0.9 s and the sea goes glassy with it
//         (Calm = 1 - 0.94 w); the push arrives with it: 46 ft/s on top of whatever the board
//         was doing, past the ordinary ceiling. The lane squeezes from 17 to the cliff line.
//   RIDE: ninety-five seconds between the walls: basalt either side at 9.9 ft with a skirt of
//         white water at the waterline, a head fall and its plume held 168 ft ahead so there is
//         always a gorge to be in, and ten wall falls streaming past and recycling. Walls,
//         skirt, head and plume all scroll their water DOWN, tied to his speed.
//   OUT:  2.8 s: the look fades, the push and the walls go with it, and the lane is the lane.
// What the chute does to the WATER -- the web's fallDip, the descent pitched at 0.47 rise over
// run ahead of him, anchored so his own height stays near zero, banked up at the lane edges --
// is WaveField.FallDip, inside Height and Swell and drawn by the ocean shader; this sets its
// inputs every frame. The gorge goes down with it (web 15647-15693): every column of the rock
// and the waterline skirt sits at the water's own height at its z, the head fall's foot stands
// -Slope*HeadZ down, and the wall falls hang from the dropped rim as they stream past.
using System.Collections.Generic;
using UnityEngine;

public class Chute : MonoBehaviour
{
    public BoardController rider;
    public ChaseCamera chase;
    public Spawner spawner;
    public SetWave setWave;
    public Whirlpool whirl;        // optional: a whirlpool already running would ride into the gorge with him (web 15844)
    public Island island;          // optional: not while the fork has him
    public System.Action<Vector3, int> onSplash;

    // ---- the web's numbers ----
    public const float In = 4.0f, Ride = 95.0f, Out = 2.8f;   // about a hundred seconds door to door
    public const float Push = 46f;              // the speed the chute adds
    public const float Lane = 9.0f;             // where the walls stand, just outside the steering
    public const float WallH = 70f;             // the rock
    public const float HeadZ = 168f, HeadW = 52f, HeadH = 104f;
    public const float CalmK = 0.94f;           // how much of the swell the chute takes out
    public const float Pitch = 0.16f;           // radians the shot tips at full weight (kept as the web keeps it; FALL_CAM is what the camera uses)
    public const float TiltK = 0.0f;            // the board's own nose-down, zero: the hull's pitch already does it
    // the descent's gradient (web FALL_SLOPE); the profile itself is WaveField.FallDip
    public const float Slope = 0.47f;
    public const float LookUp = 0.9f;           // seconds for the look to come up on the way in

    // ---- public state ----
    public int Ph = -1;            // -1 off, 0 dropping in, 1 in the chute, 2 running out (web fallPh)
    public float T;                // the phase clock
    public float W;                // how far into the chute look, 0..1 (web fallW)
    public float Travel;           // how far down the fall he has come, feet (web fallTravel)
    public float Lay, Tilt;        // the prone weight (zero, as the web leaves it) and the board's extra nose-down (zero-weighted)
    public bool summon;            // debug: go now
    public bool CastRocks = true;  // the chute's own rack (rock, log, ramp). The web's later gate (46181) leaves the drop empty: false matches that.
    public bool Busy => Ph >= 0;   // the set wave and the whirlpool wait

    // ---- private ----
    GameObject grp; Material skirtL, skirtR, headMat, plumeMat; Transform head, plume;
    float clock, zf0; bool calmSet;

    // A grid laid along one wall in the group's frame (no rotation: x across, z along the lane).
    // Its columns are dropped to the water at their own z on each refresh -- the web bakes the
    // same per-column drop into the geometry once (fallBuild's _P0 loop); here it is read off the
    // live field, so it also carries the weight and the bank and it levels with the water on the
    // way out instead of leaving the walls hanging below a sea that has come back up.
    class Sheet { public Mesh mesh; public Vector3[] base0, v; public int nu, nv; public float faceX; public bool lit; }
    readonly List<Sheet> sheets = new List<Sheet>();
    float sheetW = -1f, sheetGx, sheetAge;

    // one of the wall falls: four corners, its two ends each on the water below them (web fallSide)
    class Fall { public Mesh mesh; public Material mat; public int sx; public float z, w, h, top, rate; public readonly Vector3[] v = new Vector3[4]; }
    readonly List<Fall> sides = new List<Fall>();

    // =====================================================================================
    public void Reset()
    {
        WaveField.FallW = 0f; WaveField.FallT = 0f;
        End();
        W = 0; Lay = 0; Tilt = 0; summon = false; calmSet = false;
        Apply();
    }

    /// the web's fallGo: arm the drop; everything after is on the clock
    public void Go()
    {
        var r = rider;
        if (r == null || !r.running || r.wipe || Ph >= 0) return;
        if (island != null && island.Busy) return;
        Build();
        // whatever was already on the water would ride into it; a barrel or a whirlpool already running likewise
        if (spawner != null) spawner.ClearObstacles();
        if (setWave != null && setWave.ph != SetWave.Phase.Idle) setWave.Reset();
        if (whirl != null) whirl.Reset();
        Ph = 0; T = 0; Travel = 0;
        if (grp != null) grp.SetActive(true);
        r.Say("THE DROP!");
    }

    /// the web's fallEnd
    public void End()
    {
        Ph = -1; T = 0; W = 0; Lay = 0; Tilt = 0; Travel = 0;
        if (grp != null) grp.SetActive(false);
        if (spawner != null) spawner.holdCast = false;
        Apply();
    }

    /// the web's fallDip: the descent, pivoted on him and anchored at his own position, with the
    /// trough banked up at the lane's edge. In feet, on the field coordinate the sea is written in.
    public float Dip(float x, float fieldZ) => WaveField.FallDip(x, fieldZ);

    // =====================================================================================
    public void Step(float dt)
    {
        var r = rider; if (r == null) return;
        clock += dt;
        if (summon) { summon = false; Go(); }
        if (Ph < 0)
        {
            // the tilt comes off the board when the chute does, eased rather than snapped
            if (W > 0.001f) { W = Mathf.Max(0f, W - dt * 0.8f); Lay = W; }
            if (Tilt != 0f) { Tilt += (0f - Tilt) * Mathf.Min(1f, dt * 4.0f); if (Mathf.Abs(Tilt) < 0.002f) Tilt = 0f; }
            Apply();
            return;
        }
        T += dt;
        if (Ph == 0)
        {
            // full strength almost at once: the entry is the distance closing, not the effect fading in
            W = Mathf.Min(1f, T / LookUp);
            if (T >= In) { Ph = 1; T = 0; }
        }
        else if (Ph == 1) { W = 1f; if (T >= Ride) { Ph = 2; T = 0; } }
        else { W = Mathf.Max(0f, 1f - T / Out); if (T >= Out) { End(); return; } }
        Lay = 0f;                                   // standing: on a pitched deck a prone rider is behind his own board
        // the pivot rides with him, in field coordinates, and he moves down it at his own speed
        Travel += Mathf.Max(0f, r.eff) * dt;
        zf0 = WaveField.Scroll + r.pz + Travel;
        // the board's nose-down off the water itself, zero-weighted (web FALL_TILTK = 0)
        {
            float zf = WaveField.Scroll + r.pz;
            float ah = WaveField.Height(r.px, zf - 7f, r.t), bh = WaveField.Height(r.px, zf + 7f, r.t);
            float want = Mathf.Min(0.45f, Mathf.Atan2(bh - ah, 14f) * W * TiltK);
            Tilt += (want - Tilt) * Mathf.Min(1f, dt * 5.0f);
        }
        Apply();
        Scenery(dt, r);
    }

    /// what the ride, the rack and the sea read off the chute
    void Apply()
    {
        var r = rider;
        if (r != null)
        {
            r.chutePush = Push * W;
            if (W > 0.001f) r.laneLim = 17f - (17f - Lane + 1.2f) * W;       // the walls are walls (web 44898-44902)
            else if (Ph < 0 && calmSet) r.laneLim = 17f;
        }
        if (spawner != null)
        {
            spawner.chuteW = W;
            if (Ph >= 0) spawner.holdCast = !CastRocks;                      // the web's _inDrop gate; the rock rack otherwise
        }
        // the chop comes out of the field the board reads as well as the water on screen (web _fallCalm)
        WaveField.FallW = W; WaveField.FallZF = zf0; WaveField.FallT = Travel; WaveField.FallLane = Lane;   // the sea tips down the fall
        if (W > 0.001f) { WaveField.Calm = 1f - W * CalmK; calmSet = true; }
        else if (calmSet) { calmSet = false; if (setWave == null || setWave.ph == SetWave.Phase.Idle) WaveField.Calm = 1f; }
    }

    // =====================================================================================
    // ---- the gorge: rock, the skirt of white water, the head fall and its plume, the wall falls ----
    void Build()
    {
        if (grp != null) return;
        grp = new GameObject("Chute"); grp.SetActive(false);
        var cliff = CliffTex();
        var lit = Shader.Find("Universal Render Pipeline/Lit"); if (lit == null) lit = Shader.Find("Standard");
        for (int s = -1; s <= 1; s += 2)
        {
            // the rock: a grid pushed out by layered noise, never in past its own plane, its rim broken
            var rock = new GameObject(s < 0 ? "rockL" : "rockR", typeof(MeshFilter), typeof(MeshRenderer)); rock.transform.SetParent(grp.transform, false);
            var rs = RockMesh(s); sheets.Add(rs);
            rock.GetComponent<MeshFilter>().sharedMesh = rs.mesh;
            var rm = new Material(lit); rm.SetTexture("_BaseMap", cliff); rm.SetTextureScale("_BaseMap", new Vector2(7f, 6f));
            rm.SetFloat("_Smoothness", 0.05f); rm.SetFloat("_Metallic", 0f); rm.SetFloat("_Cull", 0f);
            var rr = rock.GetComponent<MeshRenderer>(); rr.sharedMaterial = rm; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the skirt: spray where the river meets the stone, eighteen feet at the waterline,
            // 84 by 3 like the web's so its columns can follow the water down the gorge
            var ks = SkirtMesh(s); sheets.Add(ks);
            var skm = Sheer(s < 0 ? "skirtL" : "skirtR", grp.transform, ks.mesh, FallTex(), new Vector2(2f, 6f), 0.86f);
            if (s < 0) skirtL = skm; else skirtR = skm;
        }
        // the head of the gorge; Scenery stands its foot -Slope*HeadZ down, where the web's is
        head = Quad("head", grp.transform, new Vector3(0f, HeadH * 0.5f - 6f, -HeadZ), new Vector3(HeadW, HeadH, 1f), 0, FallTex(), new Vector2(3.0f, 2.2f), 0.93f, out headMat).transform;
        plume = Quad("plume", grp.transform, new Vector3(0f, HeadH * 0.22f, -HeadZ + 26f), new Vector3(HeadW * 1.5f, HeadH * 0.85f, 1f), 0, MistTex(), Vector2.one, 0.62f, out plumeMat).transform;
        // and water comes off the walls: ten narrow falls hung from the rim, scrolling and recycling
        for (int i = 0; i < 10; i++)
        {
            var fl = new Fall { sx = (i % 2 == 1) ? 1 : -1, rate = 1f };
            fl.mesh = new Mesh { name = "sidefall" }; fl.mesh.MarkDynamic();
            fl.mesh.vertices = fl.v;
            fl.mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            fl.mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            fl.mat = Sheer("sidefall", grp.transform, fl.mesh, FallTex(), new Vector2(1f, 2.0f), 0.88f);
            sides.Add(fl);
            SideLay(fl, true);
        }
    }

    // where one of the wall falls hangs, and how big: re-rolled every time it recycles (web fallSideLay).
    // Hung from a top edge, top - h to top, measured up from the water under it (SidePlace).
    void SideLay(Fall fl, bool fresh)
    {
        fl.w = 9f + Random.value * 16f; fl.h = 46f + Random.value * 54f;
        fl.top = 26f + Random.value * 30f;
        fl.z = fresh ? (-30f - Random.value * 260f) : (-250f - Random.value * 90f);
        var c = fl.mat.GetColor("_BaseColor"); c.a = 0.78f + Random.value * 0.20f; fl.mat.SetColor("_BaseColor", c);
        fl.rate = 0.7f + Random.value * 0.9f;
    }

    // ...and where it is this frame. The web hangs these at a fixed height over y = 0, which put the
    // far ones forty feet clear of a rim that had gone sixty feet down with the river; each end of
    // this one stands on the water below it instead, so the fall shears with the gorge it is on.
    void SidePlace(Fall fl, float gx, float fz0)
    {
        float x = fl.sx * (Lane + 0.55f), z0 = fl.z - fl.w * 0.5f, z1 = fl.z + fl.w * 0.5f;
        float d0 = WaveField.FallDip(gx + x, fz0 + z0), d1 = WaveField.FallDip(gx + x, fz0 + z1);
        fl.v[0] = new Vector3(x, fl.top - fl.h + d0, z0); fl.v[1] = new Vector3(x, fl.top - fl.h + d1, z1);
        fl.v[2] = new Vector3(x, fl.top + d0, z0);        fl.v[3] = new Vector3(x, fl.top + d1, z1);
        fl.mesh.vertices = fl.v; fl.mesh.RecalculateBounds();
    }

    // every column of a wall grid onto the water at its z and at the wall's own x (bank included)
    static void Refresh(Sheet s, float gx, float fz0)
    {
        int row = s.nu + 1;
        for (int i = 0; i <= s.nu; i++)
        {
            float dy = WaveField.FallDip(gx + s.faceX, fz0 + s.base0[i].z);
            for (int j = 0; j <= s.nv; j++) { int k = j * row + i; var b = s.base0[k]; s.v[k] = new Vector3(b.x, b.y + dy, b.z); }
        }
        s.mesh.vertices = s.v;
        if (s.lit) s.mesh.RecalculateNormals();
        s.mesh.RecalculateBounds();
    }

    void Scenery(float dt, BoardController r)
    {
        if (grp == null) return;
        // no gorge on the way in: open sea with an edge across it, the walls only once he is over it
        bool show = Ph >= 1 && W > 0.01f;
        if (show && !grp.activeSelf) sheetW = -1f;        // whatever the walls last stood on is stale
        grp.SetActive(show);
        float gx = r.px * 0.35f, fz0 = WaveField.Scroll + r.pz;   // group x; the field coordinate of the group's z = 0
        grp.transform.position = new Vector3(gx, 0f, r.pz);
        float eff = r.eff;
        // The profile is fixed relative to him (the web's whole reason for baking it), so the walls
        // only need redoing when the weight or his lateral offset moves it; the timed refresh keeps
        // them honest if the profile ever gets a term that travels (WaveField's ledges and knee).
        sheetAge += dt;
        if (show && (sheetW < 0f || Mathf.Abs(W - sheetW) > 0.002f || Mathf.Abs(gx - sheetGx) > 0.3f || sheetAge > 0.25f))
        {
            sheetW = W; sheetGx = gx; sheetAge = 0f;
            for (int i = 0; i < sheets.Count; i++) Refresh(sheets[i], gx, fz0);
        }
        Fade(skirtL, 0.90f * W); Fade(skirtR, 0.90f * W);
        Scroll(skirtL, -dt * (0.55f + eff * 0.016f)); Scroll(skirtR, -dt * (0.55f + eff * 0.016f));
        // The head's foot at the web's -FALL_SLOPE*FALL_HEAD_Z (web 15759), weighted so it comes back up
        // with the water on the way out. That is past FallDip's 140 ft clamp, so the foot is some
        // thirteen feet under the pool, as in the web; the quad's lower six feet are below it anyway.
        float foot = -Slope * HeadZ * W;
        if (head != null) head.localPosition = new Vector3(0f, foot + HeadH * 0.5f - 6f, -HeadZ);
        if (plume != null) plume.localPosition = new Vector3(0f, foot + HeadH * 0.22f, -HeadZ + 26f);
        Fade(headMat, 0.93f * W); Scroll(headMat, -dt * (0.85f + eff * 0.010f));
        float br = 0.52f + 0.16f * Mathf.Sin(clock * 0.55f) + 0.08f * Mathf.Sin(clock * 1.31f + 1.1f);
        Fade(plumeMat, br * W);
        if (plumeMat != null) { var o = plumeMat.GetTextureOffset("_BaseMap"); o.x = Mathf.Sin(clock * 0.07f) * 0.04f; plumeMat.SetTextureOffset("_BaseMap", o); }
        // the wall falls stream past at his speed -- the same speed Travel advances at, so they keep
        // pace with the water under them -- and recycle far ahead
        for (int i = 0; i < sides.Count; i++)
        {
            var fl = sides[i];
            fl.z += eff * dt;
            if (fl.z > 30f) SideLay(fl, false);
            if (show) SidePlace(fl, gx, fz0);
            Scroll(fl.mat, -dt * (1.15f + eff * 0.02f) * fl.rate);
            var c = fl.mat.GetColor("_BaseColor"); c.a = Mathf.Min(c.a, 0.92f); fl.mat.SetColor("_BaseColor", c);
        }
    }

    static void Fade(Material m, float a) { if (m == null) return; var c = m.GetColor("_BaseColor"); c.a = a; m.SetColor("_BaseColor", c); }
    static void Scroll(Material m, float dy) { if (m == null) return; var o = m.GetTextureOffset("_BaseMap"); o.y += dy; m.SetTextureOffset("_BaseMap", o); }

    // a textured, transparent, double-sided quad; sx != 0 turns it to face across the lane
    static GameObject Quad(string name, Transform parent, Vector3 at, Vector3 size, int sx, Texture2D tex, Vector2 tile, float alpha, out Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); var col = q.GetComponent<Collider>(); if (col != null) Destroy(col);
        q.name = name; q.transform.SetParent(parent, false);
        q.transform.localPosition = at; q.transform.localScale = size;
        q.transform.localRotation = sx == 0 ? Quaternion.identity : Quaternion.Euler(0, sx > 0 ? -90f : 90f, 0);
        mat = Dive.Unlit(new Color(1f, 1f, 1f, alpha), true, false, true);
        mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", tile);
        var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return q;
    }

    // the same textured, transparent, double-sided water on a mesh of its own, laid in the group's frame
    static Material Sheer(string name, Transform parent, Mesh mesh, Texture2D tex, Vector2 tile, float alpha)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mat = Dive.Unlit(new Color(1f, 1f, 1f, alpha), true, false, true);
        mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", tile);
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return mat;
    }

    // the waterline skirt (web fallBuild's mk(sx, false)): 84 by 3, 300 long and 18 tall, centred 96
    // ahead at 1 down, at sx*(Lane+0.2). Built level; Refresh drops each column to the water.
    static Sheet SkirtMesh(int sx)
    {
        const int NU = 84, NV = 3;
        var v = new Vector3[(NU + 1) * (NV + 1)]; var uv = new Vector2[v.Length];
        for (int j = 0; j <= NV; j++) for (int i = 0; i <= NU; i++)
        {
            float u = i / (float)NU, w = j / (float)NV;
            int k = j * (NU + 1) + i;
            v[k] = new Vector3(sx * (Lane + 0.2f), -1f + (w - 0.5f) * 18f, -96f + (u - 0.5f) * 300f);
            uv[k] = new Vector2(u, w);
        }
        var idx = new int[NU * NV * 6]; int n = 0;
        for (int j = 0; j < NV; j++) for (int i = 0; i < NU; i++)
        {
            int a = j * (NU + 1) + i, b = a + 1, c = a + NU + 1, d = c + 1;
            idx[n++] = a; idx[n++] = c; idx[n++] = b; idx[n++] = b; idx[n++] = c; idx[n++] = d;   // double-sided material: winding is moot
        }
        var m = new Mesh { name = sx < 0 ? "skirtL" : "skirtR" }; m.MarkDynamic();
        m.vertices = v; m.uv = uv; m.triangles = idx; m.RecalculateBounds();
        return new Sheet { mesh = m, base0 = v, v = (Vector3[])v.Clone(), nu = NU, nv = NV, faceX = sx * (Lane + 0.2f), lit = false };
    }

    // the rock face (web fallBuild's mk(sx, true)): 84 by 20, 300 long and WallH tall, centred 96 ahead
    // at 26 up, its face at sx*(Lane+0.9). The relief retreats outward by up to 8.5 ft, the rim is
    // pulled down raggedly over its top 38%. Built level; Refresh drops each column to the water at
    // its z, which is the web's baked drop (-FALL_SLOPE*d clamped to -FALL_BEHIND..FALL_AHEAD).
    static Sheet RockMesh(int sx)
    {
        const int NU = 84, NV = 20;
        var v = new Vector3[(NU + 1) * (NV + 1)]; var uv = new Vector2[v.Length]; var cols = new Color[v.Length];
        float h2(float a, float b) { float s = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
        float vn(float a, float b)
        {
            float i = Mathf.Floor(a), j = Mathf.Floor(b), u = a - i, w = b - j, su = u * u * (3 - 2 * u), sw = w * w * (3 - 2 * w);
            return (h2(i, j) * (1 - su) + h2(i + 1, j) * su) * (1 - sw) + (h2(i, j + 1) * (1 - su) + h2(i + 1, j + 1) * su) * sw;
        }
        float fb(float a, float b) => vn(a, b) * 0.58f + vn(a * 2.3f + 11f, b * 2.1f + 7f) * 0.28f + vn(a * 5.3f + 23f, b * 4.7f + 3f) * 0.14f;
        for (int j = 0; j <= NV; j++) for (int i = 0; i <= NU; i++)
        {
            float u = i / (float)NU, w = j / (float)NV;
            float f = fb(u * 11.0f, w * 3.4f);
            float y = (w - 0.5f);
            if (w > 0.62f) { float k = (w - 0.62f) / 0.38f; y -= k * k * (0.05f + fb(u * 6.0f + 31.0f, 2.7f) * 0.22f); }
            float prot = 1.0f - f, sh = Mathf.Clamp(0.68f + 0.44f * prot + 0.22f * w, 0.62f, 1.22f);
            int k2 = j * (NU + 1) + i;
            v[k2] = new Vector3(sx * (Lane + 0.9f + f * 8.5f), 26f + y * WallH, -96f + (u - 0.5f) * 300f);
            uv[k2] = new Vector2(u, w); cols[k2] = new Color(sh, sh * 0.99f, sh * 0.96f, 1f);
        }
        var idx = new int[NU * NV * 6]; int n = 0;
        for (int j = 0; j < NV; j++) for (int i = 0; i < NU; i++)
        {
            int a = j * (NU + 1) + i, b = a + 1, c = a + NU + 1, d = c + 1;
            if (sx > 0) { idx[n++] = a; idx[n++] = c; idx[n++] = b; idx[n++] = b; idx[n++] = c; idx[n++] = d; }
            else { idx[n++] = a; idx[n++] = b; idx[n++] = c; idx[n++] = b; idx[n++] = d; idx[n++] = c; }
        }
        var m = new Mesh { name = sx < 0 ? "rockL" : "rockR" }; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; m.MarkDynamic();
        m.vertices = v; m.uv = uv; m.colors = cols; m.triangles = idx; m.RecalculateNormals(); m.RecalculateBounds();
        return new Sheet { mesh = m, base0 = v, v = (Vector3[])v.Clone(), nu = NU, nv = NV, faceX = sx * (Lane + 0.9f), lit = true };
    }

    // ---- the textures, drawn the way the web draws them on a canvas ----
    // ropes of water with clear ground between them, wrapped so the tile has no seam (web fallTex)
    static Texture2D FallTex()
    {
        const int Wd = 128, Ht = 512;
        var px = new Color[Wd * Ht];
        void Rope(float xx, float w, float y0, float h, float a, Color col)
        {
            int x0 = Mathf.FloorToInt(xx), x1 = Mathf.CeilToInt(xx + w);
            for (int y = Mathf.FloorToInt(y0); y < y0 + h; y++)
            {
                int yy = ((y % Ht) + Ht) % Ht;
                for (int x = x0; x < x1; x++)
                {
                    if (x < 0 || x >= Wd) continue;
                    float t = (x + 0.5f - xx) / w, g = 1f - Mathf.Abs(t - 0.5f) * 2f;   // rounded across its width
                    float al = Mathf.Clamp01(g) * a * col.a;
                    var c = px[yy * Wd + x];
                    px[yy * Wd + x] = new Color(Mathf.Lerp(c.r, col.r, al), Mathf.Lerp(c.g, col.g, al), Mathf.Lerp(c.b, col.b, al), c.a + al * (1f - c.a));
                }
            }
        }
        for (int i = 0; i < 26; i++) Rope(Random.value * Wd, 5f + Random.value * 16f, Random.value * Ht, 120f + Random.value * 380f, 0.55f + Random.value * 0.40f, new Color(1f, 1f, 1f, 0.96f));
        for (int i = 0; i < 90; i++) Rope(Random.value * Wd, 1f + Random.value * 4f, Random.value * Ht, 40f + Random.value * 240f, 0.12f + Random.value * 0.40f,
                                          Random.value < 0.5f ? new Color(1f, 1f, 1f, 0.9f) : new Color(216 / 255f, 240 / 255f, 251 / 255f, 0.9f));
        var t2 = new Texture2D(Wd, Ht, TextureFormat.RGBA32, true) { name = "fall", wrapMode = TextureWrapMode.Repeat };
        t2.SetPixels(px); t2.Apply(true, true);
        return t2;
    }

    // the mist at the foot of the head fall: soft everywhere, thinner toward the top (web mistTex)
    static Texture2D MistTex()
    {
        const int N = 256;
        var px = new Color[N * N];
        for (int i = 0; i < 40; i++)
        {
            float bx = N * (0.12f + Random.value * 0.76f), by = N * (0.30f + Random.value * 0.66f), r = N * (0.10f + Random.value * 0.26f);
            int x0 = Mathf.Max(0, (int)(bx - r)), x1 = Mathf.Min(N - 1, (int)(bx + r)), y0 = Mathf.Max(0, (int)(by - r)), y1 = Mathf.Min(N - 1, (int)(by + r));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by)) / r; if (d >= 1f) continue;
                float a = 0.28f * (1f - d); var c = px[y * N + x]; px[y * N + x] = new Color(1f, 1f, 1f, c.a + a * (1f - c.a));
            }
        }
        for (int y = 0; y < N; y++)
        {
            float fade = 1f - Mathf.Clamp01((y / (float)N) / 0.55f);   // row 0 is the top of the canvas: cut away toward it
            for (int x = 0; x < N; x++) { var c = px[y * N + x]; c.a *= (1f - fade); px[y * N + x] = c; }
        }
        // the canvas's top is the texture's last row
        var flipped = new Color[N * N]; for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) flipped[(N - 1 - y) * N + x] = px[y * N + x];
        var t2 = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "mist", wrapMode = TextureWrapMode.Clamp };
        t2.SetPixels(flipped); t2.Apply(true, true);
        return t2;
    }

    // lit stone, banded horizontally with the bands broken along their length, fractured,
    // blotched where it weathers, grained and pitted (web cliffTex)
    static Texture2D CliffTex()
    {
        const int Wd = 256, Ht = 1024;
        var px = new Color[Wd * Ht];
        var baseC = new Color(0x6f / 255f, 0x65 / 255f, 0x55 / 255f);
        for (int i = 0; i < px.Length; i++) px[i] = baseC;
        void Rect(float x, float y, float w, float h, Color c, float a)
        {
            int x0 = Mathf.Max(0, (int)x), x1 = Mathf.Min(Wd, (int)(x + w)), y0 = Mathf.Max(0, (int)y), y1 = Mathf.Min(Ht, (int)(y + h));
            for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) px[yy * Wd + xx] = Color.Lerp(px[yy * Wd + xx], c, a);
        }
        void Blob(float bx, float by, float r, Color c, float a)
        {
            int x0 = (int)(bx - r), x1 = (int)(bx + r), y0 = Mathf.Max(0, (int)(by - r)), y1 = Mathf.Min(Ht - 1, (int)(by + r));
            for (int yy = y0; yy <= y1; yy++) for (int xx = x0; xx <= x1; xx++)
            {
                float d = Mathf.Sqrt((xx - bx) * (xx - bx) + (yy - by) * (yy - by)) / r; if (d >= 1f) continue;
                int wx = ((xx % Wd) + Wd) % Wd;                   // wrapped across, so it tiles
                px[yy * Wd + wx] = Color.Lerp(px[yy * Wd + wx], c, a * (1f - d));
            }
        }
        var dark = new Color(0x3d / 255f, 0x36 / 255f, 0x2c / 255f); var pale = new Color(0x8c / 255f, 0x82 / 255f, 0x71 / 255f);
        for (int i = 0; i < 150; i++)
        {
            float y = Random.value * Ht, h = 8f + Random.value * 52f, a = 0.18f + Random.value * 0.40f;
            var c = Random.value < 0.5f ? dark : pale;
            float xx = 0;
            while (xx < Wd) { float w = 20f + Random.value * 92f; if (Random.value < 0.78f) Rect(xx, y + Random.value * 8f, w, h, c, a); xx += w + Random.value * 28f; }
        }
        var frac = new Color(0x4a / 255f, 0x42 / 255f, 0x39 / 255f);
        for (int i = 0; i < 34; i++) Rect(Random.value * Wd, Random.value * Ht, 2f + Random.value * 6f, 80f + Random.value * 360f, frac, 0.20f + Random.value * 0.30f);
        for (int i = 0; i < 46; i++) Blob(Random.value * Wd, Random.value * Ht, 18f + Random.value * 72f, new Color(168 / 255f, 155 / 255f, 128 / 255f), 0.06f + Random.value * 0.14f);
        for (int i = 0; i < 38; i++) Blob(Random.value * Wd, Random.value * Ht, 14f + Random.value * 56f, new Color(58 / 255f, 50 / 255f, 40 / 255f), 0.07f + Random.value * 0.13f);
        for (int i = 0; i < 22; i++) Blob(Random.value * Wd, Random.value * Ht, 12f + Random.value * 40f, new Color(96 / 255f, 112 / 255f, 62 / 255f), 0.06f + Random.value * 0.12f);
        for (int i = 0; i < px.Length; i++) { float n = (Random.value - 0.5f) * 26f / 255f; var c = px[i]; px[i] = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n), 1f); }
        var pit = new Color(0x15 / 255f, 0x13 / 255f, 0x0f / 255f); var lip = new Color(0x8b / 255f, 0x82 / 255f, 0x71 / 255f);
        for (int i = 0; i < 420; i++)
        {
            float bx = Random.value * Wd, by = Random.value * Ht, r = 1f + Random.value * 5f;
            Blob(bx, by, r, pit, 0.10f + Random.value * 0.30f);
            Blob(bx, by - r * 0.55f, r * 0.72f, lip, 0.08f + Random.value * 0.22f);
        }
        var t2 = new Texture2D(Wd, Ht, TextureFormat.RGBA32, true) { name = "cliff", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        t2.SetPixels(px); t2.Apply(true, true);
        return t2;
    }
}
