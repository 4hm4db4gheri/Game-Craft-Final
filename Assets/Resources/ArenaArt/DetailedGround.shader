Shader "Hollow/DetailedGround"
{
    Properties
    {
        _MainTex("Arena composition", 2D) = "white" {}
        _DetailTex("Close ground detail", 2D) = "gray" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal", 2D) = "bump" {}
        _DetailStrength("Detail strength", Range(0,2)) = 1
        _DetailScale("Detail repeat", Vector) = (6,4,0,0)
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _DetailScale;
                half _DetailStrength;
            CBUFFER_END
            Varyings Vertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonLitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                half3 detail = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, input.uv * _DetailScale.xy).rgb;
                half luminance = dot(detail, half3(0.2126,0.7152,0.0722));
                // Detail carries local texture; the single arena painting carries paths and composition.
                half gain = clamp(1 + (luminance - 0.24) * _DetailStrength, 0.82, 1.18);
                return CommonLitFragment(input, input.color * half4(gain,gain,gain,1));
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
