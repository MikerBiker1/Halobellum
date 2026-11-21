// D155MechanicsTests.cs
// The owner's D155 pass: the Jackal's beam rifle goes on through a kill to the next enemy on its line; the Seeder's rot eats any unit its seed
// takes and its splash seeds too; every plasma pistol shoots green; the Vampire is heard louder than the Grunt Goblin's needles of the same sound;
// a bullet's streak starts ahead of the muzzle, so its tail leaves from the gun.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Presentation;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class D155MechanicsTests
    {
        private static void Run(BattleSimulation sim, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks; i++) sim.Step();
        }

        private static UnitStats Rifle(bool pierce)
        {
            WeaponStats beam = TestUnits.Weapon(damage: 100f, range: 24f, type: DamageType.Energy, interval: 3f);
            beam.PiercesKills = pierce;
            return TestUnits.Unit(name: "Jackal", health: 1000f, speed: 0f, weapon: beam);
        }

        private static UnitStats Body(string name, float health) =>
            TestUnits.Unit(name: name, health: health, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 0.5f));

        [Test]
        public void ABeamRiflesKillingShot_GoesOnToTheNextEnemyOnItsLine()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Rifle(true), Team.A, Vec2.Zero);
            SimUnit front = sim.AddUnit(Body("Front", 50f), Team.B, new Vec2(0f, 8f));
            SimUnit behind = sim.AddUnit(Body("Behind", 500f), Team.B, new Vec2(0.2f, 14f));
            SimUnit aside = sim.AddUnit(Body("Aside", 500f), Team.B, new Vec2(6f, 14f));
            var pierced = new List<SimUnit>();
            sim.Events.ShotPierced += (a, w, passed, hit) => pierced.Add(hit);
            sim.Step();
            Assert.LessOrEqual(front.Health, 0f, "The first shot kills the one in front.");
            Assert.AreEqual(400f, behind.Health, 0.01f, "And goes on through to the one behind it on the same line.");
            Assert.AreEqual(500f, aside.Health, 0.01f, "Not to one off its line.");
            CollectionAssert.AreEqual(new[] { behind }, pierced);
        }

        [Test]
        public void ABeamRiflesShotThatDoesNotKill_StopsThere()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Rifle(true), Team.A, Vec2.Zero);
            SimUnit front = sim.AddUnit(Body("Front", 500f), Team.B, new Vec2(0f, 8f));
            SimUnit behind = sim.AddUnit(Body("Behind", 500f), Team.B, new Vec2(0f, 14f));
            sim.Step();
            Assert.AreEqual(400f, front.Health, 0.01f);
            Assert.AreEqual(500f, behind.Health, 0.01f);
        }

        [Test]
        public void AKillingShotOfAnotherWeapon_DoesNotGoThrough()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Rifle(false), Team.A, Vec2.Zero);
            sim.AddUnit(Body("Front", 50f), Team.B, new Vec2(0f, 8f));
            SimUnit behind = sim.AddUnit(Body("Behind", 500f), Team.B, new Vec2(0f, 14f));
            sim.Step();
            Assert.AreEqual(500f, behind.Health, 0.01f);
        }

        private static UnitStats Seeder()
        {
            WeaponStats spore = TestUnits.Weapon(damage: 1f, range: 24f, type: DamageType.Energy, interval: 10f, splash: 1.5f);
            spore.SeedSeconds = 8f;
            spore.SeedDamageSharePerSecond = 0.04f;
            spore.SeedSlowShare = 0.4f;
            UnitStats seeder = TestUnits.Unit(name: "Seeder", health: 1000f, speed: 0f, weapon: spore);
            seeder.IsFlood = true;
            return seeder;
        }

        [Test]
        public void TheSeedersRot_EatsAnyUnit_AndItsSplashSeedsToo()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Seeder(), Team.A, Vec2.Zero);
            SimUnit target = sim.AddUnit(Body("Spartan", 1000f), Team.B, new Vec2(0f, 10f));
            SimUnit neighbour = sim.AddUnit(Body("Hunter", 1000f), Team.B, new Vec2(1.2f, 10f));
            sim.Step();
            Assert.IsTrue(target.IsSeeded);
            Assert.IsTrue(neighbour.IsSeeded, "Its splash seeds too.");
            float t = target.Health, n = neighbour.Health;
            Run(sim, 1f);
            Assert.AreEqual(t - 40f, target.Health, 1f, "4% of its health a second, though the Flood has no form of it.");
            Assert.AreEqual(n - 40f, neighbour.Health, 1f);
        }

        [Test]
        public void EveryPlasmaPistol_ShootsGreen()
        {
            WeaponStats pistol = TestUnits.Weapon(damage: 5f, range: 10f, type: DamageType.Energy, interval: 0.5f);
            pistol.Name = "Plasma Pistol";
            UnitStats grunt = TestUnits.Unit(name: "Grunt", weapon: pistol);
            Assert.AreEqual(WeaponLooks.PistolGreen, WeaponLooks.For(pistol, grunt, BattleFactions.Covenant, Team.B, false).Shot);
        }

        [Test]
        public void TheVampire_IsLouderThanTheGoblinsNeedles_OfTheSameSound()
        {
            Assert.AreEqual(AudioCatalog.FireVampire.Slot, AudioCatalog.FireGoblinNeedles.Slot);
            Assert.AreEqual(1.5f * AudioCatalog.FireGoblinNeedles.Volume, AudioCatalog.FireVampire.Volume, 1e-4f, "50% louder.");
        }

        [Test]
        public void AStreak_IsDrawnItsWholeLengthBehindItsHead()
        {
            // The renderer draws a streak from its position back this far (measured in Unity, D155): a shot starts that far ahead of the muzzle.
            Assert.AreEqual(0.12f + 180f * 0.032f, EffectKit.StreakLength(180f, 0.12f), 1e-4f);
        }
    }
}
