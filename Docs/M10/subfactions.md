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
2. **Lifesteal from a unit's own data** (`lifestealShare`). Lifesteal already existed for items. The Hunter Captain's siphon laser uses it. An item on the same squad doesn't lower it: the stronger of the two applies.

A third field is looks only: `bakedTint` and `bakedScale` let units that share a baked body look different.

What already existed and got new uses: leaps (Spartan CQB, Juggernaut, Infested Marines, Grunts and Elites), auras (Hunter Captain, Methane Wagon, Grunt Goblin), mid-battle drops (Condor Gunship), spawners (Spawner, Infested Titans), death bursts (Methane Wagon), defense arcs (Goliath, Hunter Captain), lobbed shots, beams, and a melee main weapon with a gun on the side, which makes a unit charge in while firing (Chopper, Infested Warthog and Ghost).

## The units

"Wins" counts how many of the other 72 units it beats at equal cost, about 1800 credits an army, 10 battles a pair. Average is its mean win rate. Numbers come from `Tools/Data/gen_subfaction_doc.py`.

<!-- tables -->
<!-- end tables -->

## Choices worth a look

- **Names: "Infested", not "Infected".** "Infected Marine" is what the simulation already calls a Marine that rises in battle. The two would share results lines and model lookups, so the units you buy are "Infested".
- **Grunts as Flood.** In Halo's lore Grunts are poor hosts. I kept "Infested Grunt" because the Bible asks for variants of the Covenant roster, and Grunts already rise as infected in this game. Easy to swap for something else.
- **No infested Spartans**, because the Bible says Spartans can't be infected.
- **Two extra Titans.** Infested Elephant and Infested Scarab give the Flood sub-factions a Titan each that plays differently from the Abomination: both release infested chaff.
- **Pure forms.** The base Flood already has the Stalker, Ranged and Tank forms. So the sub-factions add two from Halo Wars 2's own logs: the **Juggernaut** (UNSC-based) and the **Spawner** (Covenant-based).
- **Goliath's base damage is 250**, against the Bible default of 100. It stands for its Halo Wars 2 job as a building-killer. It is the only exception to the default, and easy to remove.
- **Hunter Captain has no taunt**, though it does in Halo Wars 2. Targeting is always "the closest enemy" (Bible Section 6), and a taunt would break that rule. It has its siphon and a healing aura instead.
- **Methane Wagon fires Fire damage**, which is what makes it deadly to infantry and weak to armor. It has an aura that speeds and heals, but no debuff cloud or "boosts Grunts only", to avoid a new mechanic.
- **Blisterback is Aerial, not Artillery.** It's a flyer that shells the ground. As Aerial its counter is anti-air, which is what you'd expect. Its Halo Wars 2 "deploy to the ground" mode isn't built.
- **Cobra has no lockdown mode**, Gremlin's EMP slows rather than stuns, and the Rhino is Tier 3, more than the Wraith and Archer's Tier 2, because of its reach.
- **Aerospace Command has no Tier 1 exclusive unit, and neither does Noble.** Their draft cards always give base Tier 1 squads. The Bible's band would allow a cheap one, but air or Spartans at Tier 1 seemed wrong.
- **Spartan bodies.** Noble's Spartans use the Marine's rig, bigger and darker green, with a rifle, a rocket launcher or an SMG. It reads as "a big soldier", not a Spartan, and the base Spartan is still a capsule: I left that alone, but it now looks worse than its Noble cousins.
- **Condor Gunship** uses the Pelican model with a grey tint. They are close relatives, and it is much better than a primitive at the same size, but a real Condor should replace it.
- **The faction files are still called `*_Standard.asset`**, to keep every reference. Only the name is left over.

## Balance, first pass

- The counter web passes for all 73 units: every unit has a counter by the Section 7 table that wins at least 90%. It runs in about 2 minutes in the console (`Tools/Data/cw`), against about 30 in Unity.
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

Real models in the game now:

- **New:** Grizzly, Chopper.
- **Reused real prefabs with a tint:** Infested Warthog, Scorpion, Hornet, Elephant, Wraith, Banshee, Scarab.
- **Reused with a tint, as a stopgap:** Condor Gunship (Pelican).
- **Reused baked bodies:** Spartan Sniper, Heavy Weapons, Grenadier, CQB, Fireteam (Marine rig), Cannon Fodder, Heavy Grunts, Infested Grunt (Grunt), Infested Marine (Marine).

Still placeholder primitives, each needing a model:

| Sub-faction | Units |
| --- | --- |
| Armored Division | Cobra, Rhino, Gremlin |
| Aerospace Command | Sparrowhawk, Vulture, Shortsword (Condor Gunship would also like its own) |
| Banished | Marauder, Reaver, Blisterback, Brute Grenadier |
| Lekgolo Colony | Goliath, Skitterer, Hunter Captain |
| Unggoy Rebellion | Methane Wagon, Grunt Goblin |
| Flood, UNSC-based | Juggernaut |
| Flood, Covenant-based | Infested Ghost (no Ghost model exists yet), Infested Elite, Spawner |
| Noble | Spartans of their own, if wanted |

Left out on purpose, as asked: `vehicleshumangroundmammoth.glb` (197,535 triangles, no unit in the Bible) and `halo_3_energy_sword_by_abisv.glb` (would suit melee units later).

## Tools

- `Tools/Data/gen_roster.py` makes the unit and tech assets, the faction assets' lists, `balance.csv` and `cw/units.json`. The sub-factions are `sub_*.py`, one file each.
- `Tools/Data/cw` is the console: `dotnet run -c Release -- 10` runs the full web, `-- 10 id,id` only those units, `-- ai 20` AI matches, `-- dur` battle lengths. `report.py` prints the sub-faction report.
- After a full console run, run `gen_roster.py` (the notes list each unit's two best counters), `gen_counter_list.py` (the test's list), `gen_matchup_asset.py cw/matrix.csv` (the Hard AI's table), and `gen_subfaction_doc.py` (this document).
- `Tools/Art/units/Grizzly.py`, `Chopper.py` prepare the models. `HB_PREPARED` says where to prepare them, since `Art_Source` is shared between worktrees.
- `RealModelBuilder.BuildNewFromCommandLine` and `AnimationBaker.BakeNewFromCommandLine` build only what is missing, leaving the old models untouched.
- `MatchAutoplay -autoplaySubFaction Noble` plays a sub-faction and buys its exclusives first. `SandboxAutoplay -gallery -galleryAll -galleryOnly :` shows every exclusive unit.
