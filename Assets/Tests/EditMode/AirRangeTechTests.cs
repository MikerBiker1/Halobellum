// AirRangeTechTests.cs
// D149: the Jackal sniper's Aim for Their Wings, 4 m more range and half again the damage against air units only. A tech's AirRangeBonus
// reaches aircraft further, ground units no further; the closest target is still the one in range, and Hard values the tech once air is seen.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class AirRangeTechTests
    {
        private static readonly TechUpgrade Wings = new TechUpgrade { Name = "Aim for Their Wings", Cost = 150, AirRangeBonus = 4f, AirDamageMultiplier = 1.5f };

        private static UnitStats Sniper(bool withTech)
        {
            UnitStats sniper = TestUnits.Unit(name: "Sniper", health: 1000f, speed: 0f, radius: 0.5f, weapon: TestUnits.Weapon(damage: 40f, range: 20f, interval: 1f));
            return withTech ? UnitUpgrades.Apply(sniper, 1, new[] { Wings }) : sniper;
        }

        /// <summary>Damage the sniper deals in 3 s to a still target whose edge stands this far away, air or ground.</summary>
        private static float DamageDealt(bool withTech, bool targetIsAir, float edge, bool closerGroundDecoy = false)
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Sniper(withTech), Team.A, new Vec2(0f, 0f));
            UnitStats targetType = TestUnits.Unit(name: "Target", health: 100000f, speed: 0f, radius: 1f, air: targetIsAir, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            SimUnit target = sim.AddUnit(targetType, Team.B, new Vec2(0f, 0.5f + 1f + edge));
            if (closerGroundDecoy)
            {
                // A ground enemy a little closer than the aircraft, but out of the gun's ground range.
                UnitStats decoy = TestUnits.Unit(name: "Decoy", health: 100000f, speed: 0f, radius: 1f, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
                sim.AddUnit(decoy, Team.B, new Vec2(0.5f, 0.5f + 1f + edge - 1f));
            }
            int ticks = (int)System.Math.Round(3f / sim.TickSeconds);
            for (int i = 0; i < ticks; i++) sim.Step();
            return targetType.MaxHealth - target.Health;
        }

        [Test]
        public void TheTech_ReachesAircraft4mFurther_ButNotGroundUnits()
        {
            Assert.AreEqual(0f, DamageDealt(withTech: false, targetIsAir: true, edge: 22f), "Without the tech, 22 m is out of its 20 m range.");
            Assert.Greater(DamageDealt(withTech: true, targetIsAir: true, edge: 22f), 0f, "With it, an aircraft at 22 m is in reach.");
            Assert.AreEqual(0f, DamageDealt(withTech: true, targetIsAir: false, edge: 22f), "A ground unit at 22 m stays out of reach.");
        }

        [Test]
        public void TheTech_DoesHalfAgainTheDamage_ToAircraftOnly()
        {
            float ground = DamageDealt(withTech: true, targetIsAir: false, edge: 10f);
            float air = DamageDealt(withTech: true, targetIsAir: true, edge: 10f);
            Assert.Greater(ground, 0f);
            Assert.AreEqual(ground * 1.5f, air, 0.01f * air);
            Assert.AreEqual(ground, DamageDealt(withTech: false, targetIsAir: false, edge: 10f), 0.01f, "Against the ground, the same as without it.");
        }

        [Test]
        public void ItShootsTheAircraftInReach_NotACloserGroundUnitOutOfReach()
        {
            Assert.Greater(DamageDealt(withTech: true, targetIsAir: true, edge: 22f, closerGroundDecoy: true), 0f);
        }

        [Test]
        public void Hard_WantsTheTech_FirstOfAll_OnceAnyAircraftIsSeen()
        {
            UnitStats sniper = Sniper(withTech: false);
            var damage = new TechUpgrade { Name = "Beam Overcharge", Cost = 150, DamageMultiplier = 1.25f };
            var range = new TechUpgrade { Name = "Marksman Optics", Cost = 300, RangeBonus = 8f };
            var armor = new TechUpgrade { Name = "Point-Defense Gauntlets", Cost = 100, HealthMultiplier = 1.3f };
            UnitStats flyer = TestUnits.Unit(name: "Flyer", air: true, cost: 300);
            UnitStats walker = TestUnits.Unit(name: "Walker", cost: 300);
            var ground = new List<SeenSquad> { new SeenSquad { Type = walker } };
            // One aircraft among five squads: still the first tech it wants.
            var mixed = new List<SeenSquad> { new SeenSquad { Type = walker }, new SeenSquad { Type = walker }, new SeenSquad { Type = walker }, new SeenSquad { Type = walker }, new SeenSquad { Type = flyer } };
            var ai = new RuleBasedAi(AiDifficulty.Hard, 3);

            Assert.AreEqual(0f, ai.TechValueAgainst(sniper, Wings, ground), "No use until air is seen.");
            Assert.Greater(ai.TechValueAgainst(sniper, Wings, mixed), ai.TechValueAgainst(sniper, damage, mixed));
            Assert.Greater(ai.TechValueAgainst(sniper, Wings, mixed), ai.TechValueAgainst(sniper, range, mixed));
            Assert.Greater(ai.TechValueAgainst(sniper, Wings, mixed), ai.TechValueAgainst(sniper, armor, mixed));
        }
    }
}
