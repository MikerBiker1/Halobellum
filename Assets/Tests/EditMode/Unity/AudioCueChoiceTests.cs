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
