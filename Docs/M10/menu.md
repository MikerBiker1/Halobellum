# Main menu, settings, pause and game flow (M10)

## What the player sees

The game now opens in a new scene, **MainMenu**. It fades in from black on a dusk battlefield seen from ground level: Marines in silhouette, Scorpions on the line, a burnt-out Warthog smoking, Wraiths further on, and a huge Scarab walking slowly through the haze toward the camera, with a low sun beside it. Pelicans cross the sky in three different groups. Every 26 seconds the Scarab fires its beam at the ground. Embers drift past. The camera wanders very slowly.

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

## Test tools

Every tool skips the menu and goes where it went before:

| Flag | Goes to |
|---|---|
| `-autoplay` | Match |
| `-crowdBench` | Match, and waits for it |
| `-sandboxDemo`, with or without `-gallery` | Sandbox |
| `-benchmark` | Battle (it used to do nothing in a normal build, whose first scene was Match) |

A test run also leaves the saved window, quality and VSync alone, and `-screen-width` and so on are never undone by saved settings.

New: **`-menuDemo <folder>`** walks through the whole flow, presses the real buttons and a real Esc through the Input System, and saves a picture of each screen. It writes `MenuDemo: ok: ...` or `MenuDemo: PROBLEM: ...` to the player's log, and ends with `MenuDemo finished: N problems.` Add `-menuDemoShort` for the menu and settings only. It never changes a setting, because PlayerPrefs are shared by every build on the PC.
