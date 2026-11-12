// The full-screen look, the web's #viewer (openViewer ~12648, renderViewer, vShowBuy, the CSS at
// ~1785-1900): the item itself, live, standing upright on a beach -- sand, the sea behind it, palms
// either side, built on the viewer's own stage (Previews.BuildBeach) as the web builds its set -- and turning
// one revolution every fifteen seconds; a drag turns it by hand and it carries on from wherever it
// was left. The name and the line under it sit on a wash at the top; at the bottom are the four
// stat bars with the change from the board you ride (or the rider's perk bar), the one button --
// buy, ride, wear, or the level that opens it -- and Back. Long neon arrows down each side walk
// the shelf it was opened from. The object is framed into the clear band between the type at the
// top and the plate at the bottom, measured off the chrome rather than guessed.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Viewer : MonoBehaviour
{
    public System.Action onChange;          // something was bought, ridden or worn
    public System.Action onClose;           // Back: the shop goes up again
    public bool Open => root != null && root.activeSelf;

    const float Fov = 36f, TurnRate = 0.42f, DragK = 4.6f;     // viewCam's fov; one turn in fifteen seconds; a phone's width is three quarters of a turn
    // .go: min(302px, 80vw) by 42 (11px padding round a 16px line and the 1.5px ring); 8px
    // between the two; the sheet's 18px bottom padding; #vStats' 16px margin under the plate
    const float GoW = 302f, GoH = 42f, Gap = 8f, Foot = 18f, PlateGap = 16f;
    const float GoTrack = 0.6f / 16f;           // .go's letter-spacing .6px on its 16px
    static readonly Color ClearCol = Previews.Horizon;
    // what the bottom block sits on: the water in front of the sand's near edge -- and that water
    // as the buttons' backdrop blur and saturate(135%) hand it to their glass
    static readonly Color Water = ShopArt.Rgb(11, 76, 110);
    static readonly Color GlassBack = ShopArt.Rgb(14, 82, 120);            // matched to the reference's Back and RIDING
    static readonly Color PlateCol = ShopArt.Rgb(4, 15, 28);                // #vStats: rgba(4,15,28,.80)
    static Color PlateSeen => ShopArt.Mix(Water, PlateCol, 0.80f);          // the plate as the browser shows it
    static readonly string[] StatLabel = { "SPEED", "TURN", "AIR", "TOUGH" };
    static readonly string[] StatCls = { "speed", "turn", "air", "tough" };

    GameObject root; RectTransform rootRt;
    RawImage view; Image scrim; Text tName, tSub;
    RectTransform prevBtn, nextBtn; Image[] prevL, nextL;
    RectTransform plate; float plateW = 370f, plateH; float topPx;
    Image buy, buyArt; Text tBuy; Image buyPk; System.Action buyAct;

    Previews.Stage stage; Camera cam; RenderTexture rt; GameObject holder;
    string kind = "board", id = ""; List<string> list = new List<string>();
    float spin, grabSpin, grabX; bool grabbed; int grabId; float fade; int frames;
    float cy, hy, hr, bottom;

    // =====================================================================================
    public void Build(Transform canvas)
    {
        rootRt = UiKit.Rect(canvas, "viewer", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        root = rootRt.gameObject;
        var bg = root.AddComponent<Image>(); bg.color = ClearCol; bg.raycastTarget = false;
        // the turntable: the stage's render, and the surface a drag lands on
        var vrt = UiKit.Rect(rootRt, "view", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        view = vrt.gameObject.AddComponent<RawImage>(); view.color = new Color(1, 1, 1, 0); view.raycastTarget = true;
        view.uvRect = new Rect(1f, 0f, -1f, 1f);      // drawn mirrored in Unity's frame; shown the web's way round
        var et = vrt.gameObject.AddComponent<EventTrigger>();
        UiKit.Add(et, EventTriggerType.PointerDown, e => Grab(e as PointerEventData));
        UiKit.Add(et, EventTriggerType.Drag, e => DragTo(e as PointerEventData));
        UiKit.Add(et, EventTriggerType.PointerUp, e => LetGo(e as PointerEventData));
        // the wash behind the name, heavy at the top and gone 64px past the last line
        scrim = UiKit.Pic(rootRt, "scrim", ScrimSprite(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 160));
        scrim.raycastTarget = false;
        // .vname: 26px, 800, white over three navy shadows; .vsub: 13px at .92, line-height 1.4
        tName = UiKit.Label(rootRt, "", 26, Color.white, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(398, 32), TextAnchor.UpperCenter, 0.95f);
        tName.horizontalOverflow = HorizontalWrapMode.Wrap;
        ShopArt.Shade(tName, ShopArt.Rgb(0, 22, 48), 0.95f, new Vector2(0f, -2f));
        tSub = UiKit.Label(rootRt, "", 13, new Color(1, 1, 1, 0.92f), FontStyle.Normal, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(398, 40), TextAnchor.UpperCenter, 0.95f);
        tSub.horizontalOverflow = HorizontalWrapMode.Wrap; tSub.lineSpacing = 1.2f;      // 1.4 of the font size is 1.2 of Trebuchet's own line
        ShopArt.Shade(tSub, ShopArt.Rgb(0, 22, 48), 0.9f, new Vector2(0f, -1f));
        // the arrows, in off the bezel by 18px
        prevBtn = Arrow(rootRt, false, out prevL);
        nextBtn = Arrow(rootRt, true, out nextL);
        // .vbot: Back (.go.alt with the chevron), above it the one button, above that the plate
        var back = ShopArt.Hit(rootRt, "back", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, Foot), new Vector2(GoW, GoH));
        ShopArt.Art(back.transform, AltGo());
        ShopArt.ChevronLabel(back.transform, "Back", 16, Color.white);
        UiKit.Pressable(back.gameObject, null, Close);
        buy = ShopArt.Hit(rootRt, "buy", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, Foot + GoH + Gap), new Vector2(GoW, GoH));
        buyArt = ShopArt.Art(buy.transform, LitGo());
        tBuy = UiKit.Label(buy.transform, "", 16, Color.white, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270, 24), TextAnchor.MiddleCenter, 0f);
        ShopArt.Shade(tBuy, ShopArt.TextShade, 0.6f, new Vector2(0f, -1f));
        Tracking.On(tBuy, GoTrack);
        var pk = UiKit.Icon("puka");
        buyPk = UiKit.Pic(buy.transform, "puka", pk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14));
        buyPk.raycastTarget = false; buyPk.gameObject.SetActive(false);
        UiKit.Pressable(buy.gameObject, null, () => { if (buyAct != null) buyAct(); });
        plate = UiKit.Rect(rootRt, "stats", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, Foot + GoH + Gap + GoH + PlateGap), new Vector2(plateW, 100));
        root.SetActive(false);
    }

    /// opens on one item; `items` is the shelf it came from, in the shelf's order, for the arrows
    public void Show(string itemKind, string itemId, List<string> items)
    {
        list = items ?? new List<string>();
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        Sfx.Buzz(8);
        Load(itemKind, itemId);
    }

    /// Back: put away, and the shop told
    public void Close()
    {
        if (!Open) return;
        Hide(); Sfx.Buzz(8);
        onClose?.Invoke();
    }

    /// put away without telling anyone (the shop itself is closing)
    public void Hide()
    {
        if (holder != null) { Previews.Despawn(holder); holder = null; }
        if (rt != null) { if (cam != null) cam.targetTexture = null; view.texture = null; rt.Release(); Destroy(rt); rt = null; }
        if (root != null) root.SetActive(false);
    }

    // ---- one item ----
    void Load(string itemKind, string itemId)
    {
        if (holder != null) { Previews.Despawn(holder); holder = null; }
        kind = itemKind; id = itemId;
        // the stage is the beach (Previews.BuildBeach); its sky wall stands 330 set units out, so the lens reaches far
        if (stage == null) { stage = new Previews.Stage("ViewerStage", Previews.ViewHome, true); cam = stage.NewCamera(Fov, 6000f, ClearCol); }
        try { holder = Previews.Spawn(stage, kind, id); }
        catch (System.Exception ex) { Debug.LogWarning("Viewer: could not build " + kind + " " + id + ": " + ex.Message); holder = null; }
        if (holder != null)
        {
            // measured stood up and square, about the axis it turns on, which holds at every yaw
            holder.transform.localRotation = Quaternion.identity;
            Previews.Measure(holder, out cy, out hy, out hr, out bottom);
        }
        // a board opens rail-on, a rider opens on his face
        spin = kind == "board" ? -Mathf.PI * 0.5f : Mathf.PI;
        grabbed = false; fade = 0f; frames = 0; view.color = new Color(1, 1, 1, 0);
        string nm = "", sub = "";
        if (kind == "board") { var b = BoardCatalog.Get(id); nm = b.name; sub = b.blurb; }
        else if (kind == "rider") { var r = RiderCatalog.Get(id); nm = r.name; sub = r.blurb; }
        else { var g = GearCatalog.Get(id); if (g != null) { nm = g.name; sub = g.blurb; } }
        tName.text = nm; tSub.text = sub ?? "";
        DrawStats(); DrawButton(); SetArrows(); Bands();
    }

    void Step(int dir)
    {
        int i = list.IndexOf(id), j = i + dir;
        if (i < 0 || j < 0 || j >= list.Count) return;
        Sfx.Buzz(6);
        Load(kind, list[j]);
    }

    void SetArrows()
    {
        int i = list.IndexOf(id);
        // nothing before the first or after the last, so the arrow goes rather than sitting there greyed out
        prevBtn.gameObject.SetActive(i > 0);
        nextBtn.gameObject.SetActive(i >= 0 && i < list.Count - 1);
    }

    /// the top block's height, which is where the clear band starts
    void Bands()
    {
        float w = Mathf.Min(520f, Mathf.Max(200f, rootRt.rect.width - 32f));
        tName.rectTransform.sizeDelta = new Vector2(w, 32);
        float nameH = Mathf.Max(30f, tName.preferredHeight);
        tName.rectTransform.sizeDelta = new Vector2(w, nameH);
        tSub.rectTransform.sizeDelta = new Vector2(w, 40);
        tSub.rectTransform.anchoredPosition = new Vector2(0, -16f - nameH - 4f);
        float subH = string.IsNullOrEmpty(tSub.text) ? 0f : tSub.preferredHeight;
        tSub.rectTransform.sizeDelta = new Vector2(w, subH);
        topPx = 16f + nameH + 4f + subH + 64f;
        scrim.rectTransform.sizeDelta = new Vector2(0, topPx);
        float ah = Mathf.Clamp(rootRt.rect.height * 0.28f, 168f, 240f);     // clamp(168px, 28vh, 240px)
        prevBtn.sizeDelta = new Vector2(46, ah); nextBtn.sizeDelta = new Vector2(46, ah);
    }

    // =====================================================================================
    // ---- the plate: statDelta for a board, perkBars for a rider ----
    void DrawStats()
    {
        Pane.Clear(plate);
        plateH = 0f;
        plateW = Mathf.Max(200f, Mathf.Min(520f, rootRt.rect.width - 32f) - 28f);   // .vbot is min(520px, 100%) with 14px either side
        // #vStats: a 1px rim, padding 13 15 11, rows 5px apart (.st's margins collapse between
        // them). A row is as tall as what is in it: the 10px label's line (11.5) when the board
        // is the one ridden and the change column is empty, the 12px change's (14) when it is not
        // -- measured on the page, 16px a row against 19, which is why the plate is shorter on
        // the board you ride.
        if (kind == "board")
        {
            var b = BoardCatalog.Get(id); string eq = SaveData.S.equipped;
            var c = id == eq ? null : BoardCatalog.Get(eq);
            int[] v = { b.speed, b.turn, b.air, b.tough };
            int[] cv = c == null ? null : new[] { c.speed, c.turn, c.air, c.tough };
            float rh = cv == null ? LabelLine : DeltaLine;
            plateH = PlateTop + 4 * rh + 3 * 5f + PlateFoot;
            PlateBack();
            for (int r = 0; r < 4; r++)
            {
                string dt = ""; Color dc = Color.white;
                if (cv != null)
                {
                    int d = v[r] - cv[r];
                    if (d > 0) { dt = "+" + d; dc = Previews.Hex(0x7ff0dd); }
                    else if (d < 0) { dt = d.ToString(); dc = Previews.Hex(0xffab8e); }
                    else { dt = "·"; dc = ShopArt.Mix(PlateSeen, Color.white, 0.34f); }
                }
                Row(PlateTop + r * (rh + 5f), rh, StatLabel[r], StatColor(StatCls[r]), v[r], true, dt, dc);
            }
        }
        else if (kind == "rider")
        {
            string key, text; float pv;
            if (Perk(id, out key, out pv, out text))
            {
                string cls, label; PerkRow(key, out cls, out label);
                // a perk of 1.10 is ten percent, and ten percent of the bar is one segment
                int n = key == "lives" ? 3 : Mathf.Clamp(Rungs.JsRound((pv - 1.0) * 100.0 / 3.5), 1, 10);
                // the one row, and under it .perkTx -- which the stylesheet only styles inside #shop,
                // so in the viewer it is the page's own 16px white, an 18px line, flush left
                plateH = PlateTop + LabelLine + 5f + 18f + 11f + 1f;          // no row margin under .perkTx: the padding and the rim
                PlateBack();
                Row(PlateTop, LabelLine, label, StatColor(cls), n, false, "", Color.white);
                var tx = UiKit.Label(plate, text, 16, Color.white, FontStyle.Normal, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -(PlateTop + LabelLine + 5f + 9f)), new Vector2(-32, 18), TextAnchor.MiddleLeft, 0.4f);
                tx.raycastTarget = false;
            }
            else
            {
                // "Perk" and .perkNone's "The honest baseline" -- again unstyled out here: 16px, bold, white
                float rh = 18.5f;
                plateH = PlateTop + rh + PlateFoot;
                PlateBack();
                var pl = UiKit.Label(plate, "Perk", 10, ShopArt.Mix(PlateSeen, Color.white, 0.7f), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(PlateIn, -(PlateTop + rh * 0.5f)), new Vector2(48, 14), TextAnchor.MiddleRight, 0.4f);
                Tracking.On(pl, 0.1f);
                UiKit.Label(plate, "The honest baseline", 16, Color.white, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(PlateIn + 48f + 8f, -(PlateTop + rh * 0.5f)), new Vector2(280, 20), TextAnchor.MiddleLeft, 0.4f);
            }
        }
        plate.gameObject.SetActive(plateH > 0f);
        if (plateH > 0f) plate.sizeDelta = new Vector2(plateW, plateH);
    }

    // The plate's translucent layers are given the alpha that blends, in this project's linear
    // space, to what the browser's sRGB blend shows over the same water (ShopArt.LinA); at the
    // stylesheet's own alphas the plate came out grey and its empty segments near white.
    void PlateBack()
    {
        // box-shadow 0 10px 26px rgba(0,14,28,.42)
        float p2 = SoftPad * 2f;
        var sh = UiKit.Pic(plate, "shadow", Soft(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(p2 + 10f, p2 + 10f));
        var shC = ShopArt.Rgb(0, 14, 28);
        sh.type = Image.Type.Sliced; sh.color = new Color(shC.r, shC.g, shC.b, ShopArt.LinA(0.42f, shC, Water)); sh.raycastTarget = false;
        var bgi = Solid.Pic(plate, "plate", Solid.Rounded(plateW, plateH, 18f), new Color(PlateCol.r, PlateCol.g, PlateCol.b, ShopArt.LinA(0.80f, PlateCol, Water)), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bgi.raycastTarget = false;
        var rim = Solid.Pic(plate, "rim", Solid.Rounded(plateW, plateH, 18f, 1f), new Color(1, 1, 1, ShopArt.LinA(0.13f, Color.white, PlateSeen)), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rim.raycastTarget = false;
    }

    // the plate's insides, measured on the page: 1px rim + 13px padding + the first row's 5px
    // margin over the first row, 5 + 11 + 1 under the last; 1 + 15 in from either side
    const float PlateTop = 19f, PlateFoot = 17f, PlateIn = 16f, LabelLine = 11.5f, DeltaLine = 14f;

    /// one row `h` tall at `top` from the plate's top: the label right-aligned in 48px (10px caps
    /// at .7, tracked .1em), ten segments 9px tall centred on the row, and the change in 26px
    void Row(float top, float h, string label, Color col, int n, bool withDelta, string dTxt, Color dCol)
    {
        float cy = top + h * 0.5f, segY = cy - 4.5f;
        var lb = UiKit.Label(plate, label, 10, ShopArt.Mix(PlateSeen, Color.white, 0.7f), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(PlateIn, -cy), new Vector2(48, 14), TextAnchor.MiddleRight, 0.4f);
        Tracking.On(lb, 0.1f);
        float x0 = PlateIn + 48f + 8f, x1 = plateW - PlateIn - (withDelta ? 8f + 26f : 0f);
        float sw = Mathf.Max(2f, (x1 - x0 - 9 * 3f) / 10f);
        n = Mathf.Clamp(n, 0, 10);
        if (n > 0)
        {
            // box-shadow 0 0 8px currentColor, one soft light under the lit run
            float runW = n * sw + (n - 1) * 3f;
            var gl = UiKit.Pic(plate, "glow", Soft(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0 - SoftPad, -segY + SoftPad), new Vector2(runW + SoftPad * 2f, 9f + SoftPad * 2f));
            gl.type = Image.Type.Sliced; gl.color = new Color(col.r, col.g, col.b, ShopArt.LinA(0.45f, col, PlateSeen)); gl.raycastTarget = false;
        }
        var segSprite = Solid.Rounded(sw, 9f, 2f);
        var segOff = new Color(1, 1, 1, ShopArt.LinA(0.14f, Color.white, PlateSeen));
        for (int i = 0; i < 10; i++)
        {
            var s = Solid.Pic(plate, "seg", segSprite, i < n ? col : segOff, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0 + i * (sw + 3f), -segY), new Vector2(sw, 9f));
            s.raycastTarget = false;
        }
        if (withDelta) UiKit.Label(plate, dTxt, 12, dCol, FontStyle.Bold, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-PlateIn, -cy), new Vector2(26, 14), TextAnchor.MiddleRight, 0.4f);
    }

    static Color StatColor(string cls)
    {
        switch (cls)
        {
            case "turn": return Previews.Hex(0x4fd8e8);
            case "air": return Previews.Hex(0xffd83c);
            case "tough": return Previews.Hex(0x6ee88a);
            default: return Previews.Hex(0xff9a3c);
        }
    }

    // the web's RIDER_PERK and PERK_ROW
    static bool Perk(string rid, out string key, out float v, out string text)
    {
        key = ""; v = 1f; text = "";
        switch (rid)
        {
            case "cat": key = "trick"; v = 1.10f; text = "+10% trick points"; return true;
            case "pig": key = "float"; v = 1.12f; text = "Floats higher"; return true;
            case "otter": key = "speed"; v = 1.06f; text = "A knot faster"; return true;
            case "penguin": key = "grip"; v = 1.12f; text = "Rails that hold"; return true;
            case "frog": key = "air"; v = 1.12f; text = "Extra pop"; return true;
            case "monkey": key = "air"; v = 1.10f; text = "Springy legs"; return true;
            case "rat": key = "float"; v = 1.10f; text = "Buoyant belly"; return true;
            case "panda": key = "lives"; v = 1f; text = "One extra life"; return true;
            case "bunny": key = "air"; v = 1.12f; text = "Extra pop"; return true;
            case "chicken": key = "luck"; v = 1.25f; text = "Chests come oftener"; return true;
            case "corgi": key = "speed"; v = 1.05f; text = "Low and fast"; return true;
            case "raccoon": key = "luck"; v = 1.35f; text = "Treasure finder"; return true;
            case "squirrel": key = "trick"; v = 1.08f; text = "+8% trick points"; return true;
            case "sloth": key = "grip"; v = 1.15f; text = "Unhurried grip"; return true;
            case "lizard": key = "grip"; v = 1.10f; text = "Sticky feet"; return true;
            case "alien": key = "trick"; v = 1.12f; text = "+12% trick points"; return true;
            case "angler": key = "luck"; v = 1.20f; text = "A nose for gold"; return true;
            case "ant": key = "float"; v = 1.08f; text = "Impossibly buoyant"; return true;
            case "porcupine": key = "lives"; v = 1f; text = "One extra life"; return true;
            case "rex": key = "air"; v = 1.08f; text = "Big push-off"; return true;
        }
        return false;
    }
    static void PerkRow(string key, out string cls, out string label)
    {
        switch (key)
        {
            case "speed": cls = "speed"; label = "Speed"; return;
            case "air": cls = "air"; label = "Pop"; return;
            case "grip": cls = "turn"; label = "Grip"; return;
            case "float": cls = "tough"; label = "Float"; return;
            case "trick": cls = "air"; label = "Tricks"; return;
            case "luck": cls = "speed"; label = "Luck"; return;
            case "lives": cls = "tough"; label = "Lives"; return;
        }
        cls = "speed"; label = "Perk";
    }

    // =====================================================================================
    // ---- the one button: the level is asked before the purse, because it is the harder no ----
    static string N(int v) => v.ToString("N0");

    void DrawButton()
    {
        buyAct = null; string label = ""; bool lit = false, pk = false;
        string iid = id;
        if (kind == "board")
        {
            var b = BoardCatalog.Get(iid);
            bool have = SaveData.Owns(iid), on = SaveData.S.equipped == iid, locked = Ladder.BoardLocked(iid);
            if (on) label = "RIDING";
            else if (have) { label = "Ride it"; lit = true; buyAct = () => { Sfx.Buzz(10); SaveData.S.equipped = iid; SaveData.Flush(); Sfx.EquipBoard(); Changed(); }; }
            else if (locked) label = "Level " + Ladder.BoardLvl(iid);
            else if (SaveData.Coins < b.price) { label = N(b.price - SaveData.Coins) + " more"; pk = true; }
            else
            {
                label = "Buy " + N(b.price); lit = true; pk = true;
                buyAct = () =>
                {
                    Sfx.Buzz(10);
                    if (SaveData.Owns(iid) || SaveData.Coins < b.price || Ladder.BoardLocked(iid)) return;
                    SaveData.Coins -= b.price;
                    if (!SaveData.S.owned.Contains(iid)) SaveData.S.owned.Add(iid);
                    SaveData.S.equipped = iid;
                    SaveData.Flush(); Sfx.Buy(); Sfx.Buzz(16); Changed();
                };
            }
        }
        else if (kind == "rider")
        {
            var r = RiderCatalog.Get(iid);
            bool have = SaveData.OwnsRider(iid), on = SaveData.S.rider == iid, locked = Ladder.CharLocked(iid);
            if (on) label = "RIDING";
            else if (have) { label = "Ride it"; lit = true; buyAct = () => { Sfx.Buzz(10); SaveData.S.rider = iid; SaveData.Flush(); Sfx.EquipRider(); Previews.ForgetGear(); Changed(); }; }
            else if (locked) label = "Level " + Ladder.CharLvl(iid);
            else if (SaveData.Coins < r.price) { label = N(r.price - SaveData.Coins) + " more"; pk = true; }
            else
            {
                label = "Buy " + N(r.price); lit = true; pk = true;
                buyAct = () =>
                {
                    Sfx.Buzz(10);
                    if (SaveData.OwnsRider(iid) || SaveData.Coins < r.price || Ladder.CharLocked(iid)) return;
                    SaveData.Coins -= r.price;
                    if (!SaveData.S.ownedChars.Contains(iid)) SaveData.S.ownedChars.Add(iid);
                    SaveData.S.rider = iid;
                    SaveData.Flush(); Sfx.Buy(); Sfx.Buzz(16); Previews.ForgetGear(); Changed();
                };
            }
        }
        else
        {
            // gear is not for sale: the rank grants it, or the wheel did; a worn piece comes off
            var g = GearCatalog.Get(iid);
            if (g != null)
            {
                string slot = g.slot;
                bool have = GearCatalog.Have(iid), on = have && GearCatalog.On(SaveData.S, slot) == iid;
                if (on) { label = "Take off"; buyAct = () => { Sfx.Buzz(10); GearCatalog.SetOn(SaveData.S, slot, ""); SaveData.Flush(); Sfx.EquipStyle(); Previews.ForgetRiders(); Changed(); }; }
                else if (have) { label = "Wear"; lit = true; buyAct = () => { Sfx.Buzz(10); GearCatalog.SetOn(SaveData.S, slot, iid); SaveData.Flush(); Sfx.EquipStyle(); Previews.ForgetRiders(); Changed(); }; }
                else label = "Level " + g.lv;
            }
        }
        if (buyAct == null) buyAct = () => Sfx.Buzz(6);       // a button that says no still answers the finger
        // vShowBuy: .go (the amber glass) when it does something, .go.alt otherwise; white either way
        buyArt.sprite = lit ? LitGo() : AltGo();
        tBuy.text = label;
        tBuy.color = Color.white;
        // the puka sits after the figure, the .go's 8px gap and its own 5px margin from it: the
        // pair is centred as one
        float pw = tBuy.preferredWidth + Tracking.Extra(label, 16, GoTrack), shift = pk ? (13f + 14f) * 0.5f : 0f;
        tBuy.rectTransform.anchoredPosition = new Vector2(-shift, 0);
        buyPk.gameObject.SetActive(pk && buyPk.sprite != null);
        buyPk.rectTransform.anchoredPosition = new Vector2(-shift + pw * 0.5f + 13f + 7f, 0);
    }

    void Changed()
    {
        onChange?.Invoke();
        DrawStats(); DrawButton();
    }

    static Sprite litGo, altGo;
    static Sprite LitGo() => litGo != null ? litGo : (litGo = ShopArt.Pill(GoW, GoH, ShopArt.Glass.Amber, GlassBack));
    static Sprite AltGo() => altGo != null ? altGo : (altGo = ShopArt.Pill(GoW, GoH, ShopArt.Glass.Alt, GlassBack));

    // =====================================================================================
    // ---- turn it by hand: the object follows the finger, and carries on from where it was left ----
    void Grab(PointerEventData e)
    {
        if (e == null || grabbed) return;
        grabbed = true; grabId = e.pointerId; grabX = e.position.x; grabSpin = spin;
    }
    void DragTo(PointerEventData e)
    {
        if (e == null || !grabbed || e.pointerId != grabId) return;
        // the picture is flipped back to the web's way round, so the near side follows the finger as the web's does
        spin = grabSpin + (e.position.x - grabX) * (DragK / Mathf.Max(1, Screen.width));
    }
    void LetGo(PointerEventData e)
    {
        if (e != null && grabbed && e.pointerId == grabId) grabbed = false;
    }

    void Update()
    {
        if (!Open) return;
        float dt = Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (!grabbed) spin += dt * TurnRate;
        if (holder != null) holder.transform.localRotation = Quaternion.AngleAxis(spin * Mathf.Rad2Deg, Vector3.up);
        // a couple of frames dark while a skinned body is skinned for the first time, then in
        frames++;
        if (frames > 2) fade = Mathf.Min(1f, fade + dt / 0.25f);
        view.color = new Color(1, 1, 1, fade);
        Pulse(prevL); Pulse(nextL);
    }

    void LateUpdate()
    {
        if (!Open || holder == null || cam == null) return;
        EnsureRT();
        // fit into the band between the name block and the plate (or the buttons), whichever binds:
        // the height of the object or its reach from the axis
        float Hc = Mathf.Max(1f, rootRt.rect.height);
        float bot = Hc - (plateH > 0f ? Foot + GoH + Gap + GoH + PlateGap + plateH : Foot + GoH + Gap + GoH + 40f);
        float band = Mathf.Max(0.25f, (bot - topPx) / Hc), mid = (topPx + bot) * 0.5f / Hc;
        float aspect = Mathf.Max(0.35f, rt.width / (float)rt.height);
        float t = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
        float d = Mathf.Max(hy / (t * 0.86f * band), hr / (aspect * t * 0.86f));
        d = Mathf.Max(d, 0.2f);
        float k = d / 9f;                 // the backdrop scales with the framing, so it sits the same in every frame
        stage.Backdrop(k, bottom, Mathf.Clamp(hr * 1.7f / k, 0.95f, 2.4f), 0.28f);
        float lift = (mid - 0.5f) * 2f * d * t;      // the lens AND its aim move together, level
        cam.transform.localPosition = new Vector3(0f, cy + lift, d);
        cam.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        Previews.Render(stage, cam, rt);
    }

    void EnsureRT()
    {
        int w = Mathf.Max(16, Screen.width), h = Mathf.Max(16, Screen.height);
        float s = Mathf.Min(1f, 1600f / Mathf.Max(w, h));
        w = Mathf.Max(16, Mathf.RoundToInt(w * s)); h = Mathf.Max(16, Mathf.RoundToInt(h * s));
        if (rt != null && rt.width == w && rt.height == h) return;
        if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
        rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "viewer" };
        rt.Create();
        view.texture = rt;
    }

    // =====================================================================================
    // ---- the arrows: one tapered crescent stroked three times, deep navy, cyan tube, white core ----
    RectTransform Arrow(Transform p, bool right, out Image[] layers)
    {
        float ax = right ? 1f : 0f;
        var b = UiKit.Rect(p, right ? "next" : "prev", new Vector2(ax, 0.5f), new Vector2(ax, 0.5f), new Vector2(ax, 0.5f), new Vector2(right ? -18f : 18f, 0), new Vector2(46, 200));
        var hit = b.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = true;
        // the svg is sized by its width (46px for a 40-unit viewBox); the art carries 10 units of bloom round it
        var art = UiKit.Rect(b, "art", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f * 1.15f, 100f * 1.15f));
        if (!right) art.localScale = new Vector3(-1f, 1f, 1f);
        var sp = ArrowSprites();
        Color[] tint = { new Color(20 / 255f, 90 / 255f, 150 / 255f, 0.30f), new Color(12 / 255f, 64 / 255f, 110 / 255f, 0.16f),
                         new Color(6 / 255f, 42 / 255f, 77 / 255f, 0.66f), Previews.Hex(0x2fa8e0), new Color(232 / 255f, 251 / 255f, 1f, 0.9f) };
        layers = new Image[sp.Length];
        for (int i = 0; i < sp.Length; i++)
        {
            layers[i] = UiKit.Pic(art, "l" + i, sp[i], Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            layers[i].color = tint[i]; layers[i].raycastTarget = false;
        }
        int dir = right ? 1 : -1;
        UiKit.Pressable(b.gameObject, null, () => Step(dir));
        return b;
    }

    static float Sm(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    /// varrGlow / varrTube / varrCore: a 2.6 s breath, the core a beat out of step with the bloom
    void Pulse(Image[] L)
    {
        if (L == null || L.Length < 5 || !L[0].gameObject.activeInHierarchy) return;
        float ph = (Time.time % 2.6f) / 2.6f;
        float bloom = ph < 0.5f ? Sm(ph / 0.5f) : Sm((1f - ph) / 0.5f);
        float tube = ph < 0.45f ? Sm(ph / 0.45f) : Sm((1f - ph) / 0.55f);
        float core = ph < 0.42f ? Sm(ph / 0.42f) : Sm((1f - ph) / 0.58f);
        L[0].color = Color.Lerp(new Color(20 / 255f, 90 / 255f, 150 / 255f, 0.30f), new Color(99 / 255f, 210 / 255f, 247 / 255f, 0.85f), bloom);
        L[3].color = Color.Lerp(Previews.Hex(0x1d7fb8), Previews.Hex(0x63d2f7), tube);
        L[4].color = new Color(232 / 255f, 251 / 255f, 1f, Mathf.Lerp(0.62f, 1f, core));
    }

    static Sprite[] arrowSprites;
    /// the web's path (viewBox 0 0 40 80): bloom, fill, and the three strokes 3.4 / 1.55 / 0.46 wide
    static Sprite[] ArrowSprites()
    {
        if (arrowSprites != null) return arrowSprites;
        var pts = new List<Vector2>();
        Bez(pts, new Vector2(6, 8), new Vector2(14, 18.2f), new Vector2(34, 35.8f), new Vector2(34, 40));
        Bez(pts, new Vector2(34, 40), new Vector2(34, 44.2f), new Vector2(14, 61.8f), new Vector2(6, 72));
        Bez(pts, new Vector2(6, 72), new Vector2(13, 61.8f), new Vector2(29.5f, 44.2f), new Vector2(29.5f, 40));
        Bez(pts, new Vector2(29.5f, 40), new Vector2(29.5f, 35.8f), new Vector2(13, 18.2f), new Vector2(6, 8));
        const float K = 2.3f, X0 = -10f, Y0 = -10f, CW = 60f, CH = 100f;     // 2x of the 1.15 the svg is drawn at
        int TW = Mathf.CeilToInt(CW * K), TH = Mathf.CeilToInt(CH * K);
        float[] width = { 0f, 0f, 3.4f, 1.55f, 0.46f };
        var px = new Color[5][];
        for (int L = 0; L < 5; L++) px[L] = new Color[TW * TH];
        for (int y = 0; y < TH; y++)
        for (int x = 0; x < TW; x++)
        {
            float ux = X0 + (x + 0.5f) / K, uy = Y0 + (TH - 1 - y + 0.5f) / K;      // svg y runs down, a texture's up
            float d = Dist(pts, ux, uy); bool inside = Inside(pts, ux, uy);
            int at = y * TW + x;
            float g = inside ? 1f : Mathf.Exp(-(d / 4f) * (d / 4f));
            px[0][at] = new Color(1, 1, 1, g);
            px[1][at] = new Color(1, 1, 1, inside ? 1f : Mathf.Clamp01(1f - d * K));
            for (int L = 2; L < 5; L++) px[L][at] = new Color(1, 1, 1, Mathf.Clamp01((width[L] * 0.5f - d) * K + 0.5f));
        }
        arrowSprites = new Sprite[5];
        for (int L = 0; L < 5; L++)
        {
            var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(px[L]); tex.Apply(false, true);
            arrowSprites[L] = Sprite.Create(tex, new Rect(0, 0, TW, TH), new Vector2(0.5f, 0.5f), 100f);
        }
        return arrowSprites;
    }

    static void Bez(List<Vector2> pts, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        const int n = 24;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n, m = 1f - t;
            pts.Add(m * m * m * p0 + 3f * m * m * t * p1 + 3f * m * t * t * p2 + t * t * t * p3);
        }
    }
    static float Dist(List<Vector2> pts, float x, float y)
    {
        var p = new Vector2(x, y); float best = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count], ab = b - a;
            float l2 = ab.sqrMagnitude, u = l2 > 1e-9f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            float dd = (a + ab * u - p).sqrMagnitude;
            if (dd < best) best = dd;
        }
        return Mathf.Sqrt(best);
    }
    static bool Inside(List<Vector2> pts, float x, float y)
    {
        bool c = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            Vector2 a = pts[i], b = pts[j];
            if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) c = !c;
        }
        return c;
    }

    // ---- the wash and the soft light ----
    static Sprite scrimSprite;
    static Sprite ScrimSprite()
    {
        if (scrimSprite != null) return scrimSprite;
        const int n = 64;
        var tex = new Texture2D(1, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float[] at = { 0f, 0.40f, 0.66f, 0.86f, 1f }, al = { 0.82f, 0.72f, 0.46f, 0.16f, 0f };
        // each stop at the alpha that, blended in linear, lands over the sky where the browser's does
        var wash = new Color(2 / 255f, 10 / 255f, 22 / 255f); var skyBehind = ShopArt.Rgb(165, 205, 232);
        for (int s = 0; s < al.Length; s++) al[s] = ShopArt.LinA(al[s], wash, skyBehind);
        var px = new Color[n];
        for (int i = 0; i < n; i++)
        {
            float f = 1f - (i + 0.5f) / n;            // from the top
            float a = 0f;
            for (int s = 0; s < 4; s++) if (f >= at[s] && f <= at[s + 1]) { a = Mathf.Lerp(al[s], al[s + 1], (f - at[s]) / (at[s + 1] - at[s])); break; }
            px[i] = new Color(2 / 255f, 10 / 255f, 22 / 255f, a);
        }
        tex.SetPixels(px); tex.Apply(false, true);
        scrimSprite = Sprite.Create(tex, new Rect(0, 0, 1, n), new Vector2(0.5f, 0.5f), 100f);
        return scrimSprite;
    }

    /// a soft-edged rounded rectangle for a CSS drop-shadow or glow: sliced, its solid core inset
    /// SoftPad from the sprite's edge, so a rect grown by SoftPad each side has its core on the target
    public const float SoftPad = 12f;
    static Sprite soft;
    public static Sprite Soft()
    {
        if (soft != null) return soft;
        const int S = 44; const float R = 8f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[S * S];
        float c = S * 0.5f, half = (S - 2f * SoftPad) * 0.5f, sig = SoftPad * 0.42f;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float qx = Mathf.Abs(x + 0.5f - c) - (half - R), qy = Mathf.Abs(y + 0.5f - c) - (half - R);
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - R;
            float a = d <= 0f ? 1f : Mathf.Exp(-(d / sig) * (d / sig));
            px[y * S + x] = new Color(1, 1, 1, a);
        }
        tex.SetPixels(px); tex.Apply(false, true);
        soft = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(21, 21, 21, 21));
        return soft;
    }
}
