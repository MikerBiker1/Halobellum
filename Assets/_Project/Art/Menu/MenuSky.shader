// MenuSky.shader
// The main menu's sky (M10): a dusk gradient, a low sun with a wide glow, and long streaks of cloud lit from the sun's side, all worked
// out per pixel from the view direction, so that no texture is needed. Values above 1 near the sun feed the camera's bloom.
// Set on the scene by MainMenuSceneBuilder; used nowhere else. Original work. Depends on: nothing.
Shader "Halobellum/MenuSky"
{
    Properties
    {
        _SunDirection("Direction to the sun", Vector) = (0.45, 0.16, 0.88, 0)
        _Zenith("Top of the sky", Color) = (0.08, 0.1, 0.24, 1)
        _Middle("Middle of the sky", Color) = (0.36, 0.27, 0.42, 1)
        _Horizon("Horizon away from the sun", Color) = (0.72, 0.42, 0.38, 1)
        _HorizonSun("Horizon under the sun", Color) = (1.45, 0.5, 0.16, 1)
        _SunColor("Sun", Color) = (3.6, 1.55, 0.55, 1)
        _CloudLit("Cloud, lit side", Color) = (1.5, 0.62, 0.34, 1)
        _CloudDark("Cloud, dark side", Color) = (0.17, 0.13, 0.22, 1)
        _CloudAmount("Amount of cloud", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            float4 _SunDirection;
            float4 _Zenith, _Middle, _Horizon, _HorizonSun, _SunColor, _CloudLit, _CloudDark;
            float _CloudAmount;

            struct Input { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 position : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Output Vertex(Input input)
            {
                Output output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Layers(float2 p)
            {
                float sum = 0.0, weight = 0.5;
                for (int octave = 0; octave < 5; octave++)
                {
                    sum += Noise(p) * weight;
                    p = p * 2.03 + float2(11.3, 7.7);
                    weight *= 0.5;
                }
                return sum;
            }

            fixed4 Fragment(Output input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float3 sun = normalize(_SunDirection.xyz);
                float height = saturate(direction.y);
                float towardSun = dot(direction, sun);
                float sideOfSun = saturate(towardSun * 0.5 + 0.5);

                // Horizon to top; the horizon burns under the sun and cools away from it.
                float3 horizon = lerp(_Horizon.rgb, _HorizonSun.rgb, pow(sideOfSun, 3.0));
                float3 color = lerp(horizon, _Middle.rgb, smoothstep(0.0, 0.2, height));
                color = lerp(color, _Zenith.rgb, smoothstep(0.1, 0.62, height));

                // The glow around the sun, and its disc.
                color += _SunColor.rgb * 0.05 * pow(saturate(towardSun), 18.0);
                color += _SunColor.rgb * 0.24 * pow(saturate(towardSun), 320.0);
                color += _SunColor.rgb * smoothstep(0.99975, 0.99988, towardSun);

                // Long clouds on a flat layer high above: stretched sideways, thinning toward the top of the sky.
                if (direction.y > 0.004)
                {
                    float2 layer = direction.xz / (direction.y + 0.09);
                    float drift = _Time.y * 0.004;
                    float shape = Layers(layer * float2(0.32, 1.25) + float2(drift, drift * 0.3));
                    float detail = Layers(layer * float2(1.1, 3.6) + float2(-drift * 2.0, 3.0));
                    float cover = smoothstep(1.0 - _CloudAmount, 1.0 - _CloudAmount + 0.28, shape * 0.82 + detail * 0.22);
                    float edge = smoothstep(0.0, 0.28, Layers(layer * float2(0.32, 1.25) + float2(drift, drift * 0.3) + float2(0.12, 0.08) * sun.xz) - shape + 0.14);
                    float lit = saturate(pow(saturate(towardSun * 0.5 + 0.5), 4.0) * 1.2 + edge * 0.25);
                    float3 cloud = lerp(_CloudDark.rgb, _CloudLit.rgb, lit);
                    float fadeHigh = 1.0 - smoothstep(0.35, 0.8, height);
                    color = lerp(color, cloud, cover * fadeHigh * 0.9);
                }

                // Below the horizon nothing shows but the ground; keep it the horizon's colour for any gap.
                if (direction.y < 0.0) color = horizon * 0.6;
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
