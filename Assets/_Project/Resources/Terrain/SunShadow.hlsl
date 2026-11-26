// SunShadow.hlsl
// The sun's real shadow at a point, with soft edges (the shadows pass, before 1.0), for the game's own shaders: the floor, its marks, what stands on it and the units.
// The light casts URP's hard shadows: its soft ones crash the game as it closes on Direct3D 12 (Unity 6000.3, found 2026-11-25). The softness is done here
// instead, from nine of the shadow map's own filtered taps around the point, so the edge fades over about four texels of the map (_HBSunShadowMode below).
// Needs URP's main light shadow keywords in the shader (#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN);
// without them, or with the light's shadows off (Low quality), it is always 1.
// Depends on: URP Core.hlsl and Lighting.hlsl (which brings Shadows.hlsl), included by the shader first.

#ifndef HALOBELLUM_SUN_SHADOW
#define HALOBELLUM_SUN_SHADOW
/// How the sun's shadow is read, set by BattlefieldView: 0 soft (nine taps, the default), 1 sharp (one tap), 2 none. Read at run time rather than as a keyword:
/// with the nine taps alone, built without this switch, the game crashed as it closed on Direct3D 12 in every run; built with it, never (2026-11-25).
float _HBSunShadowMode;

/// 0 where the sun is wholly hidden (as dark as the light's shadow strength allows), 1 in full sun or beyond the shadows' reach.
half SunShadow(float3 positionWS)
{
#if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return half(1.0);
#else
    float4 coord = TransformWorldToShadowCoord(positionWS);
    if (BEYOND_SHADOW_FAR(coord)) return half(1.0);
    if (_HBSunShadowMode > 1.5) return half(1.0);
    if (_HBSunShadowMode > 0.5) return lerp(LerpWhiteTo(SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, coord.xyz), GetMainLightShadowParams().x), half(1.0), GetMainLightShadowFade(positionWS));
    float2 step = _MainLightShadowmapSize.xy * 1.25;
    half sum = 0.0;
    [unroll] for (int y = -1; y <= 1; y++)
    {
        [unroll] for (int x = -1; x <= 1; x++)
            sum += SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, float3(coord.xy + float2(x, y) * step, coord.z));
    }
    half shadow = LerpWhiteTo(sum / 9.0, GetMainLightShadowParams().x);
    // Fades out towards the end of the shadows' reach, as URP's own does.
    return lerp(shadow, half(1.0), GetMainLightShadowFade(positionWS));
#endif
}

#endif
