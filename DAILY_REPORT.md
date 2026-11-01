# Daily report: 2026-10-31 (M4)

M0 to M3 are confirmed (their reports are in this file's Git history). This session completed **M4: Slice rosters and sandbox**. All criteria pass; waiting for your confirmation.

Before M4, your D17 to D20 answers went in: 200 starting credits, squads placed in the middle (bought squads drop in from the sky and are then selected), free draft squads sell for half their cost, and an exact tie is a tied round.

## Completed
- **New combat mechanics** for the slice abilities (plain C#, with tests):
  - weapons can have their own target filter (a Hornet's bombs only hit ground units);
  - bonus damage against air (Wolverine, Vampire);
  - cone splash (Flamethrower Trooper);
  - a timed mid-battle drop: a Pelican still alive at 10 s drops 6 Marines, who fall from it in the game and count for base damage if they survive.
- **24 slice units** in `Data/Units/UNSC` and `Data/Units/Covenant`:
  - every Section 8 unit with the Bible's role, tier, unlock price, squad size, cost and health;
  - shields, defense arcs and abilities set up;
  - a placeholder prefab each (capsules for infantry, boxes for vehicles, discs for aircraft, sized by collision size);
  - each unit's notes list its two best counters.
- **Factions:** the Standard UNSC and Covenant draft cards now use these rosters, and the placeholder units are only used by tests.
- **Counter web runner:** **Halobellum > Headless Runner > Run Counter Web** (also a command-line method).
  - Every unit fights every other unit in equal-cost armies of about 1800 credits, 10 battles per pair, with sides swapped every other battle.
  - It checks that each unit has a Section 7 counter winning over 70%.
- **`balance.csv`** (Section 15): one row per unit with cost, health, squad DPS against each armor type, squad health, base damage and best counters.
- **Sandbox** (`Scenes/Sandbox.unity`, also the button on the draft screen):
  - pick blue or red and place any unit from both factions (it keeps placing until you right-click);
  - move, turn or remove squads, clear a side, copy one side's army to the other;
  - both armies' costs are shown, the seed can be changed;
  - **Run battle**, then **Reset** to the same placement; **Back to match**.
- **Shared placement code:** the Match and the Sandbox use the same placement ghost and deployment grid.

## Counter web result
**24 of 24 units pass** in Unity's own run. The full matrix is in `Docs/M4_counter_web_matrix.csv`.

| Unit (role) | Best counter, win rate |
| --- | --- |
| Marine, Grunt (chaff) | Fuel Rod Grunt 100%; Grunt also loses to Flamethrower Trooper 100% |
| Flamethrower Trooper, Fuel Rod Grunt (chaff clear) | Mantis / Scorpion / Hunter Pair 100% |
| Warthog, Ghost (flankers) | Flamethrower Trooper 100% |
| Gauss Warthog, Spartan, Elite, Zealot, Jackal (Plasma Rifle) (single-target DPS) | Marine 100% (Grunt 100% too, except vs the Jackal) |
| Scorpion, Mantis (tanks) | Gauss Warthog 100%, Spartan 100% |
| Jackal (Shield), Hunter Pair (tanks) | Elite 100% / Spartan 100% |
| Archer Missile Pod, Wraith (artillery) | Hornet, Pelican, Banshee 100% |
| Hornet, Pelican, Banshee (aerial) | Wolverine 100% |
| Wolverine, Vampire (anti-air) | several ground units 100% |
| Elephant, Scarab (Titans) | Spartan 100% |

**Not met yet (Section 15 balance targets, which are not M4 criteria):**
- Average win rates across all matchups: Scarab 93%, Elephant 87% (Titans are exempt). Spartan, Scorpion and Hunter Pair 65%, Marine 64%: above the 60% target.
- At the bottom: Gauss Warthog 24%, Wolverine 25%, Warthog 29%, Ghost 30%.
- The Flamethrower Trooper is an unreliable Marine counter: it wins 30% to 60% depending on the seeds. The Fuel Rod Grunt is the reliable one.
- Most pairs end 10-0 one way, because battles barely vary between seeds (see D23).

## Test results
- Unity EditMode **252 / 252**. This includes `SliceRosterTests` (all 24 assets against the Section 8 tables, role coverage, abilities, prefabs) and `CounterWebAssetTests` (each unit's best counter still wins over 70%, real assets, about 90 s).
- Unity PlayMode **12 / 12**, including 4 new sandbox tests: place on both sides, move/turn/remove/copy/clear, run the battle (Pelican drop included) and reset.
- .NET harness 235 / 235.
- The full counter web in Unity batch mode: 24 / 24.
- **Built game:**
  - the match autoplay and the new sandbox demo (`-sandboxDemo <folder>`) ran without errors;
  - the demo battle ended with 47 blue bodies, which includes the Pelican's 6 Marines;
  - screenshots: `Docs/M4_01_draft.png`, `M4_02_shop.png`, `M4_03_sandbox_placement.png`, `M4_04_sandbox_battle.png`.
- **Not tested automatically:** placing with a real mouse in the sandbox (it is on your checklist).
- **Note:** my fast tuning tool runs on .NET, not Unity. Its matrix differs from Unity's in 4 of 625 cells (Flamethrower cone edge cases, one battle each) because of tiny floating-point differences between the two runtimes. The game and the Unity runner share one runtime, so game-runner parity is unaffected.

## Blockers
None.

## Decisions needing your approval
- **D21:** first-pass stats for everything Section 8 doesn't give (weapons, speeds, sizes, shields, arcs). They are in the unit assets and `balance.csv`. No Section 8 number was changed.
- **D22:** how the abilities work: Vampire flies; Pelican drop; 3x bonus against air; 90-degree flamethrower cone; ground-only bombs; Hunter and Jackal arcs; anti-armor weapons; which units can't shoot air.
- **D23:** the counter web method. Two questions:
  - Section 15 says counters should win 55 to 70%, M4 says over 70%: which should the balance pass aim for?
  - Should battles get more random variation, so win rates show margins?
- **D24:** placeholder units only remain for tests and the Battle test scene.
