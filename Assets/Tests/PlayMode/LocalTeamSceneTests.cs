// LocalTeamSceneTests.cs
// M11 Stage 3 in the real Match scene: a LAN guest plays Team B of a two-humans match (built as Stage 4 will, with LanMatchFactory) and sees
// its own army at the bottom: the camera's home turned 180 degrees, its army live in the preview with the host's as grey ghosts, its bodies and
// squares in the own colours, and a squad it places through the controller and the mouse landing in its own half. While its own card offer is
// open, its tower and squad windows offer nothing to press, as the Cards phase does against the AI. A single-player match still
// has Team A, the camera at yaw 0 and the old colours, also after a guest's match.
// Depends on: Halobellum.Presentation, Halobellum.Net (LanMatchFactory), Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class LocalTeamSceneTests
    {
        private MatchController _match;
        private BattleController _battle;
        private BattleCameraController _camera;
        private UnitViewManager _views;

        private IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync("Match", LoadSceneMode.Single);
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            _camera = Object.FindAnyObjectByType<BattleCameraController>();
            _views = Object.FindAnyObjectByType<UnitViewManager>();
            Assert.IsNotNull(_match, "The Match scene has no MatchController.");
            Assert.IsNotNull(_camera, "The Match scene has no BattleCameraController.");
            _match.NewMatch(3);
            yield return null;
        }

        /// <summary>The guest's side of a LAN match: a two-humans match from the scene's own content, shown as Team B, both starting cards picked.</summary>
        private IEnumerator StartAsGuest(int seed = 7)
        {
            List<FactionInfo> factions = _match.PrepareFactions();
            var settings = new LanMatchSettings { Seed = seed, Mode = MatchMode.Faction, BaseHealth = 2000, Cards = CardFilter.AllCards };
            Match m = LanMatchFactory.Build(_match.FreshRules(), _match.CombatRules, factions, settings);
            Assert.IsTrue(m.IsTwoHumans);
            _match.UseMatch(m, Team.B);
            yield return null;
            Assert.AreEqual(Team.B, _match.LocalTeam);
            Assert.AreSame(m.EnemyDraft, _match.MyDraft, "The guest picks from Team B's cards.");

            Assert.IsTrue(_match.ChooseDraft(0).Ok, "The guest's own pick.");
            Assert.AreEqual(MatchPhase.Draft, m.Phase, "The round waits for the host's pick.");
            Assert.IsTrue(m.ChooseDraft(Team.A, 0).Ok, "The host's pick, as the coordinator applies it.");
            _match.OnRemoteCommandsApplied();
            yield return null;
            Assert.AreEqual(MatchPhase.Deployment, m.Phase);
        }

        [UnityTest]
        public IEnumerator AGuest_SeesItsOwnArmyAtTheBottom_TheCameraTurned()
        {
            yield return LoadMatch();
            yield return StartAsGuest();
            Match m = _match.Match;

            Assert.AreEqual(Team.B, _camera.HomeTeam);
            Assert.AreEqual(180f, _camera.Yaw, 0.01f, "The guest's home view looks from Team B's edge.");
            Assert.Greater(_camera.Focus.y, 0f, "Its focus leans to Team B's half, as Team A's leans to its own.");
            Assert.Greater(_camera.transform.position.z, 0f, "The camera stands at Team B's end of the field.");

            BattleSimulation preview = _battle.Simulation;
            Assert.IsNotNull(preview);
            Assert.AreNotSame(m.CurrentBattle, preview, "This is the preview, not a battle.");
            int own = m.Enemy.Squads.Count;
            Assert.Greater(own, 0, "Team B has its starting squads.");
            for (int i = 0; i < own; i++) Assert.AreEqual(Team.B, preview.Squads[i].Team, "The guest's squads come first, live.");
            Assert.AreEqual(own + m.SeenArmy(Team.A).Count, preview.Squads.Count, "Then the host's army as it was seen, as ghosts.");
            for (int i = own; i < preview.Squads.Count; i++) Assert.AreEqual(Team.A, preview.Squads[i].Team);
            Assert.AreEqual(Team.A, _views.GhostTeam, "The host's squads are the grey ghosts.");
            Assert.AreSame(m.Enemy.Squads[0], _match.PlacedFor(preview.Squads[0].Id, out Team team), "A preview squad is the guest's own placed squad.");
            Assert.AreEqual(Team.B, team);

            Assert.AreEqual(Team.B, _views.LocalTeam);
            Assert.AreEqual(_views.OwnColor, _views.SideColor(Team.B), "The guest's bodies take the own (blue) colour.");
            Assert.AreNotEqual(_views.OwnColor, _views.SideColor(Team.A), "The host's take the enemy's.");
            SimUnit body = preview.Units.First(u => u.Team == Team.B && !u.Stats.IsTower);
            SimUnit hostTower = preview.Units.First(u => u.Team == Team.A && u.Stats.IsTower);
            Assert.IsTrue(_views.HasFriendlyHealthBar(body), "The guest's units have the friendly health bar.");
            Assert.IsFalse(_views.HasFriendlyHealthBar(hostTower), "The host's tower has the enemy's.");
            BattlefieldView field = _match.GetComponent<BattlefieldView>();
            Assert.AreNotEqual(field.SquaresColor(Team.A), field.SquaresColor(Team.B), "The two sides' squares have their own colours.");
            Assert.AreEqual(Team.B, field.LocalTeam);
            Assert.AreEqual(Team.B, _match.PlacingTeam);
        }

        [UnityTest]
        public IEnumerator AGuestsSquad_PlacedWithTheMouse_LandsInTeamBsHalf()
        {
            yield return LoadMatch();
            yield return StartAsGuest();
            Match m = _match.Match;
            CombatRules rules = _battle.Rules;
            m.Enemy.Credits += 3000;
            int hostSquads = m.Player.Squads.Count;
            UnitStats type = m.Enemy.Unlocked.OrderBy(u => u.Cost).First();

            Assert.IsTrue(_match.Buy(type).Ok, _match.Message);
            PlacedSquad squad = m.Enemy.Squads[m.Enemy.Squads.Count - 1];
            Assert.AreEqual(hostSquads, m.Player.Squads.Count, "Nothing was bought for the host.");
            Assert.AreEqual(Team.B, m.Enemy.Team);
            yield return null;

            _match.BeginMoving(squad);
            Assert.IsTrue(_match.IsPlacing);
            int column = -1, row = -1;
            for (int r = 0; r < 4 && column < 0; r++)
            {
                for (int c = 1; c < 8; c++)
                {
                    if ((c != squad.Column || r != squad.Row) && _match.CanPlaceAt(c, r))
                    {
                        column = c;
                        row = r;
                        break;
                    }
                }
            }
            Assert.GreaterOrEqual(column, 0, "A free spot to move to.");
            Vec2 centre = DeploymentGrid.WorldCentre(type, column, row, _match.PlacingRotated, Team.B, rules);
            Assert.Greater(centre.Y, 0f, "Team B's grid is in the north half.");

            // The guest's turned camera, looking at the spot; the pointer on it, then a click.
            _camera.SetView(new Vector2(centre.X, centre.Y), 180f, 55f, 90f);
            var input = _match.GetComponent<DeploymentInput>();
            Vector3 screen = Camera.main.WorldToScreenPoint(new Vector3(centre.X, 0f, centre.Y));
            Assert.IsTrue(input.PointerCell(screen, out int pointedColumn, out int pointedRow));
            Assert.AreEqual((column, row), (pointedColumn, pointedRow), "The pointer finds the cell under it in the guest's own grid.");
            input.PointAt(screen, true);
            Assert.IsFalse(_match.IsPlacing, _match.Message);
            Assert.AreEqual((column, row), (squad.Column, squad.Row), "The squad stands where the guest clicked.");
            yield return null;
            int sim = _match.PreviewSquadOfPlaced(squad.Id);
            SimSquad shown = _battle.Simulation.Squads.First(s => s.Id == sim);
            Assert.AreEqual(Team.B, shown.Team);
            Assert.Greater(shown.Members[0].Position.Y, 0f, "Its bodies stand in Team B's half.");
        }

        [UnityTest]
        public IEnumerator AGuestsOwnCardOffer_DisablesTheTowerAndSquadWindows_AsTheCardsPhaseDoes()
        {
            yield return LoadMatch();
            yield return StartAsGuest();
            Match m = _match.Match;
            m.Enemy.Credits += 3000;
            BattleSimulation preview = _battle.Simulation;
            int ownSquad = preview.Squads[0].Id;
            Assert.AreEqual(Team.B, preview.Squads[0].Team);

            // The phase stays Deployment while the guest's own offer is open. Checked in the same frame, before the HUD draws the offer.
            m.Enemy.Offer = new CardOffer { Kind = CardKind.General, Round = m.Round, SkipBonus = 0 };
            Assert.AreEqual(MatchPhase.Deployment, m.Phase);
            Assert.IsTrue(_match.IsChoosingCards);
            Assert.IsFalse(_match.IsDeploying);
            TowerPanel closed = _match.DescribeTower(Team.B, TowerKind.CommandCenter);
            Assert.IsNotNull(closed);
            Assert.IsNotEmpty(closed.Buttons);
            foreach (TowerButton button in closed.Buttons) Assert.IsFalse(button.Enabled, $"'{button.Label}' waits for the card, as in the Cards phase.");
            Assert.IsNull(_match.PlacedFor(ownSquad, out _), "Behind the card screen, a squad has no window, as in the Cards phase.");
            Assert.IsNull(_match.DescribeProgress(ownSquad));

            m.Enemy.Offer = null;
            Assert.IsTrue(_match.IsDeploying);
            TowerPanel open = _match.DescribeTower(Team.B, TowerKind.CommandCenter);
            Assert.IsTrue(open.Buttons.Any(b => b.Enabled), "With the offer gone, the Command Center's buttons work again.");
            Assert.AreSame(m.Enemy.Squads[0], _match.PlacedFor(ownSquad, out Team team));
            Assert.AreEqual(Team.B, team);
            Assert.IsNotNull(_match.DescribeProgress(ownSquad));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SinglePlayer_StaysTeamA_AlsoAfterAGuestsMatch()
        {
            yield return LoadMatch();
            Assert.AreEqual(Team.A, _match.LocalTeam);
            Assert.AreEqual(Team.A, _camera.HomeTeam);
            Assert.AreEqual(0f, _camera.Yaw, 0.01f);
            Assert.Less(_camera.Focus.y, 0f, "The single-player focus leans to Team A's half.");
            BattlefieldView field = _match.GetComponent<BattlefieldView>();
            Color ownSquares = field.SquaresColor(Team.A);
            Assert.IsTrue(_match.ChooseDraft(0).Ok);
            yield return null;
            Assert.AreEqual(_views.OwnColor, _views.SideColor(Team.A), "Team A's bodies are blue, as always.");
            SimUnit body = _battle.Simulation.Units.First(u => u.Team == Team.A && !u.Stats.IsTower);
            Assert.IsTrue(_views.HasFriendlyHealthBar(body));
            Assert.AreEqual(Team.B, _views.GhostTeam);
            Assert.AreEqual(Team.A, _battle.Simulation.Squads[0].Team, "The player's squads come first.");

            yield return StartAsGuest();
            Assert.AreEqual(180f, _camera.Yaw, 0.01f);
            Assert.AreEqual(ownSquares, field.SquaresColor(Team.B), "The guest's squares take the own colour.");
            _match.NewMatch(3);
            yield return null;
            Assert.AreEqual(Team.A, _match.LocalTeam, "A new single-player match is Team A again.");
            Assert.AreEqual(Team.A, _camera.HomeTeam);
            Assert.AreEqual(0f, _camera.Yaw, 0.01f, "And the camera is back at Team A's edge.");
            Assert.AreEqual(Team.A, _views.LocalTeam);
            Assert.AreEqual(Team.A, field.LocalTeam);
            Assert.AreEqual(ownSquares, field.SquaresColor(Team.A), "Team A's squares are the own colour again.");
        }
    }
}
