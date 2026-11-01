// Match.cs
// One match against the AI (Bible Section 5): the starting draft, then rounds of
//   Deployment (no timer)  ->  Battle (90 s cap)  ->  Result
// until a base falls. Plain C#: the Match scene shows it and sends the player's actions here, and
// tests drive it directly. The AI uses exactly the same actions (Unlock, Buy, Move, Sell).
// Rules (values in MatchRules):
//   - Round 1 starts with StartingCredits (200); after each round, both players get the next
//     round's income (600, 800 ... capped at 1600). Banked credits have no limit.
//   - The free draft squads and every newly bought squad are placed at the free spot nearest the
//     middle of the zone (the player can then move them).
//   - Each round: at most NewSquadsPerRound squads bought (free draft squads don't count) and
//     UnlocksPerRound unlocks (Tier 1 unlocks are free but still count).
//   - Only squads bought this round, before their first battle, can be moved or turned.
//   - Selling refunds RefundBeforeBattle of the price before the squad's first battle (and frees
//     its slot for the round), RefundAfterBattle afterwards. Free draft squads always sell for
//     RefundAfterBattle of their cost.
//   - Every battle is built fresh from both deployments, so each round starts like the last one
//     started, plus whatever was bought (round reset).
//   - Result: every surviving body deals its BaseDamage to the enemy base, both at once. A base at
//     0 or below loses; if both fall, the lower one loses. An exact tie is a tied round: the damage
//     is undone, income is paid and the match goes on to the next round.
// Depends on: MatchRules, PlayerState, DeploymentGrid, StartingDraft, RoundSummary, IMatchAi,
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
        private readonly IMatchAi _enemyAi;
        private int _nextSquadId = 1;
        private int _battleCount;
        private int _baseHealthBeforeA, _baseHealthBeforeB;

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

        /// <summary>Income each player receives at the end of this round.</summary>
        public int NextIncome => Rules.IncomeForRound(Round + 1);

        // ------------------------------------------------------------------ draft

        /// <summary>The player picks a draft card; the AI then picks its own and round 1 begins.</summary>
        public ActionResult ChooseDraft(int cardIndex)
        {
            if (Phase != MatchPhase.Draft) return ActionResult.Fail("The draft is over.");
            if (cardIndex < 0 || cardIndex >= PlayerDraft.Count) return ActionResult.Fail("No such card.");

            ApplyDraftCard(Player, PlayerDraft[cardIndex]);
            int enemyChoice = EnemyDraft.Count > 0 ? Math.Clamp(_enemyAi.ChooseDraftCard(this, Enemy, EnemyDraft), 0, EnemyDraft.Count - 1) : -1;
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
            if (player.SquadsBoughtThisRound >= Rules.NewSquadsPerRound) return ActionResult.Fail($"You can buy {Rules.NewSquadsPerRound} squads per round.");
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

        /// <summary>True if the squad was bought this round and has not fought yet (only those can move or turn).</summary>
        public bool CanMove(PlacedSquad squad) => squad != null && squad.RoundBought == Round && !squad.HasFought;

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

        /// <summary>Credits a sale would give back right now.</summary>
        public int SellValue(PlacedSquad squad)
        {
            if (squad.FromDraft) return (int)MathF.Round(squad.Type.Cost * Rules.RefundAfterBattle);
            float share = CanMove(squad) ? Rules.RefundBeforeBattle : Rules.RefundAfterBattle;
            return (int)MathF.Round(squad.PricePaid * share);
        }

        public ActionResult Sell(Team team, int squadId)
        {
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be sold during deployment.");
            if (squad == null) return ActionResult.Fail("No such squad.");

            int refund = SellValue(squad);
            if (CanMove(squad) && !squad.FromDraft) player.SquadsBoughtThisRound = Math.Max(0, player.SquadsBoughtThisRound - 1);
            player.Credits += refund;
            player.Squads.Remove(squad);
            return ActionResult.Success($"{squad.Type.Name} sold for {refund}.");
        }

        // ------------------------------------------------------------------ battle and result

        /// <summary>The player pressed Ready: builds this round's battle from both deployments.</summary>
        public BattleSimulation StartBattle()
        {
            if (Phase != MatchPhase.Deployment) throw new InvalidOperationException("A battle can only start after deployment.");
            CurrentBattle = BuildBattle(includeEnemy: true);
            foreach (PlacedSquad squad in Player.Squads) squad.HasFought = true;
            foreach (PlacedSquad squad in Enemy.Squads) squad.HasFought = true;
            _battleCount++;
            Phase = MatchPhase.Battle;
            return CurrentBattle;
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
                summary.IncomeAwarded = NextIncome;
                Player.Credits += summary.IncomeAwarded;
                Enemy.Credits += summary.IncomeAwarded;
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
            }
            Phase = MatchPhase.Deployment;
            _enemyAi.Deploy(this, Enemy);
        }
    }
}
