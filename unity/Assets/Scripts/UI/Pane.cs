// The menu panels' shared bones, lifted from Shop so every one of them is the same pane: the
// navy sheet, the title at the top left, the teal × at the top right, and a scrolling column
// of glass cards under it. The rows and bars the web panels are made of live here too, so
// Quests, Stats, Settings, School and Profile draw from one vocabulary.
using UnityEngine;
using UnityEngine.UI;

public static class Pane
{
    public static readonly Color Dim = new Color(1, 1, 1, 0.82f);
    public static readonly Color Faint = new Color(1, 1, 1, 0.6f);

    /// the sheet: title, close button, and the scrolling column; `top` is where the column
    /// starts below the title (Shop's is 112 because of its tabs)
    public static GameObject Build(Transform canvas, string name, string title, System.Action onClose,
                                   out RectTransform content, out ScrollRect scroll, float top = 60f)
    {
        var pane = UiKit.Rect(canvas, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var bg = pane.AddComponent<Image>(); bg.color = new Color(0.04f, 0.16f, 0.30f, 1f);   // opaque: nothing of the beach or the last run shows through
        var p = pane.transform;
        UiKit.Label(p, title, 34, Color.white, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(300, 44), TextAnchor.MiddleLeft);
        var close = UiKit.Pic(p, "close", UiKit.Circle(40, UiKit.Body.Teal, 2f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -16), new Vector2(40, 40));
        UiKit.Label(close.transform, "×", 24, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 1), Vector2.zero);
        close.raycastTarget = true; UiKit.Pressable(close.gameObject, null, onClose);
        var view = UiKit.Rect(p, "view", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -top * 0.5f), new Vector2(0, -top));
        view.gameObject.AddComponent<RectMask2D>();
        scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        content = UiKit.Rect(view, "content", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
        var vl = content.gameObject.AddComponent<VerticalLayoutGroup>(); vl.padding = new RectOffset(14, 14, 10, 20); vl.spacing = 10; vl.childControlHeight = false; vl.childControlWidth = true; vl.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content; scroll.viewport = view;
        pane.AddComponent<PaneIn>().target = view;      // the column eases in; the opaque backing does not
        pane.SetActive(false);
        return pane;
    }

    public static void Clear(Transform list)
    {
        foreach (Transform c in list) { c.gameObject.SetActive(false); Object.Destroy(c.gameObject); }
    }

    /// a glass card of the given height in the column
    public static Transform Card(Transform list, string name, float h)
    {
        var card = UiKit.Pic(list, name, UiKit.Rounded(402, h, 18, 18, 18, 18, UiKit.Body.Glass, 1.5f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, h));
        var le = card.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = h; le.minHeight = h;
        card.raycastTarget = true;          // a finger dragged on a card scrolls the column
        return card.transform;
    }

    /// the web's .sec h3: a small gold heading between groups of rows
    public static void Heading(Transform list, string s)
    {
        var rt = UiKit.Rect(list, "heading", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 26));
        var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 26; le.minHeight = 26;
        UiKit.Label(rt, s.ToUpper(), 11, UiKit.GoldText, FontStyle.Bold, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(4, -4), Vector2.zero, TextAnchor.LowerLeft, 0.5f);
    }

    /// a paragraph in the column, wrapped; `h` is the height to reserve for it
    public static void Note(Transform list, string s, float h, float size = 12.5f)
    {
        var rt = UiKit.Rect(list, "note", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, h));
        var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = h; le.minHeight = h;
        var t = UiKit.Label(rt, s, size, Dim, FontStyle.Normal, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8, 0), TextAnchor.UpperLeft, 0.5f);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    /// the web's .row: a label on the left, a bold value on the right
    public static Transform Row(Transform list, string k, string v)
    {
        var c = Card(list, "row:" + k, 44);
        UiKit.Label(c, k, 14, Dim, FontStyle.Normal, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(-32, 0), TextAnchor.MiddleLeft, 0.5f);
        UiKit.Label(c, v, 15, Color.white, FontStyle.Bold, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(-32, 0), TextAnchor.MiddleRight, 0.5f);
        return c;
    }

    /// a row with a bar under the label and value: the web's .tr.goal
    public static Transform Bar(Transform list, string k, string v, float frac, bool done = false)
    {
        var c = Card(list, "bar:" + k, 62);
        UiKit.Label(c, k, 14, done ? Color.white : Dim, FontStyle.Bold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(-32, 22), TextAnchor.MiddleLeft, 0.5f);
        UiKit.Label(c, v, 13, done ? UiKit.GoldText : Color.white, FontStyle.Bold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(-32, 22), TextAnchor.MiddleRight, 0.5f);
        Track(c, new Vector2(16, 14), 370, 7, frac, done ? UiKit.GoldText : UiKit.Gold);
        return c;
    }

    /// a progress bar: a dark track with a gold fill, anchored at the card's bottom left
    public static Image Track(Transform c, Vector2 pos, float w, float h, float frac, Color col)
    {
        var track = UiKit.Pic(c, "track", UiKit.Rounded(w, h, h / 2, h / 2, h / 2, h / 2, UiKit.Body.Pill, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), pos, new Vector2(-pos.x * 2, h));
        track.color = new Color(0, 0, 0, 0.35f); track.raycastTarget = false;
        var fill = UiKit.Pic(c, "fill", UiKit.Rounded(w, h, h / 2, h / 2, h / 2, h / 2, UiKit.Body.Pill, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), pos, new Vector2(-pos.x * 2, h));
        fill.color = col; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = Mathf.Clamp01(frac); fill.raycastTarget = false;
        return fill;
    }

    /// a teal pill button inside a card, at the right edge
    public static Image Button(Transform c, string label, float w, bool lit, System.Action tap)
    {
        var btn = UiKit.Pic(c, "btn", UiKit.Rounded(w, 34, 17, 17, 17, 17, lit ? UiKit.Body.Teal : UiKit.Body.Pill, lit ? 2f : 0f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(w, 34));
        UiKit.Label(btn.transform, label, 13, lit ? UiKit.GoldText : new Color(1, 1, 1, 0.7f), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        btn.raycastTarget = true;
        if (tap != null) UiKit.Pressable(btn.gameObject, null, tap);
        return btn;
    }

    /// a full-width teal button in the column, the web's .go
    public static void Go(Transform list, string label, System.Action tap)
    {
        var rt = UiKit.Rect(list, "go", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 48));
        var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 48; le.minHeight = 48;
        var btn = UiKit.Pic(rt, "btn", UiKit.Rounded(240, 44, 22, 22, 22, 22, UiKit.Body.Teal, 2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 44));
        UiKit.Label(btn.transform, label, 14, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        btn.raycastTarget = true; UiKit.Pressable(btn.gameObject, null, tap);
    }

    public static string N(int v) => v.ToString("N0");

    /// seconds as the web's hhmm(): "1h 4m", "12m 30s", "45s"
    public static string Hhmm(int sec)
    {
        int h = sec / 3600, m = sec % 3600 / 60;
        return h > 0 ? h + "h " + m + "m" : (m > 0 ? m + "m " + (sec % 60) + "s" : sec + "s");
    }
}
