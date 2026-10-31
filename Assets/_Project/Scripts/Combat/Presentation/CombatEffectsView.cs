// CombatEffectsView.cs
// Shows who is shooting whom, so fights are readable (Bible pillar "Readable battles"):
//   - instant-hit weapons draw a short tracer from the shooter to the target (WeaponFired event);
//   - projectile weapons show a small sphere for every projectile in flight.
// Tracers and projectile spheres come from fixed-size pools, so nothing is created during battle.
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

        private BattleController _battle;
        private UnitViewManager _units;
        private Transform[] _tracers;
        private float[] _tracerTimeLeft;
        private int _nextTracer;
        private Transform[] _projectiles;
        private readonly List<Vec2> _projectilePositions = new List<Vec2>();
        private Material _tracerA, _tracerB;

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _units = GetComponent<UnitViewManager>();
            if (_battle.Simulation == null) { enabled = false; return; }

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

            _battle.Simulation.Events.WeaponFired += OnWeaponFired;
        }

        private void OnDestroy()
        {
            if (_battle != null && _battle.Simulation != null) _battle.Simulation.Events.WeaponFired -= OnWeaponFired;
        }

        private void OnWeaponFired(SimUnit attacker, int weaponIndex, SimUnit target)
        {
            if (!attacker.Weapons[weaponIndex].Stats.IsInstant) return; // Projectiles are drawn in LateUpdate.

            Vector3 from = ShotPoint(attacker);
            Vector3 to = ShotPoint(target);
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

            _battle.Simulation.GetProjectilePositions(_projectilePositions);
            for (int i = 0; i < _projectiles.Length; i++)
            {
                bool active = i < _projectilePositions.Count;
                if (active)
                {
                    Vec2 p = _projectilePositions[i];
                    _projectiles[i].position = new Vector3(p.X, shotHeight, p.Y);
                }
                if (_projectiles[i].gameObject.activeSelf != active) _projectiles[i].gameObject.SetActive(active);
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
