// The ride's sky furniture: the web game's skyLayer (web 29127-29690), which the port had left
// out, so the whole run went by under an empty gradient with not a cloud or a bird in it.
//
// Like the web's, it rides with the camera on x and z, so nothing in it comes any nearer
// however far the run goes; it is scenery a long way off. Into it:
//
// CLOUDS, in two layers, each a lit cumulus from SkyKit's atlas (Surf/Cumulus) turned to the
// lens. The near layer is the web's sixteen by the web's own rule (48 to 140 feet across, 46 to
// 150 up, 175 to 270 out, drifting 1.4 to 5 a second, high cloud faster); behind it a band of
// smaller ones low over the horizon, slower, sunk in the haze. Both creep toward the lens a
// little with the run's speed, the far band less than the near, which is the parallax that says
// the board is going somewhere. Each fades out at the ends of its drift rather than popping.
// Above both, the dome's own high cirrus (Surf/Sky) slides back slower still.
//
// GULLS: first the web's own FLIGHTS (two skeins of five, a loose three, two solos), each laid
// on a heading from its BIRD_STYLES -- crossing, cutting away on the diagonal, coming over the
// top, going away down the lane -- re-laid as a new flight whenever it leaves the box. Then,
// on a random timer, extras out of a pool: singles, pairs, loose knots and the odd long skein,
// at heights from skimming the swell to riding high on still wings, on gently curving paths
// with a bank into the turn. None of them is ever let through the lens: a path that would come
// close is lifted at the start, and anything that strays in anyway climbs out of the way.
// A flock that passes close in front calls once, as the web's gullPass does.
using System.Collections.Generic;
using UnityEngine;

public class SkyLife : MonoBehaviour
{
    public BoardController rider;
    public Camera cam;
    public Beach beach;              // the title has its own sky (BeachSky); this one stands down for it
    public Dive dive;
    public DayCycle day;
    public float birdSpan = 3.0f;    // feet, wingtip to wingtip, before the per-bird and per-flock scales

    class Cloud
    {
        public GameObject go; public Transform t;
        public float v, bob, par, xHalf, zNear, zFar, op, shownA = -1f;
    }
    class Flock
    {
        public Transform t; public readonly List<SkyKit.Gull> birds = new List<SkyKit.Gull>();
        public float hd, sp, vy, bob, turn, yMin, yMax, beat;
        public bool extra, said;
    }

    const float CloudSpan = 520f;                       // the web's CLOUD_SPAN
    const float BirdZAhead = -330f, BirdZBehind = 120f;   // the web's box
    const int SpareN = 12;                              // extra birds that can be up at once
    readonly List<Cloud> clouds = new List<Cloud>();
    readonly List<Flock> flocks = new List<Flock>();
    readonly List<SkyKit.Gull> spare = new List<SkyKit.Gull>();
    Transform root, cloudRoot, pool;
    System.Random rng = new System.Random(7741);
    float R() => (float)rng.NextDouble();
    float t, extraT = 3f;
    bool built, shown = true;

    public void Build()
    {
        root = new GameObject("Sky (ride)").transform; root.SetParent(transform, false);
        BuildClouds();
        BuildBirds();
        built = true;
    }

    // ---------------------------------------------------------------- clouds
    void BuildClouds()
    {
        if (SkyKit.CloudMaterial() == null) return;
        cloudRoot = new GameObject("Clouds").transform; cloudRoot.SetParent(root, false);
        // the web's sixteen (web 29223-29235). Their opacity there, 0.30 to 0.72, was for a flat
        // cut-out that had to stay faint to pass; a lit cloud is solid in the core and soft at the
        // rim on its own, so the range sits higher and the variety is kept.
        for (int i = 0; i < 16; i++)
        {
            float w = 48f + R() * 92f;
            AddCloud(w, 0.62f + R() * 0.36f, new Vector3((R() - 0.5f) * CloudSpan, 46f + R() * 104f, -175f - R() * 95f),
                     1.4f + R() * 3.6f, 0.020f, CloudSpan * 0.5f, -175f, -270f);
        }
        // the far band: smaller, lower and slower, kept inside the lens's far plane
        for (int i = 0; i < 10; i++)
        {
            float w = 40f + R() * 60f;
            AddCloud(w, 0.70f + R() * 0.30f, new Vector3((R() - 0.5f) * 440f, 16f + R() * 34f, -330f - R() * 70f),
                     0.6f + R() * 1.0f, 0.008f, 220f, -330f, -400f);
        }
    }

    void AddCloud(float w, float op, Vector3 at, float v, float par, float xHalf, float zNear, float zFar)
    {
        var go = SkyKit.Cloud(cloudRoot, w, (int)(R() * SkyKit.Shapes), op);
        if (go == null) return;
        go.transform.localPosition = at;
        clouds.Add(new Cloud { go = go, t = go.transform, v = v, bob = R() * Mathf.PI * 2f, par = par, xHalf = xHalf, zNear = zNear, zFar = zFar, op = op });
    }

    void StepClouds(float dt, float speed)
    {
        if (cloudRoot == null) return;
        SkyKit.LightClouds();
        var look = cam.transform.rotation;
        foreach (var c in clouds)
        {
            var p = c.t.localPosition;
            p.x += c.v * dt;
            if (p.x > c.xHalf) p.x -= c.xHalf * 2f;
            p.z += speed * c.par * dt;                  // the run's own travel, a sliver of it
            if (p.z > c.zNear) p.z -= c.zNear - c.zFar;
            c.bob += dt * 0.28f;
            p.y += Mathf.Sin(c.bob) * dt * 0.7f;
            c.t.localPosition = p;
            c.t.rotation = look;
            // out at the ends of the drift and in at the start, rather than a pop
            float fx = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c.xHalf, c.xHalf * 0.82f, Mathf.Abs(p.x)));
            float span = c.zNear - c.zFar;
            float fz = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c.zNear, c.zNear - span * 0.18f, p.z))
                     * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c.zFar, c.zFar + span * 0.18f, p.z));
            float a = c.op * fx * fz;
            if (Mathf.Abs(a - c.shownA) > 0.01f) { c.shownA = a; SkyKit.SetOpacity(c.go, a); }
        }
    }

    // ---------------------------------------------------------------- gulls
    // the web's FLIGHTS: a skein's count is odd and at least five, so the V reads as a V
    static readonly int[] FlightN = { 5, 3, 1, 5, 1 };
    static readonly bool[] FlightV = { true, false, false, true, false };

    float Yaw => beach != null ? beach.birdYaw : 90f;

    void BuildBirds()
    {
        var holder = new GameObject("Gulls").transform; holder.SetParent(root, false);
        for (int f = 0; f < FlightN.Length; f++)
        {
            var fl = new Flock();
            fl.t = new GameObject("flock " + f).transform; fl.t.SetParent(holder, false);
            fl.beat = 1.7f + R() * 2.0f;
            for (int i = 0; i < FlightN[f]; i++)
            {
                var b = SkyKit.MakeGull(fl.t, birdSpan, 0.8f + R() * 0.5f, fl.beat, Yaw, R);
                b.t.localPosition = SkyKit.FlightSpot(i, FlightN[f], FlightV[f], R);
                fl.birds.Add(b);
            }
            fl.t.localScale = Vector3.one * (1.6f + R() * 1.4f);
            fl.bob = R() * Mathf.PI * 2f;
            LayFlock(fl, true);
            flocks.Add(fl);
        }
        // the extras' birds, made now (a skinned gull with its own clip graph is too much to build
        // in the middle of a run) and put away until a flock is wanted
        pool = new GameObject("spare gulls").transform; pool.SetParent(holder, false);
        for (int i = 0; i < SpareN; i++)
        {
            var b = SkyKit.MakeGull(pool, birdSpan, 0.8f + R() * 0.5f, 1.7f + R() * 2.0f, Yaw, R);
            SkyKit.Show(b, false);
            spare.Add(b);
        }
    }

    // ---- the web's BIRD_STYLES and layFlock ----
    // hd is where it is going: 0 is +x (across to the right), PI/2 is +z (straight at the lens)
    void LayFlock(Flock f, bool first)
    {
        float y, z, w = R() * 13.2f;   // the styles' weights: 3, 3, 2.2, 2.2, 1.6, 1.2
        if (w < 3f) { f.hd = 0f; y = 62f + R() * 54f; z = -140f - R() * 90f; }
        else if (w < 6f) { f.hd = Mathf.PI; y = 62f + R() * 54f; z = -140f - R() * 90f; }
        else if (w < 8.2f) { f.hd = (R() < 0.5f ? 1f : -1f) * (0.5f + R() * 0.5f); y = 56f + R() * 52f; z = -170f - R() * 70f; }
        else if (w < 10.4f) { f.hd = Mathf.PI + (R() < 0.5f ? 1f : -1f) * (0.5f + R() * 0.5f); y = 56f + R() * 52f; z = -170f - R() * 70f; }
        else if (w < 12f) { f.hd = Mathf.PI / 2f + (R() - 0.5f) * 0.5f; y = 34f + R() * 26f; z = -230f - R() * 80f; }   // over the top
        else { f.hd = -Mathf.PI / 2f + (R() - 0.5f) * 0.5f; y = 44f + R() * 46f; z = -40f - R() * 60f; }               // away down the lane
        f.sp = 9f + R() * 17f;
        f.vy = (R() - 0.5f) * 2.4f;
        f.turn = 0f; f.yMin = 26f; f.yMax = 132f;
        float x = first ? (R() - 0.5f) * CloudSpan : (Mathf.Cos(f.hd) > 0f ? -1f : 1f) * (0.35f + R() * 0.2f) * CloudSpan;
        f.t.localPosition = new Vector3(x, Clear(f, x, y, z), z);
        f.said = false;
        Face(f);
    }

    // The extras: a single, a pair, a loose knot, or a long skein, in any of the web's styles or
    // two of their own -- skimming low over the swell far ahead, or riding high and slow on still
    // wings -- and each one curving a little as it goes.
    void LaunchExtra()
    {
        float q = R();
        int n = q < 0.35f ? 1 : q < 0.55f ? 2 : q < 0.80f ? 3 + (int)(R() * 2f) : (R() < 0.6f ? 5 : 7);
        bool vee = n >= 5;
        n = Mathf.Min(n, spare.Count);
        if (n <= 0) return;
        if (vee && n < 5) vee = false;
        var f = new Flock { extra = true };
        f.t = new GameObject("extra flock").transform; f.t.SetParent(pool.parent, false);
        f.beat = 1.7f + R() * 2.0f;
        f.t.localScale = Vector3.one * (1.3f + R() * 1.5f);
        f.bob = R() * Mathf.PI * 2f;
        float s = R();
        bool soar = false;
        if (s < 0.18f && n <= 2)
        {
            // high and slow, wings held
            soar = true;
            f.hd = (R() < 0.5f ? 0f : Mathf.PI) + (R() - 0.5f) * 0.6f; f.sp = 6f + R() * 5f;
            f.yMin = 110f; f.yMax = 175f;
            float x = (Mathf.Cos(f.hd) > 0f ? -1f : 1f) * (0.40f + R() * 0.15f) * CloudSpan;
            f.t.localPosition = new Vector3(x, 120f + R() * 50f, -150f - R() * 110f);
        }
        else if (s < 0.36f)
        {
            // low over the water, quick and straight across, well out ahead
            f.hd = R() < 0.5f ? 0f : Mathf.PI; f.sp = 16f + R() * 10f;
            f.yMin = 13f; f.yMax = 26f;
            float x = (Mathf.Cos(f.hd) > 0f ? -1f : 1f) * (0.40f + R() * 0.15f) * CloudSpan;
            f.t.localPosition = new Vector3(x, 14f + R() * 8f, -130f - R() * 140f);
            f.beat += 0.6f;
        }
        else
        {
            LayFlock(f, false);
            f.yMin = 22f; f.yMax = 150f;
            var p = f.t.localPosition; p.y += (R() - 0.4f) * 30f;       // more spread in height than the web's
            f.t.localPosition = new Vector3(p.x, Clear(f, p.x, Mathf.Clamp(p.y, f.yMin, f.yMax), p.z), p.z);
        }
        f.vy = (R() - 0.5f) * (soar ? 1.2f : 2.4f);
        f.turn = (R() - 0.5f) * (soar ? 0.10f : 0.06f);
        for (int i = 0; i < n; i++)
        {
            var b = spare[spare.Count - 1]; spare.RemoveAt(spare.Count - 1);
            b.t.SetParent(f.t, false);
            b.t.localPosition = SkyKit.FlightSpot(i, n, vee, R);
            SkyKit.Rebeat(b, f.beat, R);
            if (soar) { b.glide = true; b.gT = 4f + R() * 5f; }
            SkyKit.Show(b, shown);
            f.birds.Add(b);
        }
        Face(f);
        flocks.Add(f);
    }

    void Retire(Flock f)
    {
        foreach (var b in f.birds) { SkyKit.Show(b, false); b.t.SetParent(pool, false); spare.Add(b); }
        f.birds.Clear();
        Destroy(f.t.gameObject);
    }

    // The lens sits at the layer's origin (the layer rides with it on x and z) at its own height.
    // A straight path that would pass within a few dozen feet of it on the flat is put high enough
    // to go well over the top instead: nothing flies through the camera.
    float Clear(Flock f, float x, float y, float z)
    {
        float camY = cam != null ? cam.transform.position.y : 6f;
        var d = new Vector2(Mathf.Cos(f.hd), Mathf.Sin(f.hd));
        var p = new Vector2(x, z);
        float along = -Vector2.Dot(p, d);
        float miss = along > 0f ? (p + d * along).magnitude : p.magnitude;    // closest approach on the flat
        float need = camY + 24f + 6f * f.t.localScale.x;
        if (miss < 45f && y < need) y = need + R() * 12f;
        return y;
    }

    void Face(Flock f)
    {
        var d = new Vector3(Mathf.Cos(f.hd), 0f, Mathf.Sin(f.hd));
        // banked into the turn: the up tips toward the centre of the curve
        var c = new Vector3(-d.z, 0f, d.x) * Mathf.Clamp(f.turn * f.sp * 0.08f, -0.5f, 0.5f);
        f.t.localRotation = Quaternion.LookRotation(d, (Vector3.up + c).normalized) * Quaternion.Euler(0f, -90f, 0f);
    }

    void StepBirds(float dt)
    {
        if (pool == null) return;
        extraT -= dt;
        if (extraT <= 0f)
        {
            extraT = 3f + R() * 7f;
            int extras = 0; foreach (var f in flocks) if (f.extra) extras++;
            if (extras < 4 && spare.Count > 0) LaunchExtra();
        }
        float camY = cam.transform.position.y;
        bool awake = rider != null && rider.running && !(dive != null && (dive.On || dive.Sub > 0f)) && !(day != null && day.nightAmt > 0.45f);
        for (int k = flocks.Count - 1; k >= 0; k--)
        {
            var f = flocks[k];
            f.hd += f.turn * dt;
            var p = f.t.localPosition;
            p.x += Mathf.Cos(f.hd) * f.sp * dt;
            p.z += Mathf.Sin(f.hd) * f.sp * dt;
            f.bob += dt;
            p.y += f.vy * dt + Mathf.Sin(f.bob * 0.7f) * dt * 1.4f;
            if (p.y < f.yMin) { p.y = f.yMin; f.vy = Mathf.Abs(f.vy); }
            if (p.y > f.yMax) { p.y = f.yMax; f.vy = -Mathf.Abs(f.vy); }
            // and if one has come near the lens anyway (a turn, a bob), it climbs out of the way
            float flat = new Vector2(p.x, p.z).magnitude, room = 22f + 6f * f.t.localScale.x;
            if (flat < room + 25f && p.y - camY < room) p.y += Mathf.Min(room - (p.y - camY), 30f * dt);
            f.t.localPosition = p;
            if (f.turn != 0f) Face(f);
            // out of the box in any direction: the web's come back as a new flight, extras go home
            if (Mathf.Abs(p.x) > CloudSpan * 0.62f || p.z < BirdZAhead || p.z > BirdZBehind)
            {
                if (f.extra) { Retire(f); flocks.RemoveAt(k); continue; }
                LayFlock(f, false);
            }
            foreach (var b in f.birds) SkyKit.Beat(b, t, dt, R);
            // a flyby close in front calls once (web gullPass), not underwater and not after dark
            if (awake) GullPass(f, p);
        }
    }

    void GullPass(Flock f, Vector3 p)
    {
        const float near = 78f;
        float d = new Vector2(p.x, p.z).magnitude;
        bool ahead = p.z < near * 0.35f;      // the lens looks along -z
        if (!(d < near && ahead)) { if (d > near * 1.2f) f.said = false; return; }
        if (f.said) return;
        if (Sfx.Gull(d, near, false)) f.said = true;
    }

    void SetShown(bool on)
    {
        if (shown == on) return;
        shown = on;
        if (cloudRoot != null) cloudRoot.gameObject.SetActive(on);
        foreach (var f in flocks) foreach (var b in f.birds) SkyKit.Show(b, on);
    }

    void LateUpdate()
    {
        if (!built || cam == null) return;
        bool on = !(beach != null && beach.Showing) && !(dive != null && dive.On);
        SetShown(on);
        if (!on) return;
        float dt = Mathf.Min(0.033f, Time.deltaTime);
        t += dt;
        // with the lens on x and z, as the web's skyLayer is
        var cp = cam.transform.position;
        root.position = new Vector3(cp.x, 0f, cp.z);
        float speed = rider != null && rider.running ? Mathf.Max(0f, rider.eff) : 0f;
        StepClouds(dt, speed);
        StepBirds(dt);
    }
}
