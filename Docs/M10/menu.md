# Main menu, settings, pause and game flow (M10)

## What the player sees

The game now opens in a new scene, **MainMenu**. It fades in from black on a dusk battlefield seen from ground level: Marines in silhouette, Scorpions on the line, a burnt-out Warthog smoking, Wraiths further on, and a huge Scarab standing in the haze on all four legs, with a low sun beside it. Pelicans cross the sky in three different groups. Every 26 seconds the Scarab fires its beam at the ground. Embers drift past. The camera wanders very slowly.

On the left, under a soft darkness so the text stands out: the title **HALOBELLUM** and the buttons **New Match** (with Easy / Normal / Hard next to it), **Sandbox**, **Settings** and **Quit**. There are no Halo logos or marks. The look (near-black panels, a fine edge, warm amber) belongs to these screens only, because they belong to no faction.

**Settings** has the same panel from the menu and from a match:

| Setting | PlayerPrefs key | Default |
|---|---|---|
| Master, Music, Effects, Interface volume | `Volume.Master`, `Volume.Music`, `Volume.Sfx`, `Volume.Ui` (floats, 0 to 1) | 1, 0.6, 1, 1 |
| Windowed or fullscreen | `Video.Fullscreen` (int) | as Unity remembers it |
| Resolution | `Video.Width`, `Video.Height` (ints) | as Unity remembers it |
| Quality (Low or High: the project's Mobile and PC levels) | `Video.Quality` (int) | the project's (High) |
| VSync | `Video.VSync` (int) | on |
| UI scale, 75% to 125% | `Ui.Scale` (float) | 100% |
| Camera shake | `Fx.CameraShake` (int) | on |

Each is applied at once and again when the game starts. "Reset to defaults" puts back sound, camera shake and UI scale, and leaves the window and quality alone.

**Pause.** Esc in a match or the Sandbox opens Resume, Settings, Quit to Main Menu, Quit Game. So does a small **Menu** button at the bottom right of the screen, for those who don't know about Esc. Leaving asks "Yes, leave / Stay" first.

- Esc keeps its older jobs: while something is being placed it cancels that, and with a squad selected it clears the selection. Only then does it pause. Esc inside Settings goes back; Esc on the pause menu resumes.
- Pausing truly stops the battle in every phase. It sets `Time.timeScale` to 0, so the battle clock gets no time, no tick runs, and animation and particles stand still. The battle speed (1x, 2x, 4x, or BattleHud's own Pause) is untouched, and the battle resumes at the speed it had.
- While paused the HUD and everything that takes clicks or keys on the field are switched off, and they come back as they were.
- What still moves behind a pause: a squad already falling into place (its fall runs on real time) finishes landing, and a spell's flash fades. Neither changes the battle.

**End of a match:** the screen now has **New match** and **Main Menu**.

**Sandbox's Back button** goes back where the Sandbox was opened from. It says "Main menu" if it was opened from the menu, and "Back to match" if from the draft screen.

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
- **Shop order.** A sub-faction's own units come first in the shop list (`MatchHud.FillShopRows`). This is the display only: `PlayerState.Roster` keeps its order, and with it unlocking, prices, card offers and the AI stay the same.
- **Result line in the Sandbox.** BattleHud drew it at y 90, and the Sandbox's bar is at y 92. `SandboxHud` now tells BattleHud to draw it under the bar (y 164). The speed controls did not move. A message during a battle moves one line further down. The Battle scene keeps the line where it was, and the Match scene has its own result screen.
- **Selection marker.** A flat ring instead of a solid disc (`PlaceholderArt.Ring`), open in the middle and a little wider than the disc was, 2.6 times the collision radius. It is only a mesh in the view.
- **Narrow windows.** `HudScale.ScaleFor` lets the UI scale grow only as far as 1140 units still fit across the window, so that the 1120-unit bars of the Match and the Sandbox stay on the screen. The only sizes in the Settings list that this changes are 120% and 125% at 5:4, which both show as about 118%. Every HUD of a scene uses the same scale, so they stay in line.
- **Items panel and squad window.** See above. On the narrowest windows (under about 1200 units) the moved panel lies over the left edge of the unit list, where the pictures and the "Place for" label are. The Place buttons stay clear.
- **Squad actions and squad window.** Under about 1420 units wide the Move / Turn / Sell panel ran under the stats window and hid the Move button. It now stands right of that window (`SquadStatsWindow.ActionsPanelLeft`), in the Match and the Sandbox.

## After the owner played

- **The menu Scarab stands still.** The owner liked it there but not its walk up close, so it no longer walks and no leg swings. `MenuBackdrop` has no walking left in it; its beam and glowing panels fire as before, and the Pelicans, the camera's drift and the Marines still move. It stands at (57.5, 169), where it was about ten seconds into the old walk, so the picture is the one that was praised.
- **All four feet are on the ground.** The old walk kept one height, so feet floated by up to 2.9 m or sank by up to 0.6 m along the way. The ground slopes about 2.5 m across the stance, so `MainMenuSceneBuilder.PlantLegs` sets the body level at the middle height and slides each leg 0.75 to 1.32 m up or down at the hip until its lowest vertex is 3 cm into the ground's real triangles. Nothing is rotated, and the slide does not show at the hips even through a long lens. These are overrides in the scene only: the prefab and the battle's Scarab are untouched. `MenuScarabTests` holds all of it, reading the vertices in the editor, because a game can't read a model's meshes.
- **What the camera sees of the feet.** A low swell in front hides the last 0.25 to 1.25 m of each leg from the menu's camera, about as it did before, so the feet meet the ground just behind that crest.

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

A test run also doesn't apply the saved window, quality and VSync, and `-screen-width` and so on are never undone by saved settings. `-terrainGallery` finds its own way to the Sandbox, and is now counted as a test run too.

`-cardsDemo <folder> -cardsDemoLayout` takes only the pictures of the narrow windows and large UI scales (5:4 and 4:3 at 110% and 125%): the deployment bar and shop, the Sandbox with and without a squad window, and its result line. It also takes each faction's selection ring on that faction's ground.

New: **`-menuDemo <folder>`** walks through the whole flow, presses the real buttons and a real Esc through the Input System, and saves a picture of each screen. It writes `MenuDemo: ok: ...` or `MenuDemo: PROBLEM: ...` to the player's log, and ends with `MenuDemo finished: N problems.` Add `-menuDemoShort` for the menu and settings only, or `-menuDemoWatch` to press nothing and log what clicks sent from outside the game do. It never changes a saved setting, because PlayerPrefs are shared by every build on the PC.
