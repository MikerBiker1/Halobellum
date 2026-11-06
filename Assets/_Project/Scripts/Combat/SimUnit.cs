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

    /// <summary>What a beam weapon is doing (D37).</summary>
    public enum BeamPhase
    {
        Off = 0,
        Charging = 1,
        Firing = 2,
    }

    /// <summary>Live state of one weapon on a unit.</summary>
    public sealed class SimWeapon
    {
        public WeaponStats Stats;
        /// <summary>The enemy this weapon is shooting at, or null.</summary>
        public SimUnit Target;
        /// <summary>Seconds until this weapon can fire again.</summary>
        public float Cooldown;
        public BeamPhase BeamPhase;
        /// <summary>The enemy a charging or firing beam is locked on to, or null.</summary>
        public SimUnit BeamTarget;
        /// <summary>The spot on the ground a beam is aimed at.</summary>
        public Vec2 BeamPoint;
        /// <summary>Ticks already spent in the current beam phase.</summary>
        public int BeamTicks;
        /// <summary>True while a weapon with a wind-up (the Spartan Laser) charges its shot.</summary>
        public bool WindingUp;
        /// <summary>The enemy a winding-up weapon locked on to when it started.</summary>
        public SimUnit WindUpTarget;
        /// <summary>Seconds of wind-up left, counted down at the unit's rate of fire (a shockwave stretches it).</summary>
        public float WindUpLeft;
        /// <summary>How far the wind-up has come, 0 to 1.</summary>
        public float WindUpShare => !WindingUp || Stats.WindUpSeconds <= 0f ? 0f : System.Math.Min(1f, System.Math.Max(0f, 1f - WindUpLeft / Stats.WindUpSeconds));

        /// <summary>Stops a wind-up without firing (an EMP, or the unit died).</summary>
        public void CancelWindUp()
        {
            WindingUp = false;
            WindUpTarget = null;
            WindUpLeft = 0f;
        }

        public SimWeapon(WeaponStats stats)
        {
            Stats = stats;
        }
    }

    /// <summary>What a carrier that lands to drop its squad is doing (D60).</summary>
    public enum DropRunPhase
    {
        Flying,
        Descending,
        Ascending,
    }

    public sealed class SimUnit
    {
        /// <summary>Unique number within one battle. Also used to break ties so results are repeatable.</summary>
        public readonly int Id;
        public readonly Team Team;
        /// <summary>Which squad this body belongs to (-1 if none).</summary>
        public readonly int SquadId;
        /// <summary>The unit's current stats: its own, or those without techs while an EMP has disabled them.</summary>
        public UnitStats Stats { get; private set; }
        private readonly UnitStats _fullStats;
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
        /// <summary>Multiplies the damage of every weapon of this unit (damage buff spells). 1 = normal.</summary>
        public float DamageMultiplier = 1f;
        /// <summary>The spell that dealt the killing blow, or null (then KilledById tells the unit, if any).</summary>
        public SimSpell KilledBySpell;
        /// <summary>The weapon that decides where the unit moves and stops: the first one, except on units that switch weapons by target.</summary>
        public int MainWeaponIndex;
        /// <summary>Multiplies its weapons' range (units in smoke). Set every tick.</summary>
        public float RangeMultiplier = 1f;
        /// <summary>Multiplies its move speed (units slowed by an EMP). Set every tick.</summary>
        public float SpeedMultiplier = 1f;
        /// <summary>Multiplies the damage it deals, and how fast its weapons come ready (a fallen tower's shockwave, D55). Set every tick.</summary>
        public float ShockwaveDamage = 1f;
        public float ShockwaveFireRate = 1f;
        /// <summary>Multiplies the damage it takes (a fallen tower's shockwave, D58). Set every tick. The other tower isn't part of the army, and stays as it is.</summary>
        public float ShockwaveDamageTaken = 1f;
        /// <summary>Multiplies the fire damage it takes (standing in oil). Set every tick.</summary>
        public float FireDamageTaken = 1f;
        /// <summary>The last tick oil slowed it (several pools do not add up).</summary>
        public int OilTick = -1;
        /// <summary>Battle time until which it stays slowed, and by how much.</summary>
        public float SlowedUntil;
        public float SlowMultiplier = 1f;
        /// <summary>Battle time until which its tech upgrades are disabled (EMP).</summary>
        public float TechsDisabledUntil;
        /// <summary>True while a Seeder's seed is in it (D38). If it dies like this, it rises as a Flood unit.</summary>
        public bool IsSeeded;
        public float SeededUntil;
        public float SeedDamagePerSecond;
        public float SeedSlowShare;
        public int SeededById = -1;
        /// <summary>Killed by a spell that infects: it rises as a Flood unit.</summary>
        public bool MarkedForInfection;
        /// <summary>Battle time of its next release (units that keep releasing others).</summary>
        public float NextSpawnAt;
        /// <summary>The last tick an aura reached it (several auras do not add up).</summary>
        public int AuraTick = -1;

        /// <summary>True while it is in the air on a leap (D39): it flies over bodies and cannot attack.</summary>
        public bool IsLeaping;
        /// <summary>A flying unit has started its dive: when it lands, it dies (D46).</summary>
        public bool HasDived;
        public Vec2 LeapFrom;
        public Vec2 LeapTo;
        /// <summary>Battle time from which it can leap again.</summary>
        public float LeapReadyAt;
        /// <summary>Seconds it has been walking much slower than it wants to.</summary>
        public float HeldUpSeconds;

        /// <summary>How far along its leap it is, 0 to 1.</summary>
        public float LeapProgress
        {
            get
            {
                float total = Vec2.Distance(LeapFrom, LeapTo);
                return !IsLeaping || total < 1e-4f ? 0f : System.Math.Min(1f, Vec2.Distance(LeapFrom, Position) / total);
            }
        }

        /// <summary>How many of its tiny forms are still alive (1 for a normal unit).</summary>
        public int FormsAlive
        {
            get
            {
                int forms = Stats.FormsPerBody;
                if (forms <= 1) return 1;
                int alive = (int)System.MathF.Ceiling(Health / (Stats.MaxHealth / forms) - 1e-4f);
                return alive < 1 ? 1 : alive > forms ? forms : alive;
            }
        }

        /// <summary>The share of its forms still alive: a pack deals less as it shrinks.</summary>
        public float FormsShare => (float)FormsAlive / Stats.FormsPerBody;

        /// <summary>Its move speed right now.</summary>
        public float MoveSpeedNow => Stats.MoveSpeed * SpeedMultiplier;

        /// <summary>Switches between its full stats and its stats without techs (an EMP). Weapons keep their targets and cooldowns.</summary>
        public void SetTechsDisabled(bool disabled)
        {
            UnitStats wanted = disabled && _fullStats.WithoutTechs != null ? _fullStats.WithoutTechs : _fullStats;
            if (wanted == Stats) return;
            Stats = wanted;
            for (int i = 0; i < Weapons.Length && i < wanted.Weapons.Length; i++) Weapons[i].Stats = wanted.Weapons[i];
        }

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
        /// <summary>A carrier that lands to drop (D60): how many drops it has made, what it is doing, and where it is going.</summary>
        public int DropsMade;
        /// <summary>True until a squad that arrives late is on the field (D61): it can't act, be seen or be hit, but it does keep the battle going.</summary>
        public bool NotArrived;
        public DropRunPhase DropPhase;
        public float NextDropAt;
        public Vec2 DropSpot;
        public float DropSpotCheckAt;
        /// <summary>How high it flies: 1 at its full height, 0 on the ground. Only a carrier that lands ever comes down.</summary>
        public float Altitude = 1f;

        // Battle statistics for results and later features (XP from kills, result screen).
        public float DamageDealt;
        public int Kills;
        /// <summary>Id of the unit that dealt the killing blow, or -1.</summary>
        public int KilledById = -1;

        // For the battle's statistics (D69). Nothing in the battle reads these.
        /// <summary>Shield and health this body lost, to anything.</summary>
        public float DamageTaken;
        /// <summary>What its armor, shield type and defense arc stopped: what each hit would have done to a body with none, minus what it did, never more than it had left. A shot an arc blocked counts whole.</summary>
        public float DamagePrevented;
        /// <summary>What the seeds it planted ate (DamageDealt doesn't count it).</summary>
        public float SeedDamageDealt;
        /// <summary>The squad its deeds count for: its own, or for a body dropped, released or risen in the battle, the squad it came from (-1 if none).</summary>
        public int CreditSquadId;
        /// <summary>The spell its deeds count for (squads that drop with a spell), or null.</summary>
        public SimSpell CreditSpell;

        public SimUnit(int id, Team team, int squadId, UnitStats stats, Vec2 position, Vec2 facing)
        {
            Id = id;
            Team = team;
            SquadId = squadId;
            CreditSquadId = squadId;
            Stats = stats;
            _fullStats = stats;
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
        /// <summary>A flier that has come down to land counts as being on the ground.</summary>
        public bool IsAir => Stats.IsAir && Altitude > LandedAltitude;
        private const float LandedAltitude = 0.2f;
        public float Radius => Stats.CollisionRadius;

        public override string ToString() => $"{Stats.Name}#{Id} ({Team}) {State} hp={Health:0.#} at {Position}";
    }
}
