# Halobellum progress

Milestones and items from PROJECT_BIBLE.md Section 14. An item is ticked when it compiles, its tests pass, and it is committed and pushed. A milestone is only done once the human confirms it.

## M0: Project setup (confirmed by the human 2026-10-31)
- [x] Unity project created, Git initialized, Unity .gitignore (Library, Temp, Logs, Obj excluded), GitHub remote `origin`
- [x] Folder structure from Section 3
- [x] Empty test scene (`Assets/_Project/Scenes/TestScene.unity`)
- [x] `PROGRESS.md`, `DECISIONS.md`, `HUMAN_TODO.md`, `BLOCKERS.md`, `DAILY_REPORT.md` created

## M1: Battle simulation core (confirmed by the human 2026-10-31)
- [x] `UnitDefinition` ScriptableObject and plain C# unit data
- [x] Damage and armor math, with tests
- [x] Optional directional defense, with tests
- [x] Free continuous-2D movement with collision and separation, with tests
- [x] Fixed-timestep simulation in plain C# with closest-enemy targeting, with tests
- [x] Headless runner that simulates 3 placeholder units against each other and prints results

## Follow-ups after review (2026-10-31)
- [x] D2: energy shields use their own damage multipliers, and a hit that breaks a shield carries its remaining damage over to health
- [x] D4: arena defined in squares (60 x 30 per army, 2 squares of no man's land; 2 m per square pending D10)

## M2: Battle in the scene (confirmed by the human 2026-10-31)
- [x] Placeholder units (prefabs) spawn from data, move through the simulation's free movement (no NavMeshAgent), target and fight, show health bars, and die
- [x] Camera pan and zoom; speed controls (pause, 1x, 2x, 4x); selecting a squad opens the bottom-left stats window
- [x] Same armies and seed give the same result in the game and the headless runner
- [x] 300 vs 300 (600 units) runs at 60 FPS with placeholder art (measured on the owner's PC; see DAILY_REPORT)

## M3: Round loop (confirmed by the human 2026-10-31)
- [x] Match rules data and economy: credits, income and income cap, 2 squads per round, 1 unlock per round, refunds (with tests)
- [x] Factions and starting draft (5 cards where enough sub-factions exist; Standard UNSC and Covenant for now) (with tests)
- [x] Deployment grid: placement with snap-to-square, squad rotation, no overlap, only this round's squads movable (with tests)
- [x] Round flow: deployment (no timer), battle (90 s cap), result; base health and base damage from survivors; round reset; do-nothing AI (with tests)
- [x] Match scene with draft, deployment (shop, unlock, placement, rotation), battle and result screens
- [x] A full match against the do-nothing AI can be won and lost (headless and PlayMode tests)

## M4: Slice rosters and sandbox (confirmed by the human 2026-11-01)
- [x] Combat mechanics the slice units need: per-weapon target filters, bonus damage against air, cone splash (flamethrower), and a timed mid-battle drop (Pelican) (with tests)
- [x] UNSC slice roster (12 units, Section 8.1): data assets with unlock prices, placeholder prefabs, faction roster
- [x] Covenant slice roster (12 units, Section 8.2): data assets with unlock prices, placeholder prefabs, faction roster
- [x] Counter web runner: every unit against every unit at equal cost, report and CSV, `balance.csv`
- [x] Counter web verified: every role has a counter that wins over 70% of the time (first-pass values tuned; Bible numbers only changed with approval)
- [x] Sandbox mode: pick two armies, place them freely, run the battle

## M5: AI opponent (confirmed by the human 2026-11-01)
- [x] Rule-based AI: 4 strategies (swarm, armor push, air raid, balanced), counters the player's most expensive role, role-based placement, same rules and limits as the player, no cheating (with tests)
- [x] 3 difficulty levels (Easy, Normal, Hard), chosen on the draft screen until D62 moved them to the menu's Play page; Hard uses measured matchups
- [x] Full playable match against the AI (headless and PlayMode tests)

## M6: Leveling and tech upgrades (confirmed by the human 2026-11-01)
- [x] Squad XP from kills, level-ups for half the base price, level multipliers (with tests)
- [x] Tech upgrades: data assets, up to 3 per unit type, bought with credits, apply to every squad of that type (with tests)
- [x] UI: level, XP bar, level-up button and tech upgrades in the stats window; the AI uses levels and techs

## Later request (2026-10-31): faction-styled UI (done; waiting for the human to look)
- [x] After M5 and M6: UNSC-style UI when the player picked UNSC, Covenant-style when they picked Covenant (fonts, panels, colours)

## Answers to D25-D28 (2026-11-01)
- [x] D25: during deployment, the enemy's army as it was at the last battle start shows as grey ghosts you can select (never its current deployment)
- [x] D26: XP bar doubled (2 x the squad's price); squads ready to level up show a pulsing yellow arrow
- [x] D28: textured faction UI (UNSC hull plating, Covenant violet with glowing blue lines), downloaded fonts (waiting for the human to look)

## Adjustments after the first play test (2026-11-01)
- [x] D29: income builds on the 200 starting credits (400 after round 1, 600 after round 2 ... capped at 1600)
- [x] D30: crowds: slower units make way for much faster friends, smaller walkers give way, fighting front lines edge forward while a friend is stuck behind (counter web still 24 of 24)

## Notes for the next balance pass (from the human, 2026-10-31)
- Flamethrower Troopers should be excellent against chaff such as Marines (today they lose to Marines, which outrange them).
- Warthogs should have enough range, and enough health with their light armor, to destroy Flamethrower Troopers.
- The Pelican has a chin-mounted autocannon, so it should not be weak (today it wins about 27% of matchups on average).
- Marines may be too strong (65% average win rate).
- Adjust unit valuation (prices) if necessary.

## M7: Cards (all criteria pass; waiting for the human to confirm)
- [x] Spell cards: permanent, rechargeable, max copies, price; placed during deployment with blue (defensive, own side) and red (offensive, anywhere) outlines; take effect as the battle starts; hidden from the enemy until then (with tests)
- [x] Reinforcement cards: single-use, budget-based pricing (the Bible's examples tested), unlock the unit, placed like squads bought this round, outside the squad and unlock limits, 0 XP
- [x] Offered every round from round 2, alternating spells and reinforcements, pick 1 of 3 or skip for 50 / 100 credits; card screen UI; AI takes cards and casts spells
- [x] First-pass spell catalogue for UNSC (6) and Covenant (5), ODST drop-pod unit
- [x] D33 (your additions): friendly fire, Smoke Bomb, EMP, Banshee Ambush, ODST SMG and rockets, items and Inventory, economy and powerful buff cards, deployment slot, Intensive Training, reinforcements from round 4, squad prices in the shop
- [x] D34: smoke lasts the whole round, Intensive Training twice with separate cooldowns, Laser Sights +2 m, all items in the Sandbox
- [x] D35 (play test): drag items onto hexagons, no budget text, blue shield bar, two-click line placement at any angle, Glassing Beam hits air
- [x] D36 (after the speed trial): battle speed is real again; all move speeds halved, +1 m/s for Warthog, Ghost and Gauss Warthog; counter web and matchup table re-measured. For the next balance pass: slower armies favour long-range units (12 of 276 matchups changed winner, see D36)
- [x] D37: lobbed artillery that aims ahead and can miss (Wraith, Archer Missile Pod); Scarab Focus Beam (2 s charge, 3 s at 400 damage per second, 3 s rest). For the next balance pass: the Scarab beats 22 of 23 units and its damage against Heavy armor went up, so check it in mixed armies
- [x] M7 confirmed by the owner, along with D37

## M8: Flood faction

- [x] Packs of tiny forms: one body stands for 5, area hits hurt them all, they show and deal less as they die
- [x] Infection: Unarmored units killed near Infection Forms rise 2 s later for the Flood at 60% health; rules tested
- [x] Seeder, Carrier Form death burst, Infected Pelican crash dive, Infected Phantom drop, Abomination aura and spawning
- [x] Flood roster (11 units), techs, 8 spells, shared items and cards, Flood_Standard faction, Match and Sandbox scenes
- [x] Flood UI theme and textures, Creepster title font, placeholder prefabs, packs and rising corpses on the field
- [x] AI: Normal and Hard avoid Unarmored units against the Flood, Easy doesn't care; the AI can also play as Flood
- [x] First balance pass: counter web 35 of 35, matchup table covers all 595 pairs
- [x] D39 (after watching it): Infection Forms pounce from 6 m and jump over what holds them up; fixed units pausing when they enter the enemy's zone
- [ ] For the next balance pass: Scarab beats 33 of 34; Warthog and Ghost beat only 1 of 11 Flood units; Infection Forms beat Spartans and Warthogs; 49 of 595 pairs reach the 90 s limit
- [x] M8 confirmed by the owner, along with D39

## M9: Polish

Order agreed with the owner (D40), with approval between stages:

- [ ] Pipeline proof (Pass 0): one unit all the way through, and a full army's performance
  - [x] Crowd benchmark: baked animation draws 1200 bodies in 1.53 ms a frame, normal animation in 12.64 ms (D41)
  - [x] The Marine, a real model made with Meshy, in the game: baker, cross-fades, death, placeholder rifle; 1200 of them in 2.00 ms a frame (D42)
  - [x] Real models keep their colours, black rifle, green and red health bars (D43); the owner approved the Marine's animations
  - [x] One vehicle: the owner's Scorpion, painted, sized and in the game, not animated, by the owner's choice (D44)
  - [x] Sound on a real unit, done with M10 (D49)
- [ ] Combat identity (Pass 1): every weapon behaves as intended, with placeholder effects and sounds
- [ ] Balance pass
- [ ] Models and animation (Pass 2)
  - [x] The owner's first big batch: 18 models, weapons reduced, muzzles, turrets, code motion, air units at 13 m, baked Grunts (D45)
  - [ ] Models for the units that still have none (see HUMAN_TODO)
- [x] Effects, audio and music (Pass 3), done with M10 (D48, D49): placeholder sound and music until the owner's files come
- [x] Menus and settings, done with M10 (D50)

## M10: Sub-factions and faction-themed content

Asked for by the owner as one overnight job. Built in parallel, then merged and tested together.

- [x] The Infected Pelican dives past other air units (D46)
- [x] The 8 sub-factions, 34 exclusive units (38 until D54 and D56), the real 5-card draft; reinforcements, shop, Sandbox and AI know them (D52)
- [x] Terrain by faction, 3 layouts each ("more maps"), sky and light, blobs under units (D47)
- [x] Effects: weapon kinds, impacts, deaths, wrecks, spells, camera shake (D48)
- [x] Sound and music: complete system with original placeholders and the owner's slots (D49)
- [x] Main menu, settings, pause, game flow (D50)
- [x] Cards with unit pictures and borders by kind and faction (D51)
- [x] Merged: all tests, real matches for each faction and sub-faction tests, menu flow, card demo
- [x] Independent review by area, and fixes for the 31 problems it confirmed
- [x] Second round: the reviewers' lesser notes, each area independently checked (D53)
- [x] The owner's quick wins: smaller Pelican crash, turning camera, Level all, Screen setting, still menu Scarab (D54)
- [x] 3 techs on every unit, no infested Titans, damage techs reach death bursts (D54)
- [x] Towers: targets, shockwave, levels, Command Center and Research Center, the AI by difficulty, placeholder models (D55)
- [x] The owner's morning answers: Pelican blast of 3.5 m, Blisterback and Methane Wagon removed, `Assets_to_Download.md` (D56)
- [x] The owner's own music, in the slots, with their rules (D67). Their own sound effects are still to come (see Assets_to_Download.md)
- [x] The owner's second batch of models: 23 units fitted, animated, icons redone (D57)
- [x] Dead ground units stay 14 seconds, Shortsword to Longsword (D57)
- [x] Notes from playing: starting cards about 300, enemy's starting squads in grey, shop by price with level 1 stats, tower debuff and XP, item and arrow together, Elite Recruitment's note, Grenadier rockets, Abomination buff (D58)
- [x] Base health follows the starting card's value, a tower gives the XP of a squad of the tier of its level (D59)
- [x] Hard AI tournament on the game's real data, with a report and suggested balance changes (D59)
- [x] Applied the suggested balance changes, no Titan reinforcements, 6 Infection Forms to a group, forms from Flood vehicles, Archer pods that stay put, the Pelican's landings, tournament rerun (D60)
- [x] Flank zones for the Pelican and Aerospace's aircraft, used and defended against by the Hard AI (D61)
- [x] Every sub-faction wins 46% to 54% in the Hard AI tournament: the rule from now on (D61)
- [x] Unlock Skirmish mode: no factions, 69 units to unlock, all spell cards, 2 random starting squads, the unit bar and unlock page (D62)
- [x] The Play submenu with mode settings; Sandbox and difficulty moved there; frame-rate counter behind a setting (D62)
- [x] The draft is blind: the AI commits before your card applies (D62)
- [x] Blood by what was hit, more dust on High, a slot for the Hunter death sound, towers in the Sandbox behind a switch, always a decimal point, shop by tier and price (D62)
- [x] The Hard AI takes Rapid Resupply when about to lose; the Pelican drops 5 seconds in (D62)
- [x] Base health moves 5% at any setting, a shockwave weakens seeds, Hard levels before new squads in Unlock Skirmish (D63)
- [x] The extra squad slot costs 50; a weaker Cobra and Rhino for Armored Division (D64)
- [x] A weaker Spartan, a Zealot that leaps and cleaves, cheaper Noble Spartans, Hard's techs by the board (D65)
- [x] The third batch of models, the Anniversary Flood Form on the Infested Marine, a flying Seeder (D66)
- [x] The last shots land, nodes of light hit the base, and result windows with a ribbon (D67)
- [x] After the owner's two games: smoke and oil, towers, overkill shown, the shockwave on the bodies, Hide cards, the blueprint look, a battle log (D68)
- [x] Battle stats at the end of a game, and a switch for the battle log (D69)
- [x] The owner's sound effects in, by weapon and unit, under the music (D70)
- [x] The owner's second batch of sounds and models, the towers in faction colours, Flood growths, Covenant machines exploding light blue (D71)
- [x] The Spartan Laser's wind-up, a Covenant Phantom, Jackal snipers, balanced in the Hard AI tournament (D72)
- [x] A tougher Pelican; tournaments with seeds and damage taken; every sub-faction in the band over three runs (D73)
- [x] Fine-tuning with credited damage and removal tests; the counter web's good-against and bad-against list (D74)
- [x] Undo in deployment, base hits heard over the music, battle sounds by zoom and camera, battle stats numbers (D75)
- [x] Swarm Forms bite the ground, Seeders shoot air and convert only into Flood units, balanced; the AI's table from whole matches tried and set aside (D76)
- [x] A tutorial (D77)
- [x] The Spawner releases Infested Elites between its Infection Forms; the Infested Hornet trimmed; balanced (D79)
- [x] Battlefield maps: three interior looks (UNSC installation, Covenant warship, Flood-infested ship), one per match (D80)
- [x] No Marines on the main menu; the tutorial's wording, glowing flank zones and an exclusive unit step; the Pelican's Marines from under it (D81)
- [x] Your answers: no hit sounds, Flood tendrils on infested vehicles, glow and thrusters, the Infested Elite as the Combat Form with a plasma rifle, any map in the Sandbox (D82)
- [x] The barrage spells strike at random over a wide area; the Glassing Beam sweeps a course (D83)
- [x] The Scarab's new model (your updated file) and a walk on planted feet, one foot in the air at a time (D84)
- [x] The Spartan holds your Spartan Laser; soldiers' icons and cards show them aiming and firing (D85)
- [x] Your Brute Shot for the Brute Grenadier, shotgun for the Spartan CQB, sniper rifle for the Spartan Sniper (D86)
- [x] The Hunters: shield in front when aiming, a charge and shield bashes within 5 m, aiming icons (D87)
- [x] Level arrows one size; the Brute Grenadier melees close in; reinforcement cards show the role (D88)
- [x] The Brute Grenadier swings its Brute Shot when it cuts (D89)
- [x] The Spawner, the Pure Form Ranged and the Stalker Form animated limb by limb: a lumbering walk and a heaving belly timed to its spawns, a lopsided crawl and a recoil-and-thrust shot, a low run and lunges on its strikes (D90)
- [x] The owner's third batch of sounds; the Hunter Captain's laser a held 1 s green beam (D91)
- [x] The Combat Form and the Infested Elite animated limb by limb: a lurching run, claw-and-whip swipes on their strikes, the Elite's plasma rifle in hand (D92)
- [x] The Seeder, the Pure Form Tank and the Abomination animated: a floating Seeder with waving tendrils that falls dead from the sky, a Tank on legs and club with a sweeping blow, the Abomination's heavy steps, overhead slam and tentacle whip (D93)
- [x] Hard AI tournament after the animations: every sub-faction 48% to 52%, no change (D95)
- [ ] Models for the remaining units (see Assets_to_Download.md)
- [ ] The owner's play test of M10

