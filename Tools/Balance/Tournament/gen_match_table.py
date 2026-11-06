"""The AI's matchup table from whole matches (D76): python gen_match_table.py <one-on-one matrix.csv> <out.csv> <results.jsonl> [more results ...]

For every match, every unit a side fielded is set against every unit the other side fielded; the side's win counts for that pair, weighted by
how much of each army the two units were (their share of their side's credits on the field), so the units an army was built on count most.
A pair's rate is its weighted wins over its weight, with the one-on-one counter web's value as a prior worth PRIOR of weight: where whole
matches say little (pairs that rarely meet), the table keeps the one-on-one value. X against Y and Y against X come from the same matches,
so they always add up to 1. Mirrors are left out. The output has the counter web's layout, so the tournament and gen_matchup_asset.py read it.
"""
import collections, csv, json, sys

PRIOR = 1.0  # A match's pairs share a weight of 1 between them; a big pair in a few hundred matches outweighs this easily.

matrix_path, out_path, results = sys.argv[1], sys.argv[2], sys.argv[3:]
rows = list(csv.reader(open(matrix_path, encoding="utf-8")))
names = rows[0][1:]
known = set(names)
one_on_one = {(r[0], names[i]): float(v) for r in rows[1:] for i, v in enumerate(r[1:]) if v != ""}

wins = collections.defaultdict(float)
weight = collections.defaultdict(float)
matches = 0
for path in results:
    for line in open(path, encoding="utf-8"):
        if not line.strip():
            continue
        m = json.loads(line)
        if m["a"] == m["b"]:
            continue
        a = m["sideA"] if m["aOnTeam"] == 1 else m["sideB"]
        b = m["sideB"] if m["aOnTeam"] == 1 else m["sideA"]
        a_won = m["winner"] == "a"

        def shares(side):
            credits = {name: v[1] for name, v in side["units"].items() if name in known and v[1] > 0}
            total = sum(credits.values())
            return {name: c / total for name, c in credits.items()} if total > 0 else {}

        sa, sb = shares(a), shares(b)
        if not sa or not sb:
            continue
        matches += 1
        for x, wx in sa.items():
            for y, wy in sb.items():
                if x == y:
                    continue
                w = wx * wy
                weight[(x, y)] += w
                weight[(y, x)] += w
                wins[(x, y)] += w if a_won else 0.0
                wins[(y, x)] += 0.0 if a_won else w

table = {}
moved = []
for x in names:
    for y in names:
        if x == y or (x, y) not in one_on_one:
            continue
        prior = one_on_one[(x, y)]
        rate = (wins[(x, y)] + PRIOR * prior) / (weight[(x, y)] + PRIOR)
        table[(x, y)] = rate
        moved.append((abs(rate - prior), x, y, prior, rate, weight[(x, y)]))

out = [rows[0]]
for x in names:
    out.append([x] + [f"{table[(x, y)]:.2f}" if (x, y) in table else "" for y in names])
with open(out_path, "w", encoding="utf-8", newline="") as f:
    csv.writer(f, lineterminator="\n").writerows(out)

moved.sort(reverse=True)
print(f"{matches} matches; {sum(1 for v in weight.values() if v >= PRIOR)} of {len(table)} pairs have more evidence than the prior")
print("Biggest changes from the one-on-one table (unit, against, one on one, whole matches, weight):")
for d, x, y, p, r, w in moved[:15]:
    print(f"  {x:24} vs {y:24} {p:.2f} -> {r:.2f}  ({w:.1f})")
