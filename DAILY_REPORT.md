# Daily report: 2026-11-02 (overnight), M10

Earlier reports are in this file's Git history. **M10 is built and merged**, and the Infected Pelican now dives. It is waiting for your play test. **Start with `MORNING_DECISIONS.md`.** It lists every choice I made for you.

## The Spawner and the Infested Hornet (D79)
- **In the game:** the Spawner releases 12 Infection Forms and an Infested Elite in turn, one every 4 seconds, and has 3200 health; the Infested Hornet hits a little softer.
- **Tournaments:** every sub-faction 48.9% to 52.7% over three runs; Covenant-based Flood 47.7% to 50.0%, UNSC-based 53.9% to 50.1%.

## Your round start sound (D78)
- **In the game:** `Round start.wav` as each battle starts, every faction.

## The tutorial (D77)
- **In the game:** Play page, Tutorial: a scripted match in 29 steps, with a panel and an outline on what each step means; Skip tutorial at any time.
- **Checked:** played through by a test and by the test game (pictures of every step); a player who follows along wins in round 5.
- **Not checked:** nobody has played it by hand; the texts are drafts.

## Your Flood change (D76)
- **In the game:** Swarm Forms bite ground units; Seeders shoot air, and only raise what the Flood has its own version of. The Seeder at 475 with weaker seeds, as the change carried the Flood to 66%.
- **Tournaments:** every sub-faction 47.7% to 53.9% over three runs. The AI's table from whole matches made the AI lopsided (UNSC 32%), so the game keeps its one-on-one table.

## Your playtest: undo, base hits, sound by zoom, battle stats (D75)
- **In the game:** Undo (button under Ready, or Ctrl+Z) steps back this round's deployment, never past the card choice; base hits are louder and the music dips under them; zooming in on a fight makes it louder, far and off-screen sounds quieter; battle stats numbers have no commas.
- **Checked:** tests of each (undo of buys, sells, moves, level-ups, unlocks; the card line; the round end; the sound curve; the music dip); the match, HUD and audio scene tests.
- **Not checked:** nobody has heard the new levels; the curve's numbers are first guesses.

## Fine-tuning (D74)
- **In the game:** Grunt Goblin 1500 health with a gentler aura (0.6%), Carrier Form burst 110, Spartan Sniper 200 damage, Skitterer 7 damage.
- **Tournaments:** damage now counts drops, spawns and infected bodies; 26 outliers tested by removing them; seven rounds. Every sub-faction 47.0% to 53.2%; Flood 46.5% overall: `Docs/Balance/hard_ai_tournament_d74.txt`. The counter web list: `Docs/Balance/counter_web.md`.
- **Not fixed:** the Pure Form Tank, Covenant Phantom, Vulture, Elite and Ghost stay weak (morning items 122 to 124).

## A tougher Pelican, the Grizzly, damage taken (D73)
- **In the game:** the Pelican has 1200 health (was 800). The Grizzly stays at 500: at 450 the AI bought it into Noble's Spartans and Armored Division fell further.
- **Tournaments:** can now play fresh matches (`--seed`) and record damage taken, what armour stopped, losses and levels. Over three runs every sub-faction is from 46.2% (Covenant-based) to 53.0% (Noble), Armored Division 48.3%: `Docs/Balance/hard_ai_tournament_d73.txt`.
- **Not checked:** no Skirmish tournament; nobody has played the tougher Pelican.

## The Spartan's wind-up, the Covenant Phantom, Jackal snipers (D72)
- **In the game:** the Spartan Laser charges for 2.5 s before each shot and fires every 3.5 s; a Covenant Phantom for every Covenant sub-faction, dropping Grunts with its Gravity Lift; the Jackal (Beam Rifle), 4 snipers at 24 m.
- **Tournament at 2000 health:** the snipers first carried Banished to 65% and sank Armored Division to 39%. At 40 damage and 175 credits every sub-faction is from 46% to 54%: `Docs/Balance/hard_ai_tournament_d72.txt`.
- **Not checked:** no Skirmish tournament; nobody has played the new units.

## Your second batch of sounds and models, the towers, growths, blue explosions (D71)
- **In the game:** all your new sounds, with the M60 on every machine gun; the new Scorpion, Gremlin, Flamethrower Trooper, plasma and beam rifles; both towers, lit in the owner's faction colour; growths on the infested vehicles, as a trial; Covenant machines exploding light blue.
- **Checked:** tests of each part; pictures of the models, towers, growths and blue explosions; a recorded battle sits about 1 dB over the music.
- **Not checked:** nobody has listened, and the growths were judged in close-ups only. They can be reverted on their own.

## Your sound effects (D70)
- **In the game:** 55 of your 59 files, by weapon and unit, with your exceptions: Zealots' sword ready, the Scarab's charge then in, loop and out, Seeder and Swarm Form calls, Flood howls on rising. The SMG, M60, Ghost and flamethrower loop while firing.
- **Loudness:** your files are measured and turned to the game's levels; a recorded battle set the effects 3 dB lower again.
- **Not checked:** how it sounds. I measured a recording; nobody has listened.

## Battle stats (D69)
- **In the game:** a Battle stats button at the end of a game: a tab per battle, the field from above as it started with an icon for each squad, tower and spell, and both armies' damage dealt, taken and stopped. Settings has a Battle log switch.
- **Checked:** the window in pictures from a test game; tests of the counting, the window and the switch. The battle reads none of the new counters.
- **Not checked:** nobody has clicked it with a mouse, or opened it at a small window size.
- **After your answers:** the map takes in the flank zones, so UNSC squads deployed there show where they stood; a test shows a regrown shield's damage counts as taken.

## After your two games (D68)
- **In the game:** smoke and oil at 8 x 45 m; towers at 150 health; bases show below zero; a side under a shockwave pulses pale blue; a Hide cards button; the blueprint look; a battle log for each game you play.
- **Tournament at 2000 health:** sub-factions from 47% to 53%: `Docs/Balance/hard_ai_tournament_d68.txt`. Skirmish: Hard beats Normal 43 of 50.
- **Not checked:** the oil on the field, the blueprint's button hover, and the shockwave's pulse moving. Nobody has clicked Hide cards or Open battle logs with a mouse.

## The owner's music, and the end of a round (D67)
- **In the game:** your music by your rules; the last shots land before a battle ends; survivors hit the base as nodes of light, smallest first, then each Titan alone; a ribbon says MATCH WON, MATCH LOST or MATCH DRAWN, and VICTORY or DEFEAT for the whole game.
- **Tournament at 2000 health:** sub-factions from 48.3% to 53.4%: `Docs/Balance/hard_ai_tournament_d67.txt`. The counter web was kept, as the new rule moved 4 of 4761 matchups a little.
- **Not checked:** nobody has heard the music or how loud it sits against the battle, and I can only see still frames of the nodes.

## The third batch of models, and a flying Seeder (D66)
- **In the game:** all 16 of your files are in, with the Anniversary Human Flood Form on the Infested Marine and the red Brute on the Brute Grenadier. The Seeder flies.
- **Tournament at 2000 health:** sub-factions from 48.7% to 53.0%, so the Seeder needed no other change: `Docs/Balance/hard_ai_tournament_d66.txt`.
- **Fell back:** Meshy refused the Infested Marine and the Combat Form, and tore the Goliath. The Marine uses its file's rig with clips made by script; the other two are static models that wobble and swing.
- **Not checked:** nobody has played this batch, and I can't watch motion, only still frames. The Brute Grenadier, Infested Marine, Combat Form, Goliath and Spawner have no firing frames in the gallery. No bench was run for the new bodies.

## The Spartan, the Zealot and what the AI reads (D65)
- **In the game:** the Spartan has 1250 health; the Zealot leaps at its enemy, and its sword cleaves; Noble's Sniper costs 400 and Heavy Weapons 450; Hard picks techs by the army it has seen.
- **Tournament at 2000 health:** sub-factions from 48.1% to 51.9%: `Docs/Balance/hard_ai_tournament_d65.txt`.
- **Not reached:** the Zealot's impact is 0.82, not 1.5. Going further put Covenant at 58%.

## The slot and Armored Division (D64)
- **In the game:** the extra squad slot costs 50; the Cobra's railgun deals 175 (was 190), and the Rhino's cannon 140 (was 150).
- **Tournament:** sub-factions from 46.6% to 53.3%, with Armored Division at 53.3%: `Docs/Balance/hard_ai_tournament_d64.txt`. The Hard AI now fields the Rhino in 36% of matches, down from 68%.

## Your answers to D62 (D63)
- **In the game:** base health moves 5% at any setting; a shockwave weakens a Seeder's seeds; the Cobra reads as six-wheeled; in Unlock Skirmish, Hard levels before it buys new squads.
- **Hard against Normal, of 50:** 41 at 2000 health, 42 at 10000 (it was 18), 37 at 100.
- **Tested, not changed:** Armored Division without the Pelican wins 54.9%; the extra squad slot at 50 makes no real difference to Hard.
- **Tournament:** sub-factions from 48.1% to 54.1%. Armored Division is one win past 54%: `Docs/Balance/hard_ai_tournament_d63.txt`.

## Unlock Skirmish and the Play submenu (D62)
- **In the game:** Unlock Skirmish mode with its unit bar and unlock page; Play leads to the modes, their settings and the Sandbox; a blind draft; blood; more dust on High; towers in the Sandbox; always a decimal point; the shop by tier then price; a frame-rate counter behind a setting; the Hard AI's Rapid Resupply; the Pelican's first drop 5 seconds in.
- **Tournament:** all 8 sub-factions from 48.0% to 54.0%; Covenant 49%, UNSC 52%, Flood 48%: `Docs/Balance/hard_ai_tournament_d62.txt`. The Spartan costs 500 and the Vulture's missiles deal 50 to get there.
- **Skirmish:** Hard over Normal over Easy at 100 and 2000 health and with either card filter, but Normal beats Hard at 10000: `Docs/Balance/skirmish_tournament_d62.txt`.
- **Fixed:** a crash from an EMP on a landing Pelican, and the R key's silent turn click.
- **Not tried by a person:** real mouse input and listening to the sound. Your list is in `HUMAN_TODO.md`, and questions are items 74 to 82 of `MORNING_DECISIONS.md`.

## Flank zones and the balance band (D61)
- **In the game:** flank zones for the Pelican and Aerospace's aircraft. Only the Hard AI uses them, and only it defends against them. The Infected Pelican is back at 400.
- **Tournament:** all 8 sub-factions are within the new 46% to 54% band, from 47.1% to 53.0%, and the factions are Covenant 49%, UNSC 51%, Flood 50%: `Docs/Balance/hard_ai_tournament_after_changes.txt`. Aerospace Command went from 32% to 53% with no buff to its units.
- **Two small changes for the band:** the Infested Marine has 140 health (was 130), and the Grunt Goblin costs 400 (was 450).
- Questions are items 72 and 73 of `MORNING_DECISIONS.md`.

## Balance applied, and more Infection Forms (D60)
- **In the game:** the suggested balance changes, no Titans on reinforcement cards, 6 Infection Forms to a group, a group from every Flood vehicle that dies, 30 forms and a price of 350 for the Infected Pelican, Archer pods that can't move, and a Pelican that lands behind enemy lines up to 3 times.
- **Tournament after all of it:** Covenant 54%, Flood 53%, UNSC 43%. Sub-factions run from 48% to 57%, except Aerospace Command at 32%: `Docs/Balance/hard_ai_tournament_after_changes.txt`.
- **The old Scarab would be worse:** Covenant 63%, UNSC 38%: `Docs/Balance/hard_ai_tournament_old_scarab.txt`.
- **A power cut** stopped the first run of the counter web and the tournaments. No file was damaged, and they were run again.
- Questions are items 68 to 71 of `MORNING_DECISIONS.md`.

## The Hard AI tournament (D59)
- **3,600 whole matches** between two Hard AIs on the game's real data, 100 for each pairing of sub-factions: `Docs/Balance/hard_ai_tournament_baseline.txt`.
- **Result:** Covenant 74%, UNSC 51%, Flood 19%. The Scarab is the biggest single cause of Covenant's lead, and Flood is weak across its roster.
- **Suggested changes,** not applied: `Docs/Balance/suggestions.md`. In the same tournament they give Covenant 52%, UNSC 53%, Flood 44%, and every sub-faction 44% to 55%.
- **Also:** a dearer starting card now means a weaker base, which the tournament shows is fair, and a tower's XP goes by its level.
- Questions are items 65 to 67 of `MORNING_DECISIONS.md`.

## Your notes from playing (D58)
- **All 10 are in:** starting cards of 250 to 350, the enemy's 2 starting squads in grey, the shop by price, level 1 stats from a shop click, the tower's 40% debuff plus 40% more damage taken, XP for killing a tower, the item and the arrow both showing, Elite Recruitment's note, the Grenadier's straight rockets and the Abomination's buff.
- **The Abomination** deals 222 damage/s against Unarmored (was 89) and wins 65 of 68 matchups (was 56). Flood stays the weakest in AI matches.
- Questions are items 61 to 64 of `MORNING_DECISIONS.md`.

## Your second batch of models (D57)
- **23 units have real models now**, each moving as best it can: Spartans, Elites, Hunters and the Goblin run on Meshy's rigs and clips, the Abomination on clips made from its own bones, wings beat, legs scuttle, wheels spin, turrets aim, hover vehicles bob. Their icons are redone.
- **Colours:** five Noble armours and a gold Zealot are recoloured textures. Each Spartan has a placeholder weapon, and the Zealot has your energy sword.
- **Meshy:** 5 rigs, 69 credits' worth of clips, and 5 paint jobs (2 of them redone after my reduction crushed the Rhino): about 145 credits, leaving about 1,200.
- **Also:** dead ground units stay 14 seconds, and the Shortsword became the Longsword.
- Questions are items 58 to 60 of `MORNING_DECISIONS.md`.

## Towers and your morning answers (D55, D56)
- **Towers:** a Command Center and a Research Center per side, with levels, a shockwave when one falls, everything they sell, and an AI that uses them by difficulty. The models are plain shapes for now.
- **Your answers:** the Pelican's blast is 3.5 m in radius, the Blisterback and Methane Wagon are gone, and `Assets_to_Download.md` lists every track, unit sound and model left.
- **A code review of the towers** found 8 real defects, all fixed. The worst were the enemy's shots turning red in every match, because both sides having towers made every match count as a mirror match, and Esc opening the pause menu over a tower's window.
- Questions are items 52 to 57 of `MORNING_DECISIONS.md`.

## After your play test (D54)
- **Your five quick wins:** a smaller Pelican crash (look only), a turning and tilting camera (C resets it), Level all, a Screen setting, and a still menu Scarab.
- **Your roster notes:** every unit now has 3 techs (most had 2), and the two infested Titans are gone, which puts a Flood player at 16 or 17 units, in line with the others.
- **A bug found on the way:** damage techs never reached death bursts, so Volatile Bladder did nothing. Fixed.
- **Testing this time** covered only what these changes touch, as you asked. See the end of this report's Test results.
- Questions are items 47 to 51 of `MORNING_DECISIONS.md`.

## What M10 adds
- **Infected Pelican:** falls out of the sky at a target within 22 m, past other air units, and dies where it lands (D46).
- **Sub-factions:** all 8, with 34 exclusive units on top of the base roster, so 69 units in all, and the real 5-card draft (D52, D54, D56).
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
- **After the flank zones, only what they touch:** 174 EditMode tests (flank zones, AI, mechanics, rosters, match data, counter web, towers, cards, menus) and the PlayMode Match and Sandbox scene tests pass, after correcting one expectation of 5 forms to a pack; 59 data tests pass after the last two balance changes. In a test build's pictures the zone shows beside the enemy's area, a Pelican can be placed in it, and it arrives at 10.0 s and goes for the nearest tower. The right zone, which sits under the shop at the default camera, wasn't looked at. The full suites were **not** run. Nobody has played against the Hard AI's flanks yet.
- **After the changes of D60, only what they touch:** 185 EditMode tests (AI, cards, towers, infection, rosters, match data, counter web, draft, weapons) and 52 PlayMode tests (Match scene, effects) pass, after updating the roster table to the new numbers. A picture run of a test build showed the Pelican landing behind enemy tanks with its Marines out, with no exceptions. It was shot down before a second drop in that run, so a second and third landing have only been seen in tests and in the tournament's count. The whole counter web and a full tournament were run on the final numbers. The full suites were **not** run. Nobody has played these changes yet, and how the bigger swarms affect the frame rate wasn't measured.
- **After the base health and tower XP changes, only what they touch:** the EditMode tests for the draft, towers, round flow, match data, cards, menus, AI and leveling, and the PlayMode Match scene tests, pass. A picture run of a test build had no exceptions, and the draft cards show the new line. The full suites were **not** run. The tournament and its trials ran outside Unity on the game's own code and exported data. The suggested changes have not been run in the game itself.
- **After your notes from playing, only what they touch:** the EditMode tests for the draft, towers, AI, cards, leveling, menus, match data and rosters, and the PlayMode Match scene tests, pass. 5 expectations were out of date and were corrected: nothing of the enemy seen before the first battle (4 tests) and where a bought squad is among the views. A picture run in a test build showed all 3 factions' tower windows, the shop, a shop unit, the grey squads and an item with an arrow, with no exceptions. The Abomination's and Grenadier's rows of the counter web were rerun at 10 battles, and 30 AI matches per pairing at the old and new Abomination. The full suites and the rest of the counter web were **not** run. Nobody has played the tower changes in a real match, so their balance is untested: the counter web has no towers.
- **After the second batch of models, only what it touches:** the cards, rosters, counter web and baked-body tests, and the Sandbox, effects and match scene tests, pass. 3 expectations were out of date and were corrected: a Spartan with no body, and the Sandbox's two unit counts, which the removal of units had already left wrong. A gallery run took pictures of the units standing, moving and firing with no errors, stopping at my time limit in its later effects pictures. The Longsword's row of the counter web was rerun at 3 battles. The full suites and the rest of the counter web were **not** run. Nobody has judged the motion in play.
- **After the towers, only what they touch:** the matching Unity and .NET tests passed, including 20 new tower tests in .NET, and one real match ran as UNSC with no errors. A flanker killed a Command Center in it. The speed test's battle gave the same result line as before, so battles without towers still play the same. The Unity run came just before the last beam fix, whose test passed in .NET. The full suites, other factions' matches and the counter web were **not** run on this version. No real mouse has clicked a tower.
- **After your play test, only what those changes touch:** Unity EditMode 167 of 167 and PlayMode 31 of 31 on the matching tests, .NET 69 of 69 on the matching tests, and one real match as the UNSC-based Flood with no errors, whose picture shows 3 techs in the squad window. The full suites, the other factions' matches, the demos, the speed test and the counter web were **not** run again on this version. The numbers below are from before these changes.
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
