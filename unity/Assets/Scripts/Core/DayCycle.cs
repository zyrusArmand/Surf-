// Time of day, paced by distance the way the web game paces it: dawn through noon into sunset
// and a short night, then round again. One table drives the sky dome, the sun light, the
// ocean's reflected colours and the spray's tint, so nothing in the frame disagrees.
using UnityEngine;

public class DayCycle : MonoBehaviour
{
    struct Key { public float t; public Color top, mid, hz, glow; public float gAmt, sunX, sunY; }

    static Color C(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
    static Key K(float t, uint top, uint mid, uint hz, uint glow, float g, float sx, float sy)
        => new Key { t = t, top = C(top), mid = C(mid), hz = C(hz), glow = C(glow), gAmt = g, sunX = sx, sunY = sy };

    static readonly Key[] Keys = {
        K(0.00f, 0x3a3f6e, 0x8f7f9e, 0xd7b6b8, 0xe0ab8c, 0.26f, -0.66f, -0.205f),
        K(0.05f, 0x4a5480, 0xac96a8, 0xf0c9a8, 0xffb877, 0.40f, -0.63f, -0.088f),
        K(0.11f, 0x3f6ea8, 0xc7a98f, 0xffcf8a, 0xffc061, 0.62f, -0.58f,  0.038f),
        K(0.20f, 0x357ec0, 0xbfc9d8, 0xffdda8, 0xffd489, 0.44f, -0.47f,  0.190f),
        K(0.34f, 0x3a86cc, 0xc3d3ea, 0xf2f0e4, 0xfff2d6, 0.22f, -0.30f,  0.520f),
        K(0.50f, 0x3f8fd6, 0x8fd2f5, 0xe6f4fd, 0xfff8ea, 0.09f, -0.03f,  0.930f),
        K(0.66f, 0x4088cf, 0xa9d6ef, 0xffe6bd, 0xffd287, 0.22f,  0.26f,  0.520f),
        K(0.80f, 0x3d6fb4, 0xc9a6c0, 0xffa863, 0xff8f3c, 0.46f,  0.45f,  0.190f),
        K(0.90f, 0x2b3f80, 0xa85f86, 0xff6a3a, 0xff4f18, 0.66f,  0.57f,  0.040f),
        K(0.96f, 0x162452, 0x5a3168, 0xa8383a, 0xd8381a, 0.46f,  0.63f, -0.085f),
        K(1.00f, 0x060a1c, 0x121a38, 0x33203f, 0x6a2418, 0.16f,  0.68f, -0.215f),
        K(1.08f, 0x040814, 0x0a1024, 0x1a1430, 0x2e1a28, 0.07f,  0.78f, -0.40f),
        K(1.35f, 0x02040e, 0x06091a, 0x0e1226, 0x18203a, 0.03f,  0.95f, -0.64f),
        K(1.62f, 0x03060f, 0x080d1e, 0x14182e, 0x1c2440, 0.04f, -0.95f, -0.64f),
        K(1.86f, 0x101a3c, 0x2c2856, 0x4e3458, 0x8a4442, 0.16f, -0.78f, -0.40f),
        K(2.00f, 0x3a3f6e, 0x8f7f9e, 0xd7b6b8, 0xe0ab8c, 0.26f, -0.66f, -0.205f),
    };

    const float DayM = 5600f, NightM = 3400f, CycleM = DayM + NightM, DayStart = 0.18f;

    public Material sky;        // Surf/Sky
    public Material ocean;      // Surf/Ocean
    public Light sun;
    public float pin = -1f;     // pin the hour (0..2) for looking; -1 follows the run
    public Vector3 keyFrom;     // when set, the sun light comes from this direction instead (the beach)

    [HideInInspector] public float daylight = 1f;   // 0 night .. 1 noon
    [HideInInspector] public Vector3 sunDir = new Vector3(-0.45f, 0.75f, -0.5f).normalized;
    [HideInInspector] public Color glow = Color.white;
    [HideInInspector] public float glowAmt;

    public void Apply(float distM)
    {
        float dc = ((distM + DayStart * DayM) % CycleM + CycleM) % CycleM;
        float tod = pin >= 0f ? pin : (dc < DayM ? dc / DayM : 1f + (dc - DayM) / NightM);
        int i = 0; while (i < Keys.Length - 2 && tod > Keys[i + 1].t) i++;
        Key A = Keys[i], B = Keys[i + 1];
        float k = Mathf.Clamp01((tod - A.t) / (B.t - A.t));
        Color top = Color.Lerp(A.top, B.top, k), mid = Color.Lerp(A.mid, B.mid, k);
        Color hz = Color.Lerp(A.hz, B.hz, k); glow = Color.Lerp(A.glow, B.glow, k);
        glowAmt = Mathf.Lerp(A.gAmt, B.gAmt, k);
        float sx = Mathf.Lerp(A.sunX, B.sunX, k), sy = Mathf.Lerp(A.sunY, B.sunY, k);
        daylight = Mathf.Clamp01(sy * 2.6f);
        // the disc sits at (sx*130, sy*118, -195) in the web game; that is a direction here
        sunDir = new Vector3(sx * 130f, sy * 118f, -195f).normalized;
        float light = 0.18f + 0.82f * daylight;

        if (sky != null)
        {
            sky.SetColor("_Top", top); sky.SetColor("_Mid", mid); sky.SetColor("_Horizon", hz);
            sky.SetColor("_Glow", glow); sky.SetFloat("_GlowAmt", glowAmt);
            sky.SetVector("_SunDir", sunDir);
        }
        if (ocean != null)
        {
            ocean.SetColor("_SkyLo", hz); ocean.SetColor("_SkyHi", mid); ocean.SetColor("_Glow", glow);
            ocean.SetVector("_SunDir", sunDir); ocean.SetFloat("_Light", light);
        }
        if (sun != null)
        {
            // held above the horizon: a light from below is a torch under the chin
            Vector3 aim = new Vector3(sx * 130f, Mathf.Max(sy * 118f, 26f), -195f).normalized;
            if (keyFrom.sqrMagnitude > 0.01f) aim = keyFrom.normalized;
            sun.transform.rotation = Quaternion.LookRotation(-aim);
            sun.color = Color.Lerp(C(0xfff2cf), glow, Mathf.Min(0.55f, glowAmt * 0.62f));
            sun.intensity = 0.35f + 1.45f * daylight;
        }
        RenderSettings.ambientLight = Color.Lerp(mid * 0.55f, glow * 0.4f, glowAmt * 0.4f) * (0.3f + 0.7f * daylight);
        RenderSettings.fogColor = hz;
    }
}
