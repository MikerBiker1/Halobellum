// MatchStateHash.cs
// A stable 64-bit hash of a whole match's state (Milestone 11): two LAN machines compare it before every battle and after every result,
// and stop the match if they differ. It covers the seed, phase, round, battles fought, the winner, the id counters, the match's random
// generator, the starting cards, both sides' complete state (every field an action or a battle can change, unit types and techs sorted by
// name, ids included), what each side has seen of the other, and the last round's summary. Built with StableHasher only: ints
// little-endian, floats as bits, strings as UTF-8. Undo histories and the battle statistics window are not part of it.
// Depends on: Match, PlayerState, RoundSummary, Cards, StableHasher, Halobellum.Core.

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public static class MatchStateHash
    {
        /// <summary>The hash of the whole match state.</summary>
        public static ulong Compute(Match match)
        {
            var h = new StableHasher().Text("Halobellum.MatchState.1");
            h.Int(match.Seed).Int((int)match.Seats).Int((int)match.Phase).Int(match.Round).Int(match.BattleCount).Int(match.BattleSeed);
            h.Int(match.Winner.HasValue ? (int)match.Winner.Value : -1);
            h.Int(match.SharedNextSquadId).Int(match.SharedNextSpellId).ULong(match.RandomState);
            h.Int(match.DraftPick(Team.A)).Int(match.DraftPick(Team.B));
            Draft(h, match.PlayerDraft);
            Draft(h, match.EnemyDraft);
            foreach (Team team in new[] { Team.A, Team.B })
            {
                Player(h, match.Get(team));
                // What the other side has seen of this one.
                h.Text(match.SeenFaction(team));
                IReadOnlyList<SeenSquad> seen = match.SeenArmy(team);
                h.Int(seen.Count);
                foreach (SeenSquad squad in seen)
                {
                    h.Text(squad.Type?.Name).Int(squad.Level).Int(squad.Column).Int(squad.Row).Bool(squad.Rotated90).Text(squad.Item?.Name);
                    h.Int(squad.Techs.Count);
                    foreach (TechUpgrade tech in squad.Techs) h.Text(tech?.Name);
                }
                Buffs(h, match.SeenBuffs(team));
                foreach (PlacedTower tower in match.Get(team).Towers) h.Int((int)tower.Kind).Int(match.SeenTowerLevel(team.Opponent(), tower.Kind));
            }
            h.Bool(match.LastSummary != null);
            if (match.LastSummary != null) h.ULong(Compute(match.LastSummary));
            return h.Value;
        }

        /// <summary>
        /// The hash of a round's result as it changes the match: round, base damage both ways, both bases, income both ways, tie, winner,
        /// the XP each squad earned (by id) and the squads ready to level. The battle's own statistics and kills are left out.
        /// </summary>
        public static ulong Compute(RoundSummary summary)
        {
            var h = new StableHasher().Text("Halobellum.RoundSummary.1");
            h.Int(summary.Round).Int(summary.BaseDamageByA).Int(summary.BaseDamageByB).Int(summary.BaseHealthA).Int(summary.BaseHealthB);
            h.Int(summary.IncomeAwarded).Int(summary.IncomeAwardedB).Bool(summary.Tie).Int(summary.MatchWinner.HasValue ? (int)summary.MatchWinner.Value : -1);
            var ids = new List<int>(summary.XpGained.Keys);
            ids.Sort();
            h.Int(ids.Count);
            foreach (int id in ids) h.Int(id).Float(summary.XpGained[id]);
            Names(h, summary.PlayerSquadsReadyToLevel);
            Names(h, summary.EnemySquadsReadyToLevel);
            return h.Value;
        }

        /// <summary>The hash as 16 hex digits.</summary>
        public static string Hex(ulong hash) => StableHasher.Hex(hash);

        private static void Draft(StableHasher h, List<DraftCard> cards)
        {
            h.Int(cards.Count);
            foreach (DraftCard card in cards)
            {
                h.Text(card.Faction?.Name).Text(card.SubFaction?.Name).Int(card.Squads?.Count ?? 0);
                if (card.Squads != null) foreach (UnitStats unit in card.Squads) h.Text(unit?.Name).Int(unit?.Cost ?? 0);
            }
        }

        private static void Player(StableHasher h, PlayerState p)
        {
            h.Int((int)p.Team).Text(p.Faction?.Name).Text(p.SubFaction?.Name);
            h.Int(p.Credits).Int(p.BaseHealth).Int(p.StartingBaseHealth).Int(p.SquadsBoughtThisRound).Int(p.UnlocksThisRound);
            h.Int(p.BonusIncome).Int(p.ExtraSquadsPerRound).Int(p.IncomePenalty).Int(p.ResearchHealthSteps).Int(p.ResearchDamageSteps);
            h.Bool(p.BoughtExtraSlot).Bool(p.UsedRapidResupply).Bool(p.EliteRecruitment).Bool(p.BoughtDeploymentModule);
            h.Int(p.NextSquadId).Int(p.NextSpellId);
            Buffs(h, p.Buffs);

            var unlocked = new List<UnitStats>(p.Unlocked);
            unlocked.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name) != 0 ? string.CompareOrdinal(a.Name, b.Name) : a.Cost.CompareTo(b.Cost));
            h.Int(unlocked.Count);
            foreach (UnitStats unit in unlocked) h.Text(unit.Name).Int(unit.Cost);

            h.Int(p.Squads.Count);
            foreach (PlacedSquad s in p.Squads)
            {
                h.Int(s.Id).Text(s.Type?.Name).Int(s.Type?.Cost ?? 0).Int(s.Column).Int(s.Row).Bool(s.Rotated90).Int(s.RoundBought).Int(s.PricePaid);
                h.Bool(s.FromDraft).Bool(s.HasFought).Bool(s.HasFlanked).Int(s.Level).Float(s.Xp).Int(s.LevelUpSpent).Bool(s.FromCard).Int(s.GrantedLevelWorth);
                h.Text(s.Item?.Name);
            }

            h.Int(p.Spells.Count);
            foreach (OwnedSpell spell in p.Spells)
            {
                h.Int(spell.Id).Text(spell.Stats?.Name).Int(spell.ReadyRound).Bool(spell.IsPlaced);
                h.Float(spell.Position.X).Float(spell.Position.Y).Float(spell.AngleDegrees);
            }

            CardOffer offer = p.Offer;
            h.Bool(offer != null);
            if (offer != null)
            {
                h.Int((int)offer.Kind).Int(offer.Round).Int(offer.SkipBonus).Int(offer.Cards.Count);
                foreach (CardStats card in offer.Cards) h.Text(card?.Name).Int(card?.Cost ?? 0);
                h.Int(offer.Reinforcements.Count);
                foreach (ReinforcementCard card in offer.Reinforcements) h.Text(card.Type?.Name).Int(card.Level).Int(card.Squads).Int(card.Budget);
            }

            h.Int(p.CardsTaken.Count);
            foreach (CardStats card in p.CardsTaken) h.Text(card?.Name);
            h.Int(p.Inventory.Count);
            foreach (ItemStats item in p.Inventory) h.Text(item?.Name);
            h.Int(p.Trainings.Count);
            foreach (OwnedTraining training in p.Trainings) h.Text(training.Card?.Name).Int(training.ReadyRound);
            h.Int(p.Towers.Count);
            foreach (PlacedTower tower in p.Towers) h.Int((int)tower.Kind).Int(tower.Column).Int(tower.Row).Int(tower.Squares).Int(tower.Level).Int(tower.Falls);

            var techTypes = new List<UnitStats>();
            foreach (KeyValuePair<UnitStats, List<TechUpgrade>> pair in p.Techs)
            {
                if (pair.Value.Count > 0) techTypes.Add(pair.Key);
            }
            techTypes.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name) != 0 ? string.CompareOrdinal(a.Name, b.Name) : a.Cost.CompareTo(b.Cost));
            h.Int(techTypes.Count);
            foreach (UnitStats type in techTypes)
            {
                List<TechUpgrade> techs = p.Techs[type];
                h.Text(type.Name).Int(techs.Count);
                foreach (TechUpgrade tech in techs) h.Text(tech?.Name);
            }
        }

        private static void Buffs(StableHasher h, ArmyBuffs buffs)
        {
            if (buffs == null)
            {
                h.Bool(false);
                return;
            }
            h.Bool(true).Float(buffs.DamageMultiplier).Float(buffs.HealthMultiplier).Float(buffs.RangeBonus);
        }

        private static void Names(StableHasher h, List<string> names)
        {
            h.Int(names.Count);
            foreach (string name in names) h.Text(name);
        }
    }
}
