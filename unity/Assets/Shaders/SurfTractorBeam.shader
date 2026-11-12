// The saucer's beam as light through hazy air rather than a green cone: soft at its edges and
// full through its middle (the cone is drawn from both sides and each side fades where it turns
// edge-on to the lens, so the two together make a shaft with body), a whiter core, faint streaks
// running down its length, bright bands climbing it toward the hull so it reads as PULLING, and
// brighter than white so the bloom makes it glow. Additive: it lights what it crosses and hides
// nothing. uv.y runs 1 at the hull to 0 at the sea; _Len is its length in feet, so the bands keep
// their spacing whatever the beam's reach.
Shader "Surf/TractorBeam"
{
    Properties
    {
        _Color ("Colour", Color) = (0.49, 1, 0.6, 1)
        _Alpha ("Strength", Float) = 0
        _Len ("Length, feet", Float) = 10
        _Glow ("Glow", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Alpha, _Len, _Glow;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 w : TEXCOORD1; float3 n : TEXCOORD2; };
            V vert(A i)
            {
                V o; o.w = TransformObjectToWorld(i.pos.xyz); o.n = TransformObjectToWorldNormal(i.n);
                o.uv = i.uv; o.pos = TransformWorldToHClip(o.w); return o;
            }
            float h11(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float n11(float x) { float i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f); return lerp(h11(i), h11(i + 1.0), f); }
            half4 frag(V i) : SV_Target
            {
                float3 Vd = normalize(_WorldSpaceCameraPos - i.w), N = normalize(i.n);
                float face = abs(dot(N, Vd));                         // 1 facing the lens, 0 edge-on
                float body = pow(face, 1.7);                          // soft edges, full through the middle
                float core = pow(face, 7.0);                          // the hot core of the shaft
                float along = i.uv.y, feet = (1.0 - along) * _Len;    // 0 at the hull, _Len at the sea
                float T = _Time.y;
                // streaks down its length: a ring of soft lines round the cone, slowly turning
                float ang = i.uv.x * 6.2831853;
                float streak = 0.72 + 0.28 * n11(i.uv.x * 23.0 + T * 0.35) * (0.6 + 0.4 * n11(i.uv.x * 7.0 - T * 0.2));
                // bands of light climbing it toward the hull, a couple of feet apart, uneven
                float climb = feet * 0.55 + T * 3.2;
                float band = pow(0.5 + 0.5 * sin(climb * 3.14159 + n11(i.uv.x * 5.0) * 2.0), 6.0);
                // brightest leaving the hull, a little dimmer through the middle, flaring where it meets the sea
                float prof = lerp(0.62, 1.0, pow(along, 0.6)) + 0.35 * smoothstep(0.12, 0.0, along);
                // the far end arrives soft rather than as a cut edge
                prof *= smoothstep(0.0, 0.04, along) * 0.85 + 0.15;
                float I = (body * (0.55 + 0.30 * band) * streak + core * 0.9) * prof;
                float3 col = lerp(_Color.rgb, float3(0.9, 1.0, 0.92), saturate(core * 0.8 + band * 0.15));
                return half4(col * I * _Glow * saturate(_Alpha), 0);
            }
            ENDHLSL
        }
    }
}
