// The sharks under the water while he dives, the web's dshk system (web 41618-41816), number
// for number. The same animal as the surface's big fin and a different hazard: there is no
// surface to leap from, so what is left is the part that made the leap fair. Most of them cruise
// across the lane on their own errand; come inside one's notice and it turns on you ("IT SAW
// YOU!"). A hunter stalks, steering after you while it is far out; a second out it LOCKS: aim
// frozen, jaws open, charging ("SHARK!"). Be somewhere else when it arrives and it misses
// ("CLOSE!"); be where it aimed and it bites, which ends the run as the surface shark does.
// Dive.cs owns one of these and steps it after the scenery, as the web steps dshkStep after
// diveScenery (web 47200-47203).
using System.Collections.Generic;
using UnityEngine;

public class DiveSharks : MonoBehaviour
{
    public BoardController rider;

    // ---- the web's numbers (web 41628-41647) ----
    public const int Max = 2;                              // how many can be out at once
    public const float GapMin = 17.0f, GapMax = 34.0f;     // seconds between arrivals
    public const float Ahead = -155f, Behind = 30f;        // laid this far down the lane, taken back this far behind
    public const float Spread = 17f;                       // how wide across the lane it can come in from
    public const float Ft = 8.5f;                          // how long a shark is, in feet
    public const float Cruise = 10.0f;                     // ft/s it closes at while it is still stalking
    public const float Charge = 25.0f;                     // ...and once it has committed
    public const float LockT = 1.00f;                      // seconds out that it commits and stops tracking
    public const float Track = 3.0f;                       // the stalk's steering cap, times three, ft/s
    public const float BiteR = 2.3f;                       // how near its aim has to land: the dodge
    public const float Hunt = 0.18f;                       // the share of them that ever meant to
    public const float Notice = 9.0f;                      // how near its line passes before it turns on you
    public const float SeeT = 1.9f;                        // ...and how long out it can still notice
    public const float Apart = 55f;                        // feet between two, so they arrive one at a time
    public const float CrossMin = 3.5f, CrossMax = 8.0f;   // how fast a cruiser crosses the lane
    const float Beam = 0.74f;                              // the surface fit's squash across the beam (web BIGFIN_BEAM 23198)
    // The jaw (web SHARK_JAW_*, 22937-22941): degrees about the bone's own hinge, positive shuts it.
    const float JawShut = 58f, JawIdle = 17f, JawSwing = 11f, JawRate = 1.7f, JawEase = 13f;
    // The fitted model's nose lies along its own +x (the surface spawner's convention, see
    // Spawner.SharkYaw). A quarter turn about y puts it along +z, so the holder's yaw is the
    // web's heading as it stands: atan2(across, down the lane), 0 coming straight at the camera.
    const float FaceYaw = -90f;

    // stalk is the web's name for a shark that has seen you and is chasing; lock is the charge
    public enum Ph { Cruise, Stalk, Lock, Bite, Miss }
    public class Shark
    {
        public Transform t, jaw; public Quaternion jawRest;
        public Vector3 pos; public Ph ph; public bool hunt, done, woke;
        public float cross, jawAng, jawT, t0, aimX, aimY;
    }
    public readonly List<Shark> sharks = new List<Shark>();
    public float nextT = 6.0f;                             // the first one comes sooner (web _dshkT)
    Transform root;

    void Ensure()
    {
        if (root != null) return;
        root = new GameObject("DiveSharks").transform; root.SetParent(transform, false);
    }

    /// the run is put away (a revive, a new run): every shark laid fresh and hidden, so the
    /// next dive does not resume one four feet off his nose in mid-lunge (web 41726-41732)
    public void Reset()
    {
        if (sharks.Count > 0) { foreach (var s in sharks) Lay(s); nextT = GapMin; }
        if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
    }

    // =====================================================================================
    // ---- the step (web dshkStep 41719-41816) ----
    bool laid;
    public void Step(float dt, bool diveOn, float eff)
    {
        var r = rider; if (r == null) return;
        bool on = diveOn && r.running;
        if (root == null) { if (!on) return; Ensure(); }
        if (root.gameObject.activeSelf != diveOn) root.gameObject.SetActive(diveOn);
        if (!on)
        {
            // laid out again while the dive is not on, so the next one starts clean
            if (!diveOn && sharks.Count > 0 && !laid) { foreach (var s in sharks) Lay(s); nextT = GapMin; laid = true; }   // once, not every frame
            return;
        }
        laid = false;
        nextT -= dt;
        if (nextT <= 0f && sharks.Count < Max)
        {
            nextT = GapMin + Random.value * (GapMax - GapMin);
            var s = Make();
            if (s != null) { sharks.Add(s); Lay(s); }
        }
        for (int i = 0; i < sharks.Count; i++)
        {
            var s = sharks[i]; var p = s.pos;
            s.t0 += dt;
            // closing speed is the world going past PLUS its own swimming: it is coming the other way
            float own = (s.ph == Ph.Lock || s.ph == Ph.Bite) ? Charge : Cruise;
            p.z += (eff + own) * dt;
            float togo = (r.pz - p.z) / Mathf.Max(1f, eff + own);       // seconds until it is level with him
            if (s.ph == Ph.Cruise)
            {
                // straight across the lane, minding its own business
                p.x += s.cross * dt;
                s.aimX = p.x + s.cross; s.aimY = p.y;
                // ...until he comes inside its notice: measured ACROSS the lane and asked while it
                // is still a second or so out, because what matters is whether its line passes near
                // his with time left to turn (web 41751-41760)
                float lx = r.px - p.x, ly = (r.py - p.y) * 0.8f;
                float lat = Mathf.Sqrt(lx * lx + ly * ly);
                if (lat < Notice && togo < SeeT && togo > LockT + 0.15f && r.running && !r.wipe)
                {
                    s.ph = Ph.Stalk; s.woke = true; r.Say("IT SAW YOU!"); Sfx.Buzz(20, 30, 20);
                }
                if (p.z > r.pz + 4f && !s.done) s.done = true;            // gone past without ever having cared
            }
            else if (s.ph == Ph.Stalk)
            {
                // it steers after him while it is far enough out that steering is not cheating, and
                // the rate is capped, so coming from the far side of the lane costs it the angle
                float cap = Track * dt * 3f;
                p.x += Mathf.Clamp((r.px - p.x) * dt * 1.3f, -cap, cap);
                p.y += Mathf.Clamp((r.py - p.y) * dt * 1.3f, -cap, cap);
                s.aimX = r.px; s.aimY = r.py;
                if (togo < LockT && togo > -1f)
                {
                    s.ph = Ph.Lock; s.aimX = r.px; s.aimY = r.py;
                    r.Say("SHARK!"); Sfx.Buzz(30, 40, 30);
                }
            }
            else if (s.ph == Ph.Lock)
            {
                // committed: it runs at the spot it chose and nothing it learns from here changes it
                float k = Mathf.Min(1f, dt * 6f);
                p.x += (s.aimX - p.x) * k;
                p.y += (s.aimY - p.y) * k;
                if (!s.done && p.z > r.pz - 1.0f)
                {
                    s.done = true;
                    float ox = r.px - s.aimX, oy = r.py - s.aimY;
                    if (Mathf.Sqrt(ox * ox + oy * oy) < BiteR && r.running && !r.wipe)
                    {
                        // ends the run the way the surface shark does, and he is not surfaced first:
                        // the fog and the sand say where this happened (web 41782-41786)
                        s.ph = Ph.Bite;
                        r.Wipeout("bigfin");
                    }
                    else
                    {
                        s.ph = Ph.Miss;
                        r.Say("CLOSE!");
                    }
                }
            }
            s.pos = p;
            // it swims along its own line, nose first, with a slow roll
            float hx = s.ph == Ph.Cruise ? s.cross * 2.2f : (s.aimX - p.x), hz = Mathf.Max(0.5f, eff + own);
            s.t.position = p;
            s.t.rotation = Quaternion.Euler(0f, Mathf.Atan2(hx, hz) * Mathf.Rad2Deg, Mathf.Sin(s.t0 * 1.6f) * 0.10f * Mathf.Rad2Deg);
            Jaw(s, dt);
            if (p.z > Behind) Lay(s);
        }
    }

    // ---- the jaw (web 41797-41812) ----
    // Gaping on the run in is the tell that it has committed, and the only warning there is.
    // Cruising and stalking it works slowly open and shut; locked it opens past the modelled gape;
    // holding him it stays half shut; having missed it snaps shut and falls open on a 1.4 s beat.
    // The import mirrors x, which turns a rotation about the bone's Y the other way, hence the
    // minus (as Spawner.SharkJaw).
    void Jaw(Shark s, float dt)
    {
        if (s.jaw == null) return;
        s.jawT += dt;
        float want;
        if (s.ph == Ph.Lock) want = -8f - 10f * Mathf.Min(1f, s.t0 * 0.6f);
        else if (s.ph == Ph.Bite) want = JawShut * 0.5f;
        else if (s.ph == Ph.Miss) want = JawShut * Mathf.Max(0f, 1f - (s.t0 % 1.4f) / 0.9f);
        else want = JawIdle + Mathf.Sin(s.jawT * JawRate) * JawSwing;
        s.jawAng += (want - s.jawAng) * Mathf.Min(1f, dt * JawEase);
        s.jaw.localRotation = s.jawRest * Quaternion.AngleAxis(-s.jawAng, Vector3.up);
    }

    // =====================================================================================
    // ---- making one (web dshkMake 41670-41688) ----
    // The surface fit, as Spawner's "bigfin" case does it (nine feet along its length, squashed
    // to 0.74 across the beam), but centred on the holder in all three axes: the surface sink
    // that leaves only the fin out has no waterline to hide behind down here (web 41676-41683),
    // and it is fitted to the dive's own 8.5 feet rather than the surface's nine.
    Shark Make()
    {
        Ensure();
        var holder = new GameObject("diveShark").transform; holder.SetParent(root, false);
        var face = new GameObject("face").transform; face.SetParent(holder, false);
        var g = Models.Spawn("bigfin", face, 3.4f, PrimitiveType.Cube);
        foreach (var a in g.GetComponentsInChildren<Animation>(true)) a.enabled = false;
        foreach (var a in g.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        var s = new Shark { t = holder };
        if (g.name.EndsWith("(stand-in)"))
        {
            // no file: a plain long shape of the right size, so the hazard is still there to dodge
            g.transform.localScale = new Vector3(Ft, 1.6f, 1.2f);
            g.transform.localPosition = Vector3.zero;
        }
        else
        {
            // measured while the face turn is still identity, so the box's axes are the model's
            var b = Models.Bounds(g);
            float len = Mathf.Max(b.size.x, b.size.z);
            if (len > 1e-4f)
            {
                var ls = g.transform.localScale * (Ft / len);
                ls.z *= Beam;
                g.transform.localScale = ls;
                b = Models.Bounds(g);
                g.transform.localPosition -= b.center;
            }
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
                if (t.name == "Jaw") { s.jaw = t; s.jawRest = t.localRotation; break; }
        }
        face.localRotation = Quaternion.Euler(0f, FaceYaw, 0f);
        return s;
    }

    // ---- laying one out (web dshkLay 41689-41718) ----
    void Lay(Shark s)
    {
        var r = rider; float R() => Random.value;
        // never on top of another one: tried a few times and the furthest from anything else wins,
        // which needs no loop that can fail to end
        float bz = 0f, best = -1f;
        for (int i = 0; i < 6; i++)
        {
            float z = Ahead * (0.55f + R() * 0.45f);
            float near = 1e9f;
            foreach (var o in sharks) if (o != s) near = Mathf.Min(near, Mathf.Abs(o.pos.z - z));
            if (near > best) { best = near; bz = z; }
            if (near > Apart) break;
        }
        var p = new Vector3((R() - 0.5f) * 2f * Spread, 0f, bz);
        p.y = Mathf.Max(Dive.BedH(p.x, p.z, r != null ? r.dist : 0f) + 3.0f, -13f + R() * 8f);
        s.pos = p;
        // most of them are not interested in him: a cruiser crosses the lane and never looks at
        // him unless he comes inside its notice, which is what gives a hunter something to differ from
        s.hunt = R() < Hunt;
        s.ph = s.hunt ? Ph.Stalk : Ph.Cruise;
        s.cross = (R() < 0.5f ? -1f : 1f) * (CrossMin + R() * (CrossMax - CrossMin));
        s.jawAng = JawIdle; s.jawT = R() * 6f; s.t0 = 0f;
        s.aimX = r != null ? r.px : 0f; s.aimY = r != null ? r.py : 0f; s.done = false; s.woke = false;
        s.t.position = p;
        s.t.gameObject.SetActive(true);
    }
}
