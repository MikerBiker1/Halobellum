// Match.cs
// One match against the AI (Bible Section 5): the starting draft, then rounds of
//   Deployment (no timer)  ->  Battle (90 s cap)  ->  Result
// until a base falls. Plain C#: the Match scene shows it and sends the player's actions here, and
// tests drive it directly. The AI uses exactly the same actions (Unlock, Buy, Move, Sell).
// Rules (values in MatchRules):
//   - Round 1 starts with StartingCredits (200); after each round, both players get the next
//     round's income (400, 600, 800 ... capped at 1600; D29). Banked credits have no limit.
//   - The free draft squads and every newly bought squad are placed at the free spot nearest the
//     middle of the zone (the player can then move them).
//   - Each round: at most NewSquadsPerRound squads bought (free draft squads don't count) and
//     UnlocksPerRound unlocks (Tier 1 unlocks are free but still count).
//   - Only squads bought this round, before their first battle, can be moved or turned.
//   - Selling refunds RefundBeforeBattle of the price before the squad's first battle (and frees
//     its slot for the round), RefundAfterBattle afterwards. Free draft squads always sell for
//     RefundAfterBattle of their cost.
//   - Every battle is built fresh from both deployments, so each round starts like the last one
//     started, plus whatever was bought (round reset). Squads keep their level and XP.
//   - Leveling (Section 5): squads earn XP from kills (the victim's worth per body); when the bar
//     (XpBarPerSquadCost x the squad's price, the same at every level) is full, LevelUp costs half
//     the base price and raises the level (health and damage x level, up to 5).
//   - Tech upgrades (Section 9): BuyTech buys one of a unit type's techs for every squad of that type.
//   - Cards (Section 10, Milestone 7): from round 2, each round starts with a card offer (Cards phase
//     for the player; the AI decides at once): reinforcements every other round from round 4, general
//     cards otherwise (see CardDeck); take one (ChooseCard) or skip for a bonus (SkipCards).
//     Spells are kept and placed during deployment (PlaceSpell: offensive anywhere, defensive in your
//     own zone); they take effect when the battle starts, are hidden from the opponent until then,
//     and recharge CooldownRounds rounds after use.
//     Items go to the Inventory and are placed on a squad without one (PlaceItem); they stay on it
//     and return to the Inventory if the squad is sold. A Deployment Module lets its squad move in
//     every deployment phase. Economy cards add income on top of the income cap, buff cards improve
//     the whole army or add a deployment slot. Intensive Training fills a squad's XP bar (UseTraining).
//     Reinforcement squads are placed like squads bought this round, unlock their unit, start with
//     0 XP and don't count toward the squads per round or the unlock limit.
//   - Result: every surviving body deals its BaseDamage to the enemy base, both at once. A base at
//     0 or below loses; if both fall, the lower one loses. An exact tie is a tied round: the damage
//     is undone, income is paid and the match goes on to the next round.
// The AI only sees the match through an AiView: its own state, public information, and each army as
// it was revealed when the last battle started (SeenArmy), never the opponent's current deployment.
// The player gets the same: BuildPreview shows the enemy's last-battle army as ghosts (D25).
// Depends on: MatchRules, PlayerState, DeploymentGrid, StartingDraft, RoundSummary, IMatchAi, AiView,
//             Halobellum.Combat (HeadlessBattleRunner, BattleSimulation), Halobellum.Core.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public enum MatchPhase
    {
        Draft = 0,
        Deployment = 1,
        Battle = 2,
        Result = 3,
        MatchOver = 4,
        /// <summary>Start of a round from round 2: the player takes a card or skips (Milestone 7).</summary>
        Cards = 5,
    }

    /// <summary>Whether an action worked, and if not, why (shown to the player).</summary>
    public readonly struct ActionResult
    {
        public readonly bool Ok;
        public readonly string Message;

        private ActionResult(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }

        public static ActionResult Success(string message = "") => new ActionResult(true, message);
        public static ActionResult Fail(string message) => new ActionResult(false, message);
    }

    public sealed class Match
    {
        private readonly SimRandom _random;
        private IMatchAi _enemyAi;
        private readonly List<SeenSquad> _seenA = new List<SeenSquad>(), _seenB = new List<SeenSquad>();
        private string _seenFactionA, _seenFactionB;
        private int _nextSquadId = 1;
        private int _nextSpellId = 1;
        private int _battleCount;
        private int _baseHealthBeforeA, _baseHealthBeforeB;
        private ArmyBuffs _seenBuffsA = new ArmyBuffs(), _seenBuffsB = new ArmyBuffs();

        public Match(MatchRules rules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions, int seed, IMatchAi enemyAi)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            CombatRules = combatRules ?? throw new ArgumentNullException(nameof(combatRules));
            Seed = seed;
            _random = new SimRandom(seed);
            _enemyAi = enemyAi ?? new DoNothingAi();
            Player = new PlayerState(Team.A);
            Enemy = new PlayerState(Team.B);
            PlayerDraft = StartingDraft.Draw(factions, rules, _random);
            EnemyDraft = StartingDraft.Draw(factions, rules, _random);
            Phase = MatchPhase.Draft;
        }

        public MatchRules Rules { get; }
        public CombatRules CombatRules { get; }
        public int Seed { get; }
        public MatchPhase Phase { get; private set; }
        public int Round { get; private set; }
        /// <summary>The human player (Team A, bottom).</summary>
        public PlayerState Player { get; }
        /// <summary>The AI (Team B, top).</summary>
        public PlayerState Enemy { get; }
        public List<DraftCard> PlayerDraft { get; }
        public List<DraftCard> EnemyDraft { get; }
        /// <summary>The battle of the current round (set during Battle and Result).</summary>
        public BattleSimulation CurrentBattle { get; private set; }
        public RoundSummary LastSummary { get; private set; }
        public Team? Winner { get; private set; }

        public PlayerState Get(Team team) => team == Team.A ? Player : Enemy;

        /// <summary>The AI playing Team B.</summary>
        public IMatchAi EnemyAi => _enemyAi;

        /// <summary>Replaces the AI (e.g. after the player picks a difficulty). Only before the draft is chosen.</summary>
        public bool SetEnemyAi(IMatchAi ai)
        {
            if (Phase != MatchPhase.Draft || ai == null) return false;
            _enemyAi = ai;
            return true;
        }

        /// <summary>A team's army as it was when the last battle started (what the other side has seen).</summary>
        public IReadOnlyList<SeenSquad> SeenArmy(Team team) => team == Team.A ? _seenA : _seenB;

        /// <summary>A team's faction name once it has been seen in a battle, otherwise null.</summary>
        public string SeenFaction(Team team) => team == Team.A ? _seenFactionA : _seenFactionB;

        /// <summary>The income both players get at the end of this round, before economy cards.</summary>
        public int NextIncome => Rules.IncomeForRound(Round + 1);

        /// <summary>The credits this side receives at the end of this round: on top of the capped income, its economy cards (D33).</summary>
        public int NextIncomeFor(Team team) => NextIncome + Get(team).BonusIncome;

        /// <summary>How many squads this side may buy per round.</summary>
        public int SquadsPerRound(Team team) => Rules.NewSquadsPerRound + Get(team).ExtraSquadsPerRound;

        // ------------------------------------------------------------------ draft

        /// <summary>The player picks a draft card; the AI then picks its own and round 1 begins.</summary>
        public ActionResult ChooseDraft(int cardIndex)
        {
            if (Phase != MatchPhase.Draft) return ActionResult.Fail("The draft is over.");
            if (cardIndex < 0 || cardIndex >= PlayerDraft.Count) return ActionResult.Fail("No such card.");

            ApplyDraftCard(Player, PlayerDraft[cardIndex]);
            int enemyChoice = EnemyDraft.Count > 0 ? Math.Clamp(_enemyAi.ChooseDraftCard(new AiView(this, Team.B), EnemyDraft), 0, EnemyDraft.Count - 1) : -1;
            if (enemyChoice >= 0) ApplyDraftCard(Enemy, EnemyDraft[enemyChoice]);

            BeginRound(1);
            return ActionResult.Success();
        }

        private void ApplyDraftCard(PlayerState player, DraftCard card)
        {
            player.Faction = card.Faction;
            player.SubFaction = card.SubFaction;
            player.BaseHealth = Rules.BaseHealth;
            foreach (UnitStats type in card.Squads)
            {
                player.Unlocked.Add(type);
                if (!DeploymentGrid.FindFreeSpot(player, type, false, CombatRules, out int column, out int row)) continue;
                player.Squads.Add(new PlacedSquad
                {
                    Id = _nextSquadId++, Type = type, Column = column, Row = row,
                    RoundBought = 1, PricePaid = 0, FromDraft = true,
                });
            }
        }

        // ------------------------------------------------------------------ deployment actions

        public ActionResult Unlock(Team team, UnitStats type)
        {
            PlayerState player = Get(team);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Units can only be unlocked during deployment.");
            if (!player.Roster.Contains(type)) return ActionResult.Fail($"{type.Name} is not in your faction's roster.");
            if (player.Unlocked.Contains(type)) return ActionResult.Fail($"{type.Name} is already unlocked.");
            if (player.UnlocksThisRound >= Rules.UnlocksPerRound) return ActionResult.Fail("You already unlocked a unit this round.");
            if (player.Credits < type.UnlockCost) return ActionResult.Fail($"Not enough credits ({type.UnlockCost} needed).");

            player.Credits -= type.UnlockCost;
            player.Unlocked.Add(type);
            player.UnlocksThisRound++;
            return ActionResult.Success($"{type.Name} unlocked.");
        }

        /// <summary>Buys a squad and places it at the free spot nearest the middle of the zone.</summary>
        public ActionResult Buy(Team team, UnitStats type)
        {
            if (!DeploymentGrid.FindFreeSpot(Get(team), type, false, CombatRules, out int column, out int row))
                return ActionResult.Fail("Your deployment zone is full.");
            return Buy(team, type, column, row, false);
        }

        public ActionResult Buy(Team team, UnitStats type, int column, int row, bool rotated90)
        {
            PlayerState player = Get(team);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be bought during deployment.");
            if (!player.Unlocked.Contains(type)) return ActionResult.Fail($"Unlock {type.Name} first.");
            if (player.SquadsBoughtThisRound >= SquadsPerRound(team)) return ActionResult.Fail($"You can buy {SquadsPerRound(team)} squads per round.");
            if (player.Credits < type.Cost) return ActionResult.Fail($"Not enough credits ({type.Cost} needed).");
            if (!DeploymentGrid.CanPlace(player, type, column, row, rotated90, CombatRules)) return ActionResult.Fail("That spot is blocked or outside your zone.");

            player.Credits -= type.Cost;
            player.SquadsBoughtThisRound++;
            player.Squads.Add(new PlacedSquad
            {
                Id = _nextSquadId++, Type = type, Column = column, Row = row, Rotated90 = rotated90,
                RoundBought = Round, PricePaid = type.Cost,
            });
            return ActionResult.Success($"{type.Name} bought.");
        }

        /// <summary>True if the squad can move or turn: bought this round and not fought yet, or carrying a Deployment Module.</summary>
        public bool CanMove(PlacedSquad squad) => squad != null && (IsNew(squad) || (squad.Item != null && squad.Item.FreeDeployment));

        /// <summary>True if the squad was bought this round and has not fought yet.</summary>
        private bool IsNew(PlacedSquad squad) => squad.RoundBought == Round && !squad.HasFought;

        public ActionResult Move(Team team, int squadId, int column, int row, bool rotated90)
        {
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be moved during deployment.");
            if (squad == null) return ActionResult.Fail("No such squad.");
            if (!CanMove(squad)) return ActionResult.Fail("Only squads bought this round can be moved or turned.");
            if (!DeploymentGrid.CanPlace(player, squad.Type, column, row, rotated90, CombatRules, squad.Id)) return ActionResult.Fail("That spot is blocked or outside your zone.");

            squad.Column = column;
            squad.Row = row;
            squad.Rotated90 = rotated90;
            return ActionResult.Success();
        }

        /// <summary>
        /// Credits a sale would give back right now (the squad's price and its level-ups). Reinforcement
        /// squads (D32): before their first battle, what was paid for them; afterwards half their worth.
        /// </summary>
        public int SellValue(PlacedSquad squad)
        {
            if (squad.FromDraft) return (int)MathF.Round((squad.Type.Cost + squad.LevelUpSpent) * Rules.RefundAfterBattle);
            if (squad.FromCard && !IsNew(squad))
                return (int)MathF.Round((squad.Type.Cost + squad.GrantedLevelWorth + squad.LevelUpSpent) * Rules.RefundAfterBattle);
            float share = IsNew(squad) ? Rules.RefundBeforeBattle : Rules.RefundAfterBattle;
            return (int)MathF.Round((squad.PricePaid + squad.LevelUpSpent) * share);
        }

        // ------------------------------------------------------------------ leveling and techs

        /// <summary>XP needed to fill the squad's bar (the same at every level).</summary>
        public float XpNeeded(PlacedSquad squad) => squad.Type.Cost * Rules.XpBarPerSquadCost;

        public int LevelUpCost(PlacedSquad squad) => (int)MathF.Round(squad.Type.Cost * Rules.LevelUpCostShare);

        /// <summary>True if the squad's XP bar is full and it is below the maximum level.</summary>
        public bool CanLevelUp(PlacedSquad squad) =>
            squad != null && squad.Level < UnitUpgrades.MaxLevel && squad.Xp >= XpNeeded(squad) - 1e-3f;

        public ActionResult LevelUp(Team team, int squadId)
        {
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be leveled up during deployment.");
            if (squad == null) return ActionResult.Fail("No such squad.");
            if (squad.Level >= UnitUpgrades.MaxLevel) return ActionResult.Fail($"{squad.Type.Name} is already at the maximum level.");
            if (!CanLevelUp(squad)) return ActionResult.Fail("Its XP bar is not full yet.");
            int cost = LevelUpCost(squad);
            if (player.Credits < cost) return ActionResult.Fail($"Not enough credits ({cost} needed).");

            player.Credits -= cost;
            squad.Level++;
            squad.Xp = 0f;
            squad.LevelUpSpent += cost;
            return ActionResult.Success($"{squad.Type.Name} is now level {squad.Level}.");
        }

        /// <summary>Buys one of a unit type's tech upgrades; it applies to every squad of that type for the rest of the match.</summary>
        public ActionResult BuyTech(Team team, UnitStats type, TechUpgrade tech)
        {
            PlayerState player = Get(team);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Techs can only be bought during deployment.");
            if (type == null || tech == null || type.TechUpgrades == null || Array.IndexOf(type.TechUpgrades, tech) < 0)
                return ActionResult.Fail("That unit has no such tech.");
            if (!player.Unlocked.Contains(type)) return ActionResult.Fail($"Unlock {type.Name} first.");
            if (player.HasTech(type, tech)) return ActionResult.Fail($"{tech.Name} is already researched.");
            if (player.Credits < tech.Cost) return ActionResult.Fail($"Not enough credits ({tech.Cost} needed).");

            player.Credits -= tech.Cost;
            player.AddTech(type, tech);
            return ActionResult.Success($"{tech.Name} researched for every {type.Name}.");
        }

        /// <summary>
        /// Gives each placed squad the XP its bodies earned in the finished battle: for every enemy body
        /// killed, the victim's worth (its squad price per body, plus half per level above 1).
        /// Simulation squad i is placed squad i (Team A's squads first, then Team B's); units dropped
        /// mid-battle belong to no placed squad and earn nothing.
        /// </summary>
        private void AwardXp(BattleSimulation battle, RoundSummary summary)
        {
            var placed = new List<(PlacedSquad squad, PlayerState owner)>();
            foreach (PlacedSquad squad in Player.Squads) placed.Add((squad, Player));
            foreach (PlacedSquad squad in Enemy.Squads) placed.Add((squad, Enemy));
            var bySimSquad = new Dictionary<int, int>();
            for (int i = 0; i < battle.Squads.Count && i < placed.Count; i++) bySimSquad[battle.Squads[i].Id] = i;

            var earned = new float[placed.Count];
            foreach (SimUnit victim in battle.Units)
            {
                if (victim.IsAlive || victim.KilledById < 0) continue;
                SimUnit killer = battle.Units[victim.KilledById];
                if (!bySimSquad.TryGetValue(killer.SquadId, out int index)) continue;
                UnitStats v = victim.Stats;
                earned[index] += v.Cost / (float)Math.Max(1, v.SquadSize) * (1f + 0.5f * (v.Level - 1));
            }
            for (int i = 0; i < placed.Count; i++)
            {
                PlacedSquad squad = placed[i].squad;
                if (earned[i] <= 0f) continue;
                summary.XpGained[squad.Id] = earned[i];
                if (squad.Level < UnitUpgrades.MaxLevel) squad.Xp = Math.Min(XpNeeded(squad), squad.Xp + earned[i]);
            }
            foreach (PlacedSquad squad in Player.Squads)
            {
                if (CanLevelUp(squad)) summary.PlayerSquadsReadyToLevel.Add(squad.Type.Name);
            }
        }

        public ActionResult Sell(Team team, int squadId)
        {
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be sold during deployment.");
            if (squad == null) return ActionResult.Fail("No such squad.");

            int refund = SellValue(squad);
            if (IsNew(squad) && !squad.FromDraft && !squad.FromCard) player.SquadsBoughtThisRound = Math.Max(0, player.SquadsBoughtThisRound - 1);
            player.Credits += refund;
            if (squad.Item != null) player.Inventory.Add(squad.Item); // The item is not lost with the squad.
            player.Squads.Remove(squad);
            return ActionResult.Success($"{squad.Type.Name} sold for {refund}.");
        }

        // ------------------------------------------------------------------ battle and result

        /// <summary>The player pressed Ready: builds this round's battle from both deployments.</summary>
        public BattleSimulation StartBattle()
        {
            if (Phase != MatchPhase.Deployment) throw new InvalidOperationException("A battle can only start after deployment.");
            CurrentBattle = BuildBattle(includeEnemy: true);
            CastPlacedSpells(Player, CurrentBattle);
            CastPlacedSpells(Enemy, CurrentBattle);
            Reveal(Player, _seenA);
            Reveal(Enemy, _seenB);
            _seenBuffsA = Player.Buffs.Clone();
            _seenBuffsB = Enemy.Buffs.Clone();
            _seenFactionA = Player.Faction?.Name;
            _seenFactionB = Enemy.Faction?.Name;
            foreach (PlacedSquad squad in Player.Squads) squad.HasFought = true;
            foreach (PlacedSquad squad in Enemy.Squads) squad.HasFought = true;
            _battleCount++;
            Phase = MatchPhase.Battle;
            return CurrentBattle;
        }

        /// <summary>Every placed spell goes into the battle and starts recharging (usable again CooldownRounds + 1 rounds later).</summary>
        private void CastPlacedSpells(PlayerState player, BattleSimulation battle)
        {
            foreach (OwnedSpell spell in player.Spells)
            {
                if (!spell.IsPlaced) continue;
                battle.AddSpell(spell.Stats, player.Team, spell.Position, spell.Rotated90);
                spell.IsPlaced = false;
                spell.ReadyRound = Round + spell.Stats.CooldownRounds + 1;
            }
        }

        /// <summary>Copies an army as both sides see it when the battle starts.</summary>
        private static void Reveal(PlayerState player, List<SeenSquad> seen)
        {
            seen.Clear();
            foreach (PlacedSquad squad in player.Squads)
            {
                seen.Add(new SeenSquad
                {
                    Type = squad.Type, Level = squad.Level, Column = squad.Column, Row = squad.Row, Rotated90 = squad.Rotated90,
                    Techs = new List<TechUpgrade>(player.TechsFor(squad.Type)), Item = squad.Item,
                });
            }
        }

        /// <summary>
        /// A battle built from the current deployments, not yet started. The Match scene also uses this
        /// (without the enemy, which stays hidden) to show the player's squads during deployment.
        /// Squads are added in deployment order, Team A first, so simulation squad i is placed squad i.
        /// </summary>
        public BattleSimulation BuildBattle(bool includeEnemy)
        {
            return DeploymentGrid.CreateBattle(CombatRules, Player, includeEnemy ? Enemy : null, BattleSeed);
        }

        /// <summary>
        /// The deployment preview (decision D25): the player's current deployment, then the enemy's army
        /// as the player saw it when the last battle started (never its current deployment). Simulation
        /// squad i is the player's squad i, then SeenArmy(Team.B) in order.
        /// </summary>
        public BattleSimulation BuildPreview()
        {
            PlayerState seen = SeenAsDeployment(Team.B);
            return DeploymentGrid.CreateBattle(CombatRules, Player, seen.Squads.Count > 0 ? seen : null, BattleSeed);
        }

        /// <summary>A side's army as seen at the last battle start, as a deployment (levels and techs included).</summary>
        public PlayerState SeenAsDeployment(Team team)
        {
            var state = new PlayerState(team);
            state.SetBuffs(team == Team.A ? _seenBuffsA : _seenBuffsB);
            int id = 0;
            foreach (SeenSquad squad in SeenArmy(team))
            {
                state.Squads.Add(new PlacedSquad
                {
                    Id = ++id, Type = squad.Type, Column = squad.Column, Row = squad.Row, Rotated90 = squad.Rotated90,
                    Level = squad.Level, HasFought = true, Item = squad.Item,
                });
                foreach (TechUpgrade tech in squad.Techs) state.AddTech(squad.Type, tech);
            }
            return state;
        }

        /// <summary>The seed of this round's battle (changes every battle, repeatable for the same match seed).</summary>
        public int BattleSeed => unchecked(Seed * 7919 + _battleCount * 104729 + Round);

        /// <summary>Applies the finished battle: base damage, win check, income. Returns the round summary.</summary>
        public RoundSummary FinishBattle()
        {
            if (Phase != MatchPhase.Battle || CurrentBattle == null || !CurrentBattle.IsFinished)
                throw new InvalidOperationException("Finish the battle first.");

            BattleResult result = CurrentBattle.Result;
            var summary = new RoundSummary
            {
                Round = Round,
                Battle = result,
                BaseDamageByA = (int)MathF.Round(result.TeamA.SurvivorBaseDamage),
                BaseDamageByB = (int)MathF.Round(result.TeamB.SurvivorBaseDamage),
                Kills = RoundSummary.CountKills(CurrentBattle),
            };
            AwardXp(CurrentBattle, summary);

            _baseHealthBeforeA = Player.BaseHealth;
            _baseHealthBeforeB = Enemy.BaseHealth;
            Enemy.BaseHealth -= summary.BaseDamageByA;
            Player.BaseHealth -= summary.BaseDamageByB;

            bool playerFell = Player.BaseHealth <= 0;
            bool enemyFell = Enemy.BaseHealth <= 0;
            if (playerFell && enemyFell)
            {
                if (Player.BaseHealth < Enemy.BaseHealth) Winner = Team.B;
                else if (Enemy.BaseHealth < Player.BaseHealth) Winner = Team.A;
                else
                {
                    // Exact tie: a tied round. The damage is undone and the match goes on.
                    summary.Tie = true;
                    Player.BaseHealth = _baseHealthBeforeA;
                    Enemy.BaseHealth = _baseHealthBeforeB;
                }
            }
            else if (playerFell) Winner = Team.B;
            else if (enemyFell) Winner = Team.A;

            summary.BaseHealthA = Player.BaseHealth;
            summary.BaseHealthB = Enemy.BaseHealth;
            summary.MatchWinner = Winner;

            if (Winner.HasValue)
            {
                Phase = MatchPhase.MatchOver;
            }
            else
            {
                summary.IncomeAwarded = NextIncomeFor(Team.A);
                Player.Credits += summary.IncomeAwarded;
                Enemy.Credits += NextIncomeFor(Team.B);
                Phase = MatchPhase.Result;
            }
            LastSummary = summary;
            return summary;
        }

        /// <summary>Leaves the result screen for the next round's deployment.</summary>
        public void ContinueAfterResult()
        {
            if (Phase != MatchPhase.Result) return;
            CurrentBattle = null;
            BeginRound(Round + 1);
        }

        private void BeginRound(int round)
        {
            Round = round;
            if (round == 1)
            {
                Player.Credits = Rules.StartingCredits;
                Enemy.Credits = Rules.StartingCredits;
            }
            foreach (PlayerState player in new[] { Player, Enemy })
            {
                player.SquadsBoughtThisRound = 0;
                player.UnlocksThisRound = 0;
                player.Offer = CardDeck.HasOffer(Rules, round) ? CardDeck.Draw(player, Rules, round, _random) : null;
            }
            Phase = MatchPhase.Deployment;

            // The AI decides on its card at once, then deploys; the player sees the card screen first.
            var enemyView = new AiView(this, Team.B);
            if (Enemy.Offer != null)
            {
                int choice = _enemyAi.ChooseCard(enemyView, Enemy.Offer);
                if (choice < 0 || !ChooseCard(Team.B, choice).Ok) SkipCards(Team.B);
            }
            _enemyAi.Deploy(enemyView);
            if (Player.Offer != null) Phase = MatchPhase.Cards;
        }

        // ------------------------------------------------------------------ cards and spells (Milestone 7)

        /// <summary>Takes card `index` of this team's offer: pays its price, then adds the spell or places the reinforcement squads.</summary>
        public ActionResult ChooseCard(Team team, int index)
        {
            PlayerState player = Get(team);
            CardOffer offer = player.Offer;
            if (offer == null) return ActionResult.Fail("There is no card offer right now.");
            if (index < 0 || index >= offer.Count) return ActionResult.Fail("No such card.");
            int price = offer.PriceOf(index);
            if (player.Credits < price) return ActionResult.Fail($"Not enough credits ({price} needed).");

            player.Credits -= price;
            string message = offer.Kind == CardKind.General
                ? ApplyCard(player, offer.Cards[index])
                : PlaceReinforcements(player, offer.Reinforcements[index], price);
            ResolveOffer(team);
            return ActionResult.Success(message);
        }

        /// <summary>What a general card does when it is taken.</summary>
        private string ApplyCard(PlayerState player, CardStats card)
        {
            if (card.Type == CardType.Spell)
            {
                player.Spells.Add(new OwnedSpell { Id = _nextSpellId++, Stats = card.Spell, ReadyRound = Round });
                return $"{card.Name} added to your spells.";
            }
            player.CardsTaken.Add(card);
            switch (card.Type)
            {
                case CardType.Item:
                    player.Inventory.Add(card.Item);
                    return $"{card.Name} added to your inventory.";
                case CardType.Training:
                    player.Trainings.Add(new OwnedTraining { Card = card, ReadyRound = Round });
                    return $"{card.Name} ready: select a squad and use it.";
            }
            player.BonusIncome += card.BonusIncome;
            player.ExtraSquadsPerRound += card.ExtraSquadsPerRound;
            ArmyBuffs buffs = player.Buffs.Clone();
            buffs.DamageMultiplier *= card.AllDamageMultiplier;
            buffs.HealthMultiplier *= card.AllHealthMultiplier;
            buffs.RangeBonus += card.AllRangeBonus;
            player.SetBuffs(buffs);
            return $"{card.Name}: {card.EffectsText()}.";
        }

        /// <summary>Puts an item from the inventory on a squad that has none; it stays there and affects the whole squad.</summary>
        public ActionResult PlaceItem(Team team, ItemStats item, int squadId)
        {
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Items are placed during deployment.");
            if (item == null || !player.Inventory.Contains(item)) return ActionResult.Fail("That item is not in your inventory.");
            if (squad == null) return ActionResult.Fail("No such squad.");
            if (squad.Item != null) return ActionResult.Fail($"{squad.Type.Name} already carries {squad.Item.Name}.");
            player.Inventory.Remove(item);
            squad.Item = item;
            return ActionResult.Success($"{item.Name} placed on {squad.Type.Name}.");
        }

        /// <summary>True if this side has a copy of Intensive Training that has recharged.</summary>
        public bool CanUseTraining(Team team) => Phase == MatchPhase.Deployment && Get(team).Trainings.Exists(t => t.IsReady(Round));

        /// <summary>Intensive Training: fills the squad's XP bar (it can then be leveled up); recharges like a spell.</summary>
        public ActionResult UseTraining(Team team, int squadId)
        {
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (player.Trainings.Count == 0) return ActionResult.Fail("You don't have Intensive Training.");
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Training happens during deployment.");
            OwnedTraining training = player.Trainings.Find(t => t.IsReady(Round));
            if (training == null) return ActionResult.Fail("Intensive Training is recharging.");
            if (squad == null) return ActionResult.Fail("Select a squad first.");
            if (squad.Level >= UnitUpgrades.MaxLevel) return ActionResult.Fail($"{squad.Type.Name} is already at the maximum level.");
            if (CanLevelUp(squad)) return ActionResult.Fail($"{squad.Type.Name}'s XP bar is already full.");
            squad.Xp = XpNeeded(squad);
            training.ReadyRound = Round + training.Card.CooldownRounds + 1;
            return ActionResult.Success($"{squad.Type.Name}'s XP bar is full: it can level up.");
        }

        /// <summary>Skips this team's offer for its bonus (50 for spells, 100 for reinforcements).</summary>
        public ActionResult SkipCards(Team team)
        {
            PlayerState player = Get(team);
            if (player.Offer == null) return ActionResult.Fail("There is no card offer right now.");
            int bonus = player.Offer.SkipBonus;
            player.Credits += bonus;
            ResolveOffer(team);
            return ActionResult.Success($"Skipped: +{bonus} credits.");
        }

        private void ResolveOffer(Team team)
        {
            Get(team).Offer = null;
            if (team == Team.A && Phase == MatchPhase.Cards) Phase = MatchPhase.Deployment;
        }

        /// <summary>Unlocks the card's unit and places its squads like squads bought this round (0 XP, outside the squad limit).</summary>
        private string PlaceReinforcements(PlayerState player, ReinforcementCard card, int price)
        {
            player.Unlocked.Add(card.Type); // Ignores the one-unlock-per-round limit.
            int placed = 0;
            for (int i = 0; i < card.Squads; i++)
            {
                if (!DeploymentGrid.FindFreeSpot(player, card.Type, false, CombatRules, out int column, out int row)) break;
                player.Squads.Add(new PlacedSquad
                {
                    Id = _nextSquadId++, Type = card.Type, Column = column, Row = row, RoundBought = Round,
                    PricePaid = price / card.Squads + (i == 0 ? price % card.Squads : 0),
                    FromCard = true, Level = card.Level, GrantedLevelWorth = card.SquadWorth - card.Type.Cost,
                });
                placed++;
            }
            string what = $"{placed} x {card.Type.Name} (level {card.Level})";
            return placed < card.Squads ? $"{what} placed; your zone had no room for {card.Squads - placed} more." : $"{what} joined your army.";
        }

        /// <summary>True if this team may place this spell there now: owned, recharged, and on the right part of the field.</summary>
        public bool CanPlaceSpell(Team team, OwnedSpell spell, Vec2 position)
        {
            if (spell == null || !Get(team).Spells.Contains(spell) || !spell.IsReady(Round)) return false;
            return spell.Stats.Offensive ? DeploymentGrid.InArena(position, CombatRules) : DeploymentGrid.InZone(position, team, CombatRules);
        }

        /// <summary>Places (or moves) a spell for this round's battle. Offensive spells anywhere, defensive ones in your own zone.</summary>
        public ActionResult PlaceSpell(Team team, int spellId, Vec2 position, bool rotated90 = false)
        {
            OwnedSpell spell = Get(team).FindSpell(spellId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Spells are placed during deployment.");
            if (spell == null) return ActionResult.Fail("No such spell.");
            if (!spell.IsReady(Round)) return ActionResult.Fail($"{spell.Stats.Name} is recharging ({spell.RoundsLeft(Round)} more round(s)).");
            if (!CanPlaceSpell(team, spell, position))
                return ActionResult.Fail(spell.Stats.Offensive ? "Place it on the battlefield." : $"{spell.Stats.Name} can only be placed on your own side.");
            spell.IsPlaced = true;
            spell.Position = position;
            spell.Rotated90 = rotated90;
            return ActionResult.Success($"{spell.Stats.Name} placed.");
        }

        /// <summary>Takes a placed spell back (it stays ready).</summary>
        public ActionResult UnplaceSpell(Team team, int spellId)
        {
            OwnedSpell spell = Get(team).FindSpell(spellId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Spells are placed during deployment.");
            if (spell == null || !spell.IsPlaced) return ActionResult.Fail("That spell is not placed.");
            spell.IsPlaced = false;
            return ActionResult.Success($"{spell.Stats.Name} taken back.");
        }
    }
}
