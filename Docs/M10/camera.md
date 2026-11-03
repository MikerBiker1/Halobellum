# M10: the camera that turns

The owner asked: "Right click hold and move mouse should change the angle of the camera. It should still point to the ground at an angle, just rotate around that point." This is what was built.

## The controls

| Control | What it does |
| --- | --- |
| Hold the right mouse button and move the mouse sideways | Turns around the point on the ground the camera looks at, a full circle if wanted. |
| Hold the right mouse button and move the mouse up and down | Tilts between 35 and 75 degrees. Up lays the view flatter. The game starts at 55, as before. |
| W A S D, the arrow keys, or drag with the middle mouse button | Pans along the ground, relative to where the camera faces. Facing north, as the game starts, they do what they always did. |
| Mouse wheel | Zooms, as before. |
| C | Back to the view the scene started with: facing north, tilted 55 degrees, the start focus and zoom. |

The point looked at never moves while turning, and it stays inside the arena as before. A drag that starts over a HUD window turns nothing. Each scene (Match, Sandbox, Battle) starts at the default view. The view stays as the player left it from round to round in a match.

## Right-click and right-drag

A right-click cancels a placement, goes back a step on a spell and clears the selection, at five places in the code. A right-drag must do none of that, so **a right-click is now pressed and released with less than 6 pixels of movement within half a second, and it is acted on at the release.** Moving further is a drag. Holding longer without moving is neither. Movement counts from the frame after the press: what the mouse moved in the press's own frame happened around the press, so a right-click with the mouse still on its way somewhere stays a click and turns nothing. If the mouse keeps moving 6 pixels or more while the button is down, that is a drag.

`RightClick.cs` holds this. `RightClickTracker` is the logic with no mouse in it. `RightClick` is the one tracker the whole game reads, and the first script to ask in a frame moves it on, so all of them get the same answer that frame, whatever order they run in. The camera asks every frame, so no press goes unseen. The five places that use it: `DeploymentInput`, `SandboxController`, `SpellView` (twice), `SquadSelection`. Tests hand `RightClick.ReadForTests` what a mouse would have read.

The small price: a click takes effect when the button comes up rather than when it goes down.

## What assumed a fixed camera

| Thing | What it did | Now |
| --- | --- | --- |
| Tall things and hills around the field | Stood anywhere but south of the floor, where no camera stood behind them | The terrain's shaders leave out what stands between the camera and the floor (`KeepFloorInView`, see `terrain.md`). Nothing moved, and from the default view nothing is in the way. |
| Halos of lamps and crystals, specks of dust and spores | Flat shapes tilted towards the south, invisible from the north | They turn to face the camera in `TerrainGlow`'s vertex shader. At the default view they are where they were. A halo also fades by the same sight-line rule as what it hangs on (`FloorHiding`), so that no light hangs in the air over the floor with its post gone from under it. Specks drift free of anything, so they stay. |
| The nuke's cap | Narrower north to south, to leave the stem in view | Narrower along the way the camera faced at the blast (`MushroomCloud`). It doesn't turn afterwards. |
| The camera shake's size | Worked out the distance from the height at a 55 degree tilt | Uses the real distance |
| `EffectKit.ReadScale` | Height over 90 m, so tilting would have resized tracers and flames | Goes by the distance. The same at the default tilt. |
| Health bars, level-up arrows | Already faced the camera, one frame late if the camera moved after them | The camera now moves first (`DefaultExecutionOrder`). |

These were checked and needed nothing: land and haze (built round the middle of the map on all sides), the far clip plane (a test checks the corners of the view at the lowest tilt and widest zoom), deployment squares and spell outlines (flat on the ground), ghosts and picking (rays from the camera), blobs under units (thrown by the sun, not the camera), sound panning (by where a thing is on screen), billboards in effects (Unity's own).

## Taking pictures

- Every tool keeps the default view unless told otherwise, so that older pictures compare.
- `-cameraYaw <degrees>` and `-cameraPitch <degrees>` start any test tool from that view. `-sandboxDemo` galleries use them for their game-camera pictures. Tools may go beyond the player's limits this way.
- `-terrainGallery <folder> -terrainRing` takes each map from all round through the real controller: a squad being placed from 4 sides, an item's hexagons from 4 sides, then in battle from 8 directions at tilts of 55, 35 and 75 at the widest zoom, and from 8 directions close up across the floor's edge nearest the camera, at 70 m and at 45 m. It writes `<map>_place_y<yaw>`, `<map>_hex_y<yaw>`, `<map>_wide_p<tilt>_y<yaw>`, `<map>_near_y<yaw>` and `<map>_low_y<yaw>`.

`BattleCameraController.Orbit`, `Pan`, `DragGround`, `SetView` and `ResetView`, `DeploymentInput.PointAt` and `SquadSelection.BodyAt` can be called without a mouse.

## Known limits

- No real mouse has moved this camera in a test, since nothing here may send input to the PC. The logic is tested from what a mouse would read. The feel (speed, direction) needs a person: 0.2 degrees per pixel, in `orbitDegreesPerPixel`.
- Moving the mouse right brings the near ground to the right, and moving it up lays the view flatter, like grabbing the ground. If that feels backwards it is one sign each in `Orbit`.
- The owner's mouse could run into the edge of the screen in a long drag. The Input System's movement is what turns the camera, not the pointer's position, but whether it keeps coming at the screen's edge has not been seen.
- The ground's big cells at the floor's four corners rise a little towards the first hill, since the mesh has few lines over the floor: it is the existing mesh, and tests check its vertices rather than what lies between them. The see-through rule leaves whatever lies on the floor alone, so that this doesn't open a hole.
- Zoomed right in and looking low over a corner of the field, a big thing standing just outside it (a Hive mound, a crystal) can fill much of the screen. The rule above only leaves out what hides the floor, not what hides the land around it. Turning or zooming out clears it.
- A right-click made while the mouse goes on moving by 6 pixels or more before the button comes up is a drag, and cancels nothing. Only the movement of the press's own frame is forgiven.
