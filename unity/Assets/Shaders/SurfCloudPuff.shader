// Cumulus as a pile of lit puffs. Each puff is a quad faced to the lens in the vertex program and
// shaded in the fragment as a sphere of soft vapour: the sun's light wrapped round it, the cloud's
// own underside in its shade, the sky's colour filling the rest, a silver edge where the sun is
// behind it, and a little noise inside so no two puffs are smooth balls. The colours are the live
// sky's (raw numbers, converted at the end like the sky itself) and the sun's.
// Vertex data: position = the puff's centre in the cloud's frame, uv = (radius, seed),
// uv2 = the corner (-1..1), uv3 = (height in the cloud 0..1, the cloud's opacity).
Shader "Surf/CloudPuff"
{
    Properties
    {
        _Opacity ("Opacity", Float) = 0.95
        _Sky ("Sky (raw)", Vector) = (0.36, 0.5, 0.66, 1)
        _Hz ("Horizon (raw)", Vector) = (0.85, 0.64, 0.39, 1)
        _SunCol ("Sun colour (raw)", Vector) = (1, 0.75, 0.45, 1)
        _SunDir ("Sun direction", Vector) = (0.3, 0.2, -0.9, 0)
        _T ("Time", Float) = 0
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

            CBUFFER_START(UnityPerMaterial)
            float _Opacity, _T;
            float4 _Sky, _Hz, _SunCol, _SunDir;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; float2 uv3 : TEXCOORD2; };
            struct V { float4 pos : SV_POSITION; float2 q : TEXCOORD0; float seed : TEXCOORD1; float2 hk : TEXCOORD2; float3 wc : TEXCOORD3; float3 cn : TEXCOORD4; };

            V vert(A i)
            {
                V o;
                float3 wc = TransformObjectToWorld(i.pos.xyz);
                float3 vp = TransformWorldToView(wc);
                vp.xy += i.uv2 * i.uv.x;
                o.pos = TransformWViewToHClip(vp);
                o.q = i.uv2; o.seed = i.uv.y; o.hk = i.uv3; o.wc = wc;
                // the cloud-scale normal: from the cloud's own middle out through this puff, so the
                // whole pile is lit as one body and not as a heap of separate balls
                float3 cc = TransformObjectToWorld(float3(0, 0, 0));
                o.cn = normalize(wc - cc + float3(0, 0.35, 0) * length(wc - cc));
                return o;
            }

            float h21(float2 p) { p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float vn(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(h21(i), h21(i + float2(1, 0)), f.x), lerp(h21(i + float2(0, 1)), h21(i + float2(1, 1)), f.x), f.y); }
            float fbm(float2 p) { float v = 0, a = 0.5; [unroll] for (int k = 0; k < 4; k++) { v += a * vn(p); p = p * 2.03 + 17.1; a *= 0.5; } return v / 0.9375; }

            half4 frag(V i) : SV_Target
            {
                float2 q = i.q;
                // a soft, lumpy rim: the edge wanders with noise so the outline is vapour, not a circle
                float ang = atan2(q.y, q.x);
                float wob = fbm(float2(ang * 1.6 + i.seed * 31.0, i.seed * 7.0 + _T * 0.02)) - 0.5;
                float d = length(q) / (0.86 + 0.16 * wob);
                if (d >= 1.0) discard;
                float z = sqrt(saturate(1.0 - d * d));
                // the puff's own sphere normal, in view space, to world
                float3 nv = float3(q.x, q.y, z);
                float3 n = normalize(mul((float3x3)UNITY_MATRIX_I_V, nv));
                n = normalize(lerp(n, i.cn, 0.45));
                float3 L = normalize(_SunDir.xyz);
                float3 Vd = normalize(_WorldSpaceCameraPos - i.wc);
                // internal detail
                float det = fbm(q * 2.3 + i.seed * 13.0 + float2(_T * 0.01, 0));
                // light: the sun wrapped half round, shadowed toward the cloud's base, the sky above,
                // the warm horizon bounced up into the underside
                float wrap = saturate((dot(n, L) + 0.45) / 1.45);
                float base = i.hk.x;                                      // 0 at the base, 1 at the crown
                float self = lerp(0.42, 1.0, smoothstep(0.0, 0.85, base)) * lerp(0.82, 1.06, det);
                float3 sky = _Sky.rgb, hz = _Hz.rgb, sun = _SunCol.rgb;
                float3 amb = lerp(hz * 0.92, sky * 1.15 + 0.10, saturate(n.y * 0.5 + 0.5));
                float3 col = amb * 0.62 + sun * wrap * 0.78 * self;
                col = lerp(col, col * float3(0.80, 0.84, 0.95), (1.0 - base) * 0.45);   // a cool, shaded underside
                // the silver lining: thin vapour at the rim with the sun behind it
                float rim = pow(1.0 - z, 2.2);
                float back = pow(saturate(dot(-Vd, L)), 3.0);
                col += sun * rim * (0.35 + 1.4 * back);
                col = max(col, 0.0);
                float a = _Opacity * i.hk.y * smoothstep(1.0, 0.62, d) * lerp(0.86, 1.0, det);
                col = pow(saturate(col), 2.2);
                return half4(col * a, a);
            }
            ENDHLSL
        }
    }
}
