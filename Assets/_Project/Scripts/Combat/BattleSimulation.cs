// BattleSimulation.cs
// The battle simulation (Bible Sections 4 and 6). Plain C#: no GameObjects, models or animation,
// so the game, the tests and the headless runner all run exactly this code.
// It advances in fixed ticks (30 per second from CombatRules). Each tick, in this order:
//   1. Shields regenerate if the unit has gone long enough without damage.
//   2. Targeting: every 0.5 s (or at once if its target died) each unit picks the closest enemy it
//      can legally target. Each weapon picks its own closest legal target.
//   3. State: if the first weapon's target is in range the unit stops and attacks (Attack);
//      otherwise it moves toward that target (Move); with no legal target it waits (Idle).
//   4. Movement (MovementSystem): free 2D steering with collision.
//   5. Weapons fire when ready at targets in range: instant hits resolve now, other weapons
//      launch projectiles.
//   6. Projectiles fly and hit (splash damage, directional defense, block rolls).
//   7. Units at 0 health die. Deaths are applied at the end of the tick, so units that shoot in
//      the same tick are treated fairly regardless of list order.
//   8. The battle ends when a side has no units left or the time limit is reached.
// Random rolls (shield blocks) use a seeded SimRandom, so the same armies and seed replay the same.
// Depends on: MovementSystem, Targeting, DamageCalculator, DirectionalDefense, SquadFormation,
//             BattleEvents, BattleResult, SimUnit, Halobellum.Core.

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public sealed class BattleSimulation
    {
        private readonly CombatRules _rules;
        private readonly SimRandom _random;
        private readonly MovementSystem _movement;
        private readonly List<SimUnit> _units = new List<SimUnit>();
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly List<SimUnit> _nearby = new List<SimUnit>();
        private readonly List<string> _unitTypeOrder = new List<string>();
        private readonly float _dt;
        private readonly int _tickLimit;
        private int _nextSquadId;

        public BattleSimulation(CombatRules rules, int seed)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (rules.TicksPerSecond <= 0) throw new ArgumentException("TicksPerSecond must be above 0.");
            Seed = seed;
            _random = new SimRandom(seed);
            _movement = new MovementSystem(rules);
            _dt = rules.TickSeconds;
            _tickLimit = (int)MathF.Round(rules.BattleTimeLimitSeconds * rules.TicksPerSecond);
        }

        public int Seed { get; }
        public BattleEvents Events { get; } = new BattleEvents();
        public IReadOnlyList<SimUnit> Units => _units;
        public int ProjectileCount => _projectiles.Count;
        /// <summary>Number of ticks simulated so far.</summary>
        public int Tick { get; private set; }
        public float ElapsedSeconds => Tick * _dt;
        public float TickSeconds => _dt;
        public bool IsFinished { get; private set; }
        /// <summary>Set once the battle is finished.</summary>
        public BattleResult Result { get; private set; }

        // ------------------------------------------------------------------ setup

        /// <summary>Adds one body. Team A faces up (+Y), Team B faces down (-Y).</summary>
        public SimUnit AddUnit(UnitStats stats, Team team, Vec2 position, int squadId = -1)
        {
            if (Tick > 0) throw new InvalidOperationException("Add units before the battle starts.");
            Vec2 facing = team == Team.A ? Vec2.Up : Vec2.Down;
            var unit = new SimUnit(_units.Count, team, squadId, stats, position, facing);
            _units.Add(unit);
            if (!_unitTypeOrder.Contains(stats.Name)) _unitTypeOrder.Add(stats.Name);
            Events.RaiseUnitSpawned(unit);
            return unit;
        }

        /// <summary>Adds a whole squad (SquadSize bodies) in formation around center. Returns the squad id.</summary>
        public int AddSquad(UnitStats stats, Team team, Vec2 center, bool rotated90 = false)
        {
            int squadId = _nextSquadId++;
            List<Vec2> offsets = SquadFormation.Offsets(stats.SquadSize, stats.CollisionRadius, _rules.FormationGap, rotated90);
            foreach (Vec2 offset in offsets)
            {
                // Team B's squads face the other way, so their formation is mirrored.
                Vec2 placed = team == Team.A ? offset : -offset;
                AddUnit(stats, team, center + placed, squadId);
            }
            return squadId;
        }

        // ------------------------------------------------------------------ running

        /// <summary>Runs ticks until the battle is finished and returns the result.</summary>
        public BattleResult Run()
        {
            while (!IsFinished) Step();
            return Result;
        }

        /// <summary>Advances the battle by exactly one fixed tick.</summary>
        public void Step()
        {
            if (IsFinished) return;

            UpdateShields();
            UpdateTargetsAndStates();
            _movement.Step(_units, _dt);
            FaceTargets();
            FireWeapons();
            UpdateProjectiles();
            ApplyDeaths();

            Tick++;
            Events.RaiseTicked(Tick);
            CheckForEnd();
        }

        private void UpdateShields()
        {
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive) continue;
                unit.SecondsSinceDamaged += _dt;
                if (unit.Stats.HasShield && unit.Shield < unit.Stats.MaxShield && unit.SecondsSinceDamaged >= _rules.ShieldRegenDelaySeconds)
                {
                    unit.Shield = Math.Min(unit.Stats.MaxShield, unit.Shield + unit.Stats.ShieldRegenPerSecond * _dt);
                }
            }
        }

        private void UpdateTargetsAndStates()
        {
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive) continue;

                unit.RetargetTimer -= _dt;
                bool targetLost = !Targeting.IsTargetableEnemy(unit, unit.MoveTarget);
                if (unit.RetargetTimer <= 0f || targetLost)
                {
                    Retarget(unit);
                    unit.RetargetTimer = _rules.RetargetIntervalSeconds;
                }
                else
                {
                    // Between evaluations, only replace secondary weapon targets that died.
                    for (int w = 1; w < unit.Weapons.Length; w++)
                    {
                        SimWeapon weapon = unit.Weapons[w];
                        if (!Targeting.IsTargetableEnemy(unit, weapon.Target))
                        {
                            weapon.Target = Targeting.FindClosestLegalTarget(unit, weapon.Stats, _units);
                        }
                    }
                }

                SimUnit target = unit.MoveTarget;
                if (target == null || unit.Weapons.Length == 0)
                {
                    unit.State = UnitState.Idle;
                }
                else if (Targeting.IsInRange(unit, unit.Weapons[0].Stats, target))
                {
                    unit.State = UnitState.Attack;
                }
                else
                {
                    unit.State = UnitState.Move;
                    unit.MoveGoal = target.Position;
                }
            }
        }

        /// <summary>Re-picks the closest legal enemy for the unit and for each of its weapons.</summary>
        private void Retarget(SimUnit unit)
        {
            if (unit.Weapons.Length == 0)
            {
                unit.MoveTarget = null;
                return;
            }
            for (int w = 0; w < unit.Weapons.Length; w++)
            {
                SimWeapon weapon = unit.Weapons[w];
                weapon.Target = Targeting.FindClosestLegalTarget(unit, weapon.Stats, _units);
            }
            unit.MoveTarget = unit.Weapons[0].Target;
        }

        private void FaceTargets()
        {
            foreach (SimUnit unit in _units)
            {
                if (unit.State != UnitState.Attack || unit.MoveTarget == null) continue;
                Vec2 toTarget = unit.MoveTarget.Position - unit.Position;
                if (toTarget.LengthSquared > 1e-8f) unit.Facing = toTarget.Normalized;
            }
        }

        private void FireWeapons()
        {
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive) continue;
                for (int w = 0; w < unit.Weapons.Length; w++)
                {
                    SimWeapon weapon = unit.Weapons[w];
                    weapon.Cooldown -= _dt;

                    SimUnit target = weapon.Target;
                    bool canFire = Targeting.IsTargetableEnemy(unit, target) && Targeting.IsInRange(unit, weapon.Stats, target);
                    if (!canFire)
                    {
                        // A weapon that is not shooting stays ready, but cannot store up extra shots.
                        if (weapon.Cooldown < 0f) weapon.Cooldown = 0f;
                        continue;
                    }
                    if (weapon.Cooldown > 0f) continue;

                    weapon.Cooldown += weapon.Stats.FireInterval;
                    Events.RaiseWeaponFired(unit, w, target);

                    if (weapon.Stats.IsInstant)
                    {
                        ResolveHit(unit, weapon.Stats, target, target.Position, unit.Position);
                    }
                    else
                    {
                        _projectiles.Add(new Projectile(unit, weapon.Stats, target, unit.Position));
                    }
                }
            }
        }

        private void UpdateProjectiles()
        {
            // Iterate by index: hits never add projectiles, and finished ones are removed afterwards.
            for (int i = 0; i < _projectiles.Count; i++)
            {
                Projectile projectile = _projectiles[i];
                if (Targeting.IsTargetableEnemy(projectile.Attacker, projectile.Target))
                {
                    projectile.AimPoint = projectile.Target.Position; // Follows its target while it lives.
                }
                else
                {
                    projectile.Target = null; // Target gone: the shot lands where the target was.
                }

                Vec2 toAim = projectile.AimPoint - projectile.Position;
                float step = projectile.Weapon.ProjectileSpeed * _dt;
                if (toAim.Length <= step)
                {
                    ResolveHit(projectile.Attacker, projectile.Weapon, projectile.Target, projectile.AimPoint, projectile.Origin);
                    projectile.Done = true;
                }
                else
                {
                    projectile.Position += toAim.Normalized * step;
                }
            }
            _projectiles.RemoveAll(p => p.Done);
        }

        /// <summary>
        /// Applies one hit: to the direct target (unless a defense arc blocks it) and, for splash
        /// weapons, to every other enemy the attacker could target within the splash radius.
        /// </summary>
        private void ResolveHit(SimUnit attacker, WeaponStats weapon, SimUnit directTarget, Vec2 impactPoint, Vec2 sourcePosition)
        {
            if (directTarget != null && Targeting.IsTargetableEnemy(attacker, directTarget))
            {
                DefenseResult defense = DirectionalDefense.Evaluate(directTarget.Stats.DefenseArc, directTarget.Position,
                    directTarget.Facing, sourcePosition, weapon, true, _random);
                if (defense.Blocked)
                {
                    Events.RaiseAttackBlocked(directTarget, attacker);
                    return; // A blocked projectile is stopped completely, including any splash.
                }
                DealDamage(directTarget, attacker, weapon, defense.DamageMultiplier);
            }

            if (weapon.SplashRadius <= 0f) return;

            _movement.Grid.Query(impactPoint, weapon.SplashRadius + _movement.Grid.MaxRadius, _nearby);
            foreach (SimUnit victim in _nearby)
            {
                if (victim == directTarget) continue;
                if (!Targeting.IsTargetableEnemy(attacker, victim) || !attacker.Stats.CanTarget(victim.IsAir)) continue;
                if (Vec2.Distance(victim.Position, impactPoint) - victim.Radius > weapon.SplashRadius) continue;

                DefenseResult defense = DirectionalDefense.Evaluate(victim.Stats.DefenseArc, victim.Position,
                    victim.Facing, impactPoint, weapon, false, _random);
                DealDamage(victim, attacker, weapon, defense.DamageMultiplier);
            }
        }

        private void DealDamage(SimUnit target, SimUnit attacker, WeaponStats weapon, float directionalMultiplier)
        {
            float dealt = DamageCalculator.ApplyHit(_rules.DamageTable, weapon, target.Stats.ArmorType, directionalMultiplier,
                ref target.Shield, ref target.Health);
            target.SecondsSinceDamaged = 0f;
            attacker.DamageDealt += dealt;
            Events.RaiseUnitDamaged(target, attacker, dealt);

            if (target.Health <= 0f && target.KilledById < 0)
            {
                target.KilledById = attacker.Id;
                attacker.Kills++;
            }
        }

        private void ApplyDeaths()
        {
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive || unit.Health > 0f) continue;
                unit.State = UnitState.Dead;
                unit.Velocity = Vec2.Zero;
                unit.MoveTarget = null;
                foreach (SimWeapon weapon in unit.Weapons) weapon.Target = null;
                SimUnit killer = unit.KilledById >= 0 ? _units[unit.KilledById] : null;
                Events.RaiseUnitDied(unit, killer);
            }
        }

        private void CheckForEnd()
        {
            int aliveA = 0, aliveB = 0;
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive) continue;
                if (unit.Team == Team.A) aliveA++;
                else aliveB++;
            }

            bool sideWipedOut = aliveA == 0 || aliveB == 0;
            bool outOfTime = Tick >= _tickLimit;
            if (!sideWipedOut && !outOfTime) return;

            IsFinished = true;
            Result = BuildResult(timedOut: !sideWipedOut);
            Events.RaiseBattleEnded(Result);
        }

        // ------------------------------------------------------------------ results

        private BattleResult BuildResult(bool timedOut)
        {
            var result = new BattleResult
            {
                Seed = Seed,
                Ticks = Tick,
                DurationSeconds = ElapsedSeconds,
                TimedOut = timedOut,
                TeamA = BuildTeamResult(Team.A),
                TeamB = BuildTeamResult(Team.B),
            };

            float a = result.TeamA.SurvivorBaseDamage;
            float b = result.TeamB.SurvivorBaseDamage;
            if (a > b) result.Outcome = BattleOutcome.TeamAWins;
            else if (b > a) result.Outcome = BattleOutcome.TeamBWins;
            else if (result.TeamA.Survivors > 0 && result.TeamB.Survivors == 0) result.Outcome = BattleOutcome.TeamAWins;
            else if (result.TeamB.Survivors > 0 && result.TeamA.Survivors == 0) result.Outcome = BattleOutcome.TeamBWins;
            else result.Outcome = BattleOutcome.Draw;
            return result;
        }

        private TeamResult BuildTeamResult(Team team)
        {
            var teamResult = new TeamResult { Team = team };
            var byName = new Dictionary<string, UnitTypeResult>();

            foreach (string name in _unitTypeOrder)
            {
                var typeResult = new UnitTypeResult { UnitName = name, Team = team };
                byName[name] = typeResult;
            }

            foreach (SimUnit unit in _units)
            {
                if (unit.Team != team) continue;
                UnitTypeResult typeResult = byName[unit.Stats.Name];
                typeResult.Deployed++;
                typeResult.DamageDealt += unit.DamageDealt;
                typeResult.Kills += unit.Kills;
                if (unit.IsAlive)
                {
                    typeResult.Survivors++;
                    typeResult.SurvivorBaseDamage += unit.Stats.BaseDamage;
                }
            }

            foreach (string name in _unitTypeOrder)
            {
                UnitTypeResult typeResult = byName[name];
                if (typeResult.Deployed == 0) continue;
                teamResult.UnitTypes.Add(typeResult);
                teamResult.Deployed += typeResult.Deployed;
                teamResult.Survivors += typeResult.Survivors;
                teamResult.DamageDealt += typeResult.DamageDealt;
                teamResult.Kills += typeResult.Kills;
                teamResult.SurvivorBaseDamage += typeResult.SurvivorBaseDamage;
            }
            return teamResult;
        }

        /// <summary>A shot in flight. It follows its target and hits when it arrives.</summary>
        private sealed class Projectile
        {
            public readonly SimUnit Attacker;
            public readonly WeaponStats Weapon;
            public readonly Vec2 Origin;
            public SimUnit Target;
            public Vec2 Position;
            public Vec2 AimPoint;
            public bool Done;

            public Projectile(SimUnit attacker, WeaponStats weapon, SimUnit target, Vec2 origin)
            {
                Attacker = attacker;
                Weapon = weapon;
                Target = target;
                Origin = origin;
                Position = origin;
                AimPoint = target.Position;
            }
        }
    }
}
