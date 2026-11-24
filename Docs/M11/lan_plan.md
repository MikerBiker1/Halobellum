# Milestone 11: 1v1 over a LAN

Asked by the owner on 2026-11-24: "on a test build, please work on a way to make this game work 1v1 on a LAN connection. Have a plan, new main menu item, tests, etc." The work lives on the local branch `m11/lan` (worktree `E:\AI\_hbwt\lan`) and is not pushed until the owner says so.

## What a LAN match is

Two people on the same local network, each running the game. One clicks **LAN Game → Host**, the other **LAN Game → Join** and picks the host from a list (found automatically) or types its address. The host's skirmish settings (base health, card filter) and a seed are used for the match. Then it plays like a skirmish against the AI, except that the opponent is the other person:

- both pick a starting card from their own offer at the same time;
- each round both take or skip their card, and deploy, at the same time, without seeing the other's current deployment (as against the AI);
- when both have pressed Ready, the battle plays on both screens, identically;
- the result, income and next round follow as usual, until a base falls.

Each player sees their own army at the bottom of the screen. The host is Team A in the rules; the guest is Team B and sees the field turned 180 degrees.

Not in this milestone: internet play (relays, NAT), more than two players, spectators, rejoining a match after a dropped connection, chat, a turn timer.

## How it stays in sync (lockstep of decisions)

The battle simulation is already deterministic: battle-log replays (D106) and the tournaments rely on it. So the machines never send units or positions. They send **decisions**, and both run the same rules on them.

1. **One canonical match on both machines.** Both build the same `Match` from the host's seed and setup, in a new *two humans* mode (no AI): host = Team A, guest = Team B.
2. **Each player acts on their own side, live.** Every action that works (unlock, buy, move, sell, level up, tech, tower, card, spell, item, undo...) is recorded by the `Match` itself as an exact `MatchCommand` (team, ids, card index, positions as exact floats).
3. **Ready sends the round's commands.** When both lists are in, each machine applies the other side's list to that side. The two sides never touch each other's state during deployment, and in two-humans mode each team has its own squad and spell id counter, so the order in which the lists are applied does not matter.
4. **Hash check, then battle.** Both compute a hash of the whole match state and exchange it. If they agree, both start the battle; if not, the match stops with an "out of sync" message and both logs are kept.
5. **Host's result is final.** After the battle, the host sends its round result (base damage, XP per squad, tower falls) and a hash. The guest applies the host's numbers. If its own battle ended differently (a rare floating-point difference between two PCs), it says so and keeps going on the host's numbers.

At connection time the two games compare the protocol version, the game version, a fingerprint of the code and a fingerprint of the content (every unit, card, spell, item and rule after the player's own unit stats files are applied). A mismatch refuses the match and says what differs.

## The network layer

A new engine-free assembly, `Halobellum.Net` (`Assets/_Project/Scripts/Net`), built only on `System.Net.Sockets`. No packages are added.

- **Discovery:** the host broadcasts a small UDP announcement (port 47811) every second: game name, host name, version, TCP port. The Join page lists what it hears; an address can also be typed.
- **Connection:** one TCP connection (port 47810). Messages are length-prefixed UTF-8 JSON objects. Reading and writing happen on background threads; messages are handed to the game through a queue that the main thread drains every frame.
- **Heartbeat:** a ping every second; 10 seconds of silence counts as a lost connection.
- **Messages:** Hello / Welcome / Reject (handshake), MatchStart (seed, setup), DraftPick, Ready (round, commands), StateHash, BattleResult, Leave, Ping / Pong.
- **Session and lockstep:** `LanSession` runs the connection and handshake; `LanMatchCoordinator` runs one match over any channel (a real socket or an in-memory pair for tests). Neither knows about Unity.

## Stages

Each stage ends with its tests passing. Engine-free code is tested with `dotnet test Tools/SimTests` (seconds); Unity code with `unity.sh lan edit|play '<filter>'`.

1. **Two-humans Match (Economy).** Match mode without AI; draft picked per team; card offers per team (the guest can deploy while the host still has a card open); undo per team; per-team id counters kept in the undo snapshots; preview for either viewer; seen tower levels and result numbers per team; `MatchCommand` recording and `MatchCommands.Apply`; `MatchStateHash`; `ContentFingerprint`; `FinishBattle` with the host's result. Single-player behaviour, battle logs and their replays stay exactly as they are.
   *Tests:* two matches from one seed, both sides played by the Insane AI, their command lists exchanged and applied in opposite orders: identical hashes and battle results for whole matches; undo inside a list; exact spell positions; a draft and a card from each side; the host's result applied on the guest. Every existing test still passes.
2. **Network layer (Net).** Framing, connection, heartbeat, discovery, handshake, session, coordinator.
   *Tests:* frames split and joined over a real loopback socket; handshake accepted, and refused on a version or content mismatch; a lost connection reported; a whole match between two coordinators over loopback TCP, both sides played by the AI, with no desync.
3. **Each player at the bottom (Presentation, UI).** One "local team" that the Match scene and its HUD read instead of assuming Team A: shop, deployment, cards, spells, preview and ghosts, colours (own blue, opponent red), camera home turned 180 degrees for the guest, battle stats window, result and match-over texts, top squad glow, music. Single-player keeps Team A and looks exactly as before.
   *Tests:* EditMode text builders for Team B; PlayMode: the guest's camera, preview and colours.
4. **Menu and match flow.** A **LAN Game** main menu item with Host and Join panels (player name, discovered games, address field, status); `GameFlow` hands the session to the Match scene; a `LanMatch` driver links the coordinator to the Match scene: waiting for the opponent at the draft and at Ready, opponent status in the top bar, disconnect and out-of-sync screens, the pause menu that does not stop time in a LAN match and warns that leaving forfeits, match over back to the menu. Test-run flags `-lanDemo host|join` (both sides played by the AI) and `-windowSlot 0|1` (two windows side by side).
   *Tests:* EditMode for the menu's LAN page and GameFlow hand-over; PlayMode: menu → host → an in-process guest joins over loopback → both reach the Match scene with the same seed.
5. **End to end.** A test build, two windowed instances on this PC running `-lanDemo` over loopback for whole matches, checking that both logs agree round by round. A checklist for the owner to try on two PCs (Windows Firewall asks to allow the game the first time).
6. **Write-up.** DECISIONS entry, changelog and wiki page (shipped with the merge), this plan's status.

## Risks

- **Floating point between two PCs.** Same build, same instructions, but `Sin`/`Cos`/`Acos`/`Atan2` go through the Windows C runtime, which can differ by CPU or Windows version. Expected to be rare; covered by the host-authoritative result and the hash checks. Replacing those calls with exact polynomial versions would remove the risk but would shift balance slightly, so it is left for the owner to decide.
- **Windows Firewall** asks the first time the host listens; without permission the guest cannot connect. The Join page says so when a connection times out.
- **The guest's terrain** is built for a camera in the south. Turning the camera shows the far side's props up close. Stage 3 swaps the factions' halves and turns the map for the guest so both see the same composition; if that turns out costly it can follow later.
- **Two instances on one PC** share settings (PlayerPrefs); the test build keeps them apart from the owner's own game.

## Status

- [x] Stage 1: two-humans Match (`stage1_api.md`)
- [x] Stage 2: network layer (`net_transport.md`, `lockstep.md`)
- [ ] Stage 3: each player at the bottom
- [ ] Stage 4: menu and match flow
- [ ] Stage 5: end to end
- [ ] Stage 6: write-up
