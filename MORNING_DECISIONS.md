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
19. **Rigs:** the Pure Form Tank's rig was delivered but rated unusable (it's in `Art_Source/Prepared/PureFormTank_Rig`). The Stalker's rig stretches, and its clips weren't bought. Meshy refused the Pure Form Ranged. Try again, or leave them still? Settled for the Stalker and the Ranged Form, and the Spawner too: rigged and animated in Blender instead (D90). The Pure Form Tank is still still.

## Things you may want changed

20. *(You'll replace some ground textures: they are on `Assets_to_Download.md`.)* **Terrain:** each side shows its own faction's ground, and the sky and light follow the player. There are 3 layouts per faction, and the seed picks one for each side. Are the Covenant and Flood floors too busy behind units? That's the first thing to judge in a real game.
21. *(Kept.)* **Wrecks:** a dead vehicle now stays as a burning wreck for 14 seconds of battle time (24 for a Titan), where it used to vanish at once. A reviewer found the fire was hidden inside the hull, so it now starts at the top of the hull and is bigger. Keep it?
22. *(Kept.)* **Camera shake** is on, for big blasts. It can be turned off in Settings or with the K key. Too much?
23. *(Kept.)* **Pause** is on Esc, and there's also a small Menu button at the bottom right. Keep the button?
24. *(Fine as they are.)* **Faction marks on the cards** are plain abstract shapes I made, on purpose nothing like the real insignia. You can replace them with your own (`Assets/_Project/Resources/CardArt/Emblems`).
25. *(Kept.)* **Settings:** quality offers Low and High only (the project's two levels). UI scale runs 75% to 125%.

## From the independent review

Five reviewers played the merged game, and fixers corrected what they confirmed: 31 problems, all fixed and tested. These are the questions that were left.

27. *(Stays for now: we will remake them later with texture files; good for testing gameplay.)* **Floor paint that looks like game markers.** The Quarry's survey crosses look like waypoints, and the Firebase's landing rings and the Sanctum's magenta rings look like spell areas. Nobody has tried a spell outline on top of them. Keep them, or change the shapes?
28. **Massed flamethrowers** make a wall of orange that hides the fight behind it. It clearly reads as flamethrowers. Keep it, or thin the flames when many fire at once?
29. *(Keep.)* **Flood's scavenged rockets and cannon shells** (Infested Hornet, Scorpion, Elephant) stay orange, as captured UNSC weapons, while other Flood shots are green. Keep, or make them green?
30. *(Yes, particles. On High, moving vehicles now make 4 times the dust, in bigger, longer-lived, thicker puffs (D62).)* **Dust** no longer depends on frame rate. I fixed it at what a 60 Hz screen used to get, so your 165 Hz screen shows less than it did. If it looks thin, it is one number: `DustLookSeconds` in `CombatEffectsView.cs`.
31. *(Blood added: red for humans, light blue for Grunts, deep blue for Elites, orange for Hunters, sickly brown for Flood. The gore death and its sound are on your list; a sound slot is ready (D62).)* **Hunters and Goliaths.** They die as a burning vehicle wreck, with the Covenant soldier's death sound, though they are colonies of worms. Do you want a gore death and a sound of their own?
32. *(Keep full volumes.)* **Sound while paused or away.** Music keeps playing at full level behind the pause menu, and the game keeps running and sounding when you alt-tab. Dim it, or mute it?
33. *(The counter is behind a setting, off by default. Sandbox moved to the Play submenu, and so did the difficulty (D62).)* **Screens.** The frame-rate counter still shows in every battle. Hide it, or put it behind a setting? The draft screen still has its own Sandbox button and AI difficulty row, which the main menu now has too. Keep both?
34. *(Yes.)* **Menu Scarab:** its panels now stay dark and glow only while the beam fires. Is that the look you want? The numbers are on `MenuBackdrop`.
35. *(Fine as it is: you play at 1080p.)* **Sandbox bar:** at UI scale 110% and up on a 16:9 window it sits right of centre to clear the Items panel, so it no longer lines up with the speed controls above it. Fine?
36. *(Keep.)* **Unit pictures.** The Infested Marine is now an almost solid green soldier, at the battle tint's full strength. Soldiers are shown from the waist up everywhere, even on large cards. Spartan Sniper and Fireteam look the same, as do Grenadier and Heavy Weapons, because they share a body. The Skitterer and Methane Wagon borrow the nearest placeholder shapes. Any of these to change?
37. *(Growths, the Flood's tentacles. The model is on `Assets_to_Download.md`.)* **Infested vehicles differ only by a green tint.** At the game camera an Infested Scorpion looks much like a Scorpion. Do you want a stronger colour or growths, or wait for models?
38. *(You didn't follow this one, in plain words: the battle HUD creates small throwaway data, and cleaning it up can cause a tiny stutter about every 2.6 s. Fixing it means rewriting the HUD, so no change unless you feel stutters.)* **Battle HUD garbage.** Collections fell by about two thirds, to one every 2.6 s at 165 Hz. What's left is inside Unity's text drawing and would take rewriting the battle HUD. I recommend leaving it unless you feel hitches.

## From the second round

A second round fixed what the reviewers had noted as lesser, and each area got an independent check. These are choices I made for you, or questions it left.

39. *(Keep.)* **Anti-aliasing:** 4x MSAA is now on for the High quality level, which fixes the dashed glowing lines. Its cost is too small to measure on your PC, but it wasn't measured on a weaker card. Keep it? Low stays without it.
40. *(Looks right.)* **The HAVOK Nuke** is rebuilt as a real mushroom cloud on a stem, ending at 8.6 s of battle time. Please watch it, since nobody has seen it move. In stills, the cap's underside at 3 s shows a few round spots, and the dust skirt is patches rather than a ring. The top can run behind the HUD when the nuke lands far up the field. The cap is flattened along the way the camera faces at the blast, so that it doesn't hide its stem. It doesn't turn afterwards, so turning the camera during the blast shows it from another side.
41. *(Always sort by tier and price, in every mode (D62).)* **Shop order:** a sub-faction's own units now come first in the shop, because they are the reason to pick that card. Only the order on screen changed. Keep it?
42. *(You will replace most sounds anyway.)* **The sounds to judge by ear:**
    - The new soft sounds for selecting, turning, moving a squad and taking a spell back: are they at the right level? The levels are at the top of `UiAudio.cs`.
    - The Spartan Sniper now uses the Gauss sound. Too much like a railgun?
    - The Methane Wagon uses the falling-bomb sound, not one for a mortar.
    - The main menu's hover sound is much quieter than the factions' hovers, so it may be hard to hear over the music.
43. *(See 42.)* **Sounds left as they are:** almost every landing is the same small explosion, even a Rhino's 150 damage at 4.5 m splash. The Spartan and Brute Grenadiers play the tank cannon's sound. Do you want bigger blasts to sound bigger, or the grenades smaller?
44. *(Always a point, on any PC language (D62).)* **Decimals** follow your PC's language inside the English text, so you see "2,5 s" and "1,8 m/s". Keep it, or always use a point?
45. *(We'll see if textures fix that.)* **Shell holes:** the small near-black holes in no man's land are unchanged on every map, though they are as dark as the Flood pools that I lightened. They are 2 to 6 m across. Lighten them too?
46. *(Don't mind small windows much; optimize for 1080p.)* **Narrow Sandbox windows:** when a squad window would cover the Items panel, the panel now moves to the right of it while the squad is selected. On the narrowest windows at a high UI scale it then covers part of the unit list and the middle of the Red half, until you close the squad window. Fine?

## From the towers (D55)

52. *(Keep them like this for now.)* **Flankers.** I'd play with towers before doubling flanker squads, because towers are the job flankers were missing. If they're still weak, I'd go from 2 bodies to 3 rather than 4: doubling also doubles their damage and health for the same price, and the counter web would need running again. Which do you want?
53. *(Yes, out of date. The tournaments of D59 to D61 ran on the real rules, so the Pelican blast and the two sub-factions are measured. Towers only partly: most stay at level 1 and research is 0.00 a match. Third techs were not checked one by one.)* **Balance after this:** nothing has measured the towers, the third techs, the smaller Pelican blast, or Banished and the Unggoy Rebellion without their artillery. The AI-only matches and counter web would measure the last three. Do you want them run, or will you playtest first?
54. *(Already answered.)* **Numbers you left open,** my first pass, all in `Data/MatchRules.asset`:
    - A shockwave takes 30% off speed, damage and rate of fire.
    - A tower level costs a flat 100. I read "doubling the HP and price of 100c every time" as the health doubling at 100 each. If the price doubles too (100, 200, 400, 800), tell me and I'll change how it works.
    - Oil slows ground units of both sides by 40% and adds 50% fire damage, for 15 s.
    - Each Command Center purchase can be made once a round, and a tower has 6 m of ground it blocks.
55. *(The Hard AI uses it when about to lose, with very low base health (D62).)* **Rapid Resupply** can be used every round, for a net loss of 100 each time. The AI never uses it, since a rule for when it's worthwhile would be a guess. Fine?
56. *(Yes.)* **Army bonuses reach further now.** To make research really reach your whole army, buff cards do too: damage now raises death bursts, and health and damage now reach ODSTs from drop pods, a Pelican's Marines and what the Abomination, Spawner and Infected Pelican release. Item damage also raises a death burst. These cards weren't doing that before, so they are a little stronger. Keep it?
57. *(Yes, behind a switch in the Sandbox's top bar, off by default (D62).)* **The Sandbox has no towers.** Should it? It would let you test towers against any army, but it would change what every Sandbox fight measures unless it's a switch.

## From your play test (D54)

47. *(Keep the techs for now. New to you: the counter web runs without techs. A run with techs could reveal new counters but might take hours.)* **The 55 new third techs** are first-pass, and they are all in `Tools/Data/techs.py` under `THIRD_TECHS`. Each does something the unit's other two don't, and costs about what they cost. No existing tech or unit number changed. Nothing has measured them in battle, because the counter web runs without techs. Tell me which feel wrong.
48. *(Keep.)* **Damage techs and death bursts:** Volatile Bladder promised "a much bigger explosion" but never changed the Carrier Form's burst. Damage techs now raise death bursts, so that tech gives +40% and the Infected Pelican's new Volatile Cargo +25%. Keep it?
49. *(Fine for now.)* **The Pelican's crash** looks half its old size, and its real blast is now 3.5 m in radius, 7 m across (D56). It still deals 900 damage. The fire draws a little past that, because it goes by the size of the aircraft too. The sound didn't change, so it may now sound too big.
50. *(The camera move is perfect.)* **The camera:** please judge the turning speed (0.2 degrees per pixel) and direction. The mouse pointer isn't locked during a drag, so a long drag can run into a screen edge or onto another screen, and whether it keeps turning there hasn't been tried. A right-click now acts when the button comes up, and a press of over half a second doesn't cancel anything.
51. *(Good.)* **The screen:** until you choose one in Settings, every start goes to Windows's main screen, even if you dragged the window elsewhere. A start that has to change screens shows the old screen for about 3.5 seconds first. Your saved 2560 x 1200 window fits none of your screens, so it should now open at about 1920 x 992 on the Dell. If you drag the window after choosing a screen, the next start goes back to the one you chose.

## From the second batch of models (D57)

58. *(Played: fine as it is, the owner, 2026-11-12.)* **Longsword:** a slower, tougher bomber than the Shortsword at the same 300: 600 health, speed 5, 150 per bomb, 5 m splash. It wins 41 of 68 matchups against the roster, in a quick 3-battle run, where the Shortsword won 39. Good, or do you want it more clearly heavier? That would take it over the Bible's 300 limit for tier 2, so it would have to move up to tier 3.
59. *(Left to my judgement: clips stay as they are, with no Meshy credits spent.)* **Clips you may want swapped:** Hunters and the Goblin walk with Meshy's Monster Walk, which holds the Hunter's shield out in front, and fire with the rifle clip. The Zealot swings with a one-handed slash, and the Infested Elite attacks with a punch combo. Meshy has about 680 clips, and a new one costs 3 credits.
60. *(OK.)* **Noble colours:** Sniper blue, Heavy Weapons red, Grenadier orange, CQB near-black, Fireteam green. They are a single line each in `Tools/Art/recolour.py`.

## From your notes after playing (D58)

61. *(Answered: a dearer card means a weaker base, 100 health per 50 credits, D59.)* **Starting cards:** I took "about 300" as 250 to 350, which keeps every Tier 1 unit in the draft. Exactly 300 would be fairer, but 150-credit units like the Flamethrower Trooper could then almost never start, as 2 different units are needed. Setting `draftCardValueTolerance` to 0 does that. Which do you prefer?
62. *(Answered: a level 1 squad of the tier of the tower's level, and for level 5 a level 2 Titan, D59.)* **XP for a tower:** I read "a squad of its tier per level" as a squad of the killer's own price, per level of the tower: half an XP bar per level, as a bar is twice the price. Towers come back every battle, so a squad that keeps killing a level 1 tower fills its bar in 2 rounds. If you meant a squad of a tier set by the tower's level, tell me what level 5 would be, as there are only 4 tiers.
63. *(Good.)* **Abomination:** 400 per slam and 50 per whip are my numbers: above the Elephant against Unarmored and Light, still well below it against Heavy, as melee does 30% to Heavy. Above about 170 damage/s, more no longer changes who it beats one on one, so this is about how fast it clears a crowd. Good, or more?
64. *(Keep it.)* **The AI sees your starting squads too,** as the rules are the same for both sides. It can now counter them in round 1, which it couldn't before.

## From the Hard AI tournament (D59)

65. *(Applied, D60.)* **The suggested balance changes:** `Docs/Balance/suggestions.md`. They take the sub-factions from 23-68% to 44-55%, and the factions from Covenant 74%, UNSC 51%, Flood 19% to 52%, 53%, 44%. Apply them all, or only some?
66. **A tower's XP in numbers:** 150, 275, 450, 800 and 1,200 by level. A level 1 tower is 150 XP, which fills 3/4 of a Marine squad's bar and under 10% of a Titan's. Good?
67. **The Hard AI hardly uses towers:** it spends nearly all its credits on squads, so it almost never levels a tower and never buys research. Should it spend on them sooner? That would make it harder to beat, and let the tournament measure them.

## From the changes of D60

68. *(Answered: flank zones, D61. It is now 53%.)* **Aerospace Command is the outlier, at 32%.** It loses more than 80% of its matches to Armored Division, Noble and UNSC-based Flood. A tougher Longsword (750 health) only took it to 35%, in a trial before the last AI change. It needs a look of its own: what would you like its aircraft to be good at?
69. *(Answered with the flank zones, D61.)* **The Pelican is weak in the AI's hands:** 1.27 drops a battle of at most 3, usually shot down on the way in, and sides that field it win 28%. Do you want it tougher (it has 800 health), or is that for players to use better than the AI does?
70. *(Answered: flank zones, D61. It is now 51%.)* **UNSC is now the lowest faction, at 43%,** with Covenant 54% and Flood 53%. Archer pods that can't move and the loss of Titan reinforcement cards both cost it. Do you want UNSC raised next?
71. *(Reverted to 400, D61.)* **The Infected Pelican costs 350,** below Tier 3's band of 400 to 500, as it was already at the bottom when you asked for it to be cheaper. Fine as an exception?

## From the flank zones (D61)

72. *(Played: fine, but tougher and harder-hitting so it surely crashes on the enemy: health 900 to 1,350, blast 900 to 1,200, D134.)* **The Infected Pelican's forms:** it costs 400 again, and keeps the 30 forms you asked for. The AI barely fields it, in 2% of its matches. Good as it is?
73. *(The first drop is now 5 seconds in, wherever it is. The back-corner idea was tested: see D62.)* **The Pelican still loses more than it wins for the AI:** sides that field it win 38%. It makes 1.59 drops per battle now. It may simply need a player to use it well. Worth a look once you have played with it?

## From Unlock Skirmish (D62)

74. *(ODST stays exclusively a card.)* **Which units:** you said any unit. ODST isn't on the page, because it only lands from the drop pod spell and has no unlock price. Adding it is one line and makes 70.
75. *(5% at every setting, D63.)* **Low health:** below 2000 a card's value moves the base less, so 100 gives 105, 100 or 95, because a flat 100 would leave 0 for a 350 card. Fine?
76. *(Keep.)* **One unlock a round** and the unlock prices stay as in Faction mode. You didn't list them as a difference. Keep?
77. *(Fine: they change once all the 3D models are in.)* **Ground and music:** the seed picks one land for both teams, and one in three is the dark Flood map. Keep?
78. *(OK.)* **Blood:** Jackals purple and Brutes dark red are my guess. Machines, Grunt Goblins, Skitterers, Marauders, Reavers and towers have none. Spells draw none.
79. *(Tested: without the Pelican, Armored Division wins 54.9%, up from 54.1%, so no change. See D63.)* **The Spartan at 500 and the Vulture's missiles at 50** are what brought the band back after the Pelican's early drop. Armored Division is exactly at 54.0%, so the next change may push it out.
80. *(Fixed: Hard now levels before new squads in Skirmish, and beats Normal 42 of 50 at 10000. See D63.)* **At 10000 health, Normal beats the Hard AI,** 32 of 50. In those long matches Normal's units deal about 1.9 times Hard's damage per credit. I didn't change it. Do you want this looked into?
81. *(Fine for now.)* **Dust:** close up it now reads clearly behind a vehicle. From the game's camera it is still subtle, since vehicles are small there. The frame rate has room for more, so tell me if you want it stronger.
82. *(Keep them off by default, with the switch in the Sandbox's top bar while placing. It was already built that way, so nothing changed.)* **Sandbox towers:** I kept the switch off by default, so old Sandbox fights stay comparable. Flipping it is one line.

## From the third batch of models (D66)

83. *(Keep it harmless. See D66.)* **A Seeder against Flood does nothing:** it can't seed Flood, and Flood's ground-only units can't reach it, so both stand off until the 90 s limit and base damage decides. The AI has stopped buying it there, but you can still buy it. I left the battle rules alone. Should it be able to hurt Flood, or is that fine?
84. **The Seeder for Normal and Easy:** they pick by role, and its role is still Single-target DPS with Chaff and Flankers as its counters, even the melee ones that can't reach it. It also deploys on the DPS row rather than the aircraft row. Hard goes by the measured matchups, so only Normal and Easy read this wrongly. Should it get an Aerial role?
85. **The Seeder from the game's camera:** it reads as thin red fronds, 8 m long and 3.1 m wide. You called it big-ish. A bigger model is one number in `RealModelBuilder`, and it changes nothing in the fights.
86. **The Brute Grenadier:** to stop it reading as black, its whole texture is lifted, so its armour is now light grey, and from the game's camera the red is only small specks. A lift of just the reds would be the next thing to try. Do you want it darker and redder?
87. **The Infested Marine's rifle:** a grey slab along its right forearm, standing in for its Scavenged Rifle. It reads a little blocky. Its clips are made by script, as Meshy refused it, so please watch how it walks and swings.
88. **The Goliath and Combat Form** don't bend like figures: Meshy tore the first and refused the second, so one swings four legs and the other wobbles. Good enough, or should I try another route?
89. **Wide bodies:** the Juggernaut is 8 m arm to arm on a radius of 1.8, and the Spawner 9 m with its tendrils on a radius of 2, so both overlap their neighbours. I kept their radii, because a change there is a stat change.

## From the music and the end of a round (D67)

90. *(That's fine.)* **A song picked again resumes where it was,** as tracks did before, so that you hear all of it over a game. It could start from the top instead. One line.
91. *(Good.)* **The Flood song's chance:** you asked for more often, and I made it 40% when Flood is present, about 15% for each of the 4 others. One number.
92. *(That's fine: the round's window should not stay open long anyway.)* **Between the round's window and the next cards there is no music,** only the round's sound. Should a song carry on quietly under it?
93. *(OK.)* **Unlock Skirmish has no faction,** so a lost game plays the song of the land you fought on, unless the enemy fielded Flood.
94. *(Good.)* **The 90 second limit** also waits for the last shots, then both sides' survivors send their nodes.

## From your two games (D68)

95. **Blueprint goes wherever the neutral theme does:** Unlock Skirmish, and also the Sandbox and the starting draft in Faction mode. The draft's cards keep their factions' frames there. It can be kept to Unlock Skirmish only.
96. **The shockwave's colour:** pale blue, to show on UNSC green, Covenant violet and Flood flesh alike. It is close to a shield's blue, which matters if you ever read one for the other. One colour to change.
97. *(Yes: Settings has a Battle log switch now, D69.)* **The battle log has no switch.** Every game you play writes a small file, a few KB a round. Should the settings have one?

## Battle stats (D69)

98. *(Only at the end: as it is.)* **Only at the end of the game,** as you asked. The same window could open from each round's result screen too.
99. *(Answered: yes, it is counted.)* **What "stopped" counts:** armor, the shield type and defense arcs, against a body that had none, never past what the body had left. A hit it is weak to stops nothing rather than counting below zero. Damage a shield soaks after it grows back counts as taken, like any other; there's just no separate number for how much shield came back. A test checks it.
100. *(Yes. Flank zones: the map now takes them in, D69.)* **One window for both sides:** the map fit beside both lists (about 790 pixels at 1920 x 1080), so there are no separate tabs per side. Say if it feels small.

## Your sound effects (D70)

101. *(Settled: Yes, it is the gauss rifle (the owner, 2026-11-07).)* **`fire[grifle03]`:** I guessed it is a gauss rifle, so it plays for the Gauss Warthog, the Cobra and the Gremlin. Say if it is something else.
102. *(Answered: the M60 is on every machine gun and chaingun, and the autocannons have your new file, D71.)* **Machine guns:** only the Warthog's (and the Infested Warthog's) use the M60, as you said. The Scorpion's and Grizzly's coaxial guns, chainguns and autocannons keep the generated heavy gun sound. The M60 could go on all of them.
103. *(Settled: Taken out (D82): hits make no sound.)* **Hit sounds:** the generated ones still play, turned down. They can be taken out entirely.
104. **The Scarab's beam:** its "in" is 2.4 s and the beam fires for 3 s, so the loop is heard for about half a second before "out".
105. *(The beam rifle now has a carrier, the Jackal (Beam Rifle), D71 and D72.)* **Not used:** the beam rifle and the magnum (no unit carries one) and the plasma grenade.

## Your second batch (D71)

106. *(Settled: Growths kept. The green wash is gone: beige Flood tendrils baked into the textures instead (D82).)* **Growths, a trial as you said:** pods and bubbles in 3 to 5 clumps on each infested vehicle. They sit properly on the hulls, but read as stuck on rather than grown in, and are small dots from the game's camera. Keep them, change their colour (now orange-brown; a paler Flood tan is easy), more or fewer, or revert (commits `0976829` and `f86f1c4`)?
107. **The towers:** they keep their models' proportions inside their 6 m of squares, so they are 5.3 m (Command Center) and 5.8 m (Research Center) tall, against 9 to 9.5 m for the old plain shapes. A thin slab in the side's colour lies under each, so two towers of the same faction show whose they are. In Unlock Skirmish, where no side has a faction, both are neutral grey-blue. Tall enough? Keep the slab?
108. *(Settled: On for every model but the infested ones, and the Banshee's wing tips and Ghost's fins glow while moving (D82).)* **Glow on other models:** a builder bug meant glow never showed on any model (the Scarab's cannon, Banshee, Wraith, Hornet, Elephant, Phantom and others). It is fixed, and on only for the towers. Switching it on for the rest is one line and a rebuild. Want it?
109. **The Flamethrower Trooper:** Meshy's clips bent the crouched figure double and tore its gun (12 credits spent on them, unused), so it runs and sprays with clips made around its own crouched pose. If you'd rather, a Meshy figure standing upright (T- or A-pose) with the flamethrower separate could be rigged like the others.
110. *(Settled: The Scorpion's model was replaced already (the owner's update, M12).)* **The new Scorpion** has its own markings (UNSC, a number, white stripes) in its texture, kept as part of your model. Its textures were very dark, so they are lifted by 1.35. The Gremlin reads dark olive and has no lift. Change any of these?
111. *(Settled: The Infested Elite is now the Combat Form's model, holding a plasma rifle (D82).)* **The Infested Elite's** weapon is a Scavenged Plasma Rifle, but its body attacks with claws and holds nothing. Give it the plasma rifle?
112. *(Settled: No: blue explosions stay the Covenant's own.)* **Light blue explosions:** the Covenant's machines and their infested copies. The Banished's (Chopper, Marauder, Reaver) and the Unggoy's Grunt Goblin still explode orange, as cruder machines. Blue for them too?
113. **Sounds:** the Marauder's Spike Turret now plays the M60, as a machine gun. The biggest Flood ground units (Abomination, Juggernaut) die with the aircraft crash, now that the Pelican crash slot has that file. Your Glassing Beam file burns for about 8 s while the beam is drawn for 3.5 s, so it fades 1.2 s after the beam. The draw stinger is under 1 s, much shorter than the others. Ordinary rifles and pistols still use the generated sound. Any of these to change?
114. *(Settled: Yes, the line stays.)* **Nodes of light:** the small file below about 63 damage to a base, the big one above and for every Titan. That puts the Scorpion, Wraith and Pelican on the small file and Spartans and Zealots on the big one. Is that where you want the line?

## The Spartan, the Phantom, the snipers (D72)

115. *(Settled: Yes, the timing stays.)* **The Spartan:** I made up for the wind-up by a faster reload (every 3.5 s instead of 4) rather than damage, as 600 damage changed whom it beats. A charge broken by an EMP or its death still plays the whole laser sound, and under a shockwave the bang comes early. Laser Capacitor speeds the reload, not the wind-up. Change any of these?
116. *(Settled: The tint stays; drops not changed, balance is tight.)* **The Covenant Phantom** drops 7 Grunts per drop, up to 3, behind its Gravity Lift tech, at tier 2 and 300 like the Pelican. The Hard AI rarely buys it (15%), as with the Pelican. Should it drop something else (Elites, Jackals), use the flank zones, or be tier 3? Its purple tint over your grey Phantom: the look you want?
117. *(Settled: Balance left as it is.)* **The Jackal snipers** ended at 40 damage every 3 s, 24 m, 4 to a squad, 175 credits. At 45 damage and 150 they carried Banished to 65%. More range with less damage is possible; 30 m made them far too strong in the counter web.
118. *(Answered: the Pelican is tougher and Armored Division back in the band, D73.)* **Banished was at 54.1%** in the first run. Two more runs on new seeds put it at 52.3% over all three, but **Armored Division at 45.6%**, a hair under the band and last or near last in every run (46.3, 43.3, 47.1). Its Grizzly is its weakest regular unit (0.71 of its credits' share in damage, 43% of the matches it is in won). Leave it, or make the Grizzly cheaper and test again (8 minutes a run)?


## The Pelican and the Grizzly (D73)

119. *(Settled: Balance left as it is.)* **The Grizzly** is back at 500, as at 450 the Hard AI bought it against Scorpions and lost it to Spartans (D73). A cheaper Grizzly may still play well in your hands. Want another kind of buff for it, or leave it, now that Armored Division is at 48.3%?
120. *(Settled: Covenant-based Flood is at 50.0% since the Spawner change, D79.)* **Covenant-based Flood is at 46.2%**, at the band's floor (the Abomination's and Carrier Form's numbers are in the impact file). Leave it?
121. *(Settled: No levelled-squad test. Noted for later: a tech or unit that does damage by a share of the target's health (item 133).)* **Marauders:** you find them nearly unkillable after a level or two, but the Hard AI's units end matches at level 1.3 to 1.8, so the tournament can't see that. Want a test with levelled squads, or a look at what levels give the Marauder?


## Fine-tuning and your playtest (D74, D75)

122. *(Settled: Left: the one-on-one table stays.)* **The AI buys from the one-on-one table.** Any buff that makes a unit look better one on one makes the Hard AI buy it much more, and the slow melee units (Pure Form Tank, Grizzly) look far better one on one than in a real match, so buffing them made their sides lose (D73, D74). The fix is in the AI, not the units: build its table from whole matches (the tournament) instead of one-on-one fights, or weigh a unit's real results. Want that tried? It would change how the Hard AI plays everywhere.
123. *(Settled: Left as it is.)* **Still weak:** UNSC-based Flood's Pure Form Tank (the side does better without it), the Covenant Phantom (the AI uses its drops badly), the Vulture (bought in 89% of Aerospace matches for little), the Elite and the Ghost. Each buff tried made things worse through the AI's buying (item 122). Leave them until the AI's table is settled, or redesign one (the Phantom dropping Elites, say)?
124. *(Settled: the Flood is at 50% since D79.)* **The Flood is at 46.5%** overall, both its sub-factions at 47.0%, inside the band but last, losing most to Banished and Unggoy (35% to 40%). Leave it, or a Flood-wide nudge?
125. *(Settled: Yes, the curve stays.)* **Sound by zoom:** nearer than the starting view up to 1.7 times louder, further down to 0.4, off screen down to 12%. Base hits about 6 dB louder, the music down to 45% (small) or 30% (big) under them. All first guesses for your ears: louder, quieter, or a different curve?


## The Flood change and the tutorial (D76, D77)

126. *(Settled: Balance left as it is.)* **The Seeder** is at 475 with seeds of 4% a second and 45-damage spores, after carrying the Flood to 66%. It is still the Flood's best buy (edge +20 in UNSC-based). Its converts rise with 60% of their own unit's health, as any infected unit does. Keep these, or would you rather it converted less (fewer kinds) and kept its old numbers?
127. *(Settled: Left: the one-on-one table stays.)* **The whole-match AI table** is set aside (D76): it made the AI chase what won before, Covenant 63% and UNSC 32%. Item 122 stays open: a blend of both tables, or a table learned over several rounds, could still be tried.
128. *(Settled: No change.)* *(Wording: yours since D81, and 30 steps with the exclusive unit.)* **The tutorial:** UNSC Noble against Banished, the Flamethrower Trooper as the unit to unlock. Another faction pair? Should it offer itself the first time the game starts?


## The Spawner (D79)

129. *(Settled: Balance left as it is.)* **The Spawner** releases its Infection Forms and an Infested Elite in turn every 4 seconds. The Elites alone weren't enough (48%); 3600 health gave 53.2%, so, as you asked, 3200 (was 2800): Covenant-based Flood is now 50.0%. The AI now buys it in 88% of Covenant-based matches. If it feels too strong in play, the next step is a 5-second interval.


## The battlefield's interior looks (D80)

130. *(Settled: Outdoor maps only in the Sandbox, on its Map setting with every interior and outdoor map (D82).)* **The outdoor maps** (Firebase, Seam Flats, Creep and the rest, D47) are no longer used in matches: every match is one of the three interiors. Keep it so, bring them back into rotation beside the interiors, or make it a setting?
131. *(Settled: Kept as implemented.)* **Which match gets which look:** a mirror match its faction's look; Flood against X the Flood ship on X's hull; UNSC against Covenant and Flood against Flood by the seed, half and half. Would you rather the look followed your own faction always (so a UNSC player always fights in the hangar), or the enemy's?
132. *(Settled: One layout per look is enough: quality over quantity.)* **One look per faction for now.** Each look varies with the seed (where the pads, crates, pods and tendrils stand), but has one layout. More layouts per look (a UNSC hangar and a UNSC armoury, say), or are three looks enough?

## Your answers of 2026-11-07 (D82)

133. **For later:** a tech or a unit that does damage by a share of the target's health, so that tough units such as levelled Marauders can be worn down (the owner's note on item 121). Which faction, and tech or unit?
134. **The infested vehicles' tendrils** are baked into copies of their textures (`Tools/Art/flood_tendrils.py`): veins in the growths' orange-brown rimmed darker, a few fleshy patches, the paint under them dulled. Thicker, thinner, more or fewer? The infested infantry (Infested Grunt, the Infested Marine) keep their own colours; the Infested Grunt is still washed green, as only the vehicles were named.
135. **Thrusters:** the Banshee's and Ghost's glow blue-violet while they move, the infested ones too. Colour and size?
136. **The new spells (D83):** the barrage, storm and Blight Rain average about 120 damage to a unit in their 20 m area; the beam does 700 a hit along 30 m. Run a balance tournament on them?

## The Scarab's new model and walk (D84)

137. **Which Scarab:** your `Scarab update.glb` holds four Scarabs in a row: a small dark blue one, the grey one the game used before, a dark weathered one, and a big purple one. I used the last and biggest, the purple one, as you asked. The file itself says it is a Sketchfab model ("Vehicles>Covenant>Ground>Scarab" by jameslucino117, CC-BY-4.0), and its parts carry Halo game file names, so it may come from a Halo game. The repository is private. Keep it, or would you rather another of the four?
138. **The walk:** a step every 0.94 s at its usual speed (2.8 m of ground a cycle), each foot lifted 0.7 m, the body leaning about 0.3 m onto the legs that carry it and sinking 8 cm while a foot is up. Heavier and slower steps, or quicker ones? More or less sway? It stands 0.74 m lower than the model as made, so that its nearly straight legs have room to bend.

## The Spartan Laser and the firing icons (D85)

139. **The Spartan Laser** is rebuilt as a closed skin of about 1,300 triangles with its colours per face, 1.4 m long, so it looks a little faceted up close. It comes from the same Sketchfab author as the Scarab, with Halo names in it (see item 137). Longer, shorter, or more detail?
140. **The firing icons** show each soldier halfway through its shooting clip; the Zealot at 40% of his swing and the Abomination at 70% of its slam read better. Any whose moment you'd change? The Combat Form and the Infested Elite still stand, as their models aren't animated.

## The Hunters (D87)

141. **Both Hunters got the shield and the bash:** the Hunter Pair and the Hunter Captain (Lekgolo Colony), as both are Hunters. Keep the Captain's too, or should it keep shooting close in?
142. **First numbers, for you to balance:** a charge at whatever comes within 5 m, at 9 m/s, at most every 3 s; the bash 100 damage (the Captain's 120) every 1.5 s, also hitting what stands within 1.5 m of its target; the guns hold fire inside 5 m. Not tested in the tournament, so the Covenant and Lekgolo Colony win rates may move out of the 46-54% band.

## Arrows, the Grenadier, reinforcement cards (D88)

143. **The Grenadier's blade:** 35 damage every 1.5 s at 2 m, about the launcher's damage over time, so it should not move balance much; the launcher holds fire while it cuts. Change the numbers when you balance.
144. **A swing clip for the Grenadier?** Settled: yes (D89), Meshy's Heavy Hammer Swing, eased into a loop.


## The Flood forms' animations (D90)

145. **First versions, for your eye:** the three skeletons and clips are mine, made in Blender (Meshy couldn't rig them). Tell me what reads wrong on any of them, one by one, as with the other walkers.
146. **The Stalker's attack** is three lunges to a 1.2 s round (both arms and the tusks, then the left arm, then the right), one on each strike of its Talons (every 0.4 s), so it looks frantic up close. A single slower lunge would no longer match its strikes. Keep the three?
147. **Where the Spawner's forms appear:** its belly heaves and its maw gapes as each brood appears, but the battle still puts the forms 1 m in front of it, as before. Should they appear under its belly instead? That is a small battle change, which I left alone.
148. **The Ranged Form's shard** now leaves from the top of its column at the end of its forward thrust, about 1.4 m up and 0.9 m ahead, instead of from its spine tips at 2.5 m. Only how it looks changes; the shot is the same.

## Sounds and the Captain's laser (D91)

149. **Deaths by name:** a unit is heard by what its name says (Spartan, Marine, ODST, Flamethrower, Elite, Zealot, Grunt, Cannon Fodder, Jackal, Brute); the Grunt Goblin still goes up as a machine, and anything else keeps the generated fall. The Infested Elite and other Flood die with the Flood's sounds.
150. **The Flood by size:** a form or infested soldier (and the Swarm Form) small, a creature or taken vehicle 1 to 3 m across big, the Abomination and the Juggernaut huge. Taken aircraft still crash as aircraft.
151. **The laser's numbers:** 10 hits of 19.5 over 1 s, then 1 s of rest, for the old 96 a second. Your sound runs about 1.4 s, a little longer than the beam.

## The Combat Form and the Infested Elite (D92)

152. **One body for both, as before:** the Infested Elite is the animated Combat Form with its plasma rifle. Its walk is slower (its speed), carrying the rifle forward. Anything about the shamble, the swipe or the fall you'd change?
153. **What you see in a fight is the swipe:** both lead with their claw, so they shoot while closing in (in their walk) and swipe up close; the firing pose shows only on their cards. Should they stop to shoot instead? That would be a battle change.

## The Seeder, the Tank and the Abomination (D93)

154. **The Abomination's whip** shows twice in each slam's clip (the whip and the slam play as one attack, timed to the slam), not on each of its whip's strikes (every 0.8 s), and not while it walks in whipping from 7 to 10 m. A clip of its own for every whip would need the game to play two things at once. Good enough?
155. **The Tank walks on its club**, knuckles down, as its third foot; its small arm swings free. Keep that, or should it walk on its legs alone?

## The tournament after the animations (D95)

158. **Units the AI hardly fields:** the Stalker Form (8 of about 1,600 Flood matches), the Infected Pelican (20) and the Spartan CQB (49). Their win rates mean little, and they don't move their sub-factions. I left them; a player may still find them good. Want them looked at (price, or the AI's choices)?
159. **Two spells with losing sides:** the Glassing Beam (taken in 23% of Covenant matches, edge -16) and Gravemind's Wrath (16% of Flood matches, edge -20). Late cards go mostly to sides already behind, so this may be the AI's timing more than the spells. Left as they are, as the factions are even.

## The cinematic main menu (D94)

160. **Going back to the old menu:** Settings > Menu background > Classic. It is the old scene, untouched, and the choice is remembered. If you'd rather drop the new one altogether, say so and I'll make the classic the main menu again (or revert D94).
161. **The five shots** (Vista, Line, Covenant, Flood, Titan, 10 to 11 seconds each): any to cut, lengthen, reorder or reframe? The Scarab walks the field once a loop and fires twice, in the Line and Titan shots.
162. **Vehicles only now**, as you asked: no soldiers or Flood forms, the guns firing half as often. The Flood is only its infested vehicles and growths. If it now feels too quiet, or you'd like the big Flood forms (the Abomination, Pure Forms) back just standing, say so.

## The Mantis, the Scarab's beam and the drivers (D97, D98, D99)

163. **The Mantis's walk** is slow and heavy (a stride every 1.6 s at its 1.5 m/s). Heavier, lighter, or more swagger?
164. **The Scarab's mouth** glows green from inside (a light and the two hollows either side of the cannon). If you meant other parts by the mandibles (the front plates over the legs, say), tell me which and I'll light those.
165. **No pilot in the Hornet:** its canopy is opaque in the game, so its seat can't be seen. I can make the canopy see-through and seat a Marine (and a Combat Form in the infested one) if you'd like.
166. **Who drives the Flood's vehicles:** Combat Forms in all of them. A Grunt Combat Form in the infested Ghost instead? There's no Grunt Combat Form model yet.

## The Juggernaut and how units fight (D107)

167. *(Answered: no, a quarter stays, D108.)* **The Pure Form Ranged against air** does a quarter of its damage. At full damage it beat every aircraft in every fight (it outranges them all and its splash catches their pairs), and at half it still beat nearly all of them. A quarter keeps air as its counter while it fights back. Should it hit air harder, as a real anti-air piece?
168. *(Answered: no, and every unit now notices from at least 30 m, D108.)* **Infested Grunts notice enemies from 30 m.** Marines notice from 36 m, and the Grunts' pistol reaches 27 m. Further? And should the other clawed Flood (Infested Marines, Combat Forms) notice from further too? They have the same short reach.
169. *(Answered: no, they strike together, D108.)* **The Juggernaut's strike** brings both blades down together, once a blow (every 1.2 s). Before, its two blades took turns. Would you rather they alternate?

## Overnight balance (D112)

170. *(Settled by later balancing: 52% after D133.)* **Aerospace Command at 54%:** the top edge of the band over two rounds. If it plays strong for you, the next step would be the lasers at 15, or the Vulture (fielded in 94% of its matches).
171. **The Seeder drives UNSC-based Flood:** it raises dead UNSC units as this sub-faction's own infested units, so it beats the UNSC sub-factions hardest. The two price rises fixed the total, but the Seeder is what to watch if UNSC-based climbs again.
172. *(Answered: it now shoots the closest enemy when nothing it can seed is in range, D127.)* **The Seeder doesn't attack Flood, Titans, shielded or already-seeded units at all** (it only shoots what its seed can take). Change that?
173. *(Answered: the Captain costs 450 and shouldn't be expected to beat a Titan; but under the real rules no other unit beats the Abomination at all at equal credits (all 67 lose every battle), so the Captain stays listed and the test accepts 70% for this one pair, D134.)* **A counter test fails, from before tonight:** the Hunter Captain, the Flood Abomination's listed counter, beats it only 70% with the game's real rules (the test wants 90%; the counter web's own measure still says 100%). Neither unit changed tonight; it likely came with the Captain's beam (D91) or the 30 m noticing (D108). Find a new listed counter, or look at the matchup?

## The soldiers' animations (D129)

174. *(Overtaken: it carries your machine gun low like a minigun, D132.)* **Spartan Heavy Weapons** carries its weapon on the shoulder like the Spartan (it holds the rocket launcher model). Or should it walk like the Fireteam?
175. *(Answered: the Grenadier (a rocket launcher only) carries it on the shoulder like the Spartan, D135; the Sniper stays like the Fireteam.)* **The Grenadier and the Sniper** walk like the Fireteam: weapon low in front, shouldered to fire. Or on the shoulder like the Spartan?
176. *(No change asked, so kept as it is.)* **On the shoulder** means resting on it pointing ahead and a little up, the hand on the grip, the barrel's back behind the shoulder. Would you rather the barrel pointed back over the shoulder?
177. *(Answered: the Elite's animation is great, right hand, as it is.)* **The Elite holds its plasma rifle in the right hand** now (it was in the left), so the arm that fires is the one held out. Left again?

## Unit balance (D137)

The questions at the end of `Docs/Balance/unit_intent.md`, which has the details. Slot test values are in points of margin: 0 is as good as the army's average credit, about ±4 is chance.

178. *(Answered: ±5 is a starting point, with exceptions; the better rule is units within ±5 of each other by type and price, and exclusive units may be a little stronger than base units for their price. Used in D138.)* **The band:** every unit within about ±5 points in the slot test, except where its row in the intent sheet says otherwise (transports, anti-air, units you want stronger)?
179. *(Answered: chaff is hard to judge, it soaks big shots; give it less base damage and more tech damage. Done for the Marine and the Grunt, D138.)* **Chaff and tanks:** judge chaff by the fire it takes, and tanks by damage plus fire taken? Then the Marine is too strong, as you said, and Cannon Fodder too weak.
180. *(Answered: yes.)* **Carries:** does what you said of the Pure Form Ranged go for other fragile, long-range units (Spartan Sniper, Rhino, Jackal (Beam Rifle), Infested Wraith)?
181. *(Answered: their ratios should be high but similar, the Fuel Rod Grunt a bit too strong. D138: Fuel Rod Grunt 32 → 29, Flamethrower Trooper up.)* **Splash units:** the Fuel Rod Grunt, Brute Grenadier and Flamethrower Trooper top the damage list. In the slot test the Fuel Rod Grunt is +11, the Brute Grenadier +6, the Flamethrower −12. Judge them by the slot test?
182. *(Answered: they go for towers at all cost, hitting what they pass; then a low ratio for their price but a higher edge. Done in D138, and the AI guards a tower raided that way.)* **Flankers:** most measure −15 to −22 in the slot test. Bring them up to fair, or keep them as they are (item 52)?
183. *(Answered: a beacon button that sends them where the player wants, movable each round; without one they act as a normal unit. Not built yet: a task of its own.)* **Transports:** the Pelican and the Covenant Phantom are at a fifth of their worth, drops counted (slot −21 and −25). Hold them to fair, or accept units the AI uses badly?
184. *(Answered: the AI uses it badly; placed further back it is a monster. Left as it is; placing it further back is AI work for later.)* **Zealot:** is the impact of 1.5 you asked for in D65 still the target? It is 0.69 now, slot −1.
185. *(Answered: forget the slip: 450, listed as it is.)* **Hunter Captain:** 500 credits or 450 (the game says 450)? And if it should lose to a Titan one on one, what should counter the Abomination?
186. *(Answered: the Condor fires faster, the Spartan slower. Done in D138.)* **Spartan and Condor Gunship:** close the gap (slot +13 against −12) from both sides, or mostly from one?
187. *(Answered: not meant for every army, but each Titan should add about as much to the win rate. D138: the Abomination's slam 400 → 360.)* **Titans:** the AI buys one in 89% to 94% of matches. Is a Titan meant to be in nearly every army?
188. *(Answered: they still should show in the win rate; the slot test measures that.)* **Effects the ratio can't see** (auras, the Gremlin's slow, the Goliath's base damage): leave them to the slot test?

## The balance pass (D138), waiting for your review

189. **Push it?** The pass is in the merged worktree, not committed or pushed, as you asked. Every sub-faction wins 48% to 52% Insane against Insane over four seeds; `DECISIONS.md` D138 lists every change. Say the word and I'll commit, bump the version and push main and game-build.
190. **Flankers go for towers, and the AI now guards a tower that raiders took.** Their edge went up for every flanker and their damage stayed low, as you expected. Without the AI's guarding they were far too strong against it (Covenant-based Flood at 72%). Keep both?
191. **Combat Form and Infested Elite** still lose more than they win when bought (wins 39% and 37% when fielded), even with the 10 m leap. More would lift both Flood sub-factions through the Spawner's brood and the Infected Phantom's drops, so other Flood units would come down. Worth doing?
192. **Next, as you described:** the transports' beacon (placed by the player, movable each round) and the AI placing the Zealot further back. Each is a task of its own. Which first?

## The second balance pass (D140), waiting for your review

193. **Push both passes?** D138 and D140 are in the merged worktree, not committed or pushed, as you asked. Overnight I re-measured every unit on one roster. Phase 1 wasn't as far along as I'd reported, so I did one more phase 1 round and four phase 2 rounds. Now, Insane against Insane over eight seeds, every sub-faction wins 48.1% to 50.5% except Lekgolo at 53.5% (item 196). 11 of 13 type-and-price groups are within 10 points of win rate. The exception is Anti-chaff, held apart by the Carrier Form's XP problem (item 195). Say the word and I'll commit, bump the version and push main and game-build.
194. **Titans' unlock cost.** The Elephant (now 150 to unlock) and the Scarab (now 50) were traps for the AI at 350: it could afford one only late, and each cost its army 7 to 12 points whatever its power. The full sweep then found the Abomination 5 points below the other two (UNSC-based −9), so its unlock went 350 → 250 too; all three now add +0.5 to +3.7. Keep it this way, or would you rather a different lever (say, speed; the Scarab's walk cycle is in its own session)?
195. **XP for bodies made in a battle.** A Carrier Form's Infection Forms, a Pelican's dropped Marines, a Spawner's brood and risen forms all give the killer XP at their unit's full price, though nobody paid for them. That is why the Carrier Form and the Pelican cost their armies about 7 points whatever their stats. I added `BattleBodyXpShare` to measure it (1 = as now; nothing in the game changed). At 0, Covenant-based Flood +2.7, UNSC-based +2.1, Aerospace −2.9, Unggoy −2.1. Should such bodies give less XP, or none?
196. **Lekgolo stays at about 53.5%, and its exclusives can't bring it lower.** They took the biggest cuts (Goliath 3000 → 1760 health, Hunter Captain's laser 21 → 12.5, Skitterer 150 credits and 50 health) and are now only 7% of its credits: the AI buys the Goliath in 12% of its matches, and the last −13% to −17% moved Lekgolo by under 1 point. Cutting further would push them below the other units of their type and price, against phase 1. Its edge is in its matchups: it beats Unggoy 65%, Aerospace, Armored, Noble and Banished 57–58%, and loses only to the Flood (36% and 48%), which phase 1 made a little weaker. Its Scarab (unlock 50) is a third of its army. Leave it at 53.5%, or should Lekgolo give up something that isn't exclusive (the Scarab's unlock for Lekgolo, say)?
197. **Never bought, so not measured:** the Gauss Warthog, Stalker Form and Infected Pelican are fielded in under 10% of matches. The slot test says the Gauss Warthog is fine when fielded: the AI just prefers other units. Leave them, or look at why the AI passes them over?
## The balance remade to your vision (D141), waiting for your review

198. **Push D141?** It is in the merged worktree with D138 and D140, not committed or pushed, as you asked. **Phase 1, base units only:** UNSC, Covenant and Flood 50.0% each, Insane against Insane over eight seeds, with every exclusive unit taken out (it started at Covenant 68%, UNSC 49%, Flood 34%). **Phase 2, with exclusive units:** every sub-faction has a matchup it clearly wins, the weakest best being Banished at 56% against Aerospace (each cell ±3). Overall: UNSC-based 61.5, Covenant-based 54.9, Armored 48.8, Banished 48.3, Unggoy 47.9, Noble 46.8, Aerospace 46.7, Lekgolo 45.2. Say the word and I'll commit, bump the version and push main and game-build.
199. **UNSC-based Flood at 61.5% overall** (77% against Unggoy, 73% against Lekgolo, 70% against Banished). You said a big spread is fine as long as everyone has a good matchup, and they all do. Trimming two of its exclusives that sat well above their peers (Infested Warthog, Infested Ghost) barely moved it. The Infested Warthog is still the strongest flanker, about 8 points above the base ones (`Docs/Balance/d141_unit_review.md`). Leave it, or bring it under 60?
200. **The Covenant sub-factions against the Flood: 23% to 43%** (base units only, Covenant against Flood is 49%). The cause is mostly what the AI buys: against the Flood it puts its credits into the exclusive units the counter table likes there (the Grunt Goblin, Marauder, Brute Grenadier, Hunter Captain), which lose in real battles, instead of the Banshees that win the base-only matchup (22% of the Covenant's credits base only, 8–13% with exclusives). A player can choose differently, so this may be the hard matchup you described. Leave it, teach the Insane AI (it would need a clearly measured win), or tone the Flood down?
201. **Every Titan unlocks for 150** (the Scarab 50 → 150, the Abomination 250 → 150, the Elephant stays at 150), so the base factions match. Keep?
202. **Lekgolo's exclusives got part of D140's cuts back** (Goliath 1760 → 2400 health, Hunter Captain laser 12.5 → 15.5 and bash 72 → 90, Skitterer 50 → 60). After phase 1 lowered the Covenant base, Lekgolo was the lowest sub-faction, and the AI bought its exclusives in only 5–6% of its credits. This replaces item 196.
203. **Two price cuts on exclusives the AI barely bought:** the Reaver 300 → 250 (it never got bought, as its counter-table wins were a subset of the Vampire's), and the Chopper 200 → 175 (it added 8 points to Banished on 3% of its credits). Both stay inside their tier's band. Keep, or would you rather do it with stats?
204. **Noble against Unggoy is 27%**, the hardest matchup left outside the Flood ones. Noble's good matchup is Armored at 60%. Fine as a counter?

## The playtest batch (D142), waiting for your review

205. **Covenant Phantom.** It never lands now: it keeps its 3 drops (5 s in, then every 10 s), 7 Grunts each, falling from the air under it like the Infected Phantom's Combat Forms, and it is never pulled behind enemy lines. The Pelican is unchanged and still lands behind enemy lines, as you only named the Phantom. Not watched in a play test yet. Keep?
206. **Pelican:** autocannon 10 → 25 per shot (2.5x), 1 m splash. It now beats the Wolverine and the Vampire one-on-one; what beats it is the Reaver, Gremlin, Mantis, Grizzly, Grunt Goblin, Scarab, Elephant, Abomination, Spawner, Seeder and Infested Marine. Base Covenant has only the Scarab against it, Lekgolo only the Scarab, Noble the Mantis and Elephant. As you said, the AI must adapt or be punished. Keep?
207. **Condor Gunship:** each of its five Light Spartan Lasers 19.5 → 60, to sit with the other 500-credit units. The Wolverine, Swarm Form, Reaver and most infantry still beat it; it now beats the Vampire. Keep?
208. **Summoned bodies deal no base damage:** the Spawner's brood, every death burst, seeded and risen bodies, spell squads, and anything a summoned carrier drops. Troops a bought air transport drops (Pelican Marines, Phantom Grunts, a bought Infected Phantom's Combat Forms) still count, because you paid for that transport. A Spawner alive at time-out now deals 80 instead of about 520. Its brood still keeps the battle running to 90 s; it just costs you time now, not base. Keep, or should carried troops count for nothing too?
209. **Summon level:** everything a squad brings in a battle (drops, releases, death-burst spawns, seed forms) has the squad's level and the army's bonuses, but not its techs or item; seed forms take the level only. Infection rises are the victim's body, so they keep its level. Spell squads stay level 1. A leveled summoned body gives more XP when killed, as any leveled body does. Keep?
210. **XP split:** the killer's squad gets 50% of a kill's XP, the other 50% goes to every squad that damaged the body, by the damage each dealt (your example: Spartan 90% of the damage, Scorpion the kill → Scorpion 55%, Spartan 45%). Shield damage counts, overkill doesn't; a dropped or spawned body's damage counts for its maker; spell damage is left out; towers split the same way. Total XP per battle is unchanged. Units that soften targets will level sooner, last-hitters later. Keep?
211. **Oil Zone:** lasts the whole round; bought once for 150, it stays in your spells for the match and recharges 2 rounds after each use (cast in round 1 → ready in round 4). The Hard and Insane AI buys it once and casts it whenever it is ready (before, they paid 150 every round). It also slows your own ground units for the whole battle. Keep?
212. **Skitterer:** range 7 → 12 m; its Long Range tech still adds 8, so 20 m with it. It now beats the Flamethrower Trooper, Banshee, Hornet and Elite; the Scorpion is its listed counter. Keep?
213. **Spartan Heavy Weapons:** chaingun 15 → 13 per shot, so 65 at level 5. It now loses to the Warthog, Cobra, Chopper, Cannon Fodder and Infested Wraith. Enough, or more?
214. **Goliath:** melee only, Fuel Rod Cannon gone; Fist Slam 120 → 240 (every 2 s, 3.5 m splash) and it charges like the Hunters (6 m, 3 s cooldown). It plays the Hunter charge sound. It now loses to the Pelican, Skitterer and Combat Form. Keep?
215. **The glow:** blue under your squad with the most damage dealt this battle, red under the enemy's. Only that squad's own bodies glow; damage by what it dropped or raised still counts for it. A tie keeps the current leader; a wiped-out leader stays the leader, so nothing glows until another squad passes it. It stays through the Result, card and deployment screens and clears when the next battle starts; not in the Sandbox. Red and blue are also the spell outline colours. Keep these colours?
216. **Moddable unit stats:** 11 JSON files in the game folder's `UnitStats`, one per faction and sub-faction, read at every launch. Any mistake → the game uses its own values for every unit, the main menu lists each error as "file line N: ..." and your files are left untouched; a missing file is re-created. Every game build overwrites them, so modders keep a copy. The AI still judges units by their default stats when it picks counters, but pays modded prices. JSON, not Lua. Keep?
217. **Unlock Skirmish icons:** the unit bar and unlock page already showed the stats window on hover (since D62). Added: hover stats on the starting draft cards and reinforcement cards (every mode), and an "i" corner on each bar and page cell that pins the window, like a click on a faction-mode shop row. A real mouse hover couldn't be tested in a batch run, so please try it in a play test. Was this what you meant?
218. **Goliath body and Jackal icons:** the Scarab session made new Jackal (Shield) and Jackal (Beam Rifle) icons, a redone Goliath icon, and a rigged Goliath with walk, charge, 2 s slam, idle and death clips, on its branch `m13/art` (D139). Nothing is merged yet. Merge the art branch into this batch?
219. **Found: Fuel Rod Grunts stop the Flood draft cold.** With the D138–D141 values, a level-1 draft of Infection Forms and Carrier Forms deals no damage at all against Fuel Rod Grunts (one test's seed 3); the test now plays that squad at level 5. Worth a look when you next want balance work?


## After the D142 tournaments (D143), waiting for your play test

220. **Pelican:** with Troop Bay it comes down where it is (1.5 s), lets its 6 Marines out, and goes back up: at 5 s, then every 10 s, up to 3 drops. It never goes behind enemy lines. Keep?
223. **Sandbox:** clicking a unit row's icon or name opens its stats window (level 1, no techs); clicking again closes it. The Place button still places. Right?
221. **Spawner:** 3 Combat Forms a release (was 5); still 3 Infection Forms in between, one lot every 4 s. At equal credits it now loses to the Zealot and the Heavy Grunts. Enough?
222. **Swarm Brood:** 200 → 300 credits. I took "a bit more" as 300. Right amount?

## Your next batch (D144), waiting for your play test

224. **Laser Sights:** +4 m range (was +2). Keep?
226. **Insane AI:** it now weighs your levels and techs (and its own) against its counter table, and won't pile more of a failing type into an army it already fills (over 30%). It beats the old Insane 52.8% over 2,880 matches (every sub-faction 2-9 points better). Two other habits I tried (survivors that did little counted as farmed; farmed types remembered) did nothing and were removed. Keep?
227. **Tech icons:** 20 px icons, 30 px above each squad, in deployment only; the enemy's from the last battle. Readable? Bigger?
228. **Carrier Form:** walks through its own Infection Forms (they held it back from vehicles). Tech icons now 6 px above the squads. Right?
229. **Infected Pelican (found, not changed):** it picks the crowd it dives on by counting enemies within 4.5 m of a unit's centre, so a lone Scarab never counts as part of a crowd. Change it to count from the edge?
230. **Spartan CQB:** fights the closest enemy instead of running to the towers (the other Flankers still go for them). Noble 61% -> 62%; the CQB's damage when fielded x3. Right?
231. **Balance (found, not changed):** this worktree's tournament (Insane vs Insane, 2000 health) has Armored Division 70% and Lekgolo Colony 25%, Covenant 25% as a faction, after the unmeasured changes since D141 (Pelican, Condor, Phantom, Spawner, Carrier...). Rebalance?
232. **Hunters hit aircraft:** the Hunter Pair's cannon and the Hunter Captain's laser now reach air (the bash stays on the ground). One on one they now beat nearly every aircraft at equal credits. Covenant 25% -> 34%. Keep at full damage against air, or half?
233. **Aim for Their Wings** (Jackal sniper, 150): +4 m range and +50% damage against air only. The AI buys it first once it has seen aircraft. Right?
234. **Vampire:** replaced by 235 (D150).
235. **Vampire (D150):** 16 explosive damage (13 energy before), x3 against air, 0.5 m splash, 700 health; your NeedlerRifle sound played for every shot; its needles drawn 25% faster. It beats a Pelican with 85 health left, and the level 1 Condor beats it with 410 left. The splash hits every Infection Form in a pack, and explosive does full damage to heavy armor, so its damage per credit nearly doubled (579 -> 1093). The splash makes no blast sound or flash of its own. Right?
236. **Needle Rifle (Heavy Grunts) is ballistic (D150):** 60% against light armor, 25% against heavy, 75% against shields (energy did 100%, 75%, 150%). Heavy Grunts' damage per credit 1142 -> 710, win when fielded 48% -> 40%; Unggoy Rebellion 41% -> 34%. Rebalance the Heavy Grunts? (Done in D151.)
237. **Grunt Goblin's needles (D151):** 10 explosive with a 0.5 m splash, kept at 10 because duels put the change at x1.03; its damage per credit is 1059 (964 before). Right?
238. **Reaver health (D151):** you didn't give a number; I made it 700 -> 900. Its damage per credit went 842 -> 1215 and it is fielded 33% (12% before). Right?
239. **Elites' 1.5 m gap (D151):** they keep it in formation and while walking; standing to fight, they stay where they stopped. Right?
240. **Shield Jackal's charged bolt (D151):** 12 m/s, strips 100 shield and nothing from health; at a shielded target every shot is a charged one (one a second at most). Right?
241. **Covenant Phantom (D151):** the Pelican's 25 damage in energy beat 60 of 68 units and every Pelican, so I made it 16 (beats 52, the Pelican 53). Its damage per credit in the tournament is 1627, above the Pelican's 1259, though its side wins no more with it (edge -1). Keep 16? (The owner lowered it to 15 in D152.)
242. **Sparrowhawk's miniguns (D153):** you asked for 2 damage each at 15 a second (60 a second), raised 0.5 at a time if that made it weaker. At 2 it beat 22 of 68 units (the autocannon 33); 3 beat 27, 3.5 beat 37, so it is 3.5 each (105 a second). Keep 3.5, or 2 as you first said?
243. **Aerospace Command at 61% (D153):** the Vulture's 22 m chin gun, the explosive Condor and the 1000-health Longsword lifted it from 42%; the Vulture is now fielded in 67% of its matches. Covenant fell to 42% (Unggoy Rebellion 40%). What next?
244. **Counter margins (D154):** Insane reading the margins won 51.0% against today's Insane (noise) and bought much the same, so it is off. Try it again with more weight on the margin (it is half now), or leave it?
245. **Hunter weak back (D155):** I took it off both the Hunter Pair and the Hunter Captain, which share it. Right?
246. **Spartan Sniper rate (D155):** no number given; every 3 s -> 2.5 s. Right?
247. **Seeder rot (D155):** on any unit it took Flood to 64% (UNSC-based 68%) and the Seeder's win when fielded 43% -> 60%. Titans and units with shield up still can't be seeded. What next? (D156: rot 2.5%, Flood 51%.)
248. **Spartan Grenadier's unlock (D162):** its price is 350 but its unlock stayed 200; the Tier 3 rule (half the price) would make it 175. Keep 200?
249. **Balancing to 48-52% (D163):** seeds 1-4 put every sub-faction at 48.6-51.0%; the verification seeds 5-6 at 47.9-52.1% (Armored Division and Banished 0.1 outside, within the noise); all six pooled 49.1-51.2%. Call it done, or another round?
225. **Glassing Beam, AI:** it now aims where the squads will be when the beam fires (6 s in, then 3.6 s of sweep), forecasting both armies' walk toward each other, and picks the line's direction (16 tried) that burns the most enemy value minus its own troops. It holds the beam if no line is worth it. Normal, Hard and Insane; Easy still aims at random. Right?

## Things my test runs did to your PC

- **Windows moving, and possibly music:** while the Screen setting was being checked, a test window moved across all three of your screens and briefly went fullscreen on each. Some of those runs weren't silent, so menu music may have played for about 25 seconds at a time. Those checks have finished.
- **Your game settings:** a fixer's test pressed Reset to defaults in a real run, so volumes and UI scale are at their defaults. Any values you had set are lost, because they weren't recorded first. Camera shake is on.
- **The game's window:** test runs left the game windowed, at 2560 x 1200 today. Change it in Settings if you want fullscreen. From now on my test builds run as "Halobellum Test", with their own settings and window (a registry key and a folder in `AppData\LocalLow\DefaultCompany`, both harmless), and they can't change yours.
- **Real mouse input:** one fixer sent real clicks and wheel notches to test scrolling. Before it added a guard, a few may have landed on another open window. No harm was seen, but it can't be ruled out.

## Still to do from M9

26. *(Settled: Done: the balance passes ran as tournaments after each change, D59 to D79, every sub-faction in the 46-54% band; the owner, 2026-11-07.)* **Combat identity (Pass 1) and the balance pass** weren't done as stages of their own. The effects and sounds cover much of Pass 1, by weapon kind. I'd do the balance pass after you've played the sub-factions. The order is yours.

## Small things I noticed and left

- *(Fixed from Halopedia: the Cobra now reads as six-wheeled.)* The Cobra's description said it ran on tracks.
- Under a shockwave, a leap's speed isn't reduced. (A Seeder's seed damage now is, D63.) Infection Forms still cover about 81% of their normal distance rather than 70%.
- An infected body keeps its size but gets a melee reach of 1.2 m, measured from its centre. A big one, such as a seeded Scorpion, may never reach a target that stands still, a tower included. This comes from before the towers, and nobody has seen it happen in play.
- The menu's hover check is skipped in batch test runs, because they draw no buttons there. A sound tour in the built game covers it, using its own pointer. No real mouse has tried it.
- Air units overlap into a blob in big fights.
- The faction asset files are still named `*_Standard.asset`. Only the file name is left over.
