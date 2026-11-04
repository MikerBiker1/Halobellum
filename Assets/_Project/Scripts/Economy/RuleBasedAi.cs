// RuleBasedAi.cs
// The rule-based AI opponent (Bible Section 11, Milestone 5). Each round it:
//   1. Looks at the opponent's army as it was revealed in the last battle (never its current
//      deployment) and finds the role the opponent spent the most credits on.
//   2. Picks a strategy: swarm, armor push, air raid or balanced. Each strategy prefers some roles.
//   3. Scores every unit type in its roster: the strategy's preference for the unit's role, plus a
//      bonus if the unit counters what the opponent fields (Section 7 counter table), plus a small
//      bonus for higher tiers and, on Normal and Hard, for its sub-faction's exclusive units (M10).
//   4. Unlocks at most one unit type and buys at most 2 squads (the same limits as the player),
//      using the same Match actions. Then it spends what is left on level-ups (squads with a full
//      XP bar, most valuable first) and tech upgrades (for the unit type it has the most of).
//   5. Places squads by role: chaff and tanks in front, ranged units behind, flankers on the sides.
//   6. Cards (Milestone 7, decisions D32 and D33): takes the reinforcement card that adds the most
//      worth for its price (Hard also weighs how well the unit counters the revealed army), or the
//      general card (spell, item, economy, buff, training) it values most and can afford while keeping
//      a reserve; skips when nothing beats the skip bonus. Places its ready spells: strikes, zones,
//      EMPs and smoke on the densest part of the opponent's army as revealed at the last battle start,
//      drop pods on the opponent's most valuable revealed squad, buffs on its own densest group. Puts
//      items and Intensive Training on its most valuable squads. It never sees the opponent's spells,
//      items or new squads until they have been revealed in a battle.
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

        /// <summary>What a draft card's free squad is worth extra to Normal and Hard if it is an exclusive unit, in credits (M10).</summary>
        private const int ExclusiveDraftBonus = 40;
        /// <summary>How much more Normal and Hard want their sub-faction's exclusive units, on the same scale as the strategy's weights (M10).</summary>
        private const float ExclusiveScoreBonus = 0.6f;
        /// <summary>The player this AI is deploying for right now.</summary>
        private PlayerState _self;

        private bool IsExclusive(UnitStats unit) => _self?.SubFaction != null && _self.SubFaction.ExclusiveUnits.Contains(unit);

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
                foreach (UnitStats squad in cards[i].Squads)
                {
                    value += squad.Cost;
                    if (cards[i].SubFaction != null && cards[i].SubFaction.ExclusiveUnits.Contains(squad)) value += ExclusiveDraftBonus;
                }
                if (value > bestValue) { bestValue = value; best = i; }
            }
            return best;
        }

        // ------------------------------------------------------------------ cards and spells (Milestone 7)

        public int ChooseCard(AiView view, CardOffer offer)
        {
            if (offer == null || offer.Count == 0) return -1;
            int credits = view.Self.Credits;
            if (Difficulty == AiDifficulty.Easy)
            {
                // Easy: half the time it skips, otherwise it takes a random card it can afford.
                if (_random.NextFloat() < 0.5f) return -1;
                int pick = _random.RangeInt(0, offer.Count);
                return offer.PriceOf(pick) <= credits ? pick : -1;
            }

            int best = -1;
            float bestValue = offer.SkipBonus; // A card has to be worth more than skipping.
            for (int i = 0; i < offer.Count; i++)
            {
                int price = offer.PriceOf(i);
                if (price > credits) continue;
                float value;
                if (offer.Kind == CardKind.Reinforcement)
                {
                    ReinforcementCard card = offer.Reinforcements[i];
                    value = card.TotalWorth - price; // Free worth gained.
                    if (UsesMatchups) value *= 1f + MatchupAdvantage(card.Type, view.SeenOpponentArmy) + 0.5f * (_matchups.OverallStrength(card.Type) - 0.5f);
                }
                else
                {
                    // Keep enough for squads this round (Normal keeps more than Hard).
                    int reserve = Difficulty == AiDifficulty.Hard ? 100 : 200;
                    if (credits - price < reserve) continue;
                    value = CardValue(view, offer.Cards[i]);
                }
                if (value > bestValue)
                {
                    bestValue = value;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>A rough worth of a general card in credits, to compare the cards with each other and with skipping.</summary>
        private static float CardValue(AiView view, CardStats card)
        {
            float army = 0f, bestSquad = 100f;
            foreach (PlacedSquad squad in view.Self.Squads)
            {
                float worth = squad.Type.Cost * squad.Level;
                army += worth;
                bestSquad = Math.Max(bestSquad, worth);
            }
            switch (card.Type)
            {
                case CardType.Spell: return SpellImpact(card.Spell);
                case CardType.Economy: return card.BonusIncome * 3f;
                case CardType.Training: return 150f;
                case CardType.Item:
                    ItemStats item = card.Item;
                    return bestSquad * (item.DamageMultiplier - 1f + item.HealthMultiplier - 1f + 0.5f * (item.MoveSpeedMultiplier - 1f)
                                        + 0.02f * item.RangeBonus + 0.4f * item.LifestealShare + 8f * item.HealPerSecondShare);
                default:
                    return army * (card.AllDamageMultiplier - 1f + card.AllHealthMultiplier - 1f + 0.03f * card.AllRangeBonus)
                           + card.BonusIncome * 3f + card.ExtraSquadsPerRound * 150f;
            }
        }

        /// <summary>A rough worth of a spell in credits.</summary>
        private static float SpellImpact(SpellStats spell)
        {
            switch (spell.Effect)
            {
                case SpellEffect.Disrupt: return 60f + spell.Radius * 10f;
                case SpellEffect.Smoke: return 110f;
                case SpellEffect.Damage: return spell.DamagePerTick * Math.Max(1, spell.Ticks) * spell.Radius * 0.15f / (1f + spell.CooldownRounds * 0.5f);
                case SpellEffect.DropSquad: return (spell.DropUnit?.Cost ?? 0) * spell.DropSquads / (1f + spell.CooldownRounds * 0.5f);
                default: return 60f + spell.Radius * 10f;
            }
        }

        /// <summary>Places every ready spell for this round (see the header). Easy forgets half of them.</summary>
        private void PlaceSpells(AiView view, List<string> placed)
        {
            foreach (OwnedSpell spell in view.Spells)
            {
                if (!spell.IsReady(view.Round) || spell.IsPlaced) continue;
                if (Difficulty == AiDifficulty.Easy && _random.NextFloat() < 0.5f) continue;
                Vec2? target = null;
                if (spell.Stats.Effect == SpellEffect.Buff)
                {
                    var own = new List<(Vec2, float)>();
                    foreach (PlacedSquad squad in view.Self.Squads) own.Add((view.WorldCentre(squad), squad.Type.Cost * squad.Level));
                    target = DensestSpot(own, spell.Stats.Radius);
                }
                else
                {
                    var seen = new List<(Vec2, float)>();
                    foreach (SeenSquad squad in view.SeenOpponentArmy) seen.Add((view.WorldCentre(squad), squad.Value));
                    if (spell.Stats.Effect == SpellEffect.DropSquad) target = MostValuable(seen);
                    else target = Difficulty == AiDifficulty.Easy && seen.Count > 0 ? seen[_random.RangeInt(0, seen.Count)].Item1 : DensestSpot(seen, spell.Stats.Radius);
                }
                if (target.HasValue && view.PlaceSpell(spell, target.Value).Ok) placed.Add(spell.Stats.Name);
            }
        }

        /// <summary>
        /// Items go on its most valuable squads without one (Easy: any squad); a Deployment Module, which the
        /// AI has no use for, on its cheapest. Intensive Training on its most valuable squad that can use it.
        /// </summary>
        private void UseItemsAndTraining(AiView view, List<string> upgrades)
        {
            var squads = new List<PlacedSquad>(view.Self.Squads);
            squads.Sort((a, b) => (b.Type.Cost * b.Level).CompareTo(a.Type.Cost * a.Level));
            foreach (ItemStats item in new List<ItemStats>(view.Inventory))
            {
                var free = squads.FindAll(s => s.Item == null);
                if (free.Count == 0) break;
                PlacedSquad target = Difficulty == AiDifficulty.Easy ? free[_random.RangeInt(0, free.Count)] : item.FreeDeployment ? free[free.Count - 1] : free[0];
                if (view.PlaceItem(item, target).Ok) upgrades.Add($"{item.Name} on {target.Type.Name}");
            }
            while (view.CanUseTraining)
            {
                PlacedSquad trainee = squads.Find(s => s.Level < UnitUpgrades.MaxLevel && !view.CanLevelUp(s));
                if (trainee == null || !view.UseTraining(trainee).Ok) break;
                upgrades.Add($"training for {trainee.Type.Name}");
            }
        }

        /// <summary>The squad position with the most value within `radius` (+3 m) around it, or null.</summary>
        private static Vec2? DensestSpot(List<(Vec2 position, float value)> squads, float radius)
        {
            Vec2? best = null;
            float bestValue = 0f;
            foreach (var centre in squads)
            {
                float value = 0f;
                foreach (var other in squads)
                {
                    if (Vec2.Distance(centre.position, other.position) <= radius + 3f) value += other.value;
                }
                if (value > bestValue)
                {
                    bestValue = value;
                    best = centre.position;
                }
            }
            return best;
        }

        private static Vec2? MostValuable(List<(Vec2 position, float value)> squads)
        {
            Vec2? best = null;
            float bestValue = 0f;
            foreach (var squad in squads)
            {
                if (squad.value > bestValue)
                {
                    bestValue = squad.value;
                    best = squad.position;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ deployment

        public void Deploy(AiView view)
        {
            PlayerState self = view.Self;
            if (!self.HasFaction) return;
            _self = self;

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
            var upgrades = new List<string>();
            string unlocked = TryUnlock(view, seen, totalSeen);
            if (Difficulty != AiDifficulty.Easy) BuyCarrierTechs(view, upgrades);
            if (Difficulty == AiDifficulty.Hard && self.Towers.Count > 0 && self.Credits >= HardEliteCredits) view.BuyEliteRecruitment();
            for (int i = 0; i < view.SquadsPerRound; i++)
            {
                if (Difficulty == AiDifficulty.Easy && _random.NextFloat() < 0.35f) break; // Easy wastes budget.
                UnitStats choice = BestAffordable(view, seen, totalSeen, unlockedOnly: true);
                if (choice == null) break;
                if (!FindSpotFor(view, choice, -1, out int column, out int row)) break;
                if (!view.Buy(choice, column, row, false).Ok) break;
                bought.Add(choice.Name);
                if (UsesMatchups) UpdateThreats(view, seen); // The new squad covers part of the threat.
            }
            // Hard with credits to spare after its squads: one slot more.
            if (Difficulty == AiDifficulty.Hard && self.Towers.Count > 0 && self.Credits >= HardExtraSlotCredits && view.BuyExtraSlot().Ok)
            {
                UnitStats choice = BestAffordable(view, seen, totalSeen, unlockedOnly: true);
                if (choice != null && FindSpotFor(view, choice, -1, out int column, out int row) && view.Buy(choice, column, row, false).Ok) bought.Add(choice.Name);
            }

            // 5. Level-ups and techs with the credits left, then spells.
            UseItemsAndTraining(view, upgrades);
            SpendOnUpgrades(view, upgrades);
            SpendOnTowers(view, seen, upgrades);
            var spells = new List<string>();
            PlaceSpells(view, spells);

            LastDecision = $"Round {view.Round}: {Strategy}" + (CounteredRole.HasValue ? $", countering {CounteredRole}" : "")
                           + (unlocked != null ? $", unlocked {unlocked}" : "")
                           + (bought.Count > 0 ? ", bought " + string.Join(" + ", bought) : ", bought nothing")
                           + (upgrades.Count > 0 ? ", upgraded " + string.Join(" + ", upgrades) : "")
                           + (spells.Count > 0 ? ", cast " + string.Join(" + ", spells) : "")
                           + $", {self.Credits} credits left";
        }

        /// <summary>
        /// Level-ups first (squads with a full XP bar, most valuable first), then one tech for the unit
        /// type it has invested the most in. Easy rarely bothers; Normal and Hard keep a small reserve
        /// only before round 3 (squads and unlocks matter more early).
        /// </summary>
        private void SpendOnUpgrades(AiView view, List<string> upgrades)
        {
            PlayerState self = view.Self;
            if (Difficulty == AiDifficulty.Easy && _random.NextFloat() < 0.7f) return;

            var ready = new List<PlacedSquad>();
            foreach (PlacedSquad squad in self.Squads) if (view.CanLevelUp(squad)) ready.Add(squad);
            ready.Sort((a, b) => b.Type.Cost.CompareTo(a.Type.Cost));
            foreach (PlacedSquad squad in ready)
            {
                if (view.LevelUpCost(squad) > self.Credits) continue;
                if (view.LevelUp(squad).Ok) upgrades.Add($"{squad.Type.Name} to level {squad.Level}");
            }

            // Techs: for the unit type with the most credits invested, the cheapest tech not owned yet.
            var invested = new Dictionary<UnitStats, int>();
            foreach (PlacedSquad squad in self.Squads)
            {
                invested.TryGetValue(squad.Type, out int value);
                invested[squad.Type] = value + squad.Type.Cost * squad.Level;
            }
            var types = new List<UnitStats>(invested.Keys);
            types.Sort((a, b) => invested[b].CompareTo(invested[a]));
            int reserve = view.Round < 3 ? 100 : 0;
            foreach (UnitStats type in types)
            {
                if (type.TechUpgrades == null || invested[type] < type.Cost * 2) continue; // Only worth it with 2+ squads' worth.
                TechUpgrade cheapest = null;
                foreach (TechUpgrade tech in type.TechUpgrades)
                {
                    if (tech == null || self.HasTech(type, tech) || tech.Cost + reserve > self.Credits) continue;
                    if (cheapest == null || tech.Cost < cheapest.Cost) cheapest = tech;
                }
                if (cheapest != null && view.BuyTech(type, cheapest).Ok)
                {
                    upgrades.Add($"{cheapest.Name} ({type.Name})");
                    break; // One tech per round keeps money for squads.
                }
            }
        }

        /// <summary>A carrier's drop is the reason to field it, so the tech that opens it comes before more squads, with a single squad of it (D60).</summary>
        private static void BuyCarrierTechs(AiView view, List<string> upgrades)
        {
            PlayerState self = view.Self;
            foreach (PlacedSquad squad in self.Squads)
            {
                UnitStats type = squad.Type;
                if (type.LockedReinforcement == null || type.TechUpgrades == null) continue;
                foreach (TechUpgrade tech in type.TechUpgrades)
                {
                    if (tech == null || !tech.UnlocksReinforcement || self.HasTech(type, tech) || tech.Cost > self.Credits || !view.BuyTech(type, tech).Ok) continue;
                    upgrades.Add($"{tech.Name} ({type.Name})");
                }
            }
        }

        private const int HardEliteCredits = 1000, HardExtraSlotCredits = 400, HardOilCredits = 450;
        private const int NormalTowerCredits = 300, HardTowerCredits = 200;

        /// <summary>
        /// Credits left after the squads go to the towers. Easy doesn't care about them. Normal and Hard level the tower that falls the
        /// most, then research health and damage; Hard needs less to spare, and buys oil against an army it has seen. Rapid Resupply is left to the player.
        /// </summary>
        private void SpendOnTowers(AiView view, IReadOnlyList<SeenSquad> seen, List<string> upgrades)
        {
            PlayerState self = view.Self;
            if (Difficulty == AiDifficulty.Easy || self.Towers.Count == 0) return;
            int spare = Difficulty == AiDifficulty.Hard ? HardTowerCredits : NormalTowerCredits;

            PlacedTower weakest = null;
            foreach (PlacedTower tower in self.Towers)
            {
                if (tower.Falls > 0 && (weakest == null || tower.Falls - tower.Level > weakest.Falls - weakest.Level)) weakest = tower;
            }
            if (weakest != null && self.Credits >= spare && view.LevelUpTower(weakest.Kind).Ok) upgrades.Add($"{Match.TowerName(weakest.Kind)} to level {weakest.Level}");

            MatchRules rules = view.Rules;
            bool health = self.ResearchHealthSteps <= self.ResearchDamageSteps;
            int steps = health ? self.ResearchHealthSteps : self.ResearchDamageSteps;
            if (steps < rules.ResearchCosts.Length && self.Credits >= rules.ResearchCosts[steps] + spare && view.BuyResearch(health).Ok)
                upgrades.Add(health ? "health research" : "damage research");

            if (Difficulty == AiDifficulty.Hard && seen.Count >= 3 && self.Credits >= HardOilCredits && view.BuyOilZone().Ok) upgrades.Add("oil");
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
            float exclusive = Difficulty != AiDifficulty.Easy && IsExclusive(unit) ? ExclusiveScoreBonus : 0f;
            score += exclusive;
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
                                    + exclusive + _random.Range(-0.1f, 0.1f);
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
                if (Difficulty != AiDifficulty.Easy) score += AgainstTheFlood(unit, seen, totalSeen);
            }
            else
            {
                score += _random.Range(-0.3f, 0.3f);
            }
            return score;
        }

        /// <summary>
        /// Decision D38: the more of the enemy's army is Flood, the more Normal and Hard avoid units that
        /// only feed it (Unarmored bodies Infection Forms can infect) and want chaff clear and armor. Easy doesn't care.
        /// </summary>
        private static float AgainstTheFlood(UnitStats unit, IReadOnlyList<SeenSquad> seen, float totalSeen)
        {
            float flood = 0f;
            foreach (SeenSquad squad in seen) if (squad.Type.IsFlood) flood += squad.Value;
            if (flood <= 0f || unit.IsFlood) return 0f;
            float share = flood / totalSeen;
            if (unit.Role == UnitRole.ChaffClear) return 2f * share;
            bool feedsIt = unit.ArmorType == ArmorType.Unarmored && !unit.ImmuneToInfection;
            return feedsIt ? -4f * share : 1f * share;
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
                int price = view.SquadPrice(unit) + (isUnlocked ? 0 : unit.UnlockCost);
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

        /// <summary>
        /// The two lanes that towers make (D55). Easy pays them no attention and fills the middle. Normal defends the side whose tower has
        /// fallen more. Hard pushes every other squad up the side where the army it last saw is thinner, and defends with the rest.
        /// </summary>
        private bool LaneColumn(AiView view, int sameKind, out int column)
        {
            column = 0;
            PlayerState self = view.Self;
            if (Difficulty == AiDifficulty.Easy || self.Towers.Count < 2) return false;
            if (Difficulty == AiDifficulty.Hard && sameKind % 2 == 0 && WeakLaneColumn(view, out column)) return true;

            PlacedTower left = self.FindTower(TowerKind.CommandCenter), right = self.FindTower(TowerKind.ResearchCenter);
            if (left.Falls == right.Falls) return Difficulty == AiDifficulty.Hard && WeakLaneColumn(view, out column);
            PlacedTower defended = left.Falls > right.Falls ? left : right;
            column = defended.Column + defended.Squares / 2;
            return true;
        }

        /// <summary>The middle column of this side's lane that faces the thinner half of the army it last saw, if one half is thinner.</summary>
        private static bool WeakLaneColumn(AiView view, out int column)
        {
            column = 0;
            float atLeft = 0f, atRight = 0f;
            foreach (SeenSquad squad in view.SeenOpponentArmy)
            {
                if (view.WorldCentre(squad).X < 0f) atLeft += squad.Value;
                else atRight += squad.Value;
            }
            if (atLeft == atRight) return false;
            // Battlefield left is this side's own left only for Team A: Team B's grid is the mirror image.
            bool ownLeft = (atLeft < atRight) == (view.Team == Team.A);
            int width = view.CombatRules.ArenaWidthSquares;
            column = ownLeft ? width / 4 : width - width / 4;
            return true;
        }

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

            if (type.Role != UnitRole.Artillery && LaneColumn(view, sameKind, out int lane))
            {
                int spread = (sameKind + 1) / 2 * (sameKind % 2 == 1 ? 1 : -1);
                targetColumn = lane - width / 2 + spread * (width + 2);
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
