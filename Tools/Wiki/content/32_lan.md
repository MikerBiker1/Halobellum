---
title: LAN Game
group: Play
icon: web
order: 32
blurb: Play one against one with another person on the same local network.
---

## Hosting and joining

**LAN Game** on the main menu plays a match against another person on the same network (the same router or Wi-Fi), each on their own PC.

- **Host game:** type your name, choose **Faction mode** or **Unlock Skirmish** (with its base health and cards), then press **Host**. The panel shows the address others can join at, for example `192.168.1.5 (PC-NAME)`. When the other player's name shows, press **Start**.
- **Join game:** type your name. Games on the network show in the list after a second or two; pick one. If yours is not listed, type the address shown on the host's screen and press **Join**.
- The first time someone hosts, **Windows Firewall** asks whether to allow the game. Allow it on **Private networks**, or the other PC cannot connect. If the join list stays empty, typing the address usually still works.
- Both PCs need the same version of the game, with the same unit stats files. If anything differs, the games refuse each other and say what differs.

## The match

- The host plays the first side and the guest the second. Each sees their own army at the bottom of the screen in blue and the opponent's in red; the guest's view of the field is turned around.
- Both pick a starting card at the same time. Each round both take or skip their card and deploy at the same time, without seeing the other's new squads, as against the AI. The top bar shows what the other player is doing.
- When both have pressed **Ready**, the battle plays on both screens. Battle speed is your own, but there is no pause in a LAN battle.
- **Esc** opens the menu without stopping the match. **Leave match** forfeits.
- If the other player leaves or the connection drops, a screen says so and takes you back to the menu.
- When the match is over, press **New game** to play again with the same player, without connecting again. The next match starts once both have pressed it, with the same settings and a new seed; if the other player goes back to the menu instead, the screen says so.

## Staying in step

The two games never send units over the network, only each round's decisions. Both run the same rules on them, and before every battle they check that their matches are identical. If they ever differ, the match stops with an **Out of sync** screen. After each battle the host's result counts; if the guest's own battle somehow ended differently, the guest's result screen says so.

Not included: playing over the internet, more than two players, rejoining a dropped match, chat.
