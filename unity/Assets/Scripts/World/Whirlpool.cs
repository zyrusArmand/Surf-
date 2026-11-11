// The whirlpool: a dip in the water itself (WaveField.WhirlDip, which the sea shader carries
// too), a spinning spiral drawn on the surface, obstacles dragged round and over the edge, and
// a pull on the rider that ends with him sucked under unless he is in the air or out of reach.
// An escape ramp and a jellyfish are laid either side of it, as the web game lays them.
using UnityEngine;

public class Whirlpool : MonoBehaviour
{
    public BoardController rider; public Spawner spawner; public SetWave setWave; public Dive dive;
    public System.Action<Vector3, int> onSplash;
    public const float R = 20f, Gap = 62f, GapR = 78f, AfterSet = 17f;
    public bool summon;

    float timer; bool on; float x, z, spin; bool hit;
    Mesh art; GameObject artGo; Material artMat; float spinA;
    Vector3[] artBase;

    void Awake() { timer = 34f + Random.value * 40f; }

    public void Reset()
    {
        on = false; WaveField.WhA = 0; timer = 34f + Random.value * 40f;
        if (artGo != null) artGo.SetActive(false);
    }

    void Build()
    {
        if (artGo != null) return;
        const int rings = 12, seg = 40;
        var v = new Vector3[(rings + 1) * (seg + 1)]; var uv = new Vector2[v.Length]; var idx = new int[rings * seg * 6]; int n = 0;
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings, r = R * t;
            for (int j = 0; j <= seg; j++)
            {
                float a = j / (float)seg * Mathf.PI * 2f;
                v[i * (seg + 1) + j] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                uv[i * (seg + 1) + j] = new Vector2(0.5f + Mathf.Cos(a) * t * 0.5f, 0.5f + Mathf.Sin(a) * t * 0.5f);
            }
        }
        for (int i = 0; i < rings; i++) for (int j = 0; j < seg; j++)
        {
            int A = i * (seg + 1) + j, B = A + 1, C = A + seg + 1, D = C + 1;
            idx[n++] = A; idx[n++] = B; idx[n++] = C; idx[n++] = B; idx[n++] = D; idx[n++] = C;
        }
        art = new Mesh { name = "whirl" }; art.vertices = v; art.uv = uv; art.triangles = idx; art.RecalculateBounds();
        art.MarkDynamic(); artBase = v;
        artGo = new GameObject("WhirlArt", typeof(MeshFilter), typeof(MeshRenderer));
        artGo.GetComponent<MeshFilter>().sharedMesh = art;
        var sh = Shader.Find("Surf/Whirl");
        artMat = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit"));
        artMat.SetTexture("_MainTex", SpiralTexture());
        artGo.GetComponent<MeshRenderer>().sharedMaterial = artMat;
        artGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        artGo.SetActive(false);
    }

    // streaks of foam wound into a spiral, dark water between them, drawn once
    static Texture2D SpiralTexture()
    {
        const int S = 512; var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int xx = 0; xx < S; xx++)
        {
            float u = (xx + 0.5f) / S - 0.5f, w = (y + 0.5f) / S - 0.5f;
            float r = Mathf.Sqrt(u * u + w * w) * 2f, a = Mathf.Atan2(w, u);
            float wind = a + 3.2f * Mathf.Log(Mathf.Max(0.05f, r));
            float s1 = Mathf.Sin(wind * 3f) * 0.5f + 0.5f, s2 = Mathf.Sin(wind * 7f + 1.3f) * 0.5f + 0.5f;
            float foam = Mathf.SmoothStep(0.55f, 0.95f, s1) * 0.75f + Mathf.SmoothStep(0.7f, 0.98f, s2) * 0.35f;
            float edge = 1f - Mathf.SmoothStep(0.82f, 1.0f, r);
            float eye = Mathf.SmoothStep(0.0f, 0.18f, r);
            float dark = (1f - eye) * 0.85f;
            var c = Color.Lerp(new Color(0.02f, 0.10f, 0.16f), Color.white, foam * eye);
            px[y * S + xx] = new Color(c.r, c.g, c.b, Mathf.Clamp01((foam * 0.9f + 0.25f + dark) * edge));
        }
        tex.SetPixels(px); tex.Apply(true, false); return tex;
    }

    public void Step(float dt)
    {
        var r = rider; float eff = r.eff;
        if (!on)
        {
            timer -= dt;
            if (setWave != null && setWave.ph != SetWave.Phase.Idle) timer = Mathf.Max(timer, AfterSet);
            if (((timer <= 0f && r.dist > 700f) || summon) && (dive == null || !dive.Busy)) { timer = Gap + Random.value * GapR; summon = false; Spawn(); }
            if (WaveField.WhA > 0) { WaveField.WhA = Mathf.Max(0f, WaveField.WhA - dt * 5f); if (artGo != null) artGo.transform.localScale = new Vector3(1, Mathf.Max(0.001f, WaveField.WhA), 1); }
            if (WaveField.WhA <= 0 && artGo != null) artGo.SetActive(false);
            return;
        }
        z += eff * dt;
        float grow = Mathf.Clamp01((z + 100f) / 56f), fade = Mathf.Clamp01((24f - z) / 14f);
        WaveField.WhA = 7.6f * grow * fade; WaveField.WhX = x; WaveField.WhZ = WaveField.Scroll + z;
        artGo.SetActive(WaveField.WhA > 0.02f);
        artGo.transform.position = new Vector3(x, 0, z); artGo.transform.localScale = Vector3.one;
        if (artGo.activeSelf)
        {
            var v = art.vertices;
            for (int i = 0; i < v.Length; i++) v[i].y = WaveField.Height(x + artBase[i].x, WaveField.WhZ + artBase[i].z, r.t) + 0.07f;
            art.vertices = v; art.RecalculateBounds();
            spinA -= spin * dt * (0.85f + WaveField.WhA * 0.10f);
            artMat.SetFloat("_Spin", spinA); artMat.SetFloat("_Alpha", Mathf.Min(0.96f, WaveField.WhA * 0.22f));
        }
        // obstacles are dragged round and in; the escape pair holds its ground
        for (int i = spawner.obstacles.Count - 1; i >= 0; i--)
        {
            var o = spawner.obstacles[i];
            float ox = o.x - x, oz = o.z - z, rr = Mathf.Sqrt(ox * ox + oz * oz);
            if (rr > R * 1.35f) continue;
            bool held = o.escape;
            float grip = 0.40f + 0.60f * (1f - Mathf.Min(1f, rr / R));
            float a = Mathf.Atan2(oz, ox) + spin * (held ? 0.11f : 1.85f) * grip * dt;
            float nr = held ? rr : Mathf.Max(0.5f, rr - grip * 3.1f * dt);
            o.x = x + Mathf.Cos(a) * nr; o.z = z + Mathf.Sin(a) * nr;
            if (!held && nr <= 1.9f) { onSplash?.Invoke(new Vector3(o.x, o.y, o.z), 14); spawner.Remove(i); }
        }
        float dxp = x - r.px, dzp = z - r.pz, d = Mathf.Sqrt(dxp * dxp + dzp * dzp);
        if (d < R * 1.5f && r.running)
        {
            float pull = 1f - d / (R * 1.5f), inv = 1f / Mathf.Max(0.5f, d);
            r.vx += (dxp * inv) * pull * pull * 30f * dt;
            r.vx += (-dzp * inv) * spin * pull * 15f * dt;
            if (Random.value < dt * 10f) onSplash?.Invoke(new Vector3(r.px + (Random.value - 0.5f) * 2.2f, r.py - 0.15f, r.pz + 1.2f), 1);
        }
        if (!hit && d < R * 0.32f && !r.airborne && r.running) { hit = true; r.Wipeout("foam"); r.Say("SUCKED UNDER!"); return; }
        if (z > 24f) on = false;
    }

    void Spawn()
    {
        Build();
        var r = rider;
        x = Mathf.Clamp(r.px + (Random.value - 0.5f) * 7f, -4f, 4f); z = -112f; spin = Random.value < 0.5f ? -1f : 1f; hit = false; on = true;
        r.Say("WHIRLPOOL!"); Sfx.Buzz(20, 30, 20);
        float side = spin > 0 ? 1f : -1f;
        var a = spawner.Make("ramp", Mathf.Clamp(x - side * R * 0.62f, -15.5f, 15.5f), z + 15f); if (a != null) a.escape = true;
        var b = spawner.Make("jelly", Mathf.Clamp(x + side * R * 0.58f, -15.5f, 15.5f), z + 23f); if (b != null) b.escape = true;
    }
}
