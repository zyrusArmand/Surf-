// Cumulus, one quad per cloud facing the lens. The puffs are not drawn as discs: they are summed
// into one smooth density field (metaballs), the base cut flat, the edges eroded with noise into
// billows and wisps, and the result lit as a relief from the field's own slope: the sun wrapped
// over the crowns, the sky's colour in the shaded folds, a cool underside, thin vapour at the rim
// taking the light through it. Colours are the live sky's raw numbers, converted at the end like
// the sky itself. Per cloud: _P[i] = (x, y, radius, 0) in the quad's own feet, _N puffs, _H its
// height, _Op its opacity, _Seed its noise.
Shader "Surf/Cumulus"
{
    Properties
    {
        _Sky ("Sky (raw)", Vector) = (0.34, 0.46, 0.58, 1)
        _Hz ("Horizon (raw)", Vector) = (0.85, 0.64, 0.39, 1)
        _SunCol ("Sun colour (raw)", Vector) = (1, 0.86, 0.68, 1)
        _SunDir ("Sun direction", Vector) = (0.3, 0.3, -0.9, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Sky, _Hz, _SunCol, _SunDir;
            float4 _P[40];
            float _N, _H, _Op, _Seed;

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float2 p : TEXCOORD0; };

            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.p = i.pos.xy; return o; }

            float h21(float2 p) { p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float vn(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(h21(i), h21(i + float2(1, 0)), f.x), lerp(h21(i + float2(0, 1)), h21(i + float2(1, 1)), f.x), f.y); }
            float fbm(float2 p) { float v = 0, a = 0.5; [unroll] for (int k = 0; k < 5; k++) { v += a * vn(p); p = p * 2.03 + 17.1; a *= 0.5; } return v / 0.96875; }

            half4 frag(V i) : SV_Target
            {
                float2 p = i.p; float D = 0; float2 g = 0;
                [loop] for (int k = 0; k < 40; k++)
                {
                    if (k >= (int)_N) break;
                    float4 c = _P[k]; float2 d = p - c.xy; float r2 = c.z * c.z;
                    float e = exp(-dot(d, d) / r2); D += e; g += -2.0 * d / r2 * e;
                }
                D *= smoothstep(-0.02 * _H, 0.10 * _H, p.y);                 // a flat base
                float n1 = fbm(p * 0.09 + _Seed), n2 = fbm(p * 0.28 + _Seed * 1.7);
                float edgeZone = 1.0 - smoothstep(0.5, 1.3, D);
                float dens = D + (n1 - 0.5) * (0.45 + 0.5 * edgeZone) + (n2 - 0.5) * (0.18 + 0.25 * edgeZone);
                float a = smoothstep(0.36, 0.80, dens) * _Op;
                if (a < 0.003) discard;
                // the relief: the field's slope, plus broad billows, facing the lens
                float n3 = fbm(p * 0.16 + _Seed * 2.3), n4 = fbm(p * 0.16 + _Seed * 5.1 + 9.0);
                float3 n = normalize(float3(-g.x * 1.35 + (n3 - 0.5) * 0.9, -g.y * 1.35 + 0.30 + (n4 - 0.5) * 0.7, 1.0));
                float3 L = normalize(mul((float3x3)UNITY_MATRIX_V, _SunDir.xyz));   // the sun in the lens's own frame
                float wrap = saturate((dot(n, L) + 0.28) / 1.28); wrap = wrap * wrap * (3.0 - 2.0 * wrap);
                float hgt = saturate(p.y / max(0.01, _H));
                float3 shade = lerp(_Sky.rgb * 0.95 + 0.10, _Hz.rgb * 0.75 + 0.08, 0.35);
                float3 col = lerp(shade * 0.92, _SunCol.rgb * 1.12 + 0.06, wrap) * (0.74 + 0.36 * smoothstep(0.0, 0.8, hgt)) * lerp(0.94, 1.04, n1);
                col = lerp(col, col * float3(0.74, 0.80, 0.94), (1.0 - smoothstep(0.0, 0.45, hgt)) * 0.55);
                float thin = 1.0 - smoothstep(0.42, 0.85, dens);
                col += _SunCol.rgb * thin * 0.22;
                col = pow(saturate(col), 2.2);
                return half4(col * a, a);
            }
            ENDHLSL
        }
    }
}
