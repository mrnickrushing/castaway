// Blueprint of a structure not built yet: see-through glowing green, solid up to _Fill of its height.
Shader "LastTide/Ghost"
{
    Properties
    {
        _Color ("Color", Color) = (0.4, 1, 0.35, 0.35)
        _Fill ("Fill", Range(0,1)) = 0
        _MinY ("Bottom (world)", Float) = 0
        _Height ("Height", Float) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite On
            ColorMask 0
        }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fill, _MinY, _Height;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; float4 col : COLOR; };
            V vert(A v) { V o; o.ws = TransformObjectToWorld(v.pos.xyz); o.pos = TransformWorldToHClip(o.ws); o.n = TransformObjectToWorldNormal(v.n); o.col = v.col; return o; }
            half4 frag(V i) : SV_Target
            {
                float h = saturate((i.ws.y - _MinY) / max(0.01, _Height));
                float solid = step(h, _Fill);
                float3 view = normalize(_WorldSpaceCameraPos - i.ws);
                float edge = pow(1 - saturate(abs(dot(normalize(i.n), view))), 2);
                float scan = 0.5 + 0.5 * sin(i.ws.y * 9 - _Time.y * 4);
                float3 c = lerp(_Color.rgb * (0.8 + edge * 0.8 + scan * 0.15), i.col.rgb * 0.9 + _Color.rgb * 0.25, solid);
                float a = lerp(_Color.a * (0.6 + edge * 0.9), 0.92, solid);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
