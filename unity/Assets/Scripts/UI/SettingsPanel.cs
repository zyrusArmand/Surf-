// Settings: the web panel's three switches, music, sound effects and vibration, each a pill
// that says On or Off, and under them the record, the best trick score and the version. The
// choices live in the Save; nothing listens to them yet beyond the static accessors here.
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

    public void Show(bool on) { pane.SetActive(on); if (on) Fill(); }

    void Switch(string label, bool on, System.Action flip)
    {
        var c = Pane.Card(content, "sw:" + label, 48);
        UiKit.Label(c, label, 14, Pane.Dim, FontStyle.Normal, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, 0), TextAnchor.MiddleLeft, 0.5f);
        Pane.Button(c, on ? "On" : "Off", 72, on, () => { flip(); SaveData.Flush(); onChange?.Invoke(); Fill(); });
    }

    void Fill()
    {
        Pane.Clear(content);
        var s = SaveData.S;
        Switch("Music", !s.musicOff, () => s.musicOff = !s.musicOff);
        Switch("Sound effects", !s.sfxOff, () => s.sfxOff = !s.sfxOff);
        Switch("Vibration", !s.buzzOff, () => s.buzzOff = !s.buzzOff);
        Pane.Note(content, "Vibration is the phone’s own buzzer, so it does what the phone allows: on an iPhone that is one short tap, and only with System Haptics on.", 48, 11.5f);
        Pane.Row(content, "Your record", Pane.N(s.bestDist) + " m");
        Pane.Row(content, "Best trick score", Pane.N(s.best));
        Pane.Row(content, "Version", "v" + Application.version);
        scroll.verticalNormalizedPosition = 1f;
    }
}
