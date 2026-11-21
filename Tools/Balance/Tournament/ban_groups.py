"""Ban impact by type and price (D138): how much win rate each unit adds to the sub-factions that field it.

  python ban_groups.py <baseline results.jsonl> <ban summary.txt>[=its baseline.jsonl] [more summaries ...] [--propose out_trial.txt]
                       [--slope 2.5] [--damp 0.7] [--cap 0.25] [--k 0.01]

A ban run plays a sub-faction's matches with one unit taken out of its roster (the tournament's "without|Sub|Unit" trial, 100 matches a
pairing); the unit's impact there is the sub-faction's win rate in the baseline tournament minus its win rate in the ban run. Unlike the
edge (win rate when fielded minus when not), it isn't fooled by when the AI buys a unit: it is cause and effect. It is good to about
±4 points per sub-faction. A unit's impact is the mean over the sub-factions that field it.

Units are grouped by type and price as in edge_groups.py; the owner's rule is every unit within about ±5 points of its group. With
--propose it writes a trial file moving every unit more than 3 points from its group's middle toward it, by k power per point, times damp,
capped at ±cap, with the same levers as edge_groups.py. Units missing from the summaries (bought in under 10% of matches) are listed apart.

With --slope S the power change is sized by the unit's share of its sub-factions' credits instead: a unit that is a fifth of an army moves
the army by a fifth of its own power change, and the army's win rate moves by about S points per 1% of army power, so the change is
-gap / (S x share), times damp (shares under 3% count as 3%). Groups in BUFF_ONLY (where most units are traps the AI buys though they cost
it win rate) aim at their best unit instead of the middle, so only the weak ones move.

A later summary overrides an earlier one for the same unit and sub-faction; "summary.txt=baseline.jsonl" pairs a re-measured summary
with the tournament it was played against (the unit's impact is that baseline minus the ban run). Better, "ban/<tag>=<round>_s0.jsonl"
reads the ban runs themselves: a ban run on seed 0 replays that seed's matches exactly but for the missing unit, so it is compared with the
seed-0 tournament match by match, and its margin comes from the matches whose winner changed (about half the noise of the summary).
"""
import collections, json, math, os, re, sys

sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edge_groups import GROUPS, LEVER_OVERRIDE, UNITS, rnd_dmg, rnd_hp


def baseline(path):
    w, g = collections.Counter(), collections.Counter()
    for line in open(path, encoding="utf-8"):
        if not line.strip():
            continue
        m = json.loads(line)
        if m["a"] == m["b"]:
            continue
        g[m["a"]] += 1
        g[m["b"]] += 1
        w[m["a"]] += m["winner"] == "a"
        w[m["b"]] += m["winner"] == "b"
    return {s: 100 * w[s] / g[s] for s in g}


BUFF_ONLY = {"Titans, 800", "Transports, 300"}


def paired_baseline(path):
    return {(m["a"], m["b"], m["i"]): m["winner"] for m in map(json.loads, filter(str.strip, open(path, encoding="utf-8"))) if m["a"] != m["b"]}


def shares(path):
    cr, tot = collections.defaultdict(collections.Counter), collections.Counter()
    for line in open(path, encoding="utf-8"):
        if not line.strip():
            continue
        m = json.loads(line)
        if m["a"] == m["b"]:
            continue
        a = m["sideA"] if m["aOnTeam"] == 1 else m["sideB"]
        b = m["sideB"] if m["aOnTeam"] == 1 else m["sideA"]
        for sub, side in ((m["a"], a), (m["b"], b)):
            for n, v in side["units"].items():
                cr[sub][n] += v[1]
                tot[sub] += v[1]
    return {sub: {n: c / tot[sub] for n, c in units.items()} for sub, units in cr.items()}


def main():
    args = [a for a in sys.argv[1:]]
    opt = lambda name, default: float(args[args.index(name) + 1]) if name in args else default
    k, damp, cap, slope = opt("--k", 0.01), opt("--damp", 0.7 if "--slope" in args else 0.6), opt("--cap", 0.25), opt("--slope", 0)
    propose = args[args.index("--propose") + 1] if "--propose" in args else None
    files = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or not args[i - 1].startswith("--"))]
    base = baseline(files[0])
    share = shares(files[0])
    impact = collections.defaultdict(dict)
    margins = collections.defaultdict(dict)
    for arg in files[1:]:
        path, _, own = arg.partition("=")
        if os.path.isdir(os.path.dirname(path)) and not path.endswith(".txt"):
            # "ban/<tag>=<the round's seed-0 results>": each ban run replays that seed's matches with one unit gone, so it is compared
            # match by match with them (a paired test: only the matches whose winner changed count, and the noise is far smaller).
            ref = paired_baseline(own)
            share = shares(own)
            folder, tag = os.path.split(path)
            for trial in sorted(os.listdir(folder)):
                if not (trial.startswith(tag + "_") and trial.endswith(".trial")):
                    continue
                spec = open(os.path.join(folder, trial), encoding="utf-8").read().strip().split("|")
                res = os.path.join(folder, trial[:-6] + ".jsonl")
                if spec[0] != "without" or not os.path.exists(res):
                    continue
                sub, unit = spec[1], spec[2]
                n = d = 0
                for line in open(res, encoding="utf-8"):
                    m = json.loads(line)
                    if m["a"] == m["b"] or sub not in (m["a"], m["b"]):
                        continue
                    key = (m["a"], m["b"], m["i"])
                    if key not in ref:
                        continue
                    won = (m["winner"] == "a") == (m["a"] == sub)
                    before = (ref[key] == "a") == (m["a"] == sub)
                    n += 1
                    d += before - won
                    margins[unit].setdefault("_disc", {})[sub] = margins[unit].get("_disc", {}).get(sub, 0) + (before != won)
                if n:
                    impact[unit][sub] = 100 * d / n
                    disc = margins[unit]["_disc"][sub]
                    margins[unit][sub] = 196 * math.sqrt(max(disc - d * d / n, 1)) / n
            continue
        b = baseline(own) if own else base
        if own:
            share = shares(own)  # the latest round's armies
        for line in open(path, encoding="utf-8"):
            m = re.match(r"\s*(.+?) without (.+?): ([\d.]+)% of (\d+)", line)
            if m:
                impact[m.group(2)][m.group(1)] = b[m.group(1)] - float(m.group(3))
                margins[m.group(2)][m.group(1)] = 4.2
    units = {u["name"]: u for u in json.load(open(UNITS, encoding="utf-8"))}
    lines, spreads, within = [], [], 0
    for title, lever, names in GROUPS:
        rows = [(n, sum(impact[n].values()) / len(impact[n]), impact[n]) for n in names if n in impact]
        if len(rows) < 2:
            continue
        middle = sum(r[1] for r in rows) / len(rows)
        spread = max(r[1] for r in rows) - min(r[1] for r in rows)
        target = max(r[1] for r in rows) if title in BUFF_ONLY else middle
        spreads.append(spread)
        within += spread <= 10
        print(f"\n== {title}: middle {middle:+.1f}, spread {spread:.0f}")
        for n, imp, subs in sorted(rows, key=lambda r: -r[1]):
            dev = imp - middle
            gap = imp - target
            margin = math.sqrt(sum(margins[n].get(s, 4.2) ** 2 for s in subs)) / len(subs)
            sh = sum(share.get(s, {}).get(n, 0) for s in subs) / len(subs)
            mark = ""
            if propose and abs(gap) > 3 and abs(gap) > margin:
                p = max(-cap, min(cap, -gap * damp / (slope * 100 * max(sh, 0.03)) if slope else -k * gap * damp))
                u = units[n]
                how = LEVER_OVERRIDE.get(n, lever)
                f_hp = (1 + p) if how in ("hp", "hp+burst") else math.sqrt(1 + p) if how == "both" else 1.0
                f_dmg = (1 + p) if how == "dmg" else math.sqrt(1 + p) if how == "both" else 1.0
                changes = []
                if f_hp != 1.0 and rnd_hp(u["hp"] * f_hp) != u["hp"]:
                    changes.append(f"{n}|MaxHealth={rnd_hp(u['hp'] * f_hp)}")
                if f_dmg != 1.0:
                    for wpn in u["weapons"]:
                        if wpn["dmg"] > 0 and rnd_dmg(wpn["dmg"] * f_dmg) != wpn["dmg"]:
                            changes.append(f"{n}|{wpn['name']}|DamagePerShot={rnd_dmg(wpn['dmg'] * f_dmg):g}")
                if how == "hp+burst" and u.get("burst") and u["burst"]["dmg"] > 0 and rnd_dmg(u["burst"]["dmg"] * math.sqrt(1 + p)) != u["burst"]["dmg"]:
                    changes.append(f"{n}|DeathBurst|Damage={rnd_dmg(u['burst']['dmg'] * math.sqrt(1 + p)):g}")
                if changes:
                    lines.append(f"# {n}: impact {imp:+.1f} in {title} (aim {target:+.1f}), {100 * sh:.0f}% of credits, power {100 * p:+.0f}%")
                    lines += changes
                    mark = f"  -> power {100 * p:+.0f}%"
            per_sub = ", ".join(f"{s[:4]} {v:+.0f}" for s, v in sorted(subs.items()))
            print(f"  {n:<24}{imp:>+7.1f} ±{margin:>3.1f}  ({dev:+.1f} from the middle; {100 * sh:.0f}% of credits; {per_sub}){mark}")
        missing = [n for n in names if n not in impact]
        if missing:
            print("  not tested (bought in under 10% of matches):", ", ".join(missing))
    print(f"\nGroups within 10 points: {within} of {len(spreads)}; average spread {sum(spreads) / len(spreads):.1f}")
    print("Baseline sub-factions:", ", ".join(f"{s} {v:.0f}%" for s, v in sorted(base.items(), key=lambda i: -i[1])))
    if propose:
        open(propose, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
        print(f"Wrote {sum(1 for l in lines if not l.startswith('#'))} changes to {propose}")


if __name__ == "__main__":
    main()
