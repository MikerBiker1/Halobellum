// EffectHeat.shader
// Heat shimmer over flame and big blasts (M10): it draws the already rendered scene again, pushed a little sideways by a noise
// texture. Needs URP's opaque texture, which the PC renderer has on. It must draw before the other effects (they go on top of it).
// Depends on: URP's core library and the scene colour texture.
Shader "Halobellum/EffectHeat"
{
    Properties
    {
        _MainTex("Noise (red, green: direction; alpha: mask)", 2D) = "white" {}
        _Strength("Strength", Float) = 0.012
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Heat"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Strength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                output.screen = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 noise = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 push = (noise.rg - 0.5) * 2.0 * _Strength * input.color.a * noise.a;
                float2 uv = input.screen.xy / input.screen.w + push;
                return half4(SampleSceneColor(uv), saturate(noise.a * input.color.a * 2.0));
            }
            ENDHLSL
        }
    }
}
