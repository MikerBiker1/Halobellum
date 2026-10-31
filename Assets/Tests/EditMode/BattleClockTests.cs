// BattleClockTests.cs
// Tests the scene's battle clock: it runs the right number of fixed ticks for the frame time and
// speed, pauses, caps runaway frames, and a battle stepped with uneven frame times and speed
// changes ends exactly like the same battle in the headless runner.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class BattleClockTests
    {
        private const float Tick = 1f / 30f;

        [Test]
        public void OneSecondAtNormalSpeed_IsThirtyTicks()
        {
            var clock = new BattleClock(Tick, 100);
            int ticks = 0;
            for (int frame = 0; frame < 60; frame++) ticks += clock.Advance(1f / 60f);
            Assert.AreEqual(30, ticks, 1);
        }

        [TestCase(2f, 60)]
        [TestCase(4f, 120)]
        public void SpeedMultipliesTicks(float speed, int expected)
        {
            var clock = new BattleClock(Tick, 100) { Speed = speed };
            int ticks = 0;
            for (int frame = 0; frame < 60; frame++) ticks += clock.Advance(1f / 60f);
            Assert.AreEqual(expected, ticks, 1);
        }

        [Test]
        public void Paused_RunsNoTicks()
        {
            var clock = new BattleClock(Tick, 100) { Speed = 0f };
            Assert.IsTrue(clock.IsPaused);
            Assert.AreEqual(0, clock.Advance(1f));
        }

        [Test]
        public void SlowFrame_IsCappedAtMaxTicksPerFrame()
        {
            var clock = new BattleClock(Tick, 8);
            Assert.AreEqual(8, clock.Advance(5f));
            Assert.AreEqual(0, clock.Advance(0f), "The backlog should be dropped, not carried over.");
        }

        [Test]
        public void Alpha_TracksProgressTowardTheNextTick()
        {
            var clock = new BattleClock(Tick, 100);
            clock.Advance(Tick * 0.5f);
            Assert.AreEqual(0.5f, clock.Alpha, 0.01f);
        }

        [Test]
        public void UnevenFramesAndSpeedChanges_GiveTheSameBattleAsTheRunner()
        {
            CombatRules rules = TestRules.Default();
            var settings = new RunnerSettings { PositionJitter = 0.3f, FrontLineDistance = 10f, SquadGap = 3f };
            var rifle = TestUnits.Unit(name: "Rifle", health: 60f, speed: 4f, squadSize: 12, weapon: TestUnits.Weapon(damage: 6f, range: 12f, interval: 0.5f));
            var tank = TestUnits.Unit(name: "Tank", health: 900f, armor: ArmorType.Heavy, speed: 3f, radius: 2f,
                weapon: TestUnits.Weapon(damage: 100f, range: 16f, interval: 2.5f, projectileSpeed: 30f, splash: 2f, type: DamageType.Explosive));
            var armyA = new ArmySetup("A").Add(rifle, 4);
            var armyB = new ArmySetup("B").Add(tank, 2).Add(rifle, 1);

            BattleResult expected = HeadlessBattleRunner.RunBattle(rules, armyA, armyB, settings, 77);

            BattleSimulation sim = HeadlessBattleRunner.CreateBattle(rules, armyA, armyB, settings, 77);
            var clock = new BattleClock(rules.TickSeconds, 8);
            var frameRandom = new SimRandom(3);
            float[] speeds = { 1f, 2f, 4f, 0f, 1f };
            int frame = 0;
            while (!sim.IsFinished && frame < 100000)
            {
                if (frame % 90 == 0) clock.Speed = speeds[(frame / 90) % speeds.Length];
                int ticks = clock.Advance(frameRandom.Range(0.005f, 0.05f)); // 20 to 200 FPS
                for (int t = 0; t < ticks && !sim.IsFinished; t++) sim.Step();
                frame++;
            }

            Assert.IsTrue(sim.IsFinished);
            Assert.AreEqual(expected.ToSummaryLine(), sim.Result.ToSummaryLine());
        }
    }
}
