// DeploymentGridTests.cs
// Tests the deployment grid: squad footprints in squares (a 12-body infantry squad covers 5 x 2
// squares, as in Mechabellum), turning swaps width and depth, squads stay inside the zone and cannot
// overlap, air squads ignore ground squads, and battles place squads where they were deployed.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestMatch.

using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class DeploymentGridTests
    {
        private readonly CombatRules _rules = TestRules.Default();

        [Test]
        public void Footprints_InSquares()
        {
            DeploymentGrid.Footprint(TestMatch.Infantry(), false, _rules, out int w, out int d);
            Assert.AreEqual((5, 2), (w, d), "12 infantry (2 rows of 6) should cover 5 x 2 squares.");
            DeploymentGrid.Footprint(TestMatch.Infantry(), true, _rules, out w, out d);
            Assert.AreEqual((2, 5), (w, d));
            DeploymentGrid.Footprint(TestMatch.Tank(), false, _rules, out w, out d);
            Assert.AreEqual((2, 2), (w, d));
            DeploymentGrid.Footprint(TestMatch.Buggy(), false, _rules, out w, out d);
            Assert.AreEqual((3, 2), (w, d));
        }

        [Test]
        public void Squads_MustStayInsideTheZone()
        {
            var player = new PlayerState(Team.A);
            UnitStats infantry = TestMatch.Infantry();
            Assert.IsTrue(DeploymentGrid.CanPlace(player, infantry, 0, 0, false, _rules));
            Assert.IsTrue(DeploymentGrid.CanPlace(player, infantry, 75, 38, false, _rules));
            Assert.IsFalse(DeploymentGrid.CanPlace(player, infantry, 76, 0, false, _rules));
            Assert.IsFalse(DeploymentGrid.CanPlace(player, infantry, 0, 39, false, _rules));
            Assert.IsFalse(DeploymentGrid.CanPlace(player, infantry, -1, 0, false, _rules));
        }

        [Test]
        public void Squads_CannotOverlap_ButAirIgnoresGround()
        {
            var player = new PlayerState(Team.A);
            UnitStats infantry = TestMatch.Infantry();
            player.Squads.Add(new PlacedSquad { Id = 1, Type = infantry, Column = 10, Row = 10 });
            Assert.IsFalse(DeploymentGrid.CanPlace(player, infantry, 14, 11, false, _rules), "Overlaps the corner.");
            Assert.IsTrue(DeploymentGrid.CanPlace(player, infantry, 15, 10, false, _rules), "Right next to it is fine.");
            Assert.IsTrue(DeploymentGrid.CanPlace(player, infantry, 10, 10, false, _rules, ignoreSquadId: 1), "A squad can be put back on its own spot.");

            UnitStats flyer = TestMatch.Tank();
            flyer.IsAir = true;
            Assert.IsTrue(DeploymentGrid.CanPlace(player, flyer, 10, 10, false, _rules));
        }

        [Test]
        public void FindFreeSpot_StartsInTheMiddleOfTheZone_ThenTheNearestFreeSpot()
        {
            var player = new PlayerState(Team.A);
            UnitStats infantry = TestMatch.Infantry();
            DeploymentGrid.Footprint(infantry, false, _rules, out int width, out int depth);
            Assert.IsTrue(DeploymentGrid.FindFreeSpot(player, infantry, false, _rules, out int column, out int row));
            Assert.AreEqual((80 - width) / 2, column);
            Assert.AreEqual((40 - depth) / 2, row);

            player.Squads.Add(new PlacedSquad { Id = 1, Type = infantry, Column = column, Row = row });
            Assert.IsTrue(DeploymentGrid.FindFreeSpot(player, infantry, false, _rules, out int column2, out int row2));
            Assert.IsTrue(DeploymentGrid.CanPlace(player, infantry, column2, row2, false, _rules));
            Assert.AreEqual(column, column2, "The nearest free spot is just in front of the first squad.");
            Assert.AreEqual(row - depth, row2);
        }

        [Test]
        public void Battle_PlacesSquadsWhereTheyWereDeployed_MirroredForTheEnemy()
        {
            var match = TestMatch.Started(out UnitStats infantry, out _, out _, out _);
            match.Player.Squads.Clear();
            match.Enemy.Squads.Clear();
            match.Player.Squads.Add(new PlacedSquad { Id = 1, Type = infantry, Column = 0, Row = 0 });
            match.Enemy.Squads.Add(new PlacedSquad { Id = 2, Type = infantry, Column = 0, Row = 0, Rotated90 = true });

            BattleSimulation battle = match.BuildBattle(includeEnemy: true);
            Assert.AreEqual(24, battle.Units.Count);
            // Team A's squad covers columns 0-4 and rows 0-1 of its zone: x -80..-70 m, y -2..-6 m.
            foreach (SimUnit unit in battle.Units)
            {
                if (unit.Team == Team.A)
                {
                    Assert.That(unit.Position.X, Is.InRange(-80f, -70f));
                    Assert.That(unit.Position.Y, Is.InRange(-6f, -2f));
                }
                else
                {
                    // Turned and mirrored: 2 squares wide, 5 deep, on Team B's left (world right).
                    Assert.That(unit.Position.X, Is.InRange(76f, 80f));
                    Assert.That(unit.Position.Y, Is.InRange(2f, 12f));
                }
            }
        }
    }
}
