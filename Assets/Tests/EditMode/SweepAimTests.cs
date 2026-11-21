// SweepAimTests.cs
// The AI's Glassing Beam aim (D144): it leads the squads, aiming where they will be when the beam fires
// seconds into the battle, and keeps its ticks off its own troops.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SweepAimTests
    {
        private static SpellStats Beam() => new SpellStats
        {
            Name = "Beam", Offensive = true, Effect = SpellEffect.Damage, Radius = 6f, AreaWidth = 30f, AreaDepth = 12f,
            DelaySeconds = 6f, DamagePerTick = 700f, DamageType = DamageType.Explosive, Ticks = 10, TickInterval = 0.4f, Targets = TargetFilter.Ground,
        };

        private static UnitStats Walker() => TestUnits.Unit(name: "Walker", health: 100000f, speed: 4f, weapon: TestUnits.Weapon(damage: 0.01f, range: 10f));

        private static List<SweepAim.Squad> Armies(UnitStats own, Vec2 ownAt, UnitStats enemy, Vec2 enemyAt) => new List<SweepAim.Squad>
        {
            new SweepAim.Squad { Position = ownAt, Type = own, Value = 100f, Forward = new Vec2(0f, 1f) },
            new SweepAim.Squad { Position = enemyAt, Type = enemy, Value = 100f, Enemy = true, Forward = new Vec2(0f, -1f) },
        };

        [Test]
        public void TheForecast_MatchesWhereTheBattleMovesTheSquads_WhenTheBeamFires()
        {
            UnitStats walker = Walker();
            Vec2 ownAt = new Vec2(-4f, -30f), enemyAt = new Vec2(8f, 30f);
            Vec2[][] at = SweepAim.Forecast(Armies(walker, ownAt, walker, enemyAt), Beam(), 1);

            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit own = sim.AddUnit(walker, Team.A, ownAt);
            SimUnit enemy = sim.AddUnit(walker, Team.B, enemyAt);
            int ticks = (int)System.Math.Round(6f / sim.TickSeconds);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();

            Assert.Less(Vec2.Distance(at[0][1], enemy.Position), 3f, $"forecast {at[0][1]}, battle {enemy.Position}");
            Assert.Less(Vec2.Distance(at[0][0], own.Position), 3f, $"forecast {at[0][0]}, battle {own.Position}");
            Assert.Greater(Vec2.Distance(enemyAt, enemy.Position), 15f, "The squads have walked well away from where they stood.");
        }

        [Test]
        public void TheBeam_IsAimedWhereTheEnemyWillBe_NotWhereItWasDeployed_AndMissesItsOwnTroops()
        {
            UnitStats walker = Walker();
            SpellStats beam = Beam();
            List<SweepAim.Squad> squads = Armies(walker, new Vec2(-4f, -30f), walker, new Vec2(8f, 30f));
            Vec2[][] at = SweepAim.Forecast(squads, beam, beam.Ticks);

            Assert.IsTrue(SweepAim.Aim(beam, squads, _ => true, out Vec2 centre, out float angle));
            int onEnemy = 0;
            for (int k = 0; k < beam.Ticks; k++)
            {
                Vec2 spot = beam.BeamPoint(centre, angle, beam.BeamAlong(k + 1));
                if (Vec2.Distance(spot, at[k][1]) <= beam.Radius) onEnemy++;
                Assert.Greater(Vec2.Distance(spot, at[k][0]), beam.Radius + 3f, $"Tick {k + 1} lands on its own squad.");
                Assert.Greater(Vec2.Distance(spot, new Vec2(8f, 30f)), beam.Radius + 3f, $"Tick {k + 1} lands where the enemy was deployed.");
            }
            Assert.GreaterOrEqual(onEnemy, 3, "Several ticks burn the enemy where it will be.");
        }

        [Test]
        public void ASquadThatCannotMove_IsAimedWhereItStands()
        {
            UnitStats walker = Walker();
            UnitStats turret = TestUnits.Unit(name: "Turret", health: 100000f, speed: 0f, weapon: TestUnits.Weapon(range: 5f));
            SpellStats beam = Beam();
            List<SweepAim.Squad> squads = Armies(walker, new Vec2(0f, -40f), turret, new Vec2(10f, 30f));

            Assert.IsTrue(SweepAim.Aim(beam, squads, _ => true, out Vec2 centre, out float angle));
            int onTurret = 0;
            for (int k = 0; k < beam.Ticks; k++)
                if (Vec2.Distance(beam.BeamPoint(centre, angle, beam.BeamAlong(k + 1)), new Vec2(10f, 30f)) <= beam.Radius) onTurret++;
            Assert.GreaterOrEqual(onTurret, 3);
        }
    }
}
