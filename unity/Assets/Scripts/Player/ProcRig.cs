// Poses a ProcRider body each frame the way the web poses its built-in rig (animateRig, web
// 44118-44616, and the handstand, 44622-44795): knees loaded against the carve and the swell,
// the chest out over the front foot, the lean living in the spine, the arms thrown fore and
// aft along the board and windmilling in the air, the tuck while a rotation runs, the pop's
// crouch and extension, the landing squash, the helicopter's lie-down along the deck and the
// handstand's turn onto the front paws. The joints are eased toward their targets at the
// web's rates and the limb tubes are re-swept from wherever the joints ended up.
//
// Same entry points as RiderRig, so Bootstrap can call either: Init(rider), Stance(dt, w),
// Idle(dt, w), Ready, and the crouchW / tuckW hooks.
using System.Collections.Generic;
using UnityEngine;

public class ProcRig : MonoBehaviour
{
    public BoardController rider;
    public float crouchW, tuckW;            // a wind-up (web launchLoad), and the feet-up while the board flips (web kfTuck)
    public float handW, heliW, heliCrouchW; // the tricks' blends, read off the rider every Stance
    public bool Ready => ready;

    ProcRider.Built b; bool ready;
    Vector3 homePos; Quaternion homeRot;    // where the ride parks him, which the tricks turn him away from
    float handLift;                         // web HAND_LIFT
    float legDrop = 0.34f;

    // the eased state, radians, the web's joint rotations
    float rigCrouch, rigTuck;
    Vector3 spineR, chestR, neckR, headR;
    Vector3 hipF, kneeF, ankleF, hipB, kneeB, ankleB;   // x bend, y yaw, z splay
    Vector3 shF, shB; float elF, elB;

    const float PelvisY = 0.475f, StanceRot = -1.20f;
    const float RideCrouch = 0.30f, RideLean = 0.22f, RideHip = 0.04f;
    const float LegLen = 0.25f + 0.20f + 0.08f;
    const float StanceF = -0.45f, StanceB = 0.85f, HipF = -0.10f, HipB = 0.50f;
    static readonly float SplayF = Mathf.Asin(Mathf.Clamp((StanceF - HipF) / LegLen, -0.9f, 0.9f));
    static readonly float SplayB = Mathf.Asin(Mathf.Clamp((StanceB - HipB) / LegLen, -0.9f, 0.9f));
    const float FoldCarve = 0.12f, FoldSwell = 0.22f;   // web LEG_FOLD

    static float Ease(float cur, float tgt, float dt, float k) { return cur + (tgt - cur) * Mathf.Min(1f, dt * k); }
    static float Mx(float a, float c, float h) { return a * (1f - h) + c * h; }

    public void Init(BoardController r)
    {
        rider = r;
        b = ProcRider.Of(gameObject);
        if (b == null) { ready = false; return; }
        homePos = transform.localPosition; homeRot = transform.localRotation;
        // settle the eased state on the stance so the first frame is not a snap from zero
        Pose(1f, 1f, false); Pose(1f, 1f, false);
        handLift = MeasureHandLift();
        ready = true;
    }

    void OnDestroy() { ProcRider.Forget(gameObject); }

    float PelvisHeight() { return PelvisY + legDrop * (1f - 1f / b.legS); }

    /// the riding stance, eased in by w (1 = all of him)
    public void Stance(float dt, float w)
    {
        if (!ready || rider == null) return;
        Pose(dt, w, true);
    }

    /// standing about on the board: the trim stance with nothing happening, the ears idling
    public void Idle(float dt, float w)
    {
        if (!ready) return;
        Pose(dt, w, false);
    }

    void Pose(float dt, float w, bool live)
    {
        float T = Time.time;
        float carve = live ? rider.carve : 0f;
        bool air = live && rider.airborne && !rider.glideOn;   // hanging in a harness is not a jump
        float gf = live ? Mathf.Clamp01(rider.glideFace) : 0f; // square to the nose under the canopy (web glFace)
        float cB = live ? rider.poseCrouch : 0f, xB = live ? rider.poseExt : 0f, aB = live ? rider.poseAir : 0f, lB = live ? rider.poseLand : 0f;
        handW = live ? rider.handT : 0f; heliW = live ? rider.heliPose : 0f; heliCrouchW = live ? rider.heliCrouch : 0f;
        float h1 = Mathf.Clamp01(handW), h2 = Mathf.Clamp01(heliW), hc = Mathf.Clamp01(heliCrouchW);
        float g = Mathf.Abs(carve);
        float heave = 0f;
        if (live)
        {
            float fz = rider.dist * WaveField.Drift;
            heave = WaveField.Height(rider.px, fz + 0.6f, rider.t) - WaveField.Height(rider.px, fz - 0.6f, rider.t);
        }
        float chop = Mathf.Sin(T * 4.2f) * 0.35f + Mathf.Sin(T * 7.7f) * 0.18f;

        // ---- the tuck while a rotation runs, the crouch under everything (web 44137-44153) ----
        // The tuck is for a QUEUED turn, the committed whole revolution a tap throws, and never
        // while he has hold of the rail; a held button turns him with his arms out.
        bool grab = live && rider.grabOn && rider.airborne && !rider.wipe;            // web grab (45852)
        bool dragging = live && rider.dragOn && !rider.airborne && !rider.wipe;       // web dragging (44137)
        bool rotating = air && rider.spinQ + rider.flipQ + rider.pitchQ > 0.15f && !grab;
        bool tuckOn = rotating || tuckW > 0.25f;
        rigTuck = Ease(rigTuck, tuckOn ? 1f : 0f, dt, tuckOn ? 13f : 7f);
        float compress = (air ? (grab ? 1.05f : -0.16f + rigTuck * 1.30f + aB * 0.60f * (1f - xB) - xB * 0.45f)
                              : (g * 0.42f * (FoldCarve / 0.30f) + Mathf.Abs(heave) * FoldSwell + chop * 0.05f + (dragging ? 0.55f : 0f) + lB * 0.95f))
                       + cB * 0.95f + hc * 0.92f;   // the helicopter's crouch beat sets rigCrouch to 0.92 in the web
        rigCrouch = Ease(rigCrouch, compress - (live ? rider.releaseKick : 0f) * 0.55f, dt, 9f);   // pops upright out of a grab

        // ---- the pelvis: down by what the knees take out of the legs (web 44164-44168) ----
        float pelvisY = PelvisHeight() - RideCrouch * RideHip - Mathf.Max(0f, Mathf.Min(0.24f, rigCrouch)) * 0.14f - crouchW * 0.11f;
        var pelvisR = new Vector3(heave * 0.22f, 0f, carve * 0.10f);

        // ---- hip / knee / ankle: a deeper bend the more he compresses (web 44175-44189) ----
        float cr = Mathf.Max(-0.08f, Mathf.Min(0.26f + rigTuck * 0.72f + crouchW * 0.42f, rigCrouch + crouchW * 0.46f));
        float bendF = 0.30f + RideCrouch + cr * 0.55f, bendB = 0.32f + RideCrouch * 1.06f + cr * 0.58f;
        hipF.x = Ease(hipF.x, -SplayF - bendF * 0.5f + heave * 0.10f, dt, 10f);
        kneeF.x = Ease(kneeF.x, SplayF * 0.55f + bendF, dt, 12f);
        ankleF.x = Ease(ankleF.x, SplayF * 0.45f - bendF * 0.9f, dt, 10f);   // paw stays flat on the deck
        hipF.y = -StanceRot; hipF.z = carve * 0.10f;
        hipB.x = Ease(hipB.x, SplayB + bendB * 0.5f - heave * 0.10f, dt, 10f);
        kneeB.x = Ease(kneeB.x, -SplayB * 0.55f - bendB, dt, 12f);
        ankleB.x = Ease(ankleB.x, -SplayB * 0.45f + bendB * 0.9f, dt, 10f);
        hipB.y = -StanceRot; hipB.z = carve * 0.08f;

        // ---- spine, chest, neck, head (web 44202-44219) ----
        // stays stacked over the rail he is on; over the one he is holding, or the paw in the water
        float gSide = grab ? rider.grabSide : 0f, dragLean = dragging ? rider.dragSide * 0.42f : 0f;
        spineR.z = Ease(spineR.z, carve * 0.16f + gSide * 0.58f + dragLean, dt, 8f);
        float crouchLean = grab ? 0.62f : rigCrouch * 0.30f;
        spineR.x = Ease(spineR.x,
            (0.18f + RideLean + crouchLean + (air ? -0.32f : 0f) + rigTuck * 0.85f
             + aB * 0.34f * (1f - xB) - xB * 0.18f + lB * 0.22f + cB * 0.20f) * (1f - h2) + 0.62f * h2, dt, 8f);
        chestR.y = Ease(chestR.y, 0.20f + carve * 0.55f, dt, 7f);   // shoulders swing with the turn
        chestR.z = Ease(chestR.z, carve * 0.14f, dt, 7f);
        neckR.x = Ease(neckR.x, -spineR.x * 0.8f + (air ? 0.12f : 0f) + rigTuck * 0.46f, dt, 9f);
        // head down the line, a few degrees back toward the lens; riding switch he looks over the
        // other shoulder (web 44212)
        headR.y = Ease(headR.y, (live && rider.switchRide ? -0.69f : -1.05f) + carve * 0.34f, dt, 6f);
        headR.z = Ease(headR.z, -carve * 0.10f, dt, 6f);

        // ---- ears flap with speed and motion (web 44221) ----
        float flap = Mathf.Sin(T * 11f) * (0.18f + g * 0.35f);
        foreach (var e in b.ears)
            e.t.localRotation = ProcRider.RotXYZ(e.baseEuler.x + flap * (e.side > 0 ? 1f : -0.85f), e.baseEuler.y, e.baseEuler.z);

        // ---- arms (web 44236-44383): windmill in the air, wrapped tight in a tuck, planted on the wave ----
        Vector3 tF, tB; float teF, teB, spread;
        if (air)
        {
            float wv = T * 8f, k = rigTuck;
            tF = new Vector3((0.55f + Mathf.Sin(wv) * 0.35f) * (1f - k) + 1.28f * k, 0.20f * (1f - k) + 0.46f * k, (-1.50f - Mathf.Sin(wv) * 0.25f) * (1f - k) - 0.30f * k);
            teF = 0.35f * (1f - k) + 2.05f * k;
            tB = new Vector3((-0.35f + Mathf.Sin(wv + 2.1f) * 0.35f) * (1f - k) + 1.18f * k, -0.20f * (1f - k) - 0.46f * k, (1.50f - Mathf.Sin(wv + 2.1f) * 0.25f) * (1f - k) + 0.30f * k);
            teB = 0.30f * (1f - k) + 2.05f * k;
            spread = 0f;
        }
        else
        {
            // leading paw down the line, trailing paw back and out; swung along the BOARD (x), not the shoulder line
            float drag = Mathf.Max(0f, carve), drag2 = Mathf.Max(0f, -carve);
            tF = new Vector3(1.14f + drag * 0.30f, 0.12f, -0.06f - drag * 0.14f);
            teF = 0.20f - drag * 0.12f;
            tB = new Vector3(-1.30f - drag2 * 0.30f, -0.12f, 0.10f + drag2 * 0.14f);
            teB = 0.18f - drag2 * 0.12f;
            spread = Mathf.Sin(T * 2.4f) * 0.16f + heave * 0.55f;   // arms sway with the swell and the ride
            if (dragging)
            {
                // crouched low with one paw carving through the water beside the board (web 44275-44281)
                float ds = rider.dragSide;
                tF = new Vector3(1.15f, ds * 0.25f, 1.05f * ds); teF = 0.55f;   // reaching down and out to the surface
                tB = new Vector3(-0.35f, -0.15f, -1.45f); teB = 0.30f;          // the other paw up for balance
                spread = 0f;
            }
            else if (live && rider.waveT > 0f)
            {
                // just come round to face the lens after a half turn: a hand up and a wave for three
                // seconds, eased up into it and back down out of it (web 44331-44343)
                float el = BoardController.WaveDur - rider.waveT;
                float k = Mathf.Min(1f, el / 0.26f) * Mathf.Min(1f, rider.waveT / 0.36f), wv = Mathf.Sin(el * 8.6f);
                tF = new Vector3(Mx(tF.x, 0.06f, k), Mx(tF.y, 0.10f, k), Mx(tF.z, -2.52f - wv * 0.46f, k));
                teF = Mx(teF, 0.52f + wv * 0.32f, k);
                spread *= 1f - k;
            }
        }
        if (live && rider.glideOn)
        {
            // both arms out to the TOGGLES (web 44244-44260): up and out at about shoulder height,
            // elbows nearly straight, just under a right angle so the reach is at its widest
            tF = new Vector3(0.06f, 0.34f, -1.88f); teF = 0.04f;
            tB = new Vector3(0.06f, -0.34f, 1.88f); teB = 0.04f;
            spread = 0f;
        }
        if (grab)
        {
            // fold right over and take the rail underneath the board: the arm hangs down and out to
            // it, small angles so the paw lands on the edge, the free paw thrown high behind (web 44237-44243)
            float gs = rider.grabSide;
            tF = new Vector3(0.22f, gs * 0.18f, gs * 0.52f); teF = 0.30f;
            tB = new Vector3(-0.55f, -0.25f, -1.85f); teB = -0.50f;
            spread = 0f;
        }
        if (h2 > 0.02f)
        {
            // both arms straight out over the nose first, then hauling in until the elbows fold
            float pull = h2;
            tF = new Vector3(Mx(tF.x, 2.12f + pull * 0.26f, h2), Mx(tF.y, 0.15f, h2), Mx(tF.z, -0.30f, h2));
            tB = new Vector3(Mx(tB.x, 2.12f + pull * 0.26f, h2), Mx(tB.y, -0.15f, h2), Mx(tB.z, 0.30f, h2));
            teF = Mx(teF, 0.28f + pull * 1.42f, h2); teB = Mx(teB, 0.28f + pull * 1.42f, h2);
            spread *= 1f - h2;
        }
        shF.x = Ease(shF.x, tF.x + spread, dt, 10f);
        shF.y = Ease(shF.y, tF.y + Mathf.Sin(T * 1.9f) * 0.10f, dt, 10f);
        shF.z = Ease(shF.z, tF.z, dt, 10f);
        elF = Ease(elF, teF + Mathf.Sin(T * 2.6f) * 0.09f, dt, 10f);
        shB.x = Ease(shB.x, tB.x - spread, dt, 10f);
        shB.y = Ease(shB.y, tB.y - Mathf.Sin(T * 1.9f + 0.8f) * 0.10f, dt, 10f);
        shB.z = Ease(shB.z, tB.z, dt, 10f);
        elB = Ease(elB, teB + Mathf.Sin(T * 2.6f + 1.4f) * 0.09f, dt, 10f);

        // ---- what is written: the eased state, with the two whole-body tricks laid over it ----
        Vector3 oHipF = hipF, oKneeF = kneeF, oAnkleF = ankleF, oHipB = hipB, oKneeB = kneeB, oAnkleB = ankleB;
        Vector3 oShF = shF, oShB = shB, oHead = headR; float oElF = elF, oElB = elB;
        if (h2 > 0.02f)
        {
            // the helicopter's lie-down (web 44387-44420): down on the deck, legs trailing straight, chin up off it
            pelvisY += 0.13f * h2;
            oHipF.x = Mx(oHipF.x, -0.30f, h2); oKneeF.x = Mx(oKneeF.x, 0.34f, h2); oAnkleF.x = Mx(oAnkleF.x, -0.20f, h2);
            oHipB.x = Mx(oHipB.x, -0.30f, h2); oKneeB.x = Mx(oKneeB.x, 0.34f, h2); oAnkleB.x = Mx(oAnkleB.x, -0.20f, h2);
            oHead.x = Mx(oHead.x, 0.66f, h2); oHead.y *= 1f - h2;
        }
        if (h1 > 0.002f)
        {
            // the handstand (web HAND_POSE 44622): arms thrown overhead so that, turned over, they reach
            // down to the deck; legs straight and a little apart; chin tucked
            oShF = new Vector3(Mx(oShF.x, -2.62f, h1), Mx(oShF.y, 0f, h1), Mx(oShF.z, -0.30f, h1)); oElF = Mx(oElF, -0.18f, h1);
            oShB = new Vector3(Mx(oShB.x, -2.62f, h1), Mx(oShB.y, 0f, h1), Mx(oShB.z, 0.30f, h1)); oElB = Mx(oElB, -0.18f, h1);
            oHipF = new Vector3(Mx(oHipF.x, 0.16f, h1), Mx(oHipF.y, 0f, h1), Mx(oHipF.z, -0.15f, h1));
            oKneeF.x = Mx(oKneeF.x, -0.12f, h1); oAnkleF.x = Mx(oAnkleF.x, 0.34f, h1);
            oHipB = new Vector3(Mx(oHipB.x, -0.16f, h1), Mx(oHipB.y, 0f, h1), Mx(oHipB.z, 0.15f, h1));
            oKneeB.x = Mx(oKneeB.x, -0.12f, h1); oAnkleB.x = Mx(oAnkleB.x, 0.34f, h1);
            oHead.x = Mx(oHead.x, -0.92f, h1);
        }
        if (gf > 0.02f && h2 <= 0.02f)
        {
            // under the canopy, hanging: the legs come forward and down rather than braced on a
            // deck he is no longer standing on, and the head looks where he faces (web 44520-44529)
            oHipF.x = Mx(oHipF.x, 0.34f, gf); oKneeF.x = Mx(oKneeF.x, 0.30f, gf); oAnkleF.x = Mx(oAnkleF.x, -0.12f, gf);
            oHipB.x = Mx(oHipB.x, 0.34f, gf); oKneeB.x = Mx(oKneeB.x, 0.30f, gf); oAnkleB.x = Mx(oAnkleB.x, -0.12f, gf);
            oHead.y *= 1f - gf;
        }

        b.pelvis.localPosition = Vector3.Lerp(b.pelvis.localPosition, new Vector3(0f, pelvisY, 0.02f), w);
        Set(b.pelvis, ProcRider.RotXYZ(pelvisR.x, pelvisR.y, pelvisR.z), w);
        Set(b.spine, ProcRider.RotXYZ(spineR.x, spineR.y, spineR.z), w);
        Set(b.chest, ProcRider.RotXYZ(chestR.x, chestR.y, chestR.z), w);
        Set(b.neck, ProcRider.RotXYZ(neckR.x, neckR.y, neckR.z), w);
        Set(b.head, ProcRider.RotXYZ(oHead.x, oHead.y, oHead.z), w);
        Set(b.legF.hip, ProcRider.RotYXZ(oHipF.x, oHipF.y, oHipF.z), w);
        Set(b.legF.knee, ProcRider.RotYXZ(oKneeF.x, 0f, 0f), w);
        Set(b.legF.ankle, ProcRider.RotYXZ(oAnkleF.x, 0f, 0f), w);
        Set(b.legB.hip, ProcRider.RotYXZ(oHipB.x, oHipB.y, oHipB.z), w);
        Set(b.legB.knee, ProcRider.RotYXZ(oKneeB.x, 0f, 0f), w);
        Set(b.legB.ankle, ProcRider.RotYXZ(oAnkleB.x, 0f, 0f), w);
        Set(b.armF.sh, ProcRider.RotXYZ(oShF.x, oShF.y, oShF.z), w);
        Set(b.armF.el, ProcRider.RotXYZ(oElF, 0f, 0f), w);
        Set(b.armB.sh, ProcRider.RotXYZ(oShB.x, oShB.y, oShB.z), w);
        Set(b.armB.el, ProcRider.RotXYZ(oElB, 0f, 0f), w);

        Group(h1, h2, gf, live && rider.switchRide);
        legDrop = ProcRider.MeasureLegDrop(b);   // for next frame's pelvis height, as updateLimbs measures it
        b.Sweep();                                // the visible limb is swept from wherever the joints ended up
    }

    static void Set(Transform t, Quaternion q, float w) { t.localRotation = w >= 1f ? q : Quaternion.Slerp(t.localRotation, q, w); }

    // The rider group itself (web 44394-44415, 44792-44793). The handstand turns him over about
    // the deck line and lifts him onto his paws; the helicopter turns him to face the nose, lays
    // him flat along the deck and slides him up it. Both pivot at his feet.
    void Group(float h1, float h2, float gf = 0f, bool sw = false)
    {
        float xr = h1 * Mathf.PI - 1.36f * h2;
        // gf: under the canopy he faces the way he is going, the same eased turn (web 44523)
        var yaw = Quaternion.Slerp(homeRot, Quaternion.identity, Mathf.Clamp01(h2 + gf));
        // riding switch he is turned round on the board, half a turn on top of all of it (web applyStance 23359)
        if (sw) yaw = Quaternion.AngleAxis(180f, Vector3.up) * yaw;
        transform.localRotation = Quaternion.AngleAxis(xr * Mathf.Rad2Deg, Vector3.right) * yaw;
        transform.localPosition = homePos + new Vector3(0f, h1 * handLift, -0.92f * h2);
    }

    // web measureHandLift (44660-44680): where his feet rest when upright, plus how far the raised
    // paws reach above the deck in the pose, plus a little daylight. Turning him over sends that
    // reach the other way, so lifting by the two together puts his paws exactly where his feet were.
    float MeasureHandLift()
    {
        var par = transform.parent;
        System.Func<Vector3, float> up = wp => (par != null ? par.InverseTransformPoint(wp) : wp).y - transform.localPosition.y;
        var rs = GetComponentsInChildren<Renderer>(true);
        b.Sweep();
        float stand = float.PositiveInfinity;
        foreach (var r in rs) { var bd = r.bounds; stand = Mathf.Min(stand, up(new Vector3(bd.center.x, bd.min.y, bd.center.z))); }
        if (float.IsInfinity(stand)) stand = 0f;
        var joints = new[] { b.armF.sh, b.armF.el, b.armB.sh, b.armB.el, b.legF.hip, b.legF.knee, b.legF.ankle, b.legB.hip, b.legB.knee, b.legB.ankle, b.head };
        var keep = new List<Quaternion>();
        foreach (var j in joints) keep.Add(j.localRotation);
        b.armF.sh.localRotation = ProcRider.RotXYZ(-2.62f, 0f, -0.30f); b.armF.el.localRotation = ProcRider.RotXYZ(-0.18f, 0f, 0f);
        b.armB.sh.localRotation = ProcRider.RotXYZ(-2.62f, 0f, 0.30f); b.armB.el.localRotation = ProcRider.RotXYZ(-0.18f, 0f, 0f);
        b.legF.hip.localRotation = ProcRider.RotYXZ(0.16f, 0f, -0.15f); b.legF.knee.localRotation = ProcRider.RotYXZ(-0.12f, 0f, 0f); b.legF.ankle.localRotation = ProcRider.RotYXZ(0.34f, 0f, 0f);
        b.legB.hip.localRotation = ProcRider.RotYXZ(-0.16f, 0f, 0.15f); b.legB.knee.localRotation = ProcRider.RotYXZ(-0.12f, 0f, 0f); b.legB.ankle.localRotation = ProcRider.RotYXZ(0.34f, 0f, 0f);
        b.head.localRotation = ProcRider.RotXYZ(-0.92f, 0f, 0f);
        b.Sweep();
        float reach = float.NegativeInfinity;
        foreach (var r in rs) { var bd = r.bounds; reach = Mathf.Max(reach, up(new Vector3(bd.center.x, bd.max.y, bd.center.z))); }
        for (int i = 0; i < joints.Length; i++) joints[i].localRotation = keep[i];
        b.Sweep();
        if (float.IsInfinity(reach)) return 0f;
        return stand + reach + 0.10f;
    }
}
