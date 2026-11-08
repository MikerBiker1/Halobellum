// TowerBuilder.cs
// Where a side's towers stand and what they are at a level (D55), in one place for anything that puts towers on a field.
// A tower's health doubles with each level, and its shockwave gets shorter.
// Depends on: MatchRules, PlayerState, Match (TowerName), Halobellum.Core.

using System;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public static class TowerBuilder
    {
        /// <summary>Gives a side its Command Center on the left and its Research Center on the right, in the middle of its zone (none if the rules have no towers).</summary>
        public static void AddTo(PlayerState player, MatchRules rules, CombatRules combat)
        {
            if (!rules.HasTowers) return;
            int squares = rules.TowerSquares;
            int left = combat.ArenaWidthSquares / 4 - squares / 2;
            int row = (combat.DeploymentDepthSquares - squares) / 2;
            player.Towers.Add(new PlacedTower { Kind = TowerKind.CommandCenter, Column = left, Row = row, Squares = squares });
            player.Towers.Add(new PlacedTower { Kind = TowerKind.ResearchCenter, Column = combat.ArenaWidthSquares - left - squares, Row = row, Squares = squares });
        }

        /// <summary>A tower's stats at a level.</summary>
        public static UnitStats MakeStats(MatchRules rules, TowerKind kind, int level)
        {
            return new UnitStats
            {
                Name = Match.TowerName(kind), Role = UnitRole.Tank, SquadSize = 1, MaxHealth = rules.TowerBaseHealth * MathF.Pow(2f, level - 1),
                ArmorType = ArmorType.Heavy, CollisionRadius = rules.TowerRadius, ImmuneToInfection = true, TargetFilter = TargetFilter.Ground,
                Shockwave = new ShockwaveStats
                {
                    Kind = kind, Seconds = rules.TowerShockwaveSeconds[level - 1], SpeedMultiplier = rules.ShockwaveSpeedMultiplier,
                    DamageMultiplier = rules.ShockwaveDamageMultiplier, FireRateMultiplier = rules.ShockwaveFireRateMultiplier,
                    DamageTakenMultiplier = rules.ShockwaveDamageTakenMultiplier,
                },
            };
        }
    }
}
