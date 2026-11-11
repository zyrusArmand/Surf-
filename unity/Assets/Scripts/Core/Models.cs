// Models by name. The .glb files under Assets/Resources/Models are imported by glTFast into
// prefabs, so Resources.Load hands back the finished object. Anything missing falls back to a
// primitive of about the right size, so the game always runs -- the same rule the web version
// has: a file that is not there keeps the built-in shape.
using UnityEngine;

public static class Models
{
    public static GameObject Spawn(string name, Transform parent, float targetHeight, PrimitiveType fallback = PrimitiveType.Capsule)
    {
        var prefab = Resources.Load<GameObject>("Models/" + name);
        GameObject go;
        if (prefab != null)
        {
            go = Object.Instantiate(prefab, parent);
            go.name = name;
            Fit(go, targetHeight);
        }
        else
        {
            go = GameObject.CreatePrimitive(fallback);
            go.name = name + " (stand-in)";
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * targetHeight;
            var col = go.GetComponent<Collider>(); if (col) Object.Destroy(col);
        }
        return go;
    }

    /// scale so the object stands targetHeight tall, and sit it on its own feet at y=0
    public static void Fit(GameObject go, float targetHeight)
    {
        var b = Bounds(go);
        if (b.size.y < 1e-4f) return;
        float s = targetHeight / b.size.y;
        go.transform.localScale = go.transform.localScale * s;
        b = Bounds(go);
        go.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z) - go.transform.localPosition;
    }

    /// scale so the object's longest side is targetLength (for boards and logs)
    public static void FitLength(GameObject go, float targetLength)
    {
        var b = Bounds(go);
        float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (longest < 1e-4f) return;
        go.transform.localScale = go.transform.localScale * (targetLength / longest);
        // lay the long side along z, which is the way everything here travels
        b = Bounds(go);
        if (b.size.x > b.size.z) go.transform.localRotation *= Quaternion.Euler(0, 90, 0);
        else if (b.size.y > b.size.z) go.transform.localRotation *= Quaternion.Euler(90, 0, 0);
        b = Bounds(go);
        go.transform.localPosition -= new Vector3(b.center.x, b.center.y, b.center.z) - go.transform.localPosition;
    }

    public static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        // into the parent's local frame: the fits above work on local position and scale
        var p = go.transform.parent;
        if (p != null)
        {
            var c = p.InverseTransformPoint(b.center);
            var s = new Vector3(b.size.x / Mathf.Max(1e-4f, p.lossyScale.x), b.size.y / Mathf.Max(1e-4f, p.lossyScale.y), b.size.z / Mathf.Max(1e-4f, p.lossyScale.z));
            return new Bounds(c, s);
        }
        return b;
    }
}
