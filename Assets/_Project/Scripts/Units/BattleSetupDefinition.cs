// BattleSetupDefinition.cs
// Data asset describing one test battle: the squads of each army, the seed, and how the armies
// are laid out. The battle scene plays it, and the headless runner can run the exact same battle
// (Halobellum > Headless Runner > Run Selected Battle Setup), so both results can be compared.
// The layout values are test-battle settings; real deployment (placing squads on the grid) comes
// in Milestone 3. Create one with right-click > Create > Halobellum > Battle Setup.
// Depends on: UnitDefinition, Halobellum.Combat (ArmySetup, RunnerSettings), Halobellum.Core, UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Battle Setup", fileName = "NewBattleSetup")]
    public sealed class BattleSetupDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class SquadEntry
        {
            public UnitDefinition unit;
            [Tooltip("Number of squads, laid out automatically in rows. Ignored when Positions is filled in.")]
            [Min(0)] public int squads = 1;
            [Tooltip("Optional: one squad at each position, in deployment squares. X = across the field (0 = centre, -30 to 30), Y = squares behind the front edge of the army's deployment zone (0 to 30). Team B is mirrored.")]
            public List<Vector2> positions = new List<Vector2>();
        }

        [Header("Team A (bottom, the player's side)")]
        public string armyAName = "Army A";
        [Tooltip("Placed in this order, front row first.")]
        public List<SquadEntry> armyA = new List<SquadEntry>();

        [Header("Team B (top)")]
        public string armyBName = "Army B";
        [Tooltip("Placed in this order, front row first.")]
        public List<SquadEntry> armyB = new List<SquadEntry>();

        [Header("Battle")]
        [Tooltip("Same seed and armies = same battle, in the scene and in the headless runner.")]
        public int seed;

        [Header("Test layout (metres)")]
        [Tooltip("Each body's start position is nudged by up to this distance (seeded).")]
        public float positionJitter;
        [Tooltip("Distance from the centre line to each army's front row.")]
        public float frontLineDistance;
        [Tooltip("Gap between neighbouring squads, sideways and between rows.")]
        public float squadGap;

        /// <param name="sources">If given, records which unit asset each squad's stats came from (the scene uses it to find prefabs).</param>
        public ArmySetup BuildArmyA(Dictionary<UnitStats, UnitDefinition> sources = null) => BuildArmy(armyAName, armyA, sources);
        public ArmySetup BuildArmyB(Dictionary<UnitStats, UnitDefinition> sources = null) => BuildArmy(armyBName, armyB, sources);

        public RunnerSettings BuildLayoutSettings()
        {
            return new RunnerSettings
            {
                Battles = 1,
                BaseSeed = seed,
                PositionJitter = positionJitter,
                FrontLineDistance = frontLineDistance,
                SquadGap = squadGap,
            };
        }

        private static ArmySetup BuildArmy(string name, List<SquadEntry> entries, Dictionary<UnitStats, UnitDefinition> sources)
        {
            var army = new ArmySetup(string.IsNullOrEmpty(name) ? "Unnamed army" : name);
            foreach (SquadEntry entry in entries)
            {
                if (entry == null || entry.unit == null) continue;
                UnitStats stats = entry.unit.ToStats();
                if (entry.positions != null && entry.positions.Count > 0)
                {
                    var positions = new List<Vec2>(entry.positions.Count);
                    foreach (Vector2 p in entry.positions) positions.Add(new Vec2(p.x, p.y));
                    army.AddAt(stats, positions);
                }
                else if (entry.squads > 0)
                {
                    army.Add(stats, entry.squads);
                }
                else
                {
                    continue;
                }
                if (sources != null) sources[stats] = entry.unit;
            }
            return army;
        }
    }
}
