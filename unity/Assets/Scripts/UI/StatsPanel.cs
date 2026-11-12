// Your surfing so far: the web game's #mystats sheet ("Your surfing", reached from the profile),
// the handful of numbers a player would actually tell somebody -- four big tiles, then a few
// rows that only appear once there is something to put in them. Every other counter is behind
// the five taps on the version (SecretPanel), as the web keeps it. The counters live in the Save
// (one player's history, on this device) and the game feeds them with Stats.Bump().
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

    /// the web's noteTrick: one more of this trick, under the web's key for it. "Tricks landed"
    /// is the sum of these, and the biggest is the favourite.
    public static void Trick(string name)
    {
        var s = SaveData.S;
        Tally t = null;
        foreach (var x in s.trickTally) if (x.k == name) { t = x; break; }
        if (t == null) { t = new Tally { k = name, n = 0 }; s.trickTally.Add(t); }
        t.n++;
        s.tricks++;
        SaveData.Flush();
    }

    /// the most-done trick, or null before the first
    public static string Favourite()
    {
        Tally best = null;
        foreach (var x in SaveData.S.trickTally) if (x.n > 0 && (best == null || x.n > best.n)) best = x;
        return best?.k;
    }

    // the web's TRICK_NAME; a key it has no name for is shown as itself, as the web does
    public static string TrickName(string k)
    {
        switch (k)
        {
            case "roll": return "Barrel rolls";
            case "flip": return "Front / back flips";
            case "spin": return "Spins";
            case "grab": return "Grabs landed";
            case "drag": return "Hand drags";
            case "handstand": return "Handstands";
            case "heli": return "Helicopters";
            case "kickflip": return "Kickflips";
            case "heelflip": return "Heelflips";
            case "shove": return "Pop shove-its";
            case "bigshove": return "360 shove-its";
            case "varial": return "Varial flips";
            case "treflip": return "360 flips";
        }
        return k;
    }
}

public class StatsPanel : MonoBehaviour
{
    public System.Action onClose;
    GameObject pane; RectTransform content; ScrollRect scroll;
    public bool Open => pane != null && pane.activeSelf;

    // #mystats .big: a 2 x 2 grid 8px apart, each tile 14px corners and 10px padding, a 9px
    // letter-spaced caption over a 22px gold figure with its unit in 12px pale gold
    static readonly Color BigGold = new Color(1f, 0xdf / 255f, 0x2e / 255f);     // #ffdf2e
    static readonly Color BigUnit = new Color(1f, 0xe9 / 255f, 0xa8 / 255f);     // #ffe9a8
    const float TileH = 62f, TileGap = 8f;

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "stats", "Your surfing", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) { Sfx.Buzz(8); Fill(); } }

    void Big(Transform grid, int i, string k, string v, string unit)
    {
        float x = (i % 2) * 0.5f, row = i / 2;
        var rt = UiKit.Rect(grid, "big:" + k, new Vector2(x, 1), new Vector2(x + 0.5f, 1), new Vector2(0, 1), new Vector2(i % 2 == 0 ? 0f : TileGap * 0.5f, -row * (TileH + TileGap)), new Vector2(-TileGap * 0.5f, TileH));
        var im = rt.gameObject.AddComponent<Image>(); im.sprite = UiKit.Rounded(197, TileH, 14, 14, 14, 14, UiKit.Body.Glass, 1.5f); im.raycastTarget = true;
        var cap = UiKit.Label(rt, k.ToUpperInvariant(), 9, Pane.Faint, FontStyle.Bold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(-24, 12), TextAnchor.MiddleLeft, 0.4f);
        Tracking.On(cap, 0.11f);
        var fig = UiKit.Label(rt, v, 22, BigGold, FontStyle.Bold, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 8), new Vector2(160, 28), TextAnchor.MiddleLeft, 0.5f);
        if (!string.IsNullOrEmpty(unit))
            UiKit.Label(rt, unit, 12, BigUnit, FontStyle.Bold, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12 + fig.preferredWidth + 2f, 10), new Vector2(40, 20), TextAnchor.MiddleLeft, 0.5f);
    }

    void Fill()
    {
        Pane.Clear(content);
        var s = SaveData.S;
        int runs = s.runs;
        int landed = 0; foreach (var t in s.trickTally) landed += t.n;
        landed = Mathf.Max(landed, s.tricks);              // a save from before the tally kept only the total
        string fav = Stats.Favourite();
        // the four that lead
        var grid = UiKit.Rect(content, "big", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, TileH * 2f + TileGap));
        var le = grid.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = le.minHeight = TileH * 2f + TileGap;
        Big(grid, 0, "Best ride", Pane.N(s.bestDist), " m");
        Big(grid, 1, "Best tricks", Pane.N(s.best), null);
        Big(grid, 2, "Waves ridden", Pane.N(runs), null);
        Big(grid, 3, "Tricks landed", Pane.N(landed), null);
        // and the rows, each only once it has something in it
        if (runs > 0) Pane.Row(content, "Furthest you have gone in total", Pane.N(s.totalDist) + " m");
        if (runs > 0) Pane.Row(content, "Time on the water", Pane.Hhmm(s.secs));
        if (landed > 0 && fav != null) Pane.Row(content, "Favourite trick", Stats.TrickName(fav));
        if (s.crates > 0) Pane.Row(content, "Chests caught", Pane.N(s.crates));
        if (s.closeCalls > 0) Pane.Row(content, "Close calls shaved", Pane.N(s.closeCalls));
        // out of what the shop actually shows: the borrowed pair is not counted
        int allB = 0; foreach (var b in BoardCatalog.All) if (!b.hidden) allB++;
        int allC = 0; foreach (var r in RiderCatalog.All) if (!Rungs.RiderHidden(r.id)) allC++;
        Pane.Row(content, "Boards owned", s.owned.Count + " / " + allB);
        Pane.Row(content, "Surfers owned", s.ownedChars.Count + " / " + allC);
        if (runs == 0) Pane.Note(content, "Nothing yet — go and surf.", 30, 13f);
        scroll.verticalNormalizedPosition = 1f;
    }
}
