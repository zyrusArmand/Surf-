// The tow boat and the paraglide it hands you, the web game's set piece number for number
// (web 30407-31194 the boat, 31196-31976 the wing, 23757-23771 the grab).
//
// It is on demand only: nothing in an ordinary run sends it, and the stage button (summon) is
// how it is seen, as in the web (15187-15195). No hour is forced -- unlike the saucer, the boat
// is not gated on the sky -- and nothing else waits for it: the web's set wave, whirlpool and
// saucer do not ask about it, so neither do they here.
//
//   RUN:     it comes up from behind, 58 ft back and 13 ft out to one side, dead straight at 30.
//   PASS:    it draws level (the offer opens: GRAB THE LINE!) and goes on by at 26.
//   SWING:   past 34 ft ahead it crosses into your lane over 3.2 s on a smoothstep, coming off
//            the throttle toward 13, the bow only nodding toward the new line (a tenth of a
//            radian at most) rather than showing you its side.
//   STATION: it holds ahead of you, never backing up. If you took the line, a second after it
//            arrives the wing opens; if you have not taken it 3.4 s after it arrives the offer
//            shuts (MISSED IT); with no flight it gives up after 6 s.
//   AWAY:    it winds up toward 40 and is gone once it is 190 ft ahead.
//
// The flight: TOW, 8 s on the line, eased up toward 52 ft while being pulled in behind the boat,
// 90 a second; FLY, still on the rope (parasailing), sinking at 1.5 ft/s plus more for any bank,
// steered by the same stick as the board, 45 a second; LAND at 1.4 ft over the water, the
// canopy collapsing over 0.62 s, and whatever was thrown up there is scored as a landing --
// always landed (web settleTricks(true)), never a wipeout -- or NICE LANDING! if nothing was.
// While it has him nothing in the lane can hit him or call a close call, the shark does not
// leap at him and the whirlpool does not pull (web glPh gates); the board tricks still run.
//
// The wake: the web switched the ribbons, the foam cloud, the height-field ridge and the
// transom spray OFF (BOAT_WAKE_ON=false, "remove all trails from boat, they look awful"), so
// none of that is here. What it kept is the sea shader's own foam V (uBtA), which is ported
// into Surf/Ocean as _BtA and driven from here. The bow still puts floating things aside (web
// boatShove), which is not part of the wake switch.
//
// Public API (Bootstrap owns the wiring): Reset() at Play/Menu, Step(dt) in the running branch
// after the ride has stepped, summon for the stage. It owns rider.py/px while the wing is up
// (rider.glideOn), as the dive does, and re-places his transform after BoardController.Step.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Boat : MonoBehaviour
{
    public enum Phase { Off = -1, Run = 0, Pass = 1, Swing = 2, Station = 3, Away = 4 }   // web BTP
    public enum Grab { None = 0, Open = 1, Taken = 2, Missed = 3 }                        // web BTG
    public enum Glide { Off = -1, Tow = 0, Fly = 1, Land = 2 }                            // web GLP

    // ---- the boat's numbers (web 30651-30764) ----
    public const float PassX = 13.0f;          // how far to the side it goes by
    public const float StationZ = -50f;        // the floor on how far ahead it will sit
    public const float StationT = 6.0f;        // seconds it holds there before giving up on you
    public const float FlyBack = 62f;          // how much further ahead it sits as he climbs
    public const float SwingT = 3.2f;          // how long the crossing takes
    public const float GrabHold = 3.4f;        // how long the offer stays open once it is on station
    static readonly Vector3 BoxSize = new Vector3(5.8f, 3.2f, 20f);   // web BOAT_BOX: a twenty-foot hull
    // ---- the bow shove (web 30851-30979) ----
    const float HullHL = 10.0f, HullHB = 2.9f;                       // half-length, half-beam, off the box
    const float Lead0 = 5.0f, LeadK = 0.22f, RateCap = 30f;
    static readonly Dictionary<string, float> Mass = new Dictionary<string, float> {
        {"ramp",3.4f},{"octopus",2.4f},{"bigfin",1.9f},{"jelly",0.55f},{"debris",0.7f},{"log",1.25f} };
    // ---- the wing's numbers (web 31200-31232, 31252-31255, 31591, 31612) ----
    public const float GlTop = 52f;            // how high the tow takes you, in feet
    public const float GlTow = 8.0f;           // seconds on the line -- a winch, not a catapult
    public const float GlSink = 1.5f;          // feet a second down under canopy
    public const float GlSteer = 15f;          // how hard the wing turns
    public const float GlSwing = 3.1f;         // pendulum rate, radians a second
    public const float GlDamp = 1.35f;         // how fast a swing dies away
    public const float GlSpan = 19f;           // the modelled canopy's span
    const float RopeR = 0.30f; const int RopeSeg = 20, RopeRad = 7;

    public BoardController rider;
    public Spawner spawner;                    // the bow shoves what floats in the lane
    public ChaseCamera chase;                  // the shot climbs with him under the wing
    public Dive dive;                          // the diesel is muffled while he is under
    public Transform rig;                      // the board group: his paws are found under it
    public Transform ui;                       // the HUD's canvas, for the GRAB button
    public Material sea;                       // the ocean, for the foam V
    public System.Action<Vector3, int> onSplash;
    public bool summon;                        // the stage button: bring it now

    // ---- public state ----
    public Phase ph = Phase.Off;
    public Grab grab = Grab.None;
    public Glide gl = Glide.Off;
    public float t, x, z, yaw, spd;            // web btT, btX, btZ, btYaw, btSpd
    public int shoves;                         // how many things the bow has put out of the way
    public bool Busy => ph != Phase.Off || gl != Glide.Off;

    // ---- private ----
    int side = 1; float x0; bool glDone;       // web btSide, btX0, glDone (one wing per pass)
    Transform hull; float keel = -2.4f;
    float glT, glAge, glVx, glRoll, glRollV, glPitch, glPitchV, glYaw, glTarget, glSwingTo, glScale = 1f, glAcc;
    Transform glider; Vector3 brakeL, brakeR; bool wingOn, ropeOn;
    Mesh ropeMesh; Transform rope; Material ropeMat; Vector3[] ropeV; readonly float[] ropeLift = new float[RopeSeg + 1];
    Mesh brakeMesh; Transform brakes; readonly Vector3[] brakeV = new Vector3[4];
    Transform bar, barTube, gripL, gripR;
    GameObject btn; RectTransform btnRt; Image halo;
    Transform pugSeen; readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    class Push { public float vx, vz, w, roll, lift, yaw; public int side; public bool sided, on; public int seen; }
    readonly Dictionary<Spawner.Ob, Push> pushes = new Dictionary<Spawner.Ob, Push>();
    readonly List<Spawner.Ob> gone = new List<Spawner.Ob>();
    int frame;

    static float Smooth(float u) => u * u * (3f - 2f * u);

    // =====================================================================================
    /// the web's boatReset + glideReset: nothing left in the lane or in the sky from the last run
    public void Reset()
    {
        BoatReset();
        GlideReset();
        summon = false; glAcc = 0f;
        if (rider != null) rider.glideFace = 0f;
        pushes.Clear();
    }

    void BoatReset()
    {
        ph = Phase.Off; t = 0f; glDone = false; shoves = 0; grab = Grab.None;
        if (hull != null) hull.gameObject.SetActive(false);
        if (sea != null) sea.SetFloat("_BtA", 0f);
        if (btn != null) btn.SetActive(false);
    }

    void GlideReset()
    {
        gl = Glide.Off; glT = glAge = glVx = glRoll = glRollV = glPitch = glPitchV = glYaw = 0f;
        wingOn = false; ropeOn = false;
        if (glider != null) glider.gameObject.SetActive(false);
        if (rope != null) rope.gameObject.SetActive(false);
        if (brakes != null) brakes.gameObject.SetActive(false);
        if (bar != null) bar.gameObject.SetActive(false);
        if (rider != null) rider.glideOn = false;
        if (chase != null) chase.glide = 0f;
    }

    /// web boatStart: behind and out to one side, so it overtakes you rather than appearing alongside
    public void Begin()
    {
        var r = rider;
        if (ph != Phase.Off || r == null || !r.running) return;
        BuildHull();
        ph = Phase.Run; t = 0f;
        side = Random.value < 0.5f ? 1 : -1;
        x = r.px + side * PassX; z = r.pz + 58f; yaw = 0f; spd = 30f;
        hull.gameObject.SetActive(true);
        r.Say("BOAT!");
    }

    /// web grabAction: taking the line. It only does anything inside the window -- a press before
    /// or after is not a stored intention, it is a press at nothing. The line is in his hands from
    /// this instant, aimed on the spot (LateUpdate), rather than appearing with the canopy.
    public void TakeLine()
    {
        if (grab != Grab.Open || rider == null) return;
        grab = Grab.Taken;
        if (btn != null) btn.SetActive(false);
        BuildRig();
        ropeOn = true;
        Sfx.Buzz(14);
        rider.Say("HANG ON!");
    }

    // web glideStart: it inflates rather than appearing
    bool GlideStart()
    {
        var r = rider;
        if (gl != Glide.Off || !r.running || r.wipe) return false;
        if (r.hanging) r.ReleaseRope(false);           // off the plane's rope first (web 31782)
        BuildRig();
        gl = Glide.Tow; glT = 0f; glAge = 0f; glVx = glRoll = glRollV = glPitch = glPitchV = glYaw = 0f;
        wingOn = true; ropeOn = true; glScale = 0.2f;
        r.glideOn = true;                              // the ride lets go of py from the next frame
        r.Say("PARAGLIDE!");
        return true;
    }

    // =====================================================================================
    public void Step(float dt)
    {
        var r = rider; if (r == null) return;
        frame++;
        if (summon) { summon = false; Begin(); }
        // the wing first: in the web it is part of the ride update, which runs before the boat's
        if (gl != Glide.Off) GlideStep(dt);
        BoatStep(dt, r.eff);
        Shove(dt);
    }

    // ---- the boat (web updateBoat) ----
    void BoatStep(float dt, float eff)
    {
        var r = rider;
        if (ph == Phase.Off) return;
        t += dt;
        float sea0 = WaveField.Height(x, WaveField.Scroll + z, r.t);
        // what the boat itself does this frame, kept apart from the lane's drift so the heading
        // can be read off it
        float mvX = 0f, mvZ = 0f;
        if (ph == Phase.Run)
        {
            // a straight line: it holds the x it spawned at and comes down the lane on it
            spd = 30f; mvZ = -spd * dt;
            // the offer opens as it draws level, which is the moment there is a line within reach
            if (z < r.pz + 6f)
            {
                ph = Phase.Pass; t = 0f;
                if (!glDone && grab == Grab.None) { grab = Grab.Open; r.Say("GRAB THE LINE!"); }
            }
        }
        else if (ph == Phase.Pass)
        {
            spd = 26f; mvZ = -spd * dt;
            if (z < StationZ + 16f) { ph = Phase.Swing; t = 0f; x0 = x; }
        }
        else if (ph == Phase.Swing)
        {
            // a smoothstep over a fixed time: no sideways speed at either end, all of it in the
            // middle, and it comes right down toward your speed through the turn
            spd += (13f - spd) * Mathf.Min(1f, dt * 0.8f);
            mvZ = -spd * dt;
            float u = Mathf.Min(1f, t / SwingT), k = Smooth(u);
            mvX = (x0 + (r.px - x0) * k) - x;
            if (u >= 1f && z < StationZ + 3f) { ph = Phase.Station; t = 0f; }
        }
        else if (ph == Phase.Station)
        {
            // it waits a beat so the wing does not open the instant it arrives -- and only if you took the line
            if (t > 1.0f && grab == Grab.Taken && gl == Glide.Off && !glDone) { if (GlideStart()) glDone = true; }
            // and if you did not, the offer shuts
            if (grab == Grab.Open && t > GrabHold) { grab = Grab.Missed; r.Say("MISSED IT"); }
            // holding its place ahead of you, further out the higher he is so it stays in the
            // same part of the shot; it comes off the throttle rather than ever backing up
            float lift = Mathf.Clamp01((r.py - hull.position.y) / GlTop);
            float want = r.pz + StationZ - (gl >= Glide.Tow ? FlyBack * lift : 0f);
            mvZ = Mathf.Min(0f, (want - z) * Mathf.Min(1f, dt * (gl >= Glide.Fly ? 0.9f : 1.5f))) - eff * dt * 0.30f;
            mvX = 0f;                                   // it does not follow you about: you are on the end of the rope
            if (gl >= Glide.Fly) spd = Mathf.Max(spd, 22f);
            // and it does not leave while you are still up
            if (gl >= Glide.Tow) t = Mathf.Min(t, StationT - 0.6f);
            else if (t >= StationT)
            {
                ph = Phase.Away; t = 0f;
                if (gl < Glide.Tow) ropeOn = false;     // nothing else would clear it: there was no glide
            }
        }
        else if (ph == Phase.Away)
        {
            spd += (40f - spd) * Mathf.Min(1f, dt * 0.7f);
            mvZ = -spd * dt;
            if (z < r.pz - 190f) { BoatReset(); return; }
        }
        // move, THEN look at where you went: the heading is the direction of travel, held to a
        // token amount of it so it changes lane without showing you its side
        x += mvX; z += mvZ + eff * dt * 0.30f;
        float yawWant = (mvX * mvX + mvZ * mvZ > 1e-7f) ? Mathf.Clamp(Mathf.Atan2(-mvX, -mvZ) * 0.16f, -0.10f, 0.10f) : yaw;
        yaw += (yawWant - yaw) * Mathf.Min(1f, dt * 2.2f);
        // the keel at a measured depth, not the origin at a guessed height
        float now = Time.time;
        hull.position = new Vector3(x, sea0 - keel - 1.62f + Mathf.Sin(now * 1.9f) * 0.07f, z);
        // YXZ, which is what Unity's Euler is: yaw, then the bow under power, then the heel into the turn
        float pitch = -0.055f - 0.035f * Mathf.Min(1f, spd / 30f);
        float roll = -yaw * 0.45f + Mathf.Sin(now * 1.3f) * 0.02f;
        hull.rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
        // the diesel: level and pan from where it actually is, pitch from its throttle
        if (r.running) Sfx.Boat(x - r.px, z - r.pz, spd, dive != null && dive.Depth > 0.5f);
        // the foam V in the sea, alive only while it is actually making way (web 46796-46802)
        if (sea != null)
        {
            bool on = spd > 7f;
            sea.SetFloat("_BtX", x); sea.SetFloat("_BtZ", z + 8.4f);
            sea.SetFloat("_BtSn", Mathf.Sin(yaw)); sea.SetFloat("_BtCs", Mathf.Cos(yaw));
            sea.SetFloat("_BtA", on ? 0.62f + 0.38f * Mathf.Min(1f, spd / 24f) : 0f);
        }
    }

    // ---- the wing (web glideStep): it owns py and px outright while it runs ----
    void GlideStep(float dt)
    {
        var r = rider;
        float waterY = r.waterY, now = Time.time;
        r.glideOn = true; r.airborne = true; r.vy = 0f;     // under the canopy counts as off the water (web 45757)
        glT += dt; glAge += dt;
        // the canopy inflates ONCE, off the whole flight's clock
        if (gl != Glide.Land) { float inf = Mathf.Min(1f, glAge / 0.55f); glScale = 0.2f + 0.8f * Smooth(inf); }
        float sx = Mathf.Clamp(r.steer, -1f, 1f);          // the HUD has already folded the keys in
        if (gl == Glide.Tow)
        {
            // up, on the line, and pulled in behind the boat: a winch, not a catapult
            r.py += (GlTop - r.py) * Mathf.Min(1f, dt * 0.34f);
            r.px += (x - r.px) * Mathf.Min(1f, dt * 0.6f);
            r.speed = Mathf.Min(60f, r.speed + 6f * dt);
            Pay(90f * dt);
            // pulled nose-down and held, but the tow bounces
            glTarget = -0.20f + Mathf.Sin(now * 1.15f) * 0.035f;
            glSwingTo = (r.px - x) * 0.020f + Mathf.Sin(now * 0.71f) * 0.045f;
            // and the line STAYS ON: on the rope it is parasailing
            if (glT >= GlTow) { gl = Glide.Fly; glT = 0f; r.Say("UP AND AWAY!"); }
        }
        else if (gl == Glide.Fly)
        {
            // sinking slowly and steered by weight shift; the turn is a velocity so it banks into it
            glVx += sx * GlSteer * dt;
            glVx -= glVx * 1.5f * dt;
            glVx = Mathf.Clamp(glVx, -11f, 11f);
            r.px += glVx * dt;
            if (r.px < -17f) { r.px = -17f; glVx = 0f; }
            if (r.px > 17f) { r.px = 17f; glVx = 0f; }
            // a banked wing sinks faster, and the air is not still
            float bank = Mathf.Abs(glRoll);
            r.py -= (GlSink + bank * 4.2f) * dt;
            r.py += (Mathf.Sin(now * 0.63f) * 1.35f + Mathf.Sin(now * 0.29f + 1.7f) * 0.9f) * dt;
            glSwingTo = -glVx * 0.075f + Mathf.Sin(now * 0.53f) * 0.075f + Mathf.Sin(now * 0.29f + 2.1f) * 0.045f;
            glTarget = -0.14f - Mathf.Abs(glVx) * 0.012f + Mathf.Sin(now * 0.47f) * 0.05f;
            Pay(45f * dt);
            if (r.py <= waterY + 1.4f) { gl = Glide.Land; glT = 0f; }
        }
        else if (gl == Glide.Land)
        {
            // down on the water, the canopy flares and falls in behind him, and the line is cast off
            r.py += (waterY - r.py) * Mathf.Min(1f, dt * 6f);
            glSwingTo = 0f; glTarget = 0.22f;
            ropeOn = false;
            glScale = Mathf.Max(0.02f, 1f - glT * 1.9f);
            if (glT > 0.62f)
            {
                GlideReset();
                r.py = waterY; r.vy = 0f; r.airborne = false;
                // whatever he threw up there is scored and squared up here, the same as any landing;
                // the trick's own shout wins, so a spin is not reported as a landing
                int pts = 0;
                if (r.spinRot != 0f || r.flipRot != 0f || r.pitchRot != 0f) pts = r.Settle(true);
                if (pts == 0) r.Say("NICE LANDING!");
            }
        }
        // the pendulum, integrated rather than eased, so it overshoots the way a thing on strings does
        float dts = Mathf.Min(0.05f, dt);
        glRollV += ((glSwingTo - glRoll) * GlSwing * GlSwing - glRollV * 2f * GlDamp * GlSwing) * dts;
        glRoll += glRollV * dts;
        glPitchV += ((glTarget - glPitch) * 4.4f - glPitchV * 2.2f) * dts;
        glPitch += glPitchV * dts;
        // the wing weathercocks into the way it is going, and lags doing it
        glYaw += (Mathf.Clamp(-glVx * 0.055f - glRoll * 0.55f, -0.5f, 0.5f) - glYaw) * Mathf.Min(1f, dts * 2.4f);
        if (chase != null) chase.glide = gl != Glide.Off ? 1f : 0f;
        r.transform.position = new Vector3(r.px, r.py, r.pz);   // BoardController.Step placed him before py was written
    }

    // the web's score is a float; this one is whole points, so the fractions are carried
    void Pay(float p)
    {
        glAcc += p; int whole = Mathf.FloorToInt(glAcc);
        rider.score += whole; glAcc -= whole;
    }

    // ---- the bow puts things ASIDE, not through (web boatShove) ----
    // Where on the hull it touches decides which way it goes, latched so a thing on the centreline
    // does not shudder under the keel; inside the hull the push is half of what is left every
    // frame, so nothing is ever seen inside the boat; it keeps going after the hull has gone by,
    // and it slews and rolls away off the shoulder. The spawner re-places every obstacle each
    // frame with no yaw, so the turn a log was given is kept here and laid back on after it.
    void Shove(float dt)
    {
        if (spawner == null) return;
        var r = rider;
        bool live = ph != Phase.Off && hull != null && hull.gameObject.activeSelf;
        float sn = Mathf.Sin(yaw), cs = Mathf.Cos(yaw);
        foreach (var o in spawner.obstacles)
        {
            if (o.go == null || o.knocked) continue;
            pushes.TryGetValue(o, out var u);
            bool touching = false; float rollT = 0f, liftT = 0f;
            if (live)
            {
                float dxw = o.x - x, dzw = o.z - z;
                if (dxw * dxw + dzw * dzw < 900f)                // 30 ft: outside the hull and its lead
                {
                    float lx = dxw * cs - dzw * sn, lz = dxw * sn + dzw * cs;   // world -> hull
                    float orad = Mathf.Max(o.hx, o.hz); if (orad <= 0f) orad = 0.6f;
                    float beam = HullHB + orad, zEnd = HullHL + orad, lead = Lead0 + spd * LeadK;
                    if (lz > -(zEnd + lead) && lz < zEnd && Mathf.Abs(lx) < beam)
                    {
                        // 1 anywhere along the hull, easing in over the lead ahead of the stem, squared
                        float ahead = Mathf.Max(0f, -lz - zEnd);
                        float g = lz >= -zEnd ? 1f : Mathf.Max(0f, 1f - ahead / lead);
                        float grip = g * g;
                        touching = true;
                        if (u == null) { u = new Push(); pushes[o] = u; }
                        // which side, latched; dead ahead of the stem it goes away from the rider
                        if (!u.sided) { u.side = Mathf.Abs(lx) > 0.35f ? (lx >= 0f ? 1 : -1) : (o.x >= r.px ? 1 : -1); u.sided = true; }
                        float sd = u.side, m = (o.kind != null && Mass.TryGetValue(o.kind, out var mm)) ? mm : 1f;
                        float pen = beam - Mathf.Abs(lx);
                        float rate = Mathf.Min(RateCap, (6.0f + spd * 0.72f) * (1f + pen * 0.85f) / m);
                        bool inHull = lz >= -zEnd;
                        float step = inHull ? Mathf.Min(pen, Mathf.Max(rate * dt, pen * 0.50f)) : Mathf.Min(pen, rate * dt * grip);
                        o.x += sd * step * cs;
                        o.z -= sd * step * sn;
                        float kick = (0.55f + pen * 0.30f) * rate * dt * 1.9f * grip;
                        u.vx += sd * cs * kick; u.vz -= sd * sn * kick;
                        float carry = spd * 0.30f * dt * 1.8f * grip / m;
                        u.vx -= sn * carry; u.vz -= cs * carry;
                        // and it slews -- but a ramp is ridden square, so it gets the shove and none of the turn
                        if (!o.ramp)
                        {
                            u.w += sd * (0.45f + Mathf.Min(1f, Mathf.Abs(lx) / beam) * 1.75f) * dt * 3.0f * grip / m;
                            u.w = Mathf.Clamp(u.w, -3.2f, 3.2f);
                            rollT = sd * 0.30f * grip / m;
                        }
                        liftT = 0.32f * grip / m;
                        if (!u.on && grip > 0.30f)
                        {
                            u.on = true; shoves++;
                            onSplash?.Invoke(new Vector3(o.x - sd * 0.5f, o.y + 0.35f, o.z + 0.4f), 7);
                        }
                    }
                }
            }
            if (u == null) continue;
            u.seen = frame;
            if (!touching) { u.sided = false; u.on = false; }
            o.x += u.vx * dt; o.z += u.vz * dt;
            float drag = Mathf.Min(1f, dt * 1.7f);
            u.vx -= u.vx * drag; u.vz -= u.vz * drag;
            // the turn is kept: a log the boat has slewed stays slewed; only the rate dies away
            u.yaw += u.w * dt;
            u.w -= u.w * Mathf.Min(1f, dt * 1.3f);
            // roll and lift are offsets on top of what the swell and the float solver set
            u.roll += (rollT - u.roll) * Mathf.Min(1f, dt * (touching ? 5.0f : 2.2f));
            u.lift += (liftT - u.lift) * Mathf.Min(1f, dt * (touching ? 4.0f : 2.6f));
            o.x = Mathf.Clamp(o.x, -46f, 46f);
            if (o.kind == "bigfin") continue;          // the shark places and turns itself
            var tr = o.go.transform;
            tr.position = new Vector3(o.x, o.y + u.lift, o.z);
            tr.rotation = Quaternion.AngleAxis(u.yaw * Mathf.Rad2Deg, Vector3.up) * tr.rotation * Quaternion.AngleAxis(u.roll * Mathf.Rad2Deg, Vector3.forward);
        }
        // and the record of anything that has left the lane goes with it
        if (pushes.Count > 0)
        {
            gone.Clear();
            foreach (var kv in pushes) if (kv.Value.seen != frame) gone.Add(kv.Key);
            foreach (var o in gone) pushes.Remove(o);
        }
    }

    // =====================================================================================
    // The canopy's turn (web 44234) is eased every frame, and the offer's button follows the
    // window. A run that ends under the wing puts it away rather than leaving a canopy frozen
    // in the sky over the card.
    void Update()
    {
        var r = rider; if (r == null) return;
        float dt = Mathf.Min(0.033f, Time.deltaTime);
        if (r.wipe && (gl != Glide.Off || ropeOn)) GlideReset();
        float want = (gl != Glide.Off && !r.wipe) ? 1f : 0f;
        r.glideFace += (want - r.glideFace) * Mathf.Min(1f, dt * 4f);
        bool show = grab == Grab.Open && r.running && !r.wipe;
        if (show && btn == null) BuildButton();
        if (btn != null)
        {
            if (btn.activeSelf != show) btn.SetActive(show);
            if (show)
            {
                // it pulses, because it is the one control in the game with a deadline on it
                // (web grabPulse: 0.72 s, scale 1 to 1.07, a warm ring opening 13 px and fading)
                float ph01 = (Time.time % 0.72f) / 0.72f;
                float k = 0.5f - 0.5f * Mathf.Cos(ph01 * Mathf.PI * 2f);
                btnRt.localScale = Vector3.one * (1f + 0.07f * k);
                halo.rectTransform.localScale = new Vector3(1f + 26f / 114f * k, 1f + 26f / 94f * k, 1f);
                halo.color = new Color(1f, 0.882f, 0.588f, 0.55f * (1f - k));
            }
        }
    }

    // Placed last, after the rig has posed him, because the control lines and the bar end in his
    // actual paws and the rope ends on the bar.
    void LateUpdate()
    {
        var r = rider; if (r == null) return;
        if (glider != null && glider.gameObject.activeSelf != wingOn) glider.gameObject.SetActive(wingOn);
        if (wingOn && glider != null)
        {
            // hung from its own origin at the harness, so every swing is a rotation about him; and
            // the trick takes the wing with him (web 31931-31944)
            glider.localScale = Vector3.one * glScale;
            glider.position = new Vector3(r.px, r.py + 0.95f, r.pz - 0.3f);
            var trick = r.TrickRotation();                 // each axis the way it was thrown (web 45830-45834)
            glider.rotation = trick * Quaternion.Euler(glPitch * Mathf.Rad2Deg, glYaw * Mathf.Rad2Deg, glRoll * Mathf.Rad2Deg);
        }
        // the control lines, canopy to hand, each hand to the anchor on its own side; and the bar
        bool barOn = false; Vector3 mid = Vector3.zero;
        if (wingOn && glider != null && Paws(out var h0, out var h1))
        {
            Vector3 b0 = glider.TransformPoint(brakeL), b1 = glider.TransformPoint(brakeR);
            float straight = (h0 - b0).sqrMagnitude + (h1 - b1).sqrMagnitude;
            float crossed = (h0 - b1).sqrMagnitude + (h1 - b0).sqrMagnitude;
            brakeV[0] = b0; brakeV[1] = crossed < straight ? h1 : h0; brakeV[2] = b1; brakeV[3] = crossed < straight ? h0 : h1;
            brakeMesh.vertices = brakeV; brakeMesh.RecalculateBounds();
            if (!brakes.gameObject.activeSelf) brakes.gameObject.SetActive(true);
            // the bar spans the two hands, and waits for the arms to have arrived (glFace > 0.8)
            mid = (h0 + h1) * 0.5f;
            float span = Mathf.Max(0.35f, Vector3.Distance(h0, h1));
            bar.position = mid;
            var d = h1 - h0;
            bar.rotation = d.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, d.normalized) : Quaternion.identity;
            barTube.localScale = new Vector3(0.11f, span * 0.5f, 0.11f);          // the primitive is two tall and one across
            gripL.localPosition = new Vector3(0f, span * 0.5f - 0.17f, 0f);
            gripR.localPosition = new Vector3(0f, -span * 0.5f + 0.17f, 0f);
            barOn = r.glideFace > 0.80f;
        }
        else if (brakes != null && brakes.gameObject.activeSelf) brakes.gameObject.SetActive(false);
        if (bar != null && bar.gameObject.activeSelf != barOn) bar.gameObject.SetActive(barOn);
        // the tow rope, from the bar (or his harness before the bar is in his hands) to the transom
        bool ropeLive = ropeOn && hull != null && hull.gameObject.activeSelf;
        if (rope != null && rope.gameObject.activeSelf != ropeLive) rope.gameObject.SetActive(ropeLive);
        if (ropeLive)
        {
            Vector3 a = barOn ? mid : new Vector3(r.px, r.py + 1.0f, r.pz - 0.4f);
            AimRope(a, new Vector3(x, hull.position.y + 0.9f, z + 8.4f));
        }
    }

    // =====================================================================================
    // ---- his paws, whoever he is (web glPaws) ----
    // A rider who is a model is drawn from bones, so its own hands come first; the built riders
    // carry a paw on each wrist. A stand-in has neither, and then there are no lines to draw.
    bool Paws(out Vector3 f, out Vector3 b)
    {
        f = b = Vector3.zero;
        var pug = Pug(); if (pug == null) return false;
        var pr = ProcRider.Of(pug.gameObject);
        if (pr != null && pr.armF != null && pr.armB != null && pr.armF.wr != null && pr.armB.wr != null)
        {
            var pf = pr.armF.wr.Find("Paw"); var pb = pr.armB.wr.Find("Paw");
            f = (pf != null ? pf : pr.armF.wr).position; b = (pb != null ? pb : pr.armB.wr).position;
            return true;
        }
        if (pugSeen != pug)
        {
            pugSeen = pug; bones.Clear();
            foreach (var tr in pug.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(tr.name)) bones[tr.name] = tr;
        }
        Transform lf, rt;
        if (!bones.TryGetValue("LeftHand", out lf)) bones.TryGetValue("LeftForeArm", out lf);
        if (!bones.TryGetValue("RightHand", out rt)) bones.TryGetValue("RightForeArm", out rt);
        if (lf == null || rt == null) return false;
        f = lf.position; b = rt.position;
        return true;
    }

    // the rider is the child of the board group that is not the board
    Transform Pug()
    {
        if (rig == null) return null;
        for (int i = 0; i < rig.childCount; i++) { var c = rig.GetChild(i); if (!c.name.StartsWith("board:")) return c; }
        return null;
    }

    // ---- the rope: a tapered, laid tube bent through a little catenary and floored over the
    // swell, written in world space every frame (web aimRope) ----
    void AimRope(Vector3 a, Vector3 b)
    {
        var D = b - a; float len = D.magnitude;
        if (!(len > 1e-4f)) return;
        var T = D / len;
        // any pair of perpendiculars will do; a fixed reference keeps the frame from spinning
        var n1 = Mathf.Abs(T.y) > 0.9f ? Vector3.right : Vector3.up;
        var n2 = Vector3.Cross(T, n1).normalized;
        n1 = Vector3.Cross(n2, T).normalized;
        // it rides OVER the swell instead of through it: the centreline is lifted clear of whatever
        // the sea is doing under each ring, and the sag is kept small for the same reason
        float sag = len * 0.008f, rt = rider.t;
        for (int k = 0; k <= RopeSeg; k++)
        {
            float u = k / (float)RopeSeg;
            float lx = a.x + D.x * u, lz = a.z + D.z * u, ly = a.y + D.y * u - sag * 4f * u * (1f - u);
            float s = WaveField.Height(lx, WaveField.Scroll + lz, rt);
            ropeLift[k] = Mathf.Max(0f, (s + 0.55f) - ly);
        }
        int n = 0;
        for (int k = 0; k <= RopeSeg; k++)
        {
            float u = k / (float)RopeSeg;
            // deliberately wider at the boat, so it holds an even width on screen over a long run
            float rr = RopeR * (0.25f + 0.75f * u);
            var P = a + D * u; P.y += -sag * 4f * u * (1f - u) + ropeLift[k];
            for (int j = 0; j <= RopeRad; j++)
            {
                float ang = j / (float)RopeRad * Mathf.PI * 2f;
                ropeV[n++] = P + (n1 * Mathf.Cos(ang) + n2 * Mathf.Sin(ang)) * rr;
            }
        }
        ropeMesh.vertices = ropeV; ropeMesh.RecalculateBounds();
        // one turn of the lay every foot or so, however long the rope happens to be
        ropeMat.SetTextureScale("_BaseMap", new Vector2(1f, Mathf.Max(1f, len * 0.9f)));
    }

    // =====================================================================================
    // ---- the hull ----
    void BuildHull()
    {
        if (hull != null) return;
        hull = new GameObject("TowBoat").transform;
        // built at the origin, square, so the measurements below are in its own frame
        var fit = new GameObject("fit").transform; fit.SetParent(hull, false);
        var prefab = Resources.Load<GameObject>("Models/boat");
        if (prefab != null)
        {
            var m = Instantiate(prefab, fit); m.name = "boat"; Models.Repair(m);
            // the web's fitToBox: the long side down the lane, scaled until it is twenty feet, centred
            var b = Models.Bounds(m);
            if (b.size.x >= b.size.y && b.size.x >= b.size.z) m.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * m.transform.localRotation;
            else if (b.size.y >= b.size.x && b.size.y >= b.size.z) m.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * m.transform.localRotation;
            b = Models.Bounds(m);
            float k = BoxSize.z / Mathf.Max(1e-4f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            m.transform.localScale *= k;
            b = Models.Bounds(m);
            m.transform.localPosition -= b.center;
            // which end is the bow, off the KEEL: it sweeps up to the stem and sits flat and deep
            // at the transom; everything here drives along its local -z
            if (BowAt(m) > 0) fit.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var rd in m.GetComponentsInChildren<Renderer>(true)) { rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; rd.receiveShadows = true; }
        }
        else StandInHull(fit);
        // and it floats at a measured depth: the keel taken off the box in its own space
        var hb = Models.Bounds(fit.gameObject);
        if (hb.size.y > 1e-3f) keel = hb.min.y;
        hull.gameObject.SetActive(false);
    }

    // +1 when the bow (the end whose keel sits HIGHER) is at +z and the hull needs turning
    int BowAt(GameObject go)
    {
        var pts = new List<Vector3>();
        try
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (m == null || !m.isReadable) continue;
                var M = hull.worldToLocalMatrix * mf.transform.localToWorldMatrix; var v = m.vertices;
                for (int i = 0; i < v.Length; i += 3) pts.Add(M.MultiplyPoint3x4(v[i]));
            }
        }
        catch { pts.Clear(); }
        if (pts.Count == 0) return -1;
        float z0 = float.PositiveInfinity, z1 = float.NegativeInfinity;
        foreach (var p in pts) { if (p.z < z0) z0 = p.z; if (p.z > z1) z1 = p.z; }
        float zl = Mathf.Max(1e-6f, z1 - z0); const float Cut = 1f / 6f;
        float loN = 1e9f, loP = 1e9f; int nN = 0, nP = 0;
        foreach (var p in pts)
        {
            float u = (p.z - z0) / zl;
            if (u < Cut) { if (p.y < loN) loN = p.y; nN++; }
            else if (u > 1f - Cut) { if (p.y < loP) loP = p.y; nP++; }
        }
        if (nN == 0 || nP == 0) return -1;
        return loP > loN ? 1 : -1;
    }

    // the boat when boat.glb is missing: a white hull with a dark sheer stripe and a cabin
    static void StandInHull(Transform fit)
    {
        var white = Lit(0xf2f0ea, 0.45f); var dark = Lit(0x1f3a52, 0.3f); var glass = Lit(0x2b4a63, 0.8f);
        Box(fit, "hull", new Vector3(0f, -0.35f, 0.4f), new Vector3(5.6f, 2.5f, 18.6f), white);
        Box(fit, "bow", new Vector3(0f, -0.05f, -9.2f), new Vector3(3.6f, 1.9f, 2.2f), white);
        Box(fit, "stripe", new Vector3(0f, 0.55f, 0.4f), new Vector3(5.7f, 0.22f, 18.7f), dark);
        Box(fit, "cabin", new Vector3(0f, 1.05f, -1.5f), new Vector3(3.8f, 1.1f, 4.6f), white);
        Box(fit, "screen", new Vector3(0f, 1.15f, -3.85f), new Vector3(3.6f, 0.8f, 0.12f), glass);
    }

    // =====================================================================================
    // ---- the wing, the rope, the brakes and the bar: built the first time the line is taken ----
    void BuildRig()
    {
        if (glider != null) return;
        glider = new GameObject("Paraglider").transform;
        var prefab = Resources.Load<GameObject>("Models/paraglider");
        if (prefab != null) BuildModelWing(prefab);
        else BuildWing();
        glider.gameObject.SetActive(false);

        // the tow line: a tube of RopeRad sides and RopeSeg rings, re-written in world space each frame
        var rg = new GameObject("TowRope"); rope = rg.transform;
        ropeMesh = new Mesh { name = "rope" }; ropeMesh.MarkDynamic();
        int nv = (RopeSeg + 1) * (RopeRad + 1);
        ropeV = new Vector3[nv]; var uv = new Vector2[nv]; var idx = new int[RopeSeg * RopeRad * 6]; int q = 0;
        for (int k = 0; k <= RopeSeg; k++) for (int j = 0; j <= RopeRad; j++) uv[k * (RopeRad + 1) + j] = new Vector2(j / (float)RopeRad, k / (float)RopeSeg);
        for (int k = 0; k < RopeSeg; k++) for (int j = 0; j < RopeRad; j++)
        {
            int a = k * (RopeRad + 1) + j, b = a + 1, c = a + RopeRad + 1, d = c + 1;
            idx[q++] = a; idx[q++] = c; idx[q++] = b; idx[q++] = b; idx[q++] = c; idx[q++] = d;
        }
        ropeMesh.vertices = ropeV; ropeMesh.uv = uv; ropeMesh.triangles = idx;
        rg.AddComponent<MeshFilter>().sharedMesh = ropeMesh;
        var rr = rg.AddComponent<MeshRenderer>();
        // unlit, so it does not lose itself against the sun; both sides, since it is open
        ropeMat = Unlit(Color.white); ropeMat.SetTexture("_BaseMap", RopeTexture()); if (ropeMat.HasProperty("_Cull")) ropeMat.SetFloat("_Cull", 0f);
        rr.sharedMaterial = ropeMat; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
        rg.SetActive(false);

        // the two control lines, one mesh of two segments, the dark of the rest of the rigging
        var bg = new GameObject("Brakes"); brakes = bg.transform;
        brakeMesh = new Mesh { name = "brakes" }; brakeMesh.MarkDynamic();
        brakeMesh.vertices = brakeV; brakeMesh.SetIndices(new[] { 0, 1, 2, 3 }, MeshTopology.Lines, 0);
        bg.AddComponent<MeshFilter>().sharedMesh = brakeMesh;
        var br = bg.AddComponent<MeshRenderer>(); br.sharedMaterial = Unlit(Col(0x2f353d));
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
        bg.SetActive(false);

        // the bar the whole thing meets at: steel across both hands, a grip on each end, the rope
        // on its middle. Built along its own y, which is what it is turned to lie along.
        bar = new GameObject("ControlBar").transform;
        var steel = Lit(0x8d939c, 0.62f); var grip = Lit(0x1b1f25, 0.25f);
        barTube = Cyl(bar, "span", steel); barTube.localScale = new Vector3(0.11f, 0.5f, 0.11f);
        gripL = Cyl(bar, "gripL", grip); gripL.localScale = new Vector3(0.17f, 0.17f, 0.17f);
        gripR = Cyl(bar, "gripR", grip); gripR.localScale = new Vector3(0.17f, 0.17f, 0.17f);
        bar.gameObject.SetActive(false);
    }

    // The modelled canopy brings its own lines, converging on a riser point at the bottom, so it is
    // sized by its own proportions (the longest side is the span) and seated on that point, which
    // is the harness (web 31335-31610). It flies in the texture it shipped with (GL_PAINT false),
    // and it is FABRIC whatever the exporter said: no metal, no gloss.
    void BuildModelWing(GameObject prefab)
    {
        var m = Instantiate(prefab, glider); m.name = "paraglider"; Models.Repair(m);
        var b = Models.Bounds(m);
        float k = GlSpan / Mathf.Max(1e-4f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        m.transform.localScale *= k;
        b = Models.Bounds(m);
        m.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        b = Models.Bounds(m);
        float line = b.size.y;
        foreach (var rd in m.GetComponentsInChildren<Renderer>(true))
        {
            rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var mt in rd.materials)
            {
                if (mt == null) continue;
                foreach (var p in new[] { "_Metallic", "metallicFactor" }) if (mt.HasProperty(p)) mt.SetFloat(p, 0f);
                if (mt.HasProperty("_Smoothness")) mt.SetFloat("_Smoothness", 0f);
                if (mt.HasProperty("roughnessFactor")) mt.SetFloat("roughnessFactor", 1f);
            }
        }
        // the brakes: the box-derived pair first, then the outboard corner of the trailing edge
        // taken off the fabric itself, if the mesh can be read
        brakeL = new Vector3(b.min.x * 0.74f, line * 0.88f, b.max.z * 0.55f);
        brakeR = new Vector3(b.max.x * 0.74f, line * 0.88f, b.max.z * 0.55f);
        MeasureBrakes(m);
    }

    // Fabric or rigging, by height under the canopy's own roof in each column across the span; how
    // deep the fabric goes is the first slice below the widest one whose coverage of the span falls
    // under 72% of it; each brake is the fabric vertex furthest out and furthest back on its side.
    void MeasureBrakes(GameObject m)
    {
        var pts = new List<Vector3>();
        try
        {
            foreach (var mf in m.GetComponentsInChildren<MeshFilter>(true))
            {
                var ms = mf.sharedMesh; if (ms == null || !ms.isReadable) continue;
                var M = glider.worldToLocalMatrix * mf.transform.localToWorldMatrix; var v = ms.vertices;
                for (int i = 0; i < v.Length; i++) pts.Add(M.MultiplyPoint3x4(v[i]));
            }
        }
        catch { pts.Clear(); }
        if (pts.Count == 0) return;
        float y0 = float.PositiveInfinity, y1 = float.NegativeInfinity, xa = float.PositiveInfinity, xb = float.NegativeInfinity;
        foreach (var p in pts) { y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); xa = Mathf.Min(xa, p.x); xb = Mathf.Max(xb, p.x); }
        float h = Mathf.Max(0.001f, y1 - y0), w = Mathf.Max(0.001f, xb - xa);
        const int NB = 64, NR = 48;
        int Bin(float px) => Mathf.Clamp(Mathf.FloorToInt((px - xa) / w * NB), 0, NB - 1);
        var top = new float[NB]; for (int i = 0; i < NB; i++) top[i] = -1e9f;
        foreach (var p in pts) { int bi = Bin(p.x); if (p.y > top[bi]) top[bi] = p.y; }
        var t2 = (float[])top.Clone();
        for (int i = 0; i < NB; i++) top[i] = Mathf.Max(t2[Mathf.Max(0, i - 1)], Mathf.Max(t2[i], t2[Mathf.Min(NB - 1, i + 1)]));
        var seen = new bool[NR, NB];
        foreach (var p in pts) seen[Mathf.Clamp(Mathf.FloorToInt((y1 - p.y) / h * NR), 0, NR - 1), Bin(p.x)] = true;
        float peak = 0f; int at = 0; var cov = new float[NR];
        for (int r = 0; r < NR; r++) { int c = 0; for (int i = 0; i < NB; i++) if (seen[r, i]) c++; cov[r] = c / (float)NB; if (cov[r] > peak) { peak = cov[r]; at = r; } }
        int drop = NR;
        for (int r = at + 1; r < NR; r++) if (cov[r] < peak * 0.72f) { drop = r; break; }
        float fabD = Mathf.Clamp(drop / (float)NR, 0.12f, 0.45f);
        float sL = -1e9f, sR = -1e9f; Vector3 bL = brakeL, bR = brakeR;
        foreach (var p in pts)
        {
            if (top[Bin(p.x)] - p.y >= h * fabD) continue;     // rigging, not fabric
            float sc = Mathf.Abs(p.x) * 0.75f + p.z * 0.35f;
            if (p.x < 0f) { if (sc > sL) { sL = sc; bL = p; } }
            else if (sc > sR) { sR = sc; bR = p; }
        }
        if (sL > -1e8f && sR > -1e8f) { brakeL = bL; brakeR = bR; }
    }

    // The built wing, for when paraglider.glb is missing (web buildGlider): an arc of sixteen cells
    // with the tips hanging below the middle, each its own panel in a band of colour, lit through
    // (the colours pushed past one), with string lines down to the riser and a little camber.
    void BuildWing()
    {
        const float SPAN = 17f, CHORD = 5.4f, ARC = 0.66f, LINE = 9.4f, GLOW = 1.85f; const int NC = 16;
        float R = SPAN * 0.5f / Mathf.Sin(ARC);
        uint[] bands = { 0xff5a3c, 0xffd23f, 0xf7f7f4, 0x35c8f0, 0x2b6fd6, 0xf7f7f4 };
        Vector2 St(int i) { float a = (i / (float)NC - 0.5f) * ARC * 2f; return new Vector2(R * Mathf.Sin(a), R * Mathf.Cos(a) - R); }
        var go = new GameObject("wing"); go.transform.SetParent(glider, false);
        var mesh = new Mesh { name = "wing" };
        var v = new List<Vector3>(); var subs = new List<int>[bands.Length];
        for (int s = 0; s < bands.Length; s++) subs[s] = new List<int>();
        for (int i = 0; i < NC; i++)
        {
            var p0 = St(i); var p1 = St(i + 1); int bs = v.Count;
            foreach (var p in new[] { p0, p1 }) for (int j = 0; j <= 1; j++) v.Add(new Vector3(p.x, LINE + p.y + (j == 1 ? 0.42f : 0f), (j - 0.5f) * CHORD));
            subs[i % bands.Length].AddRange(new[] { bs, bs + 1, bs + 2, bs + 1, bs + 3, bs + 2 });
        }
        mesh.SetVertices(v); mesh.subMeshCount = bands.Length;
        for (int s = 0; s < bands.Length; s++) mesh.SetTriangles(subs[s], s);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mats = new Material[bands.Length];
        for (int s = 0; s < bands.Length; s++)
        {
            mats[s] = Lit(bands[s], 0.25f); mats[s].SetColor("_BaseColor", Col(bands[s]) * GLOW);
            if (mats[s].HasProperty("_Cull")) mats[s].SetFloat("_Cull", 0f);
        }
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        // the lines, from under the wing down to the risers over his head
        var lp = new List<Vector3>(); var li = new List<int>();
        for (int i = 1; i < NC; i += 2)
        {
            float a = (i / (float)NC - 0.5f) * ARC * 2f, lx = R * Mathf.Sin(a), ly = R * Mathf.Cos(a) - R;
            foreach (var e in new[] { new Vector3(lx, LINE + ly, -CHORD * 0.5f), new Vector3(lx, LINE + ly + 0.42f, CHORD * 0.5f) })
            { li.Add(lp.Count); lp.Add(e); li.Add(lp.Count); lp.Add(new Vector3(0f, 0f, 0.2f)); }
        }
        var lg = new GameObject("lines"); lg.transform.SetParent(glider, false);
        var lm = new Mesh { name = "lines" }; lm.SetVertices(lp); lm.SetIndices(li, MeshTopology.Lines, 0); lm.RecalculateBounds();
        lg.AddComponent<MeshFilter>().sharedMesh = lm;
        var lr = lg.AddComponent<MeshRenderer>(); lr.sharedMaterial = Unlit(Col(0xe8e4d8)); lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        brakeL = new Vector3(-SPAN * 0.38f, LINE * 0.90f, CHORD * 0.42f);
        brakeR = new Vector3(SPAN * 0.38f, LINE * 0.90f, CHORD * 0.42f);
    }

    // The lay of a rope: three strands wound along it, bright on their crowns and dark in the
    // grooves, with the tube's roundness baked across it, in hemp rather than paper (web ropeTexture)
    static Texture2D ropeTex;
    static Texture2D RopeTexture()
    {
        if (ropeTex != null) return ropeTex;
        const int S = 64;
        ropeTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "rope", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        var px = new Color32[S * S];
        for (int j = 0; j < S; j++) for (int i = 0; i < S; i++)
        {
            float u = i / (float)S, v = j / (float)S;
            float ph = (u * 3f + v) % 1f; if (ph < 0f) ph += 1f;
            float crown = Mathf.Pow(Mathf.Sin(Mathf.PI * ph), 0.55f);
            float limb = 0.70f + 0.30f * Mathf.Cos((u - 0.28f) * 6.2831853f);
            float g = (0.46f + 0.54f * crown) * limb;
            px[j * S + i] = new Color32((byte)Mathf.Min(255, Mathf.RoundToInt(255f * g)), (byte)Mathf.Min(255, Mathf.RoundToInt(255f * g * 0.95f)),
                                        (byte)Mathf.Min(255, Mathf.RoundToInt(255f * g * 0.82f)), 255);
        }
        ropeTex.SetPixels32(px); ropeTex.Apply(true, false);
        return ropeTex;
    }

    // ---- the GRAB button: its own slot above JUMP (bottom 238, 114 by 94, a 3 px ring, the
    // left corners cut 30), cut flat against the right edge (web #grabBtn) ----
    void BuildButton()
    {
        if (btn != null || ui == null) return;
        var sp = UiKit.Rounded(114, 94, 30, 0, 0, 30, UiKit.Body.Glass, 3f, false, true);
        btnRt = UiKit.Rect(ui, "grab", new Vector2(1, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-57f, 238f + 47f), new Vector2(114, 94));
        halo = UiKit.Pic(btnRt, "halo", sp, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        halo.raycastTarget = false;
        var face = UiKit.Pic(btnRt, "face", sp, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UiKit.Label(face.transform, "GRAB!", 16, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        face.raycastTarget = true;
        UiKit.Pressable(face.gameObject, TakeLine);
        btn = btnRt.gameObject; btn.SetActive(false);
    }

    // =====================================================================================
    static Color Col(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

    static Material Lit(uint hex, float smooth)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Col(hex));
        if (m.HasProperty("_Color")) m.SetColor("_Color", Col(hex));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static Material Unlit(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh != null ? sh : Shader.Find("Unlit/Color"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        return m;
    }

    static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.name = name;
        var col = b.GetComponent<Collider>(); if (col) Destroy(col);
        b.transform.SetParent(parent, false); b.transform.localPosition = pos; b.transform.localScale = size;
        b.GetComponent<Renderer>().sharedMaterial = m;
    }

    static Transform Cyl(Transform parent, string name, Material m)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name = name;
        var col = c.GetComponent<Collider>(); if (col) Destroy(col);
        c.transform.SetParent(parent, false);
        var rd = c.GetComponent<Renderer>(); rd.sharedMaterial = m; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return c.transform;
    }
}
