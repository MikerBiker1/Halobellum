// EffectsTests.cs
// The battle effects (M10): each weapon gets a look and shows who fired it, each unit a kind of death, each spell a look of its own; the
// budget drops small things first; the shake adds up sensibly; the Infected Pelican's crash is half what it was but still fills what it
// damages; and in the real Sandbox and Match scenes effects run in every kind of
// fight and between battles without an error, stay under their caps, freeze with the battle, and allocate nothing per shot.
// Depends on: Halobellum.Presentation, Halobellum.Combat, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
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
        public void FloodBombs_AreSickGreen_AndBurstInSpores_OthersStayFire()
        {
            var flood = new UnitStats { Name = "Infested Hornet", IsFlood = true };
            WeaponLook spores = LookFor("Spore Bombs", 20f, shooter: flood, faction: BattleFactions.Flood);
            Assert.AreEqual(WeaponFeelKind.Bomb, spores.Kind);
            Assert.AreEqual(WeaponLooks.FloodPlasma, spores.Shot);
            Assert.AreEqual(BlastStyle.Bio, spores.Blast);
            Assert.AreEqual(ImpactStyle.Bio, spores.Impact);
            Assert.AreEqual(WeaponLooks.RocketOrange, LookFor("Bombs", 20f).Shot, "A UNSC bomb is unchanged.");
            Assert.AreEqual(BlastStyle.Fire, LookFor("Bombs", 20f).Blast);
        }

        [TestCase(30f, 1f)]
        [TestCase(60f, 1f)]
        [TestCase(165f, 1f)]
        [TestCase(630f, 1f)]
        [TestCase(60f, 2f)]
        [TestCase(60f, 4f)]
        [TestCase(165f, 4f)]
        public void Dust_LooksAtEachBodyAsOftenAtAnyFrameRateAndSpeed(float framesPerSecond, float speed)
        {
            const int bodies = 192;
            const float battleSeconds = 20f;
            float delta = speed / framesPerSecond, due = 0f;
            int looks = 0;
            for (int frame = 0; frame < battleSeconds / delta; frame++) looks += CombatEffectsView.DustLooksThisFrame(delta, bodies, ref due);
            float perBodyPerSecond = looks / (float)bodies / battleSeconds;
            Assert.AreEqual(1f / CombatEffectsView.DustLookSeconds, perBodyPerSecond, 0.1f);
        }

        [Test]
        public void Dust_IsNotAskedForWhileNoTimePasses()
        {
            // An Esc pause is Time.timeScale 0, so Time.deltaTime is 0 and this is what the dust is told.
            float due = 0f;
            for (int i = 0; i < 1000; i++) Assert.AreEqual(0, CombatEffectsView.DustLooksThisFrame(0f, 192, ref due));
            Assert.AreEqual(0f, due);
        }

        [TestCase(30f, 1f)]
        [TestCase(60f, 1f)]
        [TestCase(165f, 1f)]
        [TestCase(60f, 4f)]
        [TestCase(20f, 4f)]
        public void Trails_AreSpacedByDistance_AtAnyFrameRateAndSpeed(float framesPerSecond, float speed)
        {
            const float metresPerSecond = 45f, spacing = 2f;
            Vector3 lastPuff = Vector3.zero;
            int puffs = 0;
            float frame = speed / framesPerSecond;
            float flown = 0f;
            while (flown < 90f)
            {
                flown += metresPerSecond * frame;
                Vector3 now = new Vector3(0f, 0f, flown);
                int due = CombatEffectsView.TrailPuffsDue(lastPuff, now, spacing, out Vector3 step);
                if (due > 0) Assert.AreEqual(spacing, step.magnitude, 0.001f, "Each puff is this far from the last.");
                puffs += due;
                lastPuff += step * due;
            }
            Assert.AreEqual(flown / spacing, puffs, 1.5f, "As many puffs as the distance asks for, whatever the frame rate.");
        }

        [Test]
        public void AHitchInATrail_LaysOnlyAFewPuffs()
        {
            int due = CombatEffectsView.TrailPuffsDue(Vector3.zero, Vector3.forward * 500f, 1f, out Vector3 _);
            Assert.AreEqual(CombatEffectsView.MostTrailPuffsAtOnce, due);
        }

        [Test]
        public void TheNukesCloud_StandsOnAStem_WithACapFarWiderThanIt()
        {
            const float radius = 14f;
            foreach (float age in new[] { 1.5f, 3f, 5f })
            {
                float stemWidest = 0f, capWidest = 0f, capLowest = float.MaxValue, top = 0f;
                int stemAt = MushroomCloud.StemPuffs + MushroomCloud.SkirtPuffs;
                for (int i = 0; i < MushroomCloud.SmokePuffs; i++)
                {
                    MushroomCloud.Puff puff = MushroomCloud.Smoke(i, age, radius, 3);
                    if (i >= MushroomCloud.StemPuffs && i < stemAt) continue; // The dust around its foot.
                    float across = Mathf.Abs(puff.Offset.x);
                    top = Mathf.Max(top, puff.Offset.y);
                    if (i < MushroomCloud.StemPuffs)
                    {
                        // Its foot flares, and its top opens into the cap: the middle of it is the stem.
                        if (puff.Offset.y > 0.25f * top && puff.Offset.y < 0.6f * top) stemWidest = Mathf.Max(stemWidest, across);
                    }
                    else
                    {
                        capWidest = Mathf.Max(capWidest, across);
                        capLowest = Mathf.Min(capLowest, puff.Offset.y);
                    }
                }
                Assert.Greater(capWidest, stemWidest * 2.5f, $"At {age} s the cap is much wider than the stem.");
                Assert.Greater(capLowest, 1.5f * radius, $"At {age} s there is clear stem under the cap.");
                Assert.Greater(top, 2.8f * radius, $"At {age} s it stands tall.");
                Assert.Greater(capWidest, 1.1f * radius, $"At {age} s the cap is wide.");
            }
            Assert.Greater(MushroomCloud.CapHeight(3f, radius), MushroomCloud.CapHeight(1.5f, radius), "It rises.");
            Assert.Greater(MushroomCloud.RimRadius(3f, radius), MushroomCloud.RimRadius(1.5f, radius), "It spreads.");
        }

        [Test]
        public void TheNukesCap_Rolls_AndTheCloudGoesAway()
        {
            const float radius = 14f;
            int rimStart = MushroomCloud.StemPuffs + MushroomCloud.SkirtPuffs;
            int rolledOutward = 0, onTop = 0;
            for (int i = rimStart; i < rimStart + MushroomCloud.RimAround * MushroomCloud.RimAcross; i++)
            {
                MushroomCloud.Puff now = MushroomCloud.Smoke(i, 4f, radius, 3), soon = MushroomCloud.Smoke(i, 4.1f, radius, 3);
                if (now.Offset.y <= MushroomCloud.CapHeight(4f, radius)) continue;
                onTop++;
                // What is on top of the rim, inside its middle, goes outward; what is outside goes down.
                float across = new Vector2(now.Offset.x, now.Offset.z / MushroomCloud.CapDepthShare).magnitude;
                float acrossSoon = new Vector2(soon.Offset.x, soon.Offset.z / MushroomCloud.CapDepthShare).magnitude;
                if (across < MushroomCloud.RimRadius(4f, radius) && acrossSoon > across) rolledOutward++;
                if (across >= MushroomCloud.RimRadius(4f, radius) && soon.Offset.y < now.Offset.y) rolledOutward++;
            }
            Assert.Greater(onTop, 10);
            Assert.Greater(rolledOutward, onTop * 0.75f, "The top of the rim rolls over outwards.");

            for (int i = 0; i < MushroomCloud.SmokePuffs; i++) Assert.Less(MushroomCloud.Smoke(i, MushroomCloud.Seconds, radius, 3).Color.a, 0.01f, "All the smoke has gone.");
            for (int i = 0; i < MushroomCloud.FirePuffs; i++) Assert.Less(MushroomCloud.Fire(i, 3.1f, radius, 3).Color.a, 0.01f, "The fire has gone out long before.");
            Assert.LessOrEqual(MushroomCloud.Seconds, 9f, "The battle isn't hidden for long.");
            Assert.LessOrEqual(MushroomCloud.SmokePuffs * 2, EffectKit.CloudSmokeCapacity, "Two nukes at once fit.");
            Assert.LessOrEqual(MushroomCloud.FirePuffs * 2, EffectKit.CloudFireCapacity, "Two nukes at once fit.");
        }

        [Test]
        public void HudLine_IsRebuiltOnlyWhenItsNumbersChange()
        {
            var line = new Halobellum.UI.HudLine();
            Assert.IsTrue(line.Changed(0L), "Never built yet, even for a zero.");
            Assert.IsFalse(line.Changed(0L));
            Assert.IsTrue(line.Changed(1L, 2L));
            Assert.IsFalse(line.Changed(1L, 2L));
            Assert.IsTrue(line.Changed(1L, 2L, 3L, 4L));
            Assert.AreEqual(line.Changed(1L, 2L, 3L, 4L), false);
            Assert.AreEqual(3L, Halobellum.UI.HudLine.Whole(2.5f), "As the text rounds it.");
            Assert.AreEqual(25L, Halobellum.UI.HudLine.Tenths(2.5f));
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

        /// <summary>All the particles of one kind that an effect has made so far.</summary>
        private static ParticleSystem.Particle[] ParticlesOf(GameObject host, string layer)
        {
            ParticleSystem system = host.GetComponentsInChildren<ParticleSystem>(true).First(s => s.name == layer);
            var particles = new ParticleSystem.Particle[system.particleCount];
            system.GetParticles(particles);
            return particles;
        }

        [Test]
        public void ThePelicansCrash_IsHalfWhatItWas_AndStillFillsWhatItDamages()
        {
            // The Infected Pelican's own numbers.
            const float burstRadius = 3.5f, bodyRadius = 2.5f;
            var host = new GameObject("Crash");
            var kit = new EffectKit(host.transform);
            new EffectRecipes(kit).PelicanCrash(Vector3.zero, bodyRadius, burstRadius);

            float Largest(string layer) => ParticlesOf(host, layer).Max(p => p.startSize);
            float Fastest(string layer) => ParticlesOf(host, layer).Max(p => p.velocity.magnitude);
            try
            {
                // What each was before it was cut, in metres and metres a second, and half of it is the most it may be now.
                const float half = 0.5f;
                Assert.LessOrEqual(Largest("Glow"), 71.4f * half + 0.01f, "The flash.");
                Assert.LessOrEqual(Largest("Ground Glow"), 71.4f * half + 0.01f, "The light on the ground.");
                Assert.LessOrEqual(Largest("Shock Ring"), 42.84f * half + 0.01f, "The outer ring.");
                Assert.LessOrEqual(Largest("Scorch"), 30.94f * half + 0.01f, "The burn on the ground.");
                Assert.LessOrEqual(Largest("Fire"), 11.31f * half + 0.01f, "A lick of fire.");
                Assert.LessOrEqual(Fastest("Fire"), 55f * half, "How far the fire flies.");
                Assert.LessOrEqual(Largest("Smoke"), 16.66f * half + 0.01f, "A puff of smoke.");
                Assert.LessOrEqual(Fastest("Sparks"), 101.2f * half, "How far the sparks fly.");
                Assert.LessOrEqual(Fastest("Chunks"), 44.8f * half, "How far the pieces fly.");
                Assert.LessOrEqual(kit.LiveParticles, 402 / 2, "It was 402 particles at once.");

                Assert.AreEqual(1, ParticlesOf(host, "Shock Ring").Count(p => Mathf.Approximately(p.startSize, burstRadius * 2f)), "One ring shows the burst's true reach.");
                Assert.GreaterOrEqual(Largest("Scorch"), burstRadius * 2f, "The burn is as wide as what the burst damages.");
                Assert.Greater(Largest("Glow"), burstRadius * 2f, "So is the flash.");
                Assert.Greater(kit.LiveParticles, 150, "Still a big crash.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
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
        public IEnumerator AnEscPause_FreezesEverything_AndMovingVehiclesAskForNoDust()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Warthog"));
            _sandbox.AddSquad(Team.A, Unit("Scorpion"));
            _sandbox.AddSquad(Team.B, Unit("Marine"));
            yield return null;
            _battle.Speed = 1f;
            _sandbox.Run();
            float deadline = Time.realtimeSinceStartup + 40f;
            while (_view.Kit.LiveParticles == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(_view.Kit.LiveParticles, 0, "The vehicles have started moving: there is dust.");

            GamePause.Pause();
            try
            {
                yield return null;
                float clock = _view.Kit.BattleTime;
                int live = _view.Kit.LiveParticles;
                for (int i = 0; i < 60; i++) yield return null;
                Assert.AreEqual(clock, _view.Kit.BattleTime, "The effects' clock stands still.");
                Assert.AreEqual(live, _view.Kit.LiveParticles, "No dust, nothing new, nothing running out.");
            }
            finally
            {
                GamePause.Resume();
            }
        }

        [UnityTest]
        public IEnumerator ARocket_MovesEveryFrame_WhereTheSimulationOnlyMovesItEveryTick()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Wolverine"));
            _sandbox.AddSquad(Team.B, Unit("Hornet"));
            yield return null;
            _battle.Speed = 8f;
            _sandbox.Run();
            var shots = new List<ShotInFlight>();
            float deadline = Time.realtimeSinceStartup + 60f;
            while (_battle.Simulation.ProjectileCount == 0 && !_battle.Simulation.IsFinished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(_battle.Simulation.ProjectileCount, 0, "A rocket is on its way.");

            // A steady 165 frames a second of game time, however fast the batch run really is: about five and a half frames to a tick.
            _battle.Speed = 1f;
            Time.captureFramerate = 165;
            try
            {
                int frames = 0, heldBySimulation = 0, heldOnScreen = 0, serial = -1;
                Vector3 shownBefore = Vector3.zero;
                Vec2 simulationBefore = default;
                float longestStep = 0f, longestSimulationStep = 0f;
                float limit = Time.realtimeSinceStartup + 30f;
                while (frames < 60 && !_battle.Simulation.IsFinished && Time.realtimeSinceStartup < limit)
                {
                    yield return null;
                    _battle.Simulation.GetProjectiles(shots);
                    int at = shots.FindIndex(shot => shot.Serial == serial);
                    if (at < 0)
                    {
                        // Follow the youngest. Only a whole flight in view says anything.
                        if (shots.Count == 0) continue;
                        serial = shots[shots.Count - 1].Serial;
                        at = shots.Count - 1;
                        _view.TryShownShot(serial, out shownBefore);
                        simulationBefore = shots[at].Position;
                        continue;
                    }
                    Assert.IsTrue(_view.TryShownShot(serial, out Vector3 shown), "Its place is known.");
                    longestStep = Mathf.Max(longestStep, Vector3.Distance(shownBefore, shown));
                    if ((shown - shownBefore).sqrMagnitude < 1e-8f) heldOnScreen++;
                    float simulationStep = Vec2.Distance(shots[at].Position, simulationBefore);
                    longestSimulationStep = Mathf.Max(longestSimulationStep, simulationStep);
                    if (simulationStep <= 0f) heldBySimulation++;
                    shownBefore = shown;
                    simulationBefore = shots[at].Position;
                    frames++;
                }
                Assert.Greater(frames, 20, "Rockets were watched.");
                Assert.Greater(heldBySimulation, frames / 2, "The simulation itself only moves at ticks (if not, this test says nothing).");
                Assert.Less(heldOnScreen, frames / 10, "On the screen it moves in nearly every frame.");
                Assert.Less(longestStep, longestSimulationStep * 0.5f, "Nothing like the simulation's jump of a tick's flight.");
            }
            finally
            {
                Time.captureFramerate = 0;
            }
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
                MushroomCloud.Puff smoke = MushroomCloud.Smoke(50, 3f, 14f, 7);
                _view.Kit.CloudSmoke(smoke.Offset, smoke.Size, smoke.Color, smoke.Degrees, smoke.Seed);
                MushroomCloud.Puff fire = MushroomCloud.Fire(20, 3f, 14f, 7);
                _view.Kit.CloudFire(fire.Offset, fire.Size, fire.Color, fire.Degrees, fire.Seed);
            }, Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator WhatTheHudAsksEveryFrameAllocatesNothing()
        {
            yield return LoadSandbox();
            _sandbox.AddSquad(Team.A, Unit("Marine"));
            _sandbox.AddSquad(Team.B, Unit("Grunt"));
            yield return null;
            _sandbox.Run();
            yield return null;
            _battle.CountAlive(Team.A);
            _battle.AliveValue(Team.A);
            Assert.That(() =>
            {
                _battle.CountAlive(Team.A);
                _battle.CountAlive(Team.B);
                _battle.AliveValue(Team.A);
                _battle.AliveValue(Team.B);
            }, Is.Not.AllocatingGCMemory());

            // A script with OnGUI and the layout pass on makes new layout groups every frame.
            foreach (MonoBehaviour hud in new MonoBehaviour[] { Object.FindAnyObjectByType<Halobellum.UI.BattleHud>(), Object.FindAnyObjectByType<Halobellum.UI.SandboxHud>(), Object.FindAnyObjectByType<Halobellum.UI.SquadStatsWindow>(), Object.FindAnyObjectByType<Halobellum.UI.PauseMenu>(), Object.FindAnyObjectByType<CameraShake>() })
            {
                Assert.IsNotNull(hud);
                Assert.IsFalse(hud.useGUILayout, hud.GetType().Name);
            }
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
        }
    }
}
