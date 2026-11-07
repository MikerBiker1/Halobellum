// BattleEvents.cs
// The simulation's event bus (Bible Section 4, rules 2 and 6). The simulation raises these
// events; visuals, sound, UI and statistics subscribe to them instead of reading the simulation
// directly. Unit positions change every tick, so instead of one event per unit move there is a
// single Ticked event after each tick; listeners then read unit positions.
// Depends on: SimUnit, BattleResult, Halobellum.Core (WeaponStats, Vec2).

using System;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public sealed class BattleEvents
    {
        /// <summary>A unit was added to the battle.</summary>
        public event Action<SimUnit> UnitSpawned;
        /// <summary>A unit (the carrier) dropped its reinforcement squad (squad id) mid-battle.</summary>
        public event Action<SimUnit, int> ReinforcementDropped;
        /// <summary>A weapon fired: attacker, weapon index on the attacker, target.</summary>
        public event Action<SimUnit, int, SimUnit> WeaponFired;
        /// <summary>A weapon with a wind-up started charging its shot: attacker, weapon index, the target it locked on to, seconds until the shot (at its rate of fire now).</summary>
        public event Action<SimUnit, int, SimUnit, float> WeaponWindingUp;
        /// <summary>A shot arrived and was not blocked: attacker, the weapon that fired it, where. Once per shot, however many it hits, and also if it hits nothing.</summary>
        public event Action<SimUnit, WeaponStats, Vec2> ShotLanded;
        /// <summary>A unit took damage: target, attacker, damage removed from shield and health.</summary>
        public event Action<SimUnit, SimUnit, float> UnitDamaged;
        /// <summary>A defense arc blocked a projectile: target, attacker.</summary>
        public event Action<SimUnit, SimUnit> AttackBlocked;
        /// <summary>A unit died: the unit, the unit that dealt the killing blow (may be null).</summary>
        public event Action<SimUnit, SimUnit> UnitDied;
        /// <summary>A tower fell, and its side's army now suffers its shockwave (D55).</summary>
        public event Action<SimUnit> ShockwaveReleased;

        /// <summary>A squad that arrives late is on the field now (D61).</summary>
        public event Action<int> SquadArrived;
        /// <summary>One simulation tick finished; the argument is the number of ticks completed.</summary>
        public event Action<int> Ticked;
        /// <summary>The battle is over.</summary>
        public event Action<BattleResult> BattleEnded;
        /// <summary>A spell took effect (each damage tick, or its buff or drop).</summary>
        public event Action<SimSpell> SpellTriggered;
        /// <summary>A unit just killed will rise as an infected unit: its corpse.</summary>
        public event Action<SimUnit> UnitInfected;
        /// <summary>An infected unit rose: its corpse, the new unit.</summary>
        public event Action<SimUnit, SimUnit> UnitRose;
        /// <summary>A unit started a leap, a charge or a dive (D91: its sound).</summary>
        public event Action<SimUnit> UnitLeapt;

        internal void RaiseUnitSpawned(SimUnit unit) => UnitSpawned?.Invoke(unit);
        internal void RaiseReinforcementDropped(SimUnit carrier, int squadId) => ReinforcementDropped?.Invoke(carrier, squadId);
        internal void RaiseWeaponFired(SimUnit attacker, int weaponIndex, SimUnit target) => WeaponFired?.Invoke(attacker, weaponIndex, target);
        internal void RaiseWeaponWindingUp(SimUnit attacker, int weaponIndex, SimUnit target, float seconds) => WeaponWindingUp?.Invoke(attacker, weaponIndex, target, seconds);
        internal void RaiseShotLanded(SimUnit attacker, WeaponStats weapon, Vec2 point) => ShotLanded?.Invoke(attacker, weapon, point);
        internal void RaiseUnitDamaged(SimUnit target, SimUnit attacker, float amount) => UnitDamaged?.Invoke(target, attacker, amount);
        internal void RaiseAttackBlocked(SimUnit target, SimUnit attacker) => AttackBlocked?.Invoke(target, attacker);
        internal void RaiseUnitDied(SimUnit unit, SimUnit killer) => UnitDied?.Invoke(unit, killer);
        internal void RaiseShockwaveReleased(SimUnit tower) => ShockwaveReleased?.Invoke(tower);
        internal void RaiseSquadArrived(int squadId) => SquadArrived?.Invoke(squadId);
        internal void RaiseTicked(int tick) => Ticked?.Invoke(tick);
        internal void RaiseBattleEnded(BattleResult result) => BattleEnded?.Invoke(result);
        internal void RaiseSpellTriggered(SimSpell spell) => SpellTriggered?.Invoke(spell);
        internal void RaiseUnitInfected(SimUnit corpse) => UnitInfected?.Invoke(corpse);
        internal void RaiseUnitLeapt(SimUnit unit) => UnitLeapt?.Invoke(unit);
        internal void RaiseUnitRose(SimUnit corpse, SimUnit risen) => UnitRose?.Invoke(corpse, risen);
    }
}
