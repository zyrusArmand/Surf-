// The rider's body. The pug file carries a Mixamo-style skeleton, and this drives it the way
// the web game does: every pose is an OFFSET from the model's own rest, so the same numbers
// mean the same thing on any file rigged this way. The knees load and never sit still, the
// lean lives in the spine so the feet stay planted, the arms trail and counter, and each ankle
// is SOLVED every frame so the soles stay flat on the deck whatever the rest of him just did.
//
// Angles are in radians, as in the original, and the rotation order is the original's XYZ.
// glTF is right-handed and Unity is not: the importer mirrors the model across x, which keeps
// an x turn as it was and reverses y and z turns. That sign flip is applied once, in Off().
using System.Collections.Generic;
using UnityEngine;

public class RiderRig : MonoBehaviour
{
    public BoardController rider;
    public float crouchW, tuckW;          // a wind-up, and the knees-up while the board flips under him
    // the two tricks, 0..1, read off the rider every Stance: the handstand's blend (web handT),
    // the helicopter's lie-down (web heliPose) and its crouch beat (web rigCrouch under the beats)
    public float handW, heliW, heliCrouchW;
    // the seat, 0 standing on a board and 1 sat: the web's tubeSit and skiSit, eased at five a
    // second toward 1 while that is the board in use and he is not wiping out, so equipping the
    // ski is somebody getting on rather than somebody teleporting into a seated pose. sitW is
    // the larger of the two; tubeW is sat IN the ring, skiW astride the ski
    public float sitW, tubeW, skiW;
    float seat;                           // web spec.seat: how far below the board's origin he sits, in feet
    bool onTube, onSki;                   // what the board in use is (SetBoard)

    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    readonly Dictionary<string, Quaternion> rest = new Dictionary<string, Quaternion>();
    readonly Dictionary<string, Quaternion> chainRest = new Dictionary<string, Quaternion>();
    float sway, load, bob;
    bool ready;
    Vector3 homePos; Quaternion homeRot;  // where the ride parks him on the deck, which the tricks turn him away from
    float handLift;                       // web HAND_LIFT: how far up to put him, inverted, so his paws land on the deck

    static readonly string[] Posed = { "LeftArm", "RightArm", "LeftForeArm", "RightForeArm", "LeftUpLeg", "RightUpLeg",
                                       "LeftLeg", "RightLeg", "Spine", "Spine01", "Spine02", "neck", "Head", "LeftFoot", "RightFoot", "Hips" };

    // the legs fold this much for each thing the ride does to him
    const float FoldCarve = 0.12f, FoldSwell = 0.22f, FoldBob = 0.16f, FoldAir = 0.10f;

    public void Init(BoardController r)
    {
        rider = r;
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
        if (!bones.ContainsKey("Hips") || !bones.ContainsKey("LeftUpLeg")) { ready = false; return; }
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        // the arms come in a little: a bone's local position IS the limb length on this kind of rig
        foreach (var n in new[] { "LeftForeArm", "RightForeArm", "LeftHand", "RightHand" })
            if (bones.TryGetValue(n, out var b)) b.localPosition *= 0.88f;
        foreach (var n in Posed) if (bones.TryGetValue(n, out var b)) rest[n] = b.localRotation;
        NormaliseArms();
        // the rest rotation of the chain above each ankle, which is what "flat" means
        foreach (var s in new[] { "Left", "Right" })
        {
            var acc = Quaternion.identity;
            foreach (var n in new[] { "Hips", s + "UpLeg", s + "Leg" }) if (bones.TryGetValue(n, out var b)) acc = acc * b.localRotation;
            chainRest[s + "Foot"] = acc;
        }
        homePos = transform.localPosition; homeRot = transform.localRotation;
        handLift = MeasureHandLift();
        ready = true;
    }

    /// The board he is on, which says whether he stands on it or sits: the tube (spec tube) is
    /// sat in, the jet ski sat on, and either carries the seat drop; everything else, the kite
    /// twin-tip included, is ridden standing. Safe before or after Init, and with null.
    public void SetBoard(BoardCatalog.Board b)
    {
        seat = b != null ? b.seat : 0f;
        onTube = b != null && b.tube;
        onSki = b != null && !b.tube && (b.id == "skijet" || b.seat != 0f);
    }

    // The handstand pose, the web's HAND_POSE (44566-44573) in the upright frame: arms thrown
    // overhead so that, turned over, they reach down to the deck; legs straight and a little apart;
    // chin tucked. The front limb's numbers serve both sides here, mirrored in y and z.
    void HandPose(float w)
    {
        Set("LeftArm", -2.62f, 0, -0.30f, w); Set("RightArm", -2.62f, 0, 0.30f, w);
        Set("LeftForeArm", -0.18f, 0, 0, w); Set("RightForeArm", -0.18f, 0, 0, w);
        Set("LeftUpLeg", 0.16f, 0, -0.15f, w); Set("RightUpLeg", 0.16f, 0, 0.15f, w);
        Set("LeftLeg", -0.12f, 0, 0, w); Set("RightLeg", -0.12f, 0, 0, w);
        Set("LeftFoot", 0.34f, 0, 0, w); Set("RightFoot", 0.34f, 0, 0, w);
        Set("Head", -0.92f, 0, 0, w);
    }

    // web measureHandLift (44603-44624): where his feet rest when upright, plus how far the raised
    // paws reach above the deck in the pose, plus a little daylight. Turning him over sends that
    // reach the other way, so lifting by the two together puts his paws exactly where his feet were.
    // Measured off the bones (the hand chain's ends are the paws) in the parent's units, up the
    // parent's axis, which is his own axis while he is upright.
    float MeasureHandLift()
    {
        var par = transform.parent;
        float Up(Transform b)
        {
            var p = par != null ? par.InverseTransformPoint(b.position) : b.position;
            return p.y - transform.localPosition.y;
        }
        float stand = float.PositiveInfinity;
        foreach (var fn in new[] { "LeftFoot", "RightFoot" })
            if (bones.TryGetValue(fn, out var fb)) foreach (var c in fb.GetComponentsInChildren<Transform>(true)) stand = Mathf.Min(stand, Up(c));
        if (float.IsInfinity(stand)) stand = 0f;
        var keep = new List<KeyValuePair<Transform, Quaternion>>();
        foreach (var n in Posed) if (bones.TryGetValue(n, out var pb)) keep.Add(new KeyValuePair<Transform, Quaternion>(pb, pb.localRotation));
        HandPose(1f);
        float reach = float.NegativeInfinity;
        foreach (var hn in new[] { "LeftHand", "RightHand", "LeftForeArm", "RightForeArm" })
            if (bones.TryGetValue(hn, out var hb)) foreach (var c in hb.GetComponentsInChildren<Transform>(true)) reach = Mathf.Max(reach, Up(c));
        foreach (var kv in keep) kv.Key.localRotation = kv.Value;
        if (float.IsInfinity(reach)) return 0f;
        return stand + reach + 0.10f;
    }

    static float Mx(float a, float b, float h) { return a * (1f - h) + b * h; }

    // The rider group itself. The handstand turns him over about the deck line and lifts him
    // onto his paws (web 44736-44737); the helicopter turns him to face the nose, lays him flat
    // along the deck and slides him up it (web 44338, 44353, 44358). Both pivot at his feet.
    // The seat (web 44535-44542 for the ring, 44550-44600 for the ski): square to the nose, since
    // his stance is side-on to a board and nobody rides a jet ski sideways; the faintest lean
    // back in the ring, as anyone sitting has, and a touch forward on the ski, which is driving
    // rather than being carried; and DOWN onto the seat rather than standing on it. The web's
    // deck solver lets go for the sit, so the spec's seat is the only term setting his height:
    // the group goes to -seat from the board's origin, where it stood on the deck. In the ring he
    // is over the hole, and on the ski half a foot forward onto the front of the saddle, which is
    // the one place a dog's reach and a man's bars agree.
    void Group(float h1, float h2, float tw, float sk)
    {
        float sit = Mathf.Clamp01(tw + sk);
        float xr = h1 * Mathf.PI - 1.36f * h2 + 0.06f * tw - 0.05f * sk;
        // under the canopy he faces the way he is going, the same eased whole-body turn (web 44234, 44520-44523)
        float gl = rider != null ? Mathf.Clamp01(rider.glideFace) : 0f;
        var yaw = Quaternion.Slerp(homeRot, Quaternion.identity, Mathf.Clamp01(h2 + sit + gl));
        transform.localRotation = Quaternion.AngleAxis(xr * Mathf.Rad2Deg, Vector3.right) * yaw;
        var pos = homePos + new Vector3(0f, h1 * handLift, -0.92f * h2);
        pos.y = Mx(pos.y, -seat, sit);
        pos.x = Mx(pos.x, 0f, tw);
        pos.z = Mx(pos.z, 0f, tw) - 0.50f * sk;
        transform.localPosition = pos;
    }

    // which side of him a limb's root hangs on, along his own right: +1 or -1
    float Side(Transform b, Vector3 right) => Vector3.Dot(b.position - bones["Hips"].position, right) < 0f ? -1f : 1f;

    // the web's aim: turn a limb's root so the line from it to its child lies along t (a world
    // direction), by w, whichever way the file was rigged
    static void Aim(Transform bone, Transform child, Vector3 t, float w)
    {
        var d = child.position - bone.position;
        if (d.sqrMagnitude < 1e-8f) return;
        d.Normalize();
        bone.rotation = Quaternion.Slerp(bone.rotation, Quaternion.FromToRotation(d, t) * bone.rotation, w);
    }

    // ---- SAT IN A RING (web R.tube, 19925-19968) ----
    // Not a variation on the ski's pose: on a ski he is astride a hull with his weight forward
    // over bars; here he is dropped through a hole with nothing under him. Arms out to the sides
    // and a little behind him, which is where they go when you are holding yourself up on a rim,
    // the elbows eased straight; legs straight down through the hole with a slow swing, because
    // they are in the water, the knees eased straight. Aimed in the group's own axes rather than
    // posed, as the web does it, and laid over the surfing stance, which stands down for it.
    void TubePose(float w)
    {
        if (w <= 0.001f) return;
        Vector3 up = transform.up, right = transform.right, back = transform.forward;   // he faces -z of his group
        float T = rider != null ? rider.t : Time.time;
        foreach (var s in new[] { "Left", "Right" })
        {
            if (!bones.TryGetValue(s + "Arm", out var sh)) continue;
            if (!bones.TryGetValue(s + "Hand", out var hand) && !bones.TryGetValue(s + "ForeArm", out hand)) continue;
            float sgn = Side(sh, right);
            Aim(sh, hand, (right * (sgn * 0.92f) + back * 0.30f - up * 0.24f).normalized, w);
            if (bones.TryGetValue(s + "ForeArm", out var fa) && rest.TryGetValue(s + "ForeArm", out var fr)) fa.localRotation = Quaternion.Slerp(fa.localRotation, fr, w * 0.7f);
        }
        foreach (var s in new[] { "Left", "Right" })
        {
            if (!bones.TryGetValue(s + "UpLeg", out var th)) continue;
            if (!bones.TryGetValue(s + "Leg", out var shin) && !bones.TryGetValue(s + "Foot", out shin)) continue;
            float sgn = Side(th, right);
            float kick = Mathf.Sin(T * 1.5f + (sgn > 0f ? 0f : 1.7f)) * 0.13f;
            Aim(th, shin, (-up + back * kick + right * (sgn * 0.10f)).normalized, w);
            if (bones.TryGetValue(s + "Leg", out var sb) && rest.TryGetValue(s + "Leg", out var sr)) sb.localRotation = Quaternion.Slerp(sb.localRotation, sr, w * 0.8f);
        }
    }

    // Arms down, whatever pose the file was modelled in. Each shoulder's REST is turned, once,
    // by exactly the rotation that swings the shoulder-to-hand line to hang about thirty-two
    // degrees out from plumb and a hair forward, so every pose downstream means the same thing.
    void NormaliseArms()
    {
        var up = transform.up;
        Vector3 fwd = transform.forward;
        if (bones.TryGetValue("headfront", out var hf) && bones.TryGetValue("Head", out var hd))
        {
            var f = hf.position - hd.position; f.y = 0;
            if (f.sqrMagnitude > 1e-6f) fwd = f.normalized;
        }
        var right = Vector3.Cross(up, fwd).normalized;
        foreach (var s in new[] { "Left", "Right" })
        {
            if (!bones.TryGetValue(s + "Arm", out var sh)) continue;
            if (!bones.TryGetValue(s + "Hand", out var hand) && !bones.TryGetValue(s + "ForeArm", out hand)) continue;
            var d = hand.position - sh.position;
            if (d.sqrMagnitude < 1e-8f) continue;
            d.Normalize();
            // outward is whichever side of him this arm already hangs on
            float sgn = Mathf.Sign(Vector3.Dot(sh.position - bones["Hips"].position, right));
            var t = (-up + right * (sgn * 0.62f) + fwd * 0.05f).normalized;
            var corrW = Quaternion.FromToRotation(d, t);
            var pq = sh.parent != null ? sh.parent.rotation : Quaternion.identity;
            rest[s + "Arm"] = Quaternion.Inverse(pq) * (corrW * sh.rotation);
        }
    }

    // an offset in the original's XYZ order, with the mirror's sign flip on y and z
    static Quaternion Off(float x, float y, float z)
    {
        return Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right)
             * Quaternion.AngleAxis(-y * Mathf.Rad2Deg, Vector3.up)
             * Quaternion.AngleAxis(-z * Mathf.Rad2Deg, Vector3.forward);
    }

    void Set(string n, float x, float y, float z, float w)
    {
        if (!bones.TryGetValue(n, out var b) || !rest.TryGetValue(n, out var r)) return;
        b.localRotation = Quaternion.Slerp(b.localRotation, r * Off(x, y, z), w);
    }

    // each ankle is handed exactly the rotation that puts the sole back where the rest pose had it
    void Plant(string a, string b, string f, float w)
    {
        if (!bones.TryGetValue(f, out var foot) || !rest.TryGetValue(f, out var fr) || !chainRest.TryGetValue(f, out var want)) return;
        var now = Quaternion.identity;
        foreach (var n in new[] { "Hips", a, b }) if (bones.TryGetValue(n, out var bn)) now = now * bn.localRotation;
        var tgt = Quaternion.Inverse(now) * want * fr;
        foot.localRotation = Quaternion.Slerp(foot.localRotation, tgt, w);
    }

    /// the riding stance, eased in by w (1 = all of him)
    public void Stance(float dt, float w)
    {
        if (!ready || rider == null) return;
        handW = rider.handT; heliW = rider.heliPose; heliCrouchW = rider.heliCrouch;
        float h1 = Mathf.Clamp01(handW), h2 = Mathf.Clamp01(heliW), hc = Mathf.Clamp01(heliCrouchW);
        // the seat, eased rather than switched (web 44228-44230); the ski's share of him is sk,
        // and the surf pose is never asked for a negative share (web _base)
        float wantT = (onTube && !rider.wipe) ? 1f : 0f, wantS = (onSki && !rider.wipe) ? 1f : 0f;
        tubeW += (wantT - tubeW) * Mathf.Min(1f, dt * 5f);
        skiW += (wantS - skiW) * Mathf.Min(1f, dt * 5f);
        sitW = Mathf.Max(tubeW, skiW);
        // and under the paraglide both arms go up and out to the toggles (web 20453-20494): the
        // glide wins over the ski's bars where both are up (skA), so the shares never pass one
        float gl = Mathf.Clamp01(rider.glideFace);
        float tw = Mathf.Clamp01(tubeW), sk = Mathf.Clamp01(skiW), skA = sk * (1f - gl), baseW = Mathf.Max(0f, 1f - gl - skA);
        float carve = rider.carve;
        float air = (rider.airborne && !rider.glideOn) ? 1f : 0f;   // hanging in a harness is not a jump
        sway += (carve - sway) * Mathf.Min(1f, dt * 5.5f);
        load += (Mathf.Abs(carve) - load) * Mathf.Min(1f, dt * 4.5f);
        float T = rider.t;
        float breathe = Mathf.Sin(T * 2.05f), chatter = Mathf.Sin(T * 4.9f + 1.1f);
        bob = breathe * 0.55f + chatter * 0.20f;
        float fz = rider.dist * WaveField.Drift;
        float swell = WaveField.Height(rider.px, fz + 0.6f, T) - WaveField.Height(rider.px, fz - 0.6f, T);

        // the helicopter's crouch beat rides in like a wind-up: the web caps what it puts in the
        // knees (rigCrouch 0.92 bends them as 0.26 would) and leans the chest by 0.30 of it
        float cB = rider.poseCrouch, xB = rider.poseExt, aB = rider.poseAir * (1f - xB), lB = rider.poseLand;
        float lo = 0.30f + load * FoldCarve + Mathf.Abs(swell) * FoldSwell + bob * FoldBob + air * FoldAir
                 + crouchW * 0.58f + hc * 0.24f + tuckW * 1.10f + cB * 0.95f + aB * 0.82f + lB * 0.85f - xB * 0.30f;
        float hip = -0.20f - lo * 0.22f + tuckW * 0.42f + aB * 0.36f, knee = Mathf.Max(0f, lo * 1.05f);
        // on the jet ski he is SAT, which these bones have to be told (web 20419-20439): sitting is
        // the thigh brought forward toward horizontal, the knee open enough that the shin drops
        // into the well rather than folding back under the seat, and the splay wide enough to
        // clear the hull's shoulder instead of gripping a board that is not there
        float sHip = Mx(hip, 1.46f, sk), sKnee = Mx(knee, 1.08f, sk);
        float hipL = sHip, hipR = sHip, hipZL = 0.06f + 0.58f * sk, hipZR = -0.06f - 0.58f * sk, kneeL = sKnee, kneeR = sKnee;

        float lean = sway * 0.42f;
        // and a fold forward at the waist on the ski, a suggestion of lean rather than a bow
        float fold = cB * 0.22f + aB * 0.40f + lB * 0.20f - xB * 0.14f + load * 0.08f + hc * 0.30f;
        float s0x = 0.05f + bob * 0.02f + 0.05f * sk + fold * 0.5f, s0y = -sway * 0.10f, s0z = lean * 0.50f;
        float s1x = 0.05f + 0.04f * sk + fold * 0.3f, s1y = -sway * 0.08f, s1z = lean * 0.38f;
        float s2x = 0.03f + 0.03f * sk + fold * 0.2f, s2y = -sway * 0.06f, s2z = lean * 0.26f;
        float nkX = -0.06f, nkY = sway * 0.20f, nkZ = -lean * 0.30f;
        float hdX = -0.10f + bob * 0.03f - aB * 0.16f, hdY = sway * 0.26f, hdZ = -lean * 0.45f;

        float sw1 = Mathf.Sin(T * 1.55f), sw2 = Mathf.Sin(T * 1.85f + 2.2f), fl = Mathf.Sin(T * 2.35f + 0.7f);
        float spread = 0.34f + load * 0.30f + air * 0.55f;
        // and ON THE SKI the arms go forward, onto the bars (web 20482-20499): shoulders rotated
        // forward and only slightly out (the grips measure 0.19 half-width on the hull), elbows
        // carrying a working bend, both hands the same; the surf arms fade by his share
        float aSpread = spread * baseW + 1.30f * gl + 0.04f * skA;
        float aUp = (-0.02f - xB * 0.30f - aB * 0.12f) * baseW - 0.55f * gl - 0.95f * skA;
        float laX = aUp + (sw1 * 0.11f - sway * 0.26f) * baseW, laY = (0.05f + sw1 * 0.10f) * baseW, laZ = -aSpread - sway * 0.18f * baseW;
        float raX = aUp + (sw2 * 0.11f + sway * 0.26f) * baseW, raY = (-0.05f + sw2 * 0.10f) * baseW, raZ = aSpread - sway * 0.18f * baseW;
        float lfX = (fl * 0.18f - load * 0.14f) * baseW + 0.42f * skA, lfZ = (-0.08f - load * 0.16f) * baseW - 0.16f * skA;
        float rfX = (-fl * 0.18f - load * 0.14f) * baseW + 0.42f * skA, rfZ = (0.08f + load * 0.16f) * baseW + 0.16f * skA;
        float footA = 0f, footW = 0f;         // an ankle angle laid over the plant, for the feet that leave the deck

        if (h2 > 0.02f)
        {
            // the helicopter (web 44265-44277, 44149-44151, 44360-44364): both arms straight out
            // over the nose first, then hauling in until the elbows fold and his face is at the
            // tip; the chest goes down to 0.62 forward and square to the board; the legs trail
            // straight; chin up off the deck, eyes past the tip.
            float pull = h2;
            laX = Mx(laX, 2.12f + pull * 0.26f, h2); laY = Mx(laY, 0.15f, h2); laZ = Mx(laZ, -0.30f, h2);
            raX = Mx(raX, 2.12f + pull * 0.26f, h2); raY = Mx(raY, -0.15f, h2); raZ = Mx(raZ, 0.30f, h2);
            lfX = Mx(lfX, 0.28f + pull * 1.42f, h2); lfZ = Mx(lfZ, 0f, h2);
            rfX = Mx(rfX, 0.28f + pull * 1.42f, h2); rfZ = Mx(rfZ, 0f, h2);
            hipL = Mx(hipL, -0.30f, h2); hipR = Mx(hipR, -0.30f, h2); hipZL = Mx(hipZL, 0f, h2); hipZR = Mx(hipZR, 0f, h2);
            kneeL = Mx(kneeL, 0.34f, h2); kneeR = Mx(kneeR, 0.34f, h2);
            s0x = Mx(s0x, 0.62f * 0.5f, h2); s0y = Mx(s0y, 0f, h2); s0z = Mx(s0z, 0f, h2);
            s1x = Mx(s1x, 0.62f * 0.3f, h2); s1y = Mx(s1y, 0f, h2); s1z = Mx(s1z, 0f, h2);
            s2x = Mx(s2x, 0.62f * 0.2f, h2); s2y = Mx(s2y, 0f, h2); s2z = Mx(s2z, 0f, h2);
            nkX = Mx(nkX, -0.62f * 0.8f, h2); nkY = Mx(nkY, 0f, h2); nkZ = Mx(nkZ, 0f, h2);
            hdX = Mx(hdX, 0.66f, h2); hdY = Mx(hdY, 0f, h2); hdZ = Mx(hdZ, 0f, h2);
            footA = -0.20f; footW = h2;
        }
        if (h1 > 0.002f)
        {
            // the handstand (web HAND_POSE 44566-44573, blended by handT as applyHandPose does):
            // arms thrown overhead so that, turned over, they reach down to the deck and take his
            // weight; legs straight and a little apart; chin tucked
            laX = Mx(laX, -2.62f, h1); laY = Mx(laY, 0f, h1); laZ = Mx(laZ, -0.30f, h1);
            raX = Mx(raX, -2.62f, h1); raY = Mx(raY, 0f, h1); raZ = Mx(raZ, 0.30f, h1);
            lfX = Mx(lfX, -0.18f, h1); rfX = Mx(rfX, -0.18f, h1);
            hipL = Mx(hipL, 0.16f, h1); hipR = Mx(hipR, 0.16f, h1); hipZL = Mx(hipZL, -0.15f, h1); hipZR = Mx(hipZR, 0.15f, h1);
            kneeL = Mx(kneeL, -0.12f, h1); kneeR = Mx(kneeR, -0.12f, h1);
            hdX = Mx(hdX, -0.92f, h1);
            footA = Mx(footA, 0.34f, h1); footW = 1f - (1f - footW) * (1f - h1);
        }

        // the tube's sit is hand-posed too, so the surfing stance has to stand down for it (web
        // 20353-20354): faded by exactly the ring's share of him, so the two never fight
        float ws = w * (1f - tw);
        Set("LeftUpLeg", hipL, 0, hipZL, ws); Set("RightUpLeg", hipR, 0, hipZR, ws);
        Set("LeftLeg", kneeL, 0, 0, ws); Set("RightLeg", kneeR, 0, 0, ws);
        Set("Spine", s0x, s0y, s0z, ws);
        Set("Spine01", s1x, s1y, s1z, ws);
        Set("Spine02", s2x, s2y, s2z, ws);
        Set("neck", nkX, nkY, nkZ, ws);
        Set("Head", hdX, hdY, hdZ, ws);
        Set("LeftArm", laX, laY, laZ, ws);
        Set("RightArm", raX, raY, raZ, ws);
        Set("LeftForeArm", lfX, 0, lfZ, ws);
        Set("RightForeArm", rfX, 0, rfZ, ws);

        Plant("LeftUpLeg", "LeftLeg", "LeftFoot", ws);
        Plant("RightUpLeg", "RightLeg", "RightFoot", ws);
        if (footW > 0f) { Set("LeftFoot", footA, 0, 0, ws * footW); Set("RightFoot", footA, 0, 0, ws * footW); }

        Group(h1, h2, tw, sk);
        // the ring's sit goes on last, over the stance, in the group's axes as it now stands (web 44757)
        if (tw > 0.01f) TubePose(w * tw);
    }

    /// standing about: arms down, weight settled, a slow breath
    public void Idle(float dt, float w)
    {
        if (!ready) return;
        float T = Time.time;
        float br = Mathf.Sin(T * 1.3f) * 0.5f + 0.5f;
        Set("LeftUpLeg", -0.08f, 0, 0.04f, w); Set("RightUpLeg", -0.08f, 0, -0.04f, w);
        Set("LeftLeg", 0.18f, 0, 0, w); Set("RightLeg", 0.18f, 0, 0, w);
        Set("Spine", 0.04f + br * 0.02f, 0, 0, w); Set("Spine01", 0.03f, 0, 0, w); Set("Spine02", 0.02f, 0, 0, w);
        Set("Head", -0.06f + br * 0.02f, 0, 0, w);
        Set("LeftArm", 0.02f, 0.05f, -0.10f, w); Set("RightArm", 0.02f, -0.05f, 0.10f, w);
        Set("LeftForeArm", 0.12f, 0, -0.06f, w); Set("RightForeArm", 0.12f, 0, 0.06f, w);
        Plant("LeftUpLeg", "LeftLeg", "LeftFoot", w);
        Plant("RightUpLeg", "RightLeg", "RightFoot", w);
        // off the seat: the next run has him getting on again, as the web's ease does
        tubeW += (0f - tubeW) * Mathf.Min(1f, dt * 5f); skiW += (0f - skiW) * Mathf.Min(1f, dt * 5f); sitW = Mathf.Max(tubeW, skiW);
        Group(0f, 0f, 0f, 0f);   // back on his feet, however the last run left him
    }

    public bool Ready => ready;
}
