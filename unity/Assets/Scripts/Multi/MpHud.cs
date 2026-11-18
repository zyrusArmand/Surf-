// The multiplayer screens, on the HUD's canvas: a "Play with friends" button on the title, the mode
// picker (Race, Together, Brawl; how many others), a status line along the top during a round (your
// place and the metres to go, the crew, your hearts), PUNCH and KICK over JUMP in a brawl, and the
// result when the round is over. During a round: the standings down the top left (place, portrait,
// name, distance and tricks, rows sliding as places change) and a small arrowhead in each rival's
// colour round your surfer, pointing at where they are. After it: the scoreboard.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MpHud : MonoBehaviour
{
    public Multi mp; public Hud hud; public BoardController rider;
    public System.Action<MpMode, int> onStart;      // the picker's choice: the round starts
    public System.Action onDone;                    // the result closed: back to the title

    RectTransform root; GameObject titleBtn, picker, result, brawl, downPanel; Text status, resTitle, resRows, downTitle, payLabel;
    Image payBtn, watchBtn;
    int count = 3;
    Text countLabel;

    // ---- the standings, the arrows, the scoreboard ----
    class Row { public int key; public RectTransform rt; public CanvasGroup cg; public Image pic; public Text place, name, line; public float y; public bool placed; }
    readonly List<Row> rows = new List<Row>();
    RectTransform board, arrows, scoreTable;
    readonly List<Image> arrowPics = new List<Image>();
    readonly List<(Image img, string id)> pending = new List<(Image, string)>();
    int builtRound = -1;
    static Sprite arrowSprite;
    const float RowH = 40f, RowGap = 4f, RowW = 182f;

    public void Build()
    {
        root = UiKit.Rect(hud.UiRoot, "multi", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        // ---- the title's button: under the bar, centred ----
        titleBtn = Button(root, "Play with friends", new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(230, 54), () => { Sfx.Buzz(10); ShowPicker(true); }, 19).gameObject;
        // ---- the picker ----
        picker = UiKit.Rect(root, "picker", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var dim = picker.AddComponent<Image>(); dim.color = new Color(0.02f, 0.08f, 0.16f, 0.86f);
        UiKit.Label(picker.transform, "PLAY WITH FRIENDS", 26, UiKit.TrickYellow, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(400, 40));
        UiKit.Label(picker.transform, "Bots ride with you for now. Online friends are coming next.", 14, Pane.Faint, FontStyle.Normal, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(380, 40));
        ModeButton("RACE", "First to 1000 m wins. Wipe out and you're back on your board.", -230, MpMode.Race);
        ModeButton("TOGETHER", "One team. A fallen surfer is back in 8 s while anyone's still up. Go as far as you can.", -360, MpMode.Together);
        ModeButton("BRAWL", "Punch and kick rivals off their boards. Three hearts each. Last one standing wins.", -490, MpMode.Brawl);
        // how many others
        countLabel = UiKit.Label(picker.transform, "", 18, Color.white, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -610), new Vector2(300, 30));
        Button(picker.transform, "-", new Vector2(0.5f, 1f), new Vector2(-120, -640), new Vector2(54, 44), () => { count = Mathf.Max(1, count - 1); Count(); }, 24);
        Button(picker.transform, "+", new Vector2(0.5f, 1f), new Vector2(120, -640), new Vector2(54, 44), () => { count = Mathf.Min(3, count + 1); Count(); }, 24);
        Count();
        Button(picker.transform, "Back", new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(160, 46), () => ShowPicker(false), 17);
        picker.SetActive(false);
        // ---- during a round ----
        // the status line on the right under the band; the standings take the left
        status = UiKit.Label(root, "", 14, Color.white, FontStyle.Bold, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10, -86), new Vector2(220, 24), TextAnchor.MiddleRight, 0.8f);
        status.raycastTarget = false;
        board = UiKit.Rect(root, "standings", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8, -86), new Vector2(RowW, 4 * (RowH + RowGap)));
        arrows = UiKit.Rect(root, "arrows", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        brawl = UiKit.Rect(root, "brawl", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0, 146), new Vector2(106, 170)).gameObject;
        AttackButton(brawl.transform, "KICK", 0f, true);
        AttackButton(brawl.transform, "PUNCH", 88f, false);
        brawl.SetActive(false);
        // ---- down: respawn for puka, watch till the end, or go home ----
        downPanel = UiKit.Rect(root, "down", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(340, 300)).gameObject;
        var dbg = downPanel.AddComponent<Image>(); dbg.sprite = UiKit.Rounded(340, 300, 22, 22, 22, 22, UiKit.Body.Glass, 2.5f); dbg.color = new Color(1f, 1f, 1f, 0.97f);
        downTitle = UiKit.Label(downPanel.transform, "", 22, UiKit.TrickYellow, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(320, 34));
        payBtn = Button(downPanel.transform, "", new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(280, 58), () => { if (mp.PayRespawn()) Sfx.Buy(); else Sfx.Buzz(30); }, 18);
        payLabel = UiKit.Label(payBtn.transform, "", 18, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        watchBtn = Button(downPanel.transform, "Watch till the end", new Vector2(0.5f, 1f), new Vector2(0, -134), new Vector2(280, 54), () => { Sfx.Buzz(10); mp.Watch(); }, 18);
        Button(downPanel.transform, "Go home", new Vector2(0.5f, 1f), new Vector2(0, -198), new Vector2(280, 54), () => { Sfx.Buzz(10); mp.GoHome(); }, 18);
        downPanel.SetActive(false);
        // ---- the result ----
        result = UiKit.Rect(root, "result", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var rdim = result.AddComponent<Image>(); rdim.color = new Color(0.02f, 0.08f, 0.16f, 0.88f);
        resTitle = UiKit.Label(result.transform, "", 28, UiKit.TrickYellow, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(400, 44));
        scoreTable = UiKit.Rect(result.transform, "scoreboard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, 136), new Vector2(ColW, 220));
        resRows = UiKit.Label(result.transform, "", 16, Color.white, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -84), new Vector2(380, 90), TextAnchor.UpperCenter);
        Button(result.transform, "Back to the beach", new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(240, 54), () => { result.SetActive(false); onDone?.Invoke(); }, 18);
        result.SetActive(false);
    }

    void Count() { countLabel.text = count == 1 ? "1 other surfer" : count + " other surfers"; }

    void ModeButton(string title, string blurb, float y, MpMode m)
    {
        var b = Button(picker.transform, "", new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(360, 110), () => { Sfx.Buzz(12); ShowPicker(false); onStart?.Invoke(m, count); }, 24);
        UiKit.Label(b.transform, title, 26, UiKit.TrickYellow, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(0, 34));
        UiKit.Label(b.transform, blurb, 14, Color.white, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(-28, -44));
    }

    void AttackButton(Transform parent, string label, float y, bool kick)
    {
        var a = UiKit.Pic(parent, label, UiKit.Rounded(106, 76, 24, 0, 0, 24, UiKit.Body.Glass, 2.5f, false, true), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, y), new Vector2(106, 76));
        UiKit.Label(a.transform, label, 17, kick ? new Color(1f, 0.6f, 0.4f) : Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        a.raycastTarget = true;
        UiKit.Pressable(a.gameObject, () => mp.LocalAttack(kick));
    }

    Image Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, System.Action tap, float font)
    {
        var b = UiKit.Pic(parent, label, UiKit.Rounded(size.x, size.y, 16, 16, 16, 16, UiKit.Body.Glass, 2.5f), anchor, anchor, new Vector2(0.5f, anchor.y >= 1f ? 1f : anchor.y <= 0f ? 0f : 0.5f), pos, size);
        if (!string.IsNullOrEmpty(label)) UiKit.Label(b.transform, label, font, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        b.raycastTarget = true;
        UiKit.Pressable(b.gameObject, null, tap);
        return b;
    }

    public void ShowPicker(bool on) { if (picker != null) picker.SetActive(on); }
    public bool PickerOpen => picker != null && picker.activeSelf;
    public bool ResultOpen => result != null && result.activeSelf;

    /// on the title (true) the button shows; in a round, the status line and, in a brawl, the buttons
    public void Title(bool on)
    {
        if (titleBtn != null) titleBtn.SetActive(on && !PickerOpen);
        if (board != null && board.gameObject.activeSelf) board.gameObject.SetActive(false);
        HideArrows();
    }

    public void ShowResult(MpResult r)
    {
        resTitle.text = r.title;
        resRows.text = (r.rows.Count > 0 ? string.Join("\n", r.rows) + "\n" : "") + "+" + r.coins + " puka";
        Scoreboard(r);
        result.SetActive(true);
        brawl.SetActive(false); status.text = "";
        if (downPanel != null) downPanel.SetActive(false);
        Standings(false); HideArrows();
    }

    // ---------------------------------------------------------------- the scoreboard
    // columns: place, portrait, name, distance, tricks, deaths, eliminations
    static readonly float[] ColX = { 0f, 22f, 60f, 156f, 210f, 262f, 316f };
    static readonly float[] ColWd = { 20f, 34f, 94f, 54f, 52f, 54f, 54f };
    const float ColW = 370f;

    void Scoreboard(MpResult r)
    {
        for (int i = scoreTable.childCount - 1; i >= 0; i--) Destroy(scoreTable.GetChild(i).gameObject);
        string[] heads = { "#", "", "SURFER", "DIST", "TRICKS", "DEATHS", "ELIMS" };
        for (int c = 0; c < heads.Length; c++)
            Cell(scoreTable, heads[c], c, 0f, 20f, 11, Pane.Faint, c == 2 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
        float y = -24f;
        foreach (var t in r.table)
        {
            var line = UiKit.Pic(scoreTable, "row", UiKit.Rounded(ColW, 40, 12, 12, 12, 12, UiKit.Body.Glass, t.you ? 2.5f : 1.5f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0, y), new Vector2(ColW, 40));
            line.raycastTarget = false;
            if (t.you) line.color = new Color(1f, 0.95f, 0.8f, 1f);
            Cell(line.transform, t.place.ToString(), 0, 0f, 40f, 17, Color.white, TextAnchor.MiddleCenter);
            Portrait(line.transform, t, new Vector2(ColX[1] + 1f, -4f), 32f);
            Cell(line.transform, t.name, 2, 0f, 40f, 15, t.tint, TextAnchor.MiddleLeft);
            string d = Mathf.RoundToInt(t.dist) + " m";
            if (r.mode == MpMode.Race && t.finished) d = t.finishT.ToString("0.0") + "s";
            Cell(line.transform, d, 3, 0f, 40f, 14, Color.white, TextAnchor.MiddleCenter);
            Cell(line.transform, t.tricks.ToString(), 4, 0f, 40f, 15, Color.white, TextAnchor.MiddleCenter);
            Cell(line.transform, t.deaths.ToString(), 5, 0f, 40f, 15, Color.white, TextAnchor.MiddleCenter);
            // only a brawl has anyone knocking anyone off
            Cell(line.transform, r.mode == MpMode.Brawl ? t.elims.ToString() : "-", 6, 0f, 40f, 15, Color.white, TextAnchor.MiddleCenter);
            y -= 44f;
        }
    }

    static Text Cell(Transform parent, string s, int col, float y, float h, float font, Color c, TextAnchor a)
    {
        var t = UiKit.Label(parent, s, font, c, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColX[col], y), new Vector2(ColWd[col], h), a, 0.6f);
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    // a round disc in their colour with their initial, the portrait over it once it has been taken
    Image Portrait(Transform parent, Standing t, Vector2 at, float size)
    {
        var disc = UiKit.Pic(parent, "face", UiKit.Rounded(size, size, size / 2, size / 2, size / 2, size / 2, UiKit.Body.Glass, 2f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), at, new Vector2(size, size));
        disc.color = new Color(t.tint.r, t.tint.g, t.tint.b, 0.9f); disc.raycastTarget = false;
        UiKit.Label(disc.transform, t.name.Substring(0, 1), size * 0.5f, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).raycastTarget = false;
        disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;           // the portrait cut to the disc
        var pic = UiKit.Pic(disc.transform, "pic", null, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        pic.preserveAspect = true; pic.raycastTarget = false; pic.enabled = false;
        var sp = Face(t.riderId);
        if (sp != null) { pic.sprite = sp; pic.enabled = true; } else pending.Add((pic, t.riderId));
        return pic;
    }

    // portraits come a frame or two after they are asked for: keep the queue moving and fill them in
    void Update()
    {
        if (pending.Count == 0) return;
        Previews.Pump();
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var (img, id) = pending[i];
            if (img == null) { pending.RemoveAt(i); continue; }
            var sp = Face(id);
            if (sp != null) { img.sprite = sp; img.enabled = true; pending.RemoveAt(i); }
            else if (Previews.Done("rider", id)) pending.RemoveAt(i);       // could not be taken: the disc stays
        }
    }

    // the shop's whole-body photograph cut down to a head and shoulders: the top square of it
    static readonly Dictionary<string, (Sprite from, Sprite face)> faces = new Dictionary<string, (Sprite, Sprite)>();
    static Sprite Face(string id)
    {
        var sp = Previews.Rider(id);
        if (sp == null) return null;
        if (faces.TryGetValue(id, out var f) && f.from == sp && f.face != null) return f.face;
        var r = sp.rect; float side = Mathf.Min(r.width, r.height) * 0.78f;
        var crop = new Rect(r.x + (r.width - side) * 0.5f, r.y + r.height - side - r.height * 0.03f, side, side);
        var face = Sprite.Create(sp.texture, crop, new Vector2(0.5f, 0.5f), 100f);
        faces[id] = (sp, face);
        return face;
    }

    // ---------------------------------------------------------------- the standings, top left
    void BuildStandings()
    {
        for (int i = board.childCount - 1; i >= 0; i--) Destroy(board.GetChild(i).gameObject);
        rows.Clear();
        foreach (var t in mp.Standings())
        {
            var bg = UiKit.Pic(board, "row", UiKit.Rounded(RowW, RowH, 13, 13, 13, 13, UiKit.Body.Glass, t.you ? 2.5f : 1.5f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(RowW, RowH));
            bg.raycastTarget = false; bg.color = new Color(1f, 1f, 1f, 0.92f);
            var row = new Row { key = t.key, rt = bg.rectTransform, cg = bg.gameObject.AddComponent<CanvasGroup>() };
            row.cg.blocksRaycasts = false;
            row.place = UiKit.Label(bg.transform, "", 16, Color.white, FontStyle.Bold, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(4, 0), new Vector2(18, 0), TextAnchor.MiddleCenter, 0.6f);
            row.pic = Portrait(bg.transform, t, new Vector2(24f, -4f), 32f);
            row.name = UiKit.Label(bg.transform, t.name, 13, t.tint, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62, -3), new Vector2(RowW - 66, 18), TextAnchor.MiddleLeft, 0.6f);
            row.line = UiKit.Label(bg.transform, "", 11, Color.white, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62, -20), new Vector2(RowW - 66, 16), TextAnchor.MiddleLeft, 0.6f);
            row.place.raycastTarget = row.name.raycastTarget = row.line.raycastTarget = false;
            rows.Add(row);
        }
        builtRound = mp.round;
    }

    void Standings(bool on)
    {
        if (board.gameObject.activeSelf != on) board.gameObject.SetActive(on);
        if (!on) return;
        if (builtRound != mp.round || rows.Count != mp.others.Count + 1) BuildStandings();
        var table = mp.Standings();
        float k = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        for (int i = 0; i < table.Count; i++)
        {
            var t = table[i];
            Row row = null; foreach (var r in rows) if (r.key == t.key) { row = r; break; }
            if (row == null) continue;
            // slide to its place: up as it overtakes, down as it is passed
            float want = -i * (RowH + RowGap);
            row.y = row.placed ? Mathf.Lerp(row.y, want, k) : want; row.placed = true;
            row.rt.anchoredPosition = new Vector2(0f, row.y);
            row.place.text = t.place.ToString();
            row.name.text = mp.mode == MpMode.Brawl ? t.name + " " + new string('♥', Mathf.Max(0, t.hearts)) : t.name;
            string trick = t.tricks == 1 ? "1 trick" : t.tricks + " tricks";
            row.line.text = (mp.mode == MpMode.Race && t.finished ? "FINISHED" : Mathf.RoundToInt(t.dist) + " m") + "  ·  " + trick;
            // down or out: faded until they are back up
            row.cg.alpha = t.outOfGame ? 0.4f : t.down ? 0.6f : 1f;
        }
    }

    // ---------------------------------------------------------------- the arrows
    // a small arrowhead per rival on a ring round your surfer, in their colour, turned toward them;
    // the further off they are the fainter it is, and it goes while they are down or right beside you
    void Arrows()
    {
        var cam = mp.cam;
        var canvas = hud.UiRoot.GetComponent<Canvas>();
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        if (arrowSprite == null) arrowSprite = MakeArrow();
        while (arrowPics.Count < mp.others.Count)
        {
            var a = UiKit.Pic(arrows, "arrow", arrowSprite, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            a.raycastTarget = false;
            var o = a.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.45f); o.effectDistance = new Vector2(1.2f, -1.2f);
            arrowPics.Add(a);
        }
        Vector3 me = rider.transform.position; me.y += 1.2f;
        Vector3 a0 = cam != null ? cam.WorldToScreenPoint(me) : Vector3.zero;
        for (int i = 0; i < arrowPics.Count; i++)
        {
            var img = arrowPics[i];
            if (cam == null || i >= mp.others.Count || a0.z <= 0f) { if (img.enabled) img.enabled = false; continue; }
            var s = mp.others[i];
            Vector3 d = new Vector3(s.px - rider.px, 0f, -(s.dist - rider.dist));
            float far = d.magnitude;
            bool show = !s.down && !s.outOfGame && far > 7f;
            if (img.enabled != show) img.enabled = show;
            if (!show) continue;
            // the direction on screen: a short step from your surfer toward them, projected
            Vector3 b = cam.WorldToScreenPoint(me + d / far * 4f);
            Vector2 sd = new Vector2(b.x - a0.x, b.y - a0.y);
            if (sd.sqrMagnitude < 1e-4f) { img.enabled = false; continue; }
            sd.Normalize();
            Vector2 at = new Vector2(a0.x, a0.y) / scale + sd * 74f;
            img.rectTransform.anchoredPosition = at;
            img.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(sd.y, sd.x) * Mathf.Rad2Deg - 90f);
            var c = s.tint; c.a = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(far / 300f));
            img.color = c;
        }
    }

    void HideArrows() { foreach (var a in arrowPics) if (a != null && a.enabled) a.enabled = false; }

    // an arrowhead pointing up, notched at the back, drawn once with soft edges
    static Sprite MakeArrow()
    {
        const int N = 48;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Vector2[] poly = { new Vector2(24f, 45f), new Vector2(44f, 5f), new Vector2(24f, 15f), new Vector2(4f, 5f) };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                        if (Inside(poly, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) hit++;
                px[y * N + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 16));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool Inside(Vector2[] p, Vector2 q)
    {
        bool c = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            if ((p[i].y > q.y) != (p[j].y > q.y) && q.x < (p[j].x - p[i].x) * (q.y - p[i].y) / (p[j].y - p[i].y) + p[i].x) c = !c;
        return c;
    }

    // the down panel: shown while he chooses, or waits to be back in Together
    void Down()
    {
        bool choose = mp != null && mp.Running && mp.Choosing, wait = mp != null && mp.Waiting && !mp.Choosing;
        bool on = choose || wait;
        if (downPanel.activeSelf != on) downPanel.SetActive(on);
        if (!on) return;
        bool afford = SaveData.Coins >= mp.Cost;
        if (wait)
        {
            downTitle.text = "Back in " + Mathf.CeilToInt(mp.WaitLeft) + "...";
            payLabel.text = afford ? "Back now  (" + mp.Cost + " puka)" : "Back now (need " + mp.Cost + " puka)";
            watchBtn.gameObject.SetActive(false);
        }
        else
        {
            downTitle.text = mp.mode == MpMode.Brawl ? "You're out!" : "Wiped out!";
            payLabel.text = (mp.mode == MpMode.Brawl ? "Back in with 1 heart" : "Back on your board") + (afford ? "  (" + mp.Cost + " puka)" : " (need " + mp.Cost + ")");
            watchBtn.gameObject.SetActive(true);
        }
        payBtn.color = afford ? Color.white : new Color(1f, 1f, 1f, 0.45f);
    }

    public void Step(bool riding)
    {
        if (titleBtn != null && titleBtn.activeSelf) titleBtn.SetActive(false);
        Down();
        bool live = mp != null && mp.Running && riding;
        if (brawl.activeSelf != (live && mp.mode == MpMode.Brawl && !mp.localOut)) brawl.SetActive(live && mp.mode == MpMode.Brawl && !mp.localOut);
        // the standings stay up while he is down or watching; the arrows only while he rides
        Standings(mp != null && mp.Running);
        if (!live) { if (status.text.Length > 0) status.text = ""; HideArrows(); return; }
        Arrows();
        if (mp.mode == MpMode.Race)
            status.text = Mathf.Max(0, Mathf.RoundToInt(Multi.RaceGoal - rider.dist)) + " m to go";
        else if (mp.mode == MpMode.Together)
        {
            int up = rider.wipe ? 0 : 1; foreach (var o in mp.others) if (!o.down) up++;
            status.text = "Crew up: " + up + "/" + (mp.others.Count + 1) + "   " + Mathf.RoundToInt(rider.dist) + " m";
        }
        else
        {
            int left = mp.localOut ? 0 : 1; foreach (var o in mp.others) if (!o.outOfGame) left++;
            status.text = "You " + new string('♥', Mathf.Max(0, mp.localHearts)) + "   " + left + " left";
        }
    }
}
