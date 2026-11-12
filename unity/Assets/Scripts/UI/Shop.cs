// The shop: boards and riders, bought with puka and equipped, and the Style shelf of gear the
// rank hands out. The web's sheet (index.html ~2330-2400, shopCard ~12936, drawShop ~13120): a
// grid of carved niches -- every item stands in ui/shelf_frame.png, its photograph in the
// opening, its name and its price on the wooden shelf under it -- dense to browse, three across
// on a phone. The one standing in use is lit in the niche; a piece of gear the rank has not
// granted is dimmed. A tile is a picture and a price and nothing else: a tap opens the viewer
// (Viewer.cs), where the thing is big enough to judge, its numbers are read, and it is bought,
// ridden or worn. The photographs come from Previews, taken one a frame as tiles come into view.
// Round the grid is the web's sheet (#shop, its h2, #shopTabs, #shopClose): the dusk wash, "Beach
// shop" in gold top left, the level badge and the purse top right, five pill tabs, and "< Back"
// on a full-width pill at the foot -- drawn by ShopArt from the stylesheet's numbers.
// The Tricks tab is the trick shelf (drawTrickShelf, web 13219-13279): the same niches, but a
// tap does not open the viewer -- one not owned is bought on the spot, one owned opens the slot
// picker (#slotPick) to put it on one of the five trick buttons. Buy, tap, pick: three taps.
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
    RectTransform lvRt; Image lvArt;
    Viewer viewer;

    // ---- the sheet's chrome, in the web's numbers at phone width (430; the canvas matches width) ----
    // #shop pads 16px all round. h2: 26px gold, a 30px line from 16 (so centred at 31), 12px under
    // it; #shopLv and the purse sit on its baseline. #shopTabs: five pills across the 398px, 8px
    // apart, 37 tall from 59, 11px under them. #shopClose: .go.calm, 302 wide, 12px under the list,
    // 16 off the bottom -- squeezed to 27 tall by the column it sits in, which is how the web draws it.
    const float Side = 16f, RefH = 932f;
    const float TitleY = 31f, LvY = 35f, LvH = 22f, CoinY = 33.2f;      // the badge, measured: 22 tall from 24 -- its 16px line in 1px of padding and a 1px ring
    const float TabTop = 59f, TabH = 37f, TabGap = 8f, TabW = (430f - 2f * Side - 4f * TabGap) / 5f;
    const float ListTop = 107f, BackW = 302f, BackH = 27f, BackFoot = 16f, ListBottom = BackFoot + BackH + 12f;
    const float NoteH = 35f;
    static readonly string[] TabIds = { "school", "boards", "riders", "gear", "tricks" };
    static readonly string[] TabNames = { "School", "Boards", "Surfers", "Style", "Tricks" };
    static readonly Color TabText = ShopArt.Rgb(0xea, 0xf6, 0xff);           // #shopTabs .tab; .tab.on is #fff
    readonly Image[] tabArt = new Image[5]; readonly Text[] tabLabel = new Text[5];

    /// the sheet's colour y layout pixels down from the top, which is what every pill on it is composited over
    static Color SheetAt(float y) => ShopArt.SheetAt(0.5f, y / RefH);
    static Sprite TabSprite(bool on) => ShopArt.Pill(TabW, TabH, on ? ShopArt.Glass.TabOn : ShopArt.Glass.TabOff, SheetAt(TabTop + TabH * 0.5f));

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
    // ...and a trick's drawn picture, 56 by 68, trimmed the same way
    static readonly Rect TrickCover = TrickCoverRect();
    static Rect TrickCoverRect() { float hole = 0.746f / (0.682f * Aspect), shot = 56f / 68f, u = Mathf.Min(1f, hole / shot); return new Rect((1f - u) * 0.5f, 0f, u, 1f); }

    class Tile
    {
        public string kind, id, name; public bool heading, note, muted, done;
        public RectTransform rt; public RawImage thumb; public Text nm; public float y, h;
    }
    readonly List<Tile> tiles = new List<Tile>();
    float laidW = -1f;
    string wornSeen, riderSeen;

    public bool Open => (pane != null && pane.activeSelf) || (viewer != null && viewer.Open);

    public void Build(Transform canvas)
    {
        pane = UiKit.Rect(canvas, "shop", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        // #shop's background: the dusk wash with the sunset glow low in it, opaque enough that
        // nothing of the beach or the last run shows through
        var bg = pane.AddComponent<Image>(); bg.sprite = ShopArt.Sheet(); bg.type = Image.Type.Simple; bg.color = Color.white;
        var p = pane.transform;
        // #shop h2: "Beach shop", 26px #ffdf2e with a hard 2px 2px #b8641c shadow
        var title = UiKit.Label(p, "Beach shop", 26, ShopArt.TitleGold, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(Side, -TitleY), new Vector2(260, 34), TextAnchor.MiddleLeft, 1f);
        ShopArt.Shade(title, ShopArt.TitleShade, 1f, new Vector2(2f, -2f));
        // #shopCoins: the count in 19px cream, and the shell 1em square after it, flush right
        tCoins = UiKit.Label(p, "0", 19, ShopArt.Cream, FontStyle.Bold, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-(Side + 1.9f + 19f + 2.7f), -CoinY), new Vector2(160, 26), TextAnchor.MiddleRight, 0f);
        var shell = UiKit.Icon("puka");
        if (shell != null) { var im = UiKit.Pic(p, "puka", shell, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-(Side + 1.9f), -CoinY), new Vector2(19, 19)); im.raycastTarget = false; }
        // #shopLv: 16px cream at .92 in a hairline pill, parked 12px off the purse -- together they
        // answer "can I have this" (sized to its text in Header)
        lvRt = UiKit.Rect(p, "level", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-120f, -LvY), new Vector2(52f, LvH));
        lvArt = ShopArt.Art(lvRt, ShopArt.Pill(52f, LvH, ShopArt.Glass.Ring, SheetAt(LvY)));
        tLv = UiKit.Label(lvRt, "Lv 1", 16, ShopArt.Mix(SheetAt(LvY), ShopArt.Cream, 0.92f), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 0f);
        // #shopTabs, in the web's order: School (which hands over to Board school -- what these
        // shapes ARE, which a shop sorted by price cannot say), Boards, Surfers, Style, Tricks
        for (int i = 0; i < TabIds.Length; i++)
        {
            string tid = TabIds[i];
            var th = ShopArt.Hit(p, "tab:" + tid, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(Side + i * (TabW + TabGap), -TabTop), new Vector2(TabW, TabH));
            tabArt[i] = ShopArt.Art(th.transform, TabSprite(false));
            tabLabel[i] = UiKit.Label(th.transform, TabNames[i], 13, TabText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 0f);
            ShopArt.Shade(tabLabel[i], ShopArt.TextShade, 0.45f, new Vector2(0f, -1f));      // text-shadow 0 1px 3px rgba(0,18,42,.6)
            Tracking.On(tabLabel[i], 0.3f / 13f);                                             // letter-spacing .3px
            UiKit.Pressable(th.gameObject, null, () => Pick(tid));
        }
        // #shopClose: "< Back" on the deep-water glass, a gold ring round it
        var back = ShopArt.Hit(p, "back", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, BackFoot), new Vector2(BackW, BackH));
        ShopArt.Art(back.transform, ShopArt.Pill(BackW, BackH, ShopArt.Glass.Calm, SheetAt(RefH - BackFoot - BackH * 0.5f)));
        ShopArt.ChevronLabel(back.transform, "Back", 16, Color.white);
        UiKit.Pressable(back.gameObject, null, () => { Show(false); onClose?.Invoke(); });
        // the shelf: a scrolling grid whose first and last 30-odd px fade, so a tile leaving the
        // view dissolves into the structure rather than being cut at a line
        view = UiKit.Rect(p, "view", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        view.offsetMin = new Vector2(0f, ListBottom); view.offsetMax = new Vector2(0f, -ListTop);
        var mask = view.gameObject.AddComponent<RectMask2D>(); mask.softness = new Vector2Int(0, 32);
        var hit = view.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);     // a drag in a gap scrolls too
        scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        content = UiKit.Rect(view, "content", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
        scroll.content = content; scroll.viewport = view;
        pane.SetActive(false);
        BuildSlotPick(canvas);
        // the full-screen look, over everything, coming back here on Back
        viewer = gameObject.AddComponent<Viewer>(); viewer.Build(canvas);
        viewer.onChange = () => onChange?.Invoke();
        viewer.onClose = () => { pane.SetActive(true); Fill(true); };
    }

    /// a tab tapped: School closes the shop and opens Board school (the web's own hand-over);
    /// the others change the shelf
    void Pick(string id)
    {
        if (id == "school") { Sfx.Buzz(8); Show(false); onGuide?.Invoke(); return; }
        if (id == tab) return;
        tab = id; Fill();
    }

    /// the purse and the level badge: the count flush right, the badge sized to its text and
    /// parked 12px to the left of it
    void Header()
    {
        tCoins.text = SaveData.Coins.ToString("N0");
        tLv.text = "Lv " + Progress.Level;
        float coinsW = tCoins.preferredWidth + 2.7f + 19f + 1.9f;
        float lvW = Mathf.Ceil(tLv.preferredWidth) + 18f + 2f;          // padding 1px 9px, a 1px ring
        lvRt.sizeDelta = new Vector2(lvW, LvH);
        lvRt.anchoredPosition = new Vector2(-(Side + coinsW + 12f), -LvY);
        lvArt.sprite = ShopArt.Pill(lvW, LvH, ShopArt.Glass.Ring, SheetAt(LvY));
    }

    public void Show(bool on)
    {
        if (!on && viewer != null && viewer.Open) viewer.Hide();
        CloseSlotPick();
        pane.SetActive(on);
        if (on) Fill();
    }

    /// opens straight onto one shelf: "boards", "riders", "gear" or "tricks"
    public void ShowOn(string which) { tab = which == "school" || string.IsNullOrEmpty(which) ? "boards" : which; Show(true); }

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
        Header();
        for (int i = 0; i < TabIds.Length; i++)
        {
            bool sel = TabIds[i] == tab;
            tabArt[i].sprite = TabSprite(sel);
            tabLabel[i].color = sel ? Color.white : TabText;
        }
        if (tab == "tricks")
            // the trick shelf (drawTrickShelf, web 13223), in the table's order with the free five
            // first and no heading (the tab is the heading). The foot says which button it is on,
            // that it is owned, or what it costs; one not owned yet is dimmed (.card.lk)
            foreach (var T in TrickCatalog.All)
            {
                bool have = TrickCatalog.Have(T.id); int slot = TrickCatalog.SlotOf(T.id);
                if (slot >= 0) Add("trick", T.id, T.name, "BUTTON " + (slot + 1), PrHave, true, false);
                else if (have) Add("trick", T.id, T.name, "OWNED", PrHave, false, false);
                else Add("trick", T.id, T.name, T.price.ToString("N0"), PrPrice, false, true);
            }
        else if (tab == "boards")
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
        // #bfe6ee at .72, set opaque at what it shows over the sheet (a translucent label blends
        // brighter in this project's linear space than in the browser)
        var col = ShopArt.Mix(SheetAt(ListTop + 20f), ShopArt.Rgb(0xbf, 0xe6, 0xee), 0.72f);
        var lb = UiKit.Label(content, title.ToUpperInvariant(), 12, col, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 15), TextAnchor.LowerLeft, 0.4f);
        Tracking.On(lb, 0.12f);                                                           // letter-spacing .12em
        tiles.Add(new Tile { heading = true, rt = lb.rectTransform });
    }

    /// .none: a shelf's empty state -- 13px at .6, 10px above and below
    void Note(string text)
    {
        var col = ShopArt.Mix(SheetAt(ListTop + 40f), Color.white, 0.6f);
        var lb = UiKit.Label(content, text, 13, col, FontStyle.Normal, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, NoteH), TextAnchor.MiddleLeft, 0f);
        lb.horizontalOverflow = HorizontalWrapMode.Wrap;
        tiles.Add(new Tile { heading = true, note = true, rt = lb.rectTransform });
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
        if (kind == "trick")
        {
            // a trick has nothing to photograph: its picture is drawn (trickThumb, web 6557), the
            // icon large on the water, loaded rather than queued -- the _lk one is .card.lk's
            // saturate(.55). The tap buys or opens the slot picker; there is no viewer for a trick
            var tx = Resources.Load<Texture2D>("UI/trick_" + id + (muted ? "_lk" : ""));
            if (tx == null && muted) tx = Resources.Load<Texture2D>("UI/trick_" + id);
            if (tx != null) { t.thumb.texture = tx; t.thumb.color = Color.white; t.thumb.uvRect = TrickCover; }
            t.done = true;
            Tappable(fr.gameObject, () => TrickTap(id));
            tiles.Add(t);
            return;
        }
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
            if (t.note)
            {
                if (rowOpen) { y += ch; rowOpen = false; }
                t.rt.anchoredPosition = new Vector2(PadX + 2f, -y); t.rt.sizeDelta = new Vector2(inner - 4f, NoteH);
                t.y = y; t.h = NoteH;
                y += NoteH; any = true;
                continue;
            }
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
        // the phone's back closes the slot picker, as the viewer's does (Escape is Android's back)
        if (pick != null && pick.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CloseSlotPick();
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

    // ---- the trick shelf's tap, and the slot picker ----
    /// web 13242-13248: owned opens the picker; not owned and affordable is bought there and then
    /// and the picker follows, because bought is halfway -- it still has to go on a button
    void TrickTap(string id)
    {
        var T = TrickCatalog.Get(id); if (T == null) return;
        if (TrickCatalog.Have(id)) { Sfx.Buzz(10); OpenSlotPick(id); return; }
        if (SaveData.Coins < T.price) { Sfx.Buzz(6); return; }      // a tile that says no still answers the finger
        if (!TrickCatalog.Buy(id)) return;
        Sfx.Buy();                                                   // noteBuy's chime
        Fill(true);
        OpenSlotPick(id);
    }

    // #slotPick (web 2281-2300, markup 3240): the sheet dimmed under rgba(4,18,36,.62); a box
    // min(100%, 360px) wide, 22px corners, padding 18 16 14, the deep-water gradient with a
    // hairline rim; "Put <name> on button" in 15px with the name gold; five buttons in a row 6px
    // apart, each the slot's number, the gesture of what is on it now and its short name, the
    // one already holding this trick ringed gold; and Cancel on .go.calm under them. A sibling
    // of the shop's sheet rather than a child of it, as the web's is, so it covers the tabs too.
    GameObject pick; Text pickTitle; RectTransform pickRow; string pickId;
    // measured on the page: the row 75 tall from 48, Cancel the .go's 42 from 135, the box 192
    const float PickW = 360f, PickPadX = 16f, PickTitleY = 27f, PickRowTop = 48f, PickBtnH = 75f, PickBtnGap = 6f;
    const float PickCancelTop = 135f, PickCancelW = 302f, PickCancelH = 42f, PickH = 192f;
    static readonly Color PickTop = ShopArt.Rgb(14, 52, 86), PickBot = ShopArt.Rgb(9, 33, 60);     // .spbox, at .97 (taken as opaque)
    /// the box's colour y layout pixels down from its top: what every button on it is composited over
    static Color PickAt(float y) => Color.Lerp(PickTop, PickBot, Mathf.Clamp01(y / PickH));

    void BuildSlotPick(Transform canvas)
    {
        var c = new Vector2(0.5f, 0.5f);
        var root = UiKit.Rect(canvas, "slotPick", Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        pick = root.gameObject;
        // the dim, which is also the way out: a tap outside the box closes it (web 13269)
        var dimC = ShopArt.Rgb(4, 18, 36);
        var dim = UiKit.Rect(root, "dim", Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        dim.color = new Color(dimC.r, dimC.g, dimC.b, ShopArt.LinA(0.62f, dimC, SheetAt(RefH * 0.5f)));
        dim.raycastTarget = true;
        var et = dim.gameObject.AddComponent<EventTrigger>();
        UiKit.Add(et, EventTriggerType.PointerClick, _ => CloseSlotPick());
        // the box: a sibling of the dim, so a tap on it is not a tap on the dim
        var box = UiKit.Rect(root, "box", c, c, c, Vector2.zero, new Vector2(PickW, PickH));
        // box-shadow 0 18px 50px rgba(0,10,30,.55): the soft sprite at five times its size
        var sh = UiKit.Pic(box, "shadow", Viewer.Soft(), Vector2.zero, Vector2.one, c, new Vector2(0f, -18f), new Vector2(Viewer.SoftPad * 10f, Viewer.SoftPad * 10f));
        sh.type = Image.Type.Sliced; sh.pixelsPerUnitMultiplier = 0.2f; sh.color = new Color(0f, 10 / 255f, 30 / 255f, 0.55f); sh.raycastTarget = false;
        var face = UiKit.Pic(box, "face", Solid.VGradient(PickW, PickH, 22f, PickTop, PickBot, new Color(1f, 1f, 1f, 0.22f), 1.5f), Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        face.raycastTarget = true;
        // .sptitle: 15px bold white, the trick's name in <b> at #ffdf2e
        pickTitle = UiKit.Label(box, "", 15, Color.white, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), c, new Vector2(0f, -PickTitleY), new Vector2(PickW - 2f * PickPadX, 20f), TextAnchor.MiddleCenter, 0f);
        pickTitle.supportRichText = true;
        Tracking.On(pickTitle, 0.3f / 15f);                                               // letter-spacing .3px
        pickRow = UiKit.Rect(box, "row", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, -PickRowTop), new Vector2(PickW - 2f * PickPadX, PickBtnH));
        // #spClose: .go.calm, min(302px, 80vw) wide, "Cancel"
        var cancel = ShopArt.Hit(box, "cancel", new Vector2(0.5f, 1), new Vector2(0.5f, 1), c, new Vector2(0f, -(PickCancelTop + PickCancelH * 0.5f)), new Vector2(PickCancelW, PickCancelH));
        ShopArt.Art(cancel.transform, ShopArt.Pill(PickCancelW, PickCancelH, ShopArt.Glass.Calm, PickAt(PickCancelTop + PickCancelH * 0.5f)));
        var cl = UiKit.Label(cancel.transform, "Cancel", 16, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 0f);
        ShopArt.Shade(cl, ShopArt.TextShade, 0.6f, new Vector2(0f, -1f));
        Tracking.On(cl, 0.6f / 16f);                                                      // .go: letter-spacing .6px
        UiKit.Pressable(cancel.gameObject, null, () => { Sfx.Buzz(6); CloseSlotPick(); });
        pick.SetActive(false);
    }

    /// openSlotPick (web 13253): the row is rebuilt each time, showing what is on each button now
    void OpenSlotPick(string id)
    {
        var T = TrickCatalog.Get(id); if (T == null || pick == null) return;
        pickId = id;
        pickTitle.text = "Put <color=#ffdf2e>" + T.name + "</color> on button";
        foreach (Transform ch in pickRow) { ch.gameObject.SetActive(false); Destroy(ch.gameObject); }
        float rowW = PickW - 2f * PickPadX, bw = (rowW - (TrickCatalog.SlotCount - 1) * PickBtnGap) / TrickCatalog.SlotCount;
        Color back = PickAt(PickRowTop + PickBtnH * 0.5f);
        for (int i = 0; i < TrickCatalog.SlotCount; i++)
        {
            var cur = TrickCatalog.Get(TrickCatalog.Slot(i)) ?? TrickCatalog.Get(TrickCatalog.DefSlots[i]);
            bool isCur = cur.id == id;
            // .sprow button: rgba(255,255,255,.09) in a 1.5px rgba(255,255,255,.26) rim, 14px
            // corners; .cur: rgba(255,223,46,.16) in a #ffdf2e rim -- composited opaque over the box
            Color fill = isCur ? ShopArt.Mix(back, HaveCol, 0.16f) : ShopArt.Mix(back, Color.white, 0.09f);
            Color rim = isCur ? HaveCol : new Color(1f, 1f, 1f, 0.26f);
            var b = UiKit.Pic(pickRow, "slot" + (i + 1), Solid.VGradient(bw, PickBtnH, 14f, fill, fill, rim, 1.5f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * (bw + PickBtnGap), 0f), new Vector2(bw, PickBtnH));
            b.raycastTarget = true;
            // padding 9 2 7, a column 2px apart: the number (17px, line-height 1), the 26px
            // gesture, the short name (8.5px at .85, cut with an ellipsis)
            var c = new Vector2(0.5f, 1);
            UiKit.Label(b.transform, (i + 1).ToString(), 17, TabText, FontStyle.Bold, c, c, new Vector2(0.5f, 0.5f), new Vector2(0f, -18.5f), new Vector2(bw, 20f), TextAnchor.MiddleCenter, 0f);
            var sp = UiKit.Icon("ges_" + cur.id);
            if (sp != null)
            {
                var g = UiKit.Pic(b.transform, "ges", sp, c, c, new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(26f, 26f));
                g.raycastTarget = false; g.preserveAspect = true;
            }
            var w = UiKit.Label(b.transform, cur.lab, 8.5f, ShopArt.Mix(fill, TabText, 0.85f), FontStyle.Bold, c, c, new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(bw - 4f, 12f), TextAnchor.MiddleCenter, 0f);
            Ellipsize(w, cur.lab, bw - 4f - Tracking.Extra(cur.lab, 8.5f, 0.5f / 8.5f));
            Tracking.On(w, 0.5f / 8.5f);                                                  // letter-spacing .5px
            int slot = i;
            UiKit.Pressable(b.gameObject, null, () => { Sfx.Buzz(10); AssignSlot(slot); });
        }
        pick.SetActive(true);
    }

    /// assignTrick's tail (web 13277-13278): the chime of putting something on, the save, and
    /// the shelf redrawn with the new BUTTON numbers. The ride's buttons read the slots when the
    /// next run starts (Hud.ApplyTrickSlots).
    void AssignSlot(int i)
    {
        if (string.IsNullOrEmpty(pickId)) return;
        TrickCatalog.Assign(pickId, i);
        Sfx.Ui("eqstyle", 0.5f);
        CloseSlotPick();
        Fill(true);
    }

    void CloseSlotPick() { if (pick != null) pick.SetActive(false); pickId = null; }

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
