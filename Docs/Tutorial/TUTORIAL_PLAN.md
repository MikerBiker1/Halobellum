# Tutorial plan (D77, for the owner's review)

A scripted match, from the main menu's Play page ("Tutorial"), that teaches the main mechanics by doing them. Not every unit: the
loop of the game, every way to see stats, and the two towers. About 10 to 15 minutes, 4 rounds, and it can be left or skipped at any time.

## How it works

- **The match is a real match**, in the Match scene, with fixed choices so every player sees the same thing:
  - a fixed seed;
  - lower base health (600 a side), so it ends in about 4 rounds;
  - one draft card the tutorial picks: UNSC Noble with 2 squads of Marines;
  - a scripted enemy (`TutorialAi`): Covenant Banished, buying the same small armies every round, weak enough that a player
    following the steps wins, strong enough that round 1 hurts a little;
  - fixed card offers in rounds 2 and 3 (a spell, an item, Intensive Training), through a small hook on the match, so the lessons
    always have the card they talk about.
- **A tutorial director** (`TutorialDirector`) walks through a list of steps. Each step has:
  - a title and a few lines of text, in a panel at the top centre (the panel moves out of the way of what it points at);
  - what it points at, outlined and pulsing: a HUD button by its label (the HUD's buttons already report where they are drawn),
    a screen area (the shop, the stats window), or a thing on the field (a squad, a tower), projected from the world;
  - what moves it on: an action done ("unlock the Scorpion", "select a squad", "open the Command Center"), or a Next button for
    a step that only explains;
  - what is locked meanwhile, kept to a minimum: Ready is locked until the round's steps are done, so the battle doesn't start in
    the middle of a lesson. Nothing else is blocked; a player who does something else just sees the step wait.
- **Leaving**: a "Skip tutorial" button in the panel, and Esc's pause menu, go back to the main menu. The tutorial never saves
  anything, and never changes the player's settings.

## The lessons

Round 1, deployment:
1. **Welcome**: what Halobellum is. Each round you buy and place squads; the battle then plays itself; the survivors' nodes of light
   hit the enemy base. The first base to fall loses.
2. **The draft**: your starting card gives your faction, sub-faction and two free squads. A cheaper card means more base health.
   (One card here; in a match there are several.) *Pick it.*
3. **The camera**: move with WASD or the screen edges, zoom with the wheel, turn with the right mouse button. *Next.*
4. **The top bar**: credits and next round's income, squads bought this round (2 a round), unlocks this round (1), both bases.
   *Next.*
5. **The shop**: a unit must be unlocked once before you can buy it. *Unlock the Scorpion, then buy it.*
6. **Squad stats**: *select a squad*. The bottom-left window: health, armor, shield, speed, what it can hit, each weapon's damage,
   rate of fire, range and damage type, and its level, XP and techs.
7. **Shop stats**: *click a unit's name in the shop* to see its stats before buying.
8. **Placing**: *move a squad* (M, or the Move button; R turns it; Del or Sell sells it, for its full price before it has fought).
   Then *press Undo* (or Ctrl+Z): every action of this round can be undone, back to the round's card.
9. **The Command Center**: *open it* (its button above it on the field). If it falls in battle, your whole army is weakened for a
   while. It sells: Level up (more health, a longer shockwave), Extra slot (one more squad this round), Oil zone (a one-use spell),
   Rapid Resupply (credits now, fewer next round), Elite Recruitment (new squads start at level 2).
10. **The Research Center**: *open it*. Health and damage for your whole army, step by step, for good.
11. **Ready**: *press Ready*.

Round 1, battle and result:
12. **The battle**: it fights itself. Speed: Space pauses, 1, 2 and 3 for 1x, 2x and 4x. Units go for the nearest enemy they can
    hit; when a tower falls its side is weakened. *Next.*
13. **The result**: what each side's survivors did to the other's base, income, top damage dealers. *Continue.*

Round 2:
14. **Cards**: from round 2 each round starts with 3 cards: spells, items, economy, buffs, training; from round 4 reinforcements
    every other round. Skipping pays a bonus. *Take the spell.*
15. **Spells**: the spell bar on the left. *Place it on the field*; offensive spells go anywhere, defensive ones on your side.
16. **Levels**: kills give XP; a full bar lets a squad level up for half its price; Level all does every ready squad of a type.
    *Level up a squad.* Techs, in the stats window, upgrade every squad of a type. *Ready.*

Round 3:
17. **Items**: *take the item*, then *drag it from the inventory onto a squad's hexagon*. One item per squad, for the whole squad.
18. **Flank zones**: some units (Pelican, Sparrowhawk, Longsword, Vulture, Condor) can deploy in the zones beside the enemy's area,
    and arrive a little late. *Next.* *Ready.*

Round 4 and the end:
19. **Finish them**: *Ready* until the enemy base falls.
20. **Battle stats**: on the match-over screen, *open Battle stats*: every battle's field from above, and each squad's damage dealt,
    taken and stopped. Then the Main Menu button. The end: what's next (Faction mode, Unlock Skirmish, the Sandbox).

## What gets built

| Piece | Where | What |
|---|---|---|
| `TutorialAi` | Economy | The scripted enemy: a fixed army per round. |
| Scripted draft and offers | Economy, `Match` | A hook to give the player a chosen draft card and chosen card offers. |
| `TutorialScript` | Presentation | The steps: text, target, condition, lock. Plain data and lambdas, testable without the HUD. |
| `TutorialDirector` | Presentation | Runs the steps on a `MatchController`, checks conditions, sets the Ready lock. |
| `TutorialHud` | UI | The panel, the outline, Next and Skip. |
| `GameFlow.StartTutorial` | Presentation | Loads the Match scene with the tutorial on. |
| Play page button | UI, `MainMenuHud` | "Tutorial". |
| Tests | EditMode and PlayMode | The script reaches every step when its actions are done; the AI's armies; Ready is locked and unlocked; Skip leaves. |
| `-tutorialDemo <folder>` | UI | Plays the tutorial by itself and takes a picture of every step, so the text and outlines can be checked from pictures. |

## Open for the owner

- The faction: UNSC Noble against Banished is the plan. Another pair?
- Wording: the step texts above are drafts.
- Should the tutorial offer itself the first time the game starts?
