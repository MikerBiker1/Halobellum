// TerrainDeployment.shader
// The deployment squares, drawn over a team's zone on any ground (M10). Lighter and darker squares of one square size in world space, thin lines
// between them, and a strong border in the team's colour. Lightening and darkening both, so they read on bright concrete and dark biomass alike. _Fade takes
// all of it away during battle.
// Depends on: BattlefieldView (sets the zone, square size, colour and fade).
Shader "Halobellum/TerrainDeployment"
{
    Properties
    {
        _Team("Team colour", Color) = (0.3, 0.6, 1, 1)
        _Zone("Zone: min x, min z, max x, max z", Vector) = (-80, -82, 80, -2)
        _Square("Square size", Float) = 2
        _Fade("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-490" }

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
                half4 _Team;
                float4 _Zone;
                float _Square;
                float _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 xz : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.xz = world.xz;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 cell = input.xz / _Square;
                float2 pixel = max(fwidth(cell), 0.0001);

                // Light and dark squares, softened where a square is only a few pixels across, so they do not shimmer.
                float2 edge = abs(frac(cell) - 0.5);
                float2 toLine = (0.5 - edge) / pixel; // Pixels to the nearest line.
                float odd = fmod(floor(cell.x) + floor(cell.y) + 4096.0, 2.0);
                float squareClear = saturate(0.6 / max(pixel.x, pixel.y) - 0.6);
                float lineAlpha = (1.0 - saturate(min(toLine.x, toLine.y) - 0.3)) * saturate(0.25 / max(pixel.x, pixel.y));

                half3 color = odd > 0.5 ? half3(1, 1, 1) : half3(0, 0, 0);
                float alpha = lerp(0.03, odd > 0.5 ? 0.1 : 0.14, squareClear);

                // Tint from the team.
                color = lerp(color, _Team.rgb, 0.3);
                alpha += 0.04;

                // Thin lines.
                color = lerp(color, half3(1, 1, 1), lineAlpha * 0.6);
                alpha = max(alpha, lineAlpha * 0.22);

                // The zone's border, in metres from it.
                float2 inside = min(input.xz - _Zone.xy, _Zone.zw - input.xz);
                float toBorder = min(inside.x, inside.y);
                float width = max(0.35, 1.5 * length(fwidth(input.xz)));
                float border = 1.0 - saturate((toBorder - width) / max(0.01, length(fwidth(input.xz))));
                color = lerp(color, _Team.rgb, border);
                alpha = lerp(alpha, 0.95, border);

                return half4(color, alpha * _Fade);
            }
            ENDHLSL
        }
    }
}
