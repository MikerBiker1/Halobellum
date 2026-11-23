// SummonedBaseDamageTests.cs
// Bodies made during a battle (a spawner's brood, a death burst, risen and seeded bodies, spell squads, and anything a summoned carrier drops)
// fight like any other but deal no base damage at the round's end. Bought bodies count; the squads a bought air carrier brings in don't either (the owner, D178).
// The owner's case: a Spawner whose brood can't hit an air unit keeps releasing to the time limit; only the Spawner itself hits the base.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit, TestUnits, TestRules.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SummonedBaseDamageTests
    {
        private static WeaponStats Harmless() => TestUnits.Weapon(damage: 0f, range: 0.5f);

        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        /// <summary>A target nothing hurts and that hurts nothing, far off.</summary>
        private static UnitStats Dummy(bool air = false) =>
            TestUnits.Unit(name: air ? "Air Dummy" : "Dummy", health: 1e6f, speed: 0f, air: air, weapon: Harmless());

        /// <summary>Ground-only brood, 3 bodies a release, 2 base damage each (the Spawner's Infection Forms).</summary>
        private static UnitStats Brood()
        {
            UnitStats brood = TestUnits.Unit(name: "Brood", health: 100f, speed: 0f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 10f, range: 1f), squadSize: 3);
            brood.BaseDamage = 2f;
            return brood;
        }

        /// <summary>A ground-only spawner that releases this every few seconds, with 80 base damage (the Spawner).</summary>
        private static UnitStats Spawner(UnitStats released, float interval = 4f)
        {
            UnitStats spawner = TestUnits.Unit(name: "Spawner", health: 1e6f, speed: 0f, filter: TargetFilter.Ground, weapon: TestUnits.Weapon(damage: 10f, range: 1f));
            spawner.BaseDamage = 80f;
            spawner.SpawnedUnit = released;
            spawner.SpawnIntervalSeconds = interval;
            return spawner;
        }

        private static UnitStats CarrierOf(string name, bool air, string dropName, int bodies = 4)
        {
            UnitStats carrier = TestUnits.Unit(name: name, health: 1e6f, speed: 0f, air: air, weapon: Harmless());
            carrier.Reinforcement = TestUnits.Unit(name: dropName, health: 1e6f, speed: 0f, weapon: Harmless(), squadSize: bodies);
            carrier.ReinforcementDelaySeconds = 0.5f;
            return carrier;
        }

        // ------------------------------------------------------------------ the owner's case

        [Test]
        public void ABrood_ThatCantHitAnAirUnit_RunsToTheTimeLimit_AndOnlyTheSpawnerHitsTheBase()
        {
            var sim = NewSim();
            sim.AddUnit(Spawner(Brood()), Team.A, new Vec2(0f, -20f));
            UnitStats air = Dummy(air: true);
            sim.AddUnit(air, Team.B, new Vec2(0f, 40f));
            BattleResult result = sim.Run();

            Assert.IsTrue(result.TimedOut, "Nothing on the ground can hit the air dummy.");
            UnitTypeResult brood = result.TeamA.UnitTypes.Single(t => t.UnitName == "Brood");
            int broodAlive = sim.Units.Count(u => u.IsAlive && u.Stats.Name == "Brood");
            Assert.Greater(broodAlive, 30, "It kept releasing for 90 s.");
            Assert.AreEqual(broodAlive, brood.Survivors);
            Assert.AreEqual(0f, brood.SurvivorBaseDamage, 1e-3f, "Its brood deals no base damage.");
            Assert.AreEqual(80f, result.TeamA.SurvivorBaseDamage, 1e-3f, "Only the Spawner hits the base.");
            Assert.AreEqual(1 + broodAlive, result.TeamA.Survivors, "Survivors still counts every living body.");
            Assert.AreEqual(air.BaseDamage, result.TeamB.SurvivorBaseDamage, 1e-3f);
            Assert.AreEqual(BattleOutcome.TeamAWins, result.Outcome, "80 against 1.");
        }

        [Test]
        public void BoughtBodiesOfTheSameType_StillCount()
        {
            var sim = NewSim();
            UnitStats brood = Brood();
            sim.AddUnit(Spawner(brood), Team.A, new Vec2(0f, -20f));
            sim.AddSquad(brood, Team.A, new Vec2(10f, -20f));
            sim.AddUnit(Dummy(air: true), Team.B, new Vec2(0f, 40f));
            BattleResult result = sim.Run();

            UnitTypeResult type = result.TeamA.UnitTypes.Single(t => t.UnitName == "Brood");
            Assert.Greater(type.Survivors, 3, "Released brood of the same type is there too.");
            Assert.AreEqual(3 * 2f, type.SurvivorBaseDamage, 1e-3f, "Only the 3 bought bodies count.");
            Assert.AreEqual(80f + 6f, result.TeamA.SurvivorBaseDamage, 1e-3f);
        }

        // ------------------------------------------------------------------ where each body comes from

        [Test]
        public void BodiesAddedBeforeTheBattle_AreDeployed()
        {
            var sim = NewSim();
            SimUnit one = sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -20f));
            sim.AddSquad(Brood(), Team.A, new Vec2(10f, -20f));
            Assert.IsTrue(sim.Units.All(u => u.Origin == BodyOrigin.Deployed));
            Assert.IsTrue(one.HitsEnemyBase);
        }

        [Test]
        public void AnAirCarriersSquad_IsCarried_AGroundCarriersHatch_IsSummoned()
        {
            var sim = NewSim();
            sim.AddUnit(CarrierOf("Air Carrier", air: true, dropName: "Carried Marine"), Team.A, new Vec2(-10f, -20f));
            sim.AddUnit(CarrierOf("Ground Carrier", air: false, dropName: "Hatchling"), Team.A, new Vec2(10f, -20f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 60f));
            BattleResult result = sim.Run(); // Nobody can hurt anybody: runs to the time limit.

            Assert.IsTrue(result.TimedOut);
            List<SimUnit> carried = sim.Units.Where(u => u.Stats.Name == "Carried Marine").ToList();
            List<SimUnit> hatched = sim.Units.Where(u => u.Stats.Name == "Hatchling").ToList();
            Assert.AreEqual(4, carried.Count);
            Assert.AreEqual(4, hatched.Count);
            Assert.IsTrue(carried.All(u => u.Origin == BodyOrigin.Carried), "A bought air carrier's troops came with it (Pelican, Phantom).");
            Assert.IsTrue(hatched.All(u => u.Origin == BodyOrigin.Summoned), "A ground unit's hatch is brood (the Spawner's Swarm Form).");
            Assert.IsTrue(carried.All(u => u.IsAlive && !u.HitsEnemyBase), "Carried troops deal no base damage (the owner, D178: Pelican Marines).");
            Assert.AreEqual(0f, result.TeamA.UnitTypes.Single(t => t.UnitName == "Carried Marine").SurvivorBaseDamage, 1e-3f);
            Assert.AreEqual(0f, result.TeamA.UnitTypes.Single(t => t.UnitName == "Hatchling").SurvivorBaseDamage, 1e-3f);
            Assert.AreEqual(1f + 1f, result.TeamA.SurvivorBaseDamage, 1e-3f, "Only the two carriers, which were bought.");
            Assert.AreEqual(10, result.TeamA.Survivors);
        }

        [Test]
        public void ALandingCarriersSquad_IsCarried()
        {
            var sim = NewSim();
            UnitStats lander = TestUnits.Unit(name: "Lander", health: 100000f, speed: 12f, air: true, radius: 2.5f, weapon: Harmless());
            lander.Reinforcement = TestUnits.Unit(name: "Dropped Marine", health: 100000f, speed: 0f, weapon: Harmless(), squadSize: 6);
            lander.ReinforcementDelaySeconds = 5f;
            lander.ReinforcementDrops = 3;
            lander.ReinforcementLandSeconds = 1f;
            sim.AddUnit(lander, Team.A, new Vec2(0f, -40f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(10f, 20f));
            int drops = 0;
            sim.Events.ReinforcementDropped += (carrier, squadId) =>
            {
                drops++;
                Assert.IsTrue(sim.SquadById(squadId).Members.All(u => u.Origin == BodyOrigin.Carried), "Dropped while it is down, still carried.");
            };
            RunSeconds(sim, 45f);
            Assert.Greater(drops, 0);
        }

        [Test]
        public void ADeathBurstsBodies_AreSummoned()
        {
            var sim = NewSim();
            UnitStats burster = TestUnits.Unit(name: "Burster", health: 10f, speed: 0f, weapon: Harmless());
            burster.DeathBurst = new DeathBurstStats
            {
                Damage = 0f, Radius = 1f, DamageType = DamageType.Explosive,
                Spawns = new[] { new SpawnEntry { Unit = Brood(), Bodies = 2 } },
            };
            sim.AddUnit(burster, Team.A, new Vec2(0f, -20f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 60f));
            var strike = new SpellStats
            {
                Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Radius = 3f, DelaySeconds = 0.5f,
                DamagePerTick = 100f, DamageType = DamageType.Explosive, Ticks = 1, Targets = TargetFilter.Ground,
            };
            sim.AddSpell(strike, Team.B, new Vec2(0f, -20f));
            RunSeconds(sim, 1.5f);
            List<SimUnit> released = sim.Units.Where(u => u.Stats.Name == "Brood").ToList();
            Assert.AreEqual(2, released.Count);
            Assert.IsTrue(released.All(u => u.Origin == BodyOrigin.Summoned && u.IsAlive && !u.HitsEnemyBase));
        }

        [Test]
        public void ARisenBody_IsSummoned()
        {
            var sim = NewSim();
            UnitStats pack = TestUnits.Unit("Pack", health: 50f, speed: 0f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 10f, range: 1f, interval: 0.5f, type: DamageType.Melee));
            pack.FormsPerBody = 5;
            pack.IsFlood = true;
            pack.Infects = true;
            sim.AddUnit(pack, Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit("Soldier", health: 10f, speed: 0f, weapon: TestUnits.Weapon(damage: 4f, range: 1f, interval: 2f)), Team.B, new Vec2(0f, 1.2f));
            sim.AddUnit(TestUnits.Unit("Bystander", health: 100000f, speed: 0f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)),
                Team.B, new Vec2(70f, 80f));
            SimUnit risen = null;
            sim.Events.UnitRose += (corpse, body) => risen = body;
            RunSeconds(sim, 3f);
            Assert.IsNotNull(risen);
            Assert.AreEqual(Team.A, risen.Team);
            Assert.AreEqual(BodyOrigin.Summoned, risen.Origin);
            Assert.Greater(risen.Stats.BaseDamage, 0f, "It keeps the original's base damage on its stats ...");
            Assert.IsFalse(risen.HitsEnemyBase, "... but deals none at the round's end.");
        }

        [Test]
        public void ASpellsSquads_AreSummoned()
        {
            var sim = NewSim();
            UnitStats trooper = TestUnits.Unit(name: "Trooper", squadSize: 4, speed: 0f, weapon: Harmless());
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -40f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 60f));
            sim.AddSpell(new SpellStats { Name = "Pods", Offensive = true, Effect = SpellEffect.DropSquad, DelaySeconds = 0.5f, DropUnit = trooper, DropSquads = 2 },
                Team.A, new Vec2(0f, -10f));
            BattleResult result = sim.Run();
            List<SimUnit> landed = sim.Units.Where(u => u.Stats == trooper).ToList();
            Assert.AreEqual(8, landed.Count);
            Assert.IsTrue(landed.All(u => u.Origin == BodyOrigin.Summoned));
            Assert.AreEqual(1f, result.TeamA.SurvivorBaseDamage, 1e-3f, "Only the deployed dummy: ODST Drop Pods, Banshee Ambush and Dispersal Pods deal none.");
        }

        // ------------------------------------------------------------------ a carrier that was itself summoned

        [Test]
        public void ASummonedAirCarrier_DropsSummonedBodies()
        {
            var sim = NewSim();
            UnitStats carrier = CarrierOf("Summoned Carrier", air: true, dropName: "Its Drop");
            carrier.SquadSize = 1;
            sim.AddUnit(Spawner(carrier, interval: 30f), Team.A, new Vec2(0f, -20f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 60f));
            BattleResult result = sim.Run();

            List<SimUnit> carriers = sim.Units.Where(u => u.Stats.Name == "Summoned Carrier").ToList();
            List<SimUnit> dropped = sim.Units.Where(u => u.Stats.Name == "Its Drop").ToList();
            Assert.GreaterOrEqual(carriers.Count, 2);
            Assert.AreEqual(4 * carriers.Count(c => c.DropsMade > 0), dropped.Count);
            Assert.Greater(dropped.Count, 0);
            Assert.IsTrue(carriers.All(u => u.Origin == BodyOrigin.Summoned));
            Assert.IsTrue(dropped.All(u => u.Origin == BodyOrigin.Summoned), "What a summoned carrier drops is summoned too.");
            Assert.AreEqual(80f, result.TeamA.SurvivorBaseDamage, 1e-3f, "Only the Spawner.");
        }

        [Test]
        public void ASeededAirCarrier_RisesSummoned_AndDropsSummonedBodies()
        {
            // The Seeder raises a seeded Covenant Phantom as an Infected Phantom, whose Combat Forms drop at once.
            var sim = NewSim();
            WeaponStats dart = TestUnits.Weapon(damage: 1f, range: 30f, interval: 100f);
            dart.SeedSeconds = 8f;
            dart.SeedDamageSharePerSecond = 0.1f;
            SimUnit seeder = sim.AddUnit(TestUnits.Unit("Seeder", health: 100000f, speed: 0f, weapon: dart), Team.A, Vec2.Zero);
            UnitStats infectedPhantom = CarrierOf("Infected Phantom", air: true, dropName: "Combat Form", bodies: 5);
            seeder.Stats.SeedForms = new[] { new SeedForm { From = "Phantom", Becomes = infectedPhantom } };
            SimUnit phantom = sim.AddUnit(TestUnits.Unit("Phantom", health: 100f, armor: ArmorType.Light, speed: 0f, air: true, weapon: Harmless()), Team.B, new Vec2(0f, 20f));
            sim.AddUnit(TestUnits.Unit("Bystander", health: 100000f, speed: 0f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)),
                Team.B, new Vec2(70f, 80f));
            sim.Step();
            Assert.IsTrue(phantom.IsSeeded);
            phantom.Health = 0f;
            RunSeconds(sim, 3f);

            SimUnit risen = sim.Units.SingleOrDefault(u => u.Stats.Name == "Infected Phantom");
            Assert.IsNotNull(risen);
            Assert.AreEqual(BodyOrigin.Summoned, risen.Origin);
            List<SimUnit> forms = sim.Units.Where(u => u.Stats.Name == "Combat Form").ToList();
            Assert.AreEqual(5, forms.Count, "It drops its squad on the next tick.");
            Assert.IsTrue(forms.All(u => u.Team == Team.A && u.Origin == BodyOrigin.Summoned && !u.HitsEnemyBase));
        }

        // ------------------------------------------------------------------ listeners

        [Test]
        public void OriginIsSet_BeforeUnitSpawnedIsRaised()
        {
            var sim = NewSim();
            sim.AddUnit(Spawner(Brood()), Team.A, new Vec2(0f, -20f));
            sim.AddUnit(Dummy(air: true), Team.B, new Vec2(0f, 40f));
            UnitStats trooper = TestUnits.Unit(name: "Trooper", squadSize: 2, speed: 0f, weapon: Harmless());
            sim.AddSpell(new SpellStats { Name = "Pods", Offensive = true, Effect = SpellEffect.DropSquad, DelaySeconds = 0.5f, DropUnit = trooper, DropSquads = 1 },
                Team.A, new Vec2(0f, -10f));
            var seen = new List<BodyOrigin>();
            sim.Events.UnitSpawned += u => seen.Add(u.Origin);
            RunSeconds(sim, 9f);
            Assert.AreEqual(2 + 3 + 3, seen.Count, "The spell's 2, then two releases of 3.");
            Assert.IsTrue(seen.All(o => o == BodyOrigin.Summoned), "Listeners see the origin already set.");
        }
    }
}
