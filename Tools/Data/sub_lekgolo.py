# Covenant Lekgolo Colony (M10): worm colonies in armor. Tough front line and healing. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Lekgolo Colony"
FACTION = "Covenant"
FOLDER = "LekgoloColony"
DESCRIPTION = "Worm colonies in armor: a tough front line that heals itself, and scuttling Skitterers."
ORDER = 5
HUNTER_ARC = dict(deg=240, front=0.7, rear=1.0, block=0, types=[])  # No weak back (the owner, D155): rear x1.5 -> x1.
# The Goliath keeps the Hunters' tougher front but has no weak back: it is no Hunter (the owner, D151).
GOLIATH_ARC = dict(deg=240, front=0.7, rear=1.0, block=0, types=[])


# Base roster units at this sub-faction's own price (the owner, D158): the Hunter Pair 450 -> 400.
COSTS = {"HunterPair": 400}


def units(U, W):
    return [
        # Health 60 -> 80 a body (the owner, D158).
        U("Skitterer", "Skitterer", FACTION, "Flanker", 1, 0, 6, 150, 80, "L", 4.5, 0.7, 0.3, "B",
          [W("Spitter Plasma", 7, 0.25, 12, "Energy")],  # 7 -> 12 m (the owner, D142).
          note="Spider-like machines worked by a few worms: a fast, light swarm of a flanker."),
        U("Goliath", "Goliath", FACTION, "Tank", 3, 250, 1, 500, 2400, "H", 1.1, 2.4, 2.5, "G",
          # Melee only, with a charge like the Hunters' (the owner, D142): no Fuel Rod Cannon (was 55 every 2.5 s, 14 m), the slam 120 -> 240
          # to make up for it. Its charge reaches 6 m from its centre: about the Hunters' 3.8 m beyond the body, on its 2.4 m radius.
          [W("Fist Slam", 240, 2, 3, "Melee", splash=3.5)],
          arc=GOLIATH_ARC, basedmg=250, leap=(6, 9, 3), charges=True, baked="Goliath",
          note="Two colonies in one suit, fighting with its fists: it charges at what comes near, as the Hunters do. Its old job as a building-killer shows as 2.5 times the base damage if it survives a battle."),
        U("HunterCaptain", "Hunter Captain", FACTION, "SingleTargetDps", 3, 225, 1, 450, 2000, "H", 1.25, 1.4, 1.5, "B",  # Its laser reaches aircraft (the owner, D148).
          # The owner, D91: a held 1 s green laser. Its damage over time is the old shots' (72 every 0.75 s, 96 a second): 10 hits of 19.5
          # over the second, then 1 s of rest. A 0.3 m reach around where it burns, so it barely touches a neighbour.
          # The laser held 2 s, then 1 s of rest (the owner, D155; was 1 s).
          [W("Siphon Laser", 15.5, 0.1, 15, "Energy", aa=True, minr=5, splash=0.3, beam=2, charge=0, rest=1, sweep=30),
           W("Shield Bash", 90, 1.5, 2.2, "Melee", splash=1.5, own="G")],  # A charge and a shield bash close in, as the Hunters' (D87).
          arc=HUNTER_ARC, lifesteal=0.3, aura=(12, 1, 0.01), leap=(5, 9, 3), charges=True, baked="HunterCaptain",
          note="Its siphon laser gives it back 30% of the damage it deals, and friends within 12 m heal 1% per second."),
    ]


TECHS = {
    "Skitterer": [
        T("ArmorPlating", "Armor Plating", 100, "Extra plating on each machine.", health=1.3),
        T("Scuttling", "Faster Scuttling", 100, "Longer legs, less time under fire.", speed=1.25),
    ],
    "Goliath": [
        T("BondArmor", "Bond Armor", 250, "Thicker plates over the colony.", health=1.25),
        T("HeavyFists", "Heavy Fists", 250, "Its fists hit harder.", damage=1.3),
    ],
    "HunterCaptain": [
        T("BondArmor", "Bond Armor", 250, "Thicker plates over the colony.", health=1.25),
        T("DeepSiphon", "Deep Siphon", 250, "The laser bites deeper.", damage=1.25),
        T("LongSiphon", "Long Siphon", 150, "The laser reaches further.", range=4),
    ],
}
