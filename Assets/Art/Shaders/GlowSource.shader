// Marks an object as a glow emitter for SelectiveGlowFeature.
// Its only pass uses LightMode "GlowSource", which the normal URP passes never draw - the glow
// feature draws it into its own HDR buffer, blurs it and adds it back as light.
// Add this as an extra material on the renderer that should glow.
Shader "GameJam/GlowSource"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (0.85, 1.0, 1.7, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Name "GlowSource"
            Tags { "LightMode" = "GlowSource" }
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _GlowIntensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes i) { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return half4(_GlowColor.rgb * _GlowIntensity, 1); }   // alpha = silhouette mask
            ENDHLSL
        }
    }
}
