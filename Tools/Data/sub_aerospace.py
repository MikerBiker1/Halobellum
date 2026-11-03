# UNSC Aerospace Command (M10): aircraft only. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Aerospace Command"
FACTION = "UNSC"
FOLDER = "AerospaceCommand"
DESCRIPTION = "Aircraft: gunships, bombers and a heavy transport."
ORDER = 3


def units(U, W):
    return [
        U("Sparrowhawk", "Sparrowhawk", FACTION, "Aerial", 2, 50, 1, 300, 550, "L", 4, 2, 1, "B",
          [W("Heavy Autocannon", 18, 0.3, 14, "Ballistic", aa=True)], airu=True,
          note="Gunship that hunts vehicles. Its gun is poor against infantry."),
        U("Longsword", "Longsword", FACTION, "Aerial", 2, 50, 1, 300, 600, "L", 5, 2.8, 1, "G",
          [W("Carpet Bombs", 160, 2.5, 5, "Explosive", proj=18, splash=5, own="G")], airu=True,
          note="Heavy bomber. It has to fly over its target, and cannot fight other aircraft at all."),
        U("Vulture", "Vulture", FACTION, "Aerial", 3, 250, 1, 500, 900, "H", 2.5, 3, 1.5, "B",
          [W("Missile Barrage", 55, 3, 22, "Explosive", proj=40, splash=3),
           W("Chin Gun", 10, 0.2, 14, "Ballistic", block=True)], airu=True,
          note="The heaviest gunship: heavy armor, slow, long-range missiles."),
        U("CondorGunship", "Condor Gunship", FACTION, "Aerial", 3, 225, 1, 450, 1500, "L", 3.25, 3, 1.5, "B",
          [W("Twin Autocannons", 20, 0.25, 16, "Ballistic")], airu=True, drop=("Marine", 6, 7),
          note="A gunship that also drops 6 Marines after 7 seconds, without needing a tech."),
    ]


TECHS = {
    "Sparrowhawk": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the airframe.", health=1.3),
        T("HeavyRounds", "Heavy Rounds", 200, "Heavier autocannon rounds.", damage=1.3),
    ],
    "Longsword": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the airframe.", health=1.3),
        T("WideBombBay", "Wide Bomb Bay", 200, "Each bomb covers more ground.", splash=1.5),
        T("Afterburners", "Afterburners", 100, "Spends less time under fire.", speed=1.25),
    ],
    "Vulture": [
        T("ArmorPlating", "Armor Plating", 250, "Extra plating on the airframe.", health=1.25),
        T("MissileRacks", "Missile Racks", 250, "Heavier missiles and guns.", damage=1.3),
    ],
    "CondorGunship": [
        T("ArmorPlating", "Armor Plating", 200, "Extra plating on the airframe.", health=1.3),
        T("Autoloaders", "Autoloaders", 200, "The guns fire faster.", fire=1.25),
    ],
}
