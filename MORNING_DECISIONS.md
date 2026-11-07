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

58. *(Still open: you will playtest it.)* **Longsword:** a slower, tougher bomber than the Shortsword at the same 300: 600 health, speed 5, 150 per bomb, 5 m splash. It wins 41 of 68 matchups against the roster, in a quick 3-battle run, where the Shortsword won 39. Good, or do you want it more clearly heavier? That would take it over the Bible's 300 limit for tier 2, so it would have to move up to tier 3.
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

72. *(Still open: you will playtest it.)* **The Infected Pelican's forms:** it costs 400 again, and keeps the 30 forms you asked for. The AI barely fields it, in 2% of its matches. Good as it is?
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
