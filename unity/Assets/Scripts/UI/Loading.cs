// What shows while the game builds itself on launch, instead of a blank frame: the title's own
// sky (the beach's deep blue overhead into its warm horizon), the word Surf in the sign's gold,
// and a thin bar in the level bar's colours that fills as the pieces come up. It fades away once
// the beach is ready behind it.
using UnityEngine;
using UnityEngine.UI;

public class Loading : MonoBehaviour
{
    Image fill; CanvasGroup cg; float shown, want, fade = -1f;
    const float BarW = 232f;

    public static Loading Show()
    {
        var go = new GameObject("Loading", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 1000;
        var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(430, 932); sc.matchWidthOrHeight = 0f;
        var l = go.AddComponent<Loading>(); l.cg = go.GetComponent<CanvasGroup>();
        var root = go.transform;
        // the sky: a vertical ramp baked into a small texture
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color top = new Color32(0x2a, 0x6e, 0xa8, 255), mid = new Color32(0x9f, 0xc4, 0xde, 255), hz = new Color32(0xf7, 0xe2, 0xbb, 255);
        for (int y = 0; y < 64; y++) { float k = y / 63f; tex.SetPixel(0, y, k < 0.45f ? Color.Lerp(hz, mid, k / 0.45f) : Color.Lerp(mid, top, (k - 0.45f) / 0.55f)); }
        tex.Apply();
        var bg = new GameObject("sky", typeof(RectTransform), typeof(RawImage)); bg.transform.SetParent(root, false);
        var bgr = (RectTransform)bg.transform; bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.offsetMin = bgr.offsetMax = Vector2.zero;
        bg.GetComponent<RawImage>().texture = tex;
        UiKit.Label(root, "Surf", 72, new Color32(0xff, 0xd2, 0x4a, 255), FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(400, 96), TextAnchor.MiddleCenter, 0.6f);
        // the bar: the level bar's track and its orange-to-gold fill
        var track = Solid.Pic(root, "track", Solid.Rounded(BarW, 8, 4), new Color32(0x24, 0x39, 0x5f, 255), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(BarW, 8));
        var f = Solid.Pic(track.transform, "fill", Solid.Rounded(BarW, 8, 4), new Color32(0xff, 0xb8, 0x2c, 255), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BarW, 8));
        f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.fillOrigin = 0; f.fillAmount = 0.03f;
        l.fill = f;
        UiKit.Label(root, "v" + Application.version, 11, new Color(1, 1, 1, 0.7f), FontStyle.Bold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(200, 16), TextAnchor.MiddleCenter, 0.4f);
        return l;
    }

    public void Set(float k) { want = Mathf.Clamp01(k); }

    public void Hide() { want = 1f; fade = 0f; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.MoveTowards(shown, want, dt * 2.5f);
        if (fill != null) fill.fillAmount = Mathf.Max(0.03f, shown);
        if (fade >= 0f && shown >= 0.999f)
        {
            fade += dt / 0.45f;
            cg.alpha = 1f - fade; cg.blocksRaycasts = false;
            if (fade >= 1f) Destroy(gameObject);
        }
    }
}
