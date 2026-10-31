// TestRules.cs
// Builds a CombatRules object for tests with the Bible's starting values (Section 6 damage table,
// 30 ticks per second, 0.5 second retarget, 5 second shield delay, 90 second battles).
// The game itself reads these from Data/CombatRules.asset; CombatRulesAssetTests checks that the
// asset and this fixture agree.
// Depends on: Halobellum.Core.

using Halobellum.Core;

namespace Halobellum.Tests
{
    public static class TestRules
    {
        public static CombatRules Default()
        {
            var rules = new CombatRules
            {
                TicksPerSecond = 30,
                RetargetIntervalSeconds = 0.5f,
                ShieldRegenDelaySeconds = 5f,
                BattleTimeLimitSeconds = 90f,
                ArenaWidth = 120f,
                ArenaLength = 160f,
                SpatialCellSize = 4f,
                SeparationPadding = 0.2f,
                SeparationWeight = 1f,
                AvoidanceWeight = 1.5f,
                AvoidanceLookaheadSeconds = 0.75f,
                CollisionIterations = 3,
            };
            DamageTable t = rules.DamageTable;
            t.SetRow(DamageType.Ballistic, 1.0f, 0.6f, 0.25f);
            t.SetRow(DamageType.Explosive, 1.0f, 1.0f, 1.0f);
            t.SetRow(DamageType.Energy, 1.0f, 1.0f, 0.75f);
            t.SetRow(DamageType.Fire, 1.5f, 0.8f, 0.3f);
            t.SetRow(DamageType.Melee, 1.0f, 0.7f, 0.3f);
            t.SetAntiArmorRow(0.5f, 1.2f, 1.5f);
            return rules;
        }
    }
}
