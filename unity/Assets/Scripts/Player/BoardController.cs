// The ride, ported number for number from the web game: the speed curve, the carve, the hop,
// the three held rotations, how a landing is judged and what it pays, and the crash.
// Feet and seconds throughout. +x is right, -z is ahead; the water scrolls past the rider.
using UnityEngine;

public class BoardController : MonoBehaviour
{
    // ---- the board's stats, Astro's originals ----
    public float statSpeed = 1f, statTurn = 1f, statAir = 1f, statGrip = 1f, statTop = 0f, statFloat = 1f;

    // ---- the speed curve ----
    const float SpdLo = 16.2f, SpdHi = 52f;
    const float SpdK = 210f * (SpdHi - SpdLo);
    public const float BoardHalfL = 2.45f, BoardHalfW = 0.55f;
    public float laneLim = 17f;             // the lane's half-width this frame: the island widens it, the chute squeezes it (web 44899)
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
    public float hullPitch, hullRoll;   // how the water under the hull lies: nose up on a climb, inside rail down
    public float t;                     // the wave clock
    public string lastMsg; public float msgT;
    public int coins, bonusCoins;      // puka from distance, plus what the chest and the rest hand over

    // ---- the pose beats both rigs read ----
    public float poseTake, poseCrouch, poseExt, poseAir, poseLand;

    // ---- the handstand (web 23288-23290, 44625-44627, 44744-44775) ----
    // Held on the water or in the air: he goes up on his front paws and it pays while it is
    // held, more in the air and more through a carve, tiring after a few seconds; past nine
    // his arms give out. Releasing banks what it earned.
    public bool handOn;                              // the button
    public float handT, handPts, handRisk, handHold; // the blend 0..1 (the rig reads it), what this stand has earned, (vestigial) risk, seconds held
    float handAcc;                                   // the fraction of a point not yet on the integer score

    // ---- the helicopter (web 23324-23370, 23770-23849, 44141-44143, 45706-45722, 26873-26880) ----
    // Choreographed, not held: crouch, lay out along the deck, hop, dive nose first, two seconds
    // under, fired back out at forty-five degrees and popped. In the air the board barrel-rolls
    // for as long as the button is held, once it has levelled off.
    const float HeliRate = Mathf.PI * 2f * 2.15f, HeliPop = 18.6f;
    static readonly float[] HeliPhase = { 0.42f, 0.50f, 0.28f, 0.52f, 0.96f, 0.52f };   // crouch, lay, hop, dive, deep, surface
    public const int PhCrouch = 0, PhLay = 1, PhHop = 2, PhDive = 3, PhDeep = 4, PhSurf = 5;
    public const int HeliUnder = PhDive;             // from this beat on he is in the water, not on it
    const float HeliOut = -0.785f, HeliSettle = 0.34f;
    public int heliPh = -1;                          // the beat running, -1 for none
    public float heliT, heliPose, heliLay, heliIncline, heliSet, heliCrouch;
    public bool heliUsed, heliAir, heliHeld;

    // ---- the dive (World/Dive.cs owns him while it runs, the way the helicopter beats do) ----
    public bool diveOn;                              // the launch, the swim or the way out is running: py is not ours
    public bool diveShield;                          // deeper than anything's draft, over the lane in the arc, or just landed: nothing can hit him
    public float divePitch;                          // the board tips the way he swims (web boardPitch under water)
    public float divePose, divePike, diveLoad, diveLift;   // for the rig: 0..1 face-down swim, the fold over the arc, the crouch, feet of air
    /// under the water, nothing floating on it can reach him
    public bool Submerged => heliPh >= HeliUnder || diveShield;

    // ---- the island (World/Island.cs owns him from the beaching to the walk, as the dive does) ----
    public bool isleOn;                              // beached, running off the board or walking: py is not ours
    public bool ashore;                              // on his own legs (web forkPh 3 and 4): no speed, no carve, the metres come from his feet
    public float isleFace;                           // radians of heading ashore, 0 facing away from the lens (web forkFace)
    public float isleWalk, isleRate = 1f;            // for the rig: the walk clip's weight 0..1 and its rate (web walkW / walkRate)
    // ---- the chute (World/Chute.cs): the slope pushes past the ordinary ceiling (web 45175-45178) ----
    public float chutePush;                          // FALL_PUSH * fallW, feet a second on top of whatever the board was doing
    // ---- the paraglide (World/Boat.cs owns him from the tow to the splashdown, as the dive does) ----
    public bool glideOn;                             // on the line or under the wing (web glPh >= 0): py is not ours, and he counts as in the air
    public float glideFace;                          // for the rig: 0 side-on to the board, 1 square to the nose with his arms out to the toggles (web glFace)

    public System.Action<Vector3, int> onSplash;   // where, how many drops
    public System.Action onWipeout;

    /// the web's mapping of the four shop stats onto the ride
    public float statGlide = 1f, statTrick = 1f, statLuck = 1f;
    /// the board and the rider added up, the web's applyBoardStats with the perk on top
    public void ApplyStats(RideStats s)
    {
        statSpeed = s.speed; statTurn = s.turn; statAir = s.air; statGrip = s.grip; statTop = s.top; statFloat = s.flo;
        statGlide = s.glide; statTrick = s.trick; statLuck = s.luck;
    }

    public void Begin()
    {
        px = 0; py = 0; pz = 0; vx = 0; vy = 0; dist = 0; speed = SpdLo; eff = SpdLo; boost = 0; trim = 0;
        airborne = false; running = true; wipe = false; wipeT = 0; score = 0; coins = 0; bonusCoins = 0;
        spinRot = flipRot = pitchRot = 0; spinHeld = flipHeld = pitchHeld = false;
        poseTake = poseCrouch = poseExt = poseAir = poseLand = 0;
        handOn = false; handT = handPts = handRisk = handHold = handAcc = 0;
        heliPh = -1; heliT = heliPose = heliLay = heliIncline = heliSet = heliCrouch = 0; heliUsed = heliAir = heliHeld = false; BoardTrickClear();
        diveOn = false; diveShield = false; divePitch = divePose = divePike = diveLoad = diveLift = 0;
        isleOn = false; ashore = false; isleFace = 0; isleWalk = 0; isleRate = 1f; chutePush = 0; laneLim = 17f;
        transform.rotation = Quaternion.identity;
    }

    /// the web's respawn after a revive: back on the board where he went in, everything kept
    public void Revive()
    {
        wipe = false; running = true; wipeT = 0; vx = vy = 0; airborne = false; px = 0; pz = 0; py = waterY;
        spinRot = flipRot = pitchRot = 0; spinHeld = flipHeld = pitchHeld = false;
        handOn = false; handT = handPts = handRisk = handHold = handAcc = 0;
        heliPh = -1; heliT = heliPose = heliLay = heliIncline = heliSet = heliCrouch = 0; heliUsed = heliAir = heliHeld = false; BoardTrickClear();
        diveOn = false; diveShield = false; divePitch = divePose = divePike = diveLoad = diveLift = 0;
        isleOn = false; ashore = false; isleFace = 0; isleWalk = 0; isleRate = 1f; chutePush = 0; laneLim = 17f;
        poseTake = poseCrouch = poseExt = poseAir = poseLand = 0; transform.rotation = Quaternion.identity;
        Say("BACK UP!");
    }

    /// the run ends where he stands, no tumble (web forkIslandDone -> gameOver): the ride's own
    /// end-of-run branch picks it up from here, the way a wipeout would, with the crash already over
    public void End(string msg)
    {
        if (wipe) return;
        wipe = true; running = false; wipeT = 0.95f;
        handOn = false; handPts = 0; handHold = 0;
        heliPh = -1; heliAir = false; heliHeld = false; heliSet = 0; heliLay = 0; heliUsed = false;
        kf = null;                                   // a board trick in hand when he goes over just stops
        Say(msg);
        onWipeout?.Invoke();
    }

    // ---- BOARD TRICKS (web 26961-27030) ----
    // One press. He pops if he is not already up, and the board does the whole figure under him
    // over about two thirds of a second: fast off the foot, slowing as it comes round. While it
    // turns it drops a hand's width and he pulls his feet up (kfTuck, read by the rig), so the
    // deck is not spinning through his paws. Finishing pays; the water arriving first does not,
    // and the board is simply put back under him as he lands.
    TrickCatalog.Trick kf; float kfT, kfDur;
    public float kfRoll, kfYaw, kfTuck;          // radians, about the board's long axis and its up; 0..1

    void StartBoardTrick(TrickCatalog.Trick T)
    {
        if (T == null || !T.Board || !TrickCatalog.Have(T.id)) return;
        if (!running || wipe || kf != null || heliAir) return;
        if (!airborne) Jump();
        if (!airborne) return;
        kf = T; kfT = 0f;
        // a 360 shove or a tre has further to go than a kickflip and gets a little longer for it
        kfDur = 0.62f + (T.axis == "both" ? 0.14f : 0f) + (T.axis == "yaw" && T.turns >= 1f ? 0.10f : 0f);
        Sfx.Buzz(8);
    }

    void BoardTrickPay()
    {
        int p = Mathf.RoundToInt(kf.pts * statTrick);
        score += p;
        boost = Mathf.Min(16f, boost + 3f + Mathf.Min(5f, p / 120f));
        Say(kf.name.ToUpper() + "! +" + p);
        Sfx.Buzz(p >= 400 ? 30 : 16);
    }

    // the way back to square: a half-turn shove is carried on round rather than unwound
    static float KfSettle(float a, float dt)
    {
        float tgt = a > Mathf.PI - 0.02f ? Turn : (a < -(Mathf.PI - 0.02f) ? -Turn : 0f);
        a = Ease(a, tgt, dt, 11f);
        return Mathf.Abs(a - tgt) < 0.012f ? 0f : a;
    }

    void BoardTrickStep(float dt)
    {
        if (kf != null)
        {
            kfT += dt;
            float k = Mathf.Min(1f, kfT / kfDur);
            float e = 1f - Mathf.Pow(1f - k, 3f);            // out: snaps away, floats home
            float a = e * Turn * kf.dir;
            kfRoll = (kf.axis == "long" || kf.axis == "both") ? a * kf.turns : 0f;
            kfYaw = kf.axis == "yaw" ? a * kf.turns : (kf.axis == "both" ? a * kf.yaw : 0f);
            kfTuck = Mathf.Sin(k * Mathf.PI);
            if (k >= 1f) { BoardTrickPay(); kf = null; kfRoll %= Turn; kfYaw %= Turn; }   // a whole turn is square already
            else if (!airborne || wipe) { kf = null; Say("SKETCHY"); }                     // the water got there first
        }
        else
        {
            kfTuck = Ease(kfTuck, 0f, dt, 12f); if (kfTuck < 0.01f) kfTuck = 0f;
            // the board stays where it ended up while he is in the air, and comes square under the splash
            if (!airborne || wipe) { kfRoll = KfSettle(kfRoll, dt); kfYaw = KfSettle(kfYaw, dt); }
        }
    }

    void BoardTrickClear() { kf = null; kfRoll = kfYaw = kfTuck = 0f; }

    static float Ease(float cur, float tgt, float dt, float k) { return cur + (tgt - cur) * Mathf.Min(1f, dt * k); }

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
        if (!running || airborne || heliPh >= 0 || diveOn || isleOn) return;   // the helicopter, the dive and the island own him through their beats
        vy = 11.8f * statAir; airborne = true; poseTake = 1f;
    }

    /// hold-to-turn: he keeps rotating for as long as the button is down. The handstand is
    /// held too, but on the water; the helicopter starts on the press and the hold is the roll.
    public void Hold(string trick, bool down)
    {
        // a board trick is one press; the release means nothing to it (web 23815)
        var bt = TrickCatalog.Get(trick);
        if (bt != null && bt.Board) { if (down) StartBoardTrick(bt); return; }
        if (trick == "hand") { handOn = down; return; }
        if (trick == "heli") { if (down) StartHeli(); else heliHeld = false; return; }
        if (down && running && !wipe && !airborne) Jump();
        if (trick == "spin") spinHeld = down;
        else if (trick == "roll") flipHeld = down;
        else if (trick == "flip") pitchHeld = down;
    }

    /// web 23770-23778: it can only be STARTED off the water; pressed again mid-trick it just takes hold
    void StartHeli()
    {
        if (!running || wipe) return;
        if (heliAir || heliPh >= 0) { heliHeld = true; return; }
        if (airborne || diveOn || isleOn) return;
        heliHeld = true;
        heliPh = 0; heliT = 0; heliUsed = true;
        Say("HELICOPTER!");
    }

    /// web 23782-23849: the beats. It owns py outright while it runs, which is how he goes under.
    void HeliStep(float dt)
    {
        heliT += dt;
        float dur = HeliPhase[heliPh], k = Mathf.Min(1f, heliT / dur);
        float smooth = k * k * (3f - 2f * k);
        if (heliPh == PhCrouch)                 // he drops onto the deck and takes hold
        {
            heliCrouch = Mathf.Max(heliCrouch, 0.30f + smooth * 0.62f);
            heliLay = smooth * 0.34f;
            py = waterY;
        }
        else if (heliPh == PhLay)               // flat along the deck, hands to the nose
        {
            heliCrouch = Mathf.Max(heliCrouch, 0.92f);
            heliLay = 0.34f + smooth * 0.66f;
            py = waterY;
        }
        else if (heliPh == PhHop)               // a small hop, and the nose comes down
        {
            heliLay = 1f;
            py = waterY + Mathf.Sin(k * Mathf.PI) * 0.66f;
            heliIncline = smooth * 0.24f;
        }
        else if (heliPh == PhDive)              // nose first, only slightly angled
        {
            heliLay = 1f;
            py = waterY - smooth * 2.45f;
            heliIncline = 0.24f + smooth * 0.21f;
            if (heliT - dt <= 0f) onSplash?.Invoke(new Vector3(px, waterY, pz), 26);
            if (Random.value < dt * 30f) onSplash?.Invoke(new Vector3(px + (Random.value - 0.5f) * 0.9f, waterY, pz + 0.2f), 1);
        }
        else if (heliPh == PhDeep)              // ...and stays down there, climbing back toward the surface
        {
            heliLay = 1f;
            float climb = Mathf.Sin(Mathf.PI * (0.5f + k * 0.5f));   // 1 at the bottom, 0 at the top
            py = waterY - 1.25f - climb * 1.20f;
            heliIncline = 0.45f - smooth * 0.35f;                     // levelling out under the water
            if (Random.value < dt * 46f) onSplash?.Invoke(new Vector3(px + (Random.value - 0.5f) * 1.5f, waterY, pz + 0.2f + Random.value * 0.9f), 1);
        }
        else if (heliPh == PhSurf)              // fired back out, leaning back at 45
        {
            heliLay = 1f;
            float shot = k * k;                                       // slow to leave, then all at once
            py = waterY - 1.25f + shot * 1.75f;
            heliIncline = 0.10f + (HeliOut - 0.10f) * shot;
            if (Random.value < dt * (18f + k * 44f)) onSplash?.Invoke(new Vector3(px + (Random.value - 0.5f) * 1.2f, waterY, pz + 0.3f), 2);
        }
        vy = 0f;
        if (k >= 1f)
        {
            heliPh++; heliT = 0f;
            if (heliPh >= HeliPhase.Length)     // hand over to the launch
            {
                heliPh = -1; heliAir = true;
                vy = HeliPop * statAir; airborne = true;
                heliIncline = HeliOut; heliSet = HeliSettle;   // out leaning back; the roll waits for him to level
                onSplash?.Invoke(new Vector3(px, waterY, pz), 34);
            }
        }
    }

    /// web 44744-44775: what a handstand pays and what it costs
    void HandstandScore(float dt, bool want)
    {
        if (handT > 0.55f)
        {
            handHold += dt;
            float tire = Mathf.Max(0f, 1f - Mathf.Max(0f, handHold - 3.4f) / 2.6f);
            float rate = (airborne ? 46f : 26f) * (1f + Mathf.Min(1f, Mathf.Abs(carve)) * 0.4f) * tire;
            handPts += rate * dt;
            handAcc += rate * dt;
            int whole = Mathf.FloorToInt(handAcc); score += whole; handAcc -= whole;
            handRisk = 0f;   // steering on his hands no longer throws him; only time does
            if (handHold > 9.0f) { Wipeout("foam"); Say("ARMS GAVE OUT!"); return; }
        }
        else handHold = Mathf.Max(0f, handHold - dt * 2.2f);
        if (!want && handPts > 0f)
        {
            if (handPts >= 30f)
            {
                int p = Mathf.RoundToInt(handPts);
                score += Mathf.RoundToInt(p * 0.35f); boost = Mathf.Min(16f, boost + 3.5f);
                Say("HANDSTAND! +" + Mathf.RoundToInt(p * 1.35f));
            }
            handPts = 0f;
        }
    }

    public void Step(float dt)
    {
        t += dt * (1f + 0.55f * Mathf.Sin(t * 0.045f) + 0.28f * Mathf.Sin(t * 0.017f + 1.3f)) * 0.5f + dt * 0.5f;
        BoardTrickStep(dt);
        if (!running) { if (wipe) wipeT += dt; return; }

        // the curve: climbs with distance and never quite arrives
        // Ashore there is no curve (web 45176): he is on his legs, the speed is nil and the metres
        // come from his feet (Island adds them). The chute pushes past the ordinary ceiling (web 45177).
        speed = ashore ? 0f : Mathf.Min(100f + statTop, (SpdLo + (SpdHi - SpdLo) * dist / (dist + SpdK)) * statSpeed + statTop);
        boost = Mathf.Max(0f, boost - dt * 3.0f / statGlide);      // glide: how long the speed you earned stays with you
        eff = ashore ? 0f : Mathf.Max(16.2f, Mathf.Min(113f + statTop + chutePush, speed + Mathf.Abs(vx) * 0.3f + trim + boost + chutePush));
        // the web's distance is the world's own units (web 45184: dist += eff*dt), and every gate in
        // the game is keyed to it (ramps at 150, jellyfish 340, sharks 560, octopuses 780, the set wave
        // at 5000, the day). Converted to true metres it ran at 0.3048 of the web's and all of them
        // came three times late, the spacing tightened three times slower, and the swell rolled by slow.
        if (!ashore) dist += eff * dt;
        WaveField.Drift = 0.46f + Mathf.Min(1f, dist / 9000f) * 0.16f;
        WaveField.Scroll = dist * WaveField.Drift;
        float stage = Mathf.Min(1f, dist / 9000f);
        WaveField.Amp = Mathf.Max(0.34f, 1.12f + stage * 1.0f + Mathf.Sin(t * 0.07f) * (0.34f + stage * 0.5f) * 0.6f);

        // the carve: push, grip, slide
        float sx = ashore ? 0f : Mathf.Clamp(steer, -1f, 1f);
        vx += sx * 55f * statTurn * dt;
        vx -= vx * 3.4f * statGrip * dt;
        if (ashore) vx = 0f;                                       // the stick walks him; the steering is let go of (web 45400, 45423)
        px += vx * dt;
        if (px < -laneLim) { px = -laneLim; vx = 0; }
        if (px > laneLim) { px = laneLim; vx = 0; }
        carve = Mathf.Clamp(vx / 9f, -1f, 1f);

        // the water under him, read a little ahead and behind so he rides the crest not the spike
        // Where the hull wants to float (web 45097-45152): a rigid hull does not follow one
        // point on the water. Sampled under the nose, the tail and both rails, it rests on the
        // highest water beneath it so a crest cannot slice through the ends, answers to the mean
        // under the whole length as well, and a hard carve drives the inside rail in.
        float fz = dist * WaveField.Drift + pz;
        float hC = WaveField.Height(px, fz, t);
        float hN = WaveField.Height(px, fz - 2.55f, t), hT = WaveField.Height(px, fz + 2.55f, t);
        float hL = WaveField.Height(px - 0.58f, fz, t), hR = WaveField.Height(px + 0.58f, fz, t);
        hullPitch = Mathf.Atan2(hN - hT, 5.10f); hullRoll = Mathf.Atan2(hR - hL, 1.16f);
        trim = (hT - hN) * 3.8f;                                   // dropping down a face drives, climbing one costs
        float hHigh = Mathf.Max(Mathf.Max(hC, hN), Mathf.Max(hT, (hL + hR) * 0.5f));
        float hMean = hC * 0.34f + hN * 0.20f + hT * 0.20f + hL * 0.13f + hR * 0.13f;
        float dig = Mathf.Max(0f, Mathf.Abs(carve) - 0.20f) / 0.80f;
        float railDig = Mathf.Min(0.17f, dig * dig * 0.17f);
        waterY = hHigh * 0.88f + hMean * 0.12f + 0.05f - railDig + (statFloat - 1f) * 0.30f;

        // the helicopter's crouch beat eases off on its own (web 44096), the beats re-assert it
        heliCrouch = Ease(heliCrouch, 0f, dt, 9f);

        if (heliPh >= 0) HeliStep(dt);          // the helicopter drives him itself (web 45394)
        else if (diveOn || isleOn) vy = 0;      // the dive owns py outright, launch to landing (web 45330-45347); the island likewise, beaching to the walk (web 45359-45449)
        else if (glideOn) vy = 0;               // and the paraglide, tow to splashdown: forty feet up passes every test below, which would start landing him (web 45348-45352)
        else if (airborne)
        {
            vy -= 20f * dt; py += vy * dt;
            if (py <= waterY)
            {
                float impact = Mathf.Min(1.6f, Mathf.Max(0f, -vy) / 15f);
                poseLand = Mathf.Min(1f, 0.55f + impact * 0.5f);
                onSplash?.Invoke(new Vector3(px, waterY, pz), 20 + (int)(impact * 40));
                Sfx.Land(impact);
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
                heliUsed = false; heliAir = false; heliHeld = false; heliSet = 0; heliLay = 0;   // down is down, rolled or not
            }
        }
        else
        {
            py += (waterY - py) * Mathf.Min(1f, dt * 14f);
            vy = 0;
        }

        // the held rotations, only in the air (and not under the water: there is no board to roll, web 45621)
        if (airborne && !diveOn && !isleOn)
        {
            float SR = SpinRate * statAir, FR = FlipRate * statAir;
            if (heliAir)
            {
                // web 45706-45722: flipRot is the roll axis, the board turning over the way it is
                // going. It turns for as long as you hold, but not until he is straight: he
                // leaves the water leaning back and the settle is a beat, not a lockout.
                if (heliSet > 0) heliSet = Mathf.Max(0f, heliSet - dt);
                if (heliHeld && heliSet <= 0) flipRot += dt * HeliRate * statAir;
                heliIncline = Ease(heliIncline, 0f, dt, heliSet > 0 ? 9f : 6f);   // dead level through the rolls
            }
            if (spinHeld) spinRot += dt * SR;
            if (flipHeld) flipRot += dt * FR;
            if (pitchHeld) pitchRot += dt * FR;
        }
        else { spinHeld = flipHeld = pitchHeld = false; }
        Sfx.TrickTurn("spin", spinRot, spinHeld); Sfx.TrickTurn("roll", flipRot, flipHeld); Sfx.TrickTurn("flip", pitchRot, pitchHeld);
        if (heliPh < 0 && !heliAir) heliIncline = Ease(heliIncline, 0f, dt, 4f);   // web 45929

        // the lie-down the rig reads (web 44141-44143): scripted off the beats, eased only on the way out
        bool heliOnNow = heliAir || heliPh >= 1;
        if (heliPh >= 0) heliPose = heliLay;
        else heliPose = Ease(heliPose, heliOnNow ? 1f : 0f, dt, heliOnNow ? 12f : 6f);

        // the handstand (web 44625-44627): the button eases him up at 7 and down at 9, and handT
        // is what the rig turns him over by and what the scoring reads
        bool handWant = handOn && !wipe && !diveOn && !isleOn;    // nothing to stand on his hands on, ashore (web 45618)
        handT += ((handWant ? 1f : 0f) - handT) * Mathf.Min(1f, dt * (handWant ? 7f : 9f));
        if (handT < 0.002f && !handWant) handT = 0f;
        HandstandScore(dt, handWant);
        if (wipe) return;                      // his arms gave out

        // the pose beats
        poseTake = Mathf.Max(0f, poseTake - dt * 2.2f);
        float tp = 1f - poseTake;
        poseCrouch = poseTake > 0 ? Mathf.Max(0f, 1f - Mathf.Abs(tp - 0.16f) / 0.16f) : 0f;
        poseExt = poseTake > 0 ? Mathf.Max(0f, 1f - Mathf.Abs(tp - 0.52f) / 0.30f) : 0f;
        poseAir = Mathf.Lerp(poseAir, airborne ? 1f : 0f, Mathf.Min(1f, dt * (airborne ? 8f : 14f)));
        poseLand = Mathf.Max(0f, poseLand - dt * 3.0f);
        if (msgT > 0) msgT -= dt;

        // coins: a shell a few metres, the web game's pace
        // the web's pay (coinsFor, web 10096-10098): a shell every five metres and one for every
        // eighteen trick points, plus what the chest, the pearls and the island hand over
        coins = Mathf.FloorToInt(dist / 5f) + Mathf.FloorToInt(score / 18f) + bonusCoins;

        // the transform: position, the trick rotation, and the carve lean on top
        transform.position = new Vector3(px, py, pz);
        var trick = Quaternion.Euler(pitchRot * Mathf.Rad2Deg, spinRot * Mathf.Rad2Deg, flipRot * Mathf.Rad2Deg);
        // the board lies along the water it rests on (web 45838, 45908), and leans into the carve
        float yaw = -carve * 0.30f, roll = -carve * 0.55f + (airborne ? 0f : hullRoll * 1.20f);
        float pitch = airborne ? Mathf.Clamp(-vy * 0.03f, -0.35f, 0.35f) : Mathf.Clamp(hullPitch * 1.02f, -0.62f, 0.68f);
        pitch += heliIncline;                  // the helicopter's nose-down and the lean-back out (web 45949)
        if (diveOn)
        {
            // the board goes with him in this port: it tips the way he swims and folds over the
            // top of the launch with him (the web pikes the rider alone, 0.72 past level), and no
            // carve rolls him about the lane under the water (web 46015)
            pitch = divePitch - 0.72f * divePike; roll = -carve * 0.55f;
        }
        transform.rotation = trick * Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
        // Island.Step re-places him after this when it has him (the beaching pitch, then his own heading ashore)
    }

    /// everything he threw in the air, scored and judged together
    public int Settle(bool landed)
    {
        int pts = 0; int names = 0; string label = "";
        float spinRevs = spinRot / Turn, flipRevs = flipRot / Turn, pitchRevs = pitchRot / Turn;
        if (spinRot > 0.4f) { pts += Mathf.FloorToInt(spinRevs * 4) * 90; names++; label += Mathf.RoundToInt(spinRevs * 360) + "° SPIN "; }
        if (flipRot > 0.4f) { pts += Mathf.FloorToInt(flipRevs * 4) * 110; names++; label += Mathf.RoundToInt(flipRevs * 360) + "° ROLL "; }
        if (pitchRot > 0.4f) { pts += Mathf.FloorToInt(pitchRevs * 4) * 130; names++; label += Mathf.RoundToInt(pitchRevs * 360) + "° FLIP "; }
        // web 26873-26880: the helicopter scores as the roll it is, and takes the name when it was rolled that way
        if (heliUsed && flipRot > 0.4f) { pts = Mathf.RoundToInt(pts * 1.35f); label = label.Replace("° ROLL", "° HELICOPTER"); }
        heliUsed = false; heliAir = false; heliPh = -1; heliHeld = false; heliSet = 0; heliLay = 0;
        if (landed && pts > 0)
        {
            if (names > 1) pts = Mathf.RoundToInt(pts * 1.5f);
            float sq(float a) => Mathf.Abs(Mathf.Repeat(a + Mathf.PI, Turn) - Mathf.PI);
            float half(float a) => Mathf.Abs(Mathf.Repeat(a + Mathf.PI / 2f, Mathf.PI) - Mathf.PI / 2f);
            float off = Mathf.Max(half(spinRot), sq(flipRot), sq(pitchRot));
            if (off < 0.16f) { pts = Mathf.RoundToInt(pts * 2.0f); label += "PERFECT "; }
            else if (off < 0.45f) { pts = Mathf.RoundToInt(pts * 1.4f); label += "CLEAN "; }
            pts = Mathf.RoundToInt(pts * statTrick);      // the rider's trick perk
            score += pts;
            boost = Mathf.Min(16f, boost + 5f + Mathf.Min(7f, pts / 110f));
            Say(label.Trim() + (names > 1 ? " COMBO" : "") + "! +" + pts);
            Sfx.Buzz(pts >= 400 ? 35 : 20);
        }
        spinRot = flipRot = pitchRot = 0; spinHeld = flipHeld = pitchHeld = false;
        return pts;
    }

    public void Say(string s) { lastMsg = s; msgT = 0.9f; }

    public void Wipeout(string kind)
    {
        if (wipe) return;
        wipe = true; running = false; wipeT = 0;
        isleOn = false; ashore = false;                                          // not his to hold any more
        handOn = false; handPts = 0; handHold = 0;                              // handT itself eases off in the tumble
        heliPh = -1; heliAir = false; heliHeld = false; heliSet = 0; heliLay = 0; heliUsed = false;
        Say(kind == "land" ? "BAD LANDING" : "WIPEOUT");
        Sfx.Hit(kind);
        onSplash?.Invoke(new Vector3(px, waterY, pz), 60);
        onWipeout?.Invoke();
    }

    /// the crash tumble, played by whoever owns the rider during the beat
    public void WipeStep(float dt)
    {
        wipeT += dt;
        float k = Mathf.Min(1f, wipeT / 0.95f);
        // he comes down off his hands and off the nose as he goes over, the rates the ride uses
        handT += (0f - handT) * Mathf.Min(1f, dt * 9f);
        heliPose = Ease(heliPose, 0f, dt, 6f); heliCrouch = Ease(heliCrouch, 0f, dt, 9f);
        if (isleOn)
        {
            // the run ended on the sand (End): he stands where he stopped while the card comes up
            vy = 0; transform.position = new Vector3(px, py, pz); return;
        }
        if (diveOn)
        {
            // taken under the water: he tumbles where he is and settles a little, and is not
            // surfaced first (web 41783: the fog and the sand say where this happened)
            vy = 0; py -= dt * 0.8f;
        }
        else
        {
            vy -= 32.2f * dt; py += vy * dt;
            float sea = WaveField.Height(px, dist * WaveField.Drift + pz, t) + 0.13f;
            if (py < sea) { py = sea - Mathf.Min(1.26f, k * 1.6f); vy = 0; }
        }
        transform.position = new Vector3(px, py, pz);
        transform.rotation *= Quaternion.Euler(dt * 260f * (1f - k), dt * 90f * (1f - k), dt * 140f * (1f - k));
    }
}
