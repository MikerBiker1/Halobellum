// SpellSimulationTests.cs
// Spell effects inside a battle (Bible Section 10, Milestone 7): strikes and damage zones hit
// everyone inside the area, the caster's own units too (decision D33), at the right time, through the
// damage table and the target filter; buffs give the caster's units a bonus shield or more damage;
// drop spells land squads that fight for that battle; EMPs strip shields, disable techs and slow armor;
// smoke halves the range of units inside; spell damage and kills are counted in the result.
// Also the battle side of items and special units: ODSTs switching weapons by target, life steal,
// healing over time.
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
        public void Strike_HitsEveryoneInsideTheRadius_AtItsDelay_TheCastersUnitsToo()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit inside = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 30f));
            SimUnit edge = sim.AddUnit(Dummy(), Team.B, new Vec2(5.4f, 30f));   // 5.4 m away, radius 0.5: its edge is inside 5 m.
            SimUnit outside = sim.AddUnit(Dummy(), Team.B, new Vec2(6f, 30f));  // Edge at 5.5 m: outside.
            SimUnit friend = sim.AddUnit(Dummy(), Team.A, new Vec2(-1f, 30f));  // Offensive spells hurt the caster's own units too.
            sim.AddSpell(Strike(), Team.A, new Vec2(0f, 30f));

            RunSeconds(sim, 0.9f);
            Assert.AreEqual(1000f, inside.Health, "Nothing before the delay.");
            RunSeconds(sim, 0.2f);
            Assert.AreEqual(900f, inside.Health, 1e-3f);
            Assert.AreEqual(900f, edge.Health, 1e-3f, "Measured to the unit's edge, like weapon range.");
            Assert.AreEqual(1000f, outside.Health);
            Assert.AreEqual(900f, friend.Health, 1e-3f, "Friendly fire (decision D33).");
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
        public void DefensiveDamage_WouldNotHurtTheCaster()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit friend = sim.AddUnit(Dummy(), Team.A, new Vec2(0f, 30f));
            SimUnit enemy = sim.AddUnit(Dummy(), Team.B, new Vec2(1f, 30f));
            SpellStats ward = Strike(delay: 0f);
            ward.Offensive = false;
            sim.AddSpell(ward, Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(1000f, friend.Health);
            Assert.AreEqual(900f, enemy.Health, 1e-3f);
        }

        // ------------------------------------------------------------------ EMP

        private static SpellStats Emp() => new SpellStats
        {
            Name = "EMP", Offensive = true, Effect = SpellEffect.Disrupt, Radius = 10f, DelaySeconds = 0f,
            ShieldDamage = 10000f, TechDisableSeconds = 10f, SlowShare = 0.4f, SlowSeconds = 10f,
        };

        [Test]
        public void Emp_StripsShields_ButNotHealth()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats shielded = Dummy(health: 300f);
            shielded.MaxShield = 500f;
            SimUnit inside = sim.AddUnit(shielded, Team.B, new Vec2(0f, 30f));
            SimUnit outside = sim.AddUnit(shielded, Team.B, new Vec2(30f, 30f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -40f));
            sim.AddSpell(Emp(), Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(0f, inside.Shield);
            Assert.AreEqual(300f, inside.Health, "Only shields.");
            Assert.AreEqual(500f, outside.Shield);
        }

        [Test]
        public void Emp_SlowsArmoredUnits_For10Seconds_NotUnarmoredOnes()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats tank = TestUnits.Unit(name: "Tank", armor: ArmorType.Heavy, speed: 5f, filter: TargetFilter.Air);
            UnitStats infantry = TestUnits.Unit(name: "Infantry", armor: ArmorType.Unarmored, speed: 5f, filter: TargetFilter.Air);
            SimUnit armored = sim.AddUnit(tank, Team.B, new Vec2(0f, 30f));
            SimUnit soft = sim.AddUnit(infantry, Team.B, new Vec2(3f, 30f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -55f));
            sim.AddSpell(Emp(), Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(3f, armored.MoveSpeedNow, 1e-3f, "40% slower.");
            Assert.AreEqual(5f, soft.MoveSpeedNow, 1e-3f);
            RunSeconds(sim, 10f);
            Assert.AreEqual(5f, armored.MoveSpeedNow, 1e-3f, "Back to normal after 10 s.");
        }

        [Test]
        public void Emp_DisablesTechsForAWhile_LevelsStay()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats basic = TestUnits.Unit(name: "Gunner", health: 100f, speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 10f));
            var tech = new TechUpgrade { Name = "Big Guns", DamageMultiplier = 2f, RangeBonus = 20f };
            UnitStats upgraded = UnitUpgrades.Apply(basic, 2, new[] { tech });
            SimUnit gunner = sim.AddUnit(upgraded, Team.B, new Vec2(0f, 30f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(40f, -55f));
            Assert.AreEqual(40f, gunner.Weapons[0].Stats.DamagePerShot, 1e-3f, "Level 2 x tech 2.");
            sim.AddSpell(Emp(), Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(20f, gunner.Weapons[0].Stats.DamagePerShot, 1e-3f, "Tech off, level still counts.");
            Assert.AreEqual(10f, gunner.Weapons[0].Stats.Range, 1e-3f);
            RunSeconds(sim, 10f);
            Assert.AreEqual(40f, gunner.Weapons[0].Stats.DamagePerShot, 1e-3f, "The tech comes back.");
            Assert.AreEqual(30f, gunner.Weapons[0].Stats.Range, 1e-3f);
        }

        // ------------------------------------------------------------------ smoke

        private static SpellStats Smoke() => new SpellStats
        {
            Name = "Smoke", Offensive = true, Effect = SpellEffect.Smoke, AreaWidth = 15f, AreaDepth = 4f,
            DelaySeconds = 0f, DurationSeconds = 20f, RangeMultiplier = 0.5f,
        };

        [Test]
        public void Smoke_HalvesTheRangeOfUnitsInside_InAStraightLine_WhileItLasts()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit inLine = sim.AddUnit(Dummy(), Team.B, new Vec2(7f, 30f));    // Within 7.5 m across.
            SimUnit past = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 33f));      // 3 m along: outside the 2 m half depth + 0.5 body.
            SimUnit friend = sim.AddUnit(Dummy(), Team.A, new Vec2(-7f, 30f));   // It affects both sides.
            sim.AddSpell(Smoke(), Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(0.5f, inLine.RangeMultiplier, 1e-5f);
            Assert.AreEqual(1f, past.RangeMultiplier);
            Assert.AreEqual(0.5f, friend.RangeMultiplier, 1e-5f, "Smoke blinds everyone inside.");
            RunSeconds(sim, 20f);
            Assert.AreEqual(1f, inLine.RangeMultiplier, "It clears.");
        }

        [Test]
        public void Smoke_WithoutADuration_LastsTheWholeBattle()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit inside = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 30f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -40f));
            SpellStats smoke = Smoke();
            smoke.DurationSeconds = 0f;
            SimSpell cast = sim.AddSpell(smoke, Team.A, new Vec2(0f, 30f));
            RunSeconds(sim, 80f);
            Assert.AreEqual(0.5f, inside.RangeMultiplier, 1e-5f, "Still there 80 s in.");
            Assert.IsFalse(cast.IsOverAt(sim.ElapsedSeconds));
            CollectionAssert.IsEmpty(smoke.Validate());
        }

        [Test]
        public void Smoke_AtRightAngles_IsALineAlongTheField()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit across = sim.AddUnit(Dummy(), Team.B, new Vec2(7f, 30f));
            SimUnit along = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 37f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -40f));
            sim.AddSpell(Smoke(), Team.A, new Vec2(0f, 30f), angleDegrees: 90f);
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(1f, across.RangeMultiplier);
            Assert.AreEqual(0.5f, along.RangeMultiplier, 1e-5f);
        }

        [Test]
        public void ALine_RunsFromItsFirstEnd_TowardTheMouse_AtAnyAngle()
        {
            SpellStats smoke = Smoke();
            Vec2 start = new Vec2(0f, 30f);
            Vec2 centre = smoke.LineFrom(start, start + new Vec2(100f, 100f), out float angle);
            Assert.AreEqual(45f, angle, 1e-3f);
            Assert.AreEqual(7.5f, Vec2.Distance(start, centre), 1e-3f, "The centre is half its length from the first end, however far the mouse is.");

            var sim = new BattleSimulation(TestRules.Default(), 1);
            Vec2 way = new Vec2(0.7071f, 0.7071f);
            SimUnit nearEnd = sim.AddUnit(Dummy(), Team.B, start + way * 1f);
            SimUnit farEnd = sim.AddUnit(Dummy(), Team.B, start + way * 14f);
            SimUnit beyond = sim.AddUnit(Dummy(), Team.B, start + way * 16f);
            SimUnit beside = sim.AddUnit(Dummy(), Team.B, centre + new Vec2(-way.Y, way.X) * 3f);
            sim.AddUnit(Dummy(), Team.A, new Vec2(0f, -40f));
            sim.AddSpell(smoke, Team.A, centre, angle);
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(0.5f, nearEnd.RangeMultiplier, 1e-5f);
            Assert.AreEqual(0.5f, farEnd.RangeMultiplier, 1e-5f);
            Assert.AreEqual(1f, beyond.RangeMultiplier, "Past the far end.");
            Assert.AreEqual(1f, beside.RangeMultiplier, "Beside the line.");
        }

        [Test]
        public void Smoke_MakesAUnitWalkCloserBeforeItCanShoot()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats gunner = TestUnits.Unit(name: "Gunner", speed: 3f, weapon: TestUnits.Weapon(damage: 1f, range: 10f));
            SimUnit shooter = sim.AddUnit(gunner, Team.A, new Vec2(0f, 0f));
            sim.AddUnit(Dummy(health: 1e6f), Team.B, new Vec2(0f, 9f)); // In range: 8.5 m to its edge.
            var cloud = Smoke();
            cloud.AreaWidth = 8f;
            cloud.AreaDepth = 8f;
            sim.AddSpell(cloud, Team.B, new Vec2(0f, 0f));
            RunSeconds(sim, 0.2f);
            Assert.AreEqual(UnitState.Move, shooter.State, "Range 5 m in the smoke: too far now.");
        }

        // ------------------------------------------------------------------ ODST weapons

        private static UnitStats Odst() => new UnitStats
        {
            Name = "ODST", Role = UnitRole.SingleTargetDps, Tier = 2, SquadSize = 1, Cost = 300, BaseDamage = 1f, MaxHealth = 100f,
            ArmorType = ArmorType.Unarmored, MoveSpeed = 3f, CollisionRadius = 0.5f, TargetFilter = TargetFilter.Both,
            Weapons = new[]
            {
                new WeaponStats { Name = "SMG", DamagePerShot = 5f, FireInterval = 0.2f, Range = 9f, DamageType = DamageType.Ballistic, TargetClass = WeaponTargetClass.ChaffOrUnarmored },
                new WeaponStats { Name = "Rockets", DamagePerShot = 60f, FireInterval = 2f, Range = 14f, DamageType = DamageType.Explosive, IsAntiArmor = true, TargetClass = WeaponTargetClass.Tougher },
            },
        };

        [TestCase(ArmorType.Unarmored, 0)]
        [TestCase(ArmorType.Light, 1)]
        [TestCase(ArmorType.Heavy, 1)]
        public void Odst_UsesTheWeaponThatFitsItsTarget(ArmorType armor, int expectedWeapon)
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit odst = sim.AddUnit(Odst(), Team.A, new Vec2(0f, 0f));
            UnitStats enemy = Dummy(health: 1e6f, armor: armor);
            enemy.Role = UnitRole.Tank; // Test units are chaff by default.
            sim.AddUnit(enemy, Team.B, new Vec2(0f, 8f));
            int? fired = null;
            sim.Events.WeaponFired += (a, w, t) => { if (a == odst) fired = w; };
            RunSeconds(sim, 1f);
            Assert.AreEqual(expectedWeapon, odst.MainWeaponIndex);
            Assert.AreEqual(expectedWeapon, fired, "It fires that weapon.");
        }

        [Test]
        public void Odst_ChaffCountsAsLight_EvenWithArmor()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit odst = sim.AddUnit(Odst(), Team.A, new Vec2(0f, 0f));
            UnitStats chaff = Dummy(health: 1e6f, armor: ArmorType.Light);
            chaff.Role = UnitRole.Chaff;
            sim.AddUnit(chaff, Team.B, new Vec2(0f, 8f));
            RunSeconds(sim, 1f);
            Assert.AreEqual(0, odst.MainWeaponIndex, "The SMG, for chaff.");
        }

        [Test]
        public void Odst_SwitchesToTheClosestEnemysWeapon_AndWalksInItsRange()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit odst = sim.AddUnit(Odst(), Team.A, new Vec2(0f, 0f));
            UnitStats tank = Dummy(health: 1e6f, armor: ArmorType.Heavy);
            tank.Role = UnitRole.Tank;
            sim.AddUnit(tank, Team.B, new Vec2(0f, 12f));
            SimUnit soft = sim.AddUnit(Dummy(health: 20f), Team.B, new Vec2(0f, 8f));
            RunSeconds(sim, 0.5f);
            Assert.AreEqual(0, odst.MainWeaponIndex, "The soft enemy is closest: SMG.");
            Assert.IsNull(odst.Weapons[1].Target, "One weapon at a time.");
            RunSeconds(sim, 3f);
            Assert.IsFalse(soft.IsAlive);
            RunSeconds(sim, 0.6f);
            Assert.AreEqual(1, odst.MainWeaponIndex, "Only armor is left: rockets.");
        }

        // ------------------------------------------------------------------ items in battle

        [Test]
        public void LifeSteal_HealsTheAttacker_UpToItsMaximum()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats basic = TestUnits.Unit(name: "Leech", health: 100f, speed: 0f, weapon: TestUnits.Weapon(damage: 20f, range: 10f));
            UnitStats leech = UnitUpgrades.Apply(basic, 1, null, new ItemStats { Name = "Absorption", LifestealShare = 0.5f }, null);
            SimUnit unit = sim.AddUnit(leech, Team.A, new Vec2(0f, 0f));
            sim.AddUnit(Dummy(health: 1e6f), Team.B, new Vec2(0f, 5f));
            unit.Health = 50f;
            RunSeconds(sim, 0.1f);
            Assert.AreEqual(60f, unit.Health, 1e-3f, "Half of the 20 damage dealt.");
            RunSeconds(sim, 10f);
            Assert.AreEqual(100f, unit.Health, 1e-3f, "Never above its maximum.");
        }

        [Test]
        public void HealingItem_RecoversASharePerSecond_ForTheWholeBody()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats basic = Dummy(health: 1000f);
            UnitStats medic = UnitUpgrades.Apply(basic, 1, null, new ItemStats { Name = "Stims", HealPerSecondShare = 0.045f }, null);
            SimUnit unit = sim.AddUnit(medic, Team.A, new Vec2(0f, 0f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(30f, 30f));
            unit.Health = 500f;
            RunSeconds(sim, 2f);
            Assert.AreEqual(590f, unit.Health, 1.5f, "4.5% of 1000 per second.");
        }

        [Test]
        public void ItemAndBuffs_ChangeTheStats_RangeBonusSkipsMelee()
        {
            UnitStats basic = TestUnits.Unit(name: "Basic", health: 100f, speed: 4f, weapon: TestUnits.Weapon(damage: 10f, range: 12f));
            var item = new ItemStats { Name = "Haste", MoveSpeedMultiplier = 1.25f, DamageMultiplier = 1.4f, HealthMultiplier = 1.5f, RangeBonus = 3f };
            var buffs = new ArmyBuffs { DamageMultiplier = 1.3f, HealthMultiplier = 1.3f, RangeBonus = 2f };
            UnitStats upgraded = UnitUpgrades.Apply(basic, 2, null, item, buffs);
            Assert.AreEqual(100f * 2f * 1.5f * 1.3f, upgraded.MaxHealth, 1e-2f);
            Assert.AreEqual(10f * 2f * 1.4f * 1.3f, upgraded.Weapons[0].DamagePerShot, 1e-2f);
            Assert.AreEqual(12f + 3f + 2f, upgraded.Weapons[0].Range, 1e-3f);
            Assert.AreEqual(5f, upgraded.MoveSpeed, 1e-3f);
            Assert.AreEqual(100f, basic.MaxHealth, "The unit type's own stats never change.");

            UnitStats melee = TestUnits.Unit(name: "Sword", weapon: TestUnits.Weapon(damage: 10f, range: 2f));
            Assert.AreEqual(2f, UnitUpgrades.Apply(melee, 1, null, null, buffs).Weapons[0].Range, 1e-3f, "Melee isn't ranged.");
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
