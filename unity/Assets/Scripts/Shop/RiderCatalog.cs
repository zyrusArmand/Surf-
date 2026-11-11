// Generated from the web game's CHARS, in its order: the riders with a model file under
// Resources/Models, and the eleven ProcRider builds at runtime (corgi, bunny, panda, otter,
// raccoon, sloth, lizard, penguin, porcupine, rex, chicken), with price and perk (RIDER_PERK).
using System.Collections.Generic;
public static class RiderCatalog
{
    public class Rider { public string id, name, blurb, perk; public int price; }
    public static readonly List<Rider> All = new List<Rider> {
        new Rider { id = "pug", name = "Astro the Pug", price = 0, blurb = "The original. Flat face, curled tail, absolutely no quit.", perk = "" },
        new Rider { id = "cat", name = "Miso the Cat", price = 900, blurb = "Grey tabby. Hates water, rides it anyway, will not discuss it.", perk = "+10% trick points" },
        new Rider { id = "pig", name = "Kiwi the Pig", price = 4200, blurb = "Round, pink and unreasonably buoyant. Floats before the board does.", perk = "Floats higher" },
        new Rider { id = "corgi", name = "Waffle the Corgi", price = 7200, blurb = "Short legs, enormous ears, catches everything out of pure enthusiasm.", perk = "Low and fast" },
        new Rider { id = "bunny", name = "Pip the Bunny", price = 10800, blurb = "All ears and hop. Lands everything, mostly by accident.", perk = "Extra pop" },
        new Rider { id = "panda", name = "Bao the Panda", price = 16500, blurb = "Sits very low, turns very slowly, absolutely immovable once committed.", perk = "One extra life" },
        new Rider { id = "frog", name = "Allen the Frog", price = 1800, blurb = "Born for this. Wide mouth, wider eyes, sticks every landing wetly.", perk = "Extra pop" },
        new Rider { id = "otter", name = "Nori the Otter", price = 5700, blurb = "Never off the water in the first place. Rolls for the fun of it.", perk = "A knot faster" },
        new Rider { id = "raccoon", name = "Bandit the Raccoon", price = 9000, blurb = "Turned up uninvited with somebody else's board. Extremely good on it.", perk = "Treasure finder" },
        new Rider { id = "sloth", name = "Chill the Sloth", price = 12600, blurb = "Takes the whole wave to make one turn and somehow still makes it.", perk = "Unhurried grip" },
        new Rider { id = "lizard", name = "Basil the Lizard", price = 18600, blurb = "Crested, long-tailed and entirely unbothered. Runs across the flat bits.", perk = "Sticky feet" },
        new Rider { id = "penguin", name = "Tux the Penguin", price = 22500, blurb = "Overdressed for the beach. Still the fastest thing off the line.", perk = "Rails that hold" },
        new Rider { id = "ant", name = "De Soto the Ant", price = 8100, blurb = "Six legs, none of them needed. Carries three times his weight in puka.", perk = "" },
        new Rider { id = "angler", name = "Wick the Anglerfish", price = 26400, blurb = "Spent her whole life a mile down with the only light for miles. Brought it up with her.", perk = "" },
        new Rider { id = "monkey", name = "Bongo the Monkey", price = 6300, blurb = "Arms longer than his legs and a tail with opinions of its own. Never once sat still.", perk = "Springy legs" },
        new Rider { id = "rat", name = "Fatty the Rat", price = 3750, blurb = "Came for the chips, stayed for the waves. Floats better than anyone expected.", perk = "Buoyant belly" },
        new Rider { id = "squirrel", name = "Hazel the Squirrel", price = 4800, blurb = "Small, twitchy and completely fearless. The tail arrives a second after she does.", perk = "+8% trick points" },
        new Rider { id = "porcupine", name = "Quill the Porcupine", price = 11700, blurb = "Nobody has ever asked him to move over. He has never had to ask either.", perk = "One extra life" },
        new Rider { id = "alien", name = "Zorp the Alien", price = 40500, blurb = "Came a very long way for this. Has not said why, and the board suits him.", perk = "" },
        new Rider { id = "rex", name = "Rexy the T. rex", price = 33000, blurb = "Tiny arms, enormous everything else. The wave gets out of the way.", perk = "Big push-off" },
        new Rider { id = "chicken", name = "Nugget the Chicken", price = 24600, blurb = "Cannot fly. Has never let that stop her. Surfs like she means it.", perk = "Chests come oftener" },
    };
    public static Rider Get(string id) { foreach (var r in All) if (r.id == id) return r; return All[0]; }
}
