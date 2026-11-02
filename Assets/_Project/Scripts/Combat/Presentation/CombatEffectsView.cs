// CombatEffectsView.cs
// Shows who is shooting whom, so fights are readable (Bible pillar "Readable battles"):
//   - instant-hit weapons draw a short tracer from the shooter to the target (WeaponFired event);
//   - projectile weapons show a small sphere for every projectile in flight; lobbed ones fly in an arc;
//   - beam weapons draw a thin line while charging, then a thick beam with a glowing spot on the ground.
// Shots and beams start at the muzzle of the model that fires them, if it has one (D45), and fly from that height to
// the height of what they aim at. The simulation knows only the ground, so this is looks only.
// Tracers, projectile spheres and beams come from fixed-size pools, so nothing is created during battle.
// Tracer lifetime counts in battle time: it pauses with the battle and runs faster at 2x and 4x.
// Depends on: BattleController, UnitViewManager, PlaceholderArt, Halobellum.Combat, UnityEngine.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class CombatEffectsView : MonoBehaviour
    {
        [SerializeField] private Color teamATracerColor = new Color(0.6f, 0.8f, 1f);
        [SerializeField] private Color teamBTracerColor = new Color(1f, 0.7f, 0.4f);
        [SerializeField] private Color projectileColor = new Color(1f, 0.95f, 0.6f);
        [Tooltip("Battle seconds a tracer stays visible.")]
        [SerializeField] private float tracerSeconds = 0.08f;
        [SerializeField] private float tracerWidth = 0.08f;
        [SerializeField] private float projectileSize = 0.45f;
        [Tooltip("Height above the ground where ground units' shots start and land.")]
        [SerializeField] private float shotHeight = 0.9f;
        [SerializeField] private int tracerPoolSize = 400;
        [SerializeField] private int projectilePoolSize = 300;
        [Tooltip("How high a lobbed shot flies at its peak, as a share of the distance it travels.")]
        [SerializeField] private float lobHeightShare = 0.3f;
        [Tooltip("Size of a lobbed shot, bigger than a normal one so its arc can be followed.")]
        [SerializeField] private float lobbedSize = 1.4f;
        [SerializeField] private Color teamABeamColor = new Color(0.45f, 0.75f, 1f);
        [SerializeField] private Color teamBBeamColor = new Color(0.75f, 0.45f, 1f);
        [Tooltip("Height of the beam above a ground unit (it comes out of a big walker's body).")]
        [SerializeField] private float beamOriginHeight = 4f;
        [SerializeField] private float beamChargeWidth = 0.25f;
        [SerializeField] private float beamWidth = 1.4f;
        [SerializeField] private int beamPoolSize = 12;

        private BattleController _battle;
        private UnitViewManager _units;
        private Transform[] _tracers;
        private float[] _tracerTimeLeft;
        private int _nextTracer;
        private int _instantShots;
        private Transform[] _projectiles;
        private readonly List<ShotInFlight> _shots = new List<ShotInFlight>();
        private Transform[] _beams;
        private Transform[] _beamSpots;
        private readonly List<BeamShot> _shownBeams = new List<BeamShot>();
        private Material _beamA, _beamB;
        private Material _tracerA, _tracerB;

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _units = GetComponent<UnitViewManager>();

            var container = new GameObject("Combat Effects").transform;
            _tracerA = PlaceholderArt.MaterialFor(teamATracerColor);
            _tracerB = PlaceholderArt.MaterialFor(teamBTracerColor);
            _tracers = new Transform[tracerPoolSize];
            _tracerTimeLeft = new float[tracerPoolSize];
            for (int i = 0; i < tracerPoolSize; i++)
            {
                _tracers[i] = PlaceholderArt.CreatePart("Tracer", container, PlaceholderArt.Cube, _tracerA);
                _tracers[i].gameObject.SetActive(false);
            }

            Material projectileMaterial = PlaceholderArt.MaterialFor(projectileColor);
            _projectiles = new Transform[projectilePoolSize];
            for (int i = 0; i < projectilePoolSize; i++)
            {
                _projectiles[i] = PlaceholderArt.CreatePart("Projectile", container, PlaceholderArt.Sphere, projectileMaterial);
                _projectiles[i].localScale = Vector3.one * projectileSize;
                _projectiles[i].gameObject.SetActive(false);
            }

            _beamA = PlaceholderArt.MaterialFor(teamABeamColor);
            _beamB = PlaceholderArt.MaterialFor(teamBBeamColor);
            _beams = new Transform[beamPoolSize];
            _beamSpots = new Transform[beamPoolSize];
            for (int i = 0; i < beamPoolSize; i++)
            {
                _beams[i] = PlaceholderArt.CreatePart("Beam", container, PlaceholderArt.Cube, _beamA);
                _beams[i].gameObject.SetActive(false);
                _beamSpots[i] = PlaceholderArt.CreatePart("Beam Spot", container, PlaceholderArt.Sphere, _beamA);
                _beamSpots[i].gameObject.SetActive(false);
            }

            _battle.BattleChanged += OnBattleChanged;
            OnBattleChanged(_battle.Simulation);
        }

        private void OnDestroy()
        {
            if (_shown != null) _shown.Events.WeaponFired -= OnWeaponFired;
            if (_battle != null) _battle.BattleChanged -= OnBattleChanged;
        }

        private BattleSimulation _shown;

        /// <summary>Listens to the newly shown battle and clears effects left from the last one.</summary>
        private void OnBattleChanged(BattleSimulation simulation)
        {
            if (_shown != null) _shown.Events.WeaponFired -= OnWeaponFired;
            _shown = simulation;
            if (_shown != null) _shown.Events.WeaponFired += OnWeaponFired;
            for (int i = 0; i < _tracers.Length; i++)
            {
                _tracerTimeLeft[i] = 0f;
                _tracers[i].gameObject.SetActive(false);
            }
        }

        private void OnWeaponFired(SimUnit attacker, int weaponIndex, SimUnit target)
        {
            WeaponStats weapon = attacker.Weapons[weaponIndex].Stats;
            if (!weapon.IsInstant || weapon.IsBeam) return; // Projectiles and beams are drawn in LateUpdate.

            Vector3 from = _units.TryMuzzle(attacker, weaponIndex, _instantShots++, out Vector3 muzzle) ? muzzle : ShotPoint(attacker);
            Vector3 to = _units.AimPoint(target, shotHeight);
            Vector3 line = to - from;
            if (line.sqrMagnitude < 1e-4f) return;

            int index = _nextTracer;
            _nextTracer = (_nextTracer + 1) % _tracers.Length;
            Transform tracer = _tracers[index];
            tracer.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(line));
            tracer.localScale = new Vector3(tracerWidth, tracerWidth, line.magnitude);
            tracer.GetComponent<MeshRenderer>().sharedMaterial = attacker.Team == Team.A ? _tracerA : _tracerB;
            tracer.gameObject.SetActive(true);
            _tracerTimeLeft[index] = tracerSeconds;
        }

        private void LateUpdate()
        {
            float battleTime = Time.deltaTime * _battle.Speed;
            for (int i = 0; i < _tracers.Length; i++)
            {
                if (_tracerTimeLeft[i] <= 0f) continue;
                _tracerTimeLeft[i] -= battleTime;
                if (_tracerTimeLeft[i] <= 0f) _tracers[i].gameObject.SetActive(false);
            }

            if (_shown != null) _shown.GetProjectiles(_shots);
            else _shots.Clear();
            for (int i = 0; i < _projectiles.Length; i++)
            {
                bool active = i < _shots.Count;
                if (active)
                {
                    ShotInFlight shot = _shots[i];
                    float arc = 4f * lobHeightShare * shot.ArcLength * shot.Progress * (1f - shot.Progress);
                    _projectiles[i].position = ShotPosition(shot) + Vector3.up * arc;
                    _projectiles[i].localScale = Vector3.one * (shot.ArcLength > 0f ? lobbedSize : projectileSize);
                }
                if (_projectiles[i].gameObject.activeSelf != active) _projectiles[i].gameObject.SetActive(active);
            }

            DrawBeams();
        }

        /// <summary>A shot starts at its muzzle and closes in on where the simulation says it is, at the height of its target.</summary>
        private Vector3 ShotPosition(ShotInFlight shot)
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

        private void DrawBeams()
        {
            if (_shown != null) _shown.GetBeams(_shownBeams);
            else _shownBeams.Clear();
            for (int i = 0; i < _beams.Length; i++)
            {
                bool active = i < _shownBeams.Count;
                if (active) DrawBeam(i, _shownBeams[i]);
                if (_beams[i].gameObject.activeSelf != active) _beams[i].gameObject.SetActive(active);
                if (_beamSpots[i].gameObject.activeSelf != active) _beamSpots[i].gameObject.SetActive(active);
            }
        }

        private void DrawBeam(int index, BeamShot beam)
        {
            if (!_units.TryMuzzle(beam.Attacker, beam.WeaponIndex, 0, out Vector3 from))
            {
                from = _units.WorldPosition(beam.Attacker);
                if (!beam.Attacker.IsAir) from.y = beamOriginHeight;
            }
            Vector3 to = new Vector3(beam.Point.X, 0.1f, beam.Point.Y);
            Vector3 line = to - from;
            Material material = beam.Attacker.Team == Team.A ? _beamA : _beamB;

            float width = beam.Firing ? beamWidth : beamChargeWidth * beam.Charge;
            Transform body = _beams[index];
            body.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(line));
            body.localScale = new Vector3(width, width, line.magnitude);
            body.GetComponent<MeshRenderer>().sharedMaterial = material;

            // The spot grows to the area the beam will hit, so the target can be read before it fires.
            float size = beam.Radius * 2f * (beam.Firing ? 1f : beam.Charge);
            Transform spot = _beamSpots[index];
            spot.position = to;
            spot.localScale = new Vector3(size, 0.3f, size);
            spot.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private Vector3 ShotPoint(SimUnit unit)
        {
            Vector3 position = _units.WorldPosition(unit);
            if (!unit.IsAir) position.y = shotHeight;
            return position;
        }
    }
}
