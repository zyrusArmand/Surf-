// The shop: boards and riders, bought with puka and equipped, and the Style shelf of gear the
// rank hands out. The web's sheet (index.html ~2330-2400, shopCard ~12936, drawShop ~13120): a
// grid of carved niches -- every item stands in ui/shelf_frame.png, its photograph in the
// opening, its name and its price on the wooden shelf under it -- dense to browse, three across
// on a phone. The one standing in use is lit in the niche; a piece of gear the rank has not
// granted is dimmed. A tile is a picture and a price and nothing else: a tap opens the viewer
// (Viewer.cs), where the thing is big enough to judge, its numbers are read, and it is bought,
// ridden or worn. The photographs come from Previews, taken one a frame as tiles come into view.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Shop : MonoBehaviour
{
    public System.Action onChange;        // the equipped board, rider or gear changed
    public System.Action onClose, onGuide;        // onGuide: the School tab hands over to Board school
    GameObject pane; Text tCoins, tLv; string tab = "boards";
    RectTransform view, content; ScrollRect scroll;
    Image tabBoards, tabRiders, tabStyle;
    Viewer viewer;

    // #shopList: grid-template-columns repeat(auto-fill, minmax(104px, 1fr)), gap 8px, padding
    // 8px 2px 14px inside the sheet's 16px; a tile is aspect-ratio 560/1035
    const float MinW = 104f, Gap = 8f, PadX = 18f, PadTop = 8f, PadBot = 14f, Aspect = 1035f / 560f;
    static readonly Color SkyBlue = new Color(0x7c / 255f, 0xc6 / 255f, 0xee / 255f);      // .thumb's background until the shot lands
    static readonly Color NameCol = new Color(0xfd / 255f, 0xf3 / 255f, 0xe2 / 255f);      // .foot .nm
    static readonly Color HaveCol = new Color(1f, 0xdf / 255f, 0x2e / 255f);               // .pr.have
    static readonly Color LockCol = new Color(0x9f / 255f, 0xd8 / 255f, 0xea / 255f, 0.85f); // .pr.lock
    // object-fit: cover -- the opening is 0.746 / (0.682 x 1035/560) = 0.592 wide for its height,
    // the shot 164/250 = 0.656, so the shot's sides are trimmed to fit
    static readonly Rect Cover = CoverRect();
    static Rect CoverRect() { float hole = 0.746f / (0.682f * Aspect), shot = Previews.W / (float)Previews.H, u = Mathf.Min(1f, hole / shot); return new Rect((1f - u) * 0.5f, 0f, u, 1f); }

    class Tile
    {
        public string kind, id, name; public bool heading, muted, done;
        public RectTransform rt; public RawImage thumb; public Text nm; public float y, h;
    }
    readonly List<Tile> tiles = new List<Tile>();
    float laidW = -1f;
    string wornSeen, riderSeen;

    public bool Open => (pane != null && pane.activeSelf) || (viewer != null && viewer.Open);

    public void Build(Transform canvas)
    {
        pane = UiKit.Rect(canvas, "shop", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var bg = pane.AddComponent<Image>(); bg.color = new Color(0.04f, 0.16f, 0.30f, 1f);   // opaque: nothing of the beach or the last run shows through
        var p = pane.transform;
        UiKit.Label(p, "Shop", 34, Color.white, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(200, 44), TextAnchor.MiddleLeft);
        var pill = UiKit.Pic(p, "coins", UiKit.Rounded(112, 32, 16, 16, 16, 16, UiKit.Body.Pill, 1.5f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-66, -20), new Vector2(112, 32));
        tCoins = UiKit.Label(pill.transform, "0", 15, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(6, 0), Vector2.zero);
        var shell = UiKit.Icon("puka"); if (shell != null) { var im = UiKit.Pic(pill.transform, "puka", shell, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(9, 0), new Vector2(17, 17)); im.raycastTarget = false; }
        // #shopLv: the level, parked against the purse -- together they answer "can I have this"
        var lv = Solid.Pic(p, "level", Solid.Rounded(64, 26, 13, 1f), new Color(1f, 0.914f, 0.659f, 0.42f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-186, -23), new Vector2(64, 26));
        lv.raycastTarget = false;
        tLv = UiKit.Label(lv.transform, "Lv 1", 15, new Color(1f, 0.914f, 0.659f, 0.92f), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 0.5f);
        var close = UiKit.Pic(p, "close", UiKit.Circle(40, UiKit.Body.Teal, 2f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -16), new Vector2(40, 40));
        UiKit.Label(close.transform, "×", 24, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 1), Vector2.zero);
        close.raycastTarget = true; UiKit.Pressable(close.gameObject, null, () => { Show(false); onClose?.Invoke(); });
        // tabs
        tabBoards = Tab(p, "Boards", 14, "boards"); tabRiders = Tab(p, "Riders", 118, "riders"); tabStyle = Tab(p, "Style", 222, "gear");
        // the fourth tab is Board school: what these shapes ARE, which a shop sorted by price cannot say
        var sch = UiKit.Pic(p, "tab:school", UiKit.Rounded(100, 34, 17, 17, 17, 17, UiKit.Body.Teal, 1.5f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(326, -66), new Vector2(100, 34));
        UiKit.Label(sch.transform, "School", 14, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        sch.raycastTarget = true; UiKit.Pressable(sch.gameObject, null, () => { Sfx.Buzz(8); Show(false); onGuide?.Invoke(); });
        // the shelf: a scrolling grid whose first and last 32px fade, so a tile leaving the view
        // dissolves into the structure rather than being cut at a line
        view = UiKit.Rect(p, "view", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(0, -112));
        var mask = view.gameObject.AddComponent<RectMask2D>(); mask.softness = new Vector2Int(0, 32);
        var hit = view.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);     // a drag in a gap scrolls too
        scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        content = UiKit.Rect(view, "content", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
        scroll.content = content; scroll.viewport = view;
        pane.SetActive(false);
        // the full-screen look, over everything, coming back here on Back
        viewer = gameObject.AddComponent<Viewer>(); viewer.Build(canvas);
        viewer.onChange = () => onChange?.Invoke();
        viewer.onClose = () => { pane.SetActive(true); Fill(true); };
    }

    Image Tab(Transform p, string label, float x, string id)
    {
        var im = UiKit.Pic(p, "tab:" + id, UiKit.Rounded(100, 34, 17, 17, 17, 17, UiKit.Body.Teal, 1.5f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -66), new Vector2(100, 34));
        UiKit.Label(im.transform, label, 14, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        im.raycastTarget = true; UiKit.Pressable(im.gameObject, null, () => { tab = id; Fill(); });
        return im;
    }

    public void Show(bool on)
    {
        if (!on && viewer != null && viewer.Open) viewer.Hide();
        pane.SetActive(on);
        if (on) Fill();
    }

    /// opens straight onto one shelf: "boards", "riders" or "gear"
    public void ShowOn(string which) { tab = which; Show(true); }

    // ---- the shelves, in the web's order: the rack by price, the cast by price, gear by slot ----
    static List<BoardCatalog.Board> RackOrder()
    {
        var l = new List<BoardCatalog.Board>();
        foreach (var b in BoardCatalog.All) if (!b.hidden) l.Add(b);      // the borrowed pair is not for sale
        for (int i = 1; i < l.Count; i++)                                   // stable, as Array.sort is
        {
            var x = l[i]; int j = i - 1;
            while (j >= 0 && l[j].price > x.price) { l[j + 1] = l[j]; j--; }
            l[j + 1] = x;
        }
        return l;
    }
    static List<RiderCatalog.Rider> CastOrder()
    {
        var l = new List<RiderCatalog.Rider>();
        foreach (var r in RiderCatalog.All) if (!Rungs.RiderHidden(r.id)) l.Add(r);
        for (int i = 1; i < l.Count; i++)
        {
            var x = l[i]; int j = i - 1;
            while (j >= 0 && l[j].price > x.price) { l[j + 1] = l[j]; j--; }
            l[j + 1] = x;
        }
        return l;
    }

    const int PrPrice = 0, PrHave = 1, PrLock = 2;

    void Fill(bool keep = false)
    {
        float keepY = content.anchoredPosition.y;
        foreach (Transform c in content) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        tiles.Clear();
        Previews.Drop();                  // what the last shelf asked for is not wanted any more
        // every rider card wears what is worn, and every gear card is worn by the rider you play:
        // a change from anywhere (the viewer, the wheel, board school) retakes the ones it spoils
        var s = SaveData.S;
        string wornNow = s.gearEyes + "|" + s.gearHead + "|" + s.gearNeck + "|" + s.gearBody;
        if (wornNow != wornSeen) { if (wornSeen != null) Previews.ForgetRiders(); wornSeen = wornNow; }
        if (s.rider != riderSeen) { if (riderSeen != null) Previews.ForgetGear(); riderSeen = s.rider; }
        tCoins.text = SaveData.Coins.ToString("N0");
        tLv.text = "Lv " + Progress.Level;
        tabBoards.color = tab == "boards" ? Color.white : new Color(1, 1, 1, 0.55f);
        tabRiders.color = tab == "riders" ? Color.white : new Color(1, 1, 1, 0.55f);
        tabStyle.color = tab == "gear" ? Color.white : new Color(1, 1, 1, 0.55f);
        if (tab == "boards")
            foreach (var b in RackOrder())
            {
                bool have = SaveData.Owns(b.id), on = SaveData.S.equipped == b.id;
                // a locked tile shows what it is waiting for INSTEAD of the price; the gate asks
                // about buying, never about riding, so a board already owned is never re-locked
                if (on) Add("board", b.id, b.name, "RIDING", PrHave, true, false);
                else if (have) Add("board", b.id, b.name, "OWNED", PrHave, false, false);
                else if (Ladder.BoardLocked(b.id)) Add("board", b.id, b.name, "Lv " + Ladder.BoardLvl(b.id), PrLock, false, false);
                else Add("board", b.id, b.name, b.price.ToString("N0"), PrPrice, false, false);
            }
        else if (tab == "riders")
            foreach (var r in CastOrder())
            {
                bool have = SaveData.OwnsRider(r.id), on = SaveData.S.rider == r.id;
                if (on) Add("rider", r.id, r.name, "RIDING", PrHave, true, false);
                else if (have) Add("rider", r.id, r.name, "OWNED", PrHave, false, false);
                else if (Ladder.CharLocked(r.id)) Add("rider", r.id, r.name, "Lv " + Ladder.CharLvl(r.id), PrLock, false, false);
                else Add("rider", r.id, r.name, r.price.ToString("N0"), PrPrice, false, false);
            }
        else
        {
            // the Style shelf: the same tiles under their slot headings. Gear is not for sale --
            // the rank grants it, or the wheel did -- so its own words sit where a price would
            foreach (var slot in GearCatalog.Slots)
            {
                bool anyIn = false; foreach (var g in GearCatalog.All) if (g.slot == slot) { anyIn = true; break; }
                if (!anyIn) continue;
                Heading(GearCatalog.SlotTitle(slot));
                foreach (var g in GearCatalog.All)
                {
                    if (g.slot != slot) continue;
                    bool have = GearCatalog.Have(g.id), on = have && GearCatalog.On(SaveData.S, slot) == g.id;
                    if (on) Add("gear", g.id, g.name, "WEARING", PrHave, true, false);
                    else if (have) Add("gear", g.id, g.name, "OWNED", PrHave, false, false);
                    else Add("gear", g.id, g.name, "Lv " + g.lv, PrLock, false, true);
                }
            }
        }
        Layout();
        content.anchoredPosition = new Vector2(0f, keep ? keepY : 0f);      // the scroller clamps it
        if (!keep) scroll.velocity = Vector2.zero;
    }

    /// .gsec: a slot heading across the whole grid
    void Heading(string title)
    {
        var lb = UiKit.Label(content, title.ToUpperInvariant(), 12, new Color(0xbf / 255f, 0xe6 / 255f, 0xee / 255f, 0.72f), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 15), TextAnchor.LowerLeft, 0.4f);
        tiles.Add(new Tile { heading = true, rt = lb.rectTransform });
    }

    /// one niche: the shadow it casts, the light in it when it is in use, the photograph in the
    /// opening, the frame over that, and the name and the price on the shelf
    void Add(string kind, string id, string title, string price, int pr, bool on, bool muted)
    {
        var t = new Tile { kind = kind, id = id, name = title, muted = muted };
        var rt = UiKit.Rect(content, kind + ":" + id, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(MinW, MinW * Aspect));
        t.rt = rt;
        float pad2 = Viewer.SoftPad * 2f;
        // drop-shadow(0 6px 10px rgba(0,16,34,.42))
        var sh = UiKit.Pic(rt, "shadow", Viewer.Soft(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(pad2 - 4f, pad2 - 4f));
        sh.type = Image.Type.Sliced; sh.color = new Color(0f, 16 / 255f, 34 / 255f, 0.42f); sh.raycastTarget = false;
        // .card.on: drop-shadow(0 0 10px rgba(255,223,46,.55)) -- a warm light in the niche, not a border on a box
        if (on)
        {
            var gl = UiKit.Pic(rt, "lit", Viewer.Soft(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(pad2, pad2));
            gl.type = Image.Type.Sliced; gl.color = new Color(1f, 0.875f, 0.18f, 0.55f); gl.raycastTarget = false;
        }
        // .thumb: left 12.7%, top 9.2%, 74.6% wide, 68.2% tall
        var th = UiKit.Rect(rt, "thumb", new Vector2(0.127f, 0.226f), new Vector2(0.873f, 0.908f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        t.thumb = th.gameObject.AddComponent<RawImage>(); t.thumb.color = SkyBlue; t.thumb.raycastTarget = false; t.thumb.uvRect = Cover;
        if (on)
        {
            // .card.on .thumb: a 1px gold rim and an 18px warm glow inside it
            var ins = UiKit.Pic(rt, "glow", Inset(), new Vector2(0.127f, 0.226f), new Vector2(0.873f, 0.908f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ins.type = Image.Type.Sliced; ins.color = new Color(1f, 0.86f, 0.17f, 1f); ins.raycastTarget = false;
        }
        var fr = UiKit.Pic(rt, "frame", Frame(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fr.raycastTarget = true;
        // .foot: left 9%, right 9%, bottom 3.4%, 19% tall -- on the shelf, which is the bottom 22.6%
        var foot = UiKit.Rect(rt, "foot", new Vector2(0.09f, 0.034f), new Vector2(0.91f, 0.224f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        t.nm = UiKit.Label(foot, title, 11, NameCol, FontStyle.Bold, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6.5f), new Vector2(0, 14), TextAnchor.MiddleCenter, 0.85f);
        Wood(t.nm);
        var pt = UiKit.Label(foot, price, 10.5f, pr == PrHave ? HaveCol : pr == PrLock ? LockCol : new Color(1f, 1f, 1f, 0.96f), FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6.5f), new Vector2(120, 13), TextAnchor.MiddleCenter, 0.85f);
        Wood(pt);
        var shell = pr == PrPrice ? UiKit.Icon("puka") : null;
        if (shell != null)
        {
            // the price and its shell, centred as one: 11px, 3px after the figure
            float pw = pt.preferredWidth, shift = (3f + 11f) * 0.5f;
            pt.rectTransform.anchoredPosition = new Vector2(-shift, -6.5f);
            var pk = UiKit.Pic(foot, "puka", shell, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-shift + pw * 0.5f + 3f + 5.5f, -6.5f), new Vector2(11, 11));
            pk.raycastTarget = false;
        }
        // .card.lk: opacity .54, and the picture's colour taken down (Previews.Muted)
        if (muted) { var cg = rt.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0.54f; }
        // the whole tile opens the viewer: one look at the thing bigger than a thumbnail before any shell is spent
        Tappable(fr.gameObject, () => OpenViewer(kind, id));
        tiles.Add(t);
        if (Previews.Done(kind, id)) Assign(t);     // already photographed: no blue flash
    }

    // the shelf's lettering: text-shadow 0 1px 2px rgba(20,8,0,.85), carved rather than printed
    static void Wood(Text t)
    {
        var s = t.GetComponent<Shadow>();
        if (s != null) { s.effectColor = new Color(20 / 255f, 8 / 255f, 0f, 0.85f); s.effectDistance = new Vector2(0f, -1f); }
    }

    /// a tap opens; a drag scrolls the shelf (the trigger hands the drag on, and a press that
    /// turned into a drag is not a tap)
    void Tappable(GameObject go, System.Action tap)
    {
        var et = go.AddComponent<EventTrigger>();
        UiKit.Add(et, EventTriggerType.InitializePotentialDrag, e => scroll.OnInitializePotentialDrag((PointerEventData)e));
        UiKit.Add(et, EventTriggerType.BeginDrag, e => scroll.OnBeginDrag((PointerEventData)e));
        UiKit.Add(et, EventTriggerType.Drag, e => scroll.OnDrag((PointerEventData)e));
        UiKit.Add(et, EventTriggerType.EndDrag, e => scroll.OnEndDrag((PointerEventData)e));
        UiKit.Add(et, EventTriggerType.Scroll, e => scroll.OnScroll((PointerEventData)e));
        UiKit.Add(et, EventTriggerType.PointerClick, e => { var pe = e as PointerEventData; if (pe != null && pe.dragging) return; tap(); });
    }

    void OpenViewer(string kind, string id)
    {
        // the arrows walk the shelf in the grid's own order
        var ids = new List<string>();
        foreach (var t in tiles) if (!t.heading && t.kind == kind) ids.Add(t.id);
        pane.SetActive(false);
        viewer.Show(kind, id, ids);
    }

    // ---- the grid, laid out by hand so a heading can run across it ----
    void Layout()
    {
        float W = view.rect.width; laidW = W;
        float inner = Mathf.Max(MinW, W - PadX * 2f);
        int cols = Mathf.Max(1, Mathf.FloorToInt((inner + Gap) / (MinW + Gap)));     // auto-fill
        float cw = (inner - Gap * (cols - 1)) / cols, ch = cw * Aspect;                 // 1fr
        float y = PadTop; int col = 0; bool rowOpen = false, any = false;
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t.heading)
            {
                if (rowOpen) { y += ch; rowOpen = false; }
                if (any) y += Gap;
                y += any ? 14f : 2f;                      // .gsec margin 14px 2px 7px; 2px when it comes first
                t.rt.anchoredPosition = new Vector2(PadX + 2f, -y); t.rt.sizeDelta = new Vector2(inner - 4f, 15f);
                t.y = y; t.h = 15f;
                y += 15f + 7f; any = true;
                continue;
            }
            if (!rowOpen) { if (any) y += Gap; rowOpen = true; col = 0; }
            else if (col >= cols) { y += ch + Gap; col = 0; }
            t.rt.anchoredPosition = new Vector2(PadX + col * (cw + Gap), -y);
            t.rt.sizeDelta = new Vector2(cw, ch);
            t.y = y; t.h = ch;
            Ellipsize(t.nm, t.name, cw * 0.82f - 2f);
            col++; any = true;
        }
        if (rowOpen) y += ch;
        content.sizeDelta = new Vector2(0f, y + PadBot);
    }

    /// text-overflow: ellipsis
    static void Ellipsize(Text t, string full, float w)
    {
        t.text = full;
        if (t.preferredWidth <= w) return;
        for (int n = full.Length - 1; n > 1; n--)
        {
            t.text = full.Substring(0, n).TrimEnd() + "…";
            if (t.preferredWidth <= w) return;
        }
    }

    // ---- the photographs: only of what somebody is looking at ----
    void Update()
    {
        if (pane == null || !pane.activeSelf) return;
        if (Mathf.Abs(view.rect.width - laidW) > 0.5f) Layout();
        Previews.Pump();
        float top = content.anchoredPosition.y, vh = view.rect.height;
        int n = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t.heading) continue;
            n++;
            if (t.done) continue;
            // the first dozen are primed; the rest are asked for a screenful ahead (300px) of the window
            bool near = n <= 12 || (t.y + t.h > top - 300f && t.y < top + vh + 300f);
            if (near) Assign(t);
        }
    }

    void Assign(Tile t)
    {
        var sp = t.muted ? Previews.Muted(t.kind, t.id) : Previews.Get(t.kind, t.id);
        if (sp != null) { t.thumb.texture = sp.texture; t.thumb.color = Color.white; t.done = true; }
        else if (Previews.Done(t.kind, t.id)) t.done = true;          // it could not be photographed: the sky stays
    }

    // ---- the frame, and the light inside the opening ----
    static Sprite frame;
    static Sprite Frame()
    {
        if (frame != null) return frame;
        frame = UiKit.Icon("shelf_frame");
        if (frame == null) frame = UiKit.Rounded(MinW, MinW * Aspect, 10, 10, 10, 10, UiKit.Body.Glass, 1.5f);
        return frame;
    }

    static Sprite inset;
    static Sprite Inset()
    {
        if (inset != null) return inset;
        const int S = 50;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float e = Mathf.Min(Mathf.Min(x + 0.5f, S - x - 0.5f), Mathf.Min(y + 0.5f, S - y - 0.5f));
            float k = 1f - Mathf.Clamp01(e / 18f);
            float a = 0.22f * k * k;                    // inset 0 0 18px rgba(255,214,40,.22)
            if (e < 1f) a = Mathf.Max(a, 0.5f);         // 0 0 0 1px rgba(255,223,46,.5)
            px[y * S + x] = new Color(1, 1, 1, a);
        }
        tex.SetPixels(px); tex.Apply(false, true);
        inset = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        return inset;
    }
}
