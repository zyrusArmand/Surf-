// A cloud: one soft cut-out on a quad, drawn after the opaque world with no depth write, so
// the sea and the palm paint over it wherever they are nearer and it never cuts a square out
// of anything. Unlit, like the web's MeshBasicMaterial: a cloud is its own light.
Shader "Surf/Cloud"
{
    Properties
    {
        _BaseMap ("Cloud", 2D) = "white" {}
        _Color ("Tint (alpha is opacity)", Color) = (1, 1, 1, 0.8)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _Color;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(c.rgb * _Color.rgb, c.a * _Color.a);
            }
            ENDHLSL
        }
    }
}
