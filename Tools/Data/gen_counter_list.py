# Rewrites the list in Assets/Tests/EditMode/Unity/CounterWebAssetTests.cs from cw/units.json and cw/matrix.csv: for every unit,
# its best counter by the Bible's Section 7 table, so that Unity's test checks the same pairs the console measured.
# Run it after a full console run: python gen_counter_list.py
import csv, json, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
TEST = os.path.join(HERE, "..", "..", "Assets", "Tests", "EditMode", "Unity", "CounterWebAssetTests.cs")
units = json.load(open(os.path.join(HERE, "cw", "units.json"), encoding="utf-8"))
rows = list(csv.reader(open(os.path.join(HERE, "cw", "matrix.csv"), encoding="utf-8")))
names = rows[0][1:]
wins = {(r[0], names[i]): float(v) for r in rows[1:] for i, v in enumerate(r[1:]) if v != ""}
by = {u["name"]: u for u in units}
COUNTERED_BY = {"Chaff": ["ChaffClear"], "ChaffClear": ["Tank", "Flanker"], "SingleTargetDps": ["Chaff", "Flanker"], "Tank": ["SingleTargetDps"],
                "Flanker": ["ChaffClear", "Tank"], "Artillery": ["Flanker", "Aerial"], "Aerial": ["AntiAir"], "Titan": ["SingleTargetDps", "Chaff"]}


def is_counter(counter, target):
    if target["role"] == "AntiAir":
        return not counter["air"] and counter["role"] != "AntiAir"
    return counter["role"] in COUNTERED_BY.get(target["role"], [])


lines = []
for u in units:
    best = None
    for other in units:
        if other is u or not is_counter(other, u) or (other["name"], u["name"]) not in wins:
            continue
        rate = wins[other["name"], u["name"]]
        if best is None or rate > best[0]:
            best = (rate, other["name"])
    if best is None or best[0] < 0.9:
        raise SystemExit(f"{u['name']} has no counter at 90%: {best}")
    lines.append(f'            ("{u["name"]}", "{best[1]}"),')

text = open(TEST, encoding="utf-8", newline="").read()
newline = "\r\n" if "\r\n" in text else "\n"
text = re.sub(r"(BestCounters =\r?\n\s*\{\r?\n).*?(\r?\n\s*\};)", lambda m: m.group(1) + newline.join(lines) + m.group(2), text, count=1, flags=re.S)
text = re.sub(r"Assert\.AreEqual\(\d+, units\.Count\);", f"Assert.AreEqual({len(units)}, units.Count);", text)
open(TEST, "w", encoding="utf-8", newline="").write(text)
print(f"{len(lines)} units listed")
