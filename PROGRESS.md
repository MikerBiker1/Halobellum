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
