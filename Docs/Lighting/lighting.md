# Lighting pass (before 1.0)

The owner approved "#4 better lighting": the battlefields should look more realistic and richer, with no new art files. This is what was done, on the test branch `test/lighting`. Nothing in the simulation, balance, unit models, UI or the menu's look changed.

## What changed

| What | How | Where |
| --- | --- | --- |
| A post-processing profile per look | Each interior look has its own profile, made in code from its theme, and the "Battlefield Mood" volume switches to it with the look. It holds Neutral tonemapping (as the project's default profile already had), contrast, saturation, colour filter, exposure, white balance, split toning (what the darks and the lights lean towards), a vignette and the bloom. The outdoor maps share one profile, "Outdoor", neutral apart from the tonemapping and the bloom. All the colour work is baked into one lookup table, so it costs nothing per pixel. | `BattlefieldLighting.cs` (new), `BattlefieldView.ApplyMood`, new fields in `TerrainTheme` |
| Glow (bloom) on lights | The bloom's threshold stays at 1.15 (in URP's gamma terms, about 1.36 in linear light), so lit units, the ground and the HUD never glow. The HUD is drawn after post-processing anyway. The bright cores of strips, seams, slits and lamps (`TerrainGlow`, alpha near 1) now go past white by `TerrainTheme.GlowCore`, so they bloom. The soft spill and pools of light on the floor are not raised, so the floor isn't washed out. Things that stand and glow (pods, fungus, lit doors, slits) are brighter by `PropGlow` (2.5 before, 3 to 3.2 now). Each look tints its bloom. | `TerrainGlow.shader` (`_Core`), `TerrainProp`'s `_GlowBoost` set per look |
| Soft contact shadows (SSAO) | URP's SSAO, run after the opaque pass and worked out from depth, at half resolution, radius 0.8 m, intensity 1, falloff 600 m (the old one faded out at 100 m, before the game camera's distance). Every shader is darkened by it, including the terrain's own shaders and the baked crowd shader, so there are soft shadows where units, vehicles, crates and pillars meet the floor. It lives on a renderer of the battle's own, so the menu keeps its renderer and its look. | `Assets/Settings/PC_BattleRenderer.asset` (new), added to `PC_RPAsset`. `BattlefieldLighting.UseBattleRenderer` puts the battle camera on it. |
| Reflections on polished floors | A cheap planar mirror. What stands around the field and its lights are drawn a second time upside down under the deck and added faintly onto it. The mirror fades with height (a slightly rough deck blurs what is far above it) and with the haze. It is only added where the deck was drawn (ZTest Greater against the deck's depth), so never over the sky, and everything drawn later (props, units, effects) covers it. Lights lying on the floor and units are not mirrored. One extra draw of the solid and glow meshes. | `TerrainMirror.shader` (new), `BattlefieldView.Mirror` |
| Choosing a quality level in a test run | `-quality Low` or `-quality High` (or a level's name or number) for any test tool, for that run only. It is put back when the run ends and nothing is saved, so one build can measure both levels. | `GameSettings.QualityFromCommandLine` |

### Per look

| Look | Grade | Glow | Mirror |
| --- | --- | --- | --- |
| UNSC Installation | Cool and clean: white balance -5, steel-blue darks, contrast 10 (was 6), saturation -2. Lights stay white. | Strips, deck lights and lit doors bloom (core x2.2, props 3.0), cool white bloom. | 0.1, faint on the polished plates. |
| Covenant Warship | Lavender and indigo: tint +6 (magenta), indigo darks, lavender lights, contrast 14 (was 8), saturation +6, exposure -0.15 so the deck isn't milky. | Energy lines, slits, orbs and lift columns bloom (core x2.6, props 3.2), lavender bloom. | 0.35. The ribs, pillars and slits show on the deck around the field. |
| Flood on a UNSC hull | Murky and warm-sick: white balance +7, tint -8 (greener), red-brown darks, sickly yellow lights, contrast 14, saturation -6, exposure -0.1, vignette 0.35. | Pod cores, fungus and the dying lights bloom (core x2.5, props 3.0), yellowish bloom. | None (the deck is mostly flesh). |
| Flood on a Covenant hull | As above. | As above. | 0.1, on the polished hull. |
| Outdoor (Sandbox only) | Neutral, as before. | Seams and crystals bloom a little (core x2). | None. |

The existing limits on mood still hold, and `BattleTerrainTests` checks them: vignette at most 0.35, contrast at most 15, exposure at most 0.2 either way. Team colours were checked in the deployment pictures: the blue and red squares and borders, and the health bars, read as before on every look.

### Per quality level

The game has two quality levels: "Mobile" (shown as Low) and "PC" (shown as High). There is no Medium. Adding one would change the settings screen, so it was not done.

| | Low (Mobile_RPAsset) | High (PC_RPAsset) |
| --- | --- | --- |
| Colour grade, tonemapping, vignette | Yes (free: one lookup table) | Yes |
| Bloom | Lighter: 75% intensity, quarter size, Dual filter, 4 steps | Half size, Gaussian, high-quality filtering, 6 steps |
| Brighter light cores | Yes (a shader multiply) | Yes |
| SSAO | No (Low's pipeline asset has no battle renderer) | Yes, half resolution |
| Floor mirror | No | Yes, on the looks that have one |
| MSAA, shadows | Unchanged | Unchanged |

The minimum requirements did not go up: Low gains only the grade and a cheaper bloom setting.

### Shadows

This was checked, and nothing was changed:
- In battle nothing casts a realtime shadow. Units (`PlaceholderArt`, the baked bodies) and the terrain have shadow casting off, and the terrain's shaders don't receive shadows. Units get soft blobs (`UnitBlobShadows`), and the floor gets painted shade from what stands nearby (the control map's alpha). So URP renders no shadow map in battle, and its cost there is nil.
- The main light's settings (High: 2048, 4 cascades, 50 m, soft shadows at high quality; Low: 1024, 1 cascade, 50 m, hard shadows) only matter for the menu, which uses them. The game camera stands about 130 m from the field, beyond 50 m, so turning shadows on in battle would need a much longer distance, shadow-caster passes in the baked crowd shader and shadow sampling in the terrain shaders, for 600 bodies. That is a bigger change than this pass, so it is listed below. The SSAO now gives the contact shadows instead.

## Frame times

From the project's battle benchmark (`Halobellum.exe -benchmark -benchmarkSpeed 1 -quality <Low|High>`): the BattleScene's 300 against 300 battle (UNSC Installation, seed 1), the whole battle, about 40,000 frames. Both builds are test builds of the same commit. The "before" build also has `-quality`, and nothing else. Measured on this PC (Radeon RX 7800 XT, i7-12700KF), one run each, windowed.

| Quality, window | Before: average (1% low) | After: average (1% low) |
| --- | --- | --- |
| High, 1600 x 900 | 1.07 ms (3.30 ms) | 1.01 ms (3.10 ms) |
| Low, 1600 x 900 | 0.87 ms (3.83 ms) | 0.88 ms (4.01 ms) |
| High, 2560 x 1440 | 1.15 ms (3.30 ms) | 1.14 ms (3.15 ms) |
| Low, 2560 x 1440 | 0.83 ms (2.70 ms) | 0.81 ms (2.77 ms) |

The terrain gallery's frame time at the game camera, paused, at High, was 0.8 to 0.9 ms before and 0.8 to 0.9 ms after, on every look.

So, on this PC, the cost is within the noise between runs, under about 0.1 ms. This graphics card is far from modest, and at these frame rates the CPU sets the pace. On a weak card SSAO at half resolution and the high-quality bloom are what would cost something, and both are on High only. This has not been measured on a weaker card.

## Pictures

From the terrain gallery (`-terrainGallery <folder>`): the game camera (default view, 55 degrees) 9 seconds into a Sandbox battle at 2x, paused, with seed 12345 for the map. The `_units` pictures are halfway in on the fight. The battles are the same armies, but not frame-identical: a few bodies stand elsewhere.

| Look | Before | After |
| --- | --- | --- |
| UNSC Installation | `before_unsc.png`, `before_unsc_units.png` | `after_unsc.png`, `after_unsc_units.png` |
| Covenant Warship | `before_covenant.png`, `before_covenant_units.png` | `after_covenant.png`, `after_covenant_units.png`, `after_covenant_low.png` (Low) |
| Flood on a UNSC hull | `before_flood_unsc.png`, `before_flood_unsc_units.png` | `after_flood_unsc.png`, `after_flood_unsc_units.png` |
| Flood on a Covenant hull | `before_flood_covenant.png`, `before_flood_covenant_units.png` | `after_flood_covenant.png`, `after_flood_covenant_units.png` |
| Outdoor (Firebase against Seam Flats) | `before_outdoor.png` | `after_outdoor.png` |

## Tests

- `BattlefieldLightingTests` (new, PlayMode): every look puts its own profile on the mood volume, set from its theme (tonemapping, white balance, saturation, vignette, bloom threshold above white), and the outdoor maps theirs. On High the mirror is drawn on the looks that have one, and the battle renderer with SSAO is chosen. On Low there is no mirror, no renderer with SSAO and a cheaper bloom. Low's pipeline asset has no SSAO at all, and High's battle renderer has it. A test run's `-quality` is read as described.
- Run for this change: PlayMode filtered to `BattleTerrainTests|BattlefieldLightingTests|EffectsTests|BattleCameraTests|MatchSceneTests|SettingsHeldForTests`. The filter also caught the scene tests that use the held settings, so 252 tests ran: 251 passed, 0 failed, 1 skipped (`AudioFlowTests.MenuButtons_...`, "Batch mode draws no IMGUI", as always). EditMode `AudioTests|MenuFlowTests|DisclaimerTests` (the command-line flag checks): 159 passed, 0 failed.

## What is left

- No realtime shadows in battle (see "Shadows"). Real shadows of units and props on the floor would be the next big step in realism. They need shadow-caster passes in `BakedCrowd.shader`, shadow sampling in the terrain shaders, a shadow distance of about 300 m with cascades fitted to the game camera, and a measurement on a modest card.
- The mirror doesn't show units, only what stands around the field and its lights. Mirroring units would need a second draw of every body.
- No Medium quality level. If one is added, `BattlefieldLighting.IsHighQuality` treats any level but Low as High, and it needs a pipeline asset with `PC_BattleRenderer` in its list to get SSAO.
- The UNSC look's change is the mildest of the four, as asked ("cool, clean, slight contrast"). If the owner wants more, the next things to raise are `Contrast`, `GlowCore` and `Mirror` in `TerrainInteriorUnsc.cs`.
- Not measured on a weaker graphics card.
