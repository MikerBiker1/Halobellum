# Daily report: 2026-10-31

## Completed
**M0: project setup** (waiting for your confirmation)
- Git repo with a Unity .gitignore (Library, Temp, Logs, Obj and build output excluded), GitHub remote `origin`, everything pushed.
- Section 3 folder structure under `Assets/_Project` and `Assets/Tests` (empty folders hold a `.gitkeep` so Git keeps them).
- Empty test scene: `Assets/_Project/Scenes/TestScene.unity`.
- `PROGRESS.md`, `DECISIONS.md`, `HUMAN_TODO.md`, `BLOCKERS.md` and this report.

**M1: battle simulation core** (every acceptance criterion passes; waiting for your confirmation)
1. `UnitDefinition` ScriptableObject (Create > Halobellum > Unit Definition) holding every Section 6 stat, plus plain C# copies (`UnitStats`) that the simulation reads.
2. Damage and armor math: the Section 6 table and global rules live in `Data/CombatRules.asset`; shields absorb damage first.
3. Optional directional defense: front and rear multipliers, and seeded block rolls against blockable projectiles of listed damage types.
4. Free continuous-2D movement: steering, separation, sideways avoidance, contact sliding, and no overlap between bodies. A spatial hash grid handles neighbor lookups. Air units fly straight over ground units. No NavMesh and no Unity physics.
5. Fixed-timestep simulation in plain C# (30 ticks per second): closest-enemy targeting re-checked every 0.5 s, Idle/Move/Attack/Dead states, weapons, projectiles, splash, shields, the 90 s limit, an event bus and battle results. The simulation code has no Unity dependency: its assemblies are set to "no engine references", so it cannot use Unity at all.
6. Headless runner: N seeded battles between two armies, with a text report and a CSV. There are 3 placeholder units (`Data/Units/Placeholder`: infantry, light vehicle, heavy tank), marked as test data, not roster or balance.

Commits: one per item, all pushed to https://github.com/MikerBiker1/Halobellum.

## Test results
- **Unity EditMode (official):** 113 / 113 passed (Unity 6000.3.27f2, batch mode on a copy of the project).
- **.NET harness (`dotnet test Tools/SimTests`):** 105 / 105 passed. This is a developer-only fast check of the plain C# code; it leaves out the 8 tests that need Unity.
- Your open Unity editor compiled all scripts with no errors.
- Tests cover what the Bible requires for this stage (damage and armor math, directional defense, targeting selection), plus movement, the simulation loop, seeded repeatability and the runner.

**Headless run** (600-credit armies, 10 battles per matchup, seeds 1000 to 1009; about 10 s in batch mode):

| Matchup | Result | Avg length |
| --- | --- | --- |
| Placeholder Heavy Tank vs Placeholder Infantry | Tank 100% | 18.7 s |
| Placeholder Heavy Tank vs Placeholder Light Vehicle | Tank 100% | 12.1 s |
| Placeholder Infantry vs Placeholder Light Vehicle | Infantry 100% | 8.0 s |

The placeholder numbers were invented only to exercise the simulation; these results say nothing about game balance.

## Blockers
None.

## Decisions needing your approval (details in DECISIONS.md)
- **D1:** the anti-armor tag replaces the damage type row (it does not multiply with it).
- **D2:** shields take damage with the unit's base armor multiplier.
- **D3:** shield regen rate is a new per-unit data field (the Bible gives a delay but no rate).
- **D4:** arena placeholder size: 120 m x 160 m.
- **D5:** units face their target when attacking and their direction of travel when moving; squad rotation only changes the formation shape.
- **D6:** range is measured to the target's edge.
- **D7:** splash has no falloff and doesn't hit air from ground weapons; a blocked projectile loses its splash too.
- **D8:** with several weapons, each weapon picks its own target and the first weapon drives movement.
- **D9:** a runner "win" is decided by the survivors' base damage.

## Notes
- Tuning values for movement (separation, avoidance, collision passes, formation gap) are also in `CombatRules.asset`, not in code.
- 300 vs 300 performance (60 FPS) is an M2 criterion and has not been measured yet.
- M2 was not started.
