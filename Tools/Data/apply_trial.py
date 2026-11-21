"""Writes a tournament trial file into the roster sources (gen_roster.py, sub_*.py, techs.py), so a trial that won goes into the game (D138): python apply_trial.py trial.txt, then python gen_roster.py"""
import glob, re, sys

DATA = __import__("os").path.dirname(__import__("os").path.abspath(__file__)) + "/"
files = {p: open(p, encoding="utf-8", newline="").read() for p in [DATA + "gen_roster.py"] + sorted(glob.glob(DATA + "sub_*.py"))}
techs_path = DATA + "techs.py"
techs = open(techs_path, encoding="utf-8", newline="").read()
done, skipped = [], []


def block(name):
    """(path, start, end) of the unit's U(...) call: from its U( to the next unit's U( (or the list's end)."""
    for path, text in files.items():
        m = re.search(r'U\("[^"]+", "' + re.escape(name) + r'", ', text)
        if not m:
            continue
        nxt = re.search(r'\n\s*U\("', text[m.end():])
        end = m.end() + nxt.start() if nxt else len(text)
        return path, m.start(), end
    raise SystemExit("No unit " + name)


def sub_in_block(name, pattern, value, what):
    path, start, end = block(name)
    text = files[path]
    part = text[start:end]
    new, n = re.subn(pattern, lambda m: m.group(1) + value + m.group(len(m.groups())) if len(m.groups()) > 1 else value, part, count=1)
    if n != 1:
        raise SystemExit(f"{name}: {what} not found")
    files[path] = text[:start] + new + text[end:]


def num(v):
    f = float(v)
    return str(int(f)) if f == int(f) else str(f)


for raw in open(sys.argv[1], encoding="utf-8"):
    line = raw.split("#")[0].strip()
    if not line:
        continue
    parts = line.split("|")
    field, value = parts[-1].split("=")
    unit = parts[0]
    if field == "GoesForTowers":
        skipped.append(line)  # every Flanker, in gen_roster.py
        continue
    if len(parts) == 2:
        if field == "MaxHealth":
            sub_in_block(unit, r'(U\("[^"]+", "[^"]+", [^,]+, "[^"]+", \d+, \d+, \d+, \d+, )[\d.]+(,)', num(value), field)
        elif field == "UnlockCost":
            sub_in_block(unit, r'(U\("[^"]+", "[^"]+", [^,]+, "[^"]+", \d+, )\d+(,)', num(value), field)
        elif field == "Cost":
            sub_in_block(unit, r'(U\("[^"]+", "[^"]+", [^,]+, "[^"]+", \d+, \d+, \d+, )\d+(,)', num(value), field)
        elif field == "LeapRange":
            sub_in_block(unit, r'(leap=\()[\d.]+(,)', num(value), field)
        else:
            raise SystemExit("Unhandled: " + line)
    elif parts[1] == "DeathBurst":
        assert field == "Damage"
        sub_in_block(unit, r'(burst=dict\(dmg=)[\d.]+(,)', num(value), line)
    elif parts[1].startswith("tech:"):
        key = {"FireRateMultiplier": "fire", "DamageMultiplier": "damage", "HealthMultiplier": "health"}[field]
        name = parts[1][5:]
        pattern = r'(T\("[^"]+", "' + re.escape(name) + r'", [^\n]*?' + key + r'=)[\d.]+'
        techs, n = re.subn(pattern, lambda m: m.group(1) + num(value), techs, count=1)
        if n != 1:
            raise SystemExit("No tech " + line)
    else:
        weapon = parts[1]
        index = {"DamagePerShot": 1, "FireInterval": 2, "Range": 3}[field]
        if index == 1:
            sub_in_block(unit, r'(W\("' + re.escape(weapon) + r'", )[\d.]+(,)', num(value), line)
        elif index == 2:
            sub_in_block(unit, r'(W\("' + re.escape(weapon) + r'", [\d.]+, )[\d.]+(,)', num(value), line)
        else:  # a weapon's reach (D141)
            sub_in_block(unit, r'(W\("' + re.escape(weapon) + r'", [\d.]+, [\d.]+, )[\d.]+(,)', num(value), line)
    done.append(line)

for path, text in files.items():
    open(path, "w", encoding="utf-8", newline="").write(text)
open(techs_path, "w", encoding="utf-8", newline="").write(techs)
print(f"applied {len(done)} lines; {len(skipped)} go-for-towers lines left to the generator's Flanker rule")
