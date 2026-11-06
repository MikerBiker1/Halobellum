// TutorialTests.cs
// The tutorial (D77), played through in the Match scene the way its steps ask: each step moves on when its thing is done, Ready stays
// locked until a step allows it, rounds 2 and 3 offer the cards the lessons talk about, a player who follows along wins, and a new match
// afterwards is an ordinary one. Clicks on the HUD are the controller's own actions here; the two the HUD reports are sent as it does.
// Depends on: Halobellum.Presentation (MatchController, TutorialDirector, BattleController), Halobellum.UI (MatchHud), Halobellum.Economy, NUnit.

using System.Collections;
using System.Linq;
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
    public class TutorialTests
    {
        private MatchController _match;
        private BattleController _battle;
        private TutorialDirector T => _match.Tutorial;

        private IEnumerator LoadTutorial()
        {
            yield return SceneManager.LoadSceneAsync("Match", LoadSceneMode.Single);
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            Assert.IsNotNull(_match);
            _match.StartTutorial();
            yield return null;
        }

        private IEnumerator Step(string title)
        {
            yield return null;
            yield return null;
            Assert.AreEqual(title, T.Current?.Title, $"At step {T.Index + 1}.");
        }

        private IEnumerator Fight()
        {
            _battle.Speed = 16f;
            _match.Ready();
            float deadline = Time.realtimeSinceStartup + 120f;
            while (_match.Match.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, _match.Match.Phase, "The battle did not finish in time.");
            yield return null;
        }

        private void BuyWhatTheBudgetAllows()
        {
            PlayerState me = _match.Match.Player;
            UnitStats scorpion = me.Roster.First(u => u.Name == "Scorpion");
            if (!me.Unlocked.Contains(scorpion) && me.Credits >= scorpion.UnlockCost + scorpion.Cost) _match.Unlock(scorpion);
            foreach (string name in new[] { "Scorpion", "Flamethrower Trooper", "Marine", "Scorpion" })
            {
                UnitStats unit = me.Roster.First(u => u.Name == name);
                if (me.Unlocked.Contains(unit)) _match.Buy(unit);
            }
        }

        [UnityTest]
        public IEnumerator TheTutorial_CanBePlayedThrough_StepByStep_AndThePlayerWins()
        {
            yield return LoadTutorial();
            Match m = _match.Match;
            Assert.IsNotNull(T, "The tutorial runs.");
            Assert.AreEqual(1, m.PlayerDraft.Count, "One starting card.");
            Assert.AreEqual("Noble", m.PlayerDraft[0].SubFaction.Name);
            Assert.AreEqual(TutorialSetup.Seed, m.Seed);

            yield return Step("Welcome to Halobellum");
            T.Next();
            yield return Step("Your starting card");
            Assert.IsTrue(_match.ChooseDraft(0).Ok);
            yield return Step("The camera");
            Assert.AreEqual("Banished", m.Enemy.SubFaction.Name);
            T.Next();
            yield return Step("The top bar");
            _match.Ready();
            Assert.AreEqual(MatchPhase.Deployment, m.Phase, "Ready is locked during the lessons.");
            T.Next();

            UnitStats flamer = m.Player.Roster.First(u => u.Name == TutorialSetup.ShopUnit);
            yield return Step("The shop: unlocking");
            Assert.IsTrue(_match.Unlock(flamer).Ok);
            yield return Step("The shop: buying");
            Assert.IsTrue(_match.Buy(flamer).Ok);
            yield return Step("Selecting a squad");
            _match.Selection.Select(_battle.Simulation.Squads[0].Id);
            yield return Step("Squad stats");
            T.Next();
            yield return Step("Stats before buying");
            T.Signal("ShopUnit");
            yield return Step("Moving a squad");
            PlacedSquad squad = m.Player.Squads[0];
            bool moved = false;
            for (int dx = -6; dx <= 6 && !moved; dx += 2)
            {
                _match.BeginMoving(squad);
                moved = _match.PlaceAt(squad.Column + dx, squad.Row + 1).Ok;
            }
            Assert.IsTrue(moved);
            yield return Step("Undo");
            Assert.IsTrue(_match.Undo().Ok);
            yield return Step("The Command Center");
            _match.Selection.SelectTower(Team.A, TowerKind.CommandCenter);
            yield return Step("What the Command Center does");
            T.Next();
            yield return Step("The Research Center");
            _match.Selection.SelectTower(Team.A, TowerKind.ResearchCenter);
            yield return Step("What the Research Center does");
            T.Next();
            yield return Step("Ready");
            yield return Fight();
            yield return Step("The round's result");
            _match.Continue();

            yield return Step("Cards");
            Assert.AreEqual(MatchPhase.Cards, m.Phase);
            int spell = m.Player.Offer.Cards.FindIndex(c => c.Type == CardType.Spell);
            Assert.GreaterOrEqual(spell, 0, "Round 2 offers the spell the lesson talks about.");
            Assert.IsTrue(_match.ChooseCard(spell).Ok);
            yield return Step("Spells");
            _match.BeginPlacingSpell(m.Player.Spells[0]);
            Assert.IsTrue(_match.PlaceSpellAt(new Vec2(0f, 30f)).Ok);
            yield return Step("Levels");
            T.Next();
            yield return Step("Techs");
            T.Next();
            yield return Step("Next battle");
            BuyWhatTheBudgetAllows();
            yield return Fight();
            if (m.Phase == MatchPhase.Result) _match.Continue();

            yield return Step("Items");
            int item = m.Player.Offer.Cards.FindIndex(c => c.Type == CardType.Item);
            Assert.GreaterOrEqual(item, 0, "Round 3 offers the item the lesson talks about.");
            Assert.IsTrue(_match.ChooseCard(item).Ok);
            yield return Step("Placing an item");
            _match.BeginPlacingItem(m.Player.Inventory[0]);
            Assert.IsTrue(_match.PlaceItemOn(m.Player.Squads[0]).Ok);
            yield return Step("Flank zones");
            T.Next();
            yield return Step("Finish them");
            for (int round = 0; round < 12 && m.Phase != MatchPhase.MatchOver; round++)
            {
                if (m.Phase == MatchPhase.Cards) _match.SkipCards();
                BuyWhatTheBudgetAllows();
                yield return Fight();
                if (m.Phase == MatchPhase.Result) _match.Continue();
                yield return null;
            }
            Assert.AreEqual(MatchPhase.MatchOver, m.Phase, "The match ends.");
            Assert.LessOrEqual(m.Enemy.BaseHealth, 0, $"A player who follows along wins (round {m.Round}, our base {m.Player.BaseHealth}).");

            yield return Step("Battle stats");
            Object.FindAnyObjectByType<MatchHud>().OpenBattleStats();
            yield return Step("That's the game");
            T.Next();
            yield return null;
            Assert.IsTrue(T.Finished);
        }

        [UnityTest]
        public IEnumerator ANewMatch_AfterTheTutorial_IsAnOrdinaryOne()
        {
            yield return LoadTutorial();
            Assert.IsNotNull(T);
            _match.NewMatch();
            yield return null;
            Assert.IsNull(_match.Tutorial, "No tutorial in the next match.");
            Assert.Greater(_match.Match.PlayerDraft.Count, 1, "An ordinary draft.");
            Assert.AreNotEqual(TutorialSetup.BaseHealth, _match.Match.Rules.BaseHealth, "The tutorial's base health is not kept.");
        }
    }
}
