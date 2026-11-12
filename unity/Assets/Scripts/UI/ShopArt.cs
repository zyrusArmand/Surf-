// The shop's and the viewer's own surfaces, drawn from the web stylesheet's numbers: the sheet's
// dusk wash (#shop), the pill buttons (.go, .go.alt, .go.calm with the gold ring), the tabs
// (#shopTabs .tab / .tab.on), the Lv badge, and the "<" chevron the Back buttons carry -- and the
// run card's row (the dial's round .hbtn.glass, the warm Again, the .rwd offers), which PillOver
// draws over the frame behind them rather than over one flat colour.
//
// Two things about the browser have to be reproduced on purpose here:
//  * A browser blends a translucent layer in sRGB; this project renders in linear space, where
//    a 9% white over navy comes out several times brighter. So every pill is composited on the
//    CPU, in sRGB, over the colour known to be behind it, and drawn OPAQUE -- it then looks the
//    way the browser drew it. What has to stay see-through (a shadow, a glow, a wash) is given
//    the alpha that lands, blended in linear, where the browser's alpha lands (LinA).
//  * .go carries backdrop-filter: saturate(135%), so the colour behind a button is saturated
//    before the glass goes over it.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class ShopArt
{
    const float Dense = 3f;                 // texels per layout pixel for a small pill: crisp at a phone's 3x
    public const float Pad = 18f;           // room round a pill for its drop shadow and glow

    public enum Glass { Amber, Alt, Calm, TabOff, TabOn, Ring, Disc, Again, Offer }

    public static Color Rgb(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);

    public static readonly Color GoldRing = Rgb(0xd8, 0xb2, 0x63);      // --gold
    public static readonly Color TitleGold = Rgb(0xff, 0xdf, 0x2e);     // #shop h2
    public static readonly Color TitleShade = Rgb(0xb8, 0x64, 0x1c);    // its 2px 2px 0 shadow
    public static readonly Color Cream = Rgb(0xff, 0xe9, 0xa8);         // #shop h2 span
    public static readonly Color TextShade = Rgb(0, 18, 42);            // rgba(0,18,42,...) under every label

    static float L(float c) => Mathf.GammaToLinearSpace(Mathf.Clamp01(c));

    /// the alpha that, blended in linear space, puts colour `c` over `back` where a browser's
    /// sRGB blend at alpha `a` puts it
    public static float LinA(float a, Color c, Color back)
    {
        if (a <= 0f) return 0f;
        if (a >= 1f) return 1f;
        float sum = 0f, wsum = 0f;
        for (int i = 0; i < 3; i++)
        {
            float t = a * c[i] + (1f - a) * back[i];
            float lb = L(back[i]), den = L(c[i]) - lb;
            float w = Mathf.Abs(den);
            if (w < 0.004f) continue;
            sum += Mathf.Clamp01((L(t) - lb) / den) * w; wsum += w;
        }
        return wsum > 0f ? sum / wsum : a;
    }

    /// straight-alpha "over", the way a browser stacks two layers
    public static Color Over(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a < 1e-5f) return new Color(0f, 0f, 0f, 0f);
        float k = under.a * (1f - over.a);
        return new Color((over.r * over.a + under.r * k) / a, (over.g * over.a + under.g * k) / a, (over.b * over.a + under.b * k) / a, a);
    }

    /// what `top` at alpha `a` over `back` looks like, opaque: for text set at an opacity
    public static Color Mix(Color back, Color top, float a)
    {
        var c = Color.Lerp(back, top, a); c.a = 1f; return c;
    }

    /// the W3C saturate() matrix
    public static Color Saturate(Color c, float s)
    {
        float r = (0.213f + 0.787f * s) * c.r + (0.715f - 0.715f * s) * c.g + (0.072f - 0.072f * s) * c.b;
        float g = (0.213f - 0.213f * s) * c.r + (0.715f + 0.285f * s) * c.g + (0.072f - 0.072f * s) * c.b;
        float b = (0.213f - 0.213f * s) * c.r + (0.715f - 0.715f * s) * c.g + (0.072f + 0.928f * s) * c.b;
        return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), c.a);
    }

    // ---- the sheet ----
    /// #shop's background at (u across, v down) of the screen: the dusk gradient at .97, over what
    /// the 14px blur lets through of the beach (light up top, dark at the foot), and the sunset
    /// glow -- radial-gradient(128% 52% at 50% 63%, rgba(226,146,72,.26) 0, transparent 62%)
    public static Color SheetAt(float u, float v)
    {
        v = Mathf.Clamp01(v);
        Color back = Color.Lerp(Rgb(185, 178, 200), Rgb(52, 52, 60), v);
        Color c0 = Rgb(9, 33, 60, 0.972f), c1 = Rgb(13, 50, 80, 0.968f), c2 = Rgb(12, 44, 72, 0.972f), c3 = Rgb(7, 26, 46, 0.978f);
        Color lin = v < 0.40f ? Color.Lerp(c0, c1, v / 0.40f) : v < 0.66f ? Color.Lerp(c1, c2, (v - 0.40f) / 0.26f) : Color.Lerp(c2, c3, (v - 0.66f) / 0.34f);
        Color c = Over(back, lin);
        float ex = (u - 0.5f) / 1.28f, ey = (v - 0.63f) / 0.52f, rho = Mathf.Sqrt(ex * ex + ey * ey);
        if (rho < 0.62f) c = Over(c, Rgb(226, 146, 72, 0.26f * (1f - rho / 0.62f)));
        c.a = 1f;
        return c;
    }

    static Sprite sheet;
    public static Sprite Sheet()
    {
        if (sheet != null) return sheet;
        const int W = 64, H = 256;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "shop sheet" };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            px[y * W + x] = SheetAt((x + 0.5f) / W, 1f - (y + 0.5f) / H);
        tex.SetPixels(px); tex.Apply(false, true);
        sheet = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return sheet;
    }

    // ---- the pills ----
    struct Spec
    {
        public bool glass, tint, flat, hollow, streak, saturate;
        public Color t0, t1, t2; public float tp;              // the tint layer's three stops
        public Color flatC;                                      // a flat fill (a tab at rest)
        public float hl, hlW;                                    // inset 0 hlW 0 rgba(255,255,255,hl)
        public Color foot; public float footOff, footBlur, footSpread;   // inset 0 -off blur -spread foot
        public float shA, shY, shBlur;                           // 0 shY shBlur rgba(0,18,42,shA)
        public Color glow; public float glowBlur;                // 0 0 glowBlur glow
        public Color ring; public float ringW;
        public bool dot;                                         // .glass::before: the small teal streak the dial's discs carry
        public Color rim2; public float rim2Off, rim2Blur, rim2Spread;   // .glass::after: inset 0 -off blur -spread rim2
    }

    static Spec Of(Glass g)
    {
        var s = new Spec { ring = GoldRing, ringW = 1.5f, hlW = 1.4f, footOff = 10f, footBlur = 14f, footSpread = 11f, shY = 3f, shBlur = 15f };
        switch (g)
        {
            case Glass.Amber:       // .go
                s.saturate = true; s.glass = true; s.tint = true; s.streak = true;
                s.t0 = Rgb(255, 196, 74, 0.30f); s.t1 = Rgb(255, 150, 30, 0.15f); s.tp = 0.55f; s.t2 = Rgb(255, 214, 120, 0.20f);
                s.hl = 0.88f; s.foot = Rgb(255, 226, 170, 0.50f); s.shA = 0.26f;
                s.glow = Rgb(255, 178, 50, 0.22f); s.glowBlur = 20f;
                break;
            case Glass.Alt:         // .go.alt: the glass with nothing in it
                s.saturate = true; s.glass = true; s.streak = true;
                s.hl = 0.88f; s.foot = Rgb(165, 225, 255, 0.42f); s.shA = 0.26f;
                break;
            case Glass.Calm:        // .go.calm: deep water, the one tint that darkens
                s.saturate = true; s.glass = true; s.tint = true; s.streak = true;
                s.t0 = Rgb(30, 70, 116, 0.30f); s.t1 = Rgb(10, 38, 72, 0.20f); s.tp = 0.55f; s.t2 = Rgb(48, 92, 140, 0.24f);
                s.hl = 0.72f; s.foot = Rgb(165, 225, 255, 0.34f); s.shA = 0.30f;
                break;
            case Glass.TabOff:      // #shopTabs .tab: glass with the light turned down
                s.flat = true; s.flatC = Rgb(255, 255, 255, 0.09f);
                break;
            case Glass.TabOn:       // .tab.on: the same glass with shallow water in it
                s.tint = true;
                s.t0 = Rgb(90, 238, 226, 0.28f); s.t1 = Rgb(20, 180, 180, 0.13f); s.tp = 0.55f; s.t2 = Rgb(150, 246, 238, 0.20f);
                s.hl = 0.86f; s.hlW = 1.3f;
                s.foot = Rgb(180, 248, 240, 0.48f); s.footOff = 9f; s.footBlur = 13f; s.footSpread = 10f;
                s.glow = Rgb(40, 214, 208, 0.24f); s.glowBlur = 16f;
                break;
            case Glass.Ring:        // #shopLv: a hairline and nothing inside
                s.hollow = true; s.ring = Rgb(255, 233, 168, 0.42f); s.ringW = 1f;
                break;
            // ---- the run card's row (web 1724-1739): the dial's .hbtn.glass discs and the warm Again ----
            case Glass.Disc:        // .hbtn.glass at 56: .glass, the 2px gold ring, its own streak and rim light
                s.saturate = true; s.glass = true; s.dot = true;
                s.hl = 0.88f; s.foot = Rgb(165, 225, 255, 0.42f); s.shA = 0.26f; s.ringW = 2f;
                s.rim2 = Rgb(150, 225, 220, 0.55f); s.rim2Off = 1.5f; s.rim2Blur = 4f; s.rim2Spread = 2f;
                break;
            case Glass.Again:       // .ob-again: .go's amber in a disc's body, ringed cream rather than gold
                s.saturate = true; s.glass = true; s.tint = true; s.dot = true;
                s.t0 = Rgb(255, 196, 74, 0.30f); s.t1 = Rgb(255, 150, 30, 0.15f); s.tp = 0.55f; s.t2 = Rgb(255, 214, 120, 0.20f);
                s.hl = 0.88f; s.foot = Rgb(255, 226, 170, 0.50f); s.shA = 0.26f;
                s.glow = Rgb(255, 178, 50, 0.22f); s.glowBlur = 20f;
                s.ring = Rgb(255, 236, 190, 0.80f);
                s.rim2 = Rgb(150, 225, 220, 0.55f); s.rim2Off = 1.5f; s.rim2Blur = 4f; s.rim2Spread = 2f;
                break;
            case Glass.Offer:       // .rwd (web 1702): a lighter sheet, a hairline white rim, no ring of gold
                s.saturate = true; s.tint = true;
                s.t0 = Rgb(255, 255, 255, 0.17f); s.t1 = Rgb(255, 255, 255, 0.05f); s.tp = 0.55f; s.t2 = Rgb(255, 255, 255, 0.08f);
                s.hl = 0.60f; s.hlW = 1.2f; s.shA = 0.22f; s.shBlur = 12f;
                s.ring = Rgb(255, 255, 255, 0.30f); s.ringW = 1f;
                break;
        }
        return s;
    }

    static float Sd(float x, float y, float w, float h, float r)
    {
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }
    static float Phi(float x) => 1f / (1f + Mathf.Exp(-1.702f * x));

    /// t along a 157-degree CSS gradient line in a w by h box (y runs down)
    static float T157(float x, float y, float w, float h)
    {
        const float a = 157f * Mathf.Deg2Rad;
        float dx = Mathf.Sin(a), dy = -Mathf.Cos(a);
        float len = Mathf.Abs(w * dx) + Mathf.Abs(h * dy);
        return Mathf.Clamp01(((x - w * 0.5f) * dx + (y - h * 0.5f) * dy) / len + 0.5f);
    }
    static Color Stops(float t, Color a, Color b, float pb, Color c) => t < pb ? Color.Lerp(a, b, t / pb) : Color.Lerp(b, c, (t - pb) / (1f - pb));
    static float Radial(float rho, float a0, float a1, float p1, float p2) => rho < p1 ? Mathf.Lerp(a0, a1, rho / p1) : rho < p2 ? Mathf.Lerp(a1, 0f, (rho - p1) / (p2 - p1)) : 0f;

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// a w by h capsule in the given glass over `back`, with Pad of room all round for its shadow
    /// and glow: show it in a rect Pad bigger on every side (Art does that)
    public static Sprite Pill(float w, float h, Glass g, Color back)
    {
        string key = g + ":" + w.ToString("0.0") + "x" + h.ToString("0.0") + ":" + ColorUtility.ToHtmlStringRGB(back);
        Sprite hit;
        if (cache.TryGetValue(key, out hit) && hit != null) return hit;
        hit = Draw(w, h, g, (x, y) => back, -1f, key);
        cache[key] = hit;
        return hit;
    }

    /// the same glass over a backdrop that changes across it -- `backAt(x, y)` in layout px from
    /// the pill's top left, Pad of room outside included -- with corners of `radius` (a capsule
    /// when negative). Not cached: the caller owns the texture and destroys it with the sprite.
    public static Sprite PillOver(float w, float h, Glass g, System.Func<float, float, Color> backAt, float radius = -1f)
    {
        return Draw(w, h, g, backAt, radius, g + " over");
    }

    static Sprite Draw(float w, float h, Glass g, System.Func<float, float, Color> backAt, float radius, string key)
    {
        var sp = Of(g);
        float S = w > 150f ? 2f : Dense;     // the long pills at 2x: a third of a megabyte each rather than a megabyte, and quicker to draw
        float bw = sp.ringW, r = radius < 0f ? h * 0.5f : Mathf.Min(radius, h * 0.5f), ri = Mathf.Max(0f, r - bw), iw = w - 2f * bw, ih = h - 2f * bw;
        int TW = Mathf.CeilToInt((w + 2f * Pad) * S), TH = Mathf.CeilToInt((h + 2f * Pad) * S);
        var px = new Color[TW * TH];
        for (int py = 0; py < TH; py++)
        for (int qx = 0; qx < TW; qx++)
        {
            float x = (qx + 0.5f) / S - Pad, y = (TH - 1 - py + 0.5f) / S - Pad;     // layout px, y down from the pill's top
            Color B = backAt(x, y); if (sp.saturate) B = Saturate(B, 1.35f); B.a = 1f;
            Color ringC = Over(B, sp.ring); ringC.a = 1f;
            float d = Sd(x, y, w, h, r);
            float cover = Mathf.Clamp01(0.5f - d * S);
            // outside: the drop shadow and the glow, then the alpha that blends right in linear
            Color o = new Color(0f, 0f, 0f, 0f);
            if (sp.shA > 0f)
            {
                float a = sp.shA * (1f - Phi(Sd(x, y - sp.shY, w, h, r) / (sp.shBlur * 0.5f)));
                if (a > 0f) o = Over(o, new Color(TextShade.r, TextShade.g, TextShade.b, a));
            }
            if (sp.glow.a > 0f)
            {
                float a = sp.glow.a * (1f - Phi(d / (sp.glowBlur * 0.5f)));
                if (a > 0f) o = Over(o, new Color(sp.glow.r, sp.glow.g, sp.glow.b, a));
            }
            if (o.a > 0.002f) o.a = LinA(o.a, o, B); else o = new Color(0f, 0f, 0f, 0f);
            if (cover <= 0f) { px[py * TW + qx] = o; continue; }
            // the padding box: everything but the ring
            float di = Sd(x - bw, y - bw, iw, ih, ri);
            float ci = Mathf.Clamp01(0.5f - di * S);
            Color inside;
            if (sp.hollow) inside = new Color(B.r, B.g, B.b, 0f);
            else
            {
                inside = B;
                float t = T157(x, y, w, h);
                if (sp.flat) inside = Over(inside, sp.flatC);
                if (sp.glass) inside = Over(inside, Stops(t, new Color(1, 1, 1, 0.10f), new Color(1, 1, 1, 0.015f), 0.45f, new Color(1, 1, 1, 0.055f)));
                if (sp.tint) inside = Over(inside, Stops(t, sp.t0, sp.t1, sp.tp, sp.t2));
                if (sp.hl > 0f)
                {
                    float hole = Mathf.Clamp01(0.5f - Sd(x - bw, y - bw - sp.hlW, iw, ih, ri) * S);
                    float a = sp.hl * ci * (1f - hole);
                    if (a > 0f) inside = Over(inside, new Color(1, 1, 1, a));
                }
                if (sp.foot.a > 0f)
                {
                    float sh = Sd(x - bw, y - bw + sp.footOff, iw, ih, ri) - sp.footSpread;
                    float a = sp.foot.a * Phi(sh / (sp.footBlur * 0.5f)) * ci;
                    if (a > 0f) inside = Over(inside, new Color(sp.foot.r, sp.foot.g, sp.foot.b, a));
                }
                if (sp.rim2.a > 0f)
                {
                    float sh = Sd(x - bw, y - bw + sp.rim2Off, iw, ih, ri) - sp.rim2Spread;
                    float a = sp.rim2.a * Phi(sh / (sp.rim2Blur * 0.5f)) * ci;
                    if (a > 0f) inside = Over(inside, new Color(sp.rim2.r, sp.rim2.g, sp.rim2.b, a));
                }
                if (sp.dot)
                {
                    // .hbtn.glass::before: left and right 19%, top 7%, 17% tall, an ellipse, rgba(206,246,242,.30)
                    // fading to .04 at 78% of its height and to nothing at the foot
                    float ex = (x - bw - iw * 0.5f) / (iw * 0.31f), top = 0.07f * ih, hh = 0.17f * ih;
                    float ty = (y - bw - top) / Mathf.Max(0.01f, hh), ey = ty * 2f - 1f;
                    float e = ex * ex + ey * ey;
                    if (e < 1f && ty >= 0f)
                    {
                        float edge = Mathf.Clamp01((1f - Mathf.Sqrt(e)) * Mathf.Min(iw * 0.31f, hh * 0.5f) * S);
                        float a = (ty < 0.78f ? Mathf.Lerp(0.30f, 0.04f, ty / 0.78f) : Mathf.Lerp(0.04f, 0f, (ty - 0.78f) / 0.22f)) * edge * ci;
                        if (a > 0f) inside = Over(inside, Rgb(206, 246, 242, a));
                    }
                }
                if (sp.streak)
                {
                    // ::before -- 1px in from the sides and top, 48% tall, a radial hung above the top
                    float sw = iw - 2f, shh = 0.48f * ih, lx = x - bw - 1f, ly = y - bw - 1f;
                    float inBox = Mathf.Clamp01(0.5f - Sd(lx, ly, sw, shh, Mathf.Min(sw, shh) * 0.5f) * S);
                    if (inBox > 0f)
                    {
                        float ex = (lx - sw * 0.5f) / (0.72f * sw), ey = (ly + 0.34f * shh) / (1.5f * shh);
                        float a = Radial(Mathf.Sqrt(ex * ex + ey * ey), 0.55f, 0.18f, 0.46f, 0.78f) * inBox * ci;
                        if (a > 0f) inside = Over(inside, new Color(1, 1, 1, a));
                    }
                    // ::after -- the foam along the bottom 52%, a radial centred below the button
                    float fh = 0.52f * ih, fx = x - bw, fy = y - bw - (ih - fh);
                    inBox = Mathf.Clamp01(0.5f - Sd(fx, fy, iw, fh, Mathf.Min(iw, fh) * 0.5f) * S);
                    if (inBox > 0f)
                    {
                        float ex = (fx - iw * 0.5f) / (1.18f * iw), ey = (fy - 1.42f * fh) / fh;
                        float a = Radial(Mathf.Sqrt(ex * ex + ey * ey), 0.34f, 0.12f, 0.46f, 0.74f) * inBox * ci;
                        if (a > 0f) inside = Over(inside, Rgb(206, 244, 255, a));
                    }
                }
            }
            // the ring: what is inside the shape but outside the padding box
            float ringA = Mathf.Clamp01(cover - ci);
            Color shape;
            if (sp.hollow) shape = new Color(ringC.r, ringC.g, ringC.b, ringA);
            else
            {
                float k = ci / Mathf.Max(cover, 1e-4f);
                shape = Color.Lerp(ringC, inside, k); shape.a = cover;
            }
            px[py * TW + qx] = Over(o, shape);
        }
        var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "pill " + key };
        tex.SetPixels(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, TW, TH), new Vector2(0.5f, 0.5f), S);
    }

    /// the pill's picture inside a control: a child Pad bigger on every side, so the control's
    /// own rect (and its hit area) stays the size of the pill
    public static Image Art(Transform control, Sprite s)
    {
        var im = UiKit.Pic(control, "art", s, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Pad * 2f, Pad * 2f));
        im.raycastTarget = false;
        return im;
    }

    /// a transparent rect that takes the finger, sized exactly
    public static Image Hit(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = UiKit.Rect(parent, name, aMin, aMax, pivot, pos, size);
        var im = rt.gameObject.AddComponent<Image>(); im.color = new Color(0, 0, 0, 0); im.raycastTarget = true;
        return im;
    }

    /// text-shadow 0 1px 3px rgba(0,18,42,a): a one-pixel drop, the blur left out
    public static void Shade(Graphic g, Color c, float a, Vector2 d)
    {
        var s = g.GetComponent<Shadow>();
        if (s == null) s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(c.r, c.g, c.b, a); s.effectDistance = d;
    }

    // ---- the chevron: the web's <path d="M14.5 5.5 8 12l6.5 6.5"/>, stroke 2, round, in 17px ----
    static Sprite chevron;
    public static Sprite Chevron()
    {
        if (chevron != null) return chevron;
        const int N = 68;
        var a0 = new Vector2(14.5f, 5.5f); var a1 = new Vector2(8f, 12f); var a2 = new Vector2(14.5f, 18.5f);
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "chevron" };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            var p = new Vector2((x + 0.5f) / N * 24f, (N - 1 - y + 0.5f) / N * 24f);
            float d = Mathf.Min(Seg(p, a0, a1), Seg(p, a1, a2));
            px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01((1f - d) * (N / 24f) + 0.5f));
        }
        tex.SetPixels(px); tex.Apply(false, true);
        chevron = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return chevron;
    }
    static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return (a + ab * t - p).magnitude;
    }

    /// a .go's contents when it carries the chevron: the 17px icon, 8px, the label, centred as one
    public static Text ChevronLabel(Transform p, string text, float size, Color col)
    {
        var t = UiKit.Label(p, text, size, col, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, size + 10f), TextAnchor.MiddleCenter, 0f);
        Shade(t, TextShade, 0.6f, new Vector2(0f, -1f));
        Tracking.On(t, 0.6f / size);                                          // .go: letter-spacing .6px
        float tw = t.preferredWidth + Tracking.Extra(text, size, 0.6f / size), total = 17f + 8f + tw;
        t.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + 25f + tw * 0.5f, 0f);
        var ic = UiKit.Pic(p, "chevron", Chevron(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-total * 0.5f + 8.5f, 0f), new Vector2(17f, 17f));
        ic.color = col; ic.raycastTarget = false;
        Shade(ic, TextShade, 0.6f, new Vector2(0f, -1f));
        return t;
    }
}
