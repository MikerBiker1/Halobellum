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
//   - Leveling (Section 5): squads earn XP from kills (the victim's worth per body), also those of the bodies they drop,
//     release or raise (D136), split: half to the killer, half to every squad that damaged the victim by the
//     damage each dealt (KillXpShare, xp_split), and the bar fills during the battle (XpSoFar); when the bar
//     (XpBarPerSquadCost x the squad's price, the same at every level) is full, LevelUp costs half
//     the base price and raises the level (health and damage x level, up to 5). LevelUpAll does that for
//     every ready squad of a unit type at once, all or nothing.
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
//   - Result: every surviving body that was not summoned in the battle (brood, death bursts, risen and
//     seeded bodies, spell squads) deals its BaseDamage to the enemy base, both at once. A base at
//     0 or below loses; if both fall, the lower one loses. An exact tie is a tied round: the damage
//     is undone, income is paid and the match goes on to the next round.
//   - Towers (D55): each side has a Command Center on its left and a Research Center on its right, in the
//     middle of its zone. Both are built afresh for every battle; a fallen tower only costs its side the
//     shockwave. LevelUpTower doubles its health and shortens its shockwave. The Command Center sells, each
//     once a round: an extra squad slot (BuyExtraSlot), credits now against less income next round
//     (RapidResupply) and squads bought at level 2 for the rest of the round (BuyEliteRecruitment);
//     and, once a match, an oil spell kept in the player's spells and recharging after each use
//     (BuyOilZone). The Research Center sells lasting health and damage for the army (BuyResearch)
//     and Deployment Modules for the Inventory (BuyDeploymentModule, D175).
// The AI only sees the match through an AiView: its own state, public information, and each army as
// it was revealed when the last battle started (SeenArmy), never the opponent's current deployment.
// The player gets the same: BuildPreview shows the enemy's last-battle army as ghosts (D25).
// Two humans (Milestone 11, a LAN game): a Match built with MatchSeats.TwoHumans has no AI. Each side picks its own starting card
// (ChooseDraft(team, index); round 1 begins when both have), takes or skips its own card offer while the other side may already deploy
// (InCards), has its own undo history and its own squad and spell id counters (kept in PlayerState, so Undo puts them back). Every action
// that works is recorded as a MatchCommand (TakeCommands), which MatchCommands.Apply does again on the other machine. None of this changes
// a match against the AI.
// Depends on: MatchRules, PlayerState, DeploymentGrid, StartingDraft, TowerBuilder, RoundSummary, IMatchAi, AiView, MatchCommand, RoundOutcome,
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

    /// <summary>Who plays the two sides of a match.</summary>
    public enum MatchSeats
    {
        /// <summary>The player is Team A, an AI (IMatchAi) plays Team B: every match before Milestone 11.</summary>
        HumanVsAi = 0,
        /// <summary>Two people, one on each side (a LAN game, Milestone 11): no AI, and each side decides for itself.</summary>
        TwoHumans = 1,
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
        // Undo in deployment (the owner, 2026-11-06): each side's state before each action this round, and as it is now ([0] Team A, [1] Team B).
        // Against the AI only Team A's is used.
        private readonly Stack<PlayerState.Snapshot>[] _undo = { new Stack<PlayerState.Snapshot>(), new Stack<PlayerState.Snapshot>() };
        private readonly PlayerState.Snapshot[] _undoNow = new PlayerState.Snapshot[2];
        private readonly int[] _undoRound = { -1, -1 };
        private int _battleCount;
        private int _baseHealthBeforeA, _baseHealthBeforeB;
        private ArmyBuffs _seenBuffsA = new ArmyBuffs(), _seenBuffsB = new ArmyBuffs();
        private readonly Dictionary<(TowerKind, int), UnitStats> _towerStats = new Dictionary<(TowerKind, int), UnitStats>();
        // Tower levels as the other side saw them when the last battle started: Team B's towers (as Team A saw them), and Team A's.
        private readonly Dictionary<TowerKind, int> _seenEnemyTowerLevels = new Dictionary<TowerKind, int>();
        private readonly Dictionary<TowerKind, int> _seenPlayerTowerLevels = new Dictionary<TowerKind, int>();
        // Two humans (M11): each side's starting card (-1 until picked), its commands not yet taken, and its command counter.
        private readonly MatchSeats _seats;
        private readonly int[] _draftPick = { -1, -1 };
        private readonly List<MatchCommand>[] _commands = { new List<MatchCommand>(), new List<MatchCommand>() };
        private readonly int[] _commandSeq = new int[2];

        /// <summary>In a two-humans match, Team B's first squad and spell id: far above Team A's, so the two never meet (Team A counts from 1).</summary>
        public const int TeamBFirstId = 1000001;

        /// <summary>
        /// A match between two people (seats = TwoHumans, Milestone 11): no AI plays either side. Both machines build it from the same
        /// rules, factions and seed, and stay the same by doing the same MatchCommands. MatchSeats.HumanVsAi gives a match against the
        /// do-nothing AI, as the other constructor does with no AI.
        /// </summary>
        public Match(MatchRules rules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions, int seed, MatchSeats seats)
            : this(rules, combatRules, factions, seed, (IMatchAi)null)
        {
            _seats = seats;
            if (seats != MatchSeats.TwoHumans) return;
            Player.NextSquadId = Player.NextSpellId = 1;
            Enemy.NextSquadId = Enemy.NextSpellId = TeamBFirstId;
        }

        public Match(MatchRules rules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions, int seed, IMatchAi enemyAi)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            CombatRules = combatRules ?? throw new ArgumentNullException(nameof(combatRules));
            Seed = seed;
            _random = new SimRandom(seed);
            _enemyAi = enemyAi ?? new DoNothingAi();
            Player = new PlayerState(Team.A);
            Enemy = new PlayerState(Team.B);
            TowerBuilder.AddTo(Player, rules, combatRules);
            TowerBuilder.AddTo(Enemy, rules, combatRules);
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

        /// <summary>Every battle of this match so far, in order, for the Battle stats window (D69). The one being fought is last, and not Finished yet.</summary>
        public IReadOnlyList<BattleStats> BattleHistory => _battleHistory;
        private readonly List<BattleStats> _battleHistory = new List<BattleStats>();
        public Team? Winner { get; private set; }

        public PlayerState Get(Team team) => team == Team.A ? Player : Enemy;

        /// <summary>Who plays the two sides.</summary>
        public MatchSeats Seats => _seats;

        /// <summary>True in a match between two people (a LAN game): no AI, each side decides for itself.</summary>
        public bool IsTwoHumans => _seats == MatchSeats.TwoHumans;

        /// <summary>The AI playing Team B.</summary>
        public IMatchAi EnemyAi => _enemyAi;

        /// <summary>Replaces the AI (e.g. after the player picks a difficulty). Only before the draft is chosen, and never in a two-humans match.</summary>
        public bool SetEnemyAi(IMatchAi ai)
        {
            if (Phase != MatchPhase.Draft || ai == null || IsTwoHumans) return false;
            _enemyAi = ai;
            return true;
        }

        /// <summary>True until the first battle starts: all a side has seen of the other's army is its starting squads.</summary>
        public bool NoBattleYet => _battleCount == 0;

        /// <summary>A team's army as it was when the last battle started, or its starting squads before the first (what the other side has seen).</summary>
        public IReadOnlyList<SeenSquad> SeenArmy(Team team) => team == Team.A ? _seenA : _seenB;

        /// <summary>A team's faction name once the draft is over, otherwise null.</summary>
        public string SeenFaction(Team team) => team == Team.A ? _seenFactionA : _seenFactionB;

        /// <summary>The income both players get at the end of this round, before economy cards.</summary>
        public int NextIncome => Rules.IncomeForRound(Round + 1);

        /// <summary>The credits this side receives at the end of this round: on top of the capped income, its economy cards (D33).</summary>
        public int NextIncomeFor(Team team) => Math.Max(0, NextIncome + Get(team).BonusIncome - Get(team).IncomePenalty);

        /// <summary>How many squads this side may buy per round.</summary>
        public int SquadsPerRound(Team team) => Rules.NewSquadsPerRound + Get(team).ExtraSquadsPerRound + (Get(team).BoughtExtraSlot ? 1 : 0);

        /// <summary>What a new squad of this type costs this side right now: more during Elite Recruitment, since it comes at a higher level.</summary>
        public int SquadPrice(Team team, UnitStats type) => type.Cost + (Get(team).EliteRecruitment ? LevelUpCost(type) : 0);

        // ------------------------------------------------------------------ draft

        /// <summary>
        /// The player picks a draft card; the AI then picks its own and round 1 begins. In a two-humans match this fails: it would not know
        /// whose card it is (the guest plays Team B), so the caller must say, with ChooseDraft(team, index).
        /// </summary>
        public ActionResult ChooseDraft(int cardIndex)
        {
            if (IsTwoHumans) return ActionResult.Fail(TwoHumansNeedsTeam);
            if (Phase != MatchPhase.Draft) return ActionResult.Fail("The draft is over.");
            if (cardIndex < 0 || cardIndex >= PlayerDraft.Count) return ActionResult.Fail("No such card.");

            // The AI chooses before the player's card is applied, so that it can't know that card.
            int enemyChoice = EnemyDraft.Count > 0 ? Math.Clamp(_enemyAi.ChooseDraftCard(new AiView(this, Team.B), EnemyDraft), 0, EnemyDraft.Count - 1) : -1;
            BeginAfterDraft(cardIndex, enemyChoice);
            return ActionResult.Success();
        }

        /// <summary>
        /// One side picks its starting card. In a two-humans match the pick waits for the other side's; once both are in, Team A's card
        /// is applied, then Team B's (as against the AI), both see each other's starting squads and round 1 begins. Against the AI,
        /// Team A's pick is ChooseDraft(index) and Team B's is the AI's.
        /// </summary>
        public ActionResult ChooseDraft(Team team, int cardIndex)
        {
            if (!IsTwoHumans) return team == Team.A ? ChooseDraft(cardIndex) : ActionResult.Fail("The AI picks Team B's starting card.");            if (Phase != MatchPhase.Draft) return ActionResult.Fail("The draft is over.");
            List<DraftCard> cards = team == Team.A ? PlayerDraft : EnemyDraft;
            if (cardIndex < 0 || cardIndex >= cards.Count) return ActionResult.Fail("No such card.");
            if (_draftPick[Side(team)] >= 0) return ActionResult.Fail("Your starting card is already chosen.");

            _draftPick[Side(team)] = cardIndex;
            Record(team, new MatchCommand { Action = MatchCommand.ChooseDraftAction, Index = cardIndex }, undoable: false);
            if (_draftPick[0] >= 0 && _draftPick[1] >= 0) BeginAfterDraft(_draftPick[0], _draftPick[1]);
            return ActionResult.Success();
        }

        /// <summary>True once this side has picked its starting card (in a two-humans match), or once the draft is over.</summary>
        public bool HasChosenDraft(Team team) => Phase != MatchPhase.Draft || _draftPick[Side(team)] >= 0;

        /// <summary>The starting card a side picked in a two-humans match, or -1.</summary>
        internal int DraftPick(Team team) => _draftPick[Side(team)];

        /// <summary>Applies both starting cards (Team A's first; -1 for none), reveals both armies and begins round 1.</summary>
        private void BeginAfterDraft(int playerChoice, int enemyChoice)
        {
            ApplyDraftCard(Player, PlayerDraft[playerChoice]);
            if (enemyChoice >= 0) ApplyDraftCard(Enemy, EnemyDraft[enemyChoice]);

            // Both sides see each other's starting squads, as they stand in the middle of the zone before anyone deploys (D58).
            Reveal(Player, _seenA);
            Reveal(Enemy, _seenB);
            _seenFactionA = Player.Faction?.Name;
            _seenFactionB = Enemy.Faction?.Name;

            BeginRound(1);
        }

        private static int Side(Team team) => team == Team.A ? 0 : 1;

        /// <summary>The next squad id for this side: its own counter in a two-humans match, otherwise the one counter both sides share.</summary>
        private int NewSquadId(PlayerState player) => IsTwoHumans ? player.NextSquadId++ : _nextSquadId++;

        /// <summary>The next spell id for this side, the same way.</summary>
        private int NewSpellId(PlayerState player) => IsTwoHumans ? player.NextSpellId++ : _nextSpellId++;

        /// <summary>The shared id counters (used against the AI), for the state hash.</summary>
        internal int SharedNextSquadId => _nextSquadId;
        internal int SharedNextSpellId => _nextSpellId;
        internal int BattleCount => _battleCount;
        internal ArmyBuffs SeenBuffs(Team team) => team == Team.A ? _seenBuffsA : _seenBuffsB;
        internal ulong RandomState => _random.State;

        /// <summary>
        /// In a two-humans match, true while this side's card offer waits for it (take or skip it first: until then its deployment
        /// actions are refused). The other side can deploy meanwhile. Against the AI it is the Cards phase for Team A.
        /// </summary>
        public bool InCards(Team team) => IsTwoHumans ? Phase == MatchPhase.Deployment && Get(team).Offer != null : team == Team.A && Phase == MatchPhase.Cards;

        /// <summary>In a two-humans match, refuses a deployment action while the side's card offer waits. Never refuses against the AI.</summary>
        private bool WaitsForCard(Team team, out ActionResult failed)
        {
            bool waits = IsTwoHumans && Get(team).Offer != null;
            failed = waits ? ActionResult.Fail("Take or skip your card first.") : default;
            return waits;
        }

        // ------------------------------------------------------------------ commands (two humans, M11)

        /// <summary>
        /// In a two-humans match: notes an action that worked as a command for the other machine (unless it is being applied from there),
        /// and, for a deployment action, makes the state before it a step Undo can go back to. The undo history therefore follows from the
        /// commands alone, the same on both machines; RecordAction does nothing in this mode.
        /// </summary>
        private void Record(Team team, MatchCommand command, bool undoable = true)
        {
            if (!IsTwoHumans) return;
            command.Team = team;
            command.Round = Round;
            if (!ApplyingRemote)
            {
                command.Seq = ++_commandSeq[Side(team)];
                _commands[Side(team)].Add(command);
            }
            if (undoable) PushUndo(team);
        }

        /// <summary>True while MatchCommands.Apply does another machine's command: it is not recorded again.</summary>
        internal bool ApplyingRemote;

        /// <summary>What the members that do not name a team say in a two-humans match, where they would not know whose side to act on.</summary>
        private const string TwoHumansNeedsTeam = "In a two-humans match, say which team acts (use the overload that takes a Team).";

        /// <summary>
        /// MatchCommands.Apply: checks that a command from the other machine is that side's next one (its Seq is one more than the last)
        /// and, if so, counts it. The other side's counter is not used for anything else on this machine, since its commands are never
        /// recorded here. `expected` is the Seq that was due.
        /// </summary>
        internal bool TakeRemoteSeq(Team team, int seq, out int expected)
        {
            int side = Side(team);
            expected = _commandSeq[side] + 1;
            if (seq != expected) return false;
            _commandSeq[side] = seq;
            return true;
        }

        /// <summary>
        /// The commands this side has done on this machine since the last call (two-humans match only; empty against the AI), in order,
        /// and forgets them. Send them to the other machine, which does them with MatchCommands.Apply. Commands applied from the other
        /// machine are not in it.
        /// </summary>
        public List<MatchCommand> TakeCommands(Team team)
        {
            var taken = new List<MatchCommand>(_commands[Side(team)]);
            _commands[Side(team)].Clear();
            return taken;
        }

        /// <summary>How many of this side's commands wait to be taken.</summary>
        public int PendingCommands(Team team) => _commands[Side(team)].Count;

        private void ApplyDraftCard(PlayerState player, DraftCard card)
        {
            player.Faction = card.Faction;
            player.SubFaction = card.SubFaction;
            player.BaseHealth = player.StartingBaseHealth = Rules.StartingBaseHealth(card.Value);
            foreach (UnitStats type in card.Squads)
            {
                player.Unlocked.Add(type);
                if (!DeploymentGrid.FindFreeSpot(player, type, false, CombatRules, out int column, out int row)) continue;
                player.Squads.Add(new PlacedSquad
                {
                    Id = NewSquadId(player), Type = type, Column = column, Row = row,
                    RoundBought = 1, PricePaid = 0, FromDraft = true,
                });
            }
        }

        // ------------------------------------------------------------------ deployment actions

        public ActionResult Unlock(Team team, UnitStats type)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            PlayerState player = Get(team);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Units can only be unlocked during deployment.");
            if (!player.Roster.Contains(type)) return ActionResult.Fail(Rules.Mode == MatchMode.UnlockSkirmish ? $"{type.Name} cannot be unlocked here." : $"{type.Name} is not in your faction's roster.");
            if (player.Unlocked.Contains(type)) return ActionResult.Fail($"{type.Name} is already unlocked.");
            if (player.UnlocksThisRound >= Rules.UnlocksPerRound) return ActionResult.Fail("You already unlocked a unit this round.");
            if (player.Credits < type.UnlockCost) return ActionResult.Fail($"Not enough credits ({type.UnlockCost} needed).");

            player.Credits -= type.UnlockCost;
            player.Unlocked.Add(type);
            player.UnlocksThisRound++;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.UnlockAction, Unit = type.Name });
            return ActionResult.Success($"{type.Name} unlocked.");
        }

        /// <summary>Buys a squad and places it at the free spot nearest the middle of the zone.</summary>
        public ActionResult Buy(Team team, UnitStats type)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            if (!DeploymentGrid.FindFreeSpot(Get(team), type, false, CombatRules, out int column, out int row))
                return ActionResult.Fail("Your deployment zone is full.");
            return Buy(team, type, column, row, false);
        }

        public ActionResult Buy(Team team, UnitStats type, int column, int row, bool rotated90)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            PlayerState player = Get(team);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be bought during deployment.");
            if (!player.Unlocked.Contains(type)) return ActionResult.Fail($"Unlock {type.Name} first.");
            if (player.SquadsBoughtThisRound >= SquadsPerRound(team)) return ActionResult.Fail($"You can buy {SquadsPerRound(team)} squads per round.");
            int price = SquadPrice(team, type);
            if (player.Credits < price) return ActionResult.Fail($"Not enough credits ({price} needed).");
            if (!DeploymentGrid.CanPlace(player, type, column, row, rotated90, CombatRules)) return ActionResult.Fail("That spot is blocked or outside your zone.");

            player.Credits -= price;
            player.SquadsBoughtThisRound++;
            player.Squads.Add(new PlacedSquad
            {
                Id = NewSquadId(player), Type = type, Column = column, Row = row, Rotated90 = rotated90,
                RoundBought = Round, PricePaid = price, Level = player.EliteRecruitment ? 2 : 1,
            });
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.BuyAction, Unit = type.Name, Column = column, Row = row, Turned = rotated90 });
            return ActionResult.Success($"{type.Name} bought.");
        }

        /// <summary>True if the squad can move or turn: bought this round and not fought yet, or carrying a Deployment Module.</summary>
        public bool CanMove(PlacedSquad squad) => squad != null && (IsNew(squad) || (squad.Item != null && squad.Item.FreeDeployment));

        /// <summary>True if the squad was bought this round and has not fought yet.</summary>
        private bool IsNew(PlacedSquad squad) => squad.RoundBought == Round && !squad.HasFought;

        public ActionResult Move(Team team, int squadId, int column, int row, bool rotated90)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be moved during deployment.");
            if (squad == null) return ActionResult.Fail("No such squad.");
            if (!CanMove(squad)) return ActionResult.Fail("Only squads bought this round can be moved or turned.");
            if (!DeploymentGrid.CanPlace(player, squad.Type, column, row, rotated90, CombatRules, squad.Id)) return ActionResult.Fail("That spot is blocked or outside your zone.");

            squad.Column = column;
            squad.Row = row;
            squad.Rotated90 = rotated90;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.MoveAction, SquadId = squadId, Column = column, Row = row, Turned = rotated90 });
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

        public int LevelUpCost(PlacedSquad squad) => LevelUpCost(squad.Type);

        private int LevelUpCost(UnitStats type) => (int)MathF.Round(type.Cost * Rules.LevelUpCostShare);

        /// <summary>True if the squad's XP bar is full and it is below the maximum level.</summary>
        public bool CanLevelUp(PlacedSquad squad) =>
            squad != null && squad.Level < UnitUpgrades.MaxLevel && squad.Xp >= XpNeeded(squad) - 1e-3f;

        public ActionResult LevelUp(Team team, int squadId)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
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
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.LevelUpAction, SquadId = squadId });
            return ActionResult.Success($"{squad.Type.Name} is now level {squad.Level}.");
        }

        /// <summary>How many of this side's squads of this type are ready to level up now, and what leveling them all costs.</summary>
        public int SquadsReadyToLevel(Team team, UnitStats type, out int totalPrice)
        {
            int count = 0;
            totalPrice = 0;
            List<PlacedSquad> squads = Get(team).Squads;
            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i].Type != type || !CanLevelUp(squads[i])) continue;
                count++;
                totalPrice += LevelUpCost(squads[i]);
            }
            return count;
        }

        /// <summary>Levels up every ready squad of this type, or none if the credits don't cover them all. Squads whose XP bar isn't full or at the maximum level are left out.</summary>
        public ActionResult LevelUpAll(Team team, UnitStats type)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            PlayerState player = Get(team);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be leveled up during deployment.");
            int count = SquadsReadyToLevel(team, type, out int totalPrice);
            if (count == 0) return ActionResult.Fail($"No {type?.Name} squad is ready to level up.");
            if (player.Credits < totalPrice) return ActionResult.Fail($"Not enough credits ({totalPrice} needed).");

            foreach (PlacedSquad squad in player.Squads)
            {
                if (squad.Type != type || !CanLevelUp(squad)) continue;
                int cost = LevelUpCost(squad);
                player.Credits -= cost;
                squad.Level++;
                squad.Xp = 0f;
                squad.LevelUpSpent += cost;
            }
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.LevelUpAllAction, Unit = type.Name });
            return ActionResult.Success($"{count} x {type.Name} leveled up for {totalPrice}.");
        }

        /// <summary>Buys one of a unit type's tech upgrades; it applies to every squad of that type for the rest of the match.</summary>
        public ActionResult BuyTech(Team team, UnitStats type, TechUpgrade tech)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            PlayerState player = Get(team);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Techs can only be bought during deployment.");
            if (type == null || tech == null || type.TechUpgrades == null || Array.IndexOf(type.TechUpgrades, tech) < 0)
                return ActionResult.Fail("That unit has no such tech.");
            if (!player.Unlocked.Contains(type)) return ActionResult.Fail($"Unlock {type.Name} first.");
            if (player.HasTech(type, tech)) return ActionResult.Fail($"{tech.Name} is already researched.");
            if (player.Credits < tech.Cost) return ActionResult.Fail($"Not enough credits ({tech.Cost} needed).");

            player.Credits -= tech.Cost;
            player.AddTech(type, tech);
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.BuyTechAction, Unit = type.Name, Tech = tech.Name });
            return ActionResult.Success($"{tech.Name} researched for every {type.Name}.");
        }

        /// <summary>The placed squads in the order the battle made them: simulation squad i is placed squad i (Team A's squads first, then Team B's).</summary>
        private List<PlacedSquad> PlacedInBattleOrder()
        {
            var placed = new List<PlacedSquad>(Player.Squads.Count + Enemy.Squads.Count);
            placed.AddRange(Player.Squads);
            placed.AddRange(Enemy.Squads);
            return placed;
        }

        /// <summary>
        /// The XP each placed squad has earned in this battle so far (all of it once the battle is over): for every enemy body
        /// killed, the victim's worth (its squad price per body, plus half per level above 1). A tower is worth what the rules
        /// say for its level (D59). A body dropped, released or risen in the battle earns for the squad it came from
        /// (SimUnit.CreditSquadId), so a Carrier Form earns what its Infection Forms kill and a Seeder what its seeded hosts do
        /// (the owner, D136); bodies a spell brought earn nothing. A unit that dies in a blast it deals itself (the Infected
        /// Pelican's dive) still earns for its squad, which comes back next round.
        /// The worth is split (the owner, xp_split): KillXpShare of it to the killer's squad, the rest among every squad that
        /// damaged the body, by the damage each dealt to it (SimUnit.AddSquadDamage; the killer's own included, spells left out).
        /// A kill that gives no XP (by a spell, a self-kill, by a body a spell brought) still gives none, so the total is unchanged.
        /// </summary>
        private float[] XpEarnedIn(BattleSimulation battle, List<PlacedSquad> placed)
        {
            var bySimSquad = new Dictionary<int, int>();
            for (int i = 0; i < battle.Squads.Count && i < placed.Count; i++) bySimSquad[battle.Squads[i].Id] = i;
            var earned = new float[placed.Count];
            bool asItLands = Rules.DamageXpAsItLands;
            foreach (SimUnit victim in battle.Units)
            {
                int index = -1;
                bool killed = !victim.IsAlive && victim.KilledById >= 0 && bySimSquad.TryGetValue(battle.Units[victim.KilledById].CreditSquadId, out index);
                if (!killed && (!asItLands || victim.SquadDamageCount == 0)) continue;
                UnitStats v = victim.Stats;
                float worth;
                if (v.IsTower)
                {
                    worth = Rules.TowerXpByLevel[Get(victim.Team).FindTower(v.Shockwave.Kind).Level - 1];
                }
                else
                {
                    worth = v.Cost / (float)Math.Max(1, v.SquadSize) * (1f + 0.5f * (v.Level - 1));
                    // A body made in the battle (its deeds count for another squad, or none) is worth BattleBodyXpShare of that (D138).
                    if (victim.SquadId < 0 || victim.SquadId != victim.CreditSquadId) worth *= Rules.BattleBodyXpShare;
                }
                _xpDamagers.Clear();
                _xpDamage.Clear();
                for (int i = 0; i < victim.SquadDamageCount; i++)
                {
                    if (!bySimSquad.TryGetValue(victim.SquadDamageSquadId(i), out int damager)) continue;
                    _xpDamagers.Add(damager);
                    _xpDamage.Add(victim.SquadDamageAmount(i));
                }
                if (!asItLands)
                {
                    SplitKillXp(worth, Rules.KillXpShare, index, _xpDamagers, _xpDamage, earned);
                    continue;
                }
                float pool = v.MaxHealth + v.MaxShield + v.Barrier;
                PayDamageXp(worth, Rules.KillXpShare, killed ? index : -1, pool, _xpDamagers, _xpDamage, earned);
            }
            return earned;
        }

        /// <summary>
        /// One body's XP when the damage half is paid as damage lands (D177): killShare of worth to the killer's squad (killerIndex; -1 for
        /// no kill), and the rest at rest / pool for every point each squad dealt (damagerIndex[i] dealt damage[i]), so damage past the pool,
        /// after healing, pays again. A killed body pays at least the whole rest among its damagers, as on a kill before (spell damage is
        /// not on the record); a kill with no damage on record gives it all to the killer.
        /// </summary>
        public static void PayDamageXp(float worth, float killShare, int killerIndex, float pool, IReadOnlyList<int> damagerIndex, IReadOnlyList<float> damage, float[] earned)
        {
            float total = 0f;
            for (int i = 0; i < damage.Count; i++) total += damage[i];
            float share = Math.Max(0f, Math.Min(1f, killShare));
            float rest = worth * (1f - share);
            if (killerIndex >= 0)
            {
                earned[killerIndex] += worth - rest;
                if (!(total > 0f))
                {
                    earned[killerIndex] += rest;
                    return;
                }
            }
            if (!(total > 0f) || !(pool > 0f)) return;
            float paid = rest * total / pool;
            if (killerIndex >= 0) paid = Math.Max(paid, rest);
            for (int i = 0; i < damage.Count; i++) earned[damagerIndex[i]] += paid * damage[i] / total;
        }

        // Scratch lists for XpEarnedIn (one victim at a time).
        private readonly List<int> _xpDamagers = new List<int>();
        private readonly List<float> _xpDamage = new List<float>();

        /// <summary>
        /// Adds one kill's XP to earned (the owner, xp_split): killShare of worth to the killer's placed squad, the rest split among the
        /// squads that damaged the body, by the damage each dealt (damagerIndex[i] dealt damage[i]; the killer may be among them).
        /// Spell damage is not on the record, so the squads share the whole rest. With no damage on record, all of it goes to the killer.
        /// </summary>
        public static void SplitKillXp(float worth, float killShare, int killerIndex, IReadOnlyList<int> damagerIndex, IReadOnlyList<float> damage, float[] earned)
        {
            float total = 0f;
            for (int i = 0; i < damage.Count; i++) total += damage[i];
            if (!(total > 0f))
            {
                earned[killerIndex] += worth;
                return;
            }
            float share = Math.Max(0f, Math.Min(1f, killShare));
            float rest = worth * (1f - share);
            earned[killerIndex] += worth - rest;
            for (int i = 0; i < damage.Count; i++) earned[damagerIndex[i]] += rest * damage[i] / total;
        }

        /// <summary>
        /// The squad's XP as its bar should show it now: during a battle, what it had plus what it has earned so far in it
        /// (capped at the bar), so that the bar fills as it kills (the owner, D136); otherwise its XP.
        /// </summary>
        public float XpSoFar(PlacedSquad squad)
        {
            if (squad == null || Phase != MatchPhase.Battle || CurrentBattle == null || squad.Level >= UnitUpgrades.MaxLevel) return squad?.Xp ?? 0f;
            List<PlacedSquad> placed = PlacedInBattleOrder();
            int index = placed.IndexOf(squad);
            if (index < 0) return squad.Xp;
            return Math.Min(XpNeeded(squad), squad.Xp + XpEarnedIn(CurrentBattle, placed)[index]);
        }

        /// <summary>Gives each placed squad the XP the outcome says it earned in the battle (OutcomeOfCurrentBattle: XpEarnedIn), found by its id.</summary>
        private void AwardXp(RoundOutcome outcome, RoundSummary summary)
        {
            var earnedById = new Dictionary<int, float>();
            foreach (SquadXp entry in outcome.Xp) earnedById[entry.SquadId] = entry.Xp;
            List<PlacedSquad> placed = PlacedInBattleOrder();
            for (int i = 0; i < placed.Count; i++)
            {
                PlacedSquad squad = placed[i];
                if (!earnedById.TryGetValue(squad.Id, out float earned) || earned <= 0f) continue;
                summary.XpGained[squad.Id] = earned;
                if (squad.Level < UnitUpgrades.MaxLevel) squad.Xp = Math.Min(XpNeeded(squad), squad.Xp + earned);
            }
            foreach (PlacedSquad squad in Player.Squads)
            {
                if (CanLevelUp(squad)) summary.PlayerSquadsReadyToLevel.Add(squad.Type.Name);
            }
            foreach (PlacedSquad squad in Enemy.Squads)
            {
                if (CanLevelUp(squad)) summary.EnemySquadsReadyToLevel.Add(squad.Type.Name);
            }
        }

        public ActionResult Sell(Team team, int squadId)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be sold during deployment.");
            if (squad == null) return ActionResult.Fail("No such squad.");

            int refund = SellValue(squad);
            if (IsNew(squad) && !squad.FromDraft && !squad.FromCard) player.SquadsBoughtThisRound = Math.Max(0, player.SquadsBoughtThisRound - 1);
            player.Credits += refund;
            if (squad.Item != null) player.Inventory.Add(squad.Item); // The item is not lost with the squad.
            player.Squads.Remove(squad);
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.SellAction, SquadId = squadId });
            return ActionResult.Success($"{squad.Type.Name} sold for {refund}.");
        }

        // ------------------------------------------------------------------ towers (D55)

        public static string TowerName(TowerKind kind) => kind == TowerKind.CommandCenter ? "Command Center" : "Research Center";

        /// <summary>A tower's stats at a level: its health doubles with each one, and its shockwave gets shorter.</summary>
        public UnitStats TowerStats(TowerKind kind, int level)
        {
            if (_towerStats.TryGetValue((kind, level), out UnitStats stats)) return stats;
            stats = TowerBuilder.MakeStats(Rules, kind, level);
            _towerStats[(kind, level)] = stats;
            return stats;
        }

        /// <summary>A tower's level as this team last saw the other side's (the player's own are always known).</summary>
        public int SeenEnemyTowerLevel(TowerKind kind) => _seenEnemyTowerLevels.TryGetValue(kind, out int level) ? level : 1;

        /// <summary>The level of the other side's tower as `viewer` saw it when the last battle started (1 before the first). Viewer A is SeenEnemyTowerLevel.</summary>
        public int SeenTowerLevel(Team viewer, TowerKind kind)
        {
            if (viewer == Team.A) return SeenEnemyTowerLevel(kind);
            return _seenPlayerTowerLevels.TryGetValue(kind, out int level) ? level : 1;
        }

        /// <summary>Adds a side's towers after its squads, so that squad and unit numbering stay as they were.</summary>
        private void PlaceTowers(BattleSimulation battle, PlayerState player, bool seenOnly)
        {
            foreach (PlacedTower tower in player.Towers)
            {
                int level = seenOnly ? SeenTowerLevel(player.Team.Opponent(), tower.Kind) : tower.Level;
                battle.AddUnit(TowerStats(tower.Kind, level), player.Team, DeploymentGrid.TowerCentre(tower, player.Team, CombatRules));
            }
        }

        private bool CanBuyFromTower(Team team, out ActionResult failed)
        {
            if (WaitsForCard(team, out failed)) return false;
            failed = ActionResult.Fail("Towers only sell during deployment.");
            return Phase == MatchPhase.Deployment && Rules.HasTowers;
        }

        public ActionResult LevelUpTower(Team team, TowerKind kind)
        {
            if (!CanBuyFromTower(team, out ActionResult failed)) return failed;
            PlayerState player = Get(team);
            PlacedTower tower = player.FindTower(kind);
            if (tower == null) return ActionResult.Fail("There is no such tower.");
            if (tower.Level >= Rules.TowerMaxLevel) return ActionResult.Fail($"The {TowerName(kind)} is already at the maximum level.");
            if (player.Credits < Rules.TowerLevelUpCost) return ActionResult.Fail($"Not enough credits ({Rules.TowerLevelUpCost} needed).");
            player.Credits -= Rules.TowerLevelUpCost;
            tower.Level++;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.LevelUpTowerAction, Tower = kind });
            return ActionResult.Success($"The {TowerName(kind)} is now level {tower.Level}.");
        }

        public ActionResult BuyExtraSlot(Team team)
        {
            if (!CanBuyFromTower(team, out ActionResult failed)) return failed;
            PlayerState player = Get(team);
            if (player.BoughtExtraSlot) return ActionResult.Fail("You already bought an extra slot this round.");
            if (player.Credits < Rules.ExtraSlotCost) return ActionResult.Fail($"Not enough credits ({Rules.ExtraSlotCost} needed).");
            player.Credits -= Rules.ExtraSlotCost;
            player.BoughtExtraSlot = true;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.BuyExtraSlotAction });
            return ActionResult.Success("One more squad can be bought this round.");
        }

        public ActionResult BuyOilZone(Team team)
        {
            if (!CanBuyFromTower(team, out ActionResult failed)) return failed;
            PlayerState player = Get(team);
            SpellStats oil = Rules.OilZone;
            if (oil == null) return ActionResult.Fail("There is no oil to buy.");
            if (player.OwnsSpell(oil)) return ActionResult.Fail($"{oil.Name} is already in your spells.");
            if (player.Credits < oil.Cost) return ActionResult.Fail($"Not enough credits ({oil.Cost} needed).");
            player.Credits -= oil.Cost;
            player.Spells.Add(new OwnedSpell { Id = NewSpellId(player), Stats = oil, ReadyRound = Round });
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.BuyOilZoneAction });
            return ActionResult.Success($"{oil.Name} added to your spells for the rest of the match.");
        }

        public ActionResult RapidResupply(Team team)
        {
            if (!CanBuyFromTower(team, out ActionResult failed)) return failed;
            PlayerState player = Get(team);
            if (player.UsedRapidResupply) return ActionResult.Fail("You already used Rapid Resupply this round.");
            player.UsedRapidResupply = true;
            player.Credits += Rules.RapidResupplyCredits;
            player.IncomePenalty += Rules.RapidResupplyPenalty;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.RapidResupplyAction });
            return ActionResult.Success($"+{Rules.RapidResupplyCredits} credits now, {Rules.RapidResupplyPenalty} less next round.");
        }

        public ActionResult BuyEliteRecruitment(Team team)
        {
            if (!CanBuyFromTower(team, out ActionResult failed)) return failed;
            PlayerState player = Get(team);
            if (player.EliteRecruitment) return ActionResult.Fail("Elite Recruitment is already on this round.");
            if (player.Credits < Rules.EliteRecruitmentCost) return ActionResult.Fail($"Not enough credits ({Rules.EliteRecruitmentCost} needed).");
            player.Credits -= Rules.EliteRecruitmentCost;
            player.EliteRecruitment = true;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.BuyEliteRecruitmentAction });
            return ActionResult.Success("Squads bought this round start at level 2, and cost half their price more.");
        }

        /// <summary>The next step of army-wide health or damage. The steps add up: two of 15% are 30%, not 32%.</summary>
        public ActionResult BuyResearch(Team team, bool health)
        {
            if (!CanBuyFromTower(team, out ActionResult failed)) return failed;
            PlayerState player = Get(team);
            int steps = health ? player.ResearchHealthSteps : player.ResearchDamageSteps;
            if (steps >= Rules.ResearchCosts.Length) return ActionResult.Fail("That research is complete.");
            int cost = Rules.ResearchCosts[steps];
            if (player.Credits < cost) return ActionResult.Fail($"Not enough credits ({cost} needed).");
            float share = health ? Rules.ResearchHealthShare : Rules.ResearchDamageShare;
            float gain = (1f + share * (steps + 1)) / (1f + share * steps);
            player.Credits -= cost;
            ArmyBuffs buffs = player.Buffs.Clone();
            if (health)
            {
                buffs.HealthMultiplier *= gain;
                player.ResearchHealthSteps++;
            }
            else
            {
                buffs.DamageMultiplier *= gain;
                player.ResearchDamageSteps++;
            }
            player.SetBuffs(buffs);
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.BuyResearchAction, Health = health });
            return ActionResult.Success($"Your whole army: +{share * (steps + 1) * 100f:0}% {(health ? "health" : "damage")} in all.");
        }

        /// <summary>The faction's Deployment Module item (the one its card gives), or null.</summary>
        public static ItemStats DeploymentModuleOf(PlayerState player)
        {
            CardStats card = player.Faction?.Cards?.Find(c => c != null && c.Type == CardType.Item && c.Item != null && c.Item.FreeDeployment);
            return card?.Item;
        }

        /// <summary>The Research Center sells Deployment Modules (the owner, D175): one goes to the Inventory, one a deployment phase (D176).</summary>
        public ActionResult BuyDeploymentModule(Team team)
        {
            if (!CanBuyFromTower(team, out ActionResult failed)) return failed;
            PlayerState player = Get(team);
            ItemStats module = DeploymentModuleOf(player);
            if (module == null) return ActionResult.Fail("There is no Deployment Module to buy.");
            if (player.BoughtDeploymentModule) return ActionResult.Fail("You already bought a Deployment Module this round.");
            if (player.Credits < Rules.DeploymentModuleCost) return ActionResult.Fail($"Not enough credits ({Rules.DeploymentModuleCost} needed).");
            player.Credits -= Rules.DeploymentModuleCost;
            player.BoughtDeploymentModule = true;
            player.Inventory.Add(module);
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.BuyDeploymentModuleAction });
            return ActionResult.Success($"{module.Name} added to your inventory.");
        }

        // ------------------------------------------------------------------ battle and result

        /// <summary>True if StartBattle may be called now: in deployment, and in a two-humans match with both card offers taken or skipped.</summary>
        public bool CanStartBattle => Phase == MatchPhase.Deployment && (!IsTwoHumans || (Player.Offer == null && Enemy.Offer == null));

        /// <summary>The player pressed Ready (in a two-humans match: both did, and both sides' commands are in): builds this round's battle from both deployments.</summary>
        public BattleSimulation StartBattle()
        {
            if (Phase != MatchPhase.Deployment) throw new InvalidOperationException("A battle can only start after deployment.");
            if (!CanStartBattle) throw new InvalidOperationException("Both sides must take or skip their card before the battle.");
            CurrentBattle = BuildBattle(includeEnemy: true);
            DelayFirstFlanks(CurrentBattle);
            CastPlacedSpells(Player, CurrentBattle);
            CastPlacedSpells(Enemy, CurrentBattle);
            Reveal(Player, _seenA);
            Reveal(Enemy, _seenB);
            _seenBuffsA = Player.Buffs.Clone();
            _seenBuffsB = Enemy.Buffs.Clone();
            foreach (PlacedTower tower in Enemy.Towers) _seenEnemyTowerLevels[tower.Kind] = tower.Level;
            foreach (PlacedTower tower in Player.Towers) _seenPlayerTowerLevels[tower.Kind] = tower.Level;
            _seenFactionA = Player.Faction?.Name;
            _seenFactionB = Enemy.Faction?.Name;
            foreach (PlacedSquad squad in Player.Squads) squad.HasFought = true;
            foreach (PlacedSquad squad in Enemy.Squads) squad.HasFought = true;
            _battleCount++;
            _battleHistory.Add(BattleStats.AtStart(CurrentBattle, Round, Player, Enemy, CombatRules));
            Phase = MatchPhase.Battle;
            return CurrentBattle;
        }

        /// <summary>A squad in a flank zone arrives late the first time, and on time after that (D61). Simulation squad i is placed squad i, Team A's first.</summary>
        private void DelayFirstFlanks(BattleSimulation battle)
        {
            int index = 0;
            foreach (PlayerState side in new[] { Player, Enemy })
            {
                foreach (PlacedSquad squad in side.Squads)
                {
                    SimSquad simSquad = battle.Squads[index++];
                    if (!DeploymentGrid.IsInFlankZone(squad, CombatRules)) continue;
                    if (!squad.HasFlanked) battle.DelayArrival(simSquad, CombatRules.FlankArrivalDelaySeconds);
                    squad.HasFlanked = true;
                }
            }
        }

        /// <summary>Every placed spell goes into the battle and starts recharging (usable again CooldownRounds + 1 rounds later).</summary>
        private void CastPlacedSpells(PlayerState player, BattleSimulation battle)
        {
            foreach (OwnedSpell spell in player.Spells)
            {
                if (!spell.IsPlaced) continue;
                // Squads that land fight in this player's army, with its bonuses.
                SpellStats stats = spell.Stats.DropUnit != null && !player.Buffs.IsNeutral
                    ? spell.Stats.WithDropUnit(UnitUpgrades.Apply(spell.Stats.DropUnit, 1, null, null, player.Buffs))
                    : spell.Stats;
                battle.AddSpell(stats, player.Team, spell.Position, spell.AngleDegrees);
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
            BattleSimulation battle = DeploymentGrid.CreateBattle(CombatRules, Player, includeEnemy ? Enemy : null, BattleSeed);
            PlaceTowers(battle, Player, false);
            if (includeEnemy) PlaceTowers(battle, Enemy, false);
            return battle;
        }

        /// <summary>
        /// The deployment preview (decision D25): the player's current deployment, then the enemy's army
        /// as the player saw it when the last battle started (never its current deployment). Simulation
        /// squad i is the player's squad i, then SeenArmy(Team.B) in order.
        /// </summary>
        public BattleSimulation BuildPreview()
        {
            PlayerState seen = SeenAsDeployment(Team.B);
            BattleSimulation battle = DeploymentGrid.CreateBattle(CombatRules, Player, seen.Squads.Count > 0 ? seen : null, BattleSeed);
            PlaceTowers(battle, Player, false);
            PlaceTowers(battle, Enemy, true);
            return battle;
        }

        /// <summary>
        /// The deployment preview as `viewer` sees it (two humans, M11): the viewer's current deployment first, then the other side's
        /// army as the viewer saw it when the last battle started, as ghosts. Simulation squad i is the viewer's squad i, then
        /// SeenArmy(other side) in order; then the viewer's towers at their levels and the other side's at the levels it saw. Each side
        /// stands in its own half, as in the battle. Viewer A gets BuildPreview(). The preview is never canonical (no jitter).
        /// </summary>
        public BattleSimulation BuildPreview(Team viewer)
        {
            if (viewer == Team.A) return BuildPreview();
            Team other = viewer.Opponent();
            PlayerState seen = SeenAsDeployment(other);
            var battle = new BattleSimulation(CombatRules, BattleSeed);
            var layout = new RunnerSettings { Battles = 1, BaseSeed = BattleSeed, PositionJitter = 0f, FrontLineDistance = 0f, SquadGap = 0f };
            var layoutRandom = new SimRandom(BattleSeed ^ 0x5EED);
            ArmyLayout.Place(battle, CombatRules, DeploymentGrid.BuildArmy(Get(viewer), CombatRules), viewer, layout, layoutRandom);
            if (seen.Squads.Count > 0) ArmyLayout.Place(battle, CombatRules, DeploymentGrid.BuildArmy(seen, CombatRules), other, layout, layoutRandom);
            PlaceTowers(battle, Get(viewer), false);
            PlaceTowers(battle, Get(other), true);
            return battle;
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
            return FinishWith(OutcomeOfCurrentBattle());
        }

        /// <summary>
        /// Applies a round outcome given from elsewhere instead of this machine's own battle (two humans, M11: the host's result is final).
        /// The local battle must be over: the summary keeps its result and kills for the screens, but base damage, XP and tower falls are
        /// the outcome's. Fails (throws) if the outcome is for another round.
        /// </summary>
        public RoundSummary FinishBattle(RoundOutcome outcome)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            if (Phase != MatchPhase.Battle || CurrentBattle == null || !CurrentBattle.IsFinished)
                throw new InvalidOperationException("Finish the battle first.");
            if (outcome.Round != Round) throw new InvalidOperationException($"That outcome is for round {outcome.Round}, not round {Round}.");
            return FinishWith(outcome);
        }

        /// <summary>
        /// What the finished battle of this round does to the match: base damage by each side, the XP each placed squad earned (in battle
        /// order, Team A's squads first) and the towers that fell. FinishBattle applies exactly this.
        /// </summary>
        public RoundOutcome OutcomeOfCurrentBattle()
        {
            if (Phase != MatchPhase.Battle || CurrentBattle == null || !CurrentBattle.IsFinished)
                throw new InvalidOperationException("Finish the battle first.");
            BattleResult result = CurrentBattle.Result;
            var outcome = new RoundOutcome
            {
                Round = Round,
                BaseDamageByA = (int)MathF.Round(result.TeamA.SurvivorBaseDamage),
                BaseDamageByB = (int)MathF.Round(result.TeamB.SurvivorBaseDamage),
                Ticks = result.Ticks,
            };
            List<PlacedSquad> placed = PlacedInBattleOrder();
            float[] earned = XpEarnedIn(CurrentBattle, placed);
            for (int i = 0; i < placed.Count; i++) outcome.Xp.Add(new SquadXp { SquadId = placed[i].Id, Xp = earned[i] });
            foreach (SimUnit unit in CurrentBattle.Units)
            {
                if (unit.Stats.IsTower && !unit.IsAlive) outcome.TowerFalls.Add(new TowerFall { Team = unit.Team, Kind = unit.Stats.Shockwave.Kind });
            }
            return outcome;
        }

        private RoundSummary FinishWith(RoundOutcome outcome)
        {
            BattleResult result = CurrentBattle.Result;
            var summary = new RoundSummary
            {
                Round = Round,
                Battle = result,
                BaseDamageByA = outcome.BaseDamageByA,
                BaseDamageByB = outcome.BaseDamageByB,
                Kills = RoundSummary.CountKills(CurrentBattle),
            };
            AwardXp(outcome, summary);
            if (_battleHistory.Count > 0) _battleHistory[_battleHistory.Count - 1].Finish(CurrentBattle);
            foreach (TowerFall fall in outcome.TowerFalls)
            {
                PlacedTower tower = Get(fall.Team).FindTower(fall.Kind);
                if (tower != null) tower.Falls++;
            }

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
                summary.IncomeAwardedB = NextIncomeFor(Team.B);
                Player.Credits += summary.IncomeAwarded;
                Enemy.Credits += summary.IncomeAwardedB;
                Player.IncomePenalty = 0;
                Enemy.IncomePenalty = 0;
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
                player.BoughtExtraSlot = player.UsedRapidResupply = player.EliteRecruitment = player.BoughtDeploymentModule = false;
                player.Offer = CardDeck.HasOffer(Rules, round) ? CardDeck.Draw(player, Rules, round, _random) : null;
            }
            Phase = MatchPhase.Deployment;

            // Two humans: no AI; each side takes or skips its own card (InCards), and its undo history starts once it has.
            if (IsTwoHumans)
            {
                foreach (Team team in new[] { Team.A, Team.B })
                {
                    if (Get(team).Offer == null) StartUndo(team);
                }
                return;
            }

            // The AI decides on its card at once, then deploys; the player sees the card screen first.
            var enemyView = new AiView(this, Team.B);
            if (Enemy.Offer != null)
            {
                int choice = _enemyAi.ChooseCard(enemyView, Enemy.Offer);
                if (choice < 0 || !ChooseCard(Team.B, choice).Ok) SkipCards(Team.B);
            }
            _enemyAi.Deploy(enemyView);
            if (Player.Offer != null) Phase = MatchPhase.Cards;
            else StartUndo(Team.A);
        }

        // ------------------------------------------------------------------ cards and spells (Milestone 7)

        // ------------------------------------------------------------------ undo in deployment (the owner, 2026-11-06)

        /// <summary>Starts this round's undo history from the side's state as it is now (deployment has begun, the card is chosen).</summary>
        private void StartUndo(Team team)
        {
            int side = Side(team);
            _undo[side].Clear();
            _undoNow[side] = Get(team).TakeSnapshot();
            _undoRound[side] = Round;
        }

        /// <summary>The state before the side's last action becomes a step Undo can go back to (if its history this round has started).</summary>
        private void PushUndo(Team team)
        {
            int side = Side(team);
            if (Phase != MatchPhase.Deployment || _undoRound[side] != Round || _undoNow[side] == null) return;
            _undo[side].Push(_undoNow[side]);
            _undoNow[side] = Get(team).TakeSnapshot();
        }

        /// <summary>
        /// Call after each of the player's deployment actions that worked: the state before it becomes a step Undo can go back to.
        /// Actions outside deployment, or before this round's history has started, are not recorded. (Team A; RecordAction(team).)
        /// </summary>
        public void RecordPlayerAction() => RecordAction(Team.A);

        /// <summary>
        /// Call after each of this side's deployment actions that worked (against the AI). In a two-humans match the Match records every
        /// action itself, so that both machines keep the same history, and this does nothing.
        /// </summary>
        public void RecordAction(Team team)
        {
            if (IsTwoHumans) return;
            PushUndo(team);
        }

        /// <summary>True if the player has a deployment action this round to take back. (Team A; CanUndoFor(team). Always false in a two-humans match.)</summary>
        public bool CanUndo => !IsTwoHumans && CanUndoFor(Team.A);

        /// <summary>How many of this round's actions can still be taken back. (Team A; UndoStepsFor(team). Always 0 in a two-humans match.)</summary>
        public int UndoSteps => IsTwoHumans ? 0 : UndoStepsFor(Team.A);

        /// <summary>True if this side has a deployment action this round to take back.</summary>
        public bool CanUndoFor(Team team) => Phase == MatchPhase.Deployment && _undoRound[Side(team)] == Round && _undo[Side(team)].Count > 0;

        /// <summary>How many of this side's actions this round can still be taken back.</summary>
        public int UndoStepsFor(Team team) => CanUndoFor(team) ? _undo[Side(team)].Count : 0;

        /// <summary>
        /// Takes back the player's last deployment action this round: credits, squads, places, levels, techs, towers, spells and items as
        /// they were. (Team A.) In a two-humans match this fails, since it would not know whose side to undo: use Undo(team).
        /// </summary>
        public ActionResult Undo() => IsTwoHumans ? ActionResult.Fail(TwoHumansNeedsTeam) : Undo(Team.A);

        /// <summary>Takes back this side's last deployment action this round (in a two-humans match also its squad and spell id counters).</summary>
        public ActionResult Undo(Team team)
        {
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Undo works during deployment.");
            if (!CanUndoFor(team)) return ActionResult.Fail("Nothing to undo this round.");
            int side = Side(team);
            _undoNow[side] = _undo[side].Pop();
            Get(team).Restore(_undoNow[side]);
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.UndoAction }, undoable: false);
            return ActionResult.Success("Undone.");
        }

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
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.ChooseCardAction, Index = index }, undoable: false);
            return ActionResult.Success(message);
        }

        /// <summary>What a general card does when it is taken.</summary>
        private string ApplyCard(PlayerState player, CardStats card)
        {
            if (card.Type == CardType.Spell)
            {
                player.Spells.Add(new OwnedSpell { Id = NewSpellId(player), Stats = card.Spell, ReadyRound = Round });
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
            if (WaitsForCard(team, out ActionResult card)) return card;
            PlayerState player = Get(team);
            PlacedSquad squad = player.FindSquad(squadId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Items are placed during deployment.");
            if (item == null || !player.Inventory.Contains(item)) return ActionResult.Fail("That item is not in your inventory.");
            if (squad == null) return ActionResult.Fail("No such squad.");
            if (squad.Item != null) return ActionResult.Fail($"{squad.Type.Name} already carries {squad.Item.Name}.");
            int inventoryIndex = player.Inventory.IndexOf(item);
            player.Inventory.Remove(item);
            squad.Item = item;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.PlaceItemAction, Item = item.Name, Index = inventoryIndex, SquadId = squadId });
            return ActionResult.Success($"{item.Name} placed on {squad.Type.Name}.");
        }

        /// <summary>True if this side has a copy of Intensive Training that has recharged.</summary>
        public bool CanUseTraining(Team team) => Phase == MatchPhase.Deployment && Get(team).Trainings.Exists(t => t.IsReady(Round));

        /// <summary>Intensive Training: fills the squad's XP bar (it can then be leveled up); recharges like a spell.</summary>
        public ActionResult UseTraining(Team team, int squadId)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
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
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.UseTrainingAction, SquadId = squadId });
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
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.SkipCardsAction }, undoable: false);
            return ActionResult.Success($"Skipped: +{bonus} credits.");
        }

        private void ResolveOffer(Team team)
        {
            Get(team).Offer = null;
            if (IsTwoHumans)
            {
                if (Phase == MatchPhase.Deployment) StartUndo(team); // The card choice is the line for this side.
                return;
            }
            if (team == Team.A && Phase == MatchPhase.Cards)
            {
                Phase = MatchPhase.Deployment;
                StartUndo(Team.A); // The card choice is the line: what came before it can't be undone.
            }
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
                    Id = NewSquadId(player), Type = card.Type, Column = column, Row = row, RoundBought = Round,
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
        public ActionResult PlaceSpell(Team team, int spellId, Vec2 position, float angleDegrees = 0f)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            OwnedSpell spell = Get(team).FindSpell(spellId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Spells are placed during deployment.");
            if (spell == null) return ActionResult.Fail("No such spell.");
            if (!spell.IsReady(Round)) return ActionResult.Fail($"{spell.Stats.Name} is recharging ({spell.RoundsLeft(Round)} more round(s)).");
            if (!CanPlaceSpell(team, spell, position))
                return ActionResult.Fail(spell.Stats.Offensive ? "Place it on the battlefield." : $"{spell.Stats.Name} can only be placed on your own side.");
            spell.IsPlaced = true;
            spell.Position = position;
            spell.AngleDegrees = angleDegrees;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.PlaceSpellAction, SpellId = spellId, X = position.X, Y = position.Y, Angle = angleDegrees });
            return ActionResult.Success($"{spell.Stats.Name} placed.");
        }

        /// <summary>Takes a placed spell back (it stays ready).</summary>
        public ActionResult UnplaceSpell(Team team, int spellId)
        {
            if (WaitsForCard(team, out ActionResult card)) return card;
            OwnedSpell spell = Get(team).FindSpell(spellId);
            if (Phase != MatchPhase.Deployment) return ActionResult.Fail("Spells are placed during deployment.");
            if (spell == null || !spell.IsPlaced) return ActionResult.Fail("That spell is not placed.");
            spell.IsPlaced = false;
            if (IsTwoHumans) Record(team, new MatchCommand { Action = MatchCommand.UnplaceSpellAction, SpellId = spellId });
            return ActionResult.Success($"{spell.Stats.Name} taken back.");
        }
    }
}
