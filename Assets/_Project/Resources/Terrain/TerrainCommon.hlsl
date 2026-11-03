// TerrainCommon.hlsl
// What the faction terrain's shaders share (M10): distance haze measured from the middle of the arena rather than from the camera,
// so the fighting floor stays perfectly clear at every zoom while the far land melts into the sky, and the light from the scene's sun.
// It also holds the rule that keeps the floor in view from every side: whatever stands between the camera and the fighting floor is left out, pixel by pixel.
// Set by BattlefieldView with Shader.SetGlobalVector. Colours are already linear.
// Depends on: URP Core.hlsl and Lighting.hlsl, included by the shader first.

#ifndef HALOBELLUM_TERRAIN_COMMON
#define HALOBELLUM_TERRAIN_COMMON

float4 _HBFogColor; // rgb: the colour the land melts into.
float4 _HBFogRange; // x: metres from the middle where it starts, y: where it is complete, z: how strong.
float4 _HBFloor; // xy: half the fighting floor's width and length.

/// Drops this pixel of something solid if it hides the floor: follows the line from the camera through it down to the ground. Hiding the outer 2 m of the floor is allowed, so that
/// a low thing standing at the edge isn't touched, and further in it thins out to nothing over 4 m.
/// From the default view nothing that stands is in the way (TerrainCanvas.IsClear), so this only shows once the camera has turned.
void KeepFloorInView(float3 positionWS, float2 pixel)
{
    float above = _WorldSpaceCameraPos.y - positionWS.y;
    // What lies on the floor itself can't hide it. (The ground's big cells at the floor's corners do rise a little towards the first hill: dropping those would leave a hole.)
    if (positionWS.y < 0.05 || above <= 0.0 || (abs(positionWS.x) < _HBFloor.x && abs(positionWS.z) < _HBFloor.y)) return;
    float2 onGround = _WorldSpaceCameraPos.xz + (positionWS.xz - _WorldSpaceCameraPos.xz) * _WorldSpaceCameraPos.y / above;
    float2 inside = _HBFloor.xy - abs(onGround);
    float thin = saturate((min(inside.x, inside.y) - 2.0) / 4.0);
    if (thin <= 0.0) return;
    // Scattered from pixel to pixel, so that the thinning has no pattern one can see.
    clip(frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))) - thin);
}

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
