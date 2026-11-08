// The green action ring on the ground around everything the player can use.
//   uv.x : angle around the ring, 0..1 (clockwise from the top)
//   uv.y : 0 inner edge .. 1 outer edge
//   _Fill : 0..1 progress of the current action, drawn as a bright sweep
Shader "LastTide/Ring"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 1, 0.3, 0.85)
        _Fill ("Fill", Range(0,1)) = 0
        _Pulse ("Pulse", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -2, -2
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fill, _Pulse;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A v) { V o; o.pos = TransformObjectToHClip(v.pos.xyz); o.uv = v.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float r = i.uv.y;
                // Soft-edged band with a bright rim on the outside, like a glowing decal.
                float band = smoothstep(0.0, 0.18, r) * smoothstep(1.0, 0.82, r);
                float rim = smoothstep(0.55, 0.8, r) * smoothstep(1.0, 0.85, r);
                float filled = step(i.uv.x, _Fill) * step(0.001, _Fill);
                float3 c = _Color.rgb * (0.75 + rim * 0.6 + _Pulse * 0.3) + filled * float3(0.6, 0.6, 0.45);
                float a = _Color.a * band * (0.55 + rim * 0.45 + filled * 0.3);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
