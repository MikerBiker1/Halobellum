"""How much each unit pulls its weight in a tournament's results: python unit_impact.py <results.jsonl> [sub-faction ...]

Ratio = share of its side's damage / share of its side's credits. From results with damage taken (D73 on):
tkn% = share of its side's damage taken, soak = tkn% / cr% (above 1: it draws and absorbs more fire than its price), stop% = share of
the damage aimed at it that armour, shields and defence arcs stopped, died% = share of its bodies dead at a battle's end,
lvl = average level of its squads still owned at the match's end. From results with credited damage (D74 on), dmg% and ratio count
what a unit's drops, spawns and the bodies it infected did as its own, and the units that only come that way are left out. Back-line units take little fire, so a low soak is not toughness. Mirrors are left out. Units that fight through bodies they make
(Seeder, Carrier Form, Infected Phantom) or that block (Jackal with a shield) score low without being weak; a carrier's numbers count what it drops (the Pelican's Marines count under the Pelican).
"""
import collections, json, sys

path = sys.argv[1]
matches = [json.loads(l) for l in open(path, encoding="utf-8") if l.strip()]


def sides(m):
    a = m["sideA"] if m["aOnTeam"] == 1 else m["sideB"]
    b = m["sideB"] if m["aOnTeam"] == 1 else m["sideA"]
    return [(m["a"], a, m["winner"] == "a"), (m["b"], b, m["winner"] == "b")]


per_sub = collections.defaultdict(lambda: {"games": 0, "wins": 0, "dmg": 0.0, "cr": 0.0, "tkn": 0.0, "u": collections.defaultdict(lambda: dict(field=0, wins=0, dmg=0.0, cr=0.0, kills=0, sr=0, tkn=0.0, prev=0.0, bodies=0, lost=0, endsq=0, endlv=0))})
has_taken = False
for m in matches:
    if m["a"] == m["b"]:
        continue
    for sub, side, won in sides(m):
        s = per_sub[sub]
        s["games"] += 1
        s["wins"] += won
        for name, v in side["units"].items():
            sr, cr, dmg, kills, endsq, endlv = v[:6]
            if len(v) >= 11:
                dmg = v[10]
                if cr == 0 and sr == 0 and dmg == 0:
                    continue
            u = s["u"][name]
            u["endsq"] += endsq
            u["endlv"] += endlv
            if len(v) >= 10:
                has_taken = True
                u["tkn"] += v[6]
                u["prev"] += v[7]
                u["bodies"] += v[8]
                u["lost"] += v[9]
                s["tkn"] += v[6]
            u["field"] += 1
            u["wins"] += won
            u["dmg"] += dmg
            u["cr"] += cr
            u["kills"] += kills
            u["sr"] += sr
            s["dmg"] += dmg
            s["cr"] += cr


def rows(sub):
    s = per_sub[sub]
    out = []
    for name, u in s["u"].items():
        without_g = s["games"] - u["field"]
        without_w = s["wins"] - u["wins"]
        edge = 100 * (u["wins"] / u["field"] - without_w / without_g) if without_g >= 20 else None
        out.append(dict(name=name, fielded=100 * u["field"] / s["games"], n=u["field"], win=100 * u["wins"] / u["field"], edge=edge,
                        dshare=100 * u["dmg"] / s["dmg"], cshare=100 * u["cr"] / s["cr"], per100=100 * u["dmg"] / max(1, u["cr"]), kills=u["kills"] / max(1, u["sr"]),
                        tshare=100 * u["tkn"] / max(1, s["tkn"]), stop=100 * u["prev"] / max(1, u["tkn"] + u["prev"]),
                        died=100 * u["lost"] / max(1, u["bodies"]), lvl=u["endlv"] / u["endsq"] if u["endsq"] else None))
    return out


def show(sub):
    r = rows(sub)
    print(f"\n== {sub}: {100*per_sub[sub]['wins']/per_sub[sub]['games']:.1f}% of {per_sub[sub]['games']}")
    extra = f"{'tkn%':>6}{'soak':>6}{'stop%':>6}{'died%':>6}{'lvl':>5}" if has_taken else ""
    print(f"{'unit':<24}{'field':>6}{'n':>5}{'win':>5}{'edge':>6}{'dmg%':>6}{'cr%':>6}{'ratio':>6}{'d/100':>7}{'kills':>6}" + extra)
    for x in sorted(r, key=lambda x: x["dshare"] / max(0.01, x["cshare"])):
        if x["cshare"] == 0 and has_taken:
            continue  # bodies it never bought (infected enemies, free drops): their damage is their maker's
        e = "  -" if x["edge"] is None else f"{x['edge']:+.0f}"
        print(f"{x['name']:<24}{x['fielded']:>5.0f}%{x['n']:>5}{x['win']:>5.0f}{e:>6}{x['dshare']:>6.1f}{x['cshare']:>6.1f}{x['dshare']/max(.01,x['cshare']):>6.2f}{x['per100']:>7.0f}{x['kills']:>6.1f}"
              + (f"{x['tshare']:>6.1f}{x['tshare']/max(.01,x['cshare']):>6.2f}{x['stop']:>6.0f}{x['died']:>6.0f}" + ("    -" if x['lvl'] is None else f"{x['lvl']:>5.1f}") if has_taken else ""))



# Whole game: by damage per 100 credits and by how much of a side's damage a unit deals, among units fielded in at least 100 matches.
agg = collections.defaultdict(lambda: dict(field=0, can=0, wins=0, dmg=0.0, cr=0.0, kills=0, sr=0, dsh=0.0))
for sub, s in per_sub.items():
    for name, u in s["u"].items():
        a = agg[name]
        a["field"] += u["field"]
        a["wins"] += u["wins"]
        a["dmg"] += u["dmg"]
        a["cr"] += u["cr"]
        a["kills"] += u["kills"]
        a["sr"] += u["sr"]
        a["can"] += s["games"]
        a["dsh"] += u["dmg"] / s["dmg"] * s["games"]
print("\n== All units (ratio = share of own side's damage / share of own side's credits)")
tot_d = {sub: s["dmg"] for sub, s in per_sub.items()}
tot_c = {sub: s["cr"] for sub, s in per_sub.items()}
res = []
for name, a in agg.items():
    if a["field"] < 100 or a["cr"] <= 0 or name.startswith("Infected ") and name != "Infected Pelican" and name != "Infected Phantom":
        continue
    d = sum(s["u"][name]["dmg"] / s["dmg"] * s["games"] for sub, s in per_sub.items() if name in s["u"]) / a["can"]
    c = sum(s["u"][name]["cr"] / s["cr"] * s["games"] for sub, s in per_sub.items() if name in s["u"]) / a["can"]
    res.append((name, 100 * a["field"] / a["can"], 100 * a["wins"] / a["field"], 100 * d, 100 * c, d / c, 100 * a["dmg"] / a["cr"], a["kills"] / a["sr"], a["field"]))
print(f"{'unit':<24}{'field':>6}{'win':>5}{'dmg%':>6}{'cr%':>6}{'ratio':>6}{'d/100':>7}{'kills':>6}{'n':>6}")
for x in sorted(res, key=lambda x: -x[5]):
    print(f"{x[0]:<24}{x[1]:>5.0f}%{x[2]:>5.0f}{x[3]:>6.1f}{x[4]:>6.1f}{x[5]:>6.2f}{x[6]:>7.0f}{x[7]:>6.1f}{x[8]:>6}")


for sub in sys.argv[2:]:
    show(sub)
