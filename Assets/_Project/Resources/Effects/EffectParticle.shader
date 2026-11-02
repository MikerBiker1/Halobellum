// EffectParticle.shader
// Every soft particle and glowing line of the battle effects (M10). One fixed blend, premultiplied alpha, so that one shader can
// be fire, glow or smoke without any variants: _Occlude 0 adds light, 1 is ordinary see-through smoke, between is flame with a body.
// _Boost lifts the colour above 1, which is what the bloom picks up (particle colours themselves stop at 1).
// _SoftFade is how many metres before the ground or a body a particle fades out, so big smoke has no hard edge where it meets them.
// _ZTest 8 (always) is for glows: light is not cut off by the ground it passes in front of.
// Depends on: URP's core library and the scene depth texture (without one, nothing fades).
Shader "Halobellum/EffectParticle"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Boost("Brightness", Float) = 1
        _Occlude("How much it hides what is behind it", Range(0, 1)) = 0
        _SoftFade("Soft edge, metres", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Effect"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Boost;
                float _Occlude;
                float _SoftFade;
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
                float eyeDepth : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = input.uv;
                output.screen = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(world).z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = texel.a * input.color.a;
                if (_SoftFade > 0.0)
                {
                    float2 uv = input.screen.xy / input.screen.w;
                    float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                    alpha *= saturate((scene - input.eyeDepth) / _SoftFade);
                }
                half3 colour = texel.rgb * input.color.rgb * alpha * _Boost;
                return half4(colour, alpha * _Occlude);
            }
            ENDHLSL
        }
    }
}
