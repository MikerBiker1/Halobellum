// TerrainGlow.shader
// Light added on top of the terrain (M10): glowing seams, the halo of a lamp or crystal, floating spores. It adds to what is behind and writes no
// depth, so it never hides a unit, and it is depth-tested, so units stand in front of it.
// Vertex colour is the light in sRGB, alpha how strong. uv.x is how much it pulses, uv.y the pulse's phase. A vertex's normal, if not zero, makes it
// drift slowly: x how far sideways, y how far up and down, z how fast. uv1, if not zero, turns it to face the camera: how far right and up along the screen it stands from its position.
// A light goes when the point it stands at would hide the floor (FloorHiding), since what it hangs on has gone from there too. Drifting specks hang on nothing, so they stay.
// Depends on: UnityEngine's time, TerrainCommon.hlsl.
Shader "Halobellum/TerrainGlow"
{
    Properties
    {
        _Boost("Brightness", Float) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-475" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Blend One One

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "TerrainCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Boost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 face : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 drift = input.normalOS;
                if (drift.z > 0.0)
                {
                    float t = _Time.y * drift.z + input.uv.y;
                    world.x += sin(t) * drift.x;
                    world.z += cos(t * 0.71) * drift.x;
                    world.y += sin(t * 0.53 + 1.3) * drift.y;
                }
                // Measured at its middle, so that a halo fades as one.
                float stays = drift.z > 0.0 ? 1.0 : 1.0 - FloorHiding(world);
                world += UNITY_MATRIX_V[0].xyz * input.face.x + UNITY_MATRIX_V[1].xyz * input.face.y;
                output.positionCS = TransformWorldToHClip(world);
                float pulse = 1.0 - input.uv.x * (0.5 + 0.5 * sin(_Time.y * 1.3 + input.uv.y));
                output.color = half4(SRGBToLinear(input.color.rgb) * input.color.a * pulse * stays * _Boost, 1.0);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
