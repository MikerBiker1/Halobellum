// LeapTests.cs
// Infection Forms leap (decision D39): they pounce on a target within range, fly over bodies, jump over
// whatever holds them up, and cannot attack in the air. Also the pause when a unit enters the enemy's
// zone, which is gone. The numbers are test fixtures only.
// Depends on: Halobellum.Combat, Halobellum.Core, TestRules, TestUnits, NUnit.

using System;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class LeapTests
    {
        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)MathF.Round(seconds * 30f);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        private static UnitStats Leaper(float range = 6f)
        {
            UnitStats unit = TestUnits.Unit("Leaper", health: 1000f, speed: 5f, radius: 0.5f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 10f, range: 1f, interval: 0.5f, type: DamageType.Melee));
            unit.LeapRange = range;
            unit.LeapSpeed = 14f;
            unit.LeapCooldownSeconds = 2.5f;
            return unit;
        }

        private static SimUnit Enemy(BattleSimulation sim, Vec2 position)
        {
            return sim.AddUnit(TestUnits.Unit("Enemy", health: 1000f, speed: 0f, radius: 0.5f, filter: TargetFilter.Air,
                weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.B, position);
        }

        private static SimUnit Block(BattleSimulation sim, Vec2 position, float radius = 0.5f)
        {
            return sim.AddUnit(TestUnits.Unit("Block", health: 1000f, speed: 0f, radius: radius, filter: TargetFilter.Air,
                weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.A, position);
        }

        [Test]
        public void PouncesOnATargetInRange_AndLandsAgainstIt()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 6f)); // 5.5 m to its edge.
            sim.Step();
            Assert.IsTrue(leaper.IsLeaping);
            Assert.Greater(leaper.Position.Y, 0.4f, "14 m/s, much faster than it walks.");
            RunSeconds(sim, 0.5f);
            Assert.IsFalse(leaper.IsLeaping, "It has landed.");
            Assert.IsTrue(Targeting.IsInRange(leaper, leaper.Weapons[0].Stats, enemy), "Right against its target, in melee range.");
            Assert.AreEqual(5f, leaper.Position.Y, 0.1f, "Bodies touching: 1 m apart.");
        }

        [Test]
        public void WalksWhileTheTargetIsOutOfRange()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, new Vec2(0f, -60f));
            SimUnit enemy = Enemy(sim, new Vec2(0f, -45f)); // 14.5 m to its edge.
            RunSeconds(sim, 0.5f);
            Assert.IsFalse(leaper.IsLeaping);
            Assert.AreEqual(-57.5f, leaper.Position.Y, 0.3f, "It walks at 5 m/s until it is close enough.");
            bool leapt = false;
            for (int i = 0; i < 40; i++)
            {
                sim.Step();
                if (!leapt && leaper.IsLeaping)
                {
                    leapt = true;
                    Assert.AreEqual(5.5f, Targeting.EdgeDistance(leaper, enemy), 0.3f, "It took off from 6 m, one tick ago at 14 m/s.");
                }
            }
            Assert.IsTrue(leapt);
        }

        [Test]
        public void NoticesEnemiesAsFarAsItsLeapReaches_NotOnlyItsMeleeRange()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, new Vec2(0f, -60f));
            UnitStats plain = Leaper(0f);
            plain.Name = "Plain";
            SimUnit walker = sim.AddUnit(plain, Team.A, new Vec2(40f, -60f));
            SimUnit enemy = Enemy(sim, new Vec2(0f, -45f));
            Enemy(sim, new Vec2(40f, -45f));
            sim.Step();
            Assert.AreSame(enemy, leaper.MoveTarget, "Leap range x the acquisition multiplier.");
            Assert.IsNull(walker.MoveTarget, "Melee range x the multiplier is only 3 m.");
        }

        [Test]
        public void FliesStraightOverBodies_WithoutTouchingThem()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, Vec2.Zero);
            SimUnit block = Block(sim, new Vec2(0f, 3f), radius: 1.5f);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 6f));
            Vec2 blockAt = block.Position;
            for (int i = 0; i < 6; i++)
            {
                sim.Step();
                Assert.AreEqual(0f, leaper.Position.X, 1e-3f, "No swerving in the air.");
            }
            Assert.AreEqual(blockAt.Y, block.Position.Y, 1e-3f, "Nothing is pushed.");
            RunSeconds(sim, 0.3f);
            Assert.Greater(leaper.Position.Y, block.Position.Y, "It is past the body.");
            Assert.IsTrue(Targeting.IsInRange(leaper, leaper.Weapons[0].Stats, enemy));
        }

        [Test]
        public void JumpsOverAWallOfFriendsThatHoldsItUp()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, new Vec2(0f, -60f));
            for (int x = -6; x <= 6; x++) Block(sim, new Vec2(x * 1.2f, -57f));
            Enemy(sim, new Vec2(0f, 70f)); // Far out of sight: it marches straight ahead.
            bool leapt = false;
            for (int i = 0; i < 60; i++)
            {
                sim.Step();
                leapt |= leaper.IsLeaping;
            }
            Assert.IsTrue(leapt, "Held up by the wall, it jumps.");
            Assert.Greater(leaper.Position.Y, -56f, "It is on the far side.");
        }

        [Test]
        public void DoesNotLeapWhenNothingHoldsItUp()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, new Vec2(0f, -60f));
            Enemy(sim, new Vec2(0f, 70f));
            for (int i = 0; i < 90; i++)
            {
                sim.Step();
                Assert.IsFalse(leaper.IsLeaping);
            }
        }

        [Test]
        public void CannotAttackInTheAir()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 6f));
            RunSeconds(sim, 0.2f);
            Assert.IsTrue(leaper.IsLeaping);
            Assert.AreEqual(1000f, enemy.Health);
            RunSeconds(sim, 1f);
            Assert.Less(enemy.Health, 1000f, "It bites once it has landed.");
        }

        [Test]
        public void WaitsForItsCooldownBeforeTheNextLeap()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 6f));
            RunSeconds(sim, 0.5f);
            Assert.IsFalse(leaper.IsLeaping);
            enemy.Position = new Vec2(0f, leaper.Position.Y + 20f);
            RunSeconds(sim, 1f);
            enemy.Position = new Vec2(0f, leaper.Position.Y + 5f);
            sim.Step();
            Assert.IsFalse(leaper.IsLeaping, "Only 1.5 s since it took off.");
            RunSeconds(sim, 1.2f);
            enemy.Position = new Vec2(0f, leaper.Position.Y + 5f);
            sim.Step();
            Assert.IsTrue(leaper.IsLeaping, "The 2.5 s are over.");
        }

        [Test]
        public void CanBeShotInTheAir()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit("Gunner", health: 1000f, speed: 0f, weapon: TestUnits.Weapon(damage: 5f, range: 30f, interval: 0.1f)), Team.B, new Vec2(0f, 6f));
            RunSeconds(sim, 0.2f);
            Assert.IsTrue(leaper.IsLeaping);
            Assert.Less(leaper.Health, 1000f);
        }

        [Test]
        public void AUnitEnteringTheEnemyZone_NeverStandsIdle()
        {
            // It used to stand idle until its next target check, up to half a second.
            var sim = NewSim();
            UnitStats walker = Leaper(0f);
            SimUnit unit = sim.AddUnit(walker, Team.A, new Vec2(0f, -3f));
            Enemy(sim, new Vec2(0f, 70f));
            bool wentIn = false;
            for (int i = 0; i < 60; i++)
            {
                sim.Step();
                Assert.AreNotEqual(UnitState.Idle, unit.State, $"Idle at {sim.ElapsedSeconds} s.");
                wentIn |= unit.UnlimitedAcquisition;
            }
            Assert.IsTrue(wentIn, "It crossed into the zone.");
        }

        [Test]
        public void ALeapThatReachesTheEdgeOfTheArena_EndsThere()
        {
            var sim = NewSim();
            SimUnit leaper = sim.AddUnit(Leaper(), Team.A, new Vec2(79f, -60f));
            Enemy(sim, new Vec2(-70f, 70f));
            sim.Step();
            leaper.IsLeaping = true;
            leaper.LeapFrom = leaper.Position;
            leaper.LeapTo = new Vec2(90f, -60f); // Outside the arena.
            RunSeconds(sim, 0.5f);
            Assert.IsFalse(leaper.IsLeaping, "It must not hang in the air, unable to attack.");
        }

        [Test]
        public void BadData_IsCaught()
        {
            UnitStats unit = Leaper();
            unit.LeapSpeed = 0f;
            CollectionAssert.IsNotEmpty(unit.Validate());
        }
    }
}
