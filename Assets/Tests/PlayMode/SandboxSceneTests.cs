// SandboxSceneTests.cs
// Plays the real Sandbox scene (Milestone 4): every slice unit is offered, squads can be placed on
// both sides (snapped to each side's grid, no overlap), moved, turned, removed, copied to the other
// side; Run plays the battle with both armies and Reset brings back the same placement. A Pelican's
// Marines appear mid-battle with views.
// Depends on: Halobellum.Presentation (SandboxController, BattleController), Halobellum.Economy,
//             Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class SandboxSceneTests
    {
        private SandboxController _sandbox;
        private BattleController _battle;

        private IEnumerator LoadSandbox()
        {
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            _sandbox = Object.FindAnyObjectByType<SandboxController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            Assert.IsNotNull(_sandbox, "The Sandbox scene has no SandboxController.");
        }

        private UnitStats Unit(string name) => _sandbox.Units.Single(u => u.Stats.Name == name).Stats;

        private static int CountViews()
        {
            GameObject units = GameObject.Find("Units");
            return units == null ? 0 : units.transform.childCount;
        }

        [UnityTest]
        public IEnumerator OffersEverySliceUnit_FromBothFactions()
        {
            yield return LoadSandbox();
            Assert.AreEqual(24, _sandbox.Units.Count);
            Assert.AreEqual(12, _sandbox.Units.Count(u => u.Faction == "UNSC"));
            Assert.AreEqual(12, _sandbox.Units.Count(u => u.Faction == "Covenant"));
            Assert.IsTrue(_sandbox.IsDeploying);
        }

        [UnityTest]
        public IEnumerator PlacesOnBothSides_OnEachSidesGrid()
        {
            yield return LoadSandbox();
            UnitStats marine = Unit("Marine"), grunt = Unit("Grunt");

            _sandbox.EditingTeam = Team.A;
            _sandbox.BeginPlacing(marine);
            Assert.AreEqual(Team.A, _sandbox.PlacingTeam);
            Assert.IsTrue(_sandbox.PlaceAt(10, 5).Ok, _sandbox.Message);
            Assert.IsTrue(_sandbox.IsPlacing, "Placing continues with the same unit.");
            Assert.IsFalse(_sandbox.CanPlaceAt(10, 5), "No overlap.");
            Assert.IsFalse(_sandbox.CanPlaceAt(79, 5), "Must stay inside the zone.");
            _sandbox.CancelPlacement();

            _sandbox.EditingTeam = Team.B;
            _sandbox.BeginPlacing(grunt);
            Assert.AreEqual(Team.B, _sandbox.PlacingTeam);
            Assert.IsTrue(_sandbox.CanPlaceAt(10, 5), "Each side has its own grid.");
            Assert.IsTrue(_sandbox.PlaceAt(10, 5).Ok, _sandbox.Message);
            _sandbox.CancelPlacement();
            yield return null;

            Assert.AreEqual(1, _sandbox.ArmyA.Squads.Count);
            Assert.AreEqual(1, _sandbox.ArmyB.Squads.Count);
            Assert.AreEqual(100, _sandbox.ArmyCost(Team.A));
            Assert.AreEqual(marine.SquadSize + grunt.SquadSize, CountViews(), "Both armies are shown while placing.");
            Assert.AreEqual(2, _battle.Simulation.Squads.Count);
            Assert.Less(_battle.Simulation.Units.First(u => u.Team == Team.A).Position.Y, 0f, "Blue stands in the bottom half.");
            Assert.Greater(_battle.Simulation.Units.First(u => u.Team == Team.B).Position.Y, 0f, "Red stands in the top half.");
        }

        [UnityTest]
        public IEnumerator MoveTurnRemoveCopyAndClear()
        {
            yield return LoadSandbox();
            UnitStats scorpion = Unit("Scorpion");
            Assert.IsTrue(_sandbox.AddSquad(Team.A, scorpion).Ok);
            PlacedSquad squad = _sandbox.ArmyA.Squads[0];

            _sandbox.BeginMoving(squad);
            Assert.IsTrue(_sandbox.PlaceAt(0, 0).Ok, _sandbox.Message);
            Assert.AreEqual(0, squad.Column);
            Assert.IsFalse(_sandbox.IsPlacing, "Moving ends after one click.");

            _sandbox.CopyToOtherSide(Team.A);
            Assert.AreEqual(1, _sandbox.ArmyB.Squads.Count);
            Assert.AreEqual(0, _sandbox.ArmyB.Squads[0].Column, "Copied to the same spot in red's zone.");
            Assert.AreEqual(_sandbox.ArmyCost(Team.A), _sandbox.ArmyCost(Team.B));

            Assert.IsTrue(_sandbox.Remove(squad).Ok);
            Assert.AreEqual(0, _sandbox.ArmyA.Squads.Count);
            _sandbox.Clear(Team.B);
            Assert.AreEqual(0, _sandbox.ArmyB.Squads.Count);
            yield return null;
            Assert.AreEqual(0, CountViews());
        }

        [UnityTest]
        public IEnumerator Run_PlaysTheBattle_ThenResetBringsBackThePlacement()
        {
            yield return LoadSandbox();
            _sandbox.Run();
            Assert.IsFalse(_sandbox.IsBattle, "Needs a squad on each side.");

            for (int i = 0; i < 3; i++) _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.A, Unit("Pelican"));
            for (int i = 0; i < 3; i++) _sandbox.AddSquad(Team.B, Unit("Grunt"));
            _sandbox.AddSquad(Team.B, Unit("Elite"));
            int placedA = _sandbox.ArmyA.Squads.Count, placedB = _sandbox.ArmyB.Squads.Count;
            int bodies = 3 * 18 + 1 + 3 * 20 + 3;

            _battle.Speed = 16f;
            _sandbox.Run();
            Assert.IsTrue(_sandbox.IsBattle);
            Assert.IsFalse(_sandbox.IsDeploying);
            Assert.IsTrue(_battle.IsPlaying);
            yield return null; // Views are rebuilt; the old ones go at the end of the frame.
            int viewsAtStart = CountViews();
            Assert.AreEqual(bodies, viewsAtStart);

            bool dropped = false;
            float pelicanDiedAt = float.MaxValue;
            _battle.Simulation.Events.ReinforcementDropped += (carrier, squadId) => dropped = true;
            _battle.Simulation.Events.UnitDied += (unit, killer) =>
            {
                if (unit.Stats.Name == "Pelican") pelicanDiedAt = _battle.Simulation.ElapsedSeconds;
            };
            float deadline = Time.realtimeSinceStartup + 120f;
            while (!_battle.Simulation.IsFinished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_battle.Simulation.IsFinished, "The battle did not finish in time.");
            if (pelicanDiedAt >= 10f && _battle.Simulation.ElapsedSeconds >= 10f) Assert.IsTrue(dropped, "A Pelican alive at 10 s drops its Marines.");
            if (dropped)
            {
                Assert.AreEqual(bodies + 6, _battle.Simulation.Units.Count, "6 Marines dropped.");
                Assert.AreEqual(_battle.Simulation.Units.Count, CountViews(), "Dropped Marines get views too.");
            }

            _sandbox.ResetBattle();
            yield return null;
            Assert.IsFalse(_sandbox.IsBattle);
            Assert.AreEqual(placedA, _sandbox.ArmyA.Squads.Count, "Same placement after Reset.");
            Assert.AreEqual(placedB, _sandbox.ArmyB.Squads.Count);
            Assert.AreEqual(bodies, CountViews());
            Assert.IsFalse(_battle.IsPlaying);
        }
    }
}
