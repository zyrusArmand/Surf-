// Plain white shapes, for the places the stylesheet uses a flat colour rather than the glass:
// the wheel pip, the run card's tiles and goal rows, a bar's gradient fill. UiKit's sprites
// carry their own fill, so tinting them with Image.color multiplies into the glass; these are
// white and opaque, and the Image's colour is the colour you get.
using UnityEngine;
using UnityEngine.UI;

public static class Solid
{
    const float Scale = 2f;
    static readonly System.Collections.Generic.Dictionary<string, Sprite> cache = new System.Collections.Generic.Dictionary<string, Sprite>();

    // signed-distance coverage of a rounded rectangle w by h with corner radius r, at layout point (ux, uy)
    static float Cover(float w, float h, float r, float ux, float uy, float ring)
    {
        float cx = w * 0.5f, cy = h * 0.5f, hx = w * 0.5f, hy = h * 0.5f;
        float qx = Mathf.Abs(ux - cx) - (hx - r), qy = Mathf.Abs(uy - cy) - (hy - r);
        float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        float cover = Mathf.Clamp01(0.5f - d * Scale);
        if (ring > 0) cover = Mathf.Clamp01(cover - Mathf.Clamp01(0.5f - (d + ring) * Scale));
        return cover;
    }

    /// a white rounded rectangle; `ring` > 0 draws only a ring of that width inside the edge
    public static Sprite Rounded(float w, float h, float r, float ring = 0f)
    {
        string key = "s:" + w + "x" + h + ":" + r + ":" + ring;
        if (cache.TryGetValue(key, out var sp) && sp != null) return sp;
        r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
        int W = Mathf.CeilToInt(w * Scale), H = Mathf.CeilToInt(h * Scale);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            px[y * W + x] = new Color(1, 1, 1, Cover(w, h, r, (x + 0.5f) / Scale, (y + 0.5f) / Scale, ring));
        tex.SetPixels(px); tex.Apply(false, true);
        sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), Scale);
        cache[key] = sp; return sp;
    }

    public static Sprite Circle(float d, float ring = 0f) => Rounded(d, d, d / 2, ring);

    /// a left-to-right gradient between two colours, with rounded ends
    public static Sprite Gradient(float w, float h, float r, Color a, Color b)
    {
        string key = "g:" + w + "x" + h + ":" + r + ":" + a + ":" + b;
        if (cache.TryGetValue(key, out var sp) && sp != null) return sp;
        r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
        int W = Mathf.CeilToInt(w * Scale), H = Mathf.CeilToInt(h * Scale);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            var c = Color.Lerp(a, b, (x + 0.5f) / W);
            c.a = Cover(w, h, r, (x + 0.5f) / Scale, (y + 0.5f) / Scale, 0f);
            px[y * W + x] = c;
        }
        tex.SetPixels(px); tex.Apply(false, true);
        sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), Scale);
        cache[key] = sp; return sp;
    }

    /// a tinted shape placed like UiKit.Pic, not a raycast target
    public static Image Pic(Transform parent, string name, Sprite s, Color col, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var im = UiKit.Pic(parent, name, s, aMin, aMax, pivot, pos, size);
        im.color = col; im.raycastTarget = false;
        return im;
    }

    public static Color Rgba(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
}
