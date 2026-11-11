// The ride, ported number for number from the web game: the speed curve, the carve, the hop,
// the three held rotations, how a landing is judged and what it pays, and the crash.
// Feet and seconds throughout. +x is right, -z is ahead; the water scrolls past the rider.
using UnityEngine;

public class BoardController : MonoBehaviour
{
    // ---- the board's stats, Astro's originals ----
    public float statSpeed = 1f, statTurn = 1f, statAir = 1f, statGrip = 1f, statTop = 0f;

    // ---- the speed curve ----
    const float SpdLo = 16.2f, SpdHi = 52f;
    const float SpdK = 210f * (SpdHi - SpdLo);
    const float FtToM = 0.3048f;
    public const float BoardHalfL = 2.45f, BoardHalfW = 0.55f;
    const float LaneLim = 17f;
    const float LandYaw = 0.88f, LandFlip = 0.72f;
    const float SpinRate = Mathf.PI * 2f * 1.35f, FlipRate = Mathf.PI * 2f * 1.25f;
    const float Turn = Mathf.PI * 2f;

    // ---- state ----
    public float px, py, pz, vx, vy;
    public float speed, eff, dist, boost, trim;
    public bool airborne, running, wipe;
    public float wipeT;
    public int score;
    public float spinRot, flipRot, pitchRot;
    public bool spinHeld, flipHeld, pitchHeld;
    public float steer;                 // -1..1 from the input layer
    public float waterY, carve;
    public float t;                     // the wave clock
    public string lastMsg; public float msgT;
    public int coins, bonusCoins;      // puka from distance, plus what the chest and the rest hand over

    // ---- the pose beats both rigs read ----
    public float poseTake, poseCrouch, poseExt, poseAir, poseLand;

    public System.Action<Vector3, int> onSplash;   // where, how many drops
    public System.Action onWipeout;

    public void Begin()
    {
        px = 0; py = 0; pz = 0; vx = 0; vy = 0; dist = 0; speed = SpdLo; eff = SpdLo; boost = 0; trim = 0;
        airborne = false; running = true; wipe = false; wipeT = 0; score = 0; coins = 0; bonusCoins = 0;
        spinRot = flipRot = pitchRot = 0; spinHeld = flipHeld = pitchHeld = false;
        poseTake = poseCrouch = poseExt = poseAir = poseLand = 0;
        transform.rotation = Quaternion.identity;
    }

    /// the title screen: he floats on the swell in the lane, not moving, while the sea rolls past
    public void Idle(float dt)
    {
        t += dt;
        running = false; wipe = false; airborne = false;
        vx -= vx * 3.4f * dt; px += vx * dt; px -= px * Mathf.Min(1f, dt * 1.5f);
        carve = Mathf.Clamp(vx / 9f, -1f, 1f);
        eff = Mathf.Max(0f, eff - dt * 20f);
        WaveField.Scroll = dist * WaveField.Drift;
        float fz = dist * WaveField.Drift + pz;
        waterY = WaveField.Height(px, fz, t) + 0.05f;
        py = waterY;
        transform.position = new Vector3(px, py, pz);
        float ahead = WaveField.Height(px, fz - 2.2f, t), behind = WaveField.Height(px, fz + 2.2f, t);
        float pitch = Mathf.Atan2(ahead - behind, 4.4f) * 0.8f;
        transform.rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0, 0);
    }

    public void Jump()
    {
        if (!running || airborne) return;
        vy = 11.8f * statAir; airborne = true; poseTake = 1f;
    }

    /// hold-to-turn: he keeps rotating for as long as the button is down
    public void Hold(string trick, bool down)
    {
        if (down && running && !wipe && !airborne) Jump();
        if (trick == "spin") spinHeld = down;
        else if (trick == "roll") flipHeld = down;
        else if (trick == "flip") pitchHeld = down;
    }

    public void Step(float dt)
    {
        t += dt * (1f + 0.55f * Mathf.Sin(t * 0.045f) + 0.28f * Mathf.Sin(t * 0.017f + 1.3f)) * 0.5f + dt * 0.5f;
        if (!running) { if (wipe) wipeT += dt; return; }

        // the curve: climbs with distance and never quite arrives
        speed = Mathf.Min(100f + statTop, (SpdLo + (SpdHi - SpdLo) * dist / (dist + SpdK)) * statSpeed + statTop);
        boost = Mathf.Max(0f, boost - dt * 3.2f);
        eff = Mathf.Max(16.2f, Mathf.Min(113f + statTop, speed + Mathf.Abs(vx) * 0.3f + trim + boost));
        dist += eff * dt * FtToM;
        WaveField.Drift = 0.46f + Mathf.Min(1f, dist / 9000f) * 0.16f;
        WaveField.Scroll = dist * WaveField.Drift;
        float stage = Mathf.Min(1f, dist / 9000f);
        WaveField.Amp = Mathf.Max(0.34f, 1.12f + stage * 1.0f + Mathf.Sin(t * 0.07f) * (0.34f + stage * 0.5f) * 0.6f);

        // the carve: push, grip, slide
        float sx = Mathf.Clamp(steer, -1f, 1f);
        vx += sx * 55f * statTurn * dt;
        vx -= vx * 3.4f * statGrip * dt;
        px += vx * dt;
        if (px < -LaneLim) { px = -LaneLim; vx = 0; }
        if (px > LaneLim) { px = LaneLim; vx = 0; }
        carve = Mathf.Clamp(vx / 9f, -1f, 1f);

        // the water under him, read a little ahead and behind so he rides the crest not the spike
        float fz = dist * WaveField.Drift + pz;
        float hMean = (WaveField.Height(px, fz - 1.2f, t) + WaveField.Height(px, fz + 1.2f, t)) * 0.5f;
        float hHigh = Mathf.Max(WaveField.Height(px, fz, t), hMean);
        waterY = hHigh * 0.88f + hMean * 0.12f + 0.05f;

        if (airborne)
        {
            vy -= 20f * dt; py += vy * dt;
            if (py <= waterY)
            {
                float impact = Mathf.Min(1.6f, Mathf.Max(0f, -vy) / 15f);
                poseLand = Mathf.Min(1f, 0.55f + impact * 0.5f);
                onSplash?.Invoke(new Vector3(px, waterY, pz), 20 + (int)(impact * 40));
                bool anySpin = spinRot > 0.4f || flipRot > 0.4f || pitchRot > 0.4f;
                if (anySpin)
                {
                    float offS = Mathf.Abs(Mathf.Repeat(spinRot + Mathf.PI / 2f, Mathf.PI) - Mathf.PI / 2f);
                    float offF = Mathf.Max(Mathf.Abs(Mathf.Repeat(flipRot + Mathf.PI, Turn) - Mathf.PI),
                                           Mathf.Abs(Mathf.Repeat(pitchRot + Mathf.PI, Turn) - Mathf.PI));
                    if (offS < LandYaw && offF < LandFlip) Settle(true);
                    else { Settle(false); Wipeout("land"); return; }
                }
                airborne = false; py = waterY; vy = 0;
            }
        }
        else
        {
            py += (waterY - py) * Mathf.Min(1f, dt * 14f);
            vy = 0;
        }

        // the held rotations, only in the air
        if (airborne)
        {
            float SR = SpinRate * statAir, FR = FlipRate * statAir;
            if (spinHeld) spinRot += dt * SR;
            if (flipHeld) flipRot += dt * FR;
            if (pitchHeld) pitchRot += dt * FR;
        }
        else { spinHeld = flipHeld = pitchHeld = false; }

        // the pose beats
        poseTake = Mathf.Max(0f, poseTake - dt * 2.2f);
        float tp = 1f - poseTake;
        poseCrouch = poseTake > 0 ? Mathf.Max(0f, 1f - Mathf.Abs(tp - 0.16f) / 0.16f) : 0f;
        poseExt = poseTake > 0 ? Mathf.Max(0f, 1f - Mathf.Abs(tp - 0.52f) / 0.30f) : 0f;
        poseAir = Mathf.Lerp(poseAir, airborne ? 1f : 0f, Mathf.Min(1f, dt * (airborne ? 8f : 14f)));
        poseLand = Mathf.Max(0f, poseLand - dt * 3.0f);
        if (msgT > 0) msgT -= dt;

        // coins: a shell a few metres, the web game's pace
        coins = (int)(dist * 0.18f) + bonusCoins;

        // the transform: position, the trick rotation, and the carve lean on top
        transform.position = new Vector3(px, py, pz);
        var trick = Quaternion.Euler(pitchRot * Mathf.Rad2Deg, spinRot * Mathf.Rad2Deg, flipRot * Mathf.Rad2Deg);
        float yaw = -carve * 0.30f, roll = -carve * 0.55f;
        float pitch = 0f;
        if (!airborne)
        {
            float ahead = WaveField.Height(px, fz - 2.2f, t), behind = WaveField.Height(px, fz + 2.2f, t);
            pitch = Mathf.Atan2(ahead - behind, 4.4f) * 0.8f;
        }
        else pitch = Mathf.Clamp(-vy * 0.03f, -0.35f, 0.35f);
        transform.rotation = trick * Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
    }

    /// everything he threw in the air, scored and judged together
    public int Settle(bool landed)
    {
        int pts = 0; int names = 0; string label = "";
        float spinRevs = spinRot / Turn, flipRevs = flipRot / Turn, pitchRevs = pitchRot / Turn;
        if (spinRot > 0.4f) { pts += Mathf.FloorToInt(spinRevs * 4) * 90; names++; label += Mathf.RoundToInt(spinRevs * 360) + "° SPIN "; }
        if (flipRot > 0.4f) { pts += Mathf.FloorToInt(flipRevs * 4) * 110; names++; label += Mathf.RoundToInt(flipRevs * 360) + "° ROLL "; }
        if (pitchRot > 0.4f) { pts += Mathf.FloorToInt(pitchRevs * 4) * 130; names++; label += Mathf.RoundToInt(pitchRevs * 360) + "° FLIP "; }
        if (landed && pts > 0)
        {
            if (names > 1) pts = Mathf.RoundToInt(pts * 1.5f);
            float sq(float a) => Mathf.Abs(Mathf.Repeat(a + Mathf.PI, Turn) - Mathf.PI);
            float half(float a) => Mathf.Abs(Mathf.Repeat(a + Mathf.PI / 2f, Mathf.PI) - Mathf.PI / 2f);
            float off = Mathf.Max(half(spinRot), sq(flipRot), sq(pitchRot));
            if (off < 0.16f) { pts = Mathf.RoundToInt(pts * 2.0f); label += "PERFECT "; }
            else if (off < 0.45f) { pts = Mathf.RoundToInt(pts * 1.4f); label += "CLEAN "; }
            score += pts;
            boost = Mathf.Min(16f, boost + 5f + Mathf.Min(7f, pts / 110f));
            Say(label.Trim() + (names > 1 ? " COMBO" : "") + "! +" + pts);
        }
        spinRot = flipRot = pitchRot = 0; spinHeld = flipHeld = pitchHeld = false;
        return pts;
    }

    public void Say(string s) { lastMsg = s; msgT = 0.9f; }

    public void Wipeout(string kind)
    {
        if (wipe) return;
        wipe = true; running = false; wipeT = 0;
        Say(kind == "land" ? "BAD LANDING" : "WIPEOUT");
        onSplash?.Invoke(new Vector3(px, waterY, pz), 60);
        onWipeout?.Invoke();
    }

    /// the crash tumble, played by whoever owns the rider during the beat
    public void WipeStep(float dt)
    {
        wipeT += dt;
        float k = Mathf.Min(1f, wipeT / 0.95f);
        vy -= 32.2f * dt; py += vy * dt;
        float sea = WaveField.Height(px, dist * WaveField.Drift + pz, t) + 0.13f;
        if (py < sea) { py = sea - Mathf.Min(1.26f, k * 1.6f); vy = 0; }
        transform.position = new Vector3(px, py, pz);
        transform.rotation *= Quaternion.Euler(dt * 260f * (1f - k), dt * 90f * (1f - k), dt * 140f * (1f - k));
    }
}
