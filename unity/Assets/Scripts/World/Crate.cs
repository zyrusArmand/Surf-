// The treasure chest: one a run, floating down the lane past 120 m, spinning so it reads as a
// chest and not a box, bobbing on the swell with a glow under it. Ride through it to take it.
// Taking it only says so and keeps it (web 26690-26696, crateHeld): it is opened after the run,
// before the card, in the ceremony (UI/ChestCeremony.cs), and what is inside is rolled then.
using UnityEngine;

public class Crate : MonoBehaviour
{
    public BoardController rider; public SetWave setWave; public Island island;   // no chest drifting up the sand
    public System.Action<Vector3, int> onSplash;
    public ChestCeremony ceremony;                                                 // where a caught chest is opened
    bool on, held; float timer = 999f, bob; Transform obj; Light glow;

    float Luck => rider != null ? Mathf.Max(0.5f, rider.statLuck) : 1f;   // a lucky rider's chests come oftener
    public void Soon() { timer = Mathf.Min(timer, 1.5f); }      // the wheel's chest prize: one straight away
    /// a new run or the title sweeps away a ceremony that may still be up, and shows no card for it (web crateCeremonyEnd from beginRun)
    public void Reset() { on = false; held = false; timer = (9f + Random.value * 11f) / Luck; if (obj != null) obj.gameObject.SetActive(false); if (ceremony != null) ceremony.End(); }

    /// the run is over: a chest in hand is opened now, and `then` (the run card) waits until the
    /// player carries on from it. False when there is no chest, and the card goes straight up.
    public bool Open(System.Action then)
    {
        if (!held || ceremony == null) return false;
        held = false;                                            // web crateCeremony: crateHeld = false
        ceremony.Play(then);
        return true;
    }

    void Build()
    {
        if (obj != null) return;
        var holder = new GameObject("Crate"); obj = holder.transform;
        var g = Models.Spawn("chest", obj, 1.6f, PrimitiveType.Cube);
        g.transform.localPosition -= new Vector3(0, 0.8f, 0);      // spin about its own middle
        glow = holder.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1f, 0.78f, 0.32f); glow.range = 7f; glow.intensity = 2.2f;
        holder.SetActive(false);
    }

    public void Step(float dt)
    {
        var r = rider; float eff = r.eff;
        bob += dt;
        if (!on)
        {
            if (!held && r.running && (setWave == null || setWave.ph == SetWave.Phase.Idle) && r.dist > 120f && (island == null || !island.On))
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    Build(); on = true; obj.gameObject.SetActive(true);
                    obj.position = new Vector3(Mathf.Clamp(r.px + (Random.value - 0.5f) * 16f, -14f, 14f), 0, -170f);
                }
            }
            return;
        }
        var p = obj.position; p.z += eff * dt;
        float sea = WaveField.Swell(p.x, WaveField.Scroll + p.z, r.t);
        p.y = sea + 1.25f + Mathf.Sin(bob * 1.7f) * 0.20f;
        obj.position = p;
        obj.rotation = Quaternion.Euler(0, obj.rotation.eulerAngles.y + dt * 1.7f * Mathf.Rad2Deg, Mathf.Sin(bob * 1.3f) * 0.06f * Mathf.Rad2Deg);
        glow.intensity = 1.6f + 0.8f * Mathf.Sin(bob * 2.6f);
        if (p.z > 14f) { on = false; obj.gameObject.SetActive(false); timer = 8f + Random.value * 11f; return; }   // a miss re-arms at 8-19 s, luck or not (web 26685)
        if (r.running && Mathf.Abs(p.z - r.pz) < 2.2f && Mathf.Abs(p.x - r.px) < 2.2f && Mathf.Abs(r.py - p.y) < 2.0f)
        {
            on = false; held = true; obj.gameObject.SetActive(false);
            // nothing is paid here: the chest is kept, and opened after the run (the line also
            // ticks the chest goal and the crates counter, Quests.Scan)
            r.Say("TREASURE CHEST!"); Sfx.Buzz(25);
            onSplash?.Invoke(new Vector3(r.px, r.py + 0.3f, r.pz), 14);
        }
    }
}
