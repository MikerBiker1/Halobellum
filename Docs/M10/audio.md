# Sound and music (M10)

The game now has sound everywhere: battle sounds, UI sounds that differ by faction, and music that follows the faction and the
screen. All the audio is **original placeholder work made by a script**: nothing was downloaded and no melody is copied from
anything. The owner's own files replace any of it just by being dropped in a folder.

**The owner must judge how it sounds.** It was built by an agent that can't hear. It was checked by measuring the files, by looking at
spectrograms of them, by recording what the running game really outputs, and with tests that the right sound is asked for at the right moment.

## Putting in your own music and sounds

Put the file in `Assets/_Project/Art/Imported/Audio/Resources/AudioOverride/`, named after its slot, for example
`music_unsc_battle.ogg`. That is all. `Assets/_Project/Art/Imported/Audio/README.txt` lists every slot and what it is for.

- Several files for one slot: `music_unsc_battle_1.ogg`, `music_unsc_battle_2.ogg`. One is picked each time.
- Import settings are set for you by the file's name (`Scripts/Editor/AudioImportRules.cs`): music streamed and compressed, stingers
  compressed, effects and UI sounds uncompressed and ready in memory.
- Use `.ogg` or `.wav` for music. An `.mp3` usually won't loop without a gap.
- Removing the file brings the generated one back.

## What plays when

| Screen | Music |
|---|---|
| Scene `MainMenu` | `music_mainmenu` |
| Draft (no side chosen yet) | `music_mainmenu` carries on |
| Cards, round result, match over | the player's faction's menu track |
| Deployment | the player's faction's deployment track |
| Battle | the player's faction's battle track |
| Match won or lost | the faction's victory or defeat stinger, with the track ducked under it |
| Sandbox | deployment and battle tracks of `BattleFactions.TeamA` |
| Battle scene | battle track of `BattleFactions.TeamA` |
| Any other scene (a settings screen) | whatever is playing carries on; the main menu's if nothing is |

Tracks crossfade (2.5 s, 1.5 s into a battle). Each track remembers where it was and resumes there, because rounds are short: otherwise only the
first minute of a battle track would ever be heard. A new match starts them over.

UI sounds use the player's faction from the moment the draft is chosen. Before that (main menu, draft) a small neutral set plays.

## How it is built

Code is in `Scripts/Combat/Presentation/Audio/`, created at run time, with no scene files touched:

- `AudioDirector`: made before the first scene, survives scene loads. All sound goes through it. A fixed pool: 20 voices for effects, 5 for UI, 2 for
  hums, 3 for music. Nothing is created during battle. Watches clicks, polls the volumes, attaches the listeners below when a scene loads. `GameAudio` is the short way to call it.
- `AudioSlots`: the names, and which name goes with which faction, screen or spell.
- `AudioCatalog`: every battle sound: its group, volume, pitch spread and priority; and the limits per group. **This is the file to tune the mix in.**
- `AudioLibrary`: finds the file behind a name: the owner's first, then the generated one.
- `VoiceLimiter`: at most so many at once and a shortest gap per group (rifles: 4 at once, 70 ms apart). 200 rifles cost the same as 4.
- `MusicDeck`: the crossfades, resuming, and stingers.
- `BattleAudio`: listens to the simulation's events. It only reads. Added to the object with the `BattleController`.
- `MatchAudio`, `SandboxAudio`: screens and player actions.
- `UiAudio`: hover and greyed-out-button sounds for IMGUI.
- `AudioCapture`: a test tool, see below.

**Sound at a high camera.** Sounds aren't placed in 3D: at that height 3D falloff would make everything equally far and quiet. They are 2D, panned by where the
thing is on screen, and quieter when it is off screen.

**Battle time.** Effects and hums pause with the battle (speed 0). Music and UI sounds go on.

**Clicks with almost no HUD changes.** An IMGUI button takes `GUIUtility.hotControl` when the mouse goes down on it. The director watches that each frame, so every button in every HUD,
including ones not written yet, clicks by itself on the mouse press, as long as it is IMGUI. A uGUI screen would need `GameAudio.Ui(UiSound.Click)` in its buttons.

**What the player did.** `MatchController.Report` is the one place all actions pass through. It now raises `ActionTaken(name of the method, result)`, using the compiler's
`CallerMemberName`, so `MatchAudio` knows Buy from Sell from a failed action, with no change in each action.

**Hover** can't be seen from outside IMGUI, so it takes one line per button (`UiAudio.Watch(rect, enabled)`), which also gives the error sound for a click on a greyed-out button. It is on the shop's buttons, card
buttons, draft cards and Ready only.

**Lines changed in shared files** (everything else is new files): `MatchController.cs` (+4, for `ActionTaken`), `MatchHud.cs` (+5, `UiAudio.Watch`), `ItemHud.cs` (+1, an item picked up clicks),
`Halobellum.Tests.EditMode.asmdef` (+1).

**No noise in test runs.** When the game runs in batch mode, or with `-autoplay`, `-sandboxDemo` and the like, the director sets the listener's volume to 0, so that runs at night don't fill the room. Add `-audioOn` to hear them.

**Volumes.** Reads PlayerPrefs `Volume.Master` (1), `Volume.Music` (0.6), `Volume.Sfx` (1), `Volume.Ui` (1) every half second, without depending on the settings screen's code.

## The sounds

Generators are in `Tools/Audio/` (Python with numpy; run with the venv):

- `synth.py`: oscillators, filters, reverb, instruments (brass, strings, choir, drums, chimes, wet noises), loudness.
- `make_sfx.py`: effects and UI sounds, 159 files. `make_music.py`: 10 tracks and 6 stingers.
- `measure.py`: the checks below, and spectrograms with `--pictures`. `make_readme.py`: the slot list.

Files are in `Assets/_Project/Audio/Generated/Resources/GeneratedAudio/` (a `Resources` folder so that they load by name).

**Weapons.** UNSC ballistic: a crack, a body of filtered noise, a pitched thump, a room slap. Covenant: FM sweeps, buzz and fizz for plasma, glassy pings for needles, a bending buzz for the sword.
Flood: wet squelches with bubbles in them (the ear takes many tiny rising chirps for liquid), a bone-hard tick for spat bone. Explosions in three sizes, and rockets and shells make a small one where they land.

**UI sets.** UNSC: short ticks and square-ish data beeps, mechanical latches, short horn calls. Covenant: inharmonic glass chimes with slow beating, using the scale of the Covenant music, low choir. Flood: squelches, bubbles, heartbeats, growls.

**Music.** All tracks are exactly 90 seconds, rendered on a circular buffer, so a tail running off the end comes back at the start: the loops close by construction.

| Set | Character | Deployment | Battle | Menu |
|---|---|---|---|---|
| UNSC | D minor. A march with snare, low strings, horns, trumpets, two original themes | 96 BPM: alone, theme A on horns, theme B on trumpets, A up a fourth with everyone, falls away, builds on the dominant back to the start | 128 BPM: 16th-note low strings, stabs, theme, up a tone, drum break, theme B high, low brass | 80 BPM: one far horn at half speed, warm strings, a distant march |
| Covenant | D, Eb, F#, G, A, Bb, C#, some notes a little flat of that; stacked fourths and clusters; choir-like voices, deep drums, glass chimes | 72 BPM: choir alone, drums, high voices, thinning with a bending line, a swell | 7/4 at 140 BPM, grouped 3+2+2: drums, stabs, a low dark horn line, a break, rising by half steps | 64 BPM: slow swells, chimes |
| Flood | No key: drones rubbing against each other, slides that never land | a heartbeat that closes in and falls back, a second pulse against it, breath, wet movement, a cloud of strings | 96 BPM, an uneven 3+3+2 pulse, tearing wet hits, growls, thick drones | sparse, breathing |
| Main menu | E minor, its own theme, belonging to no side | 64 BPM: strings and far choir, theme on a horn, strings take it, brass at the top, lets go | | |

**Size.** Files add up to about 120 MB in the repository. Mono is half the size of stereo, and WAVs are what Unity imports from (it compresses them for the build). The main menu, UNSC and Covenant deployment are stereo, because nothing plays over them.
The battle and menu tracks and Flood deployment are mono: 90 s of 16-bit 44.1 kHz stereo is 16 MB. Replacing them with your own files doesn't shrink history, but the WAVs can be deleted from then on.

## Checks

`Tools/Audio/measure.py` over all files:

- all 44.1 kHz, 16-bit, no clipped sample, peaks below -1 dB, no DC offset;
- loudness (ITU BS.1770, the standard way): battle tracks all -18.0 LUFS, deployment all -19.0, menus all -20.0, stingers all -16.0. Effects and UI sounds are levelled by their loudest moment, to the same target between factions;
- loops: the step from the last sample to the first is no bigger than an ordinary step inside the track (at most about 1.4 times the 99th percentile of steps; a click would be many times that);
- no quiet second in any track (all above about -35 dB);
- level over time and spectrograms show the sections actually changing.

Tests: `Assets/Tests/EditMode/AudioTests.cs`, `Assets/Tests/PlayMode/AudioSceneTests.cs`.

**To record what the game outputs:** start it with `-audioCapture out.wav` (with `-autoplay`, for example). It saves the final mix to a file at the end and keeps the room silent while it does.
