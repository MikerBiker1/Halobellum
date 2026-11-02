import csv, json, os
CW = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cw")
units = json.load(open(CW + r"\units.json", encoding="utf-8"))
flood = [u["name"] for u in units if u["faction"] == "Flood"]
others = [u["name"] for u in units if u["faction"] != "Flood"]
rows = list(csv.reader(open(CW + r"\matrix.csv", encoding="utf-8")))
names = rows[0][1:]
m = {(r[0], names[i]): (float(r[i + 1]) if r[i + 1] else None) for r in rows[1:] for i in range(len(names))}
print("overall wins (of %d):" % (len(names) - 1))
for n in names:
    w = sum(1 for o in names if o != n and m[n, o] > 0.5)
    vs = sum(1 for o in (others if n in flood else flood) if m[n, o] > 0.5)
    print(f"  {n:24} {w:2}   vs the other side: {vs:2}/{len(others) if n in flood else len(flood)}")
print()
for n in flood:
    print(n, "BEATS:", ", ".join(o for o in names if o != n and m[n, o] > 0.5))
    print("   LOSES TO:", ", ".join(f"{o}" for o in names if o != n and m[n, o] <= 0.5))
print()
print("Infection Forms vs chaff and armor:")
for o in ("Marine", "Grunt", "Jackal (Plasma Rifle)", "Elite", "Flamethrower Trooper", "Fuel Rod Grunt", "Warthog", "Scorpion", "Wraith", "Mantis", "Spartan", "Elephant"):
    print(f"  {o:24} {m['Infection Forms', o]*100:.0f}%")
