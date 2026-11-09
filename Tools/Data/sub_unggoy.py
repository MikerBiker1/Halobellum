# Covenant Unggoy Rebellion (M10): Grunts everywhere, and methane. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Unggoy Rebellion"
FACTION = "Covenant"
FOLDER = "UnggoyRebellion"
DESCRIPTION = "Grunts, and more Grunts: huge cheap squads, burning methane, a hero's mech."
ORDER = 6


def units(U, W):
    return [
        U("CannonFodder", "Cannon Fodder", FACTION, "Chaff", 1, 0, 30, 100, 20, "U", 1.75, 0.4, 0.25, "B",
          [W("Plasma Pistol", 3, 0.5, 9, "Energy")], tint=(0.72, 0.6, 0.42, 0.35), scale=0.9, baked="Grunt",
          note="The largest squad in the game: 30 Grunts who barely fight. They are there to be shot instead of something else."),
        U("HeavyGrunts", "Heavy Grunts", FACTION, "SingleTargetDps", 1, 0, 8, 200, 100, "U", 1.5, 0.55, 0.3, "B",
          [W("Needle Rifle", 8, 0.25, 12, "Energy")], shield=30, regen=10, tint=(0.75, 0.22, 0.2, 0.4), scale=1.1, baked="Grunt",
          note="Grunts in heavy armor with a small shield, at the top of the Tier 1 price band."),
        U("GruntGoblin", "Grunt Goblin", FACTION, "Tank", 3, 200, 1, 400, 1750, "H", 2, 1.5, 1, "B",
          [W("Goblin Plasma Cannons", 10, 0.2, 14, "Energy"),
           W("Goblin Fuel Rod", 60, 3, 16, "Explosive", proj=30, splash=2.5, own="G")], aura=(14, 1.2, 0.006), baked="GruntGoblin",
          note="A Grunt in a hero's mech. A quick tank with a strong aura: friends within 14 m move 20% faster and heal 0.6% per second."),
    ]


TECHS = {
    "CannonFodder": [
        T("Anklebiters", "Anklebiters", 100, "Pistols fire faster.", fire=1.25),
        T("ReferAFriend", "Refer a Friend", 100, "Slightly sturdier armor and tanks.", health=1.3),
    ],
    "HeavyGrunts": [
        T("ShieldUpgrade", "Shield Upgrade", 150, "Stronger energy shields.", shield=30),
        T("Overcharge", "Rifle Overcharge", 150, "Needles fire faster.", fire=1.25),
    ],
    "GruntGoblin": [
        T("HeroPlate", "Hero Plate", 250, "Thicker armor on the mech.", health=1.25),
        T("GruntHustle", "Grunt Hustle", 150, "The mech runs faster.", speed=1.25),
        T("BiggerGuns", "Bigger Guns", 250, "Bigger guns all round.", damage=1.3),
    ],
}
