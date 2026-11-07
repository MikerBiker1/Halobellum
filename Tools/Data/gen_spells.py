# Writes the first-pass spell cards (Data/Spells), the ODST drop-pod unit (Data/Units/SpellUnits), the
# other cards (Data/Cards: items, economy, buffs, training) and the factions' card lists. Decisions D31
# and D33. Deterministic GUIDs so re-running keeps references.
import hashlib, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Project"))
SPELL_SCRIPT = "f5470c48de91cdfdc40f1f87fc3b6380"
UNIT_SCRIPT = "48a24a6f0743ea50b653bb6800d1d896"
CARD_SCRIPT = "e60f424de7e158173404479ece2d51b5"


def guid(key):
    return hashlib.md5(("halobellum-spell-" + key).encode()).hexdigest()


def meta(path, g):
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def folder_meta(path):
    if not os.path.exists(path + ".meta"):
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(f"fileFormatVersion: 2\nguid: {guid('folder-' + os.path.relpath(path, ROOT))}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier:
"""

# ------------------------------------------------------------------ ODST (only used by the drop-pod spell)
ODST = """  unitName: ODST
  role: 2
  tier: 2
  unlockCost: 0
  squadSize: 12
  cost: 300
  baseDamage: 5
  maxHealth: 100
  armorType: 0
  maxShield: 0
  shieldRegenPerSecond: 0
  moveSpeed: 1.75
  collisionRadius: 0.5
  turnTime180: 0.25
  isAir: 0
  targetFilter: 2
  weapons:
  - weaponName: M7 SMG
    damagePerShot: 3
    fireInterval: 0.1
    range: 10
    minRange: 0
    projectileSpeed: 0
    splashRadius: 0
    damageType: 0
    isAntiArmor: 0
    canBeBlocked: 1
    hasOwnTargetFilter: 0
    targetFilter: 0
    airDamageMultiplier: 1
    coneDegrees: 0
    targetClass: 1
  - weaponName: M41 Rocket Launcher
    damagePerShot: 60
    fireInterval: 3
    range: 16
    minRange: 0
    projectileSpeed: 40
    splashRadius: 1.5
    damageType: 1
    isAntiArmor: 1
    canBeBlocked: 0
    hasOwnTargetFilter: 0
    targetFilter: 0
    airDamageMultiplier: 1
    coneDegrees: 0
    targetClass: 2
  hasDefenseArc: 0
  defenseArc:
    arcDegrees: 0
    frontDamageMultiplier: 1
    rearDamageMultiplier: 1
    blockChance: 0
    blockedDamageTypes:
  reinforcementUnit: {fileID: 0}
  reinforcementBodies: 0
  reinforcementDelaySeconds: 0
  reinforcementNeedsUpgrade: 0
  techUpgrades: []
  prefab: {fileID: 0}
  bakedBody: {BAKED}
  strongAgainst: Chaff
  weakAgainst: Chaff clear
  notes: 'Only lands from the ODST Drop Pods spell (UNSC, decisions D31 and D33); not in any roster. Uses the SMG on chaff and unarmored enemies, the rocket launcher on tougher ones. First-pass values.'
"""

# Damage types: 0 Ballistic, 1 Explosive, 2 Energy, 3 Fire, 4 Melee. Effects: 0 Damage, 1 Buff, 2 Drop.
# Targets: 0 Ground, 1 Air, 2 Both.
SMOKE = dict(file="SmokeBomb", name="Smoke Bomb", desc="Click one end, aim the other. Smoke fills the line and blinds both sides.",
             off=1, eff=4, cost=100, cd=3, max=1, radius=0, delay=0.5, width=45, depth=8, duration=0, rangemul=0.5)
EMP = dict(file="ElectromagneticPulse", name="Electromagnetic Pulse", desc="Fries shields and electronics: everything in the area, friend or foe.",
           off=1, eff=3, cost=150, cd=2, max=1, radius=10, delay=1, shielddmg=10000, techoff=10, slow=0.4, slowfor=10)

SPELLS = {
    "UNSC": [
        dict(file="MacStrike", name="MAC Strike", desc="A precision round from a frigate's Magnetic Accelerator Cannon in orbit.",
             off=1, eff=0, cost=150, cd=1, max=2, radius=4, delay=1, dmg=800, dtype=1, aa=1, ticks=1, interval=0, targets=0),
        dict(file="MissileBarrage", name="Archer Missile Barrage", desc="A frigate's Archer pods saturate a wide area: missiles land at random spots across it, wave after wave.",
             off=1, eff=0, cost=150, cd=2, max=2, radius=20, delay=1, dmg=65, dtype=1, aa=0, ticks=6, interval=0.5, targets=2, strike=4.5, strikes=6),  # D83
        dict(file="HavokNuke", name="HAVOK Nuke", desc="A tactical nuclear warhead. Devastating, slow to recharge. Mind your own troops.",
             off=1, eff=0, cost=350, cd=4, max=1, radius=14, delay=6, min_round=7, dmg=2500, dtype=1, aa=0, ticks=1, interval=0, targets=0),
        dict(file="OdstDropPods", name="ODST Drop Pods", desc="Orbital drop shock troopers land anywhere on the field for this battle: SMGs for infantry, rocket launchers for armor.",
             off=1, eff=2, cost=200, cd=2, max=1, radius=4, delay=1, drop="ODST", squads=1),
        SMOKE, EMP,
        dict(file="BubbleShield", name="Bubble Shield", desc="Deployable shields protect your units.",
             off=0, eff=1, cost=100, cd=1, max=2, radius=8, delay=0, shield=0.5),
        dict(file="BattlefieldRally", name="Battlefield Rally", desc="A Spartan rallies the troops: more damage for the whole battle.",
             off=0, eff=1, cost=150, cd=2, max=1, radius=10, delay=0, dmgmul=1.3),
    ],
    "Covenant": [
        dict(file="PlasmaBombardment", name="Plasma Bombardment", desc="A precise plasma shot from a cruiser in orbit.",
             off=1, eff=0, cost=150, cd=1, max=2, radius=5, delay=1, dmg=450, dtype=2, aa=0, ticks=1, interval=0, targets=0),
        dict(file="PlasmaStorm", name="Plasma Storm", desc="A storm of plasma bolts over a wide area: they strike at random spots across it.",
             off=1, eff=0, cost=150, cd=2, max=2, radius=20, delay=1, dmg=55, dtype=2, aa=0, ticks=8, interval=0.5, targets=2, strike=4, strikes=5),  # D83
        dict(file="GlassingBeam", name="Glassing Beam", desc="Click where the beam starts, aim where it ends: a supercarrier's beam sweeps 30 m of ground into glass. Devastating, slow to recharge. Mind your own troops.",
             off=1, eff=0, cost=350, cd=4, max=1, radius=6, width=30, depth=12, delay=6, min_round=7, dmg=700, dtype=2, aa=0, ticks=10, interval=0.4, targets=2),  # D83: a sweeping beam
        dict(file="BansheeAmbush", name="Banshee Ambush", desc="Two Banshee squads descend from high above, anywhere on the map, for this battle.",
             off=1, eff=2, cost=50, cd=3, max=1, radius=5, delay=1, drop_asset="Covenant/Banshee", squads=2),
        SMOKE, EMP,
        dict(file="ShieldDome", name="Energy Shield Dome", desc="A dome of hard light shields your units.",
             off=0, eff=1, cost=100, cd=1, max=2, radius=8, delay=0, shield=0.5),
        dict(file="ProphetsBlessing", name="Prophet's Blessing", desc="The Prophets' blessing drives your warriors to greater fury.",
             off=0, eff=1, cost=150, cd=2, max=1, radius=10, delay=0, dmgmul=1.3),
    ],
    "Flood": [
        dict(file="SporeBurst", name="Spore Burst", desc="A pod bursts over the area. Unarmored units it kills rise as Flood.",
             off=1, eff=0, cost=150, cd=1, max=2, radius=5, delay=1, dmg=350, dtype=1, aa=0, ticks=1, interval=0, targets=0, infects=1),
        dict(file="BlightRain", name="Blight Rain", desc="Spores rain over a wide area, bursting at random spots across it. Unarmored units it kills rise as Flood.",
             off=1, eff=0, cost=150, cd=2, max=2, radius=20, delay=1, dmg=55, dtype=1, aa=0, ticks=8, interval=0.5, targets=2, infects=1, strike=4, strikes=5),  # D83
        dict(file="GravemindsWrath", name="Gravemind's Wrath", desc="The Gravemind turns its whole will on one spot. Unarmored units it kills rise as Flood. Mind your own forms.",
             off=1, eff=0, cost=350, cd=4, max=1, radius=13, delay=6, min_round=7, dmg=1600, dtype=1, aa=0, ticks=1, interval=0, targets=0, infects=1),
        dict(file="DispersalPods", name="Dispersal Pods", desc="Two squads of Infection Forms land anywhere on the field for this battle.",
             off=1, eff=2, cost=100, cd=2, max=1, radius=5, delay=1, drop_asset="Flood/InfectionForms", squads=2),
        dict(SMOKE, name="Spore Cloud", desc="Click one end, aim the other. Spores fill the line and blind both sides."),
        dict(EMP, file="NerveSpores", name="Nerve Spores", desc="Spores that eat shields and clog machinery: everything in the area, friend or foe."),
        dict(file="BiomassShell", name="Biomass Shell", desc="A shell of fresh biomass protects your units.",
             off=0, eff=1, cost=100, cd=1, max=2, radius=8, delay=0, shield=0.5),
        dict(file="HiveFrenzy", name="Hive Frenzy", desc="The Gravemind drives your forms to greater fury.",
             off=0, eff=1, cost=150, cd=2, max=1, radius=10, delay=0, dmgmul=1.3),
    ],
}

# The ODST unit.
unit_folder = os.path.join(ROOT, "Data", "Units", "SpellUnits")
os.makedirs(unit_folder, exist_ok=True)
folder_meta(unit_folder)
odst_path = os.path.join(unit_folder, "ODST.asset")
odst_body = os.path.join(ROOT, "Resources", "BakedBodies", "ODST.asset.meta")
odst_baked = "{fileID: 11400000, guid: " + re.search(r"^guid: (\w+)", open(odst_body, encoding="utf-8").read(), re.M).group(1) + ", type: 2}"
open(odst_path, "w", encoding="utf-8", newline="\n").write(HEADER.format(script=UNIT_SCRIPT, name="ODST") + ODST.replace("{BAKED}", odst_baked))
meta(odst_path, guid("unit-ODST"))

spells_root = os.path.join(ROOT, "Data", "Spells")
os.makedirs(spells_root, exist_ok=True)
folder_meta(spells_root)
lists = {}
for faction, spells in SPELLS.items():
    folder = os.path.join(spells_root, faction)
    os.makedirs(folder, exist_ok=True)
    folder_meta(folder)
    lists[faction] = []
    for s in spells:
        drop = f"{{fileID: 11400000, guid: {guid('unit-' + s['drop'])}, type: 2}}" if s.get("drop") else "{fileID: 0}"
        if s.get("drop_asset"):
            meta_text = open(os.path.join(ROOT, "Data", "Units", s["drop_asset"] + ".asset.meta"), encoding="utf-8").read()
            drop = f"{{fileID: 11400000, guid: {re.search(r'guid: ([0-9a-f]+)', meta_text).group(1)}, type: 2}}"
        body = (f"  spellName: {s['name']}\n"
                f"  description: '{s['desc'].replace(chr(39), chr(39) * 2)}'\n"
                f"  offensive: {s['off']}\n  effect: {s['eff']}\n"
                f"  cost: {s['cost']}\n  cooldownRounds: {s['cd']}\n  maxOwnedCopies: {s['max']}\n"
                f"  radius: {s['radius']}\n  delaySeconds: {s['delay']}\n  minRound: {s.get('min_round', 0)}\n"
                f"  areaWidth: {s.get('width', 0)}\n  areaDepth: {s.get('depth', 0)}\n"
                f"  damagePerTick: {s.get('dmg', 0)}\n  damageType: {s.get('dtype', 0)}\n  isAntiArmor: {s.get('aa', 0)}\n"
                f"  infectsKills: {s.get('infects', 0)}\n"
                f"  ticks: {s.get('ticks', 1)}\n  tickInterval: {s.get('interval', 0)}\n  targets: {s.get('targets', 0)}\n"
                f"  strikeRadius: {s.get('strike', 0)}\n  strikesPerTick: {s.get('strikes', 1)}\n"
                f"  shieldBonusShare: {s.get('shield', 0)}\n  damageMultiplier: {s.get('dmgmul', 1)}\n"
                f"  dropUnit: {drop}\n  dropSquads: {s.get('squads', 1)}\n"
                f"  shieldDamage: {s.get('shielddmg', 0)}\n  techDisableSeconds: {s.get('techoff', 0)}\n"
                f"  slowShare: {s.get('slow', 0)}\n  slowSeconds: {s.get('slowfor', 0)}\n"
                f"  durationSeconds: {s.get('duration', 0)}\n  rangeMultiplier: {s.get('rangemul', 1)}\n")
        path = os.path.join(folder, s["file"] + ".asset")
        open(path, "w", encoding="utf-8", newline="\n").write(HEADER.format(script=SPELL_SCRIPT, name=s["file"]) + body)
        g = guid(faction + "-" + s["file"])  # Smoke and EMP have one asset per faction.
        meta(path, g)
        lists[faction].append(g)

# ------------------------------------------------------------------ the other cards, shared by both factions
# Types: 1 Item, 2 Economy, 3 Buff, 4 Training. Items can be taken again and again (99 copies).
CARDS = [
    dict(file="LaserSights", icon="Item_LaserSights", name="Laser Sights", type=1, cost=50, max=99, desc="Sights that let a squad hit from further away.", range=2),
    dict(file="AbsorptionModule", icon="Item_AbsorptionModule", name="Absorption Module", type=1, cost=100, max=99, desc="Turns the damage a squad deals into health.", hp=1.3, steal=0.9),
    dict(file="AmplifyingCore", icon="Item_AmplifyingCore", name="Amplifying Core", type=1, cost=100, max=99, desc="More power to everything the squad does.", dmg=1.5, hp=1.5),
    dict(file="DeploymentModule", icon="Item_DeploymentModule", name="Deployment Module", type=1, cost=0, max=99, desc="The squad can be moved freely in every deployment phase.", free=1),
    dict(file="HasteStims", icon="Item_HasteStims", name="Haste Stims", type=1, cost=50, max=99, desc="Faster and hits harder.", speed=1.25, dmg=1.4),
    dict(file="HeavyArmor", icon="Item_HeavyArmor", name="Heavy Armor", type=1, cost=50, max=99, desc="Extra plating for the whole squad.", hp=1.7),
    dict(file="RepairKit", icon="Item_RepairKit", name="Healing Stims / Repair Kit", type=1, cost=50, max=99, desc="Repairs damage continuously.", heal=0.045),
    dict(file="ImprovedFirepower", icon="Item_ImprovedFirepower", name="Improved Firepower System", type=1, cost=50, max=99, desc="Upgraded weapons systems.", dmg=1.7),
    dict(file="SupplyEnhancement", icon="Card_SupplyEnhancement", name="Supply Enhancement", type=2, cost=100, max=1, desc="Better supply lines.", income=50),
    dict(file="SuperSupplyEnhancement", icon="Card_SuperSupply", name="Super Supply Enhancement", type=2, cost=250, max=1, min_round=7, desc="A much bigger share of the fleet's supplies.", income=150),
    dict(file="AdvancedOffensiveTactics", icon="Card_AdvancedOffense", name="Advanced Offensive Tactics", type=3, cost=350, max=1, min_round=7, desc="Hit harder, everywhere.", alldmg=1.3),
    dict(file="AdvancedDefenseTactics", icon="Card_AdvancedDefense", name="Advanced Defense Tactics", type=3, cost=300, max=1, min_round=7, desc="Everyone lasts longer.", allhp=1.3),
    dict(file="AdvancedTargetingSystem", icon="Card_AdvancedTargeting", name="Advanced Targeting System", type=3, cost=400, max=1, min_round=7, desc="Better fire control for every ranged unit.", allrange=2),
    dict(file="AdditionalDeploymentSlot", icon="Card_DeploymentSlot", name="Additional Deployment Slot", type=3, cost=100, max=1, min_round=5, desc="Deploy more squads every round.", slots=1),
    dict(file="IntensiveTraining", icon="Card_IntensiveTraining", name="Intensive Training", type=4, cost=50, max=2, desc="Select a squad, then use the icon at the top left: its XP bar fills up, so you can choose to level it up.", cooldown=1),
]
cards_root = os.path.join(ROOT, "Data", "Cards")
os.makedirs(cards_root, exist_ok=True)
folder_meta(cards_root)
card_guids = []
for c in CARDS:
    icon_rel = f"Assets/_Project/Art/UI/Items/{c['icon']}.png"
    icon_guid = hashlib.md5(("halobellum-ui-art-" + icon_rel).encode()).hexdigest()
    body = (f"  cardName: {c['name']}\n  description: '{c['desc'].replace(chr(39), chr(39) * 2)}'\n  type: {c['type']}\n"
            f"  icon: {{fileID: 2800000, guid: {icon_guid}, type: 3}}\n"
            f"  cost: {c['cost']}\n  maxOwnedCopies: {c['max']}\n  minRound: {c.get('min_round', 0)}\n"
            f"  healthMultiplier: {c.get('hp', 1)}\n  damageMultiplier: {c.get('dmg', 1)}\n  moveSpeedMultiplier: {c.get('speed', 1)}\n"
            f"  rangeBonus: {c.get('range', 0)}\n  lifestealShare: {c.get('steal', 0)}\n  healPerSecondShare: {c.get('heal', 0)}\n"
            f"  freeDeployment: {c.get('free', 0)}\n  bonusIncome: {c.get('income', 0)}\n"
            f"  allDamageMultiplier: {c.get('alldmg', 1)}\n  allHealthMultiplier: {c.get('allhp', 1)}\n  allRangeBonus: {c.get('allrange', 0)}\n"
            f"  extraSquadsPerRound: {c.get('slots', 0)}\n  cooldownRounds: {c.get('cooldown', 0)}\n")
    path = os.path.join(cards_root, c["file"] + ".asset")
    open(path, "w", encoding="utf-8", newline="\n").write(HEADER.format(script=CARD_SCRIPT, name=c["file"]) + body)
    g = guid("card-" + c["file"])
    meta(path, g)
    card_guids.append(g)

for faction, asset in (("UNSC", "UNSC_Standard.asset"), ("Covenant", "Covenant_Standard.asset"), ("Flood", "Flood_Standard.asset")):
    path = os.path.join(ROOT, "Data", "Factions", asset)
    text = open(path, encoding="utf-8").read()
    text = re.sub(r"  spells:\n(  - .*\n)*", "", text)
    text = re.sub(r"  cards:\n(  - .*\n)*", "", text)
    text = text.rstrip("\n") + "\n  spells:\n" + "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in lists[faction])
    text += "  cards:\n" + "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in card_guids)
    open(path, "w", encoding="utf-8", newline="\n").write(text)
print("ok")
