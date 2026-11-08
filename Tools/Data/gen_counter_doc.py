# Writes Docs/Balance/counter_web.md from cw/units.json and cw/matrix.csv: every unit with the five it does best against and the five
# it does worst against (the owner, 2026-11-07: top 5 only).
# Run it after the counter web changes: python gen_counter_doc.py
import csv, json, os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Docs", "Balance", "counter_web.md")
TOP = 5

units = json.load(open(os.path.join(HERE, "cw", "units.json"), encoding="utf-8"))
rows = list(csv.reader(open(os.path.join(HERE, "cw", "matrix.csv"), encoding="utf-8")))
names = rows[0][1:]
win = {(r[0], names[i]): float(v) for r in rows[1:] for i, v in enumerate(r[1:]) if v != ""}

GROUPS = [("UNSC", None), ("UNSC", "Noble"), ("UNSC", "Aerospace Command"), ("UNSC", "Armored Division"),
          ("Covenant", None), ("Covenant", "Banished"), ("Covenant", "Lekgolo Colony"), ("Covenant", "Unggoy Rebellion"),
          ("Flood", None), ("Flood", "UNSC-based"), ("Flood", "Covenant-based")]


def average(name):
    mine = [s for (a, b), s in win.items() if a == name]
    return sum(mine) / max(1, len(mine))


AVERAGE = {n: average(n) for n in names}


def listed(pairs):
    return ", ".join(f"{n} {round(100 * s)}%" for n, s in pairs) or "-"


lines = [
    "# The counter web: good against, bad against",
    "",
    "Every unit against every other, one army against another of the same value (about 1,800 credits each), 10 battles a pair,",
    "no techs, no towers, no spells, on open ground (`Tools/Data/cw`). **Good against:** the five it does best against, with its own win rate;",
    "where several are equal, the strongest of them first (beating a strong unit says more). **Bad against:** the five it does worst against;",
    "where several are equal, the weakest of them first (losing to a weak unit says more).",
    "**Wins** is its average win rate against all the others. The Hard AI picks its counters from this same table.",
    "It measures armies of one unit in a straight fight: a unit that shines behind a screen of chaff, or with levels and techs, can look worse here than in play.",
    "",
    "Written by `Tools/Data/gen_counter_doc.py`; do not edit by hand.",
    "",
]
for faction, sub in GROUPS:
    group = [u for u in units if u["faction"] == faction and u.get("sub") == sub]
    if not group:
        continue
    lines += [f"## {faction}" + (f": {sub}" if sub else " (every sub-faction)"), "", "| Unit | Role | Cost | Wins | Good against | Bad against |", "|---|---|---|---|---|---|"]
    for u in sorted(group, key=lambda u: (u["tier"], u["cost"], u["name"])):
        n = u["name"]
        if n not in names:
            continue
        mine = [(o, win[(n, o)]) for o in names if o != n and (n, o) in win]
        avg = 100 * sum(s for _, s in mine) / max(1, len(mine))
        good = sorted(mine, key=lambda p: (-p[1], -AVERAGE[p[0]], p[0]))[:TOP]
        bad = sorted(mine, key=lambda p: (p[1], AVERAGE[p[0]], p[0]))[:TOP]
        lines.append(f"| **{n}** | {u['role']} | {u['cost']} | {avg:.0f}% | {listed(good)} | {listed(bad)} |")
    lines.append("")
open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
print(f"{len(names)} units written to Docs/Balance/counter_web.md")
