# Flood, UNSC-based (M10): infested UNSC troops and vehicles with scavenged weapons, plus the Juggernaut pure form.
# They are named "Infested", because "Infected X" is what the simulation calls a body that rises in a battle.
# Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "UNSC-based"
FACTION = "Flood"
FOLDER = "FloodUNSC"
DESCRIPTION = "Infested Marines and UNSC vehicles, their guns still working, and the Juggernaut."
ORDER = 7
INFESTED = (0.55, 0.68, 0.18, 0.55)


def units(U, W):
    return [
        U("InfestedMarine", "Infested Marine", FACTION, "Chaff", 1, 0, 12, 100, 110, "U", 2.25, 0.5, 0.25, "G",
          [W("Scythe Limb", 20, 0.8, 1.5, "Melee"),
           W("Scavenged Rifle", 4, 0.5, 10, "Ballistic", block=True, own="B")], flood=True, leap=(4, 10, 4), tint=INFESTED, baked="Marine",
          note="Fewer bodies than a Marine squad, each much tougher: it leaps in with a scythe limb. Cannot infect, as only Infection Forms do."),
        U("InfestedWarthog", "Infested Warthog", FACTION, "Flanker", 1, 0, 2, 200, 320, "L", 5, 1.2, 1, "G",
          [W("Ramming Mass", 50, 1.0, 1.5, "Melee", splash=1.5),
           W("Scavenged Machine Gun", 7, 0.2, 10, "Ballistic", block=True, own="B")], flood=True,
          note="Drives straight into the enemy, its gun still firing."),
        U("InfestedScorpion", "Infested Scorpion", FACTION, "Tank", 2, 50, 1, 300, 1350, "H", 1.4, 2, 2, "G",
          [W("Scavenged Cannon", 80, 3, 14, "Explosive", proj=35, splash=2.5),
           W("Tentacle Whip", 30, 1.0, 4, "Melee", own="G")], flood=True,
          note="A little tougher and shorter-ranged than the real thing, with tentacles for what gets close."),
        U("InfestedHornet", "Infested Hornet", FACTION, "Aerial", 2, 50, 2, 300, 360, "L", 4, 1.5, 1, "B",
          [W("Scavenged Rockets", 20, 1.0, 12, "Explosive", proj=40),
           W("Spore Bombs", 40, 2.0, 8, "Explosive", proj=20, splash=3, own="G")], flood=True, airu=True,
          note="Barely flying, still bombing."),
        U("Juggernaut", "Juggernaut", FACTION, "Flanker", 3, 225, 1, 450, 2300, "H", 3.25, 1.8, 1, "G",
          [W("Crushing Charge", 130, 1.2, 2.5, "Melee", splash=2.5)], flood=True, leap=(10, 14, 6),
          note="A pure form the size of a tank that moves like a flanker and leaps onto what it hunts."),
        U("InfestedElephant", "Infested Elephant", FACTION, "Titan", 4, 350, 1, 800, 7500, "H", 0.6, 5, 3, "B",
          [W("Scavenged Main Cannon", 180, 3, 18, "Explosive", proj=40, splash=2, aa=True, own="G"),
           W("Scavenged Turret (left)", 7, 0.2, 13, "Ballistic", block=True),
           W("Scavenged Turret (right)", 7, 0.2, 13, "Ballistic", block=True)], flood=True, spawn=("InfestedMarine", 4, 12),
          note="A rolling hive: it releases 4 Infested Marines every 12 seconds."),
    ]


TECHS = {
    "InfestedMarine": [
        T("DenseBiomass", "Dense Biomass", 100, "Takes much more to put down.", health=1.3),
        T("Sprint", "Reflex Sprint", 100, "Charges faster.", speed=1.25),
    ],
    "InfestedWarthog": [
        T("BiomassPlates", "Biomass Plates", 150, "Biomass over the hull.", health=1.3),
        T("Overburnt", "Overburnt Engine", 100, "Rams faster.", speed=1.25),
    ],
    "InfestedScorpion": [
        T("BiomassPlates", "Biomass Plates", 200, "Biomass over the hull.", health=1.25),
        T("LongWhip", "Long Whip", 150, "Everything reaches further.", range=3),
    ],
    "InfestedHornet": [
        T("BiomassPlates", "Biomass Plates", 150, "Biomass over the airframe.", health=1.3),
        T("SporeLoad", "Heavy Spore Load", 200, "Heavier rockets and bombs.", damage=1.3),
    ],
    "Juggernaut": [
        T("WarForm", "War Form", 250, "An even larger body.", health=1.25),
        T("RazorMass", "Razor Mass", 250, "It hits harder.", damage=1.3),
        T("Bounding", "Bounding Stride", 150, "It runs faster.", speed=1.2),
    ],
    "InfestedElephant": [
        T("BiomassPlates", "Biomass Plates", 300, "Even more biomass.", health=1.25),
        T("OvergrownCannon", "Overgrown Cannon", 300, "Bigger guns all round.", damage=1.3),
    ],
}
