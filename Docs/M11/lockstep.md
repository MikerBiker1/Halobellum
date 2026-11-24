# M11 lockstep coordinator (Stage 2, last part)

The engine-free code that runs one two-humans `Match` over a LAN channel by lockstep of decisions. It lives in `Assets/_Project/Scripts/Net/Lockstep` (assembly `Halobellum.Net`, namespace `Halobellum.Net`). It sits on the transport (`net_transport.md`) and on the two-humans Match (`stage1_api.md`). Nothing here knows about Unity: the Match scene (Stage 4) and the tests drive it the same way, by calling `Update` every frame on the main thread.

Tests: `dotnet test Tools/SimTests --filter "FullyQualifiedName~LanMatchCoordinatorTests"` (18 tests, about 10 s).

## Files

| File | What it holds |
|---|---|
| `ILanChannel.cs` | `ILanChannel` (the pipe), `LanMemoryChannel` (an in-memory pair for tests) |
| `LanMessages.cs` | `LanMessageTypes`, `LanFields` (message names and field names) |
| `LanMatchStart.cs` | `LanMatchSettings`, `LanMatchFactory`, `LanMatchStart` (the MatchStart message and the host's start), `LanContent` (content text for the Hello) |
| `LanGuestStart.cs` | `LanGuestStart`, `LanGuestStartState` (the guest's start) |
| `LanMatchCoordinator.cs` | `LanMatchCoordinator`, `LanMatchState` |

## The channel

```csharp
public interface ILanChannel
{
    bool Send(JsonObject message);              // false once closed
    bool TryReceive(out NetMessage message);    // still works after the close until the inbox is empty
    bool IsOpen { get; }
    string LostReason { get; }                  // "" while open
    void Close();
}
```

- `LanSession` implements it (`IsOpen` = Connected, `LostReason` = `Reason` once finished).
- `LanMemoryChannel.CreatePair(out a, out b)`: each message goes across as its JSON text. `Close()` on one end closes both ("Closed by this game." here, "The other game closed the connection." there); `Drop(reason)` closes both with the same reason (a pulled cable). `Pending`, `SentCount`.

## Building the same match on both machines

```csharp
public sealed class LanMatchSettings { int Seed; MatchMode Mode; int BaseHealth; CardFilter Cards;
                                       static From(int seed, MatchSetup setup); MatchSetup ToSetup(); Copy(); }
public static class LanMatchFactory
{
    // Writes the setup into freshRules (pass a new copy each time), plays SkirmishFaction.Build(factions) in Unlock Skirmish,
    // returns new Match(rules, combatRules, offered, seed, MatchSeats.TwoHumans). content = ContentFingerprint of the offered
    // factions with the rules after the setup.
    public static Match Build(MatchRules freshRules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions, LanMatchSettings settings, out ulong content);
}
```

`factions` are the real factions as `MatchController.NewMatch` builds them from its faction assets (`faction.ToInfo(cache, cardSources)`), and `freshRules` is `matchRules.ToRules()`. This is the same sequence `MatchController.NewMatch` follows (setup, then skirmish faction, then `new Match`), with `MatchSeats.TwoHumans` in place of the AI.

**Host:**

```csharp
LanMatchCoordinator coordinator = LanMatchStart.BeginAsHost(session, freshRules, combatRules, factions, settings, log);
```

It builds the match, sends `MatchStart`, and returns the coordinator for Team A.

**Guest:**

```csharp
var start = new LanGuestStart(session, freshRules, combatRules, factions, log);
// every frame: start.Poll(); until start.State != Waiting
// Started  -> start.Coordinator (Team B), start.Match, start.Settings (show the host's settings)
// Refused  -> start.Reason (the host was sent Leave {refused: true, reason} and the channel closed)
// Lost     -> start.Reason (the connection ended, or the host left, before the start)
```

The guest builds the match with `LanMatchFactory.Build` from the host's settings, then checks with `LanMatchStart.Check(host, content, MatchStateHash.Compute(match))`: the protocol, the content fingerprint and the fresh state hash must agree. It reads one message at a time and stops at the MatchStart, so the host's starting card (sent right after) is left for the coordinator.

**Handshake content:** `LanContent.Describe(factions, rules, combatRules)` gives `"UNSC=…;Covenant=…;rules=…"` (one fingerprint per faction, by name, and one for the match and combat rules, before any skirmish setup) for `LanHello.Content`, so a refused Hello says "Unit stats differ: Covenant."

## Messages

| Type | From | Fields |
|---|---|---|
| `MatchStart` | host, once | `proto`, `seed`, `mode` (name), `baseHealth`, `cards` (name), `content` (ulong), `state` (ulong) |
| `DraftPick` | both, once | `round` 0, `index`, `commands` (`MatchCommand.ListToText`, the one `ChooseDraft`) |
| `Ready` | both, every round | `round`, `commands` (the side's whole list for the round) |
| `StateHash` | both, every round | `round`, `hash` (`MatchStateHash` once both lists are applied) |
| `BattleResult` | host, every round | `round`, `outcome` (`RoundOutcome.ToText`), `hash` (`RoundOutcome.Hash`), `state` (`MatchStateHash` after the host's `FinishBattle`) |
| `Desync` | either | `round`, `reason` (this game found the match out of sync; the other stops too) |
| `Leave` | either | `reason`, `refused` (true when the guest refused the MatchStart) |

The transport adds `Hello`, `Welcome`, `Reject`, `Ping`, `Pong`. Field names are in `LanFields`, types in `LanMessageTypes`.

## LanMatchCoordinator

```csharp
public LanMatchCoordinator(Match match, Team local, ILanChannel channel, Action<string> log = null);  // two-humans match, at the draft

// every frame
void Update();                      // read messages, apply what can be applied, notice a closed channel

// the local player's steps
ActionResult ChooseDraft(int cardIndex);   // Match.ChooseDraft(local, i) and DraftPick
ActionResult Ready();               // needs Deployment and no open card; sends the round's commands; then no more local actions this round
BattleSimulation StartBattle();     // Match.StartBattle once CanStartBattle; null before
bool FinishBattle();                // call when the local battle IsFinished; true if the round finished now (guest: false while it waits)
void ContinueAfterResult();         // Match.ContinueAfterResult
void Leave(string reason = ...);    // Leave {reason}, close, state Left
event Action<RoundSummary> BattleFinished;   // host: in FinishBattle; guest: when the host's result is applied (maybe in a later Update)

// status for the HUD (read-only)
LanMatchState State;                // Draft, Deployment, WaitingForOpponent, CheckingSync, ReadyToFight, Battle, WaitingForHostResult,
                                    // Result, MatchOver, and the final Desync, OpponentLeft, ConnectionLost, Left
bool IsFinished;                    // one of the four final states
bool LocalCanAct;                   // State == Deployment: the local player may take or skip its card, deploy, undo
bool LocalPickedDraft, OpponentPickedDraft, LocalReady, OpponentReady, WaitingForOpponent, CanStartBattle;
string LastError;                   // the desync (with both hashes), why the other player left, why the connection ended,
                                    // or the guest's "Round N: this game's battle ended differently; the host's result is used."
string LocalHash, RemoteHash;       // the last hashes compared before a battle (hex)
bool HostResultUsed; IReadOnlyList<int> HostResultRounds;   // guest: rounds whose own battle ended differently (not fatal)
bool OpponentGone;                  // the other side left after the match was over (not an error)
string StatusText;                  // a short line for the top bar
Match Match; ILanChannel Channel; Team LocalTeam, RemoteTeam; bool IsHost;
```

### Round by round

1. **Draft.** `ChooseDraft(i)` picks on the Match and sends `DraftPick`. The other side's pick waits until the local pick is in (so no one picks knowing the other's card), then `MatchCommands.ApplyAll` applies it. When both are in, the Match begins round 1 by itself.
2. **Deployment.** The local player acts on the Match with `LocalTeam` while `LocalCanAct`. `Ready()` sends `TakeCommands(local)`.
3. **Both lists in.** The other side's list is applied with `MatchCommands.ApplyAll`. Any of these is a desync: a command that fails or cannot be read, a local action recorded after Ready, or the other side's card still open. Then `StateHash` is sent.
4. **Hashes.** If they agree, the state is `ReadyToFight` (`CanStartBattle`). If they differ, the state is `Desync` and `LastError` holds both hashes. A local action recorded since this machine's hash was sent is a desync here too.
5. **Battle.** `StartBattle()` checks once more that nothing local is pending and that the state hash is still the agreed one; if not, it is a desync and no battle starts (so a machine never plays a battle with an army the other game did not agree to). Play it, then `FinishBattle()` once it is over. The host applies its own outcome and sends `BattleResult`. The guest waits for that result (`WaitingForHostResult`). It checks the result against its hash, notes if its own outcome differed (`HostResultUsed`, not fatal), applies it with `Match.FinishBattle(RoundOutcome)`, and checks the host's state hash after it (a mismatch is `Desync`).
6. **Result.** `ContinueAfterResult()`. The two machines may now be a round apart. A message for a later round (a Ready, a hash, a result) waits in a buffer by round until this machine gets there.

### Endings

- `Leave` from the other side gives `OpponentLeft` with its reason. A closed channel without a Leave gives `ConnectionLost` with `LostReason`. Messages that arrived before the close are read first.
- If the host leaves (or the line drops) right after sending a round's result, the guest still plays that battle and applies the result. Then it shows `OpponentLeft` (or `MatchOver` if a base fell).
- Once the match is over, the other side leaving only sets `OpponentGone`, with its reason in `LastError` (also when the guest's round finished with the host's last result after the host left).
- A message that breaks the protocol is a desync ("the other game sent ..."): a second starting card, a Ready for a past round, a BattleResult sent to the host, or a broken message. A message or command list that cannot be read is a desync whatever the reader throws (cut-off JSON can give IndexOutOfRange, not only FormatException); it never escapes `Update`, `Ready` or `ChooseDraft`. The other side is sent `Desync`.
- A final state keeps the channel open, so the `Desync` message is not cut off. `Leave()` closes it: during a match the state becomes `Left`; after a final state (`Desync`, `OpponentLeft`, `ConnectionLost`) or `MatchOver` the state and `LastError` stay as they are (after `MatchOver` the other game is still told).

### Rules for the Match scene (Stage 4)

- Act only for `LocalTeam`, and only while `LocalCanAct`. Pick the starting card and press Ready through the coordinator, never on the Match directly.
- Start the battle through `StartBattle()` (it returns null until both games agree). Finish it through `FinishBattle()`. Show the result when `BattleFinished` fires: for the guest, that can be a few frames later.
- Call `Update()` every frame, including during the battle and on the result screen.
- Show `StatusText` and the flags in the top bar. On a final state, show `LastError`.
- Call `Leave()` whenever the player leaves the match screen (Back to menu, quitting), also after a final state or `MatchOver`: it closes the connection and keeps the error or the result on show.

## Tests (`Assets/Tests/EditMode/Net/Lockstep`)

`LockstepTestKit` provides the test content, the start over any channel, two loopback TCP sessions, and `AiSeat`. An `AiSeat` is one machine whose own side is played by the Insane `RuleBasedAi` through the coordinator, one step per call. `LanMatchCoordinatorTests`:

- whole matches over the memory pair (seeds 23, 31, 17 in Faction mode, and 5 in Unlock Skirmish, each stepped in a seeded random order per frame: host then guest, guest then host, or only one; each must see the other side's Ready first on both machines and the guest wait for the host's result in some round; plus seed 23 in the fixed order) and over loopback TCP (seed 23): no desync, the same winner, the same agreed hash before every battle and the same state hash after every result;
- the guest builds the host's match from MatchStart (settings carried, the host's early pick not lost, the host's pick not applied before the guest's);
- the handshake refused on a content mismatch, naming the faction; the MatchStart refused on a content mismatch, with the host told;
- MatchStart reads back exactly; an unknown mode or a missing field is a FormatException; another protocol is refused;
- a guest whose own battle result is tampered with ends with the host's state, `HostResultUsed` set, and the match goes on;
- the host's last result still finishes the guest's round after the host left;
- a connection dropped mid-round gives `ConnectionLost` (memory and TCP); a Leave gives `OpponentLeft` with its reason;
- early messages: the other side's Ready before the local one, and the next round's Ready during this round's battle;
- a forged command (one acting for the host's side) gives Desync and the other game is told; states that differ give Desync on both sides with both hashes;
- a local action after the lists are applied (in ReadyToFight, or in CheckingSync) gives Desync before the battle, also a change no command made;
- broken command lists in a Ready or DraftPick, and broken BattleResult outcomes, give Desync and never an exception (also a broken Ready that arrived before the local Ready);
- Leave after Desync, OpponentLeft or MatchOver keeps that state and closes the channel; the host leaving after its last result gives the guest OpponentGone with the host's reason;
- Ready waits for the card; a match against the AI is refused.
