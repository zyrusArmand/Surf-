// Who you are: the rank and the level, how far through it you are, the purse and the
// records, and the way into Stats, because "your surfing so far" belongs under your own
// name rather than beside it.
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

    public void Show(bool on) { pane.SetActive(on); if (on) Fill(); }

    void Fill()
    {
        Pane.Clear(content);
        var s = SaveData.S;
        int L = Progress.Level;
        bool capped = L >= Progress.Cap;
        int a = Progress.XpAt(L), b = capped ? a : Progress.XpAt(L + 1);
        string to = capped ? "MAX" : Pane.N(s.xp - a) + " / " + Pane.N(b - a);
        Pane.Row(content, "Rank", Progress.Rank);
        Pane.Row(content, "Level", L.ToString());
        Pane.Bar(content, "Progress", to, Progress.Frac);
        Pane.Row(content, "XP", Pane.N(s.xp));
        Pane.Row(content, "Puka", Pane.N(s.coins));
        Pane.Row(content, "Best distance", Pane.N(s.bestDist) + " m");
        Pane.Row(content, "Best trick score", Pane.N(s.best));
        Pane.Row(content, "Waves ridden", Pane.N(s.runs));
        Pane.Go(content, "Your surfing so far", () => { Show(false); onStats?.Invoke(); });
        scroll.verticalNormalizedPosition = 1f;
    }
}
