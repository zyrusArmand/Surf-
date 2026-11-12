// Settings: the web panel's three switches, music, sound effects and vibration, each a pill
// that says On or Off, the Test that buzzes the phone once whatever the switch says, the
// port's own Graphics choice, and under them the record, the best trick score and the version.
// The choices live in the Save; Sfx reads them, and Bootstrap applies the graphics one.
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    public static bool Music => !SaveData.S.musicOff;
    public static bool Sfx => !SaveData.S.sfxOff;
    public static bool Buzz => !SaveData.S.buzzOff;

    public System.Action onClose, onChange;
    GameObject pane; RectTransform content; ScrollRect scroll;
    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "settings", "Settings", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) { global::Sfx.Buzz(8); Fill(); } }

    void Switch(string label, bool on, System.Action flip)
    {
        var c = Pane.Card(content, "sw:" + label, 48);
        UiKit.Label(c, label, 14, Pane.Dim, FontStyle.Normal, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, 0), TextAnchor.MiddleLeft, 0.5f);
        // the web's switches all answer with buzz(14), heard and felt with the new setting
        Pane.Button(c, on ? "On" : "Off", 72, on, () => { flip(); SaveData.Flush(); onChange?.Invoke(); global::Sfx.Buzz(14); Fill(); });
    }

    void Fill()
    {
        Pane.Clear(content);
        var s = SaveData.S;
        Switch("Music", !s.musicOff, () => s.musicOff = !s.musicOff);
        // turning sound on plays what it sounds like: the board landing
        Switch("Sound effects", !s.sfxOff, () => { s.sfxOff = !s.sfxOff; if (!s.sfxOff) global::Sfx.Play("land", 0.6f, 1.0f); });
        Switch("Vibration", !s.buzzOff, () => s.buzzOff = !s.buzzOff);
        // Test changes nothing and only buzzes -- even with Vibration off, because "can this phone
        // do it at all" is a different question from "do I want it while I surf"
        var tc = Pane.Card(content, "sw:Feel a tap", 48);
        UiKit.Label(tc, "Feel a tap", 14, Pane.Dim, FontStyle.Normal, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, 0), TextAnchor.MiddleLeft, 0.5f);
        Pane.Button(tc, "Test", 72, false, () =>
        {
            global::Sfx.Play("btn", 0.50f, 1f);
#if UNITY_ANDROID || UNITY_IOS
            try { Handheld.Vibrate(); } catch { }
#endif
        });
        // the picture: High is everything; Fast keeps the look but drops the costly extras for an older phone
        var g = Pane.Card(content, "sw:Graphics", 48);
        UiKit.Label(g, "Graphics", 14, Pane.Dim, FontStyle.Normal, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, 0), TextAnchor.MiddleLeft, 0.5f);
        Pane.Button(g, s.fastFx ? "Fast" : "High", 72, !s.fastFx, () => { s.fastFx = !s.fastFx; SaveData.Flush(); onChange?.Invoke(); Fill(); });
        Pane.Note(content, "Vibration is the phone’s own buzzer, so it does what the phone allows: on an iPhone that is one short tap, and only with System Haptics on.", 48, 11.5f);
        Pane.Row(content, "Your record", Pane.N(s.bestDist) + " m");
        Pane.Row(content, "Best trick score", Pane.N(s.best));
        Pane.Row(content, "Version", "v" + Application.version);
        scroll.verticalNormalizedPosition = 1f;
    }
}
