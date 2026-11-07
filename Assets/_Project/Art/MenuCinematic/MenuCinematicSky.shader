// MenuCinematicSky.shader
// The cinematic main menu's sky (D94): a storm at dusk over a battle. A bruised gradient from a burning horizon to a dark top; the low sun
// tearing through a gap in heavy, fast-moving clouds lit from beneath by the fires along the horizon; an alien warship far off, its belly
// lights glowing, burning the land with a glassing beam whose foot flares on the horizon; and lightning, which MenuCinematic sets off
// (_MenuFlash, _MenuFlashDirection: globals), lighting the clouds around its stroke and drawing a jagged bolt. All worked out per pixel
// from the view direction, so no texture is needed; values above 1 feed the camera's bloom. Set by MenuCinematicBuilder; used nowhere else.
// Original work. Depends on: nothing.
Shader "Halobellum/MenuCinematicSky"
{
    Properties
    {
        _SunDirection("Direction to the sun", Vector) = (0.8, 0.07, 0.6, 0)
        _Zenith("Top of the sky", Color) = (0.03, 0.04, 0.075, 1)
        _Middle("Middle of the sky", Color) = (0.14, 0.12, 0.17, 1)
        _Horizon("Horizon away from the sun", Color) = (0.42, 0.29, 0.27, 1)
        _HorizonSun("Horizon under the sun", Color) = (1.9, 0.75, 0.27, 1)
        _SunColor("Sun", Color) = (4.2, 1.9, 0.7, 1)
        _CloudLit("Cloud, lit side", Color) = (1.2, 0.62, 0.36, 1)
        _CloudDark("Cloud, dark side", Color) = (0.055, 0.05, 0.07, 1)
        _CloudAmount("Amount of cloud", Range(0, 1)) = 0.7
        _FireGlow("Fires on the horizon", Color) = (1.0, 0.34, 0.09, 1)
        _FlashColor("Lightning", Color) = (1.6, 1.7, 2.6, 1)
        _ShipDirection("Direction to the warship", Vector) = (0.55, 0.15, 0.83, 0)
        _ShipSize("Warship's half-length, in radians", Float) = 0.11
        _ShipColor("Warship", Color) = (0.07, 0.055, 0.09, 1)
        _ShipLights("Warship's lights", Color) = (1.2, 0.5, 3.2, 1)
        _BeamColor("Glassing beam", Color) = (2.4, 0.8, 2.0, 1)
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

            float4 _SunDirection, _ShipDirection;
            float4 _Zenith, _Middle, _Horizon, _HorizonSun, _SunColor, _CloudLit, _CloudDark, _FireGlow, _FlashColor, _ShipColor, _ShipLights, _BeamColor;
            float _CloudAmount, _ShipSize;
            // Set by MenuCinematic for the whole scene, so not properties: a material's own value would hide them.
            float _MenuFlash;
            float4 _MenuFlashDirection;

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

            float Azimuth(float3 direction) { return atan2(direction.x, direction.z); }

            float AngleBetween(float a, float b)
            {
                float d = a - b;
                return d - 6.2831853 * floor((d + 3.1415927) / 6.2831853);
            }

            // How much of the warship covers this direction (0 to 1), its lights, and where it is lit along its top edge.
            float Ship(float3 direction, out float lights, out float rim)
            {
                lights = 0.0;
                rim = 0.0;
                float3 forward = normalize(_ShipDirection.xyz);
                float facing = dot(direction, forward);
                if (facing < 0.8) return 0.0;
                float3 right = normalize(cross(float3(0, 1, 0), forward));
                float3 up = cross(forward, right);
                float2 q = float2(dot(direction, right), dot(direction, up)) / facing / _ShipSize;
                if (abs(q.x) > 1.2 || abs(q.y) > 0.5) return 0.0;

                // A long hull, swollen toward its bow (+x), drooping at both ends, its tail split in two prongs.
                float along = saturate(1.0 - q.x * q.x);
                float middle = -0.035 * q.x * q.x + 0.012 * q.x;
                float half = 0.07 * pow(along, 0.55) * (0.62 + 0.6 * exp(-pow((q.x - 0.38) / 0.3, 2.0)));
                float hull = abs(q.y - middle) - half;
                // The tail's prongs, above and below, swept back.
                float prongs = 1.0;
                if (q.x < -0.55)
                {
                    float t = saturate((-0.55 - q.x) / 0.5);
                    float spread = 0.05 + t * 0.09;
                    prongs = min(abs(q.y - middle - spread), abs(q.y - middle + spread * 0.8)) - 0.022 * (1.0 - t);
                }
                // A blister under its middle, where the beam leaves.
                float blister = length((q - float2(0.12, middle - 0.075)) / float2(1.6, 1.0)) - 0.045;
                // A low fin along its spine.
                float fin = 1.0;
                if (q.x > -0.45 && q.x < 0.2)
                {
                    float t = (q.x + 0.45) / 0.65;
                    fin = (q.y - middle - half) - 0.03 * sin(t * 3.14159) ;
                    fin = max(fin, -(q.y - middle));
                }
                float shape = min(min(hull, prongs), min(blister, fin));
                float edge = fwidth(shape) + 0.002;
                float cover = 1.0 - smoothstep(-edge, edge, shape);

                // Lights along its belly and its sides.
                float row = q.y - (middle - half * 0.55);
                float spots = frac(q.x * 11.0 + 0.5) - 0.5;
                lights = exp(-(spots * spots) / 0.004) * exp(-(row * row) / 0.00012) * step(abs(q.x), 0.85);
                lights += exp(-dot(q - float2(0.12, middle - 0.12), q - float2(0.12, middle - 0.12)) / 0.0006) * 2.0;
                rim = saturate(1.0 - abs(shape) / (edge * 3.0)) * step(middle, q.y);
                return cover;
            }

            fixed4 Fragment(Output input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float3 sun = normalize(_SunDirection.xyz);
                float height = saturate(direction.y);
                float towardSun = dot(direction, sun);
                float sideOfSun = saturate(towardSun * 0.5 + 0.5);
                float azimuth = Azimuth(direction);
                float sunAround = saturate(cos(AngleBetween(azimuth, Azimuth(sun))) * 0.5 + 0.5);
                float time = _Time.y;

                // Fires along the horizon, here and there, flickering slowly.
                float fires = smoothstep(0.5, 0.8, Layers(float2(azimuth * 4.0, 2.3)) + 0.06 * sin(time * 0.9 + azimuth * 7.0));
                float low = exp(-height * 22.0);

                // Horizon to top: the horizon burns under the sun and smoulders away from it.
                float3 horizon = lerp(_Horizon.rgb, _HorizonSun.rgb, pow(sunAround, 5.0));
                float3 color = lerp(horizon, _Middle.rgb, smoothstep(0.0, 0.22, height));
                color = lerp(color, _Zenith.rgb, smoothstep(0.1, 0.65, height));
                color += _FireGlow.rgb * fires * low * 0.7;

                // The sun's glow and its disc.
                color += _SunColor.rgb * 0.045 * pow(saturate(towardSun), 9.0);
                color += _SunColor.rgb * 0.22 * pow(saturate(towardSun), 160.0);
                color += _SunColor.rgb * 0.3 * smoothstep(0.9992, 0.99975, towardSun);

                // Heavy storm clouds on a layer above, racing: dark masses with bright torn edges toward the sun, lit from beneath by the
                // fires, thinning out just above the horizon so that its burning band shows beneath them.
                if (direction.y > 0.0)
                {
                    float2 layer = direction.xz / (direction.y + 0.1);
                    float2 drift = float2(time * 0.014, time * 0.006);
                    float shape = Layers(layer * 0.85 + drift);
                    float detail = Layers(layer * 3.1 + drift * 2.6 + 5.0);
                    float billow = shape * 0.72 + detail * 0.38;
                    float tear = pow(saturate(towardSun), 6.0) * 0.12;
                    float low = 0.62 - _CloudAmount * 0.24 + tear;
                    float cover = smoothstep(low, low + 0.1, billow);
                    float thick = smoothstep(low + 0.04, low + 0.22, billow);
                    float lit = pow(sideOfSun, 4.0);
                    float3 cloud = lerp(_CloudLit.rgb * (0.25 + 0.75 * lit), _CloudDark.rgb, thick * (0.55 + 0.45 * (1.0 - lit)));
                    cloud = lerp(cloud, _CloudDark.rgb * 0.7, smoothstep(0.25, 0.6, height) * thick);
                    cloud += _FireGlow.rgb * (0.05 + fires * 0.35) * (1.0 - smoothstep(0.0, 0.25, height)) * (1.0 - thick * 0.5);
                    // Lightning lights the clouds near its stroke, and a little of the whole sky.
                    float3 strokeDirection = normalize(_MenuFlashDirection.xyz + float3(0, 0.0001, 0));
                    float nearStroke = pow(saturate(dot(direction, strokeDirection) * 0.5 + 0.5), 28.0);
                    cloud += _FlashColor.rgb * _MenuFlash * (0.02 + 1.6 * nearStroke) * (0.3 + detail);
                    float haze = smoothstep(0.012, 0.1, height);
                    color = lerp(color, cloud, cover * haze);
                }
                color += _FlashColor.rgb * _MenuFlash * 0.015;

                // The lightning's bolt: a jagged line under its stroke, from the cloud to the ground.
                if (_MenuFlash > 0.15)
                {
                    float3 strokeDirection = normalize(_MenuFlashDirection.xyz + float3(0, 0.0001, 0));
                    float strokeHeight = saturate(strokeDirection.y + 0.12);
                    float elevation = asin(clamp(direction.y, -1.0, 1.0));
                    float wander = (Noise(float2(elevation * 26.0, 3.1)) - 0.5) * 0.028 + (Noise(float2(elevation * 95.0, 7.7)) - 0.5) * 0.008;
                    float across = AngleBetween(azimuth, Azimuth(strokeDirection)) - wander * (1.0 + elevation * 6.0);
                    float span = step(-0.01, elevation) * step(elevation, strokeHeight);
                    float bolt = exp(-across * across / 0.0000035) * span;
                    color += _FlashColor.rgb * bolt * _MenuFlash * 3.0;
                }

                // The warship, far off under the clouds, hazed by the distance, and its glassing beam.
                float lights, rim;
                float ship = Ship(direction, lights, rim);
                float3 hazeColor = lerp(horizon, _Middle.rgb, smoothstep(0.0, 0.22, height));
                float3 hull = lerp(_ShipColor.rgb, hazeColor, 0.3) + _SunColor.rgb * rim * 0.06 + _FlashColor.rgb * _MenuFlash * 0.04;
                color = lerp(color, hull, ship);
                color += _ShipLights.rgb * lights * 0.9 * (0.85 + 0.15 * sin(time * 2.3));

                float3 forward = normalize(_ShipDirection.xyz);
                float3 right = normalize(cross(float3(0, 1, 0), forward));
                float3 up = cross(forward, right);
                float3 mouth = normalize(forward + (right * 0.12 + up * -0.1) * _ShipSize);
                float mouthHeight = asin(mouth.y);
                float elevationHere = asin(clamp(direction.y, -1.0, 1.0));
                float beamAcross = AngleBetween(azimuth, Azimuth(mouth)) * cos(elevationHere);
                float flicker = 0.88 + 0.12 * sin(time * 29.0) * sin(time * 7.3 + 1.0);
                float below = step(elevationHere, mouthHeight) * smoothstep(-0.02, 0.0, elevationHere);
                float core = exp(-beamAcross * beamAcross / 0.0000045);
                float glow = exp(-abs(beamAcross) / 0.006);
                color += _BeamColor.rgb * (core * 2.2 + glow * 0.25) * below * flicker;
                // Where it strikes the ground, beyond the hills: a flare and a wide glow along the horizon.
                float2 foot = float2(beamAcross / 0.09, (elevationHere + 0.005) / 0.025);
                color += (_BeamColor.rgb * 0.35 + _FireGlow.rgb * 0.6) * exp(-dot(foot, foot)) * flicker;
                color += _FireGlow.rgb * exp(-abs(beamAcross) / 0.2) * exp(-max(elevationHere, 0.0) / 0.012) * 0.35;

                // Below the horizon nothing shows but the ground; keep it the horizon's colour for any gap.
                if (direction.y < 0.0) color = horizon * 0.5;
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
