"""Combines army slot test runs (D137): python slot_combine.py <run1.csv> <run2.csv> [...] > combined.txt

Each run is the .csv the tournament tool's --slot mode writes next to its report, made from different armies (for example one per tournament seed).
It prints how well the first two runs agree, then every sub-faction's units with their values averaged over the runs: margin (points of the army's
base damage left over the baseline), its 95% interval, the win-rate value in points, the level the slot came at, and how often the unit was in the
source armies. Last, every unit across the sub-factions that field it.
"""
import collections, csv, math, sys

sys.stdout.reconfigure(encoding="utf-8")  # the report has a ± in it, whatever the console's code page

runs = [{(r["sub"], r["unit"]): r for r in csv.DictReader(open(path, encoding="utf-8"))} for path in sys.argv[1:]]
keys = sorted(set.intersection(*(set(run) for run in runs)))


def corr(xs, ys):
    mx, my = sum(xs) / len(xs), sum(ys) / len(ys)
    return sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / math.sqrt(sum((x - mx) ** 2 for x in xs) * sum((y - my) ** 2 for y in ys))


if len(runs) >= 2:
    xs = [float(runs[0][k]["margin_value"]) for k in keys]
    ys = [float(runs[1][k]["margin_value"]) for k in keys]
    print(f"Agreement of the first two runs over {len(keys)} units: r = {corr(xs, ys):.2f}, average difference {sum(abs(x - y) for x, y in zip(xs, ys)) / len(xs):.1f} points of margin.")

rows = collections.defaultdict(list)
for k in keys:
    margin = sum(float(run[k]["margin_value"]) for run in runs) / len(runs)
    ci = math.sqrt(sum(float(run[k]["margin_ci95"]) ** 2 for run in runs)) / len(runs)
    win = sum(float(run[k]["value"]) for run in runs) / len(runs)
    level = sum(float(run[k]["level"]) for run in runs) / len(runs)
    share = sum(float(run[k]["in_armies"]) for run in runs) / len(runs)
    rows[k[0]].append((k[1], int(runs[0][k]["cost"]), level, margin, ci, win, share))

print("margin = points of the army's base damage left over the baseline (0 = worth the army's average credit); ± = 95% interval;")
print("win = the same in points of win rate; lvl = the level its slot came at; in = share of the source armies that had it.")
for sub in sorted(rows):
    print(f"\n== {sub}")
    print(f"{'unit':<24}{'cost':>5}{'lvl':>5}{'margin':>8}{'±':>5}{'win':>7}{'in':>6}")
    for name, cost, level, margin, ci, win, share in sorted(rows[sub], key=lambda r: -r[3]):
        print(f"{name:<24}{cost:>5}{level:>5.1f}{margin:>+8.1f}{ci:>5.1f}{win:>+7.1f}{share:>5.0f}%")

print("\n== Every unit across the sub-factions that field it (margin)")
by_unit = collections.defaultdict(list)
for sub, entries in rows.items():
    for name, cost, level, margin, ci, win, share in entries:
        by_unit[name].append((sub, margin))
for name in sorted(by_unit, key=lambda n: -sum(m for _, m in by_unit[n]) / len(by_unit[n])):
    values = by_unit[name]
    print(f"{name:<24}{sum(m for _, m in values) / len(values):>+7.1f}   " + ", ".join(f"{sub} {m:+.0f}" for sub, m in sorted(values)))
