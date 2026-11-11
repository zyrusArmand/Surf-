// The sea. The vertex shader runs the same seven-wave field WaveField.cs runs on the CPU, so
// what the board rides and what you see are one surface. The fragment is the web game's water
// brought across: deep-to-shallow colour by height, the sky REFLECTED off a chopped normal with
// the sun's halo on it, a glitter track, light through the crests, and foam where the water is
// high AND steep rather than merely high.
Shader "Surf/Ocean"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.027, 0.298, 0.506, 1)
        _Shallow ("Shallow", Color) = (0.153, 0.573, 0.769, 1)
        _SkyLo ("Sky horizon", Color) = (0.56, 0.82, 0.96, 1)
        _SkyHi ("Sky zenith", Color) = (0.25, 0.56, 0.84, 1)
        _Glow ("Sun glow", Color) = (1, 0.95, 0.84, 1)
        _SunDir ("Sun direction", Vector) = (-0.45, 0.75, -0.5, 0)
        _Light ("Daylight", Float) = 1
        _Amp ("Swell amplitude", Float) = 1.35
        _Peak ("Peak height", Float) = 2.85
        _Dist ("Field offset", Float) = 0
        _T ("Time", Float) = 0
        _WakeX ("Wake x", Float) = 0
        _WakeZ ("Wake z", Float) = 0
        _WakeA ("Wake amount", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Deep, _Shallow, _SkyLo, _SkyHi, _Glow, _SunDir;
            float _Light, _Amp, _Peak, _Dist, _T, _WakeX, _WakeZ, _WakeA;
            CBUFFER_END
            float4 _WaveA[7];   // dx, dz, k, a
            float4 _WaveB[7];   // ek, ep, w, -

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float3 n : TEXCOORD1; float h : TEXCOORD2; };

            // the field: height and its two slopes at a field point p (x, dist*drift+z)
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
                h *= _Amp; dhx *= _Amp; dhz *= _Amp;
                // the rider's wake: a ridge in the water itself, so it is lit and foamed like a wave
                if (_WakeA > 0.001)
                {
                    float dz = p.y - (_Dist + _WakeZ);
                    float ez = exp(-dz * dz / 18.0) * (dz > 0 ? 1 : 0);
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
                wp.y = h;
                o.w = wp;
                o.n = normalize(float3(-dhx, 1.0, -dhz));
                o.h = h;
                o.pos = TransformWorldToHClip(wp);
                return o;
            }

            float h21(float2 p){ p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float vn(float2 p){ float2 i = floor(p), f = frac(p); f = f*f*(3.0-2.0*f);
                return lerp(lerp(h21(i), h21(i+float2(1,0)), f.x), lerp(h21(i+float2(0,1)), h21(i+float2(1,1)), f.x), f.y); }
            float fbm(float2 p){ float v = 0, a = 0.5; float2x2 m = float2x2(1.62, 1.18, -1.18, 1.62);
                [unroll] for (int k = 0; k < 3; k++){ v += a * vn(p); p = mul(m, p); a *= 0.5; } return v / 0.875; }

            half4 frag(V i) : SV_Target
            {
                float3 N = normalize(i.n);
                float3 L = normalize(_SunDir.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.w);
                float dCam = length(_WorldSpaceCameraPos - i.w);
                float diff = max(dot(N, L), 0.0);
                float3 H = normalize(L + V);
                // chop: a centimetre-scale tilt on the normal, at three scales, the finest only close in
                float2 gp = float2(i.w.x, i.w.z + _Dist * 0.22);
                float2 wob = float2(fbm(gp*1.85 + float2(_T*0.21, -_T*0.17)) - 0.5,
                                    fbm(gp*2.30 + float2(-_T*0.19, _T*0.23)) - 0.5);
                wob += float2(fbm(gp*6.40 + float2(_T*0.44, _T*0.31)) - 0.5,
                              fbm(gp*7.90 + float2(-_T*0.37, _T*0.41)) - 0.5) * 0.55 * (1.0 - smoothstep(6.0, 42.0, dCam));
                float3 Nr = normalize(N + float3(wob.x, 0, wob.y) * 0.20);
                float spec = pow(max(dot(Nr, H), 0.0), 140.0) * 0.9;
                float fres = pow(1.0 - max(dot(Nr, V), 0.0), 3.0);
                // the glitter track, keyed to a roughened normal, a long path when the sun is low
                float3 Ng = normalize(N + float3(wob.x, 0, wob.y) * 0.44);
                float lowSun = smoothstep(0.46, 0.04, L.y);
                float gl = pow(max(dot(Ng, H), 0.0), lerp(78.0, 22.0, lowSun)) * 0.62;
                gl *= smoothstep(-0.10, 0.05, L.y) * lerp(0.55, 1.0, lowSun);
                float hS = i.h;
                float3 base = lerp(_Deep.rgb, _Shallow.rgb, saturate(hS * 0.28 + 0.5));
                float3 lit = base * (0.38 + 0.72 * diff);
                // the sky reflected off the ripple, with the sun's halo laid into the dome
                float3 R = reflect(-V, Nr);
                float ry = saturate(R.y);
                float3 skyRefl = lerp(_SkyLo.rgb, _SkyHi.rgb, smoothstep(0.0, 0.62, ry));
                skyRefl += _Glow.rgb * pow(max(dot(R, L), 0.0), 40.0) * 0.55 * smoothstep(-0.12, 0.06, L.y);
                float3 col = lerp(lit, skyRefl, fres * 0.88) + spec;
                col += _Glow.rgb * gl;
                // light through the crests
                float thin = smoothstep(0.25 * _Peak, 0.95 * _Peak, hS);
                float toSun = pow(max(dot(V, -L), 0.0), 2.6);
                float sss = thin * (0.35 + 0.65 * toSun) * (1.0 - fres * 0.5);
                col += float3(0.08, 0.42, 0.40) * sss * 0.55;
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
                col += pow(max(dot(N, H), 0.0), 480.0) * 0.6;
                // aerial perspective: into the horizon's own colour
                float fog = smoothstep(70.0, 230.0, dCam);
                col = lerp(col, _SkyLo.rgb, fog);
                return half4(col * _Light, 1);
            }
            ENDHLSL
        }
    }
}
