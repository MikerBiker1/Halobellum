# Daily report: 2026-10-31, night session (M5, M6 and the faction UI)

M0 to M3 are confirmed. M4 is done (its report is in this file's Git history), and you asked to move on to M5 and M6. This session completed **M5: AI opponent**, **M6: Leveling and tech upgrades**, and the **faction-styled UI** you asked for. All criteria pass; waiting for your confirmation and your answers to D25 to D28.

Before that, D23 went in: a counter must win at least 90% (9 of 10) of equal-value battles. That threshold replaces Section 15's "55 to 70%" in the Bible. Your balance notes (flamethrowers, Warthog range, Pelican, Marines, valuation) are saved in PROGRESS.md for the next balance pass.

## M5: AI opponent
- **No cheating, by design.**
  - The AI only gets an `AiView`: its own state, both bases, last round's base damage, and your army **as it was when the last battle started**.
  - It deploys at the start of each round, before you act, and uses the same actions and limits (1 unlock, 2 squads per round).
- **Strategies:** swarm, armor push, air raid, balanced.
  - Each round the AI scores its roster: the strategy's preferred roles, a bonus for counters, a small bonus per tier.
  - It counters the role you spent most on (Section 7 table).
  - In round 1 it moves its free draft squads into position.
- **Placement:** chaff in front, tanks and Titans just behind, flankers on the side edges, single-target DPS and chaff clear behind the tanks, anti-air and aircraft further back, artillery at the back.
- **Difficulty:**
  - **Easy** is partly random, gives counters little weight, skips about 1 in 3 purchases and places sloppily.
  - **Normal** counters sensibly with the role table.
  - **Hard** uses **measured** matchups (the counter web's unit-vs-unit win rates, in `Data/AI/MatchupTable.asset`, rewritten by every counter-web run). It aims at the part of your army its own army doesn't beat yet, values strong all-round units, and changes strategy after a bad round.
  - Measured with the real units, AI against AI, 20 to 30 matches per pairing: **Hard beats Easy 100% and Normal about 60%; Normal beats Easy about 70%.**
  - My first version of Hard lost to Normal, which is why it now uses the measured table.
- **Difficulty choice:** buttons on the draft screen (remembered, default Normal). The top bar shows "UNSC vs Covenant / Normal AI".

## M6: Leveling and tech upgrades
- **XP and levels (Section 5):**
  - Kills earn the victim's worth per body; the XP bar is the squad's own price (data value), the same at every level.
  - Level-up costs half the base price; health and damage are multiplied by the level, up to 5.
  - Level and XP survive the round reset, and the opponent sees levels once a squad has fought.
- **Tech upgrades (Section 9):** 52 first-pass techs, 2 or 3 per unit, as data assets in `Data/Upgrades`.
  - Each is bought once per unit type and applies to every squad of that type.
  - Specials: the Pelican's **Troop Bay** (your D22: it now drops 6 Marines at 10 s), the Flamethrower's **Wide Nozzle** (+30-degree cone), the Scorpion's **Anti-Air Coax**.
- **Stats window:**
  - Your squads show level, XP bar, **Level up (price)** and the techs with **Buy** buttons.
  - The AI's squads show their level only.
  - The result screen lists your squads that are ready to level up.
- **Sandbox:** set any squad's level and give or remove techs per side; army value counts levels and techs.
- **AI:** levels up its ready squads and researches one tech per round for its most-invested unit type (Easy rarely does).

## Faction-styled UI
- **Theme assets in `Data/UI`:** colours, fonts and panel shape, all editable in the Inspector.
  - **UNSC:** dark steel panels with cut corners, amber titles in capitals, Franklin Gothic.
  - **Covenant:** deep violet rounded panels with a purple glow, cyan titles, Candara.
  - **Neutral:** draft screen and sandbox.
- **When it applies:** your faction's theme covers every screen of the match: the match screens, the battle bar and the stats window.
- **Fonts:** taken from Windows by name, so no font files were copied. I first tried Bahnschrift for UNSC, but it is a variable font and Unity drew the wrong text with it, so I switched to Franklin Gothic.
- **Screenshots:** `Docs/M6_UNSC_deployment.png`, `Docs/M6_UNSC_result.png`, `Docs/M6_Covenant_deployment.png`, `Docs/M6_Covenant_result.png`, `Docs/M6_sandbox.png`.

## Test results
- Unity EditMode **287 / 287**, Unity PlayMode **17 / 17**, .NET harness 264 / 264.
  - New: AI tests (rules, counters, placement, no cheating, difficulty, full AI-vs-AI matches), leveling and tech tests (the Bible's required leveling tests), theme tests.
  - PlayMode: a full match against the Normal AI played to the end, a level-up and tech purchase through the stats-window actions, and sandbox levels and techs.
- **Built game:** autoplay as UNSC and as Covenant (2 rounds each, 4x) and the sandbox demo ran without errors.
- **Not tested automatically:** clicking the new buttons with a real mouse, and how the themes look on another PC. On a PC without these fonts, Arial is used.
- **Small cosmetic issue:** in the neutral Sandbox list, the bottom of letters like "g" is slightly cut off.

## Blockers
None.

## Decisions needing your approval
- **D25:** how the AI decides (information it sees, strategies, placement, difficulty levels, draft pick).
- **D26:** leveling details (XP per kill, bar size, no carry-over, shields and base damage unchanged, refunds).
- **D27:** the tech catalogue and its rules (prices by tier, no per-round limit, needs the unit unlocked).
- **D28:** the faction styles.
