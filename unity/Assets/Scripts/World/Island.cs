// The fork: an island in the lane with a channel either side, the web game's set piece ashore,
// number for number (web 39811-40400, 42740-43103, 45359-45449, 46862-46898, 47108-47137).
//   ARMED:   summoned (the web arms it only from its workshop menu). The island is put 210 ft
//            ahead and slides up the lane at the ride's own speed; anything the ordinary rack
//            had already put on the sand is swept off it 45 ft ahead of him.
//   CHOICE:  when the tip reaches him. Outside 6.4 ft of the middle he has taken a channel:
//            +120, held off the sand by a wall that walks outward for 95 ft (17 degrees of
//            splay) and then runs straight for 70, the shot swinging onto the new heading.
//            Left is sharks and nothing else; right is logs, jellyfish and buoys. Out the far
//            end pays +400 and the lane is the lane again.
//   BEACHED: inside 6.4 ft he runs aground: the board sheds its speed up the sand over 1.35 s
//            and stops, and he comes off it at 14.5 ft/s on his own legs, shed at 9.4 ft/s/s
//            to a standing stop. Then he walks: the stick, 7.4 ft/s, anywhere there is sand,
//            round the palms, to the chest (150-300 puka, +600) if he likes; walking off the
//            far end of the island ends the run and pays the same again.
// It owns rider.py from the beaching on, as the dive does, and re-places his transform after
// BoardController.Step has had its say. Ashore the ride's own speed is nil and the metres come
// from his feet (BoardController.ashore). The island does not bob: it sits at mean water and
// the sea runs up it, and once he is aground he is held to its own surface, read off the mesh
// (isle.glb rasterised once into a 1.5 ft grid), or to the flat 4.00 ft constant when there is
// no file.
using System.Collections.Generic;
using UnityEngine;

public class Island : MonoBehaviour
{
    public BoardController rider;
    public ChaseCamera chase;
    public Spawner spawner;
    public SetWave setWave;
    public Chute chute;            // optional: the two set pieces do not share a lane
    public System.Action<Vector3, int> onSplash;

    // ---- the web's numbers ----
    public const float Half = 6.4f;        // half the island's width at the point: the channels start outside this
    public const float Open = 95f;          // feet of island over which the arms open
    public const float Straight = 70f;      // ...and then this much of it running dead straight
    public const float Len = Open + Straight;   // 165
    public const float SpawnZ = -210f;      // where it is put when armed
    public const float Runup = 1.35f;       // seconds the board takes to run up the sand and stop
    public const float Lane = 17f;          // the lane's own half-width
    public const float Chan = 13f;          // how wide a band outboard of the sand things are put in
    public const float Sweep = 45f;         // how far ahead of him something has to be to be taken off the sand
    public const float Wall = 0.9f;         // how far off the sand the channel holds you
    public const float Splay = 0.30f;       // radians each arm opens by
    public const float YawEase = 1.6f;      // how fast the shot swings onto the new heading
    public const float Walk = 7.4f;         // feet a second on his own legs
    public const float OffV = 14.5f, OffDrag = 9.4f, OffStop = 1.1f;   // the run he arrives on, and where it hands over to standing
    public const float BoardOnSand = 0.16f; // half the board's thickness
    public const float Sand = 4.00f;        // how far the island's crown stands above mean water: the fallback surface
    public const float IsleEnd = 8f;        // how far short of the far end the walk ends
    public const float StepM = 0.34f;       // metres of distance per foot walked up the island
    public const float WalkMin = 0.75f;     // how high above mean water sand has to be before he will stand on it
    public const float Trunk = 2.1f;        // a palm's radius to walk into, at base size
    public const float Cell = 1.5f;         // the height grid
    public const float SignH = 5.6f, SignBury = 0.8f, SignZ = -26.0f, SignOut = 0.92f;
    public const float ChestUp = 0.10f, ChestMin = 2.4f, ChestReach = 5.0f, ChestSpin = 0.75f;
    public const int Palms = 12, PalmsHeavy = 5;
    public const float PalmGap = 26.0f, PalmGapTight = 5.0f, PalmLane = 10.5f, PalmLaneZ = 78f, Shore = 1.15f;
    public const float PalmMin = 0.52f, PalmMax = 1.78f, PalmSink = 5.0f, PalmLean = 0.52f;
    public const float PalmH = 19.7f;       // the title palm's height, which the island's are measured against
    const float ArtY = -0.2f;

    // ---- public state (the HUD, the camera and the ride read these) ----
    public bool On;                // the fork is running (web forkOn)
    public int Ph;                 // 0 choosing, 1 beaching, 2 the channel, 4 running off the board, 3 walking (the web's order)
    public float Z;                // where the island's tip is (web forkZ)
    public float T, Side, Yaw, Run, Off, Face;
    public float walkX, walkY;     // the stick, -1..1: right, and UP the island (away from the water)
    public bool summon;            // debug: arm one now
    public bool Busy => On;                                   // the set wave, the whirlpool and the dive wait
    public bool Ashore => On && (Ph == 3 || Ph == 4);         // on his own legs: the camera's ashore mode
    public bool StickOn => On && Ph == 3;                     // the HUD shows the walk ring

    // ---- private ----
    bool done, signSet, chestSet, chestGot; float spawnT, landX, endZ, pitch, chestSpin, prevZ;
    GameObject art; Transform model; bool proc;
    GameObject sign, chest; Transform palms; readonly List<Vector3> palmAt = new List<Vector3>(); readonly List<float> palmR = new List<float>();
    float[] field; int fnx, fnz; float fx0, fz0; bool fieldTried;
    Transform boardWas; Vector3 boardPos; Quaternion boardRot; bool boardOff;

    // =====================================================================================
    public void Reset()
    {
        Clear();
        if (art != null) art.SetActive(false);
    }

    /// the web's forkClear: everything back the way it was, the island hidden
    void Clear()
    {
        On = false; Ph = 0; T = 0; Side = 0; done = false; spawnT = 0; Run = 0; Off = 0; endZ = 0; Face = 0; pitch = 0;
        chestGot = false; chestSet = false; signSet = false; walkX = 0; walkY = 0; summon = false;
        if (chest != null) chest.SetActive(false);
        if (sign != null) sign.SetActive(false);
        Remount();
        if (art != null) art.SetActive(false);
        if (rider != null) { rider.isleOn = false; rider.ashore = false; rider.isleFace = 0; rider.isleWalk = 0; rider.isleRate = 1f; rider.laneLim = 17f; }
        if (spawner != null) spawner.holdCast = false;
    }

    /// the web's forkArm
    public void Arm()
    {
        var r = rider;
        if (r == null || !r.running || r.wipe || On) return;
        if (chute != null && chute.Busy) return;                // not into the gorge
        Build();
        On = true; done = false; Ph = 0; T = 0; Side = 0; Run = 0; Yaw = 0;
        Z = SpawnZ;
        art.SetActive(true);
        art.transform.position = new Vector3(0, ArtY, Z);
        r.Say("FORK AHEAD - PICK A SIDE");
    }

    // ---- where the sand is, at any point down the island (web forkSandEdge / forkOnSand) ----
    public static float SandEdge(float run)
    {
        return Half * 0.5f + Mathf.Tan(Splay) * Mathf.Min(Open, Mathf.Max(0f, run)) + Half;
    }
    public bool OnSand(float x, float z)
    {
        float run = Z - z;
        if (run < 0 || run > Len) return false;
        return Mathf.Abs(x) <= SandEdge(run);
    }

    /// the island's own height under a world point, NaN where there is no island (web isleH)
    public float IsleH(float x, float z)
    {
        if (field == null || art == null) return float.NaN;
        float lx = x - art.transform.position.x, lz = z - art.transform.position.z;
        float gx = (lx - fx0) / Cell, gz = (lz - fz0) / Cell;
        int cx = Mathf.FloorToInt(gx), cz = Mathf.FloorToInt(gz);
        if (cx < 0 || cz < 0 || cx >= fnx - 1 || cz >= fnz - 1) return float.NaN;
        float tx = gx - cx, tz = gz - cz;
        float a = field[cz * fnx + cx], b = field[cz * fnx + cx + 1], c = field[(cz + 1) * fnx + cx], d = field[(cz + 1) * fnx + cx + 1];
        if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(c) || float.IsNaN(d)) return float.NaN;
        return ((a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz) + art.transform.position.y;
    }

    /// how high the ground is where he stands; the flat constant for the primitive island (web forkGround)
    public float Ground()
    {
        if (rider == null) return Sand;
        float h = IsleH(rider.px, rider.pz);
        return float.IsNaN(h) ? Sand : h;
    }

    /// may he stand here (web forkWalkable): sand above the wash, and not inside a trunk
    bool Walkable(float x, float z)
    {
        if (field == null) return Mathf.Abs(x - landX) <= Half;      // primitive island: the old lane
        float h = IsleH(x, z);
        if (!(h == h && h > WalkMin)) return false;
        if (art != null)
        {
            float lx = x - art.transform.position.x, lz = z - art.transform.position.z;
            for (int i = 0; i < palmAt.Count; i++)
            {
                float dx = lx - palmAt[i].x, dz = lz - palmAt[i].z, rr = palmR[i];
                if (dx * dx + dz * dz < rr * rr) return false;
            }
        }
        return true;
    }

    // =====================================================================================
    public void Step(float dt)
    {
        var r = rider; if (r == null) return;
        if (summon) { summon = false; Arm(); }
        if (!On || art == null)
        {
            if (Yaw != 0f) { Yaw *= Mathf.Max(0f, 1f - dt * 2.2f); if (Mathf.Abs(Yaw) < 0.0005f) Yaw = 0f; }
            return;
        }
        float eff = r.eff;
        // it keeps coming while he is choosing and the whole way down the channel, and stops once he is aground on it
        if (Ph == 0 || Ph == 2) Z += eff * dt;
        art.transform.position = new Vector3(0, ArtY, Z);
        // nothing stands on it: anything inside the sand's band, well ahead of him, goes (web 40308-40313)
        if (spawner != null)
        {
            for (int i = spawner.obstacles.Count - 1; i >= 0; i--)
            {
                var o = spawner.obstacles[i];
                if (o.z > r.pz - Sweep) continue;
                if (!OnSand(o.x, o.z)) continue;
                spawner.Remove(i);
            }
            spawner.holdCast = Ph > 0;                  // the fork casts its own (web 46175)
        }
        if (!signSet) PlaceSign();
        // the lane widens to the island's own edge plus the channel, measured at his position down it (web 44899)
        r.laneLim = Mathf.Max(17f, SandEdge(Z - r.pz) + Wall + Chan + 2f);
        if (Ph == 0)
        {
            // the moment of the choice: when the leading edge reaches him and not before
            if (Z >= r.pz - BoardController.BoardHalfL)
            {
                if (Mathf.Abs(r.px) < Half) { Ph = 1; T = 0; Beach(); }
                else
                {
                    Side = r.px < 0 ? -1f : 1f; Ph = 2; T = 0;
                    r.score += 120; r.Say((Side < 0 ? "LEFT CHANNEL" : "RIGHT CHANNEL") + "! +120"); Sfx.Buzz(12);
                }
            }
        }
        if (Ph == 2) ChannelStep(dt, eff);
        else if (Ph == 1) BeachStep(dt, eff);
        else if (Ph == 4) RunOffStep(dt);
        else if (Ph == 3) WalkStep(dt);
        if (!On) return;                                // cleared this frame
        // what the ride and the rig read off him
        r.isleOn = Ph != 0 && Ph != 2;
        r.ashore = Ph == 3 || Ph == 4;
        r.isleFace = Face;
        if (Ph == 4) { r.isleWalk = 1f; r.isleRate = Mathf.Max(1f, Off / Walk); }
        else if (Ph == 3) { r.isleWalk = Mathf.Sqrt(walkX * walkX + walkY * walkY) > 0.08f ? 1f : 0f; r.isleRate = 1f; }
        else { r.isleWalk = 0f; r.isleRate = 1f; }
        if (r.isleOn)
        {
            // BoardController.Step placed him before py was written, and with the ride's own pitch
            r.transform.position = new Vector3(r.px, r.py, r.pz);
            r.transform.rotation = r.ashore ? Quaternion.Euler(0f, Face * Mathf.Rad2Deg, 0f) : Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
        }
        prevZ = r.pz;
    }

    // ---- the channel (web 40332-40399) ----
    void ChannelStep(float dt, float eff)
    {
        var r = rider;
        T += dt;
        Run += eff * dt;
        float open = Mathf.Min(Open, Run);
        // the wall goes OUTSIDE the sand: the arm's centreline walks out, and the sand reaches HALF beyond it
        float armOff = Half * 0.5f + Mathf.Tan(Splay) * open;
        float inner = Side * (armOff + Half + Wall);
        if (Side < 0 && r.px > inner) { r.px = inner; if (r.vx > 0) r.vx *= -0.25f; }
        if (Side > 0 && r.px < inner) { r.px = inner; if (r.vx < 0) r.vx *= -0.25f; }
        r.transform.position = new Vector3(r.px, r.py, r.pz);
        // the shot goes round with him while the arm is opening, and straightens once it stops
        float wantYaw = (Run < Open) ? Side * Splay : 0f;
        Yaw += (wantYaw - Yaw) * Mathf.Min(1f, dt * YawEase);
        // what is IN the channel you took: left is sharks, right is logs and jellyfish and buoys
        spawnT -= dt;
        if (spawnT <= 0f && spawner != null)
        {
            spawnT = (Side < 0 ? 1.55f : 0.95f) * (0.72f + Random.value * 0.56f);
            float near = SandEdge(Z + 170f) + Wall + 1.0f;      // outboard of the sand at the point they spawn at
            float x = Side * (near + Random.value * Chan);
            string kind = Side < 0 ? "bigfin" : (Random.value < 0.55f ? "log" : (Random.value < 0.6f ? "jelly" : "buoy"));
            var o = spawner.Make(kind, x, -170f);
            if (o != null && kind == "bigfin")
            {
                // a shark in the channel crosses the water you are in: aimed at the wall, not at him
                o.startX = x; o.x = x; o.targetX = Side * near;
                o.dir = o.targetX > x ? 1f : -1f; o.yaw = o.dir * Mathf.PI / 2f;
                if (o.go != null && o.go.transform.childCount > 0) o.go.transform.GetChild(0).localRotation = Quaternion.Euler(0, o.dir > 0 ? 0 : 180f, 0);
                o.go.transform.position = new Vector3(o.x, o.y, o.z);
            }
        }
        if (Z > r.pz + Len + 14f)
        {
            r.score += 400; r.Say("THROUGH THE CHANNEL! +400"); Sfx.Buzz(16);
            Clear();
        }
    }

    // ---- run aground (web forkBeach, 45359-45390) ----
    void Beach()
    {
        var r = rider;
        landX = r.px;
        endZ = Z - Len + IsleEnd;        // the far end of the island: forkZ stops advancing the moment he is aground
        PlaceChest();
        Sfx.Buzz(26);
        onSplash?.Invoke(new Vector3(r.px, WaveField.Height(r.px, WaveField.Scroll + r.pz, r.t), r.pz), 22);
        r.Say("BEACHED!");
    }

    void BeachStep(float dt, float eff)
    {
        var r = rider;
        T += dt;
        float k = Mathf.Min(1f, T / Runup), e = k * k * (3f - 2f * k);
        // the board keeps its line and sheds its speed over its own run up the beach
        r.speed = Mathf.Max(0f, r.speed * (1f - Mathf.Min(1f, dt * 2.6f)));
        r.pz -= (1f - e) * eff * dt * 0.55f;
        // ...and climbs the wedge as it goes, blended off the wave onto the sand under him
        float gh = IsleH(r.px, r.pz);
        float sandY = r.waterY * (1f - e) + e * (float.IsNaN(gh) ? Sand : gh);
        r.py += (sandY - r.py) * Mathf.Min(1f, dt * 7f);
        r.airborne = false; r.vy = 0;
        pitch += ((-0.16f * e) - pitch) * Mathf.Min(1f, dt * 5f);
        if (Random.value < dt * 26f * (1f - e)) onSplash?.Invoke(new Vector3(r.px + (Random.value - 0.5f) * 2.2f, sandY - 0.3f, r.pz + 1.2f), 1);
        if (k >= 1f && !done)
        {
            done = true; Ph = 4; Face = 0; Dismount();
            Off = OffV; r.Say("OFF THE BOARD!");
        }
    }

    // ---- he runs off it (web 45391-45416) ----
    void RunOffStep(float dt)
    {
        var r = rider;
        r.speed = 0; r.vx = 0; r.steer = 0;
        Off = Mathf.Max(0f, Off - OffDrag * dt);
        r.pz -= Off * dt;
        r.py += (Ground() - r.py) * Mathf.Min(1f, dt * 6f);
        r.airborne = false; r.vy = 0;
        ChestStep(dt);
        if (Off <= OffStop) { Off = 0; Ph = 3; r.Say("ON THE ISLAND"); }
    }

    // ---- ashore, on his own legs (web 45417-45449) ----
    void WalkStep(float dt)
    {
        var r = rider;
        r.speed = 0; r.vx = 0; r.steer = 0;
        // the stick, not the steering: up the ring walks him away from the water, which is -z
        float wx = walkX, wz = -walkY;
        wx += ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ? 1f : 0f) - ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ? 1f : 0f);
        wz += ((Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f) - ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f);
        float m = Mathf.Sqrt(wx * wx + wz * wz);
        if (m > 1f) { wx /= m; wz /= m; m = 1f; }
        // anywhere there is sand, the two axes tested separately so he slides along a shore he is pushing into
        float nx = r.px + wx * Walk * dt, nz = r.pz + wz * Walk * dt;
        if (Walkable(nx, r.pz)) r.px = nx;
        if (Walkable(r.px, nz)) r.pz = nz;
        // turned to face the way he is going, eased: 0 is facing away from the lens, the way he arrives
        if (m > 0.12f)
        {
            float want = Mathf.Atan2(-wx, -wz);
            float d = want - Face; while (d > Mathf.PI) d -= Mathf.PI * 2f; while (d < -Mathf.PI) d += Mathf.PI * 2f;
            Face += d * Mathf.Min(1f, dt * 7f);
        }
        r.py += (Ground() - r.py) * Mathf.Min(1f, dt * 6f);
        r.airborne = false; r.vy = 0;
        // the metres come from his feet: up the island earns distance, back down gives it up (web 45183)
        r.dist = Mathf.Max(0f, r.dist + (prevZ - r.pz) * StepM);
        ChestStep(dt);
        if (r.pz < endZ) IslandDone();
    }

    void IslandDone()
    {
        var r = rider;
        int n = 150 + 25 * Random.Range(0, 7);
        r.bonusCoins += n; r.score += 600; Sfx.Buzz(30);
        r.End((chestGot ? "NICE ISLAND! +" : "ISLAND! +") + n + " PUKA");
    }

    // =====================================================================================
    // ---- the board stays where it beached, and he walks away from it (web forkDismount / forkRemount) ----
    void Dismount()
    {
        var b = Hull.Board; var r = rider;
        if (b == null || boardOff || art == null) return;
        boardWas = b.transform.parent; boardPos = b.transform.localPosition; boardRot = b.transform.localRotation; boardOff = true;
        b.transform.SetParent(art.transform, true);
        b.transform.rotation = Quaternion.Euler(0.10f * Mathf.Rad2Deg, (Side * 0.55f + 0.35f) * Mathf.Rad2Deg, 0.16f * Mathf.Rad2Deg);   // left lying, nose across the slope
        float bx = r.px, bz = r.pz + 1.1f, gh = IsleH(bx, bz);
        b.transform.position = new Vector3(bx, float.IsNaN(gh) ? r.py - 1.55f : gh + BoardOnSand, bz);
    }

    void Remount()
    {
        if (!boardOff) return;
        boardOff = false;
        var b = Hull.Board;
        if (b == null || boardWas == null) return;
        b.transform.SetParent(boardWas, false);
        b.transform.localPosition = boardPos; b.transform.localRotation = boardRot;
    }

    // =====================================================================================
    // ---- the island itself: isle.glb dressed in the beach's sand, or the slab island without it ----
    void Build()
    {
        if (art != null) return;
        art = new GameObject("Island");               // built active: the fits below read renderer bounds
        var holder = new GameObject("isle").transform; holder.SetParent(art.transform, false);
        var go = Models.Spawn("isle", holder, 4f, PrimitiveType.Cube);
        proc = go.name.EndsWith("(stand-in)");
        if (!proc)
        {
            // the file is in feet with the tip at its origin, running back to -165: Spawn's fit is undone
            var prefab = Resources.Load<GameObject>("Models/isle");
            go.transform.localScale = prefab != null ? prefab.transform.localScale : Vector3.one;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            model = go.transform;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var sandMat = new Material(lit != null ? lit : Shader.Find("Standard"));
            sandMat.SetColor("_BaseColor", new Color(0.400f, 0.310f, 0.178f).gamma);   // the beach map's own mean, in linear (web ISLE_SAND)
            sandMat.SetFloat("_Smoothness", 0.08f); sandMat.SetFloat("_Metallic", 0f); sandMat.SetFloat("_Cull", 0f);
            foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
            {
                rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rd.receiveShadows = true;
                var ms = rd.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = sandMat; rd.sharedMaterials = ms;
            }
            Orient(go);
            Raster(go);
            PlantPalms();
        }
        else
        {
            Destroy(go);
            BuildSlabs(holder);
        }
        art.SetActive(false);
    }

    // the tip faces the rider (+z) and the body runs away to -z: the file is built that way, and
    // the importer keeps z, but it is measured rather than assumed. The narrow end is the tip.
    void Orient(GameObject go)
    {
        var b = Models.Bounds(go);
        if (b.size.z < 1f) return;
        float wNear = WidthAt(go, b.max.z - 6f), wFar = WidthAt(go, b.min.z + 6f);
        if (wNear > wFar) go.transform.localRotation = Quaternion.Euler(0, 180f, 0) * go.transform.localRotation;
        b = Models.Bounds(go);
        go.transform.localPosition -= new Vector3(0f, 0f, b.max.z);          // the tip at the holder's origin
    }

    float WidthAt(GameObject go, float z)
    {
        float lo = 1e9f, hi = -1e9f;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh; if (mesh == null) continue;
            Vector3[] v; try { v = mesh.vertices; } catch { continue; }
            foreach (var p in v)
            {
                var w = art.transform.InverseTransformPoint(mf.transform.TransformPoint(p));
                if (Mathf.Abs(w.z - z) > 3f) continue;
                if (w.x < lo) lo = w.x; if (w.x > hi) hi = w.x;
            }
        }
        return hi > lo ? hi - lo : 0f;
    }

    // the island's own height as a grid (web isleField): rasterised once out of the mesh's
    // triangles, the highest surface in each cell, NaN where there is no sand at all
    void Raster(GameObject go)
    {
        if (fieldTried) return; fieldTried = true;
        float x0 = 1e9f, x1 = -1e9f, z0 = 1e9f, z1 = -1e9f;
        var tris = new List<Vector3[]>();
        try
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh; if (mesh == null) continue;
                var v = mesh.vertices; var idx = mesh.triangles;
                var w = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    w[i] = art.transform.InverseTransformPoint(mf.transform.TransformPoint(v[i]));
                    if (w[i].x < x0) x0 = w[i].x; if (w[i].x > x1) x1 = w[i].x; if (w[i].z < z0) z0 = w[i].z; if (w[i].z > z1) z1 = w[i].z;
                }
                for (int t = 0; t + 2 < idx.Length; t += 3) tris.Add(new[] { w[idx[t]], w[idx[t + 1]], w[idx[t + 2]] });
            }
        }
        catch (System.Exception) { field = null; return; }       // a mesh the build left unreadable: the flat constant stands in
        if (tris.Count == 0) return;
        int nx = Mathf.CeilToInt((x1 - x0) / Cell) + 2, nz = Mathf.CeilToInt((z1 - z0) / Cell) + 2;
        if (nx < 2 || nz < 2 || nx * nz > 120000) return;
        var H = new float[nx * nz]; for (int i = 0; i < H.Length; i++) H[i] = float.NaN;
        foreach (var tr in tris)
        {
            float ax0 = tr[0].x, ax1 = tr[1].x, ax2 = tr[2].x, az0 = tr[0].z, az1 = tr[1].z, az2 = tr[2].z;
            int cx0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ax0, ax1, ax2) - x0) / Cell)), cx1 = Mathf.Min(nx - 1, Mathf.CeilToInt((Mathf.Max(ax0, ax1, ax2) - x0) / Cell));
            int cz0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(az0, az1, az2) - z0) / Cell)), cz1 = Mathf.Min(nz - 1, Mathf.CeilToInt((Mathf.Max(az0, az1, az2) - z0) / Cell));
            float d = (az1 - az2) * (ax0 - ax2) + (ax2 - ax1) * (az0 - az2);
            if (Mathf.Abs(d) < 1e-9f) continue;
            for (int cx = cx0; cx <= cx1; cx++) for (int cz = cz0; cz <= cz1; cz++)
            {
                float X = x0 + cx * Cell, Zc = z0 + cz * Cell;
                float w0 = ((az1 - az2) * (X - ax2) + (ax2 - ax1) * (Zc - az2)) / d;
                float w1 = ((az2 - az0) * (X - ax2) + (ax0 - ax2) * (Zc - az2)) / d;
                float w2 = 1 - w0 - w1;
                if (w0 < -0.001f || w1 < -0.001f || w2 < -0.001f) continue;
                float y = w0 * tr[0].y + w1 * tr[1].y + w2 * tr[2].y; int k = cz * nx + cx;
                if (!(H[k] >= y)) H[k] = y;                        // NaN answers false, so the first hit wins, then the highest
            }
        }
        field = H; fnx = nx; fnz = nz; fx0 = x0; fz0 = z0;
    }

    // the web's primitive island: eighteen slabs stepped outward with rounded shoulders, a wet
    // rim band, and fifty-two humps of scrub along the spine (web makeForkIsland)
    void BuildSlabs(Transform holder)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit"); if (lit == null) lit = Shader.Find("Standard");
        var sand = new Material(lit); sand.SetColor("_BaseColor", new Color(0xe4 / 255f, 0xcf / 255f, 0x9e / 255f)); sand.SetFloat("_Smoothness", 0.07f); sand.SetFloat("_Metallic", 0f);
        var wet = new Material(lit); wet.SetColor("_BaseColor", new Color(0xcb / 255f, 0xb1 / 255f, 0x82 / 255f)); wet.SetFloat("_Smoothness", 0.20f); wet.SetFloat("_Metallic", 0f);
        var scrub = new Material(lit); scrub.SetColor("_BaseColor", new Color(0x5f / 255f, 0x7a / 255f, 0x4a / 255f)); scrub.SetFloat("_Smoothness", 0.08f);
        GameObject Prim(PrimitiveType t, Material m, Vector3 p, Vector3 s)
        {
            var g = GameObject.CreatePrimitive(t); var c = g.GetComponent<Collider>(); if (c != null) Destroy(c);
            g.transform.SetParent(holder, false); g.transform.localPosition = p; g.transform.localScale = s;
            var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }
        const int N = 18; float seg = (Open + Straight) / N;
        for (int i = 0; i < N; i++)
        {
            float zz = -i * seg, run = Mathf.Min(Open, i * seg);
            float e = Half * 0.5f + Mathf.Tan(Splay) * run + Half * Mathf.Min(1f, (i + 1) / 3f);
            Prim(PrimitiveType.Cube, sand, new Vector3(0, -0.6f, zz - seg * 0.5f), new Vector3(e * 2f, 3.4f, seg * 1.06f));
            Prim(PrimitiveType.Sphere, sand, new Vector3(0, 0.35f, zz - seg * 0.5f), new Vector3(e * 0.9f * 2f, 1.3f * 2f, seg * 0.6f * 2f));
            Prim(PrimitiveType.Cube, wet, new Vector3(0, -1.5f, zz - seg * 0.5f), new Vector3(e * 2.12f, 1.5f, seg * 1.06f));
        }
        for (int i = 0; i < 52; i++)
        {
            float run = Random.value * (Open + Straight);
            float e = Half * 0.5f + Mathf.Tan(Splay) * Mathf.Min(Open, run) + Half;
            float rr = 0.34f + Random.value * 0.34f;
            Prim(PrimitiveType.Sphere, scrub, new Vector3((Random.value * 2f - 1f) * e * 0.85f, 0.55f + Random.value * 0.4f, -run), new Vector3(rr * 2f, rr * 2f * (1.4f + Random.value * 0.5f), rr * 2f));
        }
    }

    // ---- palms, thrown at the island and dropped onto the sand (web forkDress) ----
    void PlantPalms()
    {
        if (palms != null || field == null) return;
        bool light = Resources.Load<GameObject>("Models/palmlod") != null;
        bool heavy = !light && Resources.Load<GameObject>("Models/palm2") != null;
        if (!light && !heavy) return;
        palms = new GameObject("palms").transform; palms.SetParent(art.transform, false);
        int want = light ? Palms : PalmsHeavy;
        float bx0 = fx0, bx1 = fx0 + (fnx - 1) * Cell, bz0 = fz0, bz1 = fz0 + (fnz - 1) * Cell;
        float top = -1e9f; foreach (var h in field) if (!float.IsNaN(h) && h > top) top = h;
        var put = new List<Vector2>();
        for (int tries = 0; tries < 460 && put.Count < want; tries++)
        {
            float lx = bx0 + Random.value * (bx1 - bx0), lz = bz0 + Random.value * (bz1 - bz0);
            // the lane he lands in and walks up, kept clear
            if (Mathf.Abs(lx) < PalmLane && lz > -PalmLaneZ) continue;
            float gr = Random.value, gap = PalmGapTight + (PalmGap - PalmGapTight) * gr * gr * gr;
            bool near = false; foreach (var q in put) if ((q - new Vector2(lx, lz)).magnitude < gap) { near = true; break; }
            if (near) continue;
            float h = IsleH(lx + art.transform.position.x, lz + art.transform.position.z) - art.transform.position.y;
            if (float.IsNaN(h)) continue;                 // over water
            if (h < top - Shore) continue;                // down the wet face of the beach
            float c0 = Random.value, small = 1f - c0;
            float k = PalmMin + c0 * (PalmMax - PalmMin);
            var go = Models.Spawn(light ? "palmlod" : "palm2", palms, PalmH * k, PrimitiveType.Cylinder);
            if (go.name.EndsWith("(stand-in)")) { Destroy(go); break; }
            foreach (var rd in go.GetComponentsInChildren<Renderer>(true)) { rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rd.receiveShadows = false; }
            float sink = (0.25f + 0.75f * small * small) * PalmSink;
            float lw = Random.value, lean = PalmLean * (0.10f + 0.90f * lw * lw), dir = Random.value * 6.283f;
            var wrap = new GameObject("palm").transform; wrap.SetParent(palms, false);
            go.transform.SetParent(wrap, false);
            wrap.localPosition = new Vector3(lx, h - sink, lz);
            wrap.localRotation = Quaternion.Euler(Mathf.Cos(dir) * lean * Mathf.Rad2Deg, Random.value * 360f, Mathf.Sin(dir) * lean * Mathf.Rad2Deg);
            put.Add(new Vector2(lx, lz)); palmAt.Add(new Vector3(lx, 0, lz)); palmR.Add(Trunk * k);
        }
    }

    // ---- the signpost at the head of the island, out along the left shore (web forkPlaceSign) ----
    void PlaceSign()
    {
        if (signSet || art == null || field == null) return;
        if (Resources.Load<GameObject>("Models/forksign") == null) { signSet = true; return; }
        float lz = SignZ;
        for (int k = 0; k < 8; k++)
        {
            float lx = -SandEdge(-lz) * (SignOut - k * 0.07f);
            float h = IsleH(lx + art.transform.position.x, lz + art.transform.position.z);
            if (float.IsNaN(h) || h < WalkMin) continue;
            if (sign == null)
            {
                sign = new GameObject("forksign"); sign.transform.SetParent(art.transform, false);
                var g = Models.Spawn("forksign", sign.transform, SignH, PrimitiveType.Cube);   // sized by measurement; its foot at y=0
                foreach (var rd in g.GetComponentsInChildren<Renderer>(true)) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            sign.transform.localPosition = new Vector3(lx, h - art.transform.position.y - SignBury, lz);
            sign.transform.localRotation = Quaternion.Euler(0, 0.26f * Mathf.Rad2Deg, 0);   // a little off square, so it reads as planted
            sign.SetActive(true); signSet = true;
            return;
        }
    }

    // ---- a chest, bolted to the island up the sand ahead of where he lands (web forkPlaceChest) ----
    void PlaceChest()
    {
        if (chestSet || art == null || field == null) return;
        var r = rider;
        if (chest == null)
        {
            chest = new GameObject("chest"); chest.transform.SetParent(art.transform, false);
            var g = Models.Spawn("chest", chest.transform, 1.6f * 0.9f, PrimitiveType.Cube);
            foreach (var rd in g.GetComponentsInChildren<Renderer>(true)) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        for (int i = 0; i < 40; i++)
        {
            float wz = r.pz - (20f + Random.value * 30f);
            float wx = r.px + (Random.value < 0.5f ? -1f : 1f) * (5f + Random.value * 14f);
            float h = IsleH(wx, wz);
            if (float.IsNaN(h) || h < ChestMin) continue;
            chest.transform.position = new Vector3(wx, h + ChestUp, wz);
            chestGot = false; chest.SetActive(true); chestSpin = 0; chestSet = true;
            return;
        }
        chest.SetActive(false);
    }

    void ChestStep(float dt)
    {
        if (!chestSet) PlaceChest();
        if (!signSet) PlaceSign();
        if (chest == null || !chest.activeSelf || chestGot) return;
        var r = rider;
        chestSpin += dt * ChestSpin;
        chest.transform.rotation = Quaternion.Euler(0, chestSpin * Mathf.Rad2Deg, 0);
        var p = chest.transform.position;
        float dx = r.px - p.x, dz = r.pz - p.z;
        if (Mathf.Sqrt(dx * dx + dz * dz) < ChestReach)
        {
            chestGot = true; chest.SetActive(false);
            int n = 150 + 25 * Random.Range(0, 7);
            r.bonusCoins += n; r.score += 600; Sfx.Buzz(30);
            r.Say("CHEST! +" + n + " PUKA");
        }
    }
}
