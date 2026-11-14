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
    // ---- tapped tricks queue whole turns (web 23303-23330, 45795-45800) ----
    // A tap commits a full revolution rather than turning for as long as a finger is down, which
    // is what makes tapping land square: you only eat it if you asked for more rotation than you
    // had air for. The counters stay positive so the landing check is unchanged; the direction is
    // a sign applied when the pose is set, and a held button turns the way the last tap did.
    public float spinQ, flipQ, pitchQ;
    public float spinSign = 1f, flipSign = 1f, pitchSign = 1f;
    // ---- the grab and the hand drag (web 23370-23371, 26889-26919) ----
    // Held still in the air he takes hold of a rail (INDY on the right, MELON on the left) and it
    // pays 95 a second; on the water the same hold puts a paw in the sea beside the board for 70.
    // Letting go banks it; touching down still holding the rail is a wipeout.
    public bool grabOn, dragOn;
    public float grabPts, dragPts;
    public int grabSide = 1, dragSide = 1;
    public float releaseKick;                       // the pop back upright out of either (web 44151-44152), the rig reads it
    float dragAcc;                                  // spray owed off the dragging paw, in drops
    // ---- riding switch (web 23347-23369) ----
    // A half turn is a legal landing: the board carries on tail first and he rides it backwards
    // until another half turn puts him round again. Board and rider turn together (the rigs and
    // the board's own transform read switchRide), and he waves at the lens for three seconds.
    public bool switchRide;
    public float waveT;                             // seconds of the wave left (web waveT)
    public const float WaveDur = 3.0f;
    public bool pipeAir;                            // off the barrel's wall (SetWave writes it): the height gate means nothing in there
    /// web trickReady (45608): clear of the water, or on the rope. The keys' spin, roll and grab
    /// need it, and JUMP dims while it holds.
    public bool TrickReady => (airborne && (pipeAir || py > waterY + 0.8f)) || hanging;
    public float steer;                 // -1..1 from the input layer
    public float waterY, carve;
    float prevWaterY = float.NaN;       // last frame's water height, for the float spring's damping
    public float rampPitch;             // the deck's climb under him while he rides a ramp (the spawner sets it)
    public System.Action<float> rideRamps;   // the spawner's ramp ride, run straight after he settles on the water
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
    // ---- off the top of a jet ski's wake (World/JetSki.cs, web 23333-23339, 45313-45318, 45456-45457) ----
    public bool wakeAir;                             // launched off a wake: the way down is long, under 6 rather than 20
    public float wakeGrace;                          // seconds the landing test is held off while the ridge slides out from under him
    // ---- on the tow plane's rope (World/Spawner.cs flies the plane; web 27036-27049, 45353-45358) ----
    // Hooked, he is drawn up to 5.4 ft over the water and pulled in under the plane, paid 60 a
    // second, and can throw tricks, which bank as soon as he squares up. Like the paraglide it
    // counts as in the air, so nothing on the water touches him and nothing lands him.
    public bool hanging;                             // on the line (web hanging): py and px are the rope's
    public bool ropeOffer;                           // the plane is in reach: the ROPE! button is up (the spawner writes it)
    public float towX;                               // where the plane is across the lane
    public System.Action onRope;                     // the ROPE! / LET GO button (the spawner answers it)
    float hangAcc;                                   // the fraction of a point not yet on the integer score
    float supT = -1f;                                // the Superman backflip off the line, seconds in; -1 for none
    const float HangY = 5.4f, SupDur = 0.8f; const int SupPts = 500;

    public System.Action<Vector3, int> onSplash;   // where, how many drops
    public System.Action onWipeout;

    // ---- the crash (web 27623-27641, 27868-28008, 28220-28474) ----
    // It has to LAND: the clock stops for a beat and comes back (wipeSlow, which Bootstrap applies
    // to the whole frame's dt, decayed on the real clock so it cannot stall itself), he is thrown
    // off what he was doing with the character of whatever he hit on top, tumbling mostly one way,
    // and the board is kicked out from under him and cartwheels off on its own. On "snap" it goes
    // in two halves (Hull.Snap). The card is timed on the real clock (wipeReal), not the slowed one.
    public const float WipeSlowSecs = 0.32f, WipeShow = 0.95f;   // web WIPE_SLOW, WIPE_SHOW
    const float WipeAirDrag = 1.15f, WipeSpinK = 2.4f;           // web W_AIRDRAG, W_SPINK
    public float wipeSlow;                           // 1 at the hit, easing to 0 over WipeSlowSecs of real time
    public float wipeReal;                           // real seconds since the hit (Bootstrap adds the raw frame time)
    public bool boardFree;                           // the board was kicked away and flies on its own; Bootstrap places it from boardAt/boardRot
    public Vector3 boardAt; public Quaternion boardRot = Quaternion.identity;
    public Transform seat;                           // the rig the board sits in, so the kick starts from exactly where it was
    public SetWave ring;                             // the barrel: a crash on its wall is thrown off the ring's own motion
    float carrySpin, carryFlip;                      // a rotation still under way when he went in carries on into the tumble
    float wvx, wvy, wvz, wsx, wsy, wsz, wrx, wry, wrz, wDep = -1f; bool wSplashed;
    float bvx, bvy, bvz, bsx, bsy, bsz, brx, bry, brz, snapT; bool boardLanded;
    float wobT;                                      // the cracked board's sway (web 45966-45976)

    /// how each kind of crash throws him (the web's WIPE table): up, along the lane (minus is
    /// ahead), the spin figure for each axis, and the kick the web gives its (unused) shake
    struct Throw { public float vy, vz, s0, s1, s2; public Throw(float a, float b, float c, float d, float e) { vy = a; vz = b; s0 = c; s1 = d; s2 = e; } }
    static Throw ThrowFor(string kind)
    {
        switch (kind)
        {
            case "buoy": return new Throw(9.0f, -3.5f, 9, 3, 2);
            case "jelly": return new Throw(6.5f, -1.5f, 12, 2, 7);
            case "log": return new Throw(5.0f, -2.0f, 14, 1, 3);
            case "bigfin": return new Throw(11.0f, -5.0f, 7, 8, 4);
            case "rock": return new Throw(5.5f, 0.5f, 8, 3, 5);
            case "octopus": return new Throw(7.5f, -2.5f, 10, 5, 6);
            case "pearl": return new Throw(3.0f, -7.5f, 16, 0, 1);
            case "land": return new Throw(6.0f, 1.5f, 5, 6, 9);
            case "under": return new Throw(4.5f, 0.5f, 11, 4, 5);
            case "debris": return new Throw(7.5f, -3.0f, 10, 4, 3);
            case "snap": return new Throw(4.0f, -1.0f, 13, 3, 4);
        }
        return new Throw(2.5f, -1.0f, 6, 2, 8);                     // foam, and anything without its own entry (web: W.foam)
    }

    /// three.js's Euler 'XYZ' (its default order, the one every rotation.set in the web uses):
    /// the matrices are the same numbers here, so the same angles give the same turn
    public static Quaternion Xyz(float x, float y, float z) =>
        Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);
    static void ToXyz(Quaternion q, out float x, out float y, out float z)
    {
        var m = Matrix4x4.Rotate(q);
        y = Mathf.Asin(Mathf.Clamp(m.m02, -1f, 1f));
        if (Mathf.Abs(m.m02) < 0.9999999f) { x = Mathf.Atan2(-m.m12, m.m22); z = Mathf.Atan2(-m.m01, m.m00); }
        else { x = Mathf.Atan2(m.m21, m.m11); z = 0f; }
    }

    /// he goes in exactly as he was, still turning the same way (web 45480, 45499, 32019)
    public void CarryTurn() { carrySpin = spinHeld ? SpinRate : 0f; carryFlip = flipHeld ? FlipRate : 0f; }

    void ClearCrash()
    {
        wipeSlow = 0f; wipeReal = 0f; boardFree = false; carrySpin = carryFlip = 0f; wobT = 0f;
        wSplashed = false; wDep = -1f; boardLanded = false; snapT = 0f;
    }

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
        msgT = 0f; lastMsg = ""; prevWaterY = float.NaN;                   // the last run's crash line does not open this one, nor its water the spring
        airborne = false; running = true; wipe = false; wipeT = 0; score = 0; coins = 0; bonusCoins = 0;
        spinRot = flipRot = pitchRot = 0; spinHeld = flipHeld = pitchHeld = false;
        poseTake = poseCrouch = poseExt = poseAir = poseLand = 0;
        handOn = false; handT = handPts = handRisk = handHold = handAcc = 0;
        heliPh = -1; heliT = heliPose = heliLay = heliIncline = heliSet = heliCrouch = 0; heliUsed = heliAir = heliHeld = false; BoardTrickClear();
        diveOn = false; diveShield = false; divePitch = divePose = divePike = diveLoad = diveLift = 0;
        isleOn = false; ashore = false; isleFace = 0; isleWalk = 0; isleRate = 1f; chutePush = 0; laneLim = 17f;
        hanging = false; ropeOffer = false; hangAcc = 0; supT = -1f;
        TrickInputClear(); ClearCrash();
        transform.rotation = Quaternion.identity;
    }

    /// what a run start and a revive both put back (web 27099, 27175, 27191): nothing owed, both
    /// directions forward, no rail in hand, regular stance
    void TrickInputClear()
    {
        spinQ = flipQ = pitchQ = 0f; spinSign = flipSign = pitchSign = 1f;
        grabOn = dragOn = false; grabPts = dragPts = 0f; releaseKick = 0f; dragAcc = 0f;
        switchRide = false; waveT = 0f; pipeAir = false;
    }

    /// the web's respawn after a revive: back on the board where he went in, everything kept
    public void Revive()
    {
        wipe = false; running = true; wipeT = 0; vx = vy = 0; airborne = false; px = 0; pz = 0; py = waterY; prevWaterY = float.NaN;
        spinRot = flipRot = pitchRot = 0; spinHeld = flipHeld = pitchHeld = false;
        handOn = false; handT = handPts = handRisk = handHold = handAcc = 0;
        heliPh = -1; heliT = heliPose = heliLay = heliIncline = heliSet = heliCrouch = 0; heliUsed = heliAir = heliHeld = false; BoardTrickClear();
        diveOn = false; diveShield = false; divePitch = divePose = divePike = diveLoad = diveLift = 0;
        isleOn = false; ashore = false; isleFace = 0; isleWalk = 0; isleRate = 1f; chutePush = 0; laneLim = 17f;
        poseTake = poseCrouch = poseExt = poseAir = poseLand = 0; transform.rotation = Quaternion.identity;
        hanging = false; ropeOffer = false; hangAcc = 0; supT = -1f;         // off the rope (web respawn 27184)
        TrickInputClear(); ClearCrash();
        Say("BACK UP!");
    }

    /// the run ends where he stands, no tumble (web forkIslandDone -> gameOver): the ride's own
    /// end-of-run branch picks it up from here, the way a wipeout would, with the crash already over
    public void End(string msg)
    {
        if (wipe) return;
        wipe = true; running = false; wipeT = WipeShow; wipeReal = WipeShow;   // the card at once: there was no crash to watch
        handOn = false; handPts = 0; handHold = 0;
        heliPh = -1; heliAir = false; heliHeld = false; heliSet = 0; heliLay = 0; heliUsed = false;
        kf = null;                                   // a board trick in hand when he goes over just stops
        hanging = false; ropeOffer = false; supT = -1f;
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
        if (!running || wipe || kf != null || heliAir || hanging) return;     // not on the rope (web 26970)
        if (diveOn || isleOn || glideOn || heliPh >= 0) return;              // nor under the water, ashore, on the wing or in the helicopter's beats: the keys could reach it there
        if (!airborne) Jump();
        if (!airborne) return;
        kf = T; kfT = 0f;
        // a 360 shove or a tre has further to go than a kickflip and gets a little longer for it
        kfDur = 0.62f + (T.axis == "both" ? 0.14f : 0f) + (T.axis == "yaw" && T.turns >= 1f ? 0.10f : 0f);
        Sfx.Trick(T.id);                     // web noteTrick (26978): only once the board is actually going round
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
    float tNow;                          // the real clock the wave clock's surge is keyed to (web tNow)

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
        if (!running || airborne || heliPh >= 0 || diveOn || isleOn || hanging) return;   // the helicopter, the dive, the island and the rope own him through their beats
        vy = 11.8f * statAir; airborne = true; poseTake = 1f;
    }

    /// web ropeAction's hook (27038-27040): on the line, off whatever he was doing on the water.
    /// Not out of a beat that owns him: the helicopter's, the dive's, the island's, the wing's.
    public void Hook()
    {
        if (!running || wipe || hanging || heliPh >= 0 || diveOn || isleOn || glideOn) return;
        hanging = true; wakeAir = false; airborne = true; vy = 0f; hangAcc = 0f;
        Say("HOOKED!");
    }

    /// web releaseRope (27045-27049): he drops off with a little lift; let go on purpose and he
    /// throws a Superman backflip off it, paid when it comes round
    public void ReleaseRope(bool withTrick)
    {
        if (!hanging) return;
        hanging = false; vy = 6f; airborne = true;
        if (withTrick) { supT = 0f; Say("SUPERMAN!"); }
    }

    /// hold-to-turn: he keeps rotating for as long as the button is down. The handstand is
    /// held too, but on the water; the helicopter starts on the press and the hold is the roll.
    public void Hold(string trick, bool down)
    {
        // a board trick is one press; the release means nothing to it (web 23815)
        var bt = TrickCatalog.Get(trick);
        if (bt != null && bt.Board) { if (down) StartBoardTrick(bt); return; }
        if (trick == "hand") { handOn = down; return; }              // its sound is the stand being banked, not the press
        if (trick == "heli") { if (down) StartHeli(); else heliHeld = false; return; }
        if (!down)
        {
            if (trick == "spin") spinHeld = false;
            else if (trick == "roll") flipHeld = false;
            else if (trick == "flip") pitchHeld = false;
            return;
        }
        // web spinBtnDown (23793-23800): nothing while he is not riding; off the water it pops him first
        if (!running || wipe) return;
        if (!airborne && !hanging) Jump();
        if (trick == "spin") spinHeld = true;
        else if (trick == "roll") flipHeld = true;
        else if (trick == "flip") pitchHeld = true;
        else return;
        if (airborne || hanging) Sfx.Trick(trick);                  // only when it can actually turn
    }

    // ---- the gestures' entry points (web addRoll / addFlip / addSpin, 23313-23330) ----
    // Each pops him if he is on the water and queues one more whole turn, four at most, the way
    // the last gesture pointed. Nothing is queued when he cannot leave the water (the helicopter's
    // beats, the dive, the island), so a turn is never left owing for a later jump to spend.
    bool CanQueue()
    {
        if (!running || wipe || diveOn || isleOn) return false;
        if (!airborne && !hanging) Jump();
        return airborne || hanging;
    }
    /// a barrel roll, about the length of the board (the ROLL button's axis)
    public void AddRoll(float dir)
    {
        if (!CanQueue()) return;
        flipSign = dir < 0f ? -1f : 1f; flipQ = Mathf.Min(flipQ + Turn, Turn * 4f);
        Sfx.Trick("roll");
    }
    /// a front or back flip, end over end (the FLIP button's axis)
    public void AddFlip(float dir)
    {
        if (!CanQueue()) return;
        pitchSign = dir < 0f ? -1f : 1f; pitchQ = Mathf.Min(pitchQ + Turn, Turn * 4f);
        Sfx.Trick("flip");
    }
    /// a flat spin
    public void AddSpin(float dir)
    {
        if (!CanQueue()) return;
        spinSign = dir < 0f ? -1f : 1f; spinQ = Mathf.Min(spinQ + Turn, Turn * 4f);
        Sfx.Trick("spin");
    }

    /// web startTrick (26889-26896), the keys' way in: J spins, K rolls, L grabs. None of them
    /// pops him; they need him clear of the water already, and they make no sound of their own.
    public void StartTrick(string type)
    {
        if (!running || !TrickReady) return;
        if (diveOn || isleOn || glideOn || heliPh >= 0) return;              // a key press must not start one where the buttons are hidden (a grab under the dive wiped him on landing)
        if (type == "spin") spinHeld = true;
        else if (type == "roll") flipHeld = true;
        else if (type == "grab" && !grabOn) { grabOn = true; grabPts = 0f; grabSide = vx >= 0f ? 1 : -1; }
    }
    public void ReleaseTrick(string type)
    {
        if (type == "spin") spinHeld = false;
        else if (type == "roll") flipHeld = false;
    }

    /// web startDrag (26902-26905): a paw in the water on the side he is carving toward. Not out
    /// of a beat that owns him (the helicopter's, the dive's, the island's), where there is no
    /// open water beside the board to put it in.
    public void StartDrag()
    {
        if (!running || airborne || hanging || dragOn || heliPh >= 0 || diveOn || isleOn) return;
        dragOn = true; dragPts = 0f; dragSide = vx >= 0f ? 1 : -1;
    }
    /// web endDrag (26906-26912): 25 or more is paid and named; either way he pops back up
    public void EndDrag()
    {
        if (!dragOn) return;
        int p = Mathf.RoundToInt(dragPts);
        if (p >= 25 && running && !wipe) { score += p; boost = Mathf.Min(16f, boost + 2.5f); Say("HAND DRAG! +" + p); }
        dragOn = false; dragPts = 0f; releaseKick = 0.30f;
    }
    /// web endGrab (26913-26919): 30 or more is paid and named by the rail he had
    public void EndGrab()
    {
        if (!grabOn) return;
        int p = Mathf.RoundToInt(grabPts);
        if (p >= 30 && running && !wipe) { score += p; boost = Mathf.Min(16f, boost + 3f); Say((grabSide > 0 ? "INDY" : "MELON") + " GRAB! +" + p); }
        grabOn = false; grabPts = 0f; releaseKick = 0.35f;
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
                Sfx.Trick("handstand");                  // web noteTrick (44827): the stand's sound is the bank
            }
            handPts = 0f;
        }
    }

    public void Step(float dt)
    {
        // the web's wave clock (46758): the swell surges and eases instead of ticking at one rate,
        // the surge keyed to the real clock (tNow), not to itself, and at its full depth
        tNow += dt;
        t += dt * (1f + 0.55f * Mathf.Sin(tNow * 0.045f) + 0.28f * Mathf.Sin(tNow * 0.017f + 1.3f));
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
        WaveField.CarrySideways(px);
        float stage = Mathf.Min(1f, dist / 9000f);
        // sets roll through small, medium, large and the odd extra-large (web 4058, 46754): three slow
        // sines, not one, so the size wanders instead of breathing on a single beat
        float setNoise = 0.45f * Mathf.Sin(t * 0.055f) + 0.30f * Mathf.Sin(t * 0.021f + 2.1f) + 0.25f * Mathf.Sin(t * 0.009f + 4.7f);
        WaveField.Amp = Mathf.Max(0.34f, 1.12f + stage * 1.0f + setNoise * (0.34f + stage * 0.5f));

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

        wakeGrace = Mathf.Max(0f, wakeGrace - dt);
        if (!airborne) { wakeAir = false; pipeAir = false; }   // whatever put him down (a landing, the dive, a wipeout), he is not off a wake or a wall any more
        if (heliPh >= 0) HeliStep(dt);          // the helicopter drives him itself (web 45394)
        else if (diveOn || isleOn) vy = 0;      // the dive owns py outright, launch to landing (web 45330-45347); the island likewise, beaching to the walk (web 45359-45449)
        else if (glideOn) vy = 0;               // and the paraglide, tow to splashdown: forty feet up passes every test below, which would start landing him (web 45348-45352)
        else if (hanging)
        {
            // on the plane's rope (web 45353-45357): drawn up high enough to throw tricks and
            // pulled in under the plane, paid as long as he hangs on; above the landing test,
            // which would otherwise call five feet of air a jump and bring him down
            airborne = true; vy = 0f;
            py += (HangY - py) * Mathf.Min(1f, dt * 2.4f);
            px += (towX - px) * Mathf.Min(1f, dt * 1.5f);
            hangAcc += 60f * dt; int whole = Mathf.FloorToInt(hangAcc); score += whole; hangAcc -= whole;
            speed = Mathf.Min(60f, speed + 14f * dt);
        }
        else if (airborne || py > waterY + 0.55f)                // clear of the water: off a jump, or the sea dropped away under him (web 45452)
        {
            vy -= (wakeAir ? 6f : 20f) * dt; py += vy * dt;      // off a wave that size, the way down is long (web 45456)
            if (py <= waterY && wakeGrace <= 0f && !airborne) { vy *= 0.34f; }   // just back onto the water off a dropping face: the spring takes him from here
            else if (py <= waterY && wakeGrace <= 0f)
            {
                float impact = Mathf.Min(1.6f, Mathf.Max(0f, -vy) / 15f);
                poseLand = Mathf.Min(1f, 0.55f + impact * 0.5f);
                onSplash?.Invoke(new Vector3(px, waterY, pz), 20 + (int)(impact * 40));
                Sfx.Land(impact);
                bool anySpin = spinRot > 0.4f || flipRot > 0.4f || pitchRot > 0.4f;
                // still holding the rail when he touches down (web 45478-45481)
                if (grabOn && (spinRot > 0.4f || flipRot > 0.4f || grabPts > 40f)) { CarryTurn(); Settle(false); Wipeout("foam"); return; }
                if (anySpin)
                {
                    float offS = Mathf.Abs(Mathf.Repeat(spinRot + Mathf.PI / 2f, Mathf.PI) - Mathf.PI / 2f);
                    float offF = Mathf.Max(Mathf.Abs(Mathf.Repeat(flipRot + Mathf.PI, Turn) - Mathf.PI),
                                           Mathf.Abs(Mathf.Repeat(pitchRot + Mathf.PI, Turn) - Mathf.PI));
                    if (offS < LandYaw && offF < LandFlip)
                    {
                        // an odd number of half turns of yaw: he carries on riding switch, or comes
                        // back to regular, and turns to wave at the lens (web 45491-45494)
                        if (Mathf.FloorToInt(spinRot / Mathf.PI + 0.5f) % 2 != 0)
                        {
                            switchRide = !switchRide; waveT = WaveDur;
                            Say(switchRide ? "SWITCH!" : "BACK TO REGULAR!");
                        }
                        Settle(true);
                    }
                    // short on the flip is under-rotated; otherwise the yaw was off and it is sketchy (web 45500)
                    else { CarryTurn(); Settle(false); Wipeout(offF >= LandFlip ? "under" : "land"); return; }
                }
                else if (grabOn) { Settle(false); Wipeout("foam"); return; }
                // a turn still queued when the water arrives is not carried into the next jump
                spinQ = flipQ = pitchQ = 0f;
                // he keeps a third of the speed he came down at and digs in, and the spring below
                // brings him back up through the water (web 45505), rather than stopping dead on it
                airborne = false; vy *= 0.34f;
                heliUsed = false; heliAir = false; heliHeld = false; heliSet = 0; heliLay = 0;   // down is down, rolled or not
            }
        }
        else
        {
            // ---- floated, on a spring (web 45509-45515) ----
            // The board is held to the water by a stiff damped spring rather than pinned to it:
            // it rides up a face and drops down the back with a little give, settles after a
            // landing, and the damping is measured against the water's own rise and fall so it
            // follows the swell without lagging it. It never buries itself more than 0.45 ft.
            float wDot = float.IsNaN(prevWaterY) ? 0f : Mathf.Clamp((waterY - prevWaterY) / Mathf.Max(1e-3f, dt), -14f, 14f);
            vy += ((waterY - py) * 56f - (vy - wDot) * 10.4f) * dt;
            vy = Mathf.Clamp(vy, -24f, 24f);
            py += vy * dt;
            if (py < waterY - 0.45f) { py = waterY - 0.45f; vy = Mathf.Max(vy, 0f); }
            if (vy < -5.5f && Random.value < dt * 26f) onSplash?.Invoke(new Vector3(px + (Random.value - 0.5f) * 0.8f, py + 0.15f, pz - 0.6f), 2);
        }
        prevWaterY = waterY;
        rideRamps?.Invoke(dt);                  // a ramp's deck under him wins over the sea (web 45531-45602)

        // the hand drag (web 45737-45752): leaving the water ends it, and while it lasts it pays 70
        // a second and the paw throws spray off that rail
        if (dragOn)
        {
            if (airborne || hanging || diveOn || isleOn || heliPh >= 0) EndDrag();
            else
            {
                dragPts += 70f * dt;
                dragAcc += dt * 90f;
                while (dragAcc > 1f)
                {
                    dragAcc -= 1f;
                    onSplash?.Invoke(new Vector3(px + dragSide * (0.95f + Random.value * 0.2f), waterY - 0.12f, pz - 0.15f + Random.value * 0.7f), 1);
                }
            }
        }
        releaseKick = Mathf.Max(0f, releaseKick - dt);

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
            // held, it turns while the button is down; tapped, it spends what is queued (web 45795-45800)
            if (spinHeld) spinRot += dt * SR;
            else if (spinQ > 0f) { float d = Mathf.Min(dt * SR, spinQ); spinRot += d; spinQ -= d; }
            if (flipHeld) flipRot += dt * FR;
            else if (flipQ > 0f) { float d = Mathf.Min(dt * FR, flipQ); flipRot += d; flipQ -= d; }
            if (pitchHeld) pitchRot += dt * FR;
            else if (pitchQ > 0f) { float d = Mathf.Min(dt * FR, pitchQ); pitchRot += d; pitchQ -= d; }
            if (grabOn) grabPts += 95f * dt;                         // web 45815
        }
        else { spinHeld = flipHeld = pitchHeld = false; }
        // On the rope there is no landing to judge a rotation, so it banks as soon as he lets go
        // of the button near square (web 45836-45841; the flip is counted here as well, since in
        // this port it is held like the other two). A turn still queued has not finished: banked
        // on the way past square it would pay a quarter of a roll and throw the rest away.
        if (hanging && !spinHeld && !flipHeld && !pitchHeld && spinQ <= 0.05f && flipQ <= 0.05f && pitchQ <= 0.05f
            && (spinRot > 0.4f || flipRot > 0.4f || pitchRot > 0.4f))
        {
            float offS = Mathf.Abs(Mathf.Repeat(spinRot + Mathf.PI / 2f, Mathf.PI) - Mathf.PI / 2f);
            float offF = Mathf.Max(Mathf.Abs(Mathf.Repeat(flipRot + Mathf.PI, Turn) - Mathf.PI),
                                   Mathf.Abs(Mathf.Repeat(pitchRot + Mathf.PI, Turn) - Mathf.PI));
            if (offS < 0.7f && offF < 0.7f) Settle(true);
        }
        // the gate is whether more rotation is owed, held or queued (web 45816-45818)
        Sfx.TrickTurn("spin", spinRot, spinHeld || spinQ > 0.05f); Sfx.TrickTurn("roll", flipRot, flipHeld || flipQ > 0.05f); Sfx.TrickTurn("flip", pitchRot, pitchHeld || pitchQ > 0.05f);
        if (heliPh < 0 && !heliAir) heliIncline = Ease(heliIncline, 0f, dt, 4f);   // web 45929

        // the lie-down the rig reads (web 44141-44143): scripted off the beats, eased only on the way out
        bool heliOnNow = heliAir || heliPh >= 1;
        if (heliPh >= 0) heliPose = heliLay;
        else heliPose = Ease(heliPose, heliOnNow ? 1f : 0f, dt, heliOnNow ? 12f : 6f);

        // the handstand (web 44625-44627): the button eases him up at 7 and down at 9, and handT
        // is what the rig turns him over by and what the scoring reads
        bool handWant = handOn && !wipe && !diveOn && !isleOn && !hanging;    // nothing to stand on his hands on, ashore (web 45618), or on the rope (web 44682)
        handT += ((handWant ? 1f : 0f) - handT) * Mathf.Min(1f, dt * (handWant ? 7f : 9f));
        if (handT < 0.002f && !handWant) handT = 0f;
        HandstandScore(dt, handWant);
        if (wipe) return;                      // his arms gave out

        // the pose beats
        poseTake = Mathf.Max(0f, poseTake - dt * 2.2f);
        float tp = 1f - poseTake;
        poseCrouch = poseTake > 0 ? Mathf.Max(0f, 1f - Mathf.Abs(tp - 0.16f) / 0.16f) : 0f;
        poseExt = poseTake > 0 ? Mathf.Max(0f, 1f - Mathf.Abs(tp - 0.52f) / 0.30f) : 0f;
        poseAir = Mathf.Lerp(poseAir, (airborne && !hanging) ? 1f : 0f, Mathf.Min(1f, dt * (airborne ? 8f : 14f)));   // hanging off a rope is not a jump (web 44130)
        poseLand = Mathf.Max(0f, poseLand - dt * 3.0f);
        // the wave after a half turn runs down only while he is planted on the deck (web 44342)
        if (waveT > 0f && !airborne && !hanging && !dragOn && !glideOn) waveT = Mathf.Max(0f, waveT - dt);
        if (msgT > 0) msgT -= dt;
        // the Superman backflip off the line (web 45843-45848): a whole turn back over 0.8 s,
        // paid when it comes round
        float sup = 0f;
        if (supT >= 0f)
        {
            supT += dt; sup = Mathf.Min(1f, supT / SupDur);
            if (sup >= 1f) { score += SupPts; boost = Mathf.Min(16f, boost + 6f); Say("SUPERMAN BACKFLIP! +" + SupPts); supT = -1f; sup = 0f; }
        }

        // coins: a shell a few metres, the web game's pace
        // the web's pay (coinsFor, web 10096-10098): a shell every five metres and one for every
        // eighteen trick points, plus what the chest, the pearls and the island hand over
        coins = Mathf.FloorToInt(dist / 5f) + Mathf.FloorToInt(score / 18f) + bonusCoins;

        // the transform: position, the trick rotation, and the carve lean on top
        transform.position = new Vector3(px, py, pz);
        var trick = TrickRotation();
        // the board lies along the water it rests on (web 45838, 45908), and leans into the carve
        float yaw = -carve * 0.30f, roll = -carve * 0.55f + (airborne ? 0f : hullRoll * 1.20f);
        if (grabOn && airborne) roll += grabSide * 0.30f;             // tipped toward the rail he is holding (web 45984)
        float pitch = rampPitch != 0f ? rampPitch : airborne ? Mathf.Clamp(-vy * 0.03f, -0.35f, 0.35f) : Mathf.Clamp(hullPitch * 1.02f, -0.62f, 0.68f);   // on a ramp, along its deck (web 45954)
        pitch += heliIncline;                  // the helicopter's nose-down and the lean-back out (web 45949)
        if (diveOn)
        {
            // the board goes with him in this port: it tips the way he swims and folds over the
            // top of the launch with him (the web pikes the rider alone, 0.72 past level), and no
            // carve rolls him about the lane under the water (web 46015)
            pitch = divePitch - 0.72f * divePike; roll = -carve * 0.55f;
        }
        // A cracked board is unsteady under you (web 45966-45976): a slow, readable sway rather
        // than jitter, rocking the rails, nudging the nose, and pushing him off his line so he has
        // to steer back. Each crack adds to it; in the air it is a third as much.
        wobT += dt;
        if (Hull.Cracks > 0)
        {
            float wk = Hull.Cracks * (airborne ? 0.35f : 1f);
            roll += Mathf.Sin(wobT * 4.3f) * 0.045f * wk + Mathf.Sin(wobT * 2.17f + 1.3f) * 0.030f * wk;
            yaw += Mathf.Sin(wobT * 1.71f + 1.1f) * 0.032f * wk;
            vx += Mathf.Sin(wobT * 1.45f) * wk * 2.1f * dt;
        }
        transform.rotation = trick * Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
        if (sup > 0f) transform.rotation = Quaternion.AngleAxis(-sup * 360f, Vector3.right) * transform.rotation;   // the backflip turns him end over end, outside everything else (web player.rotation.x)
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
        // a rail still in hand joins the combo under its own name (web 26927)
        if (grabOn && grabPts >= 30f) { pts += Mathf.RoundToInt(grabPts); names++; label += (grabSide > 0 ? "INDY" : "MELON") + " "; }
        // web 26873-26880: the helicopter scores as the roll it is, and takes the name when it was rolled that way
        if (heliUsed && flipRot > 0.4f) { pts = Mathf.RoundToInt(pts * 1.35f); label = label.Replace("° ROLL", "° HELICOPTER"); }
        heliUsed = false; heliAir = false; heliPh = -1; heliHeld = false; heliSet = 0; heliLay = 0;
        if (landed && pts > 0)
        {
            if (names > 1) pts = Mathf.RoundToInt(pts * 1.5f);
            float sq(float a) => Mathf.Abs(Mathf.Repeat(a + Mathf.PI, Turn) - Mathf.PI);
            float half(float a) => Mathf.Abs(Mathf.Repeat(a + Mathf.PI / 2f, Mathf.PI) - Mathf.PI / 2f);
            float off = Mathf.Max(half(spinRot), sq(flipRot), sq(pitchRot));
            // the window narrows with every crack (web 26945): damage costs you style, not just health
            float tight = 1f - Hull.Cracks * 0.20f;
            if (off < 0.16f * tight) { pts = Mathf.RoundToInt(pts * 2.0f); label += "PERFECT "; names++; }
            else if (off < 0.45f * tight) { pts = Mathf.RoundToInt(pts * 1.4f); label += "CLEAN "; names++; }
            pts = Mathf.RoundToInt(pts * statTrick);      // the rider's trick perk
            score += pts;
            boost = Mathf.Min(16f, boost + 5f + Mathf.Min(7f, pts / 110f));
            // the web's line: the names joined with " + ", PERFECT and CLEAN counted among them, so a
            // clean single spin reads "360° SPIN + CLEAN COMBO! +126" (web 26950)
            var parts = System.Text.RegularExpressions.Regex.Split(label.Trim(), @"(?<=SPIN|ROLL|FLIP|HELICOPTER|INDY|MELON|PERFECT|CLEAN) ");
            Say(string.Join(" + ", parts) + (names > 1 ? " COMBO" : "") + "! +" + pts);
            Sfx.Buzz(pts >= 400 ? 35 : 20);
        }
        spinRot = flipRot = pitchRot = 0; spinHeld = flipHeld = pitchHeld = false;
        grabOn = false; grabPts = 0f;
        spinQ = flipQ = pitchQ = 0f;                // anything still owed dies with the landing (web 26957)
        return pts;
    }

    /// the trick's turn on top of everything else, each axis the way it was last thrown (web 45834)
    public Quaternion TrickRotation()
    {
        return Quaternion.Euler(pitchRot * pitchSign * Mathf.Rad2Deg, spinRot * spinSign * Mathf.Rad2Deg, flipRot * flipSign * Mathf.Rad2Deg);
    }

    public void Say(string s) { lastMsg = s; msgT = 0.9f; }

    /// what the crash is called, the web's WIPE table (27623): each thing that ends a run says what it was
    public static string WipeMsg(string kind)
    {
        switch (kind)
        {
            case "buoy": return "BUOY!";
            case "jelly": return "ZAPPED!";                    // the web adds a lightning emoji the HUD font has no glyph for
            case "log": return "LOG!";
            case "bigfin": return "SHARK!";
            case "rock": return "ROCK!";
            case "octopus": return "TENTACLED!";
            case "pearl": return "PEARLED!";
            case "land": return "SKETCHY LANDING!";
            case "under": return "UNDER-ROTATED!";
            case "foam": return "ATE IT!";
            case "debris": return "WRECKAGE!";
            case "snap": return "BOARD SNAPPED!";
        }
        return "WIPEOUT!";
    }

    public void Wipeout(string kind)
    {
        if (wipe) return;
        waveT = 0f;                                                           // no waving to the lens on the way down
        // what he was doing, read before any of it is cleared: the launch is built from it
        bool onRing = ring != null && ring.tubeRide && !float.IsNaN(ring.rad);
        // The board flies off on its own only from the open water. Under the water it stays with
        // him (this port carries it through the dive), and ashore it is lying on the sand already.
        bool kickBoard = !diveOn && !isleOn && !ashore;
        float vx0 = vx, vy0 = vy, eff0 = eff;
        wipe = true; running = false; wipeT = 0; wipeReal = 0;
        isleOn = false; ashore = false;                                          // not his to hold any more
        handOn = false; handPts = 0; handHold = 0;                              // handT itself eases off in the tumble
        heliPh = -1; heliAir = false; heliHeld = false; heliSet = 0; heliLay = 0; heliUsed = false;
        hanging = false; ropeOffer = false; supT = -1f;                          // off the rope (web 27914)
        BoardTrickClear();                                                       // a kickflip in hand stops with the run (web 27908)
        Say(WipeMsg(kind));
        Sfx.Hit(kind);
        wipeSlow = 1f;                                                           // the clock stops, then comes back (web 27916)
        // One burst is a puff. Two -- a hard tight one at the board and a wider one around it --
        // is water being displaced by something arriving (web 27921-27922).
        onSplash?.Invoke(new Vector3(px, py, pz), 44);
        onSplash?.Invoke(new Vector3(px, py + 0.35f, pz + 0.8f), 22);

        // ---- thrown from what he was actually doing (web 27923-27988) ----
        // Forward momentum throws him over the thing, his drift carries across, vertical speed he
        // already had is kept; the table supplies the character of each crash on top of that.
        var W = ThrowFor(kind);
        float fwd = eff0 * 0.11f;
        float side = Random.value < 0.5f ? -1f : 1f;                             // he goes one way, the board the other
        float rvy = Mathf.Max(1.6f, W.vy * 0.55f + Mathf.Max(-6f, vy0) * 0.45f);
        float rvz = W.vz * 0.55f - fwd;
        float rvx = vx0 * 0.80f + side * (2.0f + Random.value * 1.8f);
        if (onRing)
        {
            // on the barrel's wall what he is doing is going round it: the ring's own tangential
            // motion, halved, no random kick and no upward floor, and gravity takes it from there
            float sp = ring.angV * Mathf.Max(0.5f, ring.rad) * 0.5f;
            rvx = Mathf.Clamp(ring.side * Mathf.Cos(ring.ang) * sp, -4f, 4f);
            rvy = Mathf.Clamp(Mathf.Sin(ring.ang) * sp, -6f, 3f);
            rvz *= 0.4f;
        }
        wvy = rvy * 1.1f + 2.2f; wvz = rvz; wvx = rvx;
        // a body goes over MOSTLY ONE WAY: the table's figure picks the main axis, the others are
        // the untidy bit on top, and a rotation already under way carries on
        wsx = (Random.value < 0.5f ? -1f : 1f) * (W.s0 * 0.20f + 0.55f);
        wsy = carrySpin * 0.18f + (Random.value < 0.5f ? -1f : 1f) * (W.s1 * 0.10f + 0.22f);
        wsz = carryFlip * 0.18f + (Random.value < 0.5f ? -1f : 1f) * (W.s2 * 0.14f + 0.30f);
        carrySpin = carryFlip = 0f;
        ToXyz(transform.rotation, out wrx, out wry, out wrz);
        wSplashed = false; wDep = -1f;
        // the board is kicked out from under him and cartwheels off on its own
        bvx = -side * (2.8f + Random.value * 2.4f) - vx0 * 0.35f; bvy = rvy * 0.6f + 4.0f; bvz = rvz * 0.45f + 1.2f;
        bsx = (Random.value - 0.5f) * 11f; bsz = (Random.value - 0.5f) * 11f; bsy = (Random.value - 0.5f) * 5f;
        boardLanded = false; snapT = 0f;
        boardAt = seat != null ? seat.position : new Vector3(px, py, pz);
        boardRot = seat != null ? seat.rotation : transform.rotation;
        ToXyz(boardRot, out brx, out bry, out brz);
        boardFree = kickBoard;
        if (kind == "snap") Hull.Snap();                                         // and on the last crack it goes in two (web 27905)
        onWipeout?.Invoke();
    }

    /// the wave clock, as Step runs it: the sea goes on rolling through the crash (web 28476-28477)
    void WaveClock(float dt)
    {
        tNow += dt;
        t += dt * (1f + 0.55f * Mathf.Sin(tNow * 0.045f) + 0.28f * Mathf.Sin(tNow * 0.017f + 1.3f));
    }

    /// in the shark's jaws (web startEaten 28019-28026): he is carried, not thrown, so the launch is
    /// taken off him, and the board goes with him as it always has here
    public void WipeHeld(float dt)
    {
        WaveClock(dt);
        wipeT += dt;
        wvx = wvy = wvz = wsx = wsy = wsz = 0f;
        boardFree = false;
    }

    /// the crash tumble, played by whoever owns the rider during the beat
    public void WipeStep(float dt)
    {
        WaveClock(dt);
        wipeT += dt;
        // he comes down off his hands and off the nose as he goes over, the rates the ride uses
        handT += (0f - handT) * Mathf.Min(1f, dt * 9f);
        heliPose = Ease(heliPose, 0f, dt, 6f); heliCrouch = Ease(heliCrouch, 0f, dt, 9f);
        if (isleOn)
        {
            // the run ended on the sand (End): he stands where he stopped while the card comes up
            vy = 0; transform.position = new Vector3(px, py, pz); return;
        }
        // The air slows a thrown body's spin the whole way down, so the first half-turn off the
        // board is the fast one (web 28240-28253)
        float airK = 1f - Mathf.Min(1f, dt * WipeAirDrag);
        wsx *= airK; wsy *= airK; wsz *= airK;
        wrx += wsx * dt * WipeSpinK; wry += wsy * dt * WipeSpinK; wrz += wsz * dt * WipeSpinK;
        float wz = pz;
        if (diveOn)
        {
            // taken under the water: he tumbles where he is and settles a little, and is not
            // surfaced first (web 41783: the fog and the sand say where this happened)
            vy = 0; py -= dt * 0.8f;
        }
        else
        {
            // thrown, tumbling, then splashdown, under real gravity (web 28231-28234)
            wvy -= 32.2f * dt;
            wvx *= 1f - Mathf.Min(1f, dt * 1.6f);                                // air drag, so he does not fly off sideways
            px += wvx * dt; py += wvy * dt; vy = wvy;
            wz = pz + Mathf.Clamp(wvz * wipeT * 0.70f, -9f, 2f);
            float sea = WaveField.Height(px, dist * WaveField.Drift + pz, t) + 0.13f;
            if (!wSplashed && py <= sea)
            {
                wSplashed = true;
                onSplash?.Invoke(new Vector3(px, sea, wz), 34);
                py = sea;
            }
            if (wSplashed)
            {
                // In the water he rides the swell down to the chin: the DEPTH settles, and where
                // that puts him is wherever the water is this frame (web 28282-28299). He comes
                // upright, the water takes the spin out of him at once, and he holds still.
                if (wDep < 0f) wDep = Mathf.Max(0f, sea - py);
                wDep += (1.26f - wDep) * Mathf.Min(1f, dt * 3.0f);
                py = sea - wDep; wvy = 0f; vy = 0f;
                wrx += (0.16f - wrx) * Mathf.Min(1f, dt * 5.2f);
                wrz *= 0.74f; wsx *= 0.62f; wsy *= 0.70f; wsz *= 0.62f;
                wry += (0f - wry) * Mathf.Min(1f, dt * 5.2f);
                // spray thrown off the hands, and only off the hands
                for (int s = -1; s <= 1; s += 2)
                    if (Random.value < dt * 6f) onSplash?.Invoke(new Vector3(px + s * (0.45f + Random.value * 0.3f), sea, wz + (Random.value - 0.5f) * 0.7f), 1);
            }
        }
        transform.position = new Vector3(px, py, wz);
        transform.rotation = Xyz(wrx, wry, wrz);

        // the board: cartwheels away, slaps down, settles flat and drifts (web 28408-28445)
        if (boardFree)
        {
            bvy -= 32.2f * dt;                                                    // it falls under the same sky he does
            boardAt += new Vector3(bvx, bvy, bvz) * dt;
            float bsea = WaveField.Height(boardAt.x, dist * WaveField.Drift + boardAt.z, t) + 0.13f;
            if (boardAt.y < bsea)
            {
                boardAt.y = bsea; bvy *= -0.30f; bvx *= 0.7f; bvz *= 0.7f;
                if (Mathf.Abs(bvy) > 1.2f) onSplash?.Invoke(new Vector3(boardAt.x, bsea, boardAt.z), 12);
                boardLanded = true;
            }
            if (boardLanded)
            {
                brx += (0f - brx) * Mathf.Min(1f, dt * 3f);
                brz += (0f - brz) * Mathf.Min(1f, dt * 3f);
                // ...and the water takes the spin and the drift out of it: this added the crash's spin
                // at a quarter rate for ever, so the board lying on the sea went on turning like a
                // record; a board in the water is dragged still within a second or two
                float wd = Mathf.Exp(-2.2f * dt);
                bsz *= wd; bvx *= wd; bvz *= wd;
                bry += bsz * dt * 0.25f;
                boardAt.y = bsea + 0.02f;
            }
            else { brx += bsx * dt; brz += bsz * dt; bry += bsy * dt; }        // and about its own length, so it corkscrews too
            boardRot = Xyz(brx, bry, brz);
        }
        // the halves of a snapped board part while they are in the air and lie down once they are in
        if (!boardLanded) snapT = Mathf.Min(2.2f, wipeT);
        Hull.SnapStep(snapT, boardLanded, dt);
    }
}
