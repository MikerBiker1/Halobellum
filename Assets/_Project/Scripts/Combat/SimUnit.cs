// SimUnit.cs
// The live state of one body during a battle: position, facing, health, shield, current state,
// targets and weapon cooldowns. One SimUnit exists per body (an 18-Marine squad is 18 SimUnits).
// Units use the simple state machine from Bible Section 4, rule 7: Idle, Move, Attack, Dead.
// Depends on: Halobellum.Core (UnitStats, WeaponStats, Vec2, Team).

using Halobellum.Core;

namespace Halobellum.Combat
{
    public enum UnitState
    {
        /// <summary>Alive but has nothing it can reach or attack.</summary>
        Idle = 0,
        /// <summary>Walking or flying toward the closest enemy.</summary>
        Move = 1,
        /// <summary>Standing still and firing at a target in range.</summary>
        Attack = 2,
        Dead = 3,
    }

    /// <summary>Live state of one weapon on a unit.</summary>
    public sealed class SimWeapon
    {
        public readonly WeaponStats Stats;
        /// <summary>The enemy this weapon is shooting at, or null.</summary>
        public SimUnit Target;
        /// <summary>Seconds until this weapon can fire again.</summary>
        public float Cooldown;

        public SimWeapon(WeaponStats stats)
        {
            Stats = stats;
        }
    }

    public sealed class SimUnit
    {
        /// <summary>Unique number within one battle. Also used to break ties so results are repeatable.</summary>
        public readonly int Id;
        public readonly Team Team;
        /// <summary>Which squad this body belongs to (-1 if none).</summary>
        public readonly int SquadId;
        public readonly UnitStats Stats;
        public readonly SimWeapon[] Weapons;

        public Vec2 Position;
        /// <summary>Direction the unit faces (length 1). Used by directional defense.</summary>
        public Vec2 Facing;
        /// <summary>Velocity applied in the last tick, in metres per second.</summary>
        public Vec2 Velocity;
        /// <summary>True while a fighting unit edges toward its target to make room for a friend stuck behind it (D30).</summary>
        public bool Advancing;
        /// <summary>Sideways step asked of this unit by faster friendly units coming through (D30), rebuilt every tick.</summary>
        public Vec2 YieldSteer;

        public float Health;
        public float Shield;
        public float SecondsSinceDamaged;

        public UnitState State;
        /// <summary>The point the unit is walking toward while in the Move state.</summary>
        public Vec2 MoveGoal;
        /// <summary>The closest enemy found at the last target evaluation, which the unit walks toward.</summary>
        public SimUnit MoveTarget;
        /// <summary>Seconds until the next target re-evaluation.</summary>
        public float RetargetTimer;
        /// <summary>Set by the movement system when this body touched a body of another squad this tick.</summary>
        public bool TouchedOtherSquad;
        /// <summary>True once the unit has entered the enemy's deployment zone: it then notices enemies at any distance.</summary>
        public bool UnlimitedAcquisition;
        /// <summary>True once this unit's mid-battle reinforcement (if it has one) has been dropped.</summary>
        public bool ReinforcementDropped;

        // Battle statistics for results and later features (XP from kills, result screen).
        public float DamageDealt;
        public int Kills;
        /// <summary>Id of the unit that dealt the killing blow, or -1.</summary>
        public int KilledById = -1;

        public SimUnit(int id, Team team, int squadId, UnitStats stats, Vec2 position, Vec2 facing)
        {
            Id = id;
            Team = team;
            SquadId = squadId;
            Stats = stats;
            Position = position;
            Facing = facing.LengthSquared > 0f ? facing.Normalized : Vec2.Up;
            Health = stats.MaxHealth;
            Shield = stats.MaxShield;
            SecondsSinceDamaged = 0f;
            State = UnitState.Idle;
            MoveGoal = position;

            int weaponCount = stats.Weapons?.Length ?? 0;
            Weapons = new SimWeapon[weaponCount];
            for (int i = 0; i < weaponCount; i++) Weapons[i] = new SimWeapon(stats.Weapons[i]);
        }

        public bool IsAlive => State != UnitState.Dead;
        public bool IsAir => Stats.IsAir;
        public float Radius => Stats.CollisionRadius;

        public override string ToString() => $"{Stats.Name}#{Id} ({Team}) {State} hp={Health:0.#} at {Position}";
    }
}
