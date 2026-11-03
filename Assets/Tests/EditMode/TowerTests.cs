// TowerTests.cs
// The towers (D55): in a battle they are shot like any ground target, but aren't part of the army; a fall weakens the whole side,
// and a second fall adds time, not severity. In a match they are rebuilt for each battle, block squads, and sell what the owner asked for.
// Depends on: Halobellum.Combat, Halobellum.Economy, Halobellum.Core, TestMatch, TestRules, TestUnits, NUnit.

using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class TowerTests
    {
        private static MatchRules RulesWithTowers()
        {
            MatchRules rules = TestMatch.Rules();
            rules.TowerBaseHealth = 100f;
            rules.OilZone = new SpellStats { Name = "Oil Zone", Offensive = true, Effect = SpellEffect.Oil, Cost = 150, AreaWidth = 15f, AreaDepth = 4f, DurationSeconds = 15f, SlowShare = 0.4f, FireDamageMultiplier = 1.5f };
            return rules;
        }

        private static Match Started(out UnitStats infantry, IMatchAi ai = null)
        {
            var match = new Match(RulesWithTowers(), TestRules.Default(), TestMatch.StandardFactions(out infantry, out _, out _, out _), 1, ai ?? new DoNothingAi());
            match.ChooseDraft(0);
            return match;
        }

        private static UnitStats Tower(float seconds = 10f, float health = 100f, TowerKind kind = TowerKind.CommandCenter) => new UnitStats
        {
            Name = "Tower", SquadSize = 1, MaxHealth = health, ArmorType = ArmorType.Light, CollisionRadius = 3f, ImmuneToInfection = true,
            Shockwave = new ShockwaveStats { Kind = kind, Seconds = seconds, SpeedMultiplier = 0.7f, DamageMultiplier = 0.7f, FireRateMultiplier = 0.7f },
        };

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        [Test]
        public void ASideWithOnlyTowersLeft_HasLost_AndTowersCountForNothing()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, weapon: TestUnits.Weapon(damage: 1000f, range: 30f)), Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit(health: 1f, speed: 0f, weapon: TestUnits.Weapon(damage: 1f, range: 1f)), Team.B, new Vec2(0f, 5f));
            sim.AddUnit(Tower(health: 100000f), Team.B, new Vec2(0f, 40f));
            BattleResult result = sim.Run();
            Assert.AreEqual(BattleOutcome.TeamAWins, result.Outcome);
            Assert.IsFalse(result.TimedOut);
            Assert.AreEqual(1, result.TeamB.Deployed, "The tower isn't a body of the army.");
            Assert.AreEqual(0, result.TeamB.Survivors);
        }

        [Test]
        public void AUnit_ShootsATower_WhenItIsTheClosestEnemy_AndNothingPushesIt()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit shooter = sim.AddUnit(TestUnits.Unit(health: 100f, speed: 4f, weapon: TestUnits.Weapon(damage: 5f, range: 2f, interval: 0.5f)), Team.A, new Vec2(0f, 0f));
            SimUnit tower = sim.AddUnit(Tower(health: 100000f), Team.B, new Vec2(0f, 12f));
            sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f), Team.B, new Vec2(0f, 60f));
            Vec2 stood = tower.Position;
            RunSeconds(sim, 6f);
            Assert.Less(tower.Health, 100000f, "It walked up and shot the tower.");
            Assert.AreEqual(stood, tower.Position);
        }

        [Test]
        public void AFallenTower_WeakensItsWholeSide_AndOnlyItsSide()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit own = sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 4f), Team.A, new Vec2(-50f, -50f));
            SimUnit enemy = sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 4f), Team.B, new Vec2(50f, 50f));
            SimUnit tower = sim.AddUnit(Tower(), Team.A, new Vec2(50f, -50f));
            tower.Health = 0f;
            RunSeconds(sim, 1f);
            Assert.AreEqual(9f, sim.ShockwaveSecondsLeft(Team.A), 0.1f);
            Assert.AreEqual(0.7f, own.SpeedMultiplier, 1e-3f);
            Assert.AreEqual(0.7f, own.ShockwaveDamage, 1e-3f);
            Assert.AreEqual(0.7f, own.ShockwaveFireRate, 1e-3f);
            Assert.AreEqual(1f, enemy.SpeedMultiplier, 1e-3f);
            Assert.AreEqual(0f, sim.ShockwaveSecondsLeft(Team.B));
            RunSeconds(sim, 10f);
            Assert.AreEqual(1f, own.SpeedMultiplier, 1e-3f, "It wore off.");
        }

        private static float DamageTaken(bool towerFalls)
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit target = sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f), Team.A, new Vec2(0f, -3f));
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 20f, interval: 0.5f)), Team.B, new Vec2(0f, 3f));
            UnitStats stats = Tower();
            stats.Shockwave.DamageTakenMultiplier = 1.4f;
            SimUnit tower = sim.AddUnit(stats, Team.A, new Vec2(60f, -60f));
            if (towerFalls) tower.Health = 0f;
            RunSeconds(sim, 1f); // The shot of the very first moment comes before the shockwave.
            float before = target.Health;
            RunSeconds(sim, 4f);
            return before - target.Health;
        }

        [Test]
        public void AFallenTower_MakesItsArmyTake40PercentMore_AndTheOtherTowerNone()
        {
            Assert.AreEqual(1.4f, DamageTaken(true) / DamageTaken(false), 0.02f);

            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f), Team.A, new Vec2(-50f, -50f));
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f), Team.B, new Vec2(50f, 50f));
            UnitStats stats = Tower();
            stats.Shockwave.DamageTakenMultiplier = 1.4f;
            SimUnit first = sim.AddUnit(stats, Team.A, new Vec2(50f, -50f));
            SimUnit second = sim.AddUnit(Tower(kind: TowerKind.ResearchCenter), Team.A, new Vec2(0f, -50f));
            first.Health = 0f;
            RunSeconds(sim, 1f);
            Assert.AreEqual(1.4f, sim.Units[0].ShockwaveDamageTaken, 1e-3f);
            Assert.AreEqual(1f, second.ShockwaveDamageTaken, 1e-3f);
        }

        [Test]
        public void TheRules_Weaken40PercentAcrossTheBoard_And40PercentMoreDamage()
        {
            ShockwaveStats shockwave = Started(out _).TowerStats(TowerKind.CommandCenter, 1).Shockwave;
            Assert.AreEqual(0.6f, shockwave.SpeedMultiplier, 1e-3f);
            Assert.AreEqual(0.6f, shockwave.DamageMultiplier, 1e-3f);
            Assert.AreEqual(0.6f, shockwave.FireRateMultiplier, 1e-3f);
            Assert.AreEqual(1.4f, shockwave.DamageTakenMultiplier, 1e-3f);
        }

        [TestCase(1, 150f)]
        [TestCase(2, 275f)]
        [TestCase(3, 450f)]
        [TestCase(4, 800f)]
        [TestCase(5, 1200f)]
        public void ATower_GivesItsKillerTheXpOfASquadOfTheTierOfItsLevel(int level, float xp)
        {
            Match match = Started(out _);
            match.Enemy.FindTower(TowerKind.CommandCenter).Level = level;
            PlacedSquad killer = match.Player.Squads[0];
            BattleSimulation battle = match.StartBattle();
            SimUnit body = battle.Squads[0].Members[0];
            foreach (SimUnit unit in battle.Units)
            {
                if (unit.Team != Team.B) continue;
                unit.Health = 0f; // Nobody gets anything for these...
                if (unit.Stats.IsTower && unit.Stats.Shockwave.Kind == TowerKind.CommandCenter) unit.KilledById = body.Id; // ...but this one.
            }
            while (!battle.IsFinished) battle.Step();
            RoundSummary summary = match.FinishBattle();
            Assert.AreEqual(xp, summary.XpGained[killer.Id], 1e-3f);
            Assert.AreEqual(1, summary.XpGained.Count);
        }

        [Test]
        public void ASecondTower_AddsItsTime_NotItsSeverity()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit own = sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 4f), Team.A, new Vec2(-50f, -50f));
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 4f), Team.B, new Vec2(50f, 50f));
            SimUnit first = sim.AddUnit(Tower(10f), Team.A, new Vec2(50f, -50f));
            SimUnit second = sim.AddUnit(Tower(4f, kind: TowerKind.ResearchCenter), Team.A, new Vec2(0f, -50f));
            first.Health = 0f;
            RunSeconds(sim, 1f);
            second.Health = 0f;
            RunSeconds(sim, 1f);
            Assert.AreEqual(12f, sim.ShockwaveSecondsLeft(Team.A), 0.1f, "10 - 2 + 4");
            Assert.AreEqual(0.7f, own.SpeedMultiplier, 1e-3f, "Not 0.49.");
        }

        [Test]
        public void OilSlows_AndFireBurnsHarder_OnTheGroundOnly()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit inside = sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 4f), Team.B, new Vec2(0f, 0f));
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 4f), Team.A, new Vec2(70f, -70f));
            sim.AddSpell(RulesWithTowers().OilZone, Team.A, inside.Position);
            sim.Step();
            sim.Step();
            Assert.AreEqual(0.6f, inside.SpeedMultiplier, 1e-3f);
            Assert.AreEqual(1.5f, inside.FireDamageTaken, 1e-3f);
        }

        [Test]
        public void EachSide_HasItsCommandCenterOnItsOwnLeft_AndNoGroundSquadStandsOnIt()
        {
            Match match = Started(out UnitStats infantry);
            CombatRules rules = match.CombatRules;
            Vec2 ownLeft = DeploymentGrid.TowerCentre(match.Player.FindTower(TowerKind.CommandCenter), Team.A, rules);
            Vec2 theirLeft = DeploymentGrid.TowerCentre(match.Enemy.FindTower(TowerKind.CommandCenter), Team.B, rules);
            Assert.Less(ownLeft.X, 0f);
            Assert.Greater(theirLeft.X, 0f, "Team B looks the other way.");
            PlacedTower tower = match.Player.FindTower(TowerKind.CommandCenter);
            Assert.IsFalse(DeploymentGrid.CanPlace(match.Player, infantry, tower.Column, tower.Row, false, rules));
            var flier = new UnitStats { Name = "Flier", SquadSize = 1, CollisionRadius = 1f, IsAir = true };
            Assert.IsTrue(DeploymentGrid.CanPlace(match.Player, flier, tower.Column, tower.Row, false, rules));
        }

        [Test]
        public void EveryBattle_RebuildsTheTowers_AtTheirLevel_AfterTheSquads()
        {
            Match match = Started(out _);
            match.Player.Credits = 1000;
            Assert.IsTrue(match.LevelUpTower(Team.A, TowerKind.CommandCenter).Ok);
            Assert.IsTrue(match.LevelUpTower(Team.A, TowerKind.CommandCenter).Ok);
            BattleSimulation battle = match.StartBattle();
            SimUnit[] towers = battle.Units.Where(u => u.Stats.IsTower).ToArray();
            Assert.AreEqual(4, towers.Length);
            Assert.AreEqual(400f, towers[0].Stats.MaxHealth, "100 doubled twice.");
            Assert.AreEqual(6f, towers[0].Stats.Shockwave.Seconds);
            Assert.AreEqual(100f, towers[1].Stats.MaxHealth);
            Assert.IsTrue(battle.Units.Take(battle.Units.Count - 4).All(u => !u.Stats.IsTower));
        }

        [Test]
        public void ATower_LevelsUp_AtAFlatPrice_ToLevel5()
        {
            Match match = Started(out _);
            match.Player.Credits = 1000;
            for (int i = 0; i < 4; i++) Assert.IsTrue(match.LevelUpTower(Team.A, TowerKind.ResearchCenter).Ok);
            Assert.IsFalse(match.LevelUpTower(Team.A, TowerKind.ResearchCenter).Ok);
            Assert.AreEqual(600, match.Player.Credits);
            Assert.AreEqual(1600f, match.TowerStats(TowerKind.ResearchCenter, 5).MaxHealth);
            Assert.AreEqual(2f, match.TowerStats(TowerKind.ResearchCenter, 5).Shockwave.Seconds);
        }

        [Test]
        public void TheExtraSlot_LastsOneRound()
        {
            Match match = Started(out _);
            match.Player.Credits = 500;
            int slots = match.SquadsPerRound(Team.A);
            Assert.IsTrue(match.BuyExtraSlot(Team.A).Ok);
            Assert.IsFalse(match.BuyExtraSlot(Team.A).Ok, "Once a round.");
            Assert.AreEqual(slots + 1, match.SquadsPerRound(Team.A));
            Assert.AreEqual(400, match.Player.Credits);
            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();
            Assert.AreEqual(slots, match.SquadsPerRound(Team.A));
        }

        [Test]
        public void RapidResupply_Gives200Now_And300LessNextRound()
        {
            Match match = Started(out _);
            int income = match.NextIncomeFor(Team.A);
            int credits = match.Player.Credits;
            Assert.IsTrue(match.RapidResupply(Team.A).Ok);
            Assert.AreEqual(credits + 200, match.Player.Credits);
            Assert.AreEqual(income - 300, match.NextIncomeFor(Team.A));
            int before = match.Player.Credits;
            match.StartBattle().Run();
            match.FinishBattle();
            Assert.AreEqual(before + income - 300, match.Player.Credits);
            Assert.AreEqual(income, match.NextIncomeFor(Team.A), "Only that round.");
        }

        [Test]
        public void EliteRecruitment_MakesNewSquadsLevel2_AtHigherPrice_ForThisRoundOnly()
        {
            Match match = Started(out UnitStats infantry);
            match.Player.Credits = 1000;
            Assert.IsTrue(match.BuyEliteRecruitment(Team.A).Ok);
            Assert.AreEqual(900, match.Player.Credits);
            Assert.AreEqual(150, match.SquadPrice(Team.A, infantry), "100 and half of it.");
            Assert.IsTrue(match.Buy(Team.A, infantry).Ok);
            PlacedSquad squad = match.Player.Squads.Last();
            Assert.AreEqual(2, squad.Level);
            Assert.AreEqual(150, squad.PricePaid);
            Assert.AreEqual(750, match.Player.Credits);
            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();
            Assert.AreEqual(100, match.SquadPrice(Team.A, infantry));
        }

        [Test]
        public void Research_AddsUp_AtItsTwoPrices_AndReachesTheArmy()
        {
            Match match = Started(out UnitStats infantry);
            match.Player.Credits = 1000;
            Assert.IsTrue(match.BuyResearch(Team.A, health: true).Ok);
            Assert.AreEqual(900, match.Player.Credits);
            Assert.IsTrue(match.BuyResearch(Team.A, health: true).Ok);
            Assert.AreEqual(600, match.Player.Credits);
            Assert.IsFalse(match.BuyResearch(Team.A, health: true).Ok, "Two steps.");
            Assert.AreEqual(1.3f, match.Player.Buffs.HealthMultiplier, 1e-3f);
            Assert.IsTrue(match.BuyResearch(Team.A, health: false).Ok);
            Assert.AreEqual(1.12f, match.Player.Buffs.DamageMultiplier, 1e-3f);
            UnitStats fighting = match.Player.EffectiveStats(new PlacedSquad { Type = infantry });
            Assert.AreEqual(infantry.MaxHealth * 1.3f, fighting.MaxHealth, 1e-2f);
        }

        [Test]
        public void DamageResearch_ReachesDeathBursts()
        {
            var carrier = new UnitStats { Name = "Carrier", SquadSize = 1, MaxHealth = 10f, DeathBurst = new DeathBurstStats { Damage = 100f, Radius = 3f } };
            UnitStats fighting = UnitUpgrades.Apply(carrier, 1, null, null, new ArmyBuffs { DamageMultiplier = 1.24f });
            Assert.AreEqual(124f, fighting.DeathBurst.Damage, 1e-3f);
            Assert.AreEqual(100f, carrier.DeathBurst.Damage, "The unit type itself isn't touched.");
        }

        [Test]
        public void Research_ReachesWhatAUnitBrings()
        {
            var brought = new UnitStats { Name = "Brought", SquadSize = 3, MaxHealth = 100f };
            var unit = new UnitStats
            {
                Name = "Unit", SquadSize = 1, MaxHealth = 10f, Reinforcement = brought, SpawnedUnit = brought,
                DeathBurst = new DeathBurstStats { Spawns = new[] { new SpawnEntry { Unit = brought, Bodies = 2 } } },
            };
            UnitStats fighting = UnitUpgrades.Apply(unit, 1, null, null, new ArmyBuffs { HealthMultiplier = 1.3f });
            Assert.AreEqual(130f, fighting.Reinforcement.MaxHealth, 1e-3f);
            Assert.AreEqual(130f, fighting.SpawnedUnit.MaxHealth, 1e-3f);
            Assert.AreEqual(3, fighting.SpawnedUnit.SquadSize);
            Assert.AreEqual(130f, fighting.DeathBurst.Spawns[0].Unit.MaxHealth, 1e-3f);
            Assert.AreEqual(100f, brought.MaxHealth, "The unit type itself isn't touched.");
        }

        [Test]
        public void SquadsThatLand_HaveTheirArmysResearch()
        {
            Match match = Started(out _);
            match.Player.Credits = 1000;
            match.BuyResearch(Team.A, health: true);
            var trooper = new UnitStats { Name = "Trooper", SquadSize = 2, MaxHealth = 100f, CollisionRadius = 0.5f };
            var pods = new SpellStats { Name = "Pods", Effect = SpellEffect.DropSquad, Offensive = true, DropUnit = trooper, Radius = 3f };
            match.Player.Spells.Add(new OwnedSpell { Id = 99, Stats = pods, ReadyRound = match.Round });
            Assert.IsTrue(match.PlaceSpell(Team.A, 99, new Vec2(0f, -30f)).Ok);
            BattleSimulation battle = match.StartBattle();
            Assert.AreEqual(115f, battle.Spells[0].Stats.DropUnit.MaxHealth, 1e-3f);
            Assert.AreEqual(100f, pods.DropUnit.MaxHealth, "The spell itself isn't touched.");
        }

        private static int BeamHits(bool shockwave)
        {
            WeaponStats beam = TestUnits.Weapon(damage: 1f, range: 30f, interval: 0.1f);
            beam.BeamSeconds = 3f;
            beam.BeamChargeSeconds = 2f;
            beam.BeamRestSeconds = 3f;
            beam.SplashRadius = 2f;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit shooter = sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f, weapon: beam), Team.A, new Vec2(0f, 0f));
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f), Team.B, new Vec2(0f, 10f));
            SimUnit tower = sim.AddUnit(Tower(seconds: 200f, health: 1000f), Team.A, new Vec2(50f, -50f));
            if (shockwave) tower.Health = 0f;
            int hits = 0;
            sim.Events.UnitDamaged += (target, attacker, amount) => { if (attacker == shooter) hits++; };
            RunSeconds(sim, 40f);
            return hits;
        }

        [Test]
        public void ABeam_FiresSlower_UnderAShockwave()
        {
            int normal = BeamHits(false), weakened = BeamHits(true);
            Assert.Greater(normal, 0);
            Assert.AreEqual(0.7f, weakened / (float)normal, 0.08f, "About 30% fewer hits: not only a longer rest, and not 30% twice.");
        }

        [Test]
        public void TheOilZone_IsOneUse()
        {
            Match match = Started(out _);
            match.Player.Credits = 1000;
            Assert.IsTrue(match.BuyOilZone(Team.A).Ok);
            Assert.IsFalse(match.BuyOilZone(Team.A).Ok, "Once a round.");
            OwnedSpell oil = match.Player.Spells.Single();
            Assert.IsTrue(match.PlaceSpell(Team.A, oil.Id, new Vec2(0f, 40f)).Ok);
            match.StartBattle();
            Assert.AreEqual(0, match.Player.Spells.Count, "Gone once used.");
        }

        [Test]
        public void TheAi_DoesNotBuyFromTowers_OnEasy_ButNormalLevelsTheOneThatFalls()
        {
            Match easy = Started(out _, new RuleBasedAi(AiDifficulty.Easy, 5));
            easy.Enemy.FindTower(TowerKind.ResearchCenter).Falls = 3;
            easy.Enemy.Credits = 2000;
            easy.EnemyAi.Deploy(new AiView(easy, Team.B));
            Assert.AreEqual(1, easy.Enemy.FindTower(TowerKind.ResearchCenter).Level);

            Match normal = Started(out _, new RuleBasedAi(AiDifficulty.Normal, 5));
            normal.Enemy.FindTower(TowerKind.ResearchCenter).Falls = 3;
            normal.Enemy.Credits = 2000;
            normal.EnemyAi.Deploy(new AiView(normal, Team.B));
            Assert.AreEqual(2, normal.Enemy.FindTower(TowerKind.ResearchCenter).Level);
            Assert.AreEqual(1, normal.Enemy.FindTower(TowerKind.CommandCenter).Level);
        }

        [Test]
        public void NormalAi_DefendsTheSideThatFalls()
        {
            var ai = new RuleBasedAi(AiDifficulty.Normal, 5);
            Match match = Started(out UnitStats infantry, ai);
            PlacedTower falls = match.Enemy.FindTower(TowerKind.ResearchCenter);
            falls.Falls = 2;
            Assert.IsTrue(ai.ChooseSpot(new AiView(match, Team.B), infantry, out int column, out _));
            Assert.Greater(column, match.CombatRules.ArenaWidthSquares / 2, "On that tower's side of its own grid.");
        }

        [Test]
        public void HardAi_PushesTheThinnerSide_OfTheArmyItSaw()
        {
            var ai = new RuleBasedAi(AiDifficulty.Hard, 5);
            Match match = Started(out UnitStats infantry, ai);
            // Everything the player has stands on the battlefield's left, as Team A sees it.
            foreach (PlacedSquad squad in match.Player.Squads) squad.Column = 2;
            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();
            var view = new AiView(match, Team.B);
            Assert.IsTrue(view.SeenOpponentArmy.All(s => view.WorldCentre(s).X < 0f));
            Assert.IsTrue(ai.ChooseSpot(view, infantry, out int column, out _));
            // The battlefield's right is Team B's own left: its low columns.
            Assert.Less(column, match.CombatRules.ArenaWidthSquares / 2, "Up the empty side.");
        }
    }
}
