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
        {"buoy",0.36f},{"jelly",0.34f},{"log",0.40f},{"ramp",0.02f},{"bigfin",0.28f},{"octopus",-0.10f} };
    const float ShkLead = 1.15f, ShkArc = 1.30f, ShkApex = 5.4f, ShkDodge = 3.2f, ShkOver = 3.4f, ShkReach = 7.5f, ShkChance = 0.66f, ShkPast = 2.2f;
    public const float ShkChew = 1.75f;

    public void Clear()
    {
        foreach (var o in obstacles) if (o.go) Destroy(o.go);
        obstacles.Clear(); spawnT = 1.8f; eater = null; bigFinCd = 0; octCd = 0;
        foreach (var st in stars) if (st.go) Destroy(st.go);
        stars.Clear();
    }

    public void Remove(int i) { if (obstacles[i].go) Destroy(obstacles[i].go); obstacles.RemoveAt(i); }

    public void Step(float dt)
    {
        var r = rider; float eff = r.eff;
        spawnT -= dt;
        float distM = r.dist;
        if (r.running && spawnT <= 0f)
        {
            float wantGap = Mathf.Max(15f, 24f - distM / 300f);
            spawnT = Mathf.Max(0.40f, wantGap / Mathf.Max(16f, eff)) * (0.75f + Random.value * 0.5f);
            // buoys and logs are the whole game to begin with; ramps, then jellyfish
            var weights = new List<(string, float)> { ("buoy", 22), ("log", 17) };
            if (distM >= 150) weights.Add(("ramp", 16));
            if (distM >= 340) weights.Add(("jelly", 20));
            if (distM >= 560 && bigFinCd <= 0f) weights.Add(("bigfin", 11));
            if (distM >= 780 && octCd <= 0) weights.Add(("octopus", Mathf.Min(30f, 4f + Mathf.Max(0f, (distM - 780f) / 28f))));
            string pick = "buoy";
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
            Make(pick, Mathf.Clamp(r.px * 0.55f + laneX, -17f, 17f), -170f);
            if (distM > 600 && Random.value < 0.30f)
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
            o.y += (oy - o.y) * Mathf.Min(1f, dt * (o.ramp ? 3f : 9f));
            float tilt = o.ramp ? 0.06f : 0.30f, span = o.ramp ? 3.2f : 1.6f;
            float slopeZ = (WaveField.Swell(o.x, fzBase + o.z + span, r.t) - WaveField.Swell(o.x, fzBase + o.z - span, r.t)) * tilt;
            float slopeX = (WaveField.Swell(o.x + span, fzBase + o.z, r.t) - WaveField.Swell(o.x - span, fzBase + o.z, r.t)) * tilt;
            float f = Mathf.Min(1f, dt * (o.ramp ? 2f : 6f));
            o.rx += (slopeZ - o.rx) * f; o.rz += (-slopeX - o.rz) * f;
            o.go.transform.position = new Vector3(o.x, o.y, o.z);
            o.go.transform.rotation = Quaternion.Euler(o.rx * Mathf.Rad2Deg, 0, o.rz * Mathf.Rad2Deg + (o.kind == "buoy" ? Mathf.Sin(r.t * 2f + o.x) * 3.4f : 0));
            if (!r.running) continue;

            // the footprint on the water, then the height test
            // the footprint shrinks with him under a coconut charge
            float halfL = BoardController.BoardHalfL * Coconuts.SizeK, halfW = BoardController.BoardHalfW * Coconuts.SizeK;
            bool contact = Mathf.Abs(r.px - o.x) < o.hx + halfW && Mathf.Abs(r.pz - o.z) < o.hz + halfL;
            if (!o.passed && o.z > r.pz + halfL)
            {
                o.passed = true;
                float m = Mathf.Abs(r.px - o.x) - (o.hx + halfW);
                if (o.kind != "ramp" && !o.used && m > 0 && m < 1.5f) { r.score += 25; r.Say("CLOSE CALL! +25"); Sfx.Close(false); Sfx.Buzz(12); }
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
            else if (r.py < r.waterY + o.h && !r.Submerged)     // under the water in the helicopter nothing on it can touch him
            {
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
            if (o.hunts && togo < ShkLead && r.running && o.z < 0 && Mathf.Abs(o.x - r.px) < ShkReach)
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
