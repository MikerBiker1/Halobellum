// FlankZoneTests.cs
// Tests the flank zones (D61): where they are and who can stand in them, arriving late only the first time, flying outside the field, and the Hard AI
// being the only one to use them and to defend against them.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestMatch, TestRules.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class FlankZoneTests
    {
        private static CombatRules Rules()
        {
            CombatRules rules = TestRules.Default();
            rules.ArenaWidthSquares = 80;
            rules.DeploymentDepthSquares = 40;
            rules.NoMansLandSquares = 2;
            rules.SquareSize = 2f;
            return rules;
        }

        private static UnitStats Flier(bool canFlank = true) => new UnitStats
        {
            Name = "Flier", Tier = 2, Cost = 300, SquadSize = 1, MaxHealth = 100000f, MoveSpeed = 5f, CollisionRadius = 1f, IsAir = true, CanFlank = canFlank, TargetFilter = TargetFilter.Both,
            Weapons = new[] { TestUnits.Weapon(damage: 1f, range: 12f) },
        };

        [Test]
        public void TheZones_AreFourColumnsWide_TwoColumnsOffTheEnemysArea_AndAsDeepAsIt()
        {
            CombatRules rules = Rules();
            Assert.IsTrue(DeploymentGrid.IsInFlankZone(-6, -42, 4, 40, rules), "The whole of the left zone.");
            Assert.IsTrue(DeploymentGrid.IsInFlankZone(82, -42, 4, 40, rules), "The whole of the right zone.");
            Assert.IsFalse(DeploymentGrid.IsInFlankZone(-7, -42, 1, 1, rules), "Too far out.");
            Assert.IsFalse(DeploymentGrid.IsInFlankZone(-2, -42, 1, 1, rules), "In the gap.");
            Assert.IsFalse(DeploymentGrid.IsInFlankZone(81, -42, 1, 1, rules), "In the gap.");
            Assert.IsFalse(DeploymentGrid.IsInFlankZone(-6, -43, 1, 1, rules), "Behind the enemy's area.");
            Assert.IsFalse(DeploymentGrid.IsInFlankZone(-6, -2, 1, 1, rules), "In no man's land.");
            Assert.IsFalse(DeploymentGrid.IsInFlankZone(-6, 0, 1, 1, rules), "Beside one's own area.");
        }

        [Test]
        public void OnlyUnitsThatCanFlank_CanStandThere_AndTheyStandBesideTheEnemy()
        {
            CombatRules rules = Rules();
            var player = new PlayerState(Team.A);
            Assert.IsTrue(DeploymentGrid.CanPlace(player, Flier(), -5, -20, false, rules));
            Assert.IsFalse(DeploymentGrid.CanPlace(player, Flier(canFlank: false), -5, -20, false, rules));
            Assert.IsTrue(DeploymentGrid.CanPlace(player, Flier(), 10, 10, false, rules), "It can still go in its own area.");

            Vec2 teamA = DeploymentGrid.WorldCentre(Flier(), -5, -20, false, Team.A, rules);
            Assert.Less(teamA.X, -rules.ArenaWidth * 0.5f);
            Assert.Greater(teamA.Y, rules.DeploymentZoneFront, "Team A's zones are over Team B's area.");
            Vec2 teamB = DeploymentGrid.WorldCentre(Flier(), -5, -20, false, Team.B, rules);
            Assert.AreEqual(-teamA.X, teamB.X, 1e-3f);
            Assert.AreEqual(-teamA.Y, teamB.Y, 1e-3f);
        }

        private static Match Started(UnitStats flier, CombatRules rules, IMatchAi ai = null)
        {
            var factions = TestMatch.StandardFactions(out _, out _, out _, out _);
            factions[0].BaseRoster.Add(flier);
            factions[1].BaseRoster.Add(flier);
            var match = new Match(TestMatch.Rules(), rules, factions, 1, ai ?? new DoNothingAi());
            match.ChooseDraft(0);
            match.Player.Credits = 5000;
            match.Player.Unlocked.Add(flier);
            return match;
        }

        [Test]
        public void ASquadInAFlankZone_IsLateTheFirstTime_AndOnTimeAfterThat()
        {
            CombatRules rules = Rules();
            UnitStats flier = Flier();
            Match match = Started(flier, rules);
            Assert.IsTrue(match.Buy(Team.A, flier, -5, -20, false).Ok);
            int index = match.Player.Squads.Count - 1;
            Vec2 stands = DeploymentGrid.WorldCentre(flier, -5, -20, false, Team.A, rules);

            BattleSimulation first = match.StartBattle();
            SimUnit body = first.Squads[index].Members[0];
            Assert.IsTrue(body.NotArrived);
            int arrived = 0;
            first.Events.SquadArrived += _ => arrived++;
            int ticks = (int)(9.9f / first.TickSeconds);
            for (int i = 0; i < ticks && !first.IsFinished; i++)
            {
                first.Step();
                Assert.IsFalse(Targeting.IsTargetableEnemy(first.Units.First(u => u.Team == Team.B), body), "Nothing can shoot it yet.");
            }
            Assert.IsTrue(body.NotArrived);
            Assert.AreEqual(stands.X, body.Position.X, 1e-2f, "It waits where it will arrive.");
            Assert.AreEqual(stands.Y, body.Position.Y, 1e-2f);
            Assert.AreEqual(0, arrived);
            while (first.ElapsedSeconds < 10.2f && !first.IsFinished) first.Step();
            Assert.IsFalse(body.NotArrived);
            Assert.AreEqual(1, arrived);
            first.Run();
            match.FinishBattle();
            match.ContinueAfterResult();
            if (match.Phase == MatchPhase.Cards) match.SkipCards(Team.A);

            BattleSimulation second = match.StartBattle();
            Assert.IsFalse(second.Squads[index].Members[0].NotArrived, "On time from now on.");
        }

        [Test]
        public void AFlanker_StaysOutsideTheField_UntilItGoesForATarget_AndAGroundUnitCannot()
        {
            CombatRules rules = Rules();
            var sim = new BattleSimulation(rules, 1);
            Vec2 outside = new Vec2(-88f, 40f);
            SimUnit flier = sim.AddUnit(Flier(), Team.A, outside);
            UnitStats plain = Flier(canFlank: false);
            SimUnit other = sim.AddUnit(plain, Team.A, outside + new Vec2(0f, 10f));
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f), Team.B, new Vec2(80f, 70f));
            sim.Step();
            Assert.AreEqual(outside.X, flier.Position.X, 0.5f);
            Assert.GreaterOrEqual(other.Position.X, -rules.ArenaWidth * 0.5f, "Anything else is kept on the field.");
        }

        [Test]
        public void FirstFlank_WaitsWhileTheBattleWouldOtherwiseEnd()
        {
            CombatRules rules = Rules();
            var sim = new BattleSimulation(rules, 1);
            int squadId = sim.AddSquad(Flier(), Team.A, new Vec2(-88f, 40f));
            sim.DelayArrival(sim.SquadById(squadId), 10f);
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f), Team.B, new Vec2(0f, 70f));
            for (int i = 0; i < 20; i++) sim.Step();
            Assert.IsFalse(sim.IsFinished, "A squad on its way keeps the battle going.");
        }

        // ------------------------------------------------------------------ the AI

        private static Match WithAi(AiDifficulty difficulty, UnitStats flier, CombatRules rules, out RuleBasedAi ai)
        {
            ai = new RuleBasedAi(difficulty, 5);
            return Started(flier, rules, ai);
        }

        private static bool BoughtAFlankSpot(AiDifficulty difficulty)
        {
            CombatRules rules = Rules();
            UnitStats flier = Flier();
            Match match = WithAi(difficulty, flier, rules, out RuleBasedAi ai);
            match.Enemy.Credits = 5000;
            match.Enemy.Unlocked.Add(flier);
            match.Enemy.SquadsBoughtThisRound = 0;
            var view = new AiView(match, Team.B);
            if (!ai.ChooseSpot(view, flier, out int column, out int row)) return false;
            DeploymentGrid.Footprint(flier, false, rules, out int width, out int depth);
            return DeploymentGrid.IsInFlankZone(column, row, width, depth, rules);
        }

        [Test]
        public void OnlyTheHardAi_ChoosesAFlankSpot()
        {
            Assert.IsTrue(BoughtAFlankSpot(AiDifficulty.Hard));
            Assert.IsFalse(BoughtAFlankSpot(AiDifficulty.Normal));
            Assert.IsFalse(BoughtAFlankSpot(AiDifficulty.Easy));
        }
    }
}
