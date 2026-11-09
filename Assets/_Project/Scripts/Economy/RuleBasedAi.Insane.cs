// RuleBasedAi.Insane.cs
// The Insane AI (D123): Hard, plus the habits the owner wins with, learned from their battle logs, and without the mistakes they beat
// Hard by. It sees nothing Hard doesn't (only what both players see: the result screen's kills and the Battle stats window) and earns the same.
//   - It reads the last battle. Each of its unit types that was farmed (lost most of its bodies while doing little for its worth) is wanted
//     less, so it stops feeding the opponent's best unit. The opponent's units that did the most damage count as bigger threats, so its
//     counters go at what actually hurt it, not only at what cost the most.
//   - Air is answered at once: while the opponent's air is not covered by what it owns that can shoot air, units that hit air are wanted much more.
//   - Carries: its one or two unit types that did the most last battle get their level-ups and techs before new squads (keeping enough for one
//     squad), then the items and Intensive Training. Up to two techs a round go to them.
//   - From round 5 a new unit type has to be clearly better than what it owns, unless it answers air it can't shoot.
//   - Economy cards are worth what they will still pay back (nothing from round 8). No tower levels, apart from a flanked tower as Hard; research from round 3.
//   - Placement: squads a reinforcement card brings go where their role belongs, as the draft squads do. New squads spread out from its own
//     clusters and from where the opponent's spells landed last battle, face the threat they counter (and air, if they shoot it), and the whole
//     layout shifts by an offset drawn each match, so it never places the same way twice.
// Spells aim as Hard's do, at the densest crowd: the owner found that killing the chaff that screens an army is worth it.
// Depends on: AiView (LastBattle, LastKills), BattleStats, MatchupTable, DeploymentGrid, Halobellum.Core.

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed partial class RuleBasedAi
    {
        /// <summary>How much less Insane wants a unit type that was fully farmed last battle, on the score's scale.</summary>
        private const float InsaneFarmedPenalty = 4f;
        /// <summary>How much more Insane wants a unit that hits only air, while the opponent's air is wholly uncovered.</summary>
        private const float InsaneAirAnswerBonus = 5f;
        /// <summary>How much more a type that did all the opponent's damage counts as a threat (one that did half counts double).</summary>
        private const float InsaneDamageThreatWeight = 2f;
        /// <summary>From this round a new unit type must beat what Insane owns by InsaneLateUnlockMargin.</summary>
        private const int InsaneLateUnlockRound = 5;
        private const float InsaneLateUnlockMargin = 0.75f;
        /// <summary>A carry did at least this share of its army's damage last battle.</summary>
        private const float InsaneCarryShare = 0.15f;
        /// <summary>The round from which an economy card is worth nothing (its income would not pay back).</summary>
        private const int InsaneLastEconomyRound = 8;

        private readonly Dictionary<UnitStats, float> _farmed = new Dictionary<UnitStats, float>();
        private readonly Dictionary<UnitStats, float> _damageShare = new Dictionary<UnitStats, float>();
        private readonly List<UnitStats> _carries = new List<UnitStats>();
        private readonly List<(Vec2 position, float radius)> _enemySpellSpots = new List<(Vec2, float)>();
        /// <summary>How much of the opponent's air its own anti-air does not cover, from 0 to 1.</summary>
        private float _airUncovered;
        private int _matchShift;
        private bool _matchShiftDrawn;

        private bool IsInsane => Difficulty == AiDifficulty.Insane;

        /// <summary>Insane's carries this round (its unit types that did the most last battle), for tests and debugging.</summary>
        public IReadOnlyList<UnitStats> Carries => _carries;

        /// <summary>How farmed this unit type was last battle, from 0 (fine) to 1 (lost everything and did nothing for its worth).</summary>
        public float FarmedShare(UnitStats type) => _farmed.TryGetValue(type, out float farmed) ? farmed : 0f;

        /// <summary>How much of the opponent's air is not covered by Insane's own anti-air, from 0 to 1.</summary>
        public float AirUncovered => _airUncovered;

        /// <summary>Insane: what the last battle says about its own units, the opponent's and the opponent's spells.</summary>
        private void ReadLastBattle(AiView view)
        {
            _farmed.Clear();
            _damageShare.Clear();
            _carries.Clear();
            _enemySpellSpots.Clear();
            _airUncovered = 0f;
            if (!_matchShiftDrawn)
            {
                _matchShiftDrawn = true;
                _matchShift = _random.RangeInt(-6, 7);
            }

            var ownDamage = new Dictionary<UnitStats, float>();
            var ownWorth = new Dictionary<UnitStats, float>();
            var ownBodies = new Dictionary<UnitStats, int>();
            var ownSurvivors = new Dictionary<UnitStats, int>();
            var enemyDamage = new Dictionary<UnitStats, float>();
            float ownTotalDamage = 0f, ownTotalWorth = 0f, enemyTotalDamage = 0f, enemyTotalWorth = 0f;
            BattleStats battle = view.LastBattle;
            if (battle != null)
            {
                foreach (BattleStatsLine line in battle.Lines)
                {
                    if (line.Kind == BattleStatsKind.Spell)
                    {
                        if (line.Team != view.Team && line.Spell != null) _enemySpellSpots.Add((line.Start, line.Spell.Radius));
                        continue;
                    }
                    if (line.Kind != BattleStatsKind.Squad || line.Type == null) continue;
                    if (line.Team == view.Team)
                    {
                        Add(ownDamage, line.Type, line.DamageDealt);
                        Add(ownWorth, line.Type, line.Type.Cost * Math.Max(1, line.Level));
                        ownBodies.TryGetValue(line.Type, out int bodies);
                        ownBodies[line.Type] = bodies + line.Bodies;
                        ownSurvivors.TryGetValue(line.Type, out int survivors);
                        ownSurvivors[line.Type] = survivors + line.Survivors;
                        ownTotalDamage += line.DamageDealt;
                        ownTotalWorth += line.Type.Cost * Math.Max(1, line.Level);
                    }
                    else
                    {
                        Add(enemyDamage, line.Type, line.DamageDealt);
                        enemyTotalDamage += line.DamageDealt;
                        enemyTotalWorth += line.Type.Cost * Math.Max(1, line.Level);
                    }
                }
            }

            if (enemyTotalDamage > 0f)
            {
                foreach (var pair in enemyDamage) _damageShare[pair.Key] = pair.Value / enemyTotalDamage;
            }

            // Farmed: most of its bodies died, and it did less for its worth than its army as a whole, or than the opponent's army did (so a whole
            // army of one type that was farmed counts too).
            float average = Math.Max(ownTotalWorth > 0f ? ownTotalDamage / ownTotalWorth : 0f, enemyTotalWorth > 0f ? enemyTotalDamage / enemyTotalWorth : 0f);
            foreach (var pair in ownWorth)
            {
                int bodies = ownBodies[pair.Key];
                if (bodies <= 0 || pair.Value <= 0f) continue;
                float lost = Math.Max(0f, Math.Min(1f, 1f - ownSurvivors[pair.Key] / (float)bodies));
                if (lost < 0.5f) continue;
                float efficiency = average > 0f ? ownDamage[pair.Key] / pair.Value / average : 1f;
                float farmed = lost * Math.Max(0f, 1f - efficiency);
                if (farmed > 0f) _farmed[pair.Key] = farmed;
            }

            // Carries: the types that did the most damage, at least InsaneCarryShare of it, and weren't farmed.
            if (ownTotalDamage > 0f)
            {
                var types = new List<UnitStats>(ownDamage.Keys);
                types.Sort((a, b) => ownDamage[b].CompareTo(ownDamage[a]));
                foreach (UnitStats type in types)
                {
                    if (_carries.Count >= 2 || ownDamage[type] < InsaneCarryShare * ownTotalDamage) break;
                    if (FarmedShare(type) < 0.5f && view.Self.Unlocked.Contains(type)) _carries.Add(type);
                }
            }

            // Air: what the opponent showed in the air against what it owns that can shoot air.
            float enemyAir = 0f, ownAntiAir = 0f;
            foreach (SeenSquad squad in view.SeenOpponentArmy) if (squad.Type.IsAir) enemyAir += squad.Value;
            foreach (PlacedSquad squad in view.Self.Squads) ownAntiAir += squad.Type.Cost * squad.Level * ShareThatHitsAir(squad.Type);
            if (enemyAir > 0f) _airUncovered = Math.Max(0f, Math.Min(1f, (1.2f * enemyAir - ownAntiAir) / enemyAir));
        }

        private static void Add(Dictionary<UnitStats, float> sums, UnitStats type, float amount)
        {
            sums.TryGetValue(type, out float sum);
            sums[type] = sum + amount;
        }

        /// <summary>How much a revealed enemy type counts as a threat: Insane weighs the damage it did last battle as well as its worth.</summary>
        private float ThreatWeight(UnitStats type) =>
            IsInsane && _damageShare.TryGetValue(type, out float share) ? 1f + InsaneDamageThreatWeight * share : 1f;

        /// <summary>Insane's change to a unit's score: less for a farmed type, more for a unit that hits air the opponent fields uncovered.</summary>
        private float InsaneAdjustment(UnitStats unit)
        {
            float adjust = -InsaneFarmedPenalty * FarmedShare(unit);
            if (_airUncovered > 0f) adjust += InsaneAirAnswerBonus * _airUncovered * ShareThatHitsAir(unit);
            return adjust;
        }

        /// <summary>How much better than what it owns a locked type must score before Insane unlocks it.</summary>
        private float InsaneUnlockHurdle(AiView view, UnitStats locked)
        {
            if (view.Round < InsaneLateUnlockRound) return 0f;
            if (_airUncovered > 0.3f && ShareThatHitsAir(locked) > 0.5f) return 0f;
            return InsaneLateUnlockMargin;
        }

        /// <summary>A general card's worth to Insane: an economy card is worth the income it will still bring in.</summary>
        private float InsaneCardValue(AiView view, CardStats card, float value)
        {
            if (card.BonusIncome <= 0 || card.Type == CardType.Spell || card.Type == CardType.Item || card.Type == CardType.Training) return value;
            return value - card.BonusIncome * 3f + card.BonusIncome * Math.Max(0f, InsaneLastEconomyRound - view.Round);
        }

        /// <summary>The carries' squads first (most valuable first), then the rest as they were.</summary>
        private List<PlacedSquad> CarriesFirst(List<PlacedSquad> squads)
        {
            var ordered = squads.FindAll(s => _carries.Contains(s.Type));
            ordered.AddRange(squads.FindAll(s => !_carries.Contains(s.Type)));
            return ordered;
        }

        /// <summary>
        /// Insane, before new squads: level up the carries' squads whose bar is full, then up to two techs for them, the most valuable first,
        /// keeping enough for one squad of the best unit it can buy.
        /// </summary>
        private void InvestInCarries(AiView view, IReadOnlyList<SeenSquad> seen, float totalSeen, List<string> upgrades)
        {
            if (_carries.Count == 0) return;
            PlayerState self = view.Self;
            UnitStats next = BestAffordable(view, seen, totalSeen, unlockedOnly: true);
            int reserve = next != null ? view.SquadPrice(next) : 0;

            var ready = new List<PlacedSquad>();
            foreach (PlacedSquad squad in self.Squads) if (_carries.Contains(squad.Type) && view.CanLevelUp(squad)) ready.Add(squad);
            ready.Sort((a, b) => (b.Type.Cost * b.Level).CompareTo(a.Type.Cost * a.Level));
            foreach (PlacedSquad squad in ready)
            {
                if (self.Credits - view.LevelUpCost(squad) < reserve) continue;
                if (view.LevelUp(squad).Ok) upgrades.Add($"{squad.Type.Name} to level {squad.Level} (carry)");
            }

            int techs = 0;
            foreach (UnitStats carry in _carries)
            {
                if (carry.TechUpgrades == null) continue;
                int invested = 0;
                foreach (PlacedSquad squad in self.Squads) if (squad.Type == carry) invested += carry.Cost * squad.Level;
                if (invested < carry.Cost * 2) continue;
                TechUpgrade best = null;
                float bestValue = 0f;
                foreach (TechUpgrade tech in carry.TechUpgrades)
                {
                    if (tech == null || self.HasTech(carry, tech) || self.Credits - tech.Cost < reserve) continue;
                    float value = TechValue(carry, tech);
                    if (value > bestValue) { bestValue = value; best = tech; }
                }
                if (best == null || !view.BuyTech(carry, best).Ok) continue;
                upgrades.Add($"{best.Name} ({carry.Name}, carry)");
                if (++techs >= 2) return;
            }
        }

        /// <summary>
        /// Insane's spot for a new squad: its role's spot shifted by this match's offset and turned toward what it should face, then of a few
        /// spots around that, the one furthest from its own clusters and from where the opponent's spells landed, without straying far.
        /// </summary>
        private bool InsaneFindSpot(AiView view, UnitStats type, int targetColumn, int targetRow, out int column, out int row)
        {
            column = row = 0;
            CombatRules rules = view.CombatRules;
            DeploymentGrid.Footprint(type, false, rules, out int width, out _);
            int arenaWidth = rules.ArenaWidthSquares, depth = rules.DeploymentDepthSquares;
            targetColumn += _matchShift;

            float? facing = FacingX(view, type);
            if (facing.HasValue)
            {
                float x0 = DeploymentGrid.WorldCentre(type, 0, targetRow, false, view.Team, rules).X;
                float x1 = DeploymentGrid.WorldCentre(type, 1, targetRow, false, view.Team, rules).X;
                if (Math.Abs(x1 - x0) > 1e-4f) targetColumn = (targetColumn + (int)Math.Round((facing.Value - x0) / (x1 - x0))) / 2;
            }

            bool front = type.Role == UnitRole.Chaff || type.Role == UnitRole.Tank || type.Role == UnitRole.Titan;
            float best = float.MaxValue;
            bool found = false;
            foreach (int dc in new[] { 0, -6, 6, -12, 12 })
            {
                foreach (int dr in front ? new[] { 0 } : new[] { 0, 2 })
                {
                    int c = Math.Max(0, Math.Min(arenaWidth - width, targetColumn + dc));
                    int r = Math.Max(0, Math.Min(depth - 1, targetRow + dr));
                    if (!DeploymentGrid.FindFreeSpotNear(view.Self, type, false, rules, c, r, out int freeColumn, out int freeRow)) continue;
                    float cost = SpotCost(view, type, freeColumn, freeRow) + 0.1f * (Math.Abs(freeColumn - targetColumn) + Math.Abs(freeRow - targetRow));
                    if (cost < best)
                    {
                        best = cost;
                        column = freeColumn;
                        row = freeRow;
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>How bad a spot is: the worth of its own squads close by (one area spell would hit them all), and the opponent's last spells there.</summary>
        private float SpotCost(AiView view, UnitStats type, int column, int row)
        {
            Vec2 centre = DeploymentGrid.WorldCentre(type, column, row, false, view.Team, view.CombatRules);
            float cost = 0f;
            foreach (PlacedSquad squad in view.Self.Squads)
            {
                if (Vec2.Distance(view.WorldCentre(squad), centre) <= 8f) cost += squad.Type.Cost * squad.Level / 100f;
            }
            foreach (var spell in _enemySpellSpots)
            {
                if (Vec2.Distance(spell.position, centre) <= spell.radius + 4f) cost += 3f;
            }
            return cost;
        }

        /// <summary>
        /// The battlefield X a new squad should face: the opponent's air for a unit that mostly shoots air, otherwise the revealed squad it counters
        /// best, weighted by how much of a threat that squad is. Null for chaff and flankers, or with nothing to face.
        /// </summary>
        private float? FacingX(AiView view, UnitStats type)
        {
            if (type.Role == UnitRole.Chaff || type.Role == UnitRole.Flanker) return null;
            IReadOnlyList<SeenSquad> seen = view.SeenOpponentArmy;
            if (ShareThatHitsAir(type) > 0.5f && type.TargetFilter != TargetFilter.Ground)
            {
                float sumX = 0f, sumValue = 0f;
                foreach (SeenSquad squad in seen)
                {
                    if (!squad.Type.IsAir) continue;
                    sumX += view.WorldCentre(squad).X * squad.Value;
                    sumValue += squad.Value;
                }
                if (sumValue > 0f) return sumX / sumValue;
            }
            if (_matchups == null) return null;
            float bestScore = 0f;
            float? bestX = null;
            foreach (SeenSquad squad in seen)
            {
                float? rate = _matchups.WinRate(type, squad.Type);
                if (!rate.HasValue || rate.Value <= 0.55f) continue;
                float score = (rate.Value - 0.5f) * squad.Value * ThreatWeight(squad.Type);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = view.WorldCentre(squad).X;
                }
            }
            return bestX;
        }
    }
}
