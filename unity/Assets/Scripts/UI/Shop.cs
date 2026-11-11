// The shop: boards and riders, bought with puka and equipped. A full-screen pane in the
// game's glass-and-gold language, two tabs, a scrolling column of cards, each with its name,
// blurb, the four stat bars (or the rider's perk) and one button: the price, EQUIP, or IN USE.
using UnityEngine;
using UnityEngine.UI;

public class Shop : MonoBehaviour
{
    public System.Action onChange;        // the equipped board or rider changed
    public System.Action onClose;
    GameObject pane, list; Text tCoins; string tab = "boards";
    RectTransform content; ScrollRect scroll;
    Image tabBoards, tabRiders;

    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        pane = UiKit.Rect(canvas, "shop", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var bg = pane.AddComponent<Image>(); bg.color = new Color(0.04f, 0.16f, 0.30f, 1f);   // opaque: nothing of the beach or the last run shows through
        var p = pane.transform;
        UiKit.Label(p, "Shop", 34, Color.white, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(200, 44), TextAnchor.MiddleLeft);
        var pill = UiKit.Pic(p, "coins", UiKit.Rounded(112, 32, 16, 16, 16, 16, UiKit.Body.Pill, 1.5f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-66, -20), new Vector2(112, 32));
        tCoins = UiKit.Label(pill.transform, "0", 15, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(6, 0), Vector2.zero);
        var shell = UiKit.Icon("puka"); if (shell != null) { var im = UiKit.Pic(pill.transform, "puka", shell, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(9, 0), new Vector2(17, 17)); im.raycastTarget = false; }
        var close = UiKit.Pic(p, "close", UiKit.Circle(40, UiKit.Body.Teal, 2f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -16), new Vector2(40, 40));
        UiKit.Label(close.transform, "×", 24, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 1), Vector2.zero);
        close.raycastTarget = true; UiKit.Pressable(close.gameObject, null, () => { Show(false); onClose?.Invoke(); });
        // tabs
        tabBoards = Tab(p, "Boards", 18, "boards"); tabRiders = Tab(p, "Riders", 128, "riders");
        // the scrolling column
        var view = UiKit.Rect(p, "view", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(0, -112));
        var mask = view.gameObject.AddComponent<RectMask2D>();
        scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        content = UiKit.Rect(view, "content", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
        var vl = content.gameObject.AddComponent<VerticalLayoutGroup>(); vl.padding = new RectOffset(14, 14, 10, 20); vl.spacing = 10; vl.childControlHeight = false; vl.childControlWidth = true; vl.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content; scroll.viewport = view;
        list = content.gameObject;
        pane.SetActive(false);
    }

    Image Tab(Transform p, string label, float x, string id)
    {
        var im = UiKit.Pic(p, "tab:" + id, UiKit.Rounded(100, 34, 17, 17, 17, 17, UiKit.Body.Teal, 1.5f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -66), new Vector2(100, 34));
        UiKit.Label(im.transform, label, 14, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        im.raycastTarget = true; UiKit.Pressable(im.gameObject, null, () => { tab = id; Fill(); });
        return im;
    }

    public void Show(bool on) { pane.SetActive(on); if (on) Fill(); }

    void Fill()
    {
        foreach (Transform c in list.transform) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        tCoins.text = SaveData.Coins.ToString("N0");
        tabBoards.color = tab == "boards" ? Color.white : new Color(1, 1, 1, 0.55f);
        tabRiders.color = tab == "riders" ? Color.white : new Color(1, 1, 1, 0.55f);
        if (tab == "boards") foreach (var b in BoardCatalog.All) Card(b.name, b.blurb, b.price, SaveData.Owns(b.id), SaveData.S.equipped == b.id, new[] { b.speed, b.turn, b.air, b.tough }, null,
            () => { if (SaveData.Coins >= b.price) { SaveData.Coins -= b.price; SaveData.S.owned.Add(b.id); SaveData.S.equipped = b.id; SaveData.Flush(); onChange?.Invoke(); Fill(); } },
            () => { SaveData.S.equipped = b.id; SaveData.Flush(); onChange?.Invoke(); Fill(); });
        else foreach (var r in RiderCatalog.All) Card(r.name, r.blurb, r.price, SaveData.OwnsRider(r.id), SaveData.S.rider == r.id, null, r.perk,
            () => { if (SaveData.Coins >= r.price) { SaveData.Coins -= r.price; SaveData.S.ownedChars.Add(r.id); SaveData.S.rider = r.id; SaveData.Flush(); onChange?.Invoke(); Fill(); } },
            () => { SaveData.S.rider = r.id; SaveData.Flush(); onChange?.Invoke(); Fill(); });
        scroll.verticalNormalizedPosition = 1f;
    }

    static readonly string[] StatNames = { "SPEED", "TURN", "AIR", "TOUGH" };
    void Card(string name, string blurb, int price, bool owned, bool inUse, int[] stats, string perk, System.Action buy, System.Action equip)
    {
        float h = stats != null ? 150f : 118f;
        var card = UiKit.Pic(list.transform, name, UiKit.Rounded(402, h, 18, 18, 18, 18, UiKit.Body.Glass, 1.5f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, h));
        var le = card.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = h; le.minHeight = h;
        var c = card.transform;
        UiKit.Label(c, name, 18, Color.white, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(260, 24), TextAnchor.MiddleLeft);
        var bl = UiKit.Label(c, blurb, 12, new Color(1, 1, 1, 0.82f), FontStyle.Normal, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -38), new Vector2(-150, 40), TextAnchor.UpperLeft, 0.5f);
        bl.horizontalOverflow = HorizontalWrapMode.Wrap;
        if (stats != null)
            for (int i = 0; i < 4; i++)
            {
                float x = 16 + i * 92;
                UiKit.Label(c, StatNames[i], 9, new Color(1, 1, 1, 0.6f), FontStyle.Bold, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 34), new Vector2(80, 12), TextAnchor.MiddleLeft, 0.4f);
                var track = UiKit.Pic(c, "track", UiKit.Rounded(80, 7, 3.5f, 3.5f, 3.5f, 3.5f, UiKit.Body.Pill, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 22), new Vector2(80, 7));
                track.color = new Color(0, 0, 0, 0.35f); track.raycastTarget = false;
                var fill = UiKit.Pic(c, "fill", UiKit.Rounded(80, 7, 3.5f, 3.5f, 3.5f, 3.5f, UiKit.Body.Pill, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 22), new Vector2(80, 7));
                fill.color = UiKit.Gold; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = stats[i] / 10f; fill.raycastTarget = false;
            }
        else if (!string.IsNullOrEmpty(perk))
            UiKit.Label(c, perk, 12, UiKit.GoldText, FontStyle.Bold, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 24), new Vector2(240, 16), TextAnchor.MiddleLeft, 0.4f);
        // the one button
        string label = inUse ? "IN USE" : owned ? "EQUIP" : price.ToString("N0");
        var btn = UiKit.Pic(c, "btn", UiKit.Rounded(110, 40, 20, 20, 20, 20, inUse ? UiKit.Body.Pill : UiKit.Body.Teal, inUse ? 0 : 2f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -14), new Vector2(110, 40));
        UiKit.Label(btn.transform, label, 14, inUse ? new Color(1, 1, 1, 0.7f) : UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(owned ? 0 : 6, 0), Vector2.zero);
        if (!owned) { var shell = UiKit.Icon("puka"); if (shell != null) { var im = UiKit.Pic(btn.transform, "puka", shell, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(16, 16)); im.raycastTarget = false; } }
        btn.raycastTarget = true;
        if (!inUse) UiKit.Pressable(btn.gameObject, null, owned ? equip : buy);
    }
}
