# Main menu, settings, pause and game flow (M10)

## What the player sees

**Since D94** the game opens on the **cinematic menu** (MainMenu, built by `Editor/MenuCinematicBuilder.cs`, moved by `UI/Menu/MenuCinematic.cs`, its sky `Art/MenuCinematic/MenuCinematicSky.shader`): the three sides at war under a storm at dusk, in a 52-second loop of five slow camera shots that fade through black into one another (Vista, Line, Covenant, Flood, Titan), the Scarab walking the field on planted feet and firing its beam, shells, plasma and mortars bursting, fires, smoke, fog, lightning and a warship glassing the hills. Every shot keeps its action in the right half, clear of the dark panel under the menu's text. Rebuild it with Halobellum > Scenes > Rebuild Main Menu Scene, or `-executeMethod Halobellum.EditorTools.MenuCinematicBuilder.BuildAndPreviewFromCommandLine`, which also renders a picture of each shot. The menu described below is kept as **MainMenuClassic** (Halobellum > Scenes > Rebuild Classic Main Menu Scene); **Settings > Menu background** chooses between them (`Ui.MenuBackground`: 0 cinematic, 1 classic), and a test tool always opens the cinematic one.

The classic menu: it opened in a new scene, **MainMenu** (now MainMenuClassic). It fades in from black on a dusk battlefield seen from ground level: Marines in silhouette, Scorpions on the line, a burnt-out Warthog smoking, Wraiths further on, and a huge Scarab standing in the haze on all four legs, with a low sun beside it. Pelicans cross the sky in three different groups. Every 26 seconds the Scarab fires its beam at the ground. Embers drift past. The camera wanders very slowly.

On the left, under a soft darkness so the text stands out: the title **HALOBELLUM** and the buttons **Play**, **Settings** and **Quit**. **Play** leads to a second page with **Faction mode**, **Unlock Skirmish mode**, **Sandbox** and **Back**. Choosing a mode opens a panel on the right with its settings and **Start**: difficulty for Faction mode; difficulty, base health (100 to 10000) and a card list for Unlock Skirmish. Esc steps back a page (D62). There are no Halo logos or marks. The look (near-black panels, a fine edge, warm amber) belongs to these screens only, because they belong to no faction.

**Settings** has the same panel from the menu and from a match:

| Setting | PlayerPrefs key | Default |
|---|---|---|
| Master, Music, Effects, Interface volume | `Volume.Master`, `Volume.Music`, `Volume.Sfx`, `Volume.Ui` (floats, 0 to 1) | 1, 0.6, 1, 1 |
| Screen | `Video.Screen` (string: name, width, height and place in Unity's list) | Windows's main screen |
| Windowed or fullscreen | `Video.Fullscreen` (int) | as Unity remembers it |
| Resolution | `Video.Width`, `Video.Height` (ints) | as Unity remembers it |
| Quality (Low or High: the project's Mobile and PC levels) | `Video.Quality` (int) | the project's (High) |
| VSync | `Video.VSync` (int) | on |
| UI scale, 75% to 125% | `Ui.Scale` (float) | 100% |
| Camera shake | `Fx.CameraShake` (int) | on |
| Menu background: Cinematic or Classic (D94) | `Ui.MenuBackground` (int) | Cinematic |

Each is applied at once and again when the game starts. "Reset to defaults" puts back sound, camera shake and UI scale, and leaves the window, the screen and quality alone.

**Pause.** Esc in a match or the Sandbox opens Resume, Settings, Quit to Main Menu, Quit Game. So does a small **Menu** button at the bottom right of the screen, for those who don't know about Esc. Leaving asks "Yes, leave / Stay" first.

- Esc keeps its older jobs: while something is being placed it cancels that, and with a squad selected it clears the selection. Only then does it pause. Esc inside Settings goes back; Esc on the pause menu resumes.
- Pausing truly stops the battle in every phase. It sets `Time.timeScale` to 0, so the battle clock gets no time, no tick runs, and animation and particles stand still. The battle speed (1x, 2x, 4x, or BattleHud's own Pause) is untouched, and the battle resumes at the speed it had.
- While paused the HUD and everything that takes clicks or keys on the field are switched off, and they come back as they were.
- What still moves behind a pause: a squad already falling into place (its fall runs on real time) finishes landing, and a spell's flash fades. Neither changes the battle.

**End of a match:** the screen now has **New match** and **Main Menu**.

**Sandbox's Back button** goes back where the Sandbox was opened from. It says "Main menu", because the menu's Play page is the only way in (D62).

## How it fits together

New files, so that they merge easily:

- `Combat/Presentation/GameFlow.cs`: the scene names, New Match, Sandbox, Quit, and where each test tool goes.
- `Combat/Presentation/GamePause.cs`: the one pause switch. Sound or anything else can listen to `GamePause.Changed`.
- `Combat/Presentation/GameSettings.cs`: every setting, its key and default, and applying them at start.
- `UI/Menu/MainMenuHud.cs`: the menu screen.
- `UI/Menu/SettingsScreen.cs`: the settings panel.
- `UI/Menu/PauseMenu.cs`: adds itself at run time to any scene with a BattleController, so no scene file holds it.
- `UI/Menu/MenuStyle.cs`, `MenuLook.cs`: the look, and every button, so that the test tool can press them.
- `UI/Menu/MenuBackdrop.cs`: what moves in the picture.
- `UI/Menu/MenuDemo.cs`: the test tool below.
- `Editor/MainMenuSceneBuilder.cs`: makes the scene and everything in `Art/Menu`.
- `Art/Menu/`: the sky shader, and the ground, ridges, rock, smoke, glow and materials the builder wrote. All of it is original and made by code. The models are the project's own. The fonts were already in the project.

Shared files were touched as little as possible:

- `MatchHud`: one button added at the end of the match. `SandboxHud`: the Back button's one line.
- `HudScale`: the player's UI scale multiplies it, and a new `ApplyFit` keeps a menu inside a narrow or tiny window.
- `CrowdBenchmark`: its flag is public, and it waits until the menu has handed over.
- `MatchController`: the difficulty key is public.
- `BattleSceneBuilder`: MainMenu is first in `GameScenes`.
- `EditorBuildSettings`: MainMenu is first.
- The UI, Editor and test assembly definitions: added references.

The simulation is not touched.

To change the picture, edit `MainMenuSceneBuilder.cs` and run it (Halobellum > Scenes > Rebuild Main Menu Scene, or `exec Halobellum.EditorTools.MainMenuSceneBuilder.BuildFromCommandLine`). `BuildAndPreviewFromCommandLine` also renders three stills without building the game, in about 3 minutes.

## Fixed after the review

- **Camera shake** has one saved value, `Fx.CameraShake`. `CameraShake.ShakeEnabled` reads and writes `GameSettings.CameraShake`, so the K key, the Settings row and Reset to defaults all agree. The old key, `Halobellum.CameraShake`, is no longer read, so a K-off saved before this comes back on once.
- **Going over a button sounds.** Every button of the menu, Settings and the pause menu now has the hover sound the HUD has (see `audio.md`), only when the pointer moves onto it. The check is `-menuDemo <folder> -menuDemoSounds`.
- **Sound stops with Esc.** `BattleAudio` pauses battle sounds, including the engine and hover hums, when `GamePause.IsPaused` is true, as well as at battle speed 0. Music and UI sounds go on.
- **The Sandbox sets its own factions.** `SandboxController.FactionOf(team)` is the faction most of a side's squads belong to. A sub-faction's unit counts as its parent faction, a placeholder counts as nothing, and an empty side is UNSC for blue and Covenant for red. It sets `BattleFactions` when the scene starts and before each change of army is shown. The map, sound and weapon looks all read that, so nothing is left over from a match.
- **Items panel.** At widths under 1820 the Sandbox bar moves right to clear it. Under 1480 the bar can't fit there, so it stays centred and the panel goes under it, level with the unit list. A squad stats window tall enough to cover the panel now pushes it to the right of that window (second round, below).
- **The menu Scarab.** Its glowing materials keep 12% of their colour and 3% of their glow, and the glow rises to 50% only as the beam fires. It uses a property block found at run time, so the shared materials, the battle's Scarab and the scene file are untouched. The numbers are `panelColorShare`, `panelGlowAtRest` and `panelGlowFiring` on `MenuBackdrop`.
- **Small ones.** The round and match summaries show bases at 0 or more. The match bar's title has a taller rect: Unity's clip moves up with the theme's text shift, and at small scales it cut the bottom of the letters.

## Second round of fixes

- **AI difficulty label.** It stands past the left side's darkness, on the bright picture, so it now uses the bright text colour on a little darkness of its own that fades to the right.
- **Spell text.** "1 x ODST squad lands" and "2 x ODST squads land". A number and its unit are joined by a non-breaking space (U+00A0) in the text of spells, items and techs, so that a line never ends between "8" and "m". A test measures this with all five fonts the game uses: the space is the same width as a plain one, and a wrapped line does not break at it.
- **Shop order.** Tier, then price, in every mode, since D62 (`ShopOrder.Sort`). Until then a sub-faction's own units came first (`MatchHud.FillShopRows`). This is the display only: `PlayerState.Roster` keeps its order, and with it unlocking, prices, card offers and the AI stay the same.
- **Result line in the Sandbox.** BattleHud drew it at y 90, and the Sandbox's bar is at y 92. `SandboxHud` now tells BattleHud to draw it under the bar (y 164). The speed controls did not move. A message during a battle moves one line further down. The Battle scene keeps the line where it was, and the Match scene has its own result screen.
- **Selection marker.** A flat ring instead of a solid disc (`PlaceholderArt.Ring`), open in the middle and a little wider than the disc was, 2.6 times the collision radius. It is only a mesh in the view.
- **Narrow windows.** `HudScale.ScaleFor` lets the UI scale grow only as far as 1140 units still fit across the window, so that the 1120-unit bars of the Match and the Sandbox stay on the screen. The only sizes in the Settings list that this changes are 120% and 125% at 5:4, which both show as about 118%. Every HUD of a scene uses the same scale, so they stay in line.
- **Items panel and squad window.** See above. On the narrowest windows (under about 1200 units) the moved panel lies over the left edge of the unit list, where the pictures and the "Place for" label are. The Place buttons stay clear.
- **Squad actions and squad window.** Under about 1420 units wide the Move / Turn / Sell panel ran under the stats window and hid the Move button. It now stands right of that window (`SquadStatsWindow.ActionsPanelLeft`), in the Match and the Sandbox.

## After the owner played

- **The menu Scarab stands still.** The owner liked it there but not its walk up close, so it no longer walks and no leg swings. `MenuBackdrop` has no walking left in it; its beam and glowing panels fire as before, and the Pelicans, the camera's drift and the Marines still move. It stands at (57.5, 169), where it was about ten seconds into the old walk, so the picture is the one that was praised.
- **All four feet are on the ground.** The old walk kept one height, so feet floated by up to 2.9 m or sank by up to 0.6 m along the way. The ground slopes about 2.5 m across the stance, so `MainMenuSceneBuilder.PlantLegs` sets the body level at the middle height and slides each leg 0.75 to 1.32 m up or down at the hip until its lowest vertex is 3 cm into the ground's real triangles. Nothing is rotated, and the slide does not show at the hips even through a long lens. These are overrides in the scene only: the prefab and the battle's Scarab are untouched. `MenuScarabTests` holds all of it, reading the vertices in the editor, because a game can't read a model's meshes. Since D84 the Scarab is the owner's updated model: a leg is its `Leg_` node with the knee under it, the body is levelled on the four feet's middle height (the new model's front feet stand a little off the ground as made), and the slides are 0.68 to 1.39 m.
- **What the camera sees of the feet.** A low swell in front hides the last 0.25 to 1.25 m of each leg from the menu's camera, about as it did before, so the feet meet the ground just behind that crest.

## Owner's quick wins: Level all, and the Screen setting

**Level all.** Beside Level up in the squad window, a button such as "Level all 3 (225)" levels every ready squad of the selected squad's unit type at once. It shows when 2 or more squads of the type are ready, whether or not the selected one is, also from a squad at the maximum level, which has no Level up. It is greyed out when the credits fall short, and outside deployment. It is all or nothing (`Match.LevelUpAll`, with `Match.SquadsReadyToLevel` for the count and price). Each squad's own `LevelUpSpent` goes up, so refunds stay right. A squad whose bar isn't full or at level 5 is left out. It goes the way Level up goes in `MatchController`, so it has the message, `ActionTaken` and the level-up sound (`MatchAudio`), and the field is rebuilt with the same squad selected. The Sandbox doesn't have it: its Level + is free. The window measures the level line and the two buttons with the theme's fonts. They all fit on one row in all three themes, with about 40 units to spare, so the window doesn't grow. If a theme's fonts ever left no room, the two buttons would go on a row of their own under the level line.

**Screen.** A row in Settings, first under Display, lists the PC's screens by name and size, the main one first and marked "(main)". The arrows move the game there at once, and the choice is saved. With one screen the row has no arrows and says "Only one: ...". `Core/DisplayChoice.cs` has the rules, in plain C# with tests, and `Presentation/GameDisplays.cs` does the moving.

- **At start** (`GameSettings.ApplyAtStart`, not in a test run, not in the editor, and not with `-screen-width`, `-monitor` and so on on the command line) the game goes to the saved screen. If none was ever chosen, or the saved one is gone, it goes to Windows's main screen. So a window dragged to another screen by hand goes back to the main one at the next start, until a screen is chosen in Settings.
- **The main screen.** Unity doesn't say which it is. On Windows, `DisplayInfo.workArea` starts at 0,0 for every screen, wherever it is on the desktop, so that can't tell them apart: logged on this PC, all three say x 0, y 0. `GameDisplays` asks Windows for the main screen's size (`MonitorFromPoint` and `GetMonitorInfo`, read only), and takes the first screen of that size in Unity's list. If there are two screens of that size, and the main one is the second in Unity's list, it takes the wrong one. On this PC the sizes all differ, and Unity's first is the main one.
- **A saved choice** is `name|width|height|place`. The same name, size and place comes first, then the same name and size, then the same name at another resolution. The place only tells two screens of one model apart.
- **Sizes.** `Screen.resolutions` follows the screen the window is on (measured: 15 sizes on the Dell, 20 on the LG, 17 on the HP), so the Resolution row lists them again after a move, and only those that fit. A window too big for the new screen shrinks to the work area less a title bar. Fullscreen at the old screen's own size takes the new screen's own size. Fullscreen at a smaller chosen size keeps it.
- **Unity's own memory.** `UnitySelectMonitor` is the window's place in Unity's list, and `Screenmanager Window Position X/Y` is its corner on that screen. Unity keeps them up to date as the window moves, even in a run that is killed. It opens the next start there before any game code runs, so a start that has to move sees the window on the old screen for about 3.5 seconds (measured from the window appearing). The move then updates Unity's memory, so the next start opens on the right screen.

The check is **`-screenDemo <folder>`**, on a test build only, and silent like the other tools. It presses the row's own buttons (no real mouse) and really moves its window to each screen in turn, windowed and fullscreen, logging `Screen.mainWindowDisplayInfo` before and after. `-screenDemoEndOn <n>` ends on the n-th screen of the row, so that the next start opens there. `-screenDemo <folder> -screenDemoFake` looks at the row with made-up lists at every UI scale and at five window sizes. `-cardsDemo <folder> -cardsDemoLevelAll` does the same job for Level all: prices, greying out, no overlap at every UI scale and theme, and pictures.

## Test tools and the player's settings

A run of a test tool (`GameFlow.IsTestRun`: all the flags below, `-menuDemo`, `-terrainGallery` and `-audioCapture`) holds every setting in memory (`GameSettings.HoldInMemory`). It starts from what was saved. What it changes, by a click or a K press that reaches its window, lasts only as long as the run, and Reset to defaults and Save do nothing to the saved values. Each such run ends its log with `GameSettings: this test run changed no saved setting.`, or a `PROBLEM` line with both sets of values. The AI's difficulty goes through `GameSettings.Difficulty` too. The PlayMode tests hold the settings the same way (`SettingsHeldForTests`), and the menu's EditMode tests do it test by test. Tests that are about PlayerPrefs itself still touch it and put back what they found, including that a key did not exist: two in `MenuFlowTests` and one in `AudioSceneTests`, because the sound code reads the saved volumes itself. So a volume changed in a test run is not heard.

The tests run in the editor, whose PlayerPrefs are under another key from a built game's: `Software\Unity\UnityEditor\DefaultCompany\Halobellum`, against `Software\DefaultCompany\Halobellum`. They never touched the built game's settings, only those used when pressing Play in the editor.

Unity's own remembered window can't be held from inside the game. It lies in the same PlayerPrefs (`Screenmanager Resolution Width` and so on), and `-screen-width` and the like are already written there when the first game code runs: a run at 1280x1024 logged those values at BeforeSceneLoad. Unity also remembers each later change of window size. So any run of a normal build with a window size on its command line changes what the next start of that build uses.

The way out is a build of its own. `PlayerBuildTool` with `-testBuild` names the product "Halobellum Test", and Unity keeps PlayerPrefs and the window under the product's name (`Software\DefaultCompany\Halobellum Test`), so a run of that build can't reach the player's key at all. Only the build differs: the project's name goes back afterward. Workstream builds that are only for test tools should use it.

## Test tools

Every tool skips the menu and goes where it went before:

| Flag | Goes to |
|---|---|
| `-autoplay` | Match |
| `-crowdBench` | Match, and waits for it |
| `-sandboxDemo`, with or without `-gallery` | Sandbox |
| `-benchmark` | Battle (it used to do nothing in a normal build, whose first scene was Match) |
| `-cardsDemo` | Match |
| `-screenDemo` | stays in the menu, as `-menuDemo` does |

A test run also doesn't apply the saved window, quality and VSync, and `-screen-width` and so on are never undone by saved settings. `-terrainGallery` finds its own way to the Sandbox, and is now counted as a test run too.

`-cardsDemo <folder> -cardsDemoLayout` takes only the pictures of the narrow windows and large UI scales (5:4 and 4:3 at 110% and 125%): the deployment bar and shop, the Sandbox with and without a squad window, and its result line. It also takes each faction's selection ring on that faction's ground.

New: **`-menuDemo <folder>`** walks through the whole flow, presses the real buttons and a real Esc through the Input System, and saves a picture of each screen. It writes `MenuDemo: ok: ...` or `MenuDemo: PROBLEM: ...` to the player's log, and ends with `MenuDemo finished: N problems.` Add `-menuDemoShort` for the menu and settings only, or `-menuDemoWatch` to press nothing and log what clicks sent from outside the game do. It never changes a saved setting, because PlayerPrefs are shared by every build on the PC.
