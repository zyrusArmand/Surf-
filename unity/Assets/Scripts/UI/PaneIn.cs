// A sheet opening: its column fades in and rises the last few points into place over a fifth
// of a second, eased out. Only the CONTENT moves; the sheet's opaque backing is there from the
// first frame, so nothing behind it ever shows through while it opens.
using UnityEngine;

public class PaneIn : MonoBehaviour
{
    public RectTransform target;
    CanvasGroup cg; Vector2 home; float e = 1f; bool homeSet;
    const float Dur = 0.20f, Rise = 14f;

    void OnEnable()
    {
        if (target == null) return;
        if (!homeSet) { home = target.anchoredPosition; homeSet = true; }
        cg = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
        e = 0f; Apply(0f);
    }

    void OnDisable() { if (target != null && homeSet) Apply(1f); }

    void Update()
    {
        if (target == null || e >= 1f) return;
        e = Mathf.Min(1f, e + Time.unscaledDeltaTime / Dur);
        Apply(e);
    }

    void Apply(float k)
    {
        float s = 1f - (1f - k) * (1f - k) * (1f - k);
        if (cg != null) cg.alpha = s;
        target.anchoredPosition = home + new Vector2(0f, -Rise * (1f - s));
    }
}
