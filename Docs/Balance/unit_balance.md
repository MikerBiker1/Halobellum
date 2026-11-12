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
