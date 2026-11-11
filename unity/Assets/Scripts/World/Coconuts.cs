// Coconuts in the water, as the web floats them: a dark ball bobbing on the swell every few
// seconds past 60 m, caught by riding through it. Three close the circuit and the charge runs
// for twenty seconds. What the charge does is SHRINK you: the ground speed is untouched, so
// you cover the same distance, but at a third of your height the same water goes past three
// times faster to look at, and a gap that was a squeeze becomes one you can steer through.
// Eased in and out, never snapped. The nut is the beach's husk sphere, not the web's 5 MB file.
using UnityEngine;

public class Coconuts : MonoBehaviour
{
    public BoardController rider; public SetWave setWave;
    public System.Action<Vector3, int> onSplash;
    public const int Max = 3;               // nuts to a charge
    public const float Hold = 20f;          // seconds the charge runs once it closes
    public const float Shrink = 0.38f;      // how small, as a share of full size
    public const float Ease = 2.6f;         // how fast it gets there and back, per second

    public int got; public float power;     // nuts in the meter; seconds of charge left
    public float K = 1f;                    // where the size ease has got to (the rider's scale)
    public static float SizeK = 1f;         // the same, for the board's footprint in the spawner
    public float peek;                      // seconds the meter plaque should stay up (the HUD reads it)
    public int popped = -1;                 // the nut that just lit, for the HUD's pop; -1 none

    class Nut { public Transform t; public bool on; public float x, z; }
    readonly System.Collections.Generic.List<Nut> nuts = new System.Collections.Generic.List<Nut>();
    Material mat; float timer, bob;

    void Awake() { timer = 4f + Random.value * 5f; }

    public void Reset()
    {
        got = 0; power = 0; timer = 4f + Random.value * 5f; K = 1f; SizeK = 1f; peek = 0; popped = -1;
        foreach (var n in nuts) { n.on = false; n.t.gameObject.SetActive(false); }
    }

    Material Mat()
    {
        if (mat != null) return mat;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(lit != null ? lit : Shader.Find("Universal Render Pipeline/Unlit")); mat.name = "coconut";
        if (lit != null)
        {
            Beach.CoconutTextures(out var baseTex, out var nrmTex);
            mat.SetTexture("_BaseMap", baseTex); mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", 0.24f);
            mat.EnableKeyword("_NORMALMAP"); mat.SetTexture("_BumpMap", nrmTex); mat.SetFloat("_BumpScale", 1.2f);
        }
        return mat;
    }

    Nut Spawn()
    {
        Nut n = null;
        foreach (var m in nuts) if (!m.on) { n = m; break; }
        if (n == null)
        {
            if (nuts.Count >= 3) return null;            // three in the water at once is plenty
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Coconut";
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat();
            go.transform.localScale = new Vector3(1.0f, 1.17f, 1.0f);   // about a foot across, an egg not a ball
            n = new Nut { t = go.transform }; nuts.Add(n);
        }
        n.on = true; n.t.gameObject.SetActive(true);
        n.x = Mathf.Clamp(rider.px + (Random.value - 0.5f) * 15f, -13f, 13f); n.z = -165f;
        n.t.rotation = Quaternion.Euler(Random.value * 172f, Random.value * 172f, Random.value * 172f);
        return n;
    }

    public void Step(float dt)
    {
        var r = rider; float eff = r.eff;
        bob += dt;
        if (power > 0)
        {
            power -= dt;
            if (power <= 0) { power = 0; got = 0; r.Say("POWER DOWN"); }
            peek = 0.5f;
        }
        else if (peek > 0) peek -= dt;
        // the size, eased toward wherever the charge says
        float wantK = power > 0 ? Shrink : 1f;
        if (Mathf.Abs(K - wantK) > 1e-4f)
        {
            K += (wantK - K) * Mathf.Min(1f, dt * Ease);
            if (Mathf.Abs(K - wantK) < 0.004f) K = wantK;
        }
        SizeK = K;
        // no new nuts while a charge is running: the meter is full and has nowhere to put them
        if (r.running && (setWave == null || setWave.ph == SetWave.Phase.Idle) && r.dist > 60f && power <= 0)
        {
            timer -= dt;
            if (timer <= 0) { timer = 5f + Random.value * 6f; Spawn(); }
        }
        popped = -1;
        foreach (var n in nuts)
        {
            if (!n.on) continue;
            n.z += eff * dt;
            float y = WaveField.Swell(n.x, WaveField.Scroll + n.z, r.t) + 0.95f + Mathf.Sin(bob * 2.1f + n.x) * 0.16f;
            n.t.position = new Vector3(n.x, y, n.z);
            n.t.rotation = Quaternion.Euler(dt * 0.6f * Mathf.Rad2Deg, dt * 1.4f * Mathf.Rad2Deg, 0) * n.t.rotation;
            if (n.z > 14f) { n.on = false; n.t.gameObject.SetActive(false); continue; }
            if (r.running && Mathf.Abs(n.z - r.pz) < 2.2f && Mathf.Abs(n.x - r.px) < 2.2f && Mathf.Abs(r.py - y) < 2.0f)
            {
                n.on = false; n.t.gameObject.SetActive(false);
                got = Mathf.Min(Max, got + 1);
                r.score += 25; Sfx.Buzz(14);
                onSplash?.Invoke(new Vector3(r.px, r.py + 0.3f, r.pz), 9);
                popped = got - 1; peek = 2.6f;
                if (got >= Max) { power = Hold; r.Say("POWER UP ACTIVATED!"); Sfx.Buzz(30); }
                else r.Say("COCONUT " + got + "/" + Max);
            }
        }
    }
}
