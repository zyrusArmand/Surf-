// A real physical thing in the water. Unity's rigid body does the falling, the spinning and the
// knocking into things; this adds the sea: at a handful of points over the object's footprint the
// wave field is sampled, and every point under the surface pushes up in proportion to how deep it
// is, damped so it settles instead of bouncing. The water itself runs past the rider (the world
// scrolls, he does not), so the body is drawn along with it at the run's speed, as everything
// floating in the lane is. Units are feet: gravity is set to 32.2 by Bootstrap.
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    public BoardController rider;           // the wave clock and the run's speed
    public float floatK = 2.6f;             // how hard a fully sunk point pushes, in body weights
    public float waterDrag = 1.8f, waterAngDrag = 1.6f;
    Rigidbody rb; Vector3[] probes; float depthRef = 0.6f;

    public void Init(BoardController r)
    {
        rider = r; rb = GetComponent<Rigidbody>();
        // the probes: the corners and the middle of the footprint, at the bottom of the body
        var b = Bounds();
        var lc = transform.InverseTransformPoint(b.center);
        var ext = transform.InverseTransformVector(b.extents);
        ext = new Vector3(Mathf.Abs(ext.x), Mathf.Abs(ext.y), Mathf.Abs(ext.z));
        float y = lc.y - ext.y * 0.6f;
        probes = new[] {
            new Vector3(lc.x, y, lc.z),
            new Vector3(lc.x + ext.x * 0.8f, y, lc.z + ext.z * 0.8f), new Vector3(lc.x - ext.x * 0.8f, y, lc.z + ext.z * 0.8f),
            new Vector3(lc.x + ext.x * 0.8f, y, lc.z - ext.z * 0.8f), new Vector3(lc.x - ext.x * 0.8f, y, lc.z - ext.z * 0.8f) };
        depthRef = Mathf.Max(0.25f, b.size.y * 0.5f);
    }

    Bounds Bounds()
    {
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(transform.position, Vector3.one);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    void FixedUpdate()
    {
        if (rb == null || probes == null || rider == null) return;
        float t = rider.t, fz0 = WaveField.Scroll;
        float each = rb.mass * Physics.gravity.magnitude * floatK / probes.Length;
        int under = 0;
        foreach (var lp in probes)
        {
            var p = transform.TransformPoint(lp);
            float sea = WaveField.Height(p.x, fz0 + p.z, t);
            float d = sea - p.y;
            if (d <= 0f) continue;
            under++;
            float k = Mathf.Min(1f, d / depthRef);
            rb.AddForceAtPosition(Vector3.up * each * k, p, ForceMode.Force);
        }
        if (under > 0)
        {
            // the water's own motion: it runs toward the camera at the run's speed
            float w = under / (float)probes.Length;
            var flow = new Vector3(0f, 0f, rider.running ? rider.eff : 0f);
            var rel = flow - rb.linearVelocity; rel.y *= 0.5f;
            rb.AddForce(rel * waterDrag * w * rb.mass, ForceMode.Force);
            rb.AddTorque(-rb.angularVelocity * waterAngDrag * w * rb.mass, ForceMode.Force);
        }
    }

    /// makes a thing physical: a rigid body with a box round what it draws, set moving
    public static Buoyancy Make(GameObject go, BoardController r, Vector3 vel, Vector3 spin, float mass = 1f)
    {
        if (go.GetComponent<Collider>() == null)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var box = go.AddComponent<BoxCollider>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var rr in rs) b.Encapsulate(rr.bounds);
                box.center = go.transform.InverseTransformPoint(b.center);
                var sz = go.transform.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Max(0.1f, Mathf.Abs(sz.x)), Mathf.Max(0.1f, Mathf.Abs(sz.y)), Mathf.Max(0.1f, Mathf.Abs(sz.z)));
            }
        }
        var rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = mass; rb.linearDamping = 0.05f; rb.angularDamping = 0.2f; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic = false; rb.useGravity = true;
        rb.linearVelocity = vel; rb.angularVelocity = spin;
        var bu = go.GetComponent<Buoyancy>(); if (bu == null) bu = go.AddComponent<Buoyancy>();
        bu.Init(r);
        return bu;
    }
}
