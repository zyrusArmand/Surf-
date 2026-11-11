// The full-screen look, the web's #viewer (openViewer ~12648, renderViewer, vShowBuy, the CSS at
// ~1785-1900): the item itself, live, standing on the shop's sand-into-sky backdrop and turning
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
    const float GoW = 302f, GoH = 44f, Gap = 8f, Foot = 18f;     // .go: min(302px, 80vw); the sheet's 18px bottom padding
    static readonly Color ClearCol = Previews.Hex(0x9fd0ee);
    static readonly string[] StatLabel = { "SPEED", "TURN", "AIR", "TOUGH" };
    static readonly string[] StatCls = { "speed", "turn", "air", "tough" };

    GameObject root; RectTransform rootRt;
    RawImage view; Image scrim; Text tName, tSub;
    RectTransform prevBtn, nextBtn; Image[] prevL, nextL;
    RectTransform plate; float plateW = 370f, plateH; float topPx;
    Image buy; Text tBuy; Image buyPk; System.Action buyAct;

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
        tName = UiKit.Label(rootRt, "", 26, Color.white, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(398, 32), TextAnchor.UpperCenter, 0.95f);
        tName.horizontalOverflow = HorizontalWrapMode.Wrap;
        tSub = UiKit.Label(rootRt, "", 13, new Color(1, 1, 1, 0.92f), FontStyle.Normal, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(398, 40), TextAnchor.UpperCenter, 0.95f);
        tSub.horizontalOverflow = HorizontalWrapMode.Wrap; tSub.lineSpacing = 1.12f;
        // the arrows, in off the bezel by 18px
        prevBtn = Arrow(rootRt, false, out prevL);
        nextBtn = Arrow(rootRt, true, out nextL);
        // Back, and above it the one button, and above that the plate
        var back = UiKit.Pic(rootRt, "back", UiKit.Rounded(GoW, GoH, 22, 22, 22, 22, UiKit.Body.Pill, 0f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, Foot), new Vector2(GoW, GoH));
        UiKit.Label(back.transform, "‹  Back", 16, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        back.raycastTarget = true; UiKit.Pressable(back.gameObject, null, Close);
        buy = UiKit.Pic(rootRt, "buy", LitGo(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, Foot + GoH + Gap), new Vector2(GoW, GoH));
        tBuy = UiKit.Label(buy.transform, "", 16, UiKit.GoldText, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270, 24));
        var pk = UiKit.Icon("puka");
        buyPk = UiKit.Pic(buy.transform, "puka", pk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14));
        buyPk.raycastTarget = false; buyPk.gameObject.SetActive(false);
        buy.raycastTarget = true; UiKit.Pressable(buy.gameObject, null, () => { if (buyAct != null) buyAct(); });
        plate = UiKit.Rect(rootRt, "stats", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, Foot + GoH + Gap + GoH + 16f), new Vector2(plateW, 100));
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
        if (stage == null) { stage = new Previews.Stage("ViewerStage", Previews.ViewHome); cam = stage.NewCamera(Fov, 200f, ClearCol); }
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
        if (kind == "board")
        {
            var b = BoardCatalog.Get(id); string eq = SaveData.S.equipped;
            var c = id == eq ? null : BoardCatalog.Get(eq);
            int[] v = { b.speed, b.turn, b.air, b.tough };
            int[] cv = c == null ? null : new[] { c.speed, c.turn, c.air, c.tough };
            plateH = 13f + 5f + 4 * 14f + 3 * 5f + 5f + 11f;
            PlateBack();
            for (int r = 0; r < 4; r++)
            {
                string dt = ""; Color dc = Color.white;
                if (cv != null)
                {
                    int d = v[r] - cv[r];
                    if (d > 0) { dt = "+" + d; dc = Previews.Hex(0x7ff0dd); }
                    else if (d < 0) { dt = d.ToString(); dc = Previews.Hex(0xffab8e); }
                    else { dt = "·"; dc = new Color(1, 1, 1, 0.34f); }
                }
                Row(13f + 5f + r * 19f, StatLabel[r], StatColor(StatCls[r]), v[r], true, dt, dc);
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
                plateH = 13f + 5f + 14f + 5f + 13f + 11f;
                PlateBack();
                Row(18f, label, StatColor(cls), n, false, "", Color.white);
                var tx = UiKit.Label(plate, text, 11, new Color(1, 1, 1, 0.82f), FontStyle.Normal, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -37f), new Vector2(-30, 13), TextAnchor.MiddleLeft, 0.4f);
                tx.raycastTarget = false;
            }
            else
            {
                plateH = 13f + 5f + 14f + 5f + 11f;
                PlateBack();
                UiKit.Label(plate, "Perk", 10, new Color(1, 1, 1, 0.7f), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(15, -18f), new Vector2(48, 14), TextAnchor.MiddleRight, 0.4f);
                UiKit.Label(plate, "The honest baseline", 11, new Color(1, 1, 1, 0.7f), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(71, -18f), new Vector2(240, 14), TextAnchor.MiddleLeft, 0.4f);
            }
        }
        plate.gameObject.SetActive(plateH > 0f);
        if (plateH > 0f) plate.sizeDelta = new Vector2(plateW, plateH);
    }

    void PlateBack()
    {
        var bgi = Solid.Pic(plate, "plate", Solid.Rounded(plateW, plateH, 18f), Solid.Rgba(4, 15, 28, 0.80f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bgi.raycastTarget = false;
        var rim = Solid.Pic(plate, "rim", Solid.Rounded(plateW, plateH, 18f, 1f), new Color(1, 1, 1, 0.13f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rim.raycastTarget = false;
    }

    /// one row at `top` from the plate's top: the label right-aligned in 48px, ten segments, and the change
    void Row(float top, string label, Color col, int n, bool withDelta, string dTxt, Color dCol)
    {
        UiKit.Label(plate, label, 10, new Color(1, 1, 1, 0.7f), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(15, -top), new Vector2(48, 14), TextAnchor.MiddleRight, 0.4f);
        float x0 = 15f + 48f + 8f, x1 = plateW - 15f - (withDelta ? 8f + 26f : 0f);
        float sw = Mathf.Max(2f, (x1 - x0 - 9 * 3f) / 10f);
        n = Mathf.Clamp(n, 0, 10);
        if (n > 0)
        {
            // box-shadow 0 0 8px currentColor, one soft light under the lit run
            float runW = n * sw + (n - 1) * 3f;
            var gl = UiKit.Pic(plate, "glow", Soft(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0 - SoftPad, -(top + 2.5f) + SoftPad), new Vector2(runW + SoftPad * 2f, 9f + SoftPad * 2f));
            gl.type = Image.Type.Sliced; gl.color = new Color(col.r, col.g, col.b, 0.45f); gl.raycastTarget = false;
        }
        var segSprite = Solid.Rounded(sw, 9f, 2f);
        for (int i = 0; i < 10; i++)
        {
            var s = Solid.Pic(plate, "seg", segSprite, i < n ? col : new Color(1, 1, 1, 0.14f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0 + i * (sw + 3f), -(top + 2.5f)), new Vector2(sw, 9f));
            s.raycastTarget = false;
        }
        if (withDelta) UiKit.Label(plate, dTxt, 12, dCol, FontStyle.Bold, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-15, -top), new Vector2(26, 14), TextAnchor.MiddleRight, 0.4f);
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
        buy.sprite = lit ? LitGo() : AltGo();
        tBuy.text = label;
        tBuy.color = lit ? UiKit.GoldText : (label == "Take off" ? Color.white : new Color(1, 1, 1, 0.62f));
        // the puka sits after the figure: the pair is centred as one
        float pw = tBuy.preferredWidth, shift = pk ? (5f + 14f) * 0.5f : 0f;
        tBuy.rectTransform.anchoredPosition = new Vector2(-shift, 0);
        buyPk.gameObject.SetActive(pk && buyPk.sprite != null);
        buyPk.rectTransform.anchoredPosition = new Vector2(-shift + pw * 0.5f + 5f + 7f, 0);
    }

    void Changed()
    {
        onChange?.Invoke();
        DrawStats(); DrawButton();
    }

    static Sprite litGo, altGo;
    static Sprite LitGo() => litGo != null ? litGo : (litGo = UiKit.Rounded(GoW, GoH, 22, 22, 22, 22, UiKit.Body.Teal, 2f));
    static Sprite AltGo() => altGo != null ? altGo : (altGo = UiKit.Rounded(GoW, GoH, 22, 22, 22, 22, UiKit.Body.Pill, 0f));

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
        float bot = Hc - (plateH > 0f ? Foot + GoH + Gap + GoH + 16f + plateH : Foot + GoH + Gap + GoH + 40f);
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
