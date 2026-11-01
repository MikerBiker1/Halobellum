# Halobellum: Project Bible v0.4

*Design and engineering reference for AI coding agents. Version 0.4, October 28, 2026. Private, non-commercial fan project.*

## 1. How agents must use this document

This file is the single source of truth. Read it fully at the start of every session. If a request conflicts with this document, follow this document and flag the conflict in `DECISIONS.md` instead of silently deviating.

**Working rules**

1. Work on one milestone item at a time, in the order given in Section 14. Do not start later milestones early.
2. After each completed item: run all tests, commit with a clear message, and tick the item in `PROGRESS.md`.
3. Never leave the project in a non-compiling state. If an item cannot be finished, revert to the last good commit and write the blocker in `BLOCKERS.md`.
4. Anything that requires the human (importing assets, clicking in the Unity editor, making a decision) goes into `HUMAN_TODO.md` with exact steps. Do not stall waiting; move to the next item that does not depend on it.
5. Prefer simple, readable code over clever code. The human cannot read C# fluently, so every script gets a short header comment explaining what it does and what it depends on.
6. Do not add features, units, or systems that are not in this document. Propose them in `DECISIONS.md` instead.
7. Every number in this document is a **starting value**. Put all of them in data files (Section 4) so balance can change without touching code.
8. If a model, sound, or texture is missing, use a placeholder primitive and log it in `HUMAN_TODO.md`. Never block on art.

**Autonomous agent rules**

1. **Technical autonomy.** Agents may independently implement, debug, refactor, optimize, and test approved features.
2. **Design authority.** Agents must not change gameplay mechanics, unit statistics, rosters, economy, or project scope without human approval. Record proposed changes in `DECISIONS.md`.
3. **Parallel development.** If more than one agent works at the same time, each uses its own Git branch or worktree, and they avoid editing the same files concurrently.
4. **Integration.** Work is merged into the stable branch only after it compiles and passes the relevant tests.
5. **Unattended operation.** When blocked, record the issue in `BLOCKERS.md` and continue with another approved, independent task. Never invent new features to stay busy.
6. **Session reporting.** At the end of every unattended session, write `DAILY_REPORT.md` with completed work, test results, blockers, and decisions that need human approval.

**Guiding principle:** agents have autonomy over implementation; the human keeps authority over game design.

## 2. Vision

A single-player 3D auto battler based on the mechanics of Mechabellum, with the unit roster drawn from the Halo franchise. The player buys and positions squads each round, then watches both armies fight automatically. Winning comes from reading the opponent’s army and deploying counters.

**Design pillars**

- **Counterplay over raw power.** Every unit has a good answer in every faction.
- **Readable battles.** The player should understand why a fight was won or lost, and recognize every unit from a distance.
- **Halo flavor.** Unit names, roles, and abilities should feel recognizably Halo.
- **Scope discipline.** A finished small game beats an unfinished huge one.

**Out of scope (do not build):** online multiplayer, matchmaking, accounts, monetization, campaign story, first or third person control, base building, timers of any kind on the deployment phase, specialists.

## 3. Technical stack

- **Engine:** Unity, latest LTS release, Universal Render Pipeline, C#. The game, the Unity project, and the Git repository are all named Halobellum.
- **Do not use** ECS/DOTS, third-party networking, or paid plugins.
- **Camera:** fixed angled top-down camera seen from a distance, like Mechabellum. Pan and zoom only.
- **Visual target:** realistic but unpolished. Models only need to be recognizable at a distance.
- **Platform:** Windows desktop. "E:\AI\Halobellum"
- **Source control:** Git. One commit per completed item. Never commit the `Library`, `Temp`, or `Logs` folders.
- **Project structure**

```
Assets/
  _Project/
    Scripts/Core, Combat, Units, Economy, AI, UI
    Data/Units, Factions, Upgrades, Cards
    Prefabs/Units, Effects, UI
    Scenes/
    Art/Imported/ (each asset folder gets a SOURCE.txt noting where it came from)
  Tests/EditMode, PlayMode
```

## 4. Architecture rules (non-negotiable)

1. **Data-driven units.** All unit stats live in ScriptableObjects (`UnitDefinition`). No stat is ever hardcoded in a script. Adding a unit means adding a data asset and a prefab, not writing new logic, unless it needs a new ability.
2. **Simulation separate from presentation.** Combat logic runs in plain C# classes that do not depend on GameObjects, models, or animation. Visuals subscribe to simulation events (unit moved, attacked, died). This allows fast automated testing and lets art be swapped freely.
3. **Deterministic simulation.** The simulation uses a seeded random number generator and a fixed timestep (30 ticks per second). The same inputs and seed should produce the same outcome, including random rolls such as shield block chances. This is preferred but not absolute: authentic Mechabellum-style movement and collision take priority over strict determinism (see Section 6). Still test seeded repeatability on the same machine, because it is cheap and makes balance results and bug reports reproducible.
4. **Headless battle runner.** Build an editor tool that runs N simulated battles between two armies with no graphics and outputs win rates to a CSV. Agents use it to test balance without the human. It runs the exact same simulation code as the game, so the same armies and seed give the same result in both.
5. **Abilities are composable components** (for example `SplashDamage`, `Infect`, `SpawnUnits`), configured from data.
6. **Events over references.** Systems communicate through events or a small event bus, not by finding each other in the scene.
7. **Simple unit state machine:** Idle, Move, Attack, Dead. Add new states only when this document calls for them.
8. **Round snapshots.** The battlefield state is saved at the end of every deployment phase and restored after every battle (see Section 5). Anything created or changed during a battle (infections, spawned units, spell effects) is temporary and discarded at round end.
9. **Directional combat.** Every unit faces the enemy team, and a squad can be rotated during deployment. Most units have no directional defense; only units whose data defines a defense arc (for example Shield Jackals and Hunters) react to the angle of the incoming attack (see Section 6).
10. **Tests required** for: damage and armor math, directional defense, targeting selection, economy and income cap, unlock rules, starting draft, leveling, round flow and base damage, and infection rules.

## 5. Core game loop

A match is a series of rounds against an AI opponent. Each round has these phases:

1. **Deployment phase (no timer).** The player spends credits, unlocks a unit, buys squads, places and rotates them, and upgrades or levels squads. The AI does the same, hidden from the player until the battle starts. The phase ends when the player presses **Ready**.
2. **Battle phase.** Both armies fight automatically. The player cannot act, except for speed control (1x, 2x, 4x) and pause. The battle ends when one side has no units left, or after **90 seconds**.
3. **Result phase.** Surviving units on **both** sides damage the opposing base (see below). Income is awarded. The field resets. Show a short summary of what killed what.

**Match rules (starting values)**

| Rule | Value |
| --- | --- |
| Starting credits | 200 |
| Income per round | 200 (round 1, paid by the starting credits), plus 200 per round after the first: 400 after round 1, 600 after round 2 ... |
| Income cap | Income per round is capped at 1600. Banked credits have no limit. |
| Base health | 2000 each |
| Base damage | Every surviving unit deals damage to the enemy base. Per-unit damage is a data field (`BaseDamage`); default is (squad cost ÷ squad size) ÷ 5. |
| Win condition | Reduce the enemy base to 0 or below. If both fall in the same round, the base with the lower (more negative) health loses; an exact tie is a tied round: the damage is undone, income is paid and the match goes on to the next round. |
| New squads per round | 2 |
| Unit unlocks per round | 1 |
| Unit sale refund | 100% before the battle of the round it was bought, 50% afterward. Free draft squads always sell for 50% of their cost. |

**How a round ends and how base damage works.** A battle ends when one side has no units left, or when 90 seconds pass. Either way, the result is resolved the same way: every unit still alive on each side deals its `BaseDamage` to the opposing base, and both sides take damage at the same time. There is no separate “round winner” penalty. A side that was wiped out simply has no survivors, so it deals nothing. Example: at 90 seconds the player has 1 Scorpion alive (300 ÷ 1 ÷ 5 = 60 damage) and the AI has 2 Ghosts alive (200 ÷ 2 ÷ 5 = 20 each, 40 total). The AI base takes 60 and the player base takes 40. Only units alive at the moment the battle ends count, and temporary units from the round (infected units, reinforcements) are included if still alive.

**Starting draft.** Before round 1, the player picks one of 5 draft cards. This is the most important decision of the game.

- Each card shows a faction, a sub-faction, and 2 random Tier 1 squads from that faction’s roster, including the sub-faction’s exclusive units.
- The same sub-faction never appears on two cards in the same draw.
- The chosen faction and sub-faction are fixed for the match. The 2 squads are free starting squads, and their unit types start unlocked.
- The AI gets its own draw and picks one by simple rules. The same combination of sub-faction and starting squads rarely repeats, so every match plays differently and every faction is worth learning.
- The final game has 8 sub-factions (3 UNSC, 3 Covenant, 2 Flood), so a faction may not appear in a draw at all. Until they are built, development uses temporary “Standard” cards (the base roster only) for UNSC and Covenant so the game loop can be tested.

**Unlocking units.** Unlocking makes a single unit type available to buy for the rest of the match. A faction can only unlock units from its own roster. At most one unlock per round. Unlock prices follow Mechabellum:

| Tier | Unlock price |
| --- | --- |
| Tier 1 | Free |
| Tier 2 | 50 |
| Tier 3 | Half the unit’s deployment price, so 200 to 250 (listed per unit in Section 8) |
| Tier 4 (Titan) | 350 (Titans deploy for 800) |

A unit can be unlocked and bought in the same round. Buying a squad is limited to 2 new squads per round, regardless of type.

**Deployment price bands (per squad).** Tier 1 costs 100 to 200, Tier 2 costs 250 to 300, Tier 3 costs 400 to 500, and Titans cost 800. Balance is done through price, so every unit stays inside its tier’s band.

**Squads.** Buying a unit deploys a *squad*: a fixed number of bodies (for example 18 Marines, 1 Scorpion). Squad size and cost are data, and cost is per squad. Units that spawn other units (for example the Pelican) do not necessarily spawn full squads; the spawn count is part of the ability data.

**Placement.** Free placement within the player’s half, on a grid with snap-to-cell for readability. Only squads **bought this round** can be moved or rotated. Squads bought in earlier rounds stay where they were placed and keep their rotation. All units face the enemy team during battle, and a squad can be set to one of two orientations during deployment: the default, or turned 90 degrees. Orientation cannot change once the squad has been in combat. This matters for wide formations: an 18-Marine squad is much wider than it is deep until it is turned 90 degrees. Units cannot overlap. Air units ignore ground footprints.

**Round reset.** After every battle, the battlefield returns to its pre-battle state, with these rules:

- Every squad returns to its placed position at full health and keeps its level and XP.
- Anything temporary is discarded: units infected during the battle, units spawned by abilities, spell effects.
- Units destroyed during the battle are remade, so each round starts from the same state as the previous round started, plus whatever the player bought.

**Squad leveling.** Squads earn XP from kills. The XP bar is visible when a squad is selected. When it is full, the player may pay **half of the squad’s base deployment price** to level it up. The XP needed to fill the bar is the same at every level. Level multipliers apply to base health and damage, and the total equals the level number:

| Level | Total multiplier | Step increase |
| --- | --- | --- |
| 1 | 1x | n/a |
| 2 | 2x | +100% |
| 3 | 3x | +50% |
| 4 | 4x | +33% |
| 5 | 5x | +25% |

**Credit sinks.** Credits are spent on squads, unit unlocks, tech upgrades, squad level-ups, spell cards, and reinforcement cards.

## 6. Combat system

**Unit stats (all data-driven):** squad size, cost, tier, unlock cost, role, health, armor type, shield (optional, regenerates after 5 seconds without damage), move speed, collision size, target filter (ground, air, both), optional defense arc settings, `BaseDamage`, and one or more weapons.

**Weapon stats:** damage per shot, fire interval (displayed as rate of fire, in shots per second), range, minimum range (for artillery), projectile speed (or instant), splash radius, and damage type. Whether a projectile can be blocked (for example by a Jackal shield) is a weapon flag. A weapon can be **lobbed** (the shot arcs to where the target is heading and only damages what is in the splash when it lands, so it can miss) or a **beam** (it charges, fires for a set time at one spot, hitting everything in its radius, keeps firing if the target dies, then rests).

**Armor types:** Unarmored (infantry), Light Armor (light vehicles), Heavy Armor (tanks, Hunters). An energy shield is a layer on top of any type. A unit whose shield is depleted counts as its base armor type: an Elite has an Unarmored body, so it can be infected once its shield is down.

**Damage types:** Ballistic, Explosive, Energy (plasma), Fire, Melee.

**Damage multipliers (starting values)**

| Damage type | Unarmored | Light | Heavy |
| --- | --- | --- | --- |
| Ballistic | 1.0 | 0.6 | 0.25 |
| Explosive | 1.0 | 1.0 | 1.0 |
| Energy | 1.0 | 1.0 | 0.75 |
| Fire | 1.5 | 0.8 | 0.3 |
| Melee | 1.0 | 0.7 | 0.3 |
| Anti-armor (tag on a weapon) | 0.5 | 1.2 | 1.5 |

**Directional defense.** This is an exception, not the default. Most units have no directional defense. Every unit faces the enemy team in battle, and the player can turn a squad 90 degrees during deployment. Units that do have a defense arc define it in data: an arc size, a front damage multiplier, a rear damage multiplier, and a block chance against specific projectile types. Shield Jackals have a 120 degree arc and block 80% of incoming ballistic projectiles (not damage) from it. Hunters have a much wider 240 degree arc, take heavily reduced damage from inside it, and take increased damage from behind.

**Targeting:** each unit always attacks the closest enemy it can legally target (by its target filter and minimum range), with no priority rules and no preferred targets. This is why chaff stays important at every stage of the game: it soaks up fire meant for expensive units. If nothing is in range, the unit moves toward the closest enemy. Re-evaluate every 0.5 seconds, not every tick.

**Movement:** deployment uses a grid for placing and rotating squads. Once the battle begins, units move freely in continuous 2D space with no grid restrictions. Units have physical collision boundaries and cannot overlap, and they steer around other units, including allies. Faster units may overtake slower ones, so formations spread, split, and reorganize on their own, and units are not locked into their starting formations. Movement is part of the simulation: the graphical game and the headless runner use the same logic, and Unity only displays the simulation’s positions (no NavMeshAgent, and no Unity physics for movement). Use simple steering, collision avoidance, and separation suitable for a flat battlefield, with a spatial partition such as a uniform hash grid for neighbor queries so 300 vs 300 stays fast. Do not implement grid-based movement during combat. Air units ignore ground units’ collision and fly in straight lines.

**Arena:** size is a data value. Start with a rectangular field split into two halves, sized by comparison to Mechabellum, and tune in playtests.

## 7. Roles and the counter web

Every faction must have at least one unit that fills each role below. Anti-armor is part of Single-target DPS, and there is no Support role. Utility units are classified into the closest role.

| Role | Job | Strong against | Weak against |
| --- | --- | --- | --- |
| Chaff | Cheap bodies that absorb shots and swarm | Single-target DPS | Chaff clear |
| Chaff clear | Splash damage vs groups | Chaff | Tanks, flankers |
| Single-target DPS | High damage to one target, including anti-armor | Tanks, heavy units | Chaff swarms, flankers |
| Tank | High health frontline | Chaff clear | Single-target DPS |
| Flanker | Fast, hits the backline | Artillery, DPS | Chaff clear, tanks |
| Artillery | Long-range splash | Slow, grouped units | Flankers, air |
| Anti-air | Stronger against air but can still hit ground targets; some units gain anti-air upgrades | Aerial | Ground units |
| Aerial | Air units, bombers, and transports | Ground armies without anti-air | Anti-air |
| Titan (Tier 4) | One huge unit, very tanky | Most things in a straight fight | Single-target DPS, large chaff swarms |

**Rule:** whenever a unit is added, list its two best counters in the unit’s data file notes. If a unit has no counter, it is a bug in the design.

## 8. Factions and rosters

Numbers below are first-pass values for the first playable slice. Costs are per squad, and health is per body. Unlock prices follow Section 5.

### 8.1 UNSC (slice roster)

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Marine | Chaff | 1 | 0 | 18 | 100 | 60 | Ballistic rifles, range 12 |
| Flamethrower Trooper | Chaff clear | 1 | 0 | 6 | 150 | 90 | Fire, short range, splash cone |
| Warthog | Flanker | 1 | 0 | 2 | 200 | 300 | Fast; light armor; machine gun |
| Gauss Warthog | Single-target DPS | 2 | 50 | 1 | 250 | 300 | Fast; light armor; anti-armor gauss cannon |
| Scorpion | Tank | 2 | 50 | 1 | 300 | 1200 | Heavy armor; cannon (explosive) plus coax |
| Wolverine | Anti-air | 2 | 50 | 1 | 300 | 500 | Rocket volleys, bonus damage against air, can still hit ground |
| Hornet | Aerial | 2 | 50 | 2 | 300 | 400 | Air, rockets, bombs ground |
| Archer Missile Pod | Artillery | 2 | 50 | 2 | 300 | 350 | Long-range explosive barrage; lobbed |
| Pelican | Aerial | 2 | 50 | 1 | 300 | 800 | Chin-mounted M370 autocannon (ballistic, hits air and ground); drops one small Marine squad (6) mid-battle after 10 seconds |
| Spartan | Single-target DPS | 3 | 225 | 1 | 450 | 1500 | Energy shield, Spartan Laser |
| Mantis | Tank | 3 | 250 | 1 | 500 | 2500 | Heavy armor; chaingun and rockets |
| Elephant | Titan | 4 | 350 | 1 | 800 | 8000 | Mobile fortress; turrets for chaff, cannon for armor |

ODSTs are not a unit. They appear as a UNSC spell card (Section 10).

### 8.2 Covenant (slice roster)

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Grunt | Chaff | 1 | 0 | 20 | 100 | 40 | Weak plasma pistols |
| Fuel Rod Grunt | Chaff clear | 1 | 0 | 4 | 150 | 80 | Explosive splash at range |
| Jackal (Plasma Rifle) | Single-target DPS | 1 | 0 | 8 | 150 | 70 | Plasma rifle, no shield |
| Jackal (Shield) | Tank | 1 | 0 | 6 | 150 | 70 | Frontal energy shield: 120 degree arc, blocks 80% of incoming ballistic projectiles. Strong against light chaff |
| Ghost | Flanker | 1 | 0 | 2 | 200 | 250 | Fast, light armor |
| Elite | Single-target DPS | 2 | 50 | 3 | 300 | 200 | Regenerating shield, Unarmored body (can be infected once its shield is down) |
| Wraith | Artillery | 2 | 50 | 1 | 300 | 900 | Lobbed mortar; heavy armor; slow |
| Vampire | Anti-air | 2 | 50 | 1 | 300 | 500 | Plasma, bonus damage against air, can still hit ground |
| Banshee | Aerial | 2 | 50 | 2 | 300 | 400 | Fast bombing runs |
| Hunter pair | Tank | 3 | 225 | 2 | 450 | 1500 | Heavy armor; 240 degree front defense arc, extremely resistant inside it, weak from behind |
| Zealot | Single-target DPS | 3 | 200 | 1 | 400 | 1400 | Energy sword, fast, strong shield, Unarmored body |
| Scarab | Titan | 4 | 350 | 1 | 800 | 9000 | Walking fortress; plasma turrets plus a charged main beam |

The Covenant has no dedicated anti-armor unit. Elites, Zealots, and Wraith splash fill that job for the slice, and a dedicated unit may be added in the balance pass if win rates demand it. The Engineer is not in the slice.

### 8.3 Flood

The Flood win by spreading. They have a roster of their own, plus temporary infected versions of other units. They have fewer unit types per tier than the other factions, because they will get more exclusive sub-faction units (M10). First-pass stats are in the unit assets and `balance.csv` (D38).

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Infection Forms | Chaff | 1 | 0 | 50 forms in 10 packs | 100 | 190 per pack | Fast; the only unit that infects |
| Carrier Form | Chaff clear | 1 | 0 | 4 | 150 | 140 | Explodes on reaching an enemy or when killed, releases 10 Infection Forms |
| Combat Form | Single-target DPS | 2 | 50 | 6 | 200 | 220 | Anti-armor claws, scavenged rifle |
| Stalker Form | Flanker | 2 | 50 | 3 | 250 | 450 | Very fast pure form |
| Pure Form Ranged | Artillery | 2 | 50 | 2 | 300 | 400 | Lobs bone shards |
| Swarm Form | Anti-air | 2 | 50 | 20 forms in 4 packs | 300 | 200 per pack | Flying packs, bonus damage against air |
| Pure Form Tank | Tank | 3 | 200 | 1 | 500 | 3200 | Heavy armor, splash slam |
| Seeder | Single-target DPS | 3 | 200 | 2 | 450 | 800 | Seeds enemies from range |
| Infected Pelican | Aerial | 3 | 200 | 1 | 400 | 900 | One-shot crash dive |
| Infected Phantom | Aerial | 3 | 200 | 1 | 450 | 1100 | Drops 5 Combat Forms after 8 seconds |
| Flood Abomination | Titan | 4 | 350 | 1 | 800 | 7000 | Very tall and large; aura for nearby Flood, keeps releasing Infection Forms |

**Packs.** One Infection Forms body is a pack of 5 tiny forms that move and act as one. A pack shows and deals less as its forms die. A single shot hurts one form; an area hit (splash, cone, beam, spell) hurts every form in the pack.

**Infection mechanic**

- **Only Infection Forms can infect.** Combat Forms, Carrier Forms, and Pure Forms do not. The Seeder and the Flood's infecting spells are the exceptions.
- Only **Unarmored** units can be infected, and only when they die within melee range of an Infection Form.
- Vehicles, Heavy and Light Armor, Spartans, and Hunters cannot be infected by Infection Forms. A unit with an Unarmored body and an energy shield, such as an Elite, can be infected only after its shield is depleted.
- A killed unit rises as an infected unit after 2 seconds. Its stats come from its original definition scaled to 60% health. It becomes a dumb melee charger: moves to the nearest enemy, no special abilities. It keeps its base damage and cannot be infected again.
- Infected units exist only for that round and are discarded at round end.
- **Seeder:** a hit plants a seed for 8 seconds. The host loses health and is slowed, and if it dies seeded it rises as an infected unit, whatever its armor. A shield that is up stops the seed. Titans and Flood cannot be seeded.
- **Infected Pelican:** a one-shot Tier 3 unit that crashes into the densest enemy cluster, dealing heavy splash damage and spawning 10 Infection Forms and 5 Combat Forms on impact. If it is shot down, it does the same where it falls.
- Counters: Fire, splash, and armored units. Players are expected to invest in chaff clear. Fighting the Flood with chaff feeds them.

### 8.4 Sub-factions (built after the slice, do not implement until approved)

Sub-factions **add** exclusive units on top of the base faction roster. They do not replace it. Once they exist, they also feed the starting draft (Section 5). The final game has 8 sub-factions: 3 UNSC (Noble, Armored Division, Aerospace Command), 3 Covenant (Banished, Lekgolo Colony, Unggoy Rebellion), and 2 Flood (UNSC-based, Covenant-based).

- **Banished (Covenant):** Choppers, Marauders, Reavers, Blisterbacks, Brute Grenadiers.
- **Noble (UNSC):** Spartan Sniper, Spartan Heavy Weapons, Spartan Grenadier, Spartan CQB, Spartan Fireteam. Expensive to unlock.
- **Lekgolo Colony (Covenant):** Goliaths, Skitterers, Hunter Captain.
- **Unggoy Rebellion (Covenant):** Cannon Fodder, Heavy Grunts, Methane Wagon, Grunt Goblin.
- **Flood, UNSC-based:** infected variants of the UNSC roster plus pure forms.
- **Flood, Covenant-based:** infected variants of the Covenant roster plus pure forms.
- **Armored Division (UNSC):** Grizzly, Cobra, Rhino, Gremlin.
- **Aerospace Command (UNSC):** Sparrowhawk, Vulture, Shortsword, Condor Gunship.

## 9. Tech upgrades

Each unit type has up to 3 tech upgrade slots bought with credits, such as armor plating, range increase, fire rate, or a special weapon. Upgrades are data assets and apply to all squads of that type for the rest of the match. Some units can unlock anti-air upgrades where it makes sense for them, usually a range and damage bonus against air. Built in Milestone 6.

## 10. Cards: spells and reinforcements

Cards are offered between rounds, every round after the first. Rounds 2 and 3 offer general cards (spells, items, economy cards, buffs); reinforcements start at round 4 and come every other round, with general cards in between (D32, D33). Strong cards only appear late: nukes and the powerful buffs from round 7, the extra deployment slot from round 5. The player picks 1 of 3, or skips: skipping a general offer gives 50 credits, and skipping a reinforcement offer gives 100. The selection screen has a Skip button that shows the bonus. There are two kinds:

- **Spell cards** are **permanent** once picked and **rechargeable**. A spell recharges over a number of rounds that depends on its impact: a nuke may take 4 rounds, a precision orbital strike 1. A player can own more than one spell. Each spell has a configurable maximum ownership count (MaxOwnedCopies) in its data. Low-impact spells can be owned several times, and each copy has its own independent cooldown. High-impact spells and buffs, such as nuclear strikes and Covenant Glassing Beams, have a maximum of 1. Once the player owns the maximum number of copies, that spell is excluded from future card offers, and if fewer than 3 eligible spells remain, the offer shows fewer cards. Maximums can be adjusted during balancing. The cooldown starts the round after the spell is used, so a spell with a 1-round cooldown can be used every other round. Each spell has a credit price set in its data. Examples: orbital strike, plasma storm, **ODST pods** (UNSC only, offensive; drop-in squad that lasts one round, can be deployed anywhere, recharges after 2 rounds), **Glassing beam** (Covenant only), and buffs such as shields or damage boosts.
- **Reinforcement cards** are **single-use**. Each one is one or more squads of a random unit from your faction (including sub-faction) at a random level, and it also unlocks that unit for the rest of the match. Pricing is described below.

**Placing spells.** Spells are placed during the deployment phase and take effect as the round starts.

- **Defensive spells (including buffs)** show a blue transparent outline during deployment and can only be placed on your own side.
- **Offensive spells** show a red transparent outline and can be placed anywhere on the field.
- Placed spells are hidden from the enemy until the round starts. This applies to the player and the AI equally.

**Reinforcement cards: budget and pricing.**

- Each round has a reinforcement budget that grows as the rounds go on (a data table).
- A card is generated by picking a random squad type from the faction roster and a random level. One squad’s worth is its base price, plus half of the base price for each level above 1.
- If one squad’s worth is **under** the budget, the card adds more copies of the same squad (same unit and level), one at a time, until the total worth reaches or passes the budget. Example: with a 300 budget, a level 1 Warthog (200) becomes 2 Warthogs (400), and the card costs 100. With a 400 budget, a level 2 Grunt squad (150) becomes 3 squads (450), and the card costs 50.
- If the total worth is **exactly** the budget, the card is free.
- If the total worth is **over** the budget, the player pays the missing credits (total worth minus budget) to take it. A single squad that is already over the budget stays one squad.
- The reinforcement squads are placed during deployment like squads bought this round, so they can be moved and rotated that round. They do not count toward the 2 new squads per round limit, ignore the one-unlock-per-round limit, and start with 0 XP.

Cards are data. Design the data so cards attach without refactoring. Built in Milestone 7.

## 11. AI opponent

The AI plays the same game under the same rules, including the income cap, unlock limit, squad limit, and starting draft.

1. **Version 1: rule-based.** Each round, spend the budget by picking a strategy from a small set (swarm, armor push, air raid, balanced). Choose strategies using the player’s previous army composition when possible, and counter the player’s most expensive role.
2. **Placement:** tanks and chaff in front, ranged units behind, flankers on the sides.
3. **Difficulty:** Easy makes poorer counters and wastes budget; Normal counters sensibly; Hard counters well and adapts each round.
4. The AI must never cheat on information or income. Difficulty is about decision quality only.

## 12. Art, audio, and legal

- This is a private, non-commercial fan project and will not be shared.
- Any art or audio the human places in `Art/Imported` may be used. Each asset folder gets a `SOURCE.txt` noting where it came from, so it can be swapped later.
- Agents use placeholders (colored primitives, one color per faction, size by role) when an asset is missing, and log what is needed in `HUMAN_TODO.md`.
- Visual target: realistic but low-detail and unpolished. Silhouette, color, and scale must make each unit recognizable from the game camera. Normalize imported model scale and tint by faction to hide style mismatches.
- Use level-of-detail models and GPU instancing for large squads.

## 13. UI requirements

- Starting draft screen: 5 cards, each showing a faction, a sub-faction, and its 2 starting squads.
- Deployment screen: credit counter and expected income, unit shop, unlock button (one per round), squads bought this round (x of 2), squad rotation control, spell placement with blue (defensive) and red (offensive) outlines, “Ready” button. No timer.
- Card selection screen (from round 2): 3 cards of the current kind (spells or reinforcements), their prices, and a Skip button showing the bonus (50 for spells, 100 for reinforcements).
- Stats window (bottom left): appears whenever a squad is selected and shows all of its stats: squad size, cost, tier, role, health, armor type, shield, move speed, collision size, target filter, and for each weapon its damage, rate of fire, range, splash radius, and damage type. It also shows level, XP bar, level-up button with its price, and tech upgrades.
- Battle screen: both base health bars, speed control, a small live army-value comparison.
- Result screen: base damage dealt by each side, top damage dealers, and the most effective counters.
- Tooltips on every unit show role, strong against, and weak against, taken from the data.

## 14. Milestones and acceptance criteria

Complete in order. A milestone is done only when every criterion passes and the human has confirmed it.

**M0: Project setup**

- Unity project created, folder structure from Section 3, Git initialized.
- Empty test scene, `PROGRESS.md`, `DECISIONS.md`, `HUMAN_TODO.md`, `BLOCKERS.md` created.

**M1: Battle simulation core**

- `UnitDefinition` ScriptableObject, damage and armor math with tests.
- Optional directional defense, free movement with collision and separation, and a fixed-timestep simulation in plain C#.
- Headless runner that simulates 3 placeholder units against each other and prints results.

**M2: Battle in the scene**

- Placeholder prefabs spawn from data, move through the simulation’s free-movement system (no NavMeshAgent), target and fight, show health bars when damaged, and die.
- Camera pan and zoom. Speed controls. Selecting a squad opens the bottom-left stats window.
- 300 vs 300 units (600 total) runs at 60 FPS on a mid-range PC, with placeholder art. For the same armies and seed, the game and the headless runner produce the same result.

**M3: Round loop**

- Deployment, battle (90 second cap), and result phases, with no deployment timer.
- Starting draft (5 cards where enough sub-factions exist), credits, income and income cap, base health and base damage from survivors, shop, placement and squad rotation, 2 squads per round, 1 unlock per round, refunds, round reset.
- A full match against a do-nothing AI can be won and lost.

**M4: Slice rosters and sandbox**

- All UNSC and Covenant units from Section 8 implemented with placeholder art and abilities, with unlock prices.
- Sandbox mode: pick two armies, place them freely, run the battle. Used for balancing.
- Counter web verified by the headless runner (every role has a counter that wins at least 90% of equal-value battles against it; D23).

**M5: AI opponent**

- Rule-based AI with 4 strategies and 3 difficulty levels. Full playable match.

**M6: Leveling and tech upgrades**

- Squad XP, leveling, and the tech upgrade system with UI.

**M7: Cards**

- Spell cards (permanent, rechargeable, placed with blue and red outlines) and reinforcement cards (single-use, budget-based), offered every round after the first and alternating between the two kinds.

**M8: Flood faction**

- Infection mechanic, Flood roster, infected unit variants, balance pass.

**M9: Polish**

- Real art, audio, VFX, menus, settings, balance pass using the headless runner.

**M10 and later:** Sub-factions with the full 5-card starting draft, more maps, faction-themed content.

## 15. Balance process

1. Keep a balance spreadsheet (`balance.csv`) with each unit’s cost, health, damage per second, role, and counters.
2. After any unit change, run the headless runner on all role matchups and record win rates.
3. Targets: a counter wins at least 90% (9 of 10) of equal-value battles against what it counters (higher tiers, levels and techs may beat it); no unit except Titans wins more than 60% across all matchups; cost-efficiency within 15% across roles.
4. Run economy simulations to confirm credits are neither piling up unused nor too scarce.
5. Balance numbers live only in data files, and agents do not change them without human approval; propose changes in DECISIONS.md. Log every approved change in `balance.csv`.

## 16. Definition of done for any task

- It compiles with no errors.
- All tests pass.
- The feature works in the scene, not only in tests.
- New data and scripts follow the folder structure and naming in this document.
- The change is committed and logged.
- `HUMAN_TODO.md` lists anything the human must do next.

## 17. Decision log

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-10-27 | Unity, URP, C# | Biggest asset and tutorial ecosystem; agent-friendly |
| 2026-10-27 | Single-player vs AI only | Removes netcode risk for a solo project |
| 2026-10-27 | Launch with UNSC vs Covenant | Core loop first, Flood after |
| 2026-10-28 | No timers anywhere on deployment | Timers are a PvP feature |
| 2026-10-28 | Specialists removed entirely | Per the project owner |
| 2026-10-28 | Support and Anti-armor roles removed | Units fit into other roles; anti-armor merged into Single-target DPS |
| 2026-10-28 | Unlock prices: Tier 1 free, Tier 2 50, Tier 3 half the deployment price, Titan 350 | Per the project owner |
| 2026-10-28 | Deployment price bands: Tier 1 100 to 200, Tier 2 250 to 300, Tier 3 400 to 500, Titan 800 | Matches Mechabellum; balance is done through price |
| 2026-10-28 | Spells are rechargeable; reinforcements are single-use | Per the project owner |
| 2026-10-28 | Income cap 1600 (banked credits unlimited); level multiplier equals level number | Per the project owner |
| 2026-10-28 | XP needed to level up is the same at every level; level-up costs half the squad’s base price | Per the project owner |
| 2026-10-28 | Targeting is always the closest enemy | Keeps chaff relevant late in the game |
| 2026-10-28 | Directional defense is an exception (Shield Jackal 120 degrees, Hunters 240 degrees); squads can be rotated | Most units have none |
| 2026-10-28 | Starting draft of 5 faction and sub-faction cards | Every match plays differently |
| 2026-10-28 | Final game has 8 sub-factions (3 UNSC, 3 Covenant, 2 Flood); a faction may be missing from a draw | Per the project owner |
| 2026-10-28 | Destroyed units are remade each round | Per the project owner |
| 2026-10-28 | Anti-air units can hit ground, with bonus damage against air | Matches Mechabellum |
| 2026-10-28 | Only Infection Forms infect, except the Seeder, which infects armored units but not shielded ones | Matches Halo lore, per the project owner |
| 2026-10-28 | Elites are Unarmored with a shield | Per the project owner |
| 2026-10-28 | Titans are exempt from the 60% win-rate ceiling | Per the project owner |
| 2026-10-28 | Realistic but unpolished art; sub-factions add to base roster | Per the project owner |
| 2026-10-28 | Sandbox mode added at M4; arena size is a tunable data value | Helps balancing |
| 2026-10-29 | Movement lives in the simulation (continuous 2D with collision and steering) and Unity only displays it, with no NavMeshAgent; authentic movement outranks strict determinism | The game and the headless runner share the same movement logic |
| 2026-10-29 | Spells are permanent, recharge by impact, and the cooldown starts the round after use | Per the project owner |
| 2026-10-29 | Flood Titan is the Flood Abomination, not the Gravemind | Per the project owner |
| 2026-10-29 | Autonomous agent rules added to Section 1; agents propose balance changes instead of making them | Agents own implementation, the human owns design |
| 2026-10-29 | Spells are placed during deployment: blue outline for defensive (own side only), red for offensive (anywhere), hidden from the enemy until the round starts | Per the project owner |
| 2026-10-29 | Cards are offered every round after the first, alternating spells and reinforcements; skipping gives 50 (spells) or 100 (reinforcements) | Per the project owner |
| 2026-10-29 | Reinforcement cards: random squad and level against a growing budget; under budget adds copies of the squad until it passes the budget, exactly on budget is free, over budget costs the difference; the card also unlocks the unit | Per the project owner |
| 2026-10-31 | The anti-armor tag replaces the damage type row (against armor and against shields) | Approved from DECISIONS.md D1 |
| 2026-10-31 | Energy shields have their own multipliers, independent of armor: Ballistic 0.75, Explosive 1.0, Energy 1.5, Fire 0.5, Melee 1.0, Anti-armor 1.0. A hit that breaks the shield carries its remaining base damage over to health with the armor multiplier | Per the project owner (D2) |
| 2026-10-31 | Shield regen rate is a per-unit data field; the 5 second delay is global | Approved from D3 |
| 2026-10-31 | Arena is 60 squares wide; each army has a 30-square deployment zone with 2 squares of no man's land between (60 x 62). Several chaff bodies can share a square; squads usually fit in 5 x 2 squares | Per the project owner (D4) |
| 2026-10-31 | Units face their target when attacking and their direction of travel when moving; squad rotation only changes formation shape | Approved from D5 |
| 2026-10-31 | Range is measured from the shooter's centre to the target's edge | Approved from D6 |
| 2026-10-31 | Splash has no falloff and follows the attacker's target filter; a blocked projectile loses its splash | Approved from D7 |
| 2026-10-31 | The first weapon is a unit's main weapon and drives movement; other weapons pick their own targets | Approved from D8 |
| 2026-10-31 | Headless runner win = survivors' base damage is higher (statistics only) | Approved from D9 |
| 2026-10-31 | One deployment square = 2 m (`squareSize` in CombatRules.asset); not final, may be changed at any time | Approved from D10 |
| 2026-10-31 | Use the URP SRP Batcher; no GPU instancing work before real Halo models are imported | Per the project owner (D11) |
| 2026-10-31 | Health bars show when a unit is damaged, and for every body of the selected squad | Approved from D12 |
| 2026-10-31 | Units turn slower the bigger they are: each unit has a 180-degree turn time (starting guideline: chaff 0.25 s, 1-square units 1 s, 2x2-square units 2 s, Titans 3 s) | Per the project owner |
| 2026-10-31 | Units drive along their facing (a unit facing the wrong way turns on the spot first) and turn toward targets at their turn speed; the main weapon fires only when facing the target within 15 degrees (data value), other weapons fire in any direction | Approved from D13 |
| 2026-10-31 | Arena enlarged to 80 squares wide with 40-square deployment zones (80 x 82 in total) | Per the project owner (replaces 60 x 62) |
| 2026-10-31 | Squads keep formation while advancing and break it, for the rest of the battle, when ANY of these happens: their bodies target different enemies, they collide with another squad, or they engage in combat | Per the project owner; details approved (D14) |
| 2026-10-31 | Acquisition range: units only notice enemies within 3 x their range; with nothing in sight they march straight ahead; inside the enemy's deployment zone their acquisition range becomes unlimited (refines the Section 6 targeting rule "if nothing is in range, move toward the closest enemy") | Per the project owner; details approved (D15) |
| 2026-10-31 | Squad formations are very straight: 2 to 3 rows of perfectly aligned bodies (1 row for up to 4 bodies) | Per the project owner; details approved (D16) |
| 2026-10-31 | Starting credits 200 (was 400); income after each round is the next round's income (600, 800 ... capped at 1600); Tier 1 unlocks are free but use the round's unlock | Per the project owner (D17) |
| 2026-10-31 | Free draft squads appear in the middle of the zone. A bought squad is placed at the free spot nearest the middle: it drops in from high above and is then selected, ready to be moved. Free draft squads sell for half their cost | Per the project owner (D18) |
| 2026-10-31 | An exact tie is a tied round (damage undone, income paid, next round) instead of a replay | Per the project owner (D19) |
| 2026-10-31 | Until M4, both Standard draft cards use the placeholder units, squads use side colors, and the do-nothing AI only keeps its free squads | Approved from D20 |
| 2026-10-31 | First-pass stats for everything Section 8 leaves open (weapons, speeds, sizes, shields, arcs) are in the unit assets and `balance.csv` | Approved from D21 |
| 2026-10-31 | The Pelican carries no troops when bought; its 6-Marine drop (after 10 s) is an upgrade in M6. The Flamethrower Trooper's cone is 15 degrees with very low damage per very fast tick; an upgrade may widen it later | Per the project owner (D22) |
| 2026-10-31 | Placeholder test units never appear in normal matches; they are offered in the Sandbox only | Per the project owner (D24) |
| 2026-10-31 | Counter threshold: a counter wins at least 90% (9 of 10) of equal-value battles; replaces "55 to 70%" | Per the project owner (D23) |
| 2026-11-01 | The AI sees only public information and your army as it was at the last battle start; you see the enemy's last-battle army as grey ghosts during deployment, never its current deployment | Approved from D25, with the owner's addition |
| 2026-11-01 | XP bar = 2 x the squad's base price; a squad ready to level up shows a pulsing yellow arrow | Per the project owner (D26) |
| 2026-11-01 | First-pass tech catalogue kept until all units are made | Approved from D27 |
| 2026-11-01 | Faction UI: textured, ship-inspired panels (UNSC grey hull plating; Covenant violet with glowing blue lines) | Per the project owner (D28) |
| 2026-11-01 | Income builds on the 200 starting credits: 400 after round 1, 600 after round 2 ... capped at 1600 | Per the project owner (D29) |
| 2026-11-01 | Crowds: slower units step aside for much faster friends behind them that are at least as big (tanks and Titans never step aside for smaller units; those go around), the unit coming through drives straight on, smaller walkers give way to bigger ones, and a fighting unit edges toward its target (still firing) while a friend is stuck behind it | Per the project owner (D30) |
| 2026-11-01 | M4, M5 and M6 confirmed; D30 (crowd movement) approved | Per the project owner |
| 2026-11-01 | Offensive spells can hurt your own army. New cards: Smoke Bomb, EMP, Banshee Ambush, Supply Enhancement, items (Inventory, one per squad), Intensive Training, powerful buffs from round 7, extra deployment slot from round 5. ODSTs: 12 bodies, SMG or rockets by target. Nukes from round 7 with a 6 s delay. Reinforcements from round 4 | Per the project owner (D31, D32, D33) |
| 2026-11-01 | Smoke lasts the whole round; Intensive Training can be taken twice; Laser Sights +2 m; every item (no spells) in the Sandbox | Per the project owner (D33, D34) |
| 2026-11-01 | Items are dragged onto hexagons; no budget shown on reinforcement cards; blue shield bar over the health bar; line-shaped spells are placed in two clicks at any angle; Glassing Beam hits air | Per the project owner (D35) |
| 2026-11-01 | Battle speed stays real (1x, 2x, 4x). Every unit's move speed is halved in the data (Marines 1.5 m/s); Warthog, Ghost and Gauss Warthog get +1 m/s on top | Per the project owner, after a trial (D36) |
| 2026-11-01 | Artillery (Wraith, Archer Missile Pod) lobs its shots, aims ahead of the target and can miss. The Scarab's Focus Beam charges 2 s, fires 3 s at 400 damage per second in a small area, survives its target's death and drifts slowly to the next one | Per the project owner (D37) |
| 2026-11-01 | The Flood are built (M8): packs of 5 Infection Forms that an area hit hurts all at once, infection, the Seeder, Carrier Form, Infected Pelican and Phantom, Swarm Form as anti-air, Abomination aura and spawning; Normal and Hard AI avoid Unarmored units against them, Easy doesn't | Design left to the agent by the project owner (D38) |

## 18. Assumptions to confirm

These were interpreted by the author of this version. Agents should implement them as written and make them easy to change.

- Base damage per surviving unit defaults to (squad cost ÷ squad size) ÷ 5.
- Unit prices and stats in Section 8 are starting values inside the tier price bands, to be tuned with the headless runner.
- The reinforcement budget starts at a placeholder of 200 plus 50 per round number and is tuning data.
