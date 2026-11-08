# UNSC Aerospace Command (M10): aircraft only. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Aerospace Command"
FACTION = "UNSC"
FOLDER = "AerospaceCommand"
DESCRIPTION = "Aircraft: gunships, bombers and a heavy transport."
ORDER = 3


def units(U, W):
    return [
        U("Sparrowhawk", "Sparrowhawk", FACTION, "Aerial", 2, 50, 1, 275, 550, "L", 4, 2, 1, "B",
          [W("Heavy Autocannon", 18, 0.3, 14, "Ballistic", aa=True)], airu=True,
          note="Gunship that hunts vehicles. Its gun is poor against infantry."),
        U("Longsword", "Longsword", FACTION, "Aerial", 2, 50, 1, 300, 600, "L", 5, 2.8, 1, "G",
          [W("Carpet Bombs", 150, 2.5, 5, "Explosive", proj=18, splash=5, own="G")], airu=True,
          note="Heavy bomber. It has to fly over its target, and cannot fight other aircraft at all."),
        U("Vulture", "Vulture", FACTION, "Aerial", 3, 200, 1, 400, 900, "H", 2.5, 3, 1.5, "B",
          [W("Missile Barrage", 50, 3, 22, "Explosive", proj=40, splash=3),
           W("Chin Gun", 10, 0.2, 14, "Ballistic", block=True)], airu=True,
          note="The heaviest gunship: heavy armor, slow, long-range missiles."),
        # D111 (the owner, 2026-11-08): its five gun pods are guns, each a light Spartan Laser on a turret of its own, finding its own target;
        # 300 damage per 4 s across the five, so it isn't too strong (to be balanced in tournaments). No Marine drop any more.
        U("CondorGunship", "Condor Gunship", FACTION, "Aerial", 3, 250, 1, 500, 1500, "L", 3.25, 3, 1.5, "B",
          [W("Twin Autocannons", 20, 0.25, 16, "Ballistic"),
           # The five fire one after another, 0.8 s apart over their 4 s reload (the owner, 2026-11-08; D114).
           W("Light Spartan Laser (nose)", 20, 4.0, 20, "Energy", aa=True, windup=2.5),
           W("Light Spartan Laser (front right)", 20, 4.0, 20, "Energy", aa=True, windup=2.5, stagger=0.8),
           W("Light Spartan Laser (front left)", 20, 4.0, 20, "Energy", aa=True, windup=2.5, stagger=1.6),
           W("Light Spartan Laser (rear right)", 20, 4.0, 20, "Energy", aa=True, windup=2.5, stagger=2.4),
           W("Light Spartan Laser (rear left)", 20, 4.0, 20, "Energy", aa=True, windup=2.5, stagger=3.2)], airu=True,
          note="A gunship: twin autocannons under its belly and five light Spartan Lasers on turrets of their own, each finding its own target."),
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
