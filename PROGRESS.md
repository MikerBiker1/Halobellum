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
- [x] 3 difficulty levels (Easy, Normal, Hard) chosen on the draft screen; Hard uses measured matchups
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
- [x] The 8 sub-factions, 38 exclusive units, the real 5-card draft; reinforcements, shop, Sandbox and AI know them (D52)
- [x] Terrain by faction, 3 layouts each ("more maps"), sky and light, blobs under units (D47)
- [x] Effects: weapon kinds, impacts, deaths, wrecks, spells, camera shake (D48)
- [x] Sound and music: complete system with original placeholders and the owner's slots (D49)
- [x] Main menu, settings, pause, game flow (D50)
- [x] Cards with unit pictures and borders by kind and faction (D51)
- [x] Merged: all tests, real matches for each faction and sub-faction tests, menu flow, card demo
- [ ] The owner's own Halo music and sound, in the slots (see MORNING_DECISIONS.md)
- [ ] Models for the new units (see MORNING_DECISIONS.md)
- [ ] The owner's play test of M10

