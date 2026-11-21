// SummonLevelTests.cs
// What a squad creates in a battle (its drops, releases, death-burst spawns and seed forms) fights at the squad's level, with that level's
// health and damage, like a bought squad at that level (the owner, 2026-11-18: "Created (summoned) units should be the level of the unit that
// created them"). Not with the creator's techs or item; seed forms take the level only, not the army's research. Built when the squad's stats
// are (UnitUpgrades.Apply), so an EMP'd creator still brings its level, and the base stats are never changed in place.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestMatch, TestUnits, TestRules.

using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SummonLevelTests
    {
        private static UnitStats Brought(string name) => TestUnits.Unit(name, health: 100f, squadSize: 3, weapon: TestUnits.Weapon(damage: 10f));

        /// <summary>A unit that brings something every way a unit can: a drop, a locked drop, two releases, a death-burst spawn and a seed form.</summary>
        private static UnitStats Creator()
        {
            UnitStats unit = TestUnits.Unit("Creator", health: 10f);
            unit.Reinforcement = Brought("Dropped");
            unit.LockedReinforcement = Brought("Locked");
            unit.SpawnedUnit = Brought("Released");
            unit.AlternateSpawnedUnit = Brought("Alternate");
            unit.SpawnIntervalSeconds = 5f;
            unit.DeathBurst = new DeathBurstStats { Spawns = new[] { new SpawnEntry { Unit = Brought("Burst"), Bodies = 2 } } };
            unit.SeedForms = new[] { new SeedForm { From = "Tank", Becomes = Brought("Infested Tank") } };
            return unit;
        }

        private static void AssertAtLevel(UnitStats stats, int level, string what)
        {
            Assert.AreEqual(level, stats.Level, what + ": its level.");
            Assert.AreEqual(100f * level, stats.MaxHealth, 1e-3f, what + ": health x level.");
            Assert.AreEqual(10f * level, stats.Weapons[0].DamagePerShot, 1e-3f, what + ": damage x level.");
            Assert.AreEqual(3, stats.SquadSize, what + ": the same squad size.");
        }

        // ------------------------------------------------------------------ stats

        [Test]
        public void WhatAUnitBrings_HasItsSquadsLevel()
        {
            UnitStats unit = Creator();
            SpawnEntry[] spawnsBefore = unit.DeathBurst.Spawns;
            SeedForm[] formsBefore = unit.SeedForms;

            UnitStats level3 = UnitUpgrades.Apply(unit, 3, null);
            AssertAtLevel(level3.Reinforcement, 3, "The drop");
            AssertAtLevel(level3.LockedReinforcement, 3, "The locked drop");
            AssertAtLevel(level3.SpawnedUnit, 3, "The release");
            AssertAtLevel(level3.AlternateSpawnedUnit, 3, "The other release");
            AssertAtLevel(level3.DeathBurst.Spawns[0].Unit, 3, "The death-burst spawn");
            Assert.AreEqual(2, level3.DeathBurst.Spawns[0].Bodies);
            AssertAtLevel(level3.SeedForms[0].Becomes, 3, "The seed form");
            Assert.AreEqual("Tank", level3.SeedForms[0].From);

            foreach (UnitStats original in new[] { unit.Reinforcement, unit.LockedReinforcement, unit.SpawnedUnit, unit.AlternateSpawnedUnit,
                                                   unit.DeathBurst.Spawns[0].Unit, unit.SeedForms[0].Becomes })
                AssertAtLevel(original, 1, "The unit type itself is not touched (" + original.Name + ")");
            Assert.AreSame(spawnsBefore, unit.DeathBurst.Spawns, "The base burst keeps its own array.");
            Assert.AreSame(formsBefore, unit.SeedForms, "The base seed forms keep their own array.");
            Assert.AreNotSame(unit.DeathBurst.Spawns, level3.DeathBurst.Spawns);
            Assert.AreNotSame(unit.SeedForms, level3.SeedForms);
        }

        [Test]
        public void AtLevel1_WhatItBringsIsUnchanged()
        {
            UnitStats unit = Creator();
            UnitStats level1 = UnitUpgrades.Apply(unit, 1, null);
            Assert.AreSame(unit, level1);
            Assert.AreSame(unit.Reinforcement, level1.Reinforcement);
            Assert.AreSame(unit.SpawnedUnit, level1.SpawnedUnit);
        }

        [Test]
        public void LevelAndResearch_BothReachWhatItBrings_SeedFormsTakeTheLevelOnly()
        {
            UnitStats fighting = UnitUpgrades.Apply(Creator(), 2, null, null, new ArmyBuffs { HealthMultiplier = 1.3f });
            Assert.AreEqual(260f, fighting.Reinforcement.MaxHealth, 1e-3f);
            Assert.AreEqual(260f, fighting.SpawnedUnit.MaxHealth, 1e-3f);
            Assert.AreEqual(260f, fighting.DeathBurst.Spawns[0].Unit.MaxHealth, 1e-3f);
            Assert.AreEqual(200f, fighting.SeedForms[0].Becomes.MaxHealth, 1e-3f, "A seed form takes the level, not the research.");
        }

        [Test]
        public void AtLevel1_ResearchReachesWhatItBrings_ButNotSeedForms()
        {
            UnitStats unit = Creator();
            UnitStats fighting = UnitUpgrades.Apply(unit, 1, null, null, new ArmyBuffs { HealthMultiplier = 1.3f });
            Assert.AreEqual(130f, fighting.SpawnedUnit.MaxHealth, 1e-3f);
            Assert.AreEqual(1, fighting.SpawnedUnit.Level);
            Assert.AreSame(unit.SeedForms, fighting.SeedForms, "As before: a level-1 seeder's forms are the unit type's.");
        }

        [Test]
        public void TechsAndItems_DoNotReachWhatItBrings()
        {
            var tech = new TechUpgrade { Name = "t", DamageMultiplier = 1.5f };
            var item = new ItemStats { Name = "x", DamageMultiplier = 1.5f };
            UnitStats fighting = UnitUpgrades.Apply(Creator(), 2, new[] { tech }, item, null);
            Assert.AreEqual(20f, fighting.SpawnedUnit.Weapons[0].DamagePerShot, 1e-3f);
            Assert.AreEqual(20f, fighting.DeathBurst.Spawns[0].Unit.Weapons[0].DamagePerShot, 1e-3f);
            Assert.AreEqual(20f, fighting.WithoutTechs.SpawnedUnit.Weapons[0].DamagePerShot, 1e-3f);
        }

        [Test]
        public void AnEmpdCreator_StillBringsItsLevel()
        {
            UnitStats fighting = UnitUpgrades.Apply(Creator(), 3, new[] { new TechUpgrade { Name = "t", HealthMultiplier = 1.2f } });
            Assert.IsNotNull(fighting.WithoutTechs);
            AssertAtLevel(fighting.WithoutTechs.SpawnedUnit, 3, "An EMP'd creator's release");
            AssertAtLevel(fighting.WithoutTechs.Reinforcement, 3, "An EMP'd creator's drop");
            AssertAtLevel(fighting.WithoutTechs.DeathBurst.Spawns[0].Unit, 3, "An EMP'd creator's burst spawn");
            Assert.AreEqual(300f, fighting.WithoutTechs.SeedForms[0].Becomes.MaxHealth, 1e-3f);
        }

        [Test]
        public void ABayUnlockedByTech_DropsAtTheLevel()
        {
            UnitStats carrier = TestUnits.Unit(name: "Carrier", health: 5000f, speed: 0f, air: true, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            carrier.LockedReinforcement = TestUnits.Unit(name: "Trooper", health: 50f, speed: 0f, squadSize: 4, weapon: TestUnits.Weapon(damage: 4f, range: 1f));
            carrier.ReinforcementDelaySeconds = 1f;
            var bay = new TechUpgrade { Name = "Troop Bay", UnlocksReinforcement = true };
            carrier.TechUpgrades = new[] { bay };

            UnitStats fighting = UnitUpgrades.Apply(carrier, 3, new[] { bay });
            Assert.AreEqual(150f, fighting.Reinforcement.MaxHealth, 1e-3f);
            Assert.AreEqual(12f, fighting.Reinforcement.Weapons[0].DamagePerShot, 1e-3f);
            Assert.AreEqual(3, fighting.Reinforcement.Level);
            Assert.AreEqual(4, fighting.Reinforcement.SquadSize);
            Assert.IsNull(carrier.Reinforcement, "The unit type still needs the tech.");
            Assert.AreEqual(50f, carrier.LockedReinforcement.MaxHealth);
        }

        [Test]
        public void NestedSpawns_TakeTheLevelToo()
        {
            UnitStats unit = Creator();
            unit.SpawnedUnit.DeathBurst = new DeathBurstStats { Spawns = new[] { new SpawnEntry { Unit = Brought("Inner Burst"), Bodies = 1 } } };
            unit.SeedForms[0].Becomes.DeathBurst = new DeathBurstStats { Spawns = new[] { new SpawnEntry { Unit = Brought("Seeded Burst"), Bodies = 1 } } };

            UnitStats level3 = UnitUpgrades.Apply(unit, 3, null);
            AssertAtLevel(level3.SpawnedUnit.DeathBurst.Spawns[0].Unit, 3, "What a release leaves when it dies");
            AssertAtLevel(level3.SeedForms[0].Becomes.DeathBurst.Spawns[0].Unit, 3, "What a seed form leaves when it dies");
            AssertAtLevel(unit.SpawnedUnit.DeathBurst.Spawns[0].Unit, 1, "The unit type's inner spawn");
            AssertAtLevel(unit.SeedForms[0].Becomes.DeathBurst.Spawns[0].Unit, 1, "The unit type's seeded spawn");
        }

        [Test]
        public void ASelfBringingUnit_DoesNotRecurseForever()
        {
            UnitStats unit = Brought("Loop");
            unit.SpawnedUnit = unit;
            unit.SpawnIntervalSeconds = 5f;
            UnitStats level2 = UnitUpgrades.Apply(unit, 2, null);
            Assert.AreEqual(2, level2.SpawnedUnit.Level);
            Assert.AreEqual(200f, level2.SpawnedUnit.SpawnedUnit.MaxHealth, 1e-3f);
        }

        // ------------------------------------------------------------------ in the battle

        [Test]
        public void ASpawnersBrood_InTheBattle_IsItsLevel()
        {
            Match match = TestMatch.Started(out UnitStats infantry, out _, out _, out _);
            match.Player.Squads.Clear();
            match.Enemy.Squads.Clear();
            UnitStats nest = TestUnits.Unit("Nest", health: 100000f, speed: 0f, radius: 2f, cost: 300, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            nest.Tier = 2;
            nest.SpawnedUnit = TestUnits.Unit("Brood", health: 400f, speed: 4f, squadSize: 2, cost: 100, weapon: TestUnits.Weapon(damage: 30f, range: 12f, interval: 0.5f));
            nest.SpawnIntervalSeconds = 2f;
            match.Player.Unlocked.Add(nest);
            var squad = new PlacedSquad { Id = 500, Type = nest, Column = 38, Row = 0, RoundBought = 1, Level = 3 };
            match.Player.Squads.Add(squad);
            match.Enemy.Squads.Add(new PlacedSquad { Id = 600, Type = infantry, Column = 36, Row = 0, RoundBought = 1 });

            BattleSimulation battle = match.StartBattle();
            float health = 1200f * match.Player.Buffs.HealthMultiplier, damage = 90f * match.Player.Buffs.DamageMultiplier;
            while (!battle.IsFinished && !battle.Units.Any(u => u.Stats.Name == "Brood")) battle.Step();
            SimUnit[] brood = battle.Units.Where(u => u.Stats.Name == "Brood").ToArray();
            Assert.IsNotEmpty(brood, "The nest releases its brood.");
            foreach (SimUnit body in brood)
            {
                Assert.AreEqual(3, body.Stats.Level);
                Assert.AreEqual(health, body.Stats.MaxHealth, 1e-2f);
                Assert.AreEqual(damage, body.Stats.Weapons[0].DamagePerShot, 1e-2f);
            }
            battle.Run();
            RoundSummary summary = match.FinishBattle();
            Assert.Greater(summary.XpGained[squad.Id], 0f, "Its brood's kills still earn for the nest (D136).");
            Assert.AreEqual(400f, nest.SpawnedUnit.MaxHealth, "The unit type itself is not touched.");
        }

        [Test]
        public void ACarrierFormsBurst_ReleasesItsLevel()
        {
            UnitStats pack = TestUnits.Unit("Pack", health: 50f, speed: 0f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 10f, range: 1f, interval: 0.5f, type: DamageType.Melee));
            pack.FormsPerBody = 5;
            pack.IsFlood = true;
            UnitStats carrier = TestUnits.Unit("Carrier", health: 50f, speed: 0f, filter: TargetFilter.Ground, weapon: TestUnits.Weapon(damage: 0f, range: 1.5f));
            carrier.IsFlood = true;
            carrier.DeathBurst = new DeathBurstStats { Damage = 30f, Radius = 4f, DamageType = DamageType.Explosive, Spawns = new[] { new SpawnEntry { Unit = pack, Bodies = 2 } } };

            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit body = sim.AddUnit(UnitUpgrades.Apply(carrier, 2, null), Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit(health: 1000f, speed: 0f, weapon: TestUnits.Weapon(damage: 200f, range: 30f)), Team.B, new Vec2(0f, 20f));
            for (int i = 0; i < 6 && !sim.IsFinished; i++) sim.Step();
            Assert.IsFalse(body.IsAlive);
            SimUnit[] packs = sim.Units.Where(u => u.Stats.Name == "Pack").ToArray();
            Assert.AreEqual(2, packs.Length);
            foreach (SimUnit p in packs)
            {
                Assert.AreEqual(2, p.Stats.Level);
                Assert.AreEqual(100f, p.Stats.MaxHealth, 1e-3f);
                Assert.AreEqual(20f, p.Stats.Weapons[0].DamagePerShot, 1e-3f);
            }
        }

        [Test]
        public void ASeedersForm_RisesAtTheSeedersLevel()
        {
            WeaponStats dart = TestUnits.Weapon(damage: 1f, range: 30f, interval: 100f);
            dart.SeedSeconds = 8f;
            dart.SeedDamageSharePerSecond = 0.1f;
            dart.SeedSlowShare = 0.3f;
            UnitStats seeder = TestUnits.Unit(health: 100000f, speed: 0f, weapon: dart);
            seeder.SeedForms = new[] { new SeedForm { From = "Tank", Becomes = TestUnits.Unit("Infested Tank", health: 50f, speed: 0f) } };

            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(UnitUpgrades.Apply(seeder, 2, null), Team.A, Vec2.Zero);
            SimUnit tank = sim.AddUnit(TestUnits.Unit("Tank", health: 100f, armor: ArmorType.Heavy, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)),
                Team.B, new Vec2(0f, 20f));
            sim.AddUnit(TestUnits.Unit("Bystander", health: 100000f, speed: 0f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)),
                Team.B, new Vec2(70f, 80f));
            sim.Step();
            Assert.IsTrue(tank.IsSeeded);
            tank.Health = 0f;
            for (int i = 0; i < 120 && !sim.IsFinished; i++) sim.Step();
            SimUnit risen = sim.Units.FirstOrDefault(u => u.Stats.Name == "Infested Tank");
            Assert.IsNotNull(risen, "The seeded tank rises as the Flood's form of it.");
            Assert.AreEqual(2, risen.Stats.Level);
            Assert.AreEqual(50f * 2f * 0.6f, risen.Stats.MaxHealth, 1e-3f, "The form at the seeder's level, with the infected share of its health.");
            Assert.AreEqual(50f, seeder.SeedForms[0].Becomes.MaxHealth, "The unit type itself is not touched.");
        }

        [Test]
        public void ABodyTakenOverByInfection_KeepsItsOwnLevel()
        {
            // Not made by the infector: the victim's own body, built from the victim's stats (the owner, 2026-11-18). Its built-in Level stays the default 1.
            UnitStats infector = TestUnits.Unit("Pack", health: 50f, speed: 0f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 10f, range: 1f, interval: 0.5f, type: DamageType.Melee));
            infector.FormsPerBody = 5;
            infector.IsFlood = true;
            infector.Infects = true;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(UnitUpgrades.Apply(infector, 3, null), Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit("Soldier", health: 10f, speed: 0f, weapon: TestUnits.Weapon(damage: 4f, range: 1f, interval: 2f)), Team.B, new Vec2(0f, 1.2f));
            sim.AddUnit(TestUnits.Unit("Bystander", health: 100000f, speed: 0f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)),
                Team.B, new Vec2(70f, 80f));
            for (int i = 0; i < 300 && !sim.IsFinished; i++) sim.Step();
            SimUnit risen = sim.Units.FirstOrDefault(u => u.Stats.InfectedFrom != null);
            Assert.IsNotNull(risen, "The soldier rises infected.");
            Assert.AreEqual(1, risen.Stats.Level);
        }
    }
}
