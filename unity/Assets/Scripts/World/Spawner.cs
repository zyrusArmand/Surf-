// What is in the water: buoys, logs, ramps, jellyfish, cast by distance and spaced by the gap
// on the water rather than by the clock, the way the web game spaces them.
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public class Ob
    {
        public string kind; public GameObject go; public float x, z, y, floatOff, hx, hz, h;
        public bool ramp, used, passed, escape; public float rx, rz;
        // knocked: shoved aside, lifted and set spinning by the thing that hit it
        public bool knocked; public float kvx, kvy, kvz, ksx, ksz, ky, kax, kaz;
        // the shark
        public float dir, startX, targetX, at, x0, z0, aimX, aimZ, yaw; public bool hunts; public string atk;
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
    float spawnT = 1.8f; int laneSlot; string lastKind = ""; int sameRun;
    const float FtToM = 0.3048f;

    static readonly Dictionary<string, float> FloatOff = new Dictionary<string, float> {
        {"buoy",0.36f},{"jelly",0.34f},{"log",0.40f},{"ramp",0.02f},{"bigfin",0.28f},{"octopus",-0.10f},{"rock",0.0f} };

    // ---- the set pieces stand the rack down ----
    // The fork casts its own run down the channel he took and the chute its own rack, so the
    // ordinary spawner holds for the length of either (web 46175, 46182). holdCast is written
    // each frame by Island/Chute; chuteW is the chute's weight 0..1, which swaps the rack for
    // rock, timber and the odd slab and tightens the gap by 30% (web 46192, 46213-46214).
    public bool holdCast;
    public float chuteW;
    const float ShkLead = 1.15f, ShkArc = 1.30f, ShkApex = 5.4f, ShkDodge = 3.2f, ShkOver = 3.4f, ShkReach = 7.5f, ShkChance = 0.66f, ShkPast = 2.2f;
    public const float ShkChew = 1.75f;

    // ---- the banner plane: a tow passes low over the lane every forty to eighty seconds ----
    public SetWave setWave; public Island island;
    Transform tow; bool towOn; float towT = 20f, towSpin;

    void TowPlane(float dt, float eff)
    {
        bool setIdle = setWave == null || setWave.ph == SetWave.Phase.Idle;
        if (island != null && island.On) towT = Mathf.Max(towT, 2.0f);   // no banner tow over a beach
        if (setIdle) towT -= dt;
        if (setIdle && towT <= 0f && !towOn)
        {
            if (tow == null)
            {
                var go = Models.Spawn("plane", null, 2f, PrimitiveType.Cube); Models.FitLength(go, 22f); go.name = "BannerPlane";
                tow = go.transform;
            }
            towOn = true; tow.gameObject.SetActive(true);
            tow.position = new Vector3(Random.value * 16f - 8f, 11f, -190f);
            towT = 40f + Random.value * 38f;
        }
        if (towOn && setIdle)
        {
            towSpin += dt;
            tow.position = new Vector3(tow.position.x, 11f + Mathf.Sin(towSpin * 1.5f) * 0.6f, tow.position.z + (eff * 0.45f + 12f) * dt);
            tow.rotation = Quaternion.Euler(0, 180f, 0);      // nose toward the rider, flying in
            if (tow.position.z > 40f) { towOn = false; tow.gameObject.SetActive(false); }
        }
    }

    void Knock(Ob o, BoardController r, float side, float up, float upR, float spin)
    {
        if (o.knocked) return;
        o.knocked = true;
        o.kvx = (o.x - r.px) * side + (Random.value - 0.5f) * 2.4f; o.kvy = up + Random.value * upR; o.kvz = 2.2f + r.eff * 0.06f;
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
        obstacles.Clear(); spawnT = 1.8f; eater = null; bigFinCd = 0; octCd = 0; holdCast = false; chuteW = 0;
        towOn = false; towT = 20f + Random.value * 20f; if (tow != null) tow.gameObject.SetActive(false);
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
        if (r.running) TowPlane(dt, eff);
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
                if (distM >= 560 && bigFinCd <= 0f) weights.Add(("bigfin", 11));
                if (distM >= 780 && octCd <= 0) weights.Add(("octopus", Mathf.Min(30f, 4f + Mathf.Max(0f, (distM - 780f) / 28f))));
            }
            string pick = weights[0].Item1;
            for (int tries = 0; tries < 8; tries++)
            {
                float total = 0; foreach (var w in weights) total += w.Item2;
                float roll = Random.value * total; string cand = weights[0].Item1;
                foreach (var w in weights) { roll -= w.Item2; if (roll <= 0) { cand = w.Item1; break; } }
                if (cand == lastKind && sameRun >= 2) continue;
                pick = cand; break;
            }
            sameRun = pick == lastKind ? sameRun + 1 : 0; lastKind = pick;
            if (pick == "bigfin") bigFinCd = 18f;
            octCd = Mathf.Max(0, octCd - 1); if (pick == "octopus") octCd = 3;
            laneSlot = (laneSlot + 1 + Random.Range(0, 3)) % 7;
            float laneX = (-3 + laneSlot) * 4.9f + (Random.value - 0.5f) * 3.0f;
            float lim = Mathf.Min(17f, r.laneLim);
            Make(pick, Mathf.Clamp(r.px * 0.55f + laneX, -lim, lim), -170f);
            if (distM > 600 && Random.value < 0.30f && chuteW <= 0.5f)
            {
                float edge = (Random.value < 0.5f ? -1 : 1) * (11.5f + Random.value * 4.5f);
                Make(Random.value < 0.5f ? "buoy" : "log", edge, -176f);
            }
        }
        bigFinCd -= dt;
        float fzBase = r.dist * WaveField.Drift;
        Stars(dt, eff, fzBase);
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            o.z += eff * dt;
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
            o.go.transform.rotation = Quaternion.Euler(o.rx * Mathf.Rad2Deg, 0, o.rz * Mathf.Rad2Deg + (o.kind == "buoy" ? Mathf.Sin(r.t * 2f + o.x) * 3.4f : 0));
            if (!r.running || r.Submerged) continue;        // under the water nothing floating on it can reach him

            // the footprint on the water, then the height test
            // the footprint shrinks with him under a coconut charge
            float halfL = BoardController.BoardHalfL * Coconuts.SizeK, halfW = BoardController.BoardHalfW * Coconuts.SizeK;
            bool contact = Mathf.Abs(r.px - o.x) < o.hx + halfW && Mathf.Abs(r.pz - o.z) < o.hz + halfL;
            if (!o.passed && o.z > r.pz + halfL)
            {
                o.passed = true;
                float m = Mathf.Abs(r.px - o.x) - (o.hx + halfW);
                if (o.kind != "ramp" && !o.used && m > 0 && m < 1.5f && !r.glideOn) { r.score += 25; r.Say("CLOSE CALL! +25"); Sfx.Close(false); Sfx.Buzz(12); }
            }
            if (!contact) continue;
            if (o.ramp)
            {
                // a surface, not a thing to hit: ride up it and leave the top
                if (!r.airborne && !o.used && o.z > r.pz - 1.5f)
                {
                    o.used = true; r.vy = 15.5f * r.statAir; r.airborne = true; r.boost = Mathf.Min(16f, r.boost + 4f); r.poseTake = 1f;
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
            else if (r.py < r.waterY + o.h && !r.Submerged && !r.glideOn)     // under the water in the helicopter nothing on it can touch him; nor on the paraglide's line (web 46633)
            {
                float topY = r.waterY + o.h;
                // dropping onto the top of it is not driving into its side: that costs the board a
                // crack and throws you back up, and the thing itself is shoved aside and set spinning
                Knock(o, r, 0.7f, 1.4f, 1.6f, 5f);
                if (r.vy < -0.5f && r.py > topY - 0.55f) { Hull.Hit(r, topY); onSplash?.Invoke(new Vector3(o.x, topY, o.z), 18); continue; }
                r.Wipeout(o.kind);
            }
        }
    }

    // The big fin: it crosses the lane from one side toward where you are, and two times in
    // three it hunts -- turning after you and, a second out, leaping clear of the water at you.
    // Three feet of sidestep or enough air gets you past it for a bonus; otherwise it has you.
    // Returns true when it has handled the obstacle's placement for this frame.
    bool Shark(Ob o, float dt, float eff, float fzBase)
    {
        var r = rider;
        float togo = Mathf.Max(0.35f, (-o.z) / Mathf.Max(8f, eff));
        if (o.atk == null)
        {
            float vx = (o.targetX - o.x) / togo;
            o.x += vx * dt;
            if (Random.value < dt * 14f) onSplash?.Invoke(new Vector3(o.x, o.y + 0.1f, o.z + 1.4f), 1);
            if (o.hunts) { float want = Mathf.Atan2(vx, Mathf.Max(1f, eff)); float d = Mathf.DeltaAngle(o.yaw * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad; o.yaw += d * Mathf.Min(1f, dt * 2.5f); }
            if (o.hunts && togo < ShkLead && r.running && !r.glideOn && o.z < 0 && Mathf.Abs(o.x - r.px) < ShkReach)   // not at a man on a wing (web 46303)
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
            o.y = sea + o.floatOff + (1.2f + ShkApex) * arc;
            float ldx = tx - o.x0, ldz = tz - o.z0;
            float yaw = o.hunts ? Mathf.Atan2(ldx, ldz) : o.yaw;
            float dyk = Mathf.PI * Mathf.Cos(Mathf.PI * k) * ShkApex, dhk = (o.hunts ? Mathf.Sqrt(ldx * ldx + ldz * ldz) : Mathf.Abs(ldx)) + 0.001f;
            float pitch = Mathf.Atan2(dyk, dhk * 1.35f);
            o.go.transform.position = new Vector3(o.x, o.y, o.z);
            o.go.transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0) * Quaternion.Euler(-pitch * Mathf.Rad2Deg, 0, 0);
            if (o.atk == "leap" && k >= 0.5f)
            {
                float off = Mathf.Abs(r.px - o.x);
                if (off < ShkDodge && r.py < o.y + ShkOver && r.running)
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
            o.y = sea + o.floatOff + 2.0f * (1f - k) - 4.5f * k * k;
            o.go.transform.position = new Vector3(o.x, o.y, o.z);
            r.px = o.x; r.py = o.y + 1.1f; r.pz = o.z; r.vy = 0;
            r.transform.position = new Vector3(r.px, r.py, r.pz);
            return true;
        }
        return false;   // done: it swims on as a fin
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
                o.hx = 1.25f; o.hz = 1.25f; o.h = 2.4f;
                if (Random.value < 0.5f && Resources.Load<GameObject>("Models/buoy2") != null) Models.Spawn("buoy2", holder.transform, 4.2f * 1.18f / 2.00f, PrimitiveType.Cylinder);
                else Models.Spawn("buoy", holder.transform, 4.2f, PrimitiveType.Cylinder);
                break;
            case "log": o.hx = 3.1f; o.hz = 0.95f; o.h = 1.15f;
                { var g = Models.Spawn("log", holder.transform, 1.2f, PrimitiveType.Capsule); Models.FitLength(g, 6.4f);
                  // FitLength lays the long side along z; a log floats ACROSS the lane, so a quarter turn in the parent's frame
                  g.transform.localRotation = Quaternion.Euler(0, 90, 0) * g.transform.localRotation; } break;
            case "ramp": o.hx = 2.4f; o.hz = 3.0f; o.h = 1.6f; o.ramp = true;
                { var g = Models.Spawn("ramp", holder.transform, 1.6f, PrimitiveType.Cube); if (g.name.EndsWith("(stand-in)")) g.transform.localScale = new Vector3(4.6f, 1.2f, 6f); } break;
            case "jelly": o.hx = 1.1f; o.hz = 1.1f; o.h = 1.4f;
                { var g = Models.Spawn("jelly", holder.transform, 2.6f, PrimitiveType.Sphere); } break;
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
            case "octopus": o.hx = 1.6f; o.hz = 1.6f; o.h = 1.4f;
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
                    o.dir = Random.value < 0.5f ? 1f : -1f;
                    o.hunts = Random.value < ShkChance;
                    o.startX = rider.px - o.dir * (11f + Random.value * 5f);
                    o.targetX = rider.px + o.dir * (0.5f + Random.value * 2.5f);
                    o.x = o.startX; o.yaw = o.dir * Mathf.PI / 2f;
                    g.transform.localRotation = Quaternion.Euler(0, o.dir > 0 ? 0 : 180f, 0);   // nose across the lane
                } break;
        }
        o.go = holder;
        o.y = WaveField.Swell(x, rider.dist * WaveField.Drift + z, rider.t) + o.floatOff;
        obstacles.Add(o);
        return o;
    }
}
