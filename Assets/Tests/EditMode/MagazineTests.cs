// MagazineTests.cs
// D157 (the owner): a weapon with a magazine fires its shots FireInterval apart, then waits a whole reload from the last before the next
// (the Spartan Sniper: 2 a second, 4 rounds, a 2 s reload; the Spartan Grenadier: 2 a second, 2 rockets, 1.2 s). Each shot aims on its own,
// so a magazine carries on at a new target, and one idle for a whole reload is full again.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class MagazineTests
    {
        private static UnitStats Sniper(float damage = 1f)
        {
            WeaponStats rifle = TestUnits.Weapon(damage: damage, range: 30f, interval: 0.5f);
            rifle.MagazineSize = 4;
            rifle.MagazineReloadSeconds = 2f;
            return TestUnits.Unit(name: "Sniper", health: 1000f, speed: 0f, weapon: rifle);
        }

        private static UnitStats Target(float health = 1e6f) =>
            TestUnits.Unit(name: "Target", health: health, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 0.5f));

        private static List<float> Shots(BattleSimulation sim, float seconds)
        {
            var shots = new List<float>();
            sim.Events.WeaponFired += (a, w, t) => { if (a.Team == Team.A) shots.Add(sim.ElapsedSeconds); };
            int ticks = (int)System.Math.Round(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks; i++) sim.Step();
            return shots;
        }

        [Test]
        public void FourShotsHalfASecondApart_ThenATwoSecondReload()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Sniper(), Team.A, Vec2.Zero);
            sim.AddUnit(Target(), Team.B, new Vec2(0f, 15f));
            List<float> shots = Shots(sim, 6.9f);
            Assert.AreEqual(8, shots.Count, "Two magazines in 6.9 s: " + string.Join(", ", shots));
            for (int i = 1; i < 4; i++) Assert.AreEqual(0.5f, shots[i] - shots[i - 1], 0.04f);
            Assert.AreEqual(2f, shots[4] - shots[3], 0.04f, "A whole reload after the fourth shot.");
            for (int i = 5; i < 8; i++) Assert.AreEqual(0.5f, shots[i] - shots[i - 1], 0.04f);
        }

        [Test]
        public void TheMagazineCarriesOn_AtTheNextTarget()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Sniper(damage: 100f), Team.A, Vec2.Zero);
            sim.AddUnit(Target(health: 50f), Team.B, new Vec2(0f, 15f));
            sim.AddUnit(Target(), Team.B, new Vec2(3f, 15f));
            List<float> shots = Shots(sim, 1.4f);
            Assert.AreEqual(3, shots.Count, "The first kills, the next two go at the other target without a reload.");
        }

        [Test]
        public void TheRateOverTime_CountsTheReload()
        {
            WeaponStats rifle = Sniper().Weapons[0];
            Assert.AreEqual(4f / 3.5f, rifle.ShotsPerSecond, 1e-4f, "4 shots every 1.5 + 2 s.");
        }
    }
}
