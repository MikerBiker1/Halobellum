# M11 Stage 4: menu and match flow

A player can now host or join a LAN game from the main menu and play it in the Match scene. The menu is a thin view over an engine-free lobby. The Match scene runs the match through the lockstep coordinator. Single-player (skirmish, tutorial, replays, menus) works as before.

## Main menu: LAN Game

The main page has a new item, **LAN Game**, between Play and Settings. Its page has **Host game**, **Join game** and **Back**, and a panel on the right.

- **Your name** is in both panels. It is kept in PlayerPrefs (`GameSettings.LanPlayerName`). It can't be changed while a connection is open.
- **Host game** opens the host panel: the name, the mode (Faction or Unlock Skirmish) and, in Unlock Skirmish, the base health and the cards. These are the LAN match's own values. The Play page's saved base health and cards are not touched.
- **Host** starts hosting: TCP 47810 for the guest and a UDP announcement on 47811 every second. The name is fixed from then on. The panel shows "Waiting for a player...", then "Guesty joined. Press Start to begin." While it waits, a line under the status says where to join: "Others can join at 192.168.1.5 (PC-NAME)." (`LanLobby.JoinAt`, from `LanAddresses`: the IPv4 addresses of the cards that are up and the computer name; "Only this PC can join, at 127.0.0.1." for a loopback-only host, with the port when it is not 47810). Once a player has joined, the game stops announcing itself, so it no longer shows in join lists. If that player leaves, it announces again. **Start** works once someone has joined. **Cancel** stops hosting, tells a joined player and keeps the panel, with Host again.
- **Join game** lists the games heard on the network (it refreshes every frame; a game drops off after 4 s of silence). A click on a game joins it. Or type an address (an IPv4 address or a computer name, with or without ":port") and press **Join**. The last address is kept. While nothing is heard the list says "No games found yet. Looking..."; after 5 s (`LanLobby.NoGamesHintMs`) it says "No games heard yet. Windows Firewall on this PC may be blocking the list: allow Halobellum on this PC, or type the address shown on the host's screen." (`LanLobby.NoGamesText`). The status line says what happens. When the host presses Start, this game follows by itself.
- Refusals are shown in plain words, for example "The host refused: The other game is version 0.416.2; this one is 0.416.1." or "The host refused: Unit stats differ: modded, Marine. Both games need the same unit stats files."
- If no connection could be made, the status adds: "If the game is hosting there, Windows Firewall on that PC may be blocking it: allow Halobellum there, or check the address." If a connection was made and the host then ended it (it stopped hosting), the hint is not added.
- **Leave** leaves a host joined and stays on the list. **Close** (nothing joined, or the join failed) closes the panel.
- On the host, a refused player shows as "A player could not join: ..." and the host keeps waiting. A player refused again later is shown again, even for the same reason. If a player leaves before the start (even in the same moment its handshake passed), the host says so and waits on a fresh listener.
- Names and texts from the other game (its name, the games heard, a refusal) are cleaned before they show: one line, and not too long (`LanIdentity.CleanName`, `CleanText`).
- Esc or Back leaves the page. A game hosted or joined from there is closed, and the other game is told.

### The lobby (engine-free)

`Assets/_Project/Scripts/Net/Lobby` (assembly `Halobellum.Net`):

| File | What it holds |
|---|---|
| `LanLobby.cs` | `LanLobby`, `LanLobbyState`, `LanLobbyOptions`: hosting, browsing, joining, refusals, Cancel, and handing the connection to the match (`TakeChannel`) |
| `LanBufferedChannel.cs` | a channel with messages put back in front: the guest's lobby reads the host's MatchStart in the menu, then hands the session over with that message put back for `LanGuestStart` |
| `LanIdentity.cs` | the code fingerprint (build ids of Core, Combat, Economy and Net), the content text of the unit stats files (`modded=no`, or `modded=yes;Marine=...;...` so a refusal names the units), and the name rules |
| `LanDemoArgs.cs` | `-lanDemo`, `-lanDemoSeed`, `-lanDemoMatches`, `-lanDemoAddress`, `-lanDemoNetwork`, `-windowSlot` and the window slot rectangles |

States: `Idle`; host: `HostWaiting`, `HostReady`, `HostFailed` (the port is in use, or the listener stopped accepting players by itself: "Hosting stopped: ..."; it tries again every 2 s); join: `Browsing`, `JoinConnecting`, `JoinWaiting`, `JoinFailed`, `GuestStarting`; then `Started` once the connection is handed over. Call `Update()` every frame.

The handshake in the menu checks the protocol, the version, the code fingerprint and the unit stats files. The whole content (every unit, card and rule after the setup) is checked again when the match starts (`LanGuestStart`).

`LanGameIdentity.Hello(name)` (Presentation) builds this game's `LanHello` from `Application.version` and `UnitStatOverrides.Active`.

## GameFlow hand-over

```csharp
public sealed class LanLaunch { ILanChannel Channel; Team LocalTeam; LanMatchSettings Settings; string OpponentName; bool Demo;
                                int DemoMatches; int? DemoSeed; int MatchNumber; bool IsHost; }
GameFlow.StartLanMatch(launch);   // keeps it and loads the Match scene
GameFlow.SetLanLaunch(launch);    // keeps it only (tests)
GameFlow.TakeLanMatch();          // the Match scene takes it once
```

- The host's launch has Team A, the settings (seed and skirmish setup) and the session. The guest's has Team B and the session with the host's MatchStart put back. The Match is built in the Match scene, because only that scene has the factions and rules the views know.
- `NewMatch`, `NewSkirmish` and `StartTutorial` drop (and close) a launch nobody took. A launch replaces any pending setup or tutorial.
- New game hands a new launch over the same open connection (`MatchNumber` one more, the same team, name and demo values; the host with the next settings, the guest with none) and loads the Match scene again through `StartLanMatch`, as the menu does.
- `-lanDemo` counts as a test run (`GameFlow.IsTestRun`): the saved settings are held in memory, so two copies on one PC don't overwrite each other's. `-windowSlot` counts as setting the window from the command line.

## The LanMatch driver

`Assets/_Project/Scripts/Combat/Presentation/LanMatch.cs`. `MatchController.Start` adds it only when `GameFlow.TakeLanMatch()` returns a launch. Meanwhile an ordinary match stands in, and the HUD shows "Starting the LAN match" over it.

- **Start.** The host calls `LanMatchStart.BeginAsHost` with `PrepareFactions()`, `FreshRules()` and `CombatRules`. The guest polls `LanGuestStart`. Both call `MatchController.UseMatch(match, team)`.
- **Every frame.** `coordinator.Update()`. When the other side's starting card ends the draft, it calls `OnRemoteCommandsApplied()`. When `CanStartBattle` is true, it calls `StartBattle()` and `MatchController.ShowStartedBattle(battle)`.
- **Starting card and Ready.** `MatchController.ChooseDraft` and `Ready` go through the coordinator in a LAN match, never to the Match. After Ready, `ActionsLocked` is true until the battle. Every action fails with "You are ready. Waiting for the other player.", and `IsDeploying` is false, so the field takes no clicks.
- **End of the battle.** When the round ending has played, `MatchController` calls `LanMatch.FinishLocalBattle()` instead of `Match.FinishBattle()`. The host finishes at once. The guest finishes when the host's result is here. The result screen shows when the coordinator raises `BattleFinished` (`MatchController.OnLanRoundFinished`).
- **Continue** calls `coordinator.ContinueAfterResult()`.
- No AI plays in a LAN match. Battle speed stays each player's own, but there is no Pause: the battle bar has no Pause button and Space does nothing (`BattleController.PauseAllowed` is false, so a speed of 0 is ignored, and one already set goes back to 1x). A pause on the host would hold up the guest's result with no limit.
- **Leaving.** `Leave()` tells the other game and closes the connection. It runs on Main Menu, on quitting, and whenever the scene is unloaded (`OnDestroy`). A guest whose match is still starting (no coordinator yet) sends Leave itself, so the host sees "The other player left", not a lost connection.
- **New game.** `RequestRematch()` (the match-over button) asks the coordinator to send `Rematch`. Once both games asked (`RematchAgreed`), `StartRematch` takes the channel (`TakeRematchChannel`), builds the next `LanLaunch` and calls `GameFlow.StartLanMatch`. The host's next settings keep the mode, base health and cards with a new seed (`LanMatchSettings.NextSeed`: `-lanDemoSeed` + k in a demo given a seed, else a fresh one). The guest's launch has no settings, so its new scene waits for the host's `MatchStart` with `LanGuestStart`. From then on the old driver does nothing: its `Update` returns at once and its `Leave` (Main Menu, quitting, `OnDestroy`) neither sends nor closes, and the coordinator's `Leave` does nothing either. The only other close of a launch's channel, `GameFlow.ForgetLan`, closes a launch nobody took; the new one is taken by the new scene. The new match logs `[LAN] rematch <k>` (1 for the second match on the connection).

### What the HUD shows (MatchHud)

- The draft's top bar and the deployment top bar name the other player and what it is doing: "Guesty: picking", "deploying", "ready", "in battle", "battle over". It comes from the other game's messages (`LanMatchCoordinator.OpponentStep`: its starting card, its Ready, and its Status when its battle is over and when it leaves the result), so it stays right when the two games are a round apart.
- After this side's starting card, and after Ready, a panel says "Waiting for opponent..." with the other player's line. Ready is greyed out after it was pressed, and the shop and the other controls are hidden until the battle. An unlock page left open when Ready is pressed (Unlock Skirmish) closes.
- After a battle the guest shows "Waiting for the host's result..." until the result arrives. Its base bars keep the round ending's hits meanwhile, and do not fill back up.
- When the guest's own battle ended differently (`HostResultUsedThisRound`), its result window says "Your game's battle ended differently; the host's result is used." under the title. The round line then always gives the host's base damage (never "both armies fell"), and the survivors and the two lists say they are "this game's battle".
- End screens (title, text, Main Menu button):
  - "The other player left"
  - "Connection lost"
  - "Out of sync", with the two state hashes that differed (before the battle, or on the guest after the host's result). For a desync of another kind (a command that failed, a broken message, the other game's own finding) the hash line is left out, since the last compared pair agreed; the text says what went wrong.
  - "The match could not start" (the guest's start was refused or lost, or setting up the match failed with an exception on either side; then the other game is told with Leave and the log says `[LAN] ended StartFailed ...`)
- Match over in a LAN match has **New game** and **Main Menu** (no battle log; Battle stats as before), with a line above the buttons:
  - "Waiting for Guesty..." after this player pressed New game (the button is then greyed out);
  - "Guesty wants a new game." when the other player pressed it first;
  - "Guesty went back to the menu." once the other player left or the connection dropped; New game is greyed out.
  When both have pressed it, the Match scene loads again with the next match on the same connection. Main Menu leaves the match first (the other game is told) and goes to the menu. Single-player's own match over (New match, Open battle logs, Main Menu) is unchanged.

### Pause menu

- In a LAN match, Esc (or the Menu button) opens the menu without stopping time (`GamePause` is not used).
- The title says "MENU".
- The third button is **Leave match**: "Leave the match?" / "Leaving forfeits the match." Yes sends Leave and goes to the main menu. Quit Game asks the same way.
- Once the match has ended (the other player left, the connection was lost, out of sync, could not start) or is over, nothing is forfeited: the button is "Quit to Main Menu", with "Leave for the main menu?" / "The match has ended."

## Test flags

- `-lanDemo host`: the menu opens the LAN page, hosts with the default settings (Faction mode), and starts as soon as a guest joins.
- `-lanDemo join`: the menu joins 127.0.0.1, or `-lanDemoAddress A`. If the host is not there yet, it tries again every 2 s. It does not listen for announcements.
- `-lanDemoNetwork`: a demo host normally listens on 127.0.0.1 only, so Windows Firewall does not ask; with this flag it listens on every network card, for a demo across two PCs.
- In both, the Insane AI plays this side. It picks the starting card through the coordinator. It takes its card and deploys on the Match for its own team (as `AiSeat` in `LockstepTestKit`), then presses Ready. Battles run at 4x, the fastest speed a player can pick. The game quits 3 s after the match ends, or after a final state.
- `-lanDemoSeed N`: the host's seed. With `-lanDemoMatches`, the k-th new game uses N + k.
- `-lanDemoMatches N` (default 1): play N matches back to back through New game, both sides pressing it 1.5 s after each match is over, then quit as above after the last one. Give both copies the same N; if the other side goes back to the menu (its last match, or it quit), this one quits too. Each new match logs `[LAN] rematch <k>` on both sides.
- `-windowSlot 0|1`: a window half the screen wide. Slot 0 is on the left and slot 1 on the right. The height is the screen height less 120 px. Only in a built game.
- Every LAN match writes to Player.log:
  - `[LAN] round <n> hash <state hash before the battle> result <outcome hash used> own <this game's own outcome hash> baseA <x> baseB <y>` (one line per round). On the guest, `result` is the host's and `own` its own battle's, so a guest battle that ended differently makes the two logs' lines differ.
  - `[LAN] match over winner <A|B>`, or `[LAN] ended <state> <reason>`
  - `[LAN] rematch <k>` when the k-th new game starts on the same connection (New game)
  The coordinator's own lines start with `[LAN]` too, so compare only the `round`, `match over` and `ended` lines. Those other lines hold each side's command lists (sent, and the other side's as applied) and the host's result text, for looking into a desync.

### Two copies on one PC

From the test build's folder (Stage 5 makes it):

```
"Halobellum Test.exe" -lanDemo host -lanDemoSeed 23 -windowSlot 0 -logFile host.log
"Halobellum Test.exe" -lanDemo join -windowSlot 1 -logFile guest.log
```

Give each copy its own `-logFile`, because both would otherwise write the same Player.log. Then compare the logs:

```
grep -E "^\[LAN\] (round|match over|ended|rematch)" host.log
grep -E "^\[LAN\] (round|match over|ended|rematch)" guest.log
```

For several matches in a row on one connection, add `-lanDemoMatches 3` to both lines.

The `round` lines and the `match over` line must be the same in both logs. The first time a copy hosts, Windows Firewall may ask. Loopback (127.0.0.1) works without permission.

## Tests

- `dotnet test Tools/SimTests --filter "FullyQualifiedName~LanLobbyTests"` (17 tests, over loopback with ports the system picks):
  - a listener that stops accepting by itself is started again, and a guest can then join with a typed "127.0.0.1:port";
  - the host's "Others can join at" line, the address and port reading, and the join list's hint after 5 s with nothing heard;
  - a host is listed, joined and started, and the guest builds the same match from the put-back MatchStart without losing the host's first card;
  - a guest of another version is refused in plain words and the host keeps waiting;
  - other unit stats name the units;
  - a guest who leaves puts the host back to waiting, and the next guest can join;
  - the host stopping tells the guest;
  - no host gives the firewall hint, and the host stopping during the handshake does not;
  - a guest refused again is shown again after another player left;
  - a full host stops announcing, and announces again when the player leaves;
  - names from the network are cleaned;
  - a port in use is tried again;
  - the content text and the name;
  - the demo flags and window slots;
  - the log lines;
  - the buffered channel.
- `LanMatchCoordinatorTests.AMatchStartTheGuestCannotBuild_IsRefused_InsteadOfWaitingForEver`: an exception while the guest builds the match refuses it, and the host is told.
- `LanMatchCoordinatorTests`: the other player's step follows its messages across a round apart; a Status that makes no sense is ignored; the log keeps every command list and the result; a state that differs after the host's result keeps the two hashes that differed; the guest's own outcome hash shows on its round line when its battle ended differently.
- `LanConnectionTests`: an accept that fails for a passing reason keeps the host accepting; one that fails for good says so.
- EditMode `LocalTeamTests`: the result texts when the host's result was used.
- EditMode `Tests/EditMode/Unity/LanMenuTests.cs` (8 tests):
  - the LAN page hosts, sees a guest, and Start leaves for the match as Team A with the guest's name and the host's settings;
  - the host's settings follow the mode, and the Play page's saved base health and cards are not touched;
  - the host panel hosts only on Host; Cancel stops hosting and keeps the panel, so the name can change;
  - Esc and Back stop hosting;
  - the join panel listens for games;
  - no host shows the firewall hint, and Close after a failed join closes the panel in one press;
  - GameFlow keeps one launch, which can be taken once;
  - `-lanDemo` is a test run and `-windowSlot` sets the window;
  - this game's Hello.
- `LanRematchTests` (16 tests, engine-free): New game over the memory pair and loopback TCP; see `lockstep.md`.
- PlayMode `Tests/PlayMode/LanFlowSceneTests.cs` (4 tests):
  - The host's lobby takes an in-process engine-free guest, and GameFlow starts the Match scene with a two-humans match as Team A. The guest's `LanGuestStart` builds the same seed. Both pick their starting card (the host through the controller, which waits for the guest's). Both press Ready (the host is then locked). The battle starts in the scene with the agreed hash. The pause menu does not stop time. The round finishes: the guest waits for the host's result, both reach the same state and outcome hash, and round 2 begins. The pause menu says leaving forfeits the match, and no longer once the match has ended. The guest leaving gives the host's end screen.
  - New game: a match made to end in a round or two (the guest's squads gone and its base nearly empty, the same on both games) reaches match over on both; the host presses New game ("Waiting for Guesty..."), the in-process guest wants one too and takes its channel, and the host's Match scene loads again: a new two-humans match as Team A with a new seed, the same mode, on the same open connection (the old scene's teardown did not close it), `[LAN] rematch 1` in the log, and the guest builds the same match from the host's next MatchStart and both reach round 1 with the same state hash.
  - A start that fails with an exception (`LanMatch.FailStart`) ends the host's match with "The match could not start", logs `[LAN] ended StartFailed ...`, and the other game is told.
  - An in-process host starts. The guest's lobby follows it, and the guest's Match scene builds the same match as Team B from the put-back MatchStart, with the camera turned. Both reach round 1 with the same state hash.

## Left for later

- The real two-copy run with `-lanDemo` needs a test build (Stage 5). It was not run here.
- The LAN page's layout was checked only by the numbers (it fits the 1120 x 882 menu). It was not looked at on screen.
- The guest's own battle end (its round ending, then waiting for the host) runs in the scene only on the guest's machine. The PlayMode test plays the guest's battle in the engine-free coordinator.
- The battle bar and the squad window still use the team names (Stage 3 note).
- The host panel's card list (its clicks and the double-click guard) runs only with IMGUI on screen; no test presses it.
- A guest whose connection drops in the same poll as its handshake passes is handled (the host listens again), but no test makes that timing happen.
