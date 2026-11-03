# Win rates side by side, from several tournament results files. Usage: python compare.py name=results.jsonl ...
import collections, json, sys

runs = [arg.split("=", 1) for arg in sys.argv[1:]]
table = {}
factions = {}
subs = set()
for name, path in runs:
    wins, games, fwins, fgames = collections.Counter(), collections.Counter(), collections.Counter(), collections.Counter()
    for raw in open(path, encoding="utf-8"):
        if not raw.strip():
            continue
        m = json.loads(raw)
        if m["a"] == m["b"]:
            continue
        for sub, faction, other, won in ((m["a"], m["fa"], m["fb"], m["winner"] == "a"), (m["b"], m["fb"], m["fa"], m["winner"] == "b")):
            games[sub] += 1
            wins[sub] += won
            factions[sub] = faction
            subs.add(sub)
            if faction != other:
                fgames[faction] += 1
                fwins[faction] += won
    table[name] = (wins, games, fwins, fgames)


def cell(w, g, key):
    return f"{100 * w[key] / g[key]:.0f}%" if g[key] else "-"


print(f"{'':<22}" + "".join(f"{name:>14}" for name, _ in runs))
for faction in ("Covenant", "UNSC", "Flood"):
    print(f"{faction:<22}" + "".join(f"{cell(table[n][2], table[n][3], faction):>14}" for n, _ in runs))
print()
for sub in sorted(subs, key=lambda s: -table[runs[0][0]][0][s] / max(1, table[runs[0][0]][1][s])):
    print(f"{sub:<22}" + "".join(f"{cell(table[n][0], table[n][1], sub):>14}" for n, _ in runs))
print(f"{'matches a sub-faction':<22}" + "".join(f"{table[n][1][next(iter(subs))]:>14}" for n, _ in runs))
