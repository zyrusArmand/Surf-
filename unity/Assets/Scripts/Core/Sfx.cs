// The game's sound, ported from the web's Web Audio layer. Every clip is a file under
// Resources/Sounds (the web has no synthesised sounds); one-shots come off a pool of sources so
// they can overlap, sustained sounds are loops that are STEERED every frame and stop themselves
// when nobody has asked for them for a quarter of a second (the web's keep-alive), the riding
// bed is six voices off three recordings with a lowpass lid that closes as he goes under, and
// the title track fades in whenever a run is not happening. The three switches in Settings are
// read straight off the save (sfxOff / musicOff / buzzOff), so nothing needs telling when they
// change except Refresh(). Gains and pitches at the call sites are the web's numbers.
//
// Call Init() once (any call does it on demand); drop SfxRunner, which this creates, nowhere.
using System.Collections.Generic;
using UnityEngine;

public static class Sfx
{
    public const float MusVol = 0.34f;          // the web's MUS_VOL
    const int PoolN = 14;                        // one-shots that may sound at once
    const float KeepAlive = 0.26f;               // a loop not asked for this long is not wanted

    public static bool SoundOn => !SaveData.S.sfxOff;
    public static bool MusicOn => !SaveData.S.musicOff;
    public static bool HapticsOn => !SaveData.S.buzzOff;

    static GameObject root;
    static AudioSource[] pool; static int poolAt;
    static AudioSource music, cue;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static bool preloaded;
    static float Now => Time.realtimeSinceStartup;

    // ---- loops ----
    class Voice
    {
        public AudioSource src; public AudioLowPassFilter lp;
        public float gain, pitch = 1f, pan;              // where the ramps have got to
        public float wantGain, wantPitch = 1f, wantPan;  // what was asked for
        public float seen, stopAt, stopTau; public bool stopping;
    }
    static readonly Dictionary<string, Voice> loops = new Dictionary<string, Voice>();
    static readonly List<string> dead = new List<string>();
    // two voices off one buffer: a second key that points at somebody else's clip
    static readonly Dictionary<string, string> Alias = new Dictionary<string, string> { { "glide2", "glide" }, { "wake2", "wake" }, { "wind2", "wind" } };
    // the riding bed goes through the lid; everything else straight out
    static readonly HashSet<string> Bed = new HashSet<string> { "glide", "glide2", "wake", "wake2", "wind", "wind2" };
    static float lidHz = 20000f, lidWant = 20000f;

    // ---- music ----
    static int musLast = -1;                     // -1 unknown, 0 off, 1 on: act on a CHANGE only
    static string musTrack = "title";
    static float musFrom, musTo, musT0, musMs; static bool musFading, musPaused;

    // ---- the long cue (the saucer) ----
    static float cueFrom, cueT0, cueMs; static bool cueFading;

    // ---- one-shots fired a beat later ----
    struct Later { public string name; public float gain, pitch, at; }
    static readonly List<Later> later = new List<Later>();

    // ---- the slow random wander under the bed ----
    class DriftS { public float a, b, t0, span; }
    static readonly Dictionary<string, DriftS> drifts = new Dictionary<string, DriftS>();

    // ---- which sample a trick has, and how many turns it has already sounded ----
    static readonly Dictionary<string, string> TrickSfx = new Dictionary<string, string> {
        { "roll", "roll" }, { "flip", "flip" }, { "spin", "spin" }, { "hand", "stand" }, { "handstand", "stand" },
        { "kickflip", "flip" }, { "heelflip", "flip" }, { "varial", "flip" }, { "treflip", "flip" }, { "shove", "spin" }, { "bigshove", "spin" } };
    static readonly Dictionary<string, int> trickTurns = new Dictionary<string, int>();

    // ---- gulls and palms remember their last pick so nothing sounds twice running ----
    static float gullLast = -99f; static int gullPrev; static int palmPrev;
    const float GullGap = 2.6f;

    // =====================================================================================
    public static void Init()
    {
        if (root != null) return;
        root = new GameObject("Sfx");
        root.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(root);
        pool = new AudioSource[PoolN];
        for (int i = 0; i < PoolN; i++) { pool[i] = NewSource("shot" + i); pool[i].priority = 128; }
        music = NewSource("music"); music.loop = true; music.priority = 0;
        cue = NewSource("cue"); cue.priority = 16;
        root.AddComponent<SfxRunner>();
        Preload();
    }

    static void Ensure() { if (root == null) Init(); }

    static AudioSource NewSource(string name)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.transform.SetParent(root.transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false; a.loop = false; a.spatialBlend = 0f; a.dopplerLevel = 0f;
        a.volume = 0f; a.pitch = 1f; a.panStereo = 0f;
        return a;
    }

    static void Preload()
    {
        if (preloaded) return; preloaded = true;
        AudioClip[] all = null;
        try { all = Resources.LoadAll<AudioClip>("Sounds"); } catch { all = null; }
        if (all == null) return;
        foreach (var c in all) if (c != null && !clips.ContainsKey(c.name)) clips[c.name] = c;
    }

    static AudioClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string a; if (Alias.TryGetValue(name, out a)) name = a;
        AudioClip c;
        if (clips.TryGetValue(name, out c)) return c;
        c = Resources.Load<AudioClip>("Sounds/" + name);
        clips[name] = c;                         // null too, so a missing file is asked for once
        return c;
    }

    /// is there a clip of that name
    public static bool Has(string name) { Ensure(); return Clip(name) != null; }

    // =====================================================================================
    // ---- one-shots ----
    /// the web's sfx(name, vol, rate): fire-and-forget, overlapping itself freely
    public static void Play(string name, float gain = 1f, float pitch = 1f)
    {
        Ensure();
        if (!SoundOn) return;
        var c = Clip(name); if (c == null) return;
        var s = Take();
        s.clip = c; s.volume = Mathf.Clamp01(gain); s.pitch = pitch > 0f ? pitch : 1f; s.panStereo = 0f;
        s.Play();
    }

    /// the same, a beat later (the web's setTimeout around a one-shot)
    public static void PlayLater(string name, float gain, float pitch, float delaySec)
    {
        Ensure();
        later.Add(new Later { name = name, gain = gain, pitch = pitch, at = Now + Mathf.Max(0f, delaySec) });
    }

    static AudioSource Take()
    {
        for (int i = 0; i < PoolN; i++)
        {
            var s = pool[(poolAt + i) % PoolN];
            if (!s.isPlaying) { poolAt = (poolAt + i + 1) % PoolN; return s; }
        }
        var o = pool[poolAt]; poolAt = (poolAt + 1) % PoolN; return o;     // all busy: the oldest goes
    }

    /// the web's sfxUi: a sound that belongs to a tap (0.55 by default, pitched a hair)
    public static void Ui(string name, float gain = 0.55f) { Play(name, gain, 0.98f + Random.value * 0.05f); }

    // ---- sustained sounds ----
    /// the web's sfxLoop: start once, then steer it every frame. Gain, pitch and pan ramp
    /// toward what is asked (0.06 / 0.10 / 0.09 s time constants); stop asking and it fades
    /// itself out a quarter of a second later. pitch <= 0 leaves the pitch where it is.
    public static void Loop(string name, float gain, float pitch = 1f, float pan = 0f)
    {
        Ensure();
        if (!SoundOn) { LoopStop(name, 120); return; }
        Voice v;
        if (!loops.TryGetValue(name, out v))
        {
            var c = Clip(name); if (c == null) return;
            var s = NewSource("loop:" + name);
            s.clip = c; s.loop = true; s.volume = 0f; s.priority = 32;
            v = new Voice { src = s };
            if (Bed.Contains(name)) { v.lp = s.gameObject.AddComponent<AudioLowPassFilter>(); v.lp.cutoffFrequency = lidHz; }
            s.Play();
            loops[name] = v;
        }
        v.seen = Now; v.stopping = false;
        v.wantGain = Mathf.Clamp(gain, 0f, 1.2f);
        if (pitch > 0f) v.wantPitch = pitch;
        v.wantPan = Mathf.Clamp(pan, -1f, 1f);
    }

    /// fade a loop out over ms and release it
    public static void LoopStop(string name, int ms = 200)
    {
        Voice v; if (!loops.TryGetValue(name, out v) || v.stopping) return;
        float s = Mathf.Max(0.05f, ms / 1000f);
        v.stopping = true; v.stopTau = s / 3f; v.stopAt = Now + s + 0.05f;
    }

    public static void LoopStopAll(int ms = 200) { foreach (var k in loops.Keys) LoopStop(k, ms); }

    /// how far under he is, 0 dry to 1 submerged: the lid on the riding bed, 20 kHz down to
    /// 340 Hz travelled geometrically (the web's bedSubmerge)
    public static void Submerge(float u) { lidWant = 20000f * Mathf.Pow(340f / 20000f, Mathf.Clamp01(u)); }

    /// the web's drift(): a slow raised-cosine walk between random targets in [lo, hi]
    public static float Drift(string key, float lo, float hi, float secs)
    {
        float now = Now; DriftS d;
        if (!drifts.TryGetValue(key, out d))
        {
            d = new DriftS { a = lo + Random.value * (hi - lo), b = lo + Random.value * (hi - lo), t0 = now, span = secs * (0.6f + Random.value * 0.8f) };
            drifts[key] = d;
        }
        float k = (now - d.t0) / Mathf.Max(0.01f, d.span);
        if (k >= 1f) { d.a = d.b; d.b = lo + Random.value * (hi - lo); d.t0 = now; d.span = secs * (0.6f + Random.value * 0.8f); k = 0f; }
        return d.a + (d.b - d.a) * (0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(k)));
    }

    /// The riding bed, the web's six voices plus the two rail loops, asked for every frame of
    /// a run (and only then: stop calling and it fades out by itself). speed is the curve
    /// (16.2..52), carve is -1..1, sub is 0 dry to 1 under water, duck is 1 normally (0.34
    /// while a saucer is on, 0.68 while he wears its costume).
    public static void RideBed(float speed, float carve, bool airborne, float sub = 0f, float duck = 1f)
    {
        Ensure();
        float rSp = Mathf.Clamp01((speed - 16f) / 22f);
        float rUnder = sub > 0f ? 0f : 1f, rAir = airborne ? 0.30f : 1f;
        sub = Mathf.Clamp01(sub);
        Submerge(sub);
        float rCv = Mathf.Clamp01((Mathf.Abs(carve) - 0.10f) / 0.80f);
        float BED = 0.95f * (1f - 0.58f * sub) * duck;
        // base glide: steady, ducked hard in the air, the board is not touching anything
        float gA = airborne ? 0.15f : 1f;
        Loop("glide", BED * (0.26f + rSp * 0.16f) * gA * Drift("g1", 0.72f, 1.00f, 5.3f), 0.95f + rSp * 0.09f, -0.22f);
        Loop("glide2", BED * (0.18f + rSp * 0.12f) * gA * Drift("g2", 0.60f, 1.00f, 7.9f), 0.99f + rSp * 0.09f, 0.24f);
        // wake: speed and rail; a jump takes it almost to nothing
        float wk = (airborne ? 0.06f : 1f) * (1f - 0.75f * sub);
        Loop("wake", BED * (0.13f + rSp * 0.26f + rCv * 0.14f) * wk * Drift("k1", 0.70f, 1.00f, 4.1f), 0.97f + rSp * 0.10f, 0.30f + carve * 0.35f);
        Loop("wake2", BED * (0.09f + rSp * 0.18f + rCv * 0.10f) * wk * Drift("k2", 0.55f, 1.00f, 6.7f), 1.02f + rSp * 0.10f, -0.28f + carve * 0.35f);
        // wind: the speed indicator, and the only layer the air makes louder
        float wn = (0.06f + 0.94f * Mathf.Pow(rSp, 1.6f)) * (1f - sub), wnAir = airborne ? 1.15f : 1f;
        Loop("wind", BED * wn * 0.30f * wnAir * Drift("n1", 0.75f, 1.00f, 3.7f), 0.92f + rSp * 0.22f, 0.34f);
        Loop("wind2", BED * wn * 0.22f * wnAir * Drift("n2", 0.55f, 1.00f, 9.1f), 0.97f + rSp * 0.22f, -0.36f);
        // the carve: a soft take and a harsh one, crossfaded in power by how hard the rail is in
        float rT = Mathf.Clamp01((Mathf.Abs(carve) - 0.12f) / 0.78f);
        float tPan = Mathf.Clamp(carve * 0.7f, -1f, 1f);
        float tLvl = (0.34f + rT * 0.46f) * rT * rUnder * rAir;
        Loop("turns", tLvl * Mathf.Sqrt(1f - rT) * 1.15f, 0.96f + rT * 0.10f, tPan);
        Loop("turnh", tLvl * Mathf.Sqrt(rT), 0.95f + rT * 0.18f, tPan);
    }

    // ---- the other sustained sounds, each with the web's formula; ask every frame it applies ----
    /// the dive bed, faded in with depth (feet under)
    public static void Under(float depth) { Loop("under", 0.46f + Mathf.Min(1f, depth / 3.0f) * 0.46f, 1.0f); }
    /// the jetpack: on is 0 idle .. 1 full thrust
    public static void Jet(float on) { Loop("jet", 0.20f + on * 0.34f, 0.93f + on * 0.20f); }
    /// the jet ski heard where it is: dx, dz from the rider in feet
    public static void Ski(float dx, float dz, bool under = false)
    {
        float d = Mathf.Sqrt(dx * dx + dz * dz), near = Mathf.Max(0f, 1f - d / 64f);
        Loop("ski", (0.08f + near * near * 0.62f) * (under ? 0.25f : 1f), 0.94f + near * 0.16f, Mathf.Clamp(dx / 26f, -1f, 1f));
    }
    /// the tow boat's diesel: level and pan from where it is, pitch from its throttle
    public static void Boat(float dx, float dz, float spd, bool under = false)
    {
        float d = Mathf.Sqrt(dx * dx + dz * dz), near = Mathf.Max(0f, 1f - d / 96f);
        Loop("boat", (0.10f + near * near * 0.56f) * (under ? 0.25f : 1f), 0.88f + Mathf.Min(1f, spd / 34f) * 0.22f, Mathf.Clamp(dx / 34f, -1f, 1f));
    }
    /// the saucer's wind: rise 0..1 over its approach, duck 1 then down to 0 once he is lifted
    public static void UfoWind(float rise, float duck = 1f) { Loop("ufowind", 0.16f + 0.46f * rise * rise * duck, 0.95f + rise * 0.10f); }
    /// the strangeness that runs under the borrowed costume
    public static void Weird() { Loop("weird", 0.30f + 0.10f * Mathf.Sin(Now / 2.3f), 1.0f); }

    // ---- the long cue, streamed (the web's cue / cueVol / cueStop) ----
    public static void Cue(string name, float vol = 0.75f, float at = 0f)
    {
        Ensure();
        if (!SoundOn) return;
        var c = Clip(name); if (c == null) return;
        cueFading = false;
        if (cue.clip != c) { cue.Stop(); cue.clip = c; }
        if (cue.isPlaying) cue.Stop();
        cue.loop = false;
        cue.time = Mathf.Clamp(at, 0f, Mathf.Max(0f, c.length - 0.05f));
        cue.volume = Mathf.Clamp01(vol);
        cue.Play();
    }
    public static void CueVol(float v) { if (cue == null || !cue.isPlaying || cueFading) return; cue.volume = Mathf.Clamp01(v); }
    public static void CueStop(int ms = 600)
    {
        if (cue == null || !cue.isPlaying) return;
        cueFrom = cue.volume; cueT0 = Now; cueMs = Mathf.Max(60, ms); cueFading = true;
    }

    // ---- music ----
    /// start (or switch to) a track; it fades in over 0.8 s and loops
    public static void Music(string track = "title")
    {
        Ensure();
        if (track != musTrack || music.clip == null)
        {
            musTrack = track; music.Stop(); musPaused = false; music.volume = 0f;
            music.clip = Clip(track); musLast = -1;
        }
        MusicTick(true);
    }
    /// fade the track out over 0.38 s and pause it where it is
    public static void MusicStop() { Ensure(); MusicTick(false); }
    /// the web's musicTick: asked every frame with "is a run NOT happening", acts on a change
    /// only, never restarts a fade. Pass !rider.running.
    public static void MusicTick(bool want)
    {
        Ensure();
        int w = (want && MusicOn) ? 1 : 0;
        if (w == musLast) return;
        musLast = w;
        if (w == 1)
        {
            if (music.clip == null) music.clip = Clip(musTrack);
            if (music.clip == null) return;
            if (musPaused) { music.UnPause(); musPaused = false; }
            else if (!music.isPlaying) music.Play();
            MusFade(MusVol, 800f);
        }
        else MusFade(0f, 380f);
    }
    static void MusFade(float to, float ms) { musFrom = music.volume; musTo = to; musT0 = Now; musMs = ms; musFading = true; }

    /// call after a settings switch is flipped: the music re-decides, loops obey the sound switch
    public static void Refresh()
    {
        Ensure();
        musLast = -1;
        if (!SoundOn) { LoopStopAll(120); CueStop(120); }
    }

    // ---- haptics ----
    /// the web's buzz(ms): every button's tap. It sounds (the btn click, weighted by what the
    /// tap meant) whatever the haptics switch says, and vibrates only if that switch is on.
    public static void Buzz(int ms)
    {
        Ensure();
        Play("btn", ms >= 30 ? 0.46f : ms >= 18 ? 0.38f : 0.30f, 0.97f + Random.value * 0.08f);
        Vibrate();
    }
    /// a pattern, e.g. Buzz(60, 50, 80) for the wipeout: the heaviest click, one vibration
    public static void Buzz(params int[] pattern)
    {
        Ensure();
        Play("btn", 0.50f, 0.97f + Random.value * 0.08f);
        Vibrate();
    }
    static void Vibrate()
    {
        if (!HapticsOn) return;
#if UNITY_ANDROID || UNITY_IOS
        try { Handheld.Vibrate(); } catch { }
#endif
    }

    // =====================================================================================
    // ---- the events, each with the web's gain and pitch ----
    /// the board hitting the water after a jump; impact = min(1.6, max(0, -vy) / 15)
    public static void Land(float impact) { Play("land", 0.34f + impact * 0.42f, 0.94f + Random.value * 0.13f); }

    /// a trick on the press: "roll", "flip", "spin", "hand"
    public static void Trick(string trick)
    {
        string s; if (!TrickSfx.TryGetValue(trick, out s)) return;
        Play(s, 0.80f, 0.96f + Random.value * 0.09f);
    }

    /// the web's trickTurn: call every frame with the rotation accumulator (radians) and whether
    /// that trick is still being held; sounds the sample again, a shade lower and softer, on
    /// every whole turn crossed while it is. Resyncs silently when it is not.
    public static void TrickTurn(string trick, float rot, bool active)
    {
        int n = Mathf.FloorToInt(rot / (Mathf.PI * 2f));
        int prev; bool had = trickTurns.TryGetValue(trick, out prev);
        if (!active || !(rot >= 0f)) { trickTurns[trick] = n; return; }
        if (!had || n <= prev) { trickTurns[trick] = n; return; }
        trickTurns[trick] = n;
        string s; if (!TrickSfx.TryGetValue(trick, out s)) return;
        Play(s, 0.66f, 0.93f + Random.value * 0.16f);
    }

    /// the near miss; a shark dodged earns the lower one
    public static void Close(bool shark = false)
    {
        if (shark) Play("close", 0.95f, 0.94f);
        else Play("close", 0.85f, 0.98f + Random.value * 0.06f);
    }

    /// the jellyfish bounce, tighter when he came down on it from above
    public static void Jelly(bool over) { Play("jelly", over ? 0.80f : 0.62f, (over ? 1.04f : 0.94f) + Random.value * 0.07f); }

    /// every wipeout: the thing you hit (buoy, log, rock/debris have one), then the run ending
    /// a beat later. Pass the wipeout kind as the ride names it.
    public static void Hit(string kind)
    {
        string hit = kind == "buoy" ? "hitbuoy" : kind == "log" ? "hitlog" : (kind == "rock" || kind == "debris") ? "hitrock" : null;
        if (hit != null) Play(hit, 0.78f, 0.95f + Random.value * 0.10f);
        PlayLater("hitend", hit != null ? 0.60f : 0.80f, 0.96f + Random.value * 0.09f, hit != null ? 0.13f : 0.04f);
    }

    /// a gull: d is how far off the bird is, near the range it is audible over, beach true on
    /// the title screen (which leans on the real seagull take). Gapped at 2.6 s however many
    /// birds there are; returns whether it called.
    public static bool Gull(float d, float near, bool beach)
    {
        float now = Now;
        if (now - gullLast < GullGap) return false;
        gullLast = now;
        float w1 = beach ? 0.18f : 0.36f, w2 = beach ? 0.18f : 0.36f;
        float x = Random.value; int pick = x < w1 ? 1 : x < w1 + w2 ? 2 : 3;
        if (pick == gullPrev) pick = 1 + (pick % 3);
        gullPrev = pick;
        float close = 1f - Mathf.Min(1f, d / Mathf.Max(1f, near));
        Play("gull" + pick, 0.14f + close * 0.40f, 0.92f + Random.value * 0.17f);
        return true;
    }

    /// the palm's gust: one of four takes, never the same twice running
    public static void Palm()
    {
        int p = 1 + Random.Range(0, 4);
        if (p == palmPrev) p = 1 + (p % 4);
        palmPrev = p;
        Play("palm" + p, 0.34f + Random.value * 0.16f, 0.95f + Random.value * 0.10f);
    }

    /// the octopus letting one go
    public static void StarThrow() { Play("starthrow", 0.42f, 0.94f + Random.value * 0.14f); }
    /// a starfish arriving: stuck is the one that lands across the lens, else a glancing second
    public static void StarHit(bool stuck) { Play("starhit", stuck ? 0.82f : 0.52f, (stuck ? 0.97f : 1.06f) + Random.value * 0.08f); }
    /// one that went past into the water d feet away (only heard within 26)
    public static void StarMiss(float d) { if (d < 26f) Play("starhit", (1f - d / 26f) * 0.30f, 0.74f + Random.value * 0.08f); }

    /// shop: a purchase, and the equips (board, rider, a style item)
    public static void Buy() { Ui("buy", 0.60f); }
    public static void EquipBoard() { Ui("eqboard", 0.55f); }
    public static void EquipRider() { Ui("eqchar", 0.55f); }
    public static void EquipStyle() { Ui("eqstyle", 0.52f); }
    /// a pearl picked up on a dive: the buy chime, pitched up
    public static void Pearl() { Play("buy", 0.5f, 1.35f); }

    // =====================================================================================
    /// per frame, from SfxRunner: the ramps, the keep-alive, the delayed shots and the fades
    public static void Tick(float dt)
    {
        if (root == null) return;
        float now = Now;
        dt = Mathf.Max(0f, dt);
        lidHz += (lidWant - lidHz) * Ease(dt, 0.12f);
        dead.Clear();
        foreach (var kv in loops)
        {
            var v = kv.Value;
            if (v.src == null) { dead.Add(kv.Key); continue; }
            if (!v.stopping && now - v.seen > KeepAlive) { v.stopping = true; v.stopTau = 0.05f; v.stopAt = now + 0.20f; }   // the web's 150 ms stop
            float target = v.stopping ? 0f : v.wantGain;
            v.gain += (target - v.gain) * Ease(dt, v.stopping ? v.stopTau : 0.06f);
            v.pitch += (v.wantPitch - v.pitch) * Ease(dt, 0.10f);
            v.pan += (v.wantPan - v.pan) * Ease(dt, 0.09f);
            v.src.volume = Mathf.Clamp01(v.gain); v.src.pitch = v.pitch; v.src.panStereo = v.pan;
            if (v.lp != null) v.lp.cutoffFrequency = lidHz;
            if (v.stopping && now >= v.stopAt) dead.Add(kv.Key);
        }
        for (int i = 0; i < dead.Count; i++)
        {
            Voice v; if (!loops.TryGetValue(dead[i], out v)) continue;
            loops.Remove(dead[i]);
            if (v.src != null) { v.src.Stop(); Object.Destroy(v.src.gameObject); }
        }
        for (int i = later.Count - 1; i >= 0; i--)
        {
            if (now < later[i].at) continue;
            var l = later[i]; later.RemoveAt(i);
            Play(l.name, l.gain, l.pitch);
        }
        if (musFading && music != null)
        {
            float k = musMs <= 0f ? 1f : Mathf.Min(1f, (now - musT0) * 1000f / musMs);
            music.volume = Mathf.Clamp01(musFrom + (musTo - musFrom) * k);
            if (k >= 1f)
            {
                musFading = false;
                if (musTo <= 0f && music.isPlaying) { music.Pause(); musPaused = true; }
            }
        }
        if (cueFading && cue != null)
        {
            float k = Mathf.Min(1f, (now - cueT0) * 1000f / cueMs);
            cue.volume = Mathf.Max(0f, cueFrom * (1f - k));
            if (k >= 1f) { cueFading = false; cue.Stop(); }
        }
    }

    // setTargetAtTime's exponential approach, as a per-frame fraction
    static float Ease(float dt, float tau) { return 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, tau)); }
}
