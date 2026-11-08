# True counters: one squad against two

Every unit, one squad of it, against two squads of every other unit, whatever they cost; 10 battles a pair, half with the sides swapped,
no techs, no towers, no spells, on open ground (`Tools/Data/cw`, `dotnet run -c Release -- true`). A unit that beats two squads of another
with one of its own truly counters it.

- **Alone wins:** its average win rate, one squad against two, over every other unit.
- **Beats two:** how many of the other units it beats, two squads of them, at least half the time.
- **Beats two of:** the five it does best against while outnumbered two to one, with its win rate (equal ones: the strongest first).
- **Countered by:** the five whose single squad does best against two squads of it, with their win rate (equal ones: the weakest first).
- Only matchups won at least half the time are listed; "none" means no unit manages it.

Squads differ in cost: a squad of Grunts is cheaper than one Scarab, so this is about squads, not credits (the counter web, `counter_web.md`,
matches armies of equal value). The Hard AI doesn't use this table.

Written by `Tools/Data/gen_true_counter_doc.py`; do not edit by hand.

## UNSC (every sub-faction)

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Marine** | Chaff | 100 | 4% | 3 of 69 | Swarm Form 100%, Cannon Fodder 100%, Gremlin 60% | Brute Grenadier 100%, Wolverine 100%, Fuel Rod Grunt 100%, Archer Missile Pod 100%, Infested Wraith 100% |
| **Flamethrower Trooper** | ChaffClear | 150 | 7% | 5 of 69 | Infection Forms 100%, Infested Grunt 100%, Grunt 100%, Cannon Fodder 100%, Jackal (Shield) 100% | Brute Grenadier 100%, Wolverine 100%, Fuel Rod Grunt 100%, Archer Missile Pod 100%, Infested Wraith 100% |
| **Warthog** | Flanker | 200 | 0% | 0 of 69 | none | Cobra 100%, Infested Elite 100%, Stalker Form 100%, Combat Form 100%, Infested Marine 100% |
| **Gauss Warthog** | SingleTargetDps | 250 | 0% | 0 of 69 | none | Cannon Fodder 100%, Grunt 100%, Infested Elite 100%, Infested Grunt 100%, Combat Form 100% |
| **Archer Missile Pod** | Artillery | 300 | 15% | 10 of 69 | Heavy Grunts 100%, Fuel Rod Grunt 100%, Brute Grenadier 100%, Flamethrower Trooper 100%, Grunt 100% | Pelican 100%, Spartan CQB 100%, Covenant Phantom 100%, Vampire 100%, Spartan Sniper 100% |
| **Hornet** | Aerial | 300 | 39% | 28 of 69 | Zealot 100%, Hunter Captain 100%, Hunter Pair 100%, Infected Pelican 100%, Goliath 100% | Reaver 100%, Heavy Grunts 100%, Elite 100%, Spartan Sniper 100%, Seeder 100% |
| **Pelican** | Aerial | 300 | 20% | 14 of 69 | Longsword 100%, Carrier Form 100%, Spartan Grenadier 100%, Pure Form Ranged 100%, Infection Forms 100% | Seeder 100%, Grunt Goblin 100%, Grizzly 100%, Spartan 100%, Mantis 100% |
| **Scorpion** | Tank | 300 | 28% | 18 of 69 | Infection Forms 100%, Heavy Grunts 100%, Infested Marine 100%, Infested Grunt 100%, Fuel Rod Grunt 100% | Spartan Sniper 100%, Sparrowhawk 100%, Seeder 100%, Hornet 100%, Spartan 100% |
| **Wolverine** | AntiAir | 300 | 10% | 7 of 69 | Swarm Form 100%, Vampire 100%, Fuel Rod Grunt 100%, Flamethrower Trooper 100%, Marine 100% | Cobra 100%, Rhino 100%, Spartan CQB 100%, Spartan Fireteam 100%, Spartan Sniper 100% |
| **Mantis** | Tank | 500 | 46% | 32 of 69 | Swarm Form 100%, Carrier Form 100%, Vampire 100%, Covenant Phantom 100%, Spartan CQB 100% | Seeder 100%, Hunter Captain 100%, Zealot 100%, Elephant 100%, Scarab 100% |
| **Spartan** | SingleTargetDps | 500 | 45% | 31 of 69 | Vulture 100%, Goliath 100%, Grizzly 100%, Longsword 100%, Banshee 100% | Cannon Fodder 100%, Infection Forms 100%, Carrier Form 100%, Infected Phantom 100%, Infected Pelican 100% |
| **Elephant** | Titan | 800 | 89% | 61 of 69 | Hunter Pair 100%, Vulture 100%, Goliath 100%, Pure Form Tank 100%, Infected Phantom 100% | none |

## UNSC: Noble

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Spartan CQB** | Flanker | 400 | 22% | 15 of 69 | Seeder 100%, Sparrowhawk 100%, Swarm Form 100%, Vampire 100%, Covenant Phantom 100% | Grizzly 100%, Mantis 100%, Juggernaut 100%, Pure Form Tank 100%, Goliath 100% |
| **Spartan Grenadier** | ChaffClear | 400 | 21% | 15 of 69 | Heavy Grunts 100%, Fuel Rod Grunt 100%, Brute Grenadier 100%, Flamethrower Trooper 100%, Grunt 100% | Pelican 100%, Covenant Phantom 100%, Vampire 100%, Swarm Form 100%, Sparrowhawk 100% |
| **Spartan Sniper** | SingleTargetDps | 400 | 30% | 21 of 69 | Vulture 100%, Longsword 100%, Hornet 100%, Seeder 100%, Sparrowhawk 100% | Infested Marine 100%, Infection Forms 100%, Carrier Form 100%, Infected Pelican 100%, Zealot 100% |
| **Spartan Heavy Weapons** | Tank | 450 | 42% | 29 of 69 | Infested Banshee 100%, Hornet 100%, Seeder 100%, Infested Hornet 100%, Sparrowhawk 100% | Pure Form Tank 100%, Elephant 100%, Scarab 100%, Flood Abomination 100%, Goliath 50% |
| **Spartan Fireteam** | SingleTargetDps | 500 | 28% | 20 of 69 | Seeder 100%, Sparrowhawk 100%, Swarm Form 100%, Vampire 100%, Reaver 100% | Infected Pelican 100%, Elephant 100%, Scarab 100%, Flood Abomination 100% |

## UNSC: Aerospace Command

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Sparrowhawk** | Aerial | 250 | 33% | 23 of 69 | Hunter Captain 100%, Hunter Pair 100%, Goliath 100%, Pure Form Tank 100%, Juggernaut 100% | Infested Grunt 100%, Heavy Grunts 100%, Elite 100%, Spartan CQB 100%, Spartan Fireteam 100% |
| **Longsword** | Aerial | 300 | 41% | 29 of 69 | Zealot 100%, Hunter Captain 100%, Hunter Pair 100%, Goliath 100%, Juggernaut 100% | Reaver 100%, Pelican 100%, Covenant Phantom 100%, Vampire 100%, Spartan Sniper 100% |
| **Condor Gunship** | Aerial | 400 | 42% | 29 of 69 | Zealot 100%, Infected Pelican 100%, Longsword 100%, Infested Hornet 100%, Carrier Form 100% | Spawner 100%, Elephant 100%, Scarab 100%, Flood Abomination 100% |
| **Vulture** | Aerial | 400 | 49% | 34 of 69 | Zealot 100%, Infected Pelican 100%, Longsword 100%, Swarm Form 100%, Carrier Form 100% | Reaver 100%, Spartan Sniper 100%, Grunt Goblin 100%, Spartan 100%, Elephant 100% |

## UNSC: Armored Division

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Gremlin** | Flanker | 200 | 0% | 0 of 69 | none | Grunt 100%, Infested Elite 100%, Infested Grunt 100%, Combat Form 100%, Infested Marine 100% |
| **Cobra** | SingleTargetDps | 300 | 7% | 5 of 69 | Fuel Rod Grunt 100%, Wolverine 100%, Chopper 100%, Warthog 100%, Scorpion 90% | Combat Form 100%, Infection Forms 100%, Covenant Phantom 100%, Vampire 100%, Carrier Form 100% |
| **Rhino** | Artillery | 400 | 21% | 16 of 69 | Elite 100%, Heavy Grunts 100%, Fuel Rod Grunt 100%, Wolverine 100%, Brute Grenadier 100% | Swarm Form 100%, Sparrowhawk 100%, Infested Hornet 100%, Seeder 100%, Hornet 100% |
| **Grizzly** | Tank | 500 | 43% | 30 of 69 | Swarm Form 100%, Carrier Form 100%, Marauder 100%, Vampire 100%, Infested Scorpion 100% | Seeder 100%, Spartan 100%, Hunter Captain 100%, Zealot 100%, Elephant 100% |

## Covenant (every sub-faction)

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Grunt** | Chaff | 100 | 5% | 3 of 69 | Swarm Form 100%, Gauss Warthog 100%, Gremlin 100% | Flamethrower Trooper 100%, Brute Grenadier 100%, Fuel Rod Grunt 100%, Archer Missile Pod 100%, Infested Wraith 100% |
| **Fuel Rod Grunt** | ChaffClear | 150 | 10% | 8 of 69 | Flamethrower Trooper 100%, Grunt 100%, Marine 100%, Cannon Fodder 100%, Jackal (Shield) 100% | Cobra 100%, Wolverine 100%, Archer Missile Pod 100%, Infested Wraith 100%, Wraith 100% |
| **Jackal (Shield)** | Tank | 150 | 1% | 1 of 69 | Warthog 90% | Flamethrower Trooper 100%, Infested Ghost 100%, Brute Grenadier 100%, Infested Warthog 100%, Fuel Rod Grunt 100% |
| **Jackal (Beam Rifle)** | SingleTargetDps | 175 | 0% | 0 of 69 | none | Infested Ghost 100%, Infested Warthog 100%, Stalker Form 100%, Combat Form 100%, Infested Marine 100% |
| **Ghost** | Flanker | 200 | 0% | 0 of 69 | none | Infested Elite 100%, Infested Grunt 100%, Combat Form 100%, Infested Marine 100%, Infested Wraith 100% |
| **Banshee** | Aerial | 300 | 39% | 27 of 69 | Zealot 100%, Hunter Captain 100%, Hunter Pair 100%, Infected Pelican 100%, Goliath 100% | Spartan 100%, Elephant 100%, Scarab 100%, Flood Abomination 100%, Spartan Sniper 70% |
| **Covenant Phantom** | Aerial | 300 | 23% | 16 of 69 | Longsword 100%, Carrier Form 100%, Infested Scorpion 100%, Spartan Grenadier 100%, Pure Form Ranged 100% | Reaver 100%, Heavy Grunts 100%, Elite 100%, Spartan CQB 100%, Swarm Form 100% |
| **Elite** | SingleTargetDps | 300 | 21% | 15 of 69 | Hornet 100%, Seeder 100%, Infested Hornet 100%, Sparrowhawk 100%, Swarm Form 100% | Rhino 100%, Juggernaut 100%, Pure Form Tank 100%, Infected Pelican 100%, Hunter Pair 100% |
| **Vampire** | AntiAir | 300 | 25% | 17 of 69 | Longsword 100%, Carrier Form 100%, Infested Scorpion 100%, Spartan Grenadier 100%, Infection Forms 100% | Wolverine 100%, Reaver 100%, Heavy Grunts 100%, Elite 100%, Spartan CQB 100% |
| **Wraith** | Artillery | 300 | 19% | 14 of 69 | Heavy Grunts 100%, Fuel Rod Grunt 100%, Brute Grenadier 100%, Flamethrower Trooper 100%, Grunt 100% | Stalker Form 100%, Spartan CQB 100%, Covenant Phantom 100%, Vampire 100%, Spartan Sniper 100% |
| **Zealot** | SingleTargetDps | 400 | 58% | 40 of 69 | Juggernaut 100%, Mantis 100%, Grizzly 100%, Grunt Goblin 100%, Spartan Sniper 100% | Swarm Form 100%, Infested Hornet 100%, Hornet 100%, Infested Banshee 100%, Banshee 100% |
| **Hunter Pair** | Tank | 450 | 51% | 36 of 69 | Carrier Form 100%, Scorpion 100%, Marauder 100%, Infested Scorpion 100%, Spartan CQB 100% | Sparrowhawk 100%, Infested Hornet 100%, Seeder 100%, Hornet 100%, Infested Banshee 100% |
| **Scarab** | Titan | 800 | 91% | 63 of 69 | Hunter Captain 100%, Hunter Pair 100%, Vulture 100%, Goliath 100%, Pure Form Tank 100% | none |

## Covenant: Banished

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Brute Grenadier** | ChaffClear | 150 | 10% | 7 of 69 | Flamethrower Trooper 100%, Grunt 100%, Marine 100%, Cannon Fodder 100%, Skitterer 100% | Archer Missile Pod 100%, Infested Wraith 100%, Wraith 100%, Pure Form Ranged 100%, Pelican 100% |
| **Chopper** | Flanker | 200 | 2% | 1 of 69 | Infection Forms 100% | Cobra 100%, Infested Scorpion 100%, Vampire 100%, Marauder 100%, Scorpion 100% |
| **Marauder** | Tank | 300 | 26% | 19 of 69 | Infection Forms 100%, Infested Marine 100%, Infested Grunt 100%, Fuel Rod Grunt 100%, Infested Warthog 100% | Spartan Sniper 100%, Seeder 100%, Grizzly 100%, Spartan 100%, Vulture 100% |
| **Reaver** | AntiAir | 300 | 11% | 8 of 69 | Vulture 100%, Longsword 100%, Hornet 100%, Swarm Form 100%, Vampire 100% | Combat Form 100%, Infested Marine 100%, Infection Forms 100%, Spartan Fireteam 100%, Carrier Form 100% |

## Covenant: Lekgolo Colony

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Skitterer** | Flanker | 150 | 1% | 1 of 69 | Swarm Form 100% | Brute Grenadier 100%, Combat Form 100%, Archer Missile Pod 100%, Infection Forms 100%, Spartan Grenadier 100% |
| **Hunter Captain** | SingleTargetDps | 450 | 51% | 35 of 69 | Hunter Pair 100%, Mantis 100%, Grizzly 100%, Grunt Goblin 100%, Carrier Form 100% | Sparrowhawk 100%, Infested Hornet 100%, Seeder 100%, Hornet 100%, Infested Banshee 100% |
| **Goliath** | Tank | 500 | 48% | 34 of 69 | Carrier Form 100%, Spartan CQB 100%, Rhino 100%, Spartan Grenadier 100%, Pure Form Ranged 100% | Sparrowhawk 100%, Infested Hornet 100%, Seeder 100%, Hornet 100%, Infested Banshee 100% |

## Covenant: Unggoy Rebellion

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Cannon Fodder** | Chaff | 100 | 3% | 2 of 69 | Spartan 100%, Gauss Warthog 100% | Marine 100%, Flamethrower Trooper 100%, Brute Grenadier 100%, Wolverine 100%, Fuel Rod Grunt 100% |
| **Heavy Grunts** | SingleTargetDps | 200 | 18% | 12 of 69 | Hornet 100%, Seeder 100%, Sparrowhawk 100%, Swarm Form 100%, Vampire 100% | Archer Missile Pod 100%, Infested Wraith 100%, Wraith 100%, Pure Form Ranged 100%, Spartan Grenadier 100% |
| **Grunt Goblin** | Tank | 400 | 35% | 24 of 69 | Vulture 100%, Swarm Form 100%, Vampire 100%, Covenant Phantom 100%, Pelican 100% | Hunter Captain 100%, Zealot 100%, Spawner 100%, Elephant 100%, Scarab 100% |

## Flood (every sub-faction)

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Infection Forms** | Chaff | 100 | 19% | 13 of 69 | Spartan 100%, Spartan Sniper 100%, Reaver 100%, Cobra 100%, Cannon Fodder 100% | Chopper 100%, Flamethrower Trooper 100%, Infested Warthog 100%, Pelican 100%, Covenant Phantom 100% |
| **Carrier Form** | ChaffClear | 150 | 29% | 20 of 69 | Spartan 100%, Spartan Sniper 100%, Infection Forms 100%, Infested Grunt 100%, Reaver 100% | Pelican 100%, Covenant Phantom 100%, Vampire 100%, Swarm Form 100%, Sparrowhawk 100% |
| **Combat Form** | SingleTargetDps | 200 | 14% | 9 of 69 | Reaver 100%, Cobra 100%, Skitterer 100%, Jackal (Shield) 100%, Gauss Warthog 100% | Pelican 100%, Covenant Phantom 100%, Vampire 100%, Swarm Form 100%, Sparrowhawk 100% |
| **Pure Form Ranged** | Artillery | 250 | 20% | 15 of 69 | Heavy Grunts 100%, Fuel Rod Grunt 100%, Brute Grenadier 100%, Flamethrower Trooper 100%, Grunt 100% | Pelican 100%, Spartan CQB 100%, Covenant Phantom 100%, Spartan Sniper 100%, Sparrowhawk 100% |
| **Stalker Form** | Flanker | 250 | 13% | 9 of 69 | Wraith 100%, Infested Wraith 100%, Jackal (Shield) 100%, Jackal (Beam Rifle) 100%, Warthog 100% | Pelican 100%, Covenant Phantom 100%, Vampire 100%, Swarm Form 100%, Sparrowhawk 100% |
| **Swarm Form** | AntiAir | 250 | 32% | 22 of 69 | Zealot 100%, Infected Pelican 100%, Longsword 100%, Seeder 100%, Infested Hornet 100% | Skitterer 100%, Marine 100%, Grunt 100%, Wolverine 100%, Reaver 100% |
| **Infected Pelican** | Aerial | 400 | 50% | 34 of 69 | Spartan 100%, Spartan Sniper 100%, Spartan Fireteam 100%, Scorpion 100%, Marauder 100% | Swarm Form 100%, Infested Hornet 100%, Hornet 100%, Infested Banshee 100%, Banshee 100% |
| **Infected Phantom** | Aerial | 400 | 48% | 33 of 69 | Infected Pelican 100%, Spartan 100%, Longsword 100%, Seeder 100%, Sparrowhawk 100% | Elephant 100%, Scarab 100%, Flood Abomination 100% |
| **Pure Form Tank** | Tank | 400 | 48% | 33 of 69 | Spartan Heavy Weapons 100%, Carrier Form 100%, Spartan CQB 100%, Spartan Grenadier 100%, Elite 100% | Sparrowhawk 100%, Hornet 100%, Infested Banshee 100%, Banshee 100%, Elephant 100% |
| **Seeder** | SingleTargetDps | 475 | 35% | 24 of 69 | Hunter Captain 100%, Hunter Pair 100%, Goliath 100%, Mantis 100%, Grizzly 100% | Infested Ghost 100%, Infested Warthog 100%, Infested Elite 100%, Infested Grunt 100%, Infested Marine 100% |
| **Flood Abomination** | Titan | 800 | 96% | 66 of 69 | Spawner 100%, Zealot 100%, Hunter Pair 100%, Infected Pelican 100%, Vulture 100% | none |

## Flood: UNSC-based

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Infested Marine** | Chaff | 100 | 14% | 9 of 69 | Seeder 100%, Spartan Sniper 100%, Reaver 100%, Jackal (Shield) 100%, Gauss Warthog 100% | Infested Scorpion 100%, Marauder 100%, Scorpion 100%, Grunt Goblin 100%, Longsword 100% |
| **Infested Warthog** | Flanker | 200 | 10% | 7 of 69 | Seeder 100%, Infection Forms 100%, Jackal (Shield) 100%, Jackal (Beam Rifle) 100%, Wraith 90% | Infested Scorpion 100%, Marauder 100%, Scorpion 100%, Grunt Goblin 100%, Banshee 100% |
| **Infested Hornet** | Aerial | 250 | 34% | 23 of 69 | Zealot 100%, Hunter Captain 100%, Hunter Pair 100%, Infected Pelican 100%, Goliath 100% | Elite 100%, Swarm Form 100%, Condor Gunship 100%, Spartan Heavy Weapons 100%, Elephant 100% |
| **Infested Scorpion** | Tank | 250 | 23% | 16 of 69 | Infection Forms 100%, Infested Marine 100%, Infested Grunt 100%, Fuel Rod Grunt 100%, Brute Grenadier 100% | Covenant Phantom 100%, Vampire 100%, Swarm Form 100%, Sparrowhawk 100%, Infested Hornet 100% |
| **Juggernaut** | Flanker | 400 | 47% | 32 of 69 | Carrier Form 100%, Spartan CQB 100%, Rhino 100%, Spartan Grenadier 100%, Elite 100% | Sparrowhawk 100%, Infested Hornet 100%, Hornet 100%, Infested Banshee 100%, Banshee 100% |

## Flood: Covenant-based

| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |
|---|---|---|---|---|---|---|
| **Infested Grunt** | Chaff | 100 | 12% | 8 of 69 | Seeder 100%, Sparrowhawk 100%, Jackal (Shield) 100%, Gauss Warthog 100%, Ghost 100% | Flamethrower Trooper 100%, Infested Scorpion 100%, Marauder 100%, Scorpion 100%, Carrier Form 100% |
| **Infested Ghost** | Flanker | 200 | 8% | 6 of 69 | Seeder 100%, Jackal (Shield) 100%, Jackal (Beam Rifle) 100%, Infection Forms 90%, Wraith 80% | Vampire 100%, Scorpion 100%, Grunt Goblin 100%, Grizzly 100%, Mantis 100% |
| **Infested Banshee** | Aerial | 250 | 39% | 27 of 69 | Zealot 100%, Hunter Captain 100%, Hunter Pair 100%, Infected Pelican 100%, Goliath 100% | Spartan Heavy Weapons 100%, Elephant 100%, Scarab 100%, Flood Abomination 100% |
| **Infested Elite** | SingleTargetDps | 250 | 11% | 9 of 69 | Seeder 100%, Gauss Warthog 100%, Ghost 100%, Gremlin 100%, Warthog 100% | Carrier Form 100%, Pure Form Tank 100%, Infected Pelican 100%, Zealot 100%, Spawner 100% |
| **Infested Wraith** | Artillery | 250 | 16% | 11 of 69 | Heavy Grunts 100%, Fuel Rod Grunt 100%, Brute Grenadier 100%, Flamethrower Trooper 100%, Grunt 100% | Stalker Form 100%, Pelican 100%, Spartan CQB 100%, Covenant Phantom 100%, Vampire 100% |
| **Spawner** | Tank | 400 | 64% | 45 of 69 | Spartan 100%, Condor Gunship 100%, Grunt Goblin 100%, Seeder 100%, Sparrowhawk 100% | Flood Abomination 100% |
