// AudioCueChoiceTests.cs
// Which sound goes with which real weapon and unit (M10), read from the game's own data: autocannons sound like heavy guns, a landing
// goes by the weapon that fired, and deaths and hums go by what the thing is. How they sound is for the owner's ears.
// Depends on: Halobellum.Presentation, Halobellum.Combat, Halobellum.Core, Halobellum.Units, NUnit, UnityEditor.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class AudioCueChoiceTests
    {
        private static UnitStats Unit(string path) => AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{path}.asset").ToStats();

        private static WeaponStats WeaponOf(string path, string weaponName)
        {
            foreach (WeaponStats weapon in Unit(path).Weapons)
            {
                if (weapon.Name == weaponName) return weapon;
            }
            Assert.Fail($"{path} has no {weaponName}");
            return null;
        }

        private static string Fire(string path, string weaponName, string faction) => AudioCatalog.Fire(WeaponFeel.For(WeaponOf(path, weaponName)), faction).Slot;

        private static string Landing(string path, string weaponName) => AudioCatalog.LandingOf(WeaponOf(path, weaponName))?.Slot;

        [TearDown]
        public void TearDown() => BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);

        [TestCase("UNSC/Pelican", "M370 Autocannon")]
        [TestCase("AerospaceCommand/CondorGunship", "Twin Autocannons")]
        [TestCase("AerospaceCommand/Sparrowhawk", "Heavy Autocannon")]
        [TestCase("Banished/Chopper", "Chopper Autocannons")]
        public void Autocannons_AreHeavyGuns_WithNoExplosionOnEveryHit(string path, string weaponName)
        {
            Assert.AreEqual("sfx_fire_heavybullet", Fire(path, weaponName, BattleFactions.Unsc));
            Assert.IsNull(Landing(path, weaponName));
        }

        [Test]
        public void RealCannons_StayCannons()
        {
            Assert.AreEqual("sfx_fire_cannon", Fire("UNSC/Scorpion", "M512 Cannon", BattleFactions.Unsc));
            Assert.AreEqual("sfx_fire_cannon", Fire("ArmoredDivision/Grizzly", "Twin Cannons", BattleFactions.Unsc));
        }

        [Test]
        public void TheHuntersAssaultCannon_SoundsLikeTheHeavyPlasmaItLooksLike()
        {
            Assert.AreEqual("sfx_fire_plasmaheavy", Fire("Covenant/HunterPair", "Assault Cannon", BattleFactions.Covenant));
            Assert.AreEqual(WeaponFeelKind.PlasmaHeavy, WeaponLooks.For(WeaponOf("Covenant/HunterPair", "Assault Cannon"), new UnitStats(), BattleFactions.Covenant, Team.A, false).Kind);
        }

        [TestCase("UNSC/Scorpion", "M512 Cannon", "sfx_explosion_small")]
        [TestCase("UNSC/Scorpion", "Coaxial Machine Gun", null)]
        [TestCase("UNSC/Elephant", "Main Cannon", "sfx_explosion_small")]
        [TestCase("UNSC/Elephant", "Turret (left)", null)]
        [TestCase("FloodUNSC/InfestedScorpion", "Scavenged Cannon", "sfx_explosion_small")]
        [TestCase("FloodUNSC/InfestedScorpion", "Tentacle Whip", null)]
        [TestCase("UNSC/Mantis", "Chaingun", null)]
        [TestCase("UNSC/Mantis", "Rockets", "sfx_explosion_small")]
        [TestCase("Covenant/Banshee", "Plasma Cannons", null)]
        [TestCase("Covenant/Banshee", "Fuel Rod Bomb", "sfx_explosion_small")]
        [TestCase("LekgoloColony/Goliath", "Fist Slam", null)]
        [TestCase("LekgoloColony/Goliath", "Fuel Rod Cannon", "sfx_explosion_small")]
        [TestCase("Banished/Marauder", "Plasma Cannon", "sfx_explosion_small")]
        [TestCase("Covenant/Elite", "Plasma Rifle", null)]
        public void ALanding_GoesByTheWeaponThatFired(string path, string weaponName, string expected)
        {
            Assert.AreEqual(expected, Landing(path, weaponName));
        }

        private static WeaponLook Look(string path, string weaponName, string faction) => WeaponLooks.For(WeaponOf(path, weaponName), Unit(path), faction, Team.A, false);

        [TestCase("Banished/Blisterback", "Plasma Missile Barrage")]
        [TestCase("Banished/Marauder", "Plasma Cannon")]
        public void AHeavyPlasmaShot_IsNotAPistol_AndIsBlueNotAFuelRod(string path, string weaponName)
        {
            Assert.AreEqual("sfx_fire_plasmaheavy", Fire(path, weaponName, BattleFactions.Covenant));
            Assert.AreEqual("sfx_explosion_small", Landing(path, weaponName));
            WeaponLook look = Look(path, weaponName, BattleFactions.Covenant);
            Assert.AreEqual(WeaponFeelKind.PlasmaHeavy, look.Kind);
            Assert.AreEqual(WeaponLooks.CovenantPlasma, look.Shot);
        }

        [TestCase("Banished/Blisterback", "Plasma Turret")]
        [TestCase("Covenant/Grunt", "Plasma Pistol")]
        [TestCase("Covenant/Banshee", "Plasma Cannons")]
        public void SmallPlasmaGuns_StaySmall(string path, string weaponName)
        {
            Assert.AreEqual("sfx_fire_plasma", Fire(path, weaponName, BattleFactions.Covenant));
            Assert.IsNull(Landing(path, weaponName));
        }

        [Test]
        public void TheSniperRifle_IsOneHardShot_WithNoExplosion()
        {
            Assert.AreEqual("sfx_fire_gauss", Fire("Noble/SpartanSniper", "SRS99 Sniper Rifle", BattleFactions.Unsc));
            Assert.IsNull(Landing("Noble/SpartanSniper", "SRS99 Sniper Rifle"));
            WeaponLook look = Look("Noble/SpartanSniper", "SRS99 Sniper Rifle", BattleFactions.Unsc);
            Assert.AreEqual(WeaponFeelKind.Gauss, look.Kind);
            Assert.AreNotEqual(WeaponLooks.GaussWhite, look.Shot, "A rifle's round, not a railgun's.");
            Assert.AreEqual("sfx_fire_bullet", Fire("Noble/SpartanFireteam", "BR85 Battle Rifle", BattleFactions.Unsc), "An ordinary rifle stays one.");
        }

        [TestCase("Noble/SpartanCQB", "M45 Shotgun")]
        [TestCase("AerospaceCommand/Vulture", "Chin Gun")]
        public void BigGunsThatHitAtOnce_AreHeavyBullets(string path, string weaponName)
        {
            Assert.AreEqual("sfx_fire_heavybullet", Fire(path, weaponName, BattleFactions.Unsc));
            Assert.IsNull(Landing(path, weaponName));
        }

        [Test]
        public void TheMethaneWagon_LobsBurningKegs_NotTheWraithsPlasma()
        {
            const string wagon = "UnggoyRebellion/MethaneWagon", mortar = "Methane Mortar";
            Assert.AreEqual("sfx_fire_bomb", Fire(wagon, mortar, BattleFactions.Covenant));
            Assert.AreEqual("sfx_explosion_small", Landing(wagon, mortar));
            WeaponLook look = Look(wagon, mortar, BattleFactions.Covenant);
            Assert.AreEqual(WeaponFeelKind.Bomb, look.Kind);
            Assert.IsTrue(look.Ball, "A body in the air, not a streak.");
            Assert.AreEqual(BlastStyle.Fire, look.Blast);
            Assert.Greater(look.Shot.r, look.Shot.b + 0.5f, "It burns.");
            Assert.AreEqual(WeaponLooks.MortarBlue, Look("Covenant/Wraith", "Plasma Mortar", BattleFactions.Covenant).Shot, "The Wraith's stays blue.");
        }

        [Test]
        public void ALobbedFloodShot_BurstsWhereItLands()
        {
            Assert.AreEqual("sfx_flood_burst", Landing("Flood/PureFormRanged", "Bone Shards"));
            Assert.IsNull(Landing("Flood/SwarmForm", "Spat Bone"), "It has no blast.");
            Assert.IsNull(Landing("FloodCovenant/Spawner", "Spore Lash"), "Its burst comes with the lash.");
        }

        [Test]
        public void AnInfestedScarabsBeam_IsTheFloods()
        {
            Assert.AreEqual(WeaponLooks.FloodPlasma, Look("FloodCovenant/InfestedScarab", "Scavenged Focus Beam", BattleFactions.Flood).Shot);
            Assert.AreEqual(WeaponLooks.ScarabBeam, Look("Covenant/Scarab", "Focus Beam", BattleFactions.Covenant).Shot);
        }

        [Test]
        public void TheWholeTable_SmallSoundsAreForSmallShots_AndWhatFliesAndBurstsIsHeardLanding()
        {
            int weapons = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets/_Project/Data/Units" }))
            {
                UnitStats stats = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid)).ToStats();
                foreach (WeaponStats weapon in stats.Weapons)
                {
                    weapons++;
                    WeaponFeelKind kind = WeaponFeel.For(weapon);
                    if (kind == WeaponFeelKind.Bullet || kind == WeaponFeelKind.Plasma || kind == WeaponFeelKind.Needle)
                        Assert.Less(weapon.DamagePerShot, 40f, $"{stats.Name}'s {weapon.Name} hits too hard for the sound of a {kind}.");
                    if (!weapon.IsInstant && weapon.SplashRadius > 0f)
                        Assert.IsNotNull(AudioCatalog.LandingOf(weapon), $"{stats.Name}'s {weapon.Name} is drawn going off where it lands.");
                }
            }
            Assert.Greater(weapons, 50);
        }

        private static string Death(string path, Team team) => BattleAudio.DeathCue(new SimUnit(0, team, 0, Unit(path), new Vec2(0f, 0f), new Vec2(0f, 1f))).Slot;

        [TestCase("Flood/SwarmForm", "sfx_death_flood")]
        [TestCase("Covenant/HunterPair", "sfx_death_infantry_covenant")]
        [TestCase("LekgoloColony/HunterCaptain", "sfx_death_infantry_covenant")]
        [TestCase("Covenant/Banshee", "sfx_death_air")]
        [TestCase("Flood/InfectedPhantom", "sfx_death_air")]
        [TestCase("Covenant/Ghost", "sfx_death_vehicle")]
        [TestCase("Covenant/Wraith", "sfx_explosion_medium")]
        [TestCase("Covenant/Scarab", "sfx_explosion_large")]
        [TestCase("UnggoyRebellion/MethaneWagon", "sfx_explosion_medium")]
        [TestCase("Flood/CarrierForm", "sfx_flood_burst")]
        [TestCase("Flood/CombatForm", "sfx_death_flood")]
        [TestCase("Flood/Abomination", "sfx_pelican_crash")]
        public void ADeath_GoesByWhatDied(string path, string expected)
        {
            Assert.AreEqual(expected, Death(path, Team.B));
        }

        [Test]
        public void ADeath_FollowsTheBodyInAMixedSandboxArmy()
        {
            BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);
            Assert.AreEqual("sfx_death_flood", Death("Flood/CombatForm", Team.A), "A Flood form in a mostly UNSC army.");
            BattleFactions.Set(BattleFactions.Flood, BattleFactions.Covenant);
            Assert.AreEqual("sfx_death_infantry", Death("UNSC/Marine", Team.A), "A Marine in a mostly Flood army.");
        }

        [Test]
        public void EveryAutocannonInTheData_IsAHeavyGun()
        {
            var names = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets/_Project/Data/Units" }))
            {
                UnitStats stats = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid)).ToStats();
                foreach (WeaponStats weapon in stats.Weapons)
                {
                    if (!weapon.Name.ToLowerInvariant().Contains("autocannon")) continue;
                    names.Add(weapon.Name);
                    Assert.AreEqual(WeaponFeelKind.HeavyBullet, WeaponFeel.For(weapon), weapon.Name);
                }
            }
            Assert.GreaterOrEqual(names.Count, 4);
        }
    }
}
