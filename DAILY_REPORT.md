# Daily report: 2026-10-31 (M4)

M0 to M3 are confirmed (their reports are in this file's Git history). This session completed **M4: Slice rosters and sandbox**. All criteria pass; waiting for your confirmation.

Before M4, your D17 to D20 answers went in: 200 starting credits, squads placed in the middle (bought squads drop in from the sky and are then selected), free draft squads sell for half their cost, and an exact tie is a tied round.

## After your D21 to D24 answers
- **D21** approved.
- **D22:**
  - The Pelican is a plain air unit until its M6 upgrade. The drop mechanic stays built and tested, just not switched on.
  - The flamethrower cone is now 15 degrees, with 3 Fire damage every 0.1 s.
  - Scorpions already lose every equal-cost fight to Hornets and Banshees: their coax does only 0.6x to aircraft.
- **D24:** placeholder units never appear in matches (a new test checks every faction) and are offered in the Sandbox under "Placeholder".
- **D23 is still open:** my suggestion is a 90% threshold (9 of 10 equal-value battles).
- Counter web rerun in Unity: still **24 of 24**.

## Completed
- **New combat mechanics** for the slice abilities (plain C#, with tests):
  - weapons can have their own target filter (a Hornet's bombs only hit ground units);
  - bonus damage against air (Wolverine, Vampire);
  - cone splash (Flamethrower Trooper);
  - a timed mid-battle drop (for the Pelican's M6 upgrade): 6 Marines fall from it at 10 s and count for base damage if they survive.
- **24 slice units** in `Data/Units/UNSC` and `Data/Units/Covenant`:
  - every Section 8 unit with the Bible's role, tier, unlock price, squad size, cost and health;
  - shields, defense arcs and abilities set up;
  - a placeholder prefab each (capsules for infantry, boxes for vehicles, discs for aircraft, sized by collision size);
  - each unit's notes list its two best counters.
- **Factions:** the Standard UNSC and Covenant draft cards now use these rosters. The placeholder units are only in tests and the Sandbox.
- **Counter web runner:** **Halobellum > Headless Runner > Run Counter Web** (also a command-line method).
  - Every unit fights every other unit in equal-cost armies of about 1800 credits, 10 battles per pair, with sides swapped every other battle.
  - It checks that each unit has a Section 7 counter winning over 70%.
- **`balance.csv`** (Section 15): one row per unit with cost, health, squad DPS against each armor type, squad health, base damage and best counters.
- **Sandbox** (`Scenes/Sandbox.unity`, also the button on the draft screen):
  - pick blue or red and place any unit from both factions, or a placeholder unit (it keeps placing until you right-click);
  - move, turn or remove squads, clear a side, copy one side's army to the other;
  - both armies' costs are shown, the seed can be changed;
  - **Run battle**, then **Reset** to the same placement; **Back to match**.
- **Shared placement code:** the Match and the Sandbox use the same placement ghost and deployment grid.

## Counter web result
**24 of 24 units pass** in Unity's own run. The full matrix is in `Docs/M4_counter_web_matrix.csv`.

| Unit (role) | Best counter, win rate |
| --- | --- |
| Marine, Grunt (chaff) | Fuel Rod Grunt 100%; Grunts also lose to the Flamethrower Trooper 100% |
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
- Average win rates across all matchups: Scarab 93%, Elephant 87% (Titans are exempt). Spartan, Scorpion, Marine and Hunter Pair 65%: above the 60% target.
- At the bottom: Gauss Warthog 24%, Pelican and Wolverine 27% (the Pelican is weaker without its troops), Warthog 29%, Ghost 30%.
- With its 15-degree cone, the Flamethrower Trooper beats Grunts but loses to Marines, which outrange it (12 m vs 11 m). The Fuel Rod Grunt is the Marine counter. A wider-cone upgrade in M6 could change that.
- Marines and Grunts also beat Hornets and Banshees, because their rifles can shoot air. Worth a look in the balance pass (Section 7 says aerial units beat ground armies without anti-air).
- Most pairs end 10-0 one way, because battles barely vary between seeds (see D23).

## Test results
- Unity EditMode **253 / 253**. This includes `SliceRosterTests` (all 24 assets against the Section 8 tables, role coverage, abilities, prefabs, no placeholder unit in any faction) and `CounterWebAssetTests` (each unit's best counter still wins over 70%, real assets, about 90 s).
- Unity PlayMode **12 / 12**, including 4 new sandbox tests: place on both sides, move/turn/remove/copy/clear, run the battle and reset, and the placeholder group.
- .NET harness 235 / 235.
- The full counter web in Unity batch mode: 24 / 24.
- **Built game:**
  - the match autoplay and the new sandbox demo (`-sandboxDemo <folder>`) ran without errors;
  - screenshots: `Docs/M4_01_draft.png`, `M4_02_shop.png`, `M4_03_sandbox_placement.png`, `M4_04_sandbox_battle.png`.
- **Not tested automatically:** placing with a real mouse in the sandbox (it is on your checklist).
- **Note:** my fast tuning tool runs on .NET, not Unity. Its matrix differs from Unity's in 4 of 625 cells (Flamethrower cone edge cases, one battle each) because of tiny floating-point differences between the two runtimes. The game and the Unity runner share one runtime, so game-runner parity is unaffected.

## Blockers
None.

## Decisions needing your approval
- **D23:** the counter threshold. I suggest a counter must win at least 9 of 10 equal-value battles (90%), replacing Section 15's 55 to 70%.
