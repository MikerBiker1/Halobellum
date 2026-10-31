# Things the human needs to do

Exact steps for anything an agent cannot do (editor clicks, importing assets, decisions). Newest at the bottom. Tick items when done.

## 2026-10-31: check M0 and M1 (a milestone is only done once you confirm it)

- [x] **Let Unity compile.** Click into the open Unity editor and wait for the spinner in the bottom-right corner to stop. The Console (Window > General > Console) should show no red errors.
- [x] **Run the tests.** Window > General > Test Runner > **EditMode** tab > **Run All**. Expected: 113 tests, all green.
- [x] **Run the headless battle.** Menu bar: **Halobellum > Headless Runner > Run Placeholder Round Robin**. The report appears in the Console (click the log line to see all of it), and a CSV file is written to `E:\AI\Halobellum\BalanceReports\`.
- [ ] **Optional: try a custom matchup.** **Halobellum > Headless Runner > Open Runner Window**. Under Army A > Squads press **+**, drag a unit asset from `Assets/_Project/Data/Units/Placeholder` into the slot and set the squad count; do the same for Army B; press **Run**.
- [x] **Review DECISIONS.md D1 to D9.** These are rules the Bible leaves open, which I filled in so the simulation could run. For each one, reply "approve", or say what you want instead.
- [x] **Optional (D4, arena size):** if you want the arena to match Mechabellum, show me the Mechabellum folder or tell me the size, and I will propose a value.
- [x] **Confirm M0 and M1** so M2 can start in a later session.

## 2026-10-31: after your D1 to D9 review

- [x] **D10 (square size):** D4 is in squares, so I set one square to 2 m (details in DECISIONS.md). Reply "approve", or give another size. Changing it later only means editing `squareSize` in `Assets/_Project/Data/CombatRules.asset`.
- [ ] **Optional re-check:** in Unity, Test Runner > EditMode > Run All should now show 123 tests, all green.

## 2026-10-31: check M2 (battle in the scene)

- [ ] **Watch the battle.** In Unity open `Assets/_Project/Scenes/Battle.unity` (Project window, double-click) and press **Play**. 300 blue placeholder units fight 300 red ones.
- [ ] **Try the controls.** Pan: W A S D / arrow keys, or hold the middle mouse button and drag. Zoom: mouse wheel. Speed: the buttons at the top, or Space (pause), 1 (1x), 2 (2x), 3 (4x). Left-click a unit to open its squad's stats window (bottom-left); right-click or Escape closes it.
- [ ] **Compare with the headless runner.** When the battle ends, the Console shows `Battle finished: seed 1000, 974 ticks ...`. Then use **Halobellum > Headless Runner > Run Selected Battle Setup**: it prints `Headless result ...` with the same line.
- [ ] **Optional: frame rate on another PC.** I measured on your PC (RX 7800 XT, i7-12700KF), which is above mid-range. The FPS counter is in the top-right corner while playing. To test a built game, ask me for the build steps.
- [ ] **Answer D11 and D12** in DECISIONS.md.
- [ ] **Confirm M2** so M3 can start.
- [ ] **Optional cleanup:** `E:\AI\_hbci` is my throwaway copy of the project for batch tests and builds (it needs a short path). You can delete it any time; I will recreate it when needed.
