// Your surfing so far: the web game's stats panel, the handful of numbers a player would
// actually tell somebody. The counters live in the Save (one player's history, on this
// device) and the game feeds them with Stats.Bump().
using UnityEngine;
using UnityEngine.UI;

public static class Stats
{
    /// adds n to a counter: runs, crates, closeCalls, barrels, tricks, jellies, ramps, dist,
    /// score, secs, earned, revives
    public static void Bump(string key, int n = 1)
    {
        var s = SaveData.S;
        switch (key)
        {
            case "runs": s.runs += n; break;
            case "crates": s.crates += n; break;
            case "closeCalls": s.closeCalls += n; break;
            case "barrels": s.barrels += n; break;
            case "tricks": s.tricks += n; break;
            case "jellies": s.jellies += n; break;
            case "ramps": s.ramps += n; break;
            case "dist": s.totalDist += n; break;
            case "score": s.totalScore += n; break;
            case "secs": s.secs += n; break;
            case "earned": s.earned += n; break;
            case "revives": s.revives += n; break;
            default: Debug.LogWarning("Stats: no counter named " + key); return;
        }
        SaveData.Flush();
    }
}

public class StatsPanel : MonoBehaviour
{
    public System.Action onClose;
    GameObject pane; RectTransform content; ScrollRect scroll;
    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "stats", "Stats", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) Fill(); }

    // a distance means more with something to hold it against (the web's funStats)
    static string Scale(int dist)
    {
        float km = dist / 1000f;
        if (km >= 42.2f) return "a marathon and then some";
        if (km >= 5f) return Mathf.RoundToInt(km / 0.4f) + " laps of an Olympic pool";
        if (dist >= 100) return Mathf.RoundToInt(dist / 50f) + " Olympic pools";
        return "just getting started";
    }

    void Fill()
    {
        Pane.Clear(content);
        var s = SaveData.S;
        Pane.Heading(content, "Your surfing so far");
        Pane.Row(content, "Best ride", Pane.N(s.bestDist) + " m");
        Pane.Row(content, "Best tricks", Pane.N(s.best));
        Pane.Row(content, "Waves ridden", Pane.N(s.runs));
        Pane.Row(content, "Tricks landed", Pane.N(s.tricks));
        if (s.runs > 0)
        {
            Pane.Heading(content, "On the water");
            Pane.Row(content, "Total distance", Pane.N(s.totalDist) + " m");
            Pane.Row(content, "Which is about", Scale(s.totalDist));
            Pane.Row(content, "Average run", Pane.N(s.totalDist / s.runs) + " m");
            Pane.Row(content, "Time on the water", Pane.Hhmm(s.secs));
            Pane.Row(content, "Total score", Pane.N(s.totalScore));
            Pane.Heading(content, "Along the way");
            Pane.Row(content, "Chests caught", Pane.N(s.crates));
            Pane.Row(content, "Close calls shaved", Pane.N(s.closeCalls));
            Pane.Row(content, "Barrels ridden", Pane.N(s.barrels));
            Pane.Row(content, "Jellyfish bounced", Pane.N(s.jellies));
            Pane.Row(content, "Ramps launched", Pane.N(s.ramps));
        }
        else Pane.Note(content, "Nothing yet — go and surf.", 30);
        Pane.Heading(content, "The collection");
        int nB = s.owned.Count, nC = s.ownedChars.Count, allB = BoardCatalog.All.Count, allC = RiderCatalog.All.Count;
        Pane.Bar(content, "Boards", nB + " / " + allB, nB / (float)Mathf.Max(1, allB));
        Pane.Bar(content, "Surfers", nC + " / " + allC, nC / (float)Mathf.Max(1, allC));
        Pane.Row(content, "Riding", BoardCatalog.Get(s.equipped).name);
        Pane.Row(content, "As", RiderCatalog.Get(s.rider).name);
        Pane.Row(content, "Puka earned", Pane.N(s.earned));
        Pane.Row(content, "Purse now", Pane.N(s.coins));
        scroll.verticalNormalizedPosition = 1f;
    }
}
