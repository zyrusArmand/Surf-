// The level ladders, as the web gates the shop: boards by their price rank inside their OWN
// family (each family has a first and a last rung), riders one per rank step across the
// whole rank table. The gate asks about buying, never about riding: nothing already owned is
// ever locked again.
using System.Collections.Generic;
using UnityEngine;

public static class Ladder
{
    // family key -> [first rung, last rung]
    static readonly Dictionary<string, int[]> FamBand = new Dictionary<string, int[]> {
        { "fun", new[] { 1, 20 } }, { "skim", new[] { 1, 16 } }, { "fish", new[] { 2, 24 } }, { "long", new[] { 2, 24 } },
        { "short", new[] { 3, 28 } }, { "wake", new[] { 3, 26 } }, { "gun", new[] { 5, 34 } }, { "foil", new[] { 39, 41 } } };
    public const int SkiLvl = 25;

    static Dictionary<string, int> boardLvl, charLvl;

    public static int BoardLvl(string id)
    {
        if (boardLvl == null)
        {
            boardLvl = new Dictionary<string, int>();
            foreach (var T in GuidePanel.Types)
            {
                var band = FamBand.ContainsKey(T.key) ? FamBand[T.key] : new[] { 1, 20 };
                var ids = new List<string>(T.ids);
                ids.Sort((a, b) => BoardCatalog.Get(a).price.CompareTo(BoardCatalog.Get(b).price));
                int n = ids.Count;
                for (int k = 0; k < n; k++) boardLvl[ids[k]] = n < 2 ? band[0] : Mathf.RoundToInt(band[0] + (band[1] - band[0]) * k / (float)(n - 1));
            }
            boardLvl["skijet"] = SkiLvl;
        }
        return boardLvl.ContainsKey(id) ? boardLvl[id] : 1;
    }

    public static int CharLvl(string id)
    {
        if (charLvl == null)
        {
            charLvl = new Dictionary<string, int>();
            var paid = new List<RiderCatalog.Rider>();
            foreach (var c in RiderCatalog.All) if (c.price > 0) paid.Add(c);
            paid.Sort((a, b) => a.price.CompareTo(b.price));
            int n = paid.Count, R = Progress.Ranks.Length;
            for (int k = 0; k < n; k++)
            {
                int r = 1 + Mathf.RoundToInt((R - 1) * (k / (float)Mathf.Max(1, n - 1)));
                charLvl[paid[k].id] = Progress.Ranks[Mathf.Min(R - 1, r)].Item1;
            }
            foreach (var c in RiderCatalog.All) if (!(c.price > 0)) charLvl[c.id] = 1;
        }
        return charLvl.ContainsKey(id) ? charLvl[id] : 1;
    }

    public static bool BoardLocked(string id) => !SaveData.Owns(id) && Progress.Level < BoardLvl(id);
    public static bool CharLocked(string id) => !SaveData.OwnsRider(id) && Progress.Level < CharLvl(id);

    /// what the next level that opens anything opens, for the card's "keep going" line
    public static string NextUnlock(out int atLevel, out int more)
    {
        atLevel = 0; more = 0;
        for (int L = Progress.Level + 1; L <= Progress.Cap; L++)
        {
            var names = new List<string>();
            foreach (var b in BoardCatalog.All) if (!SaveData.Owns(b.id) && BoardLvl(b.id) == L) names.Add(b.name);
            foreach (var c in RiderCatalog.All) if (!SaveData.OwnsRider(c.id) && CharLvl(c.id) == L) names.Add(c.name);
            if (names.Count > 0) { atLevel = L; more = names.Count - 1; return names[0]; }
        }
        return null;
    }
}
