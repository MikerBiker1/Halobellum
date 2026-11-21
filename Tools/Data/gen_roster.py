# Generates the M4 slice roster: unit assets (Data/Units/UNSC, Data/Units/Covenant), faction rosters,
# balance.csv and a JSON copy for the tuning console. Section 8 numbers (role, tier, unlock, squad,
# cost, health, armor) are the Bible's; everything else is a first-pass value (DECISIONS D21).
# The 8 sub-factions' exclusive units (M10) come from the sub_*.py modules, one each: SUBFACTIONS below says which are in.
# Usage: python gen_roster.py
import hashlib, importlib, json, os, re, struct
from techs import TECHS, THIRD_TECHS, GAMEPLAY_TECHS, T

# Which sub-factions are in the game, in draft-list order. A faction with none here keeps a plain "Standard" card.
SUBFACTIONS = ["sub_noble", "sub_armored", "sub_aerospace", "sub_banished", "sub_lekgolo", "sub_unggoy", "sub_flood_unsc", "sub_flood_covenant"]
SUBFACTIONS = SUBFACTIONS[:int(os.environ.get("HB_SUBFACTION_COUNT", len(SUBFACTIONS)))]  # Only to bring them in one by one.
SUBFACTIONS = [importlib.import_module(m) for m in SUBFACTIONS]

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SCRATCH = os.path.dirname(os.path.abspath(__file__))
UNIT_SCRIPT_GUID = "48a24a6f0743ea50b653bb6800d1d896"
TECH_SCRIPT_GUID = "c01f1ec4770119518fc3088b4a52ceeb"

ROLE = dict(Chaff=0, ChaffClear=1, SingleTargetDps=2, Tank=3, Flanker=4, Artillery=5, AntiAir=6, Aerial=7, Titan=8)
ARMOR = dict(U=0, L=1, H=2)
DTYPE = dict(Ballistic=0, Explosive=1, Energy=2, Fire=3, Melee=4)
FILTER = dict(G=0, A=1, B=2)

ROLE_TEXT = {
    "Chaff": ("Single-target DPS", "Chaff clear"),
    "ChaffClear": ("Chaff", "Tanks, flankers"),
    "SingleTargetDps": ("Tanks, heavy units", "Chaff swarms, flankers"),
    "Tank": ("Chaff clear", "Single-target DPS"),
    "Flanker": ("Artillery, DPS", "Chaff clear, tanks"),
    "Artillery": ("Slow, grouped units", "Flankers, air"),
    "AntiAir": ("Aerial", "Ground units"),
    "Aerial": ("Ground armies without anti-air", "Anti-air"),
    "Titan": ("Most things in a straight fight", "Single-target DPS, large chaff swarms"),
}


def W(name, dmg, interval, rng, type, proj=0, splash=0, minr=0, aa=False, block=False, own=None, air=1, cone=0,
      lob=False, beam=0, charge=0, rest=0, sweep=0, seed=0, seedshare=0, seedslow=0, slow=0, slowshare=0, windup=0, stagger=0, sbreak=None,
      pierce=False, mag=None, ram=0):
    return dict(name=name, dmg=dmg, interval=interval, range=rng, minr=minr, proj=proj, splash=splash, type=type,
                aa=aa, block=block, own=own, air=air, cone=cone, lob=lob, beam=beam, charge=charge, rest=rest, sweep=sweep,
                seed=seed, seedshare=seedshare, seedslow=seedslow, slow=slow, slowshare=slowshare, windup=windup, stagger=stagger, sbreak=sbreak,
                pierce=pierce, mag=mag, ram=ram)


# Seconds every Spartan Laser charges before its shot goes (the owner, 2026-11-05: the laser fires 2.5 s into its sound).
SPARTAN_LASER_WINDUP = 2.5


# The air units that can deploy in the flank zones (D61): UNSC only. With the Hornet too, Noble and Armored Division reached 70%.
# Every Flanker goes for towers (UnitStats.GoesForTowers): it heads for the nearest enemy tower, shooting what it passes (the owner, D138);
# U(towers=False) keeps one fighting instead: the Spartan CQB (the owner, D147: "CQB should definitely not just run to the towers though, they should fight").
FLANKERS = {"Pelican", "Sparrowhawk", "Longsword", "Vulture", "CondorGunship"}


def U(id, name, faction, role, tier, unlock, squad, cost, hp, armor, speed, radius, turn, filter, weapons,
      shield=0, regen=0, airu=False, arc=None, drop=None, note="", forms=1, flood=False, infects=False, immune=False,
      selfd=False, seek=False, burst=None, spawn=None, aura=None, leap=None, lifesteal=0, basedmg=None, tint=None, scale=1, sub=None, folder=None, baked=None, seedforms=None, spawn2=None,
      charges=False, phase=False, towers=None, gap=0):
    return dict(id=id, name=name, faction=faction, role=role, tier=tier, unlock=unlock, squad=squad, cost=cost,
                hp=hp, armor=armor, speed=speed, radius=radius, turn=turn, filter=filter, weapons=weapons,
                shield=shield, regen=regen, air=airu, arc=arc, drop=drop, note=note, forms=forms, flood=flood, infects=infects,
                immune=immune, selfd=selfd, seek=seek, burst=burst, spawn=spawn, aura=aura, leap=leap, lifesteal=lifesteal, seedforms=seedforms, spawn2=spawn2, charges=charges, phase=phase,
                towers=role == "Flanker" if towers is None else towers, gap=gap,
                basedmg=basedmg, tint=tint, scale=scale, sub=sub, folder=folder or faction, baked=baked)


# The unit balance pass of D138 (the owner, 2026-11-12) changed some 45 units here and in the sub_*.py modules and techs.py; DECISIONS.md D138 lists them.
UNITS = [
    # ------------------------------------------------------------------ UNSC (Section 8.1)
    U("Marine", "Marine", "UNSC", "Chaff", 1, 0, 18, 100, 50, "U", 3, 0.5, 0.25, "B",
      [W("MA5 Assault Rifle", 4, 0.5, 12, "Ballistic", block=True)]),
    U("FlamethrowerTrooper", "Flamethrower Trooper", "UNSC", "ChaffClear", 1, 0, 6, 150, 95, "U", 3.5, 0.5, 0.25, "G",
      [W("M7057 Flamethrower", 3.5, 0.1, 11, "Fire", cone=15)]),
    U("Warthog", "Warthog", "UNSC", "Flanker", 1, 0, 2, 200, 380, "L", 8, 1.2, 1, "B",
      [W("M41 Machine Gun", 8, 0.2, 11, "Ballistic", block=True)]),
    U("GaussWarthog", "Gauss Warthog", "UNSC", "SingleTargetDps", 2, 50, 1, 250, 300, "L", 7.5, 1.2, 1, "G",
      [W("M68 Gauss Cannon", 220, 2.5, 22, "Ballistic", proj=150, aa=True)]),
    U("Scorpion", "Scorpion", "UNSC", "Tank", 2, 50, 1, 300, 1200, "H", 2.5, 2, 2, "G",
      [W("M512 Cannon", 100, 2.5, 16, "Explosive", proj=35, splash=2.5),
       W("Coaxial Machine Gun", 6, 0.25, 12, "Ballistic", block=True, own="B")]),
    U("Wolverine", "Wolverine", "UNSC", "AntiAir", 2, 50, 1, 300, 480, "L", 4, 1.4, 1.5, "B",
      [W("Rocket Volley", 29, 1.0, 24, "Explosive", proj=45, splash=1.5, air=3)]),
    U("Hornet", "Hornet", "UNSC", "Aerial", 2, 50, 2, 300, 505, "L", 9, 1.5, 1, "B",
      # Its bombs gone, its rockets a 1 m splash (the owner, D155; damage to be tuned in the next tournament phase).
      [W("Rockets", 24.5, 1.0, 12, "Explosive", proj=40, splash=1)], airu=True),
    U("ArcherMissilePod", "Archer Missile Pod", "UNSC", "Artillery", 2, 50, 2, 300, 350, "L", 0, 1.4, 1.5, "G",
      [W("Archer Missile Barrage", 58, 4.0, 42, "Explosive", proj=28, splash=3.5, minr=8, lob=True)]),
    # Autocannon 10 -> 25 with a 1 m splash (the owner, D142: "at least 2.5x the damage it's doing right now and 1m splash").
    # It never goes behind enemy lines (the owner, D143: "more damaging than good"), but still comes down to let its Marines out ("this is no gravity field"):
    # it lands where it is, 1.5 s down and 1.5 s up, with the squads out at 5, 15 and 25 s.
    # Health 1550 -> 1400 (the owner, D155).
    U("Pelican", "Pelican", "UNSC", "Aerial", 2, 50, 1, 300, 1400, "L", 12, 2.5, 2, "B",
      [W("M370 Autocannon", 25, 0.3, 14, "Ballistic", splash=1)], airu=True, drop=("Marine", 6, 10, True, 3, 1.5, 5),
      note="With its Troop Bay tech it lands where it is and drops 6 Marines 5 seconds into the battle, then every 10 seconds, up to 3 drops in all; it never flies behind enemy lines (D143)."),
    U("Spartan", "Spartan", "UNSC", "SingleTargetDps", 3, 250, 1, 500, 1250, "U", 3.5, 0.6, 0.5, "B",
      [W("Spartan Laser", 450, 4.2, 24, "Energy", aa=True)], shield=400, regen=80, immune=True, baked="Spartan",
      note="Its laser winds up for 2.5 s before each shot; it fires every 3.5 s (was 4) to make up for the first shot coming later."),
    U("Mantis", "Mantis", "UNSC", "Tank", 3, 250, 1, 500, 3360, "H", 3, 2, 2, "B",
      [W("Chaingun", 9, 0.2, 14, "Ballistic", block=True),
       W("Rockets", 60, 2.5, 18, "Explosive", proj=35, splash=2, own="G")]),
    U("Elephant", "Elephant", "UNSC", "Titan", 4, 150, 1, 800, 8240, "H", 1.5, 5, 3, "B",
      [W("Main Cannon", 310, 3.0, 20, "Explosive", proj=40, splash=2, aa=True, own="G"),
       W("Turret (left)", 8.5, 0.2, 14, "Ballistic", block=True),
       W("Turret (right)", 8.5, 0.2, 14, "Ballistic", block=True)]),

    # ------------------------------------------------------------------ Covenant (Section 8.2)
    U("Grunt", "Grunt", "Covenant", "Chaff", 1, 0, 20, 100, 40, "U", 3, 0.45, 0.25, "B",
      [W("Plasma Pistol", 5, 0.5, 10, "Energy")]),  # 4.5 -> 5 (the owner, D155); its shots green, as every plasma pistol's.
    U("FuelRodGrunt", "Fuel Rod Grunt", "Covenant", "ChaffClear", 1, 0, 4, 150, 80, "U", 3, 0.5, 0.25, "G",
      [W("Fuel Rod Gun", 24.5, 2.0, 14, "Explosive", proj=25, splash=3)]),
    # A sniper since 2026-11-05 (the owner): fewer to a squad, far more range, slow hard shots that land at once. Was Jackal (Plasma Rifle): 8 per squad, 8 damage every 0.3 s, 12 m.
    U("JackalBeamRifle", "Jackal (Beam Rifle)", "Covenant", "SingleTargetDps", 1, 0, 4, 175, 70, "U", 3.2, 0.5, 0.25, "B",
      # A shot that kills goes on through to the next enemy on its line, within its range (the owner, D155).
      [W("Beam Rifle", 40, 3.0, 24, "Energy", pierce=True)], baked="Jackal",
      note="Snipers: few to a squad, they pick targets off from far away with slow, hard shots, and have little to stop a rush."),
    # The owner, D151: green shots, and at a target with shield it charges its pistol 1 s and fires a big, slow bolt (12 m/s) that strips 100 shield.
    U("JackalShield", "Jackal (Shield)", "Covenant", "Tank", 1, 0, 6, 150, 80, "U", 3, 0.6, 0.25, "B",
      [W("Plasma Pistol", 6, 0.6, 10, "Energy", sbreak=(100, 1, 12))],
      arc=dict(deg=120, front=0.5, rear=1.0, block=0.8, types=["Ballistic"]), baked="JackalShield"),
    U("Ghost", "Ghost", "Covenant", "Flanker", 1, 0, 2, 200, 250, "L", 9, 1.2, 0.8, "B",
      [W("Plasma Bolts", 9, 0.25, 10, "Energy")]),
    # The owner, D151: the three Elites stand 1.5 m apart so a blast catches fewer of them (gap=), shield 200 -> 250, range 12 -> 14.
    U("Elite", "Elite", "Covenant", "SingleTargetDps", 2, 50, 3, 300, 230, "U", 3.5, 0.6, 0.4, "B",
      [W("Plasma Rifle", 26, 0.5, 14, "Energy")], shield=300, regen=50, baked="Elite", gap=1.5),  # Shield 250 -> 300 (the owner, D155).
    U("Wraith", "Wraith", "Covenant", "Artillery", 2, 50, 1, 300, 900, "H", 2, 2, 2, "G",
      [W("Plasma Mortar", 98, 3.5, 38, "Explosive", proj=20, splash=4, minr=10, lob=True)]),
    # Health 570 -> 700: it lost to the Pelican at the same price (the owner, D149). Its needle cannons do 16 explosive damage (13 energy before, D150),
    # three times that against air, with a 0.5 m splash: the needles explode (the owner, D150). D149's x6 and then x5 against air are gone.
    # Health 700 -> 800 (the owner, D155).
    U("Vampire", "Vampire", "Covenant", "AntiAir", 2, 50, 1, 300, 800, "L", 7, 1.8, 1, "B",
      [W("Needle Cannons", 16, 0.4, 18, "Explosive", splash=0.5, air=3)], airu=True),
    U("Banshee", "Banshee", "Covenant", "Aerial", 2, 50, 2, 300, 440, "L", 10, 1.4, 0.8, "B",
      [W("Plasma Cannons", 10.25, 0.3, 10, "Energy"),
       W("Fuel Rod Bomb", 57, 2.5, 8, "Explosive", proj=30, splash=3, own="G")], airu=True),
    # The Covenant's dropship (the owner, 2026-11-05): the Pelican's mirror, with Grunts in its bay. Its weapon's name is what its sound goes by.
    # It drops from the air like the Infected Phantom and never lands (the owner, D142): land 1.5 -> 0, still 3 drops at 5, 15 and 25 s.
    # As the Pelican, but energy (the owner, D151): health 800 -> 1550, its turrets 12 every 0.4 s -> 16 every 0.3 s with a 1 m splash. The Pelican's
    # 25 in energy beat 60 of 68 units and every Pelican (energy does more to armor than ballistic); at 16 it beats 52 (the Pelican 53) and edges the Pelican.
    # The owner then lowered it by 1 (D152): 16 -> 15.
    U("CovenantPhantom", "Covenant Phantom", "Covenant", "Aerial", 2, 50, 1, 300, 1550, "L", 10, 3, 2, "B",
      [W("Plasma Turrets", 15, 0.3, 14, "Energy", splash=1)], airu=True, drop=("Grunt", 7, 10, True, 3, 0, 5),
      note="With its Gravity Lift tech it drops 7 Grunts from the air 5 seconds into the battle, then every 10 seconds, up to 3 drops in all, wherever it is; it never lands (D142)."),
    # The owner, D87: the cannon holds fire at what comes within 5 m; the Hunter charges at it along the ground and bashes it with its shield.
    # The cannon reaches aircraft too, the bash stays on the ground (the owner, D148: "make hunters and the captain be able to target air units").
    U("HunterPair", "Hunter Pair", "Covenant", "Tank", 3, 225, 2, 450, 1440, "H", 2.2, 1.2, 1.5, "B",
      [W("Assault Cannon", 57, 2.0, 16, "Explosive", proj=30, splash=2.5, minr=5),
       W("Shield Bash", 100, 1.5, 2, "Melee", splash=1.5, own="G")],
      # No weak back any more (the owner, D155): rear x1.5 -> x1.
      arc=dict(deg=240, front=0.7, rear=1.0, block=0, types=[]), leap=(5, 9, 3), charges=True, baked="Hunter"),
    U("Zealot", "Zealot", "Covenant", "SingleTargetDps", 3, 200, 1, 400, 1400, "U", 6, 0.6, 0.3, "G",
      [W("Energy Sword", 180, 0.75, 1.5, "Melee", aa=True, splash=2)], shield=600, regen=120, leap=(6, 14, 2.5), baked="Zealot",
      note="Leaps onto its enemy, and its energy sword cuts everything around its target."),
    U("Scarab", "Scarab", "Covenant", "Titan", 4, 150, 1, 800, 9050, "H", 1.5, 5, 3, "B",
      [W("Focus Beam", 32, 0.1, 24, "Energy", splash=2, aa=True, own="G", beam=3, charge=2, rest=3, sweep=2),
       W("Plasma Turret (left)", 11, 0.25, 16, "Energy"),
       W("Plasma Turret (right)", 11, 0.25, 16, "Energy")]),
    # ------------------------------------------------------------------ Flood (M8, decision D38). Speeds are final, not halved.
    # One Infection Forms body is a pack of 5 forms: health and damage below are for the whole pack.
    U("InfectionForms", "Infection Forms", "Flood", "Chaff", 1, 0, 10, 100, 290, "U", 5, 0.8, 0.2, "G",
      [W("Infectious Bite", 37, 0.5, 1, "Melee")], forms=6, flood=True, infects=True, leap=(6, 14, 2.5)),
    U("CarrierForm", "Carrier Form", "Flood", "ChaffClear", 1, 0, 4, 150, 255, "U", 2, 0.8, 0.4, "G",
      [W("Burst", 0, 1, 1.5, "Explosive")], flood=True, selfd=True, phase=True,  # D146: walks through its own Infection Forms.
      burst=dict(dmg=158, radius=4.5, type="Explosive", aa=False, spawns=[("InfectionForms", 2)])),
    # D108 (the owner, 2026-11-07): it leaps, as the Infested Elite on its model does.
    # Claws only, and faster (the owner, D153): its Scavenged Rifle (6.5 every 0.4 s, 10 m) gone, speed 2.4 -> 3.
    U("CombatForm", "Combat Form", "Flood", "SingleTargetDps", 2, 50, 6, 200, 280, "U", 3, 0.6, 0.3, "G",
      [W("Claw Swipe", 60, 0.8, 1.5, "Melee", aa=True)], flood=True, leap=(10, 12, 4)),
    U("StalkerForm", "Stalker Form", "Flood", "Flanker", 2, 50, 3, 250, 450, "L", 5, 0.9, 0.3, "G",
      [W("Talons", 28, 0.4, 1.5, "Melee")], flood=True),
    # D107 (the owner, 2026-11-07): it shoots air too; at a flyer its shards fly straight, not lobbed, for a quarter of their damage:
    # at full damage every aircraft lost to it, and air is artillery's counter (Section 7).
    U("PureFormRanged", "Pure Form Ranged", "Flood", "Artillery", 2, 50, 2, 250, 400, "L", 1, 1.1, 1, "B",
      [W("Bone Shards", 45, 3, 34, "Explosive", proj=22, splash=3, minr=8, lob=True, air=0.25)], flood=True),
    # D76 (the owner, 2026-11-06): its spat bone hits air only, from 14 m; on the ground it must close in and bite. Its weapons split air from
    # ground, so it uses one at a time, and the long one's reach means air comes first.
    U("SwarmForm", "Swarm Form", "Flood", "AntiAir", 2, 50, 4, 250, 275, "U", 3.5, 1, 0.3, "B",
      [W("Spat Bone", 13, 0.4, 14, "Ballistic", air=3, own="A"), W("Bone Barbs", 12.5, 0.4, 1.5, "Melee", own="G")], airu=True, forms=5, flood=True),
    U("PureFormTank", "Pure Form Tank", "Flood", "Tank", 3, 200, 1, 400, 3400, "H", 1.6, 2, 1.5, "G",
      [W("Slam", 200, 2, 3, "Melee", splash=4)], flood=True),
    # D76 (the owner, 2026-11-06): it shoots air too, and a seed only raises what a Flood roster has its own version of, as that version.
    U("Seeder", "Seeder", "Flood", "SingleTargetDps", 3, 200, 2, 475, 900, "L", 1.5, 1.6, 1, "B",
      # Its rot eats any unit a seed takes, not only hosts and aircraft, and the spore has a 0.5 m splash that seeds too (the owner, D155);
      # then the rot 4% -> 2.5% of the host's health a second, as Flood reached 64% (the owner, D156).
      [W("Seed Spore", 43, 2.5, 24, "Energy", proj=30, splash=0.5, seed=8, seedshare=0.025, seedslow=0.4)], airu=True, flood=True,
      seedforms=[("Marine", "InfestedMarine"), ("Warthog", "InfestedWarthog"), ("Scorpion", "InfestedScorpion"), ("Hornet", "InfestedHornet"),
                 ("Elite", "InfestedElite"), ("Ghost", "InfestedGhost"), ("Banshee", "InfestedBanshee"),
                 ("Wraith", "InfestedWraith"), ("Pelican", "InfectedPelican"), ("Covenant Phantom", "InfectedPhantom")]),
    U("InfectedPelican", "Infected Pelican", "Flood", "Aerial", 3, 200, 1, 400, 1500, "L", 6, 2.5, 1, "G",
      [W("Crash Dive", 0, 1, 1, "Explosive", own="G")], airu=True, flood=True, selfd=True, seek=True, leap=(22, 16, 0),
      # Health 900 -> 1350 so it reaches the enemy before it is shot down, and its blast 900 -> 1200 (the owner, after playing it, D134).
      burst=dict(dmg=1200, radius=3.5, type="Explosive", aa=False, spawns=[("InfectionForms", 5), ("CombatForm", 5)])),
    U("InfectedPhantom", "Infected Phantom", "Flood", "Aerial", 3, 200, 1, 400, 1400, "L", 2.5, 3, 1.5, "B",
      [W("Plasma Turrets", 12.5, 0.3, 14, "Energy")], airu=True, flood=True, drop=("CombatForm", 5, 8),
      burst=dict(dmg=0, radius=0, type="Explosive", aa=False, spawns=[("InfectionForms", 1)])),
    U("Abomination", "Flood Abomination", "Flood", "Titan", 4, 150, 1, 800, 10570, "H", 1.25, 5, 3, "B",
      [W("Tendril Slam", 381, 2.5, 7, "Melee", splash=5, own="G"),
       W("Tendril Whip", 53, 0.8, 10, "Melee")], flood=True, spawn=("InfectionForms", 2, 14), aura=(14, 1.2, 0.01), baked="Abomination"),
]

# Base Flood units that belong to one sub-faction only (the owner, D157): the Infected Pelican to UNSC-based, the Infected Phantom to
# Covenant-based. Their asset files stay in Data/Units/Flood.
FLOOD_EXCLUSIVES = {"InfectedPelican": "UNSC-based", "InfectedPhantom": "Covenant-based"}
for _unit in UNITS:
    if _unit["id"] in FLOOD_EXCLUSIVES:
        _unit["sub"] = FLOOD_EXCLUSIVES[_unit["id"]]

# The sub-factions' exclusive units and their techs. Their speeds are final, not halved (see D36 below).
for _module in SUBFACTIONS:
    for _unit in _module.units(U, W):
        _unit.update(sub=_module.NAME, folder=_module.FOLDER)
        UNITS.append(_unit)
    TECHS.update(_module.TECHS)
for _id, _tech in THIRD_TECHS.items():
    TECHS[_id].append(_tech)

# The range tech (the owner, 2026-11-08; D114): a flat 8 m for 300 credits, as Mechabellum does it, so it means most to short guns. A unit that had a
# range tech keeps its name for it; every other unit whose main weapon is a gun gets "Long Range" as a fourth tech. Not for melee units, bombers,
# the flame and shotgun units, or a unit whose fourth tech is one that changes how it plays (GAMEPLAY_TECHS).
RANGE_TECH_METRES, RANGE_TECH_COST = 8, 300
NO_RANGE_TECH = {"Longsword", "SpartanCQB", "FlamethrowerTrooper"}  # The Swarm Form has one: its spat bone at air (the owner, 2026-11-08; D115).
for _unit in UNITS:
    _techs = TECHS.get(_unit["id"])
    if _techs is None:
        continue
    _ranged = [t for t in _techs if t["fx"].get("range")]
    for _tech in _ranged:
        _tech["fx"]["range"] = RANGE_TECH_METRES
        _tech["cost"] = RANGE_TECH_COST
    _main = _unit["weapons"][0] if _unit["weapons"] else None
    if (not _ranged and _main and _main["type"] != "Melee" and _main["range"] > 3 and _unit["id"] not in NO_RANGE_TECH
            and _unit["id"] not in GAMEPLAY_TECHS):
        _techs.append(T("LongRange", "Long Range", RANGE_TECH_COST, "Every gun reaches 8 m further.", range=RANGE_TECH_METRES))
for _id, _tech in GAMEPLAY_TECHS.items():
    TECHS[_id].append(_tech)

# Best counters from the latest counter-web run (filled in by hand after each run; Bible Section 7 rule).
COUNTERS = {}
try:
    with open(os.path.join(SCRATCH, "cw", "counters.json"), encoding="utf-8") as f:
        COUNTERS = json.load(f)
except FileNotFoundError:
    pass


# Units renamed after their assets were made: their files were moved with their .meta, so they keep the GUID of their old id and
# nothing that points at them breaks (the unit and its techs).
RENAMED = {"JackalBeamRifle": "JackalPlasmaRifle"}


def guid(key):
    for new, old in RENAMED.items():
        if key == "unit-" + new or key.startswith("tech-" + new + "-"):
            key = key.replace(new, old, 1)
    return hashlib.md5(("halobellum-m4-" + key).encode()).hexdigest()


def fmt(x):
    if isinstance(x, bool):
        return "1" if x else "0"
    if isinstance(x, float) and x.is_integer():
        return str(int(x))
    return str(x)


def yaml_str(s):
    if s == "" or re.search(r"[:#\[\]{}&*!|>'\"%@`,]", s) or s != s.strip():
        return "'" + s.replace("'", "''") + "'"
    return s


def base_damage(u):
    return u["basedmg"] if u["basedmg"] is not None else round(u["cost"] / u["squad"] / 5.0, 4)


def asset_path(u):
    return os.path.join(ROOT, "Assets", "_Project", "Data", "Units", u["folder"], u["id"] + ".asset")


def existing_prefab(path):
    if not os.path.exists(path):
        return "{fileID: 0}"
    m = re.search(r"^  prefab: (\{.*\})$", open(path, encoding="utf-8").read(), re.M)
    return m.group(1) if m else "{fileID: 0}"


def existing_keeps(path):
    if not os.path.exists(path):
        return 0
    m = re.search(r"^  keepsOwnMaterials: (\d)$", open(path, encoding="utf-8").read(), re.M)
    return int(m.group(1)) if m else 0


def existing_baked(path):
    if not os.path.exists(path):
        return "{fileID: 0}"
    m = re.search(r"^  bakedBody: (\{.*\})$", open(path, encoding="utf-8").read(), re.M)
    return m.group(1) if m else "{fileID: 0}"


def baked_ref(u):
    """A reference to a baked body in Resources/BakedBodies, by its name."""
    meta = os.path.join(ROOT, "Assets", "_Project", "Resources", "BakedBodies", u["baked"] + ".asset.meta")
    return "{fileID: 11400000, guid: " + re.search(r"^guid: (\w+)", open(meta, encoding="utf-8").read(), re.M).group(1) + ", type: 2}"


def unit_yaml(u):
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
             "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             f"  m_Script: {{fileID: 11500000, guid: {UNIT_SCRIPT_GUID}, type: 3}}", f"  m_Name: {u['id']}",
             "  m_EditorClassIdentifier: ",
             f"  unitName: {yaml_str(u['name'])}", f"  role: {ROLE[u['role']]}", f"  tier: {u['tier']}",
             f"  unlockCost: {u['unlock']}", f"  squadSize: {u['squad']}", f"  cost: {u['cost']}",
             f"  baseDamage: {fmt(base_damage(u))}", f"  maxHealth: {fmt(u['hp'])}", f"  armorType: {ARMOR[u['armor']]}",
             f"  maxShield: {fmt(u['shield'])}", f"  shieldRegenPerSecond: {fmt(u['regen'])}",
             f"  moveSpeed: {fmt(u['speed'])}", f"  collisionRadius: {fmt(u['radius'])}", f"  turnTime180: {fmt(u['turn'])}",
             f"  isAir: {fmt(u['air'])}", f"  canFlank: {fmt(u['id'] in FLANKERS)}", f"  goesForTowers: {fmt(u['towers'])}", f"  squadGap: {fmt(u['gap'])}", f"  targetFilter: {FILTER[u['filter']]}", "  weapons:"]
    for w in u["weapons"]:
        lines += [f"  - weaponName: {yaml_str(w['name'])}", f"    damagePerShot: {fmt(w['dmg'])}",
                  f"    fireInterval: {fmt(w['interval'])}", f"    range: {fmt(w['range'])}", f"    minRange: {fmt(w['minr'])}",
                  f"    projectileSpeed: {fmt(w['proj'])}", f"    splashRadius: {fmt(w['splash'])}",
                  f"    damageType: {DTYPE[w['type']]}", f"    isAntiArmor: {fmt(w['aa'])}", f"    canBeBlocked: {fmt(w['block'])}",
                  f"    hasOwnTargetFilter: {fmt(w['own'] is not None)}", f"    targetFilter: {FILTER[w['own'] or 'G']}",
                  f"    airDamageMultiplier: {fmt(w['air'])}", f"    coneDegrees: {fmt(w['cone'])}",
                  f"    lobbed: {fmt(w['lob'])}", f"    beamSeconds: {fmt(w['beam'])}", f"    beamChargeSeconds: {fmt(w['charge'])}",
                  f"    beamRestSeconds: {fmt(w['rest'])}", f"    beamSweepSpeed: {fmt(w['sweep'])}",
                  f"    seedSeconds: {fmt(w['seed'])}", f"    seedDamageSharePerSecond: {fmt(w['seedshare'])}", f"    seedSlowShare: {fmt(w['seedslow'])}",
                  f"    hitSlowSeconds: {fmt(w['slow'])}", f"    hitSlowShare: {fmt(w['slowshare'])}"]
        if w["windup"]:
            lines += [f"    windUpSeconds: {fmt(w['windup'])}"]  # Only where there is one, so the other assets stay as they were.
        if w.get("stagger"):
            lines += [f"    staggerSeconds: {fmt(w['stagger'])}"]
        if w.get("ram"):  # A ram (D160): seconds of driving before it rams again.
            lines += [f"    ramRechargeSeconds: {fmt(w['ram'])}"]
        if w.get("mag"):  # A magazine (D157): shots, then the reload's seconds.
            lines += [f"    magazineSize: {fmt(w['mag'][0])}", f"    magazineReloadSeconds: {fmt(w['mag'][1])}"]
        if w.get("pierce"):  # A shot that kills goes on through to the next enemy on its line (the Jackal's beam rifle, D155).
            lines += ["    piercesKills: 1"]
        if w.get("sbreak"):  # The charged shot (D151): shield points it strips, seconds it charges, how fast the bolt flies.
            lines += [f"    shieldBreak: {fmt(w['sbreak'][0])}", f"    shieldBreakChargeSeconds: {fmt(w['sbreak'][1])}", f"    shieldBreakProjectileSpeed: {fmt(w['sbreak'][2])}"]
    arc = u["arc"]
    hexes = "".join(struct.pack("<i", DTYPE[t]).hex() for t in (arc["types"] if arc else []))
    lines += [f"  hasDefenseArc: {fmt(arc is not None)}", "  defenseArc:",
              f"    arcDegrees: {fmt(arc['deg'] if arc else 0)}",
              f"    frontDamageMultiplier: {fmt(arc['front'] if arc else 1)}",
              f"    rearDamageMultiplier: {fmt(arc['rear'] if arc else 1)}",
              f"    blockChance: {fmt(arc['block'] if arc else 0)}",
              f"    blockedDamageTypes: {hexes}"]
    drop = u["drop"]
    if drop:
        lines += [f"  reinforcementUnit: {{fileID: 11400000, guid: {guid('unit-' + drop[0])}, type: 2}}",
                  f"  reinforcementBodies: {drop[1]}", f"  reinforcementDelaySeconds: {fmt(drop[2])}",
                  f"  reinforcementNeedsUpgrade: {fmt(len(drop) > 3 and drop[3])}",
                  f"  reinforcementDrops: {drop[4] if len(drop) > 4 else 1}",
                  f"  reinforcementLandSeconds: {fmt(drop[5] if len(drop) > 5 else 0)}",
                  f"  reinforcementFirstDropSeconds: {fmt(drop[6] if len(drop) > 6 else 0)}"]
    else:
        lines += ["  reinforcementUnit: {fileID: 0}", "  reinforcementBodies: 0", "  reinforcementDelaySeconds: 0",
                  "  reinforcementNeedsUpgrade: 0"]
    b = u["burst"]
    lines += [f"  formsPerBody: {u['forms']}", f"  isFlood: {fmt(u['flood'])}", f"  infects: {fmt(u['infects'])}",
              f"  immuneToInfection: {fmt(u['immune'])}", f"  selfDestructs: {fmt(u['selfd'])}", f"  seeksDensestCluster: {fmt(u['seek'])}", f"  passesThroughOwnChaff: {fmt(u.get('phase', False))}",
              f"  deathBurstDamage: {fmt(b['dmg'] if b else 0)}", f"  deathBurstRadius: {fmt(b['radius'] if b else 0)}",
              f"  deathBurstDamageType: {DTYPE[b['type'] if b else 'Ballistic']}", f"  deathBurstAntiArmor: {fmt(bool(b and b['aa']))}"]
    if b and b["spawns"]:
        lines += ["  deathSpawns:"]
        for unit_id, bodies in b["spawns"]:
            lines += [f"  - unit: {{fileID: 11400000, guid: {guid('unit-' + unit_id)}, type: 2}}", f"    bodies: {bodies}"]
    else:
        lines += ["  deathSpawns: []"]
    sp = u["spawn"]
    lines += [f"  spawnedUnit: {{fileID: 11400000, guid: {guid('unit-' + sp[0])}, type: 2}}" if sp else "  spawnedUnit: {fileID: 0}",
              f"  spawnedBodies: {sp[1] if sp else 0}", f"  spawnIntervalSeconds: {fmt(sp[2] if sp else 0)}"]
    s2 = u["spawn2"]
    lines += [f"  alternateSpawnedUnit: {{fileID: 11400000, guid: {guid('unit-' + s2[0])}, type: 2}}" if s2 else "  alternateSpawnedUnit: {fileID: 0}",
              f"  alternateSpawnedBodies: {s2[1] if s2 else 0}"]
    au = u["aura"]
    lines += [f"  auraRadius: {fmt(au[0] if au else 0)}", f"  auraSpeedMultiplier: {fmt(au[1] if au else 1)}",
              f"  auraHealPerSecondShare: {fmt(au[2] if au else 0)}", f"  lifestealShare: {fmt(u['lifesteal'])}"]
    if u["seedforms"]:
        lines += ["  seedForms:"] + [line for name, unit_id in u["seedforms"]
                                     for line in (f"  - from: {name}", f"    becomes: {{fileID: 11400000, guid: {guid('unit-' + unit_id)}, type: 2}}")]
    else:
        lines += ["  seedForms: []"]
    lp = u["leap"]
    lines += [f"  leapRange: {fmt(lp[0] if lp else 0)}", f"  leapSpeed: {fmt(lp[1] if lp else 0)}", f"  leapCooldownSeconds: {fmt(lp[2] if lp else 0)}"]
    if u["charges"]:
        lines += ["  leapCharges: 1"]  # Its leap is a charge along the ground, and it uses one weapon at a time (the Hunters, D87).
    techs = TECHS.get(u["id"], [])
    if techs:
        lines += ["  techUpgrades:"] + [f"  - {{fileID: 11400000, guid: {guid('tech-' + u['id'] + '-' + t['id'])}, type: 2}}" for t in techs]
    else:
        lines += ["  techUpgrades: []"]
    strong, weak = ROLE_TEXT[u["role"]]
    counters = COUNTERS.get(u["id"])
    notes = ("Best counters (headless runner, equal cost): " + counters + "." if counters else "Best counters: not measured yet.")
    if u["note"]:
        notes += " " + u["note"]
    notes += (f" First-pass M10 values, {u['sub']} (Docs/M10/subfactions.md)." if u["sub"] else
              " First-pass M8 values (DECISIONS D38)." if u["flood"] else " First-pass M4 values (DECISIONS D21); Section 8 numbers are from the Bible.")
    tint = u["tint"] or (1, 1, 1, 0)
    lines += [f"  prefab: {existing_prefab(asset_path(u))}", f"  keepsOwnMaterials: {existing_keeps(asset_path(u))}", f"  bakedBody: {baked_ref(u) if u['baked'] else existing_baked(asset_path(u))}",
              f"  bakedTint: {{r: {fmt(tint[0])}, g: {fmt(tint[1])}, b: {fmt(tint[2])}, a: {fmt(tint[3])}}}", f"  bakedScale: {fmt(u['scale'])}", f"  strongAgainst: {yaml_str(strong)}",
              f"  weakAgainst: {yaml_str(weak)}", f"  notes: {yaml_str(notes)}"]
    return "\n".join(lines) + "\n"


def tech_path(u, t):
    return os.path.join(ROOT, "Assets", "_Project", "Data", "Upgrades", u["folder"], f"{u['id']}_{t['id']}.asset")


def tech_yaml(u, t):
    fx = t["fx"]
    return "\n".join([
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {TECH_SCRIPT_GUID}, type: 3}}", f"  m_Name: {u['id']}_{t['id']}",
        "  m_EditorClassIdentifier: ",
        f"  techName: {yaml_str(t['name'])}", f"  description: {yaml_str(t['desc'])}", f"  cost: {t['cost']}",
        f"  healthMultiplier: {fmt(fx.get('health', 1))}", f"  damageMultiplier: {fmt(fx.get('damage', 1))}",
        f"  fireRateMultiplier: {fmt(fx.get('fire', 1))}", f"  moveSpeedMultiplier: {fmt(fx.get('speed', 1))}",
        f"  airDamageMultiplier: {fmt(fx.get('air', 1))}", f"  rangeBonus: {fmt(fx.get('range', 0))}", f"  airRangeBonus: {fmt(fx.get('airrange', 0))}",
        f"  shieldBonus: {fmt(fx.get('shield', 0))}", f"  shieldRegenBonus: {fmt(fx.get('regen', 0))}",
        f"  splashRadiusBonus: {fmt(fx.get('splash', 0))}", f"  coneDegreesBonus: {fmt(fx.get('cone', 0))}",
        f"  addsAirTargeting: {fmt(bool(fx.get('aa', False)))}", f"  unlocksReinforcement: {fmt(bool(fx.get('drop', False)))}",
        f"  extraShotsPerAttack: {fx.get('shots', 0)}", f"  burstGapSeconds: {fmt(fx.get('gap', 0))}", f"  reloadTimeMultiplier: {fmt(fx.get('reload', 1))}",
    ]) + "\n"


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def write_meta(path, g, folder=False):
    if os.path.exists(path + ".meta"):
        return
    if folder:
        body = f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        body = f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    write(path + ".meta", body)


FLOOD_FACTION = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 6e6990b4d722009a3b5c9081c2c03694, type: 3}
  m_Name: Flood_Standard
  m_EditorClassIdentifier:
  factionName: Flood
  color: {r: 0.55, g: 0.7, b: 0.25, a: 1}
  uiTheme: {fileID: 0}
  baseRoster:
  subFactions:
  - subFactionName: Standard
    exclusiveUnits: []
  spells:
  cards:
"""


def subfaction_yaml(faction):
    """A faction's subFactions list: its sub-factions with their exclusive units, or the temporary Standard card if it has none."""
    modules = [m for m in SUBFACTIONS if m.FACTION == faction]
    if not modules:
        return "  subFactions:\n  - subFactionName: Standard\n    description: \n    exclusiveUnits: []\n"
    text = "  subFactions:\n"
    for m in modules:
        text += f"  - subFactionName: {yaml_str(m.NAME)}\n    description: {yaml_str(m.DESCRIPTION)}\n    exclusiveUnits:\n"
        text += "".join(f"    - {{fileID: 11400000, guid: {guid('unit-' + u['id'])}, type: 2}}\n" for u in UNITS if u["sub"] == m.NAME)
        costs = getattr(m, "COSTS", {})  # Base roster units at the sub-faction's own price (D158).
        by_id = {u["id"]: u for u in UNITS}
        text += "    unitCosts:\n" + "".join(f"    - unitName: {yaml_str(by_id[i]['name'])}\n      cost: {c}\n" for i, c in costs.items()) if costs else "    unitCosts: []\n"
    return text


# Decision D36: every unit moves at half the speed listed above; the fast ground vehicles get 1 m/s back.
FAST_VEHICLES = {"Warthog": 1, "Ghost": 1, "Gauss Warthog": 1}


def apply_speed_decision():
    for u in UNITS:
        if u["flood"] or u["sub"]:
            continue  # Their speeds were set after the change.
        u["speed"] = u["speed"] * 0.5 + FAST_VEHICLES.get(u["name"], 0)


def apply_windups():
    """Every weapon named Spartan Laser, on whichever unit carries one, gets its wind-up."""
    for u in UNITS:
        for w in u["weapons"]:
            if w["name"] == "Spartan Laser":
                w["windup"] = SPARTAN_LASER_WINDUP


def main():
    apply_speed_decision()
    apply_windups()
    unit_folders = ["UNSC", "Covenant", "Flood"] + [m.FOLDER for m in SUBFACTIONS]
    for faction in unit_folders:
        folder = os.path.join(ROOT, "Assets", "_Project", "Data", "Units", faction)
        os.makedirs(folder, exist_ok=True)
        write_meta(folder, guid("folder-" + faction), folder=True)
    for u in UNITS:
        write(asset_path(u), unit_yaml(u))
        write_meta(asset_path(u), guid("unit-" + u["id"]))
    for faction in unit_folders:
        folder = os.path.join(ROOT, "Assets", "_Project", "Data", "Upgrades", faction)
        os.makedirs(folder, exist_ok=True)
        write_meta(folder, guid("upgrades-folder-" + faction), folder=True)
    for u in UNITS:
        for t in TECHS.get(u["id"], []):
            write(tech_path(u, t), tech_yaml(u, t))
            write_meta(tech_path(u, t), guid("tech-" + u["id"] + "-" + t["id"]))

    # Faction rosters.
    flood_faction = os.path.join(ROOT, "Assets", "_Project", "Data", "Factions", "Flood_Standard.asset")
    if not os.path.exists(flood_faction):
        write(flood_faction, FLOOD_FACTION)
        write_meta(flood_faction, guid("faction-Flood"))
    for faction, asset in (("UNSC", "UNSC_Standard.asset"), ("Covenant", "Covenant_Standard.asset"), ("Flood", "Flood_Standard.asset")):
        path = os.path.join(ROOT, "Assets", "_Project", "Data", "Factions", asset)
        text = open(path, encoding="utf-8").read()
        roster = "".join(f"  - {{fileID: 11400000, guid: {guid('unit-' + u['id'])}, type: 2}}\n" for u in UNITS if u["faction"] == faction and not u["sub"])
        text = re.sub(r"  baseRoster:\n(?:  - .*\n)*", lambda m: "  baseRoster:\n" + roster, text)
        text = re.sub(r"  subFactions:\n(?:  - .*\n|    .*\n)*", lambda m: subfaction_yaml(faction), text)
        write(path, text)

    # balance.csv (Bible Section 15).
    def dps(w, armor):
        mult = {"Ballistic": (1, .6, .25), "Explosive": (1, 1, 1), "Energy": (1, 1, .75), "Fire": (1.5, .8, .3),
                "Melee": (1, .7, .3)}[w["type"]]
        if w["aa"]:
            mult = (.5, 1.2, 1.5)
        uptime = w["beam"] / (w["beam"] + w["charge"] + w["rest"]) if w["beam"] else 1  # Average over a beam's whole cycle.
        return w["dmg"] / w["interval"] * mult[armor] * uptime
    rows = ["unit,faction,sub_faction,role,tier,unlock,squad_size,cost,health,armor,shield,move_speed,main_range,"
            "squad_dps_vs_unarmored,squad_dps_vs_light,squad_dps_vs_heavy,squad_health,base_damage_per_body,best_counters"]
    for u in UNITS:
        d = [sum(dps(w, a) for w in u["weapons"]) * u["squad"] for a in range(3)]
        rows.append(",".join([u["name"].replace(",", ""), u["faction"], u["sub"] or "", u["role"], str(u["tier"]), str(u["unlock"]),
                              str(u["squad"]), str(u["cost"]), fmt(u["hp"]), {"U": "Unarmored", "L": "Light", "H": "Heavy"}[u["armor"]],
                              fmt(u["shield"]), fmt(u["speed"]), fmt(u["weapons"][0]["range"]),
                              f"{d[0]:.0f}", f"{d[1]:.0f}", f"{d[2]:.0f}", fmt(u["hp"] * u["squad"] + u["shield"] * u["squad"]),
                              fmt(base_damage(u)), '"' + COUNTERS.get(u["id"], "") + '"']))
    write(os.path.join(ROOT, "balance.csv"), "\n".join(rows) + "\n")

    # JSON copy for the tuning console.
    write(os.path.join(SCRATCH, "cw", "units.json"), json.dumps(UNITS, indent=1))
    print(f"wrote {len(UNITS)} units")


if __name__ == "__main__":
    main()
