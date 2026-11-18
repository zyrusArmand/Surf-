// Multiplayer: other surfers in your ocean, and the rules of the three modes.
//
// Everyone rides the same seeded ocean (Spawner.seed: obstacles by distance, one sequence), so the
// same thing waits at the same metre for all of them. Each surfer -- the bots now, players over the
// network later -- is one small state (Surfer): how far along, where across, how high, carving,
// spinning, down or out. The ocean always moves past YOU, so the others are drawn ahead of or behind
// you by the difference in distance, at their own place across the lane (CrewView).
//
//   Race     first to the finish line; a wipeout puts you back on your board a few seconds later.
//   Together everyone on one team: a downed surfer is back after a short wait as long as someone is
//            still up; the run ends only when everyone is down at once. Score: how far you got.
//   Brawl    three hearts each; punches and kicks knock rivals aside and cost them a heart (a kick
//            two), so does hitting something; last one standing wins.
using System.Collections.Generic;
using UnityEngine;

public enum MpMode { None, Race, Together, Brawl }

public class Surfer
{
    public int id; public string name, riderId, boardId; public Color tint;
    public bool bot = true;
    public float dist, px, py, vx, vy, eff, carve, spin;
    public bool down, outOfGame, finished;
    public float downT, finishT;
    public int hearts = 3, score;
    // bots: their pace, where they mean to be across, the next change of mind, the attack clock
    public float skill, wantX, mindT, atkCd, lunge;
    public bool spinning;
    // a brawl swing: wound up for a moment first (its tag shows "!"), so the one it is aimed at can
    // steer out of reach; the target is 0 for the local surfer, else a bot's id
    public float windup; public bool windKick; public int windTarget;
}

public class Multi : MonoBehaviour
{
    public BoardController rider; public Spawner spawner; public Camera cam; public Hud hud;
    public System.Action reviveLocal;                     // the game's own respawn (Bootstrap's)
    public System.Action<Vector3, int> splash;            // a splash at a point (Fx)
    public System.Action<MpResult> onEnd;                 // the round is over: show the result

    public const float RaceGoal = 1000f, TogetherWait = 8f, RaceWait = 0f;
    public MpMode mode = MpMode.None;
    public readonly List<Surfer> others = new List<Surfer>();
    public int localHearts = 3; public bool localOut, localFinished; public float localFinishT;
    public float clock; public int seed;
    bool running, ended; float localDownT;
    // a brawl opens with a count of three and nobody swinging; a hit, and a return to the board,
    // leave the local surfer a moment no one can hit him in
    public const float BrawlGrace = 3f, HitImmune = 1.5f, BackImmune = 3f;
    float startGrace, localImmune;

    // ---- going down: the choice ----
    // Down and not coming back on his own (Race, a brawl lost), he chooses: pay to get straight back
    // on, watch until the round is won (the ocean follows the surfer he watches), or go home. In
    // Together he is back for free after the wait, or sooner for a fee.
    public const int RespawnCost = 25, BrawlRespawnCost = 50;
    public bool Choosing { get; private set; }
    public bool Watching { get; private set; }
    public bool Waiting => Running && mode == MpMode.Together && LocalDown && !ended;
    public float WaitLeft => Mathf.Max(0f, TogetherWait - localDownT);
    public int Cost => mode == MpMode.Brawl ? BrawlRespawnCost : RespawnCost;
    public System.Action onHome;                           // go home: back to the beach
    Surfer watched;

    /// pay and get straight back on the board
    public bool PayRespawn()
    {
        if (!Running || SaveData.Coins < Cost) return false;
        SaveData.Coins -= Cost; SaveData.Flush();
        Choosing = false; Watching = false; watched = null; spawner.castAnyway = false;
        if (mode == MpMode.Brawl) { localOut = false; localHearts = 1; }
        localDownT = 0f; localImmune = BackImmune;
        reviveLocal?.Invoke();
        return true;
    }

    /// sit it out and watch until the round is won
    public void Watch() { if (!Running) return; Choosing = false; Watching = true; watched = null; }

    public void GoHome() { Choosing = false; Watching = false; onHome?.Invoke(); }

    // watching: the ocean follows the surfer being watched, as it follows him when he rides
    void Follow()
    {
        if (watched == null || watched.down || watched.outOfGame)
        {
            watched = null; float best = float.MinValue;
            foreach (var o in others) if (!o.down && !o.outOfGame && o.dist > best) { best = o.dist; watched = o; }
        }
        spawner.castAnyway = true;                              // his ocean goes on being cast for the one he watches
        if (watched == null) return;
        rider.dist = watched.dist; rider.px = watched.px; rider.eff = watched.eff;
    }
    CrewView view;

    public bool On => mode != MpMode.None;
    public bool Running => running && !ended;

    static readonly string[] BotNames = { "Kai", "Lani", "Nalu", "Koa", "Moana", "Iki" };

    /// set up a round before the run starts: the mode, how many bots, and the ocean's seed
    public void Prepare(MpMode m, int bots, int oceanSeed)
    {
        Stop();
        mode = m; seed = oceanSeed; ended = false; running = false; clock = 0f;
        localHearts = 3; localOut = false; localFinished = false; localDownT = 0f;
        var rng = new System.Random(oceanSeed ^ 0x5f3759df);
        var riders = new List<string>(); foreach (var r in RiderCatalog.All) if (r.id != SaveData.S.rider) riders.Add(r.id);
        var boards = new List<string>(); foreach (var b in BoardCatalog.All) boards.Add(b.id);
        for (int i = 0; i < bots; i++)
        {
            var s = new Surfer
            {
                id = i + 1, name = BotNames[i % BotNames.Length], bot = true,
                riderId = riders.Count > 0 ? riders[rng.Next(riders.Count)] : SaveData.S.rider,
                boardId = boards.Count > 0 ? boards[rng.Next(boards.Count)] : SaveData.S.equipped,
                tint = Color.HSVToRGB((0.08f + i * 0.27f) % 1f, 0.75f, 1f),
                skill = (float)(rng.NextDouble() - 0.5) * 0.10f,
                px = (i % 2 == 0 ? 1f : -1f) * (m == MpMode.Brawl ? 6f + 5f * (i / 2) : 3.5f + 3f * (i / 2)),
                // in a brawl spread out round you, not on top of you
                dist = m == MpMode.Brawl ? (i == 0 ? 14f : i == 1 ? -12f : 24f) : 0f,
                eff = 0f, hearts = 3,
                atkCd = BrawlGrace + 1f + (float)rng.NextDouble() * 2f
            };
            s.wantX = s.px;
            others.Add(s);
        }
        if (view == null) view = gameObject.AddComponent<CrewView>();
        view.Build(others, rider, cam, hud);
    }

    /// the run has started (Bootstrap.Play)
    public void Begin() { if (On) { running = true; clock = 0f; startGrace = mode == MpMode.Brawl ? BrawlGrace : 0f; localImmune = 0f; } }

    /// back to the title: everyone gone
    public void Stop()
    {
        running = false; ended = false; mode = MpMode.None;
        Choosing = false; Watching = false; watched = null; if (spawner != null) spawner.castAnyway = false;
        others.Clear();
        if (view != null) view.Clear();
    }

    // ---------------------------------------------------------------- each frame of the ride
    public void Step(float dt)
    {
        if (!Running) return;
        clock += dt;
        if (startGrace > 0f)
        {
            int was = Mathf.CeilToInt(startGrace); startGrace -= dt; int now = Mathf.CeilToInt(startGrace);
            if (hud != null && (now != was || clock <= dt)) hud.Toast(startGrace > 0f ? now.ToString() : "FIGHT!");
        }
        localImmune = Mathf.Max(0f, localImmune - dt);
        if (Watching) Follow();
        foreach (var s in others) StepBot(s, dt);
        if (mode == MpMode.Brawl) BotAttacks(dt);
        Rules(dt);
        if (view != null) view.Step(others, mode, dt);
    }

    void StepBot(Surfer s, float dt)
    {
        if (s.outOfGame) return;
        if (s.down)
        {
            // back on the board after the mode's wait; in Together only while someone is still up
            s.downT += dt;
            float wait = mode == MpMode.Together ? TogetherWait : 2.5f;
            bool someoneUp = !LocalDown || AnyBotUp(s);
            if (s.downT >= wait && (mode != MpMode.Together || someoneUp))
            { s.down = false; s.py = 0f; s.vy = 0f; s.spin = 0f; s.eff *= 0.6f; }
            return;
        }
        if (s.finished) { s.dist += s.eff * dt; return; }
        // pace: about yours, a little faster or slower by temperament, eased back toward you when far
        // off so the field stays together; in a brawl, toward whoever it is after
        float you = rider.dist, gap = s.dist - you;
        float baseV = Mathf.Max(22f, rider.running ? rider.eff : 30f);
        float rubber = Mathf.Clamp(-gap / 60f, -0.25f, 0.25f);
        float targetV = baseV * (1f + s.skill + rubber);
        if (mode == MpMode.Brawl) { var t = Target(s); if (t.HasValue) targetV = baseV * (1f + Mathf.Clamp((t.Value.y - s.dist) / 25f, -0.3f, 0.3f)); }
        s.eff = Mathf.MoveTowards(s.eff, targetV, 18f * dt);
        s.dist += s.eff * dt;
        // across: wander, steer round what is in the way, line up a ramp now and then, and in a brawl
        // go for someone
        s.mindT -= dt;
        if (s.mindT <= 0f) { s.mindT = 1.5f + Random.value * 2.5f; s.wantX = Mathf.Clamp(s.px + (Random.value - 0.5f) * 14f, -13f, 13f); }
        if (mode == MpMode.Brawl) { var t = Target(s); if (t.HasValue && Mathf.Abs(t.Value.y - s.dist) < 30f) s.wantX = t.Value.x + (s.px > t.Value.x ? 1.6f : -1.6f); }
        foreach (var o in spawner.obstacles)
        {
            if (o.go == null || o.knocked) continue;
            float ahead = (rider.dist - o.z) - s.dist;           // the obstacle's metre, against the bot's
            if (ahead < 0.5f || ahead > 38f) continue;
            float half = Mathf.Max(1.2f, o.hx + 0.9f);
            if (o.ramp) { if (s.py <= 0f && Mathf.Abs(o.x - s.px) < 6f && Random.value < 0.01f) s.wantX = o.x; continue; }
            if (Mathf.Abs(o.x - s.px) < half + 1.5f) s.wantX = Mathf.Clamp(o.x + (s.px >= o.x ? 1f : -1f) * (half + 2.8f), -15f, 15f);
        }
        s.vx = Mathf.MoveTowards(s.vx, Mathf.Clamp((s.wantX - s.px) * 2.2f, -13f, 13f), 34f * dt);
        s.px = Mathf.Clamp(s.px + s.vx * dt, -16.5f, 16.5f);
        s.carve = Mathf.Clamp(s.vx / 9f, -1f, 1f);
        s.lunge = Mathf.MoveTowards(s.lunge, 0f, dt * 4f);
        // the air: off a ramp, a spin on the way, and down again
        if (s.py > 0f || s.vy > 0f)
        {
            s.vy -= 32f * dt; s.py += s.vy * dt;
            if (s.spinning) s.spin += 540f * dt;
            if (s.py <= 0f) { s.py = 0f; s.vy = 0f; if (s.spinning) s.score += 120; s.spinning = false; s.spin = 0f; }
        }
        // what it meets: a ramp throws it, anything else puts it in the water
        foreach (var o in spawner.obstacles)
        {
            if (o.go == null || o.knocked) continue;
            float at = (rider.dist - o.z) - s.dist;
            if (Mathf.Abs(at) > 1.3f) continue;
            float half = Mathf.Max(0.9f, o.hx + 0.4f);
            if (Mathf.Abs(o.x - s.px) > half) continue;
            if (o.ramp) { if (s.py <= 0.1f) { s.vy = 13f + Random.value * 4f; s.py = 0.05f; s.spinning = Random.value < 0.7f; } }
            else if (s.py < 1.0f && o.kind != "coin") Down(s, true);
            break;
        }
        s.score += Mathf.RoundToInt(s.eff * dt * 0.5f);
        if (mode == MpMode.Race && !s.finished && s.dist >= RaceGoal) { s.finished = true; s.finishT = clock; }
    }

    bool LocalDown => rider.wipe || !rider.running;
    bool AnyBotUp(Surfer except) { foreach (var o in others) if (o != except && !o.down && !o.outOfGame) return true; return false; }

    // a brawl bot's target: the nearest rival still in, as (across, distance)
    Vector2? Target(Surfer s)
    {
        Vector2? best = null; float bd = float.MaxValue;
        if (!localOut && !LocalDown) { float d = Mathf.Abs(rider.dist - s.dist) + Mathf.Abs(rider.px - s.px) * 0.5f; if (d < bd) { bd = d; best = new Vector2(rider.px, rider.dist); } }
        foreach (var o in others)
        {
            if (o == s || o.down || o.outOfGame) continue;
            float d = Mathf.Abs(o.dist - s.dist) + Mathf.Abs(o.px - s.px) * 0.5f;
            if (d < bd) { bd = d; best = new Vector2(o.px, o.dist); }
        }
        return best;
    }

    void Down(Surfer s, bool crash)
    {
        if (s.down) return;
        s.down = true; s.downT = 0f; s.vx = 0f; s.py = 0f; s.vy = 0f; s.spinning = false;
        if (mode == MpMode.Brawl && crash) s.hearts--;
        if (mode == MpMode.Brawl && s.hearts <= 0) s.outOfGame = true;
        if (splash != null) splash(new Vector3(s.px, 0f, -(s.dist - rider.dist)), 30);
    }

    // ---------------------------------------------------------------- brawl
    public const float PunchReach = 2.6f, KickReach = 3.6f, AlongReach = 4.5f;
    float localAtkCd;

    /// the local surfer's punch (false) or kick (true): the nearest rival in reach is knocked aside
    public void LocalAttack(bool kick)
    {
        if (!Running || mode != MpMode.Brawl || localOut || LocalDown || localAtkCd > 0f || startGrace > 0f) return;
        localAtkCd = kick ? 0.9f : 0.45f;
        Surfer best = null; float bd = float.MaxValue;
        foreach (var o in others)
        {
            if (o.down || o.outOfGame) continue;
            float dz = o.dist - rider.dist, dx = o.px - rider.px;
            if (Mathf.Abs(dz) > AlongReach || Mathf.Abs(dx) > (kick ? KickReach : PunchReach)) continue;
            float d = Mathf.Abs(dz) + Mathf.Abs(dx);
            if (d < bd) { bd = d; best = o; }
        }
        Sfx.Buzz(kick ? 25 : 12);
        if (best == null) { if (hud != null) hud.Toast(kick ? "KICK!" : "PUNCH!"); return; }
        float dir = Mathf.Sign(best.px - rider.px); if (dir == 0f) dir = 1f;
        best.vx += dir * (kick ? 17f : 10f); best.wantX = best.px + dir * 5f; best.mindT = 1.2f;
        best.hearts -= 1;                                                  // every hit is one heart; a kick shoves harder
        best.windup = 0f;                                                  // a hit spoils their swing
        if (hud != null) hud.Toast(kick ? "KICK!" : "POW!");
        if (splash != null) splash(new Vector3(best.px, 0f, -(best.dist - rider.dist)), kick ? 18 : 10);
        if (best.hearts <= 0) { best.hearts = 0; Down(best, false); best.outOfGame = true; }
        else if (kick && Mathf.Abs(best.vx) > 14f) Down(best, false);           // a big enough kick puts them in the water
    }

    void BotAttacks(float dt)
    {
        localAtkCd = Mathf.Max(0f, localAtkCd - dt);
        if (startGrace > 0f) return;
        foreach (var s in others)
        {
            if (s.down || s.outOfGame) { s.windup = 0f; continue; }
            // a swing wound up: it lands only if the one it was aimed at is still in reach
            if (s.windup > 0f)
            {
                s.windup -= dt;
                if (s.windup <= 0f) { s.windup = 0f; Land(s); s.atkCd = 2.5f + Random.value * 1.5f; }
                continue;
            }
            s.atkCd -= dt; if (s.atkCd > 0f) continue;
            // who to go for: anyone in reach, the local surfer only when nobody else is already
            // winding up on him and he can be hit, so they do not all pile on at once
            bool kick = Random.value < 0.3f;
            float reach = (kick ? KickReach : PunchReach) + 0.6f;
            int target = -1;
            bool localFree = !localOut && !LocalDown && localImmune <= 0f;
            if (localFree) foreach (var o in others) if (o != s && o.windup > 0f && o.windTarget == 0) localFree = false;
            bool localNear = localFree && Mathf.Abs(rider.dist - s.dist) < AlongReach && Mathf.Abs(rider.px - s.px) < reach;
            Surfer near = null;
            foreach (var o in others)
            {
                if (o == s || o.down || o.outOfGame) continue;
                if (Mathf.Abs(o.dist - s.dist) < AlongReach && Mathf.Abs(o.px - s.px) < reach) { near = o; break; }
            }
            if (localNear && (near == null || Random.value < 0.5f)) target = 0;
            else if (near != null) target = near.id;
            if (target < 0) continue;
            s.windup = 0.35f; s.windKick = kick; s.windTarget = target; s.lunge = 0.4f;
        }
    }

    // a wound-up swing comes down: on its target if still in reach, else on the water
    void Land(Surfer s)
    {
        bool kick = s.windKick; float reach = kick ? KickReach : PunchReach;
        s.lunge = 1f;
        if (s.windTarget == 0)
        {
            if (localOut || LocalDown || localImmune > 0f) return;
            if (Mathf.Abs(rider.dist - s.dist) > AlongReach || Mathf.Abs(rider.px - s.px) > reach) { if (hud != null) hud.Toast("Dodged " + s.name + "!"); return; }
            float dir = Mathf.Sign(rider.px - s.px); if (dir == 0f) dir = 1f;
            rider.vx += dir * (kick ? 16f : 9f);
            localHearts -= 1; localImmune = HitImmune;
            Sfx.Buzz(kick ? 40 : 20);
            if (hud != null) hud.Toast(s.name + (kick ? " kicked you!" : " punched you!"));
            if (splash != null) splash(new Vector3(rider.px, rider.waterY, 0f), kick ? 18 : 10);
            if (localHearts <= 0) { localHearts = 0; rider.Wipeout("buoy"); }
            return;
        }
        foreach (var o in others)
        {
            if (o.id != s.windTarget) continue;
            if (o.down || o.outOfGame || Mathf.Abs(o.dist - s.dist) > AlongReach || Mathf.Abs(o.px - s.px) > reach) return;
            float dir = Mathf.Sign(o.px - s.px); if (dir == 0f) dir = 1f;
            o.vx += dir * (kick ? 16f : 9f); o.hearts -= 1; o.windup = 0f;
            if (o.hearts <= 0) { o.hearts = 0; Down(o, false); o.outOfGame = true; }
            return;
        }
    }

    // ---------------------------------------------------------------- the rules and the end
    void Rules(float dt)
    {
        if (mode == MpMode.Race)
        {
            if (!localFinished && !Watching && rider.dist >= RaceGoal) { localFinished = true; localFinishT = clock; End(); }
            if (Watching) foreach (var o in others) if (o.finished) { End(); break; }   // the race is won
        }
        else if (mode == MpMode.Together)
        {
            bool allDown = LocalDown; foreach (var o in others) if (!o.down) allDown = false;
            if (allDown && LocalDown) End();
        }
        else if (mode == MpMode.Brawl)
        {
            int bots = 0; foreach (var o in others) if (!o.outOfGame) bots++;
            if (!localOut && bots == 0) End();                         // last one standing: you
            else if (localOut && bots <= 1) End();                     // out, and the brawl is won by someone else
        }
    }

    /// the local surfer's wipeout has played out (Bootstrap, where the card would come up): back on
    /// the board, waiting to be, or out. Returns true when the round goes on.
    public bool LocalWipeDone(float dt)
    {
        if (!Running) return false;
        switch (mode)
        {
            case MpMode.Race:
                if (!Watching) Choosing = true;                        // respawn for puka, watch, or go home
                return true;
            case MpMode.Together:
                {
                    bool partnerUp = false; foreach (var o in others) if (!o.down) partnerUp = true;
                    if (!partnerUp) { Choosing = false; End(); return false; }
                    localDownT += dt;
                    if (hud != null && Mathf.FloorToInt(localDownT * 4f) != Mathf.FloorToInt((localDownT - dt) * 4f))
                        hud.Toast("Back in " + Mathf.CeilToInt(TogetherWait - localDownT) + "...");
                    if (localDownT >= TogetherWait) { localDownT = 0f; reviveLocal?.Invoke(); }
                    return true;
                }
            case MpMode.Brawl:
                if (localOut) return true;                              // choosing or watching already
                if (localHearts > 0) localHearts--;              // a crash costs a heart (one lost to a last hit is already gone)
                if (localHearts <= 0)
                {
                    localOut = true;
                    int bots = 0; foreach (var o in others) if (!o.outOfGame) bots++;
                    if (bots <= 1) { End(); return false; }             // nobody left to watch fight
                    Choosing = true; return true;                        // rejoin for puka, watch, or go home
                }
                localImmune = BackImmune;
                reviveLocal?.Invoke(); return true;
        }
        return false;
    }

    void End()
    {
        if (ended) return;
        ended = true; Choosing = false; Watching = false; spawner.castAnyway = false;
        var res = new MpResult { mode = mode, dist = Mathf.RoundToInt(rider.dist), coins = rider.coins };
        if (mode == MpMode.Race)
        {
            // finished first by time, then the rest by how far they got
            var rows = new List<(string name, bool you, bool fin, float t, float d)> { ("You", true, localFinished, localFinishT, rider.dist) };
            foreach (var o in others) rows.Add((o.name, false, o.finished, o.finishT, o.dist));
            rows.Sort((a, b) => a.fin != b.fin ? (a.fin ? -1 : 1) : a.fin ? a.t.CompareTo(b.t) : b.d.CompareTo(a.d));
            for (int i = 0; i < rows.Count; i++) { res.rows.Add((i + 1) + ". " + rows[i].name + (rows[i].fin ? "  " + rows[i].t.ToString("0.0") + "s" : "  " + Mathf.RoundToInt(rows[i].d) + " m")); if (rows[i].you) res.place = i + 1; }
            res.title = res.place == 1 ? "YOU WON THE RACE!" : "Race over: " + Ordinal(res.place);
        }
        else if (mode == MpMode.Together)
        {
            res.title = "Together: " + res.dist + " m";
            res.rows.Add("The crew got " + res.dist + " m before everyone went down");
            foreach (var o in others) res.rows.Add(o.name + ": " + o.score + " pts");
        }
        else
        {
            int left = 0; Surfer last = null; foreach (var o in others) if (!o.outOfGame) { left++; last = o; }
            res.place = localOut ? left + 1 : 1;
            res.title = !localOut ? "LAST ONE STANDING!" : (last != null && left == 1 ? last.name + " wins the brawl" : "You're out: " + Ordinal(res.place));
            res.rows.Add("You: " + (localOut ? "out" : localHearts + " hearts left"));
            foreach (var o in others) res.rows.Add(o.name + ": " + (o.outOfGame ? "out" : o.hearts + " hearts"));
        }
        onEnd?.Invoke(res);
    }

    static string Ordinal(int n) => n + (n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th");
}

public class MpResult
{
    public MpMode mode; public string title; public int place, dist, coins;
    public readonly List<string> rows = new List<string>();
}
