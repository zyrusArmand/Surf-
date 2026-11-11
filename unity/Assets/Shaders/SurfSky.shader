// The dome: a gradient from zenith to horizon with the sun's glow laid along it, and the sun
// itself drawn as a hot disc with a soft rim. Driven by DayCycle, which walks the same colour
// table the web game walks.
Shader "Surf/Sky"
{
    Properties
    {
        _Top ("Zenith", Vector) = (0.25, 0.56, 0.84, 1)
        _Mid ("Mid", Vector) = (0.56, 0.82, 0.96, 1)
        _Horizon ("Horizon", Vector) = (0.90, 0.96, 0.99, 1)
        _Glow ("Glow", Vector) = (1, 0.97, 0.92, 1)
        _GlowAmt ("Glow amount", Float) = 0.1
        _SunDir ("Sun direction", Vector) = (-0.45, 0.75, -0.5, 0)
        _SunSize ("Sun size", Float) = 0.045
        _Blend ("Horizon blend", Float) = 0.30
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Top, _Mid, _Horizon, _Glow, _SunDir;
            float _GlowAmt, _SunSize, _Blend;
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 d : TEXCOORD0; };
            V vert(A i){ V o; o.pos = UnityObjectToClipPos(i.pos); o.d = i.pos.xyz; return o; }
            fixed4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.d);
                float3 L = normalize(_SunDir.xyz);
                float up = saturate(d.y);
                // horizon band, mid, zenith; the horizon carries further up as the day warms
                float3 col = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, _Blend, up));
                col = lerp(col, _Top.rgb, smoothstep(_Blend, 1.0, up));
                // the glow along the horizon toward the sun
                float toSun = max(dot(normalize(float3(d.x, 0, d.z)), normalize(float3(L.x, 0.0001, L.z))), 0.0);
                float band = exp(-up * 6.0) * pow(toSun, 3.0);
                col = lerp(col, _Glow.rgb, band * _GlowAmt * 1.6);
                // the sun: a disc and a rim, cut at the horizon
                float cosA = dot(d, L);
                float disc = smoothstep(cos(_SunSize * 1.15), cos(_SunSize * 0.85), cosA);
                float rim = pow(max(cosA, 0.0), 180.0) * 0.55 + pow(max(cosA, 0.0), 24.0) * 0.18 * (0.4 + _GlowAmt);
                float above = smoothstep(-0.02, 0.01, d.y);
                col += (_Glow.rgb * rim + float3(1.4, 1.3, 1.1) * _Glow.rgb * disc) * above;
                // below the horizon there is sea, never drawn here: fade to the horizon colour
                col = lerp(_Horizon.rgb * 0.9, col, smoothstep(-0.05, 0.0, d.y));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
