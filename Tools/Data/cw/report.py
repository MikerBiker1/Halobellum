# Reads units.json and matrix.csv and reports on the sub-faction units (M10): each unit's best counter by the Bible's
# Section 7 table (it must win at least 90%), how often it wins overall, and who it beats most oddly.
# Usage: python report.py [only-sub-faction]
import csv, json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
units = json.load(open(os.path.join(HERE, "units.json"), encoding="utf-8"))
rows = list(csv.reader(open(os.path.join(HERE, "matrix.csv"), encoding="utf-8")))
names = rows[0][1:]
m = {}
for r in rows[1:]:
    for i, v in enumerate(r[1:]):
        if v != "":
            m[r[0], names[i]] = float(v)
by = {u["name"]: u for u in units}


def role_counters(counter, target):
    t = target["role"]
    c = counter["role"]
    if t == "AntiAir":
        return not counter["air"] and c != "AntiAir"
    return c in {"Chaff": ["ChaffClear"], "ChaffClear": ["Tank", "Flanker"], "SingleTargetDps": ["Chaff", "Flanker"], "Tank": ["SingleTargetDps"],
                 "Flanker": ["ChaffClear", "Tank"], "Artillery": ["Flanker", "Aerial"], "Aerial": ["AntiAir"], "Titan": ["SingleTargetDps", "Chaff"]}.get(t, [])


fails = []
print(f"{'unit':26}{'sub':18}{'role':16}{'wins':>5} {'avg':>5}  best counters")
for u in units:
    n = u["name"]
    if n not in names:
        continue
    if len(sys.argv) > 1 and u.get("sub") != sys.argv[1]:
        continue
    vals = [m[n, o] for o in names if (n, o) in m]
    if not vals:
        continue
    counters = sorted(((m[o, n], o) for o in names if (o, n) in m and role_counters(by[o], u)), reverse=True)
    best = counters[0][0] if counters else 0
    if best < 0.9:
        fails.append(n)
    if u.get("sub") or len(sys.argv) > 1 or best < 0.9:
        print(f"{n:26}{(u.get('sub') or ''):18}{u['role']:16}{sum(v > 0.5 for v in vals):5d} {sum(vals) / len(vals):5.0%}  " + ", ".join(f"{o} {r:.0%}" for r, o in counters[:3]))
print("\nFAIL (no counter at 90%):", fails if fails else "none")
over = sorted(((sum(m[u["name"], o] for o in names if (u["name"], o) in m) / max(1, sum(1 for o in names if (u["name"], o) in m)), u["name"]) for u in units if u["name"] in names and u["role"] != "Titan"), reverse=True)
print("Over 60% overall (not Titans):", [f"{n} {v:.0%}" for v, n in over if v > 0.6])
print("Under 30% overall:", [f"{n} {v:.0%}" for v, n in over if v < 0.3])
