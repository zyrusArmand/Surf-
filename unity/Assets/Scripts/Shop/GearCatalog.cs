// Generated from the web game's GEAR and GEAR_SUITS tables: what a rider wears, in four slots
// (eyes, neck, head, body). Gear is not for sale in the web game: reaching the rank IS the
// transaction (lv), or the daily wheel hands a piece over for keeps (ownedGear). Each entry
// carries the numbers its piece is built from -- the shades' frame and lens, a hat's colours
// and kind, a suit's shirt and pants prints -- exactly as the web writes them.
// Colours are 0xRRGGBB ints; -1 means the web left that field unset.
using System.Collections.Generic;
public static class GearCatalog
{
    /// a printed cloth: the web's shirt/pants spec (k is the pattern, the rest its colours)
    public class Cloth
    {
        public string k = "solid";
        public int c = -1, a = -1, b = -1, trim = -1, btn = -1;
        public int n = 0; public float f = 0f;
        public bool belt, lanyard;
        public string text, sub; public float hue;
    }
    public class Gear
    {
        public string id, name, slot, blurb; public int lv;
        // eyes: the shades
        public int frame = -1, lens = -1; public float mirror;
        // head: the hats (kind picks the shape; band and buckle are the stetson's and the top hat's)
        public string kind; public int col = -1, band = -1, buckle = -1;
        // body: the suits
        public Cloth shirt, pants; public string sleeves, len; public int hem = -1; public float rough = -1f;
    }
    public static readonly string[] Slots = { "eyes", "neck", "head", "body" };
    public static string SlotTitle(string slot) => slot == "eyes" ? "Shades" : slot == "neck" ? "Bandanas" : slot == "head" ? "Hats" : "Suits & Trunks";

    static Cloth C(string k, int c = -1, int a = -1, int b = -1, int trim = -1, int btn = -1, int n = 0, float f = 0f, bool belt = false, bool lanyard = false, string text = null, string sub = null, float hue = 0f)
        => new Cloth { k = k, c = c, a = a, b = b, trim = trim, btn = btn, n = n, f = f, belt = belt, lanyard = lanyard, text = text, sub = sub, hue = hue };

    public static readonly List<Gear> All = new List<Gear> {
        // --- shades: a bar across the face at eye height ---
        new Gear { id = "shd_classic", slot = "eyes", lv = 4, name = "Classic Shades", blurb = "Black wayfarers. Never not right.", frame = 0xffffff, lens = 0x0d1014, mirror = 0f },
        // --- neck: tied at the back, the point hanging down the chest (its own model and print) ---
        new Gear { id = "bnd_paisley", slot = "neck", lv = 14, name = "Paisley Bandana", blurb = "Tied at the back. Teal, and printed properly." },
        // --- hats: sat on the crown ---
        new Gear { id = "hat_cap", slot = "head", lv = 8, name = "Surf Cap", blurb = "Peak forward, salt-stained.", col = 0x2f6fb5, band = 0x275990, kind = "cap" },
        new Gear { id = "hat_straw", slot = "head", lv = 25, name = "Straw Lid", blurb = "A whole brim of shade.", col = 0xdcc183, kind = "brim" },
        new Gear { id = "hat_beanie", slot = "head", lv = 46, name = "Dawn Patrol Beanie", blurb = "For the cold ones before work.", col = 0x8d3f4e, kind = "beanie" },
        new Gear { id = "hat_bucket", slot = "head", lv = 74, name = "Bucket Hat", blurb = "Brim down. Opinions down.", col = 0x3f7d5a, kind = "brim" },
        new Gear { id = "hat_stetson", slot = "head", lv = 58, name = "The Cattleman", blurb = "Creased down the crown, rolled at the brim.", col = 0x8a6134, band = 0x3d2a1c, buckle = 0xc9a227, kind = "stetson" },
        new Gear { id = "hat_crown", slot = "head", lv = 111, name = "The Big Kahuna", blurb = "Worn by one surfer on the point.", col = 0xf0c23a, kind = "crown" },
        // --- suits: cut from the rider's own torso ---
        new Gear { id = "sut_teal", slot = "body", lv = 13, name = "Teal Rash Vest", blurb = "Long sleeves cut off. Sun-bleached at the shoulder.",
            shirt = C("solid", c: 0x1f9aa8, trim: 0xf2f7f6), sleeves = "short", hem = 0xf2f7f6 },
        new Gear { id = "sut_sunrise", slot = "body", lv = 32, name = "Sunrise Trunks", blurb = "Loud enough to be seen from the beach.",
            pants = C("stripes", a: 0xef6b3a, b: 0xffd98a, n: 6, f: 0.18f, trim: 0xffd98a), len = "shorts", hem = 0xffd98a },
        new Gear { id = "sut_mono", slot = "body", lv = 53, name = "Midnight Steamer", blurb = "Full suit. For the dark cold ones.",
            shirt = C("solid", c: 0x141a22, trim: 0x3f5a6b), sleeves = "long", pants = C("solid", c: 0x141a22, trim: 0x3f5a6b), len = "long", hem = 0x3f5a6b, rough = 0.5f },
        new Gear { id = "sut_tiger", slot = "body", lv = 81, name = "Tiger Line Suit", blurb = "One stripe, all the way round.",
            shirt = C("stripes", a: 0x231f1c, b: 0xf0a01e, n: 1, f: 0.14f, trim: 0xf0a01e), sleeves = "long", pants = C("stripes", a: 0x231f1c, b: 0xf0a01e, n: 1, f: 0.12f, trim: 0xf0a01e), len = "long", hem = 0xf0a01e, rough = 0.5f },
        new Gear { id = "sut_gold", slot = "body", lv = 104, name = "Gold Leaf Suit", blurb = "Point break local. Dressed like it.",
            shirt = C("brocade", c: 0x8a7220, trim: 0xf7d565), sleeves = "long", pants = C("solid", c: 0x4a3c12, trim: 0xf7d565), len = "long", hem = 0xf7d565, rough = 0.45f },
        // ---- the eleven, from the reference sheet: patterns painted to a canvas ----
        new Gear { id = "sut_surf", slot = "body", lv = 6, name = "Surf Apparel", blurb = "Tie-dye tee, checkerboard trunks. The uniform.",
            shirt = C("tiedye", text: "SURF", sub: "ASTRO", hue: 200f, trim: 0xf4f4f4), sleeves = "short", pants = C("check", a: 0x111111, b: 0xf2f2f2, n: 8, trim: 0x111111), len = "shorts" },
        new Gear { id = "sut_wood", slot = "body", lv = 10, name = "Woodworking Belt", blurb = "Plaid, denim, and a belt full of answers.",
            shirt = C("plaid", a: 0xb8262a, b: 0x1a1a1a, n: 5, trim: 0x1a1a1a), sleeves = "long", pants = C("denim", c: 0x3b5a8a, belt: true, trim: 0x4a2f16), len = "long" },
        new Gear { id = "sut_space", slot = "body", lv = 16, name = "Space Explorer", blurb = "Pressure suit. Patches earned, not bought.",
            shirt = C("space", trim: 0xd8dde3), sleeves = "long", pants = C("space", trim: 0x8c939b), len = "long", rough = 0.5f },
        new Gear { id = "sut_dandy", slot = "body", lv = 20, name = "Victorian Dandy", blurb = "Brocade waistcoat and a cravat with opinions.",
            shirt = C("brocade", c: 0x6b4a1e, trim: 0x2b1a0a, btn: 0xc9a227), sleeves = "long", pants = C("solid", c: 0x2b2b30, trim: 0x151518), len = "long" },
        new Gear { id = "sut_board", slot = "body", lv = 28, name = "Board Game Theme", blurb = "Loud shirt. Loudest player.",
            shirt = C("hawaii", c: 0xf3e6c8, trim: 0x1f3a5c), sleeves = "short", pants = C("solid", c: 0x1f3a5c, trim: 0xf3e6c8), len = "shorts" },
        new Gear { id = "sut_aviator", slot = "body", lv = 36, name = "The Honest Baseline", blurb = "Leather jacket, silk scarf, no autopilot.",
            shirt = C("leather", c: 0x5a3a1e, trim: 0x3a2412), sleeves = "long", pants = C("solid", c: 0xc9b58a, trim: 0x8a7550), len = "long", rough = 0.45f },
        new Gear { id = "sut_chef", slot = "body", lv = 44, name = "Master Chef", blurb = "Double-breasted whites. Yes, chef.",
            shirt = C("chef", trim: 0xb8b8b8, btn: 0x2a2622), sleeves = "long", pants = C("stripes", a: 0x3a3a3f, b: 0x8a8a90, n: 14, f: 0.5f, trim: 0x2a2a2e), len = "long" },
        new Gear { id = "sut_samurai", slot = "body", lv = 60, name = "Samurai", blurb = "Lacquered lames laced in gold.",
            shirt = C("armor", c: 0x8a1c1c, trim: 0xc9a227), sleeves = "short", pants = C("armor", c: 0x3a1010, trim: 0xc9a227), len = "long", rough = 0.35f },
        new Gear { id = "sut_skater", slot = "body", lv = 70, name = "Retro Skater", blurb = "Nineteen eighty-something, in full.",
            shirt = C("track", a: 0xe94b9c, b: 0x2ec4b6, c: 0xffd23f, trim: 0xffd23f), sleeves = "long", pants = C("track", a: 0x2ec4b6, b: 0xe94b9c, c: 0xffd23f, trim: 0xffd23f), len = "long" },
        new Gear { id = "sut_tech", slot = "body", lv = 88, name = "Tech Professional", blurb = "Button-up, khakis, lanyard. Ships on Friday.",
            shirt = C("button", c: 0x9fc0e6, lanyard: true, trim: 0x3f6f9f, btn: 0xf4f4f4), sleeves = "long", pants = C("solid", c: 0x8a7a55, trim: 0x5a4e36), len = "long" },
        new Gear { id = "sut_wizard", slot = "body", lv = 96, name = "Wizard", blurb = "Robes cut from the night sky.",
            shirt = C("stars", c: 0x1c2a6b, trim: 0xc9a227), sleeves = "long", pants = C("solid", c: 0x3a1f5c, trim: 0xc9a227), len = "long" },
        // the headwear those outfits came with
        new Gear { id = "hat_astro", slot = "head", lv = 16, name = "Helmet", blurb = "Sealed. Fogs up when he laughs.", col = 0xf2f2f0, kind = "astro" },
        new Gear { id = "hat_tophat", slot = "head", lv = 20, name = "Top Hat", blurb = "Silk. Tips itself.", col = 0x1b1b1f, band = 0x3a2a1c, kind = "tophat" },
        new Gear { id = "hat_band_navy", slot = "head", lv = 28, name = "Bandana Headband", blurb = "Tied at the back, tails out.", col = 0x1f3a5c, kind = "headband" },
        new Gear { id = "hat_aviator", slot = "head", lv = 36, name = "Aviator Cap", blurb = "Goggles up. For now.", col = 0x4a2f16, kind = "aviator" },
        new Gear { id = "hat_toque", slot = "head", lv = 44, name = "Chef Toque", blurb = "A hundred pleats, one for every way to cook an egg.", col = 0xf7f7f4, kind = "toque" },
        new Gear { id = "hat_samurai", slot = "head", lv = 60, name = "Kabuto", blurb = "The crest goes at the front.", col = 0x3a1010, kind = "samurai" },
        new Gear { id = "hat_band_pink", slot = "head", lv = 70, name = "Sweatband", blurb = "Neon. Absorbent. Iconic.", col = 0xe94b9c, kind = "headband" },
        new Gear { id = "hat_wizard", slot = "head", lv = 96, name = "Wizard Hat", blurb = "Pointed, starred, slightly bent.", col = 0x1c2a6b, kind = "wizard" },
    };

    public static Gear Get(string id) { if (string.IsNullOrEmpty(id)) return null; foreach (var g in All) if (g.id == id) return g; return null; }
    public static int Lv(string id) { var g = Get(id); return g != null ? g.lv : 1; }
    /// the web's gearHave: the rank grants it, or the wheel did
    public static bool Have(string id) => Progress.Level >= Lv(id) || SaveData.S.ownedGear.Contains(id);

    /// the id on in that slot of the save (the web's gearOn[slot])
    public static string On(Save s, string slot)
    {
        switch (slot) { case "eyes": return s.gearEyes; case "head": return s.gearHead; case "neck": return s.gearNeck; case "body": return s.gearBody; }
        return "";
    }
    public static void SetOn(Save s, string slot, string id)
    {
        id = id ?? "";
        switch (slot) { case "eyes": s.gearEyes = id; break; case "head": s.gearHead = id; break; case "neck": s.gearNeck = id; break; case "body": s.gearBody = id; break; }
    }
    /// the web's gearWorn: the piece on in that slot, if it is a piece of that slot and is had
    public static Gear Worn(Save s, string slot)
    {
        var g = Get(On(s, slot));
        return (g != null && g.slot == slot && Have(g.id)) ? g : null;
    }
}
