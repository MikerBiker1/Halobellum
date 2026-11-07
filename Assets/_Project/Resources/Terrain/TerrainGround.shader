// TerrainGround.shader
// The ground of the battlefield and the land around it (M10). Three tiled layers for each side's faction, sampled in world space, mixed by a
// control map built for this map: R is how much of the second layer, G the third, B how far towards the enemy's ground, A soft shade from what
// stands nearby. Each layer's alpha is its height, so one ground gives way to another around its stones and lumps rather than in a smooth fade.
// A slow macro tile breaks up repetition. It has its own light and haze, so it needs no shader keywords that a build could strip.
// Floors can shine (D80): each layer has a gloss, lit by a light of its own (_GlossDir, _GlossColor) rather than the sun, so a polished deck can catch a highlight
// towards the camera while units stay lit from the front. The highlight is brightest where a layer's height (alpha) is high: raised plates shine, seams and grime don't.
// Depends on: TerrainCommon.hlsl, BattlefieldView (sets the textures and the globals).
Shader "Halobellum/TerrainGround"
{
    Properties
    {
        [NoScaleOffset] _L0("Team A layer 1", 2D) = "gray" {}
        [NoScaleOffset] _L1("Team A layer 2", 2D) = "gray" {}
        [NoScaleOffset] _L2("Team A layer 3", 2D) = "gray" {}
        [NoScaleOffset] _L3("Team B layer 1", 2D) = "gray" {}
        [NoScaleOffset] _L4("Team B layer 2", 2D) = "gray" {}
        [NoScaleOffset] _L5("Team B layer 3", 2D) = "gray" {}
        [NoScaleOffset] _Control("Control map", 2D) = "black" {}
        [NoScaleOffset] _Macro("Macro tile", 2D) = "gray" {}
        _TileA("Team A: tiles per metre of each layer", Vector) = (0.04, 0.04, 0.04, 0)
        _TileB("Team B: tiles per metre of each layer", Vector) = (0.04, 0.04, 0.04, 0)
        _TintA("Team A tint", Color) = (1, 1, 1, 1)
        _TintB("Team B tint", Color) = (1, 1, 1, 1)
        _MacroStrength("Macro strength", Float) = 0.35
        _GlossA("Team A: gloss of each layer, and tightness", Vector) = (0, 0, 0, 24)
        _GlossB("Team B: gloss of each layer, and tightness", Vector) = (0, 0, 0, 24)
        _GlossColor("Gloss light colour; a is the sheen away from the highlight", Color) = (1, 1, 1, 0)
        _GlossDir("Direction towards the gloss light", Vector) = (0, 0.8, 0.6, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-100" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "TerrainCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TileA, _TileB;
            half4 _TintA, _TintB;
            float _MacroStrength;
            float4 _GlossA, _GlossB, _GlossDir;
            half4 _GlossColor;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
        };

        Varyings GroundVertex(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GroundVertex
            #pragma fragment Fragment

            TEXTURE2D(_L0); TEXTURE2D(_L1); TEXTURE2D(_L2);
            TEXTURE2D(_L3); TEXTURE2D(_L4); TEXTURE2D(_L5);
            TEXTURE2D(_Control); TEXTURE2D(_Macro);
            SAMPLER(sampler_L0);
            SAMPLER(sampler_Control);
            SAMPLER(sampler_Macro);

            /// How far to go from a to b at mix weight w, with each one's height pushing through the other: w = 0 is exactly a, w = 1 exactly b.
            float HeightMix(float w, float heightA, float heightB)
            {
                const float push = 0.5;
                float x = w * (1.0 + 2.0 * push) - push + (heightB - heightA) * push;
                return saturate(x * 2.0 - 0.5);
            }

            half4 Layers(TEXTURE2D_PARAM(first, s1), TEXTURE2D_PARAM(second, s2), TEXTURE2D_PARAM(third, s3), float2 xz, float4 tile, float w1, float w2, float4 gloss, out half shine)
            {
                // Each layer turned its own way, so that their repeats never line up.
                half4 a = SAMPLE_TEXTURE2D(first, s1, xz * tile.x);
                half4 b = SAMPLE_TEXTURE2D(second, s2, float2(xz.x * 0.8 - xz.y * 0.6, xz.x * 0.6 + xz.y * 0.8) * tile.y);
                half4 c = SAMPLE_TEXTURE2D(third, s3, float2(xz.x * 0.92 + xz.y * 0.39, -xz.x * 0.39 + xz.y * 0.92) * tile.z);
                half m1 = HeightMix(w1, a.a, b.a);
                half4 mix1 = lerp(a, b, m1);
                half m2 = HeightMix(w2, mix1.a, c.a);
                shine = lerp(lerp(gloss.x, gloss.y, m1), gloss.z, m2);
                return lerp(mix1, c, m2);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                KeepFloorInView(input.positionWS, input.positionCS.xy);
                float2 xz = input.positionWS.xz;
                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, input.uv);
                half4 macro = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, xz * 0.0031);
                half4 macro2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, xz * 0.0137);

                // The control map is soft: fine noise roughens the edges where one ground meets another, and leaves weights of exactly 0 and 1 alone.
                half rough = (sqrt(SAMPLE_TEXTURE2D(_Macro, sampler_Macro, xz * 0.083).b) - 0.46) * 1.0;
                control.r = saturate(control.r + rough * 4.0 * control.r * (1.0 - control.r));
                control.g = saturate(control.g + rough * 4.0 * control.g * (1.0 - control.g));
                control.b = saturate(control.b + rough * 4.0 * control.b * (1.0 - control.b));

                half shineA, shineB;
                half4 sideA = Layers(TEXTURE2D_ARGS(_L0, sampler_L0), TEXTURE2D_ARGS(_L1, sampler_L0), TEXTURE2D_ARGS(_L2, sampler_L0), xz, _TileA, control.r, control.g, _GlossA, shineA);
                half4 sideB = Layers(TEXTURE2D_ARGS(_L3, sampler_L0), TEXTURE2D_ARGS(_L4, sampler_L0), TEXTURE2D_ARGS(_L5, sampler_L0), xz, _TileB, control.r, control.g, _GlossB, shineB);
                sideA.rgb *= _TintA.rgb;
                sideB.rgb *= _TintB.rgb;

                half toB = HeightMix(control.b, sideA.a, sideB.a);
                half3 albedo = lerp(sideA.rgb, sideB.rgb, toB);
                half shine = lerp(shineA, shineB, toB) * (0.35 + 0.65 * lerp(sideA.a, sideB.a, toB));
                float power = lerp(_GlossA.w, _GlossB.w, toB);
                // Slow patches of light and dark, so no two tiles look alike. The macro texture is sRGB, hence the squares.
                half patches = macro.r * 0.6 + macro2.g * 0.4;
                albedo *= 1.0 + (sqrt(patches) - 0.72) * 2.0 * _MacroStrength;

                half3 normal = normalize(input.normalWS);
                half3 lit = albedo * TerrainLight(normal) * control.a;
                if (shine > 0.001)
                {
                    float3 toCamera = normalize(_WorldSpaceCameraPos - input.positionWS);
                    float3 halfway = normalize(normalize(_GlossDir.xyz) + toCamera);
                    half highlight = pow(saturate(dot(normal, halfway)), power) * (power + 8.0) / 32.0;
                    // Shade (from what stands nearby) dims the shine too, so a container's shadow isn't polished.
                    lit += _GlossColor.rgb * shine * (highlight + _GlossColor.a) * control.a * control.a;
                }
                return half4(ApplyWorldFog(lit, input.positionWS), 1.0);
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
            #pragma vertex GroundVertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                KeepFloorInView(input.positionWS, input.positionCS.xy);
                return 0;
            }
            ENDHLSL
        }
    }
}
