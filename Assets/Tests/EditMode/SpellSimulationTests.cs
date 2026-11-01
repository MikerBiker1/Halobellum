// SpellSimulationTests.cs
// Spell effects inside a battle (Bible Section 10, Milestone 7): strikes and damage zones hit only
// the caster's enemies inside the radius, at the right time, through the damage table and the target
// filter; buffs give the caster's units a bonus shield or more damage; drop spells land squads that
// fight for that battle; spell damage and kills are counted in the result.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SpellSimulationTests
    {
        /// <summary>A unit that stands still and never shoots (filter Air, nothing to hit).</summary>
        private static UnitStats Dummy(float health = 1000f, ArmorType armor = ArmorType.Unarmored, bool air = false) =>
            TestUnits.Unit(name: air ? "Flyer" : "Dummy", health: health, armor: armor, speed: 0f, air: air, filter: TargetFilter.Air);

        private static SpellStats Strike(float damage = 100f, float radius = 5f, float delay = 1f, TargetFilter targets = TargetFilter.Ground) => new SpellStats
        {
            Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Radius = radius, DelaySeconds = delay,
            DamagePerTick = damage, DamageType = DamageType.Explosive, Ticks = 1, Targets = targets,
        };

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        [Test]
        public void Strike_HitsOnlyEnemiesInsideTheRadius_AtItsDelay()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit inside = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 30f));
            SimUnit edge = sim.AddUnit(Dummy(), Team.B, new Vec2(5.4f, 30f));   // 5.4 m away, radius 0.5: its edge is inside 5 m.
            SimUnit outside = sim.AddUnit(Dummy(), Team.B, new Vec2(6f, 30f));  // Edge at 5.5 m: outside.
            SimUnit friend = sim.AddUnit(Dummy(), Team.A, new Vec2(-1f, 30f));  // The caster's own unit is never hit.
            sim.AddSpell(Strike(), Team.A, new Vec2(0f, 30f));

            RunSeconds(sim, 0.9f);
            Assert.AreEqual(1000f, inside.Health, "Nothing before the delay.");
            RunSeconds(sim, 0.2f);
            Assert.AreEqual(900f, inside.Health, 1e-3f);
            Assert.AreEqual(900f, edge.Health, 1e-3f, "Measured to the unit's edge, like weapon range.");
            Assert.AreEqual(1000f, outside.Health);
            Assert.AreEqual(1000f, friend.Health);
            RunSeconds(sim, 2f);
            Assert.AreEqual(900f, inside.Health, 1e-3f, "A strike hits once.");
        }

        [Test]
        public void Strike_UsesTheDamageTable_AndTheTargetFilter()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit heavy = sim.AddUnit(Dummy(armor: ArmorType.Heavy), Team.B, new Vec2(0f, 30f));
            SimUnit flyer = sim.AddUnit(Dummy(air: true), Team.B, new Vec2(1f, 30f));
            SpellStats ballistic = Strike(delay: 0f);
            ballistic.DamageType = DamageType.Ballistic;
            sim.AddSpell(ballistic, Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(1000f - 100f * 0.25f, heavy.Health, 1e-3f, "Ballistic does 0.25x to Heavy (test damage table).");
            Assert.AreEqual(1000f, flyer.Health, "A ground-only strike ignores air units.");
        }

        [Test]
        public void Zone_HitsEveryTick_ForItsDuration()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit target = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 30f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -40f)); // Someone on the caster's side, so the battle goes on.
            var storm = new SpellStats
            {
                Name = "Storm", Offensive = true, Effect = SpellEffect.Damage, Radius = 6f, DelaySeconds = 1f,
                DamagePerTick = 20f, DamageType = DamageType.Explosive, Ticks = 5, TickInterval = 1f, Targets = TargetFilter.Both,
            };
            SimSpell cast = sim.AddSpell(storm, Team.A, new Vec2(0f, 30f));
            int triggers = 0;
            sim.Events.SpellTriggered += s => triggers++;
            RunSeconds(sim, 3.05f);
            Assert.AreEqual(1000f - 3 * 20f, target.Health, 1e-3f, "Ticks at 1, 2 and 3 s.");
            RunSeconds(sim, 5f);
            Assert.AreEqual(1000f - 5 * 20f, target.Health, 1e-3f, "Exactly 5 ticks.");
            Assert.AreEqual(5, triggers);
            Assert.IsTrue(cast.IsDone);
            Assert.AreEqual(100f, cast.DamageDealt, 1e-3f);
        }

        [Test]
        public void SpellKills_AreCountedForTheSpell_AndShownInTheResult()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Dummy(health: 50f), Team.B, new Vec2(0f, 30f));
            sim.AddUnit(Dummy(health: 50f), Team.B, new Vec2(1.5f, 30f));
            SimUnit survivor = sim.AddUnit(Dummy(), Team.B, new Vec2(30f, 30f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -30f));
            SimSpell cast = sim.AddSpell(Strike(delay: 0f), Team.A, new Vec2(0f, 30f));
            BattleResult result = sim.Run();

            Assert.AreEqual(2, cast.Kills);
            Assert.IsTrue(sim.Units.Where(u => !u.IsAlive).All(u => u.KilledBySpell == cast && u.KilledById < 0));
            Assert.IsTrue(survivor.IsAlive);
            UnitTypeResult line = result.TeamA.Spells.Single();
            Assert.AreEqual("Strike", line.UnitName);
            Assert.AreEqual(2, line.Kills);
            Assert.AreEqual(100f, line.DamageDealt, 1e-3f);
            Assert.AreEqual(0, result.TeamA.Spells.Sum(s => s.Deployed), "Spells are not units.");
        }

        [Test]
        public void ShieldBuff_GivesTheCastersUnitsInRangeABonusShield()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit inside = sim.AddUnit(Dummy(health: 200f), Team.A, new Vec2(0f, -30f));
            SimUnit outside = sim.AddUnit(Dummy(health: 200f), Team.A, new Vec2(20f, -30f));
            SimUnit enemy = sim.AddUnit(Dummy(health: 200f), Team.B, new Vec2(1f, -30f));
            var dome = new SpellStats { Name = "Dome", Effect = SpellEffect.Buff, Radius = 6f, ShieldBonusShare = 0.5f };
            sim.AddSpell(dome, Team.A, new Vec2(0f, -30f));
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(100f, inside.Shield, 1e-3f, "Half its health as shield.");
            Assert.AreEqual(0f, outside.Shield);
            Assert.AreEqual(0f, enemy.Shield, "Never the enemy's units.");
        }

        [Test]
        public void DamageBuff_MultipliesTheUnitsWeaponDamage()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats gunner = TestUnits.Unit(name: "Gunner", speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 20f, interval: 1f));
            SimUnit shooter = sim.AddUnit(gunner, Team.A, new Vec2(0f, -5f));
            SimUnit target = sim.AddUnit(Dummy(health: 10000f), Team.B, new Vec2(0f, 5f));
            var rally = new SpellStats { Name = "Rally", Effect = SpellEffect.Buff, Radius = 5f, DamageMultiplier = 1.5f };
            sim.AddSpell(rally, Team.A, new Vec2(0f, -5f));
            RunSeconds(sim, 0.5f);
            Assert.AreEqual(1.5f, shooter.DamageMultiplier, 1e-5f);
            Assert.AreEqual(10000f - 15f, target.Health, 1e-3f, "The first shot already does 1.5x.");
        }

        [Test]
        public void DropSpell_LandsSquadsForTheCaster_ThatFightAndCountForBaseDamage()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats trooper = TestUnits.Unit(name: "Trooper", squadSize: 4, speed: 3f, weapon: TestUnits.Weapon(damage: 5f, range: 10f));
            sim.AddUnit(Dummy(health: 1e6f), Team.B, new Vec2(0f, 40f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -40f));
            var pods = new SpellStats { Name = "Pods", Offensive = true, Effect = SpellEffect.DropSquad, DelaySeconds = 0.5f, DropUnit = trooper, DropSquads = 2 };
            SimSpell cast = sim.AddSpell(pods, Team.A, new Vec2(0f, 20f));
            RunSeconds(sim, 0.4f);
            Assert.AreEqual(2, sim.Units.Count, "Nothing landed yet.");
            RunSeconds(sim, 0.2f);
            var landed = sim.Units.Where(u => u.Stats == trooper).ToList();
            Assert.AreEqual(8, landed.Count, "2 squads of 4.");
            Assert.IsTrue(landed.All(u => u.Team == Team.A));
            Assert.AreEqual(2, landed.Select(u => u.SquadId).Distinct().Count());
            Assert.GreaterOrEqual(cast.DroppedSquadId, 0);
            Assert.Less(landed.Max(u => Vec2.Distance(u.Position, new Vec2(0f, 20f))), 8f, "They land around the spot.");
            RunSeconds(sim, 3f);
            Assert.IsTrue(landed.Any(u => u.State == UnitState.Attack || u.State == UnitState.Move), "They fight.");
        }

        [Test]
        public void SpellsAreRepeatable_SameSeedSameResult()
        {
            string Run()
            {
                var sim = new BattleSimulation(TestRules.Default(), 7);
                UnitStats grunt = TestUnits.Unit(name: "Grunt", squadSize: 8, weapon: TestUnits.Weapon(damage: 6f, range: 8f));
                sim.AddSquad(grunt, Team.A, new Vec2(0f, -20f));
                sim.AddSquad(grunt, Team.B, new Vec2(0f, 20f));
                sim.AddSpell(Strike(damage: 40f, radius: 4f, delay: 2f), Team.A, new Vec2(0f, 14f));
                sim.AddSpell(new SpellStats { Name = "Dome", Effect = SpellEffect.Buff, Radius = 6f, ShieldBonusShare = 0.5f }, Team.B, new Vec2(0f, 20f));
                return sim.Run().ToSummaryLine();
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void SpellData_IsValidated()
        {
            Assert.IsEmpty(Strike().Validate());
            Assert.IsNotEmpty(new SpellStats { Name = "Bad buff", Effect = SpellEffect.Buff, Radius = 5f, Offensive = true, ShieldBonusShare = 0.5f }.Validate(), "Buffs are defensive.");
            Assert.IsNotEmpty(new SpellStats { Name = "No pods", Effect = SpellEffect.DropSquad }.Validate());
            Assert.IsNotEmpty(new SpellStats { Name = "Zone", Effect = SpellEffect.Damage, Radius = 5f, DamagePerTick = 5f, Ticks = 3 }.Validate(), "A zone needs a tick interval.");
        }
    }
}
