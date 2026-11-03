# Balance suggestions from the Hard AI tournament (D59)

Nothing here is in the game yet. These changes were only tried in the tournament (`Tools/Balance/Tournament/trials/suggested.txt`), and wait for the owner's approval.

- **The game as it is:** `hard_ai_tournament_baseline.txt`
- **With these changes:** `hard_ai_tournament_suggested.txt`

Both are 3,600 whole matches between two Hard AIs: 100 for each of the 36 pairings of sub-factions, including each against itself, with the sides swapped in half of them. A sub-faction's win rate below comes from 700 matches and is good to about 4 points either way.

## What the tournament found

| | The game as it is | With the suggested changes |
| --- | --- | --- |
| Covenant (against other factions) | 74% | 52% |
| UNSC | 51% | 53% |
| Flood | 19% | 44% |
| Lekgolo Colony | 68% | 54% |
| Unggoy Rebellion | 68% | 51% |
| Banished | 67% | 49% |
| Armored Division | 58% | 55% |
| Noble | 53% | 53% |
| Aerospace Command | 40% | 47% |
| UNSC-based Flood | 23% | 46% |
| Covenant-based Flood | 23% | 44% |

- **The Scarab is the biggest single cause of Covenant's lead.** It deals 1,417 damage per 100 credits per round, the Elephant 798 and the Abomination 642. A trial with its beam down 30% and its health at 8,000 took Covenant from 74% to 63% and UNSC from 51% to 63%, so the suggestion below cuts it a little less.
- **Flood is weak across its roster, not in one place.** Cheaper units gave it about 9 points, a tougher and faster Abomination about 10 more, and tougher, faster chaff about 5.
- **Towers are not why Flood loses.** Flood does worse without them: 12%, against 19% with them.
- **The two sides are fair.** Team A wins 49% of all matches, and 48% of matches between a sub-faction and itself.
- **The base health rule for starting cards works.** Cards worth 250, 300 and 350 win 51%, 48% and 51% in the game as it is, and 52%, 50% and 48% with the suggested changes.
- **The Hard AI hardly uses the towers.** It reached level 2 or above on 42 of 14,400 towers, and bought no research at all. It spends nearly all its credits on squads first, so this tournament says little about tower levels and research.

## The suggested changes

| Unit | Change | Why |
| --- | --- | --- |
| Scarab | Focus Beam 40 to 30 per hit, health 9,000 to 8,500 | Far ahead of the other Titans |
| Flood Abomination | Health 7,000 to 8,500, speed 1 to 1.25 | It has to walk into melee range |
| Infested Marine | Health 110 to 130, speed 2.25 to 3 | Dies before it reaches anything |
| Infested Grunt | Health 62 to 75, speed 2.25 to 3 | The same |
| Combat Form | Health 220 to 260 | Won least of all Flood units |
| Pure Form Ranged, Swarm Form, Infested Scorpion, Infested Hornet, Infested Elite, Infested Wraith, Infested Banshee | 300 to 250 | Bottom of the Tier 2 band |
| Infected Phantom, Seeder, Juggernaut, Spawner | 450 to 400 | Bottom of the Tier 3 band |
| Pure Form Tank | 500 to 400 | Bottom of the Tier 3 band |
| Vulture | 500 to 400 | Aerospace Command was the weakest left |
| Condor Gunship | 450 to 400 | The same |
| Sparrowhawk | 300 to 250 | The same |

If these are approved, the Tier 3 units that move also need their unlock price at half their new price (the Bible's rule): the Vulture 250 to 200 and the Condor Gunship 225 to 200. The trial left unlock prices as they are.

## Limits

- It measures two Hard AIs, not good players. A unit the AI uses badly looks weak here.
- In the trials the AI still chooses its counters from the current counter table. Applying the changes means rerunning the counter web, and then the tournament once more.
- The table of who beats whom still runs from 26% to 74% for single pairings. Some of that is wanted, as counters, and each of those numbers is good only to about 10 points.

## Running it again

```bash
HB_TOURNAMENT_DATA='E:\AI\_hbci\tournament\game_data.json' bash /e/AI/_hbwt/unity.sh merged exec Halobellum.EditorTools.TournamentDataExport.ExportFromCommandLine
```

```bash
cd Tools/Balance/Tournament && dotnet run -c Release -- 'E:\AI\_hbci\tournament\game_data.json' ../../Data/cw/matrix.csv 'E:\AI\_hbci\tournament\results.jsonl' --matches 100
```

```bash
cd Tools/Balance/Tournament && python report.py 'E:\AI\_hbci\tournament\results.jsonl' ../../../Docs/Balance/report.txt "A title"
```

A run takes about 5 minutes. It saves every match, so if it is stopped, the same command carries on from there. `--trial <file>` tries changes without touching the game, and `compare.py` puts runs side by side.
