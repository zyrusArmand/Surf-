// The placeholder clip the run card's offers and its free life play (the web's #clip and
// watchClip, web 2086-2103, 3247-3252, 27202-27255): a full-screen sheet of deep blue with a
// little sunset picture on it, a sheen sweeping across the picture, and "Back on the board in 5"
// counting down a second at a time. When it reaches nought the sheet goes and the reward is
// handed over. A real build hands this slot to a rewarded-ad SDK; nothing here talks to a
// network. It runs on the real clock, as the web's setInterval does, and it takes every tap
// while it is up so nothing under it can be pressed twice.
using UnityEngine;
using UnityEngine.UI;

public class AdClip : MonoBehaviour
{
    const float Secs = 5f, SweepSecs = 2.1f;
    System.Action then; float t; int shown = -1; Text count; RectTransform sweep; float artW;
    static Sprite artSp; static Texture2D sheetTex, sweepTex; static float artWas, artHWas;

    /// puts the clip up over everything under `root` and calls `then` when it has played
    public static AdClip Play(Transform root, System.Action then)
    {
        var rt = UiKit.Rect(root, "clip", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.SetAsLastSibling();
        var c = rt.gameObject.AddComponent<AdClip>();
        c.then = then;
        c.Build(rt);
        return c;
    }

    void Build(RectTransform rt)
    {
        var parent = rt.parent as RectTransform;
        float W = parent != null ? parent.rect.width : 430f, H = parent != null ? parent.rect.height : 932f;
        // #clip: linear-gradient(180deg, rgba(20,88,150,.86) 0%, rgba(11,56,106,.90) 46%, rgba(5,28,56,.94) 100%)
        var bg = rt.gameObject.AddComponent<RawImage>();
        bg.texture = Sheet(); bg.raycastTarget = true;                      // it swallows every tap while it plays
        // .clipbox: min(520px, 100%) inside 18px of padding, centred; the picture min(46vh, 260px), 16 under it, then the line
        float boxW = Mathf.Min(520f, W - 36f), artH = Mathf.Min(0.46f * H, 260f), msgH = 24f, boxH = artH + 16f + msgH;
        artW = boxW;
        var box = UiKit.Rect(rt, "clipbox", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(boxW, boxH));
        var art = UiKit.Pic(box, "clipart", Art(boxW, artH), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(boxW, artH));
        art.raycastTarget = false;
        art.gameObject.AddComponent<Mask>().showMaskGraphic = true;         // overflow:hidden on the rounded picture, so the sheen keeps inside it
        // .sweep: a pale diagonal sheen crossing the picture from off its left edge to off its right, over and over
        var sw = new GameObject("sweep", typeof(RectTransform)).GetComponent<RectTransform>();
        sw.SetParent(art.transform, false);
        sw.anchorMin = Vector2.zero; sw.anchorMax = Vector2.one; sw.pivot = new Vector2(0.5f, 0.5f); sw.sizeDelta = Vector2.zero;
        var si = sw.gameObject.AddComponent<RawImage>(); si.texture = Sweep(); si.raycastTarget = false;
        sweep = sw;
        // .clipmsg: 20px bold, "Back on the board in" and the count in bold
        count = UiKit.Label(box, "", 20, Color.white, FontStyle.Bold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(boxW, msgH), TextAnchor.MiddleCenter, 0f);
        count.supportRichText = true;
        Tick();
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (sweep != null)
        {
            float k = (t % SweepSecs) / SweepSecs;                        // translateX(-100%) to translateX(100%)
            sweep.anchoredPosition = new Vector2((-1f + 2f * k) * artW, 0f);
        }
        Tick();
        if (t >= Secs)
        {
            var cb = then; then = null;
            Destroy(gameObject);
            cb?.Invoke();
        }
    }

    void Tick()
    {
        int n = Mathf.Max(0, Mathf.CeilToInt(Secs - t - 1e-4f));
        if (n == shown || count == null) return;
        shown = n; count.text = "Back on the board in <b>" + n + "</b>";
    }

    // ---- the pictures, drawn once ----
    static Texture2D Sheet()
    {
        if (sheetTex != null) return sheetTex;
        const int N = 128;
        sheetTex = new Texture2D(1, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "clip sheet" };
        Color a = Solid.Rgba(20, 88, 150, 0.86f), b = Solid.Rgba(11, 56, 106, 0.90f), c = Solid.Rgba(5, 28, 56, 0.94f);
        for (int y = 0; y < N; y++)
        {
            float v = 1f - (y + 0.5f) / N;                                  // 0 at the top
            sheetTex.SetPixel(0, y, v < 0.46f ? Color.Lerp(a, b, v / 0.46f) : Color.Lerp(b, c, (v - 0.46f) / 0.54f));
        }
        sheetTex.Apply(false, true);
        return sheetTex;
    }

    /// .clipart: the dusk sky down to the horizon at 62%, the sea under it with its fine pale lines
    /// every twelve pixels, and the sun at 44% down with its glow; corners rounded at 16
    static Sprite Art(float w, float h)
    {
        if (artSp != null && Mathf.Approximately(artWas, w) && Mathf.Approximately(artHWas, h)) return artSp;
        const float S = 2f;
        int W = Mathf.CeilToInt(w * S), H = Mathf.CeilToInt(h * S);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "clip art" };
        var px = new Color[W * H];
        Color s0 = Solid.Rgba(0x3a, 0x2a, 0x6b), s1 = Solid.Rgba(0xb4, 0x49, 0x6a), s2 = Solid.Rgba(0xff, 0x8a, 0x3c), w0 = Solid.Rgba(0x1d, 0x7f, 0xc4), w1 = Solid.Rgba(0x0d, 0x4f, 0x8c);
        Color sun = Solid.Rgba(0xff, 0xe9, 0xb0), glow = Solid.Rgba(255, 190, 110);
        float cx = w * 0.5f, cy = h * 0.44f, r = 37f;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float ux = (x + 0.5f) / S, uy = h - (y + 0.5f) / S;            // layout px, y down from the top
            float v = uy / h;
            Color c = v < 0.42f ? Color.Lerp(s0, s1, v / 0.42f) : v < 0.62f ? Color.Lerp(s1, s2, (v - 0.42f) / 0.20f) : Color.Lerp(w0, w1, (v - 0.62f) / 0.38f);
            // box-shadow 0 0 60px 24px: the glow spreads 24 past the disc and blurs over 60
            float d = Mathf.Sqrt((ux - cx) * (ux - cx) + (uy - cy) * (uy - cy));
            float g = 1f - Mathf.Clamp01((d - (r + 24f - 30f)) / 60f);
            g = g * g * (3f - 2f * g);
            c = Color.Lerp(c, glow, 0.55f * g);
            c = Color.Lerp(c, sun, Mathf.Clamp01(r - d + 0.5f));
            // .sea: repeating-linear-gradient(180deg, rgba(255,255,255,.12) 0 2px, transparent 2px 12px)
            if (v >= 0.62f && ((uy - h * 0.62f) % 12f) < 2f) c = Color.Lerp(c, Color.white, 0.12f);
            // border-radius 16
            float qx = Mathf.Abs(ux - w * 0.5f) - (w * 0.5f - 16f), qy = Mathf.Abs(uy - h * 0.5f) - (h * 0.5f - 16f);
            float sd = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - 16f;
            c.a = Mathf.Clamp01(0.5f - sd * S);
            px[y * W + x] = c;
        }
        tex.SetPixels(px); tex.Apply(false, true);
        artWas = w; artHWas = h;
        artSp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), S);
        return artSp;
    }

    /// linear-gradient(105deg, transparent 38%, rgba(255,255,255,.28) 50%, transparent 62%)
    static Texture2D Sweep()
    {
        if (sweepTex != null) return sweepTex;
        const int W = 256, H = 128;
        sweepTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "clip sweep" };
        // 105deg runs a little below the horizontal, left to right: the bands lean 15 degrees off upright
        float ang = 105f * Mathf.Deg2Rad; var dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
        float len = Mathf.Abs(W * dir.x) + Mathf.Abs(H * dir.y);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            var p = new Vector2(x + 0.5f - W * 0.5f, -(y + 0.5f - H * 0.5f));
            float k = Vector2.Dot(p, dir) / len + 0.5f;
            float a = k < 0.38f || k > 0.62f ? 0f : (k < 0.5f ? (k - 0.38f) / 0.12f : (0.62f - k) / 0.12f) * 0.28f;
            sweepTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        sweepTex.Apply(false, true);
        return sweepTex;
    }
}
