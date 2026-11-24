# M11 Stage 4: menu and match flow

A player can now host or join a LAN game from the main menu and play it in the Match scene. The menu is a thin view over an engine-free lobby. The Match scene runs the match through the lockstep coordinator. Single-player (skirmish, tutorial, replays, menus) works as before.

## Main menu: LAN Game

The main page has a new item, **LAN Game**, between Play and Settings. Its page has **Host game**, **Join game** and **Back**, and a panel on the right.

- **Your name** is in both panels. It is kept in PlayerPrefs (`GameSettings.LanPlayerName`). It can't be changed while a connection is open.
- **Host game** opens the host panel: the name, the mode (Faction or Unlock Skirmish) and, in Unlock Skirmish, the base health and the cards. These are the LAN match's own values. The Play page's saved base health and cards are not touched.
- **Host** starts hosting: TCP 47810 for the guest and a UDP announcement on 47811 every second. The name is fixed from then on. The panel shows "Waiting for a player...", then "Guesty joined. Press Start to begin." Once a player has joined, the game stops announcing itself, so it no longer shows in join lists. If that player leaves, it announces again. **Start** works once someone has joined. **Cancel** stops hosting, tells a joined player and keeps the panel, with Host again.
- **Join game** lists the games heard on the network (it refreshes every frame; a game drops off after 4 s of silence). A click on a game joins it. Or type an address (an IPv4 address or a computer name) and press **Join**. The last address is kept. The status line says what happens. When the host presses Start, this game follows by itself.
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
| `LanDemoArgs.cs` | `-lanDemo`, `-lanDemoSeed`, `-lanDemoAddress`, `-windowSlot` and the window slot rectangles |

States: `Idle`; host: `HostWaiting`, `HostReady`, `HostFailed` (the port is in use; it tries again every 2 s); join: `Browsing`, `JoinConnecting`, `JoinWaiting`, `JoinFailed`, `GuestStarting`; then `Started` once the connection is handed over. Call `Update()` every frame.

The handshake in the menu checks the protocol, the version, the code fingerprint and the unit stats files. The whole content (every unit, card and rule after the setup) is checked again when the match starts (`LanGuestStart`).

`LanGameIdentity.Hello(name)` (Presentation) builds this game's `LanHello` from `Application.version` and `UnitStatOverrides.Active`.

## GameFlow hand-over

```csharp
public sealed class LanLaunch { ILanChannel Channel; Team LocalTeam; LanMatchSettings Settings; string OpponentName; bool Demo; bool IsHost; }
GameFlow.StartLanMatch(launch);   // keeps it and loads the Match scene
GameFlow.SetLanLaunch(launch);    // keeps it only (tests)
GameFlow.TakeLanMatch();          // the Match scene takes it once
```

- The host's launch has Team A, the settings (seed and skirmish setup) and the session. The guest's has Team B and the session with the host's MatchStart put back. The Match is built in the Match scene, because only that scene has the factions and rules the views know.
- `NewMatch`, `NewSkirmish` and `StartTutorial` drop (and close) a launch nobody took. A launch replaces any pending setup or tutorial.
- `-lanDemo` counts as a test run (`GameFlow.IsTestRun`): the saved settings are held in memory, so two copies on one PC don't overwrite each other's. `-windowSlot` counts as setting the window from the command line.

## The LanMatch driver

`Assets/_Project/Scripts/Combat/Presentation/LanMatch.cs`. `MatchController.Start` adds it only when `GameFlow.TakeLanMatch()` returns a launch. Meanwhile an ordinary match stands in, and the HUD shows "Starting the LAN match" over it.

- **Start.** The host calls `LanMatchStart.BeginAsHost` with `PrepareFactions()`, `FreshRules()` and `CombatRules`. The guest polls `LanGuestStart`. Both call `MatchController.UseMatch(match, team)`.
- **Every frame.** `coordinator.Update()`. When the other side's starting card ends the draft, it calls `OnRemoteCommandsApplied()`. When `CanStartBattle` is true, it calls `StartBattle()` and `MatchController.ShowStartedBattle(battle)`.
- **Starting card and Ready.** `MatchController.ChooseDraft` and `Ready` go through the coordinator in a LAN match, never to the Match. After Ready, `ActionsLocked` is true until the battle. Every action fails with "You are ready. Waiting for the other player.", and `IsDeploying` is false, so the field takes no clicks.
- **End of the battle.** When the round ending has played, `MatchController` calls `LanMatch.FinishLocalBattle()` instead of `Match.FinishBattle()`. The host finishes at once. The guest finishes when the host's result is here. The result screen shows when the coordinator raises `BattleFinished` (`MatchController.OnLanRoundFinished`).
- **Continue** calls `coordinator.ContinueAfterResult()`.
- No AI plays in a LAN match. Battle speed stays each player's own.
- **Leaving.** `Leave()` tells the other game and closes the connection. It runs on Main Menu, on quitting, and whenever the scene is unloaded (`OnDestroy`).

### What the HUD shows (MatchHud)

- The draft's top bar and the deployment top bar name the other player and what it is doing: "Guesty: picking", "deploying", "ready", "in battle".
- After this side's starting card, and after Ready, a panel says "Waiting for opponent..." with the other player's line. Ready is greyed out after it was pressed, and the shop and the other controls are hidden until the battle.
- After a battle the guest shows "Waiting for the host's result..." until the result arrives. Its base bars keep the round ending's hits meanwhile, and do not fill back up.
- End screens (title, text, Main Menu button):
  - "The other player left"
  - "Connection lost"
  - "Out of sync", with both state hashes
  - "The match could not start" (the guest's start was refused or lost, or setting up the match failed with an exception on either side; then the other game is told with Leave and the log says `[LAN] ended StartFailed ...`)
- Match over has only **Main Menu** in a LAN match: no "New match" and no battle log. It leaves the match first.

### Pause menu

- In a LAN match, Esc (or the Menu button) opens the menu without stopping time (`GamePause` is not used).
- The title says "MENU".
- The third button is **Leave match**: "Leave the match?" / "Leaving forfeits the match." Yes sends Leave and goes to the main menu. Quit Game asks the same way.
- Once the match has ended (the other player left, the connection was lost, out of sync, could not start) or is over, nothing is forfeited: the button is "Quit to Main Menu", with "Leave for the main menu?" / "The match has ended."

## Test flags

- `-lanDemo host`: the menu opens the LAN page, hosts with the default settings (Faction mode), and starts as soon as a guest joins.
- `-lanDemo join`: the menu joins 127.0.0.1, or `-lanDemoAddress A`. If the host is not there yet, it tries again every 2 s. It does not listen for announcements.
- In both, the Insane AI plays this side. It picks the starting card through the coordinator. It takes its card and deploys on the Match for its own team (as `AiSeat` in `LockstepTestKit`), then presses Ready. Battles run at 4x, the fastest speed a player can pick. The game quits 3 s after the match ends, or after a final state.
- `-lanDemoSeed N`: the host's seed.
- `-windowSlot 0|1`: a window half the screen wide. Slot 0 is on the left and slot 1 on the right. The height is the screen height less 120 px. Only in a built game.
- Every LAN match writes to Player.log:
  - `[LAN] round <n> hash <state hash before the battle> result <outcome hash> baseA <x> baseB <y>` (one line per round)
  - `[LAN] match over winner <A|B>`, or `[LAN] ended <state> <reason>`
  The coordinator's own lines start with `[LAN]` too, so compare only the `round`, `match over` and `ended` lines.

### Two copies on one PC

From the test build's folder (Stage 5 makes it):

```
"Halobellum Test.exe" -lanDemo host -lanDemoSeed 23 -windowSlot 0 -logFile host.log
"Halobellum Test.exe" -lanDemo join -windowSlot 1 -logFile guest.log
```

Give each copy its own `-logFile`, because both would otherwise write the same Player.log. Then compare the logs:

```
grep -E "^\[LAN\] (round|match over|ended)" host.log
grep -E "^\[LAN\] (round|match over|ended)" guest.log
```

The `round` lines and the `match over` line must be the same in both logs. The first time a copy hosts, Windows Firewall may ask. Loopback (127.0.0.1) works without permission.

## Tests

- `dotnet test Tools/SimTests --filter "FullyQualifiedName~LanLobbyTests"` (15 tests, over loopback with ports the system picks):
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
- PlayMode `Tests/PlayMode/LanFlowSceneTests.cs` (3 tests):
  - The host's lobby takes an in-process engine-free guest, and GameFlow starts the Match scene with a two-humans match as Team A. The guest's `LanGuestStart` builds the same seed. Both pick their starting card (the host through the controller, which waits for the guest's). Both press Ready (the host is then locked). The battle starts in the scene with the agreed hash. The pause menu does not stop time. The round finishes: the guest waits for the host's result, both reach the same state and outcome hash, and round 2 begins. The pause menu says leaving forfeits the match, and no longer once the match has ended. The guest leaving gives the host's end screen.
  - A start that fails with an exception (`LanMatch.FailStart`) ends the host's match with "The match could not start", logs `[LAN] ended StartFailed ...`, and the other game is told.
  - An in-process host starts. The guest's lobby follows it, and the guest's Match scene builds the same match as Team B from the put-back MatchStart, with the camera turned. Both reach round 1 with the same state hash.

## Left for later

- The real two-copy run with `-lanDemo` needs a test build (Stage 5). It was not run here.
- The LAN page's layout was checked only by the numbers (it fits the 1120 x 882 menu). It was not looked at on screen.
- The guest's own battle end (its round ending, then waiting for the host) runs in the scene only on the guest's machine. The PlayMode test plays the guest's battle in the engine-free coordinator.
- The battle bar and the squad window still use the team names (Stage 3 note).
- The host panel's card list (its clicks and the double-click guard) runs only with IMGUI on screen; no test presses it.
- A guest whose connection drops in the same poll as its handshake passes is handled (the host listens again), but no test makes that timing happen.
