// The run card's few moving parts and the small pictures they need: the warm gold Again pill and
// the halo that breathes behind it, the NEW BEST ribbon and the shine that crosses it once, the
// goals' bar fills that run on from where the run found them, and the green tick of a goal done.
//
// Every picture here is made new and handed to the caller's list, so the card destroys it with
// everything else it drew (Hud.cardMade); nothing is cached and nothing is made per frame. The
// motion runs on unscaled time, because the game slows the clock at a wipeout and the card should
// not crawl with it.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardFx : MonoBehaviour
{
    // ---- the motion ----
    // Each run is told the seconds since the card first drew and says when it is finished. One
    // component drives them all, so a card is one Update however much on it moves.
    readonly List<System.Func<float, bool>> runs = new List<System.Func<float, bool>>();
    float t0 = -1f;

    public void Run(System.Func<float, bool> step) { if (step != null) runs.Add(step); }

    void Update()
    {
        if (t0 < 0f) t0 = Time.unscaledTime;
        float t = Time.unscaledTime - t0;
        for (int i = runs.Count - 1; i >= 0; i--)
        {
            bool done;
            try { done = runs[i](t); }
            catch (System.Exception e) { Debug.LogWarning("CardFx: " + e.Message); done = true; }
            if (done) runs.RemoveAt(i);
        }
    }

    static float EaseOut(float k) { k = 1f - Mathf.Clamp01(k); return 1f - k * k * k; }
    static float BackOut(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        k = Mathf.Clamp01(k) - 1f;
        return 1f + c3 * k * k * k + c1 * k * k;
    }

    /// a slow breath: the halo's alpha between lo and hi and the pill a hair larger at the top of it
    public void Breathe(Image halo, RectTransform art, float period, float lo, float hi, float grow)
    {
        Color c = halo != null ? halo.color : Color.clear;
        Run(t =>
        {
            if (halo == null) return true;
            float s = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / period);
            halo.color = new Color(c.r, c.g, c.b, Mathf.Lerp(lo, hi, s));
            if (art != null) art.localScale = new Vector3(1f + grow * s, 1f + grow * s, 1f);
            return false;
        });
    }

    /// scaled up from nothing with a small overshoot, starting `at` seconds in; then `then`
    public void Pop(RectTransform r, float at, float dur, System.Action then = null)
    {
        if (r == null) return;
        r.localScale = new Vector3(0f, 0f, 1f);
        Run(t =>
        {
            if (r == null) return true;
            float k = (t - at) / dur;
            if (k <= 0f) return false;
            float s = BackOut(k);
            r.localScale = new Vector3(s, s, 1f);
            if (k < 1f) return false;
            r.localScale = Vector3.one; then?.Invoke(); return true;
        });
    }

    /// across once from x0 to x1 (anchored x), shown only while it moves
    public void Sweep(RectTransform r, float at, float dur, float x0, float x1)
    {
        if (r == null) return;
        r.gameObject.SetActive(false);
        Run(t =>
        {
            if (r == null) return true;
            float k = (t - at) / dur;
            if (k <= 0f) return false;
            if (k >= 1f) { r.gameObject.SetActive(false); return true; }
            if (!r.gameObject.activeSelf) r.gameObject.SetActive(true);
            var p = r.anchoredPosition; p.x = Mathf.Lerp(x0, x1, Mathf.SmoothStep(0f, 1f, k)); r.anchoredPosition = p;
            return false;
        });
    }

    /// a bar's fill run from w0 to w1 wide, starting `at` seconds in; then `then`
    public void Grow(Image fill, float at, float dur, float w0, float w1, System.Action then = null)
    {
        if (fill == null) return;
        var r = fill.rectTransform;
        System.Action<float> set = w => { r.sizeDelta = new Vector2(w, r.sizeDelta.y); fill.enabled = w > 0.5f; };
        set(w0);
        Run(t =>
        {
            if (fill == null) return true;
            float k = (t - at) / dur;
            if (k <= 0f) return false;
            set(Mathf.Lerp(w0, w1, EaseOut(k)));
            if (k < 1f) return false;
            set(w1); then?.Invoke(); return true;
        });
    }

    // ---- the pictures ----
    static float Sd(float x, float y, float w, float h, float r)
    {
        r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }
    static float Phi(float x) => 1f / (1f + Mathf.Exp(-1.702f * x));

    static Sprite Make(Color[] px, int TW, int TH, string name, List<Object> made, float ppu = 100f, Vector4 border = default)
    {
        var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
        tex.SetPixels(px); tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, TW, TH), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
        made?.Add(tex); made?.Add(sp);
        return sp;
    }

    /// A filled capsule, opaque: top to bottom from `top` through `mid` to `bot`, a gloss over its
    /// upper half, a bright line just inside the top, a little warmth lost at the foot and a 1px
    /// rim. With `pad` > 0 there is that much room round it for a soft drop shadow of `shadowA`,
    /// laid over `backAt` (layout px from the capsule's top left) with the alpha that lands the
    /// way a browser's would, as ShopArt's pills do.
    public static Sprite Capsule(float w, float h, Color top, Color mid, Color bot, Color rim, float gloss, float pad, float shadowA,
                                 System.Func<float, float, Color> backAt, List<Object> made, string name)
    {
        float S = w > 150f ? 2f : 3f, r = h * 0.5f;
        int TW = Mathf.CeilToInt((w + 2f * pad) * S), TH = Mathf.CeilToInt((h + 2f * pad) * S);
        var px = new Color[TW * TH];
        Color shade = ShopArt.TextShade, foot = ShopArt.Rgb(176, 92, 8);
        for (int py = 0; py < TH; py++)
        for (int qx = 0; qx < TW; qx++)
        {
            float x = (qx + 0.5f) / S - pad, y = (TH - 1 - py + 0.5f) / S - pad;     // y runs down from the capsule's top
            float d = Sd(x, y, w, h, r);
            float cover = Mathf.Clamp01(0.5f - d * S);
            Color o = new Color(0f, 0f, 0f, 0f);
            if (cover < 1f && shadowA > 0f && pad > 0f)
            {
                float a = shadowA * (1f - Phi(Sd(x, y - 3f, w, h, r) / 5f));
                if (a > 0.002f)
                {
                    Color back = backAt != null ? backAt(x, y) : ShopArt.Rgb(70, 104, 124);
                    o = new Color(shade.r, shade.g, shade.b, ShopArt.LinA(a, shade, back));
                }
            }
            if (cover <= 0f) { px[py * TW + qx] = o; continue; }
            float v = Mathf.Clamp01(y / h);
            Color c = v < 0.5f ? Color.Lerp(top, mid, v / 0.5f) : Color.Lerp(mid, bot, (v - 0.5f) / 0.5f); c.a = 1f;
            // the gloss: an inset capsule over the upper half, white fading as it comes down
            float gIn = Mathf.Clamp01(0.5f - Sd(x - 3f, y - 1.5f, w - 6f, h * 0.5f, h * 0.25f) * S);
            if (gloss > 0f && gIn > 0f) c = ShopArt.Over(c, new Color(1f, 1f, 1f, gloss * gIn * (1f - Mathf.Clamp01((y - 1.5f) / (h * 0.5f)))));
            // the bright line along the inside of the top
            float hole = Mathf.Clamp01(0.5f - Sd(x, y - 1.4f, w, h, r) * S);
            float hiA = 0.70f * (1f - hole);
            if (hiA > 0f) c = ShopArt.Over(c, ShopArt.Rgb(255, 252, 236, hiA));
            // warmth lost at the foot
            float fa = 0.22f * Mathf.Clamp01((y - (h - 9f)) / 9f);
            if (fa > 0f) c = ShopArt.Over(c, new Color(foot.r, foot.g, foot.b, fa));
            // the rim: what is inside the shape but outside one px in
            float ci = Mathf.Clamp01(0.5f - (d + 1f) * S);
            float ringK = Mathf.Clamp01(1f - ci / Mathf.Max(cover, 1e-4f)) * rim.a;
            if (ringK > 0f) c = Color.Lerp(c, new Color(rim.r, rim.g, rim.b, 1f), ringK);
            c.a = cover;
            px[py * TW + qx] = ShopArt.Over(o, c);
        }
        return Make(px, TW, TH, name, made);
    }

    /// the Again pill: light gold at the top to amber at the foot, ShopArt.Pad of room round it
    public static Sprite GoldPill(float w, float h, System.Func<float, float, Color> backAt, List<Object> made)
        => Capsule(w, h, ShopArt.Rgb(255, 234, 150), ShopArt.Rgb(255, 202, 82), ShopArt.Rgb(240, 152, 34), ShopArt.Rgb(176, 98, 14, 0.55f),
                   0.32f, ShopArt.Pad, 0.30f, backAt, made, "again pill");

    /// a soft glow round a w by h capsule, white (the Image's colour tints it), ShopArt.Pad of room each side
    public static Sprite Halo(float w, float h, float blur, List<Object> made)
    {
        const float S = 1f;
        float P = ShopArt.Pad, r = h * 0.5f;
        int TW = Mathf.CeilToInt((w + 2f * P) * S), TH = Mathf.CeilToInt((h + 2f * P) * S);
        var px = new Color[TW * TH];
        for (int py = 0; py < TH; py++)
        for (int qx = 0; qx < TW; qx++)
        {
            float x = (qx + 0.5f) / S - P, y = (TH - 1 - py + 0.5f) / S - P;
            float d = Sd(x, y, w, h, r);
            px[py * TW + qx] = new Color(1f, 1f, 1f, d <= 0f ? 1f : 2f * (1f - Phi(d / (blur * 0.5f))));
        }
        return Make(px, TW, TH, "again halo", made);
    }

    /// a band of light for a shine to sweep: soft at both sides, white
    public static Sprite ShineBand(List<Object> made)
    {
        const int TW = 24, TH = 8;
        var px = new Color[TW * TH];
        for (int y = 0; y < TH; y++)
        for (int x = 0; x < TW; x++)
        {
            float u = ((x + 0.5f) / TW - 0.5f) / 0.22f;
            px[y * TW + x] = new Color(1f, 1f, 1f, Mathf.Exp(-u * u));
        }
        return Make(px, TW, TH, "shine", made);
    }

    /// A bar's fill, h tall with round ends, running from colour a to b across its own width,
    /// cut to stretch (Image.Type.Sliced) so the ends stay round at any width. Its pixels per
    /// unit are set for a canvas at the default 100 reference pixels per unit.
    public static Sprite BarFill(float h, Color a, Color b, List<Object> made)
    {
        const float S = 2f;
        const float w = 32f;
        float r = h * 0.5f;
        int TW = Mathf.CeilToInt(w * S), TH = Mathf.CeilToInt(h * S);
        var px = new Color[TW * TH];
        for (int py = 0; py < TH; py++)
        for (int qx = 0; qx < TW; qx++)
        {
            float x = (qx + 0.5f) / S, y = (TH - 1 - py + 0.5f) / S;
            var c = Color.Lerp(a, b, (qx + 0.5f) / TW); c.a = 1f;
            float hi = 0.30f * Mathf.Clamp01(1.2f - y);                 // a lighter line along the top
            if (hi > 0f) c = ShopArt.Over(c, new Color(1f, 1f, 1f, hi));
            c.a = Mathf.Clamp01(0.5f - Sd(x, y, w, h, r) * S);
            px[py * TW + qx] = c;
        }
        float bpx = r * S;
        return Make(px, TW, TH, "goal fill", made, 100f * S, new Vector4(bpx, 0f, bpx, 0f));
    }

    /// a goal done: a green disc d across with a white tick in it
    public static Sprite Tick(float d, List<Object> made)
    {
        const float S = 3f;
        int N = Mathf.CeilToInt(d * S);
        var px = new Color[N * N];
        Color top = ShopArt.Rgb(134, 238, 168), bot = ShopArt.Rgb(46, 182, 104), rim = ShopArt.Rgb(22, 120, 64);
        // the tick on a 16-unit square, y down
        var a0 = new Vector2(4.4f, 8.4f); var a1 = new Vector2(7f, 11f); var a2 = new Vector2(11.8f, 5.6f);
        float k = 16f / d;
        for (int py = 0; py < N; py++)
        for (int qx = 0; qx < N; qx++)
        {
            float x = (qx + 0.5f) / S, y = (N - 1 - py + 0.5f) / S;
            float dd = Sd(x, y, d, d, d * 0.5f);
            float cover = Mathf.Clamp01(0.5f - dd * S);
            if (cover <= 0f) { px[py * N + qx] = new Color(0f, 0f, 0f, 0f); continue; }
            Color c = Color.Lerp(top, bot, y / d); c.a = 1f;
            float ci = Mathf.Clamp01(0.5f - (dd + 1f) * S);
            c = Color.Lerp(new Color(rim.r, rim.g, rim.b, 1f), c, ci / Mathf.Max(cover, 1e-4f));
            var p = new Vector2(x * k, y * k);
            float s = Mathf.Min(Seg(p, a0, a1), Seg(p, a1, a2)) / k;     // back in layout px
            float ta = Mathf.Clamp01((0.95f - s) * S + 0.5f);
            if (ta > 0f) c = Color.Lerp(c, Color.white, ta);
            c.a = cover;
            px[py * N + qx] = c;
        }
        return Make(px, N, N, "goal tick", made);
    }
    static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return (a + ab * t - p).magnitude;
    }
}
