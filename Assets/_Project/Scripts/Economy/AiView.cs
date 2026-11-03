// AiView.cs
// Everything an AI player is allowed to know and do (Bible Section 11: "The AI must never cheat on
// information or income"). An AI never gets the Match itself, only this view of it:
//   - its own state (credits, roster, unlocks, squads, base) and the match rules and round;
//   - public information: both bases, the opponent's faction once it has been seen in a battle;
//   - the opponent's army only as it was revealed at the start of the last battle (SeenOpponentArmy),
//     never the opponent's current deployment;
//   - its own actions, which go through the same Match rules as the player's (Unlock, Buy, Move,
//     Sell, LevelUp, BuyTech, and what the towers sell).
// The same view can drive the player's side (tests and autoplay let an AI play Team A).
// Depends on: Match, PlayerState, DeploymentGrid, Halobellum.Core.

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    /// <summary>One squad of the opponent as seen when the last battle started.</summary>
    public sealed class SeenSquad
    {
        public UnitStats Type;
        public int Level = 1;
        public int Column;
        public int Row;
        public bool Rotated90;
        /// <summary>The owner's techs for this unit type at that moment (they show in the battle).</summary>
        public IReadOnlyList<TechUpgrade> Techs = System.Array.Empty<TechUpgrade>();
        /// <summary>The item it carried in that battle, or null.</summary>
        public ItemStats Item;

        /// <summary>What the squad is worth: its price plus half its price per level above 1 (Section 10's rule).</summary>
        public int Value => Type.Cost + Type.Cost * (Level - 1) / 2;
    }

    public sealed class AiView
    {
        private readonly Match _match;

        public AiView(Match match, Team team)
        {
            _match = match;
            Team = team;
        }

        public Team Team { get; }
        public int Round => _match.Round;
        public MatchRules Rules => _match.Rules;
        public CombatRules CombatRules => _match.CombatRules;
        public int MatchSeed => _match.Seed;

        /// <summary>The AI's own state. (The opponent's PlayerState is never exposed.)</summary>
        public PlayerState Self => _match.Get(Team);
        public int NextIncome => _match.NextIncomeFor(Team);
        /// <summary>How many squads this side may buy per round.</summary>
        public int SquadsPerRound => _match.SquadsPerRound(Team);
        public int OwnBaseHealth => Self.BaseHealth;
        public int OpponentBaseHealth => _match.Get(Team.Opponent()).BaseHealth;

        /// <summary>Base damage this side took in the last round (shown to both players on the result screen).</summary>
        public int LastBaseDamageTaken => _match.LastSummary == null ? 0 : Team == Team.A ? _match.LastSummary.BaseDamageByB : _match.LastSummary.BaseDamageByA;
        /// <summary>Base damage this side dealt in the last round.</summary>
        public int LastBaseDamageDealt => _match.LastSummary == null ? 0 : Team == Team.A ? _match.LastSummary.BaseDamageByA : _match.LastSummary.BaseDamageByB;

        /// <summary>The opponent's army as revealed at the start of the last battle (empty before the first battle).</summary>
        public IReadOnlyList<SeenSquad> SeenOpponentArmy => _match.SeenArmy(Team.Opponent());
        /// <summary>The opponent's faction name once seen in a battle, otherwise null.</summary>
        public string SeenOpponentFaction => _match.SeenFaction(Team.Opponent());

        public bool CanMove(PlacedSquad squad) => Self.Squads.Contains(squad) && _match.CanMove(squad);
        public ActionResult Unlock(UnitStats type) => _match.Unlock(Team, type);
        public ActionResult Buy(UnitStats type, int column, int row, bool rotated90) => _match.Buy(Team, type, column, row, rotated90);
        public ActionResult Move(PlacedSquad squad, int column, int row, bool rotated90) => _match.Move(Team, squad.Id, column, row, rotated90);
        public ActionResult Sell(PlacedSquad squad) => _match.Sell(Team, squad.Id);
        public int SellValue(PlacedSquad squad) => _match.SellValue(squad);
        public bool CanLevelUp(PlacedSquad squad) => Self.Squads.Contains(squad) && _match.CanLevelUp(squad);
        public int LevelUpCost(PlacedSquad squad) => _match.LevelUpCost(squad);
        public ActionResult LevelUp(PlacedSquad squad) => _match.LevelUp(Team, squad.Id);
        public ActionResult BuyTech(UnitStats type, TechUpgrade tech) => _match.BuyTech(Team, type, tech);

        // Towers (D55).
        public int SquadPrice(UnitStats type) => _match.SquadPrice(Team, type);
        public ActionResult LevelUpTower(TowerKind kind) => _match.LevelUpTower(Team, kind);
        public ActionResult BuyExtraSlot() => _match.BuyExtraSlot(Team);
        public ActionResult BuyOilZone() => _match.BuyOilZone(Team);
        public ActionResult BuyEliteRecruitment() => _match.BuyEliteRecruitment(Team);
        public ActionResult BuyResearch(bool health) => _match.BuyResearch(Team, health);

        // Cards and spells (Milestone 7). Only this side's own offer and spells: the opponent's stay hidden.
        public CardOffer Offer => Self.Offer;
        public IReadOnlyList<OwnedSpell> Spells => Self.Spells;
        public ActionResult ChooseCard(int index) => _match.ChooseCard(Team, index);
        public ActionResult SkipCards() => _match.SkipCards(Team);
        public bool CanPlaceSpell(OwnedSpell spell, Vec2 position) => _match.CanPlaceSpell(Team, spell, position);
        public ActionResult PlaceSpell(OwnedSpell spell, Vec2 position, float angleDegrees = 0f) => _match.PlaceSpell(Team, spell.Id, position, angleDegrees);
        public IReadOnlyList<ItemStats> Inventory => Self.Inventory;
        public ActionResult PlaceItem(ItemStats item, PlacedSquad squad) => _match.PlaceItem(Team, item, squad.Id);
        public bool CanUseTraining => _match.CanUseTraining(Team);
        public ActionResult UseTraining(PlacedSquad squad) => _match.UseTraining(Team, squad.Id);
        /// <summary>Battlefield position (metres) of one of this side's squads.</summary>
        public Vec2 WorldCentre(PlacedSquad squad) => DeploymentGrid.WorldCentre(squad.Type, squad.Column, squad.Row, squad.Rotated90, Team, CombatRules);
        /// <summary>Battlefield position (metres) of an opponent squad as it was seen at the last battle start.</summary>
        public Vec2 WorldCentre(SeenSquad squad) => DeploymentGrid.WorldCentre(squad.Type, squad.Column, squad.Row, squad.Rotated90, Team.Opponent(), CombatRules);
    }
}
