# UNSC Armored Division (M10): heavy ground vehicles. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Armored Division"
FACTION = "UNSC"
FOLDER = "ArmoredDivision"
DESCRIPTION = "Heavy ground vehicles: the biggest tank, long guns and fast EMP scouts."
ORDER = 2


def units(U, W):
    return [
        U("Gremlin", "Gremlin", FACTION, "Flanker", 1, 0, 2, 200, 240, "L", 4.5, 1.1, 0.9, "B",
          [W("EMP Railgun", 38, 1.0, 13, "Energy", aa=True, slow=2, slowshare=0.4)],
          note="Fast scout whose EMP slows what it hits (Titans shrug it off). Good against vehicles, poor against infantry."),
        U("Cobra", "Cobra", FACTION, "SingleTargetDps", 2, 50, 1, 300, 650, "H", 1.75, 1.5, 1.5, "G",
          [W("M66 Railgun", 175, 2.2, 26, "Ballistic", aa=True)],
          note="Six-wheeled tank hunter: its twin light railguns punch through heavy armor from far off. Outranges tanks; a swarm or a flanker gets under its guns."),
        U("Rhino", "Rhino", FACTION, "Artillery", 3, 200, 1, 400, 1000, "H", 1, 1.8, 2.5, "G",
          [W("Heavy Cannon", 140, 5, 50, "Explosive", proj=22, splash=4.5, minr=12, lob=True)],
          note="The longest gun in the game. Slow to turn and move, lobbed so it can miss, helpless up close."),
        U("Grizzly", "Grizzly", FACTION, "Tank", 3, 250, 1, 500, 2100, "H", 1.35, 2.2, 2.2, "G",
          [W("Twin Cannons", 110, 2.5, 17, "Explosive", proj=35, splash=2.5),
           W("Coaxial Machine Gun", 8, 0.2, 13, "Ballistic", block=True, own="B")],
          note="The upgraded Scorpion: twin barrels and more armor, at a Tier 3 price."),
    ]


TECHS = {
    "Gremlin": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the hull.", health=1.3),
        T("WideCapacitors", "Wide Capacitors", 150, "Faster EMP shots.", fire=1.25),
    ],
    "Cobra": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the hull.", health=1.3),
        T("Capacitors", "Capacitor Upgrade", 200, "Faster railgun charging.", fire=1.25),
        T("LongBarrel", "Long Barrel", 150, "A longer railgun.", range=4),
    ],
    "Rhino": [
        T("ArmorPlating", "Armor Plating", 200, "Extra plating on the hull.", health=1.3),
        T("HighExplosive", "High-Explosive Shells", 250, "Each shell covers more ground.", splash=1.5),
    ],
    "Grizzly": [
        T("ReactiveArmor", "Reactive Armor", 250, "Explosive plates break incoming hits.", health=1.25),
        T("CanisterShell", "Canister Shell", 200, "Wider blast against infantry.", splash=1.5),
        T("AntiAirCoax", "Anti-Air Coax", 150, "Flak rounds for the coaxial gun.", air=2.0),
    ],
}
