# Daily report: 2026-11-02 (overnight), M10

Earlier reports are in this file's Git history. **M10 is built and merged**, and the Infected Pelican now dives. It is waiting for your play test. **Start with `MORNING_DECISIONS.md`.** It lists every choice I made for you.

## After your play test (D54)
- **Your five quick wins:** a smaller Pelican crash (look only), a turning and tilting camera (C resets it), Level all, a Screen setting, and a still menu Scarab.
- **Your roster notes:** every unit now has 3 techs (most had 2), and the two infested Titans are gone, which puts a Flood player at 16 or 17 units, in line with the others.
- **A bug found on the way:** damage techs never reached death bursts, so Volatile Bladder did nothing. Fixed.
- **Testing this time** covered only what these changes touch, as you asked. See the end of this report's Test results.
- Questions are items 47 to 51 of `MORNING_DECISIONS.md`.

## What M10 adds
- **Infected Pelican:** falls out of the sky at a target within 22 m, past other air units, and dies where it lands (D46).
- **Sub-factions:** all 8, with 36 exclusive units on top of the base roster, so 71 units in all, and the real 5-card draft (D52, D54).
- **Terrain:** each side stands on its own faction's ground, with 3 layouts per faction, land out to the horizon, and sky and light following the player (D47).
- **Effects:** a look per weapon kind, impacts, blasts sized to splash, deaths by kind, burning wrecks, a look per spell, the Pelican's crash, now about the size of a Titan's blast, camera shake you can turn off (D48).
- **Sound and music:** a complete system with UI sounds and music per faction and a main menu track of its own (D49). **It is original placeholder audio, not Halo's.** I don't download copyrighted files. Yours replace mine by being dropped in a folder.
- **Main menu, settings, pause:** a dusk battlefield behind the menu, every setting saved, Esc pausing in any phase (D50).
- **Cards:** pictures of each unit, and borders by kind and by faction (D51).

## What isn't done
- **No Halo music or sound is in the game.** See above.
- **19 of the new units are placeholder shapes,** waiting for models, along with 17 older ones.
- **M9's combat identity stage and balance pass** weren't done as stages of their own.

## Balance (first pass)
- **Counter web:** 73 of 73 units had a counter winning at least 90%. It wasn't run again after the two infested Titans were removed, since neither was another unit's best counter.
- **AI against itself,** 216 matches, none unfinished, 6 to 10 rounds on average. Hard beat Normal 17 of 24 and Easy 24 of 24. Normal beat Easy 21 of 24.
- **To watch:** Banished looks strongest and Covenant-based and Noble weakest, on a small sample. Six older units are over 60% average against the bigger field.

## Decisions needing your approval
**D46 to D52** in `DECISIONS.md`, and the list in `MORNING_DECISIONS.md`.

## Test results
- **Unity:** EditMode **650 / 650**. PlayMode **115 passed, 0 failed, 1 skipped** of 116: the menu's hover check, because batch runs draw no buttons (the built game's sound tour covers it). **.NET harness:** **388 / 388**.
- **Built game:** real matches as UNSC, Covenant, Flood, Noble and Banished, the menu demo through every screen (0 problems), the effects and terrain galleries and the card demo, with no exceptions in any log.
- **Independent review:** five reviewers played the merged game, and fixers corrected the 31 problems they confirmed. Among them: wrecks that didn't visibly burn, the camera shake setting doing nothing, battle sound carrying on through Esc, the Sandbox's wrong ground for exclusive units, and the mouse wheel zooming the camera while scrolling a list. Questions left are items 27 to 38.
- **Second round:** the reviewers' lesser notes were fixed, and each area got an independent check. It adds anti-aliasing, a real mushroom cloud for the nuke, smooth shots with rockets, cannons and bombs that look different, lighter Flood pools, soft sounds for moving and turning squads, a Scarab beam that can't get dropped, exclusive units first in the shop, and silent test runs. A scheduled power cut interrupted it, and no work was lost. Questions left are items 39 to 46.
- **Speed:** 192 bodies at 1x average 1.11 ms a frame with 4x anti-aliasing on, 2.9 ms for the slowest 1% of frames (7.7 before the review), and none over 16.7 ms, on your PC. Garbage collections fell from 1 per 115 frames to 1 per 424. The PC's load differed between the measurements, so frame times are indicative. The battle's result line is identical to before, so the simulation didn't change.
- **Not tested:** how anything sounds, since nobody has heard it, and how it moves or feels, since I only see still pictures.
- **Check your settings:** my test runs reset your volumes and UI scale and left the window at 2560 x 1200. See `MORNING_DECISIONS.md`.
- **To play it:** `E:\AI\Halobellum_Game\Halobellum.exe`, or open `Assets/_Project/Scenes/MainMenu.unity` in Unity and press Play.

## Blockers
**Pushing:** a safety check blocked pushing the models, so all of this is only on your PC. See item 2 of `MORNING_DECISIONS.md`.
