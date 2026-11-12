// The look of the web game's interface, drawn in code: glass buttons with a gold ring, the
// dark teal dial on the title screen, the top band of the HUD. Every sprite is rendered into a
// texture at twice the layout size from the same numbers the stylesheet uses, so nothing has
// to be exported from a paint program and nothing can drift from the original.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class UiKit
{
    public static readonly Color Gold = new Color(0.847f, 0.698f, 0.388f);        // #d8b263, the ring everywhere
    public static readonly Color GoldText = new Color(0.949f, 0.827f, 0.569f);    // #f2d391, the dial's lettering
    // The game's version, the web's (window.SURF_VERSION, index.html line 13). Shown on the title,
    // the loading screen, the run card and Settings. It was Application.version, the Unity project's
    // own build number, which is not kept in the repo and so read whatever each machine had set.
    // Keep it with the web's when the web moves on.
    public const string Version = "10.98";

    // Is the UI under this screen point? Asked of the EventSystem directly rather than through
    // IsPointerOverGameObject, which only knows about a touch once the EventSystem's own Update
    // has seen it -- in the frame a finger lands, if that has not run yet, a press on a button
    // read as a press on the water as well. And for a touch it must be asked with the finger's id,
    // which the plain overload does not do.
    static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> uiHits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
    static UnityEngine.EventSystems.PointerEventData uiProbe;
    public static bool OverUI(Vector2 screen)
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        if (uiProbe == null) uiProbe = new UnityEngine.EventSystems.PointerEventData(es);
        uiProbe.position = screen; uiHits.Clear();
        es.RaycastAll(uiProbe, uiHits);
        return uiHits.Count > 0;
    }
    public static readonly Color Navy = new Color(0f, 0.07f, 0.165f);              // the text shadow
    public static readonly Color TrickYellow = new Color(1f, 0.875f, 0.18f);       // #ffdf2e
    const float Scale = 2f;

    // The web sets every word in Trebuchet MS. It is on Windows, macOS and iOS, so it is taken
    // from the system; where it is missing (Android) the nearest humanist sans is used, as the
    // browser would, and the built-in font is the last resort.
    public static Font Font
    {
        get
        {
            if (_font != null) return _font;
            try
            {
                var have = new System.Collections.Generic.HashSet<string>(UnityEngine.Font.GetOSInstalledFontNames());
                foreach (var name in new[] { "Trebuchet MS", "Trebuchet", "Verdana", "Segoe UI", "Roboto", "Helvetica Neue", "Arial" })
                    if (have.Contains(name)) { _font = UnityEngine.Font.CreateDynamicFontFromOSFont(name, 32); break; }
            }
            catch { _font = null; }
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }
    static Font _font;

    public enum Body { Glass, Teal, Card, Band, Pill }

    // ---- the sprite ----
    // A rounded rectangle by signed distance, with a radius per corner and a ring of a given
    // width just inside the edge. Edges flagged open (the trick column's left, JUMP's right) are
    // pushed out past the texture so they come out straight and unringed, the way the
    // stylesheet cuts them against the screen edge.
    static readonly System.Collections.Generic.Dictionary<string, Sprite> cache = new System.Collections.Generic.Dictionary<string, Sprite>();
    public static Sprite Rounded(float w, float h, float rTL, float rTR, float rBR, float rBL, Body body, float ring,
                                 bool openLeft = false, bool openRight = false)
    {
        string key = w + "x" + h + ":" + rTL + "," + rTR + "," + rBR + "," + rBL + ":" + body + ":" + ring + ":" + openLeft + openRight;
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
        var sp = Render(w, h, rTL, rTR, rBR, rBL, body, ring, openLeft, openRight);
        cache[key] = sp; return sp;
    }
    static Sprite Render(float w, float h, float rTL, float rTR, float rBR, float rBL, Body body, float ring, bool openLeft, bool openRight)
    {
        int W = Mathf.CeilToInt(w * Scale), H = Mathf.CeilToInt(h * Scale);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        float ext = 40f;
        float x0 = openLeft ? -ext : 0f, x1 = openRight ? w + ext : w;
        float cx = (x0 + x1) * 0.5f, cy = h * 0.5f, hx = (x1 - x0) * 0.5f, hy = h * 0.5f;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float ux = (x + 0.5f) / Scale, uy = (y + 0.5f) / Scale;     // uy runs up
            float r = ux < cx ? (uy < cy ? rBL : rTL) : (uy < cy ? rBR : rTR);
            float qx = Mathf.Abs(ux - cx) - (hx - r), qy = Mathf.Abs(uy - cy) - (hy - r);
            float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
            float cover = Mathf.Clamp01(0.5f - d * Scale);
            float inner = Mathf.Clamp01(0.5f - (d + ring) * Scale);
            float ringMask = ring > 0 ? Mathf.Clamp01(cover - inner) : 0f;
            // layout coordinates for the fill: v runs down from the top like the stylesheet's
            float u = ux / w, v = 1f - uy / h;
            Color fill = Fill(body, u, v, w, h);
            Color c = ringMask > 0 ? Color.Lerp(fill, Gold, ringMask / Mathf.Max(cover, 1e-4f)) : fill;
            c.a *= cover;
            px[y * W + x] = c;
        }
        tex.SetPixels(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), Scale);
    }

    public static Sprite Circle(float d, Body body, float ring) => Rounded(d, d, d / 2, d / 2, d / 2, d / 2, body, ring);

    // the gradients, in the stylesheet's own terms: t runs along a 157-degree line, the top
    // 1.4px is a highlight, and a cool glow rises from the bottom edge
    static Color Fill(Body body, float u, float v, float w, float h)
    {
        float ang = 157f * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));      // css: 0deg up, clockwise
        float len = Mathf.Abs(w * dir.x) + Mathf.Abs(h * dir.y);
        float t = Mathf.Clamp01((Vector2.Dot(new Vector2((u - 0.5f) * w, (0.5f - v) * h * -1f), dir) + len * 0.5f) / len);
        float topPx = v * h, botPx = (1f - v) * h;
        Color c;
        switch (body)
        {
            case Body.Teal:
            {
                var a = new Color(19 / 255f, 84 / 255f, 90 / 255f, 0.86f);
                var b = new Color(7 / 255f, 44 / 255f, 50 / 255f, 0.93f);
                var d = new Color(15 / 255f, 72 / 255f, 78 / 255f, 0.89f);
                c = t < 0.52f ? Color.Lerp(a, b, t / 0.52f) : Color.Lerp(b, d, (t - 0.52f) / 0.48f);
                c = Over(c, new Color(198 / 255f, 240 / 255f, 236 / 255f, 0.34f * Mathf.Clamp01(1.4f - topPx)));
                c = Over(c, new Color(9 / 255f, 50 / 255f, 56 / 255f, 0.5f * Mathf.Clamp01(1f - botPx / 10f)));
                return c;
            }
            case Body.Card:
                c = new Color(0.03f, 0.13f, 0.24f, 0.94f);
                c = Over(c, new Color(1, 1, 1, 0.5f * Mathf.Clamp01(1.4f - topPx)));
                return c;
            case Body.Band:
                // the HUD's top band: navy .34 at the top, .11 at 62%, nothing at the bottom
                return new Color(3 / 255f, 20 / 255f, 40 / 255f, v < 0.62f ? Mathf.Lerp(0.34f, 0.11f, v / 0.62f) : Mathf.Lerp(0.11f, 0f, (v - 0.62f) / 0.38f));
            case Body.Pill:
                c = new Color(7 / 255f, 44 / 255f, 50 / 255f, 0.80f);
                c = Over(c, new Color(198 / 255f, 240 / 255f, 236 / 255f, 0.28f * Mathf.Clamp01(1.2f - topPx)));
                return c;
            default:
            {
                float a = t < 0.45f ? Mathf.Lerp(0.10f, 0.015f, t / 0.45f) : Mathf.Lerp(0.015f, 0.055f, (t - 0.45f) / 0.55f);
                c = new Color(1, 1, 1, a);
                c = Over(c, new Color(1, 1, 1, 0.88f * Mathf.Clamp01(1.4f - topPx)));
                c = Over(c, new Color(165 / 255f, 225 / 255f, 255 / 255f, 0.42f * Mathf.Clamp01(1f - botPx / 10f) * 0.6f));
                return c;
            }
        }
    }

    static Color Over(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a < 1e-5f) return new Color(0, 0, 0, 0);
        var rgb = ((Vector4)over * over.a + (Vector4)under * under.a * (1f - over.a)) / a;
        return new Color(rgb.x, rgb.y, rgb.z, a);
    }

    /// an icon rendered from the web game's own SVG, under Resources/UI
    static readonly System.Collections.Generic.Dictionary<string, Sprite> icons = new System.Collections.Generic.Dictionary<string, Sprite>();
    public static Sprite Icon(string name)
    {
        if (icons.TryGetValue(name, out var sp)) return sp;
        var tex = Resources.Load<Texture2D>("UI/" + name);
        sp = tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        icons[name] = sp; return sp;
    }

    // ---- the elements ----
    public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static Image Pic(Transform parent, string name, Sprite s, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = Rect(parent, name, aMin, aMax, pivot, pos, size);
        var im = rt.gameObject.AddComponent<Image>(); im.sprite = s; im.type = Image.Type.Simple; im.color = Color.white;
        return im;
    }

    public static Text Label(Transform parent, string s, float size, Color col, FontStyle style, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 sz, TextAnchor align = TextAnchor.MiddleCenter, float shadow = 0.8f)
    {
        var rt = Rect(parent, "label", aMin, aMax, pivot, pos, sz);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font; t.fontSize = Mathf.RoundToInt(size); t.text = s; t.alignment = align; t.color = col; t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        if (shadow > 0) { var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(Navy.r, Navy.g, Navy.b, shadow); sh.effectDistance = new Vector2(0, -1.2f); }
        return t;
    }

    /// a press: scales to .9 while the finger is down, like the stylesheet's :active
    public static void Pressable(GameObject go, System.Action down = null, System.Action up = null)
    {
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        var pa = go.GetComponent<PressAnim>() ?? go.AddComponent<PressAnim>();
        // A hold button (one with a down) lets go when the finger slides off; a tap button is
        // cancelled by sliding off instead, and never fires twice (exit and then up both used to).
        bool held = false, hold = down != null;
        Add(et, EventTriggerType.PointerDown, _ => { held = true; pa.Press(true); down?.Invoke(); });
        Add(et, EventTriggerType.PointerUp, _ => { pa.Press(false); if (held) { held = false; up?.Invoke(); } });
        Add(et, EventTriggerType.PointerExit, _ => { if (!held) return; held = false; pa.Press(false); if (hold) up?.Invoke(); });
    }

    public static void Add(EventTrigger et, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> cb)
    {
        var e = new EventTrigger.Entry { eventID = type }; e.callback.AddListener(cb); et.triggers.Add(e);
    }
}
