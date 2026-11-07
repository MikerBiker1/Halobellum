# M10: terrain that depends on the faction (and D80: the interior looks)

## D80: three interior looks, one for the whole field

The owner asked for the battlefield to look much better: the ground, the lighting and the surroundings, in three looks. These are a UNSC military installation or ship interior, a Covenant warship interior, and a Flood-infested ship. Since D80 every match is fought inside one of them. The outdoor maps below are still in the code for the gallery (`-terrainOutdoor`), but no match uses them.

### Which match gets which look

The two factions and the match seed decide it (`TerrainLooks.Pick`), so the look never changes during a match.

| Match | Look |
| --- | --- |
| UNSC against UNSC | UNSC Installation |
| Covenant against Covenant | Covenant Warship |
| UNSC against Covenant | The seed picks UNSC Installation or Covenant Warship, half and half |
| Flood against UNSC | Flood-Infested UNSC Ship |
| Flood against Covenant | Flood-Infested Covenant Ship |
| Flood against Flood | The seed picks either Flood ship |
| Unlock Skirmish | The seed picks one land for both teams (D62), so it is a mirror match of that land's look |
| Sandbox | Follows the factions with the most squads on each side, as before |

A sub-faction counts as its faction, as before. Who is on which side doesn't matter.

### The looks

- **UNSC Installation:** a hangar deck.
  - **Floor:** gunmetal steel plates of 4 m, with seams every 16 m and small cool lights set where they cross. Yellow and black hazard bands, lift pads and turntables, vents and hatches, faint worn lanes, and thin light strips along the edges.
  - **Around the field:** a low rail beyond the flank strips, then grated trenches with banded pillars 30 m tall. Yards hold container stacks, crates, weapon racks, tow tractors, generators, tanks and gantry cranes. A plain boxy dropship hangs in a cradle at the far end. Ribbed bulkheads stand all round, with lit doors and a bay door.
  - **Light and mood:** a cool overhead white.
- **Covenant Warship:**
  - **Floor:** polished purple-blue metal with a cool sheen, flowing lens-shaped panels, and thin cyan and violet energy lines that pulse slowly. A faint ring in the back third of each half.
  - **Around the field:** curved ribs and arches, bulging pillars with violet slits, curved walls with glowing seams, floating light orbs, faint lift columns, and a stepped dais and balcony at the far end.
  - **Light and mood:** a lavender light under a deep indigo ceiling.
- **Flood-Infested Ship,** on a UNSC or a Covenant hull:
  - **Floor:** the hull plates show in the middle of each deployment zone, where units stand. Wet, veined flesh in tan, salmon and red-brown spreads from the edges and corners, with crusts of rot, small pustules, and lifted, cracked plates oozing flesh.
  - **Around the field:** walls hung with folds of flesh that drip, great tendrils arching from the walls, clusters of infection pods with glowing cores, translucent sacs, and glowing fungus. Underneath is the old ship: grey bulkheads, banded pillars and a crane on the UNSC hull; purple ribs, slit pillars and arches on the Covenant hull. Some of its lights are dying (cold white or violet).
  - **Light and mood:** dark and murky outside the field, which stays lit enough to read.

All textures are made by scripts in `Tools/TerrainGen/make_interior_*.py`, and all shapes come from boxes, tubes and lumps. Nothing was downloaded, and there are no logos or markings.

### How it is built

- **`TerrainLook.cs`:** a look is a theme plus one layout that furnishes both halves; it also holds the rule above. Each look is in its own file: `TerrainInteriorUnsc.cs`, `TerrainInteriorCovenant.cs`, `TerrainInteriorFlood.cs`.
- **`TerrainBuilder`:** builds a look as before, with the same layout on both sides and a flat deck (`TerrainHillStyle.Flat`). The layout's `NoMansLand` paints the battle damage across the middle in place of the outdoor shell holes. The sky dome is the dark ceiling.
- **Shine on the floor:** `TerrainGround.shader` gives each layer a gloss (`TerrainTheme.Gloss`), lit by a light of its own (`GlossLightAngles`) rather than the sun. A polished deck can catch a highlight towards the camera while units stay lit from the front.
- **Mood:** a global volume, "Battlefield Mood", carries each look's vignette and mild colour grade (`TerrainTheme.Vignette`, `ColorFilter`, `Contrast`, `Saturation`, `Exposure`). It sits above the effects' bloom. The outdoor maps leave it neutral.
- **Light on the floor:** `TerrainLayout.LightPool` and `LightStrip` lay it down as added light, so it never hides a unit.
- **The rules for the floor still hold:** nothing solid on it, nothing that could hide it from the camera's default view, and paint below the units. Two more for the looks: nothing taller than 1.5 m within 96 m of the middle line, where flyers deploy in the flank strips (D61); and the Flood's floors have no solid near-black patches.
- **Tests:** `BattleTerrainTests` checks all of this for every look, and also checks the matches above, the textures, the limits on mood and light, and the triangle budget over 16 seeds.
- **Seeing them:** `Halobellum.exe -terrainGallery <folder>` now goes through the four looks. Add `-terrainLook <Id>` for one look only, or `-terrainOutdoor` for the old maps.

### Known limits

- **Size and cost:** a look is about 90,000 to 138,000 triangles (the Flood ships are the largest), under the 150,000 budget, and takes 250 to 380 ms to build. The frame time at the game's camera was about 1.0 to 1.1 ms on this PC.
- **Behind the walls:** dark roof planes stand behind the walls so that the bare deck beyond doesn't show when the camera is zoomed far out.
- **Light on units:** the sun and ambient light up the units too. They are kept bright enough on every look for units to stay readable.

## M10: terrain by faction

The owner asked: "Make terrain depending on what faction you chose." The Bible's M10 also says "more maps". This is what was built.

## What the player sees

The field is no longer two flat grey-blue and grey-red zones. Each side of it looks like the land of the faction fighting from that side, with real land around the arena out to the horizon.

- **Your half** (south, Team A) is your faction's ground. **The enemy's half** (north, Team B) is theirs. They meet in a rough, wandering line across no man's land, scattered with shell holes.
- **The sky, sun, ambient light and haze** come from the player's faction.
- **Deployment squares** are a separate overlay, in light and dark squares with a border in the team's colour. They are clear while placing and fade out over 0.8 s when a battle runs. They come back at the next deployment.
- **Every unit has a soft blob** on the ground under it. A flyer's is fainter and wider, and it is thrown aside by the sun in proportion to how high it really is, so that one can see where it is over the ground, and the blob closes in on a flyer as it dives.
- **Before the draft** there is a default backdrop (whatever the last factions were, UNSC against Covenant at first). **In the Sandbox** the map follows the faction with the most squads on each side (a sub-faction's units count for the faction it belongs to), a moment after the armies stop changing. It never changes during a battle.

## The maps

Each faction has three layouts (the brief asked for at least two). The match seed picks one for each side on its own, so 9 x 9 = 81 pairings exist before counting that the seed also moves everything within a layout. The same seed always looks the same.

| Faction | Layout | What it is |
| --- | --- | --- |
| UNSC | Firebase | A military base: a concrete apron of 8 m slabs, whole near the base and broken towards no man's land, with painted lanes, landing circles and hazard strips. Gravel roads, concrete barriers, sandbags, containers, floodlights, fences, huts, fuel tanks, hangars, a radio mast. |
| UNSC | Colony | A farming settlement: mown grass with dirt tracks, crop fields right up to the field, silos, barns, round prefab homes, wind turbines, a water tower, power lines, orchards. A warmer, lower sun. |
| UNSC | Quarry | A mining site on bare rock under a dusty sun: haul roads with dark ruts, steel road plates, survey marks, heaps of spoil, gantry cranes, conveyors. |
| Covenant | Seam Flats | Smooth purple-grey rock and pale sand, split by thin glowing cyan seams that fork as they cross the floor. Curved metal pylons in rows, crystal clusters, fins of rock, a pale disc hanging in a violet sky. |
| Covenant | Sanctum | A built site: rings of pale plates set in the floor with magenta light, tall curved gates, obelisks with floating lights, stepped daises. A deeper, redder sky. |
| Covenant | Crystal Garden | Pale drifted sand and slowly opening spirals of teal seams, in a field of tall crystals, under a cooler blue-grey sky. |
| Flood | Creep | Living biomass, brown rot and sinew. Nests of pustules with veins spreading from them, a lumpy wall around the field, arching and rearing tendrils, swollen pods, spores in the air, a dark sky. |
| Flood | Wreck | A place the Flood has taken: rot and see-through olive pools of slime over the broken slabs of a dead base, burst barriers, rusted containers, strangled masts, the great ribs of a hull. A browner sky. |
| Flood | Hive | Raw sinew under a bruised red-brown sky, cut by trenches of rot, with tall ribbed spires standing over it. |

All shapes and textures are original. Nothing was downloaded, and there are no logos or markings from anywhere.

## How it is built

- `Tools/TerrainGen/make_ground_textures.py` makes the tiled ground textures: three for each faction, a grey grain tile for concrete and metal, and a soft macro tile. They live in `Assets/_Project/Resources/Terrain`. The alpha of each layer is its height, so grounds give way to each other around stones and lumps rather than in a smooth fade.
- `Presentation/Terrain/TerrainBuilder.cs` builds a map from two factions and a seed: the land's height, the ground mesh, a 1024 x 1024 control map (which ground lies where, soft painted-in shadows), the sky dome and the haze. It takes about 300 ms, only when the map changes.
- `TerrainLayout.cs` and `TerrainLayoutsUnsc/Covenant/Flood.cs` are the layouts. A layout works on either side of the field, since the canvas says which side it is on. To add a map: write a class, and add it to the list in `TerrainLayouts`. No data asset is needed.
- `TerrainCanvas.cs` is what layouts draw on. It holds the rule that protects the floor (below).
- `TerrainProps.cs` holds the things that stand around the arena, made from a few boxes, tubes and lumps with colours in their vertices.
- `TerrainTheme.cs` holds each faction's textures, hills, sky, sun, ambient and haze. Colours are all in one place, so they are easy to change.
- `BattlefieldView.cs` shows the map and sets the sun and sky. It watches the factions and seed and rebuilds when they change.
- `UnitBlobShadows.cs` draws the blobs. One mesh rewritten each frame from kept arrays: one draw call, nothing allocated during battle. The only change to a shared file besides `BattlefieldView.cs` is one small read-only method in `UnitViewManager.cs` (`TryBodyPosition`).
- Shaders are in `Resources/Terrain`. They do their own light and haze, so they need no shader keywords that a build could strip. The land's haze is measured from the middle of the arena, not from the camera, so the fighting floor stays clear at every zoom.

## What keeps the game readable and fast

- **The floor is exactly flat.** Hills start only outside the arena and a flat band around it. Everything on the floor is flat paint, drawn before any unit, and it can't hide anything.
- **No solid patches of near-black on the Flood's floors.** Dark Flood forms stand there. The Wreck's pools were opaque and near black, 8 to 12 m across, so forms vanished on them and from a distance they looked like vehicles. They are now a mid-toned olive you can see through, and the Hive's trenches are a mid brown. A test checks it. The shell holes of no man's land stay dark on every map: they are small.
- **Nothing solid can hide the floor.** At the default view the camera stands inside the arena's width and to the south. So only things to the south of the floor and in line with it could get in the way, and they must stand back by 0.75 m per metre of height. `TerrainCanvas.Stand` refuses anything else. Hills to the south are capped low for the same reason. The tests check this on the final meshes of every layout. The camera can now turn (`camera.md`), and from other sides tall things and hills do stand in the way. So the terrain's shaders follow the line from the camera through each pixel of what stands, down to the ground, and leave the pixel out if that line lands on the floor: the outer 2 m of the floor may be hidden, and further in the thing thins out to nothing over 4 m (`KeepFloorInView` in `TerrainCommon.hlsl`). It goes by the deepest that line gets along the ground, from under the point to where it lands, so that the top of a post seen across a corner of the floor goes with the middle below it. A halo fades by the same measure, taken at its middle, so that it goes with the post it hangs on (`FloorHiding`, in `TerrainGlow`). From the default view nothing is in the way, so nothing changes there.
- **Triangles:** 55,000 to 85,000 for a whole map, under the 150,000 limit.
- **Draw calls:** one each for the ground, paint, solid things, light, haze and sky, plus two overlays and the blobs.
- **Frame time** in a built game at the game's camera, with the battle paused and about 50 bodies: 1.0 to 2.4 ms on this PC.

## The simulation

Untouched. Nothing here reads anything but the factions, the seed and where bodies are drawn. Combat, Core and Economy have no changes.

## Seeing every map

`Halobellum.exe -terrainGallery <folder>` goes through every layout of every faction, plus a Flood mirror match, and saves for each: bare ground, armies in deployment, battle, zoomed all the way out, close up on each side, and looking low at the horizon. It logs triangles and frame time. Add `-terrainMaps 2` for a quick look. Add `-terrainRing` to see each map from all round instead (`camera.md`).

## Known limits

- Glowing seams and veins are thin flat ribbons. On the High quality level (4x MSAA) they stay whole at the game camera. On Low there is no anti-aliasing, and the thinnest still break into dashes.

- At the game's camera the sky itself is out of view: the top edge of the view looks 30 degrees below the horizon at the default tilt, and 10 degrees below it at the lowest tilt the player can reach. It is there, and it shows in low views from tools, and its colour reaches the far land through the haze.
- Models don't get the haze, on purpose: units stay clear. Terrain shaders do it themselves, and the scene's Unity fog stays off.
- A change of map costs about 300 ms. It happens only at the draft choice in a match, and when the Sandbox's majority faction changes.
- The sun's colour and ambient are set for the whole map by the player's faction, so they tint the units too, and the enemy's ground. They are kept mild for that reason.
