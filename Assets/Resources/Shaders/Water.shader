Shader "LastTide/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.2, 0.46, 0.5, 0.7)
        _DeepColor ("Deep", Color) = (0.05, 0.19, 0.4, 0.95)
        _WaveTex ("Wave Noise", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_WaveTex); SAMPLER(sampler_WaveTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _WaveTex_ST;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 color : COLOR; float fogCoord : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                ws.y += sin(ws.x * 0.35 + _Time.y * 1.3) * 0.04 + cos(ws.z * 0.28 + _Time.y) * 0.04;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.color = v.color;
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv1 = i.positionWS.xz * 0.05 + _Time.y * float2(0.02, 0.013);
                float2 uv2 = i.positionWS.xz * 0.08 - _Time.y * float2(0.017, 0.021);
                float2 uv3 = i.positionWS.xz * 0.21 + _Time.y * float2(-0.031, 0.024); // small chop for the sun to glint on
                const float e = 0.012;
                float a1 = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv1).r, a2 = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv2).r, a3 = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv3).r;
                float n = a1 * 0.5 + a2 * 0.5;
                // Wave slopes from finite differences of the two swells and the chop: a real normal, not a tilt.
                float dx = (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv1 + float2(e, 0)).r - a1) + (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv2 + float2(e, 0)).r - a2) + (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv3 + float2(e, 0)).r - a3) * 0.6;
                float dz = (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv1 + float2(0, e)).r - a1) + (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv2 + float2(0, e)).r - a2) + (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv3 + float2(0, e)).r - a3) * 0.6;
                float depth = saturate(i.color.r);
                float4 c = lerp(_ShallowColor, _DeepColor, smoothstep(0.0, 0.75, depth));
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fres = pow(1 - saturate(view.y), 3);
                // Foam where the water laps the shore, broken up and drifting; flecks of white on the crests out deep.
                float foamNoise = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, i.positionWS.xz * 0.3 + _Time.y * float2(0.05, -0.04)).r;
                float foam = saturate((0.07 - depth) / 0.07) * smoothstep(0.45, 0.75, foamNoise + (1 - depth) * 0.15);
                // From far away the chop would alias into glitter: calm the normal and the flecks with distance.
                float far = saturate((distance(i.positionWS, _WorldSpaceCameraPos) - 90) / 220);
                float flecks = smoothstep(0.8, 0.92, a3 * 0.6 + n * 0.4) * smoothstep(0.25, 0.6, depth) * 0.25 * (1 - far);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                float bump = lerp(7, 1.6, far);
                inputData.normalWS = normalize(float3(-dx * bump, 1, -dz * bump));
                inputData.viewDirectionWS = view;
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // Clear tropical water: what lies under the surface (the opaque scene, nudged by the waves), absorbed
                // with real depth from the depth texture, so shallows show the sand and deep water goes blue.
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float waterZ = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float thick = max(0, LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams) - waterZ);
                float2 ruv = suv + inputData.normalWS.xz * 0.018 * saturate(thick * 0.6) * (1 - far);
                // Don't refract things in front of the water into it.
                float thickR = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams) - waterZ;
                if (thickR < 0) { ruv = suv; } else { thick = thickR; }
                float3 under = SampleSceneColor(ruv);
                // Caustics: two drifting cell patterns on the bottom, strongest in the shallows.
                float2 cuv = i.positionWS.xz * 0.45;
                float ca = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, cuv * 0.6 + _Time.y * float2(0.03, 0.02)).r;
                float cb = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, cuv * 0.8 - _Time.y * float2(0.025, 0.035)).r;
                float caustic = pow(saturate(1 - abs(ca - cb) * 4.5), 6) * saturate(thick * 2) * exp(-thick * 0.6);
                under += caustic * 0.16 * _MainLightColor.rgb;
                float3 absorb = exp(-thick * float3(0.55, 0.16, 0.12));        // red goes first: turquoise, then blue
                float murk = 1 - exp(-thick * 0.35);
                float3 water = lerp(under * absorb, c.rgb, saturate(murk * c.a));
                SurfaceData s = (SurfaceData)0;
                s.albedo = float3(0, 0, 0);
                s.smoothness = lerp(0.95, 0.4, saturate(foam));
                s.occlusion = 1;
                s.alpha = 1;
                half4 spec = UniversalFragmentPBR(inputData, s);             // sun glints and the sky's reflection
                float edge = saturate(thick * 3);                             // no hard line where water meets sand
                float3 col = water + spec.rgb * (0.35 + fres * 0.9) * edge;
                // Foam along the shore and flecks out deep, lit by the sun and sky.
                float3 foamCol = float3(0.92, 0.95, 0.95) * (_MainLightColor.rgb * 0.8 + SampleSH(float3(0, 1, 0)));
                float foamT = saturate(foam * 0.55 + flecks + smoothstep(0.12, 0.0, thick) * smoothstep(0.5, 0.75, foamNoise) * 0.5);
                col = lerp(col, foamCol, foamT);
                col = MixFog(col, inputData.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        fixed4 _ShallowColor, _DeepColor;
        sampler2D _WaveTex;

        struct Input { float3 worldPos; float3 viewDir; float4 vcol; };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            v.vertex.y += sin(wp.x * 0.35 + _Time.y * 1.3) * 0.04 + cos(wp.z * 0.28 + _Time.y) * 0.04;
            o.vcol = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv1 = IN.worldPos.xz * 0.05 + _Time.y * float2(0.02, 0.013);
            float2 uv2 = IN.worldPos.xz * 0.08 - _Time.y * float2(0.017, 0.021);
            float n = tex2D(_WaveTex, uv1).r * 0.5 + tex2D(_WaveTex, uv2).r * 0.5;
            float depth = saturate(IN.vcol.r);
            fixed4 c = lerp(_ShallowColor, _DeepColor, depth);
            float fres = pow(1 - saturate(dot(normalize(IN.viewDir), float3(0,1,0))), 3);
            o.Albedo = c.rgb + n * 0.08;
            o.Normal = normalize(float3((n - 0.5) * 0.6, 1, (n - 0.5) * 0.6));
            o.Smoothness = 0.92;
            o.Metallic = 0;
            o.Alpha = saturate(c.a + fres * 0.3);
        }
        ENDCG
    }
}
