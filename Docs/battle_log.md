# The battle log (D68)

Every game you play yourself, in Faction mode or Unlock Skirmish, writes one file. Nothing reads these files yet. They are kept in case an AI learns from them one day.

**Where they are:** `C:\Users\Mike\AppData\LocalLow\DefaultCompany\Halobellum\BattleLogs`, or press **Open battle logs** on the screen at the end of a game. The Sandbox and the test game write none.

**To send one:** send the file. It holds only what happened in that game, and each game has its own file, named by its time and sub-factions, such as `2026-11-05_18-30-00_Faction_Noble_vs_Banished.jsonl`.

A file starts when you choose your starting card and grows a line at a time, so a game you quit or that crashes keeps all it had. A file without a `match_end` line was left early.

## What is in a file

Each line is one JSON object, and its `type` says what it is.

| `type` | When | What it holds |
|---|---|---|
| `match_start` | A starting card was chosen | Format version, time, match seed, mode, AI difficulty, the base health rule, the draft cards offered and the one chosen, and both sides |
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

`Unlock`, `Buy`, `Sell`, `PlaceAt` (a squad moved), `ToggleRotation`, `LevelUp`, `LevelUpAll`, `BuyTech`, `LevelUpTower`, `BuyExtraSlot`, `BuyOilZone`, `RapidResupply`, `BuyEliteRecruitment`, `BuyResearch`, `ChooseCard`, `SkipCards`, `PlaceSpellAt`, `UnplaceSpell`, `PlaceItemOn`, `UseTraining`.

## What to know before training on them

- **The numbers move.** A log says what happened under that day's balance. It has the unit names and not their stats, so a log from before a balance change describes a different game. `match_start` has the time, and `DECISIONS.md` has what changed when.
- **The format can change.** `match_start` has a `format` number, now 1, which goes up if a line changes meaning.
- **Replaying is not promised.** The seed and your actions are there, and a match is meant to play the same from them, but nothing replays a log yet and nothing has tested that it would.
