// SandboxSceneTests.cs
// Plays the real Sandbox scene (Milestone 4): every slice unit is offered, squads can be placed on
// both sides (snapped to each side's grid, no overlap), moved, turned, removed, copied to the other
// side; Run plays the battle with both armies and Reset brings back the same placement. The
// placeholder test units are offered too (sandbox only). Every item can be put on squads (D34).
// The towers switch: off is as if towers didn't exist; on stands 4 towers on the field, blocks their squares and moves the squads already there.
// Depends on: Halobellum.Presentation (SandboxController, BattleController), Halobellum.Economy,
//             Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.Units;
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

        [UnityTest]
        public IEnumerator TheResultLine_StandsUnderTheSandboxBar()
        {
            yield return LoadSandbox();
            var hud = Object.FindAnyObjectByType<Halobellum.UI.BattleHud>();
            Assert.IsNotNull(hud);
            Assert.AreEqual(Halobellum.UI.SandboxHud.ResultLineTop, hud.ResultLineTop);
        }

        [UnityTest]
        public IEnumerator TheMouseWheel_ZoomsTheCamera_OnlyWhenThePointerIsNotOverAWindow()
        {
            yield return LoadSandbox();
            var cameraController = Object.FindAnyObjectByType<BattleCameraController>();
            Assert.IsNotNull(cameraController);
            yield return null;
            float top = Screen.height;
            UiHitAreas.Add(new Rect(0f, 0f, 100f, 100f));
            float before = cameraController.Distance;

            // Over the window (GUI rows run from the top, the pointer's from the bottom).
            cameraController.Wheel(-1f, new Vector2(50f, top - 50f));
            Assert.AreEqual(before, cameraController.Distance, "The wheel scrolled a list: the view must stay.");

            // Over the battlefield.
            cameraController.Wheel(-1f, new Vector2(300f, top - 300f));
            Assert.Greater(cameraController.Distance, before, "The wheel over the battlefield zooms.");
        }

        private static int CountViews()
        {
            GameObject units = GameObject.Find("Units");
            return units == null ? 0 : units.transform.childCount;
        }

        [UnityTest]
        public IEnumerator OffersEverySliceUnit_FromBothFactions_PlusThePlaceholders()
        {
            yield return LoadSandbox();
            Assert.AreEqual(72, _sandbox.Units.Count, "35 base units, 34 exclusive to a sub-faction, and the 3 placeholders.");
            Assert.AreEqual(11, _sandbox.Units.Count(u => u.Faction == "Flood"));
            Assert.AreEqual(12, _sandbox.Units.Count(u => u.Faction == "UNSC"));
            Assert.AreEqual(12, _sandbox.Units.Count(u => u.Faction == "Covenant"));
            Assert.AreEqual(5, _sandbox.Units.Count(u => u.Faction == "UNSC: Noble"));
            Assert.AreEqual(6, _sandbox.Units.Count(u => u.Faction == "Flood: Covenant-based"));
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
            Assert.AreEqual(6, pack.GetComponentsInChildren<Transform>(false).Count(t => t.name.StartsWith("Form ")), "A pack shows its 6 forms.");

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
        public IEnumerator Marines_PlayTheirAnimations_AndFallBeforeTheyGo()
        {
            yield return LoadSandbox();
            // Grunts have a shorter range than Marines: the Marines get to fire before any of them falls.
            _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Grunt"));
            _sandbox.AddSquad(Team.B, Unit("Grunt"));
            yield return null;

            var views = _battle.GetComponent<UnitViewManager>();
            SimUnit first = _battle.Simulation.Units.First(u => u.Stats.Name == "Marine");
            Assert.AreEqual(BakedAnimation.Still, views.PlayingAnimation(first), "Time stands still in deployment.");
            SimUnit grunt = _battle.Simulation.Units.First(u => u.Stats.Name == "Grunt");
            Assert.AreEqual(BakedAnimation.Still, views.PlayingAnimation(grunt), "Grunts have their own animated body too.");
            Assert.Greater(views.HealthBarColor(first).g, 0.6f, "Your units have green health bars.");
            Assert.Greater(views.HealthBarColor(grunt).r, 0.6f, "The enemy's are red.");
            Assert.Less(views.HealthBarColor(grunt).g, 0.5f);
            Assert.AreEqual(0f, views.BakedTintStrength(first), "A real model keeps its own colours.");

            _battle.Speed = 4f;
            _sandbox.Run();
            yield return null;
            bool moved = false, shot = false;
            SimUnit fallen = null;
            float limit = Time.realtimeSinceStartup + 40f;
            while (fallen == null && Time.realtimeSinceStartup < limit)
            {
                yield return null;
                foreach (SimUnit unit in _battle.Simulation.Units.Where(u => u.Stats.Name == "Marine"))
                {
                    BakedAnimation? playing = views.PlayingAnimation(unit);
                    moved |= playing == BakedAnimation.Moving && unit.State == UnitState.Move;
                    shot |= playing == BakedAnimation.Shooting && unit.State == UnitState.Attack;
                    if (!unit.IsAlive) fallen = unit;
                }
            }
            Assert.IsTrue(moved, "They run.");
            Assert.IsTrue(shot, "They shoot.");
            Assert.IsNotNull(fallen, "One of them fell.");
            yield return null;
            Assert.AreEqual(BakedAnimation.Dead, views.PlayingAnimation(fallen));
            Assert.IsTrue(views.IsShown(fallen), "It stays on the field to play its death.");
            limit = Time.realtimeSinceStartup + 20f;
            while (views.IsShown(fallen) && !_battle.Simulation.IsFinished && Time.realtimeSinceStartup < limit) yield return null;
            limit = Time.realtimeSinceStartup + 16f;
            while (views.IsShown(fallen) && Time.realtimeSinceStartup < limit) yield return null;
            Assert.IsFalse(views.IsShown(fallen), "Then it goes.");
        }

        [UnityTest]
        public IEnumerator Scorpion_KeepsItsPaint_AndItsBarSitsAboveIt_ButItsGhostIsGrey()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Scorpion"));
            _sandbox.AddSquad(Team.B, Unit("Scorpion"));
            yield return null;

            var views = _battle.GetComponent<UnitViewManager>();
            SimUnit blue = _battle.Simulation.Units.First(u => u.Team == Team.A);
            SimUnit red = _battle.Simulation.Units.First(u => u.Team == Team.B);
            Assert.AreEqual(views.BodyColor(blue), views.BodyColor(red), "Both sides have the same paint.");
            Assert.Greater(views.BodyColor(blue).g, views.BodyColor(blue).b, "Olive, not the side's blue.");
            Transform bar = GameObject.Find("Units").transform.GetChild(blue.Id).Find("Health Bar");
            Assert.Greater(bar.localPosition.y, 2f, "Above the top of the real model.");
        }

        [UnityTest]
        public IEnumerator AirUnits_Fly13MetresUp_AndAMuzzleSaysWhereShotsStart()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Hornet"));
            _sandbox.AddSquad(Team.A, Unit("Wraith"));
            _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Scorpion"));
            yield return null;

            var views = _battle.GetComponent<UnitViewManager>();
            Assert.AreEqual(13f, views.AirHeight);
            SimUnit hornet = _battle.Simulation.Units.First(u => u.Stats.Name == "Hornet");
            Assert.AreEqual(13f, views.WorldPosition(hornet).y);

            SimUnit wraith = _battle.Simulation.Units.First(u => u.Stats.Name == "Wraith");
            Assert.IsTrue(views.TryMuzzle(wraith, 0, 0, out Vector3 muzzle));
            Assert.Greater(muzzle.y, 1.5f, "From its gun, not from the ground.");
            SimUnit marine = _battle.Simulation.Units.First(u => u.Stats.Name == "Marine");
            Assert.IsTrue(views.TryMuzzle(marine, 0, 0, out muzzle));
            Assert.Greater(muzzle.y, 0.8f);
        }

        [UnityTest]
        public IEnumerator Wraith_TurnsItsGunTowardItsTarget()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Wraith"));
            _sandbox.AddSquad(Team.B, Unit("Marine"));
            yield return null;
            _battle.Speed = 4f;
            _sandbox.Run();
            yield return null;
            SimUnit wraith = _battle.Simulation.Units.First(u => u.Stats.Name == "Wraith");
            Transform view = GameObject.Find("Units").transform.GetChild(wraith.Id);
            Transform gun = view.GetComponentsInChildren<Transform>().Single(t => t.name == "Turret_0");

            float give = 30f;
            while (give > 0f && wraith.IsAlive && wraith.Weapons[0].Target == null)
            {
                give -= Time.deltaTime;
                yield return null;
            }
            Assert.IsNotNull(wraith.Weapons[0].Target, "It should have found a target.");
            for (int frame = 0; frame < 120 && wraith.IsAlive && wraith.Weapons[0].Target != null; frame++) yield return null;
            if (wraith.IsAlive && wraith.Weapons[0].Target != null)
            {
                SimUnit target = wraith.Weapons[0].Target;
                var towards = new Vector3(target.Position.X - view.position.x, 0f, target.Position.Y - view.position.z);
                Assert.Less(Vector3.Angle(towards, gun.forward), 60f, "The gun points about at its target.");
            }
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

        private SimUnit[] Towers() => _battle.Simulation.Units.Where(u => u.Stats.IsTower).ToArray();

        [UnityTest]
        public IEnumerator Towers_AreOffAtFirst_AndTheBattleThenHasNone()
        {
            yield return LoadSandbox();
            UnitStats marine = Unit("Marine"), grunt = Unit("Grunt");
            _sandbox.AddSquad(Team.A, marine);
            _sandbox.AddSquad(Team.B, grunt);
            yield return null;

            Assert.IsFalse(_sandbox.TowersOn);
            Assert.AreEqual(0, _sandbox.ArmyA.Towers.Count + _sandbox.ArmyB.Towers.Count);
            Assert.AreEqual(0, Towers().Length);
            Assert.AreEqual(marine.SquadSize + grunt.SquadSize, CountViews());

            _sandbox.Run();
            yield return null;
            Assert.AreEqual(0, Towers().Length, "None in the battle either.");
            Assert.AreEqual(marine.SquadSize + grunt.SquadSize, _battle.Simulation.Units.Count);
        }

        [UnityTest]
        public IEnumerator TowersOn_StandsFourOnTheField_AndBlocksTheirSquares()
        {
            yield return LoadSandbox();
            _sandbox.SetTowers(true);
            yield return null;

            Assert.IsTrue(_sandbox.TowersOn);
            SimUnit[] towers = Towers();
            Assert.AreEqual(4, towers.Length);
            var views = _battle.GetComponent<UnitViewManager>();
            foreach (Team team in new[] { Team.A, Team.B })
            {
                PlayerState army = _sandbox.Army(team);
                Assert.AreEqual(2, army.Towers.Count);
                foreach (PlacedTower tower in army.Towers)
                {
                    SimUnit unit = towers.Single(u => u.Team == team && u.Stats.Shockwave.Kind == tower.Kind);
                    Vec2 centre = DeploymentGrid.TowerCentre(tower, team, _battle.Rules);
                    Assert.AreEqual(centre.X, unit.Position.X, 1e-3f);
                    Assert.AreEqual(centre.Y, unit.Position.Y, 1e-3f);
                    Assert.IsTrue(views.IsShown(unit), $"The {unit.Stats.Name} has a view.");
                }
            }
            Assert.AreEqual(4, CountViews());

            foreach (Team team in new[] { Team.A, Team.B })
            {
                PlacedTower tower = _sandbox.Army(team).Towers[0];
                _sandbox.EditingTeam = team;
                _sandbox.BeginPlacing(Unit("Marine"));
                Assert.IsFalse(_sandbox.CanPlaceAt(tower.Column, tower.Row), "A ground squad can't stand on a tower.");
                Assert.IsFalse(_sandbox.PlaceAt(tower.Column, tower.Row).Ok);
                _sandbox.BeginPlacing(Unit("Hornet"));
                Assert.IsTrue(_sandbox.CanPlaceAt(tower.Column, tower.Row), "An air squad flies over it.");
                _sandbox.CancelPlacement();
            }

            int blockedColumn = _sandbox.ArmyA.Towers[0].Column, blockedRow = _sandbox.ArmyA.Towers[0].Row;
            _sandbox.SetTowers(false);
            yield return null;
            Assert.IsFalse(_sandbox.TowersOn);
            Assert.AreEqual(0, Towers().Length);
            Assert.AreEqual(0, CountViews());
            _sandbox.EditingTeam = Team.A;
            _sandbox.BeginPlacing(Unit("Marine"));
            Assert.IsTrue(_sandbox.CanPlaceAt(blockedColumn, blockedRow), "The squares are free again.");
            _sandbox.CancelPlacement();
        }

        [UnityTest]
        public IEnumerator TowersOn_MovesTheSquadsStandingThere_AndLeavesTheOthers()
        {
            yield return LoadSandbox();
            UnitStats marine = Unit("Marine"), hornet = Unit("Hornet");
            // Where the towers will go, on a side's own grid (they are the same for both sides).
            _sandbox.SetTowers(true);
            PlacedTower commandCenter = _sandbox.ArmyA.FindTower(TowerKind.CommandCenter);
            int towerColumn = commandCenter.Column, towerRow = commandCenter.Row, towerSquares = commandCenter.Squares;
            _sandbox.SetTowers(false);

            _sandbox.EditingTeam = Team.A;
            _sandbox.BeginPlacing(marine);
            Assert.IsTrue(_sandbox.PlaceAt(towerColumn, towerRow).Ok, _sandbox.Message);
            Assert.IsTrue(_sandbox.PlaceAt(0, 0).Ok, _sandbox.Message);
            _sandbox.BeginPlacing(hornet);
            Assert.IsTrue(_sandbox.PlaceAt(towerColumn, towerRow).Ok, _sandbox.Message);
            _sandbox.CancelPlacement();
            _sandbox.CopyToOtherSide(Team.A);
            PlacedSquad[] blue = _sandbox.ArmyA.Squads.ToArray(), red = _sandbox.ArmyB.Squads.ToArray();

            _sandbox.SetTowers(true);
            yield return null;

            foreach (Team team in new[] { Team.A, Team.B })
            {
                PlayerState army = _sandbox.Army(team);
                PlacedSquad[] before = team == Team.A ? blue : red;
                CollectionAssert.AreEqual(before, army.Squads, "The order stays: it is the squads' numbering in the battle.");

                PlacedSquad moved = army.Squads[0];
                DeploymentGrid.Footprint(moved.Type, moved.Rotated90, _battle.Rules, out int width, out int depth);
                bool onTower = moved.Column < towerColumn + towerSquares && towerColumn < moved.Column + width
                               && moved.Row < towerRow + towerSquares && towerRow < moved.Row + depth;
                Assert.IsFalse(onTower, "The squad on the tower's squares has moved off them.");
                Assert.IsTrue(DeploymentGrid.CanPlace(army, moved.Type, moved.Column, moved.Row, moved.Rotated90, _battle.Rules, moved.Id), "To a free spot.");
                Assert.LessOrEqual(Mathf.Abs(moved.Column - towerColumn) + Mathf.Abs(moved.Row - towerRow), towerSquares + width + depth, "Close by.");

                Assert.AreEqual(0, army.Squads[1].Column, "A squad elsewhere stays where it was.");
                Assert.AreEqual(0, army.Squads[1].Row);
                Assert.AreEqual(towerColumn, army.Squads[2].Column, "An air squad stays over the tower.");
                Assert.AreEqual(towerRow, army.Squads[2].Row);
            }
            Assert.AreEqual(4, Towers().Length);
            Assert.AreEqual(6, _battle.Simulation.Squads.Count, "Six squads: towers belong to none.");
        }

        [UnityTest]
        public IEnumerator TowersOn_WithNoRoomForASquad_LeavesItThereAndSaysSo()
        {
            yield return LoadSandbox();
            UnitStats scarab = Unit("Scarab");
            DeploymentGrid.Footprint(scarab, false, _battle.Rules, out int width, out int depth);
            int id = 1000;
            for (int row = 0; row + depth <= _battle.Rules.DeploymentDepthSquares; row += depth)
            {
                for (int column = 0; column + width <= _battle.Rules.ArenaWidthSquares; column += width)
                {
                    _sandbox.ArmyA.Squads.Add(new PlacedSquad { Id = id++, Type = scarab, Column = column, Row = row, PricePaid = scarab.Cost });
                }
            }
            var spots = _sandbox.ArmyA.Squads.Select(s => (s.Column, s.Row)).ToArray();

            _sandbox.SetTowers(true);
            yield return null;

            Assert.IsTrue(_sandbox.TowersOn);
            CollectionAssert.AreEqual(spots, _sandbox.ArmyA.Squads.Select(s => (s.Column, s.Row)).ToArray(), "Nothing is lost or piled up.");
            StringAssert.Contains("No room", _sandbox.Message);
        }

        [UnityTest]
        public IEnumerator Towers_StandInTheBattle_AndResetKeepsThem()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Grunt"));
            _sandbox.SetTowers(true);
            int bodies = Unit("Marine").SquadSize + Unit("Grunt").SquadSize;

            _battle.Speed = 16f;
            _sandbox.Run();
            yield return null;
            Assert.AreEqual(4, Towers().Length);
            Assert.AreEqual(bodies + 4, CountViews());
            Assert.AreEqual(bodies, _battle.CountAlive(Team.A) + _battle.CountAlive(Team.B), "Towers aren't counted as soldiers.");
            _sandbox.SetTowers(false);
            Assert.IsTrue(_sandbox.TowersOn, "The switch only works while placing.");

            float deadline = Time.realtimeSinceStartup + 120f;
            while (!_battle.Simulation.IsFinished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_battle.Simulation.IsFinished, "The battle did not finish in time.");

            _sandbox.ResetBattle();
            yield return null;
            Assert.AreEqual(4, Towers().Length, "Still there after Reset.");
            Assert.IsTrue(Towers().All(t => t.IsAlive), "And whole again.");
            Assert.AreEqual(bodies + 4, CountViews());
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
