// The set wave, as the web game stages it: a tow plane goes down off one side, the impact
// raises a swell that comes across the lane to the rider and stands up into a wall he rides
// along, scored by how deep he sits in it, until it spits him out. The wall itself is one
// more term in the shared height field (WaveField.SetWaveH), so the board rides it and the
// sea shader lights and foams it without being told.
//
// Stage two, the barrel: a lip (CurlShell, drawn with Surf/Curl) grows out of the crest and
// arches over him across about four seconds, and once there is a bore worth riding the ring
// takes charge of the board. He goes round the inside of the tube, steered by the same
// left-right input, the surface found angle by angle by firing a ray out from the tube's
// axis to the first water it meets. A whole turn is a loop and pays; a jump in there pushes
// off the wall toward the axis and the pipe pulls him back. The chase camera holds still
// while he is on the ring, as the web's does.
using UnityEngine;

public class SetWave : MonoBehaviour
{
    public enum Phase { Idle = -1, Fly, Impact, Swell, Ride, Exit }
    public BoardController rider;
    public DayCycle day;
    public System.Action<Vector3, int> onSplash;

    public const float CrashX = 46f, HoldX = 9.0f, Tube = 4.6f;
    public const float FlyS = 5.2f, ImpS = 1.3f, SwellS = 5.0f, OutS = 2.6f;
    public const float SetWaveAt = 5000f;      // metres: the first set comes late in a long run
    public const float Trim = -7.0f;           // how far down the line he trims in the barrel
    const float PipeG = 9.5f, PipePop = 5.2f;
    const float LandYaw = 0.88f, LandFlip = 0.72f;   // the same windows a landing anywhere else is judged by

    public Phase ph = Phase.Idle;
    public float t, side = 1f, ride, rideMax, pts, deep, over, cam, close, form, holdX, next = 30f;
    public float curlA;                        // how much of the lip there is to see
    public bool tubeRide, onLip, pipeAir;      // is the ring in charge; is he on the thrown lip; off the wall
    public int loops;
    public float ang, angV, rad, radV, airT, cut;
    bool armed, sweep2;
    Transform plane; Vector3 planePrev; bool planePrevOk;
    GameObject curl; Material curlMat;
    public bool summon;      // debug: bring one now

    void Awake() { next = 30f + Random.value * 15f; }

    public void Reset()
    {
        ph = Phase.Idle; WaveField.SetA = 0; cam = 0; close = 0; form = 0; curlA = 0; WaveField.Calm = 1f;
        next = 30f + Random.value * 15f; armed = false;
        tubeRide = false; onLip = false; pipeAir = false; ang = 0; angV = 0; rad = float.NaN; radV = 0; loops = 0; cut = 0;
        if (plane != null) plane.gameObject.SetActive(false);
        if (curl != null) curl.SetActive(false);
    }

    void EnsurePlane()
    {
        if (plane != null) return;
        var go = Models.Spawn("plane", null, 2f, PrimitiveType.Cube);
        Models.FitLength(go, 22f);
        go.name = "TowPlane";
        plane = go.transform; plane.gameObject.SetActive(false);
    }

    void EnsureCurl()
    {
        if (curl != null) return;
        curl = CurlShell.Make(out curlMat);
    }

    // ---- the tube's own axis and bore, read out of the lip's geometry ----
    float Pitch => -side * 1.15f * (1 - form) * (1 - form);
    public float TubeC => holdX - side * Tube;

    public void TubeGeom(float zz, out float cx, out float cy, out float r)
    {
        float pa = WaveField.PeelA(zz), pb = WaveField.PeelB(zz);
        float rd = CurlShell.R * (0.16f + 0.84f * pa) * (1 - 0.55f * pb);
        float fold = (1 - 0.38f * pb) * (1 - 0.72f * close) * form;
        float drop = -WaveField.SwPeak * 0.55f * pb;
        float ox = -side * Mathf.Sin(CurlShell.Alpha) * rd * fold;
        float oy = (-Mathf.Cos(CurlShell.Alpha) * rd + drop) * fold;
        float rz = Pitch, cr = Mathf.Cos(rz), sr = Mathf.Sin(rz);
        cx = WaveField.SetX + ox * cr - oy * sr;
        cy = WaveField.SetA + ox * sr + oy * cr;
        r = Mathf.Max(0.5f, rd * fold - 0.35f);
    }

    public void Step(float dt)
    {
        var r = rider;
        if (ph == Phase.Idle)
        {
            if (summon || r.dist > SetWaveAt)
            {
                if (!summon && !armed) { armed = true; next = Mathf.Min(next, 1.8f); }
                next -= dt;
                if (next <= 0 || summon) Begin();
            }
            Trim2(dt);
            return;
        }
        t += dt;
        if (ph == Phase.Fly)
        {
            float k = Mathf.Min(1f, t / FlyS), e = k * k * (3 - 2 * k);
            float x0 = r.px + side * 6f, x1 = side * CrashX;
            float nx = x0 + (x1 - x0) * e, ny = 26f - 24.2f * k * k, nz = -330f + 162f * (k * (2 - k));
            var p = new Vector3(nx, ny, nz);
            if (planePrevOk)
            {
                var d = p - planePrev;
                if (d.sqrMagnitude > 1e-7f) plane.rotation = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0, 180f, 0);
            }
            plane.position = p; planePrev = p; planePrevOk = true;
            plane.rotation *= Quaternion.Euler(-(0.18f + 1.27f * e) * Mathf.Rad2Deg * 0.35f, 0, side * 0.34f * Mathf.Min(1f, k * 1.4f) * Mathf.Rad2Deg);
            if (t >= FlyS)
            {
                ph = Phase.Impact; t = 0; plane.gameObject.SetActive(false);
                onSplash?.Invoke(new Vector3(side * CrashX, 0, -168f), 120);
                r.Say("IMPACT!");
            }
            return;
        }
        if (ph == Phase.Impact)
        {
            if (t < 0.6f && Random.value < dt * 20f) onSplash?.Invoke(new Vector3(side * CrashX + Random.Range(-5f, 5f), 1f, -168f + Random.Range(-5f, 5f)), 12);
            if (t >= ImpS) { ph = Phase.Swell; t = 0; }
            return;
        }
        if (ph == Phase.Swell)
        {
            float k = Mathf.Min(1f, t / SwellS), e = k * k * (3 - 2 * k);
            WaveField.SetA = WaveField.SwPeak * (0.05f + 0.95f * e);
            float hold = r.px + side * Tube;
            WaveField.SetX = side * CrashX + (hold - side * CrashX) * e;
            WaveField.SetS = side;
            cam = e * 0.75f;
            if (k > 0.55f && !sweep2) { sweep2 = true; r.Say("SET WAVE - IT IS COMING TO YOU!"); }
            if (t >= SwellS)
            {
                holdX = WaveField.SetX; ph = Phase.Ride; t = 0; ride = 0; over = 0; close = 0; form = 0; loops = 0;
                ang = 0; angV = 0;
            }
            PlaceCurl(dt);
            return;
        }
        if (ph == Phase.Ride)
        {
            WaveField.SetA = WaveField.SwPeak; WaveField.SetX = holdX; cam = Mathf.Min(1f, cam + dt * 1.6f);
            form = Mathf.Min(1f, form + dt * 0.26f);      // the barrel builds
            curlA = Mathf.Min(1f, form * 2.2f);
            WaveField.Calm += (0.22f - WaveField.Calm) * Mathf.Min(1f, dt * 1.4f);     // the sea goes glassy inside the tube
            bool circling = Mathf.Abs(angV) > 0.7f;        // keep it turning and the wave waits
            if (!circling && form >= 0.95f) ride += dt; else if (circling) over += dt;
            float c = TubeC;
            float dp = (r.px - c) * side;
            float sweet = Mathf.Max(0f, 1 - Mathf.Abs(dp - 1.2f) / 3.2f);
            deep = Mathf.Max(deep, sweet);
            float rate = (240f + 960f * sweet * sweet) * Mathf.Max(0f, 1 - over / 45f);
            r.score += Mathf.RoundToInt(rate * dt); pts += rate * dt;
            Ring(dt);
            PlaceCurl(dt);
            Trim2(dt);
            if (ride > rideMax) { ph = Phase.Exit; t = 0; Spit(); }
            return;
        }
        // Exit: the lip collapses onto the face and the section turns to whitewater
        float kk = Mathf.Min(1f, t / OutS);
        close = kk; WaveField.SetA = WaveField.SwPeak * (1 - kk * kk * 0.85f); cam = Mathf.Max(0f, 0.9f - kk);
        curlA = Mathf.Max(0f, 1 - kk * 1.15f);
        WaveField.Calm += (1f - WaveField.Calm) * Mathf.Min(1f, dt * 0.8f);
        Ring(dt);
        PlaceCurl(dt);
        Trim2(dt);
        if (t >= OutS) End();
    }

    // down the line while he is in the barrel, back to the lane's own spot after
    void Trim2(float dt)
    {
        var r = rider;
        float pzT = (tubeRide && form > 0.6f) ? Trim : 0f;
        r.pz += (pzT - r.pz) * Mathf.Min(1f, dt * (tubeRide ? 1.3f : 2.2f));
    }

    // ---- the ring: where the board goes while the wave is hollow ----
    void Ring(float dt)
    {
        var r = rider;
        bool wasTube = tubeRide;
        tubeRide = false; onLip = false;
        if (ph == Phase.Ride && curlA > 0.55f)
        {
            TubeGeom(r.pz, out float cx, out float cy, out float R);
            // the barrel does not let go: a tether toward the axis, daylight side only
            float lim = Mathf.Max(2.5f, R + 0.8f);
            float dpc = (r.px - cx) * side;
            if (dpc < -lim) { r.px += ((cx - side * lim) - r.px) * Mathf.Min(1f, dt * 10f); r.vx -= r.vx * Mathf.Min(1f, dt * 8f); }
            if (R > (wasTube ? 0.8f : 1.6f))
            {
                tubeRide = true;
                if (!wasTube)
                {
                    // join the ring wherever he already is
                    ang = Mathf.Atan2((r.px - cx) * side, -(r.py - cy));
                    angV = r.vx * side / Mathf.Max(1.5f, R);
                    rad = Mathf.Sqrt((r.px - cx) * (r.px - cx) + (r.py - cy) * (r.py - cy)); radV = 0; pipeAir = false;
                    if (r.airborne) { r.airborne = false; r.vy = 0; }
                }
                // a jump pressed on the ring pushes off the wall toward the axis
                if (r.airborne && !pipeAir) { pipeAir = true; radV = -PipePop * r.statAir; airT = 0; r.vy = 0; }
                angV += r.steer * side * 3.4f * r.statTurn * dt;
                angV -= angV * 1.7f * dt;
                angV = Mathf.Clamp(angV, -3.6f, 3.6f);
                float was = ang;
                float lipStart = Mathf.PI - CurlShell.Alpha;
                float pa = WaveField.PeelA(r.pz);
                float lipSpan = CurlShell.Sweep * (0.30f + 0.70f * pa);
                float WZ2 = WaveField.Scroll + r.pz;
                float thEnd = lipStart + lipSpan;
                float tipX = cx + side * Mathf.Sin(thEnd) * R, tipY = cy - Mathf.Cos(thEnd) * R;
                float fz = Pitch, fdx = Mathf.Sin(fz), fdy = -Mathf.Cos(fz);
                float fallL = CurlShell.Fall * pa * (R + 0.35f);
                float foamCov = Mathf.Clamp01((r.pz + 30f) / 24f) * Mathf.Clamp01((pa - 0.22f) / 0.30f) * Mathf.Min(1f, form * 1.6f);
                float SurfAt(float th)
                {
                    float rl = Mathf.Repeat(th - lipStart, Mathf.PI * 2f);
                    bool onArc = rl <= lipSpan;
                    if (onArc) return R;
                    float sx = side * Mathf.Sin(th), sy = -Mathf.Cos(th);
                    float curt = float.PositiveInfinity;
                    if (fallL > 0.01f)
                    {
                        float det = sx * (-fdy) - sy * (-fdx);
                        if (Mathf.Abs(det) > 1e-6f)
                        {
                            float rx = tipX - cx, ry = tipY - cy;
                            float tt = (rx * (-fdy) - ry * (-fdx)) / det, s = (sx * ry - sy * rx) / det;
                            if (tt > 0.4f && s >= 0 && s <= fallL) curt = tt;
                        }
                    }
                    float sea = float.PositiveInfinity, lo = 0.4f, hi = 40f; bool found = false;
                    const float ST = (40f - 0.4f) / 132f;
                    for (int i = 1; i <= 132; i++)
                    {
                        float tt = 0.4f + ST * i;
                        if (cy + sy * tt <= WaveField.Height(cx + sx * tt, WZ2, r.t)) { hi = tt; lo = tt - ST; found = true; break; }
                    }
                    if (found)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            float mid = (lo + hi) * 0.5f;
                            if (cy + sy * mid <= WaveField.Height(cx + sx * mid, WZ2, r.t)) hi = mid; else lo = mid;
                        }
                        sea = hi - 0.30f;
                    }
                    float below = Mathf.Repeat(lipStart - th, Mathf.PI * 2f);
                    if (below < 0.45f) sea = Mathf.Min(sea, R);
                    float v = Mathf.Min(sea, curt);
                    float rW = Mathf.Max(0.5f, float.IsInfinity(v) ? R : v);
                    return Mathf.Max(0.5f, rW + (R - rW) * foamCov);
                }
                // paced by distance along the wall, not by angle
                const float e = 0.06f;
                float dr = (SurfAt(ang + e) - SurfAt(ang - e)) / (2 * e);
                float rN = Mathf.Max(1.5f, SurfAt(ang));
                float stretch = Mathf.Clamp(Mathf.Sqrt(rN * rN + dr * dr) / rN, 1f, 5f);
                ang += angV * dt / stretch;
                if (Mathf.Floor(ang / (Mathf.PI * 2f)) != Mathf.Floor(was / (Mathf.PI * 2f)))
                {
                    loops++; r.score += 450; pts += 450; r.Say("LOOP THE TUBE! +450");
                }
                float rel = Mathf.Repeat(ang - lipStart, Mathf.PI * 2f);
                onLip = rel <= lipSpan;
                float ux = side * Mathf.Sin(ang), uy = -Mathf.Cos(ang);
                float rHit = SurfAt(ang);
                if (float.IsNaN(rad)) { rad = Mathf.Sqrt((r.px - cx) * (r.px - cx) + (r.py - cy) * (r.py - cy)); radV = 0; pipeAir = false; }
                if (pipeAir)
                {
                    airT += dt;
                    if (airT > 1.4f) { rad = rHit; radV = 0; pipeAir = false; PipeLand(); }
                    else
                    {
                        radV += PipeG * dt; rad += radV * dt;
                        if (radV > 0 && rad >= rHit) { rad = rHit; radV = 0; pipeAir = false; PipeLand(); }
                    }
                    if (pipeAir) r.airborne = true;
                }
                else
                {
                    rad += (rHit - rad) * Mathf.Min(1f, dt * (wasTube ? 16f : 4f));
                    r.airborne = false;
                }
                if (r.wipe) return;
                r.px = cx + ux * rad;
                r.py = Mathf.Max(cy + uy * rad, WaveField.Height(r.px, WZ2, r.t) + 0.30f);
                r.vx = 0; r.vy = 0;
                r.carve = Mathf.Lerp(r.carve, Mathf.Clamp(angV / 2.4f, -1f, 1f), Mathf.Min(1f, dt * 6f));
                // the board lies on the wall: its deck faces the tube's axis
                r.transform.position = new Vector3(r.px, r.py, r.pz);
                var trick = Quaternion.Euler(r.pitchRot * Mathf.Rad2Deg, r.spinRot * Mathf.Rad2Deg, r.flipRot * Mathf.Rad2Deg);
                var wall = Quaternion.FromToRotation(Vector3.up, new Vector3(-ux, -uy, 0f).normalized);
                r.transform.rotation = wall * trick;
            }
        }
        else if (ph != Phase.Ride) { ang = 0; angV = 0; }
        if (!tubeRide && pipeAir) { pipeAir = false; radV = 0; }   // the wave ended mid-air
        if (!tubeRide) rad = float.NaN;
    }

    // back on the wall, judged exactly as a landing anywhere else is
    void PipeLand()
    {
        var r = rider;
        bool spun = r.spinRot > 0.4f || r.flipRot > 0.4f || r.pitchRot > 0.4f;
        if (spun)
        {
            float offS = Mathf.Abs(Mathf.Repeat(r.spinRot + Mathf.PI / 2f, Mathf.PI) - Mathf.PI / 2f);
            float offF = Mathf.Max(Mathf.Abs(Mathf.Repeat(r.flipRot + Mathf.PI, Mathf.PI * 2f) - Mathf.PI),
                                   Mathf.Abs(Mathf.Repeat(r.pitchRot + Mathf.PI, Mathf.PI * 2f) - Mathf.PI));
            if (offS < LandYaw && offF < LandFlip) r.Settle(true);
            else { r.Settle(false); r.airborne = false; r.vy = 0; r.Wipeout(offF >= LandFlip ? "under" : "land"); return; }
        }
        else r.Settle(false);
        r.airborne = false; r.vy = 0; r.poseLand = 0.7f;
        onSplash?.Invoke(new Vector3(r.px, r.py, r.pz), 14);
    }

    // the lip, sat on the crest: pitched over as it forms, folded down as it closes out
    void PlaceCurl(float dt)
    {
        if (curlA <= 0.004f) { if (curl != null) curl.SetActive(false); return; }
        EnsureCurl();
        curl.SetActive(true);
        float fold = (1 - 0.72f * close) * form;
        var tr = curl.transform;
        tr.position = new Vector3(WaveField.SetX, WaveField.SetA, 0f);
        tr.rotation = Quaternion.Euler(0, 0, Pitch * Mathf.Rad2Deg);
        tr.localScale = new Vector3(-side * Mathf.Max(0.001f, fold), Mathf.Max(0.001f, fold), 1f);
        cut += ((tubeRide ? 1f : 0f) - cut) * Mathf.Min(1f, dt * 5f);
        curlMat.SetFloat("_Fade", curlA);
        curlMat.SetFloat("_T", rider.t);
        curlMat.SetFloat("_Close", close);
        curlMat.SetFloat("_Cut", cut);
        if (day != null)
        {
            curlMat.SetVector("_SunDir", day.sunDir);
            curlMat.SetFloat("_Light", 0.18f + 0.82f * day.daylight);
        }
    }

    void Begin()
    {
        EnsurePlane();
        ph = Phase.Fly; t = 0; summon = false;
        side = Random.value < 0.5f ? 1f : -1f;
        rideMax = 10f + Random.value * 8f;
        ride = 0; pts = 0; deep = 0; sweep2 = false; cam = 0; form = 0; curlA = 0; loops = 0;
        WaveField.SetA = 0; WaveField.SetX = side * CrashX; WaveField.SetS = side;
        plane.position = new Vector3(rider.px + side * 6f, 26f, -330f); plane.rotation = Quaternion.identity; planePrevOk = false;
        plane.gameObject.SetActive(true);
        rider.Say(side > 0 ? "PLANE GOING DOWN - STARBOARD!" : "PLANE GOING DOWN - PORT!");
    }

    void Spit()
    {
        int bonus = Mathf.RoundToInt(600 + pts * 0.35f + deep * 1400f + loops * 300f);
        rider.score += bonus;
        rider.Say(loops > 0 ? "SPAT OUT! " + loops + " LOOP" + (loops > 1 ? "S" : "") + " +" + bonus
                : (ride + over > 11f ? "SPAT OUT! DEEP BARREL +" + bonus : "OUT CLEAN! +" + bonus));
    }

    void End()
    {
        ph = Phase.Idle; WaveField.SetA = 0; cam = 0; close = 0; form = 0; curlA = 0; WaveField.Calm = 1f;
        tubeRide = false; pipeAir = false; rad = float.NaN;
        if (curl != null) curl.SetActive(false);
        next = 45f + Random.value * 30f;
    }
}
