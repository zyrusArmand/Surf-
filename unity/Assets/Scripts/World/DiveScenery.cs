// What there is to see under the water, laid out as the web lays it: rocks and coral heads on
// the sand (and four arches you can swim through), clumps of kelp rooted in it and swaying, and
// turtles and rays going about their business in the column. Everything is born out past the
// fog and recycled behind him; the rocks and the arches are solid and end the run (web 41398).
// The web's dive shark (dshk) is not here. The turtle's bone-driven stroke is not either: the
// files play as they come. Models are the web's own, under Resources/Models.
using System.Collections.Generic;
using UnityEngine;

public class DiveScenery : MonoBehaviour
{
    public BoardController rider;

    // ---- the floor (web FLOOR_KINDS) ----
    class Kind { public string file; public int n; public float ftA, ftB, bias, sinkA, sinkB, tilt; public bool tunnel; public float rad, wid, dep; public bool missing; }
    class Prop { public Kind k; public Transform t; public float ft, sink, hit, top, halfW, halfD, gap, mid, arch, yaw; }
    static readonly Kind[] Kinds = {
        new Kind { file = "searock", n = 12, ftA = 2.4f, ftB = 13.0f, bias = 1.9f, sinkA = 0.06f, sinkB = 0.34f, tilt = 0.20f },
        new Kind { file = "coral1", n = 11, ftA = 2.0f, ftB = 8.6f, bias = 1.6f, sinkA = 0.03f, sinkB = 0.18f, tilt = 0.13f },
        new Kind { file = "coral2", n = 11, ftA = 2.0f, ftB = 8.6f, bias = 1.6f, sinkA = 0.03f, sinkB = 0.18f, tilt = 0.13f },
        new Kind { file = "coraltunnel", n = 4, ftA = 4.0f, ftB = 6.8f, bias = 1.3f, sinkA = 0.02f, sinkB = 0.10f, tilt = 0.05f, tunnel = true } };
    const float ArchGap = 0.085f, ArchMid = 0.024f, ArchTop = 0.60f, ArchMin = 2.30f;
    const float FloorBand = 72f, FloorHit = 0.62f, FloorClear = 0.92f;
    const float Ahead = -200f, Behind = 30f, Spread = 46f, Born = Dive.Born;
    readonly List<Prop> props = new List<Prop>();

    // ---- the kelp ----
    class Kelp { public Transform t, model; public float ft, dig, yaw, lean, leanA, ph, sway; }
    const int KelpMax = 11; const float KelpBand = 70f, KelpBehind = 34f, KelpSpread = 30f, KelpLane = 2.6f;
    const float KelpH = 1.173f, KelpW = 1.90f, KelpThinMin = 1.15f, KelpThinMax = 1.85f, KelpTop = 1.0f, KelpMinFt = 5.0f, KelpTall = 1.15f;
    const float KelpDigMin = 0.04f, KelpDigMax = 0.26f, KelpDeep = 0.34f, KelpDigDeepA = 0.34f, KelpDigDeepB = 0.62f;
    readonly List<Kelp> kelps = new List<Kelp>(); bool kelpMissing;

    // ---- the animals ----
    class Body { public Transform t, model; public float ft, hx, hy, hz, hyT, ph, rate, roll, yaw, pitch, halfH; public bool sit; public GltfClips clips; }
    const int TurtMax = 13; const float TurtSit = 0.34f, TurtGapMin = 0.9f, TurtGapMax = 3.6f, TurtBehind = 38f, TurtSpread = 27f, TurtLane = 3.4f, TurtFtMin = 1.7f, TurtFtMax = 3.9f;
    const int RayMax = 5; const float RaySit = 0.40f, RayGapMin = 2.6f, RayGapMax = 7.0f, RayBehind = 40f, RayAheadCull = -330f, RaySpread = 13f, RayLane = 3.0f, RayFtMin = 3.4f, RayFtMax = 7.6f, RaySpdMin = 7.0f, RaySpdMax = 25.0f;
    // the files' own facing is kept; if a turtle or a ray swims tail first, turn this one by 180
    const float TurtFace = 0f, RayFace = 0f;
    readonly List<Body> turtles = new List<Body>(), rays = new List<Body>();
    float turtT = 2.0f, rayT = 2.0f; bool turtMissing, rayMissing;

    Transform root; bool wasOn;

    void Ensure()
    {
        if (root != null) return;
        root = new GameObject("DiveScenery").transform; root.SetParent(transform, false);
    }

    /// lay everything fresh, as the web does whenever the dive is not on
    public void Relay()
    {
        foreach (var p in props) LayProp(p, true);
        foreach (var k in kelps) LayKelp(k, true);
        foreach (var b in turtles) LayTurtle(b, 18f);
        foreach (var b in rays) LayRay(b, 18f);
        turtT = 1.2f; rayT = 1.2f;
    }

    public void Show(bool on) { Ensure(); if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on); }

    /// on: the scenery shows (web diveOn || depth > 0.5); diveOn: the rocks can hit him
    public void Step(float dt, bool on, bool diveOn, float eff, float camZ, float fogFar)
    {
        Ensure();
        var r = rider;
        Show(on);
        if (!on) { if (wasOn) Relay(); wasOn = false; return; }
        wasOn = true;
        float distM = r != null ? r.dist : 0f;
        // ---- the floor, filled in as the files allow ----
        foreach (var k in Kinds)
        {
            if (k.missing) continue;
            int have = 0; foreach (var p in props) if (p.k == k) have++;
            while (have < k.n) { var p = MakeProp(k); if (p == null) break; props.Add(p); LayProp(p, true); have++; }
        }
        foreach (var p in props)
        {
            var pos = p.t.position; pos.z += eff * dt;
            if (pos.z > Behind) { p.t.position = pos; LayProp(p, false); continue; }
            pos.y = Dive.BedH(pos.x, pos.z, distM) - p.sink;
            p.t.position = pos;
            if (!FogCull(p.t, camZ, fogFar)) continue;
            // ...and you can hit it: tested against his position, with height, because up is a direction down here
            if (r != null && diveOn && r.running && !r.wipe && p.hit > 0f)
            {
                float dx = r.px - pos.x, dz = r.pz - pos.z;
                if (p.k.tunnel)
                {
                    // the arch, tested in its own frame: world to local is the transpose of its yaw
                    float c = Mathf.Cos(p.yaw), sn = Mathf.Sin(p.yaw);
                    float lx = c * dx - sn * dz, lz = sn * dx + c * dz;
                    if (Mathf.Abs(lx) < p.halfW * FloorHit && Mathf.Abs(lz) < p.halfD * FloorHit && r.py < pos.y + p.top * FloorClear)
                    {
                        bool through = p.arch >= ArchMin && Mathf.Abs(lx - p.mid) < p.gap && r.py < pos.y + p.arch;
                        if (!through) r.Wipeout("rock");
                    }
                }
                else if (dx * dx + dz * dz < p.hit * p.hit && r.py < pos.y + p.top * FloorClear) r.Wipeout("rock");
            }
        }
        // ---- the kelp ----
        while (!kelpMissing && kelps.Count < KelpMax) { var k = MakeKelp(); if (k == null) break; kelps.Add(k); LayKelp(k, true); }
        foreach (var k in kelps)
        {
            var pos = k.t.position; pos.z += eff * dt;
            if (pos.z > KelpBehind) { k.t.position = pos; LayKelp(k, false); pos = k.t.position; }
            pos.y = Dive.BedH(pos.x, pos.z, distM) - k.dig;
            k.t.position = pos;
            if (!FogCull(k.t, camZ, fogFar)) continue;
            k.ph += dt * 0.55f;
            float sw = Mathf.Sin(k.ph) * k.sway, cw = Mathf.Cos(k.ph * 0.77f + 1.3f) * k.sway * 0.7f;
            float rx = Mathf.Sin(k.leanA) * k.lean + sw, rz = Mathf.Cos(k.leanA) * k.lean + cw;
            k.t.rotation = Quaternion.Euler(rx * Mathf.Rad2Deg, k.yaw * Mathf.Rad2Deg, rz * Mathf.Rad2Deg);
        }
        // ---- the turtles ----
        turtT -= dt;
        if (turtT <= 0f && turtles.Count < TurtMax && !turtMissing)
        {
            turtT = TurtGapMin + Random.value * (TurtGapMax - TurtGapMin);
            var b = MakeBody("turtle", 2, out turtMissing); if (b != null) { turtles.Add(b); LayTurtle(b, eff); }
        }
        foreach (var b in turtles)
        {
            var pos = b.t.position; pos.z += (eff + b.hz) * dt; pos.x += b.hx * dt; b.t.position = pos;
            if (pos.z > TurtBehind || Mathf.Abs(pos.x) > TurtSpread + 14f) { LayTurtle(b, eff); continue; }
            b.ph += dt * 3.1f * b.rate;
            if (b.sit)
            {
                float bY = Dive.BedH(pos.x, pos.z, distM) + b.halfH * b.ft + 0.06f;
                pos.y += (bY - pos.y) * Mathf.Min(1f, dt * 6.0f); b.t.position = pos;
                FogCull(b.t, camZ, fogFar); continue;
            }
            b.roll += dt * 0.7f;
            float want = Mathf.Atan2(-b.hx, -(b.hz + eff)); float d = Mathf.DeltaAngle(b.yaw * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            b.yaw += d * Mathf.Min(1f, dt * 1.6f);
            float flat = Mathf.Sqrt(b.hx * b.hx + (b.hz + eff) * (b.hz + eff));
            float wp = Mathf.Clamp(Mathf.Atan2(b.hy, Mathf.Max(0.4f, flat)), -0.42f, 0.42f) + Mathf.Sin(b.roll * 0.83f + 1.1f) * 0.07f;
            b.pitch += (wp - b.pitch) * Mathf.Min(1f, dt * 2.2f);
            pos.y += b.hy * dt + Mathf.Sin(b.ph * 0.5f) * dt * 0.55f;
            float fl = Dive.BedH(pos.x, pos.z, distM) + 1.5f;
            if (pos.y < fl) { pos.y += (fl - pos.y) * Mathf.Min(1f, dt * 2.4f); b.hy = Mathf.Abs(b.hy) + 0.35f; b.hyT = Mathf.Max(b.hyT, 1.6f); }
            const float top = -2.4f;
            if (pos.y > top) { pos.y += (top - pos.y) * Mathf.Min(1f, dt * 2.0f); b.hy = -Mathf.Abs(b.hy) - 0.35f; b.hyT = Mathf.Max(b.hyT, 1.6f); }
            b.hyT -= dt;
            if (b.hyT <= 0f) { b.hyT = 2.0f + Random.value * 3.5f; b.hy = (Random.value - 0.5f) * 2.6f; }
            b.t.position = pos;
            b.t.rotation = Quaternion.Euler(b.pitch * Mathf.Rad2Deg, b.yaw * Mathf.Rad2Deg + TurtFace, Mathf.Sin(b.roll) * 0.11f * Mathf.Rad2Deg);
            FogCull(b.t, camZ, fogFar);
        }
        // ---- the rays ----
        rayT -= dt;
        if (rayT <= 0f && rays.Count < RayMax && !rayMissing)
        {
            rayT = RayGapMin + Random.value * (RayGapMax - RayGapMin);
            var b = MakeBody("stingray", 0, out rayMissing); if (b != null) { rays.Add(b); LayRay(b, eff); }
        }
        foreach (var b in rays)
        {
            var pos = b.t.position; pos.z += (eff + b.hz) * dt; pos.x += b.hx * dt; b.t.position = pos;
            if (pos.z > RayBehind || pos.z < RayAheadCull || Mathf.Abs(pos.x) > RaySpread + 16f) { LayRay(b, eff); continue; }
            if (b.sit)
            {
                float bY = Dive.BedH(pos.x, pos.z, distM) + b.halfH * b.ft + 0.05f;
                pos.y += (bY - pos.y) * Mathf.Min(1f, dt * 6.0f); b.t.position = pos;
                FogCull(b.t, camZ, fogFar); continue;
            }
            b.roll += dt * 0.55f;
            float want = Mathf.Atan2(b.hx, b.hz + eff); float d = Mathf.DeltaAngle(b.yaw * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            b.yaw += d * Mathf.Min(1f, dt * 1.5f);
            float flat = Mathf.Sqrt(b.hx * b.hx + (b.hz + eff) * (b.hz + eff));
            float wp = -Mathf.Clamp(Mathf.Atan2(b.hy, Mathf.Max(0.4f, flat)), -0.38f, 0.38f);
            b.pitch += (wp - b.pitch) * Mathf.Min(1f, dt * 2.0f);
            pos.y += b.hy * dt;
            float fl = Dive.BedH(pos.x, pos.z, distM) + 2.2f;
            if (pos.y < fl) { pos.y += (fl - pos.y) * Mathf.Min(1f, dt * 2.2f); b.hy = Mathf.Abs(b.hy) + 0.30f; b.hyT = Mathf.Max(b.hyT, 1.6f); }
            const float top = -3.0f;
            if (pos.y > top) { pos.y += (top - pos.y) * Mathf.Min(1f, dt * 2.0f); b.hy = -Mathf.Abs(b.hy) - 0.30f; b.hyT = Mathf.Max(b.hyT, 1.6f); }
            b.hyT -= dt;
            if (b.hyT <= 0f) { b.hyT = 2.4f + Random.value * 4.0f; b.hy = (Random.value - 0.5f) * 2.2f; }
            b.t.position = pos;
            b.t.rotation = Quaternion.Euler(b.pitch * Mathf.Rad2Deg, b.yaw * Mathf.Rad2Deg + RayFace, Mathf.Sin(b.roll) * 0.07f * Mathf.Rad2Deg);
            FogCull(b.t, camZ, fogFar);
        }
    }

    // what the fog has already hidden does not need drawing (web fogCull)
    static bool FogCull(Transform t, float camZ, float fogFar)
    {
        bool vis = !(camZ - t.position.z > fogFar + 12f);
        if (t.gameObject.activeSelf != vis) t.gameObject.SetActive(vis);
        return vis;
    }

    // ---- making and laying ----
    Prop MakeProp(Kind k)
    {
        var holder = new GameObject(k.file).transform; holder.SetParent(root, false);
        var go = Models.Spawn(k.file, holder, 1f, PrimitiveType.Cube);        // one foot tall, base on the origin
        if (go.name.EndsWith("(stand-in)")) { k.missing = true; Destroy(holder.gameObject); return null; }
        if (k.rad <= 0f)
        {
            // measured on the way in: how wide it is against how tall, for the hit shape
            var b = Models.Bounds(go);
            k.rad = Mathf.Max(b.size.x, b.size.z) * 0.5f; k.wid = b.size.x; k.dep = b.size.z;
        }
        return new Prop { k = k, t = holder };
    }

    void LayProp(Prop p, bool seed)
    {
        var k = p.k; float R() => Random.value;
        float z = seed ? -(R() * (-Ahead)) : -(Born + R() * FloorBand);
        float sgn = R() < 0.5f ? -1f : 1f, x = sgn * (3.0f + R() * (Spread - 3.0f));
        float ft = k.ftA + Mathf.Pow(R(), k.bias) * (k.ftB - k.ftA);
        p.ft = ft; p.t.localScale = Vector3.one * ft;
        p.sink = ft * (k.sinkA + R() * (k.sinkB - k.sinkA));
        p.hit = ft * k.rad * FloorHit; p.top = ft;
        if (k.tunnel) { p.halfW = ft * k.wid * 0.5f; p.halfD = ft * k.dep * 0.5f; p.gap = ft * k.wid * ArchGap; p.mid = ft * k.wid * ArchMid; p.arch = ft * ArchTop; }
        p.yaw = R() * 6.283f;
        p.t.rotation = Quaternion.Euler((R() - 0.5f) * k.tilt * Mathf.Rad2Deg, p.yaw * Mathf.Rad2Deg, (R() - 0.5f) * k.tilt * Mathf.Rad2Deg);
        p.t.position = new Vector3(x, Dive.BedH(x, z, rider != null ? rider.dist : 0f) - p.sink, z);
        p.t.gameObject.SetActive(true);
    }

    Kelp MakeKelp()
    {
        var holder = new GameObject("kelp").transform; holder.SetParent(root, false);
        var go = Models.Spawn("kelp", holder, 1f, PrimitiveType.Cube);
        if (go.name.EndsWith("(stand-in)")) { kelpMissing = true; Destroy(holder.gameObject); return null; }
        return new Kelp { t = holder, model = go.transform };
    }

    void LayKelp(Kelp k, bool seed)
    {
        float R() => Random.value;
        float z = seed ? -(R() * (Born + KelpBand)) : -(Born + R() * KelpBand);
        float sgn = R() < 0.5f ? -1f : 1f, x = sgn * (KelpLane + R() * (KelpSpread - KelpLane));
        float distM = rider != null ? rider.dist : 0f;
        float bed = Dive.BedH(x, z, distM);
        float colH = Mathf.Max(KelpMinFt + 1.0f, (0f - bed) - KelpTop);         // the tallest almost reach the surface
        float ft = KelpMinFt + Mathf.Pow(R(), KelpTall) * (colH - KelpMinFt);
        float thin = KelpThinMin + R() * (KelpThinMax - KelpThinMin);
        float w = thin * KelpH / KelpW;                                         // the web's sx/sy
        k.ft = ft; k.t.localScale = new Vector3(ft * w, ft, ft * w);
        k.model.localRotation = Quaternion.Euler(0f, R() * 360f, 0f);           // so a stand does not read as one tile repeated
        k.dig = R() < KelpDeep ? ft * (KelpDigDeepA + R() * (KelpDigDeepB - KelpDigDeepA)) : ft * (KelpDigMin + R() * (KelpDigMax - KelpDigMin));
        k.yaw = R() * 6.283f; k.lean = R() * 0.10f; k.leanA = R() * 6.283f; k.ph = R() * 6.283f; k.sway = 0.018f + R() * 0.026f;
        k.t.rotation = Quaternion.Euler(0f, k.yaw * Mathf.Rad2Deg, 0f);
        k.t.position = new Vector3(x, bed - k.dig, z);
        k.t.gameObject.SetActive(true);
    }

    // a swimming body: the model fitted to one foot along its own long axis (0 x, 2 z), centred
    Body MakeBody(string file, int axis, out bool missing)
    {
        missing = false;
        var holder = new GameObject(file).transform; holder.SetParent(root, false);
        var go = Models.Spawn(file, holder, 1f, PrimitiveType.Capsule);
        if (go.name.EndsWith("(stand-in)")) { missing = true; Destroy(holder.gameObject); return null; }
        var b = Models.Bounds(go);
        float side = axis == 0 ? b.size.x : b.size.z;
        if (side > 1e-4f) go.transform.localScale = go.transform.localScale * (1f / side);
        b = Models.Bounds(go);
        go.transform.localPosition -= b.center;
        var body = new Body { t = holder, model = go.transform, halfH = Mathf.Max(0.01f, b.size.y * 0.5f) };
        // the file's own swim, if it brought one
        var clips = GltfClips.Clips(file);
        if (clips != null && clips.Length > 0)
        {
            string name = null; foreach (var c in clips) if (c != null && c.name == "swim") name = "swim";
            if (name == null) foreach (var c in clips) if (c != null) { name = c.name; break; }
            if (name != null) body.clips = GltfClips.Start(go.transform, file, new[] { name });
        }
        return body;
    }

    void LayTurtle(Body b, float eff)
    {
        float R() => Random.value; float distM = rider != null ? rider.dist : 0f;
        float sgn = R() < 0.5f ? -1f : 1f;
        float z = -(Born + R() * 60f), x = sgn * (TurtLane + R() * (TurtSpread - TurtLane));
        float bed = Dive.BedH(x, z, distM);
        float y = bed + 2.2f + R() * Mathf.Max(2f, (0f - bed) - 5.5f);
        b.ft = TurtFtMin + R() * (TurtFtMax - TurtFtMin); b.t.localScale = Vector3.one * b.ft;
        // most come the other way; the rest are quick enough to outrun him
        b.hz = R() < 0.5f ? (3.0f + R() * 11.0f) : (-36.0f + R() * 10.0f);
        b.hx = (R() - 0.5f) * 7.0f; b.hy = (R() - 0.5f) * 2.6f; b.hyT = 2.0f + R() * 3.5f;
        b.ph = R() * 6.283f; b.rate = 0.8f + R() * 0.9f; b.roll = R() * 6.283f;
        b.yaw = Mathf.Atan2(-b.hx, -(b.hz + eff)); b.pitch = 0f;
        b.sit = R() < TurtSit;
        if (b.sit)
        {
            b.hx = 0; b.hy = 0; b.hz = 0; b.hyT = 1e9f; b.rate *= 0.16f;
            b.yaw = R() * 6.283f;
            y = bed + b.halfH * b.ft + 0.06f;
        }
        if (b.clips != null) b.clips.SetSpeed(0, b.rate);
        b.t.position = new Vector3(x, y, z);
        b.t.rotation = Quaternion.Euler(0f, b.yaw * Mathf.Rad2Deg + TurtFace, 0f);
        b.t.gameObject.SetActive(true);
    }

    void LayRay(Body b, float eff)
    {
        float R() => Random.value; float distM = rider != null ? rider.dist : 0f;
        float sgn = R() < 0.5f ? -1f : 1f;
        float ang = (R() * 2f - 1f) * Mathf.PI, spd = RaySpdMin + R() * (RaySpdMax - RaySpdMin);
        b.hx = Mathf.Sin(ang) * spd; b.hz = Mathf.Cos(ang) * spd - eff;        // hz + eff is the screen speed
        b.hy = (R() - 0.5f) * 2.2f; b.hyT = 2.4f + R() * 4.0f;
        // in front if it is coming at you, behind if it is going away
        float z = (Mathf.Cos(ang) * spd > 0f) ? -(Born + R() * 70f) : (12f + R() * 24f);
        float x = sgn * (RayLane + R() * (RaySpread - RayLane));
        float bed = Dive.BedH(x, z, distM);
        float y = bed + 3.0f + R() * Mathf.Max(2f, (0f - bed) - 6.5f);
        b.ft = RayFtMin + R() * (RayFtMax - RayFtMin); b.t.localScale = Vector3.one * b.ft;
        b.yaw = Mathf.Atan2(b.hx, b.hz + eff);
        float flat = Mathf.Sqrt(b.hx * b.hx + (b.hz + eff) * (b.hz + eff));
        b.pitch = -Mathf.Clamp(Mathf.Atan2(b.hy, Mathf.Max(0.4f, flat)), -0.38f, 0.38f);
        b.roll = R() * 6.283f;
        b.rate = (0.72f + R() * 0.55f) * (RayFtMax / b.ft) * 0.55f;            // a big ray beats slower
        b.sit = R() < RaySit;
        if (b.sit)
        {
            b.hx = 0; b.hy = 0; b.hz = 0; b.hyT = 1e9f;
            z = -(Born + R() * 70f);
            b.yaw = R() * 6.283f; b.pitch = 0f;
            y = Dive.BedH(x, z, distM) + b.halfH * b.ft + 0.05f;
            b.rate *= 0.13f;
        }
        if (b.clips != null) { b.clips.SetSpeed(0, b.rate); b.clips.SetTime(0, R() * b.clips.Length(0)); }
        b.t.position = new Vector3(x, y, z);
        b.t.rotation = Quaternion.Euler(b.pitch * Mathf.Rad2Deg, b.yaw * Mathf.Rad2Deg + RayFace, 0f);
        b.t.gameObject.SetActive(true);
    }
}
