// UnitBlob.shader
// A soft dark blob on the ground under each unit (M10). All blobs are in one mesh that UnitBlobShadows rewrites each frame: uv.xy runs -1 to 1 across a
// blob and uv.z is how dark that blob is. It writes no depth, and units are drawn before it, so it only ever darkens the ground around them.
// Depends on: UnitBlobShadows (draws it).
Shader "Halobellum/UnitBlob"
{
    Properties
    {
        _Strength("Darkness", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-480" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float d = saturate(1.0 - dot(input.uv.xy, input.uv.xy));
                return half4(0.0, 0.0, 0.0, d * d * _Strength * input.uv.z);
            }
            ENDHLSL
        }
    }
}
