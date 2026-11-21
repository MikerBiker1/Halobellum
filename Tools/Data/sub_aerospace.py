# UNSC Aerospace Command (M10): aircraft only. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Aerospace Command"
FACTION = "UNSC"
FOLDER = "AerospaceCommand"
DESCRIPTION = "Aircraft: gunships, bombers and a heavy transport."
ORDER = 3


def units(U, W):
    return [
        # Its two miniguns only (the owner, D153): each fires 15 times a second, both at once, one from each gun pod (Muzzle_0a, Muzzle_0b);
        # ballistic, not anti-armor. Was a Heavy Autocannon, 18 every 0.3 s, anti-armor. The owner: 2 damage each (60 a second for the two),
        # raised 0.5 at a time if losing the anti-armor makes it weaker. 0.0666 s, just under two ticks, so each fires every second tick.
        U("Sparrowhawk", "Sparrowhawk", FACTION, "Aerial", 2, 50, 1, 250, 600, "L", 4, 2, 1, "B",
          [W("Left Minigun", 3.5, 0.0666, 14, "Ballistic"), W("Right Minigun", 3.5, 0.0666, 14, "Ballistic")], airu=True,
          note="Gunship with two miniguns firing together, 15 rounds a second each: it shreds infantry and light vehicles, less so heavy armor."),
        # Health 830 -> 1000 (the owner, D153).
        U("Longsword", "Longsword", FACTION, "Aerial", 2, 50, 1, 300, 1000, "L", 5, 2.8, 1, "G",
          [W("Carpet Bombs", 180, 2.5, 5, "Explosive", proj=18, splash=5, own="G")], airu=True,
          note="Heavy bomber. It has to fly over its target, and cannot fight other aircraft at all."),
        U("Vulture", "Vulture", FACTION, "Aerial", 3, 200, 1, 400, 1080, "H", 2.5, 3, 1.5, "B",
          [W("Missile Barrage", 69, 3, 22, "Explosive", proj=40, splash=3),
           W("Chin Gun", 10, 0.2, 22, "Ballistic", block=True)], airu=True,  # Range 14 -> 22, the missiles' (the owner, D153).
          note="The heaviest gunship: heavy armor, slow, long-range missiles."),
        # D111 (the owner, 2026-11-08): its five gun pods are guns, each a light Spartan Laser on a turret of its own, finding its own target;
        # 300 damage per 4 s across the five, so it isn't too strong (to be balanced in tournaments). No Marine drop any more.
        # Health 1130 -> 1200 (the owner, D161).
        U("CondorGunship", "Condor Gunship", FACTION, "Aerial", 3, 250, 1, 500, 1200, "L", 3.25, 3, 1.5, "B",
          [W("Twin Autocannons", 22, 0.2, 16, "Explosive", splash=0.5),  # Ballistic -> explosive with a 0.5 m splash (the owner, D153); 24 -> 22 (D163).
           # The five fire one after another, 0.8 s apart over their 4 s reload (the owner, 2026-11-08; D114).
           # Each laser 19.5 -> 60, so it deals like the other 500-credit units (the owner, D142).
           W("Light Spartan Laser (nose)", 60, 3.5, 20, "Energy", aa=True, windup=2.5),
           W("Light Spartan Laser (front right)", 60, 3.5, 20, "Energy", aa=True, windup=2.5, stagger=0.8),
           W("Light Spartan Laser (front left)", 60, 3.5, 20, "Energy", aa=True, windup=2.5, stagger=1.6),
           W("Light Spartan Laser (rear right)", 60, 3.5, 20, "Energy", aa=True, windup=2.5, stagger=2.4),
           W("Light Spartan Laser (rear left)", 60, 3.5, 20, "Energy", aa=True, windup=2.5, stagger=3.2)], airu=True,
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
