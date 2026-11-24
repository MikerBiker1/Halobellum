# Trying the LAN match (Milestone 11 test build)

The test build is in `E:\AI\_hbci\ws_lan_build` (`Halobellum.exe`). It is built from the local branch `m11/lan` and keeps its settings apart from your own game.

## On this PC: watch two AI players

Two windows side by side, the AI playing both sides, a whole match, then both close:

```
Halobellum.exe -lanDemo host -windowSlot 0 -logFile host.log
Halobellum.exe -lanDemo join -windowSlot 1 -logFile guest.log
```

Start the host first. Add `-lanDemoSeed 7` to the host for another match. The `[LAN] round ...` lines of the two logs should be the same (`own` is each game's own battle).

## On this PC: play both sides yourself

Start `Halobellum.exe` twice. In the first: **LAN Game → Host game**, then **Host**. In the second: **LAN Game → Join game**, and pick the game in the list, or type `127.0.0.1`. When the guest has joined, press **Start** on the host.

## On two PCs

1. Copy the whole `ws_lan_build` folder to the second PC. Both PCs need the same build.
2. Both PCs on the same network (same router or Wi-Fi).
3. On the host PC: **LAN Game → Host game → Host**. The first time, Windows Firewall asks whether to allow Halobellum. Allow it on **Private networks**. The host panel shows the address others can join at, for example `192.168.1.5 (PC-NAME)`.
4. On the other PC: **LAN Game → Join game**. The game shows in the list after a second or two. If it does not, type the address from the host's screen and press **Join**. If the list stays empty, the second PC's firewall is probably blocking the announcements. Typing the address still works.
5. On the host: **Start** once the guest's name shows.

## What to check

- Both pick a starting card, then each round take or skip a card and deploy at the same time.
- The guest sees their army at the bottom, in blue, and the host's in red.
- After both press Ready, the battle looks the same on both screens and the result numbers agree.
- Esc opens the menu without stopping the battle. **Leave match** forfeits.
- Closing one game: the other shows "The other player left" or "Connection lost" and goes back to the menu.

## Not in this build

Internet play, more than two players, rejoining a dropped match, chat, a turn timer. The guest's terrain is not turned: the far side's props are in front of the guest's camera.
