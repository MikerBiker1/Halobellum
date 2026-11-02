# Decisions for the owner

Things that came up while you were away. Each says what I did for now, so nothing is blocked except where it says so. M10 is built and merged on your PC. How it plays and sounds is still for you to judge: I only see still pictures, and nobody has heard the sound.

## Needs you

1. **Halo music and sound effects.** You asked for Halo music, downloaded. I don't download copyrighted game or soundtrack files, so none is in the game. Every slot is ready, though, and the game plays original placeholder music and sound made by a script, so it isn't silent. Drop your files in `Assets/_Project/Art/Imported/Audio/Resources/AudioOverride/`, named after their slot, such as `music_mainmenu.ogg` or `music_unsc_battle.ogg`. No code changes are needed. The slot names are in `Assets/_Project/Art/Imported/Audio/README.txt`.
2. **Pushing to GitHub.** A safety check blocked the push of the models, most likely because it would publish these files. Everything is committed on your PC, and nothing has been pushed. If you want it on GitHub, run `git push` yourself. The repository must stay private. It has also grown by about 120 MB of generated sound.
3. **Listen to the placeholder sound.** Are the three factions' sounds and music good enough to keep until you have your own? Are effects too loud against the music? The mix is all in one file, `AudioCatalog.cs`.

## Sub-factions: first-pass design (you left this open, as with the Flood)

Full tables and reasons are in `Docs/M10/subfactions.md`. Every number is in `balance.csv`. None of the 35 older units' numbers changed.

4. **Noble: "expensive to unlock".** I kept the Bible's price table and made all 5 Spartans Tier 3, so each costs 200 to 250 to unlock. It has no Tier 1 exclusive, so the AI rarely picks it (21 of 432 picks). Should it have something cheap?
5. **Aerospace Command** also has no Tier 1 exclusive, because cheap aircraft seemed wrong. Keep it that way?
6. **"Infested", not "Infected".** The units you buy are "Infested Marine" and so on, because "Infected Marine" is already what the game calls a Marine that rises in battle. Keep the names?
7. **Infested Grunts.** In Halo's lore, Grunts are poor hosts. I kept them because the Bible asks for variants of the Covenant roster. Keep or swap?
8. **Two extra Flood Titans:** Infested Elephant and Infested Scarab, both releasing infested chaff. Keep?
9. **New pure forms:** Juggernaut (UNSC-based) and Spawner (Covenant-based), from Halo Wars 2's logs. Keep?
10. **Goliath's base damage is 250**, against the default of 100, for its job as a building-killer. It is the only exception to the default. Keep?
11. **Simplified abilities:** the Hunter Captain has no taunt, because targeting is always "the closest enemy". The Cobra has no lockdown mode. The Gremlin's EMP slows rather than stuns. The Blisterback is an air unit with no ground mode. The Methane Wagon has no debuff cloud. Want any of these built?
12. **Two new simulation mechanics:** a hit that slows (Gremlin) and lifesteal from a unit's own data (Hunter Captain). Fine?
13. **Balance, for the balance pass:**
    - Hunter Pair, Banshee, Scorpion, Spartan, Hornet and Marine are now over 60% average against the bigger field. I left them alone, because older units' numbers need your approval.
    - In AI-only matches Banished won 69% and Armored Division 62%, while Noble won 33% and Covenant-based 29%. The sample is small and it mixes in how well the AI plays each roster.
    - The Reaver, Chopper, Infested Warthog and Infested Ghost are weak, as flankers are across the game.
    - An older note: the Scarab beat 33 of 34 before the new units.
    - Also over 60% average: Infested Elephant 91% and Infested Scarab 90% (above the real Elephant's 88% at the same 800 credits, with less health, because they release chaff), Vulture and Goliath 62%, Grizzly, Rhino and Sparrowhawk 60%.

## Models

14. **Models wanted** (these units stay placeholder shapes until you find them):
    - New: Cobra, Rhino, Gremlin, Sparrowhawk, Vulture, Shortsword, Marauder, Reaver, Blisterback, Brute Grenadier, Goliath, Skitterer, Hunter Captain, Methane Wagon, Grunt Goblin, Juggernaut, Infested Ghost, Infested Elite, Spawner.
    - Older: Archer Missile Pod, Flamethrower Trooper, Mantis, Spartan, Wolverine, Elite, Zealot, Ghost, Hunter Pair, both Jackals, Combat Form, Seeder, Swarm Form, Abomination, ODST, Vampire.
15. **Stopgaps to confirm:** the Condor Gunship is a grey Pelican. Noble's Spartans are the Marine's body, bigger and darker: "a big soldier", not a Spartan. The base Spartan is still a capsule and now looks worse than they do.
16. **Unused models:** the Mammoth (`vehicleshumangroundmammoth.glb`, 197,535 triangles) has no unit in the Bible. Do you want a unit for it? The energy sword (`halo_3_energy_sword_by_abisv.glb`) would suit melee units later. Both are untouched.
17. **Where the models came from** is your call, since you found them and the project stays private.
18. **From the last batch of models:**
    - The Elephant has no guns: its muzzles are on a crane and the cab, and the crane points backward at rest.
    - The Pelican flies with its landing legs and ramp down.
    - The Grunt holds its pistol at shin height.
    - Infection Forms are larger than life, so that they can be seen.
    - The Marine's body stays at about 5,263 triangles.
19. **Rigs:** the Pure Form Tank's rig was delivered but rated unusable (it's in `Art_Source/Prepared/PureFormTank_Rig`). The Stalker's rig stretches, and its clips weren't bought. Meshy refused the Pure Form Ranged. Try again, or leave them still?

## Things you may want changed

20. **Terrain:** each side shows its own faction's ground, and the sky and light follow the player. There are 3 layouts per faction, and the seed picks one for each side. Are the Covenant and Flood floors too busy behind units? That's the first thing to judge in a real game.
21. **Wrecks:** a dead vehicle now stays as a burning wreck for 14 seconds of battle time (24 for a Titan), where it used to vanish at once. A reviewer found the fire was hidden inside the hull, so it now starts at the top of the hull and is bigger. Keep it?
22. **Camera shake** is on, for big blasts. It can be turned off in Settings or with the K key. Too much?
23. **Pause** is on Esc, and there's also a small Menu button at the bottom right. Keep the button?
24. **Faction marks on the cards** are plain abstract shapes I made, on purpose nothing like the real insignia. You can replace them with your own (`Assets/_Project/Resources/CardArt/Emblems`).
25. **Settings:** quality offers Low and High only (the project's two levels). UI scale runs 75% to 125%.

## From the independent review

Five reviewers played the merged game, and fixers corrected what they confirmed: 31 problems, all fixed and tested. These are the questions that were left.

27. **Floor paint that looks like game markers.** The Quarry's survey crosses look like waypoints, and the Firebase's landing rings and the Sanctum's magenta rings look like spell areas. Nobody has tried a spell outline on top of them. Keep them, or change the shapes?
28. **Massed flamethrowers** make a wall of orange that hides the fight behind it. It clearly reads as flamethrowers. Keep it, or thin the flames when many fire at once?
29. **Flood's scavenged rockets and cannon shells** (Infested Hornet, Scorpion, Elephant) stay orange, as captured UNSC weapons, while other Flood shots are green. Keep, or make them green?
30. **Dust** no longer depends on frame rate. I fixed it at what a 60 Hz screen used to get, so your 165 Hz screen shows less than it did. If it looks thin, it is one number: `DustLookSeconds` in `CombatEffectsView.cs`.
31. **Hunters and Goliaths.** They die as a burning vehicle wreck, with the Covenant soldier's death sound, though they are colonies of worms. Do you want a gore death and a sound of their own?
32. **Sound while paused or away.** Music keeps playing at full level behind the pause menu, and the game keeps running and sounding when you alt-tab. Dim it, or mute it?
33. **Screens.** The frame-rate counter still shows in every battle. Hide it, or put it behind a setting? The draft screen still has its own Sandbox button and AI difficulty row, which the main menu now has too. Keep both?
34. **Menu Scarab:** its panels now stay dark and glow only while the beam fires. Is that the look you want? The numbers are on `MenuBackdrop`.
35. **Sandbox bar:** at UI scale 110% and up on a 16:9 window it sits right of centre to clear the Items panel, so it no longer lines up with the speed controls above it. Fine?
36. **Unit pictures.** The Infested Marine is now an almost solid green soldier, at the battle tint's full strength. Soldiers are shown from the waist up everywhere, even on large cards. Spartan Sniper and Fireteam look the same, as do Grenadier and Heavy Weapons, because they share a body. The Skitterer and Methane Wagon borrow the nearest placeholder shapes. Any of these to change?
37. **Infested vehicles differ only by a green tint.** At the game camera an Infested Scorpion looks much like a Scorpion. Do you want a stronger colour or growths, or wait for models?
38. **Battle HUD garbage.** Collections fell by about 70%, to one every 2.7 s at 165 Hz. What's left is inside Unity's text drawing and would take rewriting the battle HUD. I recommend leaving it unless you feel hitches.

## Things my test runs did to your PC

- **Your game settings:** a fixer's test pressed Reset to defaults in a real run, so volumes and UI scale are at their defaults. Any values you had set are lost, because they weren't recorded first. Camera shake is on.
- **The game's window:** test runs leave the game windowed at 1600 x 900. Change it in Settings if you want fullscreen.
- **Real mouse input:** one fixer sent real clicks and wheel notches to test scrolling. Before it added a guard, a few may have landed on another open window. No harm was seen, but it can't be ruled out.

## Still to do from M9

26. **Combat identity (Pass 1) and the balance pass** weren't done as stages of their own. The effects and sounds cover much of Pass 1, by weapon kind. I'd do the balance pass after you've played the sub-factions. The order is yours.

## Small things I noticed and left

- The selection marker is a solid bright yellow disc, not a ring.
- Air units overlap into a blob in big fights.
- The result line is hidden behind the Sandbox bar.
- At exactly a 5:4 window with 125% UI, about 20 units of the Sandbox bar fall off each side.
- At under 1480 units wide and UI scale 110% or more, a tall squad window can overlap the Sandbox's Items panel.
- The faction asset files are still named `*_Standard.asset`. Only the file name is left over.
