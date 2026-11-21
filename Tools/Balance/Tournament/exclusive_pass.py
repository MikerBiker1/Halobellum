"""Sub-factions toward 50% by their exclusive units only (D138, the owner: "THEN, we tweek subfaction exclusive units only to bring
them to as close to 50% win rate as possible").

  python exclusive_pass.py <round results.jsonl> [ban specs as for ban_groups.py ...] --propose out_trial.txt [--slope 4] [--damp 0.8] [--cap 0.25]

A sub-faction's win rate moves by about S points per 1% of its army's power (measured on the D138 ban rounds: S about 4). So a
sub-faction at r% needs its army's power moved by (50 - r) / S percent, times damp; its exclusive units are a share of its credits, so
each of them moves by that over their share. Where ban impacts are given, an exclusive that sits on the far side of its type-and-price
group's middle (above the middle for a buff, below it for a nerf) is left alone if the others can carry the change, so that the pass
doesn't undo the groups. Levers as in edge_groups.py; each unit's change is capped at +-cap.
"""
import collections, json, math, os, sys

sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edge_groups import GROUPS, LEVER_OVERRIDE, UNITS, rnd_dmg, rnd_hp
import ban_groups

LEVER = {n: lever for _, lever, names in GROUPS for n in names}
GROUP_OF = {n: title for title, _, names in GROUPS for n in names}


def rates_and_shares(path):
    w, g = collections.Counter(), collections.Counter()
    cr, tot = collections.defaultdict(collections.Counter), collections.Counter()
    for line in open(path, encoding="utf-8"):
        if not line.strip():
            continue
        m = json.loads(line)
        if m["a"] == m["b"]:
            continue
        a = m["sideA"] if m["aOnTeam"] == 1 else m["sideB"]
        b = m["sideB"] if m["aOnTeam"] == 1 else m["sideA"]
        for sub, side, won in ((m["a"], a, m["winner"] == "a"), (m["b"], b, m["winner"] == "b")):
            g[sub] += 1
            w[sub] += won
            for n, v in side["units"].items():
                cr[sub][n] += v[1]
                tot[sub] += v[1]
    return {s: 100 * w[s] / g[s] for s in g}, {s: {n: c / tot[s] for n, c in cr[s].items()} for s in cr}, g


def changes(u, how, p):
    f_hp = (1 + p) if how in ("hp", "hp+burst") else math.sqrt(1 + p) if how == "both" else 1.0
    f_dmg = (1 + p) if how == "dmg" else math.sqrt(1 + p) if how == "both" else 1.0
    out = []
    n = u["name"]
    if f_hp != 1.0 and rnd_hp(u["hp"] * f_hp) != u["hp"]:
        out.append(f"{n}|MaxHealth={rnd_hp(u['hp'] * f_hp)}")
    if f_dmg != 1.0:
        for wpn in u["weapons"]:
            if wpn["dmg"] > 0 and rnd_dmg(wpn["dmg"] * f_dmg) != wpn["dmg"]:
                out.append(f"{n}|{wpn['name']}|DamagePerShot={rnd_dmg(wpn['dmg'] * f_dmg):g}")
    if how == "hp+burst" and u.get("burst") and u["burst"]["dmg"] > 0 and rnd_dmg(u["burst"]["dmg"] * math.sqrt(1 + p)) != u["burst"]["dmg"]:
        out.append(f"{n}|DeathBurst|Damage={rnd_dmg(u['burst']['dmg'] * math.sqrt(1 + p)):g}")
    return out


def group_gaps(specs, results):
    """Each unit's ban impact minus its group's middle, from ban_groups' reading of the given ban specs (None without specs)."""
    if not specs:
        return {}
    impact = collections.defaultdict(dict)
    for arg in specs:
        path, _, own = arg.partition("=")
        ref = ban_groups.paired_baseline(own)
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
                key = (m["a"], m["b"], m["i"])
                if m["a"] == m["b"] or sub not in (m["a"], m["b"]) or key not in ref:
                    continue
                n += 1
                d += ((ref[key] == "a") == (m["a"] == sub)) - ((m["winner"] == "a") == (m["a"] == sub))
            if n:
                impact[unit][sub] = 100 * d / n
    mean = {u: sum(v.values()) / len(v) for u, v in impact.items()}
    gaps = {}
    for title, _, names in GROUPS:
        rows = [mean[n] for n in names if n in mean]
        if len(rows) >= 2:
            mid = sum(rows) / len(rows)
            for n in names:
                if n in mean:
                    gaps[n] = mean[n] - mid
    return gaps


def main():
    args = sys.argv[1:]
    opt = lambda name, default: float(args[args.index(name) + 1]) if name in args else default
    slope, damp, cap = opt("--slope", 4.0), opt("--damp", 0.8), opt("--cap", 0.25)
    propose = args[args.index("--propose") + 1] if "--propose" in args else None
    files = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or not args[i - 1].startswith("--"))]
    rates, shares, games = rates_and_shares(files[0])
    gaps = group_gaps(files[1:], files[0])
    units = json.load(open(UNITS, encoding="utf-8"))
    by_name = {u["name"]: u for u in units}
    lines = []
    for sub in sorted(rates, key=lambda s: -rates[s]):
        need = (50 - rates[sub]) * damp / slope / 100  # army power change, as a fraction
        excl = [u["name"] for u in units if u["sub"] == sub and u["cost"] > 0 and shares[sub].get(u["name"], 0) > 0.005]
        # Units on the far side of their group's middle sit this round out, if the rest are at least a tenth of the army.
        wrong = [n for n in excl if n in gaps and (gaps[n] > 3 if need > 0 else gaps[n] < -3)]
        use = [n for n in excl if n not in wrong]
        if sum(shares[sub][n] for n in use) < 0.10:
            use, wrong = excl, []
        total = sum(shares[sub][n] for n in use)
        p = max(-cap, min(cap, need / total)) if total else 0.0
        margin = 196 * math.sqrt(0.25 / games[sub])
        print(f"\n== {sub}: {rates[sub]:.1f}% (±{margin:.1f}); army power {100 * need:+.1f}% over {100 * total:.0f}% of credits -> exclusives {100 * p:+.0f}%")
        for n in excl:
            note = " (left alone: on the far side of its group)" if n in wrong else ""
            gap = f", {gaps[n]:+.1f} from its group's middle" if n in gaps else ""
            print(f"  {n:<24}{100 * shares[sub][n]:>5.1f}% of credits{gap}{note}")
        if abs(rates[sub] - 50) <= max(1.5, margin / 2) or not propose:
            continue
        for n in use:
            how = LEVER_OVERRIDE.get(n, LEVER.get(n, "both"))
            ch = changes(by_name[n], how, p)
            if ch:
                lines.append(f"# {n} ({sub} {rates[sub]:.1f}%): power {100 * p:+.0f}% by {how}")
                lines += ch
    if propose:
        open(propose, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
        print(f"\nWrote {sum(1 for l in lines if not l.startswith('#'))} changes to {propose}")


if __name__ == "__main__":
    main()
