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
    public bool fastFx;          // Settings > Graphics: Fast drops the sea's reflection, the depth of field, the flare and the ambient occlusion
    // the player's name on the profile
    public string uname = "";
    // the daily wheel: the day it was last spun (yyyy-mm-dd), and the prizes it hands out that
    // last until used: a spare life, double puka on the next run, a chest next run, a board
    // and a rider on loan (ids, cleared when bought or when the loan is used up)
    public string wheelLast = ""; public bool wheelLife, wheelX2, wheelChest; public string wheelLoan = "", wheelRider = "";
    // gear: the pieces owned and the one worn in each slot (eyes, head, neck, body)
    public List<string> ownedGear = new List<string>();
    public string gearEyes = "", gearHead = "", gearNeck = "", gearBody = "";
    // the bought tricks and the five slots on the trick bar
    public List<string> ownedTricks = new List<string>();
    public List<string> trickSlots = new List<string>();
    // dives and pearls, coconuts caught
    public int pearls, dives, coconuts;
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
            if (s.ownedGear == null) s.ownedGear = new List<string>(); if (s.ownedTricks == null) s.ownedTricks = new List<string>(); if (s.trickSlots == null) s.trickSlots = new List<string>();
            if (s.uname == null) s.uname = ""; if (s.wheelLast == null) s.wheelLast = ""; if (s.wheelLoan == null) s.wheelLoan = ""; if (s.wheelRider == null) s.wheelRider = "";
            if (s.gearEyes == null) s.gearEyes = ""; if (s.gearHead == null) s.gearHead = ""; if (s.gearNeck == null) s.gearNeck = ""; if (s.gearBody == null) s.gearBody = "";
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
    /// the secret panel's Reset: everything back to a first launch
    public static void Wipe()
    {
        try { PlayerPrefs.DeleteKey(Key); PlayerPrefs.DeleteKey("best"); PlayerPrefs.DeleteKey("bestDist"); PlayerPrefs.DeleteKey("coins"); PlayerPrefs.Save(); } catch { }
        s = new Save(); Flush();
    }

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
    // the web's RANKS, level by level
    public static readonly (int, string)[] Ranks = { (1, "Sandcastle Kid"), (4, "Beach Grom"), (8, "Rookie Surfer"), (13, "Whitewater Rider"), (19, "Green Wave Rider"), (25, "Line-up Regular"), (32, "Solid Surfer"), (39, "Wave Hunter"), (46, "Rail Carver"), (53, "Great Surfer"), (60, "Barrel Dodger"), (67, "Awesome Shredder"), (74, "Reef Breaker"), (81, "Point Break Local"), (87, "Storm Chaser"), (93, "Big Wave Charger"), (99, "Tube Master"), (104, "Legend of the Point"), (108, "The Big Kahuna"), (111, "Wave Whisperer") };
    public static string Rank { get { int L = Level; string r = Ranks[0].Item2; foreach (var k in Ranks) if (L >= k.Item1) r = k.Item2; return r; } }
}
