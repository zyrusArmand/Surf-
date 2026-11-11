// Generated from the web game's CHARS: the riders that have a model file, with price and perk.
using System.Collections.Generic;
public static class RiderCatalog
{
    public class Rider { public string id, name, blurb, perk; public int price; }
    public static readonly List<Rider> All = new List<Rider> {
        new Rider { id = "pug", name = "Astro the Pug", price = 0, blurb = "The original. Flat face, curled tail, absolutely no quit.", perk = "" },
        new Rider { id = "cat", name = "Miso the Cat", price = 900, blurb = "Grey tabby. Hates water, rides it anyway, will not discuss it.", perk = "+10% trick points" },
        new Rider { id = "pig", name = "Kiwi the Pig", price = 4200, blurb = "Round, pink and unreasonably buoyant. Floats before the board does.", perk = "Floats higher" },
        new Rider { id = "frog", name = "Allen the Frog", price = 1800, blurb = "Born for this. Wide mouth, wider eyes, sticks every landing wetly.", perk = "Extra pop" },
        new Rider { id = "ant", name = "De Soto the Ant", price = 8100, blurb = "Six legs, none of them needed. Carries three times his weight in puka.", perk = "" },
        new Rider { id = "angler", name = "Wick the Anglerfish", price = 26400, blurb = "Spent her whole life a mile down with the only light for miles. Brought it up with her.", perk = "" },
        new Rider { id = "monkey", name = "Bongo the Monkey", price = 6300, blurb = "Arms longer than his legs and a tail with opinions of its own. Never once sat still.", perk = "Springy legs" },
        new Rider { id = "rat", name = "Fatty the Rat", price = 3750, blurb = "Came for the chips, stayed for the waves. Floats better than anyone expected.", perk = "Buoyant belly" },
        new Rider { id = "squirrel", name = "Hazel the Squirrel", price = 4800, blurb = "Small, twitchy and completely fearless. The tail arrives a second after she does.", perk = "+8% trick points" },
        new Rider { id = "alien", name = "Zorp the Alien", price = 40500, blurb = "Came a very long way for this. Has not said why, and the board suits him.", perk = "" },
    };
    public static Rider Get(string id) { foreach (var r in All) if (r.id == id) return r; return All[0]; }
}
