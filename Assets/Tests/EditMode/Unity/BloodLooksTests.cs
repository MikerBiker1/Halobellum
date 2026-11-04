// BloodLooksTests.cs
// What each real unit bleeds, read from the game's own data: every unit has a blood or is listed as having none, the Flood and what it has
// taken over are brown, machines and towers have none; and in Unlock Skirmish a unit looks like its own faction, falling back on its side's.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Presentation, Halobellum.Core, Halobellum.Units, NUnit, UnityEditor.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class BloodLooksTests
    {
        private static UnitStats Unit(string path) => AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{path}.asset").ToStats();

        private static IEnumerable<UnitStats> EveryUnit()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets/_Project/Data/Units" }))
                yield return AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid)).ToStats();
        }

        [TearDown]
        public void TearDown() => BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant); // It is static.

        [Test]
        public void EveryUnit_HasABlood_OrIsListedAsHavingNone()
        {
            int units = 0;
            foreach (UnitStats stats in EveryUnit())
            {
                units++;
                if (stats.IsFlood) Assert.AreEqual(BloodKind.SicklyBrown, BloodLooks.For(stats), stats.Name);
                else Assert.IsTrue(BloodLooks.IsListed(stats.Name), $"{stats.Name} has no entry: say what it bleeds, or that it doesn't.");
            }
            Assert.GreaterOrEqual(units, 69);
        }

        [TestCase("UNSC/Marine", BloodKind.Red)]
        [TestCase("UNSC/FlamethrowerTrooper", BloodKind.Red)]
        [TestCase("UNSC/Spartan", BloodKind.Red)]
        [TestCase("Noble/SpartanSniper", BloodKind.Red)]
        [TestCase("SpellUnits/ODST", BloodKind.Red)]
        [TestCase("Covenant/Grunt", BloodKind.LightBlue)]
        [TestCase("Covenant/FuelRodGrunt", BloodKind.LightBlue)]
        [TestCase("UnggoyRebellion/CannonFodder", BloodKind.LightBlue)]
        [TestCase("UnggoyRebellion/HeavyGrunts", BloodKind.LightBlue)]
        [TestCase("Covenant/Elite", BloodKind.DeepBlue)]
        [TestCase("Covenant/Zealot", BloodKind.DeepBlue)]
        [TestCase("Covenant/HunterPair", BloodKind.Orange)]
        [TestCase("LekgoloColony/HunterCaptain", BloodKind.Orange)]
        [TestCase("LekgoloColony/Goliath", BloodKind.Orange)]
        [TestCase("Covenant/JackalPlasmaRifle", BloodKind.Purple)]
        [TestCase("Covenant/JackalShield", BloodKind.Purple)]
        [TestCase("Banished/BruteGrenadier", BloodKind.DarkRed)]
        public void TheOwnersColours(string path, BloodKind kind) => Assert.AreEqual(kind, BloodLooks.For(Unit(path)));

        [TestCase("UNSC/Warthog")]
        [TestCase("UNSC/Scorpion")]
        [TestCase("UNSC/Pelican")]
        [TestCase("Covenant/Banshee")]
        [TestCase("Covenant/Scarab")]
        [TestCase("Banished/Marauder")]
        [TestCase("Banished/Reaver")]
        [TestCase("LekgoloColony/Skitterer")]
        [TestCase("UnggoyRebellion/GruntGoblin")]
        public void Machines_DoNotBleed(string path) => Assert.AreEqual(BloodKind.None, BloodLooks.For(Unit(path)));

        [TestCase("Flood/CombatForm")]
        [TestCase("Flood/InfectionForms")]
        [TestCase("FloodUNSC/InfestedMarine")]
        [TestCase("FloodUNSC/InfestedWarthog")]
        [TestCase("FloodCovenant/InfestedGhost")]
        [TestCase("Flood/InfectedPelican")]
        public void TheFlood_AndWhatItHasTakenOver_AreBrown(string path) => Assert.AreEqual(BloodKind.SicklyBrown, BloodLooks.For(Unit(path)));

        [Test]
        public void ARisenBody_IsBrown_WhateverItWasBefore()
        {
            foreach (UnitStats before in new[] { Unit("UNSC/Marine"), Unit("UNSC/Warthog"), Unit("Covenant/Elite") })
                Assert.AreEqual(BloodKind.SicklyBrown, BloodLooks.For(new UnitStats { Name = "Infected " + before.Name, IsFlood = true, InfectedFrom = before }), before.Name);
            Assert.AreEqual(BloodKind.SicklyBrown, BloodLooks.For(new UnitStats { Name = "Risen", InfectedFrom = Unit("UNSC/Marine") }), "By what it came from, whatever its name.");
        }

        [Test]
        public void ATower_DoesNotBleed()
        {
            var tower = new UnitStats { Name = "Tower", Shockwave = new ShockwaveStats() };
            Assert.IsTrue(tower.IsTower);
            Assert.AreEqual(BloodKind.None, BloodLooks.For(tower));
            tower.IsFlood = true;
            Assert.AreEqual(BloodKind.None, BloodLooks.For(tower), "Not even on the Flood's side.");
        }

        [Test]
        public void EachBlood_IsItsOwnColour_AndNoBloodIsNone()
        {
            BloodKind[] bloods = System.Enum.GetValues(typeof(BloodKind)).Cast<BloodKind>().Where(kind => kind != BloodKind.None).ToArray();
            Assert.AreEqual(bloods.Length, bloods.Select(BloodLooks.ColorOf).Distinct().Count());
            foreach (BloodKind kind in bloods) Assert.AreEqual(1f, BloodLooks.ColorOf(kind).a, kind.ToString());
            Assert.AreEqual(0f, BloodLooks.ColorOf(BloodKind.None).a);
            Color light = BloodLooks.ColorOf(BloodKind.LightBlue), deep = BloodLooks.ColorOf(BloodKind.DeepBlue);
            Assert.Greater(light.b, deep.b, "A Grunt's blue is lighter than an Elite's.");
            Assert.Greater(light.g, deep.g);
        }

        [Test]
        public void SmallHitsRarelyBleed_AndBigOnesAlwaysDo()
        {
            Assert.LessOrEqual(BloodLooks.HitChance(0.01f), 0.1f, "A lick of a flamethrower.");
            Assert.Greater(BloodLooks.HitChance(0.001f), 0f, "Rifle fire on something huge still shows now and then.");
            Assert.AreEqual(1f, BloodLooks.HitChance(0.34f), 1e-5f);
            Assert.AreEqual(1f, BloodLooks.HitChance(5f), 1e-5f);
        }

        // ------------------------------------------------------------------ looks by the unit's own faction

        [Test]
        public void OfUnit_FallsBackToItsSide_AndKnowsWhatTheMapKnows()
        {
            UnitStats odst = Unit("SpellUnits/ODST");
            BattleFactions.Set(BattleFactions.Flood, BattleFactions.Covenant);
            Assert.AreEqual(BattleFactions.Flood, BattleFactions.OfUnit(odst, Team.A), "Without a map, a unit looks like its side.");
            Assert.AreEqual(BattleFactions.Covenant, BattleFactions.OfUnit(odst, Team.B));
            Assert.AreEqual(BattleFactions.Flood, BattleFactions.OfUnit(null, Team.A));

            BattleFactions.SetSkirmish(BattleFactions.Flood, new Dictionary<string, string> { { "ODST", BattleFactions.Unsc } });
            Assert.AreEqual(BattleFactions.Unsc, BattleFactions.OfUnit(odst, Team.B), "ODST counts as UNSC on any land.");
            Assert.AreEqual(BattleFactions.Flood, BattleFactions.OfUnit(Unit("UNSC/Marine"), Team.A), "What the map doesn't know goes by the land.");
            Assert.AreEqual(BattleFactions.Flood, BattleFactions.OfUnit(null, Team.B));
        }

        [TestCase(BattleFactions.Unsc)]
        [TestCase(BattleFactions.Covenant)]
        [TestCase(BattleFactions.Flood)]
        public void InASkirmish_ARisenBody_IsTheFloods_OnAnyLand(string land)
        {
            var map = new Dictionary<string, string> { { "Marine", BattleFactions.Unsc }, { "Warthog", BattleFactions.Unsc } };
            BattleFactions.SetSkirmish(land, map);
            var melee = new WeaponStats { Name = "Infected Melee", Range = 1f, FireInterval = 1f };
            foreach (string before in map.Keys)
            {
                // Named as BattleSimulation.InfectedFormOf names it: in no roster, so the map can't help.
                var risen = new UnitStats { Name = "Infected " + before, IsFlood = true, InfectedFrom = new UnitStats { Name = before }, Weapons = new[] { melee } };
                Assert.AreEqual(BattleFactions.Flood, BattleFactions.OfUnit(risen, Team.A), risen.Name);
                Assert.AreEqual(BattleFactions.Flood, BattleFactions.OfUnit(risen, Team.B), risen.Name);
            }
            var odd = new UnitStats { Name = "Risen", InfectedFrom = new UnitStats { Name = "Marine" } };
            Assert.AreEqual(BattleFactions.Flood, BattleFactions.OfUnit(odd, Team.A), "By what it came from, whatever its name.");

            // What the player hears from it: the Flood's melee, not the land's.
            Assert.AreEqual("sfx_fire_melee_flood", AudioCatalog.Fire(WeaponFeel.For(melee), BattleFactions.OfUnit(odd, Team.A)).Slot);
        }

        [Test]
        public void WithoutAMap_AFloodBody_StillGoesByItsSide()
        {
            BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);
            var risen = new UnitStats { Name = "Infected Marine", IsFlood = true, InfectedFrom = new UnitStats { Name = "Marine" } };
            Assert.AreEqual(BattleFactions.Unsc, BattleFactions.OfUnit(risen, Team.A), "A Faction match or the Sandbox is as it was.");
            Assert.AreEqual(BattleFactions.Covenant, BattleFactions.OfUnit(risen, Team.B));
        }

        [Test]
        public void InASkirmish_AMarineOnTheFloodsLand_KeepsItsOwnTracers_AndTeamBIsTinted()
        {
            var marine = new UnitStats { Name = "Marine" };
            var rifle = new WeaponStats { Name = "MA5 Assault Rifle", Range = 10f, FireInterval = 1f };
            BattleFactions.SetSkirmish(BattleFactions.Flood, new Dictionary<string, string> { { "Marine", BattleFactions.Unsc } });
            WeaponLook a = WeaponLooks.For(rifle, marine, BattleFactions.OfUnit(marine, Team.A), Team.A, true);
            WeaponLook b = WeaponLooks.For(rifle, marine, BattleFactions.OfUnit(marine, Team.B), Team.B, true);
            Assert.AreEqual(WeaponLooks.UnscTracer, a.Shot, "Not the Flood's green.");
            Assert.AreNotEqual(a.Shot, b.Shot, "The two sides don't fire the same colour.");
        }
    }
}
