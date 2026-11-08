# The battle log (D68)

Every game you play yourself, in Faction mode or Unlock Skirmish, writes one file, unless the Battle log setting is off (Settings, D69). The game can replay them (below); they are also kept in case an AI learns from them one day.

**Where they are:** `C:\Users\Mike\AppData\LocalLow\DefaultCompany\Halobellum\BattleLogs`, or press **Open battle logs** on the screen at the end of a game. The Sandbox and the test game write none.

**To send one:** send the file. It holds only what happened in that game, and each game has its own file, named by its time and sub-factions, such as `2026-11-05_18-30-00_Faction_Noble_vs_Banished.jsonl`.

A file starts when you choose your starting card and grows a line at a time, so a game you quit or that crashes keeps all it had. A file without a `match_end` line was left early.

## What is in a file

Each line is one JSON object, and its `type` says what it is.

| `type` | When | What it holds |
|---|---|---|
| `match_start` | A starting card was chosen | Format version, the game's version (`version`), time, match seed, mode, AI difficulty, the base health rule, the card filter (`cards`), the draft cards offered and the one chosen, and both sides |
| `cards_offered` | A card screen opens | Round, kind, credits, the skip bonus, each card's name and price |
| `action` | You did or tried something | Round, what, its detail, whether it worked, credits after it, and the game's reason if it was refused |
| `battle_start` | You pressed Ready | Round, the battle's seed, and both sides as they went in, with the AI's real army |
| `battle_end` | The nodes of light have landed | Round, outcome, round winner, time, base damage both ways, both bases (they can be below zero), income, each army's results by unit and spell, what killed what |
| `match_end` | The game was played to its end | Rounds, winner, both bases |

**A side** holds its faction, sub-faction, credits, base health, bonus income, research steps, unlocked units, squads, techs, spells, cards taken, inventory and towers.

**A squad** has its id, unit, column, row, whether it is turned, level, XP and the round it was bought, and its item and where it came from if it has those. Columns and rows are in its owner's frame: 0, 0 is that side's front left.

**A spell** has its name and the rounds until it is ready, and where it was placed if it was: `x` and `y` in battlefield metres, and the angle of a line-shaped one.

**Team names:** you are `player` and the AI is `enemy`.

## The `action` names

`Unlock`, `Buy`, `Sell`, `PlaceAt` (a squad moved), `ToggleRotation`, `LevelUp`, `LevelUpAll`, `BuyTech`, `LevelUpTower`, `BuyExtraSlot`, `BuyOilZone`, `RapidResupply`, `BuyEliteRecruitment`, `BuyResearch`, `ChooseCard`, `SkipCards`, `PlaceSpellAt`, `UnplaceSpell`, `PlaceItemOn`, `UseTraining`, `Undo`.

## What to know before training on them

- **The numbers move.** A log says what happened under that day's balance. It has the unit names and not their stats, so a log from before a balance change describes a different game. `match_start` has the time, and `DECISIONS.md` has what changed when.
- **The format can change.** `match_start` has a `format` number, now 1, which goes up if a line changes meaning.
- **A replay can drift.** A log replays exactly under the balance it was played with (D106, below). After a balance change the AI may buy otherwise and battles end otherwise; the replay says where.

## Replaying a log (D106)

Start the game with the log's path, and it plays that game again in the Match scene: the same match from its seed, mode, difficulty and starting card, then each of your actions in its order, done as your clicks did them, with the AI answering as it did.

    Halobellum.exe -replayLog "C:\Users\Mike\AppData\LocalLow\DefaultCompany\Halobellum\BattleLogs\2026-11-07_13-18-09_Faction_Armored-Division_vs_Armored-Division.jsonl"

| Add | What it does |
|---|---|
| `-replayStop 4` | Stops at round 4's deployment once that round's actions are done, before its battle: look around, or play on from there |
| `-replaySpeed 4` | Battle speed (2 if not given) |
| `-replayStep 0.2` | Seconds between two actions (0.4 if not given) |
| `-replayShots <folder>` | Also saves pictures (each card screen, each deployment, each battle at 2 and 10 s, each result) and `replay.txt`, then quits |

At every battle start both armies are compared with the log's (each squad's id, unit, place, turn and level, and the credits), and each battle's outcome and both bases after it. Whatever differs is shown at the end and written to `replay.txt`, and the replay carries on regardless. Only actions that worked are done again: a refused one changed nothing. A replay writes no battle log of its own. The replay uses the log's card filter and says which version of the game played the log; logs from before 0.381.1 hold neither, so they replay with all cards.
