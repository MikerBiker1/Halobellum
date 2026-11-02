// EffectsTests.cs
// The battle effects (M10): each weapon gets a look and shows who fired it, each unit a kind of death, each spell a look of its own; the
// budget drops small things first; the shake adds up sensibly; and in the real Sandbox and Match scenes effects run in every kind of
// fight and between battles without an error, stay under their caps, freeze with the battle, and allocate nothing per shot.
// Depends on: Halobellum.Presentation, Halobellum.Combat, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Halobellum.Tests
{
    public class EffectsTests
    {
        private static WeaponStats Weapon(string name, float speed = 0f) => new WeaponStats { Name = name, ProjectileSpeed = speed, Range = 10f, FireInterval = 1f };

        private static WeaponLook LookFor(string name, float speed = 0f, UnitStats shooter = null, string faction = BattleFactions.Unsc, Team team = Team.A, bool mirror = false)
        {
            return WeaponLooks.For(Weapon(name, speed), shooter ?? new UnitStats { Name = "Test" }, faction, team, mirror);
        }

        [Test]
        public void EveryWeaponKindGetsAVisibleLook()
        {
            string[] names = { "MA5 Assault Rifle", "M41 Machine Gun", "M512 Cannon", "M68 Gauss Cannon", "Rockets", "Bombs", "M7057 Flamethrower", "Plasma Rifle", "Fuel Rod Gun", "Needle Cannons", "Spartan Laser", "Energy Sword", "Spat Bone" };
            foreach (string name in names)
            {
                WeaponLook look = LookFor(name, name.Contains("Cannon") || name.Contains("Rockets") ? 30f : 0f);
                Assert.Greater(look.FlashSize, 0f, name);
                Assert.Greater(look.ShotWidth, 0f, name);
                Assert.Greater(look.Shot.maxColorComponent, 0.5f, name + " must be bright enough to see.");
            }
        }

        [Test]
        public void ColourSaysWhoFired()
        {
            Assert.AreNotEqual(LookFor("MA5 Assault Rifle").Shot, LookFor("Plasma Rifle", faction: BattleFactions.Covenant).Shot, "UNSC and Covenant differ.");
            var flood = new UnitStats { Name = "Infected Phantom", IsFlood = true };
            Assert.AreNotEqual(LookFor("Plasma Turrets", shooter: flood, faction: BattleFactions.Flood).Shot, LookFor("Plasma Rifle", faction: BattleFactions.Covenant).Shot, "Flood plasma is its own colour.");
            Assert.AreNotEqual(LookFor("Fuel Rod Gun", 25f).Shot, LookFor("Plasma Mortar", 20f).Shot, "Fuel rods are green, mortars blue.");
        }

        [Test]
        public void InAMirror_TheSecondSidesShotsAreRedder_AndTheFirstSidesAreUnchanged()
        {
            WeaponLook plain = LookFor("MA5 Assault Rifle");
            WeaponLook first = LookFor("MA5 Assault Rifle", team: Team.A, mirror: true);
            WeaponLook second = LookFor("MA5 Assault Rifle", team: Team.B, mirror: true);
            Assert.AreEqual(plain.Shot, first.Shot);
            Assert.AreNotEqual(plain.Shot, second.Shot);
            Assert.Less(second.Shot.g, plain.Shot.g, "Less green: toward red.");
        }

        [Test]
        public void AnAutocannon_ThatHitsAtOnce_IsDrawnAsHeavyBullets()
        {
            Assert.AreEqual(WeaponFeelKind.HeavyBullet, LookFor("M370 Autocannon").Kind);
            Assert.AreEqual(WeaponFeelKind.Cannon, LookFor("M512 Cannon", 35f).Kind);
        }

        [Test]
        public void EachKindOfUnitDiesItsOwnWay()
        {
            Assert.AreEqual(DeathStyle.Infantry, DeathLooks.For(new UnitStats { ArmorType = ArmorType.Unarmored }));
            Assert.AreEqual(DeathStyle.Vehicle, DeathLooks.For(new UnitStats { ArmorType = ArmorType.Heavy }));
            Assert.AreEqual(DeathStyle.Air, DeathLooks.For(new UnitStats { ArmorType = ArmorType.Light, IsAir = true }));
            Assert.AreEqual(DeathStyle.FloodBurst, DeathLooks.For(new UnitStats { IsFlood = true, ArmorType = ArmorType.Heavy }));
            Assert.AreEqual(DeathStyle.FloodAirBurst, DeathLooks.For(new UnitStats { IsFlood = true, IsAir = true, CollisionRadius = 1f }));
            Assert.AreEqual(DeathStyle.FloodAir, DeathLooks.For(new UnitStats { IsFlood = true, IsAir = true, CollisionRadius = 3f }));
            Assert.AreEqual(DeathStyle.CrashBurst, DeathLooks.For(new UnitStats { IsFlood = true, IsAir = true, CollisionRadius = 2.5f, DeathBurst = new DeathBurstStats { Radius = 7f } }));
        }

        [Test]
        public void TheBigSpellsAllLookDifferent()
        {
            SpellLook[] looks =
            {
                SpellLooks.For(new SpellStats { Name = "MAC Strike", Radius = 4f }),
                SpellLooks.For(new SpellStats { Name = "HAVOK Nuke", Radius = 14f }),
                SpellLooks.For(new SpellStats { Name = "Plasma Bombardment", Radius = 5f, DamageType = DamageType.Energy }),
                SpellLooks.For(new SpellStats { Name = "Plasma Storm", Radius = 10f, Ticks = 8, DamageType = DamageType.Energy }),
                SpellLooks.For(new SpellStats { Name = "Glassing Beam", Radius = 12f, Ticks = 5, DamageType = DamageType.Energy }),
                SpellLooks.For(new SpellStats { Name = "Archer Missile Barrage", Radius = 10f, Ticks = 6 }),
                SpellLooks.For(new SpellStats { Name = "Gravemind's Wrath", Radius = 13f }),
            };
            Assert.AreEqual(looks.Length, looks.Distinct().Count());
            Assert.AreEqual(SpellLook.Smoke, SpellLooks.For(new SpellStats { Name = "Smoke Bomb", Effect = SpellEffect.Smoke }));
            Assert.AreEqual(SpellLook.Emp, SpellLooks.For(new SpellStats { Name = "Electromagnetic Pulse", Effect = SpellEffect.Disrupt }));
            Assert.AreEqual(SpellLook.SporeWave, SpellLooks.For(new SpellStats { Name = "Nerve Spores", Effect = SpellEffect.Disrupt }));
            Assert.AreEqual(SpellLook.Nuke, SpellLooks.For(new SpellStats { Name = "A New Bomb", Radius = 12f }), "A spell with an unknown name goes by what it does.");
        }

        [Test]
        public void TheBudgetDropsSmallThingsFirst()
        {
            var budget = new EffectBudget();
            budget.BeginFrame(1f / 120f);
            Assert.IsTrue(budget.Allow(EffectPriority.Small, 0.1f));
            Assert.IsFalse(budget.Allow(EffectPriority.Small, 0.7f), "A small effect stops at a fuller layer...");
            Assert.IsTrue(budget.Allow(EffectPriority.Medium, 0.7f), "...where a medium one still goes.");
            Assert.IsFalse(budget.Allow(EffectPriority.Medium, 0.9f));
            Assert.IsTrue(budget.Allow(EffectPriority.Big, 0.9f));
            Assert.IsFalse(budget.Allow(EffectPriority.Big, 0.99f));
            Assert.IsTrue(budget.Allow(EffectPriority.Huge, 0.99f), "Only a completely full layer stops a huge one.");
            Assert.AreEqual(1, budget.Dropped[(int)EffectPriority.Small]);
        }

        [Test]
        public void SlowFramesThinOutTheSmallThings_ThenItRecovers()
        {
            var budget = new EffectBudget();
            budget.BeginFrame(1f / 120f);
            Assert.AreEqual(0f, budget.Pressure, 1e-4f);
            float relaxed = budget.FillLimit(EffectPriority.Small);
            for (int i = 0; i < 60; i++) budget.BeginFrame(1f / 25f);
            Assert.Greater(budget.Pressure, 0.9f);
            Assert.Less(budget.FillLimit(EffectPriority.Small), relaxed * 0.5f);
            Assert.AreEqual(1f, budget.FillLimit(EffectPriority.Huge), "Huge effects are not touched.");
            for (int i = 0; i < 300; i++) budget.BeginFrame(1f / 120f);
            Assert.Less(budget.Pressure, 0.05f);
        }

        [Test]
        public void TheBudgetCapsSmallParticlesPerFrame()
        {
            var budget = new EffectBudget();
            budget.BeginFrame(1f / 120f);
            int allowed = 0;
            for (int i = 0; i < EffectBudget.SmallPerFrame + 50; i++) if (budget.Allow(EffectPriority.Small, 0f)) allowed++;
            Assert.AreEqual(EffectBudget.SmallPerFrame, allowed);
            budget.BeginFrame(1f / 120f);
            Assert.IsTrue(budget.Allow(EffectPriority.Small, 0f), "A new frame starts afresh.");
        }

        [Test]
        public void TheCameraShakeAddsUpButStaysSmall()
        {
            float one = CameraShake.AddTrauma(0f, 0.3f);
            Assert.AreEqual(0.3f, one, 1e-4f);
            Assert.Greater(CameraShake.AddTrauma(one, 0.3f), one, "Two blasts shake more than one.");
            Assert.AreEqual(1f, CameraShake.AddTrauma(1f, 1f), 1e-4f, "Never past the biggest shake.");
            Assert.Less(CameraShake.ShakeShare(0.3f), 0.002f, "A vehicle's blast is a small shake.");
            Assert.LessOrEqual(CameraShake.ShakeShare(1f), 0.02f, "Even the biggest moves the camera by a sliver of its distance.");
        }

        // ------------------------------------------------------------------ in the real scenes

        private SandboxController _sandbox;
        private BattleController _battle;
        private CombatEffectsView _view;

        private IEnumerator LoadSandbox()
        {
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            _sandbox = Object.FindAnyObjectByType<SandboxController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            _view = _battle.GetComponent<CombatEffectsView>();
            yield return null;
        }

        private UnitStats Unit(string name) => _sandbox.Units.Single(u => u.Stats.Name == name).Stats;

        private static readonly string[] AllKindsBlue = { "Marine", "Flamethrower Trooper", "Scorpion", "Gauss Warthog", "Wolverine", "Archer Missile Pod", "Hornet", "Pelican", "Spartan", "Warthog" };
        private static readonly string[] AllKindsRed = { "Grunt", "Fuel Rod Grunt", "Jackal (Plasma Rifle)", "Elite", "Wraith", "Vampire", "Banshee", "Hunter Pair", "Scarab", "Zealot", "Infection Forms", "Carrier Form", "Combat Form", "Pure Form Ranged", "Seeder", "Swarm Form", "Infected Pelican", "Infected Phantom", "Flood Abomination" };

        [UnityTest]
        public IEnumerator EveryKindOfFight_RunsWithoutAnError_AndStaysUnderItsCaps()
        {
            yield return LoadSandbox();
            foreach (string name in AllKindsBlue) _sandbox.AddSquad(Team.A, Unit(name));
            foreach (string name in AllKindsRed) _sandbox.AddSquad(Team.B, Unit(name));
            yield return null;
            _battle.Speed = 16f;
            _sandbox.Run();
            int peak = 0;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (!_battle.Simulation.IsFinished && Time.realtimeSinceStartup < deadline)
            {
                peak = Mathf.Max(peak, _view.Kit.LiveParticles);
                yield return null;
            }
            Assert.IsTrue(_battle.Simulation.IsFinished, "The battle ended.");
            Assert.Greater(peak, 0, "Something was drawn.");
            Assert.LessOrEqual(peak, 12000, "All the layers' caps together.");
            LogAssert.NoUnexpectedReceived();

            // And on to the next battle, twice, with everything cleared in between.
            _sandbox.ResetBattle();
            yield return null;
            Assert.AreEqual(0, _view.Kit.LiveParticles, "A new battle starts with a clean field.");
            Assert.AreEqual(0, _view.Deaths.CorpsesOnField);
            _sandbox.Run();
            for (int i = 0; i < 30; i++) yield return null;
            _sandbox.ResetBattle();
            yield return null;
            Assert.AreEqual(0, _view.Kit.LiveParticles);
        }

        [UnityTest]
        public IEnumerator EffectsFreezeWithTheBattle()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Grunt"));
            yield return null;
            _battle.Speed = 4f;
            _sandbox.Run();
            float deadline = Time.realtimeSinceStartup + 40f;
            while (_view.Kit.LiveParticles == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(_view.Kit.LiveParticles, 0, "They are fighting.");

            _battle.Speed = 0f;
            yield return null;
            float clock = _view.Kit.BattleTime;
            int live = _view.Kit.LiveParticles;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(clock, _view.Kit.BattleTime, "The effects' clock stands still.");
            Assert.AreEqual(live, _view.Kit.LiveParticles, "Nothing runs out while paused.");
        }

        [UnityTest]
        public IEnumerator AVehiclesWreckStaysAWhile_ThenGoes_AndAnAirUnitFallsToTheGround()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Warthog"));
            _sandbox.AddSquad(Team.A, Unit("Hornet"));
            _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Marine"));
            yield return null;
            _battle.Speed = 1f;
            _sandbox.Run();
            yield return null;
            UnitViewManager views = _battle.GetComponent<UnitViewManager>();
            SimUnit car = _battle.Simulation.Units.First(u => u.Stats.Name == "Warthog");
            SimUnit flier = _battle.Simulation.Units.First(u => u.Stats.Name == "Hornet");
            car.Health = 0f;
            flier.Health = 0f;
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((car.IsAlive || flier.IsAlive) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.IsFalse(car.IsAlive);
            Assert.IsTrue(views.IsShown(car), "Its wreck stays.");
            Assert.IsTrue(views.IsShown(flier), "It falls.");
            float startHeight = views.ShownPosition(flier).y;
            for (int i = 0; i < 20; i++) yield return null;
            if (views.IsShown(flier)) Assert.Less(views.ShownPosition(flier).y, startHeight, "Downward.");

            _battle.Speed = 16f;
            deadline = Time.realtimeSinceStartup + 20f;
            while ((views.IsShown(car) || views.IsShown(flier)) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(views.IsShown(flier), "It has crashed.");
            Assert.IsFalse(views.IsShown(car), "The wreck has gone.");
        }

        [UnityTest]
        public IEnumerator FiringAndBlastsAllocateNothing()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Grunt"));
            yield return null;
            var recipes = new EffectRecipes(_view.Kit);
            WeaponLook look = LookFor("MA5 Assault Rifle");
            Vector3 from = new Vector3(0f, 1f, 0f), to = new Vector3(5f, 1f, 5f);
            // The first call of anything may warm up an engine cache.
            recipes.Muzzle(from, Vector3.forward, look);
            recipes.InstantShot(from, to, look);
            recipes.Impact(to, Vector3.forward, ImpactStyle.Bullet, ArmorType.Heavy, false, look.Shot);
            recipes.Blast(to, 3f, BlastStyle.Fire, look.Shot, EffectPriority.Big);
            Assert.That(() =>
            {
                recipes.Muzzle(from, Vector3.forward, look);
                recipes.InstantShot(from, to, look);
                recipes.ImpactAfter(0.1f, to, Vector3.forward, ImpactStyle.Bullet, ArmorType.Heavy, false, look.Shot);
                recipes.Impact(to, Vector3.forward, ImpactStyle.Bullet, ArmorType.Heavy, false, look.Shot);
                recipes.Impact(to, Vector3.forward, ImpactStyle.Plasma, ArmorType.Light, false, look.Shot);
                recipes.Blast(to, 3f, BlastStyle.Fire, look.Shot, EffectPriority.Big);
                recipes.Blast(to, 3f, BlastStyle.Plasma, look.Shot, EffectPriority.Big);
                recipes.Blast(to, 3f, BlastStyle.Bio, look.Shot, EffectPriority.Big);
                recipes.Fireball(to, 4f, EffectPriority.Big);
                recipes.VehicleDeath(to, 2f, false);
                recipes.FloodBurst(to, 1f, false);
                recipes.RunDue();
            }, Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator SpellsDrawInASandboxFight_AndLeaveTheBattleItsOwnBusiness()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Marine"));
            yield return null;
            _battle.Speed = 8f;
            _sandbox.Run();
            string[] names = { "MAC Strike", "HAVOK Nuke", "Plasma Bombardment", "Plasma Storm", "Glassing Beam", "Archer Missile Barrage", "Gravemind's Wrath", "Spore Burst", "Blight Rain" };
            foreach (string name in names)
            {
                _battle.Simulation.AddSpell(new SpellStats { Name = name, Offensive = true, Radius = 8f, DamagePerTick = 1f, Ticks = 3, TickInterval = 0.5f, DelaySeconds = 3.5f, DamageType = DamageType.Energy }, Team.A, new Vec2(0f, 10f));
            }
            _battle.Simulation.AddSpell(new SpellStats { Name = "Smoke Bomb", Effect = SpellEffect.Smoke, AreaWidth = 15f, AreaDepth = 4f, RangeMultiplier = 0.5f, DelaySeconds = 0.5f }, Team.B, new Vec2(0f, 0f));
            _battle.Simulation.AddSpell(new SpellStats { Name = "Electromagnetic Pulse", Effect = SpellEffect.Disrupt, Radius = 10f, ShieldDamage = 1f, DelaySeconds = 1f }, Team.A, new Vec2(0f, 10f));
            _battle.Simulation.AddSpell(new SpellStats { Name = "Bubble Shield", Effect = SpellEffect.Buff, Radius = 10f, ShieldBonusShare = 0.5f, DelaySeconds = 1f }, Team.A, new Vec2(0f, -10f));
            float deadline = Time.realtimeSinceStartup + 30f;
            while (_battle.Simulation.ElapsedSeconds < 8f && !_battle.Simulation.IsFinished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(_view.Kit.PeakLive, 500, "Spells make a lot of particles.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
