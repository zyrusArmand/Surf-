// The other surfers, drawn in your ocean (Multi): each one's own rider on their own board, set where
// they are against you -- ahead or behind by the difference in distance, at their own place across
// the lane -- riding the sea's height there, leaning into their carve, spinning off a ramp, and gone
// from the water while they are down. A name tag over each, in their colour, with hearts in a brawl.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CrewView : MonoBehaviour
{
    class Look { public GameObject root; public Transform body; public Text tag; public bool shown = true; }
    readonly List<Look> looks = new List<Look>();
    BoardController rider; Camera cam; Hud hud; Canvas canvas;

    public void Build(List<Surfer> crew, BoardController r, Camera c, Hud h)
    {
        Clear();
        rider = r; cam = c; hud = h; canvas = hud != null ? hud.UiRoot.GetComponent<Canvas>() : null;
        foreach (var s in crew)
        {
            var look = new Look { root = new GameObject("crew:" + s.name) };
            var board = BoardMesh.Build(s.boardId); board.transform.SetParent(look.root.transform, false);
            // the rider as the local one is set on his board: a file when there is one, else built
            bool proc = Resources.Load<GameObject>("Models/" + s.riderId) == null && ProcRider.Has(s.riderId);
            var body = proc ? ProcRider.Build(s.riderId, look.root.transform) : Models.Spawn(s.riderId, look.root.transform, 1.9f, PrimitiveType.Capsule);
            if (body != null)
            {
                body.transform.localPosition = new Vector3(0f, 0.19f, 0.2f); body.transform.localRotation = Quaternion.Euler(0f, -70f, 0f);
                foreach (var a in body.GetComponentsInChildren<Animation>(true)) { a.Stop(); a.enabled = false; }
                foreach (var a in body.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                look.body = body.transform;
            }
            if (canvas != null)
            {
                look.tag = UiKit.Label(canvas.transform, s.name, 15, s.tint, FontStyle.Bold, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(160, 22), TextAnchor.MiddleCenter, 0.8f);
                look.tag.raycastTarget = false;
            }
            looks.Add(look);
        }
    }

    public void Clear()
    {
        foreach (var l in looks) { if (l.root != null) Destroy(l.root); if (l.tag != null) Destroy(l.tag.gameObject); }
        looks.Clear();
    }

    public void Step(List<Surfer> crew, MpMode mode, float dt)
    {
        if (rider == null || cam == null) return;
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        for (int i = 0; i < looks.Count && i < crew.Count; i++)
        {
            var s = crew[i]; var l = looks[i];
            float z = -(s.dist - rider.dist);
            bool show = !s.down && !s.outOfGame && z < 25f && z > -260f;
            if (show != l.shown) { l.root.SetActive(show); l.shown = show; }
            if (l.tag != null) l.tag.gameObject.SetActive(show && z < 6f);
            if (!show) continue;
            // on the sea where they are, as the local board floats on it
            float sea = WaveField.Height(s.px, WaveField.Scroll + z, rider.t);
            l.root.transform.position = new Vector3(s.px, sea + 0.06f + s.py, z);
            float yaw = -Mathf.Atan2(s.vx, Mathf.Max(8f, s.eff)) * Mathf.Rad2Deg;
            l.root.transform.rotation = Quaternion.Euler(0f, yaw - s.spin, -s.carve * 8f);
            // a lunge at someone: a quick shove of the whole surfer sideways and back
            if (l.body != null) l.body.localPosition = new Vector3(s.lunge * 0.35f * Mathf.Sign(s.vx == 0 ? 1 : s.vx), 0.19f, 0.2f);
            if (l.tag != null)
            {
                var sp = cam.WorldToScreenPoint(l.root.transform.position + Vector3.up * 3.0f);
                if (sp.z <= 0f) { l.tag.gameObject.SetActive(false); continue; }
                ((RectTransform)l.tag.transform).anchoredPosition = new Vector2(sp.x, sp.y) / scale;
                // in a brawl: hearts, and a "!" in red while it winds up a swing, so it can be dodged
                l.tag.text = mode == MpMode.Brawl ? (s.windup > 0f ? "! " : "") + s.name + " " + new string('♥', Mathf.Max(0, s.hearts)) : s.name;
                l.tag.color = mode == MpMode.Brawl && s.windup > 0f ? new Color(1f, 0.35f, 0.25f) : s.tint;
            }
        }
    }
}
