// LevelingAndTechTests.cs
// Tests squad leveling (Bible Section 5, rule 10 "leveling") and tech upgrades (Section 9),
// Milestone 6: level multipliers, XP from kills (capped at a bar that is the same at every level),
// XP the bar shows during a battle, XP for the bodies a squad releases and for a blast it dies in (D136), the share a body made in the battle is worth (D138),
// a kill's XP split between the killer and the squads that damaged the victim (xp_split),
// level-ups for half the base price, leveling every ready squad of a type at once, level and XP
// surviving the round reset, sell value, techs
// bought once per unit type and applied to every squad of that type, a drop unlocked by a tech,
// and the AI using both.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestMatch, TestUnits, TestRules.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class LevelingAndTechTests
    {
        // ------------------------------------------------------------------ level multipliers

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void LevelMultipliesBaseHealthAndDamage_ByTheLevelNumber(int level)
        {
            UnitStats unit = TestUnits.Unit(health: 100f, weapon: TestUnits.Weapon(damage: 10f));
            UnitStats leveled = UnitUpgrades.Apply(unit, level, null);
            Assert.AreEqual(100f * level, leveled.MaxHealth, 1e-3f);
            Assert.AreEqual(10f * level, leveled.Weapons[0].DamagePerShot, 1e-3f);
            Assert.AreEqual(level, leveled.Level);
            Assert.AreEqual(unit.Name, leveled.Name, "Same unit type for visuals and results.");
            Assert.AreEqual(100f, unit.MaxHealth, "The base stats are not changed.");
            if (level == 1) Assert.AreSame(unit, leveled, "Level 1 with no techs is the base unit itself.");
        }

        [Test]
        public void LevelIsCappedAt5()
        {
            UnitStats unit = TestUnits.Unit(health: 100f);
            Assert.AreEqual(500f, UnitUpgrades.Apply(unit, 9, null).MaxHealth, 1e-3f);
        }

        // ------------------------------------------------------------------ XP and level-ups

        /// <summary>Round 1 with one strong player squad facing weak enemy chaff that it will wipe out.</summary>
        private static Match KillingRound(out PlacedSquad killer, out UnitStats chaff)
        {
            Match match = TestMatch.Started(out UnitStats infantry, out _, out UnitStats tank, out _);
            chaff = infantry;
            match.Rules.DamageXpAsItLands = false; // These tests count kill XP (xp_split); the damage XP paid as it lands (D177) has its own.
            match.Player.Squads.Clear();
            match.Enemy.Squads.Clear();
            match.Player.Unlocked.Add(tank);
            killer = new PlacedSquad { Id = 500, Type = tank, Column = 38, Row = 0, RoundBought = 1 };
            match.Player.Squads.Add(killer);
            match.Enemy.Squads.Add(new PlacedSquad { Id = 600, Type = infantry, Column = 36, Row = 0, RoundBought = 1 });
            return match;
        }

        [Test]
        public void Kills_GiveXp_WorthTheVictimsPricePerBody_CappedAtTheBar()
        {
            Match match = KillingRound(out PlacedSquad killer, out UnitStats chaff);
            match.StartBattle().Run();
            RoundSummary summary = match.FinishBattle();

            int killed = match.CurrentBattle.Units.Count(u => u.Team == Team.B && !u.IsAlive && u.KilledById >= 0
                                                             && match.CurrentBattle.Units[u.KilledById].Team == Team.A);
            Assert.Greater(killed, 0, "The tank should kill some infantry.");
            float expected = killed * chaff.Cost / (float)chaff.SquadSize;
            Assert.AreEqual(expected, summary.XpGained[killer.Id], 1e-3f);
            Assert.AreEqual(System.Math.Min(expected, match.XpNeeded(killer)), killer.Xp, 1e-3f);
            Assert.AreEqual(killer.Type.Cost * match.Rules.XpBarPerSquadCost, match.XpNeeded(killer), 1e-3f);
        }

        [Test]
        public void XpBar_FillsDuringTheBattle_AndEndsWhereTheAwardPutsIt()
        {
            Match match = KillingRound(out PlacedSquad killer, out _);
            BattleSimulation battle = match.StartBattle();
            Assert.AreEqual(0f, match.XpSoFar(killer));
            float last = 0f;
            bool roseMidBattle = false;
            while (!battle.IsFinished)
            {
                battle.Step();
                float now = match.XpSoFar(killer);
                Assert.GreaterOrEqual(now, last - 1e-4f, "The bar never goes down.");
                if (now > 0f && !battle.IsFinished) roseMidBattle = true;
                last = now;
            }
            Assert.IsTrue(roseMidBattle, "The bar fills while the battle runs (the owner, D136).");
            Assert.AreEqual(0f, killer.Xp, "Nothing is banked until the battle is finished.");
            match.FinishBattle();
            Assert.AreEqual(last, killer.Xp, 1e-3f, "The bar ends where the award puts it.");
            Assert.AreEqual(killer.Xp, match.XpSoFar(killer), 1e-3f, "After the battle it shows the banked XP, not counted twice.");
        }

        private static int KilledBy(BattleSimulation battle, string killerName) =>
            battle.Units.Count(u => u.Team == Team.B && !u.IsAlive && u.KilledById >= 0 && battle.Units[u.KilledById].Stats.Name == killerName);

        [Test]
        public void ABodyItReleases_EarnsXpForTheSquadItCameFrom()
        {
            Match match = KillingRound(out PlacedSquad killer, out UnitStats chaff);
            UnitStats nest = TestUnits.Unit("Nest", health: 100000f, speed: 0f, radius: 2f, cost: 300, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            nest.Tier = 2;
            nest.SpawnedUnit = TestUnits.Unit("Brood", health: 400f, speed: 4f, squadSize: 2, cost: 100, weapon: TestUnits.Weapon(damage: 30f, range: 12f, interval: 0.5f));
            nest.SpawnIntervalSeconds = 2f;
            killer.Type = nest;
            match.Player.Unlocked.Add(nest);
            match.StartBattle().Run();
            RoundSummary summary = match.FinishBattle();

            int byBrood = KilledBy(match.CurrentBattle, "Brood");
            Assert.Greater(byBrood, 0, "Its brood should kill some infantry.");
            Assert.AreEqual(0, KilledBy(match.CurrentBattle, "Nest"), "The nest itself kills nothing.");
            Assert.AreEqual(byBrood * chaff.Cost / (float)chaff.SquadSize, summary.XpGained[killer.Id], 1e-3f,
                "What it releases earns for its squad (the owner, D136).");
        }

        [TestCase(1f)]
        [TestCase(0.25f)]
        [TestCase(0f)]
        public void ABodyMadeInTheBattle_IsWorthBattleBodyXpShareOfItsPrice(float share)
        {
            // The enemy's nest never dies; its brood, released during the battle, does. 1 is the game as it is (D138's measuring option).
            Match match = KillingRound(out PlacedSquad killer, out _);
            match.Rules.BattleBodyXpShare = share;
            UnitStats nest = TestUnits.Unit("Nest", health: 1e7f, speed: 0f, radius: 2f, cost: 300, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            nest.Tier = 2;
            nest.SpawnedUnit = TestUnits.Unit("Brood", health: 50f, speed: 4f, squadSize: 2, cost: 100, weapon: TestUnits.Weapon(damage: 1f, range: 3f, interval: 1f));
            nest.SpawnIntervalSeconds = 2f;
            match.Enemy.Squads[0].Type = nest;
            match.Enemy.Unlocked.Add(nest);
            match.StartBattle().Run();
            RoundSummary summary = match.FinishBattle();

            BattleSimulation battle = match.CurrentBattle;
            int brood = battle.Units.Count(u => u.Stats.Name == "Brood" && !u.IsAlive && u.KilledById >= 0 && battle.Units[u.KilledById].Team == Team.A);
            Assert.Greater(brood, 0, "The tank should kill some of the brood.");
            Assert.IsTrue(battle.Units.Where(u => u.Stats.Name == "Nest").All(u => u.IsAlive), "The nest itself survives, so all the XP is the brood's.");
            summary.XpGained.TryGetValue(killer.Id, out float gained);
            Assert.AreEqual(brood * 50f * share, gained, 1e-3f);
        }

        [Test]
        public void ABlastItDiesIn_StillEarnsXpForItsSquad()
        {
            // The Infected Pelican's dive: it dies in its own blast, and its squad comes back next round with the XP.
            Match match = KillingRound(out PlacedSquad killer, out UnitStats chaff);
            UnitStats crasher = TestUnits.Unit("Crasher", health: 5000f, speed: 20f, radius: 2f, air: true, filter: TargetFilter.Ground, cost: 300,
                weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            crasher.Tier = 2;
            crasher.IsFlood = true;
            crasher.SelfDestructs = true;
            crasher.SeeksDensestCluster = true;
            crasher.DeathBurst = new DeathBurstStats { Damage = 500f, Radius = 6f, DamageType = DamageType.Explosive };
            killer.Type = crasher;
            match.Player.Unlocked.Add(crasher);
            match.StartBattle().Run();
            RoundSummary summary = match.FinishBattle();

            int blasted = KilledBy(match.CurrentBattle, "Crasher");
            Assert.Greater(blasted, 0, "Its blast should kill some infantry.");
            float expected = blasted * chaff.Cost / (float)chaff.SquadSize;
            Assert.AreEqual(expected, summary.XpGained[killer.Id], 1e-3f);
            Assert.AreEqual(System.Math.Min(expected, match.XpNeeded(killer)), killer.Xp, 1e-3f);
            Assert.IsFalse(match.CurrentBattle.Units.Any(u => u.IsAlive && u.Health <= 0f),
                "Its blast ended the battle, yet what it killed is dead, not left standing as survivors (D136).");
        }

        // ------------------------------------------------------------------ XP split (xp_split)

        [Test]
        public void KillXp_TheKillerGetsHalf_AndTheOtherHalfGoesByDamage()
        {
            // The owner's example: a Spartan (0) deals 90%, a Scorpion (1) finishes it -> the Scorpion 55%, the Spartan 45%.
            var earned = new float[2];
            Match.SplitKillXp(100f, 0.5f, 1, new List<int> { 0, 1 }, new List<float> { 90f, 10f }, earned);
            Assert.AreEqual(55f, earned[1], 1e-3f);
            Assert.AreEqual(45f, earned[0], 1e-3f);
            Assert.AreEqual(100f, earned.Sum(), 1e-3f, "No XP is lost or made.");
        }

        [Test]
        public void KillXp_WithNoDamageOnRecord_AllGoesToTheKiller()
        {
            var earned = new float[2];
            Match.SplitKillXp(100f, 0.5f, 1, new List<int>(), new List<float>(), earned);
            Assert.AreEqual(100f, earned[1], 1e-3f);
            Assert.AreEqual(0f, earned[0]);
        }

        [Test]
        public void KillXp_KillShare1_IsTheRuleBefore()
        {
            var earned = new float[3];
            Match.SplitKillXp(100f, 1f, 2, new List<int> { 0, 1, 2 }, new List<float> { 60f, 30f, 10f }, earned);
            Assert.AreEqual(100f, earned[2], 1e-3f);
            Assert.AreEqual(0f, earned[0], 1e-6f);
            Assert.AreEqual(0f, earned[1], 1e-6f);
        }

        /// <summary>
        /// KillingRound plus a second Player squad (Id 501); in the battle, body X of the enemy squad took 90 from squad 500 and 10 from
        /// squad 501, which killed it, and body Y took 50 from squad 500 but died to no squad (a spell or a self-kill). Every other enemy
        /// body (and tower) dies to nobody.
        /// </summary>
        private static RoundSummary SplitRound(float killXpShare, out UnitStats chaff, bool asItLands = false)
        {
            Match match = KillingRound(out PlacedSquad first, out chaff);
            match.Rules.KillXpShare = killXpShare;
            match.Rules.DamageXpAsItLands = asItLands;
            match.Player.Squads.Add(new PlacedSquad { Id = 501, Type = first.Type, Column = 30, Row = 0, RoundBought = 1 });
            BattleSimulation battle = match.StartBattle();
            SimSquad squad500 = battle.Squads[0], squad501 = battle.Squads[1], enemy = battle.Squads[2];
            Assert.AreEqual(Team.A, squad501.Team);
            Assert.AreEqual(Team.B, enemy.Team);
            foreach (SimUnit unit in battle.Units)
                if (unit.Team == Team.B) unit.Health = 0f;
            SimUnit x = enemy.Members[0], y = enemy.Members[1];
            x.AddSquadDamage(squad500.Id, 90f);
            x.AddSquadDamage(squad501.Id, 10f);
            x.KilledById = squad501.Members[0].Id;
            y.AddSquadDamage(squad500.Id, 50f);
            while (!battle.IsFinished) battle.Step();
            return match.FinishBattle();
        }

        [Test]
        public void KillXp_InAMatch_SplitsByTheRecord_AndAKillByNoSquadGivesNothing()
        {
            RoundSummary summary = SplitRound(0.5f, out UnitStats chaff);
            float worth = chaff.Cost / (float)chaff.SquadSize;
            Assert.AreEqual(0.55f * worth, summary.XpGained[501], 1e-3f, "The killer: half, plus its 10% of the other half.");
            Assert.AreEqual(0.45f * worth, summary.XpGained[500], 1e-3f, "The damager: its 90% of the other half; body Y gives nothing.");
            Assert.AreEqual(2, summary.XpGained.Count);
        }

        [Test]
        public void KillXp_InAMatch_KillShare1_GivesItAllToTheKiller()
        {
            RoundSummary summary = SplitRound(1f, out UnitStats chaff);
            Assert.AreEqual(chaff.Cost / (float)chaff.SquadSize, summary.XpGained[501], 1e-3f);
            Assert.IsFalse(summary.XpGained.ContainsKey(500));
        }

        [Test]
        public void TwoSquadsOnTheSameEnemy_NoXpIsLost()
        {
            // A real battle: the split only moves XP between the squads; the total is what the kills are worth.
            Match match = KillingRound(out PlacedSquad first, out UnitStats chaff);
            match.Player.Squads.Add(new PlacedSquad { Id = 501, Type = first.Type, Column = 38, Row = 4, RoundBought = 1 });
            match.StartBattle().Run();
            RoundSummary summary = match.FinishBattle();

            BattleSimulation battle = match.CurrentBattle;
            int squad500 = battle.Squads[0].Id, squad501 = battle.Squads[1].Id;
            int killed = battle.Units.Count(u => u.Team == Team.B && !u.Stats.IsTower && !u.IsAlive && u.KilledById >= 0
                                                 && (battle.Units[u.KilledById].CreditSquadId == squad500 || battle.Units[u.KilledById].CreditSquadId == squad501));
            Assert.Greater(killed, 0, "The tanks should kill some infantry.");
            Assert.IsTrue(battle.Units.Any(u => u.Team == Team.B && u.SquadDamageCount > 0), "The record is filled by the battle's hits.");
            summary.XpGained.TryGetValue(500, out float a);
            summary.XpGained.TryGetValue(501, out float b);
            Assert.AreEqual(killed * chaff.Cost / (float)chaff.SquadSize, a + b, 1e-2f);
        }

        // ------------------------------------------------------------------ damage XP as it lands (D177)

        [Test]
        public void DamageXp_ASurvivorPays_ForTheDamageItTook()
        {
            var earned = new float[1];
            Match.PayDamageXp(100f, 0.5f, -1, 200f, new List<int> { 0 }, new List<float> { 100f }, earned);
            Assert.AreEqual(25f, earned[0], 1e-3f, "Half its pool in damage: half the damage half.");
        }

        [Test]
        public void DamageXp_HealingAndBeingHurtAgain_PaysAgain()
        {
            var earned = new float[1];
            Match.PayDamageXp(100f, 0.5f, -1, 200f, new List<int> { 0 }, new List<float> { 600f }, earned);
            Assert.AreEqual(150f, earned[0], 1e-3f, "Three pools of damage, healed in between: the damage half three times.");
        }

        [Test]
        public void DamageXp_AKillFromFull_PaysAsBefore()
        {
            var before = new float[2];
            var now = new float[2];
            Match.SplitKillXp(100f, 0.5f, 1, new List<int> { 0, 1 }, new List<float> { 90f, 10f }, before);
            Match.PayDamageXp(100f, 0.5f, 1, 100f, new List<int> { 0, 1 }, new List<float> { 90f, 10f }, now);
            Assert.AreEqual(before[0], now[0], 1e-3f);
            Assert.AreEqual(before[1], now[1], 1e-3f);
        }

        [Test]
        public void DamageXp_AKillASpellHelped_StillPaysTheWholeRest()
        {
            var earned = new float[2];
            Match.PayDamageXp(100f, 0.5f, 1, 100f, new List<int> { 0 }, new List<float> { 40f }, earned);
            Assert.AreEqual(50f, earned[1], 1e-3f, "The killer: its half.");
            Assert.AreEqual(50f, earned[0], 1e-3f, "The only squad on the record: the whole other half, as on a kill before.");
        }

        [Test]
        public void DamageXp_InAMatch_ABodyNoSquadKilled_StillPaysForItsDamage()
        {
            RoundSummary summary = SplitRound(0.5f, out UnitStats chaff, asItLands: true);
            float worth = chaff.Cost / (float)chaff.SquadSize;
            float pool = chaff.MaxHealth + chaff.MaxShield;
            float xRest = 0.5f * worth * System.Math.Max(1f, 100f / pool);
            Assert.AreEqual(0.5f * worth + 0.1f * xRest, summary.XpGained[501], 1e-3f);
            Assert.AreEqual(0.9f * xRest + 0.5f * worth * 50f / pool, summary.XpGained[500], 1e-3f, "Body Y's 50 damage pays, though no squad killed it.");
        }

        [Test]
        public void LevelUp_NeedsAFullBar_CostsHalfTheBasePrice_AndResetsTheBar()
        {
            Match match = TestMatch.Started(out _, out _, out _, out _);
            PlacedSquad squad = match.Player.Squads[0];
            match.Player.Credits = 1000;
            Assert.IsFalse(match.LevelUp(Team.A, squad.Id).Ok, "The bar is empty.");

            squad.Xp = match.XpNeeded(squad);
            int cost = squad.Type.Cost / 2;
            Assert.AreEqual(cost, match.LevelUpCost(squad));
            Assert.IsTrue(match.LevelUp(Team.A, squad.Id).Ok);
            Assert.AreEqual(2, squad.Level);
            Assert.AreEqual(0f, squad.Xp);
            Assert.AreEqual(1000 - cost, match.Player.Credits);

            // The bar is the same size at the next level, and the next level-up costs the same.
            Assert.AreEqual(squad.Type.Cost * match.Rules.XpBarPerSquadCost, match.XpNeeded(squad), 1e-3f);
            squad.Xp = match.XpNeeded(squad);
            Assert.IsTrue(match.LevelUp(Team.A, squad.Id).Ok);
            Assert.AreEqual(1000 - 2 * cost, match.Player.Credits);
        }

        [Test]
        public void LevelUp_StopsAtLevel5_AndNeedsCredits_AndOnlyDuringDeployment()
        {
            Match match = TestMatch.Started(out _, out _, out _, out _);
            PlacedSquad squad = match.Player.Squads[0];
            squad.Level = 5;
            squad.Xp = match.XpNeeded(squad);
            match.Player.Credits = 1000;
            Assert.IsFalse(match.LevelUp(Team.A, squad.Id).Ok, "Level 5 is the maximum.");

            squad.Level = 2;
            match.Player.Credits = 0;
            Assert.IsFalse(match.LevelUp(Team.A, squad.Id).Ok, "Not enough credits.");

            match.Player.Credits = 1000;
            match.StartBattle();
            Assert.IsFalse(match.LevelUp(Team.A, squad.Id).Ok, "Not during the battle.");
        }

        [Test]
        public void LevelAndXp_SurviveTheRoundReset_AndTheNextBattleUsesTheLevel()
        {
            Match match = TestMatch.Started(out _, out _, out _, out _);
            PlacedSquad squad = match.Player.Squads[0];
            squad.Level = 3;
            squad.Xp = 12f;
            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();

            Assert.AreEqual(3, squad.Level);
            Assert.GreaterOrEqual(squad.Xp, 12f);
            BattleSimulation next = match.StartBattle();
            SimUnit body = next.Units.First(u => u.Team == Team.A && u.SquadId == next.Squads[0].Id);
            Assert.AreEqual(squad.Type.MaxHealth * 3f, body.Stats.MaxHealth, 1e-3f);
            Assert.AreEqual(squad.Type.MaxHealth * 3f, body.Health, 1e-3f, "Back at full (leveled) health.");
            Assert.AreEqual(3, match.SeenArmy(Team.A)[0].Level, "The opponent sees the level.");
        }

        [Test]
        public void SellValue_IncludesLevelUpSpending()
        {
            Match match = TestMatch.Started(out UnitStats infantry, out _, out _, out _);
            match.Player.Credits = 5000;
            Assert.IsTrue(match.Buy(Team.A, infantry, 0, 10, false).Ok);
            PlacedSquad bought = match.Player.Squads.Last();
            bought.Xp = match.XpNeeded(bought);
            Assert.IsTrue(match.LevelUp(Team.A, bought.Id).Ok);
            Assert.AreEqual(infantry.Cost + infantry.Cost / 2, match.SellValue(bought), "Full refund before its first battle, level-up included.");
        }

        // ------------------------------------------------------------------ leveling all of a type

        /// <summary>Round 1 with three squads of a type priced like the Carrier Form, all ready to level up, and one Tank, also ready.</summary>
        private static Match ThreeCarriers(out UnitStats carrier, out List<PlacedSquad> carriers, out PlacedSquad tank)
        {
            Match match = TestMatch.Started(out _, out _, out UnitStats tankType, out _);
            carrier = TestUnits.Unit(name: "Carrier Form", cost: 150);
            match.Player.Squads.Clear();
            match.Enemy.Squads.Clear();
            carriers = new List<PlacedSquad>();
            for (int i = 0; i < 3; i++)
            {
                var squad = new PlacedSquad { Id = 700 + i, Type = carrier, Column = 2 + 4 * i, Row = 0, RoundBought = 1 };
                squad.Xp = match.XpNeeded(squad);
                carriers.Add(squad);
                match.Player.Squads.Add(squad);
            }
            tank = new PlacedSquad { Id = 710, Type = tankType, Column = 30, Row = 0, RoundBought = 1 };
            tank.Xp = match.XpNeeded(tank);
            match.Player.Squads.Add(tank);
            return match;
        }

        [Test]
        public void LevelUpAll_ThreeCarrierPricedSquads_Cost225_AndAllLevelUp()
        {
            Match match = ThreeCarriers(out UnitStats carrier, out List<PlacedSquad> carriers, out _);
            match.Player.Credits = 1000;

            Assert.AreEqual(3, match.SquadsReadyToLevel(Team.A, carrier, out int price));
            Assert.AreEqual(225, price);
            Assert.IsTrue(match.LevelUpAll(Team.A, carrier).Ok);

            Assert.AreEqual(1000 - 225, match.Player.Credits);
            foreach (PlacedSquad squad in carriers)
            {
                Assert.AreEqual(2, squad.Level);
                Assert.AreEqual(0f, squad.Xp);
                Assert.AreEqual(75, squad.LevelUpSpent, "Each squad carries its own share, for its refund.");
            }
            Assert.AreEqual(0, match.SquadsReadyToLevel(Team.A, carrier, out price), "Nobody is ready any more.");
            Assert.AreEqual(0, price);
            Assert.IsFalse(match.LevelUpAll(Team.A, carrier).Ok, "Nothing left to level.");
            Assert.AreEqual(1000 - 225, match.Player.Credits);
        }

        [Test]
        public void LevelUpAll_TooFewCredits_ChangesNothing()
        {
            Match match = ThreeCarriers(out UnitStats carrier, out List<PlacedSquad> carriers, out _);
            match.Player.Credits = 224; // Enough for two of the three, and that must not happen.

            ActionResult result = match.LevelUpAll(Team.A, carrier);

            Assert.IsFalse(result.Ok);
            StringAssert.Contains("225", result.Message);
            Assert.AreEqual(224, match.Player.Credits);
            foreach (PlacedSquad squad in carriers)
            {
                Assert.AreEqual(1, squad.Level);
                Assert.AreEqual(match.XpNeeded(squad), squad.Xp, 1e-3f);
                Assert.AreEqual(0, squad.LevelUpSpent);
            }

            match.Player.Credits = 225;
            Assert.IsTrue(match.LevelUpAll(Team.A, carrier).Ok, "Exactly the price is enough.");
            Assert.AreEqual(0, match.Player.Credits);
        }

        [Test]
        public void LevelUpAll_LeavesOutSquadsWithoutAFullBar_AndAtTheMaximumLevel()
        {
            Match match = ThreeCarriers(out UnitStats carrier, out List<PlacedSquad> carriers, out _);
            match.Player.Credits = 1000;
            carriers[1].Xp = match.XpNeeded(carriers[1]) - 1f;
            carriers[1].Level = 2;
            carriers[2].Level = UnitUpgrades.MaxLevel;
            match.Player.Squads.Add(new PlacedSquad { Id = 720, Type = carrier, Column = 20, Row = 0, RoundBought = 1, Level = 3, Xp = match.XpNeeded(carriers[0]) });

            Assert.AreEqual(2, match.SquadsReadyToLevel(Team.A, carrier, out int price), "The first, and the level 3 one.");
            Assert.AreEqual(150, price);
            float partial = carriers[1].Xp;
            Assert.IsTrue(match.LevelUpAll(Team.A, carrier).Ok);

            Assert.AreEqual(1000 - 150, match.Player.Credits);
            Assert.AreEqual(2, carriers[0].Level);
            Assert.AreEqual(2, carriers[1].Level, "Its bar is not full.");
            Assert.AreEqual(partial, carriers[1].Xp, 1e-3f);
            Assert.AreEqual(0, carriers[1].LevelUpSpent);
            Assert.AreEqual(UnitUpgrades.MaxLevel, carriers[2].Level);
            Assert.AreEqual(0, carriers[2].LevelUpSpent);
            Assert.AreEqual(4, match.Player.FindSquad(720).Level, "The price is the same at every level.");
        }

        [Test]
        public void LevelUpAll_LeavesOtherUnitTypes_AndTheOpponent_Alone()
        {
            Match match = ThreeCarriers(out UnitStats carrier, out _, out PlacedSquad tank);
            match.Player.Credits = 1000;
            match.Enemy.Credits = 1000;
            var theirs = new PlacedSquad { Id = 730, Type = carrier, Column = 2, Row = 0, RoundBought = 1 };
            theirs.Xp = match.XpNeeded(theirs);
            match.Enemy.Squads.Add(theirs);
            float tankXp = tank.Xp;

            Assert.AreEqual(1, match.SquadsReadyToLevel(Team.A, tank.Type, out int tankPrice));
            Assert.AreEqual(tank.Type.Cost / 2, tankPrice);
            Assert.IsTrue(match.LevelUpAll(Team.A, carrier).Ok);

            Assert.AreEqual(1, tank.Level);
            Assert.AreEqual(tankXp, tank.Xp, 1e-3f);
            Assert.AreEqual(0, tank.LevelUpSpent);
            Assert.AreEqual(1, theirs.Level, "The AI's squad of the same type is its own.");
            Assert.AreEqual(1000, match.Enemy.Credits);
        }

        [Test]
        public void LevelUpAll_TheRefundsAreRightAfterwards()
        {
            Match match = TestMatch.Started(out UnitStats infantry, out _, out _, out _);
            match.Player.Squads.Clear();
            match.Player.Credits = 5000;
            match.Player.Unlocked.Add(infantry);
            Assert.IsTrue(match.Buy(Team.A, infantry, 0, 10, false).Ok);
            Assert.IsTrue(match.Buy(Team.A, infantry, 20, 10, false).Ok);
            PlacedSquad first = match.Player.Squads[0], second = match.Player.Squads[1];
            first.Xp = match.XpNeeded(first);
            second.Xp = match.XpNeeded(second);

            Assert.IsTrue(match.LevelUpAll(Team.A, infantry).Ok);
            int cost = infantry.Cost / 2;
            Assert.AreEqual(infantry.Cost + cost, match.SellValue(first), "Full refund before its first battle, level-up included.");
            int credits = match.Player.Credits;
            Assert.IsTrue(match.Sell(Team.A, first.Id).Ok);
            Assert.AreEqual(credits + infantry.Cost + cost, match.Player.Credits);

            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();
            Assert.AreEqual((int)System.MathF.Round((infantry.Cost + cost) * match.Rules.RefundAfterBattle), match.SellValue(second), "Half of price and level-up after a battle.");
        }

        [Test]
        public void LevelUpAll_OnlyDuringDeployment()
        {
            Match match = ThreeCarriers(out UnitStats carrier, out List<PlacedSquad> carriers, out _);
            match.Player.Credits = 1000;
            match.StartBattle();

            Assert.IsFalse(match.LevelUpAll(Team.A, carrier).Ok);
            Assert.AreEqual(1000, match.Player.Credits);
            Assert.AreEqual(1, carriers[0].Level);
        }

        // ------------------------------------------------------------------ techs

        private static TechUpgrade Tech(string name, int cost = 100) => new TechUpgrade { Name = name, Cost = cost };

        [Test]
        public void TechEffects_ApplyToTheStats()
        {
            WeaponStats gun = TestUnits.Weapon(damage: 10f, range: 10f, interval: 1f, splash: 2f);
            WeaponStats flamer = TestUnits.Weapon(damage: 2f, range: 5f);
            flamer.ConeDegrees = 15f;
            UnitStats unit = TestUnits.Unit(health: 100f, speed: 4f, filter: TargetFilter.Ground, weapon: gun);
            unit.Weapons = new[] { gun, flamer };
            var tech = new TechUpgrade
            {
                Name = "Everything", HealthMultiplier = 1.5f, DamageMultiplier = 2f, FireRateMultiplier = 1.25f, RangeBonus = 3f,
                MoveSpeedMultiplier = 1.5f, ShieldBonus = 50f, ShieldRegenBonus = 5f, SplashRadiusBonus = 1f, ConeDegreesBonus = 30f,
                AirDamageMultiplier = 2f, AddsAirTargeting = true,
            };
            UnitStats s = UnitUpgrades.Apply(unit, 2, new[] { tech });

            Assert.AreEqual(100f * 2f * 1.5f, s.MaxHealth, 1e-3f, "Level 2 and +50% health.");
            Assert.AreEqual(10f * 2f * 2f, s.Weapons[0].DamagePerShot, 1e-3f);
            Assert.AreEqual(0.8f, s.Weapons[0].FireInterval, 1e-3f);
            Assert.AreEqual(13f, s.Weapons[0].Range, 1e-3f);
            Assert.AreEqual(3f, s.Weapons[0].SplashRadius, 1e-3f);
            Assert.AreEqual(0f, s.Weapons[1].SplashRadius, "Splash bonus only for weapons that splash.");
            Assert.AreEqual(45f, s.Weapons[1].ConeDegrees, 1e-3f);
            Assert.AreEqual(6f, s.MoveSpeed, 1e-3f);
            Assert.AreEqual(50f, s.MaxShield, 1e-3f);
            Assert.AreEqual(2f, s.Weapons[0].AirDamageMultiplier, 1e-3f);
            Assert.IsTrue(s.Weapons[0].CanTarget(s, targetIsAir: true), "Anti-air upgrade.");
            StringAssert.Contains("+50% health", tech.EffectsText());
        }

        [Test]
        public void BuyTech_OncePerUnitType_ForUnlockedUnits_AppliesToEverySquadOfThatType()
        {
            Match match = TestMatch.Started(out UnitStats infantry, out _, out UnitStats tank, out _);
            TechUpgrade armor = Tech("Armor", 150);
            armor.HealthMultiplier = 2f;
            infantry.TechUpgrades = new[] { armor };
            tank.TechUpgrades = new[] { Tech("Tank Tech") };
            match.Player.Credits = 1000;
            match.Player.Unlocked.Add(infantry);
            match.Player.Squads.Clear();
            match.Buy(Team.A, infantry, 0, 10, false);
            match.Buy(Team.A, infantry, 20, 10, false);

            Assert.IsFalse(match.BuyTech(Team.A, tank, tank.TechUpgrades[0]).Ok, "The tank is not unlocked.");
            Assert.IsFalse(match.BuyTech(Team.A, infantry, tank.TechUpgrades[0]).Ok, "Not one of this unit's techs.");
            int before = match.Player.Credits;
            Assert.IsTrue(match.BuyTech(Team.A, infantry, armor).Ok);
            Assert.AreEqual(before - 150, match.Player.Credits);
            Assert.IsFalse(match.BuyTech(Team.A, infantry, armor).Ok, "Only once.");

            BattleSimulation battle = match.StartBattle();
            foreach (SimUnit body in battle.Units.Where(u => u.Team == Team.A))
                Assert.AreEqual(infantry.MaxHealth * 2f, body.Stats.MaxHealth, 1e-3f, "Every squad of the type gets the tech.");
            Assert.IsFalse(match.BuyTech(Team.A, infantry, armor).Ok, "Not during the battle.");
        }

        [Test]
        public void ADropThatNeedsATech_OnlyHappensAfterTheTech()
        {
            UnitStats carrier = TestUnits.Unit(name: "Carrier", health: 5000f, speed: 0f, air: true, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            carrier.LockedReinforcement = TestUnits.Unit(name: "Trooper", speed: 0f, squadSize: 4, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            carrier.ReinforcementDelaySeconds = 1f;
            var bay = new TechUpgrade { Name = "Troop Bay", UnlocksReinforcement = true };
            carrier.TechUpgrades = new[] { bay };

            int Bodies(UnitStats stats)
            {
                var sim = new BattleSimulation(TestRules.Default(), 1);
                sim.AddUnit(stats, Team.A, new Vec2(0f, -30f));
                sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 1f)), Team.B, new Vec2(0f, 60f));
                while (sim.ElapsedSeconds < 2f) sim.Step();
                return sim.Units.Count;
            }
            Assert.IsFalse(carrier.HasReinforcement);
            Assert.AreEqual(2, Bodies(carrier), "No troops without the tech.");
            Assert.AreEqual(6, Bodies(UnitUpgrades.Apply(carrier, 1, new[] { bay })), "4 troops dropped with it.");
        }

        [Test]
        public void DoubleShot_FiresTwoShotsAGapApart_AndReloadsSlower()
        {
            // D114 (the owner, 2026-11-08): the Grizzly's Double Shot, two shells in quick succession per attack, the reload 20% longer.
            UnitStats tank = TestUnits.Unit(name: "Tank", health: 5000f, speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 20f, interval: 2f));
            tank.Weapons = new[] { tank.Weapons[0], TestUnits.Weapon(damage: 1f, range: 20f, interval: 0.2f) };
            var doubleShot = new TechUpgrade { Name = "Double Shot", ExtraShotsPerAttack = 1, BurstGapSeconds = 0.3f, ReloadTimeMultiplier = 1.2f };
            Assert.IsEmpty(doubleShot.Validate("Tank"));
            StringAssert.Contains("2 shots per attack", doubleShot.EffectsText());
            StringAssert.Contains("+20% reload time", doubleShot.EffectsText());
            UnitStats upgraded = UnitUpgrades.Apply(tank, 1, new[] { doubleShot });
            Assert.AreEqual(2, upgraded.Weapons[0].ShotsPerAttack);
            Assert.AreEqual(2.4f, upgraded.Weapons[0].FireInterval, 1e-4f);
            Assert.AreEqual(1, upgraded.Weapons[1].ShotsPerAttack, "Only the main gun.");
            Assert.AreEqual(0.2f, upgraded.Weapons[1].FireInterval, 1e-4f);

            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(upgraded, Team.A, new Vec2(0f, 0f));
            sim.AddUnit(TestUnits.Unit(name: "Target", health: 100000f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 1f)), Team.B, new Vec2(0f, 10f));
            var shots = new List<float>();
            sim.Events.WeaponFired += (unit, weapon, target) => { if (unit.Team == Team.A && weapon == 0) shots.Add(sim.ElapsedSeconds); };
            while (sim.ElapsedSeconds < 5f) sim.Step();
            Assert.GreaterOrEqual(shots.Count, 4);
            float tick = 0.06f;
            Assert.AreEqual(0.3f, shots[1] - shots[0], tick, "The second shell follows the first.");
            Assert.AreEqual(2.4f, shots[2] - shots[0], tick, "The next attack after the longer reload.");
            Assert.AreEqual(0.3f, shots[3] - shots[2], tick);
        }

        [Test]
        public void TheRangeTech_ReachesGuns_NotClaws()
        {
            // D114: the flat range tech is for guns; a claw or a ram keeps its reach.
            UnitStats unit = TestUnits.Unit(weapon: TestUnits.Weapon(range: 10f));
            unit.Weapons = new[] { unit.Weapons[0], TestUnits.Weapon(range: 4f, type: DamageType.Melee) };
            UnitStats upgraded = UnitUpgrades.Apply(unit, 1, new[] { new TechUpgrade { Name = "Long Range", RangeBonus = 8f } });
            Assert.AreEqual(18f, upgraded.Weapons[0].Range, 1e-4f);
            Assert.AreEqual(4f, upgraded.Weapons[1].Range, 1e-4f, "The claw keeps its reach.");
        }

        [Test]
        public void StaggeredGuns_FireOneAfterAnother()
        {
            // D114: the Condor's five light lasers fire 0.8 s apart instead of all at once.
            UnitStats ship = TestUnits.Unit(name: "Ship", health: 5000f, speed: 0f);
            ship.Weapons = Enumerable.Range(0, 3).Select(i =>
            {
                WeaponStats gun = TestUnits.Weapon(damage: 1f, range: 20f, interval: 4f);
                gun.Name = "Gun " + i;
                gun.HasOwnTargetFilter = true;
                gun.TargetFilter = TargetFilter.Both;
                gun.StaggerSeconds = 0.8f * i;
                return gun;
            }).ToArray();
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(ship, Team.A, new Vec2(0f, 0f));
            sim.AddUnit(TestUnits.Unit(name: "Target", health: 100000f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 1f)), Team.B, new Vec2(0f, 10f));
            var first = new Dictionary<int, float>();
            sim.Events.WeaponFired += (unit, weapon, target) => { if (unit.Team == Team.A && !first.ContainsKey(weapon)) first[weapon] = sim.ElapsedSeconds; };
            while (sim.ElapsedSeconds < 4f) sim.Step();
            Assert.AreEqual(3, first.Count);
            Assert.AreEqual(0.8f, first[1] - first[0], 0.06f);
            Assert.AreEqual(1.6f, first[2] - first[0], 0.06f);
        }

        // ------------------------------------------------------------------ the AI uses them

        [Test]
        public void TheAi_LevelsUpReadySquads_AndBuysTechs()
        {
            var ai = new RuleBasedAi(AiDifficulty.Normal, 4);
            Match match = TestMatch.Started(out UnitStats infantry, out UnitStats buggy, out _, out _, ai: ai);
            foreach (UnitStats unit in match.Enemy.Roster) unit.TechUpgrades = new[] { Tech(unit.Name + " Tech", 50) };
            foreach (PlacedSquad squad in match.Enemy.Squads) squad.Xp = match.XpNeeded(squad);
            match.StartBattle().Run();
            match.FinishBattle();
            match.Enemy.Credits = 5000;
            foreach (PlacedSquad squad in match.Enemy.Squads) squad.Xp = match.XpNeeded(squad);
            match.ContinueAfterResult(); // Round 2: the AI deploys.

            Assert.IsTrue(match.Enemy.Squads.Any(s => s.Level >= 2), ai.LastDecision);
            Assert.IsTrue(match.Enemy.Techs.Count > 0, ai.LastDecision);
        }
    }
}
