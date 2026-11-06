// FloodSwarmSeederTests.cs
// D76 (the owner, 2026-11-06), with the game's own unit data: the Swarm Form's spat bone hits air only and it bites ground units in melee,
// going for air first because its spat bone reaches further; the Seeder shoots air too, and a unit that dies seeded rises as the Flood's own
// version of it (a Banshee as an Infested Banshee) only if a Flood roster has one: anything else just dies.
// Depends on: Halobellum.Combat, Halobellum.Core, Halobellum.EditorTools, NUnit.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.EditorTools;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class FloodSwarmSeederTests
    {
        private static Dictionary<string, UnitStats> AllStats() =>
            HeadlessRunnerTool.LoadSliceUnits().ToDictionary(u => u.unitName, u => u.ToStats());

        private static void Steps(BattleSimulation sim, float seconds)
        {
            int ticks = (int)(seconds * 30f);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        [Test]
        public void TheSwarmForm_BitesGroundUnits_InMelee()
        {
            Dictionary<string, UnitStats> u = AllStats();
            UnitStats swarm = u["Swarm Form"];
            Assert.IsTrue(swarm.SwitchesWeapons, "Its weapons split air from ground.");
            WeaponStats bite = swarm.Weapons.Single(w => w.Name == "Bone Barbs");
            Assert.AreEqual(DamageType.Melee, bite.DamageType);
            Assert.IsFalse(bite.CanTarget(swarm, true), "The bite is for the ground.");
            Assert.IsFalse(swarm.Weapons.Single(w => w.Name == "Spat Bone").CanTarget(swarm, false), "The spat bone is for the air.");

            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            sim.AddSquad(swarm, Team.A, new Vec2(0f, -10f));
            SimUnit marine = sim.AddUnit(u["Marine"], Team.B, new Vec2(0f, 10f));
            Steps(sim, 20f);
            Assert.Less(marine.Health, marine.Stats.MaxHealth, "Ground units get bitten.");
        }

        [Test]
        public void TheSwarmForm_GoesForAirFirst()
        {
            Dictionary<string, UnitStats> u = AllStats();
            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            SimUnit swarm = sim.AddUnit(u["Swarm Form"], Team.A, new Vec2(0f, -10f));
            SimUnit banshee = sim.AddUnit(u["Banshee"], Team.B, new Vec2(0f, 25f));
            sim.AddUnit(u["Marine"], Team.B, new Vec2(0f, 0f)); // Nearer, on the ground.
            sim.Step();
            Assert.AreSame(banshee, swarm.MoveTarget, "The flyer, which its spat bone reaches from far off.");
        }

        [Test]
        public void TheSeeder_ShootsAir_AndASeededBanshee_RisesAsAnInfestedBanshee()
        {
            Dictionary<string, UnitStats> u = AllStats();
            UnitStats seeder = u["Seeder"];
            Assert.IsTrue(seeder.CanTarget(true), "It shoots air.");
            Assert.IsTrue(seeder.SeedForms.Any(f => f.From == "Banshee" && f.Becomes.Name == "Infested Banshee"));

            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            sim.AddSquad(seeder, Team.A, new Vec2(0f, -12f));
            SimUnit banshee = sim.AddUnit(u["Banshee"], Team.B, new Vec2(0f, 12f));
            UnitStats bystander = u["Marine"].Clone(); // Far off and still, so the battle goes on after the Banshee falls.
            bystander.MoveSpeed = 0f;
            sim.AddUnit(bystander, Team.B, new Vec2(60f, 75f));
            bool seeded = false;
            for (int i = 0; i < 30 * 40 && banshee.IsAlive; i++)
            {
                sim.Step();
                seeded |= banshee.IsSeeded;
            }
            Assert.IsTrue(seeded, "The Seeder's spores reach it in the air.");
            Assert.IsFalse(banshee.IsAlive);
            Steps(sim, 3f); // The rise takes a moment.
            SimUnit risen = sim.Units.FirstOrDefault(x => x.Team == Team.A && x.Stats.Name == "Infested Banshee");
            Assert.IsNotNull(risen, "It rose as the Flood's own Banshee.");
            Assert.AreEqual(risen.Stats.MaxHealth, seeder.SeedForms.First(f => f.From == "Banshee").Becomes.MaxHealth * 0.6f, 1f, "With the infected share of its health.");
        }

        [Test]
        public void ASeededUnit_TheFloodHasNoFormOf_DoesNotRise()
        {
            Dictionary<string, UnitStats> u = AllStats();
            Assert.IsFalse(u["Seeder"].SeedForms.Any(f => f.From == "Jackal (Beam Rifle)"));
            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            sim.AddSquad(u["Seeder"], Team.A, new Vec2(0f, -12f));
            sim.AddSquad(u["Jackal (Beam Rifle)"], Team.B, new Vec2(0f, 12f));
            Steps(sim, 40f);
            Assert.IsFalse(sim.Units.Any(x => x.Team == Team.A && x.Stats.Name.Contains("Jackal")), "No infected Jackal: the Flood has no form of it.");
            Assert.IsTrue(sim.Units.Where(x => x.Team == Team.B).All(x => !x.IsAlive) || sim.IsFinished);
        }
    }
}
