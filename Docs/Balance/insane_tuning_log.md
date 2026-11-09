# Tuning for the Insane AI: live log

The goal is every sub-faction at 46–54% in Insane vs Insane, stopping once all are at 48–52% over two seeds. All runs use 2000 base health, 100 matches per pairing and 15 threads. Trials change numbers only inside the tournament; the game is untouched until a set of changes is chosen.

Price bands: Tier 1 100–200, Tier 2 250–300, Tier 3 400–500, Titans 800.

## Win rates

| Sub-faction | Hard (D116) | Insane before | Trial 1 (seed 0) | Trial 2 (seed 0) | Trial 3 (seed 0) | Trial 4 (seed 0) | Trial 5 (seed 0) | Trial 6 (seed 0) | Trial 7 (seeds 0 + 1) | Trial 8 (seeds 0 + 1) | Real data (seeds 0 + 1) | Real data, round 2 | Real data, round 3 | Real data, round 4 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| UNSC-based Flood | 52% | 60% (57 / 62) | 53% | 55% | **57%** | **57%** | 51% | 51% | 52% (50 / 53) | 52% (49 / 54) | **56% (55 / 57)** | 53% (51 / 55) | 51% (49 / 54) | running |
| Aerospace Command | 49% | 58% (56 / 60) | 51% | 51% | 50% | 50% | 52% | 54% | **55% (54 / 57)** | 53% (52 / 53) | **44% (43 / 46)** | **56% (55 / 58)** | **56% (54 / 58)** | running |
| Banished | 50% | 56% (59 / 53) | 56% | 52% | 51% | 50% | 50% | 47% | 47% (49 / 45) | 50% (52 / 48) | 52% (52 / 51) | 53% (54 / 53) | 52% (52 / 51) | running |
| Lekgolo Colony | 49% | 52% (52 / 53) | 53% | 54% | 54% | 52% | 52% | 50% | 48% (48 / 48) | 48% (49 / 48) | 54% (55 / 53) | 52% (52 / 51) | 51% (51 / 50) | running |
| Unggoy Rebellion | 52% | 49% (47 / 51) | 46% | 50% | 48% | 47% | 51% | 47% | 50% (49 / 52) | 51% (50 / 52) | 52% (51 / 53) | 51% (51 / 52) | 51% (50 / 52) | running |
| Covenant-based Flood | 48% | 45% (44 / 45) | 44% | **41%** | **45%** | 49% | 49% | 48% | 47% (46 / 48) | 47% (47 / 48) | 47% (47 / 48) | **44% (45 / 43)** | **42% (43 / 41)** | running |
| Armored Division | 49% | 43% (45 / 42) | 51% | 49% | 47% | 49% | 49% | 52% | 51% (51 / 51) | 50% (50 / 50) | 50% (49 / 50) | 47% (48 / 46) | 49% (50 / 49) | running |
| Noble | 51% | 37% (40 / 34) | 45% | 48% | 47% | **45%** | **45%** | 52% | 49% (52 / 46) | 49% (51 / 46) | **45% (48 / 42)** | **43% (45 / 41)** | 48% (51 / 44) | running |

"Insane before" shows both seeds together, then seed 0 / seed 1. A trial run on one seed should be compared with the seed 0 number.

## What drives the gaps (before tuning)

- **The Insane AI buys about 6 techs per side** (Hard: under 1). Units with good techs that it levels as its main units gain the most.
- **Too strong:**
  - Pure Form Ranged does about 3 times its price's share of damage for UNSC-based Flood (1.6 under Hard).
  - Brute Grenadier does 2.3 times its share for Banished.
  - Sparrowhawk is fielded twice as often as under Hard, and its side wins 59% when it is.
- **Money wasted:**
  - Noble and Armored Division field the Pelican in 62–65% of their matches, where it does a quarter of its price's worth.
  - The Elephant drops from 1.3 times its share under Hard to 0.65.
  - The Spawner does 0.24 times its share for Covenant-based Flood.

## Trials

### Trial 1 (seed 0): done
| Change | Why |
|---|---|
| Pure Form Ranged 250 → 300 | Too strong (both Flood sub-factions) |
| Sparrowhawk 250 → 300 | Aerospace too high |
| Brute Grenadier 150 → 175 | Banished too high |
| Pelican 300 → 250 | Noble and Armored waste money on it |
| Spartan Heavy Weapons 450 → 400 (unlock 200) | Noble far too low |
| Spawner releases every 3 s (was 4) | Covenant-based Flood low, Spawner weak |

**Result:** it moved things the right way. UNSC-based Flood went 57→53, Aerospace 56→51, Armored 45→51 and Noble 40→45. Still out of the band: Banished 56, Covenant-based Flood 44, Noble 45 and Unggoy 46.
- Pure Form Ranged at 300 still did 2 to 2.4 times its share.
- The Brute Grenadier at 175 still did 2 times its share.
- The Spawner went from 0.24 to 0.35 times its share.
- Fuel Rod Grunt does 2.5 times its share for Unggoy, but Unggoy is low, so it stays as it is for now.

### Trial 2 (seed 0): done
The owner asked for changes that fit each unit, not prices alone. Price changes from trial 1 are swapped for stat changes where those suit the unit better.
| Change | Why |
|---|---|
| Pure Form Ranged: price back to 250, Bone Shards damage 60 → 48 | Artillery that hits too hard, not one that costs too little |
| Sparrowhawk: price back to 250, autocannon damage 18 → 15 | Same: it hunts vehicles too well |
| Brute Grenadier: price back to 150, grenade damage 34 → 27 | It stays tough; it just hits less |
| Pelican 250 (kept), health 1200 → 1500 | A transport: it should live to land its Marines |
| Spartan Heavy Weapons: price back to 450, health 1700 → 2100 | Meant to be a walking wall |
| Spawner releases every 3 s (kept) | Still the weakest Covenant-based Flood unit |
| Elephant main cannon 220 → 260 | Its price is fixed at 800 (Titan); it does 0.65 times its share for Noble and Armored |
| Infested Ghost health 270 → 330 | Covenant-based Flood is low; this cheap flanker does 0.6 times its share |
| Heavy Grunts needle damage 6.5 → 7.5 | Unggoy is low; Unggoy's side wins 13 points less with them than without them |
| Grunt Goblin health 1500 → 1750 | Unggoy's tank: fielded in 90% of its matches, 0.78 times its share |

**Result:** Banished 52, Unggoy 50, Noble 48, Armored 49 and Aerospace 51 are in or near the target. Still out of the band:
- **Covenant-based Flood fell to 41%.** The Pure Form Ranged cut hurt it more than UNSC-based Flood. It also fields many of the Flood's shared units that do little: the Carrier Form, the Infected Phantom, the Flood Abomination and the Spawner.
- **UNSC-based Flood is at 55%.** It plays Pure Form Ranged far better (2.4 times its share) and has the strong Infested Hornet.
- **Lekgolo is at 54%,** right at the edge and within one run's margin. Its strength is the Fuel Rod Grunt, which Unggoy needs too, so it is left alone for now.

### Trial 3 (seed 0): done
Everything from trial 2, plus:
| Change | Why |
|---|---|
| Flood Abomination health 8,500 → 10,000 | A slow melee Titan that is fielded in 90% of matches but dies before it reaches anything (0.7 times its share; its price is fixed at 800) |
| Infected Phantom health 1,100 → 1,400 | It brings its value by dropping Combat Forms, so it should live long enough to drop more |
| Spawner health 3,200 → 4,000 | Still the weakest Covenant-based Flood unit; it is a tank that releases forms, so it should live longer |
| Infested Hornet rockets 18 → 16, spore bombs 36 → 32 | UNSC-based Flood only, 1.4 times its share; it brings UNSC-based Flood down without touching Covenant-based Flood |

**Result:** Covenant-based Flood rose to 45%, but UNSC-based Flood rose to 57%, because the shared Flood buffs help it as well: it fields the Infected Phantom in 94% of its matches. Everyone else is at 47–54%. The next step goes at units only one of the two Flood sub-factions has.

### Trial 4 (seed 0): done
Everything from trial 3, plus:
| Change | Why |
|---|---|
| Infested Scorpion cannon 80 → 72 | UNSC-based Flood only; 1.1 times its share |
| Infested Marine scythe 20 → 18 | UNSC-based Flood only, and its core unit (99% of matches, 14% of its credits) |
| Carrier Form health 140 → 170 | Covenant-based Flood fields it in 93% of matches (UNSC-based in 66%); more of them should reach their target and burst |
| Spawner releases every 2.5 s (was 3) | Covenant-based Flood only, still 0.4 times its share |

**Result:** Covenant-based Flood is up to 49%, and Lekgolo is back to 52%. UNSC-based Flood is still at 57%, where Pure Form Ranged does 2.2 times its share. Noble slipped to 45% and Unggoy to 47%.

### Trial 5 (seed 0): done
Everything from trial 4, plus:
| Change | Why |
|---|---|
| Pure Form Ranged shards 48 → 44 | UNSC-based Flood still leans on it; Covenant-based Flood now has room to take the cut |
| Infected Phantom health back to 1,250 (was 1,400 in trials 3–4; 1,100 before) | UNSC-based Flood fields it in 94% of matches, Covenant-based in 77% |
| Spartan Heavy Weapons chaingun 11 → 13 | Noble; it is there to mow down infantry, but Noble's side wins 16 points less with it than without it |
| Spartan Fireteam health 420 → 500 each | Noble; bought in only 13% of matches |
| Grunt Goblin plasma cannons 9 → 10 | Unggoy's tank, still 0.8 times its share |

**Result:** seven of the eight are at 49–52%. UNSC-based Flood fell from 57% to 51%. Only Noble is still out, at 45%. Its money goes to units that do little:
- the Elephant: in 95% of its matches, 0.75 times its share, and Noble wins 16 points less with it than without it;
- the Warthog: 70% of matches, 0.28 times its share;
- the Pelican: 62% of matches, 0.42 times its share.

### Trial 6 (seed 0): done
Everything from trial 5, plus:
| Change | Why |
|---|---|
| Elephant main cannon 260 → 300 | Fielded in 95% of Noble's matches and still well under its share; its price is fixed at 800 (Titan) |
| Warthog health 300 → 360 per vehicle | A cheap flanker that dies before doing anything (0.3 to 0.4 times its share for every UNSC sub-faction) |
Both are shared by every UNSC sub-faction. If Aerospace goes too high, its own Sparrowhawk takes it back down.

**Result:** every sub-faction is inside the 46–54% band for the first time, and Noble went from 45% to 52%. Three are still outside the 48–52% target:
- Aerospace is at 54%.
- Banished and Unggoy are at 47%. The UNSC buffs came partly at the Covenant's cost.

### Trial 7 (seeds 0 and 1): done
Everything from trial 6 with three small tweaks. It runs on both seeds: if every sub-faction is at 48–52% on both, this is the set that goes into the game.
| Change | Why |
|---|---|
| Sparrowhawk autocannon 15 → 14 | Aerospace at 54% |
| Brute Grenadier grenades 27 → 29 (34 before tuning) | Banished at 47%: it was cut further than it needed |
| Heavy Grunts needles 7.5 → 8 (6.5 before tuning) | Unggoy at 47% |

**Result:** with both seeds together, Aerospace is at 55% (54 and 57), outside the band. Banished and Covenant-based Flood are at 47% (in the band, but under the 48% target), and the rest are at 48–52%. Aerospace's gain comes mostly from the Elephant and Warthog buffs (the Elephant went from 0.75 to 0.98 times its share there). Its own aircraft lift it as well. Aerospace's own units are trimmed rather than the Elephant, which Noble needs.

### Trial 8 (seeds 0 and 1): done
Everything from trial 7, plus:
| Change | Why |
|---|---|
| Sparrowhawk autocannon 14 → 13 (18 before tuning) | Aerospace at 55% |
| Longsword carpet bombs 150 → 135 | Aerospace only; in 73% of its matches |
| Brute Grenadier grenades 29 → 31 (34 before tuning) | Banished still at 47% |

**Result:** every sub-faction is inside 46–54% on both seeds together. Aerospace is at 53% and Covenant-based Flood at 47%, one point outside the 48–52% target and within one run's margin. The other six are at 48–52%.

## In the game's data
The trial 8 set has been written into the unit data (`Tools/Data`). `gen_roster.py` was run, then the counter web for the 19 changed units, then the counter list, the AI's matchup table and the sub-faction doc. Trials don't regenerate the counter web that the AI uses to choose what to buy, so the real check is a run on the game's own exported data, with no trial file.

### Confirmation run (seeds 0 and 1): done
**Result:** it came out differently from the trial. Aerospace fell to 44%, UNSC-based Flood rose to 56%, Noble is at 45% and Lekgolo at 54%. The cause is the Pelican: at 250 credits with 1,500 health it wins its counter-web duels, so the AI now buys it far more.
- Aerospace fields it in 75% of matches (10% in the trial), and Noble in 96% (62%).
- It still does only about a third of its price's worth of damage in real matches.
- That money used to go to the Sparrowhawk and Longsword (the Sparrowhawk went from 69% of matches to 25%).
- UNSC-based Flood now fields its Infested Hornet more (70% of matches), where it does 1.45 times its share.

### Real data, round 2 (seeds 0 and 1): done
| Change | Why |
|---|---|
| Pelican back to 300 credits and 1,200 health (as before tuning) | Cheaper and tougher, it looks good in the counter web but not in real matches, and it took over Aerospace's and Noble's money |
| Infested Hornet rockets 16 → 15, spore bombs 32 → 29 | UNSC-based Flood at 56%; the Hornet does 1.45 times its share |

**Result:** it swung the other way. Aerospace is back up to 56%, and Noble is down to 43%, Covenant-based Flood to 44% and Armored to 47%. The counter web moves the AI's buying in ways the trials can't show:
- Covenant-based Flood now fields Pure Form Ranged in only 20% of matches (48% before), since its weaker damage makes the counter web rate it lower.
- Noble and Armored still field the Pelican in about 60% of matches and get a quarter of its price's worth from it. Aerospace avoids it (9%).
- The AI buys the Pelican because it wins its counter-web duels: its autocannon hits ground units that can't shoot back at aircraft. In real matches, its value is the Marines it drops.

### Real data, round 3 (seeds 0 and 1): done
| Change | Why |
|---|---|
| Pelican autocannon 14 → 10 | A transport, not a gunship. The counter web should rate it lower, so that Noble and Armored spend their money elsewhere, as Aerospace does |
| Vulture health 900 → 800 | Aerospace at 56%; fielded in 94% of its matches |
| Spartan Heavy Weapons health 2,100 → 2,400 (1,700 before tuning) | Noble at 43%; now fielded in 77% of its matches |
| Spawner Spore Lash 40 → 55 | Covenant-based Flood at 44%; the Spawner is in 86% of its matches and does 0.6 times its share |
| Grizzly cannons 100 → 110 | Armored at 47% |

**Result:** the Pelican fix worked. Noble now buys it in 27% of matches (58% before) and Armored in 36% (62%); Noble rose to 48% and Armored to 49%. Two are still out:
- **Covenant-based Flood is at 42%.** It loses badly to UNSC-based Flood (30%) and Unggoy (26%). It rarely buys Pure Form Ranged now (19% of matches), and leans on the Spawner (90% of matches, 0.65 times its share) and on weak shared Flood units.
- **Aerospace is at 56%.** It beats Noble 64% and most others about 58%. Its Vulture is in 92% of its matches and survives a lot: 59% of them die by a battle's end, against 90% or more for most units.

### Real data, round 4 (seeds 0 and 1), running
| Change | Why |
|---|---|
| Pure Form Ranged shards 44 → 48 (60 before tuning) | UNSC-based Flood is at 51% and has room; Covenant-based Flood should buy it again |
| Spawner releases 3 Combat Forms a lot (was 2) | Covenant-based Flood only, still 0.65 times its share |
| Infested Elite health 420 → 500, claw 42 → 46 | Covenant-based Flood only; bought in 19% of its matches, 0.47 times its share |
| Vulture missiles 50 → 44 | Aerospace only |
