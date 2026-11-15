// Painterly environment shader for the courtyard (stone blocks, ground, banners).
// Albedo = _BaseColor x vertex colour (each block / region is painted through its vertex colours)
//          x soft world-space mottling (hand-painted unevenness) x optional world-XZ detail texture (grass).
// Lighting: wrapped diffuse from the main light with shadows, lavender-tinted ambient (SH), screen-space AO,
// additional point lights (e.g. the Evil's power-up light), a faint warm rim. Opaque; SRP Batcher friendly.
Shader "GameJam/StylizedEnv"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _TopTint ("Top Tint (faces pointing up)", Color) = (1.08, 1.04, 0.96, 1)
        _SideTint ("Side Tint (vertical faces)", Color) = (0.92, 0.88, 0.96, 1)
        _MottleScale ("Mottle Scale", Float) = 0.11
        _MottleStrength ("Mottle Strength", Range(0, 1)) = 0.16
        [NoScaleOffset] _DetailTex ("Detail Texture (projected on world XZ)", 2D) = "gray" {}
        _DetailScale ("Detail Tiling (per world unit)", Float) = 0.04
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0
        _Wrap ("Light Wrap", Range(0, 1)) = 0.35
        _ShadowTint ("Shadow / Ambient Tint", Color) = (0.78, 0.72, 0.92, 1)
        _AmbientBoost ("Ambient Boost", Range(0, 3)) = 1.15
        _RimColor ("Rim Color", Color) = (1, 0.88, 0.75, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.1
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TopTint;
            half4 _SideTint;
            half4 _ShadowTint;
            half4 _RimColor;
            half4 _EmissionColor;
            float _MottleScale;
            float _MottleStrength;
            float _DetailScale;
            float _DetailStrength;
            float _Wrap;
            float _AmbientBoost;
            float _RimStrength;
            float _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash31(i), n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0)), n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1)), n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1)), n111 = Hash31(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            half4 frag(Varyings i, half facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (facing > 0 ? 1.0 : -1.0);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // ---- painted albedo
                half3 albedo = _BaseColor.rgb * i.color.rgb;
                float m = ValueNoise(i.positionWS * _MottleScale) * 0.65 + ValueNoise(i.positionWS * _MottleScale * 2.9 + 17.0) * 0.35;
                albedo *= 1.0 + (m - 0.5) * 2.0 * _MottleStrength;
                half up = saturate(n.y);
                if (_DetailStrength > 0.001)
                {
                    half3 d = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, i.positionWS.xz * _DetailScale).rgb;
                    half lum = dot(d, half3(0.3, 0.59, 0.11));
                    albedo *= lerp(1.0, saturate(lum * 2.4) * 1.15, _DetailStrength * up);
                }
                albedo *= lerp(_SideTint.rgb, _TopTint.rgb, up);

                // ---- main light (wrapped, shadowed)
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half wrapped = saturate((dot(n, mainLight.direction) + _Wrap) / (1.0 + _Wrap));
                half lit = wrapped * mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                half3 ambient = SampleSH(n) * _AmbientBoost * _ShadowTint.rgb;
                half3 direct = mainLight.color * lit;

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    ambient *= ao.indirectAmbientOcclusion;
                    direct *= ao.directAmbientOcclusion;
                #endif

                half3 color = albedo * (ambient + direct);

                // ---- additional lights (point lights such as the Evil's power-up glow)
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = n;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    half4 shadowMask = half4(1, 1, 1, 1);
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light l = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                        color += albedo * l.color * l.distanceAttenuation * l.shadowAttenuation * saturate(dot(n, l.direction));
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                        color += albedo * l.color * l.distanceAttenuation * l.shadowAttenuation * saturate(dot(n, l.direction) * 0.8 + 0.2);
                    LIGHT_LOOP_END
                #endif

                // ---- warm rim + emission + fog
                half rim = pow(1.0 - saturate(dot(n, v)), 4.0) * _RimStrength;
                color += _RimColor.rgb * rim * (0.4 + lit);
                color += _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 vert(Attributes i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            void frag(Varyings i, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(i.normalWS);
                    float2 oct = PackNormalOctQuadEncode(normalWS);
                    outNormalWS = half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                    outNormalWS = half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
                #endif
                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }
}
