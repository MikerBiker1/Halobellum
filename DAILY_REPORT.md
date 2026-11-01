# Daily report: 2026-11-01 (afternoon), M7: Cards

This morning's report (D25 to D30) is in this file's Git history. You approved D30 and confirmed M4 to M6 by moving on to M7. **M7 is done**: all criteria pass, waiting for your confirmation and your answers to D31 and D32.

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
  - **UNSC:** MAC Strike, Archer Missile Barrage, HAVOK Nuke, ODST Drop Pods (6 ODSTs land for one battle), Bubble Shield, Battlefield Rally.
  - **Covenant:** Plasma Bombardment, Plasma Storm, Glassing Beam, Energy Shield Dome, Prophet's Blessing.
- **Result screen:** spells appear under top damage dealers and "what killed what", for example "Your MAC Strike killed 9 Grunt".
- **AI:** takes the card worth the most for its price, or skips. It aims strikes at the densest part of your army **as it was revealed last battle**, drops pods on your most valuable squad, and puts buffs on its own densest group. Easy is careless about both.

## Decisions needing your approval
- **D31: the spell catalogue.** Prices, recharge times, damage and areas are all first-pass values in `Data/Spells`.
- **D32: card rule details I had to choose.** The main ones:
  - The card screen comes before deployment.
  - Reinforcement levels grow with the rounds: level 1 until round 4, up to 2 until round 8, up to 3 after that.
  - A reinforcement squad sold before its first battle refunds only what you paid. That way, taking a free card and selling it isn't a way to make money.
  - No spells in the Sandbox yet.

## Test results
- **Unity:** EditMode **323 / 323**, PlayMode **19 / 19**. **.NET harness:** **293 / 293**.
- **New tests:**
  - Spell effects in battle.
  - Card rules, including the Bible's pricing examples and the "every other round" recharge.
  - The spell data.
  - A scene test: a spell taken, placed (outline shown), cast and recharging, then a reinforcement card whose squads appear on the field.
  - AI-vs-AI matches with cards are played to the end.
- **Built game:** 3 rounds of autoplay as UNSC and as Covenant with no errors. Screenshots are in `Docs/M7_*.png`.
- **Not tested automatically:** placing a spell with the real mouse. The scene test uses the same functions the mouse calls.
- **Small note:** decimals follow your PC's language (for example "2,5 s"), as elsewhere in the HUD.

## Blockers
None.
