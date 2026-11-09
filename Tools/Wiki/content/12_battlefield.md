---
title: The battlefield
group: Mechanics
icon: map
order: 12
blurb: The field's size, deployment zones, placement and formations, flank zones, and how units move in battle.
---

## Size and zones

| Area | Squares | Metres |
| --- | --: | --: |
| Field width | 80 | 160 |
| Each deployment zone (depth) | 40 | 80 |
| No man's land between the zones | 2 | 4 |
| Whole field | 80 × 82 | 160 × 164 |

One square is **2 m**. You deploy at the bottom of the field and the AI at the top, each only in its own zone.

The battlefield takes one of three interior looks (a UNSC installation, a Covenant warship, or a Flood-infested ship), chosen from the two factions and the match seed. The floor is always flat, and the look has **no effect on play**.

## Placement

- Squads snap to the grid. A squad covers a rectangle of squares worked out from its formation: how many bodies it has and how big they are. Each unit page shows its footprint.
- Squads can't overlap. **Air squads only check against other air squads**, so aircraft can sit above ground squads.
- Each tower takes up **3 × 3 squares** that ground squads can't use.
- **R** turns a squad 90°, which swaps its formation's width and depth. Facing in battle is automatic; turning only changes the formation's shape.
- While you move a squad, its bodies are drawn standing where they would go: green if the spot is free, red if it's blocked.

> **Tip:** The [size chart](#/units) (the **sizes** view on the Units page) draws every unit to the same scale.

## Formations

- Bodies stand in straight rows facing the enemy: **up to 4 bodies in 1 row, up to 12 in 2 rows, more in 3 rows**. For example, 18 bodies stand in 3 rows of 6. The gap between bodies is each body's width plus 0.5 m.
- A squad marches in formation until any of these happens, and then its bodies fight on their own for the rest of the battle:
  - its bodies' closest enemies belong to different enemy squads;
  - one of its bodies touches a body of another squad;
  - one of its bodies attacks, fires or is hit (a leap counts too).
- A squad of one body is never in formation.

## Flank zones

Two strips **outside the field's side edges, beside the enemy's zone**. Each is 4 columns (8 m) wide, as deep as the enemy zone, and 2 empty columns (4 m) from the edge of the field.

- **Only units with the flank ability can deploy there.** Today these are the Pelican and Aerospace Command's four aircraft.
- A squad placed there arrives **10 seconds late the first time** it fights from there; in later battles it arrives at the start. While it waits it can't act or be hit, but it does count as alive for the end of the battle.
- Flank aircraft can fly over the flank strips in battle; other units stay inside the field. Offensive spells can't be placed in the flank strips.
- Of the AIs, only **Hard** uses flank zones, and only Hard defends against them.

{{flankers}}

## Moving squads

Select a squad, press **M** (or Move) and click where it should go. **R** turns it and **Del** (or Sell) sells it. Esc or a right-click cancels. **Undo** takes back the round's actions one at a time.

## Movement in battle

- There's no grid in battle. Units move freely, can't overlap, and steer around other units, friends included.
- Units **drive the way they face** and turn at their own speed. Every unit has a time to turn 180°: about 0.25 s for chaff, up to several seconds for Titans. A unit facing the wrong way stops and turns first.
- **Making way:** a walking unit steps aside for a friend behind it that is at least 1.2× faster and at least as big. Tanks and Titans never step aside for smaller units, so those go around. Vehicles shove smaller bodies aside.
- **Edging forward:** a unit that is fighting, with a friend held up behind it, moves toward its target at half speed while it keeps firing. It stops when the target is within 25% of its main weapon's range, and never steps inside a minimum range.
- **Air units** ignore ground units completely, fly in straight lines, and only keep apart from other air units. They fly 13 m up, but ranges are measured along the ground.
- A unit in the middle of a leap flies over bodies and lands among them.
