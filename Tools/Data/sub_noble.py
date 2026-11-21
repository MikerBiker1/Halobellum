# UNSC Noble (M10). Spartans in specialist roles, all Tier 3, so each costs 200 to 250 to unlock: expensive, as the Bible asks.
# They all have the Spartan's lot: Unarmored body, regenerating shield, cannot be infected. Numbers are first-pass values (Docs/M10/subfactions.md).
from techs import T

NAME = "Noble"
FACTION = "UNSC"
FOLDER = "Noble"
DESCRIPTION = "Spartan specialists. Every unit is Tier 3 and expensive to unlock."
ORDER = 1
# They share the Spartan's model, each in its own armour colour and with a placeholder weapon (Art/BakedBodies).


def units(U, W):
    return [
        U("SpartanSniper", "Spartan Sniper", FACTION, "SingleTargetDps", 3, 200, 1, 400, 650, "U", 1.75, 0.6, 0.5, "B",
          # Every 3 s -> 2.5 s (the owner, D155). Then 2 shots a second from a 4-round magazine, a 2 s reload after the fourth, at half the damage, 154 -> 77 (D157).
          # Then 70 damage and a 2.5 s reload (the owner, D158), then 72 and 2.4 s (D159); 76 in the balancing the owner left to me (D163).
          [W("SRS99 Sniper Rifle", 76, 0.5, 42, "Ballistic", aa=True, mag=(4, 2.4))], shield=200, regen=60, immune=True,
          note="Kills one armored target at a time from very far away; has no answer to a crowd or to anything that reaches it.",
          baked="SpartanSniper"),
        # Light armor under its shield (the owner, D160; unarmored until then).
        U("SpartanHeavyWeapons", "Spartan Heavy Weapons", FACTION, "Tank", 3, 200, 1, 400, 1930, "L", 1.3, 0.8, 0.7, "B",
          # Chaingun 13 -> 14 (D163).
          [W("M247H Chaingun", 14, 0.12, 20, "Ballistic", block=True)], shield=250, regen=60, immune=True,  # No rocket pod (the owner, D132); after it the chaingun 14 -> 18 damage and 15 -> 20 m (the pod's reach), and 450 -> 400, unlocked for half that (D133); chaingun 16 and health 2,200 in the balance pass (D138); chaingun 15 -> 13, as it grew too strong with levels (the owner, D142).
          note="A walking wall with a chaingun: mows down infantry, does little to armor. Under its shield, light armor.",
          baked="SpartanHeavyWeapons"),
        # 400 -> 350 credits, under the Tier 3 band (the owner, D162).
        U("SpartanGrenadier", "Spartan Grenadier", FACTION, "ChaffClear", 3, 200, 1, 350, 980, "U", 1.75, 0.6, 0.5, "G",
          # 2 rockets a second from its 2-rocket launcher, then a 1.2 s reload (the owner, D157; was one every 1.5 s).
          # Then a 2 s reload (the owner, D158), and 61 -> 65 damage (D159).
          [W("M41 Rocket Launcher", 65, 0.5, 19, "Explosive", proj=22, splash=3.2, minr=3, mag=(2, 2))], shield=300, regen=60, immune=True,
          note="Rockets in a straight line: strong against crowds, weak against tanks and flankers.",
          baked="SpartanGrenadier"),
        U("SpartanCQB", "Spartan CQB", FACTION, "Flanker", 3, 150, 1, 300, 1160, "U", 3.5, 0.6, 0.35, "G",
          [W("M45 Shotgun", 150, 0.8, 6, "Ballistic", cone=25, block=True)], shield=600, regen=80, immune=True, towers=False,  # No leap: it stops at its range and shoots (D130).
          # A shotgun can't reach aircraft, so it shoots ground only, and is stronger for it: 300 (was 400; under the Tier 3 band, the owner's choice) and unlocked for 150, shotgun 100 (80), shield 600 (450) (the owner, D133); shotgun 120 after the tournament (D133).
          # Shotgun 120 -> 150 (the owner, D157).
          note="Quick on its feet with a shotgun: it runs up to its short range and fires. Very strong against artillery and DPS once there; it can't shoot aircraft.",
          baked="SpartanCQB"),
        U("SpartanFireteam", "Spartan Fireteam", FACTION, "SingleTargetDps", 3, 250, 3, 500, 560, "U", 1.75, 0.6, 0.5, "B",
          [W("BR85 Battle Rifle", 33, 0.6, 16, "Ballistic", block=True),  # 31 -> 33 (D163).
           W("M41 Rocket Launcher", 129, 5, 18, "Explosive", proj=40, aa=True, own="G")], shield=140, regen=40, immune=True,
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
