# M10: terrain that depends on the faction

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
- **Nothing solid can hide the floor.** At the default view the camera stands inside the arena's width and to the south. So only things to the south of the floor and in line with it could get in the way, and they must stand back by 0.75 m per metre of height. `TerrainCanvas.Stand` refuses anything else. Hills to the south are capped low for the same reason. The tests check this on the final meshes of every layout. The camera can now turn (`camera.md`), and from other sides tall things and hills do stand in the way. So the terrain's shaders follow the line from the camera through each pixel of what stands, down to the ground, and leave the pixel out if that line lands on the floor: the outer 2 m of the floor may be hidden, and further in the thing thins out to nothing over 4 m (`KeepFloorInView` in `TerrainCommon.hlsl`). From the default view nothing is in the way, so nothing changes there.
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
