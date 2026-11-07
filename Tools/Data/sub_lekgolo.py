# Covenant Lekgolo Colony (M10): worm colonies in armor. Tough front line and healing. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Lekgolo Colony"
FACTION = "Covenant"
FOLDER = "LekgoloColony"
DESCRIPTION = "Worm colonies in armor: a tough front line that heals itself, and scuttling Skitterers."
ORDER = 5
HUNTER_ARC = dict(deg=240, front=0.7, rear=1.5, block=0, types=[])


def units(U, W):
    return [
        U("Skitterer", "Skitterer", FACTION, "Flanker", 1, 0, 6, 150, 100, "L", 4.5, 0.7, 0.3, "B",
          [W("Spitter Plasma", 7, 0.25, 7, "Energy")],
          note="Spider-like machines worked by a few worms: a fast, light swarm of a flanker."),
        U("Goliath", "Goliath", FACTION, "Tank", 3, 250, 1, 500, 2200, "H", 1.1, 2.4, 2.5, "G",
          [W("Fist Slam", 120, 2, 3, "Melee", splash=3.5),
           W("Fuel Rod Cannon", 55, 2.5, 14, "Explosive", proj=30, splash=2, own="G")],
          arc=HUNTER_ARC, basedmg=250,
          note="Two colonies in one suit. Its old job as a building-killer shows as 2.5 times the base damage if it survives a battle."),
        U("HunterCaptain", "Hunter Captain", FACTION, "SingleTargetDps", 3, 225, 1, 450, 2000, "H", 1.25, 1.4, 1.5, "G",
          # The owner, D91: a held 1 s green laser. Its damage over time is the old shots' (72 every 0.75 s, 96 a second): 10 hits of 19.5
          # over the second, then 1 s of rest. A 0.3 m reach around where it burns, so it barely touches a neighbour.
          [W("Siphon Laser", 19.5, 0.1, 15, "Energy", aa=True, minr=5, splash=0.3, beam=1, charge=0, rest=1, sweep=30),
           W("Shield Bash", 120, 1.5, 2.2, "Melee", splash=1.5, own="G")],  # A charge and a shield bash close in, as the Hunters' (D87).
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
        T("HeavyFists", "Heavy Fists", 250, "Its weapons hit harder.", damage=1.3),
    ],
    "HunterCaptain": [
        T("BondArmor", "Bond Armor", 250, "Thicker plates over the colony.", health=1.25),
        T("DeepSiphon", "Deep Siphon", 250, "The laser bites deeper.", damage=1.25),
        T("LongSiphon", "Long Siphon", 150, "The laser reaches further.", range=4),
    ],
}
