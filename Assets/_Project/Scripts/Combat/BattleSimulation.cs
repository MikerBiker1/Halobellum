// BattleSimulation.cs
// The battle simulation (Bible Sections 4 and 6). Plain C#: no GameObjects, models or animation,
// so the game, the tests and the headless runner all run exactly this code.
// It advances in fixed ticks (30 per second from CombatRules). Each tick, in this order:
//   0. Mid-battle drops: a unit with a reinforcement (Pelican) drops its squad once it has been
//      alive long enough; the dropped squad joins the battle like any other.
//   1. Shields regenerate if the unit has gone long enough without damage.
//   2. Targeting: every 0.5 s (or at once if its target died) each unit picks the closest enemy it
//      can legally target within its acquisition range (weapon range x 3; unlimited once inside the
//      enemy's deployment zone). Each weapon picks its own closest legal target.
//   3. State: if the first weapon's target is in range the unit stops and attacks (Attack);
//      otherwise it moves toward that target (Move); with nothing in sight it marches straight
//      ahead (Move); with no legal target anywhere it waits (Idle).
//   4. Squad cohesion (SimSquad): squads of several bodies march in formation until a break
//      condition is met. Then movement (MovementSystem): free 2D steering with collision.
//   5. Attacking units turn toward their target at their own turning speed. Weapons fire when ready
//      at targets in range (the main weapon only once the unit faces its target): instant hits
//      resolve now, other weapons launch projectiles.
//   6. Projectiles fly and hit (splash or cone damage, directional defense, block rolls, bonus
//      damage against air).
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
        private readonly List<SimSquad> _squads = new List<SimSquad>();
        private readonly Dictionary<int, SimSquad> _squadById = new Dictionary<int, SimSquad>();
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

        /// <summary>Fills the list with the position of every projectile in flight (for visuals).</summary>
        public void GetProjectilePositions(List<Vec2> positions)
        {
            positions.Clear();
            foreach (Projectile projectile in _projectiles) positions.Add(projectile.Position);
        }
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
            return CreateUnit(stats, team, position, squadId);
        }

        private SimUnit CreateUnit(UnitStats stats, Team team, Vec2 position, int squadId)
        {
            Vec2 facing = team == Team.A ? Vec2.Up : Vec2.Down;
            var unit = new SimUnit(_units.Count, team, squadId, stats, position, facing);
            _units.Add(unit);
            if (!_unitTypeOrder.Contains(stats.Name)) _unitTypeOrder.Add(stats.Name);
            if (squadId >= 0)
            {
                if (!_squadById.TryGetValue(squadId, out SimSquad squad))
                {
                    squad = new SimSquad(squadId, team);
                    _squadById[squadId] = squad;
                    _squads.Add(squad);
                }
                squad.Members.Add(unit);
            }
            Events.RaiseUnitSpawned(unit);
            return unit;
        }

        /// <summary>A new squad id, unique within this battle. Use it for every body of one squad.</summary>
        public int NewSquadId() => _nextSquadId++;

        public IReadOnlyList<SimSquad> Squads => _squads;

        /// <summary>The squad with this id, or null.</summary>
        public SimSquad SquadById(int squadId) => _squadById.TryGetValue(squadId, out SimSquad squad) ? squad : null;

        /// <summary>Adds a whole squad (SquadSize bodies) in formation around center. Returns the squad id.</summary>
        public int AddSquad(UnitStats stats, Team team, Vec2 center, bool rotated90 = false)
        {
            int squadId = NewSquadId();
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

            if (Tick == 0) foreach (SimSquad squad in _squads) squad.FormUp();

            SpawnReinforcements();
            UpdateShields();
            UpdateTargetsAndStates();
            UpdateSquadFormations();
            _movement.Step(_units, _dt);
            FaceTargets();
            FireWeapons();
            UpdateProjectiles();
            ApplyDeaths();

            Tick++;
            Events.RaiseTicked(Tick);
            CheckForEnd();
        }

        private readonly List<SimUnit> _dropping = new List<SimUnit>();

        /// <summary>Drops the reinforcement squad of every living unit whose drop delay has passed (once per unit).</summary>
        private void SpawnReinforcements()
        {
            _dropping.Clear();
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive || unit.ReinforcementDropped || !unit.Stats.HasReinforcement) continue;
                if (ElapsedSeconds + 1e-4f >= unit.Stats.ReinforcementDelaySeconds) _dropping.Add(unit);
            }
            foreach (SimUnit carrier in _dropping)
            {
                carrier.ReinforcementDropped = true;
                UnitStats stats = carrier.Stats.Reinforcement;
                int squadId = NewSquadId();
                Vec2 forward = carrier.Team == Team.A ? Vec2.Up : Vec2.Down;
                foreach (Vec2 offset in SquadFormation.Offsets(stats.SquadSize, stats.CollisionRadius, _rules.FormationGap, false))
                {
                    Vec2 placed = carrier.Team == Team.A ? offset : -offset;
                    SimUnit dropped = CreateUnit(stats, carrier.Team, carrier.Position + placed, squadId);
                    dropped.Facing = forward;
                    dropped.UnlimitedAcquisition = carrier.UnlimitedAcquisition || IsInEnemyZone(dropped);
                }
                _squadById[squadId].FormUp();
                Events.RaiseReinforcementDropped(carrier, squadId);
            }
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

                // Once inside the enemy's deployment zone, a unit notices enemies at any distance.
                if (!unit.UnlimitedAcquisition && IsInEnemyZone(unit)) unit.UnlimitedAcquisition = true;

                unit.RetargetTimer -= _dt;
                // Re-pick at once only if the current target died; a unit with no target waits for its next check.
                bool targetLost = unit.MoveTarget != null && !Targeting.IsTargetableEnemy(unit, unit.MoveTarget);
                if (unit.RetargetTimer <= 0f || targetLost)
                {
                    Retarget(unit);
                    unit.RetargetTimer = _rules.RetargetIntervalSeconds;
                    // Everyone picks a target on the first tick. After that, spread units across the
                    // interval by id, so 600 units don't all search on the same tick (smoother frame
                    // times). Each unit still re-evaluates every RetargetIntervalSeconds.
                    if (Tick == 0) unit.RetargetTimer -= StaggerOffset(unit);
                }
                else
                {
                    // Between evaluations, only replace secondary weapon targets that died.
                    for (int w = 1; w < unit.Weapons.Length; w++)
                    {
                        SimWeapon weapon = unit.Weapons[w];
                        if (weapon.Target != null && !Targeting.IsTargetableEnemy(unit, weapon.Target))
                        {
                            weapon.Target = Targeting.FindClosestLegalTarget(unit, weapon.Stats, _units, AcquisitionRange(unit, weapon.Stats));
                        }
                    }
                }

                SimUnit target = unit.MoveTarget;
                if (unit.Weapons.Length == 0 || (target == null && unit.UnlimitedAcquisition))
                {
                    unit.State = UnitState.Idle; // Nothing it could ever reach.
                }
                else if (target == null)
                {
                    // Nothing in sight yet: march straight ahead toward the enemy's side.
                    unit.State = UnitState.Move;
                    unit.MoveGoal = unit.Position + Forward(unit) * (unit.Stats.MoveSpeed * _rules.FormationLookaheadSeconds + 1f);
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

        /// <summary>
        /// Squad cohesion (see SimSquad): breaks formations whose break conditions are met, then sends
        /// the bodies of intact squads to their places in the formation, moving together toward the
        /// enemy their members are heading for.
        /// </summary>
        private void UpdateSquadFormations()
        {
            foreach (SimSquad squad in _squads)
            {
                if (!squad.IsIntact) continue;

                bool engaged = squad.Engaged, touched = false, split = false, anyTarget = false;
                int targetSquad = 0;
                foreach (SimUnit member in squad.Members)
                {
                    if (!member.IsAlive) continue;
                    if (member.State == UnitState.Attack) engaged = true;
                    if (member.TouchedOtherSquad) touched = true;
                    if (member.MoveTarget == null) continue;
                    // Bodies outside any squad count as their own squad.
                    int key = member.MoveTarget.SquadId >= 0 ? member.MoveTarget.SquadId : -1 - member.MoveTarget.Id;
                    if (!anyTarget) { targetSquad = key; anyTarget = true; }
                    else if (key != targetSquad) split = true;
                }

                if (engaged) squad.Break("engaged in combat");
                else if (touched) squad.Break("collided with another squad");
                else if (split) squad.Break("targets in different squads");
                else ApplyFormationGoals(squad);
            }

            foreach (SimUnit unit in _units) unit.TouchedOtherSquad = false; // Movement sets it again this tick.
        }

        private void ApplyFormationGoals(SimSquad squad)
        {
            Vec2 anchor = squad.Anchor();
            SimUnit heading = null;
            float bestDistance = float.MaxValue;
            foreach (SimUnit member in squad.Members)
            {
                if (!member.IsAlive || member.MoveTarget == null) continue;
                float distance = Vec2.DistanceSquared(anchor, member.MoveTarget.Position);
                if (distance < bestDistance) { bestDistance = distance; heading = member.MoveTarget; }
            }
            // Head for the enemy the members are targeting; with none in sight, march straight ahead.
            Vec2 direction = heading != null ? (heading.Position - anchor).Normalized : Forward(squad.Members[0]);

            for (int i = 0; i < squad.Members.Count; i++)
            {
                SimUnit member = squad.Members[i];
                if (!member.IsAlive || member.State != UnitState.Move) continue;
                // Aim a little ahead of the formation place, so members keep marching at full speed.
                float ahead = member.Stats.MoveSpeed * _rules.FormationLookaheadSeconds;
                member.MoveGoal = anchor + squad.Offsets[i] + direction * ahead;
            }
        }

        /// <summary>How far this weapon notices enemies: weapon range x the acquisition multiplier, or unlimited.</summary>
        private float AcquisitionRange(SimUnit unit, WeaponStats weapon)
        {
            if (unit.UnlimitedAcquisition || _rules.AcquisitionRangeMultiplier <= 0f) return float.PositiveInfinity;
            return weapon.Range * _rules.AcquisitionRangeMultiplier;
        }

        /// <summary>Straight ahead toward the enemy's side: up for Team A, down for Team B.</summary>
        private static Vec2 Forward(SimUnit unit) => unit.Team == Team.A ? Vec2.Up : Vec2.Down;

        /// <summary>True once the unit is inside the enemy's deployment zone (past no man's land).</summary>
        private bool IsInEnemyZone(SimUnit unit)
        {
            float front = _rules.DeploymentZoneFront;
            return unit.Team == Team.A ? unit.Position.Y > front : unit.Position.Y < -front;
        }

        private void MarkEngaged(SimUnit unit)
        {
            if (unit.SquadId >= 0 && _squadById.TryGetValue(unit.SquadId, out SimSquad squad)) squad.Engaged = true;
        }

        /// <summary>A per-unit delay of 0 to (interval - 1 tick), in whole ticks, used to spread out target searches.</summary>
        private float StaggerOffset(SimUnit unit)
        {
            int ticksPerInterval = Math.Max(1, (int)MathF.Round(_rules.RetargetIntervalSeconds / _dt));
            return (unit.Id % ticksPerInterval) * _dt;
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
                weapon.Target = Targeting.FindClosestLegalTarget(unit, weapon.Stats, _units, AcquisitionRange(unit, weapon.Stats));
            }
            unit.MoveTarget = unit.Weapons[0].Target;
        }

        private void FaceTargets()
        {
            foreach (SimUnit unit in _units)
            {
                if (unit.State != UnitState.Attack || unit.MoveTarget == null) continue;
                Vec2 toTarget = unit.MoveTarget.Position - unit.Position;
                if (toTarget.LengthSquared <= 1e-8f) continue;
                // Turn toward the target at the unit's own turning speed (instantly if it has none).
                unit.Facing = unit.Stats.TurnTime180 > 0f
                    ? Vec2.RotateTowards(unit.Facing, toTarget, unit.Stats.TurnRateDegreesPerSecond * _dt)
                    : toTarget.Normalized;
            }
        }

        /// <summary>
        /// The main weapon (index 0) only fires when the unit faces its target closely enough; the unit
        /// has to turn its body to aim. Other weapons fire in any direction (decision D8).
        /// </summary>
        private bool IsAimed(SimUnit unit, int weaponIndex, SimUnit target)
        {
            if (weaponIndex != 0 || _rules.MainWeaponFacingToleranceDegrees >= 180f) return true;
            Vec2 toTarget = target.Position - unit.Position;
            if (toTarget.LengthSquared <= 1e-8f) return true;
            return Vec2.AngleDegrees(unit.Facing, toTarget) <= _rules.MainWeaponFacingToleranceDegrees + 0.01f;
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
                    bool canFire = Targeting.IsTargetableEnemy(unit, target) && Targeting.IsInRange(unit, weapon.Stats, target)
                                   && IsAimed(unit, w, target);
                    if (!canFire)
                    {
                        // A weapon that is not shooting stays ready, but cannot store up extra shots.
                        if (weapon.Cooldown < 0f) weapon.Cooldown = 0f;
                        continue;
                    }
                    if (weapon.Cooldown > 0f) continue;

                    weapon.Cooldown += weapon.Stats.FireInterval;
                    Events.RaiseWeaponFired(unit, w, target);
                    MarkEngaged(unit);

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
        /// weapons, to every other enemy the weapon could target within the splash radius. Cone weapons
        /// instead hit every such enemy inside their cone, from the shooter out to their range.
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

            if (weapon.IsCone)
            {
                ResolveCone(attacker, weapon, directTarget, impactPoint, sourcePosition);
                return;
            }
            if (weapon.SplashRadius <= 0f) return;

            _movement.Grid.Query(impactPoint, weapon.SplashRadius + _movement.Grid.MaxRadius, _nearby);
            foreach (SimUnit victim in _nearby)
            {
                if (victim == directTarget) continue;
                if (!Targeting.IsTargetableEnemy(attacker, victim) || !weapon.CanTarget(attacker.Stats, victim.IsAir)) continue;
                if (Vec2.Distance(victim.Position, impactPoint) - victim.Radius > weapon.SplashRadius) continue;

                DefenseResult defense = DirectionalDefense.Evaluate(victim.Stats.DefenseArc, victim.Position,
                    victim.Facing, impactPoint, weapon, false, _random);
                DealDamage(victim, attacker, weapon, defense.DamageMultiplier);
            }
        }

        /// <summary>Cone splash: every other legal enemy inside the cone from the source toward the impact point.</summary>
        private void ResolveCone(SimUnit attacker, WeaponStats weapon, SimUnit directTarget, Vec2 impactPoint, Vec2 sourcePosition)
        {
            Vec2 direction = impactPoint - sourcePosition;
            if (direction.LengthSquared <= 1e-8f) direction = attacker.Facing;
            float halfAngle = weapon.ConeDegrees * 0.5f;

            _movement.Grid.Query(sourcePosition, weapon.Range + _movement.Grid.MaxRadius, _nearby);
            foreach (SimUnit victim in _nearby)
            {
                if (victim == directTarget) continue;
                if (!Targeting.IsTargetableEnemy(attacker, victim) || !weapon.CanTarget(attacker.Stats, victim.IsAir)) continue;
                Vec2 toVictim = victim.Position - sourcePosition;
                if (toVictim.Length - victim.Radius > weapon.Range) continue;
                if (toVictim.LengthSquared > 1e-8f && Vec2.AngleDegrees(direction, toVictim) > halfAngle) continue;

                DefenseResult defense = DirectionalDefense.Evaluate(victim.Stats.DefenseArc, victim.Position,
                    victim.Facing, sourcePosition, weapon, false, _random);
                DealDamage(victim, attacker, weapon, defense.DamageMultiplier);
            }
        }

        private void DealDamage(SimUnit target, SimUnit attacker, WeaponStats weapon, float directionalMultiplier)
        {
            float multiplier = target.IsAir ? directionalMultiplier * weapon.AirDamageMultiplier : directionalMultiplier;
            float dealt = DamageCalculator.ApplyHit(_rules.DamageTable, weapon, target.Stats.ArmorType, multiplier,
                ref target.Shield, ref target.Health);
            target.SecondsSinceDamaged = 0f;
            attacker.DamageDealt += dealt;
            MarkEngaged(target);
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
