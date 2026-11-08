# The counter web: good against, bad against

Every unit against every other, one army against another of the same value (about 1,800 credits each), 10 battles a pair,
no techs, no towers, no spells, on open ground (`Tools/Data/cw`). **Good against:** the five it does best against, with its own win rate;
where several are equal, the strongest of them first (beating a strong unit says more). **Bad against:** the five it does worst against;
where several are equal, the weakest of them first (losing to a weak unit says more).
**Wins** is its average win rate against all the others. The Hard AI picks its counters from this same table.
It measures armies of one unit in a straight fight: a unit that shines behind a screen of chaff, or with levels and techs, can look worse here than in play.

Written by `Tools/Data/gen_counter_doc.py`; do not edit by hand.

## UNSC (every sub-faction)

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Marine** | Chaff | 100 | 61% | Spawner 100%, Infested Banshee 100%, Infected Phantom 100%, Zealot 100%, Condor Gunship 100% | Archer Missile Pod 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0%, Infection Forms 0%, Brute Grenadier 0% |
| **Flamethrower Trooper** | ChaffClear | 150 | 39% | Spawner 100%, Infested Marine 100%, Grunt 100%, Infested Grunt 100%, Carrier Form 100% | Wolverine 0%, Archer Missile Pod 0%, Fuel Rod Grunt 0%, Seeder 0%, Spartan Grenadier 0% |
| **Warthog** | Flanker | 200 | 18% | Infested Hornet 100%, Banshee 100%, Hornet 100%, Spartan Sniper 100%, Swarm Form 100% | Ghost 0%, Chopper 0%, Stalker Form 0%, Jackal (Shield) 0%, Spartan Fireteam 0% |
| **Gauss Warthog** | SingleTargetDps | 250 | 22% | Grunt Goblin 100%, Scorpion 100%, Pure Form Tank 100%, Grizzly 100%, Goliath 100% | Chopper 0%, Stalker Form 0%, Spartan CQB 0%, Jackal (Shield) 0%, Jackal (Beam Rifle) 0% |
| **Archer Missile Pod** | Artillery | 300 | 34% | Marine 100%, Heavy Grunts 100%, Grunt 100%, Cannon Fodder 100%, Elite 100% | Chopper 0%, Stalker Form 0%, Spartan CQB 0%, Seeder 0%, Covenant Phantom 0% |
| **Hornet** | Aerial | 300 | 54% | Infested Marine 100%, Zealot 100%, Hunter Pair 100%, Hunter Captain 100%, Infested Grunt 100% | Warthog 0%, Reaver 0%, Ghost 0%, Wolverine 0%, Jackal (Shield) 0% |
| **Pelican** | Aerial | 300 | 44% | Zealot 100%, Vulture 100%, Pure Form Tank 100%, Infested Wraith 100%, Pure Form Ranged 100% | Warthog 0%, Reaver 0%, Ghost 0%, Wolverine 0%, Spartan CQB 0% |
| **Scorpion** | Tank | 300 | 59% | Marine 100%, Heavy Grunts 100%, Grunt 100%, Infested Grunt 100%, Pure Form Tank 100% | Gauss Warthog 0%, Cobra 0%, Gremlin 0%, Seeder 0%, Covenant Phantom 0% |
| **Wolverine** | AntiAir | 300 | 28% | Infested Banshee 100%, Condor Gunship 100%, Vulture 100%, Infested Hornet 100%, Banshee 100% | Warthog 0%, Reaver 0%, Ghost 0%, Gauss Warthog 0%, Chopper 0% |
| **Mantis** | Tank | 500 | 44% | Condor Gunship 100%, Marine 100%, Grunt 100%, Cannon Fodder 100%, Spartan Heavy Weapons 100% | Gauss Warthog 0%, Cobra 0%, Gremlin 0%, Seeder 0%, Skitterer 0% |
| **Spartan** | SingleTargetDps | 500 | 52% | Scarab 100%, Elephant 100%, Vulture 100%, Hunter Pair 100%, Grunt Goblin 100% | Chopper 0%, Spartan CQB 0%, Jackal (Shield) 0%, Jackal (Beam Rifle) 0%, Spartan Fireteam 0% |
| **Elephant** | Titan | 800 | 87% | Infested Banshee 100%, Infected Phantom 100%, Infested Marine 100%, Condor Gunship 100%, Vulture 100% | Combat Form 0%, Spartan Sniper 0%, Spartan 0%, Hunter Captain 0%, Zealot 0% |

## UNSC: Noble

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Spartan CQB** | Flanker | 400 | 28% | Sparrowhawk 100%, Spartan 100%, Spartan Sniper 100%, Swarm Form 100%, Vampire 100% | Warthog 0%, Chopper 0%, Stalker Form 0%, Jackal (Shield) 0%, Cobra 0% |
| **Spartan Grenadier** | ChaffClear | 400 | 37% | Marine 100%, Heavy Grunts 100%, Grunt 100%, Cannon Fodder 100%, Elite 100% | Spartan CQB 0%, Archer Missile Pod 0%, Seeder 0%, Covenant Phantom 0%, Infested Warthog 0% |
| **Spartan Sniper** | SingleTargetDps | 400 | 50% | Scarab 100%, Elephant 100%, Vulture 100%, Hunter Pair 100%, Grunt Goblin 100% | Warthog 0%, Ghost 0%, Chopper 0%, Stalker Form 0%, Spartan CQB 0% |
| **Spartan Heavy Weapons** | Tank | 450 | 50% | Infested Banshee 100%, Vulture 100%, Infested Hornet 100%, Heavy Grunts 100%, Banshee 100% | Infected Pelican 0%, Infested Ghost 0%, Infested Warthog 0%, Infection Forms 0%, Combat Form 0% |
| **Spartan Fireteam** | SingleTargetDps | 500 | 34% | Infested Hornet 100%, Sparrowhawk 100%, Hornet 100%, Spartan 100%, Cannon Fodder 100% | Chopper 0%, Stalker Form 0%, Jackal (Shield) 0%, Jackal (Beam Rifle) 0%, Archer Missile Pod 0% |

## UNSC: Aerospace Command

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Sparrowhawk** | Aerial | 250 | 58% | Zealot 100%, Vulture 100%, Hunter Pair 100%, Grunt Goblin 100%, Hunter Captain 100% | Reaver 0%, Wolverine 0%, Spartan CQB 0%, Jackal (Shield) 0%, Jackal (Beam Rifle) 0% |
| **Longsword** | Aerial | 300 | 56% | Infested Marine 100%, Zealot 100%, Hunter Pair 100%, Hunter Captain 100%, Scorpion 100% | Reaver 0%, Wolverine 0%, Jackal (Beam Rifle) 0%, Infected Pelican 0%, Gremlin 0% |
| **Condor Gunship** | Aerial | 400 | 65% | Infested Banshee 100%, Infected Phantom 100%, Zealot 100%, Vulture 100%, Hunter Pair 100% | Reaver 0%, Wolverine 0%, Seeder 0%, Skitterer 0%, Mantis 0% |
| **Vulture** | Aerial | 400 | 65% | Infested Marine 100%, Zealot 100%, Hunter Pair 100%, Hunter Captain 100%, Marine 100% | Reaver 0%, Ghost 0%, Wolverine 0%, Jackal (Beam Rifle) 0%, Gremlin 0% |

## UNSC: Armored Division

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Gremlin** | Flanker | 200 | 36% | Vulture 100%, Infested Hornet 100%, Grunt Goblin 100%, Banshee 100%, Scorpion 100% | Gauss Warthog 0%, Stalker Form 0%, Spartan CQB 0%, Jackal (Shield) 0%, Jackal (Beam Rifle) 0% |
| **Cobra** | SingleTargetDps | 300 | 36% | Hunter Pair 100%, Grunt Goblin 100%, Scorpion 100%, Pure Form Tank 100%, Infested Scorpion 100% | Jackal (Shield) 0%, Spartan Fireteam 0%, Infected Pelican 0%, Gremlin 0%, Seeder 0% |
| **Rhino** | Artillery | 400 | 49% | Infested Marine 100%, Marine 100%, Heavy Grunts 100%, Grunt 100%, Pure Form Ranged 100% | Cobra 0%, Seeder 0%, Covenant Phantom 0%, Infection Forms 0%, Pelican 0% |
| **Grizzly** | Tank | 500 | 52% | Condor Gunship 100%, Marine 100%, Heavy Grunts 100%, Grunt 100%, Infested Grunt 100% | Gauss Warthog 0%, Cobra 0%, Gremlin 0%, Seeder 0%, Infested Elite 0% |

## Covenant (every sub-faction)

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Grunt** | Chaff | 100 | 59% | Spawner 100%, Infested Banshee 100%, Infected Phantom 100%, Zealot 100%, Condor Gunship 100% | Archer Missile Pod 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0%, Flamethrower Trooper 0%, Infection Forms 0% |
| **Fuel Rod Grunt** | ChaffClear | 150 | 37% | Marine 100%, Heavy Grunts 100%, Grunt 100%, Infested Grunt 100%, Spartan 100% | Wolverine 0%, Spartan CQB 0%, Jackal (Beam Rifle) 0%, Archer Missile Pod 0%, Cobra 0% |
| **Jackal (Shield)** | Tank | 150 | 29% | Infested Banshee 100%, Infested Hornet 100%, Banshee 100%, Sparrowhawk 100%, Hornet 100% | Chopper 0%, Stalker Form 0%, Wolverine 0%, Jackal (Beam Rifle) 0%, Archer Missile Pod 0% |
| **Jackal (Beam Rifle)** | SingleTargetDps | 175 | 33% | Vulture 100%, Banshee 100%, Sparrowhawk 100%, Longsword 100%, Hornet 100% | Warthog 0%, Chopper 0%, Stalker Form 0%, Spartan CQB 0%, Archer Missile Pod 0% |
| **Ghost** | Flanker | 200 | 21% | Vulture 100%, Infested Hornet 100%, Banshee 100%, Hornet 100%, Spartan Sniper 100% | Gauss Warthog 0%, Chopper 0%, Stalker Form 0%, Jackal (Shield) 0%, Spartan Fireteam 0% |
| **Banshee** | Aerial | 300 | 60% | Infected Phantom 100%, Zealot 100%, Hunter Pair 100%, Infested Hornet 100%, Hunter Captain 100% | Warthog 0%, Reaver 0%, Ghost 0%, Wolverine 0%, Jackal (Shield) 0% |
| **Covenant Phantom** | Aerial | 300 | 37% | Zealot 100%, Hunter Pair 100%, Hunter Captain 100%, Pure Form Tank 100%, Infested Wraith 100% | Warthog 0%, Reaver 0%, Ghost 0%, Wolverine 0%, Spartan CQB 0% |
| **Elite** | SingleTargetDps | 300 | 49% | Infested Banshee 100%, Infected Phantom 100%, Condor Gunship 100%, Vulture 100%, Infested Hornet 100% | Jackal (Beam Rifle) 0%, Archer Missile Pod 0%, Infected Pelican 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0% |
| **Vampire** | AntiAir | 300 | 45% | Zealot 100%, Vulture 100%, Banshee 100%, Pure Form Tank 100%, Infested Wraith 100% | Warthog 0%, Reaver 0%, Ghost 0%, Wolverine 0%, Spartan CQB 0% |
| **Wraith** | Artillery | 300 | 43% | Marine 100%, Heavy Grunts 100%, Grunt 100%, Infested Grunt 100%, Pure Form Ranged 100% | Gauss Warthog 0%, Cobra 0%, Seeder 0%, Covenant Phantom 0%, Mantis 0% |
| **Zealot** | SingleTargetDps | 400 | 70% | Scarab 100%, Elephant 100%, Spawner 100%, Hunter Pair 100%, Grunt Goblin 100% | Seeder 0%, Covenant Phantom 0%, Pelican 0%, Vampire 0%, Swarm Form 0% |
| **Hunter Pair** | Tank | 450 | 64% | Infested Marine 100%, Grunt Goblin 100%, Marine 100%, Heavy Grunts 100%, Grunt 100% | Cobra 0%, Seeder 0%, Covenant Phantom 0%, Pelican 0%, Vampire 0% |
| **Scarab** | Titan | 800 | 94% | Flood Abomination 100%, Elephant 100%, Infested Banshee 100%, Infected Phantom 100%, Infested Marine 100% | Spartan Sniper 0%, Spartan 0%, Zealot 0%, Spawner 0%, Combat Form 70% |

## Covenant: Banished

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Brute Grenadier** | ChaffClear | 150 | 41% | Marine 100%, Heavy Grunts 100%, Grunt 100%, Infested Grunt 100%, Spartan 100% | Wolverine 0%, Spartan CQB 0%, Jackal (Beam Rifle) 0%, Archer Missile Pod 0%, Fuel Rod Grunt 0% |
| **Chopper** | Flanker | 200 | 22% | Spartan 100%, Spartan Sniper 100%, Swarm Form 100%, Archer Missile Pod 100%, Spartan Fireteam 100% | Stalker Form 0%, Infected Pelican 0%, Cobra 0%, Fuel Rod Grunt 0%, Seeder 0% |
| **Marauder** | Tank | 300 | 48% | Condor Gunship 100%, Marine 100%, Heavy Grunts 100%, Grunt 100%, Infested Grunt 100% | Gauss Warthog 0%, Spartan Fireteam 0%, Cobra 0%, Gremlin 0%, Seeder 0% |
| **Reaver** | AntiAir | 300 | 21% | Condor Gunship 100%, Vulture 100%, Infested Hornet 100%, Banshee 100%, Sparrowhawk 100% | Warthog 0%, Ghost 0%, Gauss Warthog 0%, Chopper 0%, Stalker Form 0% |

## Covenant: Lekgolo Colony

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Skitterer** | Flanker | 150 | 43% | Spawner 100%, Infected Phantom 100%, Condor Gunship 100%, Sparrowhawk 100%, Spartan 100% | Archer Missile Pod 0%, Infected Pelican 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0%, Flamethrower Trooper 0% |
| **Hunter Captain** | SingleTargetDps | 450 | 62% | Flood Abomination 100%, Elephant 100%, Infested Marine 100%, Hunter Pair 100%, Grunt Goblin 100% | Seeder 0%, Covenant Phantom 0%, Combat Form 0%, Pelican 0%, Vampire 0% |
| **Goliath** | Tank | 500 | 50% | Infested Marine 100%, Condor Gunship 100%, Marine 100%, Infested Grunt 100%, Pure Form Tank 100% | Gauss Warthog 0%, Cobra 0%, Gremlin 0%, Fuel Rod Grunt 0%, Seeder 0% |

## Covenant: Unggoy Rebellion

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Cannon Fodder** | Chaff | 100 | 50% | Infested Banshee 100%, Infected Phantom 100%, Zealot 100%, Infested Hornet 100%, Hunter Captain 100% | Wolverine 0%, Spartan Fireteam 0%, Archer Missile Pod 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0% |
| **Heavy Grunts** | SingleTargetDps | 200 | 60% | Spawner 100%, Infested Banshee 100%, Infected Phantom 100%, Condor Gunship 100%, Infested Hornet 100% | Archer Missile Pod 0%, Infected Pelican 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0%, Infection Forms 0% |
| **Grunt Goblin** | Tank | 400 | 63% | Infested Banshee 100%, Condor Gunship 100%, Vulture 100%, Infested Hornet 100%, Marine 100% | Gauss Warthog 0%, Cobra 0%, Gremlin 0%, Seeder 0%, Combat Form 0% |

## Flood (every sub-faction)

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Infection Forms** | Chaff | 100 | 40% | Infested Marine 100%, Marine 100%, Heavy Grunts 100%, Grunt 100%, Infested Grunt 100% | Fuel Rod Grunt 0%, Seeder 0%, Spartan Grenadier 0%, Covenant Phantom 0%, Flamethrower Trooper 0% |
| **Carrier Form** | ChaffClear | 150 | 52% | Infested Marine 100%, Zealot 100%, Marine 100%, Heavy Grunts 100%, Grunt 100% | Seeder 0%, Covenant Phantom 0%, Flamethrower Trooper 0%, Pelican 0%, Vampire 0% |
| **Combat Form** | SingleTargetDps | 200 | 43% | Elephant 100%, Spawner 100%, Grunt Goblin 100%, Hunter Captain 100%, Infested Scorpion 100% | Archer Missile Pod 0%, Infected Pelican 0%, Fuel Rod Grunt 0%, Seeder 0%, Covenant Phantom 0% |
| **Pure Form Ranged** | Artillery | 250 | 57% | Marine 100%, Heavy Grunts 100%, Grunt 100%, Scorpion 100%, Carrier Form 100% | Seeder 0%, Covenant Phantom 0%, Wraith 0%, Pelican 0%, Vampire 0% |
| **Stalker Form** | Flanker | 250 | 23% | Spartan Sniper 100%, Gremlin 100%, Archer Missile Pod 100%, Spartan Fireteam 100%, Jackal (Beam Rifle) 100% | Infected Pelican 0%, Cobra 0%, Fuel Rod Grunt 0%, Seeder 0%, Spartan Grenadier 0% |
| **Swarm Form** | AntiAir | 250 | 48% | Infested Banshee 100%, Infected Phantom 100%, Zealot 100%, Hunter Pair 100%, Infested Hornet 100% | Warthog 0%, Reaver 0%, Ghost 0%, Chopper 0%, Wolverine 0% |
| **Infected Pelican** | Aerial | 400 | 35% | Heavy Grunts 100%, Spartan 100%, Spartan Heavy Weapons 100%, Spartan Sniper 100%, Elite 100% | Spartan CQB 0%, Fuel Rod Grunt 0%, Seeder 0%, Covenant Phantom 0%, Flamethrower Trooper 0% |
| **Infected Phantom** | Aerial | 400 | 75% | Zealot 100%, Vulture 100%, Hunter Pair 100%, Grunt Goblin 100%, Hunter Captain 100% | Skitterer 0%, Swarm Form 0%, Elite 0%, Cannon Fodder 0%, Infested Grunt 0% |
| **Pure Form Tank** | Tank | 400 | 58% | Infested Marine 100%, Marine 100%, Heavy Grunts 100%, Infested Grunt 100%, Infested Wraith 100% | Gauss Warthog 0%, Cobra 0%, Gremlin 0%, Seeder 0%, Covenant Phantom 0% |
| **Seeder** | SingleTargetDps | 475 | 37% | Condor Gunship 100%, Vulture 100%, Hunter Pair 100%, Grunt Goblin 100%, Hunter Captain 100% | Reaver 0%, Stalker Form 0%, Wolverine 0%, Spartan CQB 0%, Jackal (Shield) 0% |
| **Flood Abomination** | Titan | 800 | 96% | Spawner 100%, Infested Banshee 100%, Infected Phantom 100%, Infested Marine 100%, Zealot 100% | Hunter Captain 0%, Elephant 0%, Scarab 0%, Warthog 100%, Reaver 100% |

## Flood: UNSC-based

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Infested Marine** | Chaff | 100 | 71% | Spawner 100%, Infested Banshee 100%, Infected Phantom 100%, Condor Gunship 100%, Infested Hornet 100% | Flamethrower Trooper 0%, Infection Forms 0%, Rhino 0%, Juggernaut 0%, Goliath 0% |
| **Infested Warthog** | Flanker | 200 | 40% | Spartan 100%, Spartan Heavy Weapons 100%, Spartan Sniper 100%, Elite 100%, Swarm Form 100% | Fuel Rod Grunt 0%, Flamethrower Trooper 0%, Brute Grenadier 0%, Skitterer 0%, Combat Form 0% |
| **Infested Hornet** | Aerial | 250 | 64% | Infected Phantom 100%, Zealot 100%, Hunter Pair 100%, Hunter Captain 100%, Scorpion 100% | Warthog 0%, Reaver 0%, Ghost 0%, Wolverine 0%, Jackal (Shield) 0% |
| **Infested Scorpion** | Tank | 250 | 56% | Infested Marine 100%, Marine 100%, Heavy Grunts 100%, Grunt 100%, Infested Grunt 100% | Cobra 0%, Seeder 0%, Covenant Phantom 0%, Combat Form 0%, Pelican 0% |
| **Juggernaut** | Flanker | 400 | 50% | Infested Marine 100%, Marine 100%, Heavy Grunts 100%, Infested Grunt 100%, Pure Form Ranged 100% | Gauss Warthog 0%, Cobra 0%, Seeder 0%, Covenant Phantom 0%, Pelican 0% |

## Flood: Covenant-based

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Infested Grunt** | Chaff | 100 | 59% | Infected Phantom 100%, Condor Gunship 100%, Grunt Goblin 100%, Heavy Grunts 100%, Sparrowhawk 100% | Fuel Rod Grunt 0%, Flamethrower Trooper 0%, Infection Forms 0%, Brute Grenadier 0%, Wraith 0% |
| **Infested Ghost** | Flanker | 200 | 39% | Spartan 100%, Spartan Heavy Weapons 100%, Spartan Sniper 100%, Elite 100%, Swarm Form 100% | Fuel Rod Grunt 0%, Flamethrower Trooper 0%, Brute Grenadier 0%, Skitterer 0%, Combat Form 0% |
| **Infested Banshee** | Aerial | 250 | 76% | Infected Phantom 100%, Zealot 100%, Hunter Pair 100%, Infested Hornet 100%, Hunter Captain 100% | Wolverine 0%, Jackal (Shield) 0%, Swarm Form 0%, Elite 0%, Cannon Fodder 0% |
| **Infested Elite** | SingleTargetDps | 250 | 47% | Grunt Goblin 100%, Scorpion 100%, Sparrowhawk 100%, Infested Scorpion 100%, Spartan 100% | Archer Missile Pod 0%, Infected Pelican 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0%, Flamethrower Trooper 0% |
| **Infested Wraith** | Artillery | 250 | 58% | Marine 100%, Heavy Grunts 100%, Grunt 100%, Pure Form Ranged 100%, Carrier Form 100% | Seeder 0%, Covenant Phantom 0%, Pelican 0%, Vampire 0%, Swarm Form 0% |
| **Spawner** | Tank | 400 | 83% | Scarab 100%, Elephant 100%, Infected Phantom 100%, Condor Gunship 100%, Vulture 100% | Flamethrower Trooper 0%, Skitterer 0%, Combat Form 0%, Grunt 0%, Heavy Grunts 0% |
