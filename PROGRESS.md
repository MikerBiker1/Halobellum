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

## M4: Slice rosters and sandbox (in progress)
- [x] Combat mechanics the slice units need: per-weapon target filters, bonus damage against air, cone splash (flamethrower), and a timed mid-battle drop (Pelican) (with tests)
- [ ] UNSC slice roster (12 units, Section 8.1): data assets with unlock prices, placeholder prefabs, faction roster
- [ ] Covenant slice roster (12 units, Section 8.2): data assets with unlock prices, placeholder prefabs, faction roster
- [ ] Counter web runner: every unit against every unit at equal cost, report and CSV, `balance.csv`
- [ ] Counter web verified: every role has a counter that wins over 70% of the time (first-pass values tuned; Bible numbers only changed with approval)
- [ ] Sandbox mode: pick two armies, place them freely, run the battle
