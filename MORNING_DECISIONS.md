# Decisions for the owner

Things that came up while you were away. Each says what I did for now, so nothing is blocked except where it says so. M10 is built and merged on your PC. How it plays and sounds is still for you to judge: I only see still pictures, and nobody has heard the sound.

## Needs you

1. **Halo music and sound effects.** You'll download them: `Assets_to_Download.md` lists every track, unit sound and model left, and how to drop them in. None of it is in the game yet, so it still plays original placeholders.
2. **Pushing to GitHub.** Done.
3. **The placeholder sound:** you'll replace it (`Assets_to_Download.md`).

## Sub-factions: first-pass design (you left this open, as with the Flood)

Full tables and reasons are in `Docs/M10/subfactions.md`. Every number is in `balance.csv`. None of the 35 older units' numbers changed.

4. **Noble: "expensive to unlock".** Kept as it is. You'll playtest the sub-factions for balance.
5. **Aerospace Command** has no Tier 1 exclusive: kept.
6. **"Infested", not "Infected":** kept.
7. **Infested Grunts:** kept.
8. **Two extra Flood Titans:** you said no, so they are gone (D54). The Abomination is the Flood's only Titan.
9. **New pure forms:** the Juggernaut and Spawner stay, unless you can't find models for them.
10. **Goliath's base damage is 250:** kept. Base damage is what a surviving body deals to the enemy base at the end of a round, not what its weapons deal. The default is price / squad size / 5, which is 100 for a Goliath. I set 250 from the start, for its job as a building-killer, so it is the only unit off that formula. No balance pass changed it.
11. **Simplified abilities:** the Blisterback and Methane Wagon are removed, so Banished has 4 exclusive units and the Unggoy Rebellion 3. That leaves both without artillery of their own, though they keep the Wraith. You asked me to raise it if this unbalances a sub-faction: I can't tell without measuring, so it's in item 53. The Hunter Captain's taunt, the Cobra's lockdown and the Gremlin's stun stay unbuilt.
12. **Two new simulation mechanics:** a hit that slows and lifesteal: kept.
13. **Balance, for the balance pass:**
    - Hunter Pair, Banshee, Scorpion, Spartan, Hornet and Marine are now over 60% average against the bigger field. I left them alone, because older units' numbers need your approval.
    - In AI-only matches Banished won 69% and Armored Division 62%, while Noble won 33% and Covenant-based 29%. The sample is small and it mixes in how well the AI plays each roster.
    - The Reaver, Chopper, Infested Warthog and Infested Ghost are weak, as flankers are across the game. You suggested doubling flanker squads: see item 52.
    - An older note: the Scarab beat 33 of 34 before the new units.
    - Also over 60% average: Vulture and Goliath 62%, Grizzly, Rhino and Sparrowhawk 60%.

## Models

14. **Models wanted:** see `Assets_to_Download.md`.
15. **Stopgaps:** you'll download a base Spartan with its weapons (`Assets_to_Download.md`). Until then Noble's Spartans are the Marine's body, the base Spartan is a capsule, and the Condor Gunship is a grey Pelican.
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

20. *(You'll replace some ground textures: they are on `Assets_to_Download.md`.)* **Terrain:** each side shows its own faction's ground, and the sky and light follow the player. There are 3 layouts per faction, and the seed picks one for each side. Are the Covenant and Flood floors too busy behind units? That's the first thing to judge in a real game.
21. *(Kept.)* **Wrecks:** a dead vehicle now stays as a burning wreck for 14 seconds of battle time (24 for a Titan), where it used to vanish at once. A reviewer found the fire was hidden inside the hull, so it now starts at the top of the hull and is bigger. Keep it?
22. *(Kept.)* **Camera shake** is on, for big blasts. It can be turned off in Settings or with the K key. Too much?
23. *(Kept.)* **Pause** is on Esc, and there's also a small Menu button at the bottom right. Keep the button?
24. *(Fine as they are.)* **Faction marks on the cards** are plain abstract shapes I made, on purpose nothing like the real insignia. You can replace them with your own (`Assets/_Project/Resources/CardArt/Emblems`).
25. *(Kept.)* **Settings:** quality offers Low and High only (the project's two levels). UI scale runs 75% to 125%.

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
38. **Battle HUD garbage.** Collections fell by about two thirds, to one every 2.6 s at 165 Hz. What's left is inside Unity's text drawing and would take rewriting the battle HUD. I recommend leaving it unless you feel hitches.

## From the second round

A second round fixed what the reviewers had noted as lesser, and each area got an independent check. These are choices I made for you, or questions it left.

39. **Anti-aliasing:** 4x MSAA is now on for the High quality level, which fixes the dashed glowing lines. Its cost is too small to measure on your PC, but it wasn't measured on a weaker card. Keep it? Low stays without it.
40. **The HAVOK Nuke** is rebuilt as a real mushroom cloud on a stem, ending at 8.6 s of battle time. Please watch it, since nobody has seen it move. In stills, the cap's underside at 3 s shows a few round spots, and the dust skirt is patches rather than a ring. The top can run behind the HUD when the nuke lands far up the field. The cap is flattened along the way the camera faces at the blast, so that it doesn't hide its stem. It doesn't turn afterwards, so turning the camera during the blast shows it from another side.
41. **Shop order:** a sub-faction's own units now come first in the shop, because they are the reason to pick that card. Only the order on screen changed. Keep it?
42. **The sounds to judge by ear:**
    - The new soft sounds for selecting, turning, moving a squad and taking a spell back: are they at the right level? The levels are at the top of `UiAudio.cs`.
    - The Spartan Sniper now uses the Gauss sound. Too much like a railgun?
    - The Methane Wagon uses the falling-bomb sound, not one for a mortar.
    - The main menu's hover sound is much quieter than the factions' hovers, so it may be hard to hear over the music.
43. **Sounds left as they are:** almost every landing is the same small explosion, even a Rhino's 150 damage at 4.5 m splash. The Spartan and Brute Grenadiers play the tank cannon's sound. Do you want bigger blasts to sound bigger, or the grenades smaller?
44. **Decimals** follow your PC's language inside the English text, so you see "2,5 s" and "1,8 m/s". Keep it, or always use a point?
45. **Shell holes:** the small near-black holes in no man's land are unchanged on every map, though they are as dark as the Flood pools that I lightened. They are 2 to 6 m across. Lighten them too?
46. **Narrow Sandbox windows:** when a squad window would cover the Items panel, the panel now moves to the right of it while the squad is selected. On the narrowest windows at a high UI scale it then covers part of the unit list and the middle of the Red half, until you close the squad window. Fine?

## From the towers (D55)

52. **Flankers.** I'd play with towers before doubling flanker squads, because towers are the job flankers were missing. If they're still weak, I'd go from 2 bodies to 3 rather than 4: doubling also doubles their damage and health for the same price, and the counter web would need running again. Which do you want?
53. **Balance after this:** nothing has measured the towers, the third techs, the smaller Pelican blast, or Banished and the Unggoy Rebellion without their artillery. The AI-only matches and counter web would measure the last three. Do you want them run, or will you playtest first?
54. **Numbers you left open,** my first pass, all in `Data/MatchRules.asset`:
    - A shockwave takes 30% off speed, damage and rate of fire.
    - A tower level costs a flat 100. I read "doubling the HP and price of 100c every time" as the health doubling at 100 each. If the price doubles too (100, 200, 400, 800), tell me and I'll change how it works.
    - Oil slows ground units of both sides by 40% and adds 50% fire damage, for 15 s.
    - Each Command Center purchase can be made once a round, and a tower has 6 m of ground it blocks.
55. **Rapid Resupply** can be used every round, for a net loss of 100 each time. The AI never uses it, since a rule for when it's worthwhile would be a guess. Fine?
56. **Army bonuses reach further now.** To make research really reach your whole army, buff cards do too: damage now raises death bursts, and health and damage now reach ODSTs from drop pods, a Pelican's Marines and what the Abomination, Spawner and Infected Pelican release. Item damage also raises a death burst. These cards weren't doing that before, so they are a little stronger. Keep it?
57. **The Sandbox has no towers.** Should it? It would let you test towers against any army, but it would change what every Sandbox fight measures unless it's a switch.

## From your play test (D54)

47. **The 55 new third techs** are first-pass, and they are all in `Tools/Data/techs.py` under `THIRD_TECHS`. Each does something the unit's other two don't, and costs about what they cost. No existing tech or unit number changed. Nothing has measured them in battle, because the counter web runs without techs. Tell me which feel wrong.
48. **Damage techs and death bursts:** Volatile Bladder promised "a much bigger explosion" but never changed the Carrier Form's burst. Damage techs now raise death bursts, so that tech gives +40% and the Infected Pelican's new Volatile Cargo +25%. Keep it?
49. **The Pelican's crash** looks half its old size, and its real blast is now 3.5 m in radius, 7 m across (D56). It still deals 900 damage. The fire draws a little past that, because it goes by the size of the aircraft too. The sound didn't change, so it may now sound too big.
50. **The camera:** please judge the turning speed (0.2 degrees per pixel) and direction. The mouse pointer isn't locked during a drag, so a long drag can run into a screen edge or onto another screen, and whether it keeps turning there hasn't been tried. A right-click now acts when the button comes up, and a press of over half a second doesn't cancel anything.
51. **The screen:** until you choose one in Settings, every start goes to Windows's main screen, even if you dragged the window elsewhere. A start that has to change screens shows the old screen for about 3.5 seconds first. Your saved 2560 x 1200 window fits none of your screens, so it should now open at about 1920 x 992 on the Dell. If you drag the window after choosing a screen, the next start goes back to the one you chose.

## From the second batch of models (D57)

58. **Longsword:** a slower, tougher bomber than the Shortsword at the same 300: 600 health, speed 5, 150 per bomb, 5 m splash. It wins 41 of 68 matchups against the roster, in a quick 3-battle run, where the Shortsword won 39. Good, or do you want it more clearly heavier? That would take it over the Bible's 300 limit for tier 2, so it would have to move up to tier 3.
59. **Clips you may want swapped:** Hunters and the Goblin walk with Meshy's Monster Walk, which holds the Hunter's shield out in front, and fire with the rifle clip. The Zealot swings with a one-handed slash, and the Infested Elite attacks with a punch combo. Meshy has about 680 clips, and a new one costs 3 credits.
60. **Noble colours:** Sniper blue, Heavy Weapons red, Grenadier orange, CQB near-black, Fireteam green. They are a single line each in `Tools/Art/recolour.py`.

## Things my test runs did to your PC

- **Windows moving, and possibly music:** while the Screen setting was being checked, a test window moved across all three of your screens and briefly went fullscreen on each. Some of those runs weren't silent, so menu music may have played for about 25 seconds at a time. Those checks have finished.
- **Your game settings:** a fixer's test pressed Reset to defaults in a real run, so volumes and UI scale are at their defaults. Any values you had set are lost, because they weren't recorded first. Camera shake is on.
- **The game's window:** test runs left the game windowed, at 2560 x 1200 today. Change it in Settings if you want fullscreen. From now on my test builds run as "Halobellum Test", with their own settings and window (a registry key and a folder in `AppData\LocalLow\DefaultCompany`, both harmless), and they can't change yours.
- **Real mouse input:** one fixer sent real clicks and wheel notches to test scrolling. Before it added a guard, a few may have landed on another open window. No harm was seen, but it can't be ruled out.

## Still to do from M9

26. **Combat identity (Pass 1) and the balance pass** weren't done as stages of their own. The effects and sounds cover much of Pass 1, by weapon kind. I'd do the balance pass after you've played the sub-factions. The order is yours.

## Small things I noticed and left

- The Cobra's description says it runs on tracks, and your model has six wheels. I left the text.
- Under a shockwave, a leap's speed and a Seeder's seed damage aren't reduced. Infection Forms still cover about 81% of their normal distance rather than 70%.
- An infected body keeps its size but gets a melee reach of 1.2 m, measured from its centre. A big one, such as a seeded Scorpion, may never reach a target that stands still, a tower included. This comes from before the towers, and nobody has seen it happen in play.
- The menu's hover check is skipped in batch test runs, because they draw no buttons there. A sound tour in the built game covers it, using its own pointer. No real mouse has tried it.
- Air units overlap into a blob in big fights.
- The faction asset files are still named `*_Standard.asset`. Only the file name is left over.
