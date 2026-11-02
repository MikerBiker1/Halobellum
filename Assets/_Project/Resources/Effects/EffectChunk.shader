// EffectChunk.shader
// Flying debris, gore and bone (M10): solid mesh particles in their particle colour, with simple light from one fixed direction.
// Cheap on purpose: chunks are small and move fast, so the real lights and shadows would never be noticed.
// Depends on: URP's core library.
Shader "Halobellum/EffectChunk"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Chunk"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half light = 0.45 + 0.55 * saturate(dot(normalize(input.normalWS), normalize(float3(0.35, 0.85, -0.4))));
                return half4(input.color.rgb * light, 1);
            }
            ENDHLSL
        }
    }
}
