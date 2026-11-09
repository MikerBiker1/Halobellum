---
title: Match flow
group: Mechanics
icon: clock
order: 10
blurb: The phases of a match, the starting draft, and what happens before, during and after each battle.
---

## The phases

1. **Draft**: once, before round 1. Pick 1 of up to 5 starting cards.
2. **Cards**: from round 2. Take 1 of up to 3 cards, or skip for a credit bonus.
3. **Deployment**: no timer. Unlock, buy, place, turn, sell, level up, buy techs, use the towers, and place spells and items. It ends when you press **Ready**.
4. **Battle**: both armies fight on their own for up to 90 seconds.
5. **Result**: base damage, experience and income, shown in a round summary window.
6. Back to step 2 for the next round, until a base falls.

The AI picks its card and deploys at the start of the round, **before you act**, using only what it has already seen of you (see [The AI](#/page/ai)).

## The starting draft

In **Faction mode** you're offered 5 starting cards. Each shows a faction, a sub-faction and **2 free starting squads**.

- The 5 cards always show 5 different sub-factions out of 8, so a whole faction can be missing from a draw.
- The 2 squads are 2 different **tier 1** unit types from that sub-faction's roster, worth **300 credits, give or take 50**, together.
- A richer card means a weaker base: **2 base health per credit away from 300**. A 250 card gives 2,100 health, 300 gives 2,000, 350 gives 1,900. The card shows both.
- The chosen units start unlocked. The 2 squads are free and appear in the middle of your zone.
- **The draft is blind**: the AI draws its own 5 cards and picks without seeing yours, so mirror matches can happen.
- After both sides pick, each sees the other's faction and its 2 starting squads as grey ghosts.

Free draft squads can be moved, turned or sold in round 1, and they don't use up one of the round's squad purchases. Selling one always returns **50%** of its cost.

> **Note:** In **Unlock Skirmish** every starting card is a pair of random tier 1 squads from all factions, and base health moves by 5% of the chosen base health per 50 credits instead.

## Deployment

- Round 1 starts with **200 credits**. See [Economy](#/page/economy) for income.
- New squads appear at the free spot nearest the middle of your zone. Move them with **M** and turn them with **R**.
- **Only squads bought this round can be moved or turned.** Squads that have fought stay where they are, unless they carry the Deployment Module item.
- **Undo** (the button under Ready, or Ctrl+Z) takes back this round's actions one at a time: unlocks, buys, sells, moves, turns, level-ups, techs, tower purchases, research, items, training and spells. It stops at the round's card choice.
- While deploying you see the enemy's army **as it was at the start of the last battle**, as grey ghosts. Click one to read its stats, level and techs. You never see what the enemy is deploying this round.

## The battle

- The simulation runs at **30 ticks per second** from a fixed seed, so the same armies and seed always play out the same way.
- Controls: **Pause, 1×, 2×, 4×** (Space pauses, keys 1, 2 and 3 pick the speed). Esc opens the pause menu.
- A battle ends when **one side has no units left** or after **90 seconds**. Towers don't count as units. A body that is about to rise as Flood does.
- Shots already in the air still land, for up to 10 more seconds. If they leave nobody alive on either side, the round is a draw: no base is hit, and income is still paid.

## The result

- **A time-out isn't special.** Whether the battle ended in a wipe or at 90 seconds, every body still alive deals its **base damage** to the enemy base. There's no extra base damage for losing, but the kills you gave up become the enemy's experience.
- Base damage is set per unit. The usual value is **(squad cost ÷ squad size) ÷ 5** per body: a 300-credit single tank deals 60, and each body of an 18-strong 100-credit squad deals about 1.1. Every unit page shows its value.
- Temporary bodies (dropped troops, released Flood forms, risen infected units, spell squads) count too if they're alive at the end.
- Each survivor sends a node of light to the enemy base: small ones first, then bigger ones, then each Titan alone.
- The **round result window** shows both sides' base damage, kills, next round's income, and which squads can level up. Its ribbon is green if you dealt more base damage than you took **that round**, red if less, and grey if equal.
- When a base falls, the ribbon reads **Victory** or **Defeat**, with buttons for **Battle stats**, the battle logs, a new match and the main menu.

## Between rounds

- Every battle is built fresh from both deployments. Every squad goes back to where it was placed, at full health, and keeps its **level and experience**.
- Destroyed units come back. Anything temporary (infected units, dropped or spawned units, spell effects) is gone.
- Towers are rebuilt for every battle, and their levels last for the match.
