// The phone web version's lighter settings (Bootstrap calls Apply on a phone in a web build only;
// the editor, the desktop builds and the native ones never see this): no shadows, no anti-aliasing,
// no HDR, a little under full resolution -- and a frame counter in the corner, so how fast a phone
// actually runs it can be read off a screenshot. The sea's coarser grid is in Ocean.Build.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class WebPhone : MonoBehaviour
{
    public static void Apply(Hud hud)
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.shadowDistance = 0f;
            urp.msaaSampleCount = 1;
            urp.supportsHDR = false;
            urp.renderScale = 0.85f;
        }
        QualitySettings.shadows = ShadowQuality.Disable;
        var go = new GameObject("WebPhone");
        var w = go.AddComponent<WebPhone>();
        if (hud != null && hud.UiRoot != null)
        {
            w.label = UiKit.Label(hud.UiRoot, "", 11, new Color(1f, 1f, 1f, 0.7f), FontStyle.Bold, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8, 6), new Vector2(160, 16), TextAnchor.LowerLeft, 0.8f);
            w.label.raycastTarget = false;
        }
    }

    Text label; int frames; float t, worst;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        frames++; t += dt; worst = Mathf.Max(worst, dt);
        if (t < 0.5f) return;
        if (label != null) label.text = Mathf.RoundToInt(frames / t) + " fps  worst " + Mathf.RoundToInt(worst * 1000f) + " ms";
        frames = 0; t = 0f; worst = 0f;
    }
}
