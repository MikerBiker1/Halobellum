# Insane playstyles (D128), 2026-11-11

The tournament tool (`Tools/Balance/Tournament`, game data `game_data_d127e.json`, 2,000 base health). `--styles` forces a playstyle.
Faction mode: 40 matches a pairing of sub-factions (1,440 matches). Unlock Skirmish: 400 matches against each difficulty. Seed 0, then seed 1 where shown.

| Insane playstyle | vs Typical Insane (Faction) | vs Hard (Faction) | vs Hard (Skirmish) | vs Normal (Skirmish) | vs Easy (Skirmish) |
|---|---|---|---|---|---|
| Typical | - | 78.8% / 79.1% | 44% / 45% | 94% / 94% | 100% / 100% |
| TowerRush | 42.6% / 43.5% | 82.9% / 85.1% | 62% / 65% | 86% / 81% | 100% / 100% |
| Synergy | 44.7% | 78.4% | 46% | | |
| Defender | 41.6% | 68.9% | 30% | | |

Decision (the owner): Faction-mode Insane stays Typical; Unlock Skirmish Insane always plays TowerRush.

Check with no `--styles` (the game's own choice by mode), seed 0: Faction Insane against Insane identical to D127's round 5 in all 3,600 matches;
Unlock Skirmish identical to the TowerRush run in all 1,600 matches (Hard 62.2%, Normal 86.0%, Easy 100%).

## Defender again, after the balance pass (D138, the owner's request)

On the D138 balance, where flankers go for towers and the AI guards a raided tower. Defender (its line just in front of its towers) against Typical Insane in Faction mode, 100 matches a pairing on two seeds; in Unlock Skirmish, Insane mirrors with Defender on each side half the time (the tournament tool's `--styles A,B` now splits a skirmish mirror), 400 matches.

| Test | Defender wins | In D128 |
|---|---|---|
| Faction mode, against Typical | 48.6% of 5,600 (47.8% / 49.4% by seed; ±1.3) | 41.6% |
| Unlock Skirmish, against Typical (mirror) | 36.5% of 400 (±4.7) | |
| Unlock Skirmish, against TowerRush (mirror) | 41.8% of 400 (±4.8) | |
| Unlock Skirmish, against Hard | 38% of 400 | 30% |

- **Faction mode:** much closer than before, now that flankers raid towers, but still not better than Typical. By the sub-faction it plays it does best with Unggoy Rebellion and Covenant-based Flood (54% each) and Lekgolo Colony (52%), and worst with Aerospace Command (44%) and UNSC-based Flood (38%). About ±4 points each.
- **Unlock Skirmish:** Defender loses to both Typical and TowerRush.
- **Kept:** Faction Insane stays Typical, Unlock Skirmish Insane stays TowerRush. No playstyle clearly beats them.
