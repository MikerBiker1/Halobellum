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
using UnityEngine;

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

        [TestCase("Banished/Marauder", "Plasma Cannon")]
        public void AHeavyPlasmaShot_IsNotAPistol_AndIsBlueNotAFuelRod(string path, string weaponName)
        {
            Assert.AreEqual("sfx_fire_plasmaheavy", Fire(path, weaponName, BattleFactions.Covenant));
            Assert.AreEqual("sfx_explosion_small", Landing(path, weaponName));
            WeaponLook look = Look(path, weaponName, BattleFactions.Covenant);
            Assert.AreEqual(WeaponFeelKind.PlasmaHeavy, look.Kind);
            Assert.AreEqual(WeaponLooks.CovenantPlasma, look.Shot);
        }

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
        public void ALobbedFloodShot_BurstsWhereItLands()
        {
            Assert.AreEqual("sfx_flood_burst", Landing("Flood/PureFormRanged", "Bone Shards"));
            Assert.AreEqual("sfx_flood_burst", Landing("FloodCovenant/InfestedBanshee", "Spore Bomb"), "Drawn bursting in gore and spores.");
            Assert.AreEqual("sfx_flood_burst", Landing("FloodUNSC/InfestedHornet", "Spore Bombs"));
            Assert.IsNull(Landing("Flood/SwarmForm", "Spat Bone"), "It has no blast.");
            Assert.IsNull(Landing("FloodCovenant/Spawner", "Spore Lash"), "Its burst comes with the lash.");
        }

        [Test]
        public void AScarabsBeam_IsItsOwn()
        {
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

        // With the owner's third batch in (D91): soldiers by kind, the Hunters' own, the Flood by size; the generated sounds stay for the rest.
        [TestCase("Flood/SwarmForm", "sfx_death_flood_small")]
        [TestCase("Covenant/HunterPair", "sfx_death_lekgolo")]
        [TestCase("LekgoloColony/HunterCaptain", "sfx_death_lekgolo")]
        [TestCase("LekgoloColony/Goliath", "sfx_death_lekgolo")]
        [TestCase("Covenant/Banshee", "sfx_death_air")]
        [TestCase("Flood/InfectedPhantom", "sfx_death_air")]
        [TestCase("Covenant/Ghost", "sfx_death_vehicle")]
        [TestCase("Covenant/Wraith", "sfx_explosion_medium")]
        [TestCase("Covenant/Scarab", "sfx_death_scarab")]
        [TestCase("Flood/CarrierForm", "sfx_flood_burst")]
        [TestCase("Flood/CombatForm", "sfx_death_flood_small")]
        [TestCase("Flood/PureFormTank", "sfx_death_flood_big")]
        [TestCase("Flood/Abomination", "sfx_death_flood_huge")]
        [TestCase("UNSC/Marine", "sfx_death_human")]
        [TestCase("Covenant/Grunt", "sfx_death_grunt")]
        public void ADeath_GoesByWhatDied(string path, string expected)
        {
            Assert.AreEqual(expected, Death(path, Team.B));
        }

        [Test]
        public void ADeath_FollowsTheBodyInAMixedSandboxArmy()
        {
            BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);
            Assert.AreEqual("sfx_death_flood_small", Death("Flood/CombatForm", Team.A), "A Flood form in a mostly UNSC army.");
            BattleFactions.Set(BattleFactions.Flood, BattleFactions.Covenant);
            Assert.AreEqual("sfx_death_human", Death("UNSC/Marine", Team.A), "A Marine in a mostly Flood army.");
        }

        [Test]
        public void WithoutTheOwnersFile_ADeathFallsBackOnTheGeneratedOnes()
        {
            // The owner's soldiers' and Flood deaths only replace the generated ones while their files are in.
            System.Func<string, AudioClip> real = AudioLibrary.Loader;
            try
            {
                AudioLibrary.Loader = wanted => wanted.StartsWith(AudioLibrary.OverrideFolder + "sfx_death_") ? null : real(wanted);
                AudioLibrary.Forget();
                AudioCatalog.ForgetClips();
                Assert.AreEqual("sfx_death_infantry", Death("UNSC/Marine", Team.A));
                Assert.AreEqual("sfx_death_infantry_covenant", Death("Covenant/HunterPair", Team.B), "No Hunter file: a Covenant soldier's fall.");
                Assert.AreEqual("sfx_death_flood", Death("Flood/CombatForm", Team.B));
                Assert.AreEqual("sfx_pelican_crash", Death("Flood/Abomination", Team.B));
            }
            finally
            {
                AudioLibrary.Loader = real;
                AudioLibrary.Forget();
                AudioCatalog.ForgetClips();
            }
        }

        [Test]
        public void InASkirmish_ADeathGoesByTheBody_NotTheLand()
        {
            var factions = new Dictionary<string, string> { { "Grunt", BattleFactions.Covenant }, { "Marine", BattleFactions.Unsc } };
            BattleFactions.SetSkirmish(BattleFactions.Unsc, factions);
            Assert.AreEqual("sfx_death_grunt", Death("Covenant/Grunt", Team.A), "A Grunt on the UNSC's land.");
            BattleFactions.SetSkirmish(BattleFactions.Covenant, factions);
            Assert.AreEqual("sfx_death_human", Death("UNSC/Marine", Team.B), "A Marine on the Covenant's land.");
        }

        [Test]
        public void InASkirmish_ADropSoundsOfWhatItDrops_AndOtherSpellsOfTheLand()
        {
            var pods = new SpellStats { Name = "ODST Drop Pods", Effect = SpellEffect.DropSquad, DropUnit = new UnitStats { Name = "ODST" } };
            var rally = new SpellStats { Name = "Battlefield Rally", Effect = SpellEffect.Buff };
            BattleFactions.SetSkirmish(BattleFactions.Flood, new Dictionary<string, string> { { "ODST", BattleFactions.Unsc } });
            Assert.AreEqual("sfx_spell_drop_pods", AudioSlots.Spell(pods, 1, BattleAudio.CasterFaction(pods, Team.B)), "Not the Flood's drop.");
            Assert.AreEqual("sfx_spell_buff_flood", AudioSlots.Spell(rally, 1, BattleAudio.CasterFaction(rally, Team.A)));

            BattleFactions.Set(BattleFactions.Flood, BattleFactions.Covenant);
            Assert.AreEqual("sfx_spell_drop_flood", AudioSlots.Spell(pods, 1, BattleAudio.CasterFaction(pods, Team.A)), "In a Faction match, as ever: its side's.");
            Assert.AreEqual("sfx_spell_drop_pods", AudioSlots.Spell(pods, 1, BattleAudio.CasterFaction(pods, Team.B)));
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
