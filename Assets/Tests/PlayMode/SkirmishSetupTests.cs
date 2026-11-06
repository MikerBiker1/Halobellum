// SkirmishSetupTests.cs
// A match setup in the real Match scene: it reaches the match on that match's own rules, "New match" plays it again, it is taken
// only once so that a plain load of the scene is Faction mode, the seed picks one land for both sides while each unit keeps its
// real faction's looks, and the flank zones go by what is unlocked. The buttons that start it are in the Play submenu's own tests.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class SkirmishSetupTests
    {
        [TearDown]
        public void LeaveNothingBehind()
        {
            GameFlow.TakeMatchSetup();
            BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant); // These are static.
        }

        private static MatchSetup Setup() => new MatchSetup { Mode = MatchMode.UnlockSkirmish, BaseHealth = 500, Cards = CardFilter.OnlyReinforcements };

        /// <summary>Starts the setup as the menu will, and waits for the Match scene's controller to have taken it.</summary>
        private static IEnumerator StartSkirmish()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            GameFlow.NewSkirmish(Setup());
            float limit = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().name != GameFlow.MatchScene && Time.realtimeSinceStartup < limit) yield return null;
            Assert.AreEqual(GameFlow.MatchScene, SceneManager.GetActiveScene().name);
            yield return null;
            yield return null;
        }

        private static MatchController Controller()
        {
            var match = Object.FindAnyObjectByType<MatchController>();
            Assert.IsNotNull(match, "The Match scene has no MatchController.");
            return match;
        }

        [UnityTest]
        public IEnumerator TheSetup_ReachesTheMatch()
        {
            yield return StartSkirmish();
            Match match = Controller().Match;
            Assert.AreEqual(MatchMode.UnlockSkirmish, match.Rules.Mode);
            Assert.AreEqual(CardFilter.OnlyReinforcements, match.Rules.Cards);
            Assert.AreEqual(500, match.Rules.BaseHealth);
            Assert.AreEqual(MatchPhase.Draft, match.Phase);
            Assert.AreEqual(5, match.PlayerDraft.Count);
            Assert.AreEqual(5, match.EnemyDraft.Count);
            foreach (DraftCard card in match.PlayerDraft) Assert.AreEqual(SkirmishFaction.Name, card.Faction.Name);
            Assert.IsNull(GameFlow.TakeMatchSetup(), "The scene has taken it.");

            int health = match.Rules.StartingBaseHealth(match.PlayerDraft[0].Value);
            Assert.IsTrue(Controller().ChooseDraft(0).Ok, Controller().Message);
            yield return null;
            Assert.AreEqual(health, match.Player.BaseHealth);
            Assert.AreEqual(70, match.Player.Roster.Count);
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
        }

        [UnityTest]
        public IEnumerator NewMatch_PlaysTheSetupAgain_FromTheAssetsValues()
        {
            yield return StartSkirmish();
            float perCredit = Controller().Match.Rules.DraftBaseHealthPerCredit;
            Assert.AreEqual(0.5f, perCredit, 1e-5f, "2 from the asset, scaled to 500 of 2000.");

            Controller().NewMatch();
            yield return null;
            MatchRules rules = Controller().Match.Rules;
            Assert.AreEqual(MatchMode.UnlockSkirmish, rules.Mode);
            Assert.AreEqual(CardFilter.OnlyReinforcements, rules.Cards);
            Assert.AreEqual(500, rules.BaseHealth);
            Assert.AreEqual(perCredit, rules.DraftBaseHealthPerCredit, 1e-5f, "The scaling isn't applied on top of itself.");
        }

        [UnityTest]
        public IEnumerator APlainLoad_IsFactionMode()
        {
            yield return StartSkirmish();
            Controller().ChooseDraft(0);
            Assert.IsTrue(BattleFactions.PerUnitLooks);

            yield return SceneManager.LoadSceneAsync(GameFlow.MatchScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            Match match = Controller().Match;
            Assert.AreEqual(MatchMode.Faction, match.Rules.Mode);
            Assert.AreEqual(CardFilter.AllCards, match.Rules.Cards);
            Assert.AreEqual(2000, match.Rules.BaseHealth, "The Match Rules asset itself never changed.");
            Assert.AreEqual(2f, match.Rules.DraftBaseHealthPerCredit, 1e-5f);
            Assert.AreNotEqual(SkirmishFaction.Name, match.PlayerDraft[0].Faction.Name);

            Controller().ChooseDraft(0);
            Assert.IsFalse(BattleFactions.PerUnitLooks, "A Faction match forgets the map of the Skirmish before it.");
            Assert.AreEqual(match.Player.Faction.Name, BattleFactions.TeamA);
            Assert.AreEqual(match.Enemy.Faction.Name, BattleFactions.TeamB);
        }

        [UnityTest]
        public IEnumerator TheSeed_PicksOneLand_AndEachUnitKeepsItsOwnFaction()
        {
            yield return StartSkirmish();
            string[] lands = { BattleFactions.Unsc, BattleFactions.Covenant, BattleFactions.Flood };
            for (int seed = 3; seed <= 5; seed++)
            {
                Controller().NewMatch(seed);
                Assert.IsTrue(Controller().ChooseDraft(0).Ok, Controller().Message);
                yield return null;
                string land = lands[seed - 3];
                Assert.AreEqual(land, BattleFactions.TeamA, $"Seed {seed}");
                Assert.AreEqual(land, BattleFactions.TeamB, $"Seed {seed}: both sides on it, so Team B's shots stay tinted.");
                Assert.IsTrue(BattleFactions.PerUnitLooks);
                Assert.AreEqual(BattleFactions.Unsc, BattleFactions.OfUnit(new UnitStats { Name = "ODST" }, Team.B), "It lands from a UNSC spell, and is in no roster.");
                Assert.AreEqual(BattleFactions.Unsc, BattleFactions.OfUnit(new UnitStats { Name = "Spartan Sniper" }, Team.B));
                Assert.AreEqual(BattleFactions.Covenant, BattleFactions.OfUnit(new UnitStats { Name = "Chopper" }, Team.A));
                Assert.AreEqual(BattleFactions.Flood, BattleFactions.OfUnit(new UnitStats { Name = "Juggernaut" }, Team.A));
                Assert.AreEqual(land, BattleFactions.OfUnit(new UnitStats { Name = "Nobody" }, Team.A), "Anything else goes by the land.");
            }
        }

        [UnityTest]
        public IEnumerator FlankZones_GoByWhatIsUnlocked()
        {
            yield return StartSkirmish();
            MatchController controller = Controller();
            controller.NewMatch(3);
            controller.ChooseDraft(0);
            yield return null;
            PlayerState player = controller.Match.Player;
            Assert.IsFalse(controller.ShowsFlankZones, "The roster has flyers, but none is unlocked yet.");

            UnitStats flyer = player.Roster.Find(unit => unit.CanFlank);
            Assert.IsNotNull(flyer);
            player.Unlocked.Add(flyer);
            Assert.IsTrue(controller.ShowsFlankZones);
        }
    }
}
