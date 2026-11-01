// LevelingAndTechTests.cs
// Tests squad leveling (Bible Section 5, rule 10 "leveling") and tech upgrades (Section 9),
// Milestone 6: level multipliers, XP from kills (capped at a bar that is the same at every level),
// level-ups for half the base price, level and XP surviving the round reset, sell value, techs
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
