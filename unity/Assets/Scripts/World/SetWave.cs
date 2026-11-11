// The set wave, as the web game stages it: a tow plane goes down off one side, the impact
// raises a swell that comes across the lane to the rider and stands up into a wall he rides
// along, scored by how deep he sits in it, until it spits him out. The wall itself is one
// more term in the shared height field (WaveField.SetWaveH), so the board rides it and the
// sea shader lights and foams it without being told. The curling lip is the next stage.
using UnityEngine;

public class SetWave : MonoBehaviour
{
    public enum Phase { Idle = -1, Fly, Impact, Swell, Ride, Exit }
    public BoardController rider;
    public System.Action<Vector3, int> onSplash;

    public const float CrashX = 46f, HoldX = 9.0f, Tube = 4.6f;
    public const float FlyS = 5.2f, ImpS = 1.3f, SwellS = 5.0f, OutS = 2.6f;
    public const float SetWaveAt = 5000f;      // metres: the first set comes late in a long run

    public Phase ph = Phase.Idle;
    public float t, side = 1f, ride, rideMax, pts, deep, over, cam, close, form, holdX, next = 30f;
    public int loops;
    bool armed, sweep2;
    Transform plane; Vector3 planePrev; bool planePrevOk;
    public bool summon;      // debug: bring one now

    void Awake() { next = 30f + Random.value * 15f; }

    public void Reset()
    {
        ph = Phase.Idle; WaveField.SetA = 0; cam = 0; close = 0; form = 0; WaveField.Calm = 1f;
        next = 30f + Random.value * 15f; armed = false;
        if (plane != null) plane.gameObject.SetActive(false);
    }

    void EnsurePlane()
    {
        if (plane != null) return;
        var go = Models.Spawn("plane", null, 2f, PrimitiveType.Cube);
        Models.FitLength(go, 22f);
        go.name = "TowPlane";
        plane = go.transform; plane.gameObject.SetActive(false);
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
            }
            return;
        }
        if (ph == Phase.Ride)
        {
            WaveField.SetA = WaveField.SwPeak; WaveField.SetX = holdX; cam = Mathf.Min(1f, cam + dt * 1.6f);
            form = Mathf.Min(1f, form + dt * 0.26f);
            WaveField.Calm += (0.22f - WaveField.Calm) * Mathf.Min(1f, dt * 1.4f);     // the sea goes glassy inside the tube
            if (form >= 0.95f) ride += dt;
            float c = holdX - side * Tube;
            float dp = (r.px - c) * side;
            float sweet = Mathf.Max(0f, 1 - Mathf.Abs(dp - 1.2f) / 3.2f);
            deep = Mathf.Max(deep, sweet);
            float rate = (240f + 960f * sweet * sweet) * Mathf.Max(0f, 1 - over / 45f);
            r.score += Mathf.RoundToInt(rate * dt); pts += rate * dt;
            if (ride > rideMax) { ph = Phase.Exit; t = 0; Spit(); }
            return;
        }
        // Exit
        float kk = Mathf.Min(1f, t / OutS);
        close = kk; WaveField.SetA = WaveField.SwPeak * (1 - kk * kk * 0.85f); cam = Mathf.Max(0f, 0.9f - kk);
        WaveField.Calm += (1f - WaveField.Calm) * Mathf.Min(1f, dt * 0.8f);
        if (t >= OutS) End();
    }

    void Begin()
    {
        EnsurePlane();
        ph = Phase.Fly; t = 0; summon = false;
        side = Random.value < 0.5f ? 1f : -1f;
        rideMax = 10f + Random.value * 8f;
        ride = 0; pts = 0; deep = 0; sweep2 = false; cam = 0; form = 0;
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
        ph = Phase.Idle; WaveField.SetA = 0; cam = 0; close = 0; form = 0; WaveField.Calm = 1f;
        next = 45f + Random.value * 30f;
    }
}
