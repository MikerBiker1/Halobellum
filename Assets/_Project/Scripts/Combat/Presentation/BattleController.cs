// BattleController.cs
// Shows and runs battles in the scene. It can get its battle two ways:
//   - Battle scene: a Battle Setup asset is assigned; on Awake it builds that battle with the same
//     HeadlessBattleRunner.CreateBattle as the headless runner, so the same setup and seed give the
//     same battle in both, and plays it at once.
//   - Match scene: no Battle Setup; the MatchController hands over each round's battle with
//     ShowBattle(). During deployment it hands over a preview battle that is shown but not run.
// While a battle is playing, each frame it asks the BattleClock how many fixed ticks to run
// (speed 0 = paused, 1x, 2x, 4x) and steps the simulation. It never moves units itself: other
// components (UnitViewManager, CombatEffectsView, the HUD) read the simulation and its events,
// and rebuild themselves when BattleChanged is raised.
// Depends on: Halobellum.Combat (BattleSimulation, BattleClock, HeadlessBattleRunner),
//             Halobellum.Units (CombatRulesDefinition, BattleSetupDefinition, UnitDefinition), UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Units;
using UnityEngine;

namespace Halobellum.Presentation
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UnitViewManager), typeof(CombatEffectsView), typeof(SquadSelection))]
    [RequireComponent(typeof(BattlefieldView))]
    public sealed class BattleController : MonoBehaviour
    {
        [SerializeField] private CombatRulesDefinition combatRules;
        [Tooltip("Battle scene only: the battle to play. Leave empty in the Match scene.")]
        [SerializeField] private BattleSetupDefinition battleSetup;
        [Tooltip("Most simulation ticks run in one frame. If the PC falls behind, the battle slows down instead of freezing.")]
        [SerializeField] private int maxTicksPerFrame = 8;
        [Tooltip("URP Lit material that all placeholder colors are copied from (Art/Placeholder/PlaceholderLit).")]
        [SerializeField] private Material placeholderMaterial;

        public CombatRulesDefinition CombatRulesAsset => combatRules;
        public BattleSetupDefinition BattleSetupAsset => battleSetup;
        public CombatRules Rules { get; private set; }
        public BattleSimulation Simulation { get; private set; }
        public BattleClock Clock { get; private set; }
        /// <summary>True while the shown battle is being run (false for a deployment preview).</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Battle speed: 0 pauses, 1, 2 or 4 speed it up. Kept from one battle to the next.</summary>
        public float Speed
        {
            get => Clock?.Speed ?? _speed;
            set
            {
                _speed = Mathf.Max(0f, value);
                if (Clock != null) Clock.Speed = _speed;
            }
        }

        /// <summary>Time spent stepping the simulation this frame, in milliseconds (for the benchmark).</summary>
        public float LastFrameSimulationMilliseconds { get; private set; }

        /// <summary>Raised whenever a different battle (or preview) is shown. Views rebuild themselves.</summary>
        public event Action<BattleSimulation> BattleChanged;

        /// <summary>Raised once when the playing battle ends, with its result.</summary>
        public event Action<BattleResult> BattleFinished;

        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private readonly Dictionary<UnitStats, UnitDefinition> _unitSources = new Dictionary<UnitStats, UnitDefinition>();
        private float _speed = 1f;

        /// <summary>The prefab set on this unit type's data asset, or null to use a placeholder primitive.</summary>
        public GameObject PrefabFor(UnitStats stats)
        {
            if (stats == null) return null;
            if (_unitSources.TryGetValue(stats, out UnitDefinition definition)) return definition.prefab;
            // Dropped squads (a Pelican's Marines) are copies of a unit type: find it by name.
            foreach (var pair in _unitSources)
            {
                if (pair.Key.Name == stats.Name) return pair.Value.prefab;
            }
            return null;
        }

        /// <summary>True if this unit type's prefab is a real model that keeps its own materials.</summary>
        public bool KeepsOwnMaterials(UnitStats stats)
        {
            if (stats == null) return false;
            if (_unitSources.TryGetValue(stats, out UnitDefinition definition)) return definition.keepsOwnMaterials;
            foreach (var pair in _unitSources)
            {
                if (pair.Key.Name == stats.Name) return pair.Value.keepsOwnMaterials;
            }
            return false;
        }

        /// <summary>The data asset behind this unit type, or null if there is none.</summary>
        public UnitDefinition DefinitionFor(UnitStats stats)
        {
            if (stats == null) return null;
            if (_unitSources.TryGetValue(stats, out UnitDefinition definition)) return definition;
            foreach (var pair in _unitSources)
            {
                if (pair.Key.Name == stats.Name) return pair.Value;
            }
            return null;
        }

        /// <summary>The baked animated model of this unit type, or null if it has none yet.</summary>
        public BakedBodyDefinition BakedBodyFor(UnitStats stats)
        {
            if (stats == null) return null;
            if (_unitSources.TryGetValue(stats, out UnitDefinition definition)) return definition.bakedBody;
            foreach (var pair in _unitSources)
            {
                if (pair.Key.Name == stats.Name) return pair.Value.bakedBody;
            }
            return null;
        }

        private void Awake()
        {
            Application.runInBackground = true; // Keep playing when the window loses focus.
            if (combatRules == null)
            {
                Debug.LogError("BattleController needs a Combat Rules asset.", this);
                enabled = false;
                return;
            }

            PlaceholderArt.SetBaseMaterial(placeholderMaterial);
            Rules = combatRules.ToRules();
            if (battleSetup != null) StartFromSetup();
        }

        private void StartFromSetup()
        {
            var sources = new Dictionary<UnitStats, UnitDefinition>();
            ArmySetup armyA = battleSetup.BuildArmyA(sources);
            ArmySetup armyB = battleSetup.BuildArmyB(sources);
            if (!IsValid(armyA) || !IsValid(armyB))
            {
                enabled = false;
                return;
            }
            ShowBattle(HeadlessBattleRunner.CreateBattle(Rules, armyA, armyB, battleSetup.BuildLayoutSettings(), battleSetup.seed), sources, play: true);
            Debug.Log($"Battle started: {armyA.Name} ({CountBodies(Team.A)} bodies) vs {armyB.Name} ({CountBodies(Team.B)} bodies), seed {battleSetup.seed}.");
        }

        /// <summary>
        /// Shows a battle. play = true runs it; play = false only displays it (deployment preview).
        /// sources maps each unit type to its data asset, so views can use its prefab.
        /// </summary>
        public void ShowBattle(BattleSimulation simulation, IReadOnlyDictionary<UnitStats, UnitDefinition> sources, bool play)
        {
            if (Simulation != null) Simulation.Events.BattleEnded -= OnBattleEnded;

            _unitSources.Clear();
            if (sources != null) foreach (var pair in sources) _unitSources[pair.Key] = pair.Value;

            Simulation = simulation;
            Clock = new BattleClock(Rules.TickSeconds, maxTicksPerFrame) { Speed = _speed };
            IsPlaying = play && simulation != null;
            if (Simulation != null) Simulation.Events.BattleEnded += OnBattleEnded;
            BattleChanged?.Invoke(Simulation);
        }

        private void Update()
        {
            LastFrameSimulationMilliseconds = 0f;
            if (!IsPlaying || Simulation == null || Simulation.IsFinished) return;
            int ticks = Clock.Advance(Time.deltaTime);
            if (ticks == 0) return;

            _stopwatch.Restart();
            for (int i = 0; i < ticks && !Simulation.IsFinished; i++) Simulation.Step();
            LastFrameSimulationMilliseconds = (float)_stopwatch.Elapsed.TotalMilliseconds;
        }

        private void OnDestroy()
        {
            if (Simulation != null) Simulation.Events.BattleEnded -= OnBattleEnded;
        }

        private void OnBattleEnded(BattleResult result)
        {
            IsPlaying = false;
            Debug.Log("Battle finished: " + result.ToSummaryLine());
            BattleFinished?.Invoke(result);
        }

        /// <summary>Living bodies on one team.</summary>
        public int CountAlive(Team team)
        {
            int count = 0;
            if (Simulation == null) return count;
            IReadOnlyList<SimUnit> units = Simulation.Units; // By index: the HUD calls this every frame, and foreach here would box an enumerator.
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (unit.Team == team && unit.IsAlive && !unit.Stats.IsTower) count++;
            }
            return count;
        }

        /// <summary>Credits' worth of the living bodies on one team (squad cost shared between its bodies).</summary>
        public float AliveValue(Team team)
        {
            float value = 0f;
            if (Simulation == null) return value;
            IReadOnlyList<SimUnit> units = Simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (unit.Team == team && unit.IsAlive) value += unit.Stats.Cost / (float)Math.Max(1, unit.Stats.SquadSize);
            }
            return value;
        }

        private int CountBodies(Team team)
        {
            int count = 0;
            foreach (SimUnit unit in Simulation.Units) if (unit.Team == team && !unit.Stats.IsTower) count++;
            return count;
        }

        private bool IsValid(ArmySetup army)
        {
            if (army.Squads.Count == 0)
            {
                Debug.LogError($"Battle setup '{battleSetup.name}': {army.Name} has no squads.", battleSetup);
                return false;
            }
            foreach (ArmySquad squad in army.Squads)
            {
                List<string> problems = squad.Stats.Validate();
                if (problems.Count == 0) continue;
                Debug.LogError($"Battle setup '{battleSetup.name}': {squad.Stats.Name} has invalid data:\n" + string.Join("\n", problems), battleSetup);
                return false;
            }
            return true;
        }
    }
}
