// MenuDistant.shader
// Something far off over the main menu's hills (D104): the owner's Covenant supercarrier. Scene fog would turn a model that far away into a
// flat patch of fog colour, so this ignores the fog and fades toward a haze colour of its own instead, by _Haze, keeping its shape and a
// little of its paint. Lit by one light from a fixed direction (the low sun), with a rim of it along its top edge, and by the sky's ambient.
// Set by MenuCinematicBuilder; used nowhere else. Original work. Depends on: URP's core library.
Shader "Halobellum/MenuDistant"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Colour", Color) = (1, 1, 1, 1)
        _HazeColor("Haze", Color) = (0.5, 0.32, 0.28, 1)
        _Haze("How much haze", Range(0, 1)) = 0.45
        _LightDirection("Direction to the light", Vector) = (0.6, 0.25, 0.75, 0)
        _LightColor("Light", Color) = (1.2, 0.7, 0.45, 1)
        _Ambient("Ambient", Color) = (0.22, 0.2, 0.28, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

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
                float4 _BaseMap_ST, _BaseColor, _HazeColor, _LightDirection, _LightColor, _Ambient;
                float _Haze;
            CBUFFER_END

            struct Input { float4 position : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float3 view : TEXCOORD2; };

            Output Vertex(Input input)
            {
                Output output;
                float3 world = TransformObjectToWorld(input.position.xyz);
                output.position = TransformWorldToHClip(world);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.view = normalize(GetCameraPositionWS() - world);
                return output;
            }

            half4 Fragment(Output input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 light = normalize(_LightDirection.xyz);
                float3 paint = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float facing = saturate(dot(normal, light));
                float rim = pow(1.0 - saturate(dot(normal, normalize(input.view))), 3.0) * saturate(normal.y * 0.5 + 0.5);
                float3 colour = paint * (_Ambient.rgb + _LightColor.rgb * facing) + _LightColor.rgb * rim * 0.35;
                return half4(lerp(colour, _HazeColor.rgb, _Haze), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
