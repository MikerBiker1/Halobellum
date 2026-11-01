# Daily report: 2026-11-01 (your D25 to D28 answers)

Last night's report (M5, M6 and the first faction styles) is in this file's Git history. Today I worked through your answers to D25 to D28. All three changes are done, tested and pushed. They wait for your look, and M4, M5 and M6 still wait for your confirmation.

## D25: the enemy's last army as grey ghosts
- During deployment, the enemy's army **as it was when the last battle started** stands in its zone in grey, at the same positions.
- **Click a ghost** to see its stats. The window says "enemy, last battle" and shows its level and the techs it fought with.
- **What the AI deploys this round stays hidden** until you press Ready. A test checks that a squad the AI buys during the round never appears.
- Seen squads now also record their owner's techs, because techs show in battle. The AI gets the same information about you.

## D26: double XP and the level-up arrow
- The XP bar is now **twice the squad's price** (`xpBarPerSquadCost` = 2 in `MatchRules.asset`).
- A squad that is ready to level up shows a **yellow arrow** above it. It pulses bright, dim, bright, and has a dark outline so it stands out over any squad. It disappears once the squad levels up.
- The arrow shows during deployment and on the result screen. Size, colours and pulse speed are on the Battle object's Unit View Manager.
- Screenshots: `Docs/D26_level_up_arrows.png` and `Docs/D26_level_up_arrows_closeup.png`.

## D28: textured faction UI
Answer to "if you can't manage this, tell me how I can help": no help was needed.
- **Textures:** drawn by a script I wrote, `Tools/UiArt/make_ui_textures.py`. They are ours, match the HUD exactly, and can be redrawn after any change.
- **Fonts:** downloaded from Google Fonts (SIL Open Font License, free to ship). Each font folder has its licence and a `SOURCE.txt`.
- **UNSC, after the frigate image:**
  - Bevelled steel frames with cut corners, ribbed edges, an engraved seam, amber stripes on the big corners, small window lights and rivets.
  - Panel interiors tiled with hull plating: plate seams, ribbed plates, vents, rows of windows, stencil markings.
  - Recessed list rows with an amber corner bracket.
  - Steel buttons with an amber tick and an amber outline on hover.
  - Segmented health and XP bars.
  - A small tab with lights on top of wide panels.
  - Titles in Saira Semi Condensed (capitals), text in Chakra Petch.
- **Covenant, after the cruiser image:**
  - Polished violet frames with rounded, leaf-like corners and a dark crevice holding a **glowing blue line**.
  - Armoured swoops with glowing strips over the two big corners.
  - Interiors tiled with pearly violet metal, flowing engraved lines and blue light strips.
  - Violet buttons with a blue outline that glows on hover.
  - Smooth glowing bars.
  - A glowing lens on top of wide panels.
  - Titles in Audiowide, text in Electrolize.
- **What stays neutral:** the draft screen and the sandbox.
- **Settings:** everything is in `Data/UI/UNSC_Theme.asset` and `Covenant_Theme.asset`: textures, fonts, text sizes, how strong the hull texture is.
- **Screenshots:** `Docs/D28_UNSC_deployment.png`, `Docs/D28_UNSC_result.png`, `Docs/D28_Covenant_deployment.png`, `Docs/D28_Covenant_result.png`.

## Fixed along the way
- **Overlapping result text:** the "Ready to level up" line on the result screen overlapped the last "what killed what" line. It now follows the list.
- **Clipped titles:** the new fonts sit lower than Arial, which the HUD was laid out with, so their text was clipped at the bottom. Each theme now moves its text up by a set amount.

## After your play test (afternoon)
- **D29, income:** income now builds on the 200 starting credits: 200 to start, **400 after round 1**, 600 after round 2, and so on up to 1600 (reached after round 7). Only `baseIncome` in `MatchRules.asset` changed, from 400 to 200.
- **D30, crowds.** Two new tests reproduced both problems on the old code (the vehicle stuck behind a chaff squad; only half of a front squad firing while the squad behind jiggled). Both pass now.
  - **Making way:** a moving unit looks ahead along its path. Friendly units at least 1.2x slower that stand in it step sideways out of the way.
  - **Pushing:** when two walking bodies collide, the smaller one gives way more.
  - **Edging forward:** a fighting unit with a held-up friend right behind it moves toward its target at half speed while still firing. It stops at 25% of its weapon range (never inside a minimum range).
  - **Balance:** the counter web still passes with the real units, 24 of 24, in both Unity and .NET. The Hard AI's matchup table was re-measured.
  - Details are in DECISIONS.md (D30) for your approval.

## Test results
- Unity EditMode **291 / 291**, Unity PlayMode **18 / 18**, .NET harness **265 / 265**.
- New tests:
  - The preview shows the enemy as seen at the last battle, never its current deployment.
  - Ghosts are grey and can be read.
  - The arrow appears for a ready squad and goes away after the level-up.
  - Both faction themes have every texture and both fonts, with licences; the neutral theme stays plain.
- **Built game:** 2-round autoplay as UNSC and as Covenant, plus four 3-round runs to catch the arrow on screen. No errors.
- **Not tested automatically:** clicking a ghost with the real mouse, and how the UI looks at resolutions other than 1920 x 1080.

## Blockers
None.

## Waiting for you
- **Feedback on the new look**, from playing once per faction. Your checklist is at the bottom of HUMAN_TODO.md.
- **Confirm M4, M5 and M6.**
