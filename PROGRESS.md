# Halobellum progress

Milestones and items from PROJECT_BIBLE.md Section 14. An item is ticked when it compiles, its tests pass, and it is committed and pushed. A milestone is only done once the human confirms it.

## M0: Project setup
- [x] Unity project created, Git initialized, Unity .gitignore (Library, Temp, Logs, Obj excluded), GitHub remote `origin`
- [x] Folder structure from Section 3
- [x] Empty test scene (`Assets/_Project/Scenes/TestScene.unity`)
- [x] `PROGRESS.md`, `DECISIONS.md`, `HUMAN_TODO.md`, `BLOCKERS.md`, `DAILY_REPORT.md` created

## M1: Battle simulation core
- [x] `UnitDefinition` ScriptableObject and plain C# unit data
- [x] Damage and armor math, with tests
- [x] Optional directional defense, with tests
- [ ] Free continuous-2D movement with collision and separation, with tests
- [ ] Fixed-timestep simulation in plain C# with closest-enemy targeting, with tests
- [ ] Headless runner that simulates 3 placeholder units against each other and prints results
