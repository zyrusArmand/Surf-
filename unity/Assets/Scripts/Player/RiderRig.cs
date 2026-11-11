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

    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    readonly Dictionary<string, Quaternion> rest = new Dictionary<string, Quaternion>();
    readonly Dictionary<string, Quaternion> chainRest = new Dictionary<string, Quaternion>();
    float sway, load, bob;
    bool ready;

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
        ready = true;
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
        float carve = rider.carve;
        float air = rider.airborne ? 1f : 0f;
        sway += (carve - sway) * Mathf.Min(1f, dt * 5.5f);
        load += (Mathf.Abs(carve) - load) * Mathf.Min(1f, dt * 4.5f);
        float T = rider.t;
        float breathe = Mathf.Sin(T * 2.05f), chatter = Mathf.Sin(T * 4.9f + 1.1f);
        bob = breathe * 0.55f + chatter * 0.20f;
        float fz = rider.dist * WaveField.Drift;
        float swell = WaveField.Height(rider.px, fz + 0.6f, T) - WaveField.Height(rider.px, fz - 0.6f, T);

        float cB = rider.poseCrouch, xB = rider.poseExt, aB = rider.poseAir * (1f - xB), lB = rider.poseLand;
        float lo = 0.30f + load * FoldCarve + Mathf.Abs(swell) * FoldSwell + bob * FoldBob + air * FoldAir
                 + crouchW * 0.58f + tuckW * 1.10f + cB * 0.95f + aB * 0.82f + lB * 0.85f - xB * 0.30f;
        float hip = -0.20f - lo * 0.22f + tuckW * 0.42f + aB * 0.36f, knee = Mathf.Max(0f, lo * 1.05f);
        Set("LeftUpLeg", hip, 0, 0.06f, w); Set("RightUpLeg", hip, 0, -0.06f, w);
        Set("LeftLeg", knee, 0, 0, w); Set("RightLeg", knee, 0, 0, w);

        float lean = sway * 0.42f;
        float fold = cB * 0.22f + aB * 0.40f + lB * 0.20f - xB * 0.14f + load * 0.08f;
        Set("Spine", 0.05f + bob * 0.02f + fold * 0.5f, -sway * 0.10f, lean * 0.50f, w);
        Set("Spine01", 0.05f + fold * 0.3f, -sway * 0.08f, lean * 0.38f, w);
        Set("Spine02", 0.03f + fold * 0.2f, -sway * 0.06f, lean * 0.26f, w);
        Set("neck", -0.06f, sway * 0.20f, -lean * 0.30f, w);
        Set("Head", -0.10f + bob * 0.03f - aB * 0.16f, sway * 0.26f, -lean * 0.45f, w);

        float sw1 = Mathf.Sin(T * 1.55f), sw2 = Mathf.Sin(T * 1.85f + 2.2f), fl = Mathf.Sin(T * 2.35f + 0.7f);
        float spread = 0.34f + load * 0.30f + air * 0.55f;
        float aUp = -0.02f - xB * 0.30f - aB * 0.12f;
        Set("LeftArm", aUp + sw1 * 0.11f - sway * 0.26f, 0.05f + sw1 * 0.10f, -spread - sway * 0.18f, w);
        Set("RightArm", aUp + sw2 * 0.11f + sway * 0.26f, -0.05f + sw2 * 0.10f, spread - sway * 0.18f, w);
        Set("LeftForeArm", fl * 0.18f - load * 0.14f, 0, -0.08f - load * 0.16f, w);
        Set("RightForeArm", -fl * 0.18f - load * 0.14f, 0, 0.08f + load * 0.16f, w);

        Plant("LeftUpLeg", "LeftLeg", "LeftFoot", w);
        Plant("RightUpLeg", "RightLeg", "RightFoot", w);
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
    }

    public bool Ready => ready;
}
