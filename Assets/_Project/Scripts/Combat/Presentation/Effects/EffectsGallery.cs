// EffectsGallery.cs
// Test tool for built games (M10): takes pictures of every effect so that a person can judge them, and measures the frame time of a big
// battle. SandboxAutoplay starts it:
//   -sandboxDemo <folder> -effects        every weapon kind firing, every kind of death, every colour of blood, dust, the Pelican's crash and every spell
//   -sandboxDemo <folder> -gallery        the same, after the pictures of the real models
//   -sandboxDemo <folder> -effectsBench   about 200 bodies at 1x and at 4x, then an army of ground vehicles, which is what kicks up dust; frame times go to bench.txt in the folder and to the log
//   Any of these with -particleScale <number>: that much dust, whatever the quality level says, for this run only.
// Each picture freezes the battle (particles run on battle time, so they freeze too), and is taken from the game's own camera and from
// close by. It reaches into the simulation in one way only, to kill a body on demand; battles here are throw-aways for pictures.
// Depends on: SandboxController, BattleController, UnitViewManager, CombatEffectsView, EffectKit, Halobellum.Combat, Halobellum.Core, UnityEngine.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class EffectsGallery : MonoBehaviour
    {
        private const float GameCameraDistance = 150f;

        private SandboxController _sandbox;
        private BattleController _battle;
        private UnitViewManager _views;
        private string _shots;
        private BattleCameraController _control;
        private Camera _camera;
        private bool _fired;
        private SimUnit _firedBy;
        private SimUnit _firedAt;
        private string _waitFor, _waitForWeapon;
        private string _only;

        /// <param name="only">Only the pictures whose names contain this ("w_Railgun", "d_Scorpion", "crash", "s_"); null for all of them.</param>
        public void Prepare(string shots, string only = null)
        {
            _shots = shots;
            _only = only;
            _sandbox = GetComponent<SandboxController>();
            _battle = GetComponent<BattleController>();
            _views = GetComponent<UnitViewManager>();
            _camera = Camera.main;
            _control = _camera.GetComponent<BattleCameraController>();
        }

        private bool Wanted(string picture) => _only == null || picture.Contains(_only);

        /// <summary>Every picture: weapons, deaths, the crash, spells.</summary>
        public IEnumerator TakeAll()
        {
            if (_control != null) _control.enabled = false;
            yield return Weapons();
            yield return Deaths();
            yield return Bloods();
            if (Wanted("dust")) yield return Dust();
            if (Wanted("crash")) yield return PelicanCrash();
            yield return Spells();
            if (_control != null) _control.enabled = true;
            Debug.Log("Effects gallery finished.");
        }

        // ------------------------------------------------------------------ weapons

        private readonly struct WeaponShot
        {
            public readonly string Label, Shooter, Target, Weapon;
            public readonly float Delay;
            /// <param name="weapon">Which of the shooter's weapons to wait for; null for any.</param>
            public WeaponShot(string label, string shooter, string target, float delay, string weapon = null)
            {
                Label = label;
                Shooter = shooter;
                Target = target;
                Delay = delay;
                Weapon = weapon;
            }
        }

        private IEnumerator Weapons()
        {
            const string tough = "Pure Form Tank";
            WeaponShot[] list =
            {
                new WeaponShot("Bullet", "Marine", tough, 0.05f),
                new WeaponShot("HeavyBullet", "Warthog", tough, 0.05f),
                new WeaponShot("Cannon", "Scorpion", tough, 0.2f),
                new WeaponShot("Gauss", "Gauss Warthog", tough, 0.06f),
                new WeaponShot("Railgun", "Cobra", tough, 0.03f),
                new WeaponShot("EmpRailgun", "Gremlin", tough, 0.03f),
                new WeaponShot("Rocket", "Wolverine", "Hornet", 0.25f),
                new WeaponShot("RocketLobbed", "Archer Missile Pod", tough, 0.9f),
                new WeaponShot("BombFlight", "Hornet", tough, 0.04f),
                new WeaponShot("Bomb", "Hornet", tough, 0.2f),
                new WeaponShot("SporeBombFlight", "Infested Hornet", tough, 0.3f, "Spore Bombs"),
                new WeaponShot("SporeBombBlast", "Infested Hornet", tough, 1.05f, "Spore Bombs"),
                new WeaponShot("SporeBombBanshee", "Infested Banshee", tough, 0.3f, "Spore Bomb"),
                new WeaponShot("Flame", "Flamethrower Trooper", tough, 0.7f),
                new WeaponShot("Plasma", "Grunt", tough, 0.08f),
                new WeaponShot("BeamRifle", "Jackal (Beam Rifle)", tough, 0.05f),
                new WeaponShot("PlasmaElite", "Elite", tough, 0.08f),
                new WeaponShot("FuelRod", "Fuel Rod Grunt", tough, 0.3f),
                new WeaponShot("Mortar", "Wraith", tough, 0.9f),
                new WeaponShot("HunterCannon", "Hunter Pair", tough, 0.2f),
                new WeaponShot("Needle", "Vampire", tough, 0.06f),
                new WeaponShot("Laser", "Spartan", tough, 0.05f),
                new WeaponShot("Sword", "Zealot", tough, 0.05f),
                new WeaponShot("Claw", "Combat Form", tough, 0.05f),
                new WeaponShot("Slam", "Flood Abomination", "Scorpion", 0.1f),
                new WeaponShot("SpatBone", "Swarm Form", "Hornet", 0.1f),
                new WeaponShot("SeedSpore", "Seeder", "Marine", 0.2f),
                new WeaponShot("BoneShards", "Pure Form Ranged", tough, 0.8f),
                new WeaponShot("FloodPlasma", "Infected Phantom", tough, 0.08f),
                new WeaponShot("PhantomPlasma", "Covenant Phantom", tough, 0.08f),
                new WeaponShot("PlasmaCannon", "Marauder", tough, 0.25f, "Plasma Cannon"),
                new WeaponShot("Sniper", "Spartan Sniper", tough, 0.05f),
                new WeaponShot("Shotgun", "Spartan CQB", tough, 0.04f),
                new WeaponShot("ChinGun", "Vulture", tough, 0.05f, "Chin Gun"),
            };
            foreach (WeaponShot shot in list)
            {
                if (Wanted("w_" + shot.Label)) yield return OneWeapon(shot);
            }
            if (Wanted("w_Beam")) yield return ScarabBeam();
            if (Wanted("w_LaserWindUp")) yield return LaserWindUp();
        }

        private IEnumerator OneWeapon(WeaponShot shot)
        {
            yield return Setup(new[] { shot.Shooter }, new[] { shot.Target });
            _battle.Speed = 6f;
            _sandbox.Run();
            _fired = false;
            _waitFor = shot.Shooter;
            _waitForWeapon = shot.Weapon;
            _battle.Simulation.Events.WeaponFired += OnFired;
            GameCamera(Vector3.zero);
            float limit = 45f;
            // First get to the fighting at speed, then watch the next shot at normal speed.
            while (!_fired && limit > 0f && !_battle.Simulation.IsFinished) { limit -= Time.unscaledDeltaTime; yield return null; }
            _battle.Speed = 1f;
            if (_firedBy != null) GameCamera(Middle(_firedBy, _firedAt));
            yield return null;
            _fired = false;
            limit = 10f;
            while (!_fired && limit > 0f && !_battle.Simulation.IsFinished) { limit -= Time.unscaledDeltaTime; yield return null; }
            if (!_fired)
            {
                Debug.LogWarning($"Effects gallery: {shot.Shooter} never fired.");
            }
            else
            {
                yield return new WaitForSecondsRealtime(shot.Delay);
                yield return Capture("w_" + shot.Label, Middle(_firedBy, _firedAt), Mathf.Max(13f, Mathf.Max(_firedBy.Radius, _firedAt.Radius) * 5f, Vector3.Distance(_views.WorldPosition(_firedBy), _views.WorldPosition(_firedAt)) * 1.05f));
            }
            _battle.Simulation.Events.WeaponFired -= OnFired;
            _sandbox.ResetBattle();
            yield return null;
        }

        /// <summary>A Spartan Laser winding up (its charge at the muzzle), most of the way through, before its next shot.</summary>
        private IEnumerator LaserWindUp()
        {
            yield return Setup(new[] { "Spartan" }, new[] { "Pure Form Tank" });
            _battle.Speed = 1f;
            _sandbox.Run();
            SimUnit charging = null;
            void OnWindUp(SimUnit attacker, int weapon, SimUnit target, float seconds) { if (charging == null) charging = attacker; }
            _battle.Simulation.Events.WeaponWindingUp += OnWindUp;
            float limit = 30f;
            while (charging == null && limit > 0f && !_battle.Simulation.IsFinished) { limit -= Time.unscaledDeltaTime; yield return null; }
            _battle.Simulation.Events.WeaponWindingUp -= OnWindUp;
            if (charging == null) Debug.LogWarning("Effects gallery: the Spartan never wound up.");
            else
            {
                yield return new WaitForSecondsRealtime(2f);
                yield return Capture("w_LaserWindUp", _views.WorldPosition(charging), 9f); // Close on the Spartan: the charge is small.
            }
            _sandbox.ResetBattle();
            yield return null;
        }

        private void OnFired(SimUnit attacker, int weapon, SimUnit target)
        {
            if (attacker.Stats.Name != _waitFor) return;
            if (_waitForWeapon != null && attacker.Weapons[weapon].Stats.Name != _waitForWeapon) return;
            _fired = true;
            _firedBy = attacker;
            _firedAt = target;
        }

        private IEnumerator ScarabBeam(string shooter = "Scarab", string picture = "w_Beam")
        {
            yield return Setup(new[] { shooter }, new[] { "Pure Form Tank" });
            _battle.Speed = 6f;
            _sandbox.Run();
            var beams = new List<BeamShot>();
            bool charged = false, fired = false;
            GameCamera(Vector3.zero);
            float limit = 45f, firingSeen = 0f;
            while (limit > 0f && !_battle.Simulation.IsFinished)
            {
                limit -= Time.unscaledDeltaTime;
                _battle.Simulation.GetBeams(beams);
                if (beams.Count > 0)
                {
                    _battle.Speed = 1f;
                    BeamShot beam = beams[0];
                    Vector3 focus = (_views.WorldPosition(beam.Attacker) + new Vector3(beam.Point.X, 0f, beam.Point.Y)) * 0.5f;
                    float distance = Mathf.Max(24f, Vector3.Distance(_views.WorldPosition(beam.Attacker), new Vector3(beam.Point.X, 0f, beam.Point.Y)) * 1.2f);
                    if (!beam.Firing && beam.Charge > 0.65f && !charged)
                    {
                        charged = true;
                        yield return Capture(picture + "_charging", focus, distance);
                    }
                    if (beam.Firing)
                    {
                        firingSeen += Time.unscaledDeltaTime;
                        if (firingSeen > 1.1f && !fired)
                        {
                            fired = true;
                            yield return Capture(picture + "_firing", focus, distance);
                            break;
                        }
                    }
                }
                yield return null;
            }
            _sandbox.ResetBattle();
            yield return null;
        }

        // ------------------------------------------------------------------ deaths

        private IEnumerator Deaths()
        {
            string[] list =
            {
                "Marine", "Placeholder Infantry", "Warthog", "Scorpion", "Elephant", "Scarab", "Hornet", "Pelican", "Banshee",
                "Infection Forms", "Combat Form", "Stalker Form", "Carrier Form", "Flood Abomination", "Swarm Form", "Infected Phantom",
                "Covenant Phantom",
            };
            foreach (string name in list)
            {
                if (Wanted("d_" + name.Replace(" ", ""))) yield return OneDeath(name);
            }
        }

        private IEnumerator OneDeath(string name)
        {
            yield return Setup(new[] { name, "Marine" }, new[] { "Marine" });
            _battle.Speed = 1f;
            _sandbox.Run();
            yield return new WaitForSecondsRealtime(0.25f);
            SimUnit victim = FirstAlive(name, Team.A);
            if (victim == null) yield break;
            Vector3 place = _views.WorldPosition(victim);
            place.y = 0f;
            GameCamera(place);
            yield return null;
            // A whole small squad goes at once, or the picture is of one body among many.
            int killed = 0;
            foreach (SimUnit unit in _battle.Simulation.Units)
            {
                if (unit.Team != Team.A || unit.Stats.Name != name || killed >= 3) continue;
                unit.Health = 0f;
                killed++;
            }
            string label = name.Replace(" ", "");
            float close = Mathf.Max(18f, victim.Radius * 9f);
            float[] times = { 0.12f, 0.4f, 1f, 3.5f };
            float start = Kit.BattleTime;
            foreach (float at in times)
            {
                while (Kit.BattleTime - start < at) yield return null;
                yield return Capture($"d_{label}_{at:0.0}", place + Vector3.up * (victim.IsAir ? 7f : 0f), close);
            }
            _sandbox.ResetBattle();
            yield return null;
        }

        // ------------------------------------------------------------------ blood and dust

        private IEnumerator Bloods()
        {
            string[] list = { "Marine", "Grunt", "Elite", "Hunter Pair", "Combat Form", "Jackal (Beam Rifle)", "Brute Grenadier" };
            foreach (string name in list)
            {
                if (Wanted("b_" + name.Replace(" ", ""))) yield return OneBlood(name);
            }
        }

        /// <summary>One colour of blood: a few hits on a body, then a squad dying and what it leaves on the ground.</summary>
        private IEnumerator OneBlood(string name)
        {
            yield return Setup(new[] { name, name }, new[] { "Marine", "Marine", "Marine", "Marine", "Scorpion" });
            _battle.Speed = 6f;
            _sandbox.Run();
            int hits = 0;
            SimUnit hurt = null;
            void OnHurt(SimUnit target, SimUnit attacker, float amount)
            {
                if (attacker == null || target.Stats.Name != name || target.Shield > 0f || amount <= 0f) return;
                hits++;
                hurt = target;
            }
            _battle.Simulation.Events.UnitDamaged += OnHurt;
            GameCamera(Vector3.zero);
            float limit = 45f;
            while (hits == 0 && limit > 0f && !_battle.Simulation.IsFinished) { limit -= Time.unscaledDeltaTime; yield return null; }
            _battle.Speed = 1f;
            if (hurt == null)
            {
                _battle.Simulation.Events.UnitDamaged -= OnHurt;
                Debug.LogWarning($"Effects gallery: {name} was never hurt.");
                _sandbox.ResetBattle();
                yield break;
            }
            string label = name.Replace(" ", "");
            Vector3 place = _views.WorldPosition(hurt);
            place.y = 0f;
            GameCamera(place);
            float close = Mathf.Max(13f, hurt.Radius * 7f);
            hits = 0;
            limit = 6f;
            // A few hits in the air at once, and just after the last of them, when its blood has appeared.
            while (hits < 12 && limit > 0f && !_battle.Simulation.IsFinished) { limit -= Time.unscaledDeltaTime; yield return null; }
            float shown = Kit.BattleTime;
            while (Kit.BattleTime - shown < 0.16f) yield return null;
            yield return Capture($"b_{label}_hits", place, close);
            _battle.Simulation.Events.UnitDamaged -= OnHurt;

            int killed = 0;
            foreach (SimUnit unit in _battle.Simulation.Units)
            {
                if (unit.Team != Team.A || unit.Stats.Name != name || !unit.IsAlive || killed >= 6) continue;
                unit.Health = 0f;
                killed++;
            }
            float[] times = { 0.12f, 0.45f, 3f };
            float start = Kit.BattleTime;
            foreach (float at in times)
            {
                while (Kit.BattleTime - start < at) yield return null;
                yield return Capture($"b_{label}_death_{at:0.00}", place, close);
            }
            _sandbox.ResetBattle();
            yield return null;
        }

        /// <summary>Vehicles and infantry on the move, to judge the dust at this run's scale. The picture waits until a Warthog has been driving for as long as its dust lasts, so that its whole trail is there.</summary>
        private IEnumerator Dust()
        {
            yield return Setup(new[] { "Warthog", "Warthog", "Scorpion", "Scorpion", "Marine", "Marine" }, new[] { "Marine" });
            _battle.Speed = 1f;
            _sandbox.Run();
            const float TrailSeconds = 1.5f;
            float limit = 30f, drivenSince = Kit.BattleTime;
            SimUnit vehicle = null;
            while (limit > 0f && !_battle.Simulation.IsFinished)
            {
                limit -= Time.unscaledDeltaTime;
                vehicle = FirstAlive("Warthog", Team.A);
                if (vehicle == null || vehicle.State != UnitState.Move || vehicle.Velocity.LengthSquared < 1f) drivenSince = Kit.BattleTime;
                if (Kit.BattleTime - drivenSince >= TrailSeconds) break;
                yield return null;
            }
            if (vehicle == null || Kit.BattleTime - drivenSince < TrailSeconds)
            {
                Debug.LogWarning("Effects gallery: no Warthog drove for long enough to show its dust.");
            }
            else
            {
                Debug.Log(FormattableString($"Effects gallery: dust x{Kit.ParticleScale:0.0#}, a Warthog driving at {Mathf.Sqrt(vehicle.Velocity.LengthSquared):0.0} m/s at {Kit.BattleTime:0.0} s, {Kit.LiveDust} puffs in the air."));
                yield return Capture(FormattableString($"dust_{Kit.ParticleScale:0.0}x"), _views.WorldPosition(vehicle), 30f);
            }
            _sandbox.ResetBattle();
            yield return null;
        }

        private IEnumerator PelicanCrash()
        {
            yield return Setup(new[] { "Infected Pelican" }, new[] { "Marine", "Marine" });
            _battle.Speed = 6f;
            _sandbox.Run();
            float limit = 60f;
            SimUnit pelican = FirstAlive("Infected Pelican", Team.A);
            while (limit > 0f && pelican != null && pelican.IsAlive && !pelican.HasDived) { limit -= Time.unscaledDeltaTime; yield return null; }
            if (pelican == null || !pelican.HasDived)
            {
                Debug.LogWarning("Effects gallery: the Infected Pelican never dived.");
                yield break;
            }
            _battle.Speed = 1f;
            Vector3 landing = new Vector3(pelican.LeapTo.X, 0f, pelican.LeapTo.Y);
            GameCamera(landing);
            yield return null;
            while (pelican.IsAlive && pelican.LeapProgress < 0.55f) yield return null;
            if (pelican.IsAlive) yield return Capture("crash_1_dive", landing, 46f);
            while (pelican.IsAlive) yield return null;
            float[] times = { 0.08f, 0.35f, 1f, 2.2f, 5f };
            float start = Kit.BattleTime;
            foreach (float at in times)
            {
                while (Kit.BattleTime - start < at) yield return null;
                yield return Capture($"crash_2_{at:0.00}", landing, 60f);
            }
            _sandbox.ResetBattle();
            yield return null;
        }

        // ------------------------------------------------------------------ spells

        private readonly struct SpellShot
        {
            public readonly SpellStats Stats;
            public readonly float[] Times;
            public SpellShot(SpellStats stats, params float[] times)
            {
                Stats = stats;
                Times = times;
            }
        }

        private IEnumerator Spells()
        {
            UnitStats marine = Find("Marine");
            SpellShot[] list =
            {
                new SpellShot(new SpellStats { Name = "MAC Strike", Offensive = true, Radius = 4f, DamagePerTick = 800f, DamageType = DamageType.Explosive, IsAntiArmor = true, DelaySeconds = 1f }, 0.05f, 0.35f, 1f, 3f),
                new SpellShot(new SpellStats { Name = "HAVOK Nuke", Offensive = true, Radius = 14f, DamagePerTick = 2500f, DamageType = DamageType.Explosive, DelaySeconds = 4f }, -1f, 0.08f, 0.45f, 1.5f, 3f, 5f, 7f),
                new SpellShot(new SpellStats { Name = "Plasma Bombardment", Offensive = true, Radius = 5f, DamagePerTick = 450f, DamageType = DamageType.Energy, DelaySeconds = 1f }, 0.1f, 0.5f, 1.5f),
                new SpellShot(new SpellStats { Name = "Plasma Storm", Offensive = true, Radius = 10f, DamagePerTick = 40f, DamageType = DamageType.Energy, Ticks = 8, TickInterval = 0.5f, DelaySeconds = 1f, Targets = TargetFilter.Both }, 0.3f, 1.5f, 3.2f),
                new SpellShot(new SpellStats { Name = "Glassing Beam", Offensive = true, Radius = 12f, DamagePerTick = 600f, DamageType = DamageType.Energy, Ticks = 5, TickInterval = 0.6f, DelaySeconds = 4f, Targets = TargetFilter.Both }, -1f, 0.2f, 1.5f, 3.5f, 6f),
                new SpellShot(new SpellStats { Name = "Archer Missile Barrage", Offensive = true, Radius = 10f, DamagePerTick = 50f, DamageType = DamageType.Explosive, Ticks = 6, TickInterval = 0.5f, DelaySeconds = 1f, Targets = TargetFilter.Both }, 0.15f, 0.7f, 1.8f),
                new SpellShot(new SpellStats { Name = "Gravemind's Wrath", Offensive = true, Radius = 13f, DamagePerTick = 1600f, DamageType = DamageType.Explosive, DelaySeconds = 4f, InfectsKills = true }, -1f, 0.1f, 0.6f, 2f, 4f),
                new SpellShot(new SpellStats { Name = "Spore Burst", Offensive = true, Radius = 5f, DamagePerTick = 350f, DamageType = DamageType.Explosive, DelaySeconds = 1f, InfectsKills = true }, 0.1f, 0.8f),
                new SpellShot(new SpellStats { Name = "Blight Rain", Offensive = true, Radius = 10f, DamagePerTick = 40f, DamageType = DamageType.Explosive, Ticks = 8, TickInterval = 0.5f, DelaySeconds = 1f, InfectsKills = true, Targets = TargetFilter.Both }, 0.5f, 2f),
                new SpellShot(new SpellStats { Name = "Electromagnetic Pulse", Offensive = true, Effect = SpellEffect.Disrupt, Radius = 10f, ShieldDamage = 100f, TechDisableSeconds = 5f, DelaySeconds = 1f }, 0.08f, 0.35f),
                new SpellShot(new SpellStats { Name = "Nerve Spores", Offensive = true, Effect = SpellEffect.Disrupt, Radius = 10f, ShieldDamage = 100f, TechDisableSeconds = 5f, DelaySeconds = 1f }, 0.3f),
                new SpellShot(new SpellStats { Name = "Battlefield Rally", Effect = SpellEffect.Buff, Radius = 10f, DamageMultiplier = 1.3f, DelaySeconds = 1f }, 0.4f),
                new SpellShot(new SpellStats { Name = "Bubble Shield", Effect = SpellEffect.Buff, Radius = 8f, ShieldBonusShare = 0.5f, DelaySeconds = 1f }, 0.4f),
                new SpellShot(new SpellStats { Name = "Smoke Bomb", Offensive = false, Effect = SpellEffect.Smoke, AreaWidth = 45f, AreaDepth = 8f, RangeMultiplier = 0.5f, DelaySeconds = 0.5f }, 2.5f),
                new SpellShot(new SpellStats { Name = "ODST Drop Pods", Offensive = true, Effect = SpellEffect.DropSquad, Radius = 4f, DropUnit = marine, DropSquads = 1, DelaySeconds = 1f }, 0.1f, 0.5f),
            };
            foreach (SpellShot shot in list)
            {
                if (Wanted("s_" + shot.Stats.Name.Replace(" ", "").Replace("'", ""))) yield return OneSpell(shot);
            }
        }

        private IEnumerator OneSpell(SpellShot shot)
        {
            yield return Setup(new[] { "Marine" }, new[] { "Marine", "Scorpion", "Marine" });
            _battle.Speed = 1f;
            _sandbox.Run();
            // Aimed at the middle of the enemy, as a player would.
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (SimUnit unit in _battle.Simulation.Units)
            {
                if (unit.Team != Team.B) continue;
                sum += new Vector2(unit.Position.X, unit.Position.Y);
                count++;
            }
            Vector2 middle = count > 0 ? sum / count : Vector2.zero;
            SimSpell spell = _battle.Simulation.AddSpell(shot.Stats, Team.A, new Vec2(middle.x, middle.y), 20f);
            var place = new Vector3(middle.x, 0f, middle.y);
            GameCamera(place);
            string label = shot.Stats.Name.Replace(" ", "").Replace("'", "");
            float radius = Mathf.Max(5f, shot.Stats.Radius);
            var frames = new List<float>();

            foreach (float at in shot.Times)
            {
                // A negative time: the warning, half a second before it goes off. The effects' clock goes on after the battle has ended.
                float battleTime = at < 0f ? shot.Stats.DelaySeconds - 0.6f : shot.Stats.DelaySeconds + at;
                while (Kit.BattleTime < battleTime)
                {
                    if (Kit.BattleTime > shot.Stats.DelaySeconds) frames.Add(Time.unscaledDeltaTime);
                    yield return null;
                }
                yield return Capture($"s_{label}_{(at < 0f ? "warn" : at.ToString("0.00"))}", place + Vector3.up * radius * 0.3f, radius * 4.2f);
            }
            Debug.Log($"Effects gallery: {label} after it went off: {Summary(frames)}");
            _sandbox.ResetBattle();
            yield return null;
        }

        // ------------------------------------------------------------------ the frame-time measurement

        /// <summary>About 200 bodies on the field at 1x and at 4x. The results go to the log and to bench.txt.</summary>
        public IEnumerator Benchmark()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var report = new StringBuilder();
            report.AppendLine($"Effects benchmark, {Screen.width}x{Screen.height}, {SystemInfo.graphicsDeviceName}, {SystemInfo.processorType}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, MSAA {SandboxAutoplay.ShownMsaa}x, dust x{Kit.ParticleScale:0.0#}");
            yield return OnAgainstOff(report);
            yield return BenchmarkOne(1f, report, "bench_1x", false);
            yield return BenchmarkOne(4f, report, "bench_4x", false);
            // The big battle is mostly infantry, which hardly kick up dust: this one is what more dust costs.
            report.AppendLine("Ground vehicles only:");
            yield return BenchmarkOne(1f, report, "bench_vehicles_1x", false, VehiclesBlue, VehiclesRed);
            yield return BenchmarkOne(4f, report, "bench_vehicles_4x", false, VehiclesBlue, VehiclesRed);
            string text = report.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.Combine(_shots, "bench.txt"), text);
        }

        /// <summary>
        /// One battle with effects going on and off every few seconds. Whatever else the PC is doing falls on both alike, so the difference
        /// between the two lists of frame times is what the effects cost, all of it: scripts, particle work and the graphics card.
        /// </summary>
        private IEnumerator OnAgainstOff(StringBuilder report)
        {
            yield return Setup(BenchBlue, BenchRed);
            _battle.Speed = 1f;
            _sandbox.Run();
            CombatEffectsView view = GetComponent<CombatEffectsView>();
            var on = new List<float>();
            var off = new List<float>();
            const float Each = 6f, Settle = 2.5f;
            float elapsed = 0f;
            yield return null;
            while (!_battle.Simulation.IsFinished && elapsed < 90f)
            {
                elapsed += Time.unscaledDeltaTime;
                float inTurn = Mathf.Repeat(elapsed, Each);
                bool muted = Mathf.FloorToInt(elapsed / Each) % 2 == 1;
                view.Kit.Muted = muted;
                // Only in the fighting, and not while what the last turn left is still dying away.
                if (inTurn > Settle && elapsed > 12f) (muted ? off : on).Add(Time.unscaledDeltaTime);
                yield return null;
            }
            view.Kit.Muted = false;
            report.AppendLine("Same battle, effects going on and off in turns, 1x:");
            report.AppendLine("  on:  " + Summary(on));
            report.AppendLine("  off: " + Summary(off));
            _sandbox.ResetBattle();
            yield return null;
        }

        private static string Summary(List<float> times)
        {
            if (times.Count == 0) return "no frames";
            times.Sort();
            double total = 0;
            int slow = 0;
            foreach (float t in times)
            {
                total += t;
                if (t > 1f / 60f) slow++;
            }
            float average = (float)(total / times.Count);
            float median = times[times.Count / 2], p99 = times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.99f))];
            return FormattableString($"{times.Count} frames, average {average * 1000f:0.00} ms, middle {median * 1000f:0.00} ms, 1% slowest {p99 * 1000f:0.00} ms, over 16.7 ms: {slow} ({100f * slow / times.Count:0.0}%)");
        }

        private static readonly string[] BenchBlue = { "Marine", "Marine", "Marine", "Marine", "Flamethrower Trooper", "Flamethrower Trooper", "Scorpion", "Scorpion", "Warthog", "Gauss Warthog", "Wolverine", "Hornet", "Pelican", "Spartan", "Archer Missile Pod" };
        private static readonly string[] BenchRed = { "Grunt", "Grunt", "Grunt", "Fuel Rod Grunt", "Jackal (Beam Rifle)", "Jackal (Beam Rifle)", "Elite", "Elite", "Ghost", "Wraith", "Banshee", "Vampire", "Hunter Pair", "Scarab", "Zealot" };
        private static readonly string[] VehiclesBlue = { "Warthog", "Warthog", "Warthog", "Warthog", "Warthog", "Gauss Warthog", "Gauss Warthog", "Gauss Warthog", "Scorpion", "Scorpion", "Scorpion", "Scorpion", "Wolverine", "Cobra", "Cobra" };
        private static readonly string[] VehiclesRed = { "Ghost", "Ghost", "Ghost", "Ghost", "Ghost", "Ghost", "Wraith", "Wraith", "Wraith", "Wraith", "Chopper", "Chopper", "Chopper", "Marauder", "Marauder" };

        private IEnumerator BenchmarkOne(float speed, StringBuilder report, string picture, bool muted, string[] blue = null, string[] red = null)
        {
            yield return Setup(blue ?? BenchBlue, red ?? BenchRed);
            _battle.Speed = speed;
            _sandbox.Run();
            int bodies = _battle.Simulation.Units.Count;
            var times = new List<float>();
            CombatEffectsView view = GetComponent<CombatEffectsView>();
            view.Kit.Muted = muted;
            float elapsed = 0f, nextPicture = 12f / speed;
            int pictures = 0;
            int mostLive = 0, mostDust = 0;
            float mostCost = 0f;
            double totalCost = 0;
            int collectionsAtStart = System.GC.CollectionCount(0);
            yield return null;
            while (!_battle.Simulation.IsFinished && elapsed < 90f)
            {
                float delta = Time.unscaledDeltaTime;
                elapsed += delta;
                // Only once the two armies are fighting.
                if (_battle.Simulation.ProjectileCount > 0 || mostLive > 400)
                {
                    times.Add(delta);
                    totalCost += view.LastFrameCostMilliseconds;
                    mostCost = Mathf.Max(mostCost, view.LastFrameCostMilliseconds);
                }
                int live = view.Kit.LiveParticles;
                if (live > mostLive) mostLive = live;
                mostDust = Mathf.Max(mostDust, view.Kit.LiveDust);
                if (elapsed >= nextPicture)
                {
                    nextPicture += 7f / speed;
                    ScreenCapture.CaptureScreenshot(Path.Combine(_shots, $"{picture}_{++pictures}.png"));
                }
                yield return null;
            }
            times.Sort();
            double total = 0;
            foreach (float t in times) total += t;
            float average = times.Count > 0 ? (float)(total / times.Count) : 0f;
            float p99 = times.Count > 0 ? times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.99f))] : 0f;
            float worst = times.Count > 0 ? times[times.Count - 1] : 0f;
            int slow = 0;
            foreach (float t in times) if (t > 1f / 60f) slow++;
            EffectBudget budget = view.Kit.Budget;
            view.Kit.Muted = false;
            report.AppendLine(FormattableString($"{(muted ? "No effects, " : "")}Speed {speed}x: {bodies} bodies at the start, {times.Count} frames in {elapsed:0.0} s; average {average * 1000f:0.00} ms ({(average > 0f ? 1f / average : 0f):0} FPS), 1% slowest {p99 * 1000f:0.00} ms ({(p99 > 0f ? 1f / p99 : 0f):0} FPS), worst {worst * 1000f:0.00} ms; frames over 16.7 ms: {slow}"));
            report.AppendLine(FormattableString($"  effects' own main-thread time: average {(times.Count > 0 ? totalCost / times.Count : 0):0.00} ms, worst {mostCost:0.00} ms; garbage collections during the battle: {System.GC.CollectionCount(0) - collectionsAtStart}"));
            report.AppendLine($"  most particles alive {mostLive}, of them dust {mostDust} (limit {view.Kit.DustLimit}); dropped small {budget.Dropped[0]}, medium {budget.Dropped[1]}, big {budget.Dropped[2]}, huge {budget.Dropped[3]}; result: {(_battle.Simulation.IsFinished ? _battle.Simulation.Result.ToSummaryLine() : "still fighting")}");
            _sandbox.ResetBattle();
            yield return null;
        }

        private static string FormattableString(System.FormattableString text) => System.FormattableString.Invariant(text);

        // ------------------------------------------------------------------ tools

        private IEnumerator Setup(string[] blue, string[] red)
        {
            if (_sandbox.IsBattle) _sandbox.ResetBattle();
            _sandbox.Clear(Team.A);
            _sandbox.Clear(Team.B);
            foreach (string name in blue) _sandbox.AddSquad(Team.A, Find(name));
            foreach (string name in red) _sandbox.AddSquad(Team.B, Find(name));
            yield return null;
        }

        private UnitStats Find(string name)
        {
            foreach (SandboxController.SandboxUnit unit in _sandbox.Units)
            {
                if (unit.Stats.Name == name) return unit.Stats;
            }
            throw new KeyNotFoundException("No sandbox unit named " + name);
        }

        private SimUnit FirstAlive(string name, Team team)
        {
            foreach (SimUnit unit in _battle.Simulation.Units)
            {
                if (unit.IsAlive && unit.Team == team && unit.Stats.Name == name) return unit;
            }
            return null;
        }

        private Vector3 Middle(SimUnit a, SimUnit b)
        {
            return (_views.WorldPosition(a) + _views.WorldPosition(b)) * 0.5f;
        }

        private EffectKit Kit => GetComponent<CombatEffectsView>().Kit;

        private void GameCamera(Vector3 focus)
        {
            BattleCameraController.ToolView(System.Environment.GetCommandLineArgs(), out float yaw, out float tilt);
            Quaternion rotation = Quaternion.Euler(tilt, yaw, 0f);
            _camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * GameCameraDistance, rotation);
        }

        /// <summary>Freezes the battle, takes the picture from the game's camera and from nearby, and carries on.</summary>
        private IEnumerator Capture(string name, Vector3 focus, float closeDistance)
        {
            float speed = _battle.Speed;
            _battle.Speed = 0f;
            yield return null;
            GameCamera(new Vector3(focus.x, 0f, focus.z));
            yield return null;
            Shot(name + "_game");
            yield return null;
            yield return null;
            Quaternion rotation = Quaternion.Euler(33f, -24f, 0f);
            _camera.transform.SetPositionAndRotation(focus + Vector3.up * 1.5f - rotation * Vector3.forward * closeDistance, rotation);
            yield return null;
            Shot(name + "_close");
            yield return null;
            yield return null;
            GameCamera(new Vector3(focus.x, 0f, focus.z));
            _battle.Speed = speed;
        }

        private void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_shots, name + ".png"));
    }
}
