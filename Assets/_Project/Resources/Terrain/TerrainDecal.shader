// TerrainDecal.shader
// Flat things painted on the ground (M10): concrete slabs, painted lines, stains, veins, cracks. They are drawn straight after the ground, before
// any unit, and write no depth, so they cannot hide anything and later ones simply paint over earlier ones, in the order they were added.
// Vertex colour is the paint in sRGB with its opacity. uv.x is how worn it is (0 = fresh; worn paint shows the ground through in flecks).
// uv.y is how much of the grain tile shows in it (concrete: a lot; paint: a little).
// Depends on: TerrainCommon.hlsl, BattlefieldView (sets the textures and the globals).
Shader "Halobellum/TerrainDecal"
{
    Properties
    {
        [NoScaleOffset] _Grain("Grain tile", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-50" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TerrainCommon.hlsl"

            TEXTURE2D(_Grain);
            SAMPLER(sampler_Grain);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 color : COLOR;
                float2 uv : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = float4(SRGBToLinear(input.color.rgb), input.color.a);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 grain = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, input.positionWS.xz * 0.083);
                half3 albedo = input.color.rgb * lerp(1.0, grain.r * 5.6, input.uv.y);
                float wear = input.uv.x > 0.0 ? smoothstep(input.uv.x - 0.08, input.uv.x + 0.08, grain.a) : 1.0;
                half3 lit = albedo * TerrainLight(half3(0, 1, 0));
                return half4(lit, input.color.a * wear);
            }
            ENDHLSL
        }
    }
}
