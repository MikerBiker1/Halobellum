// TerrainHaze.shader
// A thin sheet of drifting mist over the land around the arena (M10): dust, violet mist or spores. The mesh's vertex alpha is zero over the arena, so
// the fighting floor stays clear. Its light and dark come from the macro tile, scrolled in two directions so that it seems to move.
// Depends on: UnityEngine's time, BattlefieldView (sets the colour).
Shader "Halobellum/TerrainHaze"
{
    Properties
    {
        [NoScaleOffset] _Macro("Macro tile", 2D) = "gray" {}
        _Color("Colour and strength", Color) = (1, 1, 1, 0.2)
        _Scale("Tiles per metre", Float) = 0.004
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-470" }

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

            TEXTURE2D(_Macro);
            SAMPLER(sampler_Macro);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Scale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 xz : TEXCOORD0;
                half alpha : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.xz = world.xz;
                output.alpha = input.color.a;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 drift = _Time.y * float2(0.006, 0.0023);
                half a = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, input.xz * _Scale + drift).g;
                half b = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, input.xz * _Scale * 0.37 - drift * 0.6).r;
                half cloud = saturate((sqrt(a) * sqrt(b) - 0.32) * 2.4);
                return half4(_Color.rgb, _Color.a * cloud * input.alpha);
            }
            ENDHLSL
        }
    }
}
