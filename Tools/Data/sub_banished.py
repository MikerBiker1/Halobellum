# Covenant Banished (M10): brutal, heavy Brute machines. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Banished"
FACTION = "Covenant"
FOLDER = "Banished"
DESCRIPTION = "Brute war machines: ramming Choppers, heavy armor and flying artillery."
ORDER = 4


def units(U, W):
    return [
        U("Chopper", "Chopper", FACTION, "Flanker", 1, 0, 2, 200, 300, "L", 5.5, 1.3, 0.8, "G",
          [W("Ramming Blades", 60, 1.0, 1.5, "Melee", splash=1.5),
           W("Chopper Autocannons", 10, 0.25, 9, "Ballistic", block=True, own="B")],
          note="It rams: its main weapon is its front wheel, so it drives straight into the enemy, guns firing on the way."),
        U("BruteGrenadier", "Brute Grenadier", FACTION, "ChaffClear", 1, 0, 4, 150, 170, "U", 1.5, 0.6, 0.3, "G",
          [W("Spike Grenade Launcher", 38, 2.0, 13, "Explosive", proj=18, splash=2.8, minr=2, lob=True)],
          note="Tough infantry with lobbed grenades. Shorter range than a Fuel Rod Grunt, can miss what moves, but takes much more killing."),
        U("Marauder", "Marauder", FACTION, "Tank", 2, 50, 1, 300, 1250, "H", 1.5, 1.8, 1.8, "G",
          [W("Plasma Cannon", 80, 2.2, 15, "Energy", proj=30, splash=2),
           W("Spike Turret", 8, 0.3, 12, "Ballistic", block=True, own="B")],
          note="The Banished battle tank. A plasma main gun, so a little better than a Scorpion against shields."),
        U("Reaver", "Reaver", FACTION, "AntiAir", 2, 50, 1, 300, 700, "L", 3, 1.4, 1.2, "B",
          [W("Spike Rockets", 24, 0.5, 20, "Explosive", proj=45, air=3)],
          note="Ground anti-air walker, weak against vehicles."),
        U("Blisterback", "Blisterback", FACTION, "Aerial", 3, 225, 1, 450, 1400, "L", 1.75, 3, 1.5, "G",
          [W("Plasma Missile Barrage", 75, 4, 30, "Explosive", proj=20, splash=3.5, minr=5, lob=True, own="G"),
           W("Plasma Turret", 10, 0.3, 12, "Energy", own="B")], airu=True,
          note="A heavy flyer that shells the ground from far away. Anti-air, not flankers, is the answer."),
    ]


TECHS = {
    "Chopper": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the Chopper.", health=1.3),
        T("Boost", "Boost Engines", 100, "Rams faster.", speed=1.25),
    ],
    "BruteGrenadier": [
        T("BrutePlate", "Brute Plate", 100, "Heavier armor.", health=1.3),
        T("SpikeClusters", "Spike Clusters", 150, "Each grenade covers more ground.", splash=1),
    ],
    "Marauder": [
        T("ArmorPlating", "Armor Plating", 200, "Extra plating on the hull.", health=1.25),
        T("OverchargedCannon", "Overcharged Cannon", 200, "Hotter plasma.", damage=1.3),
        T("AntiAirSpikes", "Flak Spikes", 150, "The turret bursts near aircraft.", air=2.0),
    ],
    "Reaver": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the walker.", health=1.3),
        T("RapidRacks", "Rapid Racks", 200, "Rockets launch faster.", fire=1.25),
    ],
    "Blisterback": [
        T("ArmorPlating", "Armor Plating", 200, "Extra plating on the hull.", health=1.3),
        T("LongBarrage", "Long Barrage", 200, "Missiles with bigger motors.", range=6),
        T("WideWarheads", "Wide Warheads", 200, "Each missile covers more ground.", splash=1),
    ],
}
