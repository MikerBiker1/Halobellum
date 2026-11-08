// FloodSwarmSeederTests.cs
// D76 (the owner, 2026-11-06), with the game's own unit data: the Swarm Form's spat bone hits air only and it bites ground units in melee,
// going for air first because its spat bone reaches further; the Seeder shoots air too, and a unit that dies seeded rises as the Flood's own
// version of it (a Banshee as an Infested Banshee) only if a Flood roster has one: anything else just dies. D107 (the owner, 2026-11-07): the
// Swarm Form goes for air even with the ground right beside it, and the Pure Form Ranged shoots air, straight at it, for a quarter of the damage.
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
        public void TheSwarmForm_GoesForAirFirst_EvenWithGroundRightBesideIt()
        {
            // D107: in the owner's round 8 the Swarm Forms bit the Marauders beside them while Banshees flew over.
            Dictionary<string, UnitStats> u = AllStats();
            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            SimUnit swarm = sim.AddUnit(u["Swarm Form"], Team.A, new Vec2(0f, -10f));
            SimUnit banshee = sim.AddUnit(u["Banshee"], Team.B, new Vec2(0f, 25f));
            sim.AddUnit(u["Marine"], Team.B, new Vec2(0.8f, -9.2f)); // Within its bite's reach.
            sim.Step();
            Assert.AreSame(banshee, swarm.MoveTarget, "The flyer, though a Marine stands beside it.");
            Assert.AreEqual("Spat Bone", swarm.Weapons[swarm.MainWeaponIndex].Stats.Name);
        }

        [Test]
        public void ThePureFormRanged_ShootsAir_StraightAtIt_ForAQuarterOfTheDamage()
        {
            Dictionary<string, UnitStats> u = AllStats();
            UnitStats pure = u["Pure Form Ranged"].Clone();
            Assert.IsTrue(pure.CanTarget(true), "It shoots air (D107).");
            Assert.AreEqual(0.25f, pure.Weapons[0].AirDamageMultiplier, 1e-4f, "For a quarter of the damage: air is still what beats it.");
            Assert.IsTrue(pure.Weapons[0].Lobbed, "Lobbed at the ground, as before.");
            pure.MoveSpeed = 0f;

            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            sim.AddUnit(pure, Team.A, new Vec2(0f, -15f));
            UnitStats flyer = u["Banshee"].Clone();
            flyer.MoveSpeed = 0f; // Hovering out of its own reach, 25 m off.
            SimUnit banshee = sim.AddUnit(flyer, Team.B, new Vec2(0f, 10f));
            var shots = new List<ShotInFlight>();
            bool straight = false, arced = false;
            for (int i = 0; i < 30 * 10 && banshee.Health >= banshee.Stats.MaxHealth; i++)
            {
                sim.Step();
                sim.GetProjectiles(shots);
                foreach (ShotInFlight shot in shots)
                {
                    if (shot.AimedAt == banshee && shot.ArcLength <= 0f) straight = true;
                    if (shot.ArcLength > 0f) arced = true;
                }
            }
            Assert.IsTrue(straight, "Its shards fly straight at a flyer.");
            Assert.IsFalse(arced, "Not lobbed onto the ground under it.");
            Assert.Less(banshee.Health, banshee.Stats.MaxHealth, "And they hit.");
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
        public void TheSpawner_ReleasesInfectionFormsAndAnInfestedElite_InTurn()
        {
            // D79: Covenant-based Flood's Spawner (not in the slice units, so straight from its asset).
            UnitStats spawner = UnityEditor.AssetDatabase.LoadAssetAtPath<Halobellum.Units.UnitDefinition>("Assets/_Project/Data/Units/FloodCovenant/Spawner.asset").ToStats();
            Assert.AreEqual("Infection Forms", spawner.SpawnedUnit.Name);
            Assert.AreEqual("Infested Elite", spawner.AlternateSpawnedUnit.Name);
            Assert.AreEqual(1, spawner.AlternateSpawnedUnit.SquadSize);
            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            spawner.MoveSpeed = 0f;
            sim.AddUnit(spawner, Team.A, new Vec2(0f, -30f));
            UnitStats bystander = AllStats()["Marine"].Clone();
            bystander.MoveSpeed = 0f;
            sim.AddUnit(bystander, Team.B, new Vec2(70f, 75f));
            Steps(sim, spawner.SpawnIntervalSeconds + 0.1f);
            Assert.IsTrue(sim.Units.Any(x => x.Team == Team.A && x.Stats.Name == "Infection Forms"), "Infection Forms first.");
            Assert.IsFalse(sim.Units.Any(x => x.Stats.Name == "Infested Elite"));
            Steps(sim, spawner.SpawnIntervalSeconds);
            Assert.AreEqual(1, sim.Units.Count(x => x.Team == Team.A && x.Stats.Name == "Infested Elite"), "Then an Infested Elite.");
        }

        [Test]
        public void TheSpawnersSwarmBrood_HatchesOneSwarmForm_OnlyWithTheTech()
        {
            // D113, D114 (the owner, 2026-11-08): a tech that has the Spawner hatch one Swarm Form (a stack of five forms), once, early in each battle.
            UnitStats spawner = UnityEditor.AssetDatabase.LoadAssetAtPath<Halobellum.Units.UnitDefinition>("Assets/_Project/Data/Units/FloodCovenant/Spawner.asset").ToStats();
            TechUpgrade brood = spawner.TechUpgrades.Single(t => t.UnlocksReinforcement);
            Assert.AreEqual("Swarm Brood", brood.Name);
            Assert.IsFalse(spawner.HasReinforcement, "Nothing without the tech.");
            Assert.AreEqual("Swarm Form", spawner.LockedReinforcement.Name);
            Assert.AreEqual(1, spawner.LockedReinforcement.SquadSize, "One stack, not a squad of four.");
            foreach (bool withTech in new[] { false, true })
            {
                UnitStats stats = withTech ? UnitUpgrades.Apply(spawner, 1, new[] { brood }) : spawner.Clone();
                stats.MoveSpeed = 0f;
                var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
                sim.AddUnit(stats, Team.A, new Vec2(0f, -30f));
                UnitStats bystander = AllStats()["Marine"].Clone();
                bystander.MoveSpeed = 0f;
                sim.AddUnit(bystander, Team.B, new Vec2(70f, 75f));
                Steps(sim, spawner.ReinforcementDelaySeconds + 0.5f);
                Assert.AreEqual(withTech ? 1 : 0, sim.Units.Count(x => x.Team == Team.A && x.Stats.Name == "Swarm Form"), withTech ? "One Swarm Form with the tech." : "None without it.");
                Steps(sim, 10f);
                Assert.AreEqual(withTech ? 1 : 0, sim.Units.Count(x => x.Team == Team.A && x.Stats.Name == "Swarm Form"), "Once a battle.");
            }
        }

        [Test]
        public void TheCondorsWindow_ShowsItsMainGunAndItsLasersOnce()
        {
            // D114 (the owner, 2026-11-08): not every gun on its own line; the five light lasers fire one after another and a little quieter.
            UnitStats condor = UnityEditor.AssetDatabase.LoadAssetAtPath<Halobellum.Units.UnitDefinition>("Assets/_Project/Data/Units/AerospaceCommand/CondorGunship.asset").ToStats();
            var groups = Halobellum.UI.SquadStatsWindow.GroupWeapons(condor);
            Assert.AreEqual(2, groups.Count);
            Assert.IsTrue(groups[0].isMain);
            Assert.AreEqual(5, groups[1].count);
            Assert.AreEqual("Light Spartan Laser", Halobellum.UI.SquadStatsWindow.BaseName(groups[1].weapon.Name));
            CollectionAssert.AreEqual(new[] { 0f, 0.8f, 1.6f, 2.4f, 3.2f }, condor.Weapons.Skip(1).Select(w => (float)System.Math.Round(w.StaggerSeconds, 2)).ToArray());
            Assert.AreSame(Halobellum.Presentation.AudioCatalog.FireLightSpartanLaser, Halobellum.Presentation.AudioCatalog.WindUpFor(condor.Weapons[1]) ?? Halobellum.Presentation.AudioCatalog.FireLightSpartanLaser);
            Assert.Less(Halobellum.Presentation.AudioCatalog.FireLightSpartanLaser.Volume, Halobellum.Presentation.AudioCatalog.FireSpartanLaser.Volume);
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
