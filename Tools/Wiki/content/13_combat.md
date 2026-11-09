---
title: Combat
group: Mechanics
icon: target
order: 13
blurb: Targeting, armor and damage types, the damage table, shields, splash, artillery, beams, defense arcs and everything else that decides a fight.
---

## Targeting

- Every unit attacks the **closest enemy it's allowed to hit**. There are no priorities (a few exceptions are below). Ties go the same way every time, so results repeat.
- **Allowed** means the enemy is alive, has arrived (isn't still waiting in a flank zone), fits the weapon's **target filter** (ground, air or both), isn't closer than the weapon's **minimum range**, and is the right kind of target for weapons limited to "chaff or unarmored" or "everything tougher".
- **Range** is measured from the shooter's centre to the target's **edge**, so a big target can be hit from as far away as a small one.
- Units look for a new target every **0.5 seconds**, or at once when their target dies. Each weapon picks its own closest target.

## What a unit notices

- A unit only notices enemies within **3× its weapon's range** (or 3× its leap range if that's longer), and never less than **30 m**.
- With nothing in sight, it **marches straight ahead** toward the enemy side.
- Once a unit enters the enemy's deployment zone, it notices enemies **at any distance** for the rest of the battle.
- **The main weapon leads.** A unit's first weapon is its main weapon: its target decides where the unit walks and when it stops to fight. Other weapons pick their own targets and fire at anything in range, in any direction, even on the move.
- The main weapon fires only once the unit faces its target within **15°**.
- If the main weapon has nothing to shoot, the unit goes after the closest target of its other weapons. Failing that, it walks up to the closest enemy even if it can't hurt it (often an aircraft) and waits beside it.

### Exceptions to "closest"

- Units that use one weapon at a time (a weapon for air and one for ground, a weapon for each kind of target, or a charging melee unit) use the weapon whose target is closest, **except that an air-only weapon always goes first when it has a target**.
- A unit whose main weapon has a minimum range switches to its second weapon when an enemy gets inside that range.
- The Flood's diving aircraft goes for the **thickest group of enemies**, not the closest one.
- Seed weapons only pick targets that can be seeded (see [The Flood](#/page/flood)).

## Armor and damage types

- **Armor types:** Unarmored (infantry), Light (light vehicles, most aircraft), Heavy (tanks, Hunters, towers).
- **Damage types:** Ballistic, Explosive, Energy, Fire and Melee. A weapon can also be **anti-armor**, which replaces its damage type's row, both against armor and against shields.

{{armor-counts}}

## The damage table

Each hit's damage is multiplied by the number for its damage type and the target's armor. The last column applies while the target's energy shield is up.

{{damage-table}}

> **Tip:** An anti-armor gun does 1.5× to Heavy whatever its damage type: a ballistic anti-armor round does 1.5×, not 0.25 × 1.5. Bullets barely scratch tanks; fire is deadly to infantry and weak against armor; explosives are even against everything.

## Energy shields

- A shield is a separate layer on top of any armor. Damage goes to the shield first, using the shield column above.
- If a hit breaks the shield, the part of the hit the shield didn't absorb carries on into health, with the armor multiplier.
- Shields regenerate at each unit's own rate once it has gone **5 seconds without taking damage**.
- With its shield down, a unit counts as its body armor. For example, a shielded Unarmored body can then be infected.
- Techs can add shields, even to units that had none. Defensive spells can add a bonus shield worth a share of each unit's health. Shields don't grow with level.

## What else changes a hit

A hit's damage is also multiplied by:

- the attacker's level, techs, item, research and buff cards (already in its weapon's damage);
- a buff spell's damage multiplier on the attacker;
- the attacker's shockwave penalty (×0.6) and the target's shockwave damage taken (×1.4), if a tower has fallen;
- how many forms a Flood pack still has, for the attacker;
- bonus damage against air, if the target is flying;
- the target's defense arc;
- for **fire**, ×1.5 if the target stands in an Oil Zone.

## Splash and cones

- **Splash:** full damage to every enemy within the radius of the impact, with no falloff, measured to each body's edge. Splash follows the attacker's target filter, so ground splash never hits air.
- **Cones** (flamethrowers): each shot hits the target and every enemy inside a cone of the weapon's width, out to its range.
- **Area hits** (splash, cones, beams, spells, death bursts) hurt **every form in a Flood pack**. A single-target shot only hurts one form's worth.

## Projectiles and artillery

- Weapons either hit instantly or fire a **projectile** at a set speed. A normal projectile follows its target; if the target dies, it lands where the target was.
- **Lobbed** shots (artillery) fly to where the target is heading when fired, and only damage what is inside the splash when they land. They **can miss** a target that turns or stops. Against aircraft, a lobbed weapon fires straight.
- **Minimum range:** artillery can't attack targets closer than this.

## Beams

- A beam **charges**, then fires at one spot for a set time, hitting everything within its radius every hit interval, then **rests**.
- It keeps firing if its target dies. The spot sweeps at a set speed toward the target, or the next one, so fast units can outrun it.
- While a beam charges or fires, its unit **holds its ground**.

## Wind-up and bursts

- Some weapons (the Spartan Laser family) **wind up** before each shot. The shot goes at the end, at the locked target, or another target in range if that one is gone. Firing continuously, the next wind-up overlaps the reload, so only the first shot at a new target is delayed. An EMP or the shooter's death cancels it.
- Guns of the same kind on one unit can be **staggered** to fire one after another.
- Some techs add **extra shots per attack**, a short gap apart.

## Defense arcs and blocking

- A few units have a **defense arc** centred on their facing. A hit from **inside** the arc uses the front multiplier, and a **blockable** projectile of a listed damage type may be **blocked completely**. A hit from **outside** uses the rear multiplier.
- In the current data: the shield Jackal has a **120°** arc (front ×0.5, rear ×1, blocks **80% of blockable ballistic** shots); the Hunter-type units have **240°** arcs (front ×0.7, rear ×1.5, no blocking).
- Only the projectile's own target can block it, and **a blocked projectile loses its splash too**. Splash, cones, beams and death bursts are never blocked, but still take the front or rear multiplier.

## Hit slow

Some weapons slow what they hit directly for a few seconds. A new hit restarts the time, and the strongest slow applies. **Titans are immune.**

## Leaps, charges and dives

- **Leap:** a unit with a leap pounces on a target within its leap range and lands against it, then waits out its cooldown. It also jumps over whatever holds it up after being stuck at under half speed for 0.3 s. It can't attack in the air, but it can be shot there.
- **Charge** (the Hunter-type units): a run along the ground at a target within reach of their close-in weapon. They use one weapon at a time: with a ground enemy close, they stop shooting and close in to bash.
- **Dive** (the Flood's crashing aircraft): it falls out of the sky at a target and dies where it lands, setting off its death burst.

## Air units

- Air units can only be hit by weapons that can target air. Some anti-air weapons deal bonus damage to air, such as 3×.
- **Landing carriers** (the Pelican and the Covenant Phantom, with their drop tech) count as **ground** units while they're down, so ground-only weapons can hit them then.

{{air-units}}

## Lifesteal, healing and auras

- **Lifesteal:** a share of the damage dealt comes back as health (some units, and an item).
- **Healing:** a share of maximum health back every second (an item).
- **Auras:** friends within a radius get faster and/or heal a share of their health every second. Auras don't affect towers and never heal past full health.

## Mid-battle drops

- Some units carry a squad that comes out during the battle, either always or only once its tech is bought. A dropped squad exists only for that battle, fights with your army's bonuses, earns no experience, and counts for base damage if it survives.
- **Landing carriers** (with their drop tech) drop their first squad **5 s** into the battle wherever they are. For later drops they fly to 8 m behind the enemy's rearmost ground unit, take 1.5 s to come down, let the squad out, and go back up: up to **3 drops**, at least **10 s** apart. An EMP that switches off their techs keeps them waiting on the ground.
- Other carriers simply drop their squad under themselves after a set delay.

## Shockwave

When one of your towers falls, your whole army moves at **×0.6 speed**, deals **×0.6 damage**, fires at **×0.6 rate** and takes **×1.4 damage** for a few seconds. See [Towers](#/page/towers).
