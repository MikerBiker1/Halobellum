# UNSC Noble (M10). Spartans in specialist roles, all Tier 3, so each costs 200 to 250 to unlock: expensive, as the Bible asks.
# They all have the Spartan's lot: Unarmored body, regenerating shield, cannot be infected. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Noble"
FACTION = "UNSC"
FOLDER = "Noble"
DESCRIPTION = "Spartan specialists. Every unit is Tier 3 and expensive to unlock."
ORDER = 1
# They share the Spartan's model, each in its own armour colour and with a placeholder weapon (Resources/BakedBodies).


def units(U, W):
    return [
        U("SpartanSniper", "Spartan Sniper", FACTION, "SingleTargetDps", 3, 200, 1, 400, 650, "U", 1.75, 0.6, 0.5, "B",
          [W("SRS99 Sniper Rifle", 200, 3.0, 42, "Ballistic", aa=True)], shield=200, regen=60, immune=True,
          note="Kills one armored target at a time from very far away; has no answer to a crowd or to anything that reaches it.",
          baked="SpartanSniper"),
        U("SpartanHeavyWeapons", "Spartan Heavy Weapons", FACTION, "Tank", 3, 225, 1, 450, 2400, "U", 1.3, 0.8, 0.7, "B",
          [W("M247H Chaingun", 14, 0.12, 15, "Ballistic", block=True)], shield=250, regen=60, immune=True,  # No rocket pod (the owner, D132).
          note="A walking wall with a chaingun: mows down infantry, does little to armor. Its Unarmored body makes it weak to anti-infantry weapons once the shield is down.",
          baked="SpartanHeavyWeapons"),
        U("SpartanGrenadier", "Spartan Grenadier", FACTION, "ChaffClear", 3, 200, 1, 400, 1000, "U", 1.75, 0.6, 0.5, "G",
          [W("M41 Rocket Launcher", 62, 1.5, 19, "Explosive", proj=22, splash=3.2, minr=3)], shield=300, regen=60, immune=True,
          note="Rockets in a straight line: strong against crowds, weak against tanks and flankers.",
          baked="SpartanGrenadier"),
        U("SpartanCQB", "Spartan CQB", FACTION, "Flanker", 3, 200, 1, 400, 1200, "U", 3.5, 0.6, 0.35, "B",
          [W("M45 Shotgun", 80, 0.8, 6, "Ballistic", cone=25, block=True)], shield=450, regen=80, immune=True,  # No leap: it stops at its range and shoots (the owner, 2026-11-11; D130).
          note="Quick on its feet with a shotgun: it runs up to its short range and fires. Very strong against artillery and DPS once there.",
          baked="SpartanCQB"),
        U("SpartanFireteam", "Spartan Fireteam", FACTION, "SingleTargetDps", 3, 250, 3, 500, 500, "U", 1.75, 0.6, 0.5, "B",
          [W("BR85 Battle Rifle", 22, 0.6, 16, "Ballistic", block=True),
           W("M41 Rocket Launcher", 110, 5, 18, "Explosive", proj=40, aa=True, own="G")], shield=140, regen=40, immune=True,
          note="Three Spartans: an all-round squad with rifles and a rocket each. No single strength, no single weakness.",
          baked="SpartanFireteam"),
    ]


TECHS = {
    "SpartanSniper": [
        T("Overshield", "Overshield", 200, "A stronger energy shield.", shield=200),
        T("StabilizedScope", "Stabilized Scope", 250, "Faster, steadier shots.", fire=1.25),
    ],
    "SpartanHeavyWeapons": [
        T("Overshield", "Overshield", 250, "A stronger energy shield.", shield=300),
        T("ExtendedBelt", "Extended Belt", 200, "More bullets downrange.", fire=1.25),
        T("ArmorLock", "Reinforced Plates", 250, "Heavier plates under the shield.", health=1.25),
    ],
    "SpartanGrenadier": [
        T("Overshield", "Overshield", 200, "A stronger energy shield.", shield=200),
        T("ClusterGrenades", "Cluster Warheads", 200, "Each rocket covers more ground.", splash=1.2),
    ],
    "SpartanCQB": [
        T("Overshield", "Overshield", 200, "A stronger energy shield.", shield=200),
        T("ThrusterPack", "Better Thrusters", 150, "Closes the distance faster.", speed=1.25),
        T("WideChoke", "Wide Choke", 150, "The shotgun's spread grows.", cone=20),
    ],
    "SpartanFireteam": [
        T("Overshield", "Overshield", 250, "A stronger energy shield on all three.", shield=100),
        T("ArmorPiercing", "Armor-Piercing Rounds", 250, "Heavier weapons all round.", damage=1.25),
    ],
}
