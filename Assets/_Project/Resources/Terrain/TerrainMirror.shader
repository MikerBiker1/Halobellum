// TerrainMirror.shader
// The mirror of a polished floor (the lighting pass): what stands around the field, and its lights, drawn a second time upside down under the deck and added faintly onto it,
// as a cheap planar reflection. It fades with height above the floor, as a slightly rough deck blurs what is far above it, and with the haze. Lights lying flat on the floor
// are not mirrored. Units are not mirrored, so nothing on the floor is doubled.
// It is added only where the deck itself was drawn: it tests for lying behind the depth already there (ZTest Greater), which is the deck and its paint, and never the sky.
// Everything drawn after it (props, units, effects) covers it as usual, so it can't hide a unit. It is drawn on the High quality level only (BattlefieldView).
// _IsGlow 0: the solid mesh, lit as TerrainProp lights it. _IsGlow 1: the glow mesh, moved and turned as TerrainGlow does it. Same vertex layout as those shaders.
// Depends on: TerrainCommon.hlsl, BattlefieldView (sets _Strength, _FadeHeight, _GlowBoost, _Core, _Cull and the globals).
Shader "Halobellum/TerrainMirror"
{
    Properties
    {
        _Strength("How much of it shows", Float) = 0.2
        _FadeHeight("Metres above the floor over which it halves", Float) = 5
        _GlowBoost("How bright a full glow is (as TerrainProp)", Float) = 2.5
        _Core("Extra brightness of a light's core (as TerrainGlow)", Float) = 0
        _IsGlow("0 for the solid mesh, 1 for the glow mesh", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 1
    }

    SubShader
    {
        // After the deck (Geometry-100) and its paint (Geometry-50), before anything that stands.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-45" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest Greater
            Blend One One
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "TerrainCommon.hlsl"

            // The deck's height (TerrainCanvas.GroundHeight).
            #define MIRROR_PLANE -0.06

            CBUFFER_START(UnityPerMaterial)
                float _Strength, _FadeHeight, _GlowBoost, _Core, _IsGlow, _Cull;
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
                float3 realWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float fade : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float stays = 1.0;
                float2 face = 0;
                if (_IsGlow > 0.5)
                {
                    // As TerrainGlow: drifting specks, and halos that face the camera.
                    float3 drift = input.normalOS;
                    if (drift.z > 0.0)
                    {
                        float t = _Time.y * drift.z + input.uv.y;
                        world.x += sin(t) * drift.x;
                        world.z += cos(t * 0.71) * drift.x;
                        world.y += sin(t * 0.53 + 1.3) * drift.y;
                    }
                    stays = drift.z > 0.0 ? 1.0 : 1.0 - FloorHiding(world);
                    face = input.face;
                }
                else if (input.uv.x > 0.0)
                {
                    // As TerrainProp: tendrils and treetops sway.
                    float t = _Time.y * 0.7 + input.uv.y;
                    world.x += sin(t) * input.uv.x;
                    world.z += cos(t * 0.83) * input.uv.x * 0.7;
                }
                output.realWS = world;
                float height = world.y - MIRROR_PLANE;
                float3 mirrored = float3(world.x, 2.0 * MIRROR_PLANE - world.y, world.z);
                mirrored += UNITY_MATRIX_V[0].xyz * face.x + UNITY_MATRIX_V[1].xyz * face.y;
                output.positionCS = TransformWorldToHClip(mirrored);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                float fade = _Strength * exp2(-max(height, 0.0) / max(0.1, _FadeHeight)) * stays;
                if (_IsGlow > 0.5)
                {
                    // Lights lying on the floor itself are not mirrored: that would only double them.
                    fade *= saturate((height - 0.3) / 0.9);
                    float pulse = 1.0 - input.uv.x * (0.5 + 0.5 * sin(_Time.y * 1.3 + input.uv.y));
                    float core = 1.0 + _Core * smoothstep(0.55, 0.95, input.color.a);
                    output.color = half4(SRGBToLinear(input.color.rgb) * input.color.a * pulse * core * 1.6, 1.0);
                }
                else
                {
                    output.color = half4(SRGBToLinear(input.color.rgb), input.color.a);
                }
                output.fade = fade;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 color;
                if (_IsGlow > 0.5)
                {
                    color = input.color.rgb;
                }
                else
                {
                    // What it hides of the floor has gone from the floor (KeepFloorInView), so it goes from the mirror too.
                    KeepFloorInView(input.realWS, input.positionCS.xy);
                    half3 normal = normalize(input.normalWS);
                    color = input.color.rgb * TerrainLight(normal) + input.color.rgb * input.color.a * _GlowBoost;
                }
                // Lost in the haze as the deck is: added light fades to nothing where the deck melts into the fog.
                float t = saturate((length(input.realWS.xz) - _HBFogRange.x) / max(1.0, _HBFogRange.y - _HBFogRange.x));
                float hazed = 1.0 - t * (2.0 - t) * _HBFogRange.z;
                return half4(color * input.fade * hazed, 1.0);
            }
            ENDHLSL
        }
    }
}
