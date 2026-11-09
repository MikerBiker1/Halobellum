# The 8 sub-factions and the 5-card draft (M10)

The owner said "Go for M10" and left the design open, in the spirit of the Flood (D38). Everything here is a first pass, for review. Every number is in `balance.csv` and in the generators under `Tools/Data`, and none of the 35 existing units' numbers changed.

## What the player sees

- **The starting draft is real now.** 5 cards from the 8 sub-factions. The same sub-faction never appears twice in a draw, and a faction may not appear at all. Each card names the faction and sub-faction, has one line on what it is about, lists its exclusive units, and shows the 2 free Tier 1 squads, which may include exclusives (marked "exclusive"). The temporary Standard cards are gone.
- **The shop** shows base units first, then exclusive ones, marked "exclusive". The title says "UNSC (Noble)".
- **Reinforcement cards** draw from the base roster and the sub-faction's exclusives, so they can hand a Noble player a Spartan without its 225 unlock.
- **The sandbox** lists every exclusive unit under its own heading, such as "UNSC: Noble".
- **The AI** gets its own draw. Normal and Hard count an exclusive squad as worth 40 credits more when picking a card, and want their exclusive units a little more when buying. Hard also has the measured win rates of all 73 units. Easy doesn't care.

## The rules I followed

- Exclusive units are added on top of the base roster. They never replace it.
- Unlock prices follow the Bible's table: Tier 1 free, Tier 2 50, Tier 3 half the price, Titans 350. Prices stay inside the tier bands.
- "Noble: expensive to unlock": I did not go beyond the table. Instead all 5 Spartans are Tier 3, so each costs 200 to 250 to unlock, where other sub-factions have cheap units too.
- Base damage is the Bible default, with one exception, the Goliath (below).
- All the speeds are final values on the scale after D36.

## New mechanics (general, deterministic, tested)

The brief says to prefer what exists, and almost every unit does. Only two things are new in the simulation, both data-driven:

1. **A hit that slows** (`hitSlowSeconds`, `hitSlowShare` on a weapon). The Gremlin's EMP. Titans shrug it off. It uses the slow an EMP spell already uses.
2. **Lifesteal from a unit's own data** (`lifestealShare`). Lifesteal already existed for items. The Hunter Captain's siphon laser uses it. An item on the same squad doesn't lower it: the stronger of the two applies. While an EMP has a +health tech switched off, a unit can be above its lower maximum for now: lifesteal and healing auras then simply don't add to it, and never take health away.

A third field is looks only: `bakedTint` and `bakedScale` let units that share a baked body look different.

What already existed and got new uses: leaps (Spartan CQB, Juggernaut, Infested Marines, Grunts and Elites), auras (Hunter Captain, Grunt Goblin), mid-battle drops (the Condor Gunship's, until D111), spawners (Spawner), death bursts (Methane Wagon), defense arcs (Goliath, Hunter Captain), lobbed shots, beams, and a melee main weapon with a gun on the side, which makes a unit charge in while firing (Chopper, Infested Warthog and Ghost).

## The units

"Wins" counts how many of the other 72 units it beats at equal cost, about 1800 credits an army, 10 battles a pair. Average is its mean win rate. Numbers come from `Tools/Data/gen_subfaction_doc.py`.

<!-- tables -->
### Noble

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Spartan Sniper | Single-target DPS | 3 | 200 | 1 | 400 | 650 | Unarmored | 200 | 1.75 | SRS99 Sniper Rifle (200 per 3 s, Ballistic, anti-armor, 42 m) | 35 of 68, 52% | Marine 100%, Warthog 100% |
| Spartan Heavy Weapons | Tank | 3 | 225 | 1 | 450 | 2400 | Unarmored | 250 | 1.3 | M247H Chaingun (14 per 0.12 s, Ballistic, 15 m); Hydra Rocket Pod (80 per 3.5 s, Explosive, 20 m, splash 1.5 m) | 50 of 68, 72% | Combat Form 100%, Hunter Captain 100% |
| Spartan Grenadier | Chaff clear | 3 | 200 | 1 | 400 | 1000 | Unarmored | 300 | 1.75 | M41 Rocket Launcher (62 per 1.5 s, Explosive, 19 m, splash 3.2 m) | 23 of 68, 35% | Scorpion 100%, Hunter Pair 100% |
| Spartan CQB | Flanker | 3 | 200 | 1 | 400 | 1200 | Unarmored | 450 | 3.5 | M45 Shotgun (80 per 0.8 s, Ballistic, 6 m, cone 25 deg) | 21 of 68, 33% | Flamethrower Trooper 100%, Scorpion 100% |
| Spartan Fireteam | Single-target DPS | 3 | 250 | 3 | 500 | 500 | Unarmored | 140 | 1.75 | BR85 Battle Rifle (22 per 0.6 s, Ballistic, 16 m); M41 Rocket Launcher (110 per 5 s, Explosive, anti-armor, 18 m) | 27 of 68, 40% | Marine 100%, Grunt 100% |

- **Spartan Sniper.** Kills one armored target at a time from very far away; has no answer to a crowd or to anything that reaches it.
- **Spartan Heavy Weapons.** A walking wall with a chaingun: mows down infantry, does little to armor. Its Unarmored body makes it weak to anti-infantry weapons once the shield is down.
- **Spartan Grenadier.** Rockets in a straight line: strong against crowds, weak against tanks and flankers.
- **Spartan CQB.** A thruster pack jumps it onto its target. Very strong against artillery and DPS once there; short reach.
- **Spartan Fireteam.** Three Spartans: an all-round squad with rifles and a rocket each. No single strength, no single weakness.

### Armored Division

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Gremlin | Flanker | 1 | 0 | 2 | 200 | 240 | Light | 0 | 4.5 | EMP Railgun (38 per 1 s, Energy, anti-armor, 13 m, slows 40% for 2 s) | 26 of 68, 38% | Flamethrower Trooper 100%, Fuel Rod Grunt 100% |
| Cobra | Single-target DPS | 2 | 50 | 1 | 300 | 650 | Heavy | 0 | 1.75 | M66 Railgun (175 per 2.2 s, Ballistic, anti-armor, 26 m) | 25 of 68, 37% | Marine 100%, Grunt 100% |
| Rhino | Artillery | 3 | 200 | 1 | 400 | 1000 | Heavy | 0 | 1 | Heavy Cannon (140 per 5 s, Explosive, 50 m, splash 4.5 m, lobbed) | 29 of 68, 44% | Hornet 100%, Pelican 100% |
| Grizzly | Tank | 3 | 250 | 1 | 500 | 2400 | Heavy | 0 | 1.35 | Twin Cannons (110 per 2.5 s, Explosive, 17 m, splash 2.5 m); Coaxial Machine Gun (8 per 0.2 s, Ballistic, 13 m) | 43 of 68, 63% | Gauss Warthog 100%, Spartan 100% |

- **Gremlin.** Fast scout whose EMP slows what it hits (Titans shrug it off). Good against vehicles, poor against infantry.
- **Cobra.** Six-wheeled tank hunter: its twin light railguns punch through heavy armor from far off. Outranges tanks; a swarm or a flanker gets under its guns.
- **Rhino.** The longest gun in the game. Slow to turn and move, lobbed so it can miss, helpless up close.
- **Grizzly.** The upgraded Scorpion: twin barrels and more armor, at a Tier 3 price.

### Aerospace Command

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Sparrowhawk | Aerial | 2 | 50 | 1 | 250 | 550 | Light | 0 | 4 | Heavy Autocannon (13 per 0.3 s, Ballistic, anti-armor, 14 m) | 33 of 68, 50% | Wolverine 100%, Vampire 100% |
| Longsword | Aerial | 2 | 50 | 1 | 300 | 600 | Light | 0 | 5 | Carpet Bombs (135 per 2.5 s, Explosive, 5 m, splash 5 m) | 34 of 68, 50% | Wolverine 100%, Vampire 100% |
| Vulture | Aerial | 3 | 200 | 1 | 400 | 650 | Heavy | 0 | 2.5 | Missile Barrage (44 per 3 s, Explosive, 22 m, splash 3 m); Chin Gun (10 per 0.2 s, Ballistic, 14 m) | 31 of 68, 45% | Wolverine 100%, Vampire 100% |
| Condor Gunship | Aerial | 3 | 250 | 1 | 500 | 1300 | Light | 0 | 3.25 | Twin Autocannons (20 per 0.25 s, Ballistic, 16 m); Light Spartan Laser (nose) (20 per 4 s, Energy, anti-armor, 20 m); Light Spartan Laser (front right) (20 per 4 s, Energy, anti-armor, 20 m); Light Spartan Laser (front left) (20 per 4 s, Energy, anti-armor, 20 m); Light Spartan Laser (rear right) (20 per 4 s, Energy, anti-armor, 20 m); Light Spartan Laser (rear left) (20 per 4 s, Energy, anti-armor, 20 m) | 36 of 68, 52% | Wolverine 100%, Vampire 100% |

- **Sparrowhawk.** Gunship that hunts vehicles. Its gun is poor against infantry.
- **Longsword.** Heavy bomber. It has to fly over its target, and cannot fight other aircraft at all.
- **Vulture.** The heaviest gunship: heavy armor, slow, long-range missiles.
- **Condor Gunship.** A gunship: twin autocannons under its belly and five light Spartan Lasers on turrets of their own, each finding its own target.

### Banished

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Chopper | Flanker | 1 | 0 | 2 | 200 | 300 | Light | 0 | 5.5 | Ramming Blades (60 per 1 s, Melee, 1.5 m, splash 1.5 m); Chopper Autocannons (10 per 0.25 s, Ballistic, 9 m) | 23 of 68, 35% | Flamethrower Trooper 100%, Scorpion 100% |
| Brute Grenadier | Chaff clear | 1 | 0 | 4 | 150 | 150 | Unarmored | 0 | 1.5 | Spike Grenade Launcher (31 per 2 s, Explosive, 13 m, splash 2.8 m, lobbed); Brute Shot Blade (35 per 1.5 s, Melee, 2 m) | 25 of 68, 38% | Scorpion 100%, Mantis 100% |
| Marauder | Tank | 2 | 50 | 1 | 300 | 1250 | Heavy | 0 | 1.5 | Plasma Cannon (80 per 2.2 s, Energy, 15 m, splash 2 m); Spike Turret (8 per 0.3 s, Ballistic, 12 m) | 31 of 68, 45% | Gauss Warthog 100%, Spartan 100% |
| Reaver | Anti-air | 2 | 50 | 1 | 300 | 700 | Light | 0 | 3 | Spike Rockets (24 per 0.5 s, Explosive, 20 m, x3 vs air) | 14 of 68, 21% | Marine 100%, Flamethrower Trooper 100% |

- **Chopper.** It rams: its main weapon is its front wheel, so it drives straight into the enemy, guns firing on the way.
- **Brute Grenadier.** Tough infantry with lobbed grenades. Shorter range than a Fuel Rod Grunt, can miss what moves, but takes much more killing.
- **Marauder.** The Banished battle tank. A plasma main gun, so a little better than a Scorpion against shields.
- **Reaver.** Ground anti-air walker, weak against vehicles.

### Lekgolo Colony

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Skitterer | Flanker | 1 | 0 | 6 | 125 | 100 | Light | 0 | 4.5 | Spitter Plasma (7 per 0.25 s, Energy, 7 m) | 31 of 68, 46% | Flamethrower Trooper 100%, Scorpion 100% |
| Goliath | Tank | 3 | 250 | 1 | 500 | 2200 | Heavy | 0 | 1.1 | Fist Slam (120 per 2 s, Melee, 3 m, splash 3.5 m); Fuel Rod Cannon (55 per 2.5 s, Explosive, 14 m, splash 2 m) | 34 of 68, 49% | Gauss Warthog 100%, Spartan 100% |
| Hunter Captain | Single-target DPS | 3 | 225 | 1 | 450 | 2000 | Heavy | 0 | 1.25 | Siphon Laser (19.5 per 0.1 s, Energy, anti-armor, 15 m, splash 0.3 m, beam); Shield Bash (120 per 1.5 s, Melee, 2.2 m, splash 1.5 m) | 43 of 68, 62% | Marine 100%, Grunt 100% |

- **Skitterer.** Spider-like machines worked by a few worms: a fast, light swarm of a flanker.
- **Goliath.** Two colonies in one suit. Its old job as a building-killer shows as 2.5 times the base damage if it survives a battle.
- **Hunter Captain.** Its siphon laser gives it back 30% of the damage it deals, and friends within 12 m heal 1% per second.

### Unggoy Rebellion

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Cannon Fodder | Chaff | 1 | 0 | 30 | 100 | 20 | Unarmored | 0 | 1.75 | Plasma Pistol (3 per 0.5 s, Energy, 9 m) | 35 of 68, 52% | Flamethrower Trooper 100%, Fuel Rod Grunt 100% |
| Heavy Grunts | Single-target DPS | 1 | 0 | 8 | 200 | 100 | Unarmored | 30 | 1.5 | Needle Rifle (8 per 0.25 s, Energy, 12 m) | 42 of 68, 62% | Marine 100%, Infection Forms 100% |
| Grunt Goblin | Tank | 3 | 200 | 1 | 400 | 1750 | Heavy | 0 | 2 | Goblin Plasma Cannons (10 per 0.2 s, Energy, 14 m); Goblin Fuel Rod (60 per 3 s, Explosive, 16 m, splash 2.5 m) | 50 of 68, 74% | Gauss Warthog 100%, Spartan 100% |

- **Cannon Fodder.** The largest squad in the game: 30 Grunts who barely fight. They are there to be shot instead of something else.
- **Heavy Grunts.** Grunts in heavy armor with a small shield, at the top of the Tier 1 price band.
- **Grunt Goblin.** A Grunt in a hero's mech. A quick tank with a strong aura: friends within 14 m move 20% faster and heal 0.6% per second.

### UNSC-based

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Infested Marine | Chaff | 1 | 0 | 12 | 100 | 140 | Unarmored | 0 | 3 | Scythe Limb (18 per 0.8 s, Melee, 1.5 m); Scavenged Rifle (4 per 0.5 s, Ballistic, 10 m) | 48 of 68, 72% | Flamethrower Trooper 100%, Carrier Form 100% |
| Infested Warthog | Flanker | 1 | 0 | 2 | 200 | 320 | Light | 0 | 5 | Ramming Mass (50 per 1 s, Melee, 1.5 m, splash 1.5 m); Scavenged Machine Gun (7 per 0.2 s, Ballistic, 10 m) | 30 of 68, 46% | Flamethrower Trooper 100%, Scorpion 100% |
| Infested Scorpion | Tank | 2 | 50 | 1 | 275 | 1350 | Heavy | 0 | 1.4 | Scavenged Cannon (72 per 3 s, Explosive, 14 m, splash 2.5 m); Tentacle Whip (30 per 1 s, Melee, 4 m) | 28 of 68, 40% | Gauss Warthog 100%, Spartan 100% |
| Infested Hornet | Aerial | 2 | 50 | 2 | 275 | 360 | Light | 0 | 4 | Scavenged Rockets (15 per 1 s, Explosive, 12 m); Spore Bombs (29 per 2 s, Explosive, 8 m, splash 3 m) | 34 of 68, 50% | Wolverine 100%, Swarm Form 100% |
| Juggernaut | Flanker | 3 | 200 | 1 | 400 | 2300 | Heavy | 0 | 3.25 | Crushing Charge (130 per 1.2 s, Melee, 2.5 m, splash 2.5 m) | 33 of 68, 48% | Scorpion 100%, Pure Form Tank 100% |

- **Infested Marine.** Fewer bodies than a Marine squad, each much tougher: it leaps in with a scythe limb. Cannot infect, as only Infection Forms do.
- **Infested Warthog.** Drives straight into the enemy, its gun still firing.
- **Infested Scorpion.** A little tougher and shorter-ranged than the real thing, with tentacles for what gets close.
- **Infested Hornet.** Barely flying, still bombing.
- **Juggernaut.** A pure form the size of a tank that moves like a flanker and leaps onto what it hunts.

### Covenant-based

| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Infested Ghost | Flanker | 1 | 0 | 2 | 200 | 330 | Light | 0 | 5.5 | Hull Ram (45 per 1 s, Melee, 1.5 m, splash 1.5 m); Scavenged Plasma Bolts (8 per 0.25 s, Energy, 9 m) | 39 of 68, 57% | Flamethrower Trooper 100%, Scorpion 100% |
| Infested Elite | Single-target DPS | 2 | 50 | 3 | 250 | 500 | Unarmored | 0 | 2 | Scythe Claw (46 per 0.7 s, Melee, anti-armor, 1.5 m); Scavenged Plasma Rifle (10 per 0.5 s, Energy, 11 m) | 46 of 68, 67% | Marine 100%, Grunt 100% |
| Infested Wraith | Artillery | 2 | 50 | 1 | 250 | 1000 | Heavy | 0 | 1 | Scavenged Mortar (70 per 3.5 s, Explosive, 34 m, splash 4 m, lobbed) | 38 of 68, 56% | Hornet 100%, Banshee 100% |
| Infested Banshee | Aerial | 2 | 50 | 2 | 250 | 360 | Light | 0 | 4.5 | Scavenged Plasma Cannons (8 per 0.3 s, Energy, 10 m); Spore Bomb (45 per 2.5 s, Explosive, 8 m, splash 3 m) | 50 of 68, 74% | Wolverine 100%, Swarm Form 100% |
| Spawner | Tank | 3 | 200 | 1 | 400 | 4000 | Light | 0 | 0.9 | Spore Lash (55 per 1.2 s, Melee, 3 m, splash 2 m) | 60 of 68, 88% | Gauss Warthog 0%, Spartan 0% |

- **Infested Ghost.** Faster and lighter than the Infested Warthog.
- **Infested Elite.** The classic combat form: no shield, more health, and a claw that opens armor.
- **Infested Wraith.** A little tougher and shorter-ranged than the real thing.
- **Infested Banshee.** Barely flying, still bombing.
- **Spawner.** A mobile birthing form: it fights badly, and keeps releasing two Infection Forms and three Combat Forms in turn, one lot every 2.5 seconds; the Combat Forms draw the enemy's fire. Kill it first (D79, D116). With its Swarm Brood tech it also hatches a Swarm Form 5 seconds into each battle (D113, D114).

<!-- end tables -->

## Choices worth a look

- **Names: "Infested", not "Infected".** "Infected Marine" is what the simulation already calls a Marine that rises in battle. The two would share results lines and model lookups, so the units you buy are "Infested".
- **Grunts as Flood.** In Halo's lore Grunts are poor hosts. I kept "Infested Grunt" because the Bible asks for variants of the Covenant roster, and Grunts already rise as infected in this game. Easy to swap for something else.
- **No infested Spartans**, because the Bible says Spartans can't be infected.
- **No infested Titans.** I first gave each Flood sub-faction one. The owner removed them after playing (D54), so the Abomination is the Flood's only Titan. The tables' win counts here were measured with those two still in the web (out of 72 opponents); nothing else about them changed.
- **Pure forms.** The base Flood already has the Stalker, Ranged and Tank forms. So the sub-factions add two from Halo Wars 2's own logs: the **Juggernaut** (UNSC-based) and the **Spawner** (Covenant-based).
- **Goliath's base damage is 250**, against the Bible default of 100. It stands for its Halo Wars 2 job as a building-killer. It is the only exception to the default, and easy to remove.
- **Hunter Captain has no taunt**, though it does in Halo Wars 2. Targeting is always "the closest enemy" (Bible Section 6), and a taunt would break that rule. It has its siphon and a healing aura instead.
- **Cobra has no lockdown mode**, Gremlin's EMP slows rather than stuns, and the Rhino is Tier 3, more than the Wraith and Archer's Tier 2, because of its reach.
- **Aerospace Command has no Tier 1 exclusive unit, and neither does Noble.** Their draft cards always give base Tier 1 squads. The Bible's band would allow a cheap one, but air or Spartans at Tier 1 seemed wrong.
- **Spartan bodies.** Noble's Spartans are the owner's Spartan model in five colours (D57), each with a placeholder weapon until real ones come (`Assets_to_Download.md`).
- **Condor Gunship** has its own model since the third batch. It first borrowed the Pelican's, with a grey tint.
- **The faction files are still called `*_Standard.asset`**, to keep every reference. Only the name is left over.

## Balance, first pass

- The counter web passed for all 73 units, before the two infested Titans (D54), the Blisterback and the Methane Wagon (D56) went, which were no other unit's best counter: every unit has a counter by the Section 7 table that wins at least 90%. It runs in about 2 minutes in the console (`Tools/Data/cw`), against about 30 in Unity.
- New units I tuned to stay under 60% average where I could. See the report (`python report.py` in `Tools/Data/cw`).
- **Over the 60% line now (not Titans), units I did not change:** Hunter Pair, Banshee, Scorpion, Spartan, Hornet, Marine. They are measured against a bigger field now, and I left their numbers alone as told.
- **Weak, as flankers and anti-air are across the game:** Reaver, Chopper, Infested Warthog and Ghost are level with Warthog and Ghost.
- **Battles:** 7% of pairs reach the 90 second limit, about the same as the 8% before.
- **AI against itself**, 216 matches (24 for each pairing of difficulties), rounds 6 to 10 on average, no match unfinished. Hard beat Normal 17 of 24 and Easy 24 of 24; Normal beat Easy 21 of 24. How often each sub-faction was picked, and how often it won:

| Sub-faction | Picked | Won | Share |
| --- | --- | --- | --- |
| Banished | 78 | 54 | 69% |
| Armored Division | 63 | 39 | 62% |
| Unggoy Rebellion | 81 | 44 | 54% |
| Aerospace Command | 39 | 18 | 46% |
| UNSC-based | 63 | 25 | 40% |
| Lekgolo Colony | 39 | 15 | 38% |
| Noble | 21 | 7 | 33% |
| Covenant-based | 48 | 14 | 29% |

  It's a small sample, and the AI picks by what a card's free squads are worth, so it rarely takes Noble, which has no Tier 1 exclusive. This table mixes in how well the AI plays each roster. It doesn't say how strong each one is in a person's hands. Banished looks strongest, Covenant-based and Noble weakest: watch those three first.

## Models wanted

Real models in the game now, after the owner's three batches (D45, D57 and the third):

- **Their own model:** Grizzly, Chopper, Cobra, Rhino, Sparrowhawk, Longsword, Vulture, Condor Gunship, Brute Grenadier, Marauder, Reaver, Skitterer, Goliath, Hunter Captain, Grunt Goblin, Infested Marine, Juggernaut, Spawner.
- **The Spartan's model, recoloured:** Spartan Sniper, Heavy Weapons, Grenadier, CQB, Fireteam.
- **Reused with a tint:** Infested Warthog, Scorpion, Hornet, Wraith, Banshee, Ghost and Elite, Infested Grunt, Cannon Fodder, Heavy Grunts.

Still a placeholder primitive among the sub-factions' units: the Gremlin (Armored Division). `Assets_to_Download.md` keeps the list of what is left to find.

Left out on purpose, as asked: `vehicleshumangroundmammoth.glb` (197,535 triangles, no unit in the Bible) and `halo_3_energy_sword_by_abisv.glb` (would suit melee units later).

## Tools

- `Tools/Data/gen_roster.py` makes the unit and tech assets, the faction assets' lists, `balance.csv` and `cw/units.json`. The sub-factions are `sub_*.py`, one file each.
- `Tools/Data/cw` is the console: `dotnet run -c Release -- 10` runs the full web, `-- 10 id,id` only those units, `-- ai 20` AI matches, `-- dur` battle lengths. `report.py` prints the sub-faction report. A battle that runs out of time with no damage dealt by either side, such as a Seeder against a Combat Form, counts as a win for neither.
- After a full console run, run `gen_roster.py` (the notes list each unit's two best counters), `gen_counter_list.py` (the test's list), `gen_matchup_asset.py cw/matrix.csv` (the Hard AI's table), and `gen_subfaction_doc.py` (this document).
- `Tools/Art/units/Grizzly.py`, `Chopper.py` prepare the models. `HB_PREPARED` says where to prepare them, since `Art_Source` is shared between worktrees.
- `RealModelBuilder.BuildNewFromCommandLine` and `AnimationBaker.BakeNewFromCommandLine` build only what is missing, leaving the old models untouched.
- `MatchAutoplay -autoplaySubFaction Noble` plays a sub-faction and buys its exclusives first. `SandboxAutoplay -gallery -galleryAll -galleryOnly :` shows every exclusive unit.
