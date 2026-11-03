// CombatEffectsView.cs
// Everything you see of a fight besides the bodies themselves (M10), so that fights are readable and exciting (Bible pillar "Readable
// battles"). It listens to the simulation and draws, through EffectKit and EffectRecipes:
//   - a flash at the muzzle of each shot, and what WeaponLooks says the weapon's shot looks like. Shots start at the muzzle of the model
//     that fires them, if it has one (D45), and fly from that height to the height of what they aim at; the simulation knows only the ground;
//   - shots the simulation hits with at once still appear to fly for a moment, and their impact comes when they arrive;
//   - projectiles at their real place in flight, in an arc if they are lobbed, blended between ticks as bodies are, with trails laid by
//     distance where the look asks for one, and a blast as wide as their splash where they land;
//   - beams: a thin line and a growing spot while charging, then a thick glowing beam with a burning spot, sparks and scorched ground;
//   - dust behind moving ground units. Deaths are DeathEffects and spells are SpellEffects.
// All of it is pooled: nothing is created during battle. Everything runs on battle time: it pauses with the battle and runs faster at
// 2x and 4x. If the frame rate suffers or too much is going on, EffectBudget drops the small things first. Looks only: nothing here
// changes the simulation, and its random numbers aren't the simulation's.
// Depends on: BattleController, UnitViewManager, EffectKit, EffectRecipes, DeathEffects, SpellEffects, WeaponLooks, CameraShake,
//             EffectsPostProcess, Halobellum.Combat, UnityEngine.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    [DefaultExecutionOrder(50)] // After UnitViewManager has moved the bodies and their turrets.
    public sealed class CombatEffectsView : MonoBehaviour
    {
        private struct ShotTrack
        {
            public int Serial;
            /// <summary>Where it was at the last two ticks; it is drawn between them, as a body is.</summary>
            public Vector3 Previous, Current;
            /// <summary>Where it is drawn this frame.</summary>
            public Vector3 Shown;
            public Vector3 LastPuff;
        }

        private const int TrackSize = 1024;
        /// <summary>The most puffs one shot's trail lays in one frame. A frame that long has hitched, and the trail may as well skip.</summary>
        public const int MostTrailPuffsAtOnce = 6;
        /// <summary>Each body is looked at for dust this often, in seconds of battle time.</summary>
        public const float DustLookSeconds = 7f / 60f;

        /// <summary>Paler than other smoke: from the game's camera a rocket's trail has to show against dark ground as well as light.</summary>
        private static readonly Color RocketSmoke = new Color(0.72f, 0.7f, 0.66f, 0.62f);

        [Tooltip("How high a lobbed shot flies at its peak, as a share of the distance it travels.")]
        [SerializeField] private float lobHeightShare = 0.3f;
        [Tooltip("Height above the ground where ground units' shots start and land.")]
        [SerializeField] private float shotHeight = 0.9f;
        [Tooltip("Height of the beam above a ground unit, if its model has no muzzle for it.")]
        [SerializeField] private float beamOriginHeight = 4f;

        private BattleController _battle;
        private UnitViewManager _units;
        private BattleSimulation _shown;
        private EffectKit _kit;
        private EffectRecipes _recipes;
        private DeathEffects _deaths;
        private SpellEffects _spells;
        private Transform _container;

        private readonly List<ShotInFlight> _shots = new List<ShotInFlight>();
        private readonly List<ShotInFlight> _lastShots = new List<ShotInFlight>();
        private readonly List<ShotInFlight> _tickShots = new List<ShotInFlight>();
        private readonly ShotTrack[] _tracks = new ShotTrack[TrackSize];
        private readonly List<BeamShot> _beams = new List<BeamShot>();
        private readonly float[] _beamEmit = new float[16];
        private readonly Vector3[] _beamLastScorch = new Vector3[16];
        private readonly Dictionary<WeaponStats, WeaponLook> _looksA = new Dictionary<WeaponStats, WeaponLook>();
        private readonly Dictionary<WeaponStats, WeaponLook> _looksB = new Dictionary<WeaponStats, WeaponLook>();
        private readonly HashSet<string> _typesA = new HashSet<string>();
        private bool _mirror;
        private int _instantShots, _projectileShots;
        private float _dustDue;
        private int _dustNext;
        private readonly System.Diagnostics.Stopwatch _cost = new System.Diagnostics.Stopwatch();

        /// <summary>Main-thread time the effects took last frame, in milliseconds (for the benchmark). Not the time on the graphics card.</summary>
        public float LastFrameCostMilliseconds { get; private set; }

        /// <summary>The drawing tools (for tests and the benchmark).</summary>
        public EffectKit Kit => _kit;
        /// <summary>Deaths (for tests).</summary>
        public DeathEffects Deaths => _deaths;
        /// <summary>True if both sides could fire the same colours, so that the second side's are pushed to red (for tests).</summary>
        public bool IsMirror => _mirror;

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _units = GetComponent<UnitViewManager>();

            _container = new GameObject("Combat Effects").transform;
            _kit = new EffectKit(_container);
            _recipes = new EffectRecipes(_kit);
            _deaths = new DeathEffects(_recipes, _units);
            _spells = new SpellEffects(_recipes);

            Camera camera = Camera.main;
            if (camera != null && camera.GetComponent<CameraShake>() == null) camera.gameObject.AddComponent<CameraShake>();
            EffectsPostProcess.Apply(_container);

            _battle.BattleChanged += OnBattleChanged;
            OnBattleChanged(_battle.Simulation);
        }

        private void OnDestroy()
        {
            if (_shown != null)
            {
                _shown.Events.WeaponFired -= OnWeaponFired;
                _shown.Events.AttackBlocked -= OnAttackBlocked;
                _shown.Events.Ticked -= OnTicked;
            }
            _deaths?.Show(null);
            _spells?.Show(null);
            if (_battle != null) _battle.BattleChanged -= OnBattleChanged;
            _kit?.Destroy();
            if (_container != null) Destroy(_container.gameObject);
        }

        /// <summary>Listens to the newly shown battle and clears effects left from the last one.</summary>
        private void OnBattleChanged(BattleSimulation simulation)
        {
            if (_shown != null)
            {
                _shown.Events.WeaponFired -= OnWeaponFired;
                _shown.Events.AttackBlocked -= OnAttackBlocked;
                _shown.Events.Ticked -= OnTicked;
            }
            _shown = simulation;
            _kit.Clear();
            _recipes.Clear();
            _deaths.Show(simulation);
            _spells.Show(simulation);
            _looksA.Clear();
            _looksB.Clear();
            _shots.Clear();
            _lastShots.Clear();
            _instantShots = 0;
            _projectileShots = 0;
            _dustDue = 0f;
            _dustNext = 0;
            for (int i = 0; i < TrackSize; i++) _tracks[i].Serial = -1;
            _mirror = BothSidesLookAlike(simulation);
            if (_shown == null) return;
            _shown.Events.WeaponFired += OnWeaponFired;
            _shown.Events.AttackBlocked += OnAttackBlocked;
            _shown.Events.Ticked += OnTicked;
        }

        /// <summary>The same faction on both sides, or (in the sandbox) the same unit type on both sides.</summary>
        private bool BothSidesLookAlike(BattleSimulation simulation)
        {
            if (simulation == null) return false;
            if (BattleFactions.TeamA == BattleFactions.TeamB) return true;
            _typesA.Clear();
            IReadOnlyList<SimUnit> units = simulation.Units;
            for (int i = 0; i < units.Count; i++) if (units[i].Team == Team.A && !units[i].Stats.IsTower) _typesA.Add(units[i].Stats.Name);
            for (int i = 0; i < units.Count; i++) if (units[i].Team == Team.B && !units[i].Stats.IsTower && _typesA.Contains(units[i].Stats.Name)) return true;
            return false;
        }

        private WeaponLook LookOf(SimUnit unit, int weaponIndex)
        {
            WeaponStats weapon = unit.Weapons[weaponIndex].Stats;
            Dictionary<WeaponStats, WeaponLook> looks = unit.Team == Team.A ? _looksA : _looksB;
            if (looks.TryGetValue(weapon, out WeaponLook look)) return look;
            look = WeaponLooks.For(weapon, unit.Stats, BattleFactions.For(unit.Team), unit.Team, _mirror);
            looks[weapon] = look;
            return look;
        }

        // ------------------------------------------------------------------ a weapon fires

        private void OnWeaponFired(SimUnit attacker, int weaponIndex, SimUnit target)
        {
            WeaponStats weapon = attacker.Weapons[weaponIndex].Stats;
            if (weapon.IsBeam) return; // Beams are drawn in LateUpdate.
            _cost.Start();
            WeaponLook look = LookOf(attacker, weaponIndex);

            // This counts shots in the same order as the simulation does, so a shot's muzzle flash is at the muzzle it leaves from.
            int shotNumber = weapon.IsInstant ? _instantShots++ : _projectileShots++;
            Vector3 from = _units.TryMuzzle(attacker, weaponIndex, shotNumber, out Vector3 muzzle) ? muzzle : ShotPoint(attacker);
            Vector3 to = _units.AimPoint(target, shotHeight);
            // Shots land all over a big body, not on one spot.
            float scatter = target.Radius * 0.55f;
            to.x += Random.Range(-scatter, scatter);
            to.z += Random.Range(-scatter, scatter);
            Vector3 line = to - from;
            if (line.sqrMagnitude >= 1e-4f) DrawFiring(weapon, look, target, from, to, line.normalized);
            _cost.Stop();
        }

        private void DrawFiring(WeaponStats weapon, in WeaponLook look, SimUnit target, Vector3 from, Vector3 to, Vector3 direction)
        {
            switch (look.Kind)
            {
                case WeaponFeelKind.Burst:
                    return; // The death effect is all it needs.
                case WeaponFeelKind.Melee:
                    _recipes.MeleeHit(to, look, target.Stats.ArmorType);
                    if (weapon.SplashRadius > 0f)
                    {
                        Vector3 slam = new Vector3(target.Position.X, 0.2f, target.Position.Y);
                        _recipes.Blast(slam, weapon.SplashRadius, BlastStyle.Dust, look.Shot, EffectPriority.Big);
                        CameraShake.Kick(slam, 0.12f);
                    }
                    return;
                case WeaponFeelKind.Flame:
                    _recipes.FlameShot(from, to, weapon.ConeDegrees, weapon.Range);
                    return;
                case WeaponFeelKind.Gauss:
                    // A bright straight streak, there at once; the simulation's projectile only says when it lands.
                    _recipes.Muzzle(from, direction, look);
                    _kit.FlashLine(from, to, look.ShotWidth * _kit.ReadScale, look.Shot, 0.3f);
                    _kit.FlashLine(from, to, look.ShotWidth * 0.3f * _kit.ReadScale, Color.white, 0.18f);
                    // A railgun the simulation hits with at once has no projectile to land later, so its impact comes with the line.
                    if (weapon.IsInstant)
                    {
                        _recipes.Impact(to, direction, look.Impact, target.Stats.ArmorType, target.Shield > 0f, look.Shot);
                        if (weapon.SplashRadius > 0f) _recipes.Blast(to, weapon.SplashRadius, look.Blast, look.Shot, EffectPriority.Big);
                    }
                    return;
            }

            _recipes.Muzzle(from, direction, look);
            if (!weapon.IsInstant) return; // Drawn in flight, in LateUpdate.

            float flight = _recipes.InstantShot(from, to, look);
            if (look.Impact != ImpactStyle.None)
                _recipes.ImpactAfter(flight, to, direction, look.Impact, target.Stats.ArmorType, target.Shield > 0f, look.Shot);
            if (weapon.SplashRadius > 0f)
                _recipes.BlastAfter(flight, to, weapon.SplashRadius, look.Blast, look.Shot, EffectPriority.Big);
        }

        private void OnAttackBlocked(SimUnit target, SimUnit attacker)
        {
            _recipes.ShieldHit(_units.AimPoint(target, shotHeight));
        }

        // ------------------------------------------------------------------ every frame

        private void LateUpdate()
        {
            if (_kit == null) return;
            _cost.Start();
            float speed = _battle.Speed;
            _kit.BeginFrame(speed, Time.deltaTime, Time.unscaledDeltaTime);
            _recipes.RunDue();

            float battleDelta = Time.deltaTime * speed;
            DrawShots();
            DrawBeams(battleDelta);
            if (speed > 0f && _battle.IsPlaying) KickUpDust(battleDelta);
            _deaths.Tick(battleDelta);
            _spells.Tick();
            _kit.EndFrame();
            _cost.Stop();
            LastFrameCostMilliseconds = (float)_cost.Elapsed.TotalMilliseconds;
            _cost.Reset();
        }

        private void DrawShots()
        {
            if (_shown != null) _shown.GetProjectiles(_shots);
            else _shots.Clear();

            // Shots stay in order of their serial numbers, so one pass over both lists finds those that have landed.
            int now = 0;
            for (int last = 0; last < _lastShots.Count; last++)
            {
                ShotInFlight old = _lastShots[last];
                while (now < _shots.Count && _shots[now].Serial < old.Serial) now++;
                if (now < _shots.Count && _shots[now].Serial == old.Serial) continue;
                Land(old);
            }
            _lastShots.Clear();

            for (int i = 0; i < _shots.Count; i++)
            {
                ShotInFlight shot = _shots[i];
                _lastShots.Add(shot);
                DrawShot(shot);
            }
        }

        /// <summary>After every tick: where each shot now is, so that frames can draw it between its last two places. Called more than once in a frame at high speed.</summary>
        private void OnTicked(int tick)
        {
            _cost.Start();
            _shown.GetProjectiles(_tickShots);
            for (int i = 0; i < _tickShots.Count; i++)
            {
                ShotInFlight shot = _tickShots[i];
                ref ShotTrack track = ref _tracks[shot.Serial & (TrackSize - 1)];
                if (track.Serial != shot.Serial)
                {
                    StartTrack(ref track, shot);
                    continue;
                }
                track.Previous = track.Current;
                track.Current = ShotWorld(shot);
            }
            _cost.Stop();
        }

        /// <summary>A shot's first tick: it flies from where it left, and its trail starts there.</summary>
        private void StartTrack(ref ShotTrack track, in ShotInFlight shot)
        {
            Vector3 start = _units.TryMuzzle(shot.Attacker, shot.WeaponIndex, shot.Serial, out Vector3 muzzle) ? muzzle : ShotPoint(shot.Attacker);
            track.Serial = shot.Serial;
            track.Previous = start;
            track.Current = ShotWorld(shot);
            track.Shown = start;
            track.LastPuff = start;
        }

        /// <summary>Where a shot is drawn this frame (for tests). False if there is no such shot on the track.</summary>
        public bool TryShownShot(int serial, out Vector3 place)
        {
            ref ShotTrack track = ref _tracks[serial & (TrackSize - 1)];
            place = track.Shown;
            return track.Serial == serial;
        }

        private void DrawShot(in ShotInFlight shot)
        {
            WeaponLook look = LookOf(shot.Attacker, shot.WeaponIndex);
            ref ShotTrack track = ref _tracks[shot.Serial & (TrackSize - 1)];
            if (track.Serial != shot.Serial) StartTrack(ref track, shot);
            Vector3 world = Vector3.LerpUnclamped(track.Previous, track.Current, _battle.IsPlaying ? _battle.Clock.Alpha : 1f);
            track.Shown = world;

            Vector3 moved = track.Current - track.Previous;
            Vector3 direction = moved.sqrMagnitude > 1e-6f ? moved.normalized : Vector3.forward;
            if (moved.sqrMagnitude <= 1e-6f && shot.AimedAt != null)
            {
                Vector3 toward = _units.AimPoint(shot.AimedAt, shotHeight) - world;
                if (toward.sqrMagnitude > 1e-6f) direction = toward.normalized;
            }

            float read = _kit.ReadScale;
            switch (look.Kind)
            {
                case WeaponFeelKind.Gauss:
                    return; // Already a line from muzzle to target.
                case WeaponFeelKind.PlasmaHeavy:
                case WeaponFeelKind.Plasma when look.Ball:
                case WeaponFeelKind.Bomb when look.Ball:
                case WeaponFeelKind.Cannon when look.Ball:
                {
                    // A big glowing ball with a white heart, dripping light.
                    float size = look.ShotWidth * Mathf.Max(1f, read * 0.8f);
                    float pulse = 1f + 0.12f * Mathf.Sin(_kit.BattleTime * 26f + shot.Serial);
                    _kit.HeadGlow(world, size * 2.3f * pulse, new Color(look.Shot.r, look.Shot.g, look.Shot.b, 0.65f));
                    _kit.HeadGlow(world, size * 0.9f, Color.Lerp(look.Shot, Color.white, 0.65f));
                    int drips = TrailPuffsDue(track.LastPuff, world, 1.2f, out Vector3 step);
                    for (int p = 1; p <= drips; p++)
                        _kit.Orb(EffectPriority.Small, track.LastPuff + step * p + Random.insideUnitSphere * size * 0.3f, Random.insideUnitSphere * 1.2f + Vector3.down * 0.6f, size * 0.4f, new Color(look.Shot.r, look.Shot.g, look.Shot.b, 0.7f), 0.38f);
                    MoveLastPuff(ref track, world, drips, step);
                    return;
                }
                case WeaponFeelKind.Bio:
                {
                    _kit.HeadStreak(world, direction, 1.1f * read, look.ShotWidth * read, look.Shot);
                    _kit.HeadGlow(world, 0.9f * read, new Color(0.65f, 0.9f, 0.3f, 0.55f));
                    int motes = TrailPuffsDue(track.LastPuff, world, 1.2f, out Vector3 step);
                    for (int p = 1; p <= motes; p++)
                        _kit.Spore(EffectPriority.Small, track.LastPuff + step * p, Random.insideUnitSphere * 0.8f, 0.35f * read, EffectRecipes.SporeGreen, 0.9f);
                    MoveLastPuff(ref track, world, motes, step);
                    return;
                }
                case WeaponFeelKind.Rocket:
                {
                    // A short hot flame with a wide glow, on a thick pale trail that shows from the game's camera.
                    _kit.HeadStreak(world, direction, 1.5f * read, look.ShotWidth * read, look.Shot);
                    _kit.HeadGlow(world, 1.9f * read, new Color(look.Shot.r, look.Shot.g, look.Shot.b, 0.8f));
                    _kit.HeadGlow(world, 0.7f * read, Color.Lerp(look.Shot, Color.white, 0.6f));
                    if (!look.SmokeTrail) return;
                    int puffs = TrailPuffsDue(track.LastPuff, world, 1f * read, out Vector3 step);
                    // It rises a little, or the soft edge against the ground eats the trail of a rocket fired low.
                    for (int p = 1; p <= puffs; p++)
                        _kit.Smoke(EffectPriority.Medium, track.LastPuff + step * p - direction * 0.4f, Random.insideUnitSphere * 0.3f + Vector3.up * 1.6f, Random.Range(2.3f, 3.1f) * read, RocketSmoke, Random.Range(1f, 1.6f));
                    MoveLastPuff(ref track, world, puffs, step);
                    return;
                }
                case WeaponFeelKind.Cannon:
                {
                    // A long thin tracer, nearly white, with hardly any glow: a shell is fast and straight.
                    _kit.HeadStreak(world, direction, 4f * read, look.ShotWidth * 0.8f * read, Color.Lerp(look.Shot, Color.white, 0.45f));
                    _kit.HeadGlow(world, 0.7f * read, new Color(look.Shot.r, look.Shot.g, look.Shot.b, 0.5f));
                    if (!look.SmokeTrail) return;
                    int puffs = TrailPuffsDue(track.LastPuff, world, 2.2f * read, out Vector3 step);
                    for (int p = 1; p <= puffs; p++)
                        _kit.Smoke(EffectPriority.Small, track.LastPuff + step * p - direction * 0.4f, Random.insideUnitSphere * 0.3f, 0.5f * read, EffectRecipes.SmokeThin, 0.5f);
                    MoveLastPuff(ref track, world, puffs, step);
                    return;
                }
                case WeaponFeelKind.Bomb:
                {
                    // A short fat shape with a slow pulse, coming down: the one to watch for. A fire bomb glows redder than a rocket.
                    float pulse = 0.75f + 0.25f * Mathf.Sin(_kit.BattleTime * 15f + shot.Serial);
                    Color glow = look.SmokeTrail ? new Color(look.Shot.r, look.Shot.g * 0.55f, look.Shot.b * 0.55f, 0.8f) : new Color(look.Shot.r, look.Shot.g, look.Shot.b, 0.7f);
                    _kit.HeadStreak(world, direction, 1.5f * read, look.ShotWidth * 1.5f * read, look.Shot);
                    _kit.HeadGlow(world, 1.6f * read * pulse, glow);
                    if (!look.SmokeTrail) return;
                    int puffs = TrailPuffsDue(track.LastPuff, world, 1.8f * read, out Vector3 step);
                    for (int p = 1; p <= puffs; p++)
                        _kit.Smoke(EffectPriority.Small, track.LastPuff + step * p - direction * 0.4f, Random.insideUnitSphere * 0.3f, 0.7f * read, EffectRecipes.SmokeThin, 0.6f);
                    MoveLastPuff(ref track, world, puffs, step);
                    return;
                }
                default:
                    _kit.HeadStreak(world, direction, 3f * read, look.ShotWidth * read, look.Shot);
                    return;
            }
        }

        /// <summary>
        /// How many puffs a trail owes between its last puff and where the shot is now, and the step between them, so that a trail is spaced by
        /// distance at any frame rate and battle speed. Never more than MostTrailPuffsAtOnce.
        /// </summary>
        public static int TrailPuffsDue(Vector3 lastPuff, Vector3 now, float spacing, out Vector3 step)
        {
            Vector3 way = now - lastPuff;
            float distance = way.magnitude;
            if (distance < spacing)
            {
                step = Vector3.zero;
                return 0;
            }
            step = way / distance * spacing;
            return Mathf.Min((int)(distance / spacing), MostTrailPuffsAtOnce);
        }

        private static void MoveLastPuff(ref ShotTrack track, Vector3 now, int puffs, Vector3 step)
        {
            if (puffs == 0) return;
            track.LastPuff = puffs >= MostTrailPuffsAtOnce ? now : track.LastPuff + step * puffs;
        }

        /// <summary>A shot has reached the end of its flight: its impact, and a blast as wide as its splash.</summary>
        private void Land(in ShotInFlight shot)
        {
            WeaponStats weapon = shot.Attacker.Weapons[shot.WeaponIndex].Stats;
            WeaponLook look = LookOf(shot.Attacker, shot.WeaponIndex);
            ref ShotTrack track = ref _tracks[shot.Serial & (TrackSize - 1)];
            bool known = track.Serial == shot.Serial;
            bool airborne = shot.AimedAt != null && shot.AimedAt.IsAir && shot.ArcLength <= 0f;

            Vector3 at = new Vector3(shot.Position.X, shotHeight, shot.Position.Y);
            if (known) at = track.Current;
            if (airborne) at.y = _units.AimPoint(shot.AimedAt, shotHeight).y;
            else if (shot.ArcLength > 0f) at.y = 0.3f; // Lobbed: it comes down on the ground, hit or miss.
            else if (shot.AimedAt != null) at.y = Mathf.Min(at.y, _units.AimPoint(shot.AimedAt, shotHeight).y);

            if (weapon.SplashRadius > 0f)
            {
                _recipes.Blast(at, weapon.SplashRadius, look.Blast, look.Shot, EffectPriority.Big);
                if (weapon.SplashRadius >= 3f) CameraShake.Kick(at, 0.05f + weapon.SplashRadius * 0.015f);
                return;
            }
            SimUnit target = shot.AimedAt;
            Vector3 direction = known ? (at - track.Previous).normalized : Vector3.down;
            _recipes.Impact(at, direction, look.Impact, target != null ? target.Stats.ArmorType : ArmorType.Unarmored, target != null && target.Shield > 0f, look.Shot);
        }

        /// <summary>Where a shot is at this tick, with its arc if it is lobbed.</summary>
        private Vector3 ShotWorld(in ShotInFlight shot)
        {
            float arc = 4f * lobHeightShare * shot.ArcLength * shot.Progress * (1f - shot.Progress);
            return ShotPosition(shot) + Vector3.up * arc;
        }

        /// <summary>A shot starts at its muzzle and closes in on where the simulation says it is, at the height of its target.</summary>
        private Vector3 ShotPosition(in ShotInFlight shot)
        {
            float left = 1f - shot.Progress;
            float endHeight = shot.AimedAt != null ? _units.AimPoint(shot.AimedAt, shotHeight).y : shotHeight;
            float startHeight = ShotPoint(shot.Attacker).y;
            float x = shot.Position.X, z = shot.Position.Y;
            if (_units.TryMuzzle(shot.Attacker, shot.WeaponIndex, shot.Serial, out Vector3 muzzle))
            {
                startHeight = muzzle.y;
                x += (muzzle.x - shot.Attacker.Position.X) * left;
                z += (muzzle.z - shot.Attacker.Position.Y) * left;
            }
            return new Vector3(x, Mathf.Lerp(startHeight, endHeight, shot.Progress), z);
        }

        // ------------------------------------------------------------------ beams

        private void DrawBeams(float battleDelta)
        {
            if (_shown != null) _shown.GetBeams(_beams);
            else _beams.Clear();
            for (int i = 0; i < _beams.Count && i < _beamEmit.Length; i++) DrawBeam(i, _beams[i], battleDelta);
        }

        private void DrawBeam(int index, in BeamShot beam, float battleDelta)
        {
            WeaponLook look = LookOf(beam.Attacker, beam.WeaponIndex);
            if (!_units.TryMuzzle(beam.Attacker, beam.WeaponIndex, 0, out Vector3 from))
            {
                from = _units.WorldPosition(beam.Attacker);
                if (!beam.Attacker.IsAir) from.y = beamOriginHeight;
            }
            Vector3 to = new Vector3(beam.Point.X, 0.12f, beam.Point.Y);
            float read = Mathf.Max(1f, _kit.ReadScale * 0.8f);
            Color color = look.Shot;
            float time = _kit.BattleTime;

            if (!beam.Firing)
            {
                // Charging: a thin wavering line, light gathering at the muzzle, the target spot growing to what it will hit.
                float charge = Mathf.Clamp01(beam.Charge);
                float flicker = 0.75f + 0.25f * Mathf.Sin(time * 45f);
                _kit.Line(from, to, Mathf.Lerp(0.08f, 0.5f, charge) * read * flicker, new Color(color.r, color.g, color.b, 0.35f + 0.5f * charge));
                _kit.HeadGlow(from, Mathf.Lerp(0.8f, 4.5f, charge * charge) * read, new Color(color.r, color.g, color.b, 0.8f));
                _kit.HeadGlow(from, Mathf.Lerp(0.3f, 1.6f, charge) * read, Color.white);
                _kit.GroundHead(to, beam.Radius * 2.6f * charge, new Color(color.r, color.g, color.b, 0.75f));
                _beamEmit[index] += battleDelta * 26f;
                while (_beamEmit[index] >= 1f)
                {
                    _beamEmit[index] -= 1f;
                    Vector3 away = Random.onUnitSphere * Random.Range(2f, 4f);
                    _kit.Orb(EffectPriority.Medium, from + away, -away * 2.4f, 0.3f * read, color, 0.4f);
                }
                _beamLastScorch[index] = to;
                return;
            }

            // Firing: a thick glowing beam with a white core, on a burning spot that throws sparks and fire.
            float pulse = 0.88f + 0.12f * Mathf.Sin(time * 38f) * Mathf.Sin(time * 23f);
            _kit.Line(from, to, look.ShotWidth * 3.6f * pulse * read, new Color(color.r, color.g, color.b, 0.45f));
            _kit.Line(from, to, look.ShotWidth * 1.8f * pulse * read, new Color(color.r, color.g, color.b, 0.9f));
            _kit.Line(from, to, look.ShotWidth * 0.8f * pulse * read, new Color(0.92f, 0.98f, 1f, 1f));
            _kit.HeadGlow(from, 5f * pulse * read, color);
            _kit.HeadGlow(to + Vector3.up * 0.6f, beam.Radius * 3.4f * pulse, new Color(1f, 0.88f, 0.65f, 0.95f));
            _kit.GroundHead(to, beam.Radius * 4.5f * pulse, new Color(1f, 0.52f, 0.16f, 1f));

            _beamEmit[index] += battleDelta * 60f;
            while (_beamEmit[index] >= 1f)
            {
                _beamEmit[index] -= 1f;
                float roll = Random.value;
                Vector2 around = Random.insideUnitCircle * beam.Radius;
                Vector3 spot = to + new Vector3(around.x, 0.2f, around.y);
                if (roll < 0.55f) _kit.Spark(EffectPriority.Big, spot, Random.onUnitSphere * 9f + Vector3.up * 11f, 0.085f * _kit.ReadScale, EffectRecipes.SparkHot, Random.Range(0.35f, 0.9f));
                else if (roll < 0.85f) _kit.Fire(EffectPriority.Big, spot, Vector3.up * Random.Range(3f, 8f) + Random.insideUnitSphere * 2f, beam.Radius * Random.Range(0.6f, 1f), Random.Range(0.35f, 0.7f));
                else _kit.Smoke(EffectPriority.Medium, spot, Vector3.up * Random.Range(3f, 6f), beam.Radius * Random.Range(1f, 1.5f), EffectRecipes.SmokeDark, Random.Range(1.2f, 2.4f));
            }
            if ((to - _beamLastScorch[index]).sqrMagnitude > 1.5f)
            {
                _beamLastScorch[index] = to;
                _kit.Scorch(EffectPriority.Medium, to, beam.Radius * 2.2f, EffectRecipes.ScorchBlack, 35f);
            }
        }

        // ------------------------------------------------------------------ dust

        /// <summary>
        /// How many bodies to look at for dust this frame, so that each is looked at once per DustLookSeconds of battle time at any frame rate and any speed.
        /// due carries the part of a body left over to the next frame. Never more than all of them in one frame.
        /// </summary>
        public static int DustLooksThisFrame(float battleDelta, int bodies, ref float due)
        {
            due += battleDelta / DustLookSeconds * bodies;
            int looks = (int)due;
            due -= looks;
            return Mathf.Min(looks, bodies);
        }

        /// <summary>Vehicles and hovering craft kick up dust while they move; infantry a little now and then. The bodies take turns, spread over the time between looks.</summary>
        private void KickUpDust(float battleDelta)
        {
            if (_shown == null) return;
            IReadOnlyList<SimUnit> units = _shown.Units;
            int looks = DustLooksThisFrame(battleDelta, units.Count, ref _dustDue);
            for (int look = 0; look < looks; look++)
            {
                _dustNext = (_dustNext + 1) % units.Count;
                SimUnit unit = units[_dustNext];
                if (!unit.IsAlive || unit.State != UnitState.Move || unit.IsAir || unit.IsLeaping) continue;
                if (unit.Velocity.LengthSquared < 0.25f) continue;
                float radius = unit.Radius;
                var base3 = new Vector3(unit.Position.X, 0.25f, unit.Position.Y);
                if (unit.Stats.ArmorType != ArmorType.Unarmored || radius >= 1f)
                {
                    Vector2 off = Random.insideUnitCircle * radius * 0.7f;
                    _kit.Dust(EffectPriority.Small, base3 + new Vector3(off.x, 0f, off.y), Vector3.up * 0.5f, radius * Random.Range(1.2f, 1.9f), new Color(0.6f, 0.55f, 0.46f, 0.22f), Random.Range(0.8f, 1.3f));
                }
                else if (Random.value < 0.08f)
                {
                    _kit.Dust(EffectPriority.Small, base3, Vector3.up * 0.3f, 0.9f, new Color(0.6f, 0.55f, 0.46f, 0.16f), 0.6f);
                }
            }
        }

        private Vector3 ShotPoint(SimUnit unit)
        {
            Vector3 position = _units.WorldPosition(unit);
            if (!unit.IsAir) position.y = shotHeight;
            return position;
        }
    }
}
