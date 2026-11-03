# First-pass tech upgrades for the slice rosters (Bible Section 9; DECISIONS D27). Imported by gen_roster.py.
# Effects: health, damage, fire (rate of fire multiplier), speed, air (damage vs air multiplier),
# range (m), shield (+points), regen, splash (m), cone (degrees), aa (can shoot air), drop (unlocks the drop).


def T(id, name, cost, desc, **fx):
    return dict(id=id, name=name, cost=cost, desc=desc, fx=fx)


TECHS = {
    # ------------------------------------------------------------------ Flood (M8, D38)
    "InfectionForms": [
        T("HardenedCarapace", "Hardened Carapace", 100, "A tougher shell on every form.", health=1.4),
        T("Scuttling", "Faster Scuttling", 100, "Longer legs, less time under fire.", speed=1.25),
    ],
    "CarrierForm": [
        T("VolatileBladder", "Volatile Bladder", 120, "A much bigger explosion.", damage=1.4),
        T("ThickHide", "Thick Hide", 100, "Keeps walking through more fire.", health=1.4),
    ],
    "CombatForm": [
        T("BoneBlades", "Bone Blades", 150, "Sharpened claws for the front line.", damage=1.3),
        T("DenseBiomass", "Dense Biomass", 120, "Takes much more to put down.", health=1.3),
        T("Sprint", "Reflex Sprint", 100, "Charges faster.", speed=1.25),
    ],
    "StalkerForm": [
        T("Leaper", "Long Leaper", 150, "Stronger legs for a faster hunt.", speed=1.25),
        T("RazorTalons", "Razor Talons", 150, "Talons that cut through plating.", damage=1.3),
    ],
    "PureFormRanged": [
        T("LongShards", "Long Shards", 150, "Fires its shards further.", range=6),
        T("ShardCluster", "Shard Cluster", 150, "The shards burst over a wider area.", splash=1.5),
    ],
    "SwarmForm": [
        T("BonePlates", "Bone Plates", 120, "Armored wings and hide.", health=1.3),
        T("Barbed", "Barbed Shards", 150, "Shards that tear through hulls.", damage=1.3),
    ],
    "PureFormTank": [
        T("ReinforcedMass", "Reinforced Mass", 200, "Even more biomass.", health=1.3),
        T("WideSlam", "Wide Slam", 150, "Its slam reaches further.", splash=1.5),
    ],
    "Seeder": [
        T("FastSpores", "Fast Spores", 200, "Fires seeds more often.", fire=1.4),
        T("LongSpores", "Long Spores", 150, "Reaches hosts further away.", range=5),
    ],
    "InfectedPelican": [
        T("ReinforcedHull", "Infested Hull", 150, "More likely to reach the crowd.", health=1.4),
        T("HotEngines", "Overburnt Engines", 120, "Dives faster.", speed=1.3),
    ],
    "InfectedPhantom": [
        T("ArmorPlating", "Grown Armor", 150, "Biomass plates over the hull.", health=1.3),
        T("Gunners", "Infested Gunners", 150, "The turrets fire harder.", damage=1.3),
    ],
    "Abomination": [
        T("TendrilMass", "Tendril Mass", 250, "Thicker, heavier tendrils.", damage=1.3),
        T("WarForm", "War Form", 250, "An even larger body.", health=1.3),
    ],
    # ------------------------------------------------------------------ UNSC
    "Marine": [
        T("BodyArmor", "Body Armor", 100, "Ceramic plates for every Marine.", health=1.3),
        T("ExtendedMagazines", "Extended Magazines", 120, "Fewer reloads, more bullets downrange.", fire=1.25),
    ],
    "FlamethrowerTrooper": [
        T("WideNozzle", "Wide Nozzle", 150, "Sprays a much wider cone of fire.", cone=30),
        T("NapalmMix", "Napalm Mix", 150, "A stickier, hotter fuel.", damage=1.3),
        T("FireSuits", "Fire-Resistant Suits", 100, "Heavier protective suits.", health=1.3),
    ],
    "Warthog": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the hull.", health=1.3),
        T("LongBarrel", "Long-Barrel Gun", 150, "The gunner reaches further.", range=3),
    ],
    "GaussWarthog": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the hull.", health=1.3),
        T("Capacitors", "Capacitor Upgrade", 200, "Faster gauss charging.", fire=1.25),
    ],
    "Scorpion": [
        T("ReactiveArmor", "Reactive Armor", 200, "Explosive plates break incoming hits.", health=1.25),
        T("CanisterShell", "Canister Shell", 200, "Wider blast against infantry.", splash=1.5),
        T("AntiAirCoax", "Anti-Air Coax", 150, "Flak rounds for the coaxial gun.", air=2.0),
    ],
    "Wolverine": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the hull.", health=1.3),
        T("LongRangeRockets", "Long-Range Rockets", 200, "Rockets with bigger motors.", range=6),
        T("RapidReload", "Rapid Reload", 200, "Faster volley reloads.", fire=1.25),
    ],
    "Hornet": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the airframe.", health=1.3),
        T("MissilePods", "Missile Pods", 200, "Heavier rockets and bombs.", damage=1.3),
    ],
    "ArcherMissilePod": [
        T("ExtendedRange", "Extended Range", 200, "Longer-range missiles.", range=8),
        T("ClusterWarheads", "Cluster Warheads", 200, "Each missile covers more ground.", splash=1.5),
    ],
    "Pelican": [
        T("TroopBay", "Troop Bay", 200, "Carries 6 Marines and drops them 10 seconds into the battle.", drop=True),
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the airframe.", health=1.3),
        T("HeavyAutocannon", "Heavy Autocannon", 150, "Heavier rounds for the chin gun.", damage=1.3),
    ],
    "Spartan": [
        T("Overshield", "Overshield", 250, "A stronger energy shield.", shield=300),
        T("LaserCapacitor", "Laser Capacitor", 250, "The Spartan Laser charges faster.", fire=1.25),
    ],
    "Mantis": [
        T("ArmorPlating", "Armor Plating", 250, "Extra plating on the walker.", health=1.25),
        T("RocketPods", "Rocket Pods", 250, "Heavier weapons all round.", damage=1.3),
    ],
    "Elephant": [
        T("ReinforcedHull", "Reinforced Hull", 300, "Even thicker armor.", health=1.25),
        T("HeavyCannon", "Heavy Cannon", 300, "Bigger guns all round.", damage=1.3),
    ],
    # ------------------------------------------------------------------ Covenant
    "Grunt": [
        T("MethaneTanks", "Reinforced Methane Tanks", 100, "Sturdier armor and tanks.", health=1.3),
        T("Overcharge", "Pistol Overcharge", 120, "Pistols fire faster.", fire=1.25),
    ],
    "FuelRodGrunt": [
        T("ExtendedRange", "Extended Range", 150, "Longer-range fuel rods.", range=4),
        T("HeavyRods", "Heavy Rods", 150, "Bigger fuel rod blasts.", damage=1.3),
    ],
    "JackalPlasmaRifle": [
        T("Gauntlets", "Point-Defense Gauntlets", 100, "Small shields on the arms.", health=1.3),
        T("Overcharge", "Rifle Overcharge", 150, "Plasma rifles fire faster.", fire=1.25),
    ],
    "JackalShield": [
        T("StrongerShields", "Stronger Shields", 100, "Tougher Jackals behind tougher shields.", health=1.3),
        T("Overcharge", "Pistol Overcharge", 120, "Pistols fire faster.", fire=1.25),
    ],
    "Ghost": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the Ghost.", health=1.3),
        T("Boost", "Boost Engines", 100, "Faster Ghosts.", speed=1.25),
    ],
    "Elite": [
        T("ShieldUpgrade", "Shield Upgrade", 200, "Stronger energy shields.", shield=150),
        T("PlasmaOvercharge", "Plasma Overcharge", 200, "Hotter plasma.", damage=1.25),
    ],
    "Wraith": [
        T("ExtendedRange", "Extended Range", 200, "Longer mortar arcs.", range=8),
        T("HeavyMortar", "Heavy Mortar", 200, "Bigger plasma blasts.", splash=1.5),
    ],
    "Vampire": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the airframe.", health=1.3),
        T("NeedlerOvercharge", "Needler Overcharge", 200, "Needle cannons fire faster.", fire=1.25),
    ],
    "Banshee": [
        T("ArmorPlating", "Armor Plating", 150, "Extra plating on the airframe.", health=1.3),
        T("FuelRodUpgrade", "Fuel Rod Upgrade", 200, "Heavier bombs and cannons.", damage=1.3),
    ],
    "HunterPair": [
        T("AssaultCannon", "Assault Cannon Upgrade", 250, "Bigger fuel rod cannons.", damage=1.3),
        T("ReinforcedShields", "Reinforced Shields", 250, "Thicker bond shields.", health=1.25),
    ],
    "Zealot": [
        T("ShieldUpgrade", "Shield Upgrade", 250, "A much stronger energy shield.", shield=300),
        T("SpeedBoost", "Speed Boost", 200, "Closes the distance faster.", speed=1.25),
    ],
    "Scarab": [
        T("ReinforcedHull", "Reinforced Hull", 300, "Even thicker armor.", health=1.25),
        T("FocusBeamUpgrade", "Focus Beam Upgrade", 300, "Stronger weapons all round.", damage=1.3),
    ],
}


# Every fighting unit has 3 techs (the owner, 2026-11-02; D54). These are the third ones, first-pass: each does something its other two don't,
# and costs about what they cost. gen_roster.py adds them after the sub-factions' own.
THIRD_TECHS = {
    # UNSC
    "Marine": T("ArmorPiercing", "Armor-Piercing Rounds", 120, "Heavier rounds for the MA5.", damage=1.25),
    "Warthog": T("HeavyRounds", "Heavy Rounds", 150, "Harder-hitting machine gun ammunition.", damage=1.3),
    "GaussWarthog": T("LongRails", "Long Rails", 200, "Longer rails reach further.", range=4),
    "Hornet": T("Afterburners", "Afterburners", 120, "Gets over the target faster.", speed=1.25),
    "ArcherMissilePod": T("ArmoredLauncher", "Armored Launcher", 150, "Plating around the pod.", health=1.3),
    "Spartan": T("Thrusters", "Better Thrusters", 200, "Moves up faster.", speed=1.2),
    "Mantis": T("TargetingArray", "Targeting Array", 250, "Everything reaches further.", range=4),
    "Elephant": T("Autoloaders", "Autoloaders", 300, "Every gun fires faster.", fire=1.2),
    # Covenant
    "Grunt": T("HotPlasma", "Hot Plasma", 100, "Hotter pistol bolts.", damage=1.25),
    "FuelRodGrunt": T("Harness", "Reinforced Harness", 100, "Armor around the gun and tanks.", health=1.3),
    "JackalPlasmaRifle": T("MarksmanOptics", "Marksman Optics", 120, "Jackal eyes and a better sight reach further.", range=3),
    "JackalShield": T("HotPlasma", "Hot Plasma", 120, "Hotter pistol bolts.", damage=1.25),
    "Ghost": T("PlasmaOvercharge", "Plasma Overcharge", 150, "Hotter plasma bolts.", damage=1.25),
    "Elite": T("CombatStride", "Combat Stride", 150, "Closes the distance faster.", speed=1.2),
    "Wraith": T("ArmorPlating", "Armor Plating", 200, "Extra plating on the hull.", health=1.25),
    "Vampire": T("LongNeedlers", "Long Needlers", 150, "Needles that fly further.", range=3),
    "Banshee": T("Boost", "Boost Engines", 120, "Gets over the target faster.", speed=1.25),
    "HunterPair": T("LongRods", "Long Fuel Rods", 200, "The cannon reaches further.", range=4),
    "Zealot": T("HonedBlade", "Honed Blade", 200, "A hotter energy sword.", damage=1.25),
    "Scarab": T("LongLens", "Long Focus Lens", 300, "Everything reaches further.", range=4),
    # Flood
    "InfectionForms": T("Stingers", "Barbed Stingers", 100, "A deeper bite.", damage=1.3),
    "CarrierForm": T("LongStride", "Long Stride", 100, "Reaches the crowd sooner.", speed=1.25),
    "StalkerForm": T("ChitinPlates", "Chitin Plates", 120, "Plates grown over the body.", health=1.3),
    "PureFormRanged": T("RapidShards", "Rapid Shards", 150, "Grows and fires its shards faster.", fire=1.25),
    "SwarmForm": T("SwiftWings", "Swift Wings", 120, "Faster through the air.", speed=1.25),
    "PureFormTank": T("CrushingMass", "Crushing Mass", 200, "A heavier slam.", damage=1.3),
    "Seeder": T("ThickSac", "Thick Spore Sac", 150, "More to shoot through.", health=1.3),
    "InfectedPelican": T("VolatileCargo", "Volatile Cargo", 200, "A harder blast where it comes down.", damage=1.25),
    "InfectedPhantom": T("HotEngines", "Overburnt Engines", 120, "Reaches the fight faster.", speed=1.25),
    "Abomination": T("SurgingMass", "Surging Mass", 200, "It drags itself along faster.", speed=1.2),
    # Noble
    "SpartanSniper": T("ArmorPiercing", "Armor-Piercing Rounds", 250, "Heavier sniper rounds.", damage=1.25),
    "SpartanGrenadier": T("RapidLoader", "Rapid Loader", 200, "Grenades go out faster.", fire=1.25),
    "SpartanFireteam": T("Thrusters", "Better Thrusters", 200, "Moves up faster.", speed=1.2),
    # Armored Division
    "Gremlin": T("LongRails", "Long Rails", 150, "The EMP railgun reaches further.", range=3),
    "Rhino": T("Autoloader", "Autoloader", 250, "The cannon fires faster.", fire=1.25),
    # Aerospace Command
    "Sparrowhawk": T("Afterburners", "Afterburners", 120, "Gets over the target faster.", speed=1.25),
    "Vulture": T("LongMissiles", "Long-Range Missiles", 200, "Everything reaches further.", range=4),
    "CondorGunship": T("HeavyRounds", "Heavy Rounds", 200, "Harder-hitting autocannon shells.", damage=1.25),
    # Banished
    "Chopper": T("SharpBlades", "Sharpened Blades", 150, "Harder ramming and hotter guns.", damage=1.3),
    "BruteGrenadier": T("RapidLauncher", "Rapid Launcher", 120, "Spike grenades go out faster.", fire=1.25),
    "Reaver": T("LongSpikes", "Long Spikes", 150, "Rockets with bigger motors.", range=4),
    # Lekgolo Colony
    "Skitterer": T("HotSpit", "Hot Spit", 100, "Hotter plasma.", damage=1.25),
    "Goliath": T("BondStride", "Bond Stride", 200, "It closes the distance faster.", speed=1.2),
    # Unggoy Rebellion
    "CannonFodder": T("PanicSprint", "Panic Sprint", 100, "Nobody wants to be last.", speed=1.25),
    "HeavyGrunts": T("HeavyNeedles", "Heavy Needles", 150, "Bigger needles.", damage=1.25),
    "MethaneWagon": T("FastPump", "Faster Pump", 150, "The mortar fires more often.", fire=1.25),
    # Flood, UNSC-based
    "InfestedMarine": T("BoneBlades", "Bone Blades", 100, "A sharper scythe limb.", damage=1.3),
    "InfestedWarthog": T("RamSpikes", "Ram Spikes", 150, "Bone through the bumper.", damage=1.3),
    "InfestedScorpion": T("OvergrownCannon", "Overgrown Cannon", 200, "Harder hits all round.", damage=1.3),
    "InfestedHornet": T("HotEngines", "Overburnt Engines", 120, "Barely flying, but faster.", speed=1.25),
    # Flood, Covenant-based
    "InfestedGrunt": T("BoneBlades", "Bone Blades", 100, "Sharper claws.", damage=1.3),
    "InfestedGhost": T("RamSpikes", "Ram Spikes", 150, "Bone through the hull.", damage=1.3),
    "InfestedWraith": T("LongArc", "Long Arc", 200, "The mortar reaches further.", range=4),
    "InfestedBanshee": T("HotEngines", "Overburnt Engines", 120, "Barely flying, but faster.", speed=1.25),
    "Spawner": T("BarbedLash", "Barbed Lash", 150, "A harder lash.", damage=1.3),
}
