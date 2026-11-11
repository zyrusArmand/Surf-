// What survives between runs: puka, XP, records, the boards and riders owned and the ones
// in use. One JSON blob in PlayerPrefs, the same fields the web game's save carries, so the
// cloud save can replace the store later without touching the callers.
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Save
{
    public int coins, xp, best, bestDist;
    public List<string> owned = new List<string> { "astro" };
    public List<string> ownedChars = new List<string> { "pug" };
    public string equipped = "astro", rider = "pug";
    public int runs;
    // the three goals on the board (see Quests): the pool id, the count so far, and the tick
    public List<GoalSave> goals = new List<GoalSave>();
    // the run history behind Stats, the web's surfStats counters that the port can feed
    public int crates, closeCalls, barrels, tricks, jellies, ramps, totalDist, totalScore, secs, earned, revives;
    // settings: stored as "off" flags so a save written before they existed reads as all on
    public bool musicOff, sfxOff, buzzOff;
}

[System.Serializable]
public class GoalSave { public string id; public int n; public bool done; }

public static class SaveData
{
    const string Key = "surfSave";
    static Save s;
    public static Save S
    {
        get
        {
            if (s != null) return s;
            try { var j = PlayerPrefs.GetString(Key, ""); s = string.IsNullOrEmpty(j) ? new Save() : JsonUtility.FromJson<Save>(j); }
            catch { s = new Save(); }
            if (s == null) s = new Save();
            if (s.owned == null || s.owned.Count == 0) s.owned = new List<string> { "astro" };
            if (s.ownedChars == null || s.ownedChars.Count == 0) s.ownedChars = new List<string> { "pug" };
            if (string.IsNullOrEmpty(s.equipped)) s.equipped = "astro";
            if (string.IsNullOrEmpty(s.rider)) s.rider = "pug";
            if (s.goals == null) s.goals = new List<GoalSave>();
            // the older keys, carried across once
            if (s.best == 0 && PlayerPrefs.HasKey("best")) { s.best = PlayerPrefs.GetInt("best", 0); s.bestDist = PlayerPrefs.GetInt("bestDist", 0); s.coins = PlayerPrefs.GetInt("coins", 0); }
            return s;
        }
    }
    public static int Best { get => S.best; set { S.best = value; } }
    public static int BestDist { get => S.bestDist; set { S.bestDist = value; } }
    public static int Coins { get => S.coins; set { S.coins = Mathf.Max(0, value); } }
    public static bool Owns(string id) => S.owned.Contains(id);
    public static bool OwnsRider(string id) => S.ownedChars.Contains(id);
    public static void Flush()
    {
        try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(S)); PlayerPrefs.Save(); } catch { }
    }
}

/// levels: the web's curve, 600 * (L-1)^1.45 XP to reach level L, paid in puka on the way up
public static class Progress
{
    public const int Cap = 111;
    public static int XpAt(int L) => L <= 1 ? 0 : Mathf.RoundToInt(600f * Mathf.Pow(L - 1, 1.45f));
    public static int LevelAt(int xp) { int L = 1; while (L < Cap && xp >= XpAt(L + 1)) L++; return L; }
    public static int Level => LevelAt(SaveData.S.xp);
    public static float Frac { get { int L = Level; if (L >= Cap) return 1f; int a = XpAt(L), b = XpAt(L + 1); return Mathf.Clamp01((SaveData.S.xp - a) / (float)Mathf.Max(1, b - a)); } }
    public static int LevelPay(int L) => 25 * L;
    /// adds XP; returns the puka paid out for any levels gained
    public static int AddXp(int n)
    {
        n = Mathf.Max(0, n); if (n == 0) return 0;
        int was = Level; SaveData.S.xp += n; int now = Level, pay = 0;
        for (int L = was + 1; L <= now; L++) pay += LevelPay(L);
        SaveData.Coins += pay; return pay;
    }
    static readonly (int, string)[] Ranks = { (1, "Sandcastle Kid"), (4, "Beach Grom"), (8, "Rookie Surfer"), (14, "Local"), (22, "Charger"), (32, "Barrel Hunter"), (45, "Pro"), (60, "Legend"), (80, "Kahuna"), (100, "Big Kahuna") };
    public static string Rank { get { int L = Level; string r = Ranks[0].Item2; foreach (var k in Ranks) if (L >= k.Item1) r = k.Item2; return r; } }
}
