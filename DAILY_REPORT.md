# Daily report: 2026-11-01 (afternoon), M7: Cards

This morning's report (D25 to D30) is in this file's Git history. You approved D30 and confirmed M4 to M6 by moving on to M7. **M7 is done**: all criteria pass, waiting for your confirmation.

## What M7 adds
- **Card screen:** opens at the start of every round from round 2, before deployment. It offers spells in round 2, reinforcements in round 3, then keeps alternating.
  - Pick 1 of 3 cards, or **Skip** for +50 credits (spells) or +100 (reinforcements). The button shows the bonus.
  - Each card shows its price, and you can't take a card you can't afford.
- **Spell cards** (permanent and rechargeable, Bible Section 10):
  - You pay once when you take the card, and the spell is yours for the match. It can be used in the round you take it.
  - Each spell has a maximum number of copies. Once you own the maximum, it isn't offered again, and offers can show fewer than 3 cards.
  - After use, a spell recharges for its number of rounds. A 1-round recharge means every other round, as in your example.
  - **Placing spells:** a **Spells** panel on the left during deployment, with Place, Move and X (take back). While placing, the area follows the mouse:
    - **red** for offensive spells (anywhere on the field);
    - **blue** for defensive spells and buffs (your own side only);
    - grey where the spell can't go.
  - Spells go off a moment after the battle starts. Strikes and zones flash, and drop pods fall from the sky. The enemy's spells stay hidden until the battle starts, and the AI never sees yours.
- **Reinforcement cards** (single-use):
  - Each card is a random unit from your roster at a random level. It's copied until it reaches the round's budget (200 + 50 per round), and you pay whatever it's worth above the budget. The Bible's examples are all checked by tests.
  - The squads join your army like squads bought this round, so you can move them. They unlock the unit, ignore the 2-squad and 1-unlock limits, and start with 0 XP.
- **First spells** (D31):
  - **UNSC:** MAC Strike, Archer Missile Barrage, HAVOK Nuke, ODST Drop Pods (12 ODSTs land for one battle), Bubble Shield, Battlefield Rally.
  - **Covenant:** Plasma Bombardment, Plasma Storm, Glassing Beam, Energy Shield Dome, Prophet's Blessing.
- **Result screen:** spells appear under top damage dealers and "what killed what", for example "Your MAC Strike killed 9 Grunt".
- **AI:** takes the card worth the most for its price, or skips. It aims strikes at the densest part of your army **as it was revealed last battle**, drops pods on your most valuable squad, and puts buffs on its own densest group. Easy is careless about both.

## Your D31 and D32 additions (D33, later the same day)
- **Friendly fire:** offensive spells now hit your own army too.
- **New spells:** Smoke Bomb and EMP for both factions, Banshee Ambush for the Covenant.
- **Bigger, later spells:** the HAVOK Nuke and Glassing Beam appear from round 7 and land 6 s in.
- **ODSTs:** 12 troopers, SMGs on chaff and unarmored targets, rocket launchers on everything else.
- **Card schedule:** rounds 2 and 3 offer spells, items, economy cards and buffs; reinforcements start at round 4, then every other round.
- **Items:** the 8 items, with an **Inventory** panel and hexagons over squads. New icons are in `Art/UI/Items`.
- **Economy and buffs:** Supply Enhancement; from round 5 the extra deployment slot; from round 7 Super Supply and the 3 Advanced cards.
- **Intensive Training:** an icon at the top left. Select a squad, click it, confirm.
- **Shop:** every unit shows its price, even before it's unlocked.
- **Your answers to D33 (D34):** smoke lasts the whole round, Intensive Training can be taken twice (two icons, separate cooldowns), Laser Sights is +2 m, and the Sandbox has an **Items** panel with all 8 items (no spells).

## Decisions needing your approval
None: D31 to D34 are answered. Spells stay out of the Sandbox, as you asked.

## Test results
- **Unity:** EditMode **354 / 354**, PlayMode **22 / 22**. **.NET harness:** **320 / 320**.
- **New tests:**
  - Spell effects in battle.
  - Card rules, including the Bible's pricing examples and the "every other round" recharge.
  - The spell data.
  - A scene test: a spell taken, placed (outline shown), cast and recharging, then a reinforcement card whose squads appear on the field.
  - AI-vs-AI matches with cards are played to the end.
- **Built game:** 5 rounds of autoplay as UNSC and as Covenant, plus the Sandbox demo, with no errors. Screenshots are in `Docs/M7_*.png` and `Docs/D3*.png`.
- **Not tested automatically:** placing a spell, clicking a hexagon or the training icon with the real mouse. The scene tests use the same functions the buttons call.
- **Small note:** decimals follow your PC's language (for example "2,5 s"), as elsewhere in the HUD.

## Blockers
None.
