// What is in the water: buoys, logs, ramps, jellyfish, cast by distance and spaced by the gap
// on the water rather than by the clock, the way the web game spaces them.
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public class Ob
    {
        public string kind; public GameObject go; public float x, z, y, floatOff, hx, hz, h;
        public bool ramp, used, passed, escape; public float rx, rz, rampSink, rampLift = 1f;
        // knocked: shoved aside, lifted and set spinning by the thing that hit it
        public bool knocked; public float kvx, kvy, kvz, ksx, ksz, ky, kax, kaz;
        // the shark
        public float dir, startX, targetX, at, x0, z0, aimX, aimZ, yaw; public bool hunts; public string atk;
        // its jaw (a bone in the model), how far the mouth sits under the holder, and the bite clock
        public float lx = float.NaN;     // where it was across the lane last frame, for the swept contact test
        public Transform jaw; public Quaternion jawRest; public float jawAng = float.NaN, jawT, bite = -1f, lift = 1.2f;
    }
    public Ob eater;                 // the shark that has him, while the chew lasts
    float bigFinCd; int octCd;
    public System.Action<Color> onSplat;      // a starfish in the face

    // the octopus's starfish, lobbed at where he will be
    class Star { public GameObject go; public float vx, vy, vz, sx, sy, t; }
    readonly List<Star> stars = new List<Star>();
    static Mesh starMesh; static Material starMat;

    public BoardController rider;
    public System.Action<Vector3, int> onSplash;
    public readonly List<Ob> obstacles = new List<Ob>();

    // ---- seated on the mirror, for the sea's reflection only (PlanarReflection) ----
    // Each floating thing is moved so its waterline (the swell it rides, where its holder's height
    // is measured from) sits on the reflection's plane, and put back the moment that render is done.
    readonly List<(Transform t, float dy)> reflShift = new List<(Transform, float)>();
    public void SeatForReflection(float planeY)
    {
        reflShift.Clear();
        if (rider == null) return;
        float fz0 = WaveField.Scroll;
        foreach (var o in obstacles)
        {
            if (o.go == null || !o.go.activeInHierarchy || o.knocked) continue;
            float dy = planeY - WaveField.Swell(o.x, fz0 + o.z, rider.t);
            o.go.transform.position += new Vector3(0f, dy, 0f);
            reflShift.Add((o.go.transform, dy));
        }
    }
    public void UnseatAfterReflection()
    {
        foreach (var s in reflShift) if (s.t != null) s.t.position -= new Vector3(0f, s.dy, 0f);
        reflShift.Clear();
    }
    float spawnT = 1.8f; int laneSlot; string lastKind = ""; int sameRun;

    static readonly Dictionary<string, float> FloatOff = new Dictionary<string, float> {
        {"buoy",0.36f},{"jelly",0.34f},{"log",0.40f},{"ramp",0.02f},{"bigfin",0.28f},{"octopus",-0.10f},{"rock",0.0f} };

    // ---- the set pieces stand the rack down ----
    // The fork casts its own run down the channel he took and the chute its own rack, so the
    // ordinary spawner holds for the length of either (web 46175, 46182). holdCast is written
    // each frame by Island/Chute; chuteW is the chute's weight 0..1, which swaps the rack for
    // rock, timber and the odd slab and tightens the gap by 30% (web 46192, 46213-46214).
    // the ramp, as the web builds it and sinks it (22780, 23179-23187)
    const float RampL = 2.55f, RampH = 2.15f, RampSink = 0.26f, RampLaunchDrop = 0.35f;
    static readonly float[] RampTiers = { 0f, 0.42f, 0.74f, 1f };
    public bool holdCast;
    public float chuteW;
    const float ShkLead = 1.15f, ShkArc = 1.30f, ShkApex = 5.4f, ShkDodge = 3.2f, ShkOver = 3.4f, ShkReach = 7.5f, ShkChance = 0.66f, ShkPast = 2.2f;
    public const float ShkChew = 1.75f;

    // ---- the banner plane: a tow passes low over the lane every forty to eighty seconds ----
    public SetWave setWave; public Island island;
    Transform tow; bool towOn; float towT = 34f, towSpin;
    // The web fits its model into the box of the plane it builds from primitives, by the longest
    // side (PLANE_BOX 22460-22464, fitToBox 5661), and flies it at scale 1 ("the banner tow passes
    // close; it needs no help", 46662): 6.5 ft across the wings. At 22 ft it was over three times
    // that, its floats hung down to seven and a half feet, and at the same 11 ft it read as a
    // plane skimming the water at head height rather than one passing well over him.
    const float TowSpan = 6.5f;
    // ---- and its rope (web 22479-22481, 27036-27049, 45353-45358, 46676-46686) ----
    // While the plane is between 90 and 4 ft ahead the ROPE! button is up. Take it and he is
    // HOOKED: towed up under the plane (BoardController owns that, rider.hanging) until he lets
    // go, which is a Superman backflip off the line, or the plane passes 2 ft behind him and he
    // drops off. The rope is a plain white line from just over his head to just under the plane.
    const float RopeFar = -90f, RopeNear = -4f, RopeDrop = 2f;
    LineRenderer towRope;

    void TowPlane(float dt, float eff)
    {
        var r = rider;
        if (r.onRope == null) r.onRope = RopeAction;
        bool setIdle = setWave == null || setWave.ph == SetWave.Phase.Idle;
        if (island != null && island.On) towT = Mathf.Max(towT, 2.0f);   // no banner tow over a beach
        if (setIdle) towT -= dt;
        // the set wave takes the sky, and whatever tow was out goes with it (web 32301-32302)
        if (!setIdle && towOn) { towOn = false; tow.gameObject.SetActive(false); if (r.hanging) r.ReleaseRope(false); }
        if (r.running && setIdle && towT <= 0f && !towOn)
        {
            if (tow == null)
            {
                // The file lays the seaplane's fuselage along x (nose at -x in the file, so +x once
                // the importer has mirrored it; the tall tail fin at the other end) and its wings
                // along z. Sized by its longest side, the wingspan; then turned a quarter so the nose
                // points down the lane at the lens, the way it flies in (web 22465), and re-centred.
                var holder = new GameObject("BannerPlane");
                var go = Models.Spawn("plane", holder.transform, 2f, PrimitiveType.Cube);
                PlaneProps.Attach(go);                                           // its two props turn (web 46669)
                Models.FitLength(go, TowSpan);
                if (!go.name.EndsWith("(stand-in)"))
                {
                    go.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * go.transform.localRotation;
                    var pb = Models.Bounds(go); go.transform.position -= pb.center - holder.transform.position;
                }
                tow = holder.transform;
            }
            towOn = true; tow.gameObject.SetActive(true);
            tow.position = new Vector3(Random.value * 16f - 8f, 11f, -190f);
            towT = 40f + Random.value * 38f;
        }
        // It flies on through a wipeout as well: the world keeps running under the crash, and a
        // plane frozen in the sky over the tumble was the one thing that stopped.
        if (towOn && setIdle)
        {
            towSpin += dt;
            tow.position = new Vector3(tow.position.x, 11f + Mathf.Sin(towSpin * 1.5f) * 0.6f, tow.position.z + (eff * 0.45f + 12f) * dt);
            // nose toward the rider, flying in: a slow bank one way and the other, and the nose
            // riding up and down a touch with the bob, so it reads as flying rather than hung there
            tow.rotation = Quaternion.Euler(Mathf.Cos(towSpin * 1.5f) * 2.5f, Mathf.Sin(towSpin * 0.4f) * 3f, Mathf.Sin(towSpin * 0.7f) * 6f);
            if (tow.position.z > 40f) { towOn = false; tow.gameObject.SetActive(false); }
        }
        // the offer is a window, not an event: in reach, the button is up (web 46676-46677)
        r.ropeOffer = towOn && r.running && !r.wipe && tow.position.z > RopeFar && tow.position.z < RopeNear;
        if (r.hanging)
        {
            if (r.isleOn || r.diveOn || r.glideOn) r.hanging = false;         // something else has him now: the line is simply gone
            else if (!towOn || tow.position.z > RopeDrop) r.ReleaseRope(false);    // it has gone over him (web 45358, 46674)
            else r.towX = tow.position.x;                                     // the rope pulls him in under it
        }
        TowRope();
    }

    /// a revive puts the tow away and the next one 30-54 s out (web respawn 27186)
    public void ClearTow()
    {
        towOn = false; if (tow != null) tow.gameObject.SetActive(false);
        towT = 30f + Random.value * 24f; TowRope();
    }

    // web ropeAction (27036-27044): inside the window it hooks him; on the line it lets him go
    void RopeAction()
    {
        var r = rider;
        if (r == null || !r.running) return;
        if (r.hanging) { r.ReleaseRope(true); return; }
        if (r.ropeOffer && towOn) { r.towX = tow.position.x; r.Hook(); TowRope(); }
    }

    // the line, from just over his head to just under the plane (web 46681-46686)
    void TowRope()
    {
        var r = rider;
        bool on = r.hanging && towOn && tow != null;
        if (towRope == null)
        {
            if (!on) return;
            var go = new GameObject("TowRope");
            go.transform.SetParent(transform, false);
            towRope = go.AddComponent<LineRenderer>();
            towRope.useWorldSpace = true; towRope.positionCount = 2;
            towRope.widthMultiplier = 0.045f; towRope.numCapVertices = 0;
            towRope.sharedMaterial = Dive.Unlit(Color.white, false);
            towRope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; towRope.receiveShadows = false;
        }
        if (towRope.gameObject.activeSelf != on) towRope.gameObject.SetActive(on);
        if (!on) return;
        towRope.SetPosition(0, new Vector3(r.px, r.py + 1.2f, r.pz - 0.5f));
        towRope.SetPosition(1, tow.position + new Vector3(0f, -0.6f, 0f));
    }

    void Knock(Ob o, BoardController r, float side, float sideR, float up, float upR, float back, float backK, float spin)
    {
        if (o.knocked) return;
        o.knocked = true;
        o.kvx = (o.x - r.px) * side + (Random.value - 0.5f) * sideR; o.kvy = up + Random.value * upR; o.kvz = back + r.eff * backK;
        o.ksx = (Random.value - 0.5f) * spin; o.ksz = (Random.value - 0.5f) * spin;
        // from here it is a real body in the water: thrown with the web's numbers, then left to
        // gravity, the swell under it, the other things it hits and the board
        if (o.go != null)
        {
            float mass = o.kind == "log" ? 4f : o.kind == "ramp" ? 5f : o.kind == "jelly" ? 0.6f : 3f;
            Buoyancy.Make(o.go, r, new Vector3(o.kvx, o.kvy, r.eff + o.kvz), new Vector3(o.ksx, 0f, o.ksz), mass);
        }
    }

    public void Clear()
    {
        Hull.Reset();
        foreach (var o in obstacles) if (o.go) Destroy(o.go);
        // a run's start as the web's (27117): a little open water first, a fresh streak, the fin held
        // off six seconds, a random lane
        obstacles.Clear(); spawnT = 2.0f; eater = null; bigFinCd = 6f; octCd = 0; holdCast = false; chuteW = 0;
        lastKind = ""; sameRun = 0; laneSlot = Random.Range(0, 5);
        // the first tow comes 34 to 60 s into a run (web reset 27114)
        towOn = false; towT = 34f + Random.value * 26f; if (tow != null) tow.gameObject.SetActive(false);
        if (towRope != null) towRope.gameObject.SetActive(false);
        foreach (var st in stars) if (st.go) Destroy(st.go);
        stars.Clear();
    }

    public void Remove(int i) { if (obstacles[i].go) Destroy(obstacles[i].go); obstacles.RemoveAt(i); }

    /// everything on the water goes, and nothing else changes (web fallGo: whatever was already
    /// in the lane when the drop was armed would otherwise ride into it)
    public void ClearObstacles()
    {
        foreach (var o in obstacles) if (o.go) Destroy(o.go);
        obstacles.Clear(); eater = null;
    }

    public void Step(float dt)
    {
        var r = rider; float eff = r.eff;
        spawnT -= dt;
        float distM = r.dist;
        TowPlane(dt, eff);                 // it only sets off while he is riding; one already out flies on through a crash
        if (setWave != null && setWave.ph != SetWave.Phase.Idle) spawnT = Mathf.Max(spawnT, 0.6f);   // nothing new is cast while the set wave is up (web 46170)
        if (holdCast) spawnT = Mathf.Max(spawnT, 0.5f);
        if (r.running && spawnT <= 0f && !holdCast)
        {
            float wantGap = Mathf.Max(15f, 24f - distM / 300f) * (1f - 0.30f * chuteW);
            spawnT = Mathf.Max(0.40f, wantGap / Mathf.Max(16f, eff)) * (0.75f + Random.value * 0.5f);
            // buoys and logs are the whole game to begin with; ramps, then jellyfish
            var weights = new List<(string, float)>();
            if (chuteW > 0.5f)
            {
                // the chute casts its own: nothing that floats, only what a river puts in there
                weights.Add(("rock", 34)); weights.Add(("log", 28)); weights.Add(("ramp", 22));
            }
            else
            {
                weights.Add(("buoy", 22)); weights.Add(("log", 17));
                if (distM >= 150) weights.Add(("ramp", 16));
                if (distM >= 340) weights.Add(("jelly", 20));
                if (distM >= 560) weights.Add(("bigfin", 11));
                if (distM >= 780) weights.Add(("octopus", Mathf.Min(30f, 4f + Mathf.Max(0f, (distM - 780f) / 28f))));
            }
            // the web's pick exactly (46199-46213): eight tries, each rejected if it would make a third
            // in a row, a fin inside its cooldown or a thrower inside its; all eight rejected is a buoy
            string pick = null;
            for (int tries = 0; tries < 8; tries++)
            {
                float total = 0; foreach (var w in weights) total += w.Item2;
                float roll = Random.value * total; string cand = weights[0].Item1;
                foreach (var w in weights) { roll -= w.Item2; if (roll <= 0) { cand = w.Item1; break; } }
                if (cand == lastKind && sameRun >= 2) continue;
                if (cand == "bigfin" && bigFinCd > 0f) continue;
                if (cand == "octopus" && octCd > 0) continue;
                pick = cand; break;
            }
            if (pick == null) pick = "buoy";
            sameRun = pick == lastKind ? sameRun + 1 : 1; lastKind = pick;
            if (pick == "bigfin") bigFinCd = 9f;
            octCd = Mathf.Max(0, octCd - 1); if (pick == "octopus") octCd = 3;
            laneSlot = (laneSlot + 1 + Random.Range(0, 3)) % 7;
            float laneX = (-3 + laneSlot) * 4.9f + (Random.value - 0.5f) * 3.0f;
            float lim = Mathf.Min(17f, r.laneLim);
            Make(pick, Mathf.Clamp(r.px * 0.55f + laneX, -lim, lim), -170f);
            if (distM > 600 && Random.value < 0.30f)
            {
                float edge = (Random.value < 0.5f ? -1 : 1) * (11.5f + Random.value * 4.5f);
                Make(Random.value < 0.5f ? "buoy" : "log", edge, -176f);
            }
        }
        bigFinCd = Mathf.Max(0f, bigFinCd - dt);
        float fzBase = r.dist * WaveField.Drift;
        Stars(dt, eff, fzBase);
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            o.z += eff * dt;
            // the set wave runs over everything already on the water rather than it vanishing:
            // pushed past him and culled by the usual rule (web 46526)
            if (setWave != null && setWave.Sweeping && o.kind != "debris") o.z += 42f * dt;
            if (o.kind == "bigfin" && Shark(o, dt, eff, fzBase)) continue;
            if (o.kind == "octopus") Octopus(o, dt);
            if (o.z > 30f) { Destroy(o.go); obstacles.RemoveAt(i); continue; }
            float oy = WaveField.Swell(o.x, fzBase + o.z, r.t) + o.floatOff;
            if (o.knocked && o.go != null && o.go.GetComponent<Rigidbody>() != null)
            {
                // physics has it now: just keep the record where the body actually is
                var bp = o.go.transform.position; o.x = bp.x; o.y = bp.y; o.z = bp.z; o.used = true;
                if (o.z > 30f || o.y < -60f) { Destroy(o.go); obstacles.RemoveAt(i); }
                continue;
            }
            if (o.knocked)
            {
                // it takes the impact too: flung off, falling back, spinning as it goes
                o.kvy -= 14f * dt; o.x += o.kvx * dt; o.z += o.kvz * dt; o.ky += o.kvy * dt;
                o.kax += o.ksx * dt; o.kaz += o.ksz * dt;
                if (o.ky < 0f && o.kvy < 0f) { o.ky = 0f; o.kvy = 0f; o.kvx *= 0.6f; o.kvz *= 0.6f; o.ksx *= 0.5f; o.ksz *= 0.5f; }
                o.y = oy + o.ky; o.used = true;
                o.go.transform.position = new Vector3(o.x, o.y, o.z);
                o.go.transform.rotation = Quaternion.Euler((o.rx + o.kax) * Mathf.Rad2Deg, 0, (o.rz + o.kaz) * Mathf.Rad2Deg);
                continue;
            }
            float tilt = o.ramp ? 0.06f : 0.30f, span = o.ramp ? 3.2f : 1.6f;
            float slopeZ = (WaveField.Swell(o.x, fzBase + o.z + span, r.t) - WaveField.Swell(o.x, fzBase + o.z - span, r.t)) * tilt;
            float slopeX = (WaveField.Swell(o.x + span, fzBase + o.z, r.t) - WaveField.Swell(o.x - span, fzBase + o.z, r.t)) * tilt;
            float f = Mathf.Min(1f, dt * (o.ramp ? 2f : 6f));
            if (o.kind == "rock")
            {
                // A rock is lodged in the bed, not floating: it sits ON the chute's water wherever that
                // is. Eased like a float it lagged the fall line by feet -- the water at its z rises at
                // 0.47 x his speed as it comes up the drop toward him, and a nine-a-second follow left
                // the whole cluster under the surface. And it lies along the slope: the float tilt
                // above leans the +z end DOWN when the water there is higher (positive Euler x tips
                // +z down), which on a 0.47 gradient stood it fifty degrees off the surface.
                o.y = oy;
                float gz = slopeZ / (tilt * 2f * span), gx = slopeX / (tilt * 2f * span);   // the true rise over run
                o.rx += (-Mathf.Atan(gz) - o.rx) * f; o.rz += (Mathf.Atan(gx) - o.rz) * f;
            }
            else
            {
                o.y += (oy - o.y) * Mathf.Min(1f, dt * (o.ramp ? 3f : 9f));
                o.rx += (slopeZ - o.rx) * f; o.rz += (-slopeX - o.rz) * f;
            }
            o.go.transform.position = new Vector3(o.x, o.y, o.z);
            var tiltQ = Quaternion.Euler(o.rx * Mathf.Rad2Deg, 0, o.rz * Mathf.Rad2Deg + (o.kind == "buoy" ? Mathf.Sin(r.t * 2f + o.x) * 3.4f : 0));
            // the shark keeps its heading under the swell's tilt (web: rotation.y is its own, only x
            // and z come from the water); writing yaw 0 here every frame threw it away, and it slid
            // across the lane side-on and never turned to come at him
            o.go.transform.rotation = o.kind == "bigfin" ? Quaternion.Euler(0f, SharkYaw(o, o.yaw), 0f) * tiltQ : tiltQ;
            if (!r.running || r.Submerged) continue;        // under the water nothing floating on it can reach him

            // the footprint on the water, then the height test
            // the footprint shrinks with him under a coconut charge
            float halfL = BoardController.BoardHalfL * Coconuts.SizeK, halfW = BoardController.BoardHalfW * Coconuts.SizeK;
            bool contact;
            if (o.ramp) contact = Mathf.Abs(r.px - o.x) < o.hx + halfW && Mathf.Abs(r.pz - o.z) < o.hz + halfL;
            else
            {
                // the web's test (46547-46558): the thing is an ellipse, not a box, so its corners do
                // not reach out and take you; and it is swept over where it was a frame ago, across
                // the lane and along it, so a fast pass cannot step through it between two frames
                float RX = o.hx + halfW, RZ = o.hz;
                float ox0 = float.IsNaN(o.lx) ? o.x : o.lx;
                float xlo = Mathf.Min(ox0, o.x), xhi = Mathf.Max(ox0, o.x);
                float dxo = r.px < xlo ? xlo - r.px : (r.px > xhi ? r.px - xhi : 0f);
                contact = false;
                if (dxo < RX)
                {
                    float reach = RZ * Mathf.Sqrt(Mathf.Max(0f, 1f - (dxo / RX) * (dxo / RX)));
                    float z1 = o.z, z0 = z1 - eff * dt;
                    contact = Mathf.Min(z0, z1) - reach < r.pz + halfL && Mathf.Max(z0, z1) + reach > r.pz - halfL;
                }
            }
            o.lx = o.x;
            if (!o.passed && o.z > r.pz + halfL)
            {
                o.passed = true;
                float m = Mathf.Abs(r.px - o.x) - (o.hx + halfW);
                // only for the things that would have ended it (web 46583): not a rock, not a ramp
                bool harmful = o.kind == "buoy" || o.kind == "log" || o.kind == "jelly" || o.kind == "bigfin" || o.kind == "octopus";
                if (harmful && !o.used && m > 0 && m < 1.5f && !r.glideOn && !r.hanging) { r.score += 25; r.Say("CLOSE CALL! +25"); Sfx.Close(false); Sfx.Buzz(12); }
            }
            if (!contact) continue;
            if (o.ramp)
            {
                // a surface, not a thing to hit: ride up it and leave the top
                if (!r.airborne && !o.used && o.z > r.pz - 1.5f)
                {
                    o.used = true; r.vy = 15.5f * r.statAir * o.rampLift; r.airborne = true; r.boost = Mathf.Min(16f, r.boost + 4f); r.poseTake = 1f;
                    r.Say("RAMP!"); Sfx.Buzz(12);
                }
            }
            else if (o.kind == "jelly" && r.py < o.y + o.h + 0.35f)
            {
                if (o.used) continue; o.used = true;
                bool over = r.airborne && r.py > o.y + 0.10f;
                r.vy = over ? Mathf.Max(12.5f, -r.vy * 0.85f + 9f) : Mathf.Max(9f, r.vy + 8f);
                r.airborne = true; r.py = Mathf.Max(r.py, o.y + 0.9f);
                r.score += 60; r.boost = Mathf.Min(16f, r.boost + 3f); r.Say("JELLY BOUNCE! +60"); Sfx.Jelly(over); Sfx.Buzz(15);
                onSplash?.Invoke(new Vector3(o.x, o.y + 0.5f, o.z), over ? 14 : 20);
            }
            else if (r.py < r.waterY + o.h && !r.Submerged && !r.glideOn && !r.hanging)     // under the water in the helicopter nothing on it can touch him; nor on the paraglide's line or the plane's rope (web 46633)
            {
                float topY = r.waterY + o.h;
                // dropping onto the top of it is not driving into its side: that costs the board a
                // crack and throws you back up, and the thing itself is shoved aside and set spinning
                // knocked off the top it is shoved gently (web boardHit 27822); driven into, it goes
                // harder, higher and spinning faster (web 46641)
                if (r.vy < -0.5f && r.py > topY - 0.55f) { Knock(o, r, 0.7f, 2.0f, 1.4f, 1.6f, 1.8f, 0.05f, 5f); Hull.Hit(r, topY); onSplash?.Invoke(new Vector3(o.x, topY, o.z), 18); continue; }
                Knock(o, r, 0.9f, 2.4f, 2.0f, 2.4f, 2.2f, 0.06f, 6f);
                r.Wipeout(o.kind);
            }
        }
    }

    // The big fin: it crosses the lane from one side toward where you are, and two times in
    // three it hunts -- turning after you and, a second out, leaping clear of the water at you.
    // Three feet of sidestep or enough air gets you past it for a bonus; otherwise it has you.
    // Returns true when it has handled the obstacle's placement for this frame.
    // The web's heading w is the direction the nose points in world terms, (sin w, cos w) across and
    // down the lane (0 is straight at the camera). The model's own nose lies along its length, +x
    // turned to the side it crosses from (o.dir, the child's half turn), so the holder's yaw that puts
    // that nose on (sin w, cos w) is this one, in degrees.
    static float SharkYaw(Ob o, float w)
    {
        float nx = Mathf.Sin(w), nz = Mathf.Cos(w);
        return Mathf.Atan2(-nz * o.dir, nx * o.dir) * Mathf.Rad2Deg;
    }

    bool Shark(Ob o, float dt, float eff, float fzBase)
    {
        var r = rider;
        float togo = Mathf.Max(0.35f, (-o.z) / Mathf.Max(8f, eff));
        SharkJaw(o, dt, togo);
        if (o.atk == null)
        {
            float vx = (o.targetX - o.x) / togo;
            o.x += vx * dt;
            if (Random.value < dt * 14f) onSplash?.Invoke(new Vector3(o.x, o.y + 0.1f, o.z + 1.4f), 1);
            if (o.hunts) { float want = Mathf.Atan2(vx, Mathf.Max(1f, eff)); float d = Mathf.DeltaAngle(o.yaw * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad; o.yaw += d * Mathf.Min(1f, dt * 2.5f); }
            if (o.hunts && togo < ShkLead && r.running && !r.glideOn && !r.hanging && o.z < 0 && Mathf.Abs(o.x - r.px) < ShkReach)   // not at a man on a wing (web 46303)
            {
                o.atk = "leap"; o.at = 0; o.x0 = o.x; o.z0 = o.z; o.aimX = r.px; o.aimZ = r.pz;
                onSplash?.Invoke(new Vector3(o.x, o.y, o.z), 18);
            }
            return false;        // the usual float and tilt and contact test still apply
        }
        if (o.atk == "leap" || o.atk == "miss")
        {
            o.at += dt;
            float k = Mathf.Min(1f, o.at / ShkArc);
            float tx = o.hunts ? o.aimX : (o.aimX + (o.aimX - o.x0 > 0 ? ShkPast : -ShkPast));
            float tz = o.aimZ + (o.hunts ? 4.5f : 1.6f);
            o.x = o.x0 + (tx - o.x0) * k; o.z = o.z0 + (tz - o.z0) * k;
            float sea = WaveField.Swell(o.x, fzBase + o.z, r.t);
            float arc = Mathf.Sin(Mathf.PI * k);
            o.y = sea + o.floatOff + (o.lift + ShkApex) * arc;       // the JAWS clear the water by the apex, not the fin
            float ldx = tx - o.x0, ldz = tz - o.z0;
            float yaw = o.hunts ? Mathf.Atan2(ldx, ldz) : o.yaw;
            float dyk = Mathf.PI * Mathf.Cos(Mathf.PI * k) * ShkApex, dhk = (o.hunts ? Mathf.Sqrt(ldx * ldx + ldz * ldz) : Mathf.Abs(ldx)) + 0.001f;
            float pitch = Mathf.Atan2(dyk, dhk * 1.35f);
            o.go.transform.position = new Vector3(o.x, o.y, o.z);
            // nose up out of the water and over: pitched about its BEAM (its length lies along its own
            // x, so a turn about x rolled it instead of lifting the nose)
            o.go.transform.rotation = Quaternion.Euler(0f, SharkYaw(o, yaw), 0f) * Quaternion.Euler(0f, 0f, pitch * o.dir * Mathf.Rad2Deg);
            if (o.atk == "leap" && k >= 0.5f)
            {
                float off = Mathf.Abs(r.px - o.x);
                if (off < ShkDodge && r.py < o.y + ShkOver && r.running && !r.hanging)   // not a man on the tow rope (web 46349)
                {
                    o.atk = "bite"; eater = o; o.at = 0;
                    r.Wipeout("bigfin"); r.Say("EATEN!"); Sfx.Buzz(22);
                    return true;
                }
                o.atk = "miss"; r.score += 60; r.Say("DODGED THE SHARK! +60"); Sfx.Close(true); Sfx.Buzz(14);
            }
            if (k >= 1f) { onSplash?.Invoke(new Vector3(o.x, o.y, o.z), 26); o.atk = "done"; o.targetX = o.x; o.y = sea + o.floatOff; }
            return true;
        }
        if (o.atk == "bite")
        {
            // it takes him under over the chew, and the ride's wipeout follows it
            o.at += dt; float k = Mathf.Min(1f, o.at / ShkChew);
            float sea = WaveField.Swell(o.x, fzBase + o.z, r.t);
            o.y = sea + o.floatOff + (o.lift + 2.0f) * (1f - k) - 4.5f * k * k;
            o.go.transform.position = new Vector3(o.x, o.y, o.z);
            r.px = o.x; r.py = o.y - o.lift + 0.2f; r.pz = o.z; r.vy = 0;      // held in the mouth, not on its back
            r.transform.position = new Vector3(r.px, r.py, r.pz);
            return true;
        }
        return false;   // done: it swims on as a fin
    }

    // ---- the jaw works (web 46375-46407) ----
    // Cruising it chews slowly open and shut, so the mouth never reads as a frozen model; a beat
    // before it reaches your lane it snaps shut and falls open again; in a leap it gapes on the
    // way up and shuts at the apex, where the strike is decided; holding you it stays half shut.
    // Angles are the web's degrees about the bone's own hinge (its Y), positive shutting. The
    // import mirrors x, which turns a rotation about Y the other way, hence the minus.
    const float JawShut = 58f, JawIdle = 17f, JawSwing = 11f, JawRate = 1.7f, JawEase = 13f, BiteSecs = 0.85f, BiteLead = 0.55f, BiteX = 5.0f;
    void SharkJaw(Ob o, float dt, float togo)
    {
        if (o.jaw == null) return;
        var r = rider;
        o.jawT += dt;
        if (o.bite < 0f && o.atk == null && togo < BiteLead && Mathf.Abs(o.x - r.px) < BiteX) o.bite = 0f;
        float want;
        if (o.atk == "leap" || o.atk == "miss")
        {
            float k = Mathf.Min(1f, o.at / ShkArc);
            want = k < 0.5f ? -14f - 6f * Mathf.Sin(Mathf.PI * k) : JawShut * Mathf.Max(0f, 1f - (k - 0.5f) / 0.34f);
        }
        else if (o.atk == "bite") want = JawShut * 0.42f;
        else if (o.bite >= 0f && o.bite < BiteSecs)
        {
            o.bite += dt; float b = o.bite / BiteSecs;
            want = JawShut * Mathf.Clamp01(b < 0.30f ? b / 0.30f : 1f - (b - 0.55f) / 0.45f);
        }
        else want = JawIdle + Mathf.Sin(o.jawT * JawRate) * JawSwing;
        o.jawAng = float.IsNaN(o.jawAng) ? want : o.jawAng + (want - o.jawAng) * Mathf.Min(1f, dt * JawEase);
        o.jaw.localRotation = o.jawRest * Quaternion.AngleAxis(-o.jawAng, Vector3.up);
    }

    // the octopus bobs in its own time and, once it is in range ahead, lobs starfish at him
    void Octopus(Ob o, float dt)
    {
        o.at += dt;
        if (o.z > -100f && o.z < -4f)
        {
            o.aimZ -= dt;          // aimZ doubles as the throw timer here
            if (o.aimZ <= 0f) { o.aimZ = 0.85f + Random.value * 0.85f; Throw(o); }
        }
    }

    void Throw(Ob o)
    {
        var r = rider;
        Sfx.StarThrow();
        if (starMesh == null)
        {
            // a flat five-pointed star
            var v = new List<Vector3>(); var idx = new List<int>();
            v.Add(Vector3.zero);
            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2f, rr = (i % 2 == 0) ? 0.55f : 0.24f; v.Add(new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr)); }
            for (int i = 0; i < 10; i++) { idx.Add(0); idx.Add(1 + (i + 1) % 10); idx.Add(1 + i); idx.Add(0); idx.Add(1 + i); idx.Add(1 + (i + 1) % 10); }
            starMesh = new Mesh { name = "star" }; starMesh.SetVertices(v); starMesh.SetTriangles(idx, 0); starMesh.RecalculateNormals();
            starMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); starMat.SetColor("_BaseColor", new Color(1f, 0.54f, 0.24f)); starMat.SetFloat("_Smoothness", 0.3f);
        }
        var go = new GameObject("star", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = starMesh; go.GetComponent<MeshRenderer>().sharedMaterial = starMat;
        float fx = o.x, fy = o.y + 1.15f, fz = o.z;
        go.transform.position = new Vector3(fx, fy, fz); go.transform.rotation = Random.rotation;
        bool onTarget = Random.value < 0.55f;
        float wide = onTarget ? 0f : (Random.value < 0.5f ? -1f : 1f) * (2.6f + Random.value * 3.8f);
        float tx = r.px + r.vx * 0.22f + wide, ty = r.py + 0.55f, tz = 0f;
        float T = Mathf.Max(0.40f, Mathf.Sqrt((tx - fx) * (tx - fx) + (tz - fz) * (tz - fz)) / (26f + Random.value * 5f));
        stars.Add(new Star { go = go, vx = (tx - fx) / T, vy = ((ty - fy) + 0.5f * 13f * T * T) / T, vz = (tz - fz) / T - r.eff * 0.4f,
                             sx = (Random.value - 0.5f) * 13f, sy = (Random.value - 0.5f) * 13f });
    }

    void Stars(float dt, float eff, float fzBase)
    {
        var r = rider;
        for (int i = stars.Count - 1; i >= 0; i--)
        {
            var st = stars[i]; st.t += dt; st.vy -= 13f * dt;
            var p = st.go.transform.position;
            p.x += st.vx * dt; p.y += st.vy * dt; p.z += st.vz * dt + eff * dt * 0.4f;
            st.go.transform.position = p;
            st.go.transform.rotation *= Quaternion.Euler(st.sx * dt * Mathf.Rad2Deg, st.sy * dt * Mathf.Rad2Deg, 0);
            bool hit = r.running && Mathf.Abs(p.x - r.px) < 1.5f && Mathf.Abs(p.z - r.pz) < 2.2f && Mathf.Abs(p.y - r.py) < 2.2f;
            float sea = WaveField.Height(p.x, fzBase + p.z, r.t);
            if (hit)
            {
                onSplat?.Invoke(new Color(1f, 0.54f, 0.24f)); r.Say("SPLAT!"); Sfx.StarHit(true);
                onSplash?.Invoke(p, 10);
                Destroy(st.go); stars.RemoveAt(i); continue;
            }
            if (p.y < sea || st.t > 5f || p.z > 18f)
            {
                if (p.y < sea) { onSplash?.Invoke(new Vector3(p.x, sea, p.z), 6); Sfx.StarMiss(Mathf.Sqrt((p.x - r.px) * (p.x - r.px) + (p.z - r.pz) * (p.z - r.pz))); }
                Destroy(st.go); stars.RemoveAt(i);
            }
        }
    }

    public Ob Make(string kind, float x, float z)
    {
        var o = new Ob { kind = kind, x = x, z = z, floatOff = FloatOff[kind] };
        var holder = new GameObject(kind);
        holder.transform.SetParent(transform, false);
        switch (kind)
        {
            case "buoy":
                // two liveries, picked at random as the web picks them: the danger buoy, and the
                // small channel marker at 1.18 against its 2.00; the hitbox is the original's
                // the hitbox scales with the livery, as the web's does (OBST_SIZE 23169, 23275): its
                // r 0.85 / h 2.2 times 2.00 for the danger buoy, 1.18 for the channel marker. The
                // danger buoy is 4.4 ft tall to the hit test -- not a thing a plain hop clears.
                if (Random.value < 0.5f && Resources.Load<GameObject>("Models/buoy2") != null)
                { Models.Spawn("buoy2", holder.transform, 4.2f * 1.18f / 2.00f, PrimitiveType.Cylinder); o.hx = o.hz = 0.85f * 1.18f; o.h = 2.2f * 1.18f; }
                else { Models.Spawn("buoy", holder.transform, 4.2f, PrimitiveType.Cylinder); o.hx = o.hz = 0.85f * 2.00f; o.h = 2.2f * 2.00f; }
                break;
            // the web's log (22764): five feet of trunk, its hitbox 2.50 across the lane and 0.62 along
            // it -- a plank, long and thin. It was drawn 6.4 ft long here, with a box to match.
            case "log": o.hx = 2.50f; o.hz = 0.62f; o.h = 1.1f;
                { var g = Models.Spawn("log", holder.transform, 1.2f, PrimitiveType.Capsule); Models.FitLength(g, 5.0f);
                  // FitLength lays the long side along z; a log floats ACROSS the lane, so a quarter turn in the parent's frame
                  g.transform.localRotation = Quaternion.Euler(0, 90, 0) * g.transform.localRotation; } break;
            case "ramp": o.hx = 1.70f; o.hz = 2.55f; o.h = 1.6f; o.ramp = true;
                {
                    // the web's ramp (22780-22862): 5.1 feet up its length (RL 2.55 each way), lip at
                    // the far end (-z, which is how the file has it), skirt 0.30 under the deck line
                    var g = Models.Spawn("ramp", holder.transform, 1.6f, PrimitiveType.Cube);
                    if (g.name.EndsWith("(stand-in)")) g.transform.localScale = new Vector3(4.6f, 1.2f, 6f);
                    else
                    {
                        Models.FitLength(g, RampL * 2f);
                        var rb = Models.Bounds(g);
                        g.transform.position += new Vector3(0f, -0.30f - (rb.min.y - holder.transform.position.y), 0f);
                    }
                    // one of four heights in the water (web RAMP_TIERS): the deeper it sits, the lower it launches
                    float tier = RampTiers[Random.Range(0, RampTiers.Length)];
                    o.rampSink = RampH * tier * RampSink; o.rampLift = 1f - tier * RampLaunchDrop;
                } break;
            case "jelly": o.hx = 1.05f; o.hz = 1.05f; o.h = 1.6f;          // web 22750
                {
                    // the web keeps both jellyfish models and picks one at random each time (OBST_VARIANTS);
                    // both are fitted to the same box
                    string jm = Random.value < 0.5f && Resources.Load<GameObject>("Models/jelly2") != null ? "jelly2" : "jelly";
                    var g = Models.Spawn(jm, holder.transform, 2.6f, PrimitiveType.Sphere);
                } break;
            case "rock": o.hx = 1.55f; o.hz = 0.95f; o.h = 1.15f;
                // the chute's own hazard (web 22652-22675): wide and LOW, the opposite of the rest of
                // the rack. At chute speed there is no steering round it; it is broad enough that
                // going round is not on and short enough that going over it is. Five overlapping
                // lumps, because a boulder in white water is a cluster and one shape is a traffic cone;
                // a sheet of white water breaking over it, which is what makes it read as IN the fall.
                {
                    var lit = Shader.Find("Universal Render Pipeline/Lit"); if (lit == null) lit = Shader.Find("Standard");
                    var grey = new Material(lit); grey.SetColor("_BaseColor", new Color(0x6b / 255f, 0x6f / 255f, 0x74 / 255f)); grey.SetFloat("_Smoothness", 1f - 0.92f); grey.SetFloat("_Metallic", 0f);
                    var wet = new Material(lit); wet.SetColor("_BaseColor", new Color(0x4a / 255f, 0x50 / 255f, 0x54 / 255f)); wet.SetFloat("_Smoothness", 1f - 0.62f); wet.SetFloat("_Metallic", 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a2 = i / 5f * Mathf.PI * 2f, rr = 0.55f + Random.value * 0.55f;
                        var lump = GameObject.CreatePrimitive(PrimitiveType.Sphere); var lc = lump.GetComponent<Collider>(); if (lc != null) Destroy(lc);
                        lump.name = "lump"; lump.transform.SetParent(holder.transform, false);
                        lump.transform.localPosition = new Vector3(Mathf.Cos(a2) * 0.85f * (i > 0 ? 1f : 0f), 0.10f + Random.value * 0.30f, Mathf.Sin(a2) * 0.55f * (i > 0 ? 1f : 0f));
                        lump.transform.localRotation = Quaternion.Euler(Random.value * 3f * Mathf.Rad2Deg, Random.value * 3f * Mathf.Rad2Deg, Random.value * 3f * Mathf.Rad2Deg);
                        lump.transform.localScale = new Vector3(rr * 2f, rr * 2f * 0.72f, rr * 2f);   // a unit sphere is radius 0.5
                        var lr = lump.GetComponent<MeshRenderer>(); lr.sharedMaterial = i == 0 ? wet : grey; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    var foam = GameObject.CreatePrimitive(PrimitiveType.Sphere); var fc = foam.GetComponent<Collider>(); if (fc != null) Destroy(fc);
                    foam.name = "foam"; foam.transform.SetParent(holder.transform, false);
                    foam.transform.localPosition = new Vector3(0, 0.30f, 0); foam.transform.localScale = new Vector3(1.15f * 2f * 1.5f, 1.15f * 2f * 0.34f, 1.15f * 2f * 0.85f);
                    var fr = foam.GetComponent<MeshRenderer>(); fr.sharedMaterial = Dive.Unlit(new Color(0xea / 255f, 0xf7 / 255f, 1f, 0.34f), true); fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                } break;
            case "octopus": o.hx = 1.25f; o.hz = 1.25f; o.h = 2.4f;        // the body only, its arms are not a wall (web 22649)
                {
                    var g = Models.Spawn("octopus", holder.transform, 2.4f, PrimitiveType.Sphere);
                    foreach (var a in g.GetComponentsInChildren<Animation>(true)) a.enabled = false;
                    foreach (var a in g.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                    o.aimZ = 0.8f + Random.value;      // first throw
                } break;
            case "bigfin": o.hx = 1.70f; o.hz = 0.50f; o.h = 4.2f;
                {
                    var g = Models.Spawn("bigfin", holder.transform, 3.4f, PrimitiveType.Cube);
                    foreach (var a in g.GetComponentsInChildren<Animation>(true)) a.enabled = false;
                    foreach (var a in g.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                    // ---- a shark IN the water, with only its fin out (web 23218-23268) ----
                    // Fitted 3.4 ft tall and stood on its belly at the holder, the whole animal lay ON
                    // the sea: a small fish floating side-on, which is what read as a shark that was
                    // not swimming. The web fits it nine feet long, squashes it across its beam to
                    // 0.74, and drops it until only the top 0.36 ft is above the waterline, so the
                    // fin cuts the water and the body is a dark shape under it.
                    var sb = Models.Bounds(g);
                    float sLong = Mathf.Max(sb.size.x, sb.size.z);
                    if (sLong > 1e-4f)
                    {
                        var ls = g.transform.localScale * (9f / sLong);
                        ls.z *= 0.74f;
                        g.transform.localScale = ls;
                        sb = Models.Bounds(g);
                        g.transform.localPosition += new Vector3(-sb.center.x, (0.36f - o.floatOff) - sb.max.y, -sb.center.z);
                    }
                    foreach (var t in g.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Jaw") { o.jaw = t; o.jawRest = t.localRotation; break; }
                    o.jawT = Random.value * 6f;
                    // how far the mouth sits under the holder: the leap lifts by this as well, so the
                    // jaws (not the fin) clear the water by the apex
                    if (o.jaw != null) o.lift = Mathf.Max(0f, holder.transform.position.y - o.jaw.position.y);
                    o.dir = Random.value < 0.5f ? 1f : -1f;
                    o.hunts = Random.value < ShkChance;
                    o.startX = rider.px - o.dir * (11f + Random.value * 5f);
                    o.targetX = rider.px + o.dir * (0.5f + Random.value * 2.5f);
                    o.x = o.startX; o.yaw = o.dir * Mathf.PI / 2f;
                    g.transform.localRotation = Quaternion.Euler(0, o.dir > 0 ? 0 : 180f, 0);   // nose across the lane
                } break;
        }
        o.go = holder;
        // ---- sat IN the water, not on it ----
        // The web's float heights assumed its own model pivots; here the buoy is stood on its base
        // and the log centred, so the buoy hung a third of a foot clear of the sea and the log
        // barely touched it, and both read as hovering (and their reflections as detached). Each
        // floating thing is seated by its own measured bottom instead: a buoy's float base half a
        // foot under, a log a bit under half its thickness, a jellyfish with only the top of its bell out.
        float draft = kind == "buoy" ? 0.5f : kind == "log" ? 0.45f : kind == "jelly" ? 0.72f : -1f;   // a jellyfish is IN the sea: the top of its bell out, the tentacles under
        if (draft >= 0f)
        {
            var rs = holder.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var bb = rs[0].bounds; foreach (var rr in rs) bb.Encapsulate(rr.bounds);
                float bottom = bb.min.y - holder.transform.position.y, tall = bb.size.y;
                float sink = kind == "buoy" ? draft : draft * Mathf.Min(tall, kind == "log" ? 1.6f : 2.6f);
                // the MODEL is moved down inside its holder, not the holder: the holder keeps the
                // web's height, which the jellyfish bounce and the knocks are measured from
                float shift = -sink - o.floatOff - bottom;
                foreach (Transform c in holder.transform) c.localPosition += new Vector3(0f, shift, 0f);
            }
        }
        if (o.ramp) o.floatOff -= o.rampSink;
        o.y = WaveField.Swell(x, rider.dist * WaveField.Drift + z, rider.t) + o.floatOff;
        obstacles.Add(o);
        return o;
    }
}
