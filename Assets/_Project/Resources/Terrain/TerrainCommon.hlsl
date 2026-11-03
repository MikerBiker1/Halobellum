// TerrainCommon.hlsl
// What the faction terrain's shaders share (M10): distance haze measured from the middle of the arena rather than from the camera,
// so the fighting floor stays perfectly clear at every zoom while the far land melts into the sky, and the light from the scene's sun.
// It also holds the rule that keeps the floor in view from every side: whatever stands between the camera and the fighting floor is left out, pixel by pixel, and TerrainGlow's lights fade with it.
// Set by BattlefieldView with Shader.SetGlobalVector. Colours are already linear.
// Depends on: URP Core.hlsl and Lighting.hlsl, included by the shader first.

#ifndef HALOBELLUM_TERRAIN_COMMON
#define HALOBELLUM_TERRAIN_COMMON

float4 _HBFogColor; // rgb: the colour the land melts into.
float4 _HBFogRange; // x: metres from the middle where it starts, y: where it is complete, z: how strong.
float4 _HBFloor; // xy: half the fighting floor's width and length.

/// How much of what stands at this point must go because it hides the floor, from 0 (all of it stays) to 1 (none of it does): follows the line from the camera through it down to the ground.
/// Hiding the outer 2 m of the floor is allowed, so that a low thing standing at the edge isn't touched, and further in it thins out to nothing over 4 m.
/// It goes by the deepest the line gets on its way along the ground, from under the point to where it lands, since that is where the same thing lower down would land: the line can cross
/// a corner of the floor and leave it, and the top of a post mustn't stay hanging in the air over a middle that has gone.
/// From the default view nothing that stands is in the way (TerrainCanvas.IsClear), so this only shows once the camera has turned.
float FloorHiding(float3 positionWS)
{
    float above = _WorldSpaceCameraPos.y - positionWS.y;
    // What lies on the floor itself can't hide it. (The ground's big cells at the floor's corners do rise a little towards the first hill: dropping those would leave a hole.)
    if (positionWS.y < 0.05 || above <= 0.0 || (abs(positionWS.x) < _HBFloor.x && abs(positionWS.z) < _HBFloor.y)) return 0.0;
    float2 onGround = _WorldSpaceCameraPos.xz + (positionWS.xz - _WorldSpaceCameraPos.xz) * _WorldSpaceCameraPos.y / above;
    float deepest = -1.0;
    for (int step = 1; step <= 6; step++)
    {
        float2 inside = _HBFloor.xy - abs(lerp(positionWS.xz, onGround, step / 6.0));
        deepest = max(deepest, min(inside.x, inside.y));
    }
    return saturate((deepest - 2.0) / 4.0);
}

/// Drops this pixel of something solid as far as it hides the floor (FloorHiding).
void KeepFloorInView(float3 positionWS, float2 pixel)
{
    float thin = FloorHiding(positionWS);
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
