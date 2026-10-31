// BattleController.cs
// Runs one battle in the scene. On Awake it builds the BattleSimulation from the Battle Setup and
// Combat Rules data assets, using the same HeadlessBattleRunner.CreateBattle as the headless
// runner, so the same setup and seed give the same battle in both. Each frame it asks the
// BattleClock how many fixed ticks to run (speed 0 = paused, 1x, 2x, 4x) and steps the simulation.
// It never moves units itself: other components (UnitViewManager, CombatEffectsView, the HUD)
// read the simulation and subscribe to its events.
// Depends on: Halobellum.Combat (BattleSimulation, BattleClock, HeadlessBattleRunner),
//             Halobellum.Units (CombatRulesDefinition, BattleSetupDefinition), UnityEngine.

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

        /// <summary>Battle speed: 0 pauses, 1, 2 or 4 speed it up.</summary>
        public float Speed
        {
            get => Clock?.Speed ?? 0f;
            set { if (Clock != null) Clock.Speed = Mathf.Max(0f, value); }
        }

        /// <summary>Time spent stepping the simulation this frame, in milliseconds (for the benchmark).</summary>
        public float LastFrameSimulationMilliseconds { get; private set; }

        /// <summary>Raised once when the battle ends, with its result.</summary>
        public event Action<BattleResult> BattleFinished;

        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private readonly Dictionary<UnitStats, UnitDefinition> _unitSources = new Dictionary<UnitStats, UnitDefinition>();

        /// <summary>The prefab set on this unit type's data asset, or null to use a placeholder primitive.</summary>
        public GameObject PrefabFor(UnitStats stats)
        {
            return stats != null && _unitSources.TryGetValue(stats, out UnitDefinition definition) ? definition.prefab : null;
        }

        private void Awake()
        {
            if (combatRules == null || battleSetup == null)
            {
                Debug.LogError("BattleController needs a Combat Rules asset and a Battle Setup asset.", this);
                enabled = false;
                return;
            }

            Application.runInBackground = true; // Keep playing when the window loses focus.
            PlaceholderArt.SetBaseMaterial(placeholderMaterial);
            Rules = combatRules.ToRules();
            ArmySetup armyA = battleSetup.BuildArmyA(_unitSources);
            ArmySetup armyB = battleSetup.BuildArmyB(_unitSources);
            if (!IsValid(armyA) || !IsValid(armyB))
            {
                enabled = false;
                return;
            }

            Simulation = HeadlessBattleRunner.CreateBattle(Rules, armyA, armyB, battleSetup.BuildLayoutSettings(), battleSetup.seed);
            Simulation.Events.BattleEnded += OnBattleEnded;
            Clock = new BattleClock(Rules.TickSeconds, maxTicksPerFrame);
            Debug.Log($"Battle started: {armyA.Name} ({CountBodies(Team.A)} bodies) vs {armyB.Name} ({CountBodies(Team.B)} bodies), seed {battleSetup.seed}.");
        }

        private void Update()
        {
            LastFrameSimulationMilliseconds = 0f;
            if (Simulation == null || Simulation.IsFinished) return;
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
            Debug.Log("Battle finished: " + result.ToSummaryLine());
            BattleFinished?.Invoke(result);
        }

        /// <summary>Living bodies on one team.</summary>
        public int CountAlive(Team team)
        {
            int count = 0;
            foreach (SimUnit unit in Simulation.Units)
            {
                if (unit.Team == team && unit.IsAlive) count++;
            }
            return count;
        }

        private int CountBodies(Team team)
        {
            int count = 0;
            foreach (SimUnit unit in Simulation.Units) if (unit.Team == team) count++;
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
