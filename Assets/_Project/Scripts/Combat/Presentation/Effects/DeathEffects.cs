// DeathEffects.cs
// What happens when a body dies (M10). The kind of death comes from DeathLooks:
//   infantry fall with a puff (baked bodies play their own death; others tip over and sink);
//   vehicles blow up and stay as blackened wrecks that burn and smoke for a while, then sink away;
//   air units blow up in the air and fall burning to the ground, where they crash;
//   Flood bodies burst in gore and spores;
//   the Infected Pelican trails fire and smoke in its dive, and its crash is the biggest thing on the field.
// Death events are noted and handled in LateUpdate, after UnitViewManager has dealt with them in its own way, and after the
// simulation has said whether the body rises again: such a corpse is left to UnitViewManager. Wrecks and falling bodies run on battle
// time and are capped; past the cap the oldest wreck goes first. The simulation is only read.
// Depends on: EffectRecipes, EffectKit, UnitViewManager, WeaponLooks (DeathLooks), Halobellum.Combat, UnityEngine.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class DeathEffects
    {
        private enum CorpseKind
        {
            /// <summary>Tips over, lies a moment, sinks.</summary>
            Infantry,
            /// <summary>A blackened wreck.</summary>
            Wreck,
            /// <summary>Falling from the sky on fire.</summary>
            Falling,
        }

        private sealed class Corpse
        {
            public Transform Root;
            public CorpseKind Kind;
            public float Age;
            public float Life;
            public Vector3 Start;
            public Quaternion StartRotation;
            public Vector3 SpinAxis;
            public float BodyRadius;
            public float NextPuff;
            public bool Flood;
        }

        private struct Pending
        {
            public SimUnit Unit;
        }

        private const int MostCorpses = 48;
        private const int MostFires = 28;
        private const float WreckSeconds = 14f;
        private const float TitanWreckSeconds = 24f;
        private const float InfantryCorpseSeconds = 2.4f;
        private const float SinkSeconds = 1.6f;
        private const float FallGravity = 15f;
        private const float DivePuffsPerSecond = 60f;
        /// <summary>Where a wreck's hull is, as a share of the height of the whole body with its turret and antennas.</summary>
        private const float HullTopShare = 0.7f;
        private static readonly int TintId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Burnt = new Color(0.13f, 0.12f, 0.11f);

        private struct BurningSpot
        {
            public Vector3 Position;
            public float Until;
            public float BodyRadius;
            public float NextPuff;
            /// <summary>How high the fire starts: on top of a wreck's hull; on the ground if nothing is left standing.</summary>
            public float Top;
        }

        private readonly EffectRecipes _recipes;
        private readonly EffectKit _kit;
        private readonly UnitViewManager _units;
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly HashSet<SimUnit> _rising = new HashSet<SimUnit>();
        private readonly List<Corpse> _corpses = new List<Corpse>();
        private readonly Stack<Corpse> _spareCorpses = new Stack<Corpse>();
        private readonly BurningSpot[] _fires = new BurningSpot[MostFires];
        private int _nextFire;
        private float _divePuffs;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private BattleSimulation _shown;

        /// <summary>Wrecks and falling bodies on the field right now (for tests).</summary>
        public int CorpsesOnField => _corpses.Count;

        public DeathEffects(EffectRecipes recipes, UnitViewManager units)
        {
            _recipes = recipes;
            _kit = recipes.Kit;
            _units = units;
        }

        public void Show(BattleSimulation simulation)
        {
            if (_shown != null)
            {
                _shown.Events.UnitDied -= OnUnitDied;
                _shown.Events.UnitInfected -= OnUnitInfected;
            }
            _shown = simulation;
            _pending.Clear();
            _rising.Clear();
            foreach (Corpse corpse in _corpses) _spareCorpses.Push(corpse);
            _corpses.Clear();
            for (int i = 0; i < MostFires; i++) _fires[i].Until = 0f;
            if (_shown != null)
            {
                _shown.Events.UnitDied += OnUnitDied;
                _shown.Events.UnitInfected += OnUnitInfected;
            }
        }

        private void OnUnitDied(SimUnit unit, SimUnit killer) => _pending.Add(new Pending { Unit = unit });

        private void OnUnitInfected(SimUnit corpse) => _rising.Add(corpse);

        // ------------------------------------------------------------------ every frame

        public void Tick(float battleDelta)
        {
            for (int i = 0; i < _pending.Count; i++) Die(_pending[i].Unit);
            _pending.Clear();
            DiveTrails(battleDelta);
            UpdateCorpses(battleDelta);
            UpdateFires();
        }

        private void Die(SimUnit unit)
        {
            UnitStats stats = unit.Stats;
            Vector3 position = _units.ShownPosition(unit);
            float radius = stats.CollisionRadius;
            bool rises = _rising.Contains(unit);
            DeathStyle style = DeathLooks.For(stats);
            switch (style)
            {
                case DeathStyle.Infantry:
                    _recipes.InfantryDeath(position, radius);
                    if (!rises) Keep(unit, CorpseKind.Infantry, InfantryCorpseSeconds, position, false);
                    break;

                case DeathStyle.Vehicle:
                    bool titan = DeathLooks.IsTitan(stats);
                    _recipes.VehicleDeath(position, radius, titan);
                    if (!rises)
                    {
                        Keep(unit, CorpseKind.Wreck, titan ? TitanWreckSeconds : WreckSeconds, position, false);
                        AddFire(position, radius, titan ? TitanWreckSeconds : WreckSeconds, _units.BodyTopOf(unit) * HullTopShare);
                    }
                    break;

                case DeathStyle.Air:
                    _recipes.AirDeath(position, radius, false);
                    Keep(unit, CorpseKind.Falling, 6f, position, false);
                    break;

                case DeathStyle.FloodAir:
                    _recipes.AirDeath(position, radius, true);
                    Keep(unit, CorpseKind.Falling, 6f, position, true);
                    break;

                case DeathStyle.FloodBurst:
                    _recipes.FloodBurst(position, radius, false);
                    break;

                case DeathStyle.FloodAirBurst:
                    _recipes.FloodBurst(position, radius, true);
                    break;

                case DeathStyle.CrashBurst:
                    _recipes.PelicanCrash(position, radius, stats.DeathBurst.Radius);
                    AddFire(new Vector3(position.x, 0f, position.z), radius * 3f, TitanWreckSeconds);
                    break;
            }

            // A burst weapon that goes off on death (the Carrier Form): a blast as wide as its reach.
            if (style != DeathStyle.CrashBurst && stats.DeathBurst != null && stats.DeathBurst.Radius > 0f)
            {
                bool flood = stats.IsFlood || stats.InfectedFrom != null;
                _recipes.Blast(position + Vector3.up * 0.5f, stats.DeathBurst.Radius, flood ? BlastStyle.Bio : BlastStyle.Fire, WeaponLooks.FloodPlasma, EffectPriority.Big);
                if (!flood) CameraShake.Kick(position, 0.3f);
            }
        }

        /// <summary>The Infected Pelican's dive: fire and smoke all the way down.</summary>
        private void DiveTrails(float battleDelta)
        {
            if (_shown == null || battleDelta <= 0f) return;
            // Enough to look solid, and the same at any frame rate: the part of a puff left over waits for the next frame.
            _divePuffs += battleDelta * DivePuffsPerSecond;
            int puffs = Mathf.Min((int)_divePuffs, 4);
            _divePuffs -= (int)_divePuffs;
            IReadOnlyList<SimUnit> units = _shown.Units;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.HasDived || !unit.IsLeaping || !unit.IsAlive) continue;
                Vector3 position = _units.ShownPosition(unit);
                float radius = unit.Stats.CollisionRadius;
                for (int p = 0; p < puffs; p++) _recipes.BurningTrail(position + Random.insideUnitSphere * radius * 0.4f, radius, EffectPriority.Huge);
                _kit.HeadGlow(position, radius * 2f, new Color(1f, 0.72f, 0.38f, 0.5f));
            }
        }

        // ------------------------------------------------------------------ bodies left on the field

        private void Keep(SimUnit unit, CorpseKind kind, float life, Vector3 position, bool flood)
        {
            Transform root = _units.KeepCorpse(unit);
            if (root == null) return;
            if (_corpses.Count >= MostCorpses) DropOldest();

            Corpse corpse = _spareCorpses.Count > 0 ? _spareCorpses.Pop() : new Corpse();
            corpse.Root = root;
            corpse.Kind = kind;
            corpse.Age = 0f;
            corpse.Life = life;
            corpse.Start = position;
            corpse.StartRotation = root.rotation;
            corpse.SpinAxis = Random.onUnitSphere;
            corpse.BodyRadius = unit.Stats.CollisionRadius;
            corpse.NextPuff = 0f;
            corpse.Flood = flood;
            _corpses.Add(corpse);
            if (kind != CorpseKind.Infantry) Blacken(root);
        }

        private void DropOldest()
        {
            // Wrecks and tipped-over bodies can go at once; something falling is left to land.
            for (int i = 0; i < _corpses.Count; i++)
            {
                if (_corpses[i].Kind == CorpseKind.Falling) continue;
                Remove(i);
                return;
            }
            Remove(0);
        }

        private void Remove(int index)
        {
            Corpse corpse = _corpses[index];
            if (corpse.Root != null) corpse.Root.gameObject.SetActive(false);
            corpse.Root = null;
            _corpses.RemoveAt(index);
            _spareCorpses.Push(corpse);
        }

        private void Blacken(Transform root)
        {
            root.GetComponentsInChildren(false, _renderers);
            foreach (Renderer renderer in _renderers)
            {
                renderer.GetPropertyBlock(_block);
                _block.SetColor(TintId, Burnt);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void UpdateCorpses(float battleDelta)
        {
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                Corpse corpse = _corpses[i];
                if (corpse.Root == null)
                {
                    corpse.Root = null;
                    _corpses.RemoveAt(i);
                    _spareCorpses.Push(corpse);
                    continue;
                }
                corpse.Age += battleDelta;
                switch (corpse.Kind)
                {
                    case CorpseKind.Infantry:
                    {
                        float fall = Mathf.Clamp01(corpse.Age / 0.35f);
                        Vector3 flat = corpse.Start + Vector3.down * Sink(corpse);
                        corpse.Root.SetPositionAndRotation(flat, corpse.StartRotation * Quaternion.Euler(fall * fall * 85f, 0f, 0f));
                        break;
                    }
                    case CorpseKind.Wreck:
                        corpse.Root.position = corpse.Start + Vector3.down * Sink(corpse);
                        break;
                    case CorpseKind.Falling:
                        if (Fall(corpse)) { Remove(i); continue; }
                        break;
                }
                if (corpse.Age >= corpse.Life) Remove(i);
            }
        }

        /// <summary>How far a corpse has sunk: nothing until near the end of its time.</summary>
        private static float Sink(Corpse corpse)
        {
            float sinking = (corpse.Age - (corpse.Life - SinkSeconds)) / SinkSeconds;
            return sinking <= 0f ? 0f : sinking * (Mathf.Max(1.5f, corpse.BodyRadius * 2f));
        }

        /// <summary>Moves a falling body and burns its way down. True once it has hit the ground.</summary>
        private bool Fall(Corpse corpse)
        {
            float age = corpse.Age;
            float height = corpse.Start.y - 0.5f * FallGravity * age * age;
            if (height <= 0.2f)
            {
                _recipes.GroundCrash(new Vector3(corpse.Start.x, 0f, corpse.Start.z), corpse.BodyRadius);
                AddFire(new Vector3(corpse.Start.x, 0f, corpse.Start.z), corpse.BodyRadius, WreckSeconds);
                return true;
            }
            var position = new Vector3(corpse.Start.x, height, corpse.Start.z);
            corpse.Root.SetPositionAndRotation(position, corpse.StartRotation * Quaternion.AngleAxis(age * 130f, corpse.SpinAxis));
            if (age >= corpse.NextPuff)
            {
                corpse.NextPuff = age + 0.035f;
                _recipes.BurningTrail(position, corpse.BodyRadius * 0.75f, EffectPriority.Big);
                if (corpse.Flood) _kit.Spore(EffectPriority.Small, position, Random.insideUnitSphere * 2f, 0.5f * _kit.ReadScale, EffectRecipes.SporeGreen, 1.6f);
            }
            return false;
        }

        // ------------------------------------------------------------------ what goes on burning

        private void AddFire(Vector3 position, float bodyRadius, float seconds, float top = 0f)
        {
            _fires[_nextFire] = new BurningSpot { Position = position, Until = _kit.BattleTime + seconds, BodyRadius = bodyRadius, NextPuff = _kit.BattleTime + 0.6f, Top = top };
            _nextFire = (_nextFire + 1) % MostFires;
        }

        private void UpdateFires()
        {
            float now = _kit.BattleTime;
            for (int i = 0; i < MostFires; i++)
            {
                ref BurningSpot fire = ref _fires[i];
                if (fire.Until <= now || now < fire.NextPuff) continue;
                fire.NextPuff = now + Mathf.Lerp(0.3f, 0.16f, Mathf.Clamp01(fire.BodyRadius / 5f));
                float left = (fire.Until - now) / 4f; // Dies down over the last few seconds.
                float strength = Mathf.Clamp01(left);
                // From the top of the hull: below that the flames are hidden inside it. Flames are read from the game camera too, so they grow with its distance.
                Vector3 spot = fire.Position + new Vector3(Random.Range(-0.6f, 0.6f) * fire.BodyRadius, fire.Top + 0.3f, Random.Range(-0.6f, 0.6f) * fire.BodyRadius);
                float size = fire.BodyRadius * 0.8f + 0.7f;
                float flames = size * Mathf.Max(1f, _kit.ReadScale);
                // A thin column, rising quickly, so that it marks the wreck and doesn't hide what is fighting around it.
                _kit.Smoke(EffectPriority.Small, spot + Vector3.up * 1.2f, Vector3.up * Random.Range(4f, 6.5f) + Random.insideUnitSphere * 0.4f, size * Random.Range(0.9f, 1.3f), new Color(0.17f, 0.165f, 0.16f, 0.42f * strength), Random.Range(1.8f, 2.8f));
                if (strength >= 1f)
                {
                    _kit.Fire(EffectPriority.Small, spot, Vector3.up * Random.Range(2f, 4f), flames * Random.Range(0.7f, 1.15f), Random.Range(0.55f, 0.95f));
                    Vector3 other = fire.Position + new Vector3(Random.Range(-0.6f, 0.6f) * fire.BodyRadius, fire.Top + 0.3f, Random.Range(-0.6f, 0.6f) * fire.BodyRadius);
                    _kit.Fire(EffectPriority.Small, other, Vector3.up * Random.Range(2f, 4f), flames * Random.Range(0.45f, 0.8f), Random.Range(0.45f, 0.8f));
                }
                if (Random.value < 0.15f) _kit.Spark(EffectPriority.Small, spot, Vector3.up * Random.Range(3f, 7f) + Random.insideUnitSphere * 1.5f, 0.05f * _kit.ReadScale, EffectRecipes.SparkHot, Random.Range(0.4f, 0.9f));
            }
        }
    }
}
