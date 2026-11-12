// Where the saucer's beam meets the sea: a pool of green light on the water, brightest in the
// middle, with rings of it running outward, brighter than white so the bloom spreads it. Additive,
// laid flat a hand's width over the surface so the swell can still rise through its edge.
Shader "Surf/BeamPool"
{
    Properties
    {
        _Color ("Colour", Color) = (0.49, 1, 0.6, 1)
        _Alpha ("Strength", Float) = 0
        _Glow ("Glow", Float) = 1.6
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
                float4 _Color; float _Alpha, _Glow;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;                       // 0 in the middle, 1 at the rim
                if (d >= 1.0) discard;
                float T = _Time.y;
                float glow = exp(-d * d * 3.2);
                float rings = pow(0.5 + 0.5 * sin((d * 7.0 - T * 2.6) * 3.14159), 8.0) * (1.0 - d) * 0.55;
                float I = (glow * 0.85 + rings) * smoothstep(1.0, 0.75, d);
                float3 col = lerp(_Color.rgb, float3(0.85, 1.0, 0.9), glow * 0.5);
                return half4(col * I * _Glow * saturate(_Alpha), 0);
            }
            ENDHLSL
        }
    }
}
