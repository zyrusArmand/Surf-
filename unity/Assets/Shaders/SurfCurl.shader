// The curling lip of the set wave, the web game's shader brought across: a sheet of aerated
// water rippling along its length, lit through with green, laced with foam toward the thrown
// edge, misting where it falls, going white as the section closes out. Colours are the web's
// raw numbers, converted at the end like the sea's.
Shader "Surf/Curl"
{
    Properties
    {
        _Fade ("Fade", Float) = 0
        _T ("Time", Float) = 0
        _Light ("Daylight", Float) = 1
        _Close ("Closing", Float) = 0
        _Cut ("Cut", Float) = 0
        _SunDir ("Sun direction", Vector) = (-0.45, 0.75, -0.5, 0)
        _Sky ("Sky", Vector) = (0.56, 0.82, 0.96, 1)
        _Deep ("Deep", Vector) = (0.039, 0.369, 0.612, 1)
        _Shallow ("Shallow", Vector) = (0.2, 0.682, 0.871, 1)
        _Foam ("Foam", Vector) = (0.96, 0.99, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Fade, _T, _Light, _Close, _Cut;
            float4 _SunDir, _Sky, _Deep, _Shallow, _Foam;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 w : TEXCOORD1; float3 n : TEXCOORD2; float fall : TEXCOORD3; };

            V vert(A i)
            {
                V o;
                o.uv = i.uv; o.fall = i.uv2.x;
                float ripple = sin(i.pos.z * 0.20 + _T * 1.55) * 0.42
                             + sin(i.pos.z * 0.41 - _T * 2.10 + i.uv.x * 4.2) * 0.20
                             + sin(i.uv.x * 7.0 - _T * 1.15) * 0.13
                             + sin(i.uv.x * 2.3 - _T * 2.60 + i.pos.z * 0.08) * 0.34 * smoothstep(0.25, 1.0, i.uv.x);
                float3 disp = i.pos.xyz + i.n * ripple * smoothstep(0.0, 0.20, i.uv.x);
                o.w = TransformObjectToWorld(disp);
                o.n = TransformObjectToWorldNormal(i.n);
                o.pos = TransformWorldToHClip(o.w);
                return o;
            }

            float h21(float2 p){ p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float vn(float2 p){ float2 i = floor(p), f = frac(p); f = f*f*(3.0-2.0*f);
                return lerp(lerp(h21(i), h21(i+float2(1,0)), f.x), lerp(h21(i+float2(0,1)), h21(i+float2(1,1)), f.x), f.y); }
            float fbm(float2 p){ float v = 0, a = 0.5; float2x2 m = float2x2(1.62, 1.18, -1.18, 1.62);
                [unroll] for (int k = 0; k < 3; k++){ v += a * vn(p); p = mul(m, p); a *= 0.5; } return v / 0.875; }

            half4 frag(V i) : SV_Target
            {
                float t = i.uv.x;
                float3 N = normalize(i.n);
                float3 L = normalize(_SunDir.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.w);
                if (dot(N, V) < 0.0) N = -N;
                float diff = max(dot(N, L), 0.0);
                float3 H = normalize(L + V);
                float spec = pow(max(dot(N, H), 0.0), 120.0) * 0.85;
                float fres = pow(1.0 - max(dot(N, V), 0.0), 3.0);
                float3 base = lerp(_Deep.rgb, _Shallow.rgb, saturate(0.62 + t * 0.34));
                float3 col = base * (0.38 + 0.72 * diff) + _Sky.rgb * fres * 0.5 + spec;
                col += float3(0.10, 0.60, 0.62) * (1.0 + 0.35 * smoothstep(0.10, 1.0, t)) * 0.30;
                float za = i.w.z;
                float flow = _T * 0.38;
                float warp = vn(float2(za * 0.07, t * 0.60)) * 1.30;
                float band = fbm(float2(t * 0.85 - flow + warp * 0.40, za * 0.27));
                float grain = fbm(float2(t * 2.40 - flow * 1.7 + warp * 0.70, za * 0.86));
                float lace = smoothstep(0.34, 0.80, band);
                float fleck = smoothstep(0.46, 0.88, grain);
                float sheet = smoothstep(0.30, 1.0, t);
                float foam = saturate(lace * (0.40 + sheet * 0.62) + fleck * (0.26 + sheet * 0.44));
                col = lerp(col, _Foam.rgb, foam * 0.62);
                float cloudA = fbm(float2(t * 1.1 - flow * 0.5 + warp * 0.5, za * 0.16));
                float mottle = smoothstep(0.30, 0.85, cloudA) * smoothstep(0.06, 0.30, t);
                col = lerp(col, float3(0.86, 0.93, 0.96), mottle * 0.52);
                float edge = smoothstep(0.70, 1.0, t);
                col = lerp(col, _Foam.rgb, saturate(edge * (0.42 + 0.62 * band + 0.48 * grain)) * 0.95);
                float boil = fbm(float2(i.w.z * 0.60 + _T * 0.85, i.fall * 3.2 - _T * 1.55));
                float nearBrk = smoothstep(-12.9, 24.0, i.w.z);
                float cloud = fbm(float2(i.w.z * 0.30 + _T * 0.48, i.fall * 1.6 - _T * 0.85));
                float mist = smoothstep(0.02, 0.55, i.fall) * saturate(0.32 + 0.95 * cloud);
                col = lerp(col, float3(0.90, 0.95, 0.97), mist * 0.84);
                float churn = smoothstep(0.14, 0.66, i.fall) * saturate(0.66 + 0.72 * boil);
                col = lerp(col, float3(0.93, 0.97, 0.99), churn * 0.97);
                float crash2 = smoothstep(0.03, 0.45, i.fall) * nearBrk * saturate(0.45 + 0.75 * boil);
                col = lerp(col, float3(0.95, 0.98, 1.00), crash2 * 0.90);
                col = lerp(col, _Deep.rgb * 0.62, (1.0 - smoothstep(0.10, 0.80, i.uv.y)) * 0.30);
                col = lerp(col, _Foam.rgb, (1.0 - smoothstep(0.0, 0.15, t)) * saturate(0.45 + 0.55 * lace + 0.35 * fleck) * 0.40);
                col = lerp(col, _Foam.rgb, _Close * 0.92);
                float a = _Fade * (0.99 - 0.14 * t);
                // the near wall thins while he is inside, so the lens can see him
                a *= 1.0 - _Cut * 0.55 * (1.0 - smoothstep(0.55, 1.0, t));
                col = pow(saturate(col), 2.2) * _Light;
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }
}
