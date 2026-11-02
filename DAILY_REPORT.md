# Daily report: 2026-11-02 (overnight), M10

Earlier reports are in this file's Git history. **M10 is built and merged**, and the Infected Pelican now dives. It is waiting for your play test. **Start with `MORNING_DECISIONS.md`.** It lists every choice I made for you.

## What M10 adds
- **Infected Pelican:** falls out of the sky at a target within 22 m, past other air units, and dies where it lands (D46).
- **Sub-factions:** all 8, with 38 exclusive units on top of the base roster, so 73 units in all, and the real 5-card draft (D52).
- **Terrain:** each side stands on its own faction's ground, with 3 layouts per faction, land out to the horizon, and sky and light following the player (D47).
- **Effects:** a look per weapon kind, impacts, blasts sized to splash, deaths by kind, burning wrecks, a look per spell, the Pelican's crash as the biggest thing a unit can do, camera shake you can turn off (D48).
- **Sound and music:** a complete system with UI sounds and music per faction and a main menu track of its own (D49). **It is original placeholder audio, not Halo's.** I don't download copyrighted files. Yours replace mine by being dropped in a folder.
- **Main menu, settings, pause:** a dusk battlefield behind the menu, every setting saved, Esc pausing in any phase (D50).
- **Cards:** pictures of each unit, and borders by kind and by faction (D51).

## What isn't done
- **No Halo music or sound is in the game.** See above.
- **19 of the new units are placeholder shapes,** waiting for models, along with 17 older ones.
- **M9's combat identity stage and balance pass** weren't done as stages of their own.

## Balance (first pass)
- **Counter web:** 73 of 73 units have a counter winning at least 90%.
- **AI against itself,** 216 matches, none unfinished, 6 to 10 rounds on average. Hard beat Normal 17 of 24 and Easy 24 of 24. Normal beat Easy 21 of 24.
- **To watch:** Banished looks strongest and Covenant-based and Noble weakest, on a small sample. Six older units are over 60% average against the bigger field.

## Decisions needing your approval
**D46 to D52** in `DECISIONS.md`, and the list in `MORNING_DECISIONS.md`.

## Test results
- **Unity:** EditMode **535 / 535**, PlayMode **70 / 70**. **.NET harness:** **382 / 382**.
- **Built game:** real matches as UNSC, Covenant, Flood, Noble and Banished, the menu demo through every screen (0 problems), the effects and terrain galleries and the card demo, with no exceptions in any log.
- **Speed:** 192 bodies at 1x average 1.5 ms a frame with effects on, 7.7 ms for the slowest 1% of frames, on your PC.
- **Not tested:** how anything sounds, since nobody has heard it, and how it moves or feels, since I only see still pictures.

## Blockers
**Pushing:** a safety check blocked pushing the models, so all of this is only on your PC. See item 2 of `MORNING_DECISIONS.md`.
