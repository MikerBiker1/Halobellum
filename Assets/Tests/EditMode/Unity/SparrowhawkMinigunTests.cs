// SparrowhawkMinigunTests.cs
// D153 (the owner): the Sparrowhawk fires only its two miniguns, both at once, each 15 times a second, ballistic and not anti-armor,
// each from its own gun pod.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Presentation;
using Halobellum.Core;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class SparrowhawkMinigunTests
    {
        private static UnitStats Sparrowhawk() =>
            AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/AerospaceCommand/Sparrowhawk.asset").ToStats();

        [Test]
        public void ItHasOnlyItsTwoMiniguns_BallisticAndNotAntiArmor()
        {
            UnitStats stats = Sparrowhawk();
            Assert.AreEqual(2, stats.Weapons.Length);
            foreach (WeaponStats gun in stats.Weapons)
            {
                StringAssert.Contains("Minigun", gun.Name);
                Assert.AreEqual(DamageType.Ballistic, gun.DamageType);
                Assert.IsFalse(gun.IsAntiArmor, gun.Name);
                Assert.AreEqual(1f / 15f, gun.FireInterval, 0.001f, "15 rounds a second.");
                Assert.IsTrue(WeaponLooks.For(gun, stats, BattleFactions.Unsc, Team.A, false).OwnMuzzle, "Each from its own gun pod.");
            }
        }

        [Test]
        public void BothMiniguns_FireTogether_15TimesASecondEach()
        {
            UnitStats stats = Sparrowhawk();
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(stats, Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit(name: "Target", health: 1e6f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 1f)), Team.B, new Vec2(0f, 8f));
            var shots = new Dictionary<int, List<float>> { { 0, new List<float>() }, { 1, new List<float>() } };
            sim.Events.WeaponFired += (a, w, t) => { if (a.Team == Team.A) shots[w].Add(sim.ElapsedSeconds); };
            int ticks = (int)System.Math.Round(4f / sim.TickSeconds);
            for (int i = 0; i < ticks; i++) sim.Step();
            Assert.Greater(shots[0].Count, 20);
            float seconds = shots[0][shots[0].Count - 1] - shots[0][0];
            Assert.AreEqual(15f, (shots[0].Count - 1) / seconds, 0.5f, "15 rounds a second from each.");
            Assert.AreEqual(shots[0].Count, shots[1].Count, 1);
            for (int i = 0; i < System.Math.Min(shots[0].Count, shots[1].Count); i++)
                Assert.AreEqual(shots[0][i], shots[1][i], 1e-4f, "Both guns fire on the same tick, not in turn.");
        }
    }
}
