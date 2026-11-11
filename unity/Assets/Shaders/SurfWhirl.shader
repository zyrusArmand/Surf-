// The whirlpool's surface: a spiral texture spun about its centre, faster toward the eye,
// drawn over the water and faded by how deep the dip has grown.
Shader "Surf/Whirl"
{
    Properties
    {
        _MainTex ("Spiral", 2D) = "white" {}
        _Spin ("Spin", Float) = 0
        _Alpha ("Alpha", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _Spin, _Alpha;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float2 wc = i.uv - 0.5;
                float wr = length(wc);
                float wa = atan2(wc.y, wc.x);
                wa += _Spin * (0.16 + 0.84 / (wr * 7.0 + 0.55));
                float2 wuv = float2(cos(wa), sin(wa)) * wr + 0.5;
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, wuv);
                return half4(t.rgb, t.a * _Alpha);
            }
            ENDHLSL
        }
    }
}
