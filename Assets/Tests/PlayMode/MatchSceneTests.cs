// MatchSceneTests.cs
// Plays the real Match scene (Milestone 3): starting draft, deployment preview (the AI's army stays
// hidden), buying a squad (placed in the middle, dropped in, then selected), a full round
// (deployment, battle, result, next round with income), the AI difficulty choice, and full
// matches that are won, lost, and played to the end against the Normal AI (Milestone 5), the
// enemy's last-battle army shown as grey ghosts (D25) and the level-up arrow (D26), and cards
// (Milestone 7): the card screen from round 2, a spell placed and cast in battle, reinforcements.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class MatchSceneTests
    {
        private MatchController _match;
        private BattleController _battle;

        private IEnumerator LoadMatch(int seed = 3)
        {
            yield return SceneManager.LoadSceneAsync("Match", LoadSceneMode.Single);
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            Assert.IsNotNull(_match, "The Match scene has no MatchController.");
            _match.NewMatch(seed);
            yield return null;
        }

        /// <summary>Both sides' two towers (D55): they stand after the squads, and are always shown.</summary>
        private const int TowersShown = 4;

        /// <summary>The bodies of squads shown, not counting the towers.</summary>
        private static int CountViews()
        {
            GameObject units = GameObject.Find("Units");
            return units == null ? 0 : units.transform.childCount - TowersShown;
        }

        private IEnumerator PlayRound()
        {
            _battle.Speed = 16f;
            _match.Ready();
            float deadline = Time.realtimeSinceStartup + 120f;
            while (_match.Match.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, _match.Match.Phase, "The battle did not finish in time.");
        }

        [UnityTest]
        public IEnumerator AFallenTowersShockwave_HasABarThatRunsDownWithTheTimeLeft()
        {
            // D122.
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            _battle.Speed = 1f;
            _match.Ready();
            float started = Time.realtimeSinceStartup + 10f;
            while (!(_match.Match.Phase == MatchPhase.Battle && _battle.IsPlaying) && Time.realtimeSinceStartup < started) yield return null;
            Assert.AreEqual(MatchPhase.Battle, _match.Match.Phase);
            MatchHud hud = Object.FindAnyObjectByType<MatchHud>();
            Assert.AreEqual(0f, hud.ShockwaveBarShare(Team.A), "No shockwave, no bar.");
            SimUnit tower = _battle.Simulation.Units.First(u => u.Team == Team.A && u.Stats.IsTower);
            tower.Health = 0f;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_battle.Simulation.ActiveShockwave(Team.A) == null && Time.realtimeSinceStartup < deadline) yield return null;
            float full = hud.ShockwaveBarShare(Team.A);
            Assert.Greater(full, 0.9f, "A full bar as it begins.");
            float from = Time.time;
            while (Time.time < from + 1f) yield return null;
            float later = hud.ShockwaveBarShare(Team.A);
            float seconds = tower.Stats.Shockwave.Seconds;
            Assert.AreEqual(full - 1f / seconds, later, 0.5f / seconds, "It runs down with the time left.");
        }

        [UnityTest]
        public IEnumerator TheCards_CanBePutAsideToSeeTheBoard_AndComeBack()
        {
            yield return LoadMatch();
            Match m = _match.Match;
            _match.ChooseDraft(0);
            yield return PlayRound();
            yield return null;
            _match.Continue();
            yield return null;
            Assert.AreEqual(MatchPhase.Cards, m.Phase);

            MatchHud hud = Object.FindAnyObjectByType<MatchHud>();
            Assert.IsFalse(hud.CardsHidden, "A card screen starts with its cards showing.");
            hud.ToggleCards();
            Assert.IsTrue(hud.CardsHidden);
            yield return null;
            yield return null;
            Assert.AreEqual(MatchPhase.Cards, m.Phase, "Nothing was chosen.");
            Assert.IsNotNull(m.Player.Offer, "The offer still waits.");

            hud.ToggleCards();
            Assert.IsFalse(hud.CardsHidden, "The same button brings them back.");
            hud.ToggleCards();
            Assert.IsTrue(_match.SkipCards().Ok);
            yield return null;
            yield return null;
            Assert.IsFalse(hud.CardsHidden, "The next card screen won't start with its cards put away.");
        }

        [Test]
        public void ABasesBar_GoesOnBelowZero() => Assert.AreEqual("Enemy base: -340 / 2000", MatchHud.BaseLine("Enemy base", -340, 2000));

        [UnityTest]
        public IEnumerator AtTheEndOfARound_NodesOfLightHitTheBases_ThenTheResultComes()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            int yoursBefore = m.Player.BaseHealth, enemyBefore = m.Enemy.BaseHealth;
            _battle.Speed = 16f;
            _match.Ready();
            float deadline = Time.realtimeSinceStartup + 120f;
            while (_match.Ending == null && m.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(_match.Ending, "The battle ended, and its ending began.");
            Assert.AreEqual(MatchPhase.Battle, m.Phase, "The result waits for the nodes.");
            Assert.IsTrue(_battle.Simulation.IsFinished);
            Assert.AreEqual(yoursBefore, m.Player.BaseHealth, "Nothing is taken off yet.");
            Assert.AreEqual(enemyBefore, m.Enemy.BaseHealth);

            int yoursShown = yoursBefore, enemyShown = enemyBefore;
            while (_match.Ending != null && Time.realtimeSinceStartup < deadline)
            {
                Assert.LessOrEqual(_match.ShownBaseHealth(Team.A), yoursShown, "Base health only goes down.");
                Assert.LessOrEqual(_match.ShownBaseHealth(Team.B), enemyShown);
                yoursShown = _match.ShownBaseHealth(Team.A);
                enemyShown = _match.ShownBaseHealth(Team.B);
                yield return null;
            }
            Assert.AreNotEqual(MatchPhase.Battle, m.Phase);
            RoundSummary s = m.LastSummary;
            if (!s.Tie)
            {
                Assert.AreEqual(s.BaseHealthA, yoursShown, "The hits add up to the round's result.");
                Assert.AreEqual(s.BaseHealthB, enemyShown);
            }
        }

        [UnityTest]
        public IEnumerator Draft_ThenDeployment_ShowsThePlayersSquads_AndOnlyTheEnemysStartingSquads()
        {
            yield return LoadMatch();
            Assert.AreEqual(MatchPhase.Draft, _match.Match.Phase);
            Assert.AreEqual(5, _match.Match.PlayerDraft.Count, "5 of the 8 sub-factions (M10).");

            _match.ChooseDraft(0);
            yield return null;
            Assert.AreEqual(MatchPhase.Deployment, _match.Match.Phase);
            int playerBodies = 0;
            foreach (PlacedSquad squad in _match.Match.Player.Squads) playerBodies += squad.Type.SquadSize;
            int startingBodies = 0;
            foreach (SeenSquad squad in _match.Match.SeenArmy(Team.B)) startingBodies += squad.Type.SquadSize;
            Assert.AreEqual(2, _match.Match.SeenArmy(Team.B).Count);
            Assert.AreEqual(playerBodies + startingBodies, CountViews(), "The player's squads, and the enemy's two starting squads in grey (D58).");
            Assert.IsFalse(_battle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator Buying_PlacesTheSquadInTheMiddle_DropsItIn_ThenSelectsIt()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            UnitStats type = m.Player.Squads[0].Type;
            int creditsBefore = m.Player.Credits;
            int viewsBefore = CountViews();

            Assert.IsTrue(DeploymentGrid.FindFreeSpot(m.Player, type, false, m.CombatRules, out int column, out int row));
            Assert.IsTrue(_match.Buy(type).Ok, _match.Message);
            yield return null;

            Assert.AreEqual(creditsBefore - type.Cost, m.Player.Credits);
            Assert.AreEqual(viewsBefore + type.SquadSize, CountViews());
            PlacedSquad bought = m.Player.Squads[m.Player.Squads.Count - 1];
            Assert.AreEqual(column, bought.Column);
            Assert.AreEqual(row, bought.Row);
            Assert.IsTrue(_match.IsDropping);
            Assert.IsNull(_match.SelectedPlacedSquad, "Selected only once it lands.");
            Transform units = GameObject.Find("Units").transform;
            int playerBodies = 0;
            foreach (PlacedSquad squad in m.Player.Squads) playerBodies += squad.Type.SquadSize;
            Assert.Greater(units.GetChild(playerBodies - 1).position.y, 50f, "A bought squad starts high above the field.");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_match.IsDropping && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.IsFalse(_match.IsDropping);
            Assert.Less(units.GetChild(units.childCount - 1).position.y, 0.5f, "It has landed on its spot.");
            Assert.AreSame(bought, _match.SelectedPlacedSquad, "The new squad is selected when it lands.");
        }

        [UnityTest]
        public IEnumerator FullRound_BattleThenResultThenNextRoundWithIncome()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            int creditsBefore = m.Player.Credits;

            yield return PlayRound();
            Assert.AreEqual(MatchPhase.Result, m.Phase);
            Assert.IsNotNull(m.LastSummary);
            Assert.GreaterOrEqual(_battle.Simulation.Squads.Count, m.Player.Squads.Count + m.Enemy.Squads.Count, "The battle reveals the AI's army (Flood release more squads during it).");

            _match.Continue();
            yield return null;
            Assert.AreEqual(MatchPhase.Cards, m.Phase, "Round 2 opens with the card screen.");
            Assert.AreEqual(2, m.Round);
            Assert.AreEqual(creditsBefore + m.LastSummary.IncomeAwarded, m.Player.Credits);
            Assert.IsTrue(_match.SkipCards().Ok);
            Assert.AreEqual(MatchPhase.Deployment, m.Phase);
        }

        /// <summary>Leaves the result screen and skips the card offer if there is one.</summary>
        private void ContinueAndSkipCards()
        {
            _match.Continue();
            if (_match.Match.Phase == MatchPhase.Cards) _match.SkipCards();
        }

        [UnityTest]
        public IEnumerator FullMatch_CanBeLost()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            while (m.Player.Squads.Count > 0) _match.Sell(m.Player.Squads[0]);

            int rounds = 0;
            while (m.Phase != MatchPhase.MatchOver && rounds < 200)
            {
                yield return PlayRound();
                if (m.Phase == MatchPhase.Result) ContinueAndSkipCards();
                rounds++;
            }
            Assert.AreEqual(Team.B, m.Winner);
        }

        [UnityTest]
        public IEnumerator LevelUpAndTech_FromTheStatsWindowActions_ChangeTheSquadsBattleStats()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            PlacedSquad squad = m.Player.Squads[0];
            m.Player.Credits = 2000;
            squad.Xp = m.XpNeeded(squad);

            Assert.IsTrue(_match.LevelUp(squad).Ok, _match.Message);
            yield return null;
            Assert.AreEqual(2, squad.Level);
            Assert.AreEqual(_match.SelectedPlacedSquad, squad, "Still selected after the level-up.");
            SquadProgressView view = _match.DescribeProgress(_battle.Simulation.Squads[0].Id);
            Assert.AreEqual(2, view.Level);
            Assert.IsTrue(view.ShowXp);
            Assert.IsNotEmpty(view.Techs, "Every slice unit has techs.");
            Assert.AreEqual(squad.Type.MaxHealth * 2f, _battle.Simulation.Units[0].Stats.MaxHealth, 1e-3f, "The preview shows level 2.");

            TechLine first = view.Techs[0];
            Assert.IsTrue(first.ButtonEnabled, "Affordable during deployment.");
            first.OnButton();
            yield return null;
            Assert.IsTrue(m.Player.HasTech(squad.Type, first.Tech));
            Assert.IsTrue(_match.DescribeProgress(_battle.Simulation.Squads[0].Id).Techs[0].Owned);
        }

        [UnityTest]
        public IEnumerator LevelAll_ShowsFromTwoReadySquads_WhicheverIsSelected_AndGoesTheWayLevelUpGoes()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            UnitStats type = m.Player.Squads[0].Type;
            m.Player.Credits = 5000;
            while (m.Player.Squads.FindAll(s => s.Type == type).Count < 3)
            {
                m.Player.SquadsBoughtThisRound = 0;
                Assert.IsTrue(_match.Buy(type).Ok, _match.Message);
            }
            yield return null;
            List<PlacedSquad> same = m.Player.Squads.FindAll(s => s.Type == type);
            int cost = m.LevelUpCost(same[0]);
            int SimSquad(PlacedSquad squad) => _battle.Simulation.Squads[m.Player.Squads.IndexOf(squad)].Id;

            Assert.IsNull(_match.DescribeProgress(SimSquad(same[0])).LevelAllLabel, "Nobody is ready.");
            same[1].Xp = m.XpNeeded(same[1]);
            Assert.IsNull(_match.DescribeProgress(SimSquad(same[0])).LevelAllLabel, "One ready squad has Level up, not this.");

            same[2].Xp = m.XpNeeded(same[2]);
            SquadProgressView view = _match.DescribeProgress(SimSquad(same[0]));
            Assert.AreEqual($"Level all 2 ({2 * cost})", view.LevelAllLabel, "The selected squad isn't ready itself.");
            Assert.IsTrue(view.LevelAllEnabled);
            Assert.IsFalse(view.LevelUpEnabled);

            same[0].Level = UnitUpgrades.MaxLevel;
            view = _match.DescribeProgress(SimSquad(same[0]));
            Assert.IsNull(view.LevelUpLabel, "No Level up at the maximum level.");
            Assert.AreEqual($"Level all 2 ({2 * cost})", view.LevelAllLabel, "Still reachable from it.");
            same[0].Level = 1;

            same[0].Xp = m.XpNeeded(same[0]);
            m.Player.Credits = 3 * cost - 1;
            view = _match.DescribeProgress(SimSquad(same[0]));
            Assert.AreEqual($"Level all 3 ({3 * cost})", view.LevelAllLabel);
            Assert.IsFalse(view.LevelAllEnabled, "Too few credits.");

            m.Player.Credits = 3 * cost;
            _match.GetComponent<SquadSelection>().Select(SimSquad(same[0]));
            view = _match.DescribeProgress(SimSquad(same[0]));
            Assert.IsTrue(view.LevelAllEnabled);
            string action = null;
            _match.ActionTaken += (name, result) => action = name;
            view.OnLevelAll();
            yield return null;

            Assert.AreEqual(nameof(MatchController.LevelUpAll), action, "So the sound of a level-up plays.");
            Assert.AreEqual(0, m.Player.Credits);
            foreach (PlacedSquad squad in same) Assert.AreEqual(2, squad.Level);
            Assert.AreEqual(same[0], _match.SelectedPlacedSquad, "Still selected after it.");
            Assert.AreEqual(same[0].Type.MaxHealth * 2f, _battle.Simulation.Squads[m.Player.Squads.IndexOf(same[0])].Members[0].Stats.MaxHealth, 1e-3f, "The field shows level 2.");
            Assert.IsNull(_match.DescribeProgress(SimSquad(same[0])).LevelAllLabel);
        }

        [UnityTest]
        public IEnumerator TheUi_UsesThePlayersFactionTheme()
        {
            yield return LoadMatch();
            Assert.IsNull(_match.PlayerTheme, "No faction yet on the draft screen (neutral look).");
            _match.ChooseDraft(1);
            yield return null;
            Assert.IsNotNull(_match.PlayerTheme);
            Assert.AreEqual(_match.Match.Player.Faction.Name, _match.PlayerTheme.themeName);
        }

        [UnityTest]
        public IEnumerator Difficulty_IsChosenOnTheDraftScreen_AndLockedAfterward()
        {
            yield return LoadMatch();
            _match.SetDifficulty(AiDifficulty.Hard);
            Assert.AreEqual(AiDifficulty.Hard, _match.Difficulty);
            Assert.AreEqual("Hard AI", _match.Match.EnemyAi.Label);
            _match.ChooseDraft(0);
            _match.SetDifficulty(AiDifficulty.Easy);
            Assert.AreEqual("Hard AI", _match.Match.EnemyAi.Label, "The difficulty cannot change once the match started.");
            _match.SetDifficulty(AiDifficulty.Normal); // Leave the remembered setting at the default for other tests.
        }

        [UnityTest]
        public IEnumerator FullMatch_AgainstTheNormalAi_IsPlayedToTheEnd()
        {
            yield return LoadMatch(seed: 21);
            _match.SetDifficulty(AiDifficulty.Normal);
            Match m = _match.Match;
            var playerAi = new RuleBasedAi(AiDifficulty.Normal, 22); // Plays the human's side.
            _match.ChooseDraft(playerAi.ChooseDraftCard(new AiView(m, Team.A), m.PlayerDraft));
            yield return null;
            Assert.AreEqual("Normal AI", m.EnemyAi.Label);

            int rounds = 0;
            while (m.Phase != MatchPhase.MatchOver && rounds < 40)
            {
                if (m.Phase == MatchPhase.Cards)
                {
                    int card = playerAi.ChooseCard(new AiView(m, Team.A), m.Player.Offer);
                    if (card < 0 || !_match.ChooseCard(card).Ok) _match.SkipCards();
                }
                playerAi.Deploy(new AiView(m, Team.A));
                yield return PlayRound();
                Assert.Greater(m.Enemy.Squads.Count, 0, "The AI fields an army.");
                if (m.Phase == MatchPhase.Result) _match.Continue();
                rounds++;
            }
            Assert.AreEqual(MatchPhase.MatchOver, m.Phase, $"No winner after {rounds} rounds.");
        }

        [UnityTest]
        public IEnumerator FullMatch_CanBeWon()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            m.Enemy.Squads.Clear();   // The do-nothing AI's free squads leave...
            m.Enemy.BaseHealth = 1;   // ...and its base is nearly gone: any survivor finishes it.

            yield return PlayRound();
            Assert.AreEqual(MatchPhase.MatchOver, m.Phase);
            Assert.AreEqual(Team.A, m.Winner);
        }

        [UnityTest]
        public IEnumerator AtTheEnd_BattleStatsShowEachBattle_AndANewGameForgetsThem()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            m.Enemy.BaseHealth = 1;        // Whoever lives at the end of a round finishes this base...
            m.Player.BaseHealth = 100000;  // ...and this one holds until then.
            int rounds = 0;
            while (m.Phase != MatchPhase.MatchOver && rounds < 4)
            {
                if (rounds == 1) m.Enemy.Squads.Clear(); // A fight first; then the do-nothing AI's army leaves, so that the player's survivors end it.
                yield return PlayRound();
                rounds++;
                if (m.Phase != MatchPhase.Result) continue;
                ContinueAndSkipCards();
                yield return null;
            }
            Assert.AreEqual(MatchPhase.MatchOver, m.Phase);
            Assert.AreEqual(rounds, m.BattleHistory.Count, "A tab for each battle.");

            BattleStats stats = m.BattleHistory[rounds - 1];
            Assert.IsTrue(m.BattleHistory.All(b => b.Finished));
            Assert.AreEqual(m.Player.Squads.Count, stats.Lines.Count(l => l.Team == Team.A && l.Kind == BattleStatsKind.Squad));
            Assert.Greater(m.BattleHistory.Sum(b => b.Lines.Where(l => l.Team == Team.A).Sum(l => l.DamageDealt)), 0f, "The player's squads fought.");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.IsNotNull(_match.BattlePictureOf(rounds - 1), "The field was pictured as the battle started.");

            MatchHud hud = Object.FindAnyObjectByType<MatchHud>();
            hud.OpenBattleStats();
            Assert.IsTrue(hud.BattleStatsWindow.IsOpen);
            Assert.AreEqual(rounds - 1, hud.BattleStatsWindow.Tab, "It opens on the last battle.");
            BattleStatsLine best = stats.Side(Team.A, false)[0];
            hud.BattleStatsWindow.Pick(stats, best);
            Assert.AreSame(best, hud.BattleStatsWindow.Picked);
            var map = new Rect(100f, 100f, 400f, 410f);
            Assert.AreSame(best, BattleStatsWindow.LineAt(map, stats, BattleStatsWindow.OnMap(map, stats, best.Start)), "Its icon on the map is where it stood.");
            yield return null;
            hud.CloseBattleStats();
            Assert.IsFalse(hud.BattleStatsWindow.IsOpen);

            _match.NewMatch();
            yield return null;
            Assert.AreEqual(0, _match.Match.BattleHistory.Count, "A new game starts with no stats.");
            Assert.IsNull(_match.BattlePictureOf(0), "The pictures went with the old game.");
        }

        [UnityTest]
        public IEnumerator Round2_ShowsTheEnemysLastArmyAsGreyGhosts_AndReadySquadsGetAnArrow()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            yield return PlayRound();
            Assert.AreEqual(MatchPhase.Result, m.Phase);
            ContinueAndSkipCards();
            yield return null;

            // D25: the player's squads, then the enemy's army as it was at the last battle start.
            int playerBodies = 0, ghostBodies = 0;
            foreach (PlacedSquad squad in m.Player.Squads) playerBodies += squad.Type.SquadSize;
            foreach (SeenSquad squad in m.SeenArmy(Team.B)) ghostBodies += squad.Type.SquadSize;
            Assert.Greater(ghostBodies, 0);
            Assert.AreEqual(playerBodies + ghostBodies, CountViews(), "Ghosts of the last battle, not the AI's new squads.");
            var views = _battle.GetComponent<UnitViewManager>();
            SimSquad ghost = _battle.Simulation.Squads[m.Player.Squads.Count];
            Assert.AreEqual(Team.B, ghost.Team);
            Assert.IsTrue(views.BodyColor(ghost.Members[0]) == views.GhostColor, "Ghosts are grey.");
            SquadProgressView seen = _match.DescribeProgress(ghost.Id);
            Assert.IsNotNull(seen, "A ghost can be selected and read.");
            Assert.AreEqual(m.SeenArmy(Team.B)[0].Level, seen.Level);
            Assert.IsNull(seen.LevelUpLabel, "Nothing to buy on an enemy ghost.");

            // D26: a squad with a full XP bar shows the arrow until it levels up.
            PlacedSquad ready = m.Player.Squads[0];
            ready.Xp = m.XpNeeded(ready);
            yield return null;
            CollectionAssert.Contains(views.SquadsWithArrows(), _battle.Simulation.Squads[0].Id);
            CollectionAssert.DoesNotContain(views.SquadsWithArrows(), ghost.Id);
            m.Player.Credits = 5000;
            Assert.IsTrue(_match.LevelUp(ready).Ok, _match.Message);
            yield return null;
            CollectionAssert.DoesNotContain(views.SquadsWithArrows(), _battle.Simulation.Squads[0].Id);
        }

        [UnityTest]
        public IEnumerator SmokeBomb_IsPlacedInTwoClicks_OneEndThenTheOther()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            SpellStats smoke = m.Player.Faction.Cards.Find(c => c.Spell != null && c.Spell.Effect == SpellEffect.Smoke).Spell;
            var owned = new OwnedSpell { Id = 900, Stats = smoke, ReadyRound = m.Round };
            m.Player.Spells.Add(owned);

            _match.BeginPlacingSpell(owned);
            Assert.IsNull(_match.PlacingLineStart);
            Vec2 start = new Vec2(0f, 20f);
            _match.SetLineStart(start);
            Assert.IsFalse(owned.IsPlaced, "The first click only sets one end.");
            _match.ClearLineStart();
            Assert.IsNull(_match.PlacingLineStart, "Right-click goes back one step.");
            Assert.AreSame(owned, _match.PlacingSpell, "Still placing.");

            _match.SetLineStart(start);
            Assert.IsTrue(_match.PlaceLineTowards(start + new Vec2(0f, 50f)).Ok, _match.Message);
            Assert.IsTrue(owned.IsPlaced);
            Assert.AreEqual(90f, owned.AngleDegrees, 1e-3f, "It points at the mouse.");
            Assert.AreEqual(20f + smoke.AreaWidth * 0.5f, owned.Position.Y, 1e-3f, "Its length stays the same.");
            Assert.IsNull(_match.PlacingSpell);
            yield return null;
            yield return null;
            Assert.GreaterOrEqual(_battle.GetComponent<SpellView>().VisibleOutlines, 1, "The placed line shows.");

            // Aimed up and to the right: the drawn rectangle really lies along that direction.
            _match.BeginPlacingSpell(owned);
            _match.SetLineStart(start);
            Assert.IsTrue(_match.PlaceLineTowards(start + new Vec2(30f, 30f)).Ok, _match.Message);
            Assert.AreEqual(45f, owned.AngleDegrees, 1e-3f);
            yield return null;
            yield return null;
            GameObject drawn = GameObject.Find("Spell Rectangle");
            Assert.IsNotNull(drawn, "The rectangle is drawn.");
            Vector3 along = new Vector3(0.7071f, 0f, 0.7071f); // The field's (x, y) is the world's (x, z).
            Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(drawn.transform.right, along)), 1e-3f, "It lies along the aimed direction.");
            Assert.AreEqual(owned.Position.X, drawn.transform.position.x, 1e-3f);
            Assert.AreEqual(owned.Position.Y, drawn.transform.position.z, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Items_GoOnASquad_FromTheInventory()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            CardStats card = m.Player.Faction.Cards.Find(c => c.Type == CardType.Item && c.Item.HealthMultiplier > 1f);
            Assert.IsNotNull(card, "The factions offer items.");
            Assert.IsNotNull(_match.IconFor(card), "Items have icons.");
            m.Player.Inventory.Add(card.Item);
            PlacedSquad squad = m.Player.Squads[0];

            _match.BeginPlacingItem(card.Item);
            Assert.AreSame(card.Item, _match.PlacingItem);
            yield return null;
            Assert.IsTrue(_match.SquadOnScreen(squad, out _), "Its hexagon can be drawn over the squad.");
            Assert.IsTrue(_match.PlaceItemOn(squad).Ok, _match.Message);
            Assert.IsNull(_match.PlacingItem);
            Assert.AreSame(card.Item, squad.Item);
            yield return null;
            Assert.AreEqual(squad.Type.MaxHealth * card.Item.HealthMultiplier, _battle.Simulation.Squads[0].Members[0].Stats.MaxHealth, 1e-2f, "The field shows the stronger squad.");
        }

        [UnityTest]
        public IEnumerator IntensiveTraining_FillsTheSelectedSquadsXpBar()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            CardStats training = m.Player.Faction.Cards.Find(c => c.Type == CardType.Training);
            Assert.IsNotNull(training, "The factions offer Intensive Training.");
            Assert.IsNotNull(_match.IconFor(training));
            m.Player.Trainings.Add(new OwnedTraining { Card = training, ReadyRound = m.Round });
            Assert.IsFalse(_match.UseTraining(null).Ok);
            PlacedSquad squad = m.Player.Squads[0];
            Assert.IsTrue(_match.UseTraining(squad).Ok, _match.Message);
            Assert.IsTrue(m.CanLevelUp(squad));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Cards_ASpellIsTakenPlacedAndCast_ThenReinforcementsJoin()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            yield return PlayRound();
            _match.Continue();
            yield return null;

            // Round 2: spells.
            Assert.AreEqual(MatchPhase.Cards, m.Phase);
            CardOffer offer = m.Player.Offer;
            Assert.AreEqual(CardKind.General, offer.Kind);
            Assert.AreEqual(3, offer.Count, "The factions have more than 3 cards.");
            m.Player.Credits = 3000;
            int strike = offer.Cards.FindIndex(c => c.Spell != null && c.Spell.Offensive && c.Spell.Effect == SpellEffect.Damage);
            if (strike < 0)
            {
                // Not offered with this seed: swap one in, it is the spell's journey being tested.
                offer.Cards[0] = m.Player.Faction.Cards.Find(c => c.Spell != null && c.Spell.Offensive && c.Spell.Effect == SpellEffect.Damage && c.MinRound <= m.Round);
                strike = 0;
            }
            Assert.IsTrue(_match.ChooseCard(strike).Ok, _match.Message);
            Assert.AreEqual(MatchPhase.Deployment, m.Phase);
            OwnedSpell spell = m.Player.Spells.Single();

            _match.BeginPlacingSpell(spell);
            Assert.IsFalse(_match.CanPlaceSpellAt(new Vec2(0f, m.CombatRules.ArenaLength)), "Not outside the field.");
            Assert.IsTrue(_match.PlaceSpellAt(new Vec2(0f, 30f)).Ok, _match.Message);
            yield return null;
            yield return null;
            var view = _battle.GetComponent<SpellView>();
            Assert.IsNotNull(view, "The Match scene draws spells.");
            Assert.GreaterOrEqual(view.VisibleOutlines, 1, "The placed spell shows its outline.");

            _battle.Speed = 16f;
            _match.Ready();
            Assert.IsTrue(_battle.Simulation.Spells.Any(s => s.Stats == spell.Stats && s.Team == Team.A), "Cast into the battle.");
            float deadline = Time.realtimeSinceStartup + 120f;
            while (m.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, m.Phase);
            SimSpell cast = _battle.Simulation.Spells.First(s => s.Team == Team.A);
            Assert.IsTrue(cast.TicksDone > 0 || _battle.Simulation.ElapsedSeconds < cast.Stats.DelaySeconds,
                $"{cast.Stats.Name} went off (battle lasted {_battle.Simulation.ElapsedSeconds:0.0} s, delay {cast.Stats.DelaySeconds} s, ticks {cast.TicksDone}).");
            Assert.IsFalse(spell.IsReady(3), "Recharging next round.");

            // Round 4: reinforcements (round 3 is another general offer).
            _match.Continue();
            yield return null;
            Assert.AreEqual(CardKind.General, m.Player.Offer.Kind, "Round 3.");
            _match.SkipCards();
            yield return PlayRound();
            _match.Continue();
            yield return null;
            Assert.AreEqual(MatchPhase.Cards, m.Phase);
            Assert.AreEqual(CardKind.Reinforcement, m.Player.Offer.Kind);
            int before = m.Player.Squads.Count;
            ReinforcementCard card = m.Player.Offer.Reinforcements[0];
            Assert.IsTrue(_match.ChooseCard(0).Ok, _match.Message);
            Assert.AreEqual(before + card.Squads, m.Player.Squads.Count);
            yield return null;
            int bodies = 0;
            foreach (PlacedSquad squad in m.Player.Squads) bodies += squad.Type.SquadSize;
            foreach (SeenSquad squad in m.SeenArmy(Team.B)) bodies += squad.Type.SquadSize;
            Assert.AreEqual(bodies, CountViews(), "The reinforcement squads are shown on the field.");
        }
    }
}
