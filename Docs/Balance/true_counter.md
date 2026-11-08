# True counters: one squad against two

Every unit, one squad of it, against two squads of every other unit, whatever they cost; 10 battles a pair, half with the sides swapped,
no techs, no towers, no spells, on open ground (`Tools/Data/cw`, `dotnet run -c Release -- true`). A unit that beats two squads of another
with one of its own truly counters it. Only units within 100 credits of each other are compared, so a squad faces squads of about its own price.

- **Alone wins:** its average win rate, one squad against two, over the units within 100 credits of it.
- **Beats two:** how many of the units within 100 credits of it it beats, two squads of them, at least half the time.
- **Beats two of:** the five it does best against while outnumbered two to one, with its win rate (equal ones: the strongest first).
- **Countered by:** the five whose single squad does best against two squads of it, with their win rate (equal ones: the weakest first).
- Only matchups won at least half the time are listed; "none" means no unit manages it.

The counter web (`counter_web.md`) matches armies of equal value instead. The Hard AI doesn't use this table.

Written by `Tools/Data/gen_true_counter_doc.py`; do not edit by hand.

## UNSC (every sub-faction)

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Marine** | Chaff | 100 | 8% | 2 of 20 | Cannon Fodder 100%, Gremlin 60% | Brute Grenadier 100%, Fuel Rod Grunt 100% |
| **Flamethrower Trooper** | ChaffClear | 150 | 17% | 5 of 30 | Infection Forms 100%, Infested Grunt 100%, Grunt 100%, Jackal (Shield) 100%, Cannon Fodder 100% | Infested Wraith 100%, Brute Grenadier 100%, Fuel Rod Grunt 100%, Infested Scorpion 100%, Pure Form Ranged 100% |
| **Warthog** | Flanker | 200 | 0% | 0 of 44 | none | Cobra 100%, Wraith 100%, Marauder 100%, Scorpion 100%, Infested Elite 100% |
| **Gauss Warthog** | SingleTargetDps | 250 | 0% | 0 of 38 | none | Infested Elite 100%, Pelican 100%, Combat Form 100%, Covenant Phantom 100%, Heavy Grunts 100% |
| **Archer Missile Pod** | Artillery | 300 | 2% | 1 of 44 | Heavy Grunts 100% | Spartan CQB 100%, Pelican 100%, Juggernaut 100%, Pure Form Tank 100%, Covenant Phantom 100% |
| **Hornet** | Aerial | 300 | 41% | 19 of 44 | Zealot 100%, Infected Pelican 100%, Longsword 100%, Infested Scorpion 100%, Pure Form Tank 100% | Reaver 100%, Elite 100%, Heavy Grunts 100%, Spartan Sniper 100%, Condor Gunship 90% |
| **Pelican** | Aerial | 300 | 18% | 8 of 44 | Longsword 100%, Pure Form Ranged 100%, Combat Form 100%, Infested Wraith 100%, Stalker Form 100% | Grunt Goblin 100%, Spawner 100%, Reaver 90% |
| **Scorpion** | Tank | 300 | 15% | 6 of 44 | Heavy Grunts 100%, Infested Warthog 100%, Infested Ghost 100%, Chopper 100%, Ghost 100% | Vulture 100%, Spartan Sniper 100%, Sparrowhawk 100%, Infected Pelican 100%, Zealot 100% |
| **Wolverine** | AntiAir | 300 | 5% | 2 of 44 | Swarm Form 100%, Vampire 100% | Rhino 100%, Cobra 100%, Grunt Goblin 100%, Spartan CQB 100%, Juggernaut 100% |
| **Mantis** | Tank | 500 | 5% | 1 of 21 | Spartan CQB 100% | Seeder 100%, Hunter Captain 100%, Zealot 100%, Spawner 60% |
| **Spartan** | SingleTargetDps | 500 | 25% | 5 of 21 | Seeder 100%, Vulture 100%, Goliath 100%, Rhino 100%, Grizzly 100% | Infected Phantom 100%, Infected Pelican 100%, Spawner 100% |
| **Elephant** | Titan | 800 | 0% | 0 of 2 | none | none |

## UNSC: Noble

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Spartan CQB** | Flanker | 400 | 17% | 6 of 35 | Seeder 100%, Vampire 100%, Covenant Phantom 100%, Wraith 100%, Wolverine 100% | Grizzly 100%, Mantis 100%, Hunter Pair 100%, Goliath 100%, Juggernaut 100% |
| **Spartan Grenadier** | ChaffClear | 400 | 3% | 1 of 35 | Wolverine 90% | Spartan Heavy Weapons 100%, Goliath 100%, Pelican 100%, Juggernaut 100%, Pure Form Tank 100% |
| **Spartan Sniper** | SingleTargetDps | 400 | 39% | 14 of 35 | Hornet 100%, Longsword 100%, Seeder 100%, Vulture 100%, Vampire 100% | Infected Pelican 100%, Zealot 100%, Spawner 100% |
| **Spartan Heavy Weapons** | Tank | 450 | 10% | 2 of 21 | Seeder 100%, Spartan Grenadier 100% | Pure Form Tank 100%, Goliath 50% |
| **Spartan Fireteam** | SingleTargetDps | 500 | 5% | 1 of 21 | Seeder 100% | Infected Pelican 100% |

## UNSC: Aerospace Command

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Sparrowhawk** | Aerial | 250 | 39% | 15 of 38 | Carrier Form 100%, Longsword 100%, Infested Scorpion 100%, Pure Form Ranged 100%, Fuel Rod Grunt 100% | Elite 100%, Heavy Grunts 100%, Swarm Form 100%, Infested Elite 60% |
| **Longsword** | Aerial | 300 | 37% | 17 of 44 | Zealot 100%, Infested Scorpion 100%, Pure Form Ranged 100%, Juggernaut 100%, Combat Form 100% | Reaver 100%, Pelican 100%, Covenant Phantom 100%, Condor Gunship 100%, Vampire 100% |
| **Condor Gunship** | Aerial | 400 | 25% | 9 of 35 | Zealot 100%, Infected Pelican 100%, Longsword 100%, Covenant Phantom 100%, Wraith 100% | Spawner 100% |
| **Vulture** | Aerial | 400 | 29% | 10 of 35 | Zealot 100%, Infected Pelican 100%, Longsword 100%, Scorpion 100%, Marauder 100% | Grunt Goblin 100%, Reaver 100%, Spartan 100%, Spartan Sniper 100%, Elite 80% |

## UNSC: Armored Division

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Gremlin** | Flanker | 200 | 0% | 0 of 44 | none | Grunt 100%, Infested Grunt 100%, Infested Elite 100%, Combat Form 100%, Elite 100% |
| **Cobra** | SingleTargetDps | 300 | 9% | 4 of 44 | Wolverine 100%, Chopper 100%, Warthog 100%, Scorpion 90% | Combat Form 100%, Covenant Phantom 100%, Condor Gunship 100%, Vampire 100%, Vulture 100% |
| **Rhino** | Artillery | 400 | 8% | 3 of 35 | Elite 100%, Wolverine 100%, Archer Missile Pod 70% | Goliath 100%, Juggernaut 100%, Spartan 100%, Seeder 100%, Vulture 100% |
| **Grizzly** | Tank | 500 | 5% | 1 of 21 | Spartan CQB 100% | Spartan 100%, Seeder 100%, Hunter Captain 100%, Zealot 100% |

## Covenant (every sub-faction)

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Grunt** | Chaff | 100 | 5% | 1 of 20 | Gremlin 100% | Flamethrower Trooper 100%, Brute Grenadier 100%, Fuel Rod Grunt 100%, Carrier Form 60%, Infection Forms 50% |
| **Fuel Rod Grunt** | ChaffClear | 150 | 24% | 8 of 30 | Flamethrower Trooper 100%, Marine 100%, Grunt 100%, Jackal (Shield) 100%, Cannon Fodder 100% | Infested Wraith 100%, Infested Scorpion 100%, Pure Form Ranged 100%, Infested Hornet 100%, Sparrowhawk 100% |
| **Jackal (Shield)** | Tank | 150 | 3% | 1 of 30 | Warthog 90% | Infested Ghost 100%, Infested Warthog 100%, Infested Grunt 100%, Flamethrower Trooper 100%, Stalker Form 100% |
| **Jackal (Beam Rifle)** | SingleTargetDps | 175 | 0% | 0 of 30 | none | Infested Ghost 100%, Infested Warthog 100%, Stalker Form 100%, Infested Wraith 100%, Combat Form 100% |
| **Ghost** | Flanker | 200 | 0% | 0 of 44 | none | Marauder 100%, Infested Grunt 100%, Scorpion 100%, Infested Elite 100%, Infested Wraith 100% |
| **Banshee** | Aerial | 300 | 41% | 18 of 44 | Zealot 100%, Infected Pelican 100%, Longsword 100%, Infested Scorpion 100%, Pure Form Tank 100% | Spartan Sniper 70%, Reaver 60% |
| **Covenant Phantom** | Aerial | 300 | 25% | 11 of 44 | Longsword 100%, Infested Scorpion 100%, Pure Form Ranged 100%, Combat Form 100%, Infested Wraith 100% | Grunt Goblin 100%, Spartan CQB 100%, Reaver 100%, Elite 100%, Heavy Grunts 100% |
| **Elite** | SingleTargetDps | 300 | 24% | 11 of 44 | Swarm Form 100%, Hornet 100%, Sparrowhawk 100%, Infested Hornet 100%, Vampire 100% | Rhino 100%, Juggernaut 100%, Pure Form Tank 100%, Infected Pelican 100%, Wraith 90% |
| **Vampire** | AntiAir | 300 | 27% | 12 of 44 | Longsword 100%, Infested Scorpion 100%, Combat Form 100%, Infested Wraith 100%, Stalker Form 100% | Wolverine 100%, Grunt Goblin 100%, Spartan CQB 100%, Reaver 100%, Elite 100% |
| **Wraith** | Artillery | 300 | 10% | 5 of 44 | Heavy Grunts 100%, Warthog 100%, Elite 90%, Ghost 70%, Gremlin 70% | Spartan CQB 100%, Stalker Form 100%, Juggernaut 100%, Pure Form Tank 100%, Covenant Phantom 100% |
| **Zealot** | SingleTargetDps | 400 | 41% | 14 of 35 | Spartan Sniper 100%, Juggernaut 100%, Reaver 100%, Scorpion 100%, Grunt Goblin 100% | Condor Gunship 100%, Vulture 100%, Longsword 100%, Banshee 100%, Hornet 100% |
| **Hunter Pair** | Tank | 450 | 10% | 2 of 21 | Spartan CQB 100%, Spartan Grenadier 70% | Seeder 100%, Hunter Captain 100% |
| **Scarab** | Titan | 800 | 0% | 0 of 2 | none | none |

## Covenant: Banished

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Brute Grenadier** | ChaffClear | 150 | 22% | 7 of 30 | Flamethrower Trooper 100%, Marine 100%, Grunt 100%, Skitterer 100%, Jackal (Shield) 100% | Infested Wraith 100%, Infested Scorpion 100%, Pure Form Ranged 100%, Infested Hornet 100%, Sparrowhawk 100% |
| **Chopper** | Flanker | 200 | 3% | 1 of 44 | Infection Forms 100% | Cobra 100%, Marauder 100%, Scorpion 100%, Infested Scorpion 100%, Vampire 100% |
| **Marauder** | Tank | 300 | 12% | 6 of 44 | Infested Warthog 100%, Chopper 100%, Ghost 100%, Warthog 100%, Infested Ghost 80% | Vulture 100%, Spartan Sniper 100%, Infected Pelican 100%, Zealot 100%, Spawner 100% |
| **Reaver** | AntiAir | 300 | 18% | 8 of 44 | Swarm Form 100%, Hornet 100%, Longsword 100%, Vulture 100%, Vampire 100% | Combat Form 100%, Juggernaut 100%, Pure Form Tank 100%, Spartan Sniper 100%, Infected Pelican 100% |

## Covenant: Lekgolo Colony

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Skitterer** | Flanker | 150 | 3% | 1 of 30 | Swarm Form 100% | Combat Form 100%, Brute Grenadier 100%, Infested Scorpion 100%, Infection Forms 100%, Infested Banshee 100% |
| **Hunter Captain** | SingleTargetDps | 450 | 33% | 7 of 21 | Spartan CQB 100%, Grunt Goblin 100%, Hunter Pair 100%, Rhino 100%, Grizzly 100% | Seeder 100% |
| **Goliath** | Tank | 500 | 17% | 4 of 21 | Spartan CQB 100%, Rhino 100%, Spartan Grenadier 100%, Spartan Heavy Weapons 50% | Spartan 100%, Seeder 100% |

## Covenant: Unggoy Rebellion

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Cannon Fodder** | Chaff | 100 | 0% | 0 of 20 | none | Marine 100%, Flamethrower Trooper 100%, Brute Grenadier 100%, Fuel Rod Grunt 100%, Heavy Grunts 100% |
| **Heavy Grunts** | SingleTargetDps | 200 | 25% | 11 of 44 | Swarm Form 100%, Hornet 100%, Sparrowhawk 100%, Vampire 100%, Covenant Phantom 100% | Archer Missile Pod 100%, Wraith 100%, Scorpion 100%, Infested Wraith 100%, Pure Form Ranged 100% |
| **Grunt Goblin** | Tank | 400 | 14% | 5 of 35 | Vulture 100%, Vampire 100%, Covenant Phantom 100%, Pelican 100%, Wolverine 100% | Hunter Captain 100%, Zealot 100%, Spawner 100%, Infected Pelican 90% |

## Flood (every sub-faction)

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Infection Forms** | Chaff | 100 | 40% | 8 of 20 | Skitterer 100%, Jackal (Shield) 100%, Cannon Fodder 100%, Ghost 100%, Gremlin 100% | Chopper 100%, Infested Warthog 100%, Flamethrower Trooper 100%, Carrier Form 100%, Infested Ghost 90% |
| **Carrier Form** | ChaffClear | 150 | 50% | 15 of 30 | Infection Forms 100%, Infested Elite 100%, Infested Grunt 100%, Skitterer 100%, Jackal (Shield) 100% | Infested Hornet 100%, Sparrowhawk 100%, Infested Banshee 100%, Swarm Form 100% |
| **Combat Form** | SingleTargetDps | 200 | 21% | 9 of 44 | Reaver 100%, Cobra 100%, Skitterer 100%, Jackal (Shield) 100%, Gauss Warthog 100% | Pelican 100%, Covenant Phantom 100%, Vampire 100%, Infested Hornet 100%, Longsword 100% |
| **Pure Form Ranged** | Artillery | 250 | 26% | 11 of 38 | Heavy Grunts 100%, Fuel Rod Grunt 100%, Brute Grenadier 100%, Flamethrower Trooper 100%, Jackal (Shield) 100% | Pelican 100%, Covenant Phantom 100%, Longsword 100%, Sparrowhawk 100%, Infested Banshee 80% |
| **Stalker Form** | Flanker | 250 | 21% | 8 of 38 | Infested Wraith 100%, Wraith 100%, Jackal (Shield) 100%, Jackal (Beam Rifle) 100%, Warthog 100% | Pelican 100%, Covenant Phantom 100%, Vampire 100%, Infested Hornet 100%, Longsword 100% |
| **Swarm Form** | AntiAir | 250 | 43% | 16 of 38 | Carrier Form 100%, Sparrowhawk 100%, Longsword 100%, Infested Hornet 100%, Infested Scorpion 100% | Skitterer 100%, Wolverine 100%, Reaver 100%, Elite 100%, Heavy Grunts 100% |
| **Infected Pelican** | Aerial | 400 | 40% | 14 of 35 | Spartan Sniper 100%, Spartan 100%, Elite 100%, Reaver 100%, Scorpion 100% | Condor Gunship 100%, Vulture 100%, Infected Phantom 100%, Banshee 100%, Hornet 100% |
| **Infected Phantom** | Aerial | 400 | 36% | 13 of 35 | Infected Pelican 100%, Longsword 100%, Seeder 100%, Vampire 100%, Spartan 100% | none |
| **Pure Form Tank** | Tank | 400 | 24% | 8 of 35 | Elite 100%, Reaver 100%, Spartan CQB 100%, Wraith 100%, Spartan Heavy Weapons 100% | Banshee 100%, Hornet 100%, Longsword 60% |
| **Seeder** | SingleTargetDps | 475 | 29% | 6 of 21 | Hunter Captain 100%, Goliath 100%, Hunter Pair 100%, Rhino 100%, Grizzly 100% | Spartan Fireteam 100%, Spartan Heavy Weapons 100%, Spartan CQB 100%, Spartan 100%, Infected Phantom 100% |
| **Flood Abomination** | Titan | 800 | 0% | 0 of 2 | none | none |

## Flood: UNSC-based

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Infested Marine** | Chaff | 100 | 25% | 5 of 20 | Jackal (Shield) 100%, Ghost 100%, Gremlin 100%, Jackal (Beam Rifle) 100%, Warthog 100% | Carrier Form 90% |
| **Infested Warthog** | Flanker | 200 | 11% | 5 of 44 | Infection Forms 100%, Jackal (Shield) 100%, Jackal (Beam Rifle) 100%, Wraith 90%, Warthog 50% | Marauder 100%, Scorpion 100%, Infested Scorpion 100%, Banshee 100%, Hornet 80% |
| **Infested Hornet** | Aerial | 250 | 37% | 14 of 38 | Carrier Form 100%, Longsword 100%, Infested Scorpion 100%, Fuel Rod Grunt 100%, Brute Grenadier 100% | Elite 100%, Swarm Form 100%, Heavy Grunts 70% |
| **Infested Scorpion** | Tank | 250 | 26% | 10 of 38 | Fuel Rod Grunt 100%, Brute Grenadier 100%, Flamethrower Trooper 100%, Infested Warthog 100%, Chopper 100% | Covenant Phantom 100%, Vampire 100%, Infested Hornet 100%, Longsword 100%, Sparrowhawk 100% |
| **Juggernaut** | Flanker | 400 | 23% | 8 of 35 | Elite 100%, Reaver 100%, Spartan CQB 100%, Wraith 100%, Rhino 100% | Longsword 100%, Zealot 100%, Banshee 100%, Hornet 100% |

## Flood: Covenant-based

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Infested Grunt** | Chaff | 100 | 15% | 3 of 20 | Jackal (Shield) 100%, Ghost 100%, Gremlin 100% | Flamethrower Trooper 100%, Carrier Form 100%, Fuel Rod Grunt 80% |
| **Infested Ghost** | Flanker | 200 | 10% | 5 of 44 | Jackal (Shield) 100%, Jackal (Beam Rifle) 100%, Infection Forms 90%, Wraith 80%, Infested Wraith 60% | Scorpion 100%, Vampire 100%, Marauder 80% |
| **Infested Banshee** | Aerial | 250 | 41% | 16 of 38 | Carrier Form 100%, Longsword 100%, Infested Scorpion 100%, Fuel Rod Grunt 100%, Brute Grenadier 100% | none |
| **Infested Elite** | SingleTargetDps | 250 | 18% | 8 of 38 | Gauss Warthog 100%, Ghost 100%, Gremlin 100%, Warthog 100%, Jackal (Shield) 90% | Carrier Form 100% |
| **Infested Wraith** | Artillery | 250 | 21% | 8 of 38 | Heavy Grunts 100%, Fuel Rod Grunt 100%, Brute Grenadier 100%, Flamethrower Trooper 100%, Jackal (Shield) 100% | Pelican 100%, Stalker Form 100%, Covenant Phantom 100%, Vampire 100%, Infested Hornet 100% |
| **Spawner** | Tank | 400 | 52% | 19 of 35 | Spartan Sniper 100%, Seeder 100%, Vampire 100%, Condor Gunship 100%, Spartan 100% | none |
