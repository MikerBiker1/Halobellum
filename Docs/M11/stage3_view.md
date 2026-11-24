# M11 Stage 3: each player at the bottom

The Match scene and its HUD now read one **local team** instead of assuming Team A. In single-player it is Team A, so the game looks and plays as before. A LAN guest plays Team B and sees its own army at the bottom, with the same screens as Team A.

## The local team

`MatchController.LocalTeam` (a `Team`, Team A by default) is the single source. Around it:

```csharp
public Team LocalTeam { get; }            // Team A, or Team B for a LAN guest
public Team OpponentTeam { get; }
public PlayerState Me { get; }            // Match.Get(LocalTeam)
public PlayerState Them { get; }          // Match.Get(OpponentTeam)
public bool IsMine(Team team);
public List<DraftCard> MyDraft { get; }   // PlayerDraft for A, EnemyDraft for B
public Team PlacingTeam => LocalTeam;     // DeploymentInput and the flank zones follow it
```

`NewMatch` (single-player, the tutorial, replays, "New match") always sets Team A again.

## What follows the local team

- **Actions.** Unlock, buy, move, turn, sell, level up, level all, techs, towers, research, cards, spells, items and training all act for `LocalTeam`. Undo is `Match.Undo(LocalTeam)` and `CanUndoFor(LocalTeam)`. Against the AI that is exactly `Undo()` and `CanUndo`.
- **Cards.** `IsChoosingCards` is `Match.InCards(LocalTeam)`. In a two-humans match the phase stays Deployment while an offer is open, so the HUD shows the card screen while the local side's own offer is open. `IsDeploying` is false meanwhile. The tower and squad windows and the level-up arrows also use `IsDeploying`, so behind the card screen nothing can be pressed, as in the Cards phase against the AI.
- **Draft.** `ChooseDraft(i)` calls `Match.ChooseDraft(LocalTeam, i)` in a two-humans match. The round starts when the other pick is in too (see `OnRemoteCommandsApplied` below). The draft screen shows `MyDraft`.
- **Preview and ghosts.** `Match.BuildPreview(LocalTeam)`: the local side's squads first (live), then the other side's army as last seen (grey ghosts, `GhostTeam = OpponentTeam`). All the index mappings (selection, tech icons, level-up arrows, drops) work unchanged, because the local squads still come first. In battle the order is canonical (Team A first), and `PlacedFor` says which team a squad is.
- **Towers.** `DescribeTower` treats the local side's towers as "Your tower". The other side's level comes from `Match.SeenTowerLevel(LocalTeam, kind)` (static helper `MatchController.ShownTowerLevel`).
- **Squad window.** Own squads show XP, level-up and tech buttons; the other side's show only what is seen.
- **HUD (MatchHud).** Credits, income, squads bought, the shop, the unit bar and unlock page, inventory, item slots, training, spells, both base bars, the battle panels ("Your base" on the left), the round-end lights and numbers, the result and match-over screens. The words come from the new `LocalSideTexts` (round word, round line, base damage, bases, survivors, "Your"/"Enemy", ready to level up via `SquadsReadyToLevel(team)`, income via `IncomeFor(team)`, victory/defeat). The top bar names the AI, or "Other player" in a two-humans match.
- **Battle stats window.** `LocalTeam` is on the left as "Your army". For Team B the map is turned 180 degrees: the picture is drawn turned, and `OnMap`/`LineAt` take `turned`.
- **Battle bar (BattleHud).** The local side's living bodies are on the left.
- **Top squad glow.** Blue under the local side's leader, red under the other's.
- **Audio.** Round and game won/lost stingers, the UI faction and the game-over song follow the local side.
- **Colours.** `UnitViewManager.LocalTeam`: the local side's bodies take the own colour (blue) and the friendly health bar (green); the other side red. `BattlefieldView.LocalTeam`: the local side's squares and flank zones are blue. In a mirror match the other side's shots are pushed to red (`CombatEffectsView`).
- **Camera.** `BattleCameraController.ViewFrom(team)` sets the home view: yaw 0 and focus (0, -5) for Team A; yaw 180 and focus (0, 5) for Team B. The C key returns there. Pan, zoom and the focus limits are unchanged and work at any yaw. Single-player never moves the camera here.
- **Grid.** Team B's grid is Team A's turned 180 degrees (x and y negated). `DeploymentInput.CellUnder` does the screen-to-cell maths for either side, so a drag on the guest's turned camera lands in the right cell.

## Hand-over API for Stage 4

The guest builds its match only after the host's MatchStart arrives, so the scene starts as usual and the LAN driver swaps the match in. Call these after the scene's `Start` (one frame after loading):

```csharp
List<FactionInfo> factions = controller.PrepareFactions();   // the scene's own unit and card objects (the views know them)
MatchRules freshRules = controller.FreshRules();              // a new copy every call: the setup writes into it
CombatRules combat = controller.CombatRules;

// Host:  LanMatchStart.BeginAsHost(channel, freshRules, combat, factions, ...)
// Guest: new LanGuestStart(channel, freshRules, combat, factions), then its Coordinator once MatchStart is in
// Both end with coordinator.Match and the local team (host A, guest B).

controller.UseMatch(coordinator.Match, coordinator.LocalTeam);
```

- `UseMatch(match, localTeam)` shows the match for that side: it sets the local team everywhere (views, squares, camera), clears placements and selection, and shows the draft (or the field, if the draft is already over). It writes no battle log and sets no AI.
- `OnRemoteCommandsApplied()`: call it after applying the other side's commands (`MatchCommands.ApplyAll`). When those commands end the draft, it sets the factions' lands and shows the local army; it also raises `PhaseChanged` so the HUD redraws.
- Build the match from `PrepareFactions()`. Otherwise the views do not find the unit assets and draw placeholders.
- A test starts the scene as the guest in the same way with `LanMatchFactory.Build(controller.FreshRules(), controller.CombatRules, controller.PrepareFactions(), settings)` and `UseMatch(m, Team.B)` (see `LocalTeamSceneTests`).

## Tests

- EditMode (`Tests/EditMode/Unity/LocalTeamTests.cs`): result and match-over texts for Team A (unchanged) and Team B, a won match from both sides, seen tower levels for either side, the grid maths for both sides (`CellUnder`, 180-degree turn), the camera home, the battle bar line, the turned stats map, the opponent label.
- PlayMode (`Tests/PlayMode/LocalTeamSceneTests.cs`): the guest's camera home (yaw 180, focus and camera in Team B's half), its army live in the preview with the host's as ghosts, its own colours and health bars and squares, a squad placed through the controller and the mouse landing in Team B's half; with its own card offer open, its tower and squad windows offer nothing to press; single-player keeps Team A, yaw 0 and the old colours, also after a guest's match.

## Left for later

- **Stage 4 must still:** route Ready and the draft through the coordinator (today `Ready()` starts the battle directly when `Match.CanStartBattle`); finish the guest's battle with the host's result (`FinishBattle(outcome)`); show "waiting for the other player" at the draft and after Ready; replace "New match" in a LAN match; handle the pause menu and leaving; write a LAN battle log (none is written in a two-humans match now). *Done in Stage 4, except the battle log: a LAN match writes its decisions (each side's command lists, the host's result) to Player.log instead (stage4_flow.md, lan_plan.md "Known limits").*
- **Terrain is not turned for the guest.** The map is composed for a camera in the south. The guest sees it from the north: the shaders cut away near props, and the light comes from behind. It works but looks worse. A fix: build the guest's map with the factions swapped, turn the Battlefield root 180 degrees, and turn the sun, sky sun, sheen and blob shadow throw with it. Left as a follow-up because it touches the lighting code in several places.
- **Small labels.** The squad window still falls back to "Team B" as the side label of the guest's own squads (single-player shows "Team A"). The battle bar keeps the team names ("Team B ... Team A").
- **Single-player tools** (autoplay, the tutorial, battle-log replays and the demos) stay Team A only.
- **Hidden information** stays honest only: both machines hold the whole match, including the other side's current deployment.
