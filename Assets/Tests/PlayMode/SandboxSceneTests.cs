// SandboxSceneTests.cs
// Plays the real Sandbox scene (Milestone 4): every slice unit is offered, squads can be placed on
// both sides (snapped to each side's grid, no overlap), moved, turned, removed, copied to the other
// side; Run plays the battle with both armies and Reset brings back the same placement. The
// placeholder test units are offered too (sandbox only). Every item can be put on squads (D34).
// Depends on: Halobellum.Presentation (SandboxController, BattleController), Halobellum.Economy,
//             Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Linq;
using Halobellum.Combat;
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
        public IEnumerator OffersEverySliceUnit_FromBothFactions_PlusThePlaceholders()
        {
            yield return LoadSandbox();
            Assert.AreEqual(38, _sandbox.Units.Count);
            Assert.AreEqual(11, _sandbox.Units.Count(u => u.Faction == "Flood"));
            Assert.AreEqual(12, _sandbox.Units.Count(u => u.Faction == "UNSC"));
            Assert.AreEqual(12, _sandbox.Units.Count(u => u.Faction == "Covenant"));
            Assert.AreEqual(3, _sandbox.Units.Count(u => u.Faction == SandboxController.ExtraUnitsLabel));
            Assert.IsTrue(_sandbox.IsDeploying);
        }

        [UnityTest]
        public IEnumerator Flood_ShowsItsForms_AndMarinesRiseInTheRealScene()
        {
            yield return LoadSandbox();
            UnitStats forms = Unit("Infection Forms");
            _sandbox.AddSquad(Team.A, forms);
            _sandbox.AddSquad(Team.A, forms);
            _sandbox.AddSquad(Team.A, forms);
            _sandbox.AddSquad(Team.B, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Scorpion"));
            yield return null;

            GameObject pack = GameObject.Find("Units").transform.GetChild(0).gameObject;
            Assert.AreEqual(5, pack.GetComponentsInChildren<Transform>(false).Count(t => t.name.StartsWith("Form ")), "A pack shows its 5 forms.");

            _battle.Speed = 4f;
            _sandbox.Run();
            float limit = Time.realtimeSinceStartup + 40f;
            SimUnit risen = null;
            while (risen == null && !_battle.Simulation.IsFinished && Time.realtimeSinceStartup < limit)
            {
                yield return null;
                risen = _battle.Simulation.Units.FirstOrDefault(u => u.Stats.InfectedFrom != null);
            }
            Assert.IsNotNull(risen, "A Marine rose.");
            Assert.AreEqual("Infected Marine", risen.Stats.Name);
            Assert.AreEqual(Team.A, risen.Team);
            yield return null;
            Assert.IsNotNull(GameObject.Find($"Infected Marine #{risen.Id} (A)"), "The risen Marine has a view.");
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
        public IEnumerator LevelsAndTechs_CanBeSetFreely_PerSide()
        {
            yield return LoadSandbox();
            UnitStats pelican = Unit("Pelican");
            _sandbox.AddSquad(Team.A, pelican);
            PlacedSquad squad = _sandbox.ArmyA.Squads[0];
            _sandbox.SetLevel(squad, 3);
            Assert.AreEqual(3, squad.Level);
            Assert.AreEqual(pelican.Cost * 2, _sandbox.ArmyCost(Team.A), "Worth its price plus half per level above 1.");

            TechUpgrade bay = pelican.TechUpgrades.Single(t => t.UnlocksReinforcement);
            _sandbox.ToggleTech(Team.A, pelican, bay);
            Assert.IsTrue(_sandbox.ArmyA.HasTech(pelican, bay));
            Assert.IsFalse(_sandbox.ArmyB.HasTech(pelican, bay), "Techs are per side.");
            yield return null;
            Assert.IsTrue(_battle.Simulation.Units[0].Stats.HasReinforcement, "The Troop Bay switches the drop on.");
            Assert.AreEqual(pelican.MaxHealth * 3f, _battle.Simulation.Units[0].Stats.MaxHealth, 1e-3f);
            Assert.IsNotNull(_sandbox.DescribeProgress(_battle.Simulation.Squads[0].Id));

            _sandbox.ToggleTech(Team.A, pelican, bay);
            yield return null;
            Assert.IsFalse(_battle.Simulation.Units[0].Stats.HasReinforcement, "Removing the tech switches it off again.");
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

            float deadline = Time.realtimeSinceStartup + 120f;
            while (!_battle.Simulation.IsFinished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_battle.Simulation.IsFinished, "The battle did not finish in time.");
            Assert.AreEqual(bodies, _battle.Simulation.Units.Count, "The Pelican carries no troops before its M6 upgrade.");

            _sandbox.ResetBattle();
            yield return null;
            Assert.IsFalse(_sandbox.IsBattle);
            Assert.AreEqual(placedA, _sandbox.ArmyA.Squads.Count, "Same placement after Reset.");
            Assert.AreEqual(placedB, _sandbox.ArmyB.Squads.Count);
            Assert.AreEqual(bodies, CountViews());
            Assert.IsFalse(_battle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator ShieldedUnits_ShowABlueBarOverTheirHealthBar()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Spartan"));
            _sandbox.AddSquad(Team.B, Unit("Marine"));
            yield return null;
            var views = _battle.GetComponent<UnitViewManager>();
            var selection = _battle.GetComponent<SquadSelection>();
            SimUnit spartan = _battle.Simulation.Squads[0].Members[0];
            SimUnit marine = _battle.Simulation.Squads[1].Members[0];
            yield return null;
            Assert.AreEqual(-1f, views.ShieldBarFraction(spartan), "Bars stay hidden at full health and shield.");

            spartan.Shield = spartan.Stats.MaxShield * 0.5f;
            yield return null;
            Assert.AreEqual(0.5f, views.ShieldBarFraction(spartan), 1e-3f, "A damaged shield shows, half full.");

            selection.Select(_battle.Simulation.Squads[1].Id);
            yield return null;
            Assert.AreEqual(-1f, views.ShieldBarFraction(marine), "A selected unit without a shield has no shield bar.");
        }

        [UnityTest]
        public IEnumerator OffersEveryItem_OnceEach_NoSpells_AndTheyWorkOnBothSides()
        {
            yield return LoadSandbox();
            Assert.AreEqual(8, _sandbox.Items.Count, "All 8 items, once each.");
            Assert.AreEqual(8, _sandbox.Items.Select(i => i.Name).Distinct().Count());
            Assert.IsTrue(_sandbox.Items.All(i => _sandbox.IconFor(i) != null), "With their icons.");

            UnitStats marine = Unit("Marine");
            _sandbox.AddSquad(Team.A, marine);
            _sandbox.AddSquad(Team.B, marine);
            yield return null;
            PlacedSquad blue = _sandbox.ArmyA.Squads[0], red = _sandbox.ArmyB.Squads[0];
            int costBefore = _sandbox.ArmyCost(Team.B);

            ItemStats armor = _sandbox.Items.Single(i => i.Name == "Heavy Armor");
            _sandbox.BeginPlacingItem(armor);
            Assert.AreSame(armor, _sandbox.PlacingItem);
            Assert.IsTrue(_sandbox.SquadOnScreen(red, out _), "Its hexagon can be drawn over the squad.");
            Assert.IsTrue(_sandbox.PlaceItemOn(red).Ok);
            Assert.AreSame(armor, red.Item);
            Assert.IsNull(_sandbox.PlacingItem);
            Assert.Greater(_sandbox.ArmyCost(Team.B), costBefore, "The item counts toward the army's value.");
            yield return null;
            SimSquad redSquad = _battle.Simulation.Squads[1];
            Assert.AreEqual(marine.MaxHealth * 1.7f, redSquad.Members[0].Stats.MaxHealth, 1e-2f, "The whole squad gets it.");

            _sandbox.BeginPlacingItem(armor);
            Assert.IsFalse(_sandbox.PlaceItemOn(red).Ok, "One item per squad.");
            Assert.IsTrue(_sandbox.PlaceItemOn(blue).Ok, "The same item can go on any number of squads.");

            SquadProgressView view = _sandbox.DescribeProgress(redSquad.Id);
            StringAssert.Contains("Heavy Armor", view.ItemText);
            Assert.IsNotNull(view.OnRemoveItem);
            view.OnRemoveItem();
            Assert.IsNull(red.Item, "It can be taken off again.");

            _sandbox.CopyToOtherSide(Team.A);
            Assert.AreSame(armor, _sandbox.ArmyB.Squads[0].Item, "Copying a side copies its items.");
            _sandbox.Run();
            yield return null;
            Assert.AreEqual(marine.MaxHealth * 1.7f, _battle.Simulation.Squads[1].Members[0].Stats.MaxHealth, 1e-2f, "They count in the battle.");
        }
    }
}
