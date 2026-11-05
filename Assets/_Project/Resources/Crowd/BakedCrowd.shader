// BakedCrowd.shader
// Draws animated bodies in big batches (M9 Pass 0, D41): instead of the CPU skinning each body, every frame
// of every bone is stored in a texture and the vertex shader reads it, so thousands of bodies can share a
// draw call. It has no UnityPerMaterial buffer on purpose: the SRP Batcher must leave it to GPU instancing.
// Each vertex names up to two bones (TEXCOORD1.xy) and the first bone's weight (TEXCOORD2.x).
// A bone takes 3 texels (the top 3 rows of its matrix), one row of texels per animation frame.
// A body plays one clip and cross-fades from the one before (D42): _AnimNow and _AnimOld are each
// (first row, frames, start time, loops), _AnimBlend is (when the fade began, how long it takes). Times are on
// the global _BattleTime clock, so bodies pause, and speed up, with the battle.
// A vertex with colour alpha above 0 is a flat colour (a rifle made of boxes) instead of the texture.
// _BaseColor tints the body: rgb is the colour, alpha how strongly (the side's colour, a ghost, infection).
// A second material can draw a see-through part of the mesh, such as a Jackal's shield: _Glass on, in the transparent queue, with blending.
// That part glows in its vertices' own colour, unlit, and their colour's alpha says how much it shows. The depth pass never sees that queue.
Shader "Halobellum/BakedCrowd"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 0)
        [NoScaleOffset] _AnimTex("Baked animation", 2D) = "black" {}
        _AnimFps("Frames per second", Float) = 30
        [HideInInspector] _Glass("See-through", Float) = 0
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Writes depth", Float) = 1
        [HideInInspector] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_AnimTex);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float _AnimFps;
        float _BattleTime;
        float _Glass;

        UNITY_INSTANCING_BUFFER_START(BakedCrowdProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float4, _AnimNow)
            UNITY_DEFINE_INSTANCED_PROP(float4, _AnimOld)
            UNITY_DEFINE_INSTANCED_PROP(float4, _AnimBlend)
        UNITY_INSTANCING_BUFFER_END(BakedCrowdProps)

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 bones : TEXCOORD1;
            float2 weights : TEXCOORD2;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float2 uv : TEXCOORD1;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3x4 BoneMatrix(int bone, int row)
        {
            int x = bone * 3;
            return float3x4(
                LOAD_TEXTURE2D_LOD(_AnimTex, int2(x, row), 0),
                LOAD_TEXTURE2D_LOD(_AnimTex, int2(x + 1, row), 0),
                LOAD_TEXTURE2D_LOD(_AnimTex, int2(x + 2, row), 0));
        }

        /// Which texture row a clip is on at this moment.
        int ClipRow(float4 clip)
        {
            float frame = floor(max(0.0, _BattleTime - clip.z) * _AnimFps);
            frame = clip.w > 0.5 ? fmod(frame, clip.y) : min(frame, clip.y - 1.0);
            return (int)(clip.x + frame);
        }

        float3x4 Skin(Attributes input, int row)
        {
            return BoneMatrix((int)input.bones.x, row) * input.weights.x
                 + BoneMatrix((int)input.bones.y, row) * (1.0 - input.weights.x);
        }

        Varyings BakedVertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);

            float4 blend = UNITY_ACCESS_INSTANCED_PROP(BakedCrowdProps, _AnimBlend);
            float fade = blend.y > 0.0 ? saturate((_BattleTime - blend.x) / blend.y) : 1.0;
            fade = fade * fade * (3.0 - 2.0 * fade); // Ease in and out.

            float3x4 skin = Skin(input, ClipRow(UNITY_ACCESS_INSTANCED_PROP(BakedCrowdProps, _AnimNow)));
            // Reading the old clip too costs more, so it only happens during a fade.
            if (fade < 1.0)
                skin = skin * fade + Skin(input, ClipRow(UNITY_ACCESS_INSTANCED_PROP(BakedCrowdProps, _AnimOld))) * (1.0 - fade);

            float3 position = mul(skin, float4(input.positionOS.xyz, 1.0));
            float3 normal = mul((float3x3)skin, input.normalOS);
            output.positionCS = TransformWorldToHClip(TransformObjectToWorld(position));
            output.normalWS = TransformObjectToWorldNormal(normal);
            output.uv = input.uv;
            output.color = input.color;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex BakedVertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 albedo = lerp(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb, input.color.rgb, input.color.a);
                half4 tint = UNITY_ACCESS_INSTANCED_PROP(BakedCrowdProps, _BaseColor);
                // Keeps the texture's light and dark, lifted so that dark armour still shows the colour.
                albedo = lerp(albedo, (0.35 + 0.9 * dot(albedo, half3(0.3, 0.59, 0.11))) * tint.rgb * 1.3, tint.a);
                if (_Glass > 0.5) return half4(albedo * 1.6, input.color.a);
                Light light = GetMainLight();
                half lit = saturate(dot(normalize(input.normalWS), light.direction));
                return half4(albedo * (0.45 + 0.75 * lit * light.color), 1.0);
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
