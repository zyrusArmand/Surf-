// The treasure chest: one a run, floating down the lane past 120 m, spinning so it reads as a
// chest and not a box, bobbing on the swell with a glow under it. Ride through it to take it;
// it pays out in puka on the spot (the web's roll: mostly 25 or 50, sometimes 100 or 200+).
// Boards and riders from the chest come with the shop.
using UnityEngine;

public class Crate : MonoBehaviour
{
    public BoardController rider; public SetWave setWave;
    public System.Action<Vector3, int> onSplash;
    bool on, held; float timer = 999f, bob; Transform obj; Light glow;

    float Luck => rider != null ? Mathf.Max(0.5f, rider.statLuck) : 1f;   // a lucky rider's chests come oftener
    public void Soon() { timer = Mathf.Min(timer, 1.5f); }      // the wheel's chest prize: one straight away
    public void Reset() { on = false; held = false; timer = (9f + Random.value * 11f) / Luck; if (obj != null) obj.gameObject.SetActive(false); }

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
            if (!held && r.running && (setWave == null || setWave.ph == SetWave.Phase.Idle) && r.dist > 120f)
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
        if (p.z > 14f) { on = false; obj.gameObject.SetActive(false); timer = (8f + Random.value * 11f) / Luck; return; }
        if (r.running && Mathf.Abs(p.z - r.pz) < 2.2f && Mathf.Abs(p.x - r.px) < 2.2f && Mathf.Abs(r.py - p.y) < 2.0f)
        {
            on = false; held = true; obj.gameObject.SetActive(false);
            float q = Random.value;
            int n = q < 0.10f ? 200 + 25 * Random.Range(0, 7) : q < 0.32f ? 100 : q < 0.64f ? 50 : 25;
            r.coins += n;
            r.Say("TREASURE CHEST! +" + n + " PUKA"); Sfx.Buzz(25);
            onSplash?.Invoke(new Vector3(r.px, r.py + 0.3f, r.pz), 14);
        }
    }
}
