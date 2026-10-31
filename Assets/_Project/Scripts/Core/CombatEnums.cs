// CombatEnums.cs
// The fixed lists of categories used by unit data and the simulation: armor types, damage types,
// target filters, roles and teams (Bible Sections 6 and 7).
// Depends on: nothing (plain C#). The numeric values are saved in data assets, so only add new
// entries at the end of a list; never reorder or remove them.

namespace Halobellum.Core
{
    /// <summary>Base armor of a unit's body. An energy shield is a separate layer on top.</summary>
    public enum ArmorType
    {
        Unarmored = 0,
        Light = 1,
        Heavy = 2,
    }

    public enum DamageType
    {
        Ballistic = 0,
        Explosive = 1,
        Energy = 2,
        Fire = 3,
        Melee = 4,
    }

    /// <summary>Which enemies a unit is allowed to attack.</summary>
    public enum TargetFilter
    {
        Ground = 0,
        Air = 1,
        Both = 2,
    }

    public enum UnitRole
    {
        Chaff = 0,
        ChaffClear = 1,
        SingleTargetDps = 2,
        Tank = 3,
        Flanker = 4,
        Artillery = 5,
        AntiAir = 6,
        Aerial = 7,
        Titan = 8,
    }

    public enum Team
    {
        A = 0,
        B = 1,
    }

    public static class TeamExtensions
    {
        public static Team Opponent(this Team team) => team == Team.A ? Team.B : Team.A;
    }
}
