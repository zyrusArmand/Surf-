// The opening, once per launch: the moment the world has built, the camera comes down out of the
// sky behind the beach and settles into the title's own shot, the SURF logo drops in with a splash
// once it is there, and the bar along the top fades in last. A tap anywhere skips straight to the
// end. After it the logo stays, bobbing a little, as the title's name.
using UnityEngine;
using UnityEngine.UI;

public class Intro : MonoBehaviour
{
    public RectTransform logo;           // the carved logo, under the title menu (Hud)
    public GameObject menu;              // the title menu, whose other parts fade in at the end

    const float Fly = 3.0f, LogoAt = 2.25f, LogoFor = 0.55f, BarAt = 2.75f, BarFor = 0.6f, End = 3.4f;
    float t = -1f;                       // < 0: not started; >= End: done
    bool landed;
    Vector2 logoHome; CanvasGroup logoCg;

    /// running, and taking the screen's taps for itself
    public bool Busy => t >= 0f && t < End;

    public void Begin()
    {
        if (logo != null)
        {
            logoHome = logo.anchoredPosition;
            logoCg = Group(logo.gameObject);
            logoCg.blocksRaycasts = false;
        }
        t = 0f; landed = false;
        Apply();
    }

    void Update()
    {
        if (Busy)
        {
            bool tap = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            t = tap && t > 0.2f ? End : t + Time.unscaledDeltaTime;
            Apply();
        }
        else if (t >= End && logo != null)
        {
            // settled: the logo rides a slow swell of its own
            float s = Time.unscaledTime;
            logo.anchoredPosition = logoHome + new Vector2(0f, Mathf.Sin(s * 1.3f) * 3f);
            logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(s * 0.9f + 1f) * 0.8f);
        }
    }

    void Apply()
    {
        // the logo, if there is one: dropped from above, a touch big and tipped, landing with a bounce
        float u = Mathf.Clamp01((t - LogoAt) / LogoFor);
        if (logo != null)
        {
            float back = u >= 1f ? 1f : 1f + 2.7f * Mathf.Pow(u - 1f, 3f) + 1.7f * Mathf.Pow(u - 1f, 2f);   // ease out, overshooting a little
            logoCg.alpha = Mathf.Clamp01(u * 3f);
            logo.anchoredPosition = logoHome + new Vector2(0f, (1f - back) * 140f);
            logo.localScale = Vector3.one * Mathf.LerpUnclamped(1.35f, 1f, back);
            logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-8f, 0f, back));
            if (!landed && u >= 0.62f) { landed = true; Sfx.Land(0.6f); Sfx.Buzz(14); }
        }
        // the bar and the rest of the title's overlay, last
        float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - BarAt) / BarFor));
        if (menu != null)
            foreach (Transform c in menu.transform)
            {
                if (c == logo) continue;
                var cg = Group(c.gameObject);
                cg.alpha = b; cg.blocksRaycasts = b > 0.99f;
            }
    }

    // (a missing component is Unity's own "null", which ?? does not see, so asked plainly)
    static CanvasGroup Group(GameObject go) { var g = go.GetComponent<CanvasGroup>(); return g != null ? g : go.AddComponent<CanvasGroup>(); }

    /// The camera, after the beach has put it in the title's shot: through the flight, moved back up
    /// to where the flight is. It starts up and behind, looking down at the board, and comes down
    /// onto the title's own place and angle, the lens widening as it lands.
    public void Place(Camera cam)
    {
        if (!Busy || cam == null) return;
        float u = Mathf.Clamp01(t / Fly);
        if (u >= 1f) return;
        float e = u * u * u * (u * (u * 6f - 15f) + 10f);          // smootherstep: no jolt at either end
        Vector3 p1 = cam.transform.position; Quaternion r1 = cam.transform.rotation; float f1 = cam.fieldOfView;
        Vector3 fwd = r1 * Vector3.forward, flat = new Vector3(fwd.x, 0f, fwd.z).normalized;
        Vector3 focus = p1 + fwd * 14f;
        Vector3 p0 = p1 - flat * 16f + Vector3.up * 10f;
        Quaternion r0 = Quaternion.LookRotation(focus - p0, Vector3.up);
        // a gentle arc rather than a straight slide: a little higher through the middle
        Vector3 p = Vector3.Lerp(p0, p1, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 1.5f;
        cam.transform.SetPositionAndRotation(p, Quaternion.Slerp(r0, r1, e));
        cam.fieldOfView = Mathf.Lerp(f1 - 18f, f1, e);
    }
}
