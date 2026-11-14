// Time of day, paced by distance the way the web game paces it: dawn through noon into sunset
// and a short night, then round again. One table drives the sky dome, the sun light, the
// ocean's reflected colours and the spray's tint, so nothing in the frame disagrees.
//
// The night half carries the moon on its own arc (the web's mX/mY keys: it is not the sun
// mirrored, it rises in the east as the sun sets, is highest a little after the middle of the
// night and sets in the west at dawn). Two facts come out of it for anything that glows or
// waits for the dark: nightAmt, how far the sun is DOWN, which saturates the moment the disc
// touches the sea; and moonAmt, how far the moon has cleared it, which is what "night proper"
// means -- see Ufo.MoonUp.
using UnityEngine;

public class DayCycle : MonoBehaviour
{
    struct Key { public float t; public Color top, mid, hz, glow; public float gAmt, sunX, sunY, mX, mY; }

    static Vector4 V(Color c) => new Vector4(c.r, c.g, c.b, 1f);
    static Color C(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
    // keys that predate the night have no moon of their own: it is under the water (web mY -0.5)
    static Key K(float t, uint top, uint mid, uint hz, uint glow, float g, float sx, float sy, float mx = 0f, float my = -0.5f)
        => new Key { t = t, top = C(top), mid = C(mid), hz = C(hz), glow = C(glow), gAmt = g, sunX = sx, sunY = sy, mX = mx, mY = my };

    // The sunrise (0 to 0.34, where every run starts) from reference photographs of beach sunrises:
    // indigo overhead, pink and lavender through the middle, coral to gold along the horizon, a
    // strong glow round the sun. It was the web's paler set -- grey-lilac, a cream horizon -- which
    // read washed out, and since the sea mirrors the middle and horizon colours, left the water slate.
    static readonly Key[] Keys = {
        K(0.00f, 0x262c66, 0x76569a, 0xe98a86, 0xff9a6a, 0.34f, -0.66f, -0.205f),
        K(0.05f, 0x30407e, 0xa8689e, 0xff9a72, 0xffa860, 0.52f, -0.63f, -0.088f),
        K(0.11f, 0x35589e, 0xc886a6, 0xffaa6c, 0xffb350, 0.74f, -0.58f,  0.038f),
        K(0.20f, 0x3a72b6, 0xd4a2b4, 0xffc488, 0xffc464, 0.56f, -0.47f,  0.190f),
        K(0.34f, 0x3a86cc, 0xb9cfea, 0xf6e8d2, 0xfff0d0, 0.24f, -0.30f,  0.520f),
        K(0.50f, 0x3f8fd6, 0x8fd2f5, 0xe6f4fd, 0xfff8ea, 0.09f, -0.03f,  0.930f),
        K(0.66f, 0x4088cf, 0xa9d6ef, 0xffe6bd, 0xffd287, 0.22f,  0.26f,  0.520f),
        K(0.80f, 0x3d6fb4, 0xc9a6c0, 0xffa863, 0xff8f3c, 0.46f,  0.45f,  0.190f),
        K(0.90f, 0x2b3f80, 0xa85f86, 0xff6a3a, 0xff4f18, 0.66f,  0.57f,  0.040f),
        K(0.96f, 0x162452, 0x5a3168, 0xa8383a, 0xd8381a, 0.46f,  0.63f, -0.085f),
        K(1.00f, 0x060a1c, 0x121a38, 0x33203f, 0x6a2418, 0.16f,  0.68f, -0.215f, -0.72f, -0.10f),
        K(1.08f, 0x040814, 0x0a1024, 0x1a1430, 0x2e1a28, 0.07f,  0.78f, -0.40f,  -0.66f,  0.06f),
        K(1.35f, 0x02040e, 0x06091a, 0x0e1226, 0x18203a, 0.03f,  0.95f, -0.64f,  -0.34f,  0.52f),
        K(1.62f, 0x03060f, 0x080d1e, 0x14182e, 0x1c2440, 0.04f, -0.95f, -0.64f,   0.10f,  0.72f),
        K(1.86f, 0x101a3c, 0x2c2856, 0x4e3458, 0x8a4442, 0.16f, -0.78f, -0.40f,   0.52f,  0.31f),
        K(2.00f, 0x262c66, 0x76569a, 0xe98a86, 0xff9a6a, 0.34f, -0.66f, -0.205f,  0.70f, -0.10f),
    };

    const float DayM = 5600f, NightM = 3400f, CycleM = DayM + NightM, DayStart = 0.18f;
    // the web's MOON_I is 0.30 against a SUN_I of 1.75; the port's sun peaks at 1.25, so the
    // moon is scaled by the same ratio to keep the two in the proportion the web has them
    const float MoonI = 0.30f * (1.25f / 1.75f);

    public Material sky;        // Surf/Sky
    public Material ocean;      // Surf/Ocean
    public Light sun;
    public Light moonLight;     // made here on first use when nothing hands one in
    public float pin = -1f;     // pin the hour (0..2) for looking; -1 follows the run
    public Vector3 keyFrom;     // when set, the sun light comes from this direction instead (the beach)
    public bool beachLook;      // the title screen's own sky and light, read off the web game

    [HideInInspector] public float daylight = 1f;   // 0 night .. 1 noon (the web's lit)
    [HideInInspector] public Vector3 sunDir = new Vector3(-0.45f, 0.75f, -0.5f).normalized;
    [HideInInspector] public Vector3 moonDir = Vector3.down;
    [HideInInspector] public Color glow = Color.white;
    [HideInInspector] public float glowAmt;
    [HideInInspector] public float tod;             // where in the cycle, 0..2 (the web's dayT)
    [HideInInspector] public float nightAmt;        // how dark it is out, 0 by day and 1 at night
    [HideInInspector] public float moonAmt;         // how far the moon has cleared the sea, 0..1
    // what the clouds are lit by (SkyKit.LightClouds, and the dome's own high cloud): the sky's
    // raw colours, the light they catch and where it comes from, the glow under their bases
    [HideInInspector] public Vector4 skyTop, skyMid, skyHz;
    [HideInInspector] public Vector3 cloudKey = new Vector3(-0.45f, 0.75f, -0.5f).normalized;
    [HideInInspector] public Vector4 cloudSun = Vector4.one, cloudUnder;
    [HideInInspector] public float cloudLight = 1f;
    float cloudDrift;                                // the high cloud's wind, accumulated
    float shadeWind;                                 // the cumulus' wind, accumulated, for their shadows on the sea

    public void Apply(float distM)
    {
        float dc = ((distM + DayStart * DayM) % CycleM + CycleM) % CycleM;
        tod = pin >= 0f ? pin : (dc < DayM ? dc / DayM : 1f + (dc - DayM) / NightM);
        int i = 0; while (i < Keys.Length - 2 && tod > Keys[i + 1].t) i++;
        Key A = Keys[i], B = Keys[i + 1];
        float k = Mathf.Clamp01((tod - A.t) / (B.t - A.t));
        Color top = Color.Lerp(A.top, B.top, k), mid = Color.Lerp(A.mid, B.mid, k);
        Color hz = Color.Lerp(A.hz, B.hz, k); glow = Color.Lerp(A.glow, B.glow, k);
        glowAmt = Mathf.Lerp(A.gAmt, B.gAmt, k);
        float sx = Mathf.Lerp(A.sunX, B.sunX, k), sy = Mathf.Lerp(A.sunY, B.sunY, k);
        float mx = Mathf.Lerp(A.mX, B.mX, k), my = Mathf.Lerp(A.mY, B.mY, k);
        daylight = Mathf.Clamp01(sy * 2.6f);
        // the disc sits at (sx*130, sy*118, -195) in the web game; that is a direction here
        sunDir = new Vector3(sx * 130f, sy * 118f, -195f).normalized;
        // the moon at (mx*130, my*118, -205); it fades in as it clears the sea, not at an hour
        moonDir = new Vector3(mx * 130f, my * 118f, -205f).normalized;
        float moonUp = Mathf.Clamp01((my + 0.02f) / 0.14f);
        moonAmt = moonUp;
        // out when the sun is down, gone once it is properly up
        nightAmt = Mathf.Clamp01((0.085f - sy) / 0.13f);
        float light = 0.18f + 0.82f * daylight + 0.22f * moonUp * (1f - daylight);
        if (beachLook)
        {
            // the title: a beach sunrise as the ride's now is, indigo to pink to a coral-gold horizon
            // (was the web title's deep blue to grey-blue to sand)
            top = C(0x2a3a82); mid = C(0xb07aa8); hz = C(0xffa86a); glow = C(0xffb35c); glowAmt = 0.78f;
            sunDir = new Vector3(-0.3f * 130f, 0.08f * 118f, -195f).normalized; daylight = 1f; light = 1f;   // mirrored with the set
            moonUp = 0f; moonAmt = 0f; nightAmt = 0f;
        }

        Clouds(top, mid, hz, sy, light: beachLook ? 1f : 0.10f + 0.90f * daylight + 0.25f * moonUp * (1f - daylight), moonUp: moonUp);

        if (sky != null)
        {
            // as vectors, not colours: the web hands these raw hex numbers to its shaders with no
            // colour-space conversion, and the frame is encoded once at the end
            sky.SetVector("_Top", V(top)); sky.SetVector("_Mid", V(mid)); sky.SetVector("_Horizon", V(hz));
            sky.SetVector("_Glow", V(glow)); sky.SetFloat("_GlowAmt", glowAmt);
            sky.SetVector("_SunDir", sunDir);
            sky.SetFloat("_SunOn", beachLook ? 0f : 1f);       // the web's title sky has no sun disc; its sun is the ride's alone
            sky.SetFloat("_Blend", beachLook ? 0.58f : 0.30f);
            sky.SetFloat("_SunX", beachLook ? -0.3f : sx);
            sky.SetVector("_MoonDir", moonDir);
            sky.SetFloat("_MoonUp", moonUp);
            // the stars shimmer slowly, the web's night*(0.80+0.20*sin(t*0.9))
            sky.SetFloat("_Night", nightAmt * (0.80f + 0.20f * Mathf.Sin(Time.time * 0.9f)));
            // the high cloud: its noise once, then the light it catches and its drift
            if (sky.HasProperty("_CloudNoise"))
            {
                if (sky.GetTexture("_CloudNoise") != SkyKit.SkyNoise()) sky.SetTexture("_CloudNoise", SkyKit.SkyNoise());
                sky.SetFloat("_CloudsOn", 1f);
                sky.SetVector("_CloudSun", cloudKey); sky.SetVector("_CloudLit", cloudSun); sky.SetFloat("_CloudLight", cloudLight);
                // soft sun rays off the same noise when the sun is low; the title's sky has no sun to cast them
                sky.SetFloat("_Rays", beachLook ? 0f : 1f);
                // the wind, plus a sliver of the run's own travel: the high sky slides back overhead
                // far slower than anything below it. Wrapped at 200, where every scale the shader
                // reads the noise at lands on a whole tile (at 100 two of them were half a tile out,
                // so the veil would jump), and it never loses precision.
                cloudDrift = Mathf.Repeat(cloudDrift + Time.deltaTime * 0.0035f, 200f);
                sky.SetVector("_CloudScroll", new Vector4(cloudDrift, Mathf.Repeat(-distM * 0.00045f, 200f), 0f, 0f));
            }
        }
        if (ocean != null)
        {
            ocean.SetVector("_SkyLo", V(hz)); ocean.SetVector("_SkyHi", V(mid)); ocean.SetVector("_Glow", V(glow));
            // the path on the water belongs to whatever is up: a moonpath once the sun is gone
            bool moonPath = !beachLook && sy < 0.02f && moonUp > 0.02f;
            ocean.SetVector("_SunDir", moonPath ? moonDir : sunDir); ocean.SetFloat("_Light", light);
            // the cumulus' shadows on the water: the high cloud's noise, read blurred a few hundred
            // feet to a tile, carried on the clouds' own wind (SkyLife.Wind, the one they all ride)
            // and creeping in with the run as the near ones do (SkyLife's 0.020 of the travel). Gone after dark, faint with the sun on the sea's edge.
            if (ocean.HasProperty("_CloudShade"))
            {
                if (ocean.GetTexture("_CloudShadeTex") != SkyKit.SkyNoise()) ocean.SetTexture("_CloudShadeTex", SkyKit.SkyNoise());
                const float Tile = 620f;
                shadeWind = Mathf.Repeat(shadeWind + Time.deltaTime * SkyLife.Wind, Tile);
                float creep = Mathf.Repeat(distM * 0.020f, Tile);
                // subtle: the web's sea has no cloud shadows at all, and at full strength they greyed
                // the water in broad patches and put out the sun's glitter; the ramp also reached full
                // already at the low sun every run starts under, so it climbs on to the day's height
                float str = 0.35f * (beachLook ? 0.8f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.45f, sy)) * (1f - nightAmt));
                ocean.SetVector("_CloudShade", new Vector4(Mathf.Repeat(-shadeWind / Tile, 1f), Mathf.Repeat(-creep / Tile, 1f), str, 1f / Tile));
            }
        }
        if (sun != null)
        {
            // held above the horizon: a light from below is a torch under the chin
            Vector3 aim = new Vector3(sx * 130f, Mathf.Max(sy * 118f, 26f), -195f).normalized;
            if (keyFrom.sqrMagnitude > 0.01f) aim = keyFrom.normalized;
            // the light that casts the shadows turns in steps of a fifth of a degree rather than a
            // hair every frame: a shadow map re-fitted to a sun that never stops moving makes every
            // shadow edge crawl and flicker. The sky and the sea keep the smooth sun (sunDir).
            var want = Quaternion.LookRotation(-aim);
            if (Quaternion.Angle(sun.transform.rotation, want) > 0.2f || beachLook) sun.transform.rotation = want;
            sun.color = beachLook ? C(0xffab63) : Color.Lerp(C(0xfff2cf), glow, Mathf.Min(0.55f, glowAmt * 0.62f));
            sun.intensity = beachLook ? 1.15f : 0.30f + 0.95f * daylight;
        }
        // the moon is a light rather than a colour, which is what stops the night going to nothing
        if (moonLight == null && !beachLook)
        {
            var go = new GameObject("Moon"); moonLight = go.AddComponent<Light>();
            moonLight.type = LightType.Directional; moonLight.shadows = LightShadows.None; moonLight.color = C(0xa8bcdf); moonLight.intensity = 0f;
        }
        if (moonLight != null)
        {
            Vector3 maim = new Vector3(mx * 60f, Mathf.Max(6f, my * 60f), -40f).normalized;
            moonLight.transform.rotation = Quaternion.LookRotation(-maim);
            moonLight.intensity = beachLook ? 0f : MoonI * moonUp * (1f - daylight);
        }
        if (beachLook)
        {
            // the web beach's hemisphere light: sky blue above, warm sand below
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = C(0xbcd6ef) * 0.9f; RenderSettings.ambientEquatorColor = Color.Lerp(C(0xbcd6ef), C(0xe0b078), 0.5f) * 0.85f; RenderSettings.ambientGroundColor = C(0xe0b078) * 0.8f;
            RenderSettings.fog = false;
        }
        else
        {
            RenderSettings.fog = true;
            // ---- the web's hemisphere light, as it is (3657, 29050-29109) ----
            // Sky 0xdfeeff above, the sea's shallow colour warmed by the glow below (the bounce off
            // the water), at 0.62 x (0.24 + 0.76 daylight + 0.20 moonUp). three r128 takes those
            // numbers as linear and a lit surface gets albedo x that, no pi. The flat grey this
            // replaces came to about a twentieth of it at the dawn a run starts in, and the side of
            // anything facing away from the sun -- a log, a buoy, the pug, the board's rails --
            // went nearly black against the web's. (The web adds a sky environment map on top;
            // that part is left out here, so this is the lower bound of its fill, not more.)
            // Unity reads ambient colours as sRGB, so the web's linear numbers go in encoded.
            float hemiI = 0.62f * (0.24f + 0.76f * daylight + 0.20f * moonUp);
            Color skyL = new Color(0.875f, 0.933f, 1.0f) * hemiI;
            Color shal = new Color(0.153f, 0.573f, 0.769f);
            Color grL = Color.Lerp(shal, glow, glowAmt * 0.50f) * 0.62f * hemiI;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = skyL.gamma;
            RenderSettings.ambientEquatorColor = Color.Lerp(skyL, grL, 0.5f).gamma;
            RenderSettings.ambientGroundColor = grL.gamma;
        }
        RenderSettings.fogColor = hz;
    }

    // ---- the light on the clouds ----
    // The web tints each cloud white-to-glow by the glow amount and dims it by how much sun is on
    // the scene, which leaves a sunset's clouds grey: the sea has lost the light by then and the
    // clouds have not. So the clouds keep it a while longer -- full with the sun a tenth up, the
    // last red going as it sinks a fifth under -- and after dark the moon is the light they catch.
    void Clouds(Color top, Color mid, Color hz, float sy, float light, float moonUp)
    {
        skyTop = V(top); skyMid = V(mid); skyHz = V(hz);
        float y = beachLook ? 0.3f : sy;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.22f, 0.10f, y));
        Vector3 key = beachLook && keyFrom.sqrMagnitude > 0.01f ? keyFrom.normalized : sunDir;
        float mw = beachLook ? 0f : Mathf.Clamp01((0.02f - key.y) / 0.10f) * moonUp;
        if (mw > 0f) key = Vector3.Lerp(key, moonDir, mw).normalized;
        cloudKey = key;
        Color sc = Color.Lerp(Color.white, glow, Mathf.Min(1f, glowAmt * 0.95f)) * (0.06f + 0.94f * k);
        sc = Color.Lerp(sc, new Color(0.55f, 0.66f, 0.88f) * 0.24f, mw * (1f - k));
        cloudSun = new Vector4(sc.r, sc.g, sc.b, 1f);
        // the underside burn: the glow, pushed toward a backlit edge's hot white (web cloudUnder)
        Color un = Color.Lerp(glow, new Color(1.55f, 1.48f, 1.34f), 0.25f);
        cloudUnder = new Vector4(un.r, un.g, un.b, Mathf.Min(1f, glowAmt * 1.10f) * k);
        cloudLight = light;
    }
}
