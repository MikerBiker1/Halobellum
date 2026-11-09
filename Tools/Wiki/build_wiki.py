# build_wiki.py
# Builds the Halobellum wiki (Wiki/ at the repo root) from the game's own data, so the wiki shows the numbers the game uses.
#   python Tools/Wiki/build_wiki.py
# Reads: the faction, unit, tech, spell and card assets (Assets/_Project/Data), the combat and match rules, the measured
# counter table (Data/AI/MatchupTable.asset), the unit icons and card emblems (Assets/_Project/Resources), and the written
# pages in Tools/Wiki/content/*.md. Writes: Wiki/index.html, Wiki/wiki.css, Wiki/wiki.js (copied from Tools/Wiki/site),
# Wiki/data.js (everything the pages show) and Wiki/img/ (the pictures). Open Wiki/index.html in a browser; it needs no server.
# Run it again after any change to units, techs, spells, cards, rules or the written pages. The wiki is private, like the repo.
# Depends on: unity_yaml.py, mdlite.py (both here), the Python standard library.

import json
import os
import re
import shutil
import sys
from datetime import date

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mdlite  # noqa: E402
import unity_yaml as uy  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
PROJECT = os.path.join(ROOT, "Assets", "_Project")
DATA = os.path.join(PROJECT, "Data")
RES = os.path.join(PROJECT, "Resources")
OUT = os.path.join(ROOT, "Wiki")

ROLES = ["Chaff", "Chaff Clear", "Single Target DPS", "Tank", "Flanker", "Artillery", "Anti-Air", "Aerial", "Titan"]
ARMOR = ["Unarmored", "Light", "Heavy"]
DAMAGE = ["Ballistic", "Explosive", "Energy", "Fire", "Melee"]
TARGETS = ["Ground", "Air", "Ground and air"]
SPELL_EFFECTS = ["Damage", "Buff", "Drop squad", "Disrupt", "Smoke", "Oil"]
CARD_TYPES = ["Spell", "Item", "Economy", "Buff", "Training"]
TARGET_CLASS = ["Any", "Chaff or unarmored", "Tougher targets"]
FACTION_FILES = ["UNSC_Standard", "Covenant_Standard", "Flood_Standard"]


def clean(d):
    return {k: v for k, v in d.items() if not k.startswith("m_")}


def ref(value, guids):
    """The asset path a {fileID, guid} reference points to, or None."""
    if not isinstance(value, dict) or not value.get("guid"):
        return None
    return guids.get(value["guid"])


def stem(path):
    return os.path.splitext(os.path.basename(path))[0]


def compact(name):
    return "".join(c for c in name if c.isalnum())


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")


def r2(x, n=2):
    if isinstance(x, float):
        x = round(x, n)
        return int(x) if x == int(x) else x
    return x


class Builder:
    def __init__(self):
        self.guids = uy.guid_index(PROJECT)
        self.combat = clean(uy.load_asset(os.path.join(DATA, "CombatRules.asset")))
        self.match = clean(uy.load_asset(os.path.join(DATA, "MatchRules.asset")))
        self.units = {}      # id -> unit dict
        self.unit_paths = {}  # path -> id
        self.techs = {}
        self.spells = {}
        self.cards = {}
        self.images = {}     # wiki path -> source path

    # ---------------------------------------------------------------- pictures
    def image(self, source, folder, name):
        if not source or not os.path.exists(source):
            return None
        rel = "img/%s/%s.png" % (folder, slug(name))
        self.images[rel] = source
        return rel

    def unit_icon(self, name):
        return self.image(os.path.join(RES, "UnitIcons", name + ".png"), "units", name)

    def emblem(self, file_stem):
        return self.image(os.path.join(RES, "CardArt", "Emblems", file_stem + ".png"), "emblems", file_stem)

    # ---------------------------------------------------------------- units
    def unit(self, path):
        if path is None:
            return None
        if path in self.unit_paths:
            return self.unit_paths[path]
        d = clean(uy.load_asset(path))
        uid = stem(path)
        if uid in self.units:
            uid = os.path.basename(os.path.dirname(path)) + "-" + uid
        self.unit_paths[path] = uid
        name = d.get("unitName") or uid
        u = {
            "id": uid, "name": name, "folder": os.path.basename(os.path.dirname(path)),
            "role": ROLES[d.get("role", 0)], "roleIndex": d.get("role", 0), "tier": d.get("tier", 0),
            "unlockCost": d.get("unlockCost", 0), "squadSize": d.get("squadSize", 0), "cost": d.get("cost", 0),
            "baseDamage": r2(d.get("baseDamage", 0)), "health": r2(d.get("maxHealth", 0)), "armor": ARMOR[d.get("armorType", 0)],
            "shield": r2(d.get("maxShield", 0)), "shieldRegen": r2(d.get("shieldRegenPerSecond", 0)),
            "speed": r2(d.get("moveSpeed", 0)), "radius": r2(d.get("collisionRadius", 0)), "turn180": r2(d.get("turnTime180", 0)),
            "air": bool(d.get("isAir")), "canFlank": bool(d.get("canFlank")), "targets": TARGETS[d.get("targetFilter", 0)],
            "formsPerBody": max(1, d.get("formsPerBody", 1) or 1), "isFlood": bool(d.get("isFlood")),
            "infects": bool(d.get("infects")), "immuneToInfection": bool(d.get("immuneToInfection")),
            "selfDestructs": bool(d.get("selfDestructs")), "seeksDensest": bool(d.get("seeksDensestCluster")),
            "strongAgainst": d.get("strongAgainst") or "", "weakAgainst": d.get("weakAgainst") or "",
            "icon": self.unit_icon(name),
        }
        self.units[uid] = u
        u["weapons"] = [self.weapon(w) for w in (d.get("weapons") or []) if isinstance(w, dict)]
        if d.get("hasDefenseArc") and isinstance(d.get("defenseArc"), dict):
            a = d["defenseArc"]
            u["defenseArc"] = {"degrees": r2(a.get("arcDegrees", 0)), "front": r2(a.get("frontDamageMultiplier", 1)),
                               "rear": r2(a.get("rearDamageMultiplier", 1)), "blockChance": r2(a.get("blockChance", 0)),
                               "blocks": self.hex_enums(a.get("blockedDamageTypes"), DAMAGE)}
        rein = self.unit(ref(d.get("reinforcementUnit"), self.guids))
        if rein and d.get("reinforcementBodies", 0) > 0:
            u["reinforcement"] = {"unit": rein, "bodies": d["reinforcementBodies"], "delay": r2(d.get("reinforcementDelaySeconds", 0)),
                                  "needsTech": bool(d.get("reinforcementNeedsUpgrade")), "drops": d.get("reinforcementDrops", 1) or 1,
                                  "landSeconds": r2(d.get("reinforcementLandSeconds", 0) or 0),
                                  "firstDrop": r2(d.get("reinforcementFirstDropSeconds", 0) or 0)}
        if d.get("deathBurstDamage", 0) or d.get("deathSpawns"):
            spawns = []
            for s in d.get("deathSpawns") or []:
                sid = self.unit(ref(s.get("unit"), self.guids))
                if sid and s.get("bodies", 0) > 0:
                    spawns.append({"unit": sid, "bodies": s["bodies"]})
            u["deathBurst"] = {"damage": r2(d.get("deathBurstDamage", 0)), "radius": r2(d.get("deathBurstRadius", 0)),
                               "type": DAMAGE[d.get("deathBurstDamageType", 0)], "antiArmor": bool(d.get("deathBurstAntiArmor")),
                               "spawns": spawns}
        sp = self.unit(ref(d.get("spawnedUnit"), self.guids))
        if sp and d.get("spawnedBodies", 0) > 0:
            u["spawns"] = {"unit": sp, "bodies": d["spawnedBodies"], "interval": r2(d.get("spawnIntervalSeconds", 0))}
            alt = self.unit(ref(d.get("alternateSpawnedUnit"), self.guids))
            if alt and d.get("alternateSpawnedBodies", 0) > 0:
                u["spawns"]["alternate"] = {"unit": alt, "bodies": d["alternateSpawnedBodies"]}
        if d.get("auraRadius", 0):
            u["aura"] = {"radius": r2(d["auraRadius"]), "speed": r2(d.get("auraSpeedMultiplier", 1)),
                         "heal": r2(d.get("auraHealPerSecondShare", 0), 4)}
        if d.get("lifestealShare", 0):
            u["lifesteal"] = r2(d["lifestealShare"], 3)
        forms = []
        for f in d.get("seedForms") or []:
            b = self.unit(ref(f.get("becomes"), self.guids))
            if b and f.get("from"):
                forms.append({"from": f["from"], "becomes": b})
        if forms:
            u["seedForms"] = forms
        if d.get("leapRange", 0):
            u["leap"] = {"range": r2(d["leapRange"]), "speed": r2(d.get("leapSpeed", 0)),
                         "cooldown": r2(d.get("leapCooldownSeconds", 0) or 0), "charge": bool(d.get("leapCharges"))}
        u["techs"] = []
        for t in d.get("techUpgrades") or []:
            tp = ref(t, self.guids)
            if tp:
                u["techs"].append(self.tech(tp, uid))
        self.derive(u)
        return uid

    def hex_enums(self, raw, names):
        raw = (raw or "").strip()
        out = []
        for i in range(0, len(raw) - 7, 8):
            value = int.from_bytes(bytes.fromhex(raw[i:i + 8]), "little")
            if 0 <= value < len(names):
                out.append(names[value])
        return out

    def weapon(self, w):
        out = {
            "name": w.get("weaponName") or "Weapon", "damage": r2(w.get("damagePerShot", 0)), "interval": r2(w.get("fireInterval", 0), 3),
            "range": r2(w.get("range", 0)), "minRange": r2(w.get("minRange", 0)), "projectileSpeed": r2(w.get("projectileSpeed", 0)),
            "splash": r2(w.get("splashRadius", 0)), "type": DAMAGE[w.get("damageType", 0)], "antiArmor": bool(w.get("isAntiArmor")),
            "blockable": bool(w.get("canBeBlocked")), "airMultiplier": r2(w.get("airDamageMultiplier", 1)),
            "cone": r2(w.get("coneDegrees", 0)), "lobbed": bool(w.get("lobbed")),
            "targetClass": TARGET_CLASS[w.get("targetClass", 0) or 0],
        }
        if w.get("hasOwnTargetFilter"):
            out["targets"] = TARGETS[w.get("targetFilter", 0)]
        if w.get("beamSeconds", 0):
            out["beam"] = {"seconds": r2(w["beamSeconds"]), "charge": r2(w.get("beamChargeSeconds", 0)),
                           "rest": r2(w.get("beamRestSeconds", 0)), "sweep": r2(w.get("beamSweepSpeed", 0))}
        if w.get("seedSeconds", 0):
            out["seed"] = {"seconds": r2(w["seedSeconds"]), "damageShare": r2(w.get("seedDamageSharePerSecond", 0), 3),
                           "slow": r2(w.get("seedSlowShare", 0))}
        if w.get("hitSlowSeconds", 0):
            out["hitSlow"] = {"seconds": r2(w["hitSlowSeconds"]), "slow": r2(w.get("hitSlowShare", 0))}
        if w.get("windUpSeconds", 0):
            out["windUp"] = r2(w["windUpSeconds"])
        if w.get("staggerSeconds", 0):
            out["stagger"] = r2(w["staggerSeconds"])
        # Damage per second of one body: a beam hits every interval while it fires, then charges and rests.
        dps = out["damage"] / out["interval"] if out["interval"] else 0
        if "beam" in out:
            b = out["beam"]
            cycle = b["seconds"] + b["charge"] + b["rest"]
            dps *= b["seconds"] / cycle if cycle else 1
        out["dps"] = r2(dps)
        return out

    def derive(self, u):
        """Figures a reader can compare: totals per squad and damage against each armour."""
        bodies = u["squadSize"]
        u["squadHealth"] = r2((u["health"] + u["shield"]) * bodies)
        u["bodyDps"] = r2(sum(w["dps"] for w in u["weapons"]))
        u["squadDps"] = r2(u["bodyDps"] * bodies)
        u["footprint"] = r2(u["radius"] * 2 / self.combat["squareSize"], 2)
        rows = {"Ballistic": "ballistic", "Explosive": "explosive", "Energy": "energy", "Fire": "fire", "Melee": "melee"}
        vs = {}
        for armor in ARMOR:
            total = 0
            for w in u["weapons"]:
                row = self.combat["antiArmor"] if w["antiArmor"] else self.combat[rows[w["type"]]]
                total += w["dps"] * row[armor.lower()]
            vs[armor] = r2(total * bodies)
        shield = 0
        for w in u["weapons"]:
            key = "antiArmor" if w["antiArmor"] else rows[w["type"]]
            shield += w["dps"] * self.combat["shieldMultipliers"][key]
        vs["Shields"] = r2(shield * bodies)
        u["dpsVs"] = vs
        u["healthPerCredit"] = r2(u["squadHealth"] / u["cost"], 2) if u["cost"] else 0
        u["dpsPerCredit"] = r2(u["squadDps"] / u["cost"] * 100, 2) if u["cost"] else 0
        u["maxRange"] = max([w["range"] for w in u["weapons"]] or [0])

    # ---------------------------------------------------------------- techs, spells, cards
    def tech(self, path, unit_id):
        tid = stem(path)
        if tid in self.techs:
            self.techs[tid]["units"].append(unit_id)
            return tid
        d = clean(uy.load_asset(path))
        effects = []
        def mult(key, label):
            v = d.get(key, 1)
            if v not in (1, None):
                effects.append({"label": label, "text": "%+d%%" % round((v - 1) * 100), "good": v > 1})
        def bonus(key, label, unit=""):
            v = d.get(key, 0)
            if v:
                effects.append({"label": label, "text": "%+g%s" % (r2(v), unit), "good": v > 0})
        mult("healthMultiplier", "Health")
        mult("damageMultiplier", "Damage")
        mult("fireRateMultiplier", "Fire rate")
        mult("moveSpeedMultiplier", "Speed")
        mult("airDamageMultiplier", "Damage vs air")
        bonus("rangeBonus", "Range", " m")
        bonus("shieldBonus", "Shield")
        bonus("shieldRegenBonus", "Shield regen", "/s")
        bonus("splashRadiusBonus", "Splash", " m")
        bonus("coneDegreesBonus", "Cone", "°")
        if d.get("addsAirTargeting"):
            effects.append({"label": "Targets", "text": "Can shoot air", "good": True})
        if d.get("unlocksReinforcement"):
            effects.append({"label": "Unlocks", "text": "Mid-battle drop", "good": True})
        if d.get("extraShotsPerAttack"):
            effects.append({"label": "Shots", "text": "+%d per attack" % d["extraShotsPerAttack"], "good": True})
        rt = d.get("reloadTimeMultiplier", 1) or 1
        if rt != 1:
            effects.append({"label": "Reload", "text": "%+d%%" % round((rt - 1) * 100), "good": rt < 1})
        self.techs[tid] = {"id": tid, "name": d.get("techName") or tid, "description": d.get("description") or "",
                           "cost": d.get("cost", 0), "effects": effects, "units": [unit_id]}
        return tid

    def spell(self, path, faction):
        sid = faction + "-" + stem(path)
        if sid in self.spells:
            return sid
        d = clean(uy.load_asset(path))
        name = d.get("spellName") or stem(path)
        effect = SPELL_EFFECTS[d.get("effect", 0)]
        s = {"id": sid, "name": name, "faction": faction, "description": d.get("description") or "",
             "offensive": bool(d.get("offensive")), "effect": effect, "cost": d.get("cost", 0),
             "cooldown": d.get("cooldownRounds", 0), "copies": d.get("maxOwnedCopies", 1), "radius": r2(d.get("radius", 0)),
             "delay": r2(d.get("delaySeconds", 0)), "minRound": d.get("minRound", 0),
             "line": {"length": r2(d.get("areaWidth", 0)), "width": r2(d.get("areaDepth", 0))} if d.get("areaWidth", 0) else None,
             "emblem": self.emblem("Spell_" + compact(name)) or self.emblem("Effect_" + SPELL_EFFECTS[d.get("effect", 0)].replace(" ", "")) or self.emblem("Kind_Spell")}
        if d.get("damagePerTick", 0):
            s["damage"] = {"perTick": r2(d["damagePerTick"]), "type": DAMAGE[d.get("damageType", 0)], "antiArmor": bool(d.get("isAntiArmor")),
                           "ticks": d.get("ticks", 1), "interval": r2(d.get("tickInterval", 0)), "targets": TARGETS[d.get("targets", 0)],
                           "infects": bool(d.get("infectsKills")), "strikeRadius": r2(d.get("strikeRadius", 0) or 0),
                           "strikesPerTick": d.get("strikesPerTick", 1) or 1}
        if d.get("shieldBonusShare", 0) or (d.get("damageMultiplier", 1) or 1) != 1:
            s["buff"] = {"shieldShare": r2(d.get("shieldBonusShare", 0)), "damage": r2(d.get("damageMultiplier", 1))}
        drop = self.unit(ref(d.get("dropUnit"), self.guids))
        if drop:
            s["drop"] = {"unit": drop, "squads": d.get("dropSquads", 1)}
            self.units[drop].setdefault("summonedBy", []).append(sid)
        if d.get("shieldDamage", 0) or d.get("techDisableSeconds", 0) or d.get("slowShare", 0):
            s["disrupt"] = {"shieldDamage": r2(d.get("shieldDamage", 0)), "techDisable": r2(d.get("techDisableSeconds", 0)),
                            "slow": r2(d.get("slowShare", 0)), "slowSeconds": r2(d.get("slowSeconds", 0))}
        if effect == "Smoke":
            s["smoke"] = {"duration": r2(d.get("durationSeconds", 0)), "range": r2(d.get("rangeMultiplier", 1))}
        self.spells[sid] = s
        return sid

    def card(self, path):
        cid = stem(path)
        if cid in self.cards:
            return cid
        d = clean(uy.load_asset(path))
        icon = ref(d.get("icon"), self.guids)
        kind = CARD_TYPES[d.get("type", 1)]
        emblem = (self.emblem(stem(icon)) if icon else None) or self.emblem("Kind_" + kind)
        effects = []
        def mult(key, label):
            v = d.get(key, 1)
            if v not in (1, None):
                effects.append({"label": label, "text": "%+d%%" % round((v - 1) * 100), "good": v > 1})
        def bonus(key, label, fmt="%+g"):
            v = d.get(key, 0)
            if v:
                effects.append({"label": label, "text": fmt % r2(v), "good": v > 0})
        if kind == "Item":
            mult("healthMultiplier", "Health")
            mult("damageMultiplier", "Damage")
            mult("moveSpeedMultiplier", "Speed")
            bonus("rangeBonus", "Range", "%+g m")
            if d.get("lifestealShare", 0):
                effects.append({"label": "Lifesteal", "text": "%g%% of damage" % r2(d["lifestealShare"] * 100), "good": True})
            if d.get("healPerSecondShare", 0):
                effects.append({"label": "Healing", "text": "%g%% health/s" % r2(d["healPerSecondShare"] * 100), "good": True})
            if d.get("freeDeployment"):
                effects.append({"label": "Deployment", "text": "Move it every round", "good": True})
        bonus("bonusIncome", "Income", "%+g credits/round")
        mult("allDamageMultiplier", "Army damage")
        mult("allHealthMultiplier", "Army health")
        bonus("allRangeBonus", "Ranged units' range", "%+g m")
        bonus("extraSquadsPerRound", "Squads per round")
        c = {"id": cid, "name": d.get("cardName") or cid, "description": d.get("description") or "", "kind": kind,
             "cost": d.get("cost", 0), "copies": d.get("maxOwnedCopies", 1), "minRound": d.get("minRound", 0),
             "cooldown": d.get("cooldownRounds", 0), "effects": effects, "emblem": emblem, "factions": []}
        self.cards[cid] = c
        return cid

    # ---------------------------------------------------------------- factions
    def factions(self):
        out = []
        for file in FACTION_FILES:
            d = clean(uy.load_asset(os.path.join(DATA, "Factions", file + ".asset")))
            name = d.get("factionName") or file
            col = d.get("color") or {}
            f = {"id": slug(name), "name": name, "color": "#%02x%02x%02x" % tuple(int(col.get(k, 1) * 255) for k in "rgb"),
                 "emblem": self.emblem("Faction_" + name), "base": [], "subFactions": [], "spells": [], "cards": []}
            for r in d.get("baseRoster") or []:
                uid = self.unit(ref(r, self.guids))
                if uid:
                    f["base"].append(uid)
                    self.units[uid].setdefault("factions", []).append(name)
                    self.units[uid].setdefault("homes", []).append({"faction": name, "sub": None})
            for s in d.get("subFactions") or []:
                sub = {"id": slug(name + "-" + s.get("subFactionName", "")), "name": s.get("subFactionName", ""),
                       "description": s.get("description") or "", "units": []}
                for r in s.get("exclusiveUnits") or []:
                    uid = self.unit(ref(r, self.guids))
                    if uid:
                        sub["units"].append(uid)
                        self.units[uid].setdefault("factions", []).append(name)
                        self.units[uid].setdefault("homes", []).append({"faction": name, "sub": sub["name"]})
                f["subFactions"].append(sub)
            for r in d.get("spells") or []:
                p = ref(r, self.guids)
                if p:
                    f["spells"].append(self.spell(p, name))
            for r in d.get("cards") or []:
                p = ref(r, self.guids)
                if p:
                    cid = self.card(p)
                    f["cards"].append(cid)
                    self.cards[cid]["factions"].append(name)
            out.append(f)
        return out

    # ---------------------------------------------------------------- counters
    def counters(self):
        table = clean(uy.load_asset(os.path.join(DATA, "AI", "MatchupTable.asset")))
        by_name = {u["name"]: uid for uid, u in self.units.items()}
        rates = {}
        for e in table.get("entries") or []:
            a, b = by_name.get(e.get("attacker")), by_name.get(e.get("defender"))
            if a and b:
                rates.setdefault(a, {})[b] = r2(float(e.get("winRate", 0)), 3)
        for uid, row in rates.items():
            u = self.units[uid]
            # Against: how often it wins against each enemy (equal credits); best and worst first, rival factions only.
            mine = set(u.get("factions", []))
            rivals = [(v, k) for k, v in row.items() if not mine & set(self.units[k].get("factions", []))]
            pool = rivals or [(v, k) for k, v in row.items()]
            u["goodAgainst"] = [{"unit": k, "rate": v} for v, k in sorted(pool, key=lambda p: (-p[0], self.units[p[1]]["cost"]))[:6] if v > 0.5]
            u["badAgainst"] = [{"unit": k, "rate": v} for v, k in sorted(pool, key=lambda p: (p[0], self.units[p[1]]["cost"]))[:6] if v < 0.5]
            u["winAverage"] = r2(sum(row.values()) / len(row), 3) if row else None
        return {"source": table.get("source") or "", "rates": rates}

    # ---------------------------------------------------------------- content
    def content(self):
        pages = []
        folder = os.path.join(HERE, "content")
        for name in sorted(os.listdir(folder)):
            if not name.endswith(".md"):
                continue
            with open(os.path.join(folder, name), encoding="utf-8") as f:
                text = f.read()
            meta, body = mdlite.front_matter(text)
            html, toc = mdlite.render(body)
            pages.append({"id": meta.get("id") or slug(name[:-3].split("_", 1)[-1]), "title": meta.get("title") or name,
                          "group": meta.get("group") or "Guide", "blurb": meta.get("blurb") or "", "icon": meta.get("icon") or "",
                          "order": int(meta.get("order") or 99), "html": html, "toc": toc})
        pages.sort(key=lambda p: p["order"])
        return pages

    def version(self):
        text = open(os.path.join(ROOT, "ProjectSettings", "ProjectSettings.asset"), encoding="utf-8").read()
        m = re.search(r"bundleVersion: (\S+)", text)
        return m.group(1) if m else "?"

    def build(self):
        factions = self.factions()
        counters = self.counters()
        content = self.content()
        towers = self.towers()
        data = {
            "version": self.version(), "built": date.today().isoformat(),
            "factions": factions, "units": self.units, "techs": self.techs, "spells": self.spells, "cards": self.cards,
            "counters": counters, "combat": self.combat, "match": self.match, "towers": towers, "pages": content,
            "enums": {"roles": ROLES, "armor": ARMOR, "damage": DAMAGE},
        }
        os.makedirs(OUT, exist_ok=True)
        for name in os.listdir(os.path.join(HERE, "site")):
            shutil.copy2(os.path.join(HERE, "site", name), os.path.join(OUT, name))
        img = os.path.join(OUT, "img")
        if os.path.isdir(img):
            shutil.rmtree(img)
        for rel, src in self.images.items():
            dst = os.path.join(OUT, rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(src, dst)
        with open(os.path.join(OUT, "data.js"), "w", encoding="utf-8", newline="\n") as f:
            f.write("// Built by Tools/Wiki/build_wiki.py from the game's data. Do not edit: run the builder again.\n")
            f.write("window.WIKI = ")
            json.dump(data, f, ensure_ascii=False, separators=(",", ":"))
            f.write(";\n")
        print("Wiki built: %d units, %d techs, %d spells, %d cards, %d pages, %d pictures -> %s"
              % (len(self.units), len(self.techs), len(self.spells), len(self.cards), len(content), len(self.images), OUT))

    def towers(self):
        m = self.match
        levels = []
        for i, seconds in enumerate(m.get("towerShockwaveSeconds") or []):
            levels.append({"level": i + 1, "health": r2(m["towerBaseHealth"] * 2 ** i), "shockwave": seconds,
                           "xp": (m.get("towerXpByLevel") or [None] * 9)[i]})
        return {"levels": levels, "radius": m.get("towerRadius"), "squares": m.get("towerSquares"),
                "levelUpCost": m.get("towerLevelUpCost"),
                "shockwave": {"speed": m.get("shockwaveSpeedMultiplier"), "damage": m.get("shockwaveDamageMultiplier"),
                              "fireRate": m.get("shockwaveFireRateMultiplier"), "damageTaken": m.get("shockwaveDamageTakenMultiplier")}}


if __name__ == "__main__":
    Builder().build()
