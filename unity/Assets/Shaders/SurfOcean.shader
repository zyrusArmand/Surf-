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
                    float dz = p.y - (_Dist + _WakeZ);
                    // the ridge rises behind the tail, not AT it: switched on at the tail it lifted the
                    // water over the back of the board
                    float dzb = dz - 2.6;
                    float ez = exp(-dzb * dzb / 18.0) * smoothstep(0.0, 2.2, dz);
                    float ax = saturate(1.0 - abs(p.x - _WakeX) / 3.5);
                    h += _WakeA * ez * ax;
                }
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
                    float fz = saturate(abs(lp.z) / max(0.01, _HullS.y));
                    float half = _HullS.x * sqrt(saturate(1.0 - fz * fz * fz * fz));
                    float inside = (1.0 - smoothstep(half - 0.12, half + 0.10, abs(lp.x))) * (1.0 - smoothstep(0.97, 1.04, fz));
                    float upY = _HullW2L[1][1];                       // how a world rise moves along the board's own up
                    float floor0 = _HullS.z - 0.03;
                    if (inside > 0.001 && lp.y > floor0 && abs(upY) > 0.2)
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
                    // above the underside, which rises toward nose and tail with the rocker: cut lower
                    // than that and a hole opens to the sky under the ends; cut higher (the middle of the
                    // board's whole height, nose tip included) and water lies across the deck
                    if (qz < 1.0 && abs(hq.x) < qw && hq.y > _HullS.z + _HullC.z * qz * qz + 0.02) discard;
                }
                float3 N = normalize(i.n);
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
                    // the web's tiled chop map at three scales, the finest only close in
                    float2 ca = SAMPLE_TEXTURE2D(_Chop, sampler_Chop, gp*0.055 + float2( _T*0.0063, -_T*0.0052)).xy * 2.0 - 1.0;
                    float2 cb = SAMPLE_TEXTURE2D(_Chop, sampler_Chop, gp*0.168 + float2(-_T*0.0116,  _T*0.0097)).xy * 2.0 - 1.0;
                    wob = ca + cb * 0.78;
                    float2 cc = SAMPLE_TEXTURE2D(_Chop, sampler_Chop, gp*0.52 + float2( _T*0.021, -_T*0.017)).xy * 2.0 - 1.0;
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
                float shade = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.w));
                shade = lerp(shade, 1.0, GetMainLightShadowFade(i.w));
                // water is not a diffuse surface: a shadow on it is a faint dimming, never a hole. And
                // a low sun throws every shadow tens of feet across the sea, so they fade out as it sets
                shade = lerp(1.0, shade, 0.45 * smoothstep(0.10, 0.45, L.y));
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
                    float3 mw = float3(i.w.x, 2.0 * _ReflY - i.w.y, i.w.z);
                    mw.xz += (Nr.xz + float2(wob.x, wob.y) * 0.08) * (1.4 + 0.012 * dCam);
                    float4 rc = mul(_ReflVP, float4(mw, 1.0));
                    float2 ruv = rc.xy / max(1e-4, rc.w) * 0.5 + 0.5;
                    float inside = smoothstep(0.0, 0.04, ruv.x) * smoothstep(1.0, 0.96, ruv.x) * smoothstep(0.0, 0.04, ruv.y) * smoothstep(1.0, 0.96, ruv.y);
                    float3 rs = SAMPLE_TEXTURE2D(_ReflTex, sampler_ReflTex, saturate(ruv)).rgb;
                    rs = pow(max(rs, 0.0), 1.0 / 2.2);                       // back to the plain numbers this shader works in
                    skyRefl = lerp(skyRefl, rs, inside * (rc.w > 0.0 ? 0.92 : 0.0));
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
                    float wf = exp(-dz * dz / 60.0) * (dz > 0 ? 1 : 0) * saturate(1.0 - abs(i.w.x - _WakeX) / 2.4) * _WakeA;
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
