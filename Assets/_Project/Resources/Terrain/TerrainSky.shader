// TerrainSky.shader
// The sky of the terrain (M10): a gradient from the horizon up, thin slow clouds, the sun's glow, and on an alien sky a pale disc hanging in it. The
// mesh is a dome that always stays centred on the camera. At every tilt the game's camera allows the sky is out of view; it shows when a tool looks lower.
// Depends on: UnityEngine's time, BattlefieldView (sets the colours).
Shader "Halobellum/TerrainSky"
{
    Properties
    {
        [NoScaleOffset] _Macro("Macro tile", 2D) = "gray" {}
        _Top("Top of the sky", Color) = (0.2, 0.3, 0.6, 1)
        _Horizon("Horizon", Color) = (0.7, 0.75, 0.8, 1)
        _Cloud("Clouds", Color) = (1, 1, 1, 0.5)
        _SunColor("Sun glow", Color) = (1, 0.9, 0.7, 1)
        _SunDirection("Towards the sun", Vector) = (0, 1, 0, 0)
        _Moon("Hanging disc: colour, 0 alpha for none", Color) = (0, 0, 0, 0)
        _MoonDirection("Towards the disc, w its size", Vector) = (0, 0.5, 1, 0.04)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Macro);
            SAMPLER(sampler_Macro);

            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Horizon, _Cloud, _SunColor, _Moon;
                float4 _SunDirection, _MoonDirection;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.direction = input.positionOS.xyz;
                output.positionCS = TransformWorldToHClip(_WorldSpaceCameraPos + normalize(input.positionOS.xyz) * 1400.0);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float up = saturate(d.y);
                half3 color = lerp(_Horizon.rgb, _Top.rgb, pow(up, 0.55));

                // Clouds on a flat ceiling high above.
                float2 ceiling = d.xz / (d.y + 0.18);
                float2 drift = _Time.y * float2(0.0016, 0.0007);
                half a = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, ceiling * 0.14 + drift).g;
                half b = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, ceiling * 0.041 - drift * 0.5).r;
                half cloud = saturate((sqrt(a) * sqrt(b) - 0.42) * 3.0) * smoothstep(-0.02, 0.2, d.y);
                color = lerp(color, _Cloud.rgb, cloud * _Cloud.a);

                half sun = saturate(dot(d, normalize(_SunDirection.xyz)));
                color += _SunColor.rgb * (pow(sun, 8.0) * 0.18 + pow(sun, 90.0) * 0.5 + pow(sun, 900.0) * 3.0);

                if (_Moon.a > 0.0)
                {
                    float3 m = normalize(_MoonDirection.xyz);
                    float along = dot(d, m);
                    float disc = smoothstep(cos(_MoonDirection.w * 1.03), cos(_MoonDirection.w * 0.98), along);
                    color = lerp(color, _Moon.rgb * (0.65 + a * 0.5), disc * _Moon.a * (1.0 - cloud * 0.6));
                    color += _Moon.rgb * pow(saturate(along), 60.0) * 0.12 * _Moon.a;
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
