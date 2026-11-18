// The other surfers, drawn in your ocean (Multi): each one's own rider on their own board, set where
// they are against you -- ahead or behind by the difference in distance, at their own place across
// the lane -- riding the sea's height there, leaning into their carve, spinning off a ramp, and gone
// from the water while they are down. A name tag over each, in their colour, with hearts in a brawl.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CrewView : MonoBehaviour
{
    class Look { public GameObject root; public Transform body; public Text tag, hearts; public bool shown = true; public RiderRig rig; public ProcRig prig; }
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
                // posed by the same rigs as yours: stood on the board, and able to throw a punch or a kick
                if (proc) { look.prig = body.AddComponent<ProcRig>(); look.prig.Init(null); if (!look.prig.Ready) look.prig = null; }
                else { look.rig = body.AddComponent<RiderRig>(); look.rig.Init(null); if (!look.rig.Ready) look.rig = null; }
                // in their own clothes (the riders built in code ride bare, as yours does)
                if (!proc && s.outfit != null)
                    try { Gear.Dress(body.transform, s.outfit, true); } catch (System.Exception ex) { Debug.LogWarning("CrewView: could not dress " + s.name + ": " + ex.Message); }
            }
            if (canvas != null)
            {
                look.tag = UiKit.Label(canvas.transform, s.name, 15, s.tint, FontStyle.Bold, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(160, 22), TextAnchor.MiddleCenter, 0.8f);
                look.tag.raycastTarget = false;
                look.hearts = UiKit.Label(canvas.transform, "", 20, new Color(1f, 0.3f, 0.35f), FontStyle.Bold, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(120, 24), TextAnchor.MiddleCenter, 0.9f);
                look.hearts.raycastTarget = false;
            }
            looks.Add(look);
        }
    }

    public void Clear()
    {
        foreach (var l in looks) { if (l.root != null) Destroy(l.root); if (l.tag != null) Destroy(l.tag.gameObject); if (l.hearts != null) Destroy(l.hearts.gameObject); }
        looks.Clear();
        if (youHearts != null) { Destroy(youHearts.gameObject); youHearts = null; }
        foreach (var p in pops) if (p.t != null) Destroy(p.t.gameObject);
        pops.Clear();
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
            if (l.hearts != null) l.hearts.gameObject.SetActive(show && z < 6f && mode == MpMode.Brawl);
            if (!show) continue;
            // on the sea where they are, as the local board floats on it
            float sea = WaveField.Height(s.px, WaveField.Scroll + z, rider.t);
            l.root.transform.position = new Vector3(s.px, sea + 0.06f + s.py, z);
            float yaw = -Mathf.Atan2(s.vx, Mathf.Max(8f, s.eff)) * Mathf.Rad2Deg;
            // the trick in the air: spin about up, flip end over end, roll along the board
            l.root.transform.rotation = Quaternion.Euler(0f, yaw - s.spin, 0f) * Quaternion.Euler(-s.flip, 0f, 0f) * Quaternion.Euler(0f, 0f, s.roll - s.carve * 8f);
            // a lunge at someone: a quick shove of the whole surfer toward them
            l.root.transform.position += new Vector3(s.lunge * 0.35f * (s.strikeDir == 0f ? 1f : s.strikeDir), 0f, 0f);
            // stood on the board, and the punch or kick laid over it
            Multi.StrikeCurve(s.strikeT, s.strikeHold, out float sw, out float se);
            if (l.rig != null)
            {
                l.rig.Idle(dt, 1f);
                if (sw > 0f) l.rig.StrikePose(sw, se, s.strikeKick, Vector3.Dot(l.rig.Facing(), new Vector3(s.strikeDir, 0f, 0f)) >= 0f);
            }
            else if (l.prig != null)
            {
                l.prig.Idle(dt, 1f);
                if (sw > 0f) l.prig.StrikePose(sw, se, s.strikeKick, Vector3.Dot(l.prig.Facing(), new Vector3(s.strikeDir, 0f, 0f)) >= 0f);
            }
            if (l.tag != null)
            {
                var sp = cam.WorldToScreenPoint(l.root.transform.position + Vector3.up * 3.0f);
                if (sp.z <= 0f) { l.tag.gameObject.SetActive(false); if (l.hearts != null) l.hearts.gameObject.SetActive(false); continue; }
                ((RectTransform)l.tag.transform).anchoredPosition = new Vector2(sp.x, sp.y) / scale;
                // the hearts over each surfer in a brawl: full and lost
                if (l.hearts != null && mode == MpMode.Brawl)
                {
                    ((RectTransform)l.hearts.transform).anchoredPosition = new Vector2(sp.x, sp.y) / scale + new Vector2(0f, 22f);
                    l.hearts.text = Hearts(s.hearts);
                }
                // in a brawl: hearts, and a "!" in red while it winds up a swing, so it can be dodged
                l.tag.text = mode == MpMode.Brawl ? (s.windup > 0f ? "! " : "") + s.name : s.name;
                l.tag.color = mode == MpMode.Brawl && s.windup > 0f ? new Color(1f, 0.35f, 0.25f) : s.tint;
                // a trick just landed: called out over them for a moment
                if (s.callT > 0f && !(mode == MpMode.Brawl && s.windup > 0f)) { l.tag.text = s.name + "  " + s.call; l.tag.color = UiKit.TrickYellow; }
            }
        }
    }

    // full hearts, and the lost ones faded (one glyph the font is sure to have)
    static string Hearts(int n)
    {
        n = Mathf.Clamp(n, 0, 3);
        return new string('\u2665', n) + (n < 3 ? "<color=#ffffff55>" + new string('\u2665', 3 - n) + "</color>" : "");
    }

    // ---- your own hearts, over your own head ----
    Text youHearts;
    public void StepYou(MpMode mode, int hearts, bool show)
    {
        if (canvas == null || rider == null || cam == null) return;
        if (youHearts == null)
        {
            youHearts = UiKit.Label(canvas.transform, "", 22, new Color(1f, 0.3f, 0.35f), FontStyle.Bold, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(120, 26), TextAnchor.MiddleCenter, 0.9f);
            youHearts.raycastTarget = false;
        }
        bool on = show && mode == MpMode.Brawl;
        var sp = cam.WorldToScreenPoint(rider.transform.position + Vector3.up * 3.6f);
        if (sp.z <= 0f) on = false;
        if (youHearts.gameObject.activeSelf != on) youHearts.gameObject.SetActive(on);
        if (!on) return;
        float scale = Mathf.Max(0.01f, canvas.scaleFactor);
        ((RectTransform)youHearts.transform).anchoredPosition = new Vector2(sp.x, sp.y) / scale;
        youHearts.text = Hearts(hearts);
    }

    // ---- a hit, seen: a word that jumps out of the spot and fades ----
    class PopWord { public Text t; public Vector3 at; public float age; }
    readonly List<PopWord> pops = new List<PopWord>();
    public void Pop(Vector3 world, string word, Color c)
    {
        if (canvas == null) return;
        var t = UiKit.Label(canvas.transform, word, 30, c, FontStyle.Bold, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 40), TextAnchor.MiddleCenter, 1f);
        t.raycastTarget = false;
        var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.05f, 0.05f, 0.1f, 0.9f); o.effectDistance = new Vector2(2f, -2f);
        pops.Add(new PopWord { t = t, at = world });
    }

    public void StepPops(float dt)
    {
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i]; p.age += dt;
            if (p.t == null || p.age > 0.7f || cam == null) { if (p.t != null) Destroy(p.t.gameObject); pops.RemoveAt(i); continue; }
            var sp = cam.WorldToScreenPoint(p.at + Vector3.up * (2.2f + p.age * 2.5f));
            if (sp.z <= 0f) { p.t.enabled = false; continue; }
            p.t.enabled = true;
            ((RectTransform)p.t.transform).anchoredPosition = new Vector2(sp.x, sp.y) / scale;
            // a punch of size, then away
            float k = p.age < 0.1f ? Mathf.Lerp(0.4f, 1.35f, p.age / 0.1f) : Mathf.Lerp(1.35f, 1f, Mathf.Min(1f, (p.age - 0.1f) / 0.15f));
            p.t.transform.localScale = Vector3.one * k;
            var c = p.t.color; c.a = p.age < 0.45f ? 1f : 1f - (p.age - 0.45f) / 0.25f; p.t.color = c;
        }
    }
}
