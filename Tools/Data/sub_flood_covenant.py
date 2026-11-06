# Flood, Covenant-based (M10): infested Covenant troops and vehicles with scavenged plasma, plus the Spawner pure form.
# They are named "Infested", because "Infected X" is what the simulation calls a body that rises in a battle.
# Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Covenant-based"
FACTION = "Flood"
FOLDER = "FloodCovenant"
DESCRIPTION = "Infested Covenant troops with their plasma, and the Spawner."
ORDER = 8
INFESTED = (0.55, 0.68, 0.18, 0.55)


def units(U, W):
    return [
        U("InfestedGrunt", "Infested Grunt", FACTION, "Chaff", 1, 0, 16, 100, 75, "U", 3, 0.45, 0.25, "G",
          [W("Claws", 11, 0.7, 1.5, "Melee"),
           W("Scavenged Plasma Pistol", 3, 0.5, 9, "Energy", own="B")], flood=True, leap=(4, 10, 4), tint=INFESTED, baked="Grunt",
          note="A rushing crowd with claws. Cannot infect, as only Infection Forms do."),
        U("InfestedGhost", "Infested Ghost", FACTION, "Flanker", 1, 0, 2, 200, 270, "L", 5.5, 1.2, 0.8, "G",
          [W("Hull Ram", 45, 1.0, 1.5, "Melee", splash=1.5),
           W("Scavenged Plasma Bolts", 8, 0.25, 9, "Energy", own="B")], flood=True, burst=dict(dmg=0, radius=0, type="Explosive", aa=False, spawns=[("InfectionForms", 1)]),
          note="Faster and lighter than the Infested Warthog."),
        U("InfestedElite", "Infested Elite", FACTION, "SingleTargetDps", 2, 50, 3, 250, 420, "U", 2, 0.6, 0.35, "G",
          [W("Scythe Claw", 42, 0.7, 1.5, "Melee", aa=True),
           W("Scavenged Plasma Rifle", 10, 0.5, 11, "Energy", own="B")], flood=True, leap=(5, 12, 4), tint=INFESTED, baked="InfestedElite",
          note="The classic combat form: no shield, more health, and a claw that opens armor."),
        U("InfestedWraith", "Infested Wraith", FACTION, "Artillery", 2, 50, 1, 250, 1000, "H", 1, 2, 2, "G",
          [W("Scavenged Mortar", 70, 3.5, 34, "Explosive", proj=20, splash=4, minr=10, lob=True)], flood=True, burst=dict(dmg=0, radius=0, type="Explosive", aa=False, spawns=[("InfectionForms", 1)]),
          note="A little tougher and shorter-ranged than the real thing."),
        U("InfestedBanshee", "Infested Banshee", FACTION, "Aerial", 2, 50, 2, 250, 360, "L", 4.5, 1.4, 0.8, "B",
          [W("Scavenged Plasma Cannons", 8, 0.3, 10, "Energy"),
           W("Spore Bomb", 45, 2.5, 8, "Explosive", proj=30, splash=3, own="G")], flood=True, airu=True, burst=dict(dmg=0, radius=0, type="Explosive", aa=False, spawns=[("InfectionForms", 1)]),
          note="Barely flying, still bombing."),
        U("Spawner", "Spawner", FACTION, "Tank", 3, 200, 1, 400, 2800, "L", 0.9, 2, 2, "G",
          [W("Spore Lash", 40, 1.2, 3, "Melee", splash=2)], flood=True, spawn=("InfectionForms", 2, 4), spawn2=("InfestedElite", 1),
          note="A mobile birthing form: it fights badly, and releases 12 Infection Forms and an Infested Elite in turn, one every 4 seconds; the Elites draw the enemy's fire. Kill it first (D79)."),
    ]


TECHS = {
    "InfestedGrunt": [
        T("DenseBiomass", "Dense Biomass", 100, "Takes much more to put down.", health=1.3),
        T("Sprint", "Reflex Sprint", 100, "Charges faster.", speed=1.25),
    ],
    "InfestedGhost": [
        T("BiomassPlates", "Biomass Plates", 150, "Biomass over the hull.", health=1.3),
        T("Overburnt", "Overburnt Engine", 100, "Rams faster.", speed=1.25),
    ],
    "InfestedElite": [
        T("DenseBiomass", "Dense Biomass", 150, "Takes much more to put down.", health=1.3),
        T("BoneBlades", "Bone Blades", 200, "Sharpened claws.", damage=1.25),
        T("Sprint", "Reflex Sprint", 100, "Charges faster.", speed=1.25),
    ],
    "InfestedWraith": [
        T("BiomassPlates", "Biomass Plates", 200, "Biomass over the hull.", health=1.25),
        T("SporeShells", "Spore Shells", 200, "Each shell covers more ground.", splash=1.5),
    ],
    "InfestedBanshee": [
        T("BiomassPlates", "Biomass Plates", 150, "Biomass over the airframe.", health=1.3),
        T("SporeLoad", "Heavy Spore Load", 200, "Heavier bombs and cannons.", damage=1.3),
    ],
    "Spawner": [
        T("ThickSac", "Thick Sac", 250, "It takes longer to kill.", health=1.3),
        T("SwiftGait", "Swift Gait", 150, "It moves up faster.", speed=1.3),
    ],
}
