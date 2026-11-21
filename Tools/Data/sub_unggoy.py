# Covenant Unggoy Rebellion (M10): Grunts everywhere, and methane. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Unggoy Rebellion"
FACTION = "Covenant"
FOLDER = "UnggoyRebellion"
DESCRIPTION = "Grunts, and more Grunts: huge cheap squads, burning methane, a hero's mech."
ORDER = 6


def units(U, W):
    return [
        U("CannonFodder", "Cannon Fodder", FACTION, "Chaff", 1, 0, 30, 100, 45, "U", 1.75, 0.4, 0.25, "B",
          [W("Plasma Pistol", 3.5, 0.5, 9, "Energy")], tint=(0.72, 0.6, 0.42, 0.35), scale=0.9, baked="Grunt", gap=1,  # 1 m apart (the owner, D157).
          note="The largest squad in the game: 30 Grunts who barely fight. They are there to be shot instead of something else."),
        # Needles are ballistic, not energy (the owner, D150). Then 10 damage (9.5), no shield (was 30, 10 a second) and health 110 -> 150 (the owner, D151).
        # Damage 10 -> 11 (the owner, D155). Cannon Fodder above: 3 -> 3.5 (D155).
        U("HeavyGrunts", "Heavy Grunts", FACTION, "SingleTargetDps", 1, 0, 8, 200, 150, "U", 1.5, 0.55, 0.3, "B",
          [W("Needle Rifle", 11, 0.25, 12, "Ballistic")], tint=(0.75, 0.22, 0.2, 0.4), scale=1.1, baked="Grunt",
          note="Grunts in heavy armor with a small shield, at the top of the Tier 1 price band."),
        U("GruntGoblin", "Grunt Goblin", FACTION, "Tank", 3, 200, 1, 400, 2400, "H", 2, 1.5, 1, "B",
          # Needle cannons like the Vampire's, with its sound and look: explosive with a 0.5 m splash (plasma cannons, energy, until D151, the owner).
          # Its fuel rod gone; the needles 10 -> 14 with a 1 m splash (0.5) and 16 m range (14) (the owner, D157).
          # Needles 14 -> 13.5 (D163): at 13 Unggoy fell to 47%, at 14 it stood at 53%; the Goblin is Unggoy's answer to aircraft.
          [W("Goblin Needle Cannons", 13.5, 0.2, 16, "Explosive", splash=1)], aura=(14, 1.2, 0.006), baked="GruntGoblin",
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
