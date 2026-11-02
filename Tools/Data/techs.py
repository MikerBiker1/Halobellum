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
