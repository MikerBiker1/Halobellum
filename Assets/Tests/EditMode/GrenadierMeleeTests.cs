// GrenadierMeleeTests.cs
// The Brute Grenadier fights close in (the owner, 2026-11-07: a Grenadier stood beside an enemy Grenadier without attacking it). Its
// launcher cannot fire inside 2 m; an enemy that close gets the Brute Shot's blade instead, and the launcher holds fire meanwhile. With
// nothing that close it lobs grenades as before. A range bonus lengthens the launcher, not the blade. The numbers are test fixtures, close
// to the Grenadier's.
// Depends on: Halobellum.Combat, Halobellum.Core, TestRules, TestUnits, NUnit.

using System;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class GrenadierMeleeTests
    {
        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)MathF.Round(seconds * 30f);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        private static UnitStats Grenadier()
        {
            WeaponStats launcher = TestUnits.Weapon(damage: 34f, range: 13f, interval: 2f, minRange: 2f, type: DamageType.Explosive);
            launcher.Name = "Spike Grenade Launcher";
            WeaponStats blade = TestUnits.Weapon(damage: 35f, range: 2f, interval: 1.5f, type: DamageType.Melee);
            blade.Name = "Brute Shot Blade";
            UnitStats unit = TestUnits.Unit("Brute Grenadier", health: 5000f, speed: 1.5f, radius: 0.6f, filter: TargetFilter.Ground, weapon: launcher);
            unit.Weapons = new[] { launcher, blade };
            return unit;
        }

        private static SimUnit Enemy(BattleSimulation sim, Vec2 position)
        {
            return sim.AddUnit(TestUnits.Unit("Enemy", health: 1000f, speed: 0f, radius: 0.5f, filter: TargetFilter.Air,
                weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.B, position);
        }

        [Test]
        public void WithNothingClose_ItLobsGrenades()
        {
            var sim = NewSim();
            SimUnit grenadier = sim.AddUnit(Grenadier(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 8f));
            RunSeconds(sim, 3f);
            Assert.AreEqual(0, grenadier.MainWeaponIndex, "The launcher.");
            Assert.Less(enemy.Health, 1000f, "Its grenades land.");
            Assert.AreEqual(0f, grenadier.Position.Y, 0.01f, "In range: it stands and shoots.");
        }

        [Test]
        public void AnEnemyRightBesideIt_GetsTheBlade_AndTheLauncherHoldsFire()
        {
            var sim = NewSim();
            SimUnit grenadier = sim.AddUnit(Grenadier(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 1.5f)); // 0.9 m to its edge: inside the launcher's 2 m.
            RunSeconds(sim, 5f);
            Assert.AreEqual(1, grenadier.MainWeaponIndex, "The blade.");
            Assert.AreEqual(UnitState.Attack, grenadier.State, "It fights where it stands.");
            Assert.IsNull(grenadier.Weapons[0].Target, "The launcher holds fire.");
            Assert.AreEqual(1000f - 35f * 4f, enemy.Health, 35.01f, "A swing every 1.5 s: three or four in 5 s.");
            Assert.Less(enemy.Health, 1000f - 35f * 2.5f);
        }

        [Test]
        public void TwoGrenadiers_SideBySide_FightEachOther()
        {
            var sim = NewSim();
            SimUnit a = sim.AddUnit(Grenadier(), Team.A, Vec2.Zero);
            SimUnit b = sim.AddUnit(Grenadier(), Team.B, new Vec2(0f, 1.4f));
            RunSeconds(sim, 4f);
            Assert.Less(a.Health, 5000f, "B swings at A.");
            Assert.Less(b.Health, 5000f, "A swings at B.");
        }

        [Test]
        public void ACloseEnemy_IsBladed_EvenWithAnotherInLauncherRange()
        {
            var sim = NewSim();
            SimUnit grenadier = sim.AddUnit(Grenadier(), Team.A, Vec2.Zero);
            SimUnit close = Enemy(sim, new Vec2(0f, 1.5f));
            SimUnit far = Enemy(sim, new Vec2(0f, 9f));
            RunSeconds(sim, 3f);
            Assert.AreEqual(1, grenadier.MainWeaponIndex);
            Assert.Less(close.Health, 1000f, "The one beside it is cut.");
            Assert.AreEqual(1000f, far.Health, 0.01f, "No grenades meanwhile.");
        }

        [Test]
        public void ARangeBonus_LengthensTheLauncher_NotTheBlade()
        {
            var tech = new TechUpgrade { Name = "Long Spikes", RangeBonus = 4f };
            UnitStats stats = UnitUpgrades.Apply(Grenadier(), 1, new[] { tech });
            Assert.AreEqual(17f, stats.Weapons[0].Range, 0.001f);
            Assert.AreEqual(2f, stats.Weapons[1].Range, 0.001f, "The blade reaches as far as its arm.");
        }
    }
}
