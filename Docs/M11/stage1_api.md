# M11 Stage 1: the two-humans Match (API for the network layer)

Everything here is engine-free (`Halobellum.Economy`, namespace `Halobellum.Economy`). A match against the AI is unchanged: same ids, same results, same battle logs and replays, same AI. All new behaviour sits behind `MatchSeats.TwoHumans`.

## What was added

### Seats

```csharp
public enum MatchSeats { HumanVsAi = 0, TwoHumans = 1 }

public Match(MatchRules rules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions, int seed, MatchSeats seats);
public MatchSeats Seats { get; }
public bool IsTwoHumans { get; }
public const int TeamBFirstId = 1000001;
```

- In a two-humans match no AI is ever called. `SetEnemyAi` returns false.
- Each side has its own squad and spell id counters: Team A counts from 1, Team B from `TeamBFirstId`. The counters live in `PlayerState.NextSquadId` / `NextSpellId` (public get, internal set) and are part of `PlayerState.Snapshot`, so Undo puts them back. Against the AI they stay 0 and the old shared counter is used, so ids do not change.

### Draft

```csharp
public ActionResult ChooseDraft(Team team, int cardIndex);
public bool HasChosenDraft(Team team);
public ActionResult ChooseDraft(int cardIndex);   // unchanged against the AI; fails in a two-humans match (say the team)
```

Team A picks from `PlayerDraft`, Team B from `EnemyDraft`. Each side picks once. When both picks are in, Team A's card is applied, then Team B's (the same order as against the AI). Both armies are revealed and round 1 begins (`Phase == Deployment`, `Round == 1`). Against the AI, `ChooseDraft(Team.A, i)` is `ChooseDraft(i)` and `ChooseDraft(Team.B, i)` fails. In a two-humans match `ChooseDraft(i)` (no team) fails, so a screen that does not yet know its team cannot pick the host's card on the guest's machine.

### Cards

```csharp
public bool InCards(Team team);       // two humans: Phase == Deployment && Get(team).Offer != null
public bool CanStartBattle { get; }   // Deployment, and (two humans) neither side has an open offer
```

In a two-humans match, `BeginRound` draws both offers (A, then B, with the match's random generator, as before), sets `Phase = Deployment` and never shows `MatchPhase.Cards`. A side with an open offer gets "Take or skip your card first." from every deployment action until it calls `ChooseCard(team, i)` or `SkipCards(team)`. The other side can deploy in the meantime. `StartBattle()` throws while either offer is open.

### Undo per side

```csharp
public ActionResult Undo(Team team);
public bool CanUndoFor(Team team);
public int UndoStepsFor(Team team);
public void RecordAction(Team team);
// unchanged against the AI (Team A): Undo(), CanUndo, UndoSteps, RecordPlayerAction()
// in a two-humans match: Undo() fails, CanUndo is false, UndoSteps is 0, RecordPlayerAction() does nothing
```

A side's undo history starts when its offer is resolved, or at round start if it has none. In a two-humans match the Match records an undo step itself after every deployment action that works, so the history follows from the commands alone and is the same on both machines. `RecordAction` and `RecordPlayerAction` do nothing in this mode, so a UI that still calls them does no harm.

### Views for either side

```csharp
public BattleSimulation BuildPreview(Team viewer);         // viewer A == BuildPreview()
public int SeenTowerLevel(Team viewer, TowerKind kind);    // the other side's tower level as `viewer` saw it; viewer A == SeenEnemyTowerLevel(kind)
// RoundSummary
public int IncomeAwardedB;                                 // IncomeAwarded stays Team A's (the battle log writes it)
public List<string> EnemySquadsReadyToLevel;               // PlayerSquadsReadyToLevel stays Team A's
public int IncomeFor(Team team);
public List<string> SquadsReadyToLevel(Team team);
```

`BuildPreview(Team.B)` puts the viewer's live army first: simulation squad i is `Enemy.Squads[i]`. Then come the ghosts, `SeenArmy(Team.A)` in order, then B's towers at their real levels and A's towers at the levels B saw. Each side stands in its own half. The preview is never canonical (no jitter).

### Commands

```csharp
public sealed class MatchCommand
{
    public Team Team; public int Round; public int Seq; public string Action;
    public int SquadId, SpellId, Index; public string Unit, Tech, Item; public TowerKind Tower; public bool Health;
    public int Column, Row; public bool Turned; public float X, Y, Angle;
    public string ToText();                                        // one JSON object; floats as int bits
    public static MatchCommand Parse(string text);                 // FormatException if it is not a command
    public static string ListToText(IEnumerable<MatchCommand> commands);   // one per line
    public static List<MatchCommand> ParseList(string text);
    // action names: ChooseDraftAction, ChooseCardAction, SkipCardsAction, UnlockAction, BuyAction, MoveAction, SellAction, LevelUpAction,
    // LevelUpAllAction, BuyTechAction, LevelUpTowerAction, BuyExtraSlotAction, BuyOilZoneAction, RapidResupplyAction,
    // BuyEliteRecruitmentAction, BuyResearchAction, BuyDeploymentModuleAction, PlaceSpellAction, UnplaceSpellAction, PlaceItemAction,
    // UseTrainingAction, UndoAction
}
public static class MatchCommands
{
    public static ActionResult Apply(Match match, Team from, MatchCommand command);   // from = the team the other machine plays
    public static bool ApplyAll(Match match, Team from, IEnumerable<MatchCommand> commands, List<string> failures = null);
    public static UnitStats FindUnit(PlayerState player, string name);
}
// Match
public List<MatchCommand> TakeCommands(Team team);   // returns and clears this side's recorded commands
public int PendingCommands(Team team);
```

- In a two-humans match, every team action that works is recorded into that team's list with `Round` and a per-team `Seq`. That covers every public mutating method that takes a Team, plus `Undo(team)`, `ChooseCard`, `SkipCards` and `ChooseDraft(team, i)`. `Buy(team, type)` (free spot) is recorded as a `Buy` with the column and row it found.
- `MatchCommands.Apply` does the command on its team and returns the result. It refuses (without doing anything) a command whose `Team` is not `from`, so a list from the other machine can never act for this machine's side; a command whose `Round` is not the match's round; and a command out of order: each side's `Seq` must go 1, 2, 3, ... over the whole match, so a repeated, skipped or reordered command fails. It works only in a two-humans match. `MatchCommand.Parse` throws `FormatException` for a missing or unknown tower name (numbers such as "7" too), and so does `RoundOutcome.Parse` for a tower fall. Commands applied this way are not recorded again (`TakeCommands` only holds what was done on this machine), but they do enter the side's undo history, so a later `Undo` in the list takes back the same thing.
- Units are found by name (roster first, then unlocked types), techs by name on the unit, items by their inventory index (checked against the name). Squads and spells are found by id.
- The two sides never touch each other's state during deployment, and their ids come from separate counters. So applying B's list after A's live actions, or A's after B's, gives the same state. The tests check this byte for byte with `MatchStateHash`.

### Hashes and fingerprint

```csharp
public sealed class StableHasher   // 64-bit FNV-1a: Byte, Int, Long, ULong, Bool, Float (bits), Double, Text (UTF-8 + length), Value, Hex(ulong),
                                   // FloatToBits(float), BitsToFloat(int)
public static class MatchStateHash
{
    public static ulong Compute(Match match);          // whole state: seats, phase, round, battles, winner, counters, random state, drafts,
                                                       // both PlayerStates (every field, unit types and techs sorted by name), seen armies,
                                                       // seen buffs and tower levels, and the last summary
    public static ulong Compute(RoundSummary summary); // round, base damage both ways, bases, income both ways, tie, winner, XP by id, ready lists
    public static string Hex(ulong hash);
}
public static class ContentFingerprint
{
    public static ulong Compute(IReadOnlyList<FactionInfo> factions, MatchRules rules, CombatRules combatRules);
    public static ulong Of(object content);
}
```

`ContentFingerprint` walks every public instance field by reflection, sorted by name, including weapons, techs and the units a unit brings. Cycles are cut. The damage table is read through its getters. Floats go in as their bits. Undo histories, the battle statistics window and kill lists are not part of the state hash. `Core/SimRandom` gained a read-only `State` for this.

### The host's result

```csharp
public sealed class RoundOutcome
{
    public int Round, BaseDamageByA, BaseDamageByB, Ticks;
    public List<SquadXp> Xp;              // { int SquadId; float Xp } for every placed squad, battle order (A first)
    public List<TowerFall> TowerFalls;    // { Team Team; TowerKind Kind }
    public ulong Hash(); public string ToText(); public static RoundOutcome Parse(string text);
}
// Match
public RoundOutcome OutcomeOfCurrentBattle();       // the finished battle's effect on the match
public RoundSummary FinishBattle(RoundOutcome outcome);
public RoundSummary FinishBattle();                 // unchanged: FinishBattle(OutcomeOfCurrentBattle())
```

`FinishBattle(outcome)` needs the local battle to be finished. It uses the outcome's base damage, XP (matched by squad id) and tower falls. The summary's `Battle` and `Kills`, and the battle statistics window, still come from the local battle, for display only. It throws if the outcome is for another round. (The class is `RoundOutcome` because `Halobellum.Combat.BattleOutcome` already exists.)

### Replays and logs

- `IReplayHands` has a new `Team Team { get; }`. `MatchLogReplay.Apply` acts on `match.Get(hands.Team)`. `MatchHands(Match match, Team team = Team.A)` uses `RecordAction(team)` and `Undo(team)`. The Match scene's `ControllerHands` returns Team A, so battle-log replays are unchanged.
- `MatchLog.MoveDetail` and `TurnDetail` are culture-invariant. The output is the same as before wherever the minus sign is '-'.

## How a network layer uses it, round by round

Host = Team A, guest = Team B. Both machines run the same build and content.

1. **Connect.** Compare the protocol version, game version and `ContentFingerprint.Compute(factions, rules after MatchSetup, combatRules)`. Refuse on any difference.
2. **Build.** The host chooses the seed and the setup and sends them. Both build `new Match(rules, combatRules, factions, seed, MatchSeats.TwoHumans)`. Optionally compare `MatchStateHash.Compute` now.
3. **Draft.** The local player calls `ChooseDraft(localTeam, i)`. Send `TakeCommands(localTeam)` (one `ChooseDraft` command). When the opponent's list arrives, apply it with `MatchCommands.ApplyAll(match, remoteTeam, ...)`. Once both picks are in, round 1 begins on each machine by itself (`HasChosenDraft` tells the UI whom it waits for).
4. **Cards and deployment.** While `InCards(localTeam)`, show the card screen; `ChooseCard` / `SkipCards` resolve it. Then the local player deploys live on the canonical match: all `Match` actions with `localTeam`, `Undo(localTeam)`, `BuildPreview(localTeam)` for the field, and `SeenTowerLevel(localTeam, kind)` for the other side's towers. Never call actions for the other team locally.
5. **Ready.** The local side must be out of cards. Send `MatchCommand.ListToText(TakeCommands(localTeam))` with the round. Do not take more local actions after Ready. For fairness, a hash of the list can be sent first and the list only after both hashes are in.
6. **Apply the remote list.** `MatchCommands.ApplyAll(match, remoteTeam, MatchCommand.ParseList(text), failures)`. Any failure is a desync (or a forged command), and `ParseList` throwing `FormatException` is too. Both machines now hold the same state, whatever the order.
7. **Hash check.** Exchange `MatchStateHash.Compute(match)`. If the two differ, stop the match ("out of sync") and keep both logs. If they agree, `CanStartBattle` is true on both.
8. **Battle.** Each machine calls `StartBattle()` and plays it at its own speed until `CurrentBattle.IsFinished`.
9. **Outcome.** The host calls `OutcomeOfCurrentBattle()`, sends `ToText()` plus `Hash()`, and then calls `FinishBattle()`. The guest waits for its own battle to finish, compares the host's hash with its own `OutcomeOfCurrentBattle().Hash()` (note any difference), and calls `FinishBattle(RoundOutcome.Parse(text))`. Optionally exchange `MatchStateHash.Compute(match)` again after the result.
10. **Next round.** Wait until both have seen the result, then call `ContinueAfterResult()` on each machine. Both draw the same offers, and the loop goes back to step 4 until `Phase == MatchPhase.MatchOver`.

Rule of thumb: everything that changes the match goes through a `Match` action for the local team (recorded automatically) or through `MatchCommands.Apply` for the remote team. Nothing else may change match state.
