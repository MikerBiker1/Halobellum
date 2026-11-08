# Writes Docs/Balance/true_counter.md from cw/units.json and cw/true_matrix.csv (dotnet run -c Release -- true, in cw): one squad of
# each unit against two squads of every other, whatever they cost (the owner, 2026-11-07). For each unit: the five it beats best even two
# against one, and the five that beat two squads of it with one of theirs, its true counters.
# Run it after the true counter web changes: python gen_true_counter_doc.py
import csv, json, os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Docs", "Balance", "true_counter.md")
TOP = 5
HALF = 0.5  # Only matchups won at least half the time are listed.

units = json.load(open(os.path.join(HERE, "cw", "units.json"), encoding="utf-8"))
rows = list(csv.reader(open(os.path.join(HERE, "cw", "true_matrix.csv"), encoding="utf-8")))
names = rows[0][1:]
# win[(a, b)]: one squad of a against two squads of b, a's win rate.
win = {(r[0], names[i]): float(v) for r in rows[1:] for i, v in enumerate(r[1:]) if v != ""}
ALONE = {n: sum(s for (a, b), s in win.items() if a == n) / max(1, sum(1 for (a, b) in win if a == n)) for n in names}

GROUPS = [("UNSC", None), ("UNSC", "Noble"), ("UNSC", "Aerospace Command"), ("UNSC", "Armored Division"),
          ("Covenant", None), ("Covenant", "Banished"), ("Covenant", "Lekgolo Colony"), ("Covenant", "Unggoy Rebellion"),
          ("Flood", None), ("Flood", "UNSC-based"), ("Flood", "Covenant-based")]


def listed(pairs):
    return ", ".join(f"{n} {round(100 * s)}%" for n, s in pairs) or "none"


lines = [
    "# True counters: one squad against two",
    "",
    "Every unit, one squad of it, against two squads of every other unit, whatever they cost; 10 battles a pair, half with the sides swapped,",
    "no techs, no towers, no spells, on open ground (`Tools/Data/cw`, `dotnet run -c Release -- true`). A unit that beats two squads of another",
    "with one of its own truly counters it.",
    "",
    "- **Alone wins:** its average win rate, one squad against two, over every other unit.",
    "- **Beats two:** how many of the other units it beats, two squads of them, at least half the time.",
    "- **Beats two of:** the five it does best against while outnumbered two to one, with its win rate (equal ones: the strongest first).",
    "- **Countered by:** the five whose single squad does best against two squads of it, with their win rate (equal ones: the weakest first).",
    "- Only matchups won at least half the time are listed; \"none\" means no unit manages it.",
    "",
    "Squads differ in cost: a squad of Grunts is cheaper than one Scarab, so this is about squads, not credits (the counter web, `counter_web.md`,",
    "matches armies of equal value). The Hard AI doesn't use this table.",
    "",
    "Written by `Tools/Data/gen_true_counter_doc.py`; do not edit by hand.",
    "",
]
for faction, sub in GROUPS:
    group = [u for u in units if u["faction"] == faction and u.get("sub") == sub]
    if not group:
        continue
    lines += [f"## {faction}" + (f": {sub}" if sub else " (every sub-faction)"), "",
              "| Unit | Role | Cost | Alone wins | Beats two | Beats two of | Countered by |", "|---|---|---|---|---|---|---|"]
    for u in sorted(group, key=lambda u: (u["tier"], u["cost"], u["name"])):
        n = u["name"]
        if n not in names:
            continue
        beats = [(o, win[(n, o)]) for o in names if o != n and (n, o) in win]
        countered = [(o, win[(o, n)]) for o in names if o != n and (o, n) in win]
        good = sorted([p for p in beats if p[1] >= HALF], key=lambda p: (-p[1], -ALONE[p[0]], p[0]))[:TOP]
        bad = sorted([p for p in countered if p[1] >= HALF], key=lambda p: (-p[1], ALONE[p[0]], p[0]))[:TOP]
        count = sum(1 for _, s in beats if s >= HALF)
        lines.append(f"| **{n}** | {u['role']} | {u['cost']} | {100 * ALONE[n]:.0f}% | {count} of {len(beats)} | {listed(good)} | {listed(bad)} |")
    lines.append("")
open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
print(f"{len(names)} units written to Docs/Balance/true_counter.md")
