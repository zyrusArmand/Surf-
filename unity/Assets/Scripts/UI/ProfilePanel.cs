// Who you are: the web's #profile, behind the avatar on the top bar. Your name (tap it to change
// it), the rank and the level, how far through it you are, the purse and the record, and the way
// into Stats, because "your surfing so far" belongs under your own name rather than beside it.
using UnityEngine;
using UnityEngine.UI;

public class ProfilePanel : MonoBehaviour
{
    public System.Action onClose, onStats;
    GameObject pane; RectTransform content; ScrollRect scroll;
    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "profile", "Profile", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) { Sfx.Buzz(8); Fill(); } }

    void Fill()
    {
        Pane.Clear(content);
        var s = SaveData.S;
        int L = Progress.Level;
        bool capped = L >= Progress.Cap;
        int a = Progress.XpAt(L), b = capped ? a : Progress.XpAt(L + 1);
        string to = capped ? "MAX" : Pane.N(s.xp - a) + " / " + Pane.N(b - a);
        NameRow();
        Pane.Row(content, "Rank", Progress.Rank);
        Pane.Row(content, "Level", L.ToString());
        Pane.Bar(content, "Progress", to, Progress.Frac);
        Pane.Row(content, "Puka", Pane.N(s.coins));
        Pane.Row(content, "Best distance", Pane.N(s.bestDist) + " m");
        Pane.Go(content, "Your surfing so far", () => { Show(false); onStats?.Invoke(); });
        scroll.verticalNormalizedPosition = 1f;
    }

    // The name sits on a pill the way the web's .sw does, and the pill is the field: a tap brings
    // the keyboard up (the web asks with a prompt, which a phone draws the same way), and the
    // new name is kept when the keyboard goes. Empty reads "Surfer", as the web's unameOr does.
    void NameRow()
    {
        var c = Pane.Card(content, "row:Name", 44);
        UiKit.Label(c, "Name", 14, Pane.Dim, FontStyle.Normal, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, 0), TextAnchor.MiddleLeft, 0.5f);
        var pill = Pane.Button(c, "", 160, false, null);
        var txt = pill.GetComponentInChildren<Text>();
        txt.color = Color.white; txt.supportRichText = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;          // the field scrolls its own text; overflow would run it off the pill
        var sh = txt.GetComponent<Shadow>(); if (sh != null) Destroy(sh);
        var ph = UiKit.Label(pill.transform, "Surfer", 13, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 0f);
        var f = pill.gameObject.AddComponent<InputField>();
        f.textComponent = txt; f.placeholder = ph; f.targetGraphic = pill;
        f.characterLimit = SaveData.NameMax; f.lineType = InputField.LineType.SingleLine;
        f.text = SaveData.S.uname;
        f.onEndEdit.AddListener(v =>
        {
            if (SaveData.CleanName(v) == SaveData.S.uname) return;
            SaveData.SetName(v); Sfx.Buzz(10); Fill();
        });
    }
}
