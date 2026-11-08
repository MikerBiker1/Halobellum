// MenuDistantGlow.shader
// Light far off over the main menu's hills (D104): the supercarrier's glassing beam and the glow where it strikes. Added to what is behind it,
// and never fogged, which would dim it to nothing at that distance; hidden behind the nearer hills as anything is. Its colour times the
// texture's alpha (a soft glow), times the vertex colour (a line's own colour). Set by MenuCinematicBuilder; used nowhere else.
// Original work. Depends on: URP's core library.
Shader "Halobellum/MenuDistantGlow"
{
    Properties
    {
        _BaseMap("Glow", 2D) = "white" {}
        _BaseColor("Colour", Color) = (3, 1, 2.5, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _BaseColor;
            CBUFFER_END

            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; float4 colour : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 colour : COLOR; };

            Output Vertex(Input input)
            {
                Output output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.colour = input.colour;
                return output;
            }

            half4 Fragment(Output input) : SV_Target
            {
                float glow = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                return half4(_BaseColor.rgb * input.colour.rgb * input.colour.a * glow, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
