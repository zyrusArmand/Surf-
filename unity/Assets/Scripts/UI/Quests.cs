// The goals: three at a time from the web game's pool, each a count towards a target and
// 100 puka when it gets there. The game reports what happened through Event(); run-scoped
// goals (a distance or a trick score in one run) are settled once at the end of the run with
// RunEnd(); finished goals are swapped for fresh ones when the NEXT run starts (Roll), so the
// card after the run you finished them on still shows the tick. The panel lists the three
// with a bar each, and DONE +100 against the ones that paid.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Quests : MonoBehaviour
{
    public class Goal { public string id, text, ev, icon; public int target; public bool run; }
    // the web's GOAL_POOL, with the line drawing each one wears (Resources/UI/goal_<icon>)
    public static readonly Goal[] Pool = {
        new Goal { id = "barrel1",   text = "Ride a barrel",               target = 1,    ev = "barrel", icon = "wave" },
        new Goal { id = "chest1",    text = "Catch a treasure chest",      target = 1,    ev = "chest",  icon = "shell" },
        new Goal { id = "close3",    text = "Shave 3 close calls",         target = 3,    ev = "close",  icon = "fin" },
        new Goal { id = "jelly3",    text = "Bounce 3 jellyfish",          target = 3,    ev = "jelly",  icon = "star" },
        new Goal { id = "ramp3",     text = "Launch off 3 ramps",          target = 3,    ev = "ramp",   icon = "dolphin" },
        new Goal { id = "dist800",   text = "Surf 800 m in one run",       target = 800,  ev = "dist",   icon = "sunset",  run = true },
        new Goal { id = "dist1500",  text = "Surf 1,500 m in one run",     target = 1500, ev = "dist",   icon = "sunrays", run = true },
        new Goal { id = "trick1500", text = "1,500 trick points in a run", target = 1500, ev = "score",  icon = "boards",  run = true },
        new Goal { id = "pearl10",   text = "Gather 10 pearls on a dive",  target = 10,   ev = "pearl",  icon = "shell" },
    };
    public const int Pay = 100;
    public static System.Action<string> onToast;      // "GOAL DONE! +100", for the HUD to show

    public static Goal Def(string id) { foreach (var g in Pool) if (g.id == id) return g; return Pool[0]; }

    static GoalSave Fresh(List<GoalSave> have)
    {
        var cands = new List<Goal>();
        foreach (var g in Pool) { bool taken = false; foreach (var h in have) if (h.id == g.id) { taken = true; break; } if (!taken) cands.Add(g); }
        var pick = cands.Count > 0 ? cands[Random.Range(0, cands.Count)] : Pool[0];
        return new GoalSave { id = pick.id, n = 0, done = false };
    }

    /// the three goals, built the moment they are first needed so the first run counts
    public static List<GoalSave> Ensure()
    {
        var gs = SaveData.S.goals;
        // drop ids this build no longer knows, then top up to three
        for (int i = gs.Count - 1; i >= 0; i--) { bool known = false; foreach (var g in Pool) if (g.id == gs[i].id) { known = true; break; } if (!known) gs.RemoveAt(i); }
        while (gs.Count > 3) gs.RemoveAt(gs.Count - 1);
        while (gs.Count < 3) gs.Add(Fresh(gs));
        return gs;
    }

    /// something happened during the run: key is close, chest, barrel, jelly or ramp
    public static void Event(string key, int n)
    {
        if (n <= 0) return;
        bool paid = false;
        foreach (var g in Ensure())
        {
            var d = Def(g.id);
            if (d.ev != key || d.run || g.done) continue;
            g.n = Mathf.Min(d.target, g.n + n);
            if (g.n >= d.target) { SaveData.Coins += Pay; paid = true; g.done = true; }
        }
        if (paid) { SaveData.Flush(); Sfx.Buzz(30); onToast?.Invoke("GOAL DONE! +" + Pay); }
    }

    /// the run-scoped goals, settled once; returns the puka they paid, for the caller to bank
    public static int RunEnd(int dist, int score)
    {
        int paid = 0;
        foreach (var g in Ensure())
        {
            var d = Def(g.id);
            if (!d.run || g.done) continue;
            int v = d.ev == "dist" ? dist : score;
            g.n = Mathf.Max(g.n, Mathf.Min(d.target, v));
            if (v >= d.target) { paid += Pay; g.done = true; }
        }
        if (paid > 0) onToast?.Invoke("GOAL DONE! +" + paid);
        return paid;
    }

    /// at the start of a run: finished goals make way for new ones
    public static void Roll()
    {
        var gs = Ensure();
        for (int i = 0; i < gs.Count; i++) if (gs[i].done) gs[i] = Fresh(gs);
        atStart.Clear();
        foreach (var g in gs) atStart[g.id] = g.n;
    }

    // what each goal stood at when the run began, so the run card can run its bar on from there
    // rather than from nothing; a revive carries on the same run, so it is not taken again
    static readonly Dictionary<string, int> atStart = new Dictionary<string, int>();
    /// the goal's count when this run began, or -1 when no run has begun since the game opened
    public static int StartN(string id) => atStart.TryGetValue(id, out var n) ? n : -1;

    // ---- the run's messages, read for what they announce ----
    // The water (Spawner, Crate, SetWave) tells the rider what happened with Say(), and that is
    // the one place every event already passes through. Called once a frame from the game loop
    // after the world has stepped; a message is new when msgT did not simply tick down.
    // The tricks are counted here too, for Stats: the web counts each one as it is thrown, and
    // the port counts it as it lands, which is the only moment the ride says its name.
    static float prevT; static string prevMsg;
    public static void Scan(BoardController r, float dt)
    {
        if (r == null) { prevT = 0; prevMsg = null; return; }
        bool fresh = r.running && r.msgT > 0f && (r.msgT > prevT - dt + 0.001f || r.lastMsg != prevMsg);
        prevT = r.msgT; prevMsg = r.lastMsg;
        if (!fresh) return;
        string m = r.lastMsg ?? "";
        if (m.StartsWith("CLOSE CALL") || m.StartsWith("DODGED THE SHARK")) { Event("close", 1); Stats.Bump("closeCalls"); }
        else if (m.StartsWith("TREASURE CHEST")) { Event("chest", 1); Stats.Bump("crates"); }
        else if (m.StartsWith("SPAT OUT") || m.StartsWith("OUT CLEAN")) { Event("barrel", 1); Stats.Bump("barrels"); Stats.Trick("barrel"); }   // the web notes a barrel as a trick too
        else if (m.StartsWith("JELLY BOUNCE")) { Event("jelly", 1); Stats.Bump("jellies"); }
        else if (m.StartsWith("RAMP")) { Event("ramp", 1); Stats.Bump("ramps"); }
        else if (m.StartsWith("PEARL!")) Event("pearl", 1);
        else if (m.StartsWith("HANDSTAND! +")) Stats.Trick("handstand");
        else if (m.StartsWith("INDY GRAB! +") || m.StartsWith("MELON GRAB! +")) Stats.Trick("grab");   // web endGrab's noteTrick (26916)
        else if (m.StartsWith("HAND DRAG! +")) Stats.Trick("drag");                                     // web endDrag's (26909)
        else if (m.Contains("° "))
        {
            // "540° SPIN 360° FLIP CLEAN COMBO! +…": every figure in it was a trick, counted under
            // the web's key for it, so a combo is as many tricks as it has parts
            foreach (System.Text.RegularExpressions.Match x in Figure.Matches(m))
                Stats.Trick(x.Groups[1].Value == "SPIN" ? "spin" : x.Groups[1].Value == "ROLL" ? "roll" : x.Groups[1].Value == "FLIP" ? "flip" : "heli");
        }
        else
        {
            // a board trick says its own name: "KICKFLIP! +220"
            foreach (var T in TrickCatalog.All)
                if (T.Board && m.StartsWith(T.name.ToUpperInvariant() + "! +")) { Stats.Trick(T.id); break; }
        }
    }
    static readonly System.Text.RegularExpressions.Regex Figure = new System.Text.RegularExpressions.Regex(@"\d+° (SPIN|ROLL|FLIP|HELICOPTER)\b");

    // ---- the panel ----
    public System.Action onClose;
    GameObject pane; RectTransform content; ScrollRect scroll;
    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "quests", "Quests", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) Fill(); }

    void Fill()
    {
        Pane.Clear(content);
        Pane.Note(content, "Three at a time. Each one pays " + Pay + " puka, and a finished goal makes way for a new one on your next run.", 40);
        foreach (var g in Ensure())
        {
            var d = Def(g.id);
            int n = Mathf.Min(g.n, d.target);
            var c = g.done ? Pane.Bar(content, d.text, "DONE +" + Pay, 1f, true)
                           : Pane.Bar(content, d.text, Pane.N(n) + " / " + Pane.N(d.target), n / (float)d.target);
            // the goal's line drawing in front of its words, gold with the rest of the row once done
            var ic = UiKit.Icon("goal_" + d.icon);
            if (ic != null)
            {
                var im = UiKit.Pic(c, "icon", ic, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(16, -19), new Vector2(19, 19));
                im.preserveAspect = true; im.raycastTarget = false; im.color = g.done ? UiKit.GoldText : Pane.Dim;
                // the words move over for it: the web's 23px icon column
                foreach (var t in c.GetComponentsInChildren<Text>()) if (t.alignment == TextAnchor.MiddleLeft) { var rt = t.rectTransform; rt.anchoredPosition += new Vector2(11.5f, 0); rt.sizeDelta -= new Vector2(23f, 0); }
            }
        }
        scroll.verticalNormalizedPosition = 1f;
    }
}
