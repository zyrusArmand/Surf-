// The run card's glass, drawn the way the browser draws it (web 1525-1739, the .over rules).
//
// Everything on that card is frosted glass: the card itself is backdrop-filter: blur(16px)
// saturate(135%) over the wipeout, and so are the Again pill, the three discs and the two
// offers (blur(13px)). A canvas cannot blur what is behind it, but the frame behind it is
// known: it is the moment the run ended. So the frame is taken once (Grab), shrunk and blurred
// here on the CPU, and every surface on the card is composited over it in sRGB, the way the
// browser blends -- which is also what keeps a 20% white from coming out several times
// brighter, as it does when Unity blends it in linear space (see ShopArt). The card is then
// one opaque picture, its tiles, rows and bars painted into it; only the type and the icons
// go over it as UI.
//
// The scrim behind the card (#overlay.over's radial wash) stays a live layer over the moving
// sea, its alpha set per texel to land where the browser's does over that same frame.
using System.Collections.Generic;
using UnityEngine;

public static class CardArt
{
    // ---- the frame behind the card ----
    /// what is behind the card, softened: sRGB colour at layout px (x across 430, y down from the top)
    public class Backdrop
    {
        public int w, h; public Color[] px; public float W, H; public Color avg;

        public Color At(float x, float y)
        {
            if (px == null) return avg;
            float u = x / W * w - 0.5f, v = (1f - y / H) * h - 0.5f;        // texel rows run bottom-up
            int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, w - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = Mathf.Clamp01(u - x0), fy = Mathf.Clamp01(v - y0);
            var a = Color.Lerp(px[y0 * w + x0], px[y0 * w + x1], fx);
            var b = Color.Lerp(px[y1 * w + x0], px[y1 * w + x1], fx);
            var c = Color.Lerp(a, b, fy); c.a = 1f; return c;
        }

        /// the scene with the scrim over it: what the glass is laid on
        public Color Under(float x, float y) => ShopArt.Over(At(x, y), Scrim(x, y, W, H));
    }

    /// the frame as it was, shrunk to a texel about every four layout px and blurred by `blur`
    /// layout px (a CSS blur radius is the gaussian's sigma)
    public static Backdrop Grab(Texture2D shot, float W, float H, float blur)
    {
        if (shot == null || shot.width < 8 || shot.height < 8) return null;
        var src = shot.GetPixels32();
        int sw = shot.width, sh = shot.height;
        float perCss = sw / Mathf.Max(1f, W);
        int f = Mathf.Max(1, Mathf.RoundToInt(4f * perCss));
        int w = Mathf.Max(2, sw / f), h = Mathf.Max(2, sh / f);
        int step = Mathf.Max(1, f / 3);            // a sample every few pixels is plenty for what is about to be blurred
        var px = new Color[w * h];
        double ar = 0, ag = 0, ab = 0;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int r = 0, g = 0, b = 0, n = 0;
            for (int yy = 0; yy < f; yy += step)
            {
                int row = (y * f + yy) * sw + x * f;
                for (int xx = 0; xx < f; xx += step) { var c = src[row + xx]; r += c.r; g += c.g; b += c.b; n++; }
            }
            var col = new Color(r / (255f * n), g / (255f * n), b / (255f * n), 1f);
            px[y * w + x] = col; ar += col.r; ag += col.g; ab += col.b;
        }
        float texelCss = f / perCss;
        Gauss(px, w, h, Mathf.Max(0.5f, blur / texelCss));
        int all = w * h;
        // a frame that came back black is a capture that did not work, not a black sea
        if ((ar + ag + ab) / all < 0.01) return null;
        return new Backdrop { w = w, h = h, px = px, W = W, H = H, avg = new Color((float)(ar / all), (float)(ag / all), (float)(ab / all), 1f) };
    }

    /// when there is no frame to be had: one even colour, the evening sea the card usually sits on
    public static Backdrop Flat(Color c, float W, float H) => new Backdrop { W = W, H = H, avg = c };

    static void Gauss(Color[] px, int w, int h, float sigma)
    {
        int R = Mathf.Clamp(Mathf.CeilToInt(sigma * 3f), 1, 40);
        var k = new float[2 * R + 1]; float sum = 0f;
        for (int i = -R; i <= R; i++) { k[i + R] = Mathf.Exp(-0.5f * i * i / (sigma * sigma)); sum += k[i + R]; }
        for (int i = 0; i < k.Length; i++) k[i] /= sum;
        var tmp = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color c = Color.clear;
            for (int i = -R; i <= R; i++) c += px[y * w + Mathf.Clamp(x + i, 0, w - 1)] * k[i + R];
            tmp[y * w + x] = c;
        }
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color c = Color.clear;
            for (int i = -R; i <= R; i++) c += tmp[Mathf.Clamp(y + i, 0, h - 1) * w + x] * k[i + R];
            c.a = 1f; px[y * w + x] = c;
        }
    }

    // ---- the scrim: radial-gradient(150% 108% at 50% 46%, rgba(4,22,42,.46) 0, .38 42%, .20 74%, .05 100%) ----
    public static readonly Color Ink = ShopArt.Rgb(4, 22, 42);
    public static Color Scrim(float x, float y, float W, float H)
    {
        float ex = (x - 0.5f * W) / (1.5f * W), ey = (y - 0.46f * H) / (1.08f * H), rho = Mathf.Sqrt(ex * ex + ey * ey);
        float a = rho < 0.42f ? Mathf.Lerp(0.46f, 0.38f, rho / 0.42f) : rho < 0.74f ? Mathf.Lerp(0.38f, 0.20f, (rho - 0.42f) / 0.32f)
                : rho < 1f ? Mathf.Lerp(0.20f, 0.05f, (rho - 0.74f) / 0.26f) : 0.05f;
        return new Color(Ink.r, Ink.g, Ink.b, a);
    }

    /// the scrim as a screen-sized picture, each texel's alpha the one that lands, blended in linear,
    /// where the browser's lands over the frame behind it
    public static Texture2D ScrimTexture(Backdrop bd)
    {
        int w = 48, h = Mathf.Clamp(Mathf.RoundToInt(48f * bd.H / bd.W), 48, 160);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "run card scrim" };
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            float x = (i + 0.5f) / w * bd.W, y = (1f - (j + 0.5f) / h) * bd.H;
            var s = Scrim(x, y, bd.W, bd.H);
            px[j * w + i] = new Color(Ink.r, Ink.g, Ink.b, ShopArt.LinA(s.a, Ink, bd.At(x, y)));
        }
        tex.SetPixels(px); tex.Apply(false, true);
        return tex;
    }

    // ---- the card and what is painted on it ----
    /// one rounded box on the card, in card px (0,0 at the card's top left, its 1px border included)
    public class Box
    {
        public float x, y, w, h, r;
        public Color c0, c1, c2; public float p1 = -1f; public bool across;   // the fill: c0 to c1 (then to c2 from p1); across runs left to right
        public Color edge; public float edgeW = 1f;                             // border
        public Color hi; public float hiY = 1f, hiBlur;                         // inset 0 hiY hiBlur hi
        public Color ring;                                                      // 0 0 0 1px ring, outside
        public Color glow; public float glowY, glowBlur;                        // 0 glowY glowBlur glow, outside
        public Color under;                                                     // 0 1px 0 under, a hairline below
        public Box clip;                                                        // overflow:hidden on this box's parent
    }

    static float Sd(float x, float y, float w, float h, float r)
    {
        r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }
    static float Phi(float x) => 1f / (1f + Mathf.Exp(-1.702f * x));
    const float S = 2f;          // texels per layout px

    static Color Fill(Box b, float x, float y)
    {
        float t = b.across ? Mathf.Clamp01((x - b.x) / Mathf.Max(1f, b.w)) : Mathf.Clamp01((y - b.y) / Mathf.Max(1f, b.h));
        if (b.p1 < 0f) return Color.Lerp(b.c0, b.c1, t);
        return t < b.p1 ? Color.Lerp(b.c0, b.c1, t / b.p1) : Color.Lerp(b.c1, b.c2, (t - b.p1) / Mathf.Max(0.001f, 1f - b.p1));
    }

    /// `col` with box b laid over it at card point (x, y)
    static Color Paint(Color col, Box b, float x, float y)
    {
        float lx = x - b.x, ly = y - b.y;
        float reach = Mathf.Max(b.glowBlur * 1.6f + Mathf.Abs(b.glowY), 2f);
        if (lx < -reach || ly < -reach || lx > b.w + reach || ly > b.h + reach) return col;
        float d = Sd(lx, ly, b.w, b.h, b.r);
        float cover = Mathf.Clamp01(0.5f - d * S);
        float clipK = 1f;
        if (b.clip != null) clipK = Mathf.Clamp01(0.5f - Sd(x - b.clip.x, y - b.clip.y, b.clip.w, b.clip.h, b.clip.r) * S);
        // outside: the glow, the ring, the hairline under
        if (cover < 1f)
        {
            float out1 = 1f - cover;
            if (b.glow.a > 0f)
            {
                float a = b.glow.a * (1f - Phi(Sd(lx, ly - b.glowY, b.w, b.h, b.r) / Mathf.Max(0.5f, b.glowBlur * 0.5f))) * out1 * clipK;
                if (a > 0f) col = ShopArt.Over(col, new Color(b.glow.r, b.glow.g, b.glow.b, a));
            }
            if (b.ring.a > 0f)
            {
                float a = b.ring.a * Mathf.Clamp01(0.5f - (d - 1f) * S) * out1;
                if (a > 0f) col = ShopArt.Over(col, new Color(b.ring.r, b.ring.g, b.ring.b, a));
            }
            if (b.under.a > 0f)
            {
                float a = b.under.a * Mathf.Clamp01(0.5f - Sd(lx, ly - 1f, b.w, b.h, b.r) * S) * out1;
                if (a > 0f) col = ShopArt.Over(col, new Color(b.under.r, b.under.g, b.under.b, a));
            }
        }
        if (cover <= 0f) return col;
        cover *= clipK;
        // inside: the fill, the inset light, the border
        var f = Fill(b, x, y);
        Color inside = ShopArt.Over(col, f);
        float bw = b.edge.a > 0f ? b.edgeW : 0f;
        float di = Sd(lx - bw, ly - bw, b.w - 2f * bw, b.h - 2f * bw, Mathf.Max(0f, b.r - bw));
        float ci = Mathf.Clamp01(0.5f - di * S);
        if (b.hi.a > 0f)
        {
            float hole = Sd(lx - bw, ly - bw - b.hiY, b.w - 2f * bw, b.h - 2f * bw, Mathf.Max(0f, b.r - bw));
            float a = b.hiBlur > 0f ? b.hi.a * Phi(hole / (b.hiBlur * 0.5f)) * ci : b.hi.a * ci * (1f - Mathf.Clamp01(0.5f - hole * S));
            if (a > 0f) inside = ShopArt.Over(inside, new Color(b.hi.r, b.hi.g, b.hi.b, a));
        }
        if (bw > 0f)
        {
            float ra = Mathf.Clamp01(1f - ci) * b.edge.a;
            if (ra > 0f) inside = ShopArt.Over(inside, new Color(b.edge.r, b.edge.g, b.edge.b, ra));
        }
        return Color.Lerp(col, inside, cover);
    }

    /// the card: `card` is the card's own box (its fill, border and inset light), `boxes` what is
    /// painted on it in order; it sits at (X, Y) layout px on a screen whose frame is `bd`.
    /// Returns the picture, Pad bigger than the card on every side for its shadow; `seen` then
    /// answers what colour any card point came out, for setting type at an opacity over it.
    public const float Pad = 44f;
    public static Sprite Bake(Box card, List<Box> boxes, float X, float Y, Backdrop bd, out System.Func<float, float, Color> seen)
    {
        // box-shadow 0 12px 30px rgba(0,16,36,.38), 0 2px 6px rgba(0,14,30,.26): soft, so set
        // against the frame's average rather than texel by texel
        Color sh1 = ShopArt.Rgb(0, 16, 36), sh2 = ShopArt.Rgb(0, 14, 30);
        var lut = new float[33];
        for (int i = 0; i <= 32; i++) lut[i] = ShopArt.LinA(i / 32f, sh1, bd.avg);
        System.Func<float, float, Color> inside = (x, y) =>
        {
            Color c = ShopArt.Saturate(bd.Under(X + x, Y + y), 1.35f); c.a = 1f;    // backdrop-filter: blur, then saturate
            c = Paint(c, card, x, y);
            for (int i = 0; i < boxes.Count; i++) c = Paint(c, boxes[i], x, y);
            c.a = 1f; return c;
        };
        seen = inside;
        float cw = card.w, ch = card.h;
        int TW = Mathf.CeilToInt((cw + 2f * Pad) * S), TH = Mathf.CeilToInt((ch + 2f * Pad) * S);
        var px = new Color[TW * TH];
        for (int py = 0; py < TH; py++)
        for (int qx = 0; qx < TW; qx++)
        {
            float x = (qx + 0.5f) / S - Pad, y = (TH - 1 - py + 0.5f) / S - Pad;
            float d = Sd(x, y, cw, ch, card.r);
            float cover = Mathf.Clamp01(0.5f - d * S);
            Color o = new Color(0f, 0f, 0f, 0f);
            if (cover < 1f)
            {
                float a1 = 0.38f * (1f - Phi(Sd(x, y - 12f, cw, ch, card.r) / 15f));
                float a2 = 0.26f * (1f - Phi(Sd(x, y - 2f, cw, ch, card.r) / 3f));
                float a = a1 + a2 * (1f - a1);
                if (a > 0.002f)
                {
                    float k = Mathf.Clamp01(a) * 32f; int i0 = Mathf.Min(31, (int)k);
                    var c = Color.Lerp(sh2, sh1, a1 / Mathf.Max(a, 1e-4f));
                    o = new Color(c.r, c.g, c.b, Mathf.Lerp(lut[i0], lut[i0 + 1], k - i0));
                }
            }
            if (cover <= 0f) { px[py * TW + qx] = o; continue; }
            var ins = inside(x, y);
            px[py * TW + qx] = ShopArt.Over(o, new Color(ins.r, ins.g, ins.b, cover));
        }
        var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "run card" };
        tex.SetPixels(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, TW, TH), new Vector2(0.5f, 0.5f), S);
    }
}
