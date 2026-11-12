# Daily report: 2026-11-02 (overnight), M10

Earlier reports are in this file's Git history. **M10 is built and merged**, and the Infected Pelican now dives. It is waiting for your play test. **Start with `MORNING_DECISIONS.md`.** It lists every choice I made for you.

## The Infected Pelican, the Abomination's counter, your answers (D134)
- **Infected Pelican, after your play test:** health 900 → 1,350 so it reaches the enemy, blast 900 → 1,200.
- **The tournament you asked for before the push:** every sub-faction 48–52%. Covenant-based Flood 49 → 50%; UNSC-based Flood stays at 48% (48.0 → 48.5). The AI buys the Pelican in only about 1% of Flood matches; those it buys now do 11–29% more damage. Most Infected Pelicans are Pelicans the Seeders took.
- **Item 173:** no unit but the Hunter Captain beats the Abomination at equal credits under the real rules (four Captains against two Abominations, 70%); all 67 others lose every battle. As you chose, the Captain stays listed and the test accepts 70% for that pair only.
- **Items 175–177:** the Grenadier will carry its rocket launcher on the shoulder like the Spartan (D135, the Scarab session). The Sniper and the Elite stay as they are.
- **Not checked:** not played in the game.

## Noble rebalanced: the CQB and the Heavy (D133)
- **The tournament you asked for:** Noble had fallen to 45%. The Heavy losing its rockets did it; the CQB was fielded in only 5% of Noble's matches with or without its leap.
- **CQB, your changes:** ground only (a shotgun can't reach aircraft), 300 credits, shotgun 100, shield 600. Not too strong: still fielded in only about a quarter of Noble's matches, so I raised the shotgun to 120.
- **Heavy:** 400 credits, chaingun 18 damage and 20 m (the rockets' reach). Both unlock for half their price, as the Bible's Tier 3 rule asks: CQB 150, Heavy 200.
- **The CQB's 300 is under the Tier 3 band (400 to 500):** noted as your exception in the Bible and its test.
- **Result:** every sub-faction 48–52%, two rounds running. Noble 52%, the Heavy in 96% of Noble's matches again.
- **Not checked:** not played in the game; the tests pass apart from the old Hunter Captain one (item 173).

## The Heavy Spartan's machine gun (D132)
- **Your gun:** whole, shield and mount and all, every triangle kept.
- **Carried like a minigun:** low in front at the belt in both hands, the body leaning into it; it walks heavily and fires from the hip, the gun shaking as it rattles.
- **No more rockets:** the Hydra pod is gone. One on one it now beats 31 of the other 68 units (50 before), so Noble is likely weaker; the top session is running the Insane tournaments you asked for.
- **Checked:** the tests, the game's gallery pictures. **Not checked:** played in a match.

## Your twinblade, MA5B and battle rifle (D131)
- **Twinblade:** the Zealot carries it now. Its blades are as you made them; only the hilt went from 8,795 triangles to 899.
- **The glow:** the blades are drawn as light, a white core inside three cyan halos that fade outwards, like your picture. The Zealot's icon shows the white blade (icons can't show a see-through glow).
- **MA5B:** the Marines' and Infested Marines' rifle, down to about 900 triangles, as they come 18 to a squad.
- **Battle rifle:** the Spartan Fireteam carries it now, in place of the assault rifle.
- **Checked:** the tests and the game's gallery pictures. **Not checked:** played in a match.

## The CQB stops and shoots (D130)
- **No more jump:** the Spartan CQB runs up to its shotgun's range and fires; its thruster leap is gone. Better Thrusters (25% faster) stays.
- **Balance:** not measured, as you asked for no tournaments for now. The leap was how it reached artillery, so Noble may come out weaker.
- **The Zealot's sword:** I made it glow (a white core in a cyan halo), but this sword model's blades are so thin it hardly showed. When your new model is in, I'll put the glow on it. The Zealot is unchanged for now.
- **Checked:** the roster, leap and body tests. **Not checked:** played in the game.

## The Spartans', the Elite's and the Zealot's own animations (D129)
- **Why:** they walked and fired with the Marines' clips, which didn't fit them.
- **Spartan:** walks with the laser resting on its right shoulder, the left arm swinging; fires standing, the laser shouldered, a small kick each shot. **Heavy Weapons** the same, slower, its chaingun rattling.
- **Fireteam, Grenadier, Sniper:** walk with the weapon held low in front, aimed at nothing; fire standing, shouldered and aimed.
- **CQB:** both hands on the shotgun at the hip, always: it runs low with it and fires from there, crouched (both hands, as you asked).
- **Elite:** walks with the plasma rifle in its right hand at hip level (it was in the left); fires with the arm straight out in front.
- **Zealot:** runs with the right arm straight, the sword pointing down behind it. Its strike is unchanged.
- **Feet:** each walk is timed to its unit's speed, so the feet don't skate.
- **To confirm:** morning items 174 to 177.
- **Checked:** the new tests, the pictures, the icons. **Not checked:** played in the game.

## Insane rushes a tower in Unlock Skirmish (D128)
- **The problem:** in skirmish Hard beat Insane, 56% to 44%.
- **Your four playstyles:** all four were built and measured. In Faction mode none beat the usual Insane (Tower Rush 43%, Synergy 45%, Defender 42%), so Faction Insane is unchanged.
- **Skirmish:** Insane now always plays Tower Rush. It beats Hard 62% and 65% on two seeds, Normal 86% and 81% (94% before), and Easy 100%.
- **How it plays:** it picks a lane at the start, sends every new squad up it, and levels its tower on the other side once a round.
- **Synergy and Defender** stay in the code for the tournament (`--styles`); the game doesn't use them.
- **Not checked:** not played in the game; the tests pass.

## Seeders attack everything; the Seeder's death sound (D127)
- **Why they ignored the Elephant and Spartans:** a Seeder could only aim at what its seed can infect: no Titans, no raised shields, no Flood, nothing already seeded. Flamethrower Troopers qualified, so it shot those.
- **Now:** it seeds the closest enemy in range that it can; with none in range, it shoots the closest enemy anyway, without a seed.
- **Seeds (your idea):** a seed now only eats a host the Flood can convert, or an aircraft; a Spartan or a Hunter is only slowed. The Seeder stays at 475 with Seed Spore 45.
- **Balanced:** five Insane rounds; your pick, round 5, has every sub-faction at 49–51%. Noble had fallen to 46% when Seeders melted Spartans.
- **Sound:** a Seeder's death is your Infection Form leap.
- **Not checked:** not played in the game; the tests pass.

## A changelog (D126)
- **Wiki → Reference → Changelog:** every version from 0.378.0, newest first, in a few plain lines each. Each new version adds its entry.
- **Wiki → The AI → Balance:** the Insane tournament numbers.

## Balance tuned for the Insane AI (D125)
- **New standard:** balance is measured with Insane against Insane. Before tuning it ran from 37% (Noble) to 60% (UNSC-based Flood).
- **Now:** every sub-faction is at 49–52%, two rounds running. Covenant-based Flood 52, UNSC-based Flood 51, Aerospace 51, Lekgolo 50, Noble, Banished, Armored and Unggoy 49.
- **How:** damage, health and spawn changes on 23 units, with no price changes. The full list is in DECISIONS D125, and every trial and round is in `Docs/Balance/insane_tuning_log.md`.
- **Spawner at 3,600 health (you chose it):** Covenant-based Flood 52% → 50% under Insane, 77% → 75% under Hard, unchanged under Normal (77%) and Easy (59%). The Spawner is not what makes Covenant-based Flood strong under Hard and Normal.
- **Spawner counter:** with five Combat Forms a lot it screens itself, so single-target units no longer beat it. You kept it as it is: swarm clearers (the Flamethrower Trooper) now count as its counter in the tests.
- **Still open:** morning item 173 (Hunter Captain vs Flood Abomination at 70%). The Abomination now has 10,000 health.
- **Not checked:** not played in the game; the data tests pass.

## Taken aircraft fall, shockwave bar, seeded machines grow (D122)
- **Flood fliers:** the Infested Banshee and Infested Hornet now fall burning like the Banshee and Hornet, with their sounds. The Infected Phantom already did. The Infected Pelican keeps its crash.
- **Shockwave:** its line has a bar under it that runs down smoothly with the time left.
- **Seeded machines:** no explosion, no fall, no death sound. The body stays where it was, in the air if it flew, and the Flood's growths swell on it for 2 s, then it turns.
- **Not checked:** not seen in the game; the tests pass.

## Ghost blast fixed, Infested Ghost drifts (D121)
- **No blast heard:** it was playing, but buried. Your knock-out file is loudest at its end, made for 2.1 s at speed 1. At 2x or 4x the blast came in its loudest part. The knock-out now skips ahead at faster speeds, so it ends as the blast comes.
- **Infested Ghost** drifts and blows up like the Ghost, plus a Flood gore burst.
- **Also fixed:** the last aircraft of a match battle now still crashes after the battle ends.
- **Not checked:** not heard in the game; the tests pass, including one at 2x speed.

## Banshee crash, turret explosion, Ghost drift (D120)
- **Banshee and Infested Banshee:** your fall-and-crash file plays as one, timed so that the crash comes as the wreck lands at any battle speed.
- **turret_explosion:** every machine whose death sound was generated (small vehicles, Phantoms, Vampire). The Seeder, a creature, keeps the generated one.
- **Ghost:** knocked out, it drifts in a random direction for 2.1 s with `ghost_initial_destroyed`, then blows up light blue with `turret_explosion`.
- **Not checked:** not seen or heard in the game; the tests pass, including one that watches a Ghost drift and blow up.

## Taken vehicles' sounds, spell info, heavy towers (D119)
- **Infected vehicles:** wherever they used a generated sound, they now use the normal vehicle's. The Infested Banshee's spore bomb uses the Banshee's bomb sound. The Infested Hornet and the Infected Pelican crash like UNSC aircraft.
- **Spells:** click a spell's name in the spell bar (or Place / Move) to see what it does, when it strikes and its recharge.
- **Towers** have heavy armor.
- **Generated sounds still playing:** listed in the reply for D119 (deaths of non-UNSC aircraft and small vehicles, Flood bone and spore shots, Hornet bombs, Covenant melee, most spells, engine hums, drop thuds, infection start, UI hover, sell and skip).
- **Not checked:** not seen or heard in the game; heavy towers not balance-tested. Tests pass.

## UNSC rockets and bombs heard coming down (D118)
- `rocket or bomb flying down` now plays for every UNSC rocket, missile and bomb in flight (Longsword, M41s, Hornet, Wolverine, Mantis, Vulture, Hydra pod, Archer pod), ending as it lands. The Archer Missile Barrage and HAVOK spells keep it too.
- **Not checked:** not heard in the game; the audio tests pass.

## Sounds: SMG, falling aircraft, your new files (D117)
- **SMG:** your file is one shot and then silence, so looping it gave one shot a second while an ODST fires ten. It now plays every shot, overlapping. **The Ghost** had the same fault and has the same fix.
- **Falling aircraft:** any aircraft that blows up and falls plays the Pelican dive while it falls; its crash comes as it hits the ground.
- **New:** `Longsword_firing` is the Longsword's bombs; `rocket or bomb flying down` plays before the Archer Missile Barrage and the HAVOK Nuke land.
- **Not checked:** not heard in the game; the audio tests pass.

## No Infested Grunt, Spawner Combat Forms, balance (D116)
- **Infested Grunt removed.** Covenant-based has 5 exclusive units.
- **Spawner** releases two Combat Forms (not an Infested Elite) in turn with its Infection Forms.
- **Balance, 2 rounds each:** after D115 and this, Unggoy was 55% and Aerospace 46%. Sparrowhawk back to 250, Skitterer to 125: **every sub-faction 48% to 52%**. Report: `Docs/Balance/hard_ai_tournament_d116.txt`.
- **Not checked:** not played by hand.

## After your play tests (D115)
- **Units go after air:** a unit whose main weapon has nothing to hit uses its other guns' targets, and failing that walks to the closest enemy even if it can't hurt it. Your Infested Ghosts now chase Infested Hornets with their plasma bolts.
- **Level 1** shows in unit stats.
- **Swarm Brood:** your Covenant-based game had no Swarm Forms because the tech wasn't bought. The hatched Swarm Form now rises from the ground so it's easy to see.
- **Swarm Form:** a 4th tech, Long Range (+8 m, 300).
- **Not checked:** the chase moves air matchups a lot (melee-plus-gun units now beat most air units at equal cost); not balance-tested in the tournament. The rise not seen in the game.

## Fourth tech, Double Shot, the range tech, the Condor, gun sounds, and balance (D114)
- **Swarm Brood** now hatches **one** Swarm Form (a stack of five), not a squad; 200 credits.
- **Up to 4 techs.** The Grizzly's **Double Shot** (200): 2 shells 0.3 s apart per attack, reload 20% longer.
- **Range tech: a flat +8 m for 300** on almost every gun unit. 20 units kept their own range tech (now 8 m, 300); 28 got "Long Range" as a 4th. None for melee units, the Longsword, the flamethrower, the CQB Spartan, the Swarm Form, or the Grizzly (its 4th is Double Shot). It no longer lengthens claws.
- **Condor:** its window shows the autocannons and "Light Spartan Laser x5 (secondary)"; the lasers fire 0.8 s apart and a little quieter. The Vampire now beats it.
- **Gun sounds:** machine guns and autocannons play every shot, overlapping, instead of a loop that didn't keep time.
- **Balance, 2 rounds:** Aerospace went to 56% (more Sparrowhawks); **Sparrowhawk 275** put every sub-faction at **48% to 52%**. Report: `Docs/Balance/hard_ai_tournament_d114.txt`.
- **The AI buys few techs:** 0.2 to 1.3 per side per match, nearly all drop techs. So the range tech and Double Shot barely show in the tournament.
- **Not checked:** not played by hand; the sounds not listened to.

## The Spawner's Swarm Brood (D113)
- **Swarm Brood** (250 credits): the Spawner hatches a full squad of Swarm Forms 5 seconds into each battle, once a battle. It replaces **Barbed Lash**, as a unit can have only 3 techs.
- The AI (Normal and Hard) buys it as soon as it has a Spawner, as it does the Pelican's Troop Bay.
- The tournament tool now counts the techs each AI owns, for the next run.
- **Not checked:** not balance-tested (no tournament until you ask), not played by hand. Tests: 116 passed.

## Overnight: UNSC-based Flood and the Condor balanced (D112)
- **UNSC-based Flood (56%):** your suggestion order, one at a time. A weaker Infested Marine made it **stronger** (62%: the AI bought more Seeders), so it was undone. The Infested Scorpion at 275, then the Infested Hornet at 275, brought it to 50%.
- **Condor:** the other session rigged its five gun pods as Light Spartan Lasers and removed the Marines (D111). As given it made Aerospace win 83%: without the drop it fights from the start. Now 500 credits, lasers 20 each: Aerospace 54%.
- **Result over two rounds:** every sub-faction 47% to 54%. Aerospace sits on the top edge (54%); Armored Division and Covenant-based Flood at 47%. Report: `Docs/Balance/hard_ai_tournament_d112.txt`. Version 0.383.2.
- **Not checked:** not played by hand.

## The Condor Gunship's other guns (D111)
- **Five more guns:** the jointed things under the Condor that looked like landing legs are gun pods. Each now turns on its own mount toward its own target and fires a light Spartan Laser: a 2.5 s charge, then a red beam, with the Spartan Laser's sound. The belly autocannon is unchanged. Picture: `Docs/M10/condor_d111.jpg`.
- **Numbers:** 60 damage each every 4 s, so 300 every 4 s across the five, as you asked. The top session balances them next.
- **No more Marine drop.**
- **Checked:** the model's and roster's tests; the guns' turrets and muzzles in the model's pictures. **Not checked:** played by hand; balance.

## The counter web measured again (D110)
- **Done:** `Docs/Balance/counter_web.md` shows each unit's top 5 good and bad matchups; the counter web measured again for the latest changes and the Hard AI's buying table rebuilt. Biggest moves: Combat Form +4, Pure Form Ranged +4, Infected Pelican +3 points. Version 0.382.4.
- **Not checked:** no tournament (you'll ask when you want one).

## The squad you move, shown in its squares (D109)
- **In the game:** while you move a squad, the squad itself stands in the green squares under the mouse, turned if you press R; it goes back if you cancel. Version 0.382.3.
- **Checked:** a new test (the bodies stand in the squares under the pointer, and go back when cancelled) and the match scene tests.
- **Not checked:** not tried by hand.

## Every unit notices enemies from 30 m, the Combat Form leaps, your Infection Form leap sound (D108)
- **30 m for everyone:** no unit runs blind any more. Short-reach units used to notice nothing until an enemy was close: Stalkers, the Chopper and the infested Warthog and Ghost only at 4.5 m, the Pure Form Tank, Spawner and Goliath at 9 m, the infested soldiers at 12 to 15 m, Infection Forms and Zealots at 18 m. The Infested Grunts' own 30 m from D107 is folded into this.
- **The Combat Form leaps**, as the Infested Elite does.
- **Your `Infection form leap.wav`** plays when Infection Forms leap. Other Flood forms keep the Flood leap sound.
- **Morning items 167 to 169:** your no is recorded, and all three stay as they are.
- **One counter is now close:** Hunter Captains beat the Abomination 8 or 9 times in 10 (it was 10 in 10), since its Infection Forms see them from 30 m. The counter test wants 9 in 10, so it may fail on that pair until the next balance pass. I left balance alone.
- **Checked:** the simulation, Flood, Scarab, sound, rules and replay tests; every listed counter in the counter console. **Not checked:** played by hand.

## The Juggernaut animated, and five changes to how units fight (D107)
- **The Juggernaut** runs with a bounding stride, its two scythe arms writhing. To strike it rears up and brings both blades down together in front of it. It pounces with its legs tucked when it leaps, and collapses onto its knees when it dies. Pictures: `Docs/M10/juggernaut_d107_strike.jpg`, `Docs/M10/juggernaut_d107_run.jpg`.
- **The Scarab** stands still while its beam is on and turns with the beam, so the beam always leaves its front. The Hunter Captain's laser does the same.
- **Swarm Forms** go for flyers first, even with ground units right beside them.
- **The Pure Form Ranged** shoots flyers, its shards flying straight at them, for a quarter of the damage, so air still beats it (morning item 167).
- **Seeders** keep drifting when they have nothing to do, instead of freezing in the air.
- **Infested Grunts** notice enemies from 30 m instead of 12 (morning item 168).
- **Checked:** tests for each; the counter matchups they touch, in the counter console. **Not checked:** played by hand; no tournament, as you asked.

## The supercarrier's beam meets the ship (D105)
- **In the menu:** the glassing beam now comes down from under the supercarrier's hull, a third of the way from its bow, where you drew it.
- **Checked:** the menu's pictures and its tests. **Not checked:** played by hand.

## The menu: your supercarrier, the vehicles firing, no glow through the Scarab (D104)
- **In the menu:** your Covenant supercarrier hangs far off over the hills in place of the drawn warship, its glassing beam falling to the burning land behind them. The Mantises fire their chaingun and rockets, Ghosts strafe with plasma, the Scorpions fire more often. The Scarab's mouth glow no longer shows through its back when the camera is behind it. Picture: `Docs/M10/menu_d104.jpg`.
- **Checked:** the menu tests; pictures of every shot and of the menu in the test game.
- **Not checked:** played by hand.

## The beam's backdraft gone, an Elite in the Wraith, the Chopper's rider on its seat (D103)
- **In the game:** the Scarab's beam no longer throws a column of light out behind its mouth. The Wraith's hatch has an Elite sitting in it (a Combat Form in the infested Wraith). The Chopper's Brute sits on the low saddle behind the tank with his feet on the rests, no longer through the tank.
- **Menu:** the racing Ghosts and Warthogs keep clear of everything standing; the beam shows nothing behind the Scarab; the Wraiths have their Elites.
- **Checked:** the crew and menu tests; pictures in the test game (the Wraith, the infested Wraith, the Chopper, the Scarab firing) and of the menu.
- **Not checked:** played by hand.

## The Mantis animated, the Scarab's beam, drivers in the vehicles (D97, D98, D99)
- **The Mantis (D97)** walks now, heavily, each foot planted, the hips dropping onto it, the arms swinging a little; it raises both arms to fire, the chaingun shuddering with every round and the rocket arm kicking as a rocket leaves, and falls flat on its chest when it dies. Its rockets leave its left arm now, its rounds its right.
- **The Scarab (D98)** gathers its shot: energy spirals into its mouth, arcs coil in and a ring of light closes round it, then the beam fires, wound with turning coils of energy, pulses running down it, sparks peeling off; its mouth glows green inside meanwhile, the hollows either side of the cannon lit. The menu's Scarab fires the same way.
- **Drivers (D99):** a Marine at the wheel and the gun of each Warthog, a Grunt on the Ghost, a Brute in the Marauder's hatch and on the Chopper, a Marine in the Elephant's cupola, Combat Forms in the infested Warthog and Ghost. Closed vehicles have nobody to see.
- **Menu:** the Mantises animated and firing, the Scarab's charge and beam as in battle, drivers in the vehicles (none in the wrecks). Pictures: `Docs/M10/vehicles_d97_d99.jpg`.
- **Checked:** new tests (the Mantis's body, its two muzzles and its clip lengths; the Scarab's mouth light and glowing sides; the beam's strands; every crewed vehicle's soldiers and none in the closed ones; a gunner turning with its gun), the menu tests, and pictures in the test game (the Mantis walking and firing, the Scarab charging and firing, every crewed vehicle).
- **Not checked:** played by hand; balance (nothing in the battle changed).

## The version: 0.378.0 and on (D102)
- **In the game:** the menu shows Version 0.378.0. Each push now raises it: the last number for a small change, the middle one for a substantial one (the last back to 0). The first is yours.
- **How:** `python Tools/version.py patch` or `minor`, in the same commit; the other session knows the rule too.

## The menu's music after the disclaimer, and the build number (D101)
- **In the game:** the menu's music starts only once you click the disclaimer away, with the same click as a menu button. The version in the lower left is now the build number: how many commits main has when the game is built (it goes up with every change pushed).
- **Checked:** the disclaimer, menu and audio tests, a new one for the build number; a test build's menu picture.
- **Not checked:** the music's start heard by hand.

## The disclaimer's addition and the version (D100)
- **In the game:** Public Availability ends with your request not to share the game publicly; HALOBELLUM on the disclaimer is in the main menu's title font and colour; the main menu shows "Version 0.1.0" small in the lower left.
- **Checked:** the disclaimer and menu tests; pictures of the disclaimer at 1600x900 and 1024x768 and of the menu (`Docs/M10/menu_version_d100.png`).
- **Note:** the version comes from Unity's Player Settings (Version), 0.1.0 today; change it there and the menu follows.

## Your disclaimer before the main menu (D96)
- **In the game:** when the game starts, your disclaimer shows first: white on black, your words as given, and a glowing "Click anywhere to continue". A click or a key goes on to the menu, which fades in; the Microsoft link opens in the browser. It shows once per start.
- **Checked:** new tests (the words in order; shown for a player, not in tests or picture runs) and the menu tests; pictures at 1600x900 and 1024x768 (`Docs/M10/disclaimer_d96.png`).
- **Not checked:** clicking through it by hand.

## The cinematic main menu (D94)
- **In the game:** the main menu is now a battle of the three sides' vehicles under a storm at dusk, in five slow camera shots that fade into one another, 52 seconds round: the whole field from high up; low along the human line as its tanks fire; among the Covenant's Wraiths and Ghosts behind the striding Scarab; on the Flood's creep, its infested Wraith among the growths; and up at the Scarab as it sweeps the field with its beam. Tank shells and Wraith mortars burst now and then, wrecks burn, lightning strikes, an alien warship glasses the hills far off. **After your look:** no soldiers or Flood forms any more, and far fewer shots in the air. The menu's text sits on the dark left as before; every shot keeps its action on the right. Pictures: `Docs/M10/menu_d94_shots.jpg`; `menu_d94_game.jpg` is the first version in the test game.
- **Your old menu is kept:** Settings > Menu background > Classic brings it back (and Cinematic goes back to the new one).
- **Checked:** the menu tests (the new menu's shots, fades, armies, the Scarab's walk and beam; the classic menu's own tests now on its scene; the build has both scenes; the Settings choice); pictures of every shot from the scene builder and the menu in the test game.
- **Not checked:** played by hand; the frame rate on a weaker PC.

## The tournament after the animations (D95)
- **Result:** every sub-faction wins 48% to 52% (Banished 52%, Noble and Unggoy Rebellion 51%, Aerospace Command and Covenant-based 50%, Lekgolo Colony and Armored Division 49%, UNSC-based 48%), so nothing needed changing. Two rounds, 7,200 matches at 2000 base health. Report: `Docs/Balance/hard_ai_tournament_d95.txt`.
- **Done first:** the counter web measured again for the Brute Grenadier and the Hunter Captain, whose weapons changed, and the AI's matchup table rebuilt from it.
- **Worth a look:** the AI almost never fields the Stalker Form, the Infected Pelican or the Spartan CQB; the Glassing Beam and Gravemind's Wrath go with losing sides (items 158 and 159).

## The Seeder, the Pure Form Tank and the Abomination animated (D93)
- **In the game:** the **Seeder** floats like a jellyfish, each tendril waving on its own, the red tips pulsing; it squeezes and lashes as it spits a spore, and when killed it falls from the sky. The **Pure Form Tank** walks on its legs and its club, one limb at a time, heavily; it hauls the club back, sweeps it round and staggers from the blow. The **Abomination** steps slowly and heavily, torso twisting, tentacles writhing; it rears up with both arms overhead and slams down, and its long tentacle arm cracks out in a whip between slams. Their cards show them attacking. Pictures: `Docs/M10/flood_d93_clips.jpg`, `flood_d93_game.jpg`, `flood_d93_icons.jpg`.
- **Checked:** new tests for the three bodies (clip lengths against their weapons, the Seeder centred and its tendril chains, the rigs' still root); the body, icon, real-model and timing tests; all three in the test game.
- **Not checked:** balance (nothing in the battle changed); not played by hand.

## The Combat Form and the Infested Elite animated (D92)
- **In the game:** both move limb by limb now. They run hunched and lurching, the head hanging off the back lolling, the three whips of the left arm swinging, the feelers on the chest bobbing; up close they slash with the right claw, then lash down with the whips, a blow on each strike; the Infested Elite carries its plasma rifle forward and fires it from the right hand, the Combat Form fires from its claw; they die dropping to their knees and onto their faces. Their cards show them firing. Pictures: `Docs/M10/combat_d92_clips.jpg`, `combat_d92_game.jpg`, `combat_d92_icons.jpg`.
- **Checked:** new tests for both bodies (clips, speeds, the rifle, the shots' start); the body, icon and real-model tests (one expectation from D82 updated: the Elite now has the Combat Form's animated body); the Sandbox timing test; both in the test game.
- **Not checked:** balance (nothing in the battle changed); not played by hand.

## Your new sounds and the Hunter Captain's laser (D91)
- **In the game:** all 20 of your files are in: deaths for humans, Spartans, Elites, Grunts, Jackals, the Brute and the Hunters; the Flood's by size; the Flood's leap and three blows; the Hunter's charge and bash; level up, spell placed and an item put in a hexagon. The Hunter Captain's laser is a 1 s green beam with your sound, drawn as a thin laser.
- **Balance:** the laser deals what it did (96 a second, now 10 hits over a second, then a second's rest), still heals the Captain, and stops when it bashes. No tournament, as you said.
- **Checked:** new tests for the laser (its hits, rest, damage over time, healing, stopping for the bash) and for the sounds (each death, leap, bash, laser and the hexagon sound); the audio, Hunter and clip tests. One audio test that drops files in failed once in the batch and passed alone.
- **Not checked:** not heard or seen in the game by hand.

## The Spawner, the Ranged Form and the Stalker Form animated (D90)
- **In the game:** all three move limb by limb now, instead of a stiff wobble. The **Stalker Form** runs low on all fours at full speed, legs in diagonal pairs, spine and tail sack rolling; it attacks in sudden lunges, three to a round (both arms and its tusks, then each arm), each sprung back into its crouch, each landing on one of its strikes. The **Pure Form Ranged** crawls lopsided, one leg reaching long and the other hitching short, arms pawing, its column swaying late; to shoot it rears back, then thrusts forward hard and the shard leaves the top of its column at the end of the thrust. The **Spawner** lumbers, swaying heavily onto each foot, every tendril writhing to its own rhythm; its belly swells and heaves forward to the maw, which gapes as each brood appears, timed to its spawns; its lash whips the tendrils forward. Their cards show them attacking. Pictures: `Docs/M10/flood_d90_clips.jpg`, `flood_d90_game.jpg`, `flood_d90_icons.jpg`.
- **How:** Meshy couldn't rig them (item 19), so I built their skeletons and skin weights myself in Blender (`Tools/Art/creature_rig.py`). Planted feet slide under 2 cm at their walking speeds.
- **Checked:** new tests for the clip timing, the three bodies, and in a Sandbox battle that the Spawner gives birth on its spawns and the Stalker's blows land on its strikes; the body, icon and real-model tests; all three in the test game.
- **Not checked:** balance (nothing in the battle changed); not played by hand; the Spawner's forms still appear in front of it, not under its belly (item 147).

## The Grenadier's swing (D89)
- **In the game:** a Brute Grenadier cutting at something beside it plays a heavy two-handed overhead chop with its Brute Shot, instead of its shooting pose.
- **Checked:** the body and baked body tests (a new one for the Grenadier's blade and swing); the clip rendered frame by frame in Blender; the Grenadiers against Combat Forms in the test game.
- **Not checked:** the swing's timing against the blade's hits (1.8 s against 1.5 s); not played by hand.
- **Cost:** 15 Meshy credits (785 left).

## Level arrows, the Grenadier's blade, the role on reinforcement cards (D88)
- **In the game:** the level-up arrow is the same size over every squad (the Scarab's was huge). The Brute Grenadier cuts with its Brute Shot's blade an enemy too close for its grenades (inside 2 m), and holds its grenades meanwhile; two Grenadiers side by side now fight. Reinforcement cards show the unit's role on its own line under the name.
- **Checked:** new tests for the Grenadier (grenades at range, the blade close in with the launcher quiet, two Grenadiers fighting, range bonuses leaving the blade alone); the Hunter, weapon-switching, roster, counter web, card and level-arrow tests; pictures of the reinforcement cards at 1600x900 and 800x500.
- **Not checked:** balance (no tournament until you ask); the Grenadier has no swing clip yet, so it cuts in its shooting pose; not played by hand.

## The Hunters fight like Hunters (D87)
- **In the game:** both Hunters (the Hunter Pair's and the Hunter Captain) hold their shield in front: braced on guard, walking behind it, crouched low behind it while the cannon fires over it, as in your picture. When an enemy on the ground comes within 5 m, they stop shooting, charge at it along the ground and bash it with the shield, again and again while it stays close. Their icons show them aiming. Pictures: `Docs/M10/hunters_d87.jpg` (the test game), `hunters_d87_clips.jpg` (every clip) and `hunters_d87_icons.jpg`.
- **Checked:** new tests for the charge and the bash (the cannon at range, the charge and a bash within 5 m, bashing on, no jump over blockers, range techs leaving the bash alone) and for both Hunters' data and clips; the leap, roster, icon, effects and sound tests; the clips in pictures and in the test game.
- **Not checked:** balance (you said you'd do it); not played by hand.

## The Brute Shot, the shotgun and the sniper rifle (D86)
- **In the game:** the Brute Grenadier holds the first Brute Shot of your file, the Spartan CQB the third shotgun (the darkest) and the Spartan Sniper the third sniper rifle, each in place of a stand-in; shots leave their muzzles, and their icons show them aiming. Pictures: `Docs/M10/weapons_d86_icons.jpg` and `Docs/M10/weapons_d86.jpg`.
- **Checked:** the baked body tests (new cases for the two Spartans, the Brute's updated) and the icon tests; the three in the test game.
- **Not checked:** not played by hand.

## The Spartan Laser, and soldiers firing on their cards (D85)
- **In the game:** the Spartan holds the second laser of your `weaponshumanspartan_laser.glb`, the green one with the small blue screen, in place of the rocket launcher; its shots and charge start at the lens. Every soldier's picture, on the cards, in the shop and in the squad window, now shows them aiming and firing instead of standing; the Zealot thrusts his sword and the Abomination rears up. Every icon was rendered again, so the Scarab's and the D82 changes show too. Pictures: `Docs/M10/icons_aiming.jpg` and `Docs/M10/spartan_laser.jpg`.
- **Checked:** the icon and baked body tests (a new one for the Spartan's laser); the icons by eye; the Spartan in the test game.
- **Not checked:** the Combat Form and the Infested Elite are static models, so their pictures still stand.

## The Scarab: your new model and a real walk (D84)
- **In the game:** the purple Scarab from your `Scarab update.glb` (the last and biggest of the four in the file), with its own textures, at the same size. It walks on four legs, one foot in the air at a time: each planted foot stays put while the body passes over it, and the body sways and dips onto the legs carrying it. Its steps keep pace with its speed, and dust rises where each foot lands. The cap on its mast turns to aim the plasma guns. Pictures from the test game, one walk cycle each: `Docs/M10/scarab_walk_side.jpg` and `Docs/M10/scarab_walk_quarter.jpg`.
- **Checked:** new walk tests (no planted foot slides more than 1 cm; one foot up at a time; the same steps per metre at three speeds; the sway and dip; stopping and settling; turning on the spot; the muzzles), the model and menu tests; pictures from the test game.
- **Not checked:** nobody has watched it walk in a real match; turning on the spot faster than it can step, its feet slide a little. The other walkers' weak animations are for later, one by one.

## The spells: random strikes and a sweeping beam (D83)
- **In the game:** the Archer barrage, the Plasma Storm and Blight Rain cover a 20 m area, their strikes landing at random spots in it; the Glassing Beam is placed like smoke and sweeps 30 m.
- **Checked:** the spell tests (new ones for scattered strikes, the same strikes for the same seed, and the beam's course); pictures of the three in the test game.
- **Not checked:** the balance tournament (it plays spells); not played by hand.

## Your answers to the open items (D82)
- **In the game:** no hit sounds; the infested vehicles in tendrils of the growths' colour instead of green; the Combat Form facing forward; glow on the models; the Banshee's wing tips and the Ghost's fins glow while they move; the Infested Elite is the Combat Form holding a plasma rifle; the Sandbox's Map button shows any interior or outdoor map.
- **Checked:** the model, spell and Sandbox map tests; pictures in the test game of the infested vehicles, the Banshee, the Ghost, the Infested Elite and the Combat Form.
- **Not checked:** the thrusters are faint in close-ups from the front; not played by hand.

## Your menu, tutorial and Pelican notes (D81)
- **In the game:** the main menu without its Marines; the tutorial with your wording, glowing flank zones at step 26 and a new step 27 where you buy one of Noble's Spartans; the Pelican's Marines come out from under it.
- **Checked:** the drop, menu and tutorial tests (103); the menu's picture.
- **Not checked:** the glowing zones and the new step have not been seen in pictures; nobody has played the tutorial by hand.

## Battlefield maps: three interior looks (D80)
- **In the game:** every match is fought inside a UNSC hangar, a Covenant warship, or a ship the Flood has taken (on a UNSC or Covenant hull), with its own floor, light and surroundings. Which one depends on the factions and the match seed (`Docs/M10/terrain.md`). Pictures: `Docs/M10/interior_looks.jpg`.
- **Checked:** the terrain tests (every look keeps the floor clear and flat, the flank strips open, under the triangle budget over 16 seeds), the camera, right-click and menu tests; pictures of every look from the test game.
- **Not checked:** nobody has played on them; the outdoor maps are out of the matches (item 130).

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
