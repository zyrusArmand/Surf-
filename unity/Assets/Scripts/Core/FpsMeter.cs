// For testing on a phone: in a Development Build (and only there) a small readout in the top
// corner says how the game is running, averaged over half a second: frames a second, the average
// frame time, and the worst single frame in that half second (a hitch shows there even when the
// average looks fine). Normal builds never create it.
using UnityEngine;

public class FpsMeter : MonoBehaviour
{
    float acc, worst; int frames; string text = "";
    GUIStyle style;

    public static void Install()
    {
        if (!Debug.isDebugBuild || Application.isEditor) return;
        var go = new GameObject("FpsMeter"); DontDestroyOnLoad(go); go.AddComponent<FpsMeter>();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        acc += dt; frames++; if (dt > worst) worst = dt;
        if (acc >= 0.5f)
        {
            text = (frames / acc).ToString("0") + " fps   " + (acc / frames * 1000f).ToString("0.0") + " ms   worst " + (worst * 1000f).ToString("0") + " ms";
            acc = 0f; frames = 0; worst = 0f;
        }
    }

    void OnGUI()
    {
        if (style == null) { style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.018f), fontStyle = FontStyle.Bold }; style.normal.textColor = Color.yellow; }
        GUI.Label(new Rect(Screen.width * 0.03f, Screen.height * 0.10f, Screen.width, Screen.height * 0.04f), text, style);
    }
}
