"""Edge by type and price (D138, the owner: "prioritise edge (meaning winrate) for each type of units in their range of price").

  python edge_groups.py <results.jsonl> [--propose out_trial.txt] [--k 0.015] [--damp 0.6] [--cap 0.25]

A unit's edge in a sub-faction is its side's win rate in the matches where it fought minus the win rate in those where it didn't. It is pooled
over the sub-factions that field it, each weighted by how precise it is (1 / variance), and only where the unit fought in, and was missing
from, at least 40 matches. Units are grouped by type and price; each group's middle is the plain mean of its units' edges, and the spread is
best minus worst. The owner's rule: every unit within about ±5 points of its group (a spread of 10 or less).

With --propose it writes a trial file (the tournament's format, absolute values from Tools/Data/cw/units.json) that moves every unit more
than 3 points (and more than its own 95% margin) from its group's middle toward it: a power change of k per point of the gap, times damp,
capped at ±cap. The lever suits the unit: health for chaff, tanks, flankers and transports; damage for single-target units and artillery;
both for the rest (each by the square root, so that health times damage moves by the power change). Units seen in fewer than 150 matches
are not moved: their edge is too uncertain. apply_trial.py writes such a file into the roster.
"""
import collections, json, math, os, sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
UNITS = os.path.join(HERE, "..", "..", "Data", "cw", "units.json")

GROUPS = [  # (title, lever, units): lever = "hp", "dmg" or "both"
    ("Chaff, 100", "hp", ["Marine", "Grunt", "Infection Forms", "Cannon Fodder", "Infested Marine"]),
    ("Anti-chaff, 150", "both", ["Fuel Rod Grunt", "Flamethrower Trooper", "Carrier Form", "Brute Grenadier"]),
    ("Single-target, 175-250", "dmg", ["Jackal (Beam Rifle)", "Combat Form", "Gauss Warthog", "Heavy Grunts", "Infested Elite"]),
    ("Single-target, 300", "dmg", ["Elite", "Cobra"]),
    ("Single-target, 400-500", "dmg", ["Spartan", "Zealot", "Seeder", "Spartan Sniper", "Spartan Fireteam", "Hunter Captain"]),
    ("Tank, 275-300", "hp", ["Scorpion", "Infested Scorpion", "Marauder"]),
    ("Tank, 400-500", "hp", ["Hunter Pair", "Pure Form Tank", "Mantis", "Spawner", "Spartan Heavy Weapons", "Grunt Goblin", "Grizzly", "Goliath"]),
    ("Artillery, 250-400", "dmg", ["Pure Form Ranged", "Archer Missile Pod", "Wraith", "Infested Wraith", "Rhino"]),
    ("Anti-air, 250-300", "both", ["Swarm Form", "Wolverine", "Vampire", "Reaver"]),
    ("Aircraft, 250-300", "both", ["Hornet", "Banshee", "Infested Banshee", "Infested Hornet", "Sparrowhawk", "Longsword"]),
    ("Aircraft, 400-500", "both", ["Infected Pelican", "Infected Phantom", "Vulture", "Condor Gunship"]),
    ("Titans, 800", "both", ["Elephant", "Scarab", "Flood Abomination"]),
    ("Flankers, 125-250", "hp", ["Warthog", "Ghost", "Stalker Form", "Chopper", "Gremlin", "Skitterer", "Infested Warthog", "Infested Ghost"]),
    ("Flankers, 300-400", "hp", ["Spartan CQB", "Juggernaut"]),
    ("Transports, 300", "hp", ["Pelican", "Covenant Phantom"]),
]
# Units whose worth is mostly elsewhere than their own guns: moved by health (and their burst) instead of the group's lever.
LEVER_OVERRIDE = {"Infected Phantom": "hp", "Spawner": "hp", "Carrier Form": "hp+burst", "Infected Pelican": "hp+burst"}


def edges(path):
    per = collections.defaultdict(lambda: {"g": 0, "w": 0, "u": collections.defaultdict(lambda: [0, 0])})
    for line in open(path, encoding="utf-8"):
        if not line.strip():
            continue
        m = json.loads(line)
        if m["a"] == m["b"]:
            continue
        a = m["sideA"] if m["aOnTeam"] == 1 else m["sideB"]
        b = m["sideB"] if m["aOnTeam"] == 1 else m["sideA"]
        for sub, side, won in ((m["a"], a, m["winner"] == "a"), (m["b"], b, m["winner"] == "b")):
            s = per[sub]
            s["g"] += 1
            s["w"] += won
            for n, v in side["units"].items():
                if v[1] > 0:
                    s["u"][n][0] += 1
                    s["u"][n][1] += won
    pooled = {}
    for n in {n for s in per.values() for n in s["u"]}:
        num = den = 0.0
        seen = 0
        subs = {}
        for sub, s in per.items():
            f, w = s["u"].get(n, [0, 0])
            wo = s["g"] - f
            if f < 40 or wo < 40:
                continue
            e = 100 * (w / f - (s["w"] - w) / wo)
            var = 2500 / f + 2500 / wo  # in points squared, with p near one half
            num += e / var
            den += 1 / var
            seen += f
            subs[sub] = e
        if den:
            pooled[n] = (num / den, 1.96 / math.sqrt(den), seen, subs)
    return pooled, per


def rnd_hp(v):
    return round(v) if v < 30 else 5 * round(v / 5) if v < 200 else 10 * round(v / 10)


def rnd_dmg(v):
    return round(v * 2) / 2 if v < 20 else round(v)


def main():
    path = sys.argv[1]
    opt = lambda name, default: float(sys.argv[sys.argv.index(name) + 1]) if name in sys.argv else default
    k, damp, cap = opt("--k", 0.015), opt("--damp", 0.6), opt("--cap", 0.25)
    propose = sys.argv[sys.argv.index("--propose") + 1] if "--propose" in sys.argv else None
    pooled, per = edges(path)
    units = {u["name"]: u for u in json.load(open(UNITS, encoding="utf-8"))}
    lines, total, within = [], 0, 0
    spreads = []
    for title, lever, names in GROUPS:
        rows = [(n, *pooled[n]) for n in names if n in pooled]
        if len(rows) < 2:
            continue
        middle = sum(r[1] for r in rows) / len(rows)
        spread = max(r[1] for r in rows) - min(r[1] for r in rows)
        spreads.append((title, spread))
        total += 1
        within += spread <= 10
        print(f"\n== {title}: middle {middle:+.1f}, spread {spread:.0f}")
        for n, e, margin, seen, subs in sorted(rows, key=lambda r: -r[1]):
            dev = e - middle
            move = abs(dev) > 3 and abs(dev) > margin and seen >= 150
            mark = ""
            if move and propose:
                p = max(-cap, min(cap, -k * dev * damp))
                u = units[n]
                how = LEVER_OVERRIDE.get(n, lever)
                f_hp = (1 + p) if how in ("hp", "hp+burst") else math.sqrt(1 + p) if how == "both" else 1.0
                f_dmg = (1 + p) if how == "dmg" else math.sqrt(1 + p) if how == "both" else 1.0
                changes = []
                if f_hp != 1.0:
                    new = rnd_hp(u["hp"] * f_hp)
                    if new != u["hp"]:
                        changes.append(f"{n}|MaxHealth={new}")
                if f_dmg != 1.0:
                    for w in u["weapons"]:
                        if w["dmg"] > 0:
                            new = rnd_dmg(w["dmg"] * f_dmg)
                            if new != w["dmg"]:
                                changes.append(f"{n}|{w['name']}|DamagePerShot={new:g}")
                if how == "hp+burst" and u.get("burst") and u["burst"]["dmg"] > 0:
                    new = rnd_dmg(u["burst"]["dmg"] * (1 + p) ** 0.5)
                    if new != u["burst"]["dmg"]:
                        changes.append(f"{n}|DeathBurst|Damage={new:g}")
                if changes:
                    lines.append(f"# {n}: edge {e:+.1f} in {title} (middle {middle:+.1f}), power {100 * p:+.0f}%")
                    lines += changes
                    mark = f"  -> power {100 * p:+.0f}%"
            per_sub = ", ".join(f"{s[:4]} {v:+.0f}" for s, v in sorted(subs.items()))
            print(f"  {n:<24}{e:>+7.1f} ±{margin:>4.1f}  ({dev:+.1f} from the middle, {seen} matches; {per_sub}){mark}")
    print(f"\nGroups within 10 points: {within} of {total}; average spread {sum(s for _, s in spreads) / len(spreads):.1f}")
    print("Sub-factions:", ", ".join(f"{s} {100 * v['w'] / v['g']:.0f}%" for s, v in sorted(per.items(), key=lambda i: -i[1]['w'] / i[1]['g'])))
    if propose:
        open(propose, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
        print(f"Wrote {sum(1 for l in lines if not l.startswith('#'))} changes to {propose}")


if __name__ == "__main__":
    main()
