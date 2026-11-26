// TerrainProp.shader
// Everything that stands on the terrain (M10): barricades, crystals, tendrils. One mesh, one draw call, colours in the vertices.
// Vertex colour rgb is the surface in sRGB, alpha how much it glows. A grey grain tile, read in world space, keeps surfaces from looking flat when
// zoomed in. uv.x is how much the vertex sways and uv.y its phase, for tendrils and treetops; the sway only ever moves it sideways by a little.
// Props cast the sun's shadows and receive them (the shadows pass, before 1.0); a glowing part lights itself, so shadow only dims its unlit share.
// Depends on: TerrainCommon.hlsl, BattlefieldView (sets the textures and the globals).
Shader "Halobellum/TerrainProp"
{
    Properties
    {
        [NoScaleOffset] _Grain("Grain tile", 2D) = "gray" {}
        _GlowBoost("How bright a full glow is", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "TerrainCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _GlowBoost;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 color : COLOR;
        };

        Varyings PropVertex(Attributes input)
        {
            Varyings output;
            float3 world = TransformObjectToWorld(input.positionOS.xyz);
            float sway = input.uv.x;
            if (sway > 0.0)
            {
                float t = _Time.y * 0.7 + input.uv.y;
                world.x += sin(t) * sway;
                world.z += cos(t * 0.83) * sway * 0.7;
            }
            output.positionWS = world;
            output.positionCS = TransformWorldToHClip(world);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = float4(SRGBToLinear(input.color.rgb), input.color.a);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma vertex PropVertex
            #pragma fragment Fragment

            TEXTURE2D(_Grain);
            SAMPLER(sampler_Grain);

            half4 Fragment(Varyings input) : SV_Target
            {
                KeepFloorInView(input.positionWS, input.positionCS.xy);
                half3 normal = normalize(input.normalWS);
                float3 p = input.positionWS;
                // Read the grain from whichever side the surface mostly faces.
                float2 uv = abs(normal.y) > 0.7 ? p.xz : (abs(normal.x) > abs(normal.z) ? p.zy : p.xy);
                half grain = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, uv * 0.11).r;
                half3 albedo = input.color.rgb * lerp(1.0, grain * 5.6, 0.55);

                half3 lit = albedo * TerrainLightAt(normal, p);
                lit += input.color.rgb * input.color.a * _GlowBoost;
                return half4(ApplyWorldFog(lit, p), 1.0);
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
            #pragma vertex PropVertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                KeepFloorInView(input.positionWS, input.positionCS.xy);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVertex
            #pragma fragment Fragment

            float3 _LightDirection;

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = PropVertex(input);
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, normalize(output.normalWS), _LightDirection)));
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
