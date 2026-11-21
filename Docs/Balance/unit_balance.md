# Unit balance (D137)

Balance moves from sub-factions to units. Every sub-faction wins 47% to 53% Insane against Insane, but the units inside them are far apart. In the D136 tournament the damage a unit deals for its price runs from 0.2 to 2.5 times its share. The Spartan's side wins 10 points more often when it buys one, while the Condor Gunship's, at the same price, wins 9 points less. A sub-faction lands near 50% when a unit that is too strong and one that is too weak cancel out. Each fix to a shared unit moves two or three sub-factions, and each check was a 15-minute tournament that takes the owner's whole CPU and still has 2 to 3 points of noise.

Two tools now come first:

1. **The unit intent sheet** (`unit_intent.md`): one line per unit with its job, what it should beat and lose to, how strong it should be for its price, and what it measures now. The owner decides it. It keeps balance from fighting the owner's intent: a 500-credit Hunter Captain should not beat a Titan, and the Pure Form Ranged is a low-health, long-range carry that is meant to deal a lot.
2. **The army slot test** (below): what a unit is worth inside the armies the AI really builds. It takes minutes, on 8 threads.

The order of work:
- Shared units first, since they move several sub-factions.
- Fix each by unit (health, damage, fire rate, range or price, whichever fits) and check each change with the slot test.
- One tournament per batch to check the sub-factions, with small corrections on exclusive units, then a new slot test from that tournament's armies.
- At the end, refresh the AI's measured unit values.

Done means every unit within its band and every sub-faction at 48% to 52%.

## The army slot test

It lives in the tournament tool (`Tools/Balance/Tournament/SlotTest.cs`).

- **Real armies.** It reads a tournament's results file and takes each side's squads at the end of every match, as the Insane AI built them, at the levels they reached, along with the enemy army from the same match. Each army is brought to the same budget (4,000 credits by default), keeping its mix. A level costs what it costs in a match: half the unit's price each.
- **One slot.** For every unit a sub-faction can field, it adds a slot of that unit, about a fifth of the budget (800 credits: five or six Marines, one Spartan, one Elephant), at the average level that unit reached in that sub-faction's armies. The enemy gets exactly as many credits more of its own mix. The baseline gives the army a fifth more of its own mix instead. Every version of a pair fights on the same seed, so they differ only by the slot.
- **Value.** How many points of win rate the slot adds over the baseline. 0 means the unit's credits are worth as much as the army's average credit; below 0, less; above 0, more.
- **Margin.** A win or a loss is coarse, so it also measures the margin: the survivors' base damage (what they would deal to the enemy's base in a match) minus the enemy's, over the army's starting base damage. It moves with every body that lives or dies, so it settles with far fewer battles. The reports sort by it.
- **Role on its own.** A carry shows its worth because the army protects it, and a meat shield shows its worth because the carries behind it live longer. That is why the test uses whole armies rather than one unit against another.
- **Shared units** are measured in each army that fields them. A Marine is not worth the same in Aerospace Command as in Noble.
- **Placement.** Squads stand on the real deployment grid where Hard puts them: chaff and tanks in front, single-target units and chaff clear behind, aircraft and artillery further back, ground flankers on the edges. Units that can flank (Aerospace Command's four aircraft and the Pelican) go to the flank zones, left and right in turn, arriving on time as they do after their first battle.

**What it leaves out.** Battles only: no towers, techs, cards, items or spells.
- The Pelican's Troop Bay needs a tech, so here it carries no one.
- The armies are end-of-match armies (average level 1.9), so the test speaks for the middle and late game more than for the first rounds.
- Anti-air is measured against every enemy, with or without air, so its value is an average over armies it often has nothing to shoot at.
- The baseline's win rate leans about 2 points toward the enemy side. The slot values don't, since every version of a pair has the same lean.

**Running it.**

```
cd Tools/Balance/Tournament
dotnet build -c Release
dotnet bin/Release/net10.0/Tournament.dll <game_data.json> - <out.txt> --slot <results.jsonl> --threads 8
    [--pairs 210] [--budget 4000] [--slot-share 0.2] [--levels real|one] [--only "Aerospace Command,UNSC-based"] [--trial trial.txt]
python slot_combine.py <run1.csv> <run2.csv> > combined.txt
```

- **Outputs:** the report goes to `out.txt`, and one line per unit to `out.csv`.
- **`--pairs`:** how many army pairs per sub-faction, spread evenly over its opponents. 210 pairs for all eight sub-factions take about 2.5 minutes on 8 threads; one sub-faction takes about 20 seconds.
- **`--levels one`:** fights every squad at level 1, which is the worth of the unit's price alone.
- **Trial file:** same format as the tournament's, for example `Vulture|MaxHealth=900`. It tries a change without touching the game: run the sub-factions that field the unit with and without it, and compare.
- **`slot_combine.py`:** averages runs made from different armies (one per tournament seed). It also says how well they agree.

## First results (D136 armies)

The reports are in this folder:
- `slot_test_d137.txt`: both seeds combined, with every unit across the armies that field it at the end.
- `slot_test_d137_s0.txt` and `slot_test_d137_s1.txt`: one per seed, with win rates.
- Their `.csv` files.

**It repeats.** Two runs, each on its own tournament seed's armies (210 pairs per sub-faction, 28,980 battles each), agree at r = 0.93. They differ by 3.3 points of margin per unit on average. Combined, a unit's value is good to about ±4 points.

**The units are far apart:** margins run from +13 to −25.
- 19 units are within ±5.
- 11 are above +5.
- 39 are below −5.

0 is the army's average credit, which the strong units pull up. As they come down, the weak units' numbers rise.

| Too strong (margin) | | Too weak (margin) | |
| --- | --- | --- | --- |
| Spartan | +13 (Aerospace +13, Armored +15, Noble +12) | Covenant Phantom | −25 |
| Flood Abomination | +11 (Covenant-based +12, UNSC-based +9) | Vulture | −23 |
| Fuel Rod Grunt | +11 | Infested Elite | −23 |
| Spawner | +11 | Stalker Form, Ghost | −22 |
| Hunter Pair | +10 | Pelican (no Troop Bay here) | −21 |
| Grunt | +10 | Chopper, Warthog | −20 |
| Cobra | +10 | Longsword | −19 |
| Marine | Aerospace +14, Armored +8, Noble +3 | Wraith | −18 |
| Spartan Heavy Weapons | +8 | Sparrowhawk, Gremlin | −17 |
| Heavy Grunts | +7 | Hornet, Vampire, Archer Missile Pod, Combat Form, Infested Ghost | −16 |

**The owner's calls, and what the test says.**
- *Marine worse:* yes in Aerospace Command (+14) and Armored Division (+8). About fair in Noble (+3).
- *Vulture better:* yes. It is the weakest aircraft (−23), and the AI buys it in three Aerospace armies out of four.
- *Combat Form not so bad:* yes, −16 and −15 in the two Flood armies. The Infected Phantom, which fights through the Combat Forms it drops, is −19 and −8.
- *Spartan against Condor Gunship:* worse than the tournament showed. The Spartan is +13 in all three UNSC armies; the Condor is −12. They are 25 points apart.
- *Pure Form Ranged is a carry:* in UNSC-based Flood it is the second-best unit (+7). It ends matches at level 2.8, the highest in the game. In Covenant-based Flood it ends at 2.2 and measures −12: there it does not live long enough to carry.

**Surprises.**
- **The Flamethrower Trooper** deals more damage for its price than almost any unit, yet measures −11 to −14 in all three UNSC armies. Its damage goes into cheap bodies and doesn't win the battle.
- **Titans** are about fair once levels count (Elephant −1, Scarab −3). The exception is the Flood Abomination (+11). At level 1 they all looked strong (+10 to +31), because the units around them had lost their levels.
- **Ground flankers** are mostly weak: the Warthog, Ghost, Chopper, Gremlin, Stalker Form, Infested Warthog and Infested Ghost measure −15 to −22 (the Skitterer −4, the Juggernaut −6). They don't use the flank zones (only Aerospace Command's aircraft and the Pelican can), so this is how they really fight.
- **Aerospace Command's aircraft** were weak without the flank zones (−6 to −18). They are weaker still with them (−12 to −23).
- **Sub-factions:**
  - Aerospace Command's armies win 31% to 40% of these battles at equal credits, and UNSC-based Flood's 31% to 36%. Yet they win 53% and 47% of their matches.
  - Covenant-based Flood's armies win 33% to 39% here, and 51% of matches.
  - Something outside the battles makes up the difference for all three: towers, techs, cards, spells, or the early rounds. The slot test can't see it. A unit's value inside its own army still holds, which is what the test is for.

## Next

1. The owner answers the intent sheet's questions (`unit_intent.md`, at its end; also `MORNING_DECISIONS.md`, items 178 to 188), above all the band (question 11).
2. A first batch, proposed with the owner:
   - Bring down the strongest shared units: the Spartan, the Marine in Aerospace Command and Armored Division, the Fuel Rod Grunt, the Hunter Pair, the Grunt and the Flood Abomination.
   - Bring up the weakest units meant to be fair: the Vulture, the Combat Form (which lifts the Infected Phantom too), the Condor Gunship and the Infested Elite.
   - Each change checked with the slot test.
3. One tournament for the batch, then a new slot test from its armies.

## The balance pass (D138)

The owner answered the intent sheet's questions (`unit_intent.md`, at its end) and asked for a big pass with these tools. The rule became: units of the same type and price within about ±5 points of each other, exclusive units a little above base units.

**What changed in the test.** Battles now have each side's towers and techs. Towers changed the picture most: without them flankers and Aerospace Command's flank-zone aircraft measured far below their price, with them far above, because a fallen tower weakens its whole army. `slot_groups.py` lists the slot values by type and price. Trial files now reach a unit's carried copies (the Spawner's brood, a dropship's squad), as the game does.

**What the pass learned.**
- The slot test points the right way within a type and price, but its sizes need the tournament: it undervalues Flood and aircraft armies, whose armies win 31–40% of its battles and about half their matches. The first trial, sized by the slot test alone, put the Flood at 69%.
- The AI buys by the counter table (one unit against another at equal credits). A buff that wins those fights made the AI buy that unit more even where it lost: the Archer and the Mantis in Aerospace Command, the Elite as Banished's and Lekgolo's answer to Aerospace's aircraft. So every round re-measured the table and exported the game data again.
- Flankers that go for towers lift every flanker's edge and swing the sub-factions a lot until the AI defends: Covenant-based Flood went from 49% to 72% before the AI guarded raided towers.

**Result** (Insane against Insane, seeds 0 to 3, 2,800 matches a sub-faction):

| Sub-faction | Seeds 0 + 1 | Seeds 2 + 3 | All four |
|---|---|---|---|
| Aerospace Command | 51% | 53% | **52%** |
| Armored Division | 49% | 50% | **49%** |
| Noble | 49% | 50% | **49%** |
| Banished | 50% | 48% | **49%** |
| Lekgolo Colony | 49% | 48% | **48%** |
| Unggoy Rebellion | 51% | 52% | **52%** |
| Covenant-based Flood | 50% | 50% | **50%** |
| UNSC-based Flood | 51% | 50% | **50%** |

Every number changed is in `DECISIONS.md` D138. The Combat Form and the Infested Elite still lose more than they win when bought: raising them lifts both Flood sub-factions through the Spawner's brood and the Infected Phantom's drops, so that is the owner's call.

**By type and price, the owner's ±5 rule is not met yet.** The slot test on the final armies (`slot_test_d138_groups.txt`, against `slot_test_d138_groups_before.txt` from the same test before the pass):
- **Closer:** the Flamethrower Trooper moved to the middle of the 150-credit anti-chaff units, from 10 points below it. The Titans are within 7.5 points of each other, from 8.3. The Spartan Fireteam joined its group.
- **Still wide:** most groups spread 13 to 28 points.
  - The Carrier Form sits 27 points below the Brute Grenadier.
  - The Combat Form and the Infested Elite sit 17 below their group.
  - The Banshee and the Vampire sit about 20 below theirs.
  - The flankers spread 30 (the Infested Ghost and Chopper on top, the Stalker Form and Juggernaut at the bottom).
- **Why:** the pass ended on the tournament's word. Each time a buff sized by the slot test overshot in real matches (the Flood units, the Elite, the anti-air), it came back, to keep the sub-factions even.
- **Next:** narrow each group with swaps inside one sub-faction, one unit up and its neighbour down, so that the sub-faction's total stays put. Each swap is checked with one tournament.

## The second pass: win rate by type and price, then exclusives (D140)

The owner, after D138: "In this pass, let's prioritise edge (meaning winrate) for each type of units in their range of price. Don't care about the win rates of the subfactions until all of this is done. THEN, we tweek subfaction exclusive units only to bring them to as close to 50% win rate as possible."

**The measure: ban impact, not edge.** Edge (a side's win rate when it fields a unit, minus when it doesn't) hardly moves with a unit's stats: about 0.16 points per 1% of power, against 2.5 points of noise, and it is fooled by when the AI buys a unit. Ban impact is cause and effect: the sub-faction's win rate minus its win rate with that one unit taken out of its roster (the tournament's `without|Sub|Unit` trial, 100 matches a pairing, about 2 minutes). Two things make it sharper:
- A ban run on seed 0 replays that seed's matches exactly but for the missing unit (with no trial, all 700 winners come out the same), so it is compared with the seed-0 tournament match by match, not with the four-seed rate, whose seeds sit up to 6 points apart.
- Its margin comes from the matches whose winner changed: about ±2.5 points for a unit three sub-factions field, ±4 to ±7 for one only one fields.
`ban_groups.py` lists every unit by type and price with its impact, its share of credits and the gap to its group's middle; `exclusive_pass.py` sizes the exclusive-unit changes; `/e/AI/_hbci/bp2/` holds the round, sweep and diagnostic scripts.

**How much a change moves a unit.** For most units the army's win rate moved about 4 points per 1% of army power (the unit's share of credits times its own power change): the Marine, Mantis, Seeder and Spartan Fireteam moved as predicted; the Grunt Goblin (28% of Unggoy's credits) and the Infested Marine overshot. Four units barely answered to power at all, and each had a reason outside its stats:
- **The Titans: the unlock cost.** The Elephant and the Scarab cost their armies 7 and 12 points (Banished, Lekgolo and Unggoy each won 10 to 15 points more without a Scarab), and +15% and +23% power changed that by less than 1 point. With 350 to unlock on top of 800, the AI could afford one only late in a match. With no unlock cost, Armored Division won 64% instead of 45% and its Elephant spending doubled; Lekgolo won 61% instead of 53%. Scarab health +50% gave +6, speed 0.75 → 1.25 +4, price 600 +14. Unlock 150 for the Elephant and 50 for the Scarab put all three Titans at about +1 (the Abomination, unlock 350, already was).
- **The Carrier Form and the Pelican: the XP they feed.** Neither answered to health, blast, speed, a cheaper drop tech or a fourth drop. A kill's XP is the victim's price per body, and that holds for bodies made in the battle too: the Infection Forms a Carrier Form bursts into, a Pelican's dropped Marines, a Spawner's brood, risen forms. With leveling off, the Carrier Form's impact went from −6.9 to −1.8 (and Covenant-based Flood won 71% instead of 57%). `MatchRules.BattleBodyXpShare` (1, the game as it is) lets the tournament measure it: at 0, Covenant-based Flood +2.7, UNSC-based +2.1, Armored +1.3, Aerospace −2.9, Unggoy −2.1, the rest within 1. It stays at 1: the owner decides.

**Phase 1** (rounds b1 and b2, Insane against Insane, seeds 0 to 3, then ban runs for every unit changed):

| Group | Spread at the start (e1) | After b2 |
|---|---|---|
| Chaff, 100 | 14 | 11 |
| Anti-chaff, 150 | 9 | 13 |
| Single-target, 175–250 | 3 | 3 |
| Single-target, 300 | 3 | 3 |
| Single-target, 400–500 | 7 | 4 |
| Tank, 275–300 | 1 | 1 |
| Tank, 400–500 | 13 | 9 |
| Artillery, 250–400 | 3 | 3 |
| Anti-air, 250–300 | 6 | 6 |
| Aircraft, 250–300 | 8 | 8 |
| Aircraft, 400–500 | 2 | 2 |
| Titans, 800 | 13 | 1 |
| Flankers, 125–250 | 10 | 9 |
| Flankers, 300–400 | 0 | 0 |
| Transports, 300 | 6 | 7 |

Groups within 10 points: 12 of 15 at the start, 13 of 15 after b2 (average spread 6.6 → 5.4). The two left: the Marine and the Flamethrower Trooper sit about 6 points above their groups after two health cuts each (60 → 50 and 120 → 105), the Carrier Form 7 below (the XP above). Most of what remains is within the measure's noise. *These b2 numbers mixed ban runs from different rounds, some on stats changed since; the full sweep on round x4, below, found more left to do.*

**Phase 2** (rounds x1 to x4, exclusive units only, each sized from the last: a sub-faction at r% needs its army's power moved by (50 − r) / S percent, spread over its exclusives by their share of its credits; S turned out nearer 2 than 4 for whole sub-factions):

| Sub-faction | After b2 | x1 | x2 | x3 | x4 |
|---|---|---|---|---|---|
| Lekgolo Colony | 60 | 56 | 54 | 54 | **52.1** |
| Covenant-based Flood | 56 | 53 | 51 | 51 | **51.3** |
| Banished | 53 | 52 | 51 | 50 | **50.0** |
| Aerospace Command | 51 | 53 | 51 | 50 | **50.0** |
| Noble | 53 | 51 | 50 | 50 | **49.9** |
| Armored Division | 48 | 49 | 49 | 49 | **49.2** |
| UNSC-based Flood | 37 | 42 | 49 | 49 | **49.0** |
| Unggoy Rebellion | 42 | 44 | 45 | 48 | **48.5** |

Lekgolo was the hard one: once the Scarab was a third of its credits its exclusives were only 8%, and cutting the Goliath and the Hunter Captain further only made the AI stop buying them (bought in about a quarter of its matches). The Skitterer still added 6.6 points (going for towers, not its health, is its worth), so its price went 125 → 150 instead.

**The full sweep on x4, then rounds y1 to y5.** With phase 2 done, every unit bought in at least 10% of its sub-factions' matches was ban-tested again on the x4 roster (117 runs, about 4 hours). On one roster the groups were wider than the mixed table had said: 9 of 13 within 10 points, average spread 8.5. The Spawner added +14.9 (its group's middle +2.2), the Infested Ghost +13.4 (middle +6.1), the Flamethrower Trooper +7.9 and the Infested Marine +8.1, and the Abomination −4.9 against the other Titans' +3.7 and +2.7. So phase 1 went round again (y1), then phase 2 (y2 to y5):

- **y1 (phase 1):** Spawner 3380 → 2900 health; Infested Ghost 165 → 150, ram 45 → 40, bolts 8 → 7 (its health cuts alone hadn't lowered it); Flamethrower Trooper 105 → 95; Infested Marine 150 → 140; Abomination unlock 350 → 250, the lever that fixed the other Titans. Ban runs after: Spawner +8.1, Infested Ghost +9.7, Flamethrower +6.9, Infested Marine +6.1, Abomination −0.5. 12 of 13 groups within 10.
- **y2:** Spawner 2900 → 2700, Flamethrower damage 3.5 → 3.2, and exclusives toward 50% (Covenant-based +8%, Aerospace +4%, Noble +6%, Armored +10%, Lekgolo −13% to −17%). **Aerospace fell to 42.7%.** Two changes had moved the AI's counter table, not just the units. After the Flamethrower's damage cut, neither Aerospace nor Armored bought it against the other, and that matchup went 63 → 42% for Aerospace. The Vulture's +4% made the AI buy it against Unggoy (4% → 21% of Aerospace's credits there), and that matchup went 44 → 32%.
- **y3:** Aerospace's exclusives back to x4, Covenant-based's +8% halved. Aerospace vs Unggoy came back, but Aerospace stayed at 43%.
- **y4:** the Flamethrower's damage back to 3.5 (its health stays 95), Armored's +10% halved. Aerospace vs Armored back to 63%.
- **y5:** Sparrowhawk and Longsword +5%, Gremlin and Grizzly +5%, Infested Wraith +8%; the Spartan Heavy Weapons back to 1930 health, as y2's +6% put it 6 points above its group.

| Sub-faction | x4 (seeds 0–7) | y1 (0–3) | y2 (0–7) | y3 (0–3) | y4 (0–7) | y5 seeds 0–3 | y5 seeds 4–7 | **y5, all eight** |
|---|---|---|---|---|---|---|---|---|
| Lekgolo Colony | 52.6 | 54.6 | 53.3 | 54.1 | 53.8 | 54.0 | 53.1 | **53.5** |
| UNSC-based Flood | 48.6 | 49.8 | 51.6 | 52.3 | 50.5 | 49.6 | 51.4 | **50.5** |
| Banished | 49.7 | 51.2 | 50.2 | 50.7 | 50.5 | 50.4 | 50.4 | **50.4** |
| Unggoy Rebellion | 48.7 | 49.7 | 51.2 | 50.2 | 50.3 | 50.3 | 50.2 | **50.3** |
| Noble | 50.3 | 49.2 | 48.0 | 47.5 | 50.8 | 49.1 | 50.6 | **49.8** |
| Aerospace Command | 50.3 | 49.0 | 42.7 | 43.3 | 47.8 | 49.7 | 48.1 | **48.9** |
| Covenant-based Flood | 51.7 | 48.5 | 52.9 | 49.6 | 48.5 | 48.4 | 48.7 | **48.5** |
| Armored Division | 48.0 | 48.0 | 50.0 | 52.3 | 47.8 | 48.5 | 47.6 | **48.1** |

| Group | Start (e1) | x4, full sweep | **y5** |
|---|---|---|---|
| Chaff, 100 | 14 | 11 | **8** |
| Anti-chaff, 150 | 9 | 17 | **15** |
| Single-target, 175–250 | 3 | 2 | **2** |
| Single-target, 400–500 | 7 | 8 | **5** |
| Tank, 275–300 | 1 | 5 | **5** |
| Tank, 400–500 | 13 | 16 | **9** |
| Artillery, 250–400 | 3 | 8 | **9** |
| Anti-air, 250–300 | 6 | 8 | **8** |
| Aircraft, 250–300 | 8 | 2 | **6** |
| Aircraft, 400–500 | 2 | 6 | **6** |
| Titans, 800 | 13 | 9 | **3** |
| Flankers, 125–250 | 10 | 11 | **10** |
| Transports, 300 | 6 | 6 | **6** |

At y5, 11 of 13 groups are within 10 points and the average spread is 7.2, against 8.5 on x4. Every unit is measured on its final stats: x4 for the units unchanged since, then y1, y4 or y5. Units more than 5 points from their group's middle:
- **Carrier Form:** −9.9. This is the XP its Infection Forms feed the enemy (item 195), not its stats.
- **Within their margins** (±4 to ±8): the Flamethrower Trooper +5.5, Rhino +5.4, Infested Ghost +5.3, Spawner +5.3 and Gremlin −5.2.

`Docs/Balance/d140_unit_review.md` has every unit's numbers.

**Lekgolo stays at 53.5%.** Its exclusives are 7% of its credits. After y2's cuts (Goliath 2070 → 1760 health, Hunter Captain's laser 14.5 → 12.5 and bash 83 → 72, Skitterer 60 → 50), the AI buys the Goliath in 12% of its matches, and Lekgolo moved by under 1 point. Cutting further would push them below the other units of their type and price. Its edge is in its matchups: it wins 65% against Unggoy and 57–58% against Aerospace, Armored, Noble and Banished, and loses only to the two Flood sub-factions (36% and 48%), which phase 1 made a little weaker. What to do about it is the owner's call.

**Not measurable this way:** the Gauss Warthog, the Stalker Form and the Infected Pelican are bought in under 10% of matches, so a ban changes nothing. The slot test puts the Gauss Warthog above its group when it is fielded: the AI prefers its roster-mates.

## The balance remade: base factions first, then exclusives (D141)

The owner, after D140: "Ok remake the balance, it needs to be my vision. Here's how we can do that : Balance the 3 factions with only their non exlusive units. Tournaments with no exclusives. Then, we can run tournaments with exclusive units, it's okay to see a big disparity in winrate, as long as there is always a matchup that the subfaction is good at." Their vision: base units of the same type and price about equal in power, so the exclusive units can shine; a sub-faction may counter another, and whoever lands in a bad matchup has to think hard about their choices.

### Phase 1: the three factions with base units only

**The tournament.** One sub-faction stands for each faction: Armored Division for the UNSC, Banished for the Covenant, UNSC-based for the Flood. A trial file takes every one of the 33 exclusive units out (`without|Sub|Unit`), and the tournament's new `--among "A,B,C"` option plays only pairings between those three (no mirrors), 400 matches a pairing a seed, Insane against Insane. Their sub-faction spells, cards and techs stay in, so "base" includes those. Ban runs work as in D140, restricted the same way (`bban.sh`), and `bpair.py` splits a ban impact by opponent.

**Two lessons.**
- Base units only, a change moves a faction far more than D140's 4 points per 1% of army power: z1's changes moved the Covenant −21 and the Flood +20.
- A small stat change can flip a cell of the AI's counter table (`MatchupTable`), and then the AI buys differently. z7's Scarab +8% flipped "Spartan beats Scarab" from 1.0 to 0.0, and the Covenant went to 62% against the UNSC. Every round since lists the flips first (`cwonly.sh <round> <previous>`, `mdiff.py`), before anything is exported.

| Round (4 seeds unless said) | UNSC | Covenant | Flood | What changed |
|---|---|---|---|---|
| z0 (the D140 roster) | 48.5 | **67.8** | **33.6** | |
| z1 | 49.3 | 46.9 | 53.8 | Titans all unlock 150; Fuel Rod Grunt 28 → 23; Carrier Form 230 → 265, burst 144 → 165; Swarm Form 240 → 290 |
| z2 | 48.7 | 48.8 | 52.5 | Fuel Rod Grunt 24.5; Carrier Form 255, burst 158 |
| z3 | 45.9 | 52.2 | 51.9 | Scarab 9050, beam 32; Elephant 8410, cannon 316; Pelican 1550 |
| z4 | 50.7 | 44.6 | 54.7 | Fuel Rod Grunt range 16 → 14; Elite rifle 28 → 26 |
| z5 | 50.3 | 46.2 | 53.5 | Banshee 430, cannons 10, bomb 55 |
| z6 | 50.2 | 46.6 | 53.1 | Seeder spore 47 → 43 |
| z7 | 44.0 | 54.7 | 51.3 | Scarab 9500, beam 33, turrets 11 (the flip above) |
| z8 (8 seeds) | 50.9 | 48.1 | 51.0 | Scarab back but turrets 11; Banshee 440, 10.25, 57 |
| z9 | 50.2 | 47.7 | 52.1 | Elephant 8240, cannon 310; Hornet 505, 24.5, 45; Vampire 570, 13 |
| z10 | 48.2 | 49.8 | 52.1 | Fuel Rod Grunt range 15, Elite 25 (reverted) |
| z11 | 49.8 | 47.7 | 52.5 | Swarm Form 275, 13; Infected Phantom 1400, 12.5 |
| **z12 (8 seeds)** | **50.0** | **50.0** | **50.0** | Infection Forms 310 → 290 |

z12 pairings over eight seeds: Covenant–Flood 49.4, Covenant–UNSC 50.7, Flood–UNSC 49.4.

**Base units by type and price** (ban impact base only, 36 units, seed 0 against the same seed):

| Group | z0 spread | z12 spread |
|---|---|---|
| Chaff, 100 | 4.4 | 3.5 |
| Anti-chaff, 150 | 25.6 | 15.1 |
| Single-target, 175–300 | 2.6 | 5.8 |
| Single-target, 400–500 | 5.8 | 7.5 |
| Tank, 400–500 | 4.8 | 8.2 |
| Artillery, 250–300 | 4.1 | 4.5 |
| Anti-air, 250–300 | 19.4 | 5.2 |
| Aircraft, 300–400 | 3.4 | 14.4 |
| Titans, 800 | 18.4 | 14.2 |
| Flankers, 200–250 | 2.8 | 9.6 |
| Transports, 300–400 | 11.5 | 6.9 |

8 of 11 groups within 10 points (7 at the start), average spread 9.3 → 8.6. The three wide groups each hold one unit the Flood can hardly answer: the Flamethrower Trooper (+16.5; +48 against the Flood, −24 against the Covenant), the Banshee (+10.0) and the Elephant (+17.1; +35 against the Flood, whose only answer to it is the Combat Form). Their ban impact is how much one matchup depends on them more than how strong they are, so they were left.

### Phase 2: the exclusive units

**The tournament.** All eight sub-factions, Insane against Insane, 100 matches a pairing a seed, and the sub-faction matchup table (`matchups.py`). Targets: every sub-faction has a matchup it clearly wins (55% or more over eight seeds), and exclusive units at or a little above the base units of their type and price. A big spread overall is allowed.

**Reading a ban run here.** A ban changes what the AI buys from the first round, so the match plays out differently and the seed's luck doesn't carry over: seed 0's offset from the four-seed mean shows up in every unit of that sub-faction (−2.7 to +1.9). Read against the mean instead.

**What the AI buys.** The Insane AI scores a unit by 8 × its advantage against the threats it has seen and not yet covered, plus 4 × (its average over the whole counter table − 0.5), plus 0.6 if it is exclusive (`RuleBasedAi.Score`). So a unit whose wins are a subset of another's never gets bought: the Reaver, whose counter-table wins were a subset of the Vampire's, was bought in no match at all.

| Round (4 seeds) | UNSF | CovF | Armo | Ungg | Nobl | Aero | Bani | Lekg | What changed |
|---|---|---|---|---|---|---|---|---|---|
| p0 (z12) | 62.0 | 54.9 | 48.4 | 49.4 | 48.6 | 45.6 | 45.4 | 45.6 | |
| p1 | 63.1 | **58.0** | 48.6 | 48.4 | 46.5 | 46.6 | 44.4 | 44.4 | Six exclusives below their peers up (Marauder, Longsword, Grizzly, Infested Banshee, Elite, Wraith); Infested Warthog 195 → 180 |
| p2 | 62.0 | 55.4 | 49.7 | 49.0 | 46.8 | 47.0 | 45.2 | 44.9 | Covenant-based's three back; Infested Warthog ram 50 → 46, Infested Ghost ram 40 → 37 |
| p3 | 62.4 | 55.4 | 49.8 | 49.0 | 47.0 | 46.7 | 45.1 | 44.5 | Reaver 300 → 250 |
| p4 | 62.5 | 54.4 | 49.6 | 49.0 | 47.1 | 46.8 | 46.4 | 44.2 | Chopper 200 → 175 |
| p5 | 61.9 | 54.2 | 50.1 | 48.1 | 47.0 | 46.5 | 46.7 | 45.5 | Goliath 1760 → 2400; Hunter Captain 12.5 → 15.5, 72 → 90; Skitterer 50 → 60 |
| p6 | 61.6 | 53.9 | 49.5 | 47.7 | 46.6 | 46.5 | 49.1 | 45.1 | Brute Grenadier 150, 32, 35 (its numbers before D140) |
| **p6, seeds 0–7** | **61.5** | **54.9** | **48.8** | **47.9** | **46.8** | **46.7** | **48.3** | **45.2** | |

p1's lesson: an exclusive that sits below its peers by ban impact isn't worth buffing when its sub-faction is already strong. The AI misuses it, but the buff still adds power (Covenant-based 54.9 → 58.0).

**The final matchup table** (p6, seeds 0 to 7, 800 matches a pairing, a cell good to about ±3; the row's win rate against the column):

| | all | UNSF | CovF | Armo | Bani | Ungg | Nobl | Aero | Lekg | best |
|---|---|---|---|---|---|---|---|---|---|---|
| UNSC-based Flood | 61.5 | – | 56 | 42 | 70 | 77 | 57 | 56 | 73 | Ungg 77 |
| Covenant-based Flood | 54.9 | 44 | – | 43 | 57 | 66 | 50 | 51 | 73 | Lekg 73 |
| Armored Division | 48.8 | 58 | 57 | – | 48 | 47 | 40 | 38 | 53 | UNSF 58 |
| Banished | 48.3 | 30 | 43 | 52 | – | 55 | 55 | 56 | 48 | Aero 56 |
| Unggoy Rebellion | 47.9 | 23 | 34 | 53 | 45 | – | 73 | 64 | 42 | Nobl 73 |
| Noble | 46.8 | 43 | 50 | 60 | 45 | 27 | – | 52 | 51 | Armo 60 |
| Aerospace Command | 46.7 | 44 | 49 | 62 | 44 | 36 | 48 | – | 44 | Armo 62 |
| Lekgolo Colony | 45.2 | 27 | 27 | 47 | 52 | 58 | 49 | 56 | – | Ungg 58 |

**The Covenant against the Flood.** With base units only it is 49%; with exclusives the three Covenant sub-factions win 23% to 43% against the two Flood ones. Mostly it is what the AI buys (`spend.py`). Base only, Banished spends 32% of its credits on the Scarab and 22% on the Banshee against the Flood. With exclusives, the Covenant sub-factions spend 8–13% on Banshees and put the rest into the exclusives the counter table likes against Flood units, which lose in real battles: the Grunt Goblin (21% of Unggoy's credits there), the Brute Grenadier and Marauder (13% and 12% of Banished's), the Hunter Captain. UNSC-based Flood spends 22–27% on the Abomination, 14–16% on the Infested Marine and 13–15% on the Infected Phantom against them. A player can choose differently, so this is left for the owner (`MORNING_DECISIONS.md` 200).

**Every unit on the final roster** (p6, every unit bought in 10% or more of its sub-factions' matches ban-tested, 120 runs, read against the eight-seed mean; each unit in `Docs/Balance/d141_unit_review.md`):
- All 14 type-and-price groups that hold two or more units are within 10 points of each other with the exclusives in. Their average spread is 5.7.
- 19 of the 30 exclusives in a group are at or above the base units of their group. None is more than 4 points below, which is inside a ban run's margin of ±4.1 to ±4.6. The lowest are the Infested Banshee (−3.8), the Marauder (−3.6), the Spartan Sniper and Spartan Fireteam (−2.7), the Infested Elite (−2.3), the Grizzly (−2.1) and the Longsword (−2.0).
- The flankers sit highest above their base peers: the Infested Warthog and Infested Ghost are about 8 points above, the Chopper and Skitterer about 4.
- Phase 1's three wide base units shrink once the exclusives are in: the Flamethrower Trooper goes from +16.5 to +1.6, the Elephant from +17.1 to +1.0 and the Banshee from +10.0 to +2.5. Their base-only numbers measured one matchup's dependence on them more than their strength.
- Two units hurt their side, each by more than the margin: the Unggoy Rebellion wins 9 points more without its Scarab (±4), and Armored Division 8 points more without the Pelican (±4). Why isn't tested. For the Pelican, the player-placed transport beacon isn't built yet, so the AI picks the drop points.
