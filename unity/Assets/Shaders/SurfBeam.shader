// A flat unlit colour with an opacity, blended whichever way the material asks: additive
// (One One) for the saucer's beam, which is light and should brighten whatever it crosses and
// hide nothing behind it; ordinary alpha for the drops off its hull. No depth write, double
// sided, as the web's MeshBasicMaterial is set up for both.
Shader "Surf/Beam"
{
    Properties
    {
        _Color ("Colour", Color) = (0.49, 1, 0.6, 1)
        _Alpha ("Opacity", Float) = 0.3
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Alpha, _SrcBlend, _DstBlend;
            CBUFFER_END
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }
            half4 frag(V i) : SV_Target
            {
                // premultiplied, so One/One adds colour * alpha and SrcAlpha/OneMinus blends it
                float a = saturate(_Alpha);
                return half4(_Color.rgb * a, a);
            }
            ENDHLSL
        }
    }
}
