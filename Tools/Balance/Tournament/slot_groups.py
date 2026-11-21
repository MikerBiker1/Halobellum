"""Army slot test values by type and price (D138): python slot_groups.py <run1.csv> [<run2.csv> ...]

The owner's rule (2026-11-12): units of the same type and price should be within ±5 points of each other (no 150-credit anti-chaff unit 10
points from another), and a sub-faction's exclusive units may be a little stronger than the base units for their price. For each group it
prints every unit's margin (averaged over the runs and over the sub-factions that field it, then per sub-faction), the group's spread, and
how far each unit is from the middle of the group's base units. Flankers and transports are listed apart: their numbers wait for the
changes the owner asked for (flankers that go for towers, transports with a beacon).
"""
import collections, csv, sys

sys.stdout.reconfigure(encoding="utf-8")

GROUPS = [
    ("Chaff, 100", ["Marine", "Grunt", "Infection Forms", "Cannon Fodder", "Infested Marine"]),
    ("Anti-chaff, 150", ["Fuel Rod Grunt", "Flamethrower Trooper", "Carrier Form", "Brute Grenadier"]),
    ("Single-target, 175-250", ["Jackal (Beam Rifle)", "Combat Form", "Gauss Warthog", "Heavy Grunts", "Infested Elite"]),
    ("Single-target, 300", ["Elite", "Cobra"]),
    ("Single-target, 400-500", ["Spartan", "Zealot", "Seeder", "Spartan Sniper", "Spartan Fireteam", "Hunter Captain"]),
    ("Anti-chaff, 400", ["Spartan Grenadier"]),
    ("Tank, 150", ["Jackal (Shield)"]),
    ("Tank, 275-300", ["Scorpion", "Infested Scorpion", "Marauder"]),
    ("Tank, 400-500", ["Hunter Pair", "Pure Form Tank", "Mantis", "Spawner", "Spartan Heavy Weapons", "Grunt Goblin", "Grizzly", "Goliath"]),
    ("Artillery, 250-400", ["Pure Form Ranged", "Archer Missile Pod", "Wraith", "Infested Wraith", "Rhino"]),
    ("Anti-air, 250-300", ["Swarm Form", "Wolverine", "Vampire", "Reaver"]),
    ("Aircraft, 250-300", ["Hornet", "Banshee", "Infested Banshee", "Infested Hornet", "Sparrowhawk", "Longsword"]),
    ("Aircraft, 400-500", ["Infected Pelican", "Infected Phantom", "Vulture", "Condor Gunship"]),
    ("Titans, 800", ["Elephant", "Scarab", "Flood Abomination"]),
    ("Flankers (wait for the tower change)", ["Warthog", "Ghost", "Stalker Form", "Chopper", "Gremlin", "Skitterer", "Infested Warthog", "Infested Ghost", "Juggernaut", "Spartan CQB"]),
    ("Transports (wait for the beacon)", ["Pelican", "Covenant Phantom"]),
]
SHORT = {"Aerospace Command": "Aero", "Armored Division": "Arm", "Noble": "Nob", "Banished": "Ban", "Lekgolo Colony": "Lek",
         "Unggoy Rebellion": "Ung", "Covenant-based": "Cov", "UNSC-based": "UNSCf"}

runs = [list(csv.DictReader(open(p, encoding="utf-8"))) for p in sys.argv[1:]]
values = collections.defaultdict(lambda: collections.defaultdict(list))
exclusive = set()
for run in runs:
    count = collections.Counter(r["unit"] for r in run)
    for r in run:
        values[r["unit"]][r["sub"]].append(float(r["margin_value"]))
units = {u: {s: sum(v) / len(v) for s, v in subs.items()} for u, subs in values.items()}
for u, subs in units.items():
    if len(subs) == 1 and not any(u == n for n in ("Infection Forms", "Carrier Form", "Combat Form", "Stalker Form", "Pure Form Ranged",
                                                    "Swarm Form", "Pure Form Tank", "Seeder", "Infected Pelican", "Infected Phantom", "Flood Abomination")):
        exclusive.add(u)
# The two Flood sub-factions share one base roster; their exclusive units are the infested vehicles and the Juggernaut and Spawner.
for u in ("Infested Marine", "Infested Warthog", "Infested Scorpion", "Infested Hornet", "Juggernaut", "Infested Ghost", "Infested Elite",
          "Infested Wraith", "Infested Banshee", "Spawner"):
    exclusive.add(u)

listed = set()
for title, names in GROUPS:
    rows = [(n, sum(units[n].values()) / len(units[n]), units[n]) for n in names if n in units]
    listed.update(n for n, _, _ in rows)
    base = [m for n, m, _ in rows if n not in exclusive] or [m for _, m, _ in rows]
    middle = sum(base) / len(base)
    spread = max(m for _, m, _ in rows) - min(m for _, m, _ in rows)
    print(f"\n== {title}: middle of the base units {middle:+.1f}, spread {spread:.1f}")
    for n, m, subs in sorted(rows, key=lambda r: -r[1]):
        tag = "*" if n in exclusive else " "
        per = ", ".join(f"{SHORT[s]} {v:+.0f}" for s, v in sorted(subs.items())) if len(subs) > 1 else ""
        print(f"  {tag}{n:<24}{m:>+7.1f}  ({m - middle:+.1f} from the middle)  {per}")
missing = sorted(set(units) - listed)
if missing:
    print("\nNot in a group:", ", ".join(missing))
print("\n* = a sub-faction's exclusive unit (may sit a little above the base units).")
