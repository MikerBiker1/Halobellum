# Covenant Banished (M10): brutal, heavy Brute machines. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Banished"
FACTION = "Covenant"
FOLDER = "Banished"
DESCRIPTION = "Brute war machines: ramming Choppers, heavy armor and flying artillery."
ORDER = 4


def units(U, W):
    return [
        # Health 250 -> 300: it died before it reached the towers (the owner, D162); 315 (D163).
        U("Chopper", "Chopper", FACTION, "Flanker", 1, 0, 2, 175, 315, "L", 5.5, 1.3, 0.8, "G",
          [W("Ramming Blades", 66, 1.0, 1.5, "Melee", splash=1.5),  # 60 -> 66 (D163).
           W("Chopper Autocannons", 10, 0.25, 9, "Ballistic", block=True, own="B")],
          note="It rams: its main weapon is its front wheel, so it drives straight into the enemy, guns firing on the way."),
        # Health 150 -> 158 a body (D163).
        U("BruteGrenadier", "Brute Grenadier", FACTION, "ChaffClear", 1, 0, 4, 150, 158, "U", 1.5, 0.6, 0.3, "G",
          [W("Spike Grenade Launcher", 35, 2.0, 14, "Explosive", proj=18, splash=2.8, minr=2, lob=True),  # Range 13 -> 14 (the owner, D158); 32 -> 35 damage (D163).
           # The owner, 2026-11-07: too close for its grenades, it swings the Brute Shot's blade (its launcher holds fire meanwhile).
           W("Brute Shot Blade", 35, 1.5, 2, "Melee")], baked="BruteGrenadier",
          note="Tough infantry with lobbed grenades. Shorter range than a Fuel Rod Grunt, can miss what moves, but takes much more killing."),
        # Health 1350 -> 1500 (the owner, D157), 1600 (D163).
        U("Marauder", "Marauder", FACTION, "Tank", 2, 50, 1, 300, 1600, "H", 1.5, 1.8, 1.8, "G",
          [W("Plasma Cannon", 104, 2.2, 15, "Energy", proj=30, splash=2),  # 88 -> 104 (D163).
           W("Spike Turret", 8, 0.3, 12, "Ballistic", block=True, own="B")],
          note="The Banished battle tank. A plasma main gun, so a little better than a Scorpion against shields."),
        # Health 700 -> 900 (the owner, D151).
        # Heavy armor (the owner, D158; light until then).
        U("Reaver", "Reaver", FACTION, "AntiAir", 2, 50, 1, 250, 900, "H", 3, 1.4, 1.2, "B",
          [W("Spike Rockets", 25, 0.5, 20, "Explosive", proj=45, air=3)],
          note="Ground anti-air walker, weak against vehicles."),
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
}
