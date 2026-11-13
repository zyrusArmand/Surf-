// The sea. The vertex shader runs the same seven-wave field WaveField.cs runs on the CPU, so
// what the board rides and what you see are one surface. The fragment is the web game's water
// brought across: deep-to-shallow colour by height, the sky REFLECTED off a chopped normal with
// the sun's halo on it, a glitter track, light through the crests, and foam where the water is
// high AND steep rather than merely high.
Shader "Surf/Ocean"
{
    Properties
    {
        _Deep ("Deep", Vector) = (0.027, 0.298, 0.506, 1)
        _Shallow ("Shallow", Vector) = (0.153, 0.573, 0.769, 1)
        _SkyLo ("Sky horizon", Vector) = (0.56, 0.82, 0.96, 1)
        _SkyHi ("Sky zenith", Vector) = (0.25, 0.56, 0.84, 1)
        _Glow ("Sun glow", Vector) = (1, 0.95, 0.84, 1)
        _SunDir ("Sun direction", Vector) = (-0.45, 0.75, -0.5, 0)
        _Light ("Daylight", Float) = 1
        _Amp ("Swell amplitude", Float) = 1.35
        _Peak ("Peak height", Float) = 2.85
        _Dist ("Field offset", Float) = 0
        _T ("Time", Float) = 0
        _WakeX ("Wake x", Float) = 0
        _WakeZ ("Wake z", Float) = 0
        _WakeA ("Wake amount", Float) = 0
        _Lift ("Sea level", Float) = 0
        _Calm ("Swell calm", Float) = 1
        _FallW ("Chute", Float) = 0
        _FallZF ("Chute origin", Float) = 0
        _FallT ("Chute travel", Float) = 0
        _FallLane ("Chute lane", Float) = 9
        _HullS ("Hull half width, half length, bottom, on", Vector) = (0, 0, 0, 0)
        _SwA ("Set wave height", Float) = 0
        _SwX ("Set wave crest x", Float) = 0
        _SwS ("Set wave side", Float) = 1
        _WhX ("Whirl x", Float) = 0
        _WhZ ("Whirl z (field)", Float) = 0
        _WhA ("Whirl depth", Float) = 0
        _SkiX ("Jet ski wake x", Float) = 0
        _SkiZ ("Jet ski wake z (field)", Float) = 0
        _SkiA ("Jet ski wake height", Float) = 0
        _BtX ("Boat stern x", Float) = 0
        _BtZ ("Boat stern z", Float) = 0
        _BtSn ("Boat heading sin", Float) = 0
        _BtCs ("Boat heading cos", Float) = 1
        _BtA ("Boat wake amount", Float) = 0
        _Chop ("Chop normals", 2D) = "bump" {}
        _ChopK ("Chop from texture", Float) = 0
        _Shore ("Shore line (-z)", Float) = 100000
        _ShoreW ("Shore taper width", Float) = 60
    }
    SubShader
    {
        // drawn after the solid world (but still writing depth) so the depth texture holds what is
        // UNDER the water: that is how the foam finds where a buoy, a rock or the board meets it
        Tags { "RenderType"="Opaque" "Queue"="Transparent-100" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            // the reflection (PlanarReflection.cs), set globally
            TEXTURE2D(_ReflTex); SAMPLER(sampler_ReflTex); float4x4 _ReflVP; float _ReflOn, _ReflY;

            CBUFFER_START(UnityPerMaterial)
            float4 _Deep, _Shallow, _SkyLo, _SkyHi, _Glow, _SunDir;
            float _Light, _Amp, _Peak, _Dist, _T, _WakeX, _WakeZ, _WakeA, _Lift, _Shore, _ShoreW, _Calm, _SwA, _SwX, _SwS, _WhX, _WhZ, _WhA, _FallW, _FallZF, _FallT, _FallLane;
            float4 _HullS, _HullC; float4x4 _HullW2L;      // _HullC: the plan's centre (x, z) and the rocker (how far the underside rises to the ends), in the board's frame
            float _BtX, _BtZ, _BtSn, _BtCs, _BtA;           // the tow boat's stern, heading and how hard it is making way (Boat.cs)
            float _SkiX, _SkiZ, _SkiA;                      // the jet ski's wake: lateral centre, centre in field z, height (JetSki.cs)
            CBUFFER_END
            TEXTURE2D(_Chop); SAMPLER(sampler_Chop); float _ChopK;
            float4 _WaveA[7];   // dx, dz, k, a
            float4 _WaveB[7];   // ek, ep, w, -

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float3 n : TEXCOORD1; float h : TEXCOORD2; float4 sw : TEXCOORD3; float4 sp : TEXCOORD4; };

            // the field: height and its two slopes at a field point p (x, dist*drift+z)
            // the chute: the web's fallDip, the lane tipping down a fall, anchored at the rider
            float fallH(float2 q)
            {
                if (_FallW <= 0.002) return 0.0;
                float d = clamp(_FallZF - q.y, _FallT - 6.0, _FallT + 140.0);
                float u = min(1.6, abs(q.x) / max(0.001, _FallLane));
                float wob = sin(q.x * 0.37) * 3.2;
                float A = -0.47 * _FallT;
                return _FallW * (-0.47 * d - A) + 1.1 * _FallW * u * u;
            }

            // The board's wake: a V, as a hull's is. The water the rails push aside stands up along
            // them from where the board sits in the sea (a foot and a half up from the tail, hugging
            // the rails) and peels off the tail as two ridges that open and settle behind him, with
            // the water drawn in a little straight behind the tail. (A single hump across the lane
            // read as an arc drawn on the sea; switched on AT the tail it lifted the water over it.)
            // The rings a splash sends out through the water (Fx.Hit): from the OUTLINE of what hit
            // it (the board's own, half width x half length on its heading, as a rounded stadium; a
            // small circle for anything else), a crest running out at three feet a second with a
            // trough and a smaller crest behind, gone within about five feet and a second and a half,
            // and the dent it punched, filling back in fast. In the sea's own height and slope, so
            // they are lit like water and not drawn on it.
            float4 _Rip[4];      // x, field z, age, height
            float4 _RipS[4];     // half width, half length, sin and cos of the heading
            float ripH(float2 p)
            {
                float h = 0.0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float4 R = _Rip[k];
                    if (R.w > 0.0)
                    {
                        float4 S = _RipS[k];
                        float2 q = p - R.xy;
                        float2 lq = float2(q.x * S.w - q.y * S.z, q.x * S.z + q.y * S.w);    // into the shape's own frame
                        float hw = max(S.x, 0.05), hl = max(S.y, hw);
                        float r = max(0.0, length(float2(lq.x, max(abs(lq.y) - (hl - hw), 0.0))) - hw);   // feet out from the outline
                        float age = R.z;
                        float d = r - (0.25 + 3.2 * age);
                        float env = R.w * exp(-age * 2.4) * saturate(1.2 - r / 5.0);
                        h += env * (exp(-d * d / 0.14) - 0.55 * exp(-(d + 0.65) * (d + 0.65) / 0.18) + 0.30 * exp(-(d + 1.3) * (d + 1.3) / 0.24));
                        h -= R.w * 1.2 * exp(-r * r / 0.25) * exp(-age * 6.0);
                    }
                }
                return h;
            }
            bool ripOn() { return _Rip[0].w + _Rip[1].w + _Rip[2].w + _Rip[3].w > 0.0; }

            // The wake is the path the board drew through the water, not a fixed V behind where it
            // is now: _WakeP holds where the board was across the lane when it passed the water that
            // is now 0, 3, 6 ... 21 feet behind the tail, so a carve bends the trail back to where he
            // came from. _WakeC is the carve (-1..1): the arm on the side he is moving toward stands
            // taller, so the two catch the light differently through a turn.
            float _WakeP[8]; float _WakeC;
            float wakeCx(float dz)
            {
                float u = clamp(dz / 3.0, 0.0, 6.999); int i0 = (int)u;
                return lerp(_WakeP[i0], _WakeP[i0 + 1], u - i0);
            }
            float wakeH(float2 p)
            {
                float dz = p.y - (_Dist + _WakeZ);              // feet behind the tail; negative is along the board
                float sx = p.x - wakeCx(dz), ax = abs(sx);
                float arm = 0.55 + 0.30 * max(dz, 0.0);
                float da = ax - arm;
                float wide = 0.30 + 0.07 * max(dz, 0.0);        // each ridge a broad swell, broadening as it travels (narrower read as a drawn line)
                float ridge = exp(-da * da / wide) * smoothstep(-1.6, -0.2, dz) * exp(-max(dz, 0.0) / 18.0)
                            * (1.0 + 0.5 * _WakeC * (sx >= 0.0 ? 1.0 : -1.0));
                float trough = exp(-ax * ax / 0.35) * smoothstep(-0.2, 0.9, dz) * exp(-max(dz, 0.0) / 6.0);
                return _WakeA * (0.55 * ridge - 0.22 * trough);
            }

            // The jet ski's wake (web wakeBump 3782-3794, and its copy in the web's sea 4255-4266):
            // a broad ridge across the lane, a gaussian 18 ft deep along it, eased off over 64 ft
            // either side of where the ski crossed, the same as WaveField.SkiWakeH. In the field like
            // the rider's wake and the rings, its slope taken per pixel in frag the same way, so it
            // is lit, foamed and fogged as the sea it is.
            float skiH(float2 p)
            {
                float sdz = p.y - _SkiZ;
                float sez = exp(-(sdz * sdz) / (2.0 * 18.0 * 18.0));
                float sax = saturate(1.0 - abs(p.x - _SkiX) / 64.0);
                return _SkiA * sez * sax * sax * (3.0 - 2.0 * sax);
            }

            void field(float2 p, out float h, out float dhx, out float dhz)
            {
                h = 0; dhx = 0; dhz = 0;
                [unroll] for (int i = 0; i < 7; i++)
                {
                    float4 a = _WaveA[i], b = _WaveB[i];
                    float u = a.x * p.x + a.y * p.y;
                    float e = 0.32 + 0.68 * sin(u * a.z * b.x - _T * b.z * b.x * 0.5 + b.y);
                    float ph = u * a.z - _T * b.z;
                    h += a.w * e * sin(ph);
                    float c = a.w * e * a.z * cos(ph);
                    dhx += c * a.x; dhz += c * a.y;
                }
                h *= _Amp * _Calm; dhx *= _Amp * _Calm; dhz *= _Amp * _Calm;
                if (_FallW > 0.002)
                {
                    h += fallH(p);
                    dhx += (fallH(p + float2(0.6, 0)) - fallH(p - float2(0.6, 0))) / 1.2;
                    dhz += (fallH(p + float2(0, 0.6)) - fallH(p - float2(0, 0.6))) / 1.2;
                }
                // the whirlpool: a dip with a steep eye, its slope folded into the normal
                if (_WhA > 0.001)
                {
                    float2 wd = float2(p.x - _WhX, p.y - _WhZ);
                    float r = length(wd);
                    if (r < 20.0)
                    {
                        float k = 1.0 - r / 20.0;
                        float k7 = pow(k, 7.0);
                        float f = k * k * (3.0 - 2.0 * k) * (1.0 + 2.1 * k7);
                        h -= _WhA * f;
                        if (r > 1e-4)
                        {
                            float dsm = 6.0 * k * (1.0 - k);
                            float dfdk = dsm * (1.0 + 2.1 * k7) + k * k * (3.0 - 2.0 * k) * 14.7 * pow(k, 6.0);
                            float dfdr = dfdk * (-1.0 / 20.0);
                            dhx -= _WhA * dfdr * (wd.x / r); dhz -= _WhA * dfdr * (wd.y / r);
                        }
                    }
                }
                // the rider's wake: a ridge in the water itself, so it is lit and foamed like a wave
                if (_WakeA > 0.001)
                {
                    // the height here; its slope is taken per pixel in frag, since the ridges are
                    // narrower than the sea's vertices are apart
                    h += wakeH(p);
                }
                if (ripOn()) h += ripH(p);     // the splash rings, the same way
                if (_SkiA > 0.001) h += skiH(p);   // and the jet ski's wake
            }

            V vert(A i)
            {
                V o;
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                float h, dhx, dhz;
                field(float2(wp.x, wp.z + _Dist), h, dhx, dhz);
                // the set wave: steep front face, long shoulder, its height falling away behind
                // the break into whitewater. sw = (face 0..1, broken 0..1, thrown 0..1, metres out)
                o.sw = float4(0, 0, 0, 999);
                if (_SwA > 0.0015)
                {
                    float d = (wp.x - _SwX) * _SwS, f, dfdd;
                    if (d <= 0.0) { float k = saturate(1.0 + d / 10.0); f = pow(k, 3.2); dfdd = (k > 0.0) ? (3.2 * pow(k, 2.2) / 10.0) : 0.0; }
                    else { float k = saturate(1.0 - d / 34.0); f = 0.34 + 0.66 * k * k * (3.0 - 2.0 * k); dfdd = (k > 0.0 && k < 1.0) ? (-0.66 * 6.0 * k * (1.0 - k) / 34.0) : 0.0; }
                    float wz = wp.z;
                    float ub = saturate((wz - 30.0) / 26.0); float b = ub * ub * (3.0 - 2.0 * ub);
                    float dbdz = (ub > 0.0 && ub < 1.0) ? (6.0 * ub * (1.0 - ub) / 26.0) : 0.0;
                    float m = 1.0 - 0.55 * b;
                    h += _SwA * m * f; dhx += _SwA * m * dfdd * _SwS; dhz += _SwA * (-0.55 * dbdz) * f;
                    float ua = saturate((wz - (30.0 - 78.0)) / (78.0 - 26.0));
                    o.sw = float4(f, b, ua * ua * (3.0 - 2.0 * ua), d);
                }
                // the swell dies as it shoals: full out at sea, nothing at the waterline
                float st = saturate((_Shore - wp.z) / _ShoreW); st = st * st * (3.0 - 2.0 * st);
                h *= st; dhx *= st; dhz *= st;
                wp.y = h + _Lift;
                // ---- the board is a solid thing on the water: no crest comes up through the hull ----
                // Inside the board's own outline (a rounded plan, narrowing to nose and tail) the
                // surface is held just under its bottom, eased in over the last few inches of rail
                // so it still sits IN the water rather than on a flat patch of it.
                if (_HullS.w > 0.5)
                {
                    float3 lp = mul(_HullW2L, float4(wp, 1.0)).xyz; lp.xz -= _HullC.xy;
                    // fzR is NOT clamped: clamped at 1 it never passed the end of the board, so every bit
                    // of sea in line with the board, ahead or behind and however far, counted as partly
                    // inside it and had its crests cut down (small divots straight up the lane)
                    float fzR = abs(lp.z) / max(0.01, _HullS.y), fz = saturate(fzR);
                    float halfW = _HullS.x * sqrt(saturate(1.0 - fz * fz * fz * fz));   // not "half": that is a type in HLSL
                    float inside = (1.0 - smoothstep(halfW - 0.12, halfW + 0.10, abs(lp.x))) * (1.0 - smoothstep(0.97, 1.04, fzR));
                    inside *= step(0.0001, halfW + 0.10 - abs(lp.x)) * step(fzR, 1.04);
                    float upY = _HullW2L[1][1];                       // how a world rise moves along the board's own up
                    float floor0 = _HullS.z + _HullC.z * fz * fz + 0.10;      // just above the underside: the rails still sit IN the water, the deck stays out of it
                    // only while the board is the right way up: upside down in a flip the sign of upY
                    // turned the push into a pull and drew the sea up after it in a spike
                    if (inside > 0.001 && lp.y > floor0 && upY > 0.5)
                    {
                        float drop = (lp.y - floor0) / upY;
                        wp.y -= drop * inside;
                    }
                }
                o.w = wp;
                o.n = normalize(float3(-dhx, 1.0, -dhz));
                o.h = h;
                o.pos = TransformWorldToHClip(wp);
                o.sp = ComputeScreenPos(o.pos);
                return o;
            }

            float h21(float2 p){ p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float vn(float2 p){ float2 i = floor(p), f = frac(p); f = f*f*(3.0-2.0*f);
                return lerp(lerp(h21(i), h21(i+float2(1,0)), f.x), lerp(h21(i+float2(0,1)), h21(i+float2(1,1)), f.x), f.y); }
            float fbm(float2 p){ float v = 0, a = 0.5; float2x2 m = float2x2(1.62, 1.18, -1.18, 1.62);
                [unroll] for (int k = 0; k < 3; k++){ v += a * vn(p); p = mul(m, p); a *= 0.5; } return v / 0.875; }

            half4 frag(V i) : SV_Target
            {
                // ---- and per pixel: no water inside the hull ----
                // The sea's vertices are a foot or two apart, so the vertex push above cannot hold a
                // crest out of the whole outline; between them the water still showed through the
                // deck. Any bit of sea inside the board's plan and above its bottom is simply not
                // drawn: the board itself is what is seen there, as it would be.
                if (_HullS.w > 0.5)
                {
                    float3 hq = mul(_HullW2L, float4(i.w, 1.0)).xyz; hq.xz -= _HullC.xy;
                    float qz = abs(hq.z) / max(0.01, _HullS.y * 0.94);
                    float qw = _HullS.x * 0.92 * sqrt(saturate(1.0 - qz * qz * qz * qz));
                    // only at the DECK: the rails may sit in the water with a real waterline round them,
                    // which the depth test draws; what never shows is water lying across the deck. The
                    // underside rises toward nose and tail with the rocker (_HullC.z), and the deck is the
                    // board's own thickness (_HullC.w) above it
                    // and only with the board lying flat: tipped up in a flip or a bad landing its plan
                    // stands on edge and the cut sliced a wedge out of the sea down to the sky
                    if (_HullW2L[1][1] > 0.5 && qz < 1.0 && abs(hq.x) < qw && hq.y > _HullS.z + _HullC.z * qz * qz + _HullC.w * 0.8) discard;
                }
                float3 N = normalize(i.n);
                // the wake's slope, per pixel: lit as water standing up along the rails and peeling off
                // the tail, not painted on a flat sea
                if (_WakeA > 0.001)
                {
                    float2 pw = float2(i.w.x, i.w.z + _Dist); const float we = 0.05;
                    float gx = (wakeH(pw + float2(we, 0)) - wakeH(pw - float2(we, 0))) / (2.0 * we);
                    float gz = (wakeH(pw + float2(0, we)) - wakeH(pw - float2(0, we))) / (2.0 * we);
                    N = normalize(N + float3(-gx, 0.0, -gz));
                }
                if (ripOn())
                {
                    float2 pr = float2(i.w.x, i.w.z + _Dist); const float re = 0.06;
                    float rx = (ripH(pr + float2(re, 0)) - ripH(pr - float2(re, 0))) / (2.0 * re);
                    float rz = (ripH(pr + float2(0, re)) - ripH(pr - float2(0, re))) / (2.0 * re);
                    N = normalize(N + float3(-rx, 0.0, -rz));
                }
                if (_SkiA > 0.001)
                {
                    float2 pk = float2(i.w.x, i.w.z + _Dist); const float ke = 0.25;
                    float kx = (skiH(pk + float2(ke, 0)) - skiH(pk - float2(ke, 0))) / (2.0 * ke);
                    float kz = (skiH(pk + float2(0, ke)) - skiH(pk - float2(0, ke))) / (2.0 * ke);
                    N = normalize(N + float3(-kx, 0.0, -kz));
                }
                float3 L = normalize(_SunDir.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.w);
                float dCam = length(_WorldSpaceCameraPos - i.w);
                float diff = max(dot(N, L), 0.0);
                float3 H = normalize(L + V);
                // chop: a centimetre-scale tilt on the normal, at three scales, the finest only close in
                float2 gp = float2(i.w.x, i.w.z + _Dist * 0.22);
                float2 wob;
                if (_ChopK > 0.5)
                {
                    // the web's tiled chop map at three scales, the finest only close in; one mip sharper
                    // than the sampler would pick, as the web samples it, or at this grazing angle the
                    // ripple averages out to flat a few feet from the board
                    float2 ca = SAMPLE_TEXTURE2D_BIAS(_Chop, sampler_Chop, gp*0.055 + float2( _T*0.0063, -_T*0.0052), -1.0).xy * 2.0 - 1.0;
                    float2 cb = SAMPLE_TEXTURE2D_BIAS(_Chop, sampler_Chop, gp*0.168 + float2(-_T*0.0116,  _T*0.0097), -1.0).xy * 2.0 - 1.0;
                    wob = ca + cb * 0.78;
                    float2 cc = SAMPLE_TEXTURE2D_BIAS(_Chop, sampler_Chop, gp*0.52 + float2( _T*0.021, -_T*0.017), -1.0).xy * 2.0 - 1.0;
                    wob += cc * 0.45 * (1.0 - smoothstep(6.0, 42.0, dCam));
                }
                else
                {
                    wob = float2(fbm(gp*1.85 + float2(_T*0.21, -_T*0.17)) - 0.5,
                                 fbm(gp*2.30 + float2(-_T*0.19, _T*0.23)) - 0.5)
                        + float2(fbm(gp*6.40 + float2(_T*0.44, _T*0.31)) - 0.5,
                                 fbm(gp*7.90 + float2(-_T*0.37, _T*0.41)) - 0.5) * 0.55;
                }
                float3 Nr = normalize(N + float3(wob.x, 0, wob.y) * 0.20);
                float spec = pow(max(dot(Nr, H), 0.0), 140.0) * 0.9;
                float fres = pow(1.0 - max(dot(Nr, V), 0.0), 3.0);
                // the glitter track, keyed to a roughened normal, a long path when the sun is low
                float3 Ng = normalize(N + float3(wob.x, 0, wob.y) * 0.44);
                float lowSun = smoothstep(0.46, 0.04, L.y);
                float gl = pow(max(dot(Ng, H), 0.0), lerp(78.0, 22.0, lowSun)) * 0.62;
                gl *= smoothstep(-0.10, 0.05, L.y) * lerp(0.55, 1.0, lowSun);
                float hS = i.h - i.sw.x * _SwA;      // the swell's own height, the wall taken out
                float3 base = lerp(_Deep.rgb, _Shallow.rgb, saturate(hS * 0.28 + 0.5));
                // what stands between the sun and this bit of sea: the rider, the palm, the buoys, the lip
                // No sun shadows on the open sea. Water is not a diffuse surface and barely shows one,
                // and with the sun low (most of this game) every log's and buoy's shadow was thrown tens
                // of feet across the water as a dark slab. Only the board's own contact shade is kept.
                float shade = 1.0;
                // and the board's own contact shadow: the water right under and round the hull darkens,
                // most when the board sits on it, gone once it is a couple of feet up
                if (_HullS.w > 0.5)
                {
                    float3 hp = mul(_HullW2L, float4(i.w, 1.0)).xyz; hp.xz -= _HullC.xy;
                    float hz = saturate(abs(hp.z) / max(0.01, _HullS.y));
                    float hw = _HullS.x * sqrt(saturate(1.0 - hz * hz * hz * hz));
                    float2 out2 = float2(max(0.0, abs(hp.x) - hw), max(0.0, abs(hp.z) - _HullS.y));
                    float gap = max(0.0, _HullS.z - hp.y);
                    float hug = exp(-dot(out2, out2) / (0.18 + 0.30 * gap));
                    shade = min(shade, 1.0 - hug * 0.38 * (1.0 - smoothstep(0.15, 2.4, gap)));
                }
                float sunLit = lerp(0.52, 1.0, shade);
                float3 lit = base * (0.38 + 0.72 * diff * shade) * lerp(0.86, 1.0, shade);
                // the sky reflected off the ripple, with the sun's halo laid into the dome
                float3 R = reflect(-V, Nr);
                float ry = saturate(R.y);
                float3 skyRefl = lerp(_SkyLo.rgb, _SkyHi.rgb, smoothstep(0.0, 0.62, ry));
                // the world in the water: the mirrored point looked up in the reflection, bent by the
                // ripple; the painted sky gradient stays underneath it as the web's fallback
                if (_ReflOn > 0.5)
                {
                    // The ripple bends it by a small, fixed amount (it grew with distance before, and along
                    // the view that read as a gap).
                    // looked up on the mean level: the floating things were each moved, for the reflection's
                    // own render, so their waterline sits there (PlanarReflection.beforeRender), which puts
                    // every reflection's start at its own waterline and mirrors nothing from under the sea
                    float3 mw = float3(i.w.x, _ReflY, i.w.z);
                    mw.xz += (Nr.xz + float2(wob.x, wob.y) * 0.05) * 0.55;
                    float4 rc = mul(_ReflVP, float4(mw, 1.0));
                    float2 ruv = rc.xy / max(1e-4, rc.w) * 0.5 + 0.5;
                    // faded only at the sides: water close to the lens looks its reflection up below the
                    // bottom of the picture (it is mirrored about its own height now), and fading there
                    // drew a straight line across the sea behind the board; clamped, it just continues
                    float inside = smoothstep(0.0, 0.04, ruv.x) * smoothstep(1.0, 0.96, ruv.x);
                    // a choppy sea breaks a reflection up and draws it out down the water: bent by the
                    // ripple in the picture itself (more up and down than across), softened from the
                    // picture's mips, and gathered from three taps down the line
                    ruv += float2(Nr.x * 0.010 + wob.x * 0.002, Nr.z * 0.024 + wob.y * 0.004);
                    float lod = 0.6 + 0.9 * saturate(dCam / 90.0);
                    float4 rs4 = SAMPLE_TEXTURE2D_LOD(_ReflTex, sampler_ReflTex, saturate(ruv), lod) * 0.5
                               + SAMPLE_TEXTURE2D_LOD(_ReflTex, sampler_ReflTex, saturate(ruv + float2(0.0, 0.007)), lod) * 0.25
                               + SAMPLE_TEXTURE2D_LOD(_ReflTex, sampler_ReflTex, saturate(ruv - float2(0.0, 0.007)), lod) * 0.25;
                    // the mirror holds only what stands in it, over nothing (PlanarReflection clears to
                    // transparent): laid over the painted sky by how much of the pixel it covers
                    float3 rs = pow(max(rs4.rgb, 0.0), 1.0 / 2.2);           // back to the plain numbers this shader works in
                    // and an empty pixel is black as well as clear: should the pipeline hand the mirror
                    // back with its alpha forced to 1, black still reads as nothing there, rather than
                    // turning the open sea black (only a near-black object is lost from the mirror)
                    float rsA = min(saturate(rs4.a), saturate(max(rs4.r, max(rs4.g, rs4.b)) * 40.0));
                    float3 seen = rs + skyRefl * (1.0 - rsA);
                    skyRefl = lerp(skyRefl, seen, inside * (rc.w > 0.0 ? 0.94 : 0.0));
                }
                skyRefl += _Glow.rgb * pow(max(dot(R, L), 0.0), 40.0) * 0.55 * smoothstep(-0.12, 0.06, L.y);
                float3 col = lerp(lit, skyRefl, fres * 0.88) + spec * shade;
                col += _Glow.rgb * gl * lerp(1.0, shade, 0.5);
                // light through the crests
                float thin = smoothstep(0.25 * _Peak, 0.95 * _Peak, hS);
                float toSun = pow(max(dot(V, -L), 0.0), 2.6);
                float sss = thin * (0.35 + 0.65 * toSun) * (1.0 - fres * 0.5);
                col += float3(0.08, 0.42, 0.40) * sss * 0.55 * sunLit;
                // foam: high AND steep, torn by drifting noise
                float2 fpC = float2(i.w.x, i.w.z + _Dist * 0.55);
                float2 fpM = float2(i.w.x, i.w.z + _Dist * 0.30);
                float2 fpF = float2(i.w.x, i.w.z + _Dist * 0.12);
                float shimmer = fbm(fpC * 0.55 + float2(_T*0.05, -_T*0.04)) * 2.0 - 1.0;
                float steep = smoothstep(0.09, 0.28, 1.0 - saturate(N.y));
                float crest = smoothstep(0.62 * _Peak, 0.92 * _Peak, hS + shimmer * 0.10) * (0.30 + 0.70 * steep);
                float lace = smoothstep(0.40, 0.84, fbm(fpM * 1.45 + float2(-_T*0.07, _T*0.06))) * crest;
                float fleck = smoothstep(0.56, 0.93, fbm(fpF * 3.90 + float2(_T*0.11, _T*0.09))) * crest;
                float slope = 1.0 - saturate(N.y);
                float lip = smoothstep(0.16, 0.34, slope) * smoothstep(0.25 * _Peak, 0.55 * _Peak, hS);
                float foam = saturate(crest * 0.42 + lace * 0.52 + fleck * 0.24 + lip * 0.55);
                // the rider's wake foams where it ridges
                if (_WakeA > 0.001)
                {
                    float dz = (i.w.z + _Dist) - (_Dist + _WakeZ);
                    // fading in off the tail rather than switched on along a line there (which drew a
                    // straight edge across the water behind the board), and following the wake's V: a
                    // churned strip straight behind, and foam along the two arms as they open
                    float ax = abs(i.w.x - wakeCx(dz)), armF = 0.55 + 0.30 * max(dz, 0.0);
                    float along = smoothstep(-1.6, -0.2, dz) * exp(-max(dz, 0.0) / 14.0);   // from where the rails meet the water, as the ridges do
                    float churn = saturate(1.0 - ax / (0.6 + 0.12 * max(dz, 0.0))) * 0.75 * smoothstep(-0.3, 0.8, dz);
                    float arms = exp(-(ax - armF) * (ax - armF) / (0.30 + 0.07 * max(dz, 0.0)));
                    // the arms are mostly glassy ridges, the light off their shape; the foam is the churn
                    // close behind the board, with only a little lace out along the arms
                    float wf = along * max(churn, arms * 0.45 * exp(-max(dz, 0.0) / 7.0)) * _WakeA * 1.6;
                    foam = max(foam, wf * (0.6 + 0.4 * lace));
                }
                col = lerp(col, float3(0.94, 0.97, 1.0), foam * 0.66);
                // the set wave's face: a darker band low on the wall, green light through it, foam
                // along the crest, and the sections behind the break collapsed to whitewater
                if (_SwA > 0.0015)
                {
                    float vSw = i.sw.x, vBrk = i.sw.y, vPa = i.sw.z, vDx = i.sw.w;
                    float laceR = smoothstep(0.40, 0.84, fbm(fpM * 1.45 + float2(-_T*0.07, _T*0.06)));
                    float fleckR = smoothstep(0.56, 0.93, fbm(fpF * 3.90 + float2(_T*0.11, _T*0.09)));
                    col = lerp(col, _Deep.rgb * 0.86, smoothstep(0.05, 0.42, vSw) * (1.0 - smoothstep(0.52, 0.95, vSw)) * 0.34);
                    col += float3(0.10, 0.60, 0.62) * smoothstep(0.14, 0.86, vSw) * 0.30;
                    float swFoam = smoothstep(0.62, 1.0, vSw) * (0.45 + 0.55 * laceR + 0.35 * fleckR);
                    col = lerp(col, float3(0.96, 0.99, 1.0), saturate(swFoam) * 0.40);
                    float ww = vBrk * smoothstep(0.04, 0.34, vSw);
                    col = lerp(col, float3(0.90, 0.95, 0.97), saturate(ww * (0.52 + 0.34 * laceR + 0.30 * fleckR)) * 0.88);
                    // the whitewater at the foot of the thrown section, where the lip lands
                    float land = smoothstep(-19.0, -13.0, vDx) * (1.0 - smoothstep(-3.0, -0.2, vDx));
                    land *= vPa * (0.80 + 0.50 * vBrk);
                    col = lerp(col, float3(0.93, 0.97, 0.99), saturate(land * (0.72 + 0.55 * laceR + 0.40 * fleckR)) * 0.96);
                    float core = smoothstep(-11.0, -6.5, vDx) * (1.0 - smoothstep(-5.0, -1.8, vDx));
                    core *= vPa;
                    col = lerp(col, float3(0.97, 0.99, 1.00), saturate(core * (0.80 + 0.40 * laceR + 0.30 * fleckR)) * 0.92);
                }
                // ---- and the tow boat's V (web 4795-4813) ----
                // The one part of the boat's wake the web kept when the ribbons and the foam cloud
                // were switched off: along and across the hull's heading, arms opening at 0.35 a foot
                // astern from 2.2 ft off the stern, and the propeller's churn straight behind it.
                if (_BtA > 0.001)
                {
                    float2 bd = float2(i.w.x - _BtX, i.w.z - _BtZ);
                    float av = bd.x * _BtSn + bd.y * _BtCs;     // along, astern positive
                    float cv = bd.x * _BtCs - bd.y * _BtSn;     // across
                    if (av > -4.0 && av < 150.0)
                    {
                        float laceB = smoothstep(0.40, 0.84, fbm(fpM * 1.45 + float2(-_T*0.07, _T*0.06)));
                        float fleckB = smoothstep(0.56, 0.93, fbm(fpF * 3.90 + float2(_T*0.11, _T*0.09)));
                        float ac = abs(cv);
                        float arm = 2.2 + max(av, 0.0) * 0.35;
                        float ridge = 1.0 - smoothstep(1.5, 4.6, abs(ac - arm));
                        float churn = (1.0 - smoothstep(2.2, 6.5, ac)) * exp(-max(av, 0.0) * 0.026);
                        float fadeB = 1.0 - smoothstep(0.0, 132.0, max(av, 0.0));
                        float fB = saturate(ridge * fadeB + churn) * _BtA;
                        col = lerp(col, float3(0.96, 0.99, 1.0), saturate(fB * (0.92 + 0.28 * laceB + 0.20 * fleckB)) * 0.97);
                    }
                }
                col += pow(max(dot(N, H), 0.0), 480.0) * 0.6 * shade;
                // ---- where things meet the water: a ring of foam round every buoy, log, rock and the
                // hull, and the water going clear and turquoise as it shoals against the beach ----
                {
                    float2 suv = i.sp.xy / max(1e-4, i.sp.w);
                    float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float under = scene - i.sp.w;                              // feet of water between the surface and whatever is beneath it
                    if (under > 0.0 && under < 40.0)
                    {
                        float tear = fbm(fpF * 2.6 + float2(_T * 0.13, -_T * 0.10));
                        float ring = (1.0 - smoothstep(0.0, 0.55 + 0.45 * tear, under)) * smoothstep(0.30, 0.65, tear + 0.25);
                        float shallow = 1.0 - smoothstep(0.0, 5.0, under);
                        col = lerp(col, col * float3(0.82, 1.06, 1.04) + float3(0.02, 0.10, 0.09), shallow * 0.45);
                        col = lerp(col, float3(0.95, 0.98, 1.0), saturate(ring) * 0.85 * (1.0 - smoothstep(60.0, 140.0, dCam)));
                    }
                }
                // aerial perspective: into the horizon's own colour
                float fog = smoothstep(70.0, 230.0, dCam);
                col = lerp(col, _SkyLo.rgb, fog);
                // the maths above runs in plain sRGB numbers, as the web's does; to linear here
                col = pow(saturate(col), 2.2);
                // the brightest glints go past white, so bloom picks out the sparkle and leaves the rest of the sea alone
                col += pow(_Glow.rgb, 2.2) * max(0.0, (spec + gl) * shade - 0.30) * 2.6 * (1.0 - fog);
                return half4(col * _Light, 1);
            }
            ENDHLSL
        }
    }
}
