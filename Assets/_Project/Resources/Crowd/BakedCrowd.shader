// BakedCrowd.shader
// Draws animated bodies in big batches (M9 Pass 0): instead of the CPU skinning each body, every frame of
// every bone is stored in a texture and the vertex shader reads it, so thousands of bodies can share a
// draw call. It has no UnityPerMaterial buffer on purpose: the SRP Batcher must leave it to GPU instancing.
// Each vertex names up to two bones (TEXCOORD1.xy) and the first bone's weight (TEXCOORD2.x).
// A bone takes 3 texels (the top 3 rows of its matrix), one row of texels per animation frame.
Shader "Halobellum/BakedCrowd"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _AnimTex("Baked animation", 2D) = "black" {}
        _FrameCount("Frames", Float) = 1
        _AnimFps("Frames per second", Float) = 30
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_AnimTex);
        float _FrameCount;
        float _AnimFps;

        UNITY_INSTANCING_BUFFER_START(BakedCrowdProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float, _AnimOffset)
        UNITY_INSTANCING_BUFFER_END(BakedCrowdProps)

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 bones : TEXCOORD1;
            float2 weights : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3x4 BoneMatrix(int bone, int frame)
        {
            int x = bone * 3;
            return float3x4(
                LOAD_TEXTURE2D_LOD(_AnimTex, int2(x, frame), 0),
                LOAD_TEXTURE2D_LOD(_AnimTex, int2(x + 1, frame), 0),
                LOAD_TEXTURE2D_LOD(_AnimTex, int2(x + 2, frame), 0));
        }

        Varyings BakedVertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);

            float time = _Time.y + UNITY_ACCESS_INSTANCED_PROP(BakedCrowdProps, _AnimOffset);
            int frame = (int)fmod(floor(time * _AnimFps), _FrameCount);
            float3x4 first = BoneMatrix((int)input.bones.x, frame);
            float3x4 second = BoneMatrix((int)input.bones.y, frame);
            float3x4 skin = first * input.weights.x + second * (1.0 - input.weights.x);

            float3 position = mul(skin, float4(input.positionOS.xyz, 1.0));
            float3 normal = mul((float3x3)skin, input.normalOS);
            output.positionCS = TransformWorldToHClip(TransformObjectToWorld(position));
            output.normalWS = TransformObjectToWorldNormal(normal);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex BakedVertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Light light = GetMainLight();
                half lit = saturate(dot(normalize(input.normalWS), light.direction));
                half4 color = UNITY_ACCESS_INSTANCED_PROP(BakedCrowdProps, _BaseColor);
                color.rgb *= 0.4 + 0.6 * lit * light.color;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex BakedVertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
