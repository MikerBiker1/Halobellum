# Writes the unit tables and win rates in Docs/M10/subfactions.md from cw/units.json and cw/matrix.csv, between the
# "<!-- tables -->" markers, so that the numbers in the document are always the numbers in the game.
# Run it after gen_roster.py and a console run: python gen_subfaction_doc.py
import csv, json, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
DOC = os.path.join(HERE, "..", "..", "Docs", "M10", "subfactions.md")
units = json.load(open(os.path.join(HERE, "cw", "units.json"), encoding="utf-8"))
rows = list(csv.reader(open(os.path.join(HERE, "cw", "matrix.csv"), encoding="utf-8")))
names = rows[0][1:]
wins = {(r[0], names[i]): float(v) for r in rows[1:] for i, v in enumerate(r[1:]) if v != ""}
COUNTERED_BY = {"Chaff": ["ChaffClear"], "ChaffClear": ["Tank", "Flanker"], "SingleTargetDps": ["Chaff", "Flanker"], "Tank": ["SingleTargetDps"],
                "Flanker": ["ChaffClear", "Tank"], "Artillery": ["Flanker", "Aerial"], "Aerial": ["AntiAir"], "Titan": ["SingleTargetDps", "Chaff"]}
ROLE = {"ChaffClear": "Chaff clear", "SingleTargetDps": "Single-target DPS", "AntiAir": "Anti-air"}
ARMOR = {"U": "Unarmored", "L": "Light", "H": "Heavy"}


def is_counter(counter, target):
    if target["role"] == "AntiAir":
        return not counter["air"] and counter["role"] != "AntiAir"
    return counter["role"] in COUNTERED_BY.get(target["role"], [])


def weapon_text(w):
    bits = [f"{w['dmg']:g} per {w['interval']:g} s", w["type"]]
    if w["aa"]:
        bits.append("anti-armor")
    bits.append(f"{w['range']:g} m")
    if w["splash"]:
        bits.append(f"splash {w['splash']:g} m")
    if w["cone"]:
        bits.append(f"cone {w['cone']:g} deg")
    if w["lob"]:
        bits.append("lobbed")
    if w["beam"]:
        bits.append("beam")
    if w["air"] != 1:
        bits.append(f"x{w['air']:g} vs air")
    if w["slow"]:
        bits.append(f"slows {w['slowshare'] * 100:g}% for {w['slow']:g} s")
    return f"{w['name']} ({', '.join(bits)})"


def table(sub):
    out = ["| Unit | Role | Tier | Unlock | Squad | Cost | Health | Armor | Shield | Speed | Weapons | Wins, average | Best counters |",
           "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    for u in [x for x in units if x["sub"] == sub]:
        n = u["name"]
        rates = [wins[n, o] for o in names if (n, o) in wins]
        counters = sorted(((wins[o["name"], n], o["name"]) for o in units if (o["name"], n) in wins and is_counter(o, u)), key=lambda c: -c[0])[:2]
        extra = []
        if u["forms"] > 1:
            extra.append(f"{u['forms']} forms each")
        weapons = "; ".join(weapon_text(w) for w in u["weapons"])
        out.append(f"| {n} | {ROLE.get(u['role'], u['role'])} | {u['tier']} | {u['unlock']} | {u['squad']} | {u['cost']} | {u['hp']:g} | {ARMOR[u['armor']]} | "
                   f"{u['shield']:g} | {u['speed']:g} | {weapons} | {sum(v > 0.5 for v in rates)} of {len(rates)}, {sum(rates) / len(rates):.0%} | "
                   + ", ".join(f"{c[1]} {c[0]:.0%}" for c in counters) + " |")
    return "\n".join(out)


parts = []
for sub in dict.fromkeys(u["sub"] for u in units if u["sub"]):
    parts.append(f"### {sub}\n\n{table(sub)}\n\n" + "\n".join(f"- **{u['name']}.** {u['note']}" for u in units if u["sub"] == sub) + "\n")
text = open(DOC, encoding="utf-8").read()
text = re.sub(r"(<!-- tables -->\n).*(\n<!-- end tables -->)", lambda m: m.group(1) + "\n".join(parts) + m.group(2), text, flags=re.S)
open(DOC, "w", encoding="utf-8", newline="\n").write(text)
print("written")
