# Daily report: 2026-10-31 (M2)

M0 and M1 were confirmed earlier today. This session completed **M2: Battle in the scene**. All criteria pass; waiting for your confirmation.

## Completed
- **Battle scene** `Assets/_Project/Scenes/Battle.unity`: plays the 300 vs 300 test battle (`Data/TestBattle_300v300.asset`) through the same simulation as the headless runner.
- **Units from data:** each body is spawned from the prefab set on its unit asset. Placeholder prefabs were generated for the 3 test units (`Prefabs/Units`): capsules for infantry, boxes for vehicles, sized from the collision radius and tinted blue or red by side. Units move by the simulation's free movement (no NavMeshAgent, no Unity physics), target, fight, show health bars when damaged and disappear when they die. Short tracer lines show instant shots, and small spheres show projectiles. The floor shows both deployment zones, no man's land, and a faint checkerboard of 2 m squares.
- **Camera:** fixed angled top-down view. Pan with W A S D / arrows or by middle-mouse dragging; zoom with the mouse wheel.
- **Speed controls:** Pause, 1x, 2x, 4x (buttons, or Space / 1 / 2 / 3). The top bar also shows the battle clock and bodies alive per side, and an FPS counter sits top-right.
- **Stats window:** left-click a unit to select its squad. Its stats appear bottom-left: squad size and bodies alive, cost, tier, role, health, armor, shield, speed, collision size, target filter, each weapon's damage, rate, range, splash and type, and any defense arc. Level, XP and upgrades come with M6.
- **Same result in game and runner:** the scene and the runner build battles with the same code. A PlayMode test plays the whole battle in the scene at high speed and checks its result line against the headless runner's (both: `seed 1000, 974 ticks (32.5 s), TeamBWins; A 0/300 alive, 32126.0 dmg; B 127/300 alive, 35640.0 dmg`). A new menu item, Halobellum > Headless Runner > Run Selected Battle Setup, lets you compare by hand.
- **Tools:** Halobellum > Scenes > Rebuild Battle Scene; Halobellum > Placeholders > Create Missing Placeholder Prefabs; a Windows build script; and a `-benchmark` mode for the built game.
- **Smoother frame times:** units now do their 0.5 s target searches on different ticks instead of all on the same tick. Each unit still re-checks every 0.5 s, as the Bible says.

## Test results
- Unity EditMode: **130 / 130** passed. Unity PlayMode: **3 / 3** passed (scene spawns one prefab view per body and units move; full battle matches the runner; squad selection).
- .NET harness: 122 / 122 passed. Your open Unity editor: no compile errors.

**Performance** (built Windows game, 1920 x 1080, 300 vs 300 placeholder units, whole battle; your PC: Radeon RX 7800 XT, Core i7-12700KF):

| Speed | Average FPS | 1% low FPS | Worst frame | Frames under 60 FPS |
| --- | --- | --- | --- | --- |
| 1x | 963 | 279 | 12.5 ms | 0 |
| 4x | 790 | 175 | 10.9 ms | 0 |

The simulation uses about 0.07 ms per frame at 1x and 0.33 ms at 4x. Your PC is above mid-range, but there is roughly 15x headroom below the 16.7 ms frame budget for 60 FPS, so a mid-range PC should hold 60 FPS comfortably. That is not measured. Screenshot: `Docs/M2_battle_300v300.png`.

## Problems found and fixed
- Built games showed a completely magenta screen. Unity's runtime primitives come with a built-in material URP can't draw in builds. Fixed with a real URP Lit material asset (`Art/Placeholder/PlaceholderLit.mat`) referenced by the scene. The editor never showed the problem.
- My batch-mode copy of the project must live at a short path because of Windows' 260-character path limit, so it is now at `E:\AI\_hbci` (safe to delete).

## Blockers
None.

## Decisions needing your approval
- **D11:** keep URP's SRP Batcher instead of GPU instancing for now; revisit GPU instancing and LOD with real models in M9.
- **D12:** health bars also show for the selected squad at full health.

## Update after your M2 feedback (same day)
- **D11 and D12 approved** and added to the Bible's decision log.
- **Shield anti-armor multiplier corrected to 1.0x** (asset, test fixture, decision log).
- **Turning speed:** every unit has a 180-degree turn time (`turnTime180` on the unit asset). Placeholders: infantry 0.25 s, light vehicle 1 s, heavy tank 2 s. Units drive along their facing, turn toward targets at that speed, and the main weapon waits until the unit faces its target within 15 degrees. **D13** describes this and needs your approval. The stats window shows the turn time.
- **Balanced test battle** `Data/TestBattle_Balanced.asset`, now the Battle scene's battle. Two identical 300-body armies (12 tanks front and centre, 12 light-vehicle squads on the flanks, 22 infantry squads in four staggered rows so they arrive in waves). Battle setups can now place each squad at a position in deployment squares. Result: Team B wins after 45.9 s with 18 of 300 left. The front tank lines start 12 m apart (only 2 squares of no man's land), inside the tanks' 16 m range, so they fire right away.
- **Tests:** Unity EditMode 140 / 140, PlayMode 3 / 3, .NET 132 / 132. The scene still matches the headless runner exactly.
- **Performance** (balanced battle, built game, 1920 x 1080): 1x average 897 FPS, worst frame 10.4 ms; 4x average 649 FPS, worst frame 12.1 ms; no frame below 60 FPS.
- Screenshots: `Docs/M2_balanced_start.png`, `Docs/M2_balanced_6s_selected.png`.

## Last M2 battle test (same day)
- **D13 approved** and added to the Bible's decision log.
- **New layout** of `Data/TestBattle_Balanced.asset` (both armies identical): 6 infantry squads as a screen in front, the 12 tanks moved back from row 2 to row 6, flankers unchanged on both sides, and the other 16 infantry squads spread through the zone in 5 staggered rows down to both back corners (row 28).
- **Placeholder speeds lowered as a trial:** infantry 4 to 3 m/s, light vehicle 9 to 7 m/s, heavy tank 3 to 2.5 m/s. These are test units, not roster data; real speeds come with the M4 units.
- **Result:** Team A wins after 54.3 s with 2 of 300 left. The front chaff screens fight from the first seconds; the middle waves reach the front by about 15 s and the back-corner squads arrive last, around 15 to 20 s. Screenshots: `Docs/M2_waves_03s.png`, `Docs/M2_waves_15s.png`, `Docs/M2_waves_30s.png`.
- **Tests:** Unity EditMode 140 / 140, PlayMode 3 / 3 (the scene still matches the headless runner exactly).
- **Performance:** 1x average 1096 FPS; one single frame of 16.7 ms (right at the 60 FPS limit) out of 59,316; 4x average 1010 FPS with no slow frames.
