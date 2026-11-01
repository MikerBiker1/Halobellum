// RuleBasedAi.cs
// The rule-based AI opponent (Bible Section 11, Milestone 5). Each round it:
//   1. Looks at the opponent's army as it was revealed in the last battle (never its current
//      deployment) and finds the role the opponent spent the most credits on.
//   2. Picks a strategy: swarm, armor push, air raid or balanced. Each strategy prefers some roles.
//   3. Scores every unit type in its roster: the strategy's preference for the unit's role, plus a
//      bonus if the unit counters what the opponent fields (Section 7 counter table), plus a small
//      bonus for higher tiers.
//   4. Unlocks at most one unit type and buys at most 2 squads (the same limits as the player),
//      using the same Match actions.
//   5. Places squads by role: chaff and tanks in front, ranged units behind, flankers on the sides.
// Difficulty only changes decision quality, never information or income:
//   Easy   - picks strategies partly at random, counters poorly, often leaves credits unspent, and
//            places squads sloppily.
//   Normal - counters the opponent's most expensive role sensibly.
//   Hard   - weighs the whole revealed army using measured matchups (MatchupTable: how often each
//            unit beats each other unit at equal value, from the counter web), so it picks real
//            counters and avoids units the opponent's army beats; changes strategy when the last
//            round went badly. Without a table it falls back to the role table.
// Depends on: AiView, IMatchAi, DeploymentGrid, MatchupTable, Halobellum.Combat (CounterWeb role counters),
//             Halobellum.Core (UnitStats, UnitRole, SimRandom).

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public enum AiStrategy
    {
        Swarm = 0,
        ArmorPush = 1,
        AirRaid = 2,
        Balanced = 3,
    }

    public enum AiDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    public sealed class RuleBasedAi : IMatchAi
    {
        private readonly SimRandom _random;
        private readonly MatchupTable _matchups;
        private bool _movedDraftSquads;

        /// <param name="matchups">Measured unit-vs-unit win rates (used by Hard). Null = role table only.</param>
        public RuleBasedAi(AiDifficulty difficulty, int seed, MatchupTable matchups = null)
        {
            Difficulty = difficulty;
            _random = new SimRandom(unchecked(seed * 31 + 17 + (int)difficulty));
            _matchups = matchups != null && matchups.Count > 0 ? matchups : null;
        }

        private bool UsesMatchups => Difficulty == AiDifficulty.Hard && _matchups != null;

        /// <summary>
        /// How much better than even this unit does against the revealed army (-0.5 to +0.5), from the
        /// matchup table. Each enemy unit type counts by how much the opponent spent on it, minus what
        /// the AI's own army already beats, so new squads go where the threat is not yet covered.
        /// </summary>
        private float MatchupAdvantage(UnitStats unit, IReadOnlyList<SeenSquad> seen)
        {
            if (_threats == null) return 0f;
            float sum = 0f, weightTotal = 0f;
            foreach (var threat in _threats)
            {
                float? rate = _matchups.WinRate(unit, threat.Key);
                if (!rate.HasValue) continue;
                sum += threat.Value * (rate.Value - 0.5f);
                weightTotal += threat.Value;
            }
            return weightTotal > 0f ? sum / weightTotal : 0f;
        }

        private Dictionary<UnitStats, float> _threats;

        /// <summary>Hard: how much of each revealed enemy unit type the AI's own army does not beat yet.</summary>
        private void UpdateThreats(AiView view, IReadOnlyList<SeenSquad> seen)
        {
            _threats = new Dictionary<UnitStats, float>();
            foreach (SeenSquad squad in seen)
            {
                _threats.TryGetValue(squad.Type, out float value);
                _threats[squad.Type] = value + squad.Value;
            }
            foreach (UnitStats type in new List<UnitStats>(_threats.Keys))
            {
                float covered = 0f;
                foreach (PlacedSquad own in view.Self.Squads)
                {
                    float? rate = _matchups.WinRate(own.Type, type);
                    if (rate.HasValue) covered += own.Type.Cost * own.Level * System.Math.Max(0f, 2f * (rate.Value - 0.5f));
                }
                float threat = _threats[type];
                _threats[type] = System.Math.Max(threat * 0.25f, threat - covered);
            }
        }

        public AiDifficulty Difficulty { get; }
        public string Label => $"{Difficulty} AI";
        /// <summary>The strategy picked for the current round.</summary>
        public AiStrategy Strategy { get; private set; } = AiStrategy.Balanced;
        /// <summary>The role the AI decided to counter this round (null when it has seen nothing yet).</summary>
        public UnitRole? CounteredRole { get; private set; }
        /// <summary>A one-line summary of the last round's decisions (for tests and debugging).</summary>
        public string LastDecision { get; private set; } = "";

        // How much each strategy wants each role (index = UnitRole): Chaff, ChaffClear, SingleTargetDps,
        // Tank, Flanker, Artillery, AntiAir, Aerial, Titan.
        private static readonly float[][] StrategyWeights =
        {
            new[] { 3.0f, 1.5f, 0.5f, 0.5f, 2.0f, 0.5f, 0.5f, 0.5f, 0.5f }, // Swarm
            new[] { 1.0f, 0.5f, 2.0f, 3.0f, 0.5f, 1.0f, 0.5f, 0.0f, 3.0f }, // Armor push
            new[] { 1.0f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 1.5f, 3.0f, 0.5f }, // Air raid
            new[] { 1.5f, 1.2f, 1.5f, 1.5f, 1.0f, 1.0f, 0.8f, 1.0f, 1.0f }, // Balanced
        };

        // ------------------------------------------------------------------ draft

        public int ChooseDraftCard(AiView view, IReadOnlyList<DraftCard> cards)
        {
            if (cards.Count == 0) return 0;
            if (Difficulty == AiDifficulty.Easy) return _random.RangeInt(0, cards.Count);
            // The card whose free squads are worth the most.
            int best = 0, bestValue = -1;
            for (int i = 0; i < cards.Count; i++)
            {
                int value = 0;
                foreach (UnitStats squad in cards[i].Squads) value += squad.Cost;
                if (value > bestValue) { bestValue = value; best = i; }
            }
            return best;
        }

        // ------------------------------------------------------------------ deployment

        public void Deploy(AiView view)
        {
            PlayerState self = view.Self;
            if (!self.HasFaction) return;

            // 1. What did the opponent field last battle?
            var seen = view.SeenOpponentArmy;
            var valueByRole = new float[Enum.GetValues(typeof(UnitRole)).Length];
            float totalSeen = 0f;
            foreach (SeenSquad squad in seen)
            {
                valueByRole[(int)squad.Type.Role] += squad.Value;
                totalSeen += squad.Value;
            }
            CounteredRole = null;
            if (totalSeen > 0f)
            {
                int top = 0;
                for (int r = 1; r < valueByRole.Length; r++) if (valueByRole[r] > valueByRole[top]) top = r;
                CounteredRole = (UnitRole)top;
            }

            // 2. Strategy.
            if (UsesMatchups) UpdateThreats(view, seen);
            Strategy = ChooseStrategy(view, seen, valueByRole, totalSeen);

            // In round 1, put the free draft squads where their role belongs.
            if (!_movedDraftSquads)
            {
                _movedDraftSquads = true;
                foreach (PlacedSquad squad in self.Squads.ToArray())
                {
                    if (!view.CanMove(squad)) continue;
                    if (FindSpotFor(view, squad.Type, squad.Id, out int column, out int row)) view.Move(squad, column, row, false);
                }
            }

            // 3-4. Unlock at most one unit type, then buy up to the squad limit.
            var bought = new List<string>();
            string unlocked = TryUnlock(view, seen, totalSeen);
            for (int i = 0; i < view.Rules.NewSquadsPerRound; i++)
            {
                if (Difficulty == AiDifficulty.Easy && _random.NextFloat() < 0.35f) break; // Easy wastes budget.
                UnitStats choice = BestAffordable(view, seen, totalSeen, unlockedOnly: true);
                if (choice == null) break;
                if (!FindSpotFor(view, choice, -1, out int column, out int row)) break;
                if (!view.Buy(choice, column, row, false).Ok) break;
                bought.Add(choice.Name);
                if (UsesMatchups) UpdateThreats(view, seen); // The new squad covers part of the threat.
            }

            LastDecision = $"Round {view.Round}: {Strategy}" + (CounteredRole.HasValue ? $", countering {CounteredRole}" : "")
                           + (unlocked != null ? $", unlocked {unlocked}" : "")
                           + (bought.Count > 0 ? ", bought " + string.Join(" + ", bought) : ", bought nothing")
                           + $", {self.Credits} credits left";
        }

        private AiStrategy ChooseStrategy(AiView view, IReadOnlyList<SeenSquad> seen, float[] valueByRole, float totalSeen)
        {
            if (Difficulty == AiDifficulty.Easy && (totalSeen <= 0f || _random.NextFloat() < 0.5f))
                return (AiStrategy)_random.RangeInt(0, 4);
            if (totalSeen <= 0f) return AiStrategy.Balanced;

            // How well each strategy's preferred roles counter what the opponent fields.
            var fit = new float[4];
            if (UsesMatchups)
            {
                // Hard with measured matchups: the strategy whose favourite roles hold the units that do best.
                foreach (UnitStats unit in view.Self.Roster)
                {
                    float advantage = MatchupAdvantage(unit, seen);
                    if (advantage <= 0f) continue;
                    for (int s = 0; s < 4; s++) fit[s] += StrategyWeights[s][(int)unit.Role] * advantage;
                }
            }
            else for (int s = 0; s < 4; s++)
            {
                for (int role = 0; role < StrategyWeights[s].Length; role++)
                {
                    if (Difficulty == AiDifficulty.Normal)
                    {
                        // Normal: only the opponent's most expensive role.
                        if (CountersRole((UnitRole)role, CounteredRole.Value, seen)) fit[s] += StrategyWeights[s][role];
                    }
                    else
                    {
                        // Hard: every role the opponent fields, by how much it spent on it, minus roles that are countered by it.
                        for (int target = 0; target < valueByRole.Length; target++)
                        {
                            if (valueByRole[target] <= 0f) continue;
                            float share = valueByRole[target] / totalSeen;
                            if (CountersRole((UnitRole)role, (UnitRole)target, seen)) fit[s] += StrategyWeights[s][role] * share;
                            if (CountersRole((UnitRole)target, (UnitRole)role, seen)) fit[s] -= StrategyWeights[s][role] * share * 0.5f;
                        }
                    }
                }
            }

            // Hard adapts: after a bad round (more base damage taken than dealt) it avoids repeating its strategy.
            if (Difficulty == AiDifficulty.Hard && view.Round > 1 && view.LastBaseDamageTaken > view.LastBaseDamageDealt)
                fit[(int)Strategy] *= 0.6f;

            int best = 0;
            for (int s = 1; s < 4; s++) if (fit[s] > fit[best] + 1e-4f) best = s;
            return (AiStrategy)best;
        }

        /// <summary>True if units of counterRole counter units of targetRole (anti-air: any ground role that is not anti-air).</summary>
        private static bool CountersRole(UnitRole counterRole, UnitRole targetRole, IReadOnlyList<SeenSquad> seen)
        {
            if (targetRole == UnitRole.AntiAir) return counterRole != UnitRole.AntiAir && counterRole != UnitRole.Aerial;
            return CounterWeb.RoleCounters(counterRole, targetRole);
        }

        /// <summary>How much the AI wants a unit type this round.</summary>
        private float Score(UnitStats unit, IReadOnlyList<SeenSquad> seen, float totalSeen)
        {
            float score = StrategyWeights[(int)Strategy][(int)unit.Role] + 0.4f * unit.Tier;
            if (totalSeen > 0f)
            {
                switch (Difficulty)
                {
                    case AiDifficulty.Easy:
                        if (CounteredRole.HasValue && CountersSeenRole(unit, CounteredRole.Value, seen)) score += 1f;
                        score += _random.Range(-1.5f, 1.5f);
                        break;
                    case AiDifficulty.Normal:
                        if (CounteredRole.HasValue && CountersSeenRole(unit, CounteredRole.Value, seen)) score += 3f;
                        score += _random.Range(-0.3f, 0.3f);
                        break;
                    default:
                        if (UsesMatchups)
                        {
                            // Measured counters dominate; the strategy only nudges.
                            score = 0.5f * StrategyWeights[(int)Strategy][(int)unit.Role] + 0.4f * unit.Tier
                                    + 8f * MatchupAdvantage(unit, seen) + 4f * (_matchups.OverallStrength(unit) - 0.5f)
                                    + _random.Range(-0.1f, 0.1f);
                            break;
                        }
                        float counters = 0f, countered = 0f;
                        foreach (SeenSquad squad in seen)
                        {
                            if (CounterWeb.IsCounter(unit, squad.Type)) counters += squad.Value;
                            if (CounterWeb.IsCounter(squad.Type, unit)) countered += squad.Value;
                        }
                        score += 4f * counters / totalSeen - 2f * countered / totalSeen;
                        score += _random.Range(-0.1f, 0.1f);
                        break;
                }
            }
            else
            {
                score += _random.Range(-0.3f, 0.3f);
            }
            return score;
        }

        private static bool CountersSeenRole(UnitStats unit, UnitRole role, IReadOnlyList<SeenSquad> seen)
        {
            foreach (SeenSquad squad in seen)
            {
                if (squad.Type.Role == role && CounterWeb.IsCounter(unit, squad.Type)) return true;
            }
            return false;
        }

        /// <summary>The best unit type it can afford now (unlocked only, or any it could unlock and buy).</summary>
        private UnitStats BestAffordable(AiView view, IReadOnlyList<SeenSquad> seen, float totalSeen, bool unlockedOnly)
        {
            PlayerState self = view.Self;
            UnitStats best = null;
            float bestScore = float.MinValue;
            foreach (UnitStats unit in self.Roster)
            {
                bool isUnlocked = self.Unlocked.Contains(unit);
                if (unlockedOnly && !isUnlocked) continue;
                int price = unit.Cost + (isUnlocked ? 0 : unit.UnlockCost);
                if (price > self.Credits) continue;
                if (Difficulty == AiDifficulty.Easy && _random.NextFloat() < 0.2f) return unit; // Easy sometimes grabs whatever comes first.
                float score = Score(unit, seen, totalSeen);
                if (score > bestScore) { bestScore = score; best = unit; }
            }
            return best;
        }

        /// <summary>Unlocks the best locked unit type it can afford to unlock and buy, if it beats what is already unlocked.</summary>
        private string TryUnlock(AiView view, IReadOnlyList<SeenSquad> seen, float totalSeen)
        {
            PlayerState self = view.Self;
            if (self.UnlocksThisRound >= view.Rules.UnlocksPerRound) return null;
            UnitStats bestLocked = null, bestOwned = BestAffordable(view, seen, totalSeen, unlockedOnly: true);
            float lockedScore = float.MinValue;
            foreach (UnitStats unit in self.Roster)
            {
                if (self.Unlocked.Contains(unit) || unit.UnlockCost + unit.Cost > self.Credits) continue;
                float score = Score(unit, seen, totalSeen);
                if (score > lockedScore) { lockedScore = score; bestLocked = unit; }
            }
            if (bestLocked == null) return null;
            float ownedScore = bestOwned != null ? Score(bestOwned, seen, totalSeen) : float.MinValue;
            if (lockedScore <= ownedScore && Difficulty != AiDifficulty.Easy) return null;
            return view.Unlock(bestLocked).Ok ? bestLocked.Name : null;
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Where this AI would place a new squad of this type now (its role-based spot).</summary>
        public bool ChooseSpot(AiView view, UnitStats type, out int column, out int row) => FindSpotFor(view, type, -1, out column, out row);

        /// <summary>
        /// Where a squad of this type goes (Section 11): chaff and tanks in front, single-target DPS and
        /// chaff clear just behind, anti-air and aircraft further back, artillery at the back, flankers
        /// on the sides. Squads of the same kind spread out from the centre (or from their side).
        /// </summary>
        private bool FindSpotFor(AiView view, UnitStats type, int ignoreSquadId, out int column, out int row)
        {
            CombatRules rules = view.CombatRules;
            DeploymentGrid.Footprint(type, false, rules, out int width, out _);
            int depth = rules.DeploymentDepthSquares, arenaWidth = rules.ArenaWidthSquares;
            int targetRow;
            switch (type.Role)
            {
                case UnitRole.Chaff: targetRow = 0; break;
                case UnitRole.Tank:
                case UnitRole.Titan: targetRow = 3; break;
                case UnitRole.Flanker: targetRow = 2; break;
                case UnitRole.ChaffClear: targetRow = 6; break;
                case UnitRole.SingleTargetDps: targetRow = 8; break;
                case UnitRole.AntiAir: targetRow = depth / 4; break;
                case UnitRole.Aerial: targetRow = depth / 3; break;
                default: targetRow = depth / 2; break; // Artillery.
            }

            int sameKind = 0;
            foreach (PlacedSquad squad in view.Self.Squads) if (squad.Id != ignoreSquadId && squad.Type.Role == type.Role) sameKind++;
            int targetColumn;
            if (type.Role == UnitRole.Flanker)
            {
                // Alternate between the left and right edges.
                targetColumn = sameKind % 2 == 0 ? 2 : arenaWidth - width - 2;
            }
            else
            {
                // Centre, then alternately right and left of it.
                int step = (sameKind + 1) / 2 * (sameKind % 2 == 1 ? 1 : -1);
                targetColumn = (arenaWidth - width) / 2 + step * (width + 2);
            }

            if (Difficulty == AiDifficulty.Easy)
            {
                targetColumn += _random.RangeInt(-12, 13);
                targetRow += _random.RangeInt(0, 8);
            }
            targetColumn = Math.Max(0, Math.Min(arenaWidth - width, targetColumn));
            targetRow = Math.Max(0, Math.Min(depth - 1, targetRow));

            if (ignoreSquadId < 0) return DeploymentGrid.FindFreeSpotNear(view.Self, type, false, rules, targetColumn, targetRow, out column, out row);

            // Moving an existing squad: search as if it were not there.
            int index = view.Self.Squads.FindIndex(s => s.Id == ignoreSquadId);
            PlacedSquad moving = view.Self.Squads[index];
            view.Self.Squads.RemoveAt(index);
            bool found = DeploymentGrid.FindFreeSpotNear(view.Self, type, false, rules, targetColumn, targetRow, out column, out row);
            view.Self.Squads.Insert(index, moving);
            return found;
        }
    }
}
