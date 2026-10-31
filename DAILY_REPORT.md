# Daily report: 2026-10-31 (M3)

M0, M1 and M2 are confirmed (their reports are in this file's Git history). This session completed **M3: Round loop**. All criteria pass; waiting for your confirmation.

## Completed
- **Keeps running in the background:** Run In Background is on, so the game no longer pauses when you click another window.
- **Match logic (plain C#, `Scripts/Economy`):**
  - Credits: 400 to start, income 400 + 200 per round, capped at 1600; banked credits unlimited.
  - Per round: 2 new squads, 1 unlock (Tier 1 free, Tier 2 50, Tier 3 from the unit's data).
  - Selling: 100% back before a squad's first battle, 50% after.
  - Starting draft: up to 5 cards, never the same sub-faction twice, 2 free Tier 1 squads that start unlocked; the AI gets its own draw.
  - Deployment grid: squares, snap-to-square, turn 90 degrees, no overlap, only this round's squads movable.
  - Battles are built fresh from both deployments every round (round reset). Survivors on both sides damage the enemy base at the same time. The lower base loses if both fall; an exact tie replays the round.
  - The do-nothing AI.
- **Data:** `Data/MatchRules.asset` (all Section 5 numbers) and `Data/Factions/UNSC_Standard.asset` and `Covenant_Standard.asset` (temporary Standard cards using the placeholder units until M4).
- **Match scene** (`Scenes/Match.unity`, first in the build):
  - Draft screen.
  - Deployment: top bar with round, credits and next income, squads x of 2, unlocks x of 1, both bases and Ready; shop with Buy and Unlock; placement ghost (green or red); Move, Turn and Sell for the selected squad; the AI's army stays hidden; no timer.
  - Battle: base bars, live army value, speed controls.
  - Result screen: base damage both ways, bases, survivors, top damage dealers, what killed what, income, Continue.
  - Match-over screen with New match.
- **Battle display reworked** to show a new battle every round and a deployment preview. The Battle test scene still works as before.

## Test results
- Unity EditMode **209 / 209**. These include the Bible's required tests for economy and income cap, unlock rules, starting draft, and round flow and base damage, plus a full match won and a full match lost against the do-nothing AI.
- Unity PlayMode **8 / 8**: the real Match scene through draft, buying and placing, a full round with income, and a match won and lost.
- .NET harness 199 / 199. Your open Unity editor: no compile errors.
- **Built game, autoplay at 4x** (draft card 0, 2 squads bought per round, 3 rounds): every screen drew correctly. Battle frame rate about 1100 to 1400 FPS. Screenshots: `Docs/M3_01_draft.png`, `Docs/M3_r01_1_deployment.png`, `Docs/M3_r02_2_battle.png`, `Docs/M3_r02_3_result.png`.
- Not tested automatically: the placement ghost under a real mouse (it is on your checklist).

## Blockers
None.

## Decisions needing your approval
- **D17:** income timing (round 1 = 400 starting credits, then +600, +800 ... capped at 1600); Tier 1 unlocks are free but use the round's unlock; base damage rounded to whole numbers.
- **D18:** draft squads are auto-placed front centre, movable in round 1, and sell for 0; selling is allowed any time during deployment, and a 100% refund frees the squad's slot.
- **D19:** an exact tie restores both bases and replays the round's deployment with no income.
- **D20:** temporary M3 content: both Standard cards use the placeholder units, side colors instead of faction colors, and what the do-nothing AI does.
