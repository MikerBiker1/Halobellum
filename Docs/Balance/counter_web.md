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
| **Marine** | Chaff | 100 | 49% | Infected Phantom 100%, Infested Elite 100%, Zealot 100%, Condor Gunship 100%, Longsword 100% | Wolverine 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0%, Jackal (Shield) 0%, Archer Missile Pod 0% |
| **Flamethrower Trooper** | ChaffClear | 150 | 38% | Spawner 100%, Infested Elite 100%, Infested Marine 100%, Zealot 100%, Carrier Form 100% | Wolverine 0%, Fuel Rod Grunt 0%, Spartan Grenadier 0%, Archer Missile Pod 0%, Pure Form Ranged 0% |
| **Warthog** | Flanker | 200 | 25% | Infested Banshee 100%, Banshee 100%, Hornet 100%, Swarm Form 100%, Infested Hornet 100% | Stalker Form 0%, Infested Ghost 0%, Chopper 0%, Infested Warthog 0%, Jackal (Shield) 0% |
| **Gauss Warthog** | SingleTargetDps | 250 | 19% | Pure Form Tank 100%, Mantis 100%, Goliath 100%, Scorpion 100%, Marauder 100% | Stalker Form 0%, Fuel Rod Grunt 0%, Infested Ghost 0%, Infested Warthog 0%, Jackal (Beam Rifle) 0% |
| **Archer Missile Pod** | Artillery | 300 | 34% | Heavy Grunts 100%, Cannon Fodder 100%, Spartan Fireteam 100%, Grunt 100%, Elite 100% | Stalker Form 0%, Reaver 0%, Spartan Sniper 0%, Covenant Phantom 0%, Spartan CQB 0% |
| **Hornet** | Aerial | 300 | 54% | Infected Phantom 100%, Spawner 100%, Grizzly 100%, Zealot 100%, Carrier Form 100% | Ghost 0%, Wolverine 0%, Warthog 0%, Reaver 0%, Jackal (Beam Rifle) 0% |
| **Pelican** | Aerial | 300 | 82% | Infected Phantom 100%, Spawner 100%, Infested Elite 100%, Vulture 100%, Zealot 100% | Reaver 0%, Seeder 0%, Gremlin 0%, Hunter Captain 0%, Mantis 0% |
| **Scorpion** | Tank | 300 | 50% | Infested Marine 100%, Heavy Grunts 100%, Pure Form Tank 100%, Cannon Fodder 100%, Juggernaut 100% | Gauss Warthog 0%, Spartan Sniper 0%, Cobra 0%, Spartan 0%, Seeder 0% |
| **Wolverine** | AntiAir | 300 | 24% | Vulture 100%, Condor Gunship 100%, Infested Banshee 100%, Banshee 100%, Hornet 100% | Ghost 0%, Gauss Warthog 0%, Stalker Form 0%, Warthog 0%, Infested Ghost 0% |
| **Mantis** | Tank | 500 | 57% | Pelican 100%, Infested Banshee 100%, Cannon Fodder 100%, Swarm Form 100%, Infested Hornet 100% | Gauss Warthog 0%, Infected Pelican 0%, Cobra 0%, Spartan 0%, Gremlin 0% |
| **Spartan** | SingleTargetDps | 500 | 46% | Scarab 100%, Elephant 100%, Grunt Goblin 100%, Grizzly 100%, Vulture 100% | Ghost 0%, Stalker Form 0%, Fuel Rod Grunt 0%, Infested Ghost 0%, Chopper 0% |
| **Elephant** | Titan | 800 | 84% | Pelican 100%, Grunt Goblin 100%, Infected Phantom 100%, Grizzly 100%, Infested Marine 100% | Spartan Sniper 0%, Spartan 0%, Combat Form 0%, Condor Gunship 0%, Zealot 0% |

## UNSC: Noble

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Spartan CQB** | Flanker | 300 | 39% | Spartan Fireteam 100%, Infested Wraith 100%, Gremlin 100%, Spartan Heavy Weapons 100%, Spartan 100% | Jackal (Shield) 0%, Flamethrower Trooper 0%, Covenant Phantom 0%, Vampire 0%, Seeder 0% |
| **Spartan Grenadier** | ChaffClear | 400 | 31% | Heavy Grunts 100%, Cannon Fodder 100%, Grunt 100%, Skitterer 100%, Gremlin 100% | Archer Missile Pod 0%, Covenant Phantom 0%, Spartan CQB 0%, Rhino 0%, Wraith 0% |
| **Spartan Heavy Weapons** | Tank | 400 | 47% | Vulture 100%, Condor Gunship 100%, Heavy Grunts 100%, Longsword 100%, Infested Banshee 100% | Stalker Form 0%, Warthog 0%, Chopper 0%, Infested Warthog 0%, Jackal (Shield) 0% |
| **Spartan Sniper** | SingleTargetDps | 400 | 37% | Elephant 100%, Grizzly 100%, Vulture 100%, Condor Gunship 100%, Hunter Pair 100% | Ghost 0%, Stalker Form 0%, Warthog 0%, Fuel Rod Grunt 0%, Infested Ghost 0% |
| **Spartan Fireteam** | SingleTargetDps | 500 | 52% | Heavy Grunts 100%, Infested Banshee 100%, Banshee 100%, Hornet 100%, Sparrowhawk 100% | Jackal (Shield) 0%, Archer Missile Pod 0%, Pure Form Ranged 0%, Spartan CQB 0%, Infected Pelican 0% |

## UNSC: Aerospace Command

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Sparrowhawk** | Aerial | 250 | 54% | Grizzly 100%, Vulture 100%, Zealot 100%, Carrier Form 100%, Longsword 100% | Wolverine 0%, Infested Ghost 0%, Infested Warthog 0%, Reaver 0%, Jackal (Beam Rifle) 0% |
| **Longsword** | Aerial | 300 | 60% | Spawner 100%, Grizzly 100%, Infested Marine 100%, Zealot 100%, Carrier Form 100% | Reaver 0%, Covenant Phantom 0%, Vampire 0%, Spartan 0%, Spartan Heavy Weapons 0% |
| **Vulture** | Aerial | 400 | 66% | Infected Phantom 100%, Grizzly 100%, Zealot 100%, Carrier Form 100%, Longsword 100% | Wolverine 0%, Reaver 0%, Jackal (Beam Rifle) 0%, Spartan Sniper 0%, Vampire 0% |
| **Condor Gunship** | Aerial | 500 | 65% | Elephant 100%, Grizzly 100%, Vulture 100%, Zealot 100%, Carrier Form 100% | Wolverine 0%, Reaver 0%, Jackal (Beam Rifle) 0%, Jackal (Shield) 0%, Spartan Sniper 0% |

## UNSC: Armored Division

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Gremlin** | Flanker | 200 | 50% | Pelican 100%, Grizzly 100%, Vulture 100%, Condor Gunship 100%, Longsword 100% | Fuel Rod Grunt 0%, Infested Ghost 0%, Infested Warthog 0%, Spartan Grenadier 0%, Jackal (Shield) 0% |
| **Cobra** | SingleTargetDps | 300 | 41% | Grunt Goblin 100%, Grizzly 100%, Pure Form Tank 100%, Mantis 100%, Hunter Pair 100% | Infested Ghost 0%, Jackal (Shield) 0%, Brute Grenadier 0%, Spartan Sniper 0%, Covenant Phantom 0% |
| **Rhino** | Artillery | 400 | 40% | Infested Marine 100%, Heavy Grunts 100%, Cannon Fodder 100%, Spartan Fireteam 100%, Grunt 100% | Covenant Phantom 0%, Spartan CQB 0%, Cobra 0%, Infection Forms 0%, Wraith 0% |
| **Grizzly** | Tank | 500 | 68% | Pelican 100%, Infested Marine 100%, Heavy Grunts 100%, Infested Banshee 100%, Pure Form Tank 100% | Spartan Sniper 0%, Cobra 0%, Spartan 0%, Gremlin 0%, Hunter Captain 0% |

## Covenant (every sub-faction)

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Grunt** | Chaff | 100 | 51% | Infected Phantom 100%, Infested Elite 100%, Zealot 100%, Condor Gunship 100%, Longsword 100% | Fuel Rod Grunt 0%, Jackal (Beam Rifle) 0%, Spartan Grenadier 0%, Archer Missile Pod 0%, Pure Form Ranged 0% |
| **Fuel Rod Grunt** | ChaffClear | 150 | 26% | Heavy Grunts 100%, Cannon Fodder 100%, Grunt 100%, Elite 100%, Skitterer 100% | Wolverine 0%, Reaver 0%, Jackal (Beam Rifle) 0%, Spartan Grenadier 0%, Archer Missile Pod 0% |
| **Jackal (Shield)** | Tank | 150 | 34% | Condor Gunship 100%, Infested Banshee 100%, Banshee 100%, Hornet 100%, Sparrowhawk 100% | Stalker Form 0%, Wolverine 0%, Fuel Rod Grunt 0%, Infested Ghost 0%, Chopper 0% |
| **Jackal (Beam Rifle)** | SingleTargetDps | 175 | 31% | Vulture 100%, Condor Gunship 100%, Banshee 100%, Hornet 100%, Sparrowhawk 100% | Stalker Form 0%, Warthog 0%, Infested Ghost 0%, Chopper 0%, Infested Warthog 0% |
| **Ghost** | Flanker | 200 | 16% | Banshee 100%, Hornet 100%, Swarm Form 100%, Infested Hornet 100%, Spartan 100% | Gauss Warthog 0%, Stalker Form 0%, Warthog 0%, Fuel Rod Grunt 0%, Infested Ghost 0% |
| **Banshee** | Aerial | 300 | 56% | Infected Phantom 100%, Spawner 100%, Grizzly 100%, Zealot 100%, Carrier Form 100% | Ghost 0%, Wolverine 0%, Warthog 0%, Reaver 0%, Jackal (Beam Rifle) 0% |
| **Covenant Phantom** | Aerial | 300 | 39% | Spawner 100%, Zealot 100%, Carrier Form 100%, Longsword 100%, Pure Form Tank 100% | Ghost 0%, Wolverine 0%, Warthog 0%, Infested Ghost 0%, Chopper 0% |
| **Elite** | SingleTargetDps | 300 | 51% | Vulture 100%, Condor Gunship 100%, Longsword 100%, Infested Banshee 100%, Banshee 100% | Fuel Rod Grunt 0%, Jackal (Beam Rifle) 0%, Archer Missile Pod 0%, Pure Form Ranged 0%, Brute Grenadier 0% |
| **Vampire** | AntiAir | 300 | 44% | Spawner 100%, Vulture 100%, Zealot 100%, Carrier Form 100%, Longsword 100% | Ghost 0%, Wolverine 0%, Warthog 0%, Infested Ghost 0%, Chopper 0% |
| **Wraith** | Artillery | 300 | 42% | Infested Marine 100%, Heavy Grunts 100%, Cannon Fodder 100%, Spartan Fireteam 100%, Grunt 100% | Gauss Warthog 0%, Spartan Sniper 0%, Covenant Phantom 0%, Spartan CQB 0%, Cobra 0% |
| **Zealot** | SingleTargetDps | 400 | 65% | Elephant 100%, Grunt Goblin 100%, Infested Elite 100%, Grizzly 100%, Pure Form Tank 100% | Flamethrower Trooper 0%, Covenant Phantom 0%, Vampire 0%, Seeder 0%, Marine 0% |
| **Hunter Pair** | Tank | 450 | 56% | Infested Marine 100%, Carrier Form 100%, Heavy Grunts 100%, Cannon Fodder 100%, Mantis 100% | Spartan Sniper 0%, Covenant Phantom 0%, Cobra 0%, Vampire 0%, Spartan 0% |
| **Scarab** | Titan | 800 | 92% | Elephant 100%, Pelican 100%, Grunt Goblin 100%, Grizzly 100%, Infested Marine 100% | Spartan 0%, Spawner 0%, Flood Abomination 0%, Combat Form 20%, Infected Phantom 40% |

## Covenant: Banished

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Brute Grenadier** | ChaffClear | 150 | 37% | Infested Elite 100%, Heavy Grunts 100%, Cannon Fodder 100%, Combat Form 100%, Grunt 100% | Jackal (Beam Rifle) 0%, Spartan Grenadier 0%, Archer Missile Pod 0%, Pure Form Ranged 0%, Covenant Phantom 0% |
| **Chopper** | Flanker | 175 | 28% | Swarm Form 100%, Infested Hornet 100%, Seeder 100%, Spartan Heavy Weapons 100%, Spartan 100% | Gauss Warthog 0%, Pure Form Ranged 0%, Brute Grenadier 0%, Flamethrower Trooper 0%, Spartan CQB 0% |
| **Reaver** | AntiAir | 250 | 29% | Pelican 100%, Vulture 100%, Condor Gunship 100%, Longsword 100%, Infested Banshee 100% | Gauss Warthog 0%, Stalker Form 0%, Warthog 0%, Infested Ghost 0%, Chopper 0% |
| **Marauder** | Tank | 300 | 48% | Infested Marine 100%, Heavy Grunts 100%, Cannon Fodder 100%, Juggernaut 100%, Swarm Form 100% | Gauss Warthog 0%, Spartan Sniper 0%, Cobra 0%, Spartan 0%, Infested Scorpion 0% |

## Covenant: Lekgolo Colony

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Skitterer** | Flanker | 150 | 50% | Infected Phantom 100%, Infested Elite 100%, Condor Gunship 100%, Longsword 100%, Infested Banshee 100% | Fuel Rod Grunt 0%, Spartan Grenadier 0%, Archer Missile Pod 0%, Pure Form Ranged 0%, Brute Grenadier 0% |
| **Hunter Captain** | SingleTargetDps | 450 | 50% | Flood Abomination 100%, Grunt Goblin 100%, Grizzly 100%, Infested Marine 100%, Carrier Form 100% | Jackal (Shield) 0%, Brute Grenadier 0%, Spartan Sniper 0%, Flamethrower Trooper 0%, Covenant Phantom 0% |
| **Goliath** | Tank | 500 | 50% | Infested Marine 100%, Mantis 100%, Hunter Pair 100%, Spartan Fireteam 100%, Elite 100% | Gauss Warthog 0%, Spartan Sniper 0%, Covenant Phantom 0%, Cobra 0%, Vampire 0% |

## Covenant: Unggoy Rebellion

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Cannon Fodder** | Chaff | 100 | 57% | Infected Phantom 100%, Infested Elite 100%, Zealot 100%, Condor Gunship 100%, Longsword 100% | Fuel Rod Grunt 0%, Spartan Grenadier 0%, Archer Missile Pod 0%, Pure Form Ranged 0%, Brute Grenadier 0% |
| **Heavy Grunts** | SingleTargetDps | 200 | 62% | Infected Phantom 100%, Spawner 100%, Infested Elite 100%, Vulture 100%, Zealot 100% | Fuel Rod Grunt 0%, Spartan Grenadier 0%, Archer Missile Pod 0%, Pure Form Ranged 0%, Brute Grenadier 0% |
| **Grunt Goblin** | Tank | 400 | 80% | Pelican 100%, Grizzly 100%, Infested Marine 100%, Vulture 100%, Condor Gunship 100% | Cobra 0%, Spartan 0%, Hunter Captain 0%, Combat Form 0%, Zealot 0% |

## Flood (every sub-faction)

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Infection Forms** | Chaff | 100 | 42% | Infested Elite 100%, Infested Marine 100%, Heavy Grunts 100%, Cannon Fodder 100%, Combat Form 100% | Chopper 0%, Spartan Grenadier 0%, Pure Form Ranged 0%, Flamethrower Trooper 0%, Covenant Phantom 0% |
| **Carrier Form** | ChaffClear | 150 | 64% | Infested Elite 100%, Infested Marine 100%, Zealot 100%, Heavy Grunts 100%, Cannon Fodder 100% | Flamethrower Trooper 0%, Covenant Phantom 0%, Vampire 0%, Seeder 0%, Hunter Captain 0% |
| **Combat Form** | SingleTargetDps | 200 | 55% | Elephant 100%, Grunt Goblin 100%, Infested Elite 100%, Grizzly 100%, Mantis 100% | Brute Grenadier 0%, Flamethrower Trooper 0%, Covenant Phantom 0%, Infected Pelican 0%, Infection Forms 0% |
| **Pure Form Ranged** | Artillery | 250 | 36% | Heavy Grunts 100%, Cannon Fodder 100%, Swarm Form 100%, Spartan Fireteam 100%, Grunt 100% | Stalker Form 0%, Archer Missile Pod 0%, Spartan Sniper 0%, Covenant Phantom 0%, Spartan CQB 0% |
| **Stalker Form** | Flanker | 250 | 23% | Spartan Heavy Weapons 100%, Spartan 100%, Spartan Sniper 100%, Pure Form Ranged 100%, Archer Missile Pod 100% | Spartan Grenadier 0%, Brute Grenadier 0%, Flamethrower Trooper 0%, Covenant Phantom 0%, Spartan CQB 0% |
| **Swarm Form** | AntiAir | 250 | 54% | Infected Phantom 100%, Spawner 100%, Zealot 100%, Condor Gunship 100%, Carrier Form 100% | Ghost 0%, Wolverine 0%, Warthog 0%, Infested Ghost 0%, Chopper 0% |
| **Infected Pelican** | Aerial | 400 | 40% | Infested Elite 100%, Heavy Grunts 100%, Mantis 100%, Combat Form 100%, Spartan Fireteam 100% | Fuel Rod Grunt 0%, Pure Form Ranged 0%, Brute Grenadier 0%, Flamethrower Trooper 0%, Covenant Phantom 0% |
| **Infected Phantom** | Aerial | 400 | 76% | Grunt Goblin 100%, Grizzly 100%, Zealot 100%, Condor Gunship 100%, Carrier Form 100% | Marine 0%, Skitterer 0%, Grunt 0%, Swarm Form 0%, Hornet 0% |
| **Pure Form Tank** | Tank | 400 | 58% | Infested Elite 100%, Infested Marine 100%, Carrier Form 100%, Cannon Fodder 100%, Mantis 100% | Gauss Warthog 0%, Covenant Phantom 0%, Cobra 0%, Vampire 0%, Gremlin 0% |
| **Seeder** | SingleTargetDps | 475 | 47% | Pelican 100%, Vulture 100%, Zealot 100%, Carrier Form 100%, Longsword 100% | Wolverine 0%, Infested Ghost 0%, Chopper 0%, Infested Warthog 0%, Reaver 0% |
| **Flood Abomination** | Titan | 800 | 96% | Pelican 100%, Grunt Goblin 100%, Infected Phantom 100%, Spawner 100%, Infested Elite 100% | Hunter Captain 0%, Elephant 0%, Scarab 0%, Ghost 100%, Gauss Warthog 100% |

## Flood: UNSC-based

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Infested Marine** | Chaff | 100 | 68% | Pelican 100%, Infected Phantom 100%, Infested Elite 100%, Condor Gunship 100%, Heavy Grunts 100% | Flamethrower Trooper 0%, Rhino 0%, Infection Forms 0%, Wraith 0%, Marauder 0% |
| **Infested Warthog** | Flanker | 200 | 28% | Sparrowhawk 100%, Swarm Form 100%, Gremlin 100%, Seeder 100%, Spartan Heavy Weapons 100% | Stalker Form 0%, Fuel Rod Grunt 0%, Chopper 0%, Spartan Grenadier 0%, Archer Missile Pod 0% |
| **Infested Hornet** | Aerial | 275 | 53% | Spawner 100%, Zealot 100%, Carrier Form 100%, Longsword 100%, Pure Form Tank 100% | Ghost 0%, Wolverine 0%, Warthog 0%, Chopper 0%, Reaver 0% |
| **Infested Scorpion** | Tank | 275 | 49% | Infested Marine 100%, Heavy Grunts 100%, Cannon Fodder 100%, Juggernaut 100%, Grunt 100% | Covenant Phantom 0%, Cobra 0%, Vampire 0%, Spartan 0%, Seeder 0% |
| **Juggernaut** | Flanker | 400 | 55% | Infested Marine 100%, Cannon Fodder 100%, Hunter Pair 100%, Spartan Fireteam 100%, Elite 100% | Covenant Phantom 0%, Cobra 0%, Vampire 0%, Marauder 0%, Infested Scorpion 0% |

## Flood: Covenant-based

| Unit | Role | Cost | Wins | Good against | Bad against |
|---|---|---|---|---|---|
| **Infested Ghost** | Flanker | 200 | 26% | Sparrowhawk 100%, Swarm Form 100%, Gremlin 100%, Seeder 100%, Spartan 100% | Stalker Form 0%, Fuel Rod Grunt 0%, Chopper 0%, Spartan Grenadier 0%, Archer Missile Pod 0% |
| **Infested Banshee** | Aerial | 250 | 59% | Infected Phantom 100%, Spawner 100%, Zealot 100%, Carrier Form 100%, Longsword 100% | Wolverine 0%, Warthog 0%, Reaver 0%, Jackal (Shield) 0%, Spartan Heavy Weapons 0% |
| **Infested Elite** | SingleTargetDps | 250 | 72% | Elephant 100%, Grunt Goblin 100%, Infected Phantom 100%, Grizzly 100%, Vulture 100% | Brute Grenadier 0%, Flamethrower Trooper 0%, Infected Pelican 0%, Infection Forms 0%, Marine 0% |
| **Infested Wraith** | Artillery | 250 | 50% | Infested Marine 100%, Heavy Grunts 100%, Cannon Fodder 100%, Spartan Fireteam 100%, Grunt 100% | Covenant Phantom 0%, Spartan CQB 0%, Vampire 0%, Seeder 0%, Hunter Captain 0% |
| **Spawner** | Tank | 400 | 75% | Scarab 100%, Elephant 100%, Grunt Goblin 100%, Infested Elite 100%, Grizzly 100% | Flamethrower Trooper 0%, Covenant Phantom 0%, Vampire 0%, Infested Hornet 0%, Swarm Form 0% |
