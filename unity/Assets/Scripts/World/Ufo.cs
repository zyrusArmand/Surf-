// The night visitor, the web game's sequence number for number. It comes out at night and
// sunset is not night: the clock runs only while the sun is properly under (nightAmt >= 0.98)
// AND the moon is properly up (moonAmt >= 0.75), and resets the moment either slips, so dusk
// never counts toward it. Thirty seconds of that and it comes -- but never on top of the set
// wave: the clock runs, the moment it fires waits for the lane to be idle. Because the day is
// paced by distance, this effectively never happens by itself in an ordinary run (the night
// is ten kilometres in); the stage button (summon) is how it gets seen, pinning the sky to the
// middle of the night (1.35) first.
//
// It is heard before it is seen. 16.5 s before the saucer moves the sea starts going wrong
// underneath (the wind loop, swelling in over the whole approach); 10.5 s before, the cue
// starts -- a build whose peak lands where the hull breaks the surface, 2.2 s into RISE. A set
// wave starting inside the pre-roll postpones it, and a build that is not going to arrive is
// taken back down.
//
// Then: RISE, straight up the middle thirty feet ahead, out of the water over two seconds,
// dripping; BEAM, it settles 12.5 ft ahead and 10.4 ft over the sea while the light reaches
// down (0.85 s) and leans at him -- it finds him where he is; LIFT, 3.5 s: he is drawn in
// under it (1.4 s) and held a few feet off the deck, the board standing up on its tail beside
// him, while the saucer keeps station over him; LEAVE, the beam fades in 0.6 s and it climbs
// away over 3.2 s. The ride never stops: the board still steers underneath him the whole time,
// only the rider and his board are lifted to the eye.
//
// What it does to him: a swap, not a reward. He comes back down as Moo the Cow on Cattle
// Class with a bale of hay on the back -- BORROWED, for 35 s. While the borrow is on the save's
// loan fields carry the pair (wheelRider / wheelLoan) and the real rider and board stay in
// SaveData.S.rider / equipped, so nothing that happens in the window can write the costume to
// disk. It wears off on its own timer ("THE SPELL WEARS OFF"), or silently when the run ends.
//
// Public API (Bootstrap owns the wiring): Reset() at Play/Menu, Step(dt) in the running
// branch, RunEnd() when the card comes up (before the save is flushed), summon / arm for the
// stage, Duck for the riding bed, Holds for the set wave, onRebuild = BuildRider.
using System.Collections.Generic;
using UnityEngine;

public class Ufo : MonoBehaviour
{
    public enum Phase { Off = 0, Rise = 1, Beam = 2, Lift = 3, Leave = 4 }

    // ---- the web's numbers ----
    public const float NightAmt = 0.98f;   // the sun properly under
    public const float MoonUp = 0.75f;     // and the moon properly up
    public const float TestHour = 1.35f;   // where the stage button pins the sky: the middle of the night
    public const float Wait = 30f;         // seconds of moonlit dark before it comes
    public const float Scale = 9.0f;       // feet across
    public const float Hover = 10.4f;      // how high it stands off the water once it is over you
    public const float BeamW = 2.62f;      // the pool on the water, feet across its radius
    public const float Face = 0f;          // square to the lens, and it stays there
    public const float LiftH = 2.6f;       // feet he comes off the board
    public const float Reach = 0.85f;      // seconds for the light to travel down to him
    public const float Hold = 3.5f;        // seconds he hangs in it
    public const float Draw = 1.4f;        // seconds to draw him in under the middle of the beam
    public const float Pre = 10.5f;        // seconds of cue before the saucer moves
    public const float WindPre = 16.5f;    // and the wind before that
    public const float Keep = 35f;         // the borrow, in seconds
    public const float BoardLift = 2.30f, BoardSide = 0.90f, BoardSpin = 0.50f;
    public const string Char = "cow", Board = "cattleclass";

    public BoardController rider;
    public DayCycle day;
    public SetWave setWave;
    public Transform rig;                               // the board group under the player: board root + rider
    public System.Action<Vector3, int> onSplash;
    public System.Action onRebuild;                     // Bootstrap.BuildRider: the costume goes on and comes off through it
    public bool summon;                                 // debug: pin the night and bring it now (the web's stage button)
    public bool arm;                                    // debug: pin the night and run the countdown from the pre-roll

    public Phase ph = Phase.Off;
    public float t, dark, windT, hold;
    public bool pre, grab, loaned;
    public bool On => pre || ph != Phase.Off;           // the saucer is happening
    public bool Holds => ph != Phase.Off;               // the set wave waits for this
    public float Duck => On ? 0.34f : (loaned ? 0.68f : 1f);   // what the riding bed drops to
    public Transform Saucer => holder;

    Transform holder; float halfH, emitY, emitR; Transform beam; Material beamMat; Light lamp;
    string wasWheelRider, wasWheelLoan; bool cueLive;
    // the drops off the hull, pooled
    class Drip { public Transform m; public float vy, t; }
    readonly List<Drip> drips = new List<Drip>();
    readonly Stack<Transform> dripPool = new Stack<Transform>();
    Material dripMat; Mesh dripMesh;
    // the lift: where the ride parked him, and the drift toward the middle of the light
    bool lifted; Vector3 pugPos0; Quaternion pugRot0, pugQ0; float pugYaw0, ox, oz, spin;
    GameObject hay;

    void Awake()
    {
        // a save carrying the borrowed pair is a save written mid-abduction by an older build:
        // the web's loader scrubs it the same way, so nobody is stranded on a loan
        var S = SaveData.S;
        S.owned.Remove(Board); S.owned.Remove("haybale");
        if (S.equipped == Board || S.equipped == "haybale") S.equipped = "astro";
        if (S.wheelLoan == Board) S.wheelLoan = "";
        if (S.wheelRider == Char) S.wheelRider = "";
    }

    // =====================================================================================
    /// the web's reset(): no saucer left over the water, the countdown back to nought, and
    /// the costume handed back quietly -- a run starting or ending is one of the ways it ends
    public void Reset()
    {
        ResetSaucer();
        dark = 0; windT = 0;
        if (pre) { pre = false; StopCue(900); }
        GiveBack(true);
    }

    /// the run card: the costume comes off silently (the web does this as the wipeout is
    /// staged), so the save that follows never sees it. The saucer itself stays where it is.
    public void RunEnd() { GiveBack(true); }

    // the SAUCER's own state and nothing else: the costume has its own clock
    void ResetSaucer()
    {
        Drop();
        ph = Phase.Off; t = 0; grab = false;
        foreach (var d in drips) Pool(d.m);
        drips.Clear();
        if (holder != null) { holder.gameObject.SetActive(false); if (beamMat != null) beamMat.SetFloat("_Alpha", 0f); }
        if (lamp != null) lamp.intensity = 0f;
    }

    // =====================================================================================
    public void Step(float dt)
    {
        var r = rider; if (r == null) return;
        float eff = r.eff;
        // the wind runs on its own clock: it starts before the sequence has and carries across
        // the moment it does, swells all the way in, then gets out of the way once he is off
        // the board and the cue has paid off
        if (windT > 0f)
        {
            windT += dt;
            bool gone = ph == Phase.Lift || ph == Phase.Leave;
            if (gone && windT > WindPre + 9f) windT = 0f;
            else
            {
                float rise = Mathf.Min(1f, windT / WindPre);
                float duck = gone ? Mathf.Max(0f, 1f - (windT - (WindPre + 2f)) / 6f) : 1f;
                Sfx.UfoWind(rise, duck);
            }
        }
        // the costume: its own clock, and it stays strange until he is himself again
        if (loaned)
        {
            hold -= dt; if (hold <= 0f) GiveBack(false);
            Sfx.Weird();
            AttachHay();
        }
        // the cue climbs as the hull comes up, so the surface breaks on the loudest thing in the game
        if (pre) Sfx.CueVol(0.72f);
        else if (ph == Phase.Rise) Sfx.CueVol(0.72f + 0.28f * Mathf.Min(1f, t / 2.2f));
        else if (ph != Phase.Off) Sfx.CueVol(1.0f);

        if (ph == Phase.Off)
        {
            bool swIdle = setWave == null || setWave.ph == SetWave.Phase.Idle;
            if (summon) { summon = false; if (day != null) day.pin = TestHour; Begin(); }
            else if (arm) { arm = false; dark = Wait - (Pre + 0.5f); if (day != null) { day.pin = TestHour; day.Apply(r.dist); } }   // the sky is night NOW, so the clock below keeps what was just set
            // the dark has to be continuous, and the moon has to be up: dusk that slips back
            // does not count toward the thirty seconds
            if (ph == Phase.Off)
            {
                if (r.running && day != null && day.nightAmt >= NightAmt && day.moonAmt >= MoonUp)
                {
                    dark += dt;
                    if (windT <= 0f && dark >= Wait - WindPre && swIdle) windT = 1e-4f;
                    if (!pre && dark >= Wait - Pre && swIdle) { pre = true; Sfx.Cue("ufo", 0.72f); cueLive = true; }
                    // a wave starting inside the pre-roll postpones it: the build comes back down
                    if (pre && !swIdle) { pre = false; StopCue(900); }
                    if (windT > 0f && !swIdle) windT = 0f;
                    if (dark >= Wait && swIdle) Begin();
                }
                else { dark = 0f; windT = 0f; if (pre) { pre = false; StopCue(900); } }
            }
            if (ph == Phase.Off && drips.Count == 0) return;
        }
        if (holder == null) return;
        t += dt;
        Vector3 up = holder.position;
        float sea = WaveField.Height(up.x, WaveField.Scroll + up.z, r.t);
        if (ph == Phase.Rise)
        {
            // out of the water over two seconds, dripping the whole way and for a while after
            float k = Mathf.Min(1f, t / 2.0f), e = k * k * (3f - 2f * k);
            up.y = sea - halfH + (halfH * 2f + 7.5f) * e;
            up.z += eff * dt * 0.25f;                        // it drifts with the world, slower than the sea
            holder.position = up;
            if (t < 0.12f) onSplash?.Invoke(new Vector3(up.x, sea, up.z), 26);
            if (Random.value < dt * 26f && t < 4.5f)
                SpawnDrip(up.x + (Random.value - 0.5f) * Scale * 0.8f, up.y - halfH * 0.5f, up.z + (Random.value - 0.5f) * Scale * 0.8f);
            if (t >= 2.2f) { ph = Phase.Beam; t = 0f; }
        }
        else if (ph == Phase.Beam)
        {
            // it settles overhead but does not slide across to be exactly above him: the beam
            // leans out and finds him where he is, which is the half of this that reads as searching
            up.z += (r.pz - 12.5f - up.z) * Mathf.Min(1f, dt * 1.3f);
            up.y += ((sea + Hover) - up.y) * Mathf.Min(1f, dt * 1.6f);
            holder.position = up;
            if (Random.value < dt * 8f && t < 2.5f)
                SpawnDrip(up.x + (Random.value - 0.5f) * Scale * 0.7f, up.y - halfH * 0.5f, up.z + (Random.value - 0.5f) * Scale * 0.7f);
            float k = Mathf.Min(1f, t / 0.7f);
            float reach = Mathf.Min(1f, t / Reach);           // the far end travels down rather than arriving whole
            BeamTo(sea, k, reach, new Vector3(r.px, r.py + 0.2f, r.pz), true);
            // it only has him once the light has actually got down to him
            if (reach >= 1f && t > Reach + 0.35f) { ph = Phase.Lift; t = 0f; grab = true; }
        }
        else if (ph == Phase.Lift)
        {
            // he comes to the light rather than the light to him; the saucer keeps station over
            // him, slowly, which is not the same as chasing him -- the lean of the beam finds him
            up.z += (r.pz - 12.5f - up.z) * Mathf.Min(1f, dt * 1.4f);
            up.x += (r.px - up.x) * Mathf.Min(1f, dt * 0.8f);
            holder.position = up;
            Vector3 aim = RiderWorld(); aim.y += 0.2f;
            BeamTo(sea, 1f, 1f, aim, true);
            if (t >= Hold)
            {
                ph = Phase.Leave; t = 0f; grab = false;
                // and she comes back on a different board: the one she went up on went vertical
                // into the light with her. Both are borrowed, and nothing here saves.
                Borrow();
                onSplash?.Invoke(new Vector3(r.px, r.py, r.pz), 30);
                r.Say("ABDUCTED!");
            }
        }
        else if (ph == Phase.Leave)
        {
            up.y += dt * (6f + t * 14f);
            holder.position = up;
            BeamTo(sea, Mathf.Max(0f, 1f - t / 0.6f), 1f, Vector3.zero, false);
            if (t > 3.2f) ResetSaucer();
        }
        // the drops: a half-gravity fall, carried with the world, gone when they meet the sea
        for (int i = drips.Count - 1; i >= 0; i--)
        {
            var d = drips[i]; d.t += dt; d.vy -= 32.2f * dt * 0.5f;
            var p = d.m.position; p.y += d.vy * dt; p.z += eff * dt; d.m.position = p;
            float w = WaveField.Height(p.x, WaveField.Scroll + p.z, r.t);
            if (p.y <= w || d.t > 4f) { Pool(d.m); drips.RemoveAt(i); }
        }
    }

    // the cue belongs to the sequence: it runs while the sequence does and stops whenever it
    // does not (the web's 300 ms net), including a run that dies mid-abduction. ONCE per stop:
    // re-arming the fade every frame restarts it from the current volume and it never ends.
    void Update()
    {
        bool live = rider != null && rider.running && (loaned || pre || ph != Phase.Off);
        if (!live && cueLive) StopCue(600);
    }
    void StopCue(int ms) { if (cueLive) { Sfx.CueStop(ms); cueLive = false; } }

    void Begin()
    {
        if (!Build()) return;
        ResetSaucer();
        ph = Phase.Rise; t = 0f; summon = false;
        holder.gameObject.SetActive(true);
        // a pre-roll already running is arriving at its peak exactly now; without one (the
        // stage button skips the countdown) seek two seconds short of the peak
        if (!pre) { Sfx.Cue("ufo", 0.82f, Pre - 2.0f); cueLive = true; }
        pre = false;
        // up the middle, and facing you: it does not rotate at any point
        holder.position = new Vector3(rider.px, 0f, rider.pz - 30f);
        holder.rotation = Quaternion.Euler(0f, Face * Mathf.Rad2Deg, 0f);
    }

    // =====================================================================================
    // The beam is a cone whose LENGTH is however far it is from the hull to the water, worked
    // out every frame because both ends move. It aims at him and carries on to the water.
    void BeamTo(float sea, float k, float reach, Vector3 aimAt, bool aimed)
    {
        if (beam == null) return;
        Vector3 up = holder.position;
        float top = emitY;
        float full = Mathf.Max(0.5f, up.y + top - sea);
        Vector3 A = aimed ? new Vector3(aimAt.x - up.x, aimAt.y - (up.y + top), aimAt.z - up.z) : new Vector3(0f, -full, 0f);
        if (A.sqrMagnitude < 1e-6f) A = new Vector3(0f, -full, 0f);
        Vector3 D = A.normalized;
        float toSea = (up.y + top - sea) / Mathf.Max(0.15f, -D.y);
        float len = Mathf.Max(0.4f, toSea * Mathf.Max(0.02f, reach));
        beam.localRotation = Quaternion.FromToRotation(Vector3.down, D);
        beam.localScale = new Vector3(BeamW, len, BeamW);
        beam.localPosition = new Vector3(D.x * len * 0.5f, top + D.y * len * 0.5f, D.z * len * 0.5f);
        // a slow throb, so it reads as something running rather than a painted cone
        beamMat.SetFloat("_Alpha", k * (0.30f + 0.07f * Mathf.Sin(t * 7.0f)));
        if (lamp != null)
        {
            lamp.transform.localPosition = new Vector3(D.x * len * 0.4f, top + D.y * len * 0.4f, D.z * len * 0.4f);
            lamp.intensity = k * 7.0f;
        }
    }

    Vector3 RiderWorld()
    {
        var p = Pug();
        return p != null ? p.position : rider.transform.position;
    }

    // the rider is the child of the board group that is not the board
    Transform Pug()
    {
        if (rig == null) return null;
        for (int i = 0; i < rig.childCount; i++) { var c = rig.GetChild(i); if (!c.name.StartsWith("board:")) return c; }
        return null;
    }
    Transform BoardRoot()
    {
        if (rig == null) return null;
        for (int i = 0; i < rig.childCount; i++) { var c = rig.GetChild(i); if (c.name.StartsWith("board:")) return c; }
        return null;
    }

    // =====================================================================================
    // ---- the lift: both go up, and the board stands up on its tail ----
    // Last, after everything that stands him on the deck has had its say (the ride writes the
    // group's lean and the rig his stance every Update), otherwise he is put straight back on
    // the board the frame after the beam picks him up. Only ever put back what was moved.
    void LateUpdate()
    {
        var pug = Pug();
        if (!(grab && holder != null && holder.gameObject.activeSelf && pug != null && rider != null && !rider.wipe)) { Drop(); return; }
        float dt = Mathf.Min(0.033f, Time.deltaTime);
        if (!lifted)
        {
            lifted = true;
            pugPos0 = pug.localPosition; pugRot0 = pug.localRotation; pugQ0 = pug.rotation;
            pugYaw0 = pug.rotation.eulerAngles.y;
            ox = 0f; oz = 0f; spin = 0f;
        }
        float rise = Mathf.Min(1f, t / Draw), e = rise * rise * (3f - 2f * rise);
        // measured off the board AS THE RIDE LEFT IT, before this frame's lift moves it
        rig.localPosition = Vector3.zero;
        Vector3 rw = rig.position;
        float by = rig.TransformPoint(pugPos0).y;
        // ...and IN, to the middle of the light, carried across frames so he actually arrives
        float cx = rw.x + ox, cz = rw.z + oz;
        cx += (holder.position.x - cx) * Mathf.Min(1f, dt * 3.8f);
        cz += (holder.position.z - cz) * Mathf.Min(1f, dt * 3.8f);
        ox = cx - rw.x; oz = cz - rw.z;
        // the board: in, up, and over onto its tail -- a quarter turn the positive way about x
        // sends the nose (-z) to +y; the slow spin is the OUTER turn, about the world's up
        rig.position = new Vector3(cx + BoardSide * e, rw.y + e * BoardLift, cz);
        spin += dt * BoardSpin * e;
        float yaw = rig.rotation.eulerAngles.y;                 // as the ride left it this frame
        var stand = Quaternion.Euler(90f, yaw + spin * Mathf.Rad2Deg, 0f);
        rig.rotation = Quaternion.Slerp(rig.rotation, stand, e);
        // and the rider, in world terms, whatever the board did: a few feet off the board,
        // upright, keeping the way he was facing and turning slowly
        pug.position = new Vector3(cx, by + e * LiftH, cz);
        var want = Quaternion.Euler(0f, pugYaw0 + t * 1.1f * Mathf.Rad2Deg, 0f);
        pug.rotation = Quaternion.Slerp(want, pugQ0, 1f - e);  // e=0 is exactly the pose the ride had him in
    }

    void Drop()
    {
        if (!lifted) return;
        lifted = false;
        var pug = Pug();
        if (pug != null) { pug.localPosition = pugPos0; pug.localRotation = pugRot0; }
        if (rig != null) rig.localPosition = Vector3.zero;
    }

    // =====================================================================================
    // ---- the borrow: a cow on a saucer for thirty-five seconds, never saved ----
    void Borrow()
    {
        if (loaned || BoardCatalog.Get(Board).id != Board) return;
        var S = SaveData.S;
        wasWheelRider = S.wheelRider; wasWheelLoan = S.wheelLoan;
        S.wheelRider = Char; S.wheelLoan = Board;
        loaned = true; hold = Keep;
        Drop();
        onRebuild?.Invoke();
        AttachHay();
    }

    /// quiet when the run is ending anyway; otherwise it says so, because a character changing
    /// under you with no explanation is a bug
    public void GiveBack(bool quiet)
    {
        if (!loaned) return;
        var S = SaveData.S;
        S.wheelRider = wasWheelRider ?? ""; S.wheelLoan = wasWheelLoan ?? "";
        loaned = false; hold = 0f;
        StopCue(700);                                 // he is back on his own board: done
        Drop();
        onRebuild?.Invoke();
        if (!quiet && rider != null) rider.Say("THE SPELL WEARS OFF");
    }

    // a bale on the back of it, under the board root so the ordinary board coming back takes
    // the hay with it and there is no flag to get out of step
    void AttachHay()
    {
        var root = BoardRoot();
        if (root == null) return;
        if (hay != null && hay.transform.parent == root) return;
        if (hay != null) Destroy(hay);
        hay = HayStack();
        hay.transform.SetParent(root, false);
        hay.transform.localPosition = new Vector3(0f, 0.255f, 1.15f);   // behind her, on the deck
    }

    static float Hash(float x, float y) { float n = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return n - Mathf.Floor(n); }

    static Material Lit(uint hex)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.SetColor("_BaseColor", Col(hex)); m.SetFloat("_Smoothness", 0.05f);
        return m;
    }
    static Color Col(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

    static GameObject Box(Transform parent, float w, float h, float d, Material m)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var col = b.GetComponent<Collider>(); if (col) Destroy(col);
        b.transform.SetParent(parent, false); b.transform.localScale = new Vector3(w, h, d);
        b.GetComponent<Renderer>().sharedMaterial = m;
        return b;
    }

    // a bale is a block with two strings round it, and the strings are the whole of why it
    // reads as a bale rather than as a yellow box
    static GameObject HayStack()
    {
        var g = new GameObject("hay");
        var A = Lit(0xd8ab4e); var B = Lit(0xbf8f38); var TW = Lit(0x4a3116);
        GameObject Bale(float w, float h, float d, Material mat)
        {
            var b = new GameObject("bale"); b.transform.SetParent(g.transform, false);
            Box(b.transform, w, h, d, mat);
            foreach (var z in new[] { -d * 0.27f, d * 0.27f })
            {
                var st = Box(b.transform, w * 1.03f, h * 0.10f, d * 0.055f, TW);
                st.transform.localPosition = new Vector3(0f, 0f, z);
            }
            return b;
        }
        var b1 = Bale(0.60f, 0.29f, 0.40f, A); b1.transform.localPosition = new Vector3(-0.15f, 0.145f, 0.00f);
        var b2 = Bale(0.60f, 0.29f, 0.40f, B); b2.transform.localPosition = new Vector3(0.16f, 0.145f, 0.05f); b2.transform.localRotation = Quaternion.Euler(0f, 0.26f * Mathf.Rad2Deg, 0f);
        var b3 = Bale(0.54f, 0.26f, 0.36f, A); b3.transform.localPosition = new Vector3(0.01f, 0.420f, 0.02f); b3.transform.localRotation = Quaternion.Euler(0f, -0.40f * Mathf.Rad2Deg, 0f);
        // a few stalks off the top, hashed so the bale is the same bale every time
        for (int i = 0; i < 10; i++)
        {
            float h1 = Hash(i * 3.1f, 7.7f), h2 = Hash(i * 1.9f, 2.3f), h3 = Hash(i * 5.3f, 4.1f);
            var st = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var col = st.GetComponent<Collider>(); if (col) Destroy(col);
            st.transform.SetParent(g.transform, false);
            float len = 0.18f + h1 * 0.13f;
            st.transform.localScale = new Vector3(0.010f, len * 0.5f, 0.010f);   // the primitive is 2 tall
            st.transform.localPosition = new Vector3((h2 - 0.5f) * 0.52f, 0.56f + h3 * 0.05f, (h1 - 0.5f) * 0.30f);
            st.transform.localRotation = Quaternion.Euler((h3 - 0.5f) * 1.3f * Mathf.Rad2Deg, h2 * 3.0f * Mathf.Rad2Deg, (h2 - 0.5f) * 1.3f * Mathf.Rad2Deg);
            st.GetComponent<Renderer>().sharedMaterial = A;
        }
        return g;
    }

    // =====================================================================================
    // ---- the saucer itself ----
    bool Build()
    {
        if (holder != null) return true;
        holder = new GameObject("Ufo").transform;
        GameObject s3;
        var prefab = Resources.Load<GameObject>("Models/ufo");
        if (prefab != null)
        {
            s3 = Models.Spawn("ufo", holder, 3f, PrimitiveType.Sphere);
            // centred on itself and scaled to a size in FEET: Scale across the wider of x and z
            var b = Models.Bounds(s3);
            float k = Scale / Mathf.Max(1e-4f, Mathf.Max(b.size.x, b.size.z));
            s3.transform.localScale *= k;
            b = Models.Bounds(s3);
            s3.transform.localPosition -= b.center;
            b = Models.Bounds(s3);
            halfH = b.size.y * 0.5f;
            MeasureEmitter(s3, b);
        }
        else
        {
            s3 = BuildSaucer();
            s3.transform.SetParent(holder, false);
            halfH = 1.35f; emitY = -0.95f; emitR = 0.58f * BeamW;
        }
        // the beam: a truncated cone standing on its emitter under the hull, opening downward,
        // leaving the hull at the size of the thing it leaves (the housing over the pool)
        var bg = new GameObject("beam"); beam = bg.transform; beam.SetParent(holder, false);
        var mf = bg.AddComponent<MeshFilter>(); var mr = bg.AddComponent<MeshRenderer>();
        mf.sharedMesh = Cone(Mathf.Min(0.95f, emitR / BeamW), 1f, 30);
        var bs = Shader.Find("Surf/Beam");
        beamMat = new Material(bs != null ? bs : Shader.Find("Universal Render Pipeline/Unlit"));
        beamMat.SetColor("_Color", Col(0x7dff9a)); beamMat.SetFloat("_Alpha", 0f);
        beamMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); beamMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        beamMat.renderQueue = 3100;
        mr.sharedMaterial = beamMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        // no pool on the water: the beam ends where it ends
        var lg = new GameObject("light"); lg.transform.SetParent(holder, false);
        lamp = lg.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = Col(0x8dffa8); lamp.range = 90f; lamp.intensity = 0f;
        holder.gameObject.SetActive(false);
        return true;
    }

    // Where the light actually comes out, measured off the model: the lowest point, then a
    // slice scan up from it -- the housing's radius grows gently and then the hull FLARES, so
    // walk up while each slice is within half again of the last and stop at the step. The
    // emitter is the FACE at the bottom of that (the widest reach in its lowest third), and the
    // cone starts a third of the way up the housing so there is no seam to find.
    void MeasureEmitter(GameObject s3, Bounds b)
    {
        float lo = float.PositiveInfinity; const int N = 96;
        float hgt = Mathf.Max(1e-4f, b.size.y);
        var rad = new float[N];
        var pts = new List<Vector3>();
        try
        {
            foreach (var f in s3.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) Gather(f.sharedMesh, f.transform, pts);
            foreach (var s in s3.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh != null) Gather(s.sharedMesh, s.transform, pts);
        }
        catch { pts.Clear(); }
        if (pts.Count == 0)
        {
            // the mesh is not readable here: the old eyeballed numbers
            emitY = -halfH * 0.92f; emitR = 0.58f * BeamW; return;
        }
        foreach (var w in pts) if (w.y < lo) lo = w.y;
        foreach (var w in pts)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((w.y - lo) / hgt * N), 0, N - 1);
            float r = Mathf.Sqrt(w.x * w.x + w.z * w.z); if (r > rad[i]) rad[i] = r;
        }
        float e = 0f; bool seen = false; int fi = 0;
        for (int i = 0; i < N; i++)
        {
            if (rad[i] <= 0f) continue;                  // nothing in this slice
            if (seen && rad[i] > e * 1.5f) break;        // the hull, flaring out
            if (rad[i] > e) e = rad[i];
            seen = true; fi = i;
        }
        float mouth = 0f;
        for (int i = 0; i <= Mathf.Max(0, Mathf.FloorToInt(fi * 0.34f)); i++) if (rad[i] > mouth) mouth = rad[i];
        emitY = lo + (fi / (float)N) * hgt * 0.34f;
        emitR = Mathf.Max(0.05f, mouth > 0f ? mouth : e);
    }

    void Gather(Mesh m, Transform tr, List<Vector3> into)
    {
        if (!m.isReadable) return;
        var v = m.vertices;
        for (int i = 0; i < v.Length; i++) into.Add(holder.InverseTransformPoint(tr.TransformPoint(v[i])));
    }

    // a unit truncated cone, centred on itself: y -0.5 at the wide end (radius 1) to +0.5 at
    // the emitter (radius rTop), open at both ends; drawn double sided by the material
    static Mesh Cone(float rTop, float rBot, int segs)
    {
        var m = new Mesh { name = "beam" };
        var v = new Vector3[(segs + 1) * 2]; var uv = new Vector2[v.Length];
        for (int i = 0; i <= segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
            v[i] = new Vector3(c * rTop, 0.5f, s * rTop); uv[i] = new Vector2(i / (float)segs, 1f);
            v[segs + 1 + i] = new Vector3(c * rBot, -0.5f, s * rBot); uv[segs + 1 + i] = new Vector2(i / (float)segs, 0f);
        }
        var idx = new int[segs * 6]; int n = 0;
        for (int i = 0; i < segs; i++)
        {
            int a = i, b = i + 1, c = segs + 1 + i, d = segs + 2 + i;
            idx[n++] = a; idx[n++] = c; idx[n++] = b; idx[n++] = b; idx[n++] = c; idx[n++] = d;
        }
        m.vertices = v; m.uv = uv; m.triangles = idx; m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // The stand-in when ufo.glb is missing: a lens of two shallow domes, a canopy on top, a
    // housing underneath and a ring of lights round the rim, nine feet across.
    static GameObject BuildSaucer()
    {
        var g = new GameObject("ufo (stand-in)");
        var hull = Lit(0xb8bec8); var dark = Lit(0x3a3f48);
        var glass = Lit(0x8fd8ff);
        GameObject Ball(string nm, Vector3 pos, Vector3 scl, Material m)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Sphere); o.name = nm;
            var col = o.GetComponent<Collider>(); if (col) Destroy(col);
            o.transform.SetParent(g.transform, false); o.transform.localPosition = pos; o.transform.localScale = scl;
            o.GetComponent<Renderer>().sharedMaterial = m; return o;
        }
        Ball("top", new Vector3(0, 0.15f, 0), new Vector3(9f, 1.3f, 9f), hull);
        Ball("under", new Vector3(0, -0.15f, 0), new Vector3(8.6f, 1.6f, 8.6f), dark);
        Ball("dome", new Vector3(0, 0.75f, 0), new Vector3(3.2f, 1.6f, 3.2f), glass);
        Ball("housing", new Vector3(0, -0.85f, 0), new Vector3(2.4f, 0.6f, 2.4f), dark);
        var lamp = Lit(0x7dff9a); lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", Col(0x7dff9a) * 2.5f);
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            Ball("lamp", new Vector3(Mathf.Cos(a) * 3.9f, -0.05f, Mathf.Sin(a) * 3.9f), Vector3.one * 0.32f, lamp);
        }
        return g;
    }

    // ---- the drops: a handful off the hull for the first few seconds out of the sea, pooled ----
    void SpawnDrip(float x, float y, float z)
    {
        if (drips.Count > 90) return;
        if (dripMat == null)
        {
            var bs = Shader.Find("Surf/Beam");
            dripMat = new Material(bs != null ? bs : Shader.Find("Universal Render Pipeline/Unlit"));
            dripMat.SetColor("_Color", Col(0xcfefff)); dripMat.SetFloat("_Alpha", 0.75f);
            dripMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); dripMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dripMat.renderQueue = 3050;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); dripMesh = tmp.GetComponent<MeshFilter>().sharedMesh; Destroy(tmp);
        }
        Transform m;
        if (dripPool.Count > 0) { m = dripPool.Pop(); m.gameObject.SetActive(true); }
        else
        {
            var go = new GameObject("drip"); go.AddComponent<MeshFilter>().sharedMesh = dripMesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = dripMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            m = go.transform;
        }
        m.localScale = new Vector3(0.18f, 0.18f * 2.2f, 0.18f);     // a falling drop is not a ball (the primitive is 1 across)
        m.position = new Vector3(x, y, z);
        drips.Add(new Drip { m = m, vy = -1.2f - Random.value * 1.4f, t = 0f });
    }
    void Pool(Transform m) { if (m == null) return; m.gameObject.SetActive(false); dripPool.Push(m); }
}
