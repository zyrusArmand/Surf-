// What your level is made of: the web's levelInfo sheet behind the level pill. Three questions
// in the order anybody asks them: where am I, what is left, and what is any of it FOR. The
// ladder is stated outright rather than described, and the "what it opens" rows are read off
// the same tables the shop locks against, so this page cannot drift from what the racks do.
using UnityEngine;
using UnityEngine.UI;

public class LevelPanel : MonoBehaviour
{
    public System.Action onClose;
    GameObject pane; RectTransform content; ScrollRect scroll;
    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "levelInfo", "Your level", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) { Sfx.Buzz(8); Fill(); } }

    // the web's .rank: the title leads, because it is the answer to "what am I"
    void Title(string s)
    {
        var rt = UiKit.Rect(content, "rank", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 36));
        var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 36; le.minHeight = 36;
        UiKit.Label(rt, s, 25, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(4, 0), Vector2.zero, TextAnchor.MiddleLeft, 0.6f);
    }

    void Fill()
    {
        Pane.Clear(content);
        var s = SaveData.S;
        int L = Progress.Level;
        bool capped = L >= Progress.Cap;
        int a = Progress.XpAt(L), b = capped ? a : Progress.XpAt(L + 1);
        int span = Mathf.Max(1, b - a), into = capped ? 0 : s.xp - a;
        Title(Rungs.RankAt(L));
        Pane.Row(content, "Level", L.ToString());
        Pane.Row(content, "Total XP", Pane.N(s.xp));
        Pane.Bar(content, capped ? "At the cap" : "Toward level " + (L + 1), capped ? "MAX" : Pane.N(into) + " / " + Pane.N(span), capped ? 1f : Progress.Frac);
        if (!capped)
        {
            Pane.Row(content, "Still to go", Pane.N(span - into) + " XP");
            if (Rungs.NextRank(L, out int at, out string nm)) Pane.Row(content, "Next title", nm + "  at " + at);
            Pane.Row(content, "Level " + L + " began at", Pane.N(Progress.XpAt(L)));
            Pane.Row(content, "Level " + (L + 1) + " needs", Pane.N(Progress.XpAt(L + 1)));
        }
        Pane.Heading(content, "Where XP comes from");
        Pane.Note(content, "Every run pays its distance in metres, plus a tenth of its trick score — the same two figures the wipeout card shows you. " +
            "A metre and a point used to be worth the same, which put nine tenths of every level on the trick counter; a tenth puts the two within sight of each other, " +
            "so distance is the spine and tricks are the multiplier on it. The rungs get further apart as you climb: each one is 600 × (level − 1)^1.45, " +
            "which is why the first arrives inside a run or two and the later ones take a session. There are " + Progress.Cap + " of them.", 150);
        Pane.Heading(content, "What a level pays");
        Pane.Note(content, "Reaching a level pays " + Progress.LevelPay(1) + " puka for every level you have reached — " + Pane.N(Progress.LevelPay(L)) + " at level " + L +
            ", and " + Pane.N(Progress.LevelPay(L + 1)) + " for the next one. It is paid on the card for the run that earned it, and a run that takes you up two levels pays for both.", 76);
        int ri = Rungs.RankIdx(L);
        string more = Rungs.NextRank(L, out int nAt, out string nName)
            ? ", and " + Pane.N(Rungs.RankPay(Rungs.RankRungAt(nAt))) + " when you make " + nName + " at level " + nAt + "." : ".";
        Pane.Note(content, "A new title pays a good deal more on top: " + Pane.N(Rungs.RankPay(ri)) + " puka for " + Rungs.RankAt(L) + more +
            " There are twenty titles across the ladder and they are worth 210,000 puka between them.", 76);
        // what it is FOR, by name: boards, riders and gear whose gate is a level still ahead
        var nx = new System.Collections.Generic.List<(int L, string name)>();
        for (int l = L + 1; l <= Progress.Cap; l++) foreach (var name in Rungs.UnlocksAt(l)) nx.Add((l, name));
        Pane.Heading(content, "What the next levels open");
        if (nx.Count > 0)
        {
            for (int i = 0; i < nx.Count && i < 8; i++) Pane.Row(content, "Level " + nx[i].L, nx[i].name);
            if (nx.Count > 8) Pane.Note(content, "…and " + (nx.Count - 8) + " more, the last of them at level " + nx[nx.Count - 1].L + ".", 30);
        }
        else Pane.Note(content, "Nothing left to unlock — every board and every rider in the shop is already open to you.", 40);
        scroll.verticalNormalizedPosition = 1f;
    }
}
