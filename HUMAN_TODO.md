# Things the human needs to do

Exact steps for anything an agent cannot do (editor clicks, importing assets, decisions). Newest at the bottom. Tick items when done.

## 2026-10-31: check M0 and M1 (a milestone is only done once you confirm it)

- [ ] **Let Unity compile.** Click into the open Unity editor and wait for the spinner in the bottom-right corner to stop. The Console (Window > General > Console) should show no red errors.
- [ ] **Run the tests.** Window > General > Test Runner > **EditMode** tab > **Run All**. Expected: 113 tests, all green.
- [ ] **Run the headless battle.** Menu bar: **Halobellum > Headless Runner > Run Placeholder Round Robin**. The report appears in the Console (click the log line to see all of it), and a CSV file is written to `E:\AI\Halobellum\BalanceReports\`.
- [ ] **Optional: try a custom matchup.** **Halobellum > Headless Runner > Open Runner Window**. Under Army A > Squads press **+**, drag a unit asset from `Assets/_Project/Data/Units/Placeholder` into the slot and set the squad count; do the same for Army B; press **Run**.
- [ ] **Review DECISIONS.md D1 to D9.** These are rules the Bible leaves open, which I filled in so the simulation could run. For each one, reply "approve", or say what you want instead.
- [ ] **Optional (D4, arena size):** if you want the arena to match Mechabellum, show me the Mechabellum folder or tell me the size, and I will propose a value.
- [ ] **Confirm M0 and M1** so M2 can start in a later session.
