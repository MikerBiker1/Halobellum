// PlayerState.cs
// Everything one player owns during a match: faction and sub-faction from the draft, credits, base
// health, unlocked unit types, and the squads placed on the deployment grid. The deployment is the
// part that persists between rounds: every battle is built fresh from it, so units destroyed in a
// battle are back at full health in their places next round (Bible Section 5, Round reset).
// Depends on: Halobellum.Core (UnitStats, Team), FactionInfo.

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    /// <summary>One squad on the deployment grid.</summary>
    public sealed class PlacedSquad
    {
        public int Id;
        public UnitStats Type;
        /// <summary>Left-most column of its footprint, in the owner's frame (0 = the owner's left edge of the field).</summary>
        public int Column;
        /// <summary>Front-most row of its footprint, counted back from the front edge of the owner's zone (0 = front).</summary>
        public int Row;
        /// <summary>Turned 90 degrees (width and depth swapped).</summary>
        public bool Rotated90;
        public int RoundBought;
        /// <summary>Credits paid (0 for the free starting squads from the draft).</summary>
        public int PricePaid;
        /// <summary>True for the free starting squads from the draft (they do not count toward the squads per round).</summary>
        public bool FromDraft;
        /// <summary>True once the squad has been in a battle: it can no longer be moved or turned.</summary>
        public bool HasFought;
        /// <summary>True once it has deployed in a flank zone: only that first time does it arrive late (D61).</summary>
        public bool HasFlanked;
        /// <summary>Squad level, 1 to 5 (Bible Section 5, Squad leveling). It survives the round reset.</summary>
        public int Level = 1;
        /// <summary>Experience earned from kills toward the next level. It survives the round reset.</summary>
        public float Xp;
        /// <summary>Credits spent leveling this squad up (refunded with the squad's price when sold).</summary>
        public int LevelUpSpent;
        /// <summary>True for squads from a reinforcement card (Milestone 7): they don't count toward the squads per round.</summary>
        public bool FromCard;
        /// <summary>For reinforcement squads: the worth of the levels it came with (half its price per level above 1).</summary>
        public int GrantedLevelWorth;
        /// <summary>The item this squad carries (decision D33), or null. It affects the whole squad.</summary>
        public ItemStats Item;
    }

    /// <summary>One of a player's two towers (D55). Every battle builds it afresh; its level stays.</summary>
    public sealed class PlacedTower
    {
        public TowerKind Kind;
        /// <summary>In the owner's frame, as for a squad.</summary>
        public int Column;
        public int Row;
        public int Squares;
        public int Level = 1;
        /// <summary>How many battles it has fallen in, which both sides saw.</summary>
        public int Falls;
    }

    public sealed class PlayerState
    {
        public readonly Team Team;
        public FactionInfo Faction;
        public SubFactionInfo SubFaction;
        public int Credits;
        public int BaseHealth;
        /// <summary>The base's health when the match began: more with a cheap starting card, less with a dear one (D59).</summary>
        public int StartingBaseHealth;
        public readonly HashSet<UnitStats> Unlocked = new HashSet<UnitStats>();
        public readonly List<PlacedSquad> Squads = new List<PlacedSquad>();
        public int SquadsBoughtThisRound;
        public int UnlocksThisRound;

        /// <summary>Spells owned (Milestone 7), each with its own cooldown and placement for the round.</summary>
        public readonly List<OwnedSpell> Spells = new List<OwnedSpell>();
        /// <summary>This round's card offer while it waits for a decision, otherwise null.</summary>
        public CardOffer Offer;

        /// <summary>Every non-spell card taken this match (economy, buffs, items, training), for the copies limit.</summary>
        public readonly List<CardStats> CardsTaken = new List<CardStats>();
        /// <summary>Items waiting to be placed on a squad.</summary>
        public readonly List<ItemStats> Inventory = new List<ItemStats>();
        /// <summary>Extra credits every round (Economy cards).</summary>
        public int BonusIncome;
        /// <summary>More squads can be deployed per round (Additional Deployment Slot).</summary>
        public int ExtraSquadsPerRound;
        /// <summary>Army-wide bonuses from buff cards.</summary>
        public ArmyBuffs Buffs { get; private set; } = new ArmyBuffs();
        /// <summary>Copies of Intensive Training taken; each has its own cooldown.</summary>
        public readonly List<OwnedTraining> Trainings = new List<OwnedTraining>();

        /// <summary>This player's towers, in the order of TowerKind (none in a match without towers, or in the Sandbox).</summary>
        public readonly List<PlacedTower> Towers = new List<PlacedTower>();
        /// <summary>This round's Command Center purchases: each once a round (D55).</summary>
        public bool BoughtExtraSlot, BoughtOilZone, UsedRapidResupply, EliteRecruitment;
        /// <summary>Credits Rapid Resupply takes from the next round's income.</summary>
        public int IncomePenalty;
        /// <summary>Research Center steps bought.</summary>
        public int ResearchHealthSteps, ResearchDamageSteps;

        public PlacedTower FindTower(TowerKind kind) => Towers.Find(t => t.Kind == kind);

        /// <summary>How many times the player took this card.</summary>
        public int CopiesOwned(CardStats card)
        {
            int count = 0;
            if (card.Spell != null)
            {
                foreach (OwnedSpell owned in Spells) if (owned.Stats == card.Spell) count++;
                return count;
            }
            foreach (CardStats taken in CardsTaken) if (taken == card) count++;
            return count;
        }

        /// <summary>Replaces the army-wide bonuses (buff cards; stats are worked out again).</summary>
        public void SetBuffs(ArmyBuffs buffs)
        {
            Buffs = buffs;
            _effective.Clear();
        }

        public OwnedSpell FindSpell(int spellId) => Spells.Find(s => s.Id == spellId);

        /// <summary>Tech upgrades bought, per unit type (they apply to every squad of that type).</summary>
        public readonly Dictionary<UnitStats, List<TechUpgrade>> Techs = new Dictionary<UnitStats, List<TechUpgrade>>();
        private readonly Dictionary<(UnitStats, int, int, ItemStats), UnitStats> _effective = new Dictionary<(UnitStats, int, int, ItemStats), UnitStats>();

        public PlayerState(Team team)
        {
            Team = team;
        }

        /// <summary>The techs this player owns for a unit type (empty if none).</summary>
        public IReadOnlyList<TechUpgrade> TechsFor(UnitStats type) =>
            type != null && Techs.TryGetValue(type, out List<TechUpgrade> list) ? list : (IReadOnlyList<TechUpgrade>)System.Array.Empty<TechUpgrade>();

        public bool HasTech(UnitStats type, TechUpgrade tech) => Techs.TryGetValue(type, out List<TechUpgrade> list) && list.Contains(tech);

        public void AddTech(UnitStats type, TechUpgrade tech)
        {
            if (!Techs.TryGetValue(type, out List<TechUpgrade> list)) Techs[type] = list = new List<TechUpgrade>();
            if (!list.Contains(tech)) list.Add(tech);
        }

        /// <summary>Removes a tech (sandbox only; in a match techs are permanent).</summary>
        public void RemoveTech(UnitStats type, TechUpgrade tech)
        {
            if (Techs.TryGetValue(type, out List<TechUpgrade> list)) list.Remove(tech);
        }

        /// <summary>The squad's battle stats: its unit type at its level with this player's techs, its item and the army's buffs (cached).</summary>
        public UnitStats EffectiveStats(PlacedSquad squad)
        {
            // Key on which techs are owned (one bit per slot), and apply them in slot order.
            int mask = 0;
            var owned = new List<TechUpgrade>();
            TechUpgrade[] slots = squad.Type.TechUpgrades ?? System.Array.Empty<TechUpgrade>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || !HasTech(squad.Type, slots[i])) continue;
                mask |= 1 << i;
                owned.Add(slots[i]);
            }
            var key = (squad.Type, squad.Level, mask, squad.Item);
            if (!_effective.TryGetValue(key, out UnitStats stats))
            {
                stats = UnitUpgrades.Apply(squad.Type, squad.Level, owned, squad.Item, Buffs);
                _effective[key] = stats;
            }
            return stats;
        }

        public bool HasFaction => Faction != null;

        /// <summary>Unit types this player can ever unlock (empty until the draft is chosen).</summary>
        public List<UnitStats> Roster => Faction != null ? Faction.RosterFor(SubFaction) : new List<UnitStats>();

        public PlacedSquad FindSquad(int squadId) => Squads.Find(s => s.Id == squadId);

        public string Label => HasFaction ? $"{Faction.Name} ({SubFaction?.Name})" : $"Team {Team}";

        // ------------------------------------------------------------------ undo in deployment (the owner, 2026-11-06)

        /// <summary>
        /// Everything a deployment action can change, to put it back with Restore. Squads, spells and towers keep their own objects
        /// (the field values are copied back into them), so a selection or preview holding one stays valid.
        /// </summary>
        public sealed class Snapshot
        {
            internal int Credits, SquadsBoughtThisRound, UnlocksThisRound, BonusIncome, ExtraSquadsPerRound, IncomePenalty, ResearchHealthSteps, ResearchDamageSteps;
            internal bool BoughtExtraSlot, BoughtOilZone, UsedRapidResupply, EliteRecruitment;
            internal ArmyBuffs Buffs;
            internal CardOffer Offer;
            internal List<UnitStats> Unlocked;
            internal List<(PlacedSquad squad, PlacedSquad values)> Squads;
            internal List<(OwnedSpell spell, OwnedSpell values)> Spells;
            internal List<(OwnedTraining training, int readyRound)> Trainings;
            internal List<(PlacedTower tower, int level, int falls)> Towers;
            internal List<CardStats> CardsTaken;
            internal List<ItemStats> Inventory;
            internal List<(UnitStats type, List<TechUpgrade> techs)> Techs;
        }

        public Snapshot TakeSnapshot()
        {
            var s = new Snapshot
            {
                Credits = Credits, SquadsBoughtThisRound = SquadsBoughtThisRound, UnlocksThisRound = UnlocksThisRound, BonusIncome = BonusIncome,
                ExtraSquadsPerRound = ExtraSquadsPerRound, IncomePenalty = IncomePenalty, ResearchHealthSteps = ResearchHealthSteps, ResearchDamageSteps = ResearchDamageSteps,
                BoughtExtraSlot = BoughtExtraSlot, BoughtOilZone = BoughtOilZone, UsedRapidResupply = UsedRapidResupply, EliteRecruitment = EliteRecruitment,
                Buffs = Buffs, Offer = Offer, Unlocked = new List<UnitStats>(Unlocked),
                Squads = new List<(PlacedSquad, PlacedSquad)>(), Spells = new List<(OwnedSpell, OwnedSpell)>(),
                Trainings = new List<(OwnedTraining, int)>(), Towers = new List<(PlacedTower, int, int)>(),
                CardsTaken = new List<CardStats>(CardsTaken), Inventory = new List<ItemStats>(Inventory), Techs = new List<(UnitStats, List<TechUpgrade>)>(),
            };
            foreach (PlacedSquad squad in Squads) s.Squads.Add((squad, CopySquad(squad, new PlacedSquad())));
            foreach (OwnedSpell spell in Spells) s.Spells.Add((spell, CopySpell(spell, new OwnedSpell())));
            foreach (OwnedTraining training in Trainings) s.Trainings.Add((training, training.ReadyRound));
            foreach (PlacedTower tower in Towers) s.Towers.Add((tower, tower.Level, tower.Falls));
            foreach (KeyValuePair<UnitStats, List<TechUpgrade>> pair in Techs) s.Techs.Add((pair.Key, new List<TechUpgrade>(pair.Value)));
            return s;
        }

        public void Restore(Snapshot s)
        {
            Credits = s.Credits; SquadsBoughtThisRound = s.SquadsBoughtThisRound; UnlocksThisRound = s.UnlocksThisRound; BonusIncome = s.BonusIncome;
            ExtraSquadsPerRound = s.ExtraSquadsPerRound; IncomePenalty = s.IncomePenalty; ResearchHealthSteps = s.ResearchHealthSteps; ResearchDamageSteps = s.ResearchDamageSteps;
            BoughtExtraSlot = s.BoughtExtraSlot; BoughtOilZone = s.BoughtOilZone; UsedRapidResupply = s.UsedRapidResupply; EliteRecruitment = s.EliteRecruitment;
            Offer = s.Offer;
            SetBuffs(s.Buffs);
            Unlocked.Clear();
            Unlocked.UnionWith(s.Unlocked);
            Squads.Clear();
            foreach ((PlacedSquad squad, PlacedSquad values) in s.Squads) Squads.Add(CopySquad(values, squad));
            Spells.Clear();
            foreach ((OwnedSpell spell, OwnedSpell values) in s.Spells) Spells.Add(CopySpell(values, spell));
            Trainings.Clear();
            foreach ((OwnedTraining training, int readyRound) in s.Trainings)
            {
                training.ReadyRound = readyRound;
                Trainings.Add(training);
            }
            foreach ((PlacedTower tower, int level, int falls) in s.Towers)
            {
                tower.Level = level;
                tower.Falls = falls;
            }
            CardsTaken.Clear();
            CardsTaken.AddRange(s.CardsTaken);
            Inventory.Clear();
            Inventory.AddRange(s.Inventory);
            Techs.Clear();
            foreach ((UnitStats type, List<TechUpgrade> techs) in s.Techs) Techs[type] = new List<TechUpgrade>(techs);
            _effective.Clear();
        }

        private static PlacedSquad CopySquad(PlacedSquad from, PlacedSquad to)
        {
            to.Id = from.Id; to.Type = from.Type; to.Column = from.Column; to.Row = from.Row; to.Rotated90 = from.Rotated90;
            to.RoundBought = from.RoundBought; to.PricePaid = from.PricePaid; to.FromDraft = from.FromDraft; to.HasFought = from.HasFought;
            to.HasFlanked = from.HasFlanked; to.Level = from.Level; to.Xp = from.Xp; to.LevelUpSpent = from.LevelUpSpent; to.FromCard = from.FromCard;
            to.GrantedLevelWorth = from.GrantedLevelWorth; to.Item = from.Item;
            return to;
        }

        private static OwnedSpell CopySpell(OwnedSpell from, OwnedSpell to)
        {
            to.Id = from.Id; to.Stats = from.Stats; to.ReadyRound = from.ReadyRound; to.IsPlaced = from.IsPlaced;
            to.Position = from.Position; to.AngleDegrees = from.AngleDegrees; to.SingleUse = from.SingleUse;
            return to;
        }
    }
}
