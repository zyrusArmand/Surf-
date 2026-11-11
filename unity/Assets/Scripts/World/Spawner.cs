// What is in the water: buoys, logs, ramps, jellyfish, cast by distance and spaced by the gap
// on the water rather than by the clock, the way the web game spaces them.
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public class Ob
    {
        public string kind; public GameObject go; public float x, z, y, floatOff, hx, hz, h;
        public bool ramp, used, passed; public float rx, rz;
    }

    public BoardController rider;
    public System.Action<Vector3, int> onSplash;
    public readonly List<Ob> obstacles = new List<Ob>();
    float spawnT = 1.8f; int laneSlot; string lastKind = ""; int sameRun;
    const float FtToM = 0.3048f;

    static readonly Dictionary<string, float> FloatOff = new Dictionary<string, float> {
        {"buoy",0.36f},{"jelly",0.34f},{"log",0.40f},{"ramp",0.02f} };

    public void Clear()
    {
        foreach (var o in obstacles) if (o.go) Destroy(o.go);
        obstacles.Clear(); spawnT = 1.8f;
    }

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
            laneSlot = (laneSlot + 1 + Random.Range(0, 3)) % 7;
            float laneX = (-3 + laneSlot) * 4.9f + (Random.value - 0.5f) * 3.0f;
            Make(pick, Mathf.Clamp(r.px * 0.55f + laneX, -17f, 17f), -170f);
            if (distM > 600 && Random.value < 0.30f)
            {
                float edge = (Random.value < 0.5f ? -1 : 1) * (11.5f + Random.value * 4.5f);
                Make(Random.value < 0.5f ? "buoy" : "log", edge, -176f);
            }
        }
        float fzBase = r.dist * WaveField.Drift;
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            o.z += eff * dt;
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
            bool contact = Mathf.Abs(r.px - o.x) < o.hx + BoardController.BoardHalfW && Mathf.Abs(r.pz - o.z) < o.hz + BoardController.BoardHalfL;
            if (!o.passed && o.z > r.pz + BoardController.BoardHalfL)
            {
                o.passed = true;
                float m = Mathf.Abs(r.px - o.x) - (o.hx + BoardController.BoardHalfW);
                if (o.kind != "ramp" && !o.used && m > 0 && m < 1.5f) { r.score += 25; r.Say("CLOSE CALL! +25"); }
            }
            if (!contact) continue;
            if (o.ramp)
            {
                // a surface, not a thing to hit: ride up it and leave the top
                if (!r.airborne && !o.used && o.z > r.pz - 1.5f)
                {
                    o.used = true; r.vy = 15.5f * r.statAir; r.airborne = true; r.boost = Mathf.Min(16f, r.boost + 4f); r.poseTake = 1f;
                    r.Say("RAMP!");
                }
            }
            else if (o.kind == "jelly" && r.py < o.y + o.h + 0.35f)
            {
                if (o.used) continue; o.used = true;
                bool over = r.airborne && r.py > o.y + 0.10f;
                r.vy = over ? Mathf.Max(12.5f, -r.vy * 0.85f + 9f) : Mathf.Max(9f, r.vy + 8f);
                r.airborne = true; r.py = Mathf.Max(r.py, o.y + 0.9f);
                r.score += 60; r.boost = Mathf.Min(16f, r.boost + 3f); r.Say("JELLY BOUNCE! +60");
                onSplash?.Invoke(new Vector3(o.x, o.y + 0.5f, o.z), over ? 14 : 20);
            }
            else if (r.py < r.waterY + o.h)
            {
                r.Wipeout(o.kind);
            }
        }
    }

    void Make(string kind, float x, float z)
    {
        var o = new Ob { kind = kind, x = x, z = z, floatOff = FloatOff[kind] };
        var holder = new GameObject(kind);
        holder.transform.SetParent(transform, false);
        switch (kind)
        {
            case "buoy": o.hx = 1.25f; o.hz = 1.25f; o.h = 2.4f; Models.Spawn("buoy", holder.transform, 4.2f, PrimitiveType.Cylinder); break;
            case "log": o.hx = 3.1f; o.hz = 0.95f; o.h = 1.15f;
                { var g = Models.Spawn("log", holder.transform, 1.2f, PrimitiveType.Capsule); Models.FitLength(g, 6.4f);
                  // FitLength lays the long side along z; a log floats ACROSS the lane, so a quarter turn in the parent's frame
                  g.transform.localRotation = Quaternion.Euler(0, 90, 0) * g.transform.localRotation; } break;
            case "ramp": o.hx = 2.4f; o.hz = 3.0f; o.h = 1.6f; o.ramp = true;
                { var g = Models.Spawn("ramp", holder.transform, 1.6f, PrimitiveType.Cube); if (g.name.EndsWith("(stand-in)")) g.transform.localScale = new Vector3(4.6f, 1.2f, 6f); } break;
            case "jelly": o.hx = 1.1f; o.hz = 1.1f; o.h = 1.4f;
                { var g = Models.Spawn("jelly", holder.transform, 2.6f, PrimitiveType.Sphere); } break;
        }
        o.go = holder;
        o.y = WaveField.Swell(x, rider.dist * WaveField.Drift + z, rider.t) + o.floatOff;
        obstacles.Add(o);
    }
}
