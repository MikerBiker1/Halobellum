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
    /// <summary>A projectile in flight, for drawing. ArcLength is 0 unless the shot is lobbed.</summary>
    public struct ShotInFlight
    {
        public Vec2 Position;
        public float ArcLength;
        /// <summary>How far along its flight the shot is, 0 to 1.</summary>
        public float Progress;
    }

    /// <summary>A beam charging or firing, for drawing.</summary>
    public struct BeamShot
    {
        public SimUnit Attacker;
        public Vec2 Point;
        public float Radius;
        public bool Firing;
        /// <summary>How far the charge has come, 0 to 1 (1 while firing).</summary>
        public float Charge;
    }

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
        private readonly List<SimSpell> _spells = new List<SimSpell>();
        private readonly List<PendingRise> _rises = new List<PendingRise>();
        private readonly List<SimUnit> _bursting = new List<SimUnit>();
        private readonly List<SimUnit> _spawners = new List<SimUnit>();
        private readonly List<SimUnit> _auras = new List<SimUnit>();
        private readonly Dictionary<UnitStats, UnitStats> _infectedForms = new Dictionary<UnitStats, UnitStats>();
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
        /// <summary>Spells cast into this battle, in the order they were added.</summary>
        public IReadOnlyList<SimSpell> Spells => _spells;
        public int ProjectileCount => _projectiles.Count;

        /// <summary>Fills the list with the position of every projectile in flight (for visuals).</summary>
        public void GetProjectiles(List<ShotInFlight> shots)
        {
            shots.Clear();
            foreach (Projectile projectile in _projectiles)
            {
                float total = projectile.Weapon.Lobbed ? Vec2.Distance(projectile.Origin, projectile.AimPoint) : 0f;
                float progress = total > 1e-4f ? Math.Min(1f, Vec2.Distance(projectile.Origin, projectile.Position) / total) : 0f;
                shots.Add(new ShotInFlight { Position = projectile.Position, ArcLength = total, Progress = progress });
            }
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
            if (stats.SpawnedUnit != null)
            {
                unit.NextSpawnAt = ElapsedSeconds + stats.SpawnIntervalSeconds;
                _spawners.Add(unit);
            }
            if (stats.AuraRadius > 0f) _auras.Add(unit);
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

        /// <summary>Casts a spell for this team at this spot; it takes effect at its delay once the battle runs.</summary>
        public SimSpell AddSpell(SpellStats stats, Team team, Vec2 position, float angleDegrees = 0f)
        {
            if (Tick > 0) throw new InvalidOperationException("Add spells before the battle starts.");
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            var spell = new SimSpell(_spells.Count, stats, team, position, angleDegrees);
            _spells.Add(spell);
            return spell;
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
            UpdateRises();
            UpdateSpawners();
            UpdateSpells();
            UpdateStatusEffects();
            UpdateShields();
            UpdateTargetsAndStates();
            UpdateSquadFormations();
            UpdateAdvances();
            UpdateLeaps();
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

        /// <summary>A new squad of this unit type on the spot, for this battle only.</summary>
        private void SpawnSquad(UnitStats stats, Team team, Vec2 centre, int bodies)
        {
            int squadId = NewSquadId();
            Vec2 forward = team == Team.A ? Vec2.Up : Vec2.Down;
            foreach (Vec2 offset in SquadFormation.Offsets(bodies, stats.CollisionRadius, _rules.FormationGap, false))
            {
                SimUnit spawned = CreateUnit(stats, team, centre + (team == Team.A ? offset : -offset), squadId);
                spawned.Facing = forward;
                spawned.UnlimitedAcquisition = IsInEnemyZone(spawned);
            }
            _squadById[squadId].FormUp();
        }

        /// <summary>Killed units whose time has come get up on the other side.</summary>
        private void UpdateRises()
        {
            for (int i = 0; i < _rises.Count; i++)
            {
                PendingRise rise = _rises[i];
                if (ElapsedSeconds + 1e-4f < rise.At) continue;
                SimUnit risen = CreateUnit(InfectedFormOf(rise.Corpse.Stats), rise.Team, rise.Corpse.Position, -1);
                risen.Facing = rise.Team == Team.A ? Vec2.Up : Vec2.Down;
                risen.UnlimitedAcquisition = true;
                Events.RaiseUnitRose(rise.Corpse, risen);
                _rises.RemoveAt(i--);
            }
        }

        /// <summary>Units that keep releasing others (the Abomination's Infection Forms).</summary>
        private void UpdateSpawners()
        {
            int count = _spawners.Count; // New squads may be spawners too; they start next tick.
            for (int i = 0; i < count; i++)
            {
                SimUnit unit = _spawners[i];
                if (!unit.IsAlive || ElapsedSeconds + 1e-4f < unit.NextSpawnAt) continue;
                unit.NextSpawnAt += unit.Stats.SpawnIntervalSeconds;
                SpawnSquad(unit.Stats.SpawnedUnit, unit.Team, unit.Position + Forward(unit) * (unit.Radius + 1f), unit.Stats.SpawnedUnit.SquadSize);
            }
        }

        /// <summary>
        /// What a unit becomes when it is infected (Bible Section 8.3): its own body and base damage, 60%
        /// of its health, no shield or techs, and a melee attack as a dumb charger. It cannot be infected again.
        /// </summary>
        private UnitStats InfectedFormOf(UnitStats original)
        {
            if (_infectedForms.TryGetValue(original, out UnitStats infected)) return infected;
            float damagePerSecond = 0f;
            foreach (WeaponStats weapon in original.Weapons) damagePerSecond += weapon.DamagePerShot * weapon.ShotsPerSecond;
            infected = new UnitStats
            {
                Name = "Infected " + original.Name,
                Role = original.Role,
                Tier = original.Tier,
                SquadSize = 1,
                BaseDamage = original.BaseDamage,
                MaxHealth = original.MaxHealth * _rules.InfectedHealthShare,
                ArmorType = original.ArmorType,
                MoveSpeed = original.MoveSpeed * _rules.InfectedSpeedMultiplier,
                CollisionRadius = original.CollisionRadius,
                TurnTime180 = original.TurnTime180,
                IsAir = original.IsAir,
                TargetFilter = TargetFilter.Ground,
                IsFlood = true,
                InfectedFrom = original,
                Weapons = new[]
                {
                    new WeaponStats
                    {
                        Name = "Infected Melee",
                        DamagePerShot = damagePerSecond * _rules.InfectedDamageShare * _rules.InfectedMeleeInterval,
                        FireInterval = _rules.InfectedMeleeInterval,
                        Range = _rules.InfectedMeleeRange,
                        DamageType = DamageType.Melee,
                    },
                },
            };
            _infectedForms[original] = infected;
            return infected;
        }

        /// <summary>True if this unit, just killed, rises as an infected unit.</summary>
        private bool WillRise(SimUnit unit)
        {
            if (unit.IsSeeded || unit.MarkedForInfection) return true;
            if (unit.Stats.IsFlood || unit.Stats.ImmuneToInfection || unit.IsAir || unit.Stats.ArmorType != ArmorType.Unarmored) return false;
            _movement.Grid.Query(unit.Position, unit.Radius + _rules.InfectionRange + _movement.Grid.MaxRadius, _nearby);
            foreach (SimUnit other in _nearby)
            {
                if (!other.Stats.Infects || !Targeting.IsTargetableEnemy(unit, other)) continue;
                if (Targeting.EdgeDistance(unit, other) <= _rules.InfectionRange) return true;
            }
            return false;
        }

        /// <summary>A dying unit's explosion and the units it releases.</summary>
        private void ApplyDeathBurst(SimUnit unit)
        {
            DeathBurstStats burst = unit.Stats.DeathBurst;
            if (burst.Damage > 0f && burst.Radius > 0f)
            {
                WeaponStats weapon = burst.AsWeapon();
                _movement.Grid.Query(unit.Position, burst.Radius + _movement.Grid.MaxRadius, _nearby);
                foreach (SimUnit victim in _nearby)
                {
                    if (!Targeting.IsTargetableEnemy(unit, victim) || !weapon.CanTarget(unit.Stats, victim.IsAir)) continue;
                    if (Vec2.Distance(victim.Position, unit.Position) - victim.Radius > burst.Radius) continue;
                    DefenseResult defense = DirectionalDefense.Evaluate(victim.Stats.DefenseArc, victim.Position,
                        victim.Facing, unit.Position, weapon, false, _random);
                    DealDamage(victim, unit, weapon, defense.DamageMultiplier, true);
                }
            }
            foreach (SpawnEntry spawn in burst.Spawns) SpawnSquad(spawn.Unit, unit.Team, unit.Position, spawn.Bodies);
        }

        /// <summary>Applies every spell effect that is due this tick (in the order the spells were cast).</summary>
        private void UpdateSpells()
        {
            if (_spells.Count == 0) return;
            float now = ElapsedSeconds + 1e-4f;
            bool gridReady = false;
            foreach (SimSpell spell in _spells)
            {
                SpellStats stats = spell.Stats;
                while (!spell.IsDone && now >= stats.DelaySeconds + spell.TicksDone * stats.TickInterval)
                {
                    if (!gridReady)
                    {
                        _movement.Grid.Build(_units); // Current positions (the grid is empty before the first move).
                        gridReady = true;
                    }
                    spell.TicksDone++;
                    switch (stats.Effect)
                    {
                        case SpellEffect.Damage: ApplySpellDamage(spell); break;
                        case SpellEffect.Buff: ApplySpellBuff(spell); break;
                        case SpellEffect.DropSquad: DropSpellSquads(spell); break;
                        case SpellEffect.Disrupt: ApplySpellDisruption(spell); break;
                    }
                    Events.RaiseSpellTriggered(spell);
                    if (stats.Effect != SpellEffect.Damage || stats.TickInterval <= 0f) break;
                }
            }
        }

        /// <summary>
        /// Lasting spell effects, worked out afresh every tick: an EMP's disabled techs and slowed armored
        /// units, and the reduced range of every unit inside smoke (of either side, as long as it hangs).
        /// </summary>
        private void UpdateStatusEffects()
        {
            float now = ElapsedSeconds;
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive) continue;
                unit.SetTechsDisabled(now < unit.TechsDisabledUntil);
                unit.SpeedMultiplier = now < unit.SlowedUntil ? unit.SlowMultiplier : 1f;
                unit.RangeMultiplier = 1f;
                if (unit.IsSeeded) UpdateSeed(unit, now);
            }
            foreach (SimUnit source in _auras)
            {
                if (!source.IsAlive) continue;
                foreach (SimUnit unit in _units)
                {
                    if (!unit.IsAlive || unit.Team != source.Team || unit == source || unit.AuraTick == Tick) continue;
                    if (Vec2.Distance(unit.Position, source.Position) - unit.Radius > source.Stats.AuraRadius) continue;
                    unit.AuraTick = Tick;
                    unit.SpeedMultiplier *= source.Stats.AuraSpeedMultiplier;
                    if (unit.Health > 0f) unit.Health = Math.Min(unit.Stats.MaxHealth, unit.Health + unit.Stats.MaxHealth * source.Stats.AuraHealPerSecondShare * _dt);
                }
            }
            foreach (SimSpell spell in _spells)
            {
                if (spell.Stats.Effect != SpellEffect.Smoke || spell.TicksDone == 0 || spell.IsOverAt(now)) continue;
                foreach (SimUnit unit in _units)
                {
                    if (unit.IsAlive && spell.Covers(unit)) unit.RangeMultiplier = Math.Min(unit.RangeMultiplier, spell.Stats.RangeMultiplier);
                }
            }
        }

        /// <summary>A seed eats its host and slows it until it runs out (it can kill: the host then rises).</summary>
        private void UpdateSeed(SimUnit unit, float now)
        {
            if (now >= unit.SeededUntil)
            {
                unit.IsSeeded = false;
                return;
            }
            unit.SpeedMultiplier *= 1f - unit.SeedSlowShare;
            if (unit.Health <= 0f) return;
            unit.Health -= unit.SeedDamagePerSecond * _dt;
            unit.SecondsSinceDamaged = 0f;
            if (unit.Health <= 0f && unit.KilledById < 0 && unit.KilledBySpell == null && unit.SeededById >= 0)
            {
                unit.KilledById = unit.SeededById;
                _units[unit.SeededById].Kills++;
            }
        }

        /// <summary>A hit from a Seeder plants its seed, if the target can take one.</summary>
        private void PlantSeed(SimUnit target, SimUnit attacker, WeaponStats weapon)
        {
            if (!Targeting.CanBeSeeded(target)) return;
            target.IsSeeded = true;
            target.SeededUntil = ElapsedSeconds + weapon.SeedSeconds;
            target.SeedDamagePerSecond = target.Stats.MaxHealth * weapon.SeedDamageSharePerSecond;
            target.SeedSlowShare = weapon.SeedSlowShare;
            target.SeededById = attacker.Id;
        }

        /// <summary>An EMP: every unit in the area loses shield and its techs for a while; armored units are slowed.</summary>
        private void ApplySpellDisruption(SimSpell spell)
        {
            SpellStats stats = spell.Stats;
            float now = ElapsedSeconds;
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive || !spell.Covers(unit)) continue;
                float removed = Math.Min(unit.Shield, stats.ShieldDamage);
                unit.Shield -= removed;
                spell.DamageDealt += removed;
                if (stats.TechDisableSeconds > 0f) unit.TechsDisabledUntil = Math.Max(unit.TechsDisabledUntil, now + stats.TechDisableSeconds);
                if (stats.SlowShare > 0f && unit.Stats.ArmorType != ArmorType.Unarmored)
                {
                    unit.SlowedUntil = Math.Max(unit.SlowedUntil, now + stats.SlowSeconds);
                    unit.SlowMultiplier = 1f - stats.SlowShare;
                }
            }
        }

        private void ApplySpellDamage(SimSpell spell)
        {
            WeaponStats weapon = spell.Stats.AsWeapon();
            _movement.Grid.Query(spell.Position, spell.Stats.Radius + _movement.Grid.MaxRadius, _nearby);
            foreach (SimUnit victim in _nearby)
            {
                // Offensive spells hit everyone in the area, the caster's own units too (D33).
                if (!victim.IsAlive || (victim.Team == spell.Team && !spell.Stats.Offensive) || !spell.Stats.Targets.Allows(victim.IsAir)) continue;
                if (!spell.Covers(victim)) continue;
                float multiplier = (victim.IsAir ? weapon.AirDamageMultiplier : 1f) * victim.FormsAlive;
                float dealt = DamageCalculator.ApplyHit(_rules.DamageTable, weapon, victim.Stats.ArmorType, multiplier, ref victim.Shield, ref victim.Health);
                victim.SecondsSinceDamaged = 0f;
                spell.DamageDealt += dealt;
                MarkEngaged(victim);
                Events.RaiseUnitDamaged(victim, null, dealt);
                if (victim.Health <= 0f && victim.KilledById < 0 && victim.KilledBySpell == null)
                {
                    victim.KilledBySpell = spell;
                    spell.Kills++;
                    if (spell.Stats.InfectsKills && victim.Team != spell.Team && !victim.Stats.IsFlood && !victim.Stats.ImmuneToInfection
                        && !victim.IsAir && victim.Stats.ArmorType == ArmorType.Unarmored) victim.MarkedForInfection = true;
                }
            }
        }

        private void ApplySpellBuff(SimSpell spell)
        {
            _movement.Grid.Query(spell.Position, spell.Stats.Radius + _movement.Grid.MaxRadius, _nearby);
            foreach (SimUnit unit in _nearby)
            {
                if (!unit.IsAlive || unit.Team != spell.Team) continue;
                if (Vec2.Distance(unit.Position, spell.Position) - unit.Radius > spell.Stats.Radius) continue;
                if (spell.Stats.ShieldBonusShare > 0f) unit.Shield += unit.Stats.MaxHealth * spell.Stats.ShieldBonusShare;
                if (spell.Stats.DamageMultiplier > 0f) unit.DamageMultiplier *= spell.Stats.DamageMultiplier;
            }
        }

        /// <summary>Drop pods: the spell's squads land in formation around the spot, facing the enemy, for this battle only.</summary>
        private void DropSpellSquads(SimSpell spell)
        {
            UnitStats stats = spell.Stats.DropUnit;
            if (stats == null) return;
            Vec2 forward = spell.Team == Team.A ? Vec2.Up : Vec2.Down;
            ArmyLayout.FootprintOf(stats, _rules.FormationGap, out float width, out _);
            for (int n = 0; n < spell.Stats.DropSquads; n++)
            {
                int squadId = NewSquadId();
                if (n == 0) spell.DroppedSquadId = squadId;
                // Several squads stand side by side, centred on the spot.
                float offsetX = (n - (spell.Stats.DropSquads - 1) * 0.5f) * (width + _rules.FormationGap * 2f);
                Vec2 centre = spell.Position + new Vec2(spell.Team == Team.A ? offsetX : -offsetX, 0f);
                foreach (Vec2 offset in SquadFormation.Offsets(stats.SquadSize, stats.CollisionRadius, _rules.FormationGap, false))
                {
                    Vec2 placed = spell.Team == Team.A ? offset : -offset;
                    SimUnit dropped = CreateUnit(stats, spell.Team, centre + placed, squadId);
                    dropped.Facing = forward;
                    dropped.UnlimitedAcquisition = IsInEnemyZone(dropped);
                }
                _squadById[squadId].FormUp();
            }
        }

        private void UpdateShields()
        {
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive) continue;
                unit.SecondsSinceDamaged += _dt;
                if (unit.Stats.HealPerSecondShare > 0f && unit.Health > 0f)
                    unit.Health = Math.Min(Math.Max(unit.Health, unit.Stats.MaxHealth), unit.Health + unit.Stats.MaxHealth * unit.Stats.HealPerSecondShare * _dt);
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

                // Once inside the enemy's deployment zone, a unit notices enemies at any distance. It looks
                // straight away, instead of standing idle until its next check.
                if (!unit.UnlimitedAcquisition && IsInEnemyZone(unit))
                {
                    unit.UnlimitedAcquisition = true;
                    unit.RetargetTimer = 0f;
                }

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
                    for (int w = 1; w < unit.Weapons.Length && !unit.Stats.SwitchesWeapons; w++)
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
                    unit.MoveGoal = unit.Position + Forward(unit) * (unit.MoveSpeedNow * _rules.FormationLookaheadSeconds + 1f);
                }
                else if (Targeting.IsInRange(unit, unit.Weapons[unit.MainWeaponIndex].Stats, target))
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
                float ahead = member.MoveSpeedNow * _rules.FormationLookaheadSeconds;
                member.MoveGoal = anchor + squad.Offsets[i] + direction * ahead;
            }
        }

        private bool[] _advancing = new bool[0];

        /// <summary>
        /// Decision D30: a fighting unit edges toward its target (at AdvanceSpeedMultiplier of its speed,
        /// still firing) while a friendly ground unit right behind it is trying to move through it and
        /// is blocked. A friend that is itself edging forward counts too, so a whole column moves up.
        /// It stops once the target is within AdvanceRangeShare of its main weapon's range, and never
        /// steps inside the weapon's minimum range. Reads last tick's positions, velocities and edging
        /// flags, and writes all new flags at once, so the order of units doesn't matter.
        /// </summary>
        private void UpdateAdvances()
        {
            if (_advancing.Length < _units.Count) _advancing = new bool[Math.Max(_units.Count, _advancing.Length * 2)];
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit unit = _units[i];
                _advancing[i] = false;
                if (_rules.AdvanceSpeedMultiplier <= 0f || !unit.IsAlive || unit.IsAir || unit.IsLeaping || unit.State != UnitState.Attack
                    || unit.MoveTarget == null || unit.MoveSpeedNow <= 0f) continue;
                WeaponStats weapon = unit.Weapons[unit.MainWeaponIndex].Stats;
                float step = unit.MoveSpeedNow * _rules.AdvanceSpeedMultiplier * _dt;
                float closest = Math.Max(weapon.MinRange, weapon.Range * unit.RangeMultiplier * _rules.AdvanceRangeShare);
                if (Targeting.EdgeDistance(unit, unit.MoveTarget) - step <= closest) continue;
                _advancing[i] = HasBlockedFriendBehind(unit);
            }
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit unit = _units[i];
                unit.Advancing = _advancing[i];
                if (unit.Advancing) unit.MoveGoal = unit.MoveTarget.Position;
            }
        }

        /// <summary>True if a friendly ground unit touching this one is trying to walk through it and is held up.</summary>
        private bool HasBlockedFriendBehind(SimUnit unit)
        {
            const float contactSlack = 0.3f; // Metres beyond the separation padding that still count as touching.
            SpatialHashGrid grid = _movement.Grid;
            float touch = _rules.SeparationPadding + contactSlack;
            grid.Query(unit.Position, unit.Radius + grid.MaxRadius + touch, _nearby);
            foreach (SimUnit friend in _nearby)
            {
                if (friend == unit || friend.Team != unit.Team || friend.IsAir || !friend.IsAlive || friend.IsLeaping) continue;
                bool pushing = friend.State == UnitState.Move || friend.Advancing;
                if (!pushing) continue;

                Vec2 toUnit = unit.Position - friend.Position;
                float gap = toUnit.Length;
                if (gap < 1e-5f || gap - unit.Radius - friend.Radius > touch) continue;
                Vec2 way = friend.MoveGoal - friend.Position;
                if (way.LengthSquared < 1e-8f) continue;
                way = way.Normalized;
                if (Vec2.Dot(way, toUnit / gap) < 0.5f) continue; // We are not in its way.

                float wantedSpeed = friend.MoveSpeedNow * (friend.Advancing ? _rules.AdvanceSpeedMultiplier : 1f);
                if (Vec2.Dot(friend.Velocity, way) < 0.5f * wantedSpeed) return true; // Held up.
            }
            return false;
        }

        /// <summary>
        /// Decision D39: a walking unit that can leap pounces on a target within its leap range, landing
        /// against it, and jumps over whatever holds it up (a slower friend, a crowd), straight on along its way.
        /// </summary>
        private void UpdateLeaps()
        {
            foreach (SimUnit unit in _units)
            {
                float range = unit.Stats.LeapRange;
                if (range <= 0f || !unit.IsAlive || unit.IsLeaping) continue;
                if (unit.State != UnitState.Move)
                {
                    unit.HeldUpSeconds = 0f;
                    continue;
                }
                Vec2 way = unit.MoveGoal - unit.Position;
                if (way.LengthSquared < 1e-8f) continue;
                way = way.Normalized;
                unit.HeldUpSeconds = Vec2.Dot(unit.Velocity, way) < 0.5f * unit.MoveSpeedNow ? unit.HeldUpSeconds + _dt : 0f;
                if (ElapsedSeconds + 1e-4f < unit.LeapReadyAt) continue;

                SimUnit target = unit.MoveTarget;
                if (Targeting.IsTargetableEnemy(unit, target) && Targeting.EdgeDistance(unit, target) <= range)
                {
                    Vec2 fromTarget = (unit.Position - target.Position).Normalized;
                    StartLeap(unit, target.Position + fromTarget * (target.Radius + unit.Radius));
                }
                else if (unit.HeldUpSeconds >= _rules.LeapWhenHeldUpSeconds)
                {
                    StartLeap(unit, unit.Position + way * range);
                }
            }
        }

        private void StartLeap(SimUnit unit, Vec2 landing)
        {
            unit.IsLeaping = true;
            unit.LeapFrom = unit.Position;
            unit.LeapTo = landing;
            unit.LeapReadyAt = ElapsedSeconds + unit.Stats.LeapCooldownSeconds;
            unit.HeldUpSeconds = 0f;
            MarkEngaged(unit); // Its squad's formation is over.
        }

        /// <summary>How far this weapon notices enemies: weapon range x the acquisition multiplier, or unlimited.</summary>
        private float AcquisitionRange(SimUnit unit, WeaponStats weapon)
        {
            if (unit.UnlimitedAcquisition || _rules.AcquisitionRangeMultiplier <= 0f) return float.PositiveInfinity;
            return Math.Max(weapon.Range, unit.Stats.LeapRange) * _rules.AcquisitionRangeMultiplier; // A leap is its reach too.
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
            if (unit.Stats.SwitchesWeapons) ChooseWeaponByTarget(unit);
            if (unit.Stats.SeeksDensestCluster)
            {
                SimUnit cluster = DensestCluster(unit);
                if (cluster != null) unit.Weapons[unit.MainWeaponIndex].Target = cluster;
            }
            unit.MoveTarget = unit.Weapons[unit.MainWeaponIndex].Target;
        }

        /// <summary>The enemy with the most other enemies within the unit's death-burst radius (ties: the lowest id).</summary>
        private SimUnit DensestCluster(SimUnit unit)
        {
            WeaponStats weapon = unit.Weapons[unit.MainWeaponIndex].Stats;
            float radius = unit.Stats.DeathBurst?.Radius ?? 0f;
            float radiusSquared = radius * radius;
            SimUnit best = null;
            int bestCount = -1;
            foreach (SimUnit candidate in _units)
            {
                if (!Targeting.IsLegalTarget(unit, weapon, candidate)) continue;
                int count = 0;
                foreach (SimUnit other in _units)
                {
                    if (other.Team == candidate.Team && other.IsAlive && !other.IsAir && Vec2.DistanceSquared(other.Position, candidate.Position) <= radiusSquared) count++;
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>
        /// Units that switch weapons (ODSTs, D33) use one weapon at a time: the one whose target is the
        /// closest enemy. It decides where the unit moves; the other weapons wait.
        /// </summary>
        private static void ChooseWeaponByTarget(SimUnit unit)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int w = 0; w < unit.Weapons.Length; w++)
            {
                SimUnit target = unit.Weapons[w].Target;
                if (target == null) continue;
                float distance = Vec2.DistanceSquared(unit.Position, target.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = w;
                }
            }
            unit.MainWeaponIndex = best;
            for (int w = 0; w < unit.Weapons.Length; w++)
            {
                if (w != best) unit.Weapons[w].Target = null;
            }
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
            if (weaponIndex != unit.MainWeaponIndex || _rules.MainWeaponFacingToleranceDegrees >= 180f) return true;
            Vec2 toTarget = target.Position - unit.Position;
            if (toTarget.LengthSquared <= 1e-8f) return true;
            return Vec2.AngleDegrees(unit.Facing, toTarget) <= _rules.MainWeaponFacingToleranceDegrees + 0.01f;
        }

        private void FireWeapons()
        {
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive || unit.IsLeaping) continue;
                for (int w = 0; w < unit.Weapons.Length; w++)
                {
                    SimWeapon weapon = unit.Weapons[w];
                    weapon.Cooldown -= _dt;
                    if (weapon.BeamPhase != BeamPhase.Off)
                    {
                        UpdateBeam(unit, weapon);
                        continue;
                    }

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

                    Events.RaiseWeaponFired(unit, w, target);
                    MarkEngaged(unit);

                    if (unit.Stats.SelfDestructs && w == unit.MainWeaponIndex)
                    {
                        unit.Health = 0f;
                        break;
                    }
                    if (weapon.Stats.IsBeam)
                    {
                        weapon.BeamPhase = BeamPhase.Charging;
                        weapon.BeamTarget = target;
                        weapon.BeamPoint = target.Position;
                        weapon.BeamTicks = 0;
                        continue;
                    }

                    weapon.Cooldown += weapon.Stats.FireInterval;
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

        /// <summary>
        /// One tick of a beam (D37). While charging, its spot stays on the locked target (or where that
        /// target died). While firing, it hits everything around the spot every FireInterval, whether or
        /// not the target still lives; the spot moves at BeamSweepSpeed toward the locked target or, once
        /// that one is dead, toward the weapon's new target, and never leaves the weapon's reach.
        /// </summary>
        private void UpdateBeam(SimUnit unit, SimWeapon weapon)
        {
            WeaponStats stats = weapon.Stats;
            MarkEngaged(unit);
            if (!Targeting.IsTargetableEnemy(unit, weapon.BeamTarget)) weapon.BeamTarget = null;

            if (weapon.BeamPhase == BeamPhase.Charging)
            {
                if (weapon.BeamTarget != null) weapon.BeamPoint = weapon.BeamTarget.Position;
                if (++weapon.BeamTicks < TicksFor(stats.BeamChargeSeconds)) return;
                weapon.BeamPhase = BeamPhase.Firing;
                weapon.BeamTicks = 0;
                return;
            }

            if (weapon.BeamTarget == null && Targeting.IsTargetableEnemy(unit, weapon.Target)) weapon.BeamTarget = weapon.Target;
            if (weapon.BeamTarget != null)
            {
                Vec2 toTarget = weapon.BeamTarget.Position - weapon.BeamPoint;
                float step = stats.BeamSweepSpeed * _dt;
                weapon.BeamPoint = toTarget.Length <= step ? weapon.BeamTarget.Position : weapon.BeamPoint + toTarget.Normalized * step;
            }
            Vec2 fromUnit = weapon.BeamPoint - unit.Position;
            float reach = stats.Range * unit.RangeMultiplier + unit.Radius + stats.SplashRadius;
            if (fromUnit.Length > reach) weapon.BeamPoint = unit.Position + fromUnit.Normalized * reach;

            if (weapon.BeamTicks % TicksFor(stats.FireInterval) == 0) ResolveBeamHit(unit, stats, weapon.BeamPoint);
            if (++weapon.BeamTicks < TicksFor(stats.BeamSeconds)) return;
            weapon.BeamPhase = BeamPhase.Off;
            weapon.BeamTarget = null;
            weapon.Cooldown = stats.BeamRestSeconds;
        }

        private int TicksFor(float seconds) => Math.Max(1, (int)MathF.Round(seconds / _dt));

        /// <summary>One hit of a beam: every legal enemy around the spot.</summary>
        private void ResolveBeamHit(SimUnit attacker, WeaponStats weapon, Vec2 point)
        {
            _movement.Grid.Query(point, weapon.SplashRadius + _movement.Grid.MaxRadius, _nearby);
            foreach (SimUnit victim in _nearby)
            {
                if (!Targeting.IsTargetableEnemy(attacker, victim) || !weapon.CanTarget(attacker.Stats, victim.IsAir)) continue;
                if (Vec2.Distance(victim.Position, point) - victim.Radius > weapon.SplashRadius) continue;

                DefenseResult defense = DirectionalDefense.Evaluate(victim.Stats.DefenseArc, victim.Position,
                    victim.Facing, attacker.Position, weapon, false, _random);
                DealDamage(victim, attacker, weapon, defense.DamageMultiplier, true);
            }
        }

        /// <summary>The spot a lobbed shot should land on to meet a target that keeps moving the way it is now.</summary>
        private static Vec2 LeadPoint(Vec2 origin, SimUnit target, float projectileSpeed)
        {
            Vec2 point = target.Position;
            for (int i = 0; i < 3; i++) point = target.Position + target.Velocity * (Vec2.Distance(origin, point) / projectileSpeed);
            return point;
        }

        /// <summary>Every beam charging or firing right now, for drawing.</summary>
        public void GetBeams(List<BeamShot> beams)
        {
            beams.Clear();
            foreach (SimUnit unit in _units)
            {
                if (!unit.IsAlive) continue;
                foreach (SimWeapon weapon in unit.Weapons)
                {
                    if (weapon.BeamPhase == BeamPhase.Off) continue;
                    bool firing = weapon.BeamPhase == BeamPhase.Firing;
                    beams.Add(new BeamShot
                    {
                        Attacker = unit,
                        Point = weapon.BeamPoint,
                        Radius = weapon.Stats.SplashRadius,
                        Firing = firing,
                        Charge = firing ? 1f : (weapon.BeamTicks + 1f) / TicksFor(weapon.Stats.BeamChargeSeconds),
                    });
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
                DealDamage(directTarget, attacker, weapon, defense.DamageMultiplier, weapon.IsCone || weapon.SplashRadius > 0f);
                if (weapon.SeedSeconds > 0f) PlantSeed(directTarget, attacker, weapon);
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
                DealDamage(victim, attacker, weapon, defense.DamageMultiplier, true);
            }
        }

        /// <summary>Cone splash: every other legal enemy inside the cone from the source toward the impact point.</summary>
        private void ResolveCone(SimUnit attacker, WeaponStats weapon, SimUnit directTarget, Vec2 impactPoint, Vec2 sourcePosition)
        {
            Vec2 direction = impactPoint - sourcePosition;
            if (direction.LengthSquared <= 1e-8f) direction = attacker.Facing;
            float halfAngle = weapon.ConeDegrees * 0.5f;

            float coneRange = weapon.Range * attacker.RangeMultiplier;
            _movement.Grid.Query(sourcePosition, coneRange + _movement.Grid.MaxRadius, _nearby);
            foreach (SimUnit victim in _nearby)
            {
                if (victim == directTarget) continue;
                if (!Targeting.IsTargetableEnemy(attacker, victim) || !weapon.CanTarget(attacker.Stats, victim.IsAir)) continue;
                Vec2 toVictim = victim.Position - sourcePosition;
                if (toVictim.Length - victim.Radius > coneRange) continue;
                if (toVictim.LengthSquared > 1e-8f && Vec2.AngleDegrees(direction, toVictim) > halfAngle) continue;

                DefenseResult defense = DirectionalDefense.Evaluate(victim.Stats.DefenseArc, victim.Position,
                    victim.Facing, sourcePosition, weapon, false, _random);
                DealDamage(victim, attacker, weapon, defense.DamageMultiplier, true);
            }
        }

        /// <param name="areaHit">An area hit (splash, cone, beam) hurts every tiny form in a pack (D38).</param>
        private void DealDamage(SimUnit target, SimUnit attacker, WeaponStats weapon, float directionalMultiplier, bool areaHit)
        {
            float multiplier = (target.IsAir ? directionalMultiplier * weapon.AirDamageMultiplier : directionalMultiplier) * attacker.DamageMultiplier * attacker.FormsShare;
            if (areaHit) multiplier *= target.FormsAlive;
            float dealt = DamageCalculator.ApplyHit(_rules.DamageTable, weapon, target.Stats.ArmorType, multiplier,
                ref target.Shield, ref target.Health);
            target.SecondsSinceDamaged = 0f;
            attacker.DamageDealt += dealt;
            MarkEngaged(target);
            Events.RaiseUnitDamaged(target, attacker, dealt);
            if (attacker.Stats.LifestealShare > 0f && attacker.IsAlive && attacker.Health > 0f)
                attacker.Health = Math.Min(attacker.Stats.MaxHealth, attacker.Health + dealt * attacker.Stats.LifestealShare);

            if (target.Health <= 0f && target.KilledById < 0 && target.KilledBySpell == null)
            {
                target.KilledById = attacker.Id;
                attacker.Kills++;
            }
        }

        private void ApplyDeaths()
        {
            int count = _units.Count; // Death bursts add units; those can't die before they exist.
            for (int i = 0; i < count; i++)
            {
                SimUnit unit = _units[i];
                if (!unit.IsAlive || unit.Health > 0f) continue;
                unit.State = UnitState.Dead;
                unit.Velocity = Vec2.Zero;
                unit.MoveTarget = null;
                foreach (SimWeapon weapon in unit.Weapons) weapon.Target = null;
                SimUnit killer = unit.KilledById >= 0 ? _units[unit.KilledById] : null;
                Events.RaiseUnitDied(unit, killer);
                if (WillRise(unit))
                {
                    _rises.Add(new PendingRise { Corpse = unit, Team = unit.Team == Team.A ? Team.B : Team.A, At = ElapsedSeconds + _rules.InfectionDelaySeconds });
                    Events.RaiseUnitInfected(unit);
                }
                if (unit.Stats.DeathBurst != null) _bursting.Add(unit);
            }
            foreach (SimUnit unit in _bursting) ApplyDeathBurst(unit);
            _bursting.Clear();
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

            // A body about to rise is still on its way into the fight.
            foreach (PendingRise rise in _rises)
            {
                if (rise.Team == Team.A) aliveA++;
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

            // Spells: one line per spell name (several copies of one spell add up).
            foreach (SimSpell spell in _spells)
            {
                if (spell.Team != team) continue;
                UnitTypeResult line = teamResult.Spells.Find(s => s.UnitName == spell.Stats.Name);
                if (line == null)
                {
                    line = new UnitTypeResult { UnitName = spell.Stats.Name, Team = team };
                    teamResult.Spells.Add(line);
                }
                line.DamageDealt += spell.DamageDealt;
                line.Kills += spell.Kills;
            }
            return teamResult;
        }

        /// <summary>A killed unit about to rise for the other side.</summary>
        private struct PendingRise
        {
            public SimUnit Corpse;
            public Team Team;
            public float At;
        }

        /// <summary>A shot in flight. It follows its target and hits when it arrives; a lobbed one flies to a fixed spot.</summary>
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
                Target = weapon.Lobbed ? null : target;
                Origin = origin;
                Position = origin;
                AimPoint = weapon.Lobbed ? LeadPoint(origin, target, weapon.ProjectileSpeed) : target.Position;
            }
        }
    }
}
