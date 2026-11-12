// The trick rack, from the web game's TRICKS table (index.html 6507-6537), the save's half of it
// (7653-7666) and the shelf's buy and assign (13219-13279). The five buttons down the left of
// the ride used to BE the five tricks; now they are five SLOTS, and what sits in each is the
// player's to choose. The original five are free and in the slots by default; the board tricks
// are bought on the Tricks shelf and dropped into any slot, bumping whatever was there (it stays
// owned). There are no level gates on a trick in the web: the price is the only door.
//
// A board trick is a skateboard flip done by the board under his feet rather than by him, timed
// rather than held: one press, one trick. Its figure is carried here as the web writes it, for
// the ride to read:
//   axis   long = about the board's length (a kickflip), yaw = flat (a shove-it),
//          both = long + yaw (varial, tre)
//   turns  full turns of the main axis; yaw = turns of the flat axis on a 'both'
//   dir    which way; a heelflip is a kickflip the other way
//   pts    what landing it pays, before the rider's trick perk
// The pictures: ui/ges_<id>.png is the button's gesture and ui/trick_<id>.png the shelf card's
// picture (trickThumb, web 6560: the icon, large, on the board cards' water), both rendered
// from the web's own TRICK_ICON svg; trick_<id>_lk.png is that picture through .card.lk's
// saturate(.55), for a trick not owned yet.
using System.Collections.Generic;

public static class TrickCatalog
{
    public class Trick
    {
        public string id, name, lab, kind, blurb;
        public int price;
        // a board trick's figure (unused on the base five)
        public string axis; public float turns, yaw; public int dir, pts;
        public bool Board => kind == "board";
    }

    public static readonly List<Trick> All = new List<Trick> {
        new Trick { id = "roll", name = "Barrel roll", lab = "ROLL", kind = "base", price = 0, blurb = "Hold to roll, let go square." },
        new Trick { id = "flip", name = "Flip",        lab = "FLIP", kind = "base", price = 0, blurb = "Front or back, held and released." },
        new Trick { id = "spin", name = "Spin",        lab = "SPIN", kind = "base", price = 0, blurb = "Flat spins. Half turns land switch." },
        new Trick { id = "hand", name = "Handstand",   lab = "HAND", kind = "base", price = 0, blurb = "Hold it as long as you dare." },
        new Trick { id = "heli", name = "Helicopter",  lab = "HELI", kind = "base", price = 0, blurb = "Nose down, three turns, up and out." },
        new Trick { id = "kickflip", name = "Kickflip",     lab = "KICK",   kind = "board", axis = "long", turns = 1f,   dir = 1,  pts = 220, price = 600,  blurb = "The board flips once under your feet." },
        new Trick { id = "heelflip", name = "Heelflip",     lab = "HEEL",   kind = "board", axis = "long", turns = 1f,   dir = -1, pts = 220, price = 800,  blurb = "A kickflip the other way round." },
        new Trick { id = "shove",    name = "Pop shove-it", lab = "SHOVE",  kind = "board", axis = "yaw",  turns = 0.5f, dir = 1,  pts = 180, price = 700,  blurb = "The board spins a flat 180 and you land it backwards." },
        new Trick { id = "bigshove", name = "360 shove-it", lab = "360 SH", kind = "board", axis = "yaw",  turns = 1f,   dir = 1,  pts = 320, price = 1400, blurb = "A full flat turn of the board." },
        new Trick { id = "varial",   name = "Varial flip",  lab = "VARIAL", kind = "board", axis = "both", turns = 1f, yaw = 0.5f, dir = 1, pts = 420, price = 1800, blurb = "Kickflip and shove-it at once." },
        new Trick { id = "treflip",  name = "360 flip",     lab = "TRE",    kind = "board", axis = "both", turns = 1f, yaw = 1f,   dir = 1, pts = 640, price = 3200, blurb = "The tre flip. Kickflip plus a full shove-it." },
    };

    /// TRICK_SLOTS_DEF: what is on the five buttons, top to bottom, before anything is bought
    public static readonly string[] DefSlots = { "roll", "flip", "spin", "hand", "heli" };
    public const int SlotCount = 5;

    static Dictionary<string, Trick> byId;
    public static Trick Get(string id)
    {
        if (byId == null) { byId = new Dictionary<string, Trick>(); foreach (var t in All) byId[t.id] = t; }
        return id != null && byId.TryGetValue(id, out var T) ? T : null;
    }

    /// trickHave: the base five are always yours; a board trick once it is bought
    public static bool Have(string id)
    {
        var T = Get(id);
        return T != null && (T.kind == "base" || SaveData.S.ownedTricks.Contains(id));
    }

    /// the five slots, top button first, made sound before they are read (see Sanitise)
    public static List<string> Slots { get { Sanitise(); return SaveData.S.trickSlots; } }
    public static string Slot(int i) { var s = Slots; return i >= 0 && i < SlotCount ? s[i] : null; }
    /// which button a trick is on, or -1
    public static int SlotOf(string id) => Slots.IndexOf(id);

    /// The web's loader (7653-7666), run on every read rather than once at load, so a save from
    /// before the rack, a wipe, or a renamed trick can never leave the bar short: the bought list
    /// keeps only board tricks that still exist, and a slot whose id is not owned any more, or is
    /// in two slots, gives its place back to whichever of the original five is free.
    static void Sanitise()
    {
        var s = SaveData.S;
        if (s.ownedTricks == null) s.ownedTricks = new List<string>();
        if (s.trickSlots == null) s.trickSlots = new List<string>();
        for (int i = s.ownedTricks.Count - 1; i >= 0; i--)
        {
            string k = s.ownedTricks[i]; var T = Get(k);
            if (T == null || T.kind == "base" || s.ownedTricks.IndexOf(k) != i) s.ownedTricks.RemoveAt(i);
        }
        var want = s.trickSlots; var outL = new List<string>(SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            string id = i < want.Count ? want[i] : null;
            outL.Add(!string.IsNullOrEmpty(id) && Have(id) && !outL.Contains(id) ? id : null);
        }
        for (int i = 0; i < SlotCount; i++)
        {
            if (outL[i] != null) continue;
            string free = null;
            foreach (var d in DefSlots) if (!outL.Contains(d)) { free = d; break; }
            outL[i] = free ?? DefSlots[i];
        }
        bool same = want.Count == SlotCount;
        for (int i = 0; same && i < SlotCount; i++) same = want[i] == outL[i];
        if (!same) s.trickSlots = outL;
    }

    /// the shelf's buy (web 13244-13246): the shells go, the trick is kept; false if it cannot be
    /// had (already owned, unknown, a base trick, or not enough puka)
    public static bool Buy(string id)
    {
        var T = Get(id);
        if (T == null || T.kind == "base" || Have(id) || SaveData.Coins < T.price) return false;
        SaveData.Coins -= T.price;
        SaveData.S.ownedTricks.Add(id);
        SaveData.Flush();
        return true;
    }

    /// assignTrick (web 13272-13279): into slot i. If it was already on another button the two
    /// swap, so the bar can be rearranged; otherwise whatever was on button i simply comes off
    /// (it stays owned).
    public static void Assign(string id, int i)
    {
        if (!Have(id) || i < 0 || i >= SlotCount) return;
        var s = Slots;
        int prev = s.IndexOf(id);
        if (prev >= 0 && prev != i) s[prev] = s[i];
        s[i] = id;
        SaveData.Flush();
    }
}
