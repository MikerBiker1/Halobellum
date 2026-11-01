// InfectionTests.cs
// The Flood's rules (Bible Section 8.3, decision D38): packs of tiny forms, infection, Seeders, death
// bursts, the crash dive, spawning and the Abomination's aura. The numbers are test fixtures only.
// Depends on: Halobellum.Combat, Halobellum.Core, TestRules, TestUnits, NUnit.

using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class InfectionTests
    {
        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)System.MathF.Round(seconds * 30f);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        /// <summary>A pack of 5 forms, 10 health each and 2 damage each per hit.</summary>
        private static UnitStats Pack(float speed = 0f, bool infects = true)
        {
            UnitStats pack = TestUnits.Unit("Pack", health: 50f, speed: speed, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 10f, range: 1f, interval: 0.5f, type: DamageType.Melee));
            pack.FormsPerBody = 5;
            pack.IsFlood = true;
            pack.Infects = infects;
            return pack;
        }

        private static UnitStats Soldier(float health = 10f, ArmorType armor = ArmorType.Unarmored, float shield = 0f)
        {
            return TestUnits.Unit("Soldier", health: health, armor: armor, speed: 0f, shield: shield,
                weapon: TestUnits.Weapon(damage: 4f, range: 1f, interval: 2f));
        }

        /// <summary>An enemy far away that keeps the battle going after a closer one dies.</summary>
        private static void Bystander(BattleSimulation sim, Team team = Team.B)
        {
            sim.AddUnit(TestUnits.Unit("Bystander", health: 100000f, speed: 0f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)),
                team, new Vec2(70f, team == Team.B ? 80f : -80f));
        }

        private static SimUnit Rose(BattleSimulation sim) => sim.Units.FirstOrDefault(u => u.Stats.InfectedFrom != null);

        // ------------------------------------------------------------------ packs

        [Test]
        public void Pack_ShowsAndDealsLess_AsItsFormsDie()
        {
            var sim = NewSim();
            SimUnit pack = sim.AddUnit(Pack(), Team.A, Vec2.Zero);
            Assert.AreEqual(5, pack.FormsAlive);
            Assert.AreEqual(1f, pack.FormsShare);
            pack.Health = 30f;
            Assert.AreEqual(3, pack.FormsAlive);
            pack.Health = 21f;
            Assert.AreEqual(3, pack.FormsAlive, "A wounded form still stands.");
            pack.Health = 1f;
            Assert.AreEqual(1, pack.FormsAlive);

            SimUnit target = sim.AddUnit(TestUnits.Unit(health: 1000f, speed: 0f, armor: ArmorType.Unarmored, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.B, new Vec2(0f, 1.2f));
            pack.Health = 30f;
            pack.Facing = Vec2.Up;
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(1000f - 10f * 0.6f, target.Health, 1e-3f, "3 of its 5 forms bite.");
        }

        [Test]
        public void Pack_TakesOneBulletAsOneForm_ButASplashHitsThemAll()
        {
            var bullets = NewSim();
            sim_AddShooter(bullets, TestUnits.Weapon(damage: 5f, range: 30f, interval: 100f));
            SimUnit a = bullets.AddUnit(Pack(), Team.B, new Vec2(0f, 10f));
            bullets.Step();
            Assert.AreEqual(45f, a.Health, 1e-3f);

            var splash = NewSim();
            sim_AddShooter(splash, TestUnits.Weapon(damage: 5f, range: 30f, interval: 100f, splash: 2f));
            SimUnit b = splash.AddUnit(Pack(), Team.B, new Vec2(0f, 10f));
            splash.Step();
            Assert.AreEqual(50f - 5f * 5f, b.Health, 1e-3f, "All 5 forms are caught.");
        }

        private static SimUnit sim_AddShooter(BattleSimulation sim, WeaponStats weapon)
        {
            return sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f, weapon: weapon), Team.A, Vec2.Zero);
        }

        [Test]
        public void Pack_IsHurtMoreByASpell_ThanASingleBody()
        {
            var sim = NewSim();
            SimUnit pack = sim.AddUnit(Pack(), Team.B, new Vec2(0f, 30f));
            sim.AddSpell(new SpellStats { Effect = SpellEffect.Damage, DamagePerTick = 4f, Radius = 3f, Offensive = true }, Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(50f - 4f * 5f, pack.Health, 1e-3f);
        }

        // ------------------------------------------------------------------ infection

        [Test]
        public void UnarmoredUnit_KilledByForms_RisesForTheFloodAfterTwoSeconds()
        {
            var sim = NewSim();
            sim.AddUnit(Pack(), Team.A, Vec2.Zero);
            SimUnit victim = sim.AddUnit(Soldier(), Team.B, new Vec2(0f, 1.2f));
            Bystander(sim);
            RunSeconds(sim, 0.2f);
            Assert.IsFalse(victim.IsAlive);
            Assert.IsNull(Rose(sim), "It takes 2 seconds to rise.");
            RunSeconds(sim, 1.7f);
            Assert.IsNull(Rose(sim));
            RunSeconds(sim, 0.4f);
            SimUnit risen = Rose(sim);
            Assert.IsNotNull(risen);
            Assert.AreEqual(Team.A, risen.Team, "It fights for the side that infected it.");
            Assert.AreEqual(victim.Position.X, risen.Position.X, 1e-3f);
            Assert.AreEqual(6f, risen.Stats.MaxHealth, 1e-3f, "60% of its health.");
            Assert.AreEqual("Infected Soldier", risen.Stats.Name);
            Assert.AreEqual(DamageType.Melee, risen.Stats.Weapons[0].DamageType);
            Assert.IsTrue(risen.Stats.IsFlood);
        }

        [TestCase(ArmorType.Light)]
        [TestCase(ArmorType.Heavy)]
        public void ArmoredUnit_IsNotInfected(ArmorType armor)
        {
            var sim = NewSim();
            sim.AddUnit(Pack(), Team.A, Vec2.Zero);
            SimUnit victim = sim.AddUnit(Soldier(armor: armor, health: 1f), Team.B, new Vec2(0f, 1.2f));
            Bystander(sim);
            RunSeconds(sim, 3f);
            Assert.IsFalse(victim.IsAlive);
            Assert.IsNull(Rose(sim));
        }

        [Test]
        public void Spartan_IsImmune_ThoughItsBodyIsUnarmored()
        {
            var sim = NewSim();
            sim.AddUnit(Pack(), Team.A, Vec2.Zero);
            UnitStats spartan = Soldier();
            spartan.ImmuneToInfection = true;
            SimUnit victim = sim.AddUnit(spartan, Team.B, new Vec2(0f, 1.2f));
            Bystander(sim);
            RunSeconds(sim, 3f);
            Assert.IsFalse(victim.IsAlive);
            Assert.IsNull(Rose(sim));
        }

        [Test]
        public void ShieldedUnit_IsInfected_OnceItsShieldIsDown()
        {
            var sim = NewSim();
            sim.AddUnit(Pack(), Team.A, Vec2.Zero);
            sim.AddUnit(Soldier(health: 10f, shield: 10f), Team.B, new Vec2(0f, 1.2f));
            Bystander(sim);
            RunSeconds(sim, 3f);
            Assert.IsNotNull(Rose(sim), "The shield only delays it.");
            Assert.AreEqual(0f, Rose(sim).Stats.MaxShield, "It has no shield when it rises.");
        }

        [Test]
        public void UnitKilledFarFromAnyForm_DoesNotRise()
        {
            var sim = NewSim();
            sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, weapon: TestUnits.Weapon(damage: 100f, range: 30f)), Team.A, Vec2.Zero);
            sim.AddUnit(Pack(), Team.A, new Vec2(20f, 0f));
            SimUnit victim = sim.AddUnit(Soldier(), Team.B, new Vec2(0f, 10f));
            Bystander(sim);
            RunSeconds(sim, 3f);
            Assert.IsFalse(victim.IsAlive);
            Assert.IsNull(Rose(sim));
        }

        [Test]
        public void OnlyInfectionForms_Infect()
        {
            var sim = NewSim();
            sim.AddUnit(Pack(infects: false), Team.A, Vec2.Zero);
            sim.AddUnit(Soldier(), Team.B, new Vec2(0f, 1.2f));
            Bystander(sim);
            RunSeconds(sim, 3f);
            Assert.IsNull(Rose(sim));
        }

        [Test]
        public void InfectedUnit_CannotBeInfectedAgain()
        {
            var sim = NewSim();
            sim.AddUnit(Pack(), Team.A, Vec2.Zero);
            sim.AddUnit(Soldier(), Team.B, new Vec2(0f, 1.2f));
            sim.AddUnit(Pack(), Team.B, new Vec2(0f, 3f));
            Bystander(sim);
            RunSeconds(sim, 2.5f);
            SimUnit risen = Rose(sim);
            Assert.IsNotNull(risen);
            risen.Health = 0f;
            RunSeconds(sim, 3f);
            Assert.AreEqual(1, sim.Units.Count(u => u.Stats.InfectedFrom != null));
        }

        [Test]
        public void Battle_WaitsForABodyAboutToRise()
        {
            var sim = NewSim();
            // The killer dies right after its victim: the side is not beaten yet, its victim is about to rise.
            SimUnit pack = sim.AddUnit(Pack(), Team.A, Vec2.Zero);
            SimUnit victim = sim.AddUnit(Soldier(), Team.B, new Vec2(0f, 1.2f));
            Bystander(sim);
            RunSeconds(sim, 0.2f);
            Assert.IsFalse(victim.IsAlive);
            pack.Health = 0f;
            RunSeconds(sim, 0.2f);
            Assert.IsFalse(pack.IsAlive);
            Assert.IsFalse(sim.IsFinished, "The body on its way counts.");
            RunSeconds(sim, 2f);
            Assert.IsNotNull(Rose(sim));
            Assert.IsFalse(sim.IsFinished);
        }

        [Test]
        public void Spell_ThatInfects_RaisesUnarmoredKills_ButNotArmoredOnes()
        {
            var sim = NewSim();
            sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.A, Vec2.Zero);
            sim.AddUnit(Soldier(), Team.B, new Vec2(0f, 30f));
            sim.AddUnit(Soldier(armor: ArmorType.Heavy, health: 1f), Team.B, new Vec2(1f, 30f));
            sim.AddUnit(Soldier(health: 100000f), Team.B, new Vec2(0f, 60f));
            sim.AddSpell(new SpellStats { Effect = SpellEffect.Damage, DamagePerTick = 100f, Radius = 3f, Offensive = true, InfectsKills = true }, Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 2.5f);
            Assert.AreEqual(1, sim.Units.Count(u => u.Stats.InfectedFrom != null));
        }

        // ------------------------------------------------------------------ the Seeder

        private static WeaponStats SeedDart()
        {
            WeaponStats dart = TestUnits.Weapon(damage: 1f, range: 30f, interval: 100f);
            dart.SeedSeconds = 8f;
            dart.SeedDamageSharePerSecond = 0.1f;
            dart.SeedSlowShare = 0.3f;
            return dart;
        }

        private static SimUnit Dummy(BattleSimulation sim, Vec2 position, ArmorType armor = ArmorType.Heavy, float health = 100f, float shield = 0f, UnitRole role = UnitRole.Tank)
        {
            UnitStats stats = TestUnits.Unit("Tank", health: health, armor: armor, speed: 0f, shield: shield, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f));
            stats.Role = role;
            return sim.AddUnit(stats, Team.B, position);
        }

        [Test]
        public void Seed_EatsAndSlowsItsHost_AndAnArmoredHostRisesWhenItDies()
        {
            var sim = NewSim();
            sim_AddShooter(sim, SeedDart());
            SimUnit tank = Dummy(sim, new Vec2(0f, 20f));
            Bystander(sim);
            sim.Step();
            Assert.IsTrue(tank.IsSeeded);
            RunSeconds(sim, 1f);
            Assert.AreEqual(0.7f, tank.SpeedMultiplier, 1e-3f);
            Assert.Less(tank.Health, 91f);
            Assert.Greater(tank.Health, 85f);
            tank.Health = 0f;
            RunSeconds(sim, 2.5f);
            SimUnit risen = Rose(sim);
            Assert.IsNotNull(risen, "A seeded unit rises whatever its armor.");
            Assert.AreEqual(Team.A, risen.Team);
        }

        [Test]
        public void Seed_RunsOut_AndThenTheHostStaysDeadIfKilled()
        {
            var sim = NewSim();
            WeaponStats dart = SeedDart();
            dart.SeedSeconds = 1f;
            sim_AddShooter(sim, dart);
            SimUnit tank = Dummy(sim, new Vec2(0f, 20f));
            Bystander(sim);
            RunSeconds(sim, 1.5f);
            Assert.IsFalse(tank.IsSeeded);
            Assert.AreEqual(1f, tank.SpeedMultiplier);
            tank.Health = 0f;
            RunSeconds(sim, 3f);
            Assert.IsNull(Rose(sim));
        }

        [Test]
        public void Seeder_IgnoresShieldsThatAreUp_AndTitans_AndFlood_AndHostsAlreadySeeded()
        {
            var sim = NewSim();
            sim_AddShooter(sim, SeedDart());
            SimUnit shielded = Dummy(sim, new Vec2(0f, 10f), shield: 50f);
            SimUnit titan = Dummy(sim, new Vec2(3f, 10f), role: UnitRole.Titan);
            SimUnit flood = sim.AddUnit(Pack(), Team.B, new Vec2(6f, 10f));
            SimUnit plain = Dummy(sim, new Vec2(0f, 25f));
            RunSeconds(sim, 0.2f);
            Assert.IsFalse(shielded.IsSeeded);
            Assert.IsFalse(titan.IsSeeded);
            Assert.IsFalse(flood.IsSeeded);
            Assert.IsTrue(plain.IsSeeded, "It reaches past them for one it can seed.");
        }

        // ------------------------------------------------------------------ death bursts

        private static UnitStats Carrier(UnitStats released)
        {
            UnitStats carrier = TestUnits.Unit("Carrier", health: 50f, speed: 0f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 0f, range: 1.5f));
            carrier.IsFlood = true;
            carrier.SelfDestructs = true;
            carrier.DeathBurst = new DeathBurstStats
            {
                Damage = 30f, Radius = 4f, DamageType = DamageType.Explosive,
                Spawns = released == null ? new SpawnEntry[0] : new[] { new SpawnEntry { Unit = released, Bodies = 2 } },
            };
            return carrier;
        }

        [Test]
        public void Carrier_ExplodesWhenItReachesAnEnemy_AndReleasesItsForms()
        {
            var sim = NewSim();
            SimUnit carrier = sim.AddUnit(Carrier(Pack()), Team.A, Vec2.Zero);
            SimUnit near = sim.AddUnit(Soldier(health: 100f), Team.B, new Vec2(0f, 2f));
            SimUnit far = sim.AddUnit(Soldier(health: 100f), Team.B, new Vec2(0f, 30f));
            RunSeconds(sim, 0.2f);
            Assert.IsFalse(carrier.IsAlive);
            Assert.AreEqual(70f, near.Health, 1e-3f);
            Assert.AreEqual(100f, far.Health);
            Assert.AreEqual(2, sim.Units.Count(u => u.Stats.Name == "Pack" && u.IsAlive));
            Assert.AreEqual(Team.A, sim.Units.First(u => u.Stats.Name == "Pack").Team);
        }

        [Test]
        public void Carrier_ShotDead_StillBursts()
        {
            var sim = NewSim();
            SimUnit carrier = sim.AddUnit(Carrier(Pack()), Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit(health: 1000f, speed: 0f, weapon: TestUnits.Weapon(damage: 100f, range: 30f)), Team.B, new Vec2(0f, 20f));
            RunSeconds(sim, 0.2f);
            Assert.IsFalse(carrier.IsAlive);
            Assert.AreEqual(2, sim.Units.Count(u => u.Stats.Name == "Pack"));
        }

        [Test]
        public void LevelledBurst_HitsHarder()
        {
            UnitStats level3 = UnitUpgrades.Apply(Carrier(null), 3, null);
            Assert.AreEqual(90f, level3.DeathBurst.Damage, 1e-3f);
        }

        [Test]
        public void CrashDive_GoesForTheThickestGroup_NotTheClosestUnit()
        {
            var sim = NewSim();
            UnitStats pelican = TestUnits.Unit("Crasher", health: 100f, speed: 20f, air: true, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            pelican.IsFlood = true;
            pelican.SelfDestructs = true;
            pelican.SeeksDensestCluster = true;
            pelican.DeathBurst = new DeathBurstStats { Damage = 500f, Radius = 6f, DamageType = DamageType.Explosive };
            sim.AddUnit(pelican, Team.A, Vec2.Zero);

            SimUnit lonely = Dummy(sim, new Vec2(0f, 10f), health: 1000f);
            var crowd = new SimUnit[5];
            for (int i = 0; i < crowd.Length; i++) crowd[i] = Dummy(sim, new Vec2(30f + i * 1.05f, 30f), health: 1000f);
            RunSeconds(sim, 4f);
            Assert.AreEqual(1000f, lonely.Health, "It flies past the closest unit.");
            foreach (SimUnit unit in crowd) Assert.Less(unit.Health, 1000f);
        }

        // ------------------------------------------------------------------ the Abomination

        [Test]
        public void Spawner_KeepsReleasing_WhileItLives()
        {
            var sim = NewSim();
            UnitStats abomination = TestUnits.Unit("Abomination", health: 100000f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            abomination.SpawnedUnit = Pack();
            abomination.SpawnedUnit.SquadSize = 2;
            abomination.SpawnIntervalSeconds = 5f;
            SimUnit body = sim.AddUnit(abomination, Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.B, new Vec2(0f, 80f));
            RunSeconds(sim, 4.5f);
            Assert.AreEqual(0, sim.Units.Count(u => u.Stats.Name == "Pack"));
            RunSeconds(sim, 6f);
            Assert.AreEqual(4, sim.Units.Count(u => u.Stats.Name == "Pack"), "Twice, 2 bodies each.");
            body.Health = 0f;
            RunSeconds(sim, 6f);
            Assert.AreEqual(4, sim.Units.Count(u => u.Stats.Name == "Pack"), "Not after it is dead.");
        }

        [Test]
        public void Aura_SpeedsAndHealsFriendsNearby_ButNotFarAway_AndDoesNotStack()
        {
            var sim = NewSim();
            UnitStats leader = TestUnits.Unit("Leader", health: 1000f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f));
            leader.AuraRadius = 10f;
            leader.AuraSpeedMultiplier = 1.5f;
            leader.AuraHealPerSecondShare = 0.1f;
            sim.AddUnit(leader, Team.A, Vec2.Zero);
            sim.AddUnit(leader, Team.A, new Vec2(4f, 0f));
            UnitStats friend = TestUnits.Unit("Friend", health: 100f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f));
            SimUnit near = sim.AddUnit(friend, Team.A, new Vec2(2f, 2f));
            SimUnit far = sim.AddUnit(friend, Team.A, new Vec2(40f, 0f));
            sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.B, new Vec2(0f, 80f));
            near.Health = 50f;
            far.Health = 50f;
            RunSeconds(sim, 1f);
            Assert.AreEqual(1.5f, near.SpeedMultiplier, 1e-3f, "Two auras do not add up.");
            Assert.AreEqual(60f, near.Health, 0.5f);
            Assert.AreEqual(1f, far.SpeedMultiplier);
            Assert.AreEqual(50f, far.Health);
        }

        // ------------------------------------------------------------------ data rules

        [Test]
        public void BadData_IsCaught()
        {
            UnitStats zero = TestUnits.Unit();
            zero.FormsPerBody = 0;
            CollectionAssert.IsNotEmpty(zero.Validate());

            UnitStats noBurst = TestUnits.Unit();
            noBurst.SelfDestructs = true;
            CollectionAssert.IsNotEmpty(noBurst.Validate());

            UnitStats spawner = TestUnits.Unit();
            spawner.SpawnedUnit = TestUnits.Unit();
            CollectionAssert.IsNotEmpty(spawner.Validate());
        }
    }
}
