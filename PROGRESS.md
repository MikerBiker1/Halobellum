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
