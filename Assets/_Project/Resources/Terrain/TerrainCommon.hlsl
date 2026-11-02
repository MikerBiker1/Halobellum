// TerrainCommon.hlsl
// What the faction terrain's shaders share (M10): distance haze measured from the middle of the arena rather than from the camera,
// so the fighting floor stays perfectly clear at every zoom while the far land melts into the sky, and the light from the scene's sun.
// Set by BattlefieldView with Shader.SetGlobalVector. Colours are already linear.
// Depends on: URP Core.hlsl and Lighting.hlsl, included by the shader first.

#ifndef HALOBELLUM_TERRAIN_COMMON
#define HALOBELLUM_TERRAIN_COMMON

float4 _HBFogColor; // rgb: the colour the land melts into.
float4 _HBFogRange; // x: metres from the middle where it starts, y: where it is complete, z: how strong.

float3 ApplyWorldFog(float3 color, float3 positionWS)
{
    float t = saturate((length(positionWS.xz) - _HBFogRange.x) / max(1.0, _HBFogRange.y - _HBFogRange.x));
    return lerp(color, _HBFogColor.rgb, t * (2.0 - t) * _HBFogRange.z);
}

/// The sun and the ambient on a surface.
half3 TerrainLight(half3 normalWS)
{
    Light light = GetMainLight();
    half lit = saturate(dot(normalWS, light.direction));
    return light.color * lit + SampleSH(normalWS);
}

#endif
