// LanMatchCoordinator.cs
// Runs one two-humans Match over a LAN channel by lockstep of decisions (Milestone 11). Each machine holds the same Match; the
// local player acts live on its own side, and the coordinator swaps the decisions with the other machine:
//   Draft       the local pick goes through ChooseDraft here -> DraftPick {commands}. The other side's pick is applied once the
//               local one is in (MatchCommands.ApplyAll, which calls Match.ChooseDraft(remote, i)). Both in -> round 1.
//   Deployment  the local player deploys live on the Match with its own team (recorded by the Match). Ready here sends the
//               round's whole command list -> Ready {round, commands}. Once both lists are in, the other side's list is applied
//               (any command that fails is a desync), then both exchange MatchStateHash -> StateHash {round, hash}. When the two
//               hashes agree, CanStartBattle is true; if not, the state is Desync with both hashes.
//   Battle      StartBattle here; each machine plays its battle at its own speed. When it is over, call FinishBattle: the host
//               finishes at once and sends BattleResult {round, outcome, hash, state}; the guest waits for the host's result and
//               finishes with it (Match.FinishBattle(RoundOutcome)). If the guest's own battle ended differently, that is noted
//               (HostResultUsed) and the match goes on with the host's numbers; if the state after it still differs, it is Desync.
//   Result      ContinueAfterResult here (or on the Match) starts the next round on this machine; the other one may be ahead or
//               behind: messages for a later round wait in a buffer until this machine gets there. Only a little later: a message
//               for a round more than MaxRoundsAhead past this machine's (or for a round already behind it) is a Desync, so the
//               buffer never holds more than a few rounds.
// Update handles at most LanLimits.MaxMessagesPerUpdate messages; the rest wait for the next frame. Text from the other game (a
// message type, a reason) is cut short (LanLimits.Clip) before it goes into a reason, the log or a Desync sent back.
// Status messages tell the other game where this one is (its battle is over; it left the result for the next round), so the HUD
// can say what the other player is doing (OpponentStep). They change nothing in the match.
// The log (when given) gets every command list sent and applied and the round's result text, so a desync between two PCs can be
// looked into and replayed from both games' logs.
// A Leave from the other side gives OpponentLeft, a closed channel ConnectionLost, a found desync Desync (the other side is told
// with a Desync message, so both stop). A message that cannot be read, of any kind, is a desync too. After the match is over,
// the other side leaving is not an error. If the host leaves right after sending the last result, the guest still finishes
// that round with it first. A final state keeps the channel open (so the Desync message is not cut off): the scene calls Leave
// when it leaves the match screen, which closes the channel and keeps a final state or MatchOver as it is.
// The local player must not act on the Match after Ready in the same round (LocalCanAct says when it may), nor ever for the
// other team: an action after Ready is a desync, found when the lists are applied, when the hashes are compared, or at the
// latest in StartBattle (before this machine plays a battle the other game did not agree to).
// Everything runs on the caller's thread: call Update every frame.
// Plain C# 9 without the engine.
// Depends on: ILanChannel, LanMessages, NetMessage, NetLog, LanLimits, Halobellum.Economy (Match, MatchCommand, MatchCommands, MatchStateHash,
//             RoundOutcome, RoundSummary), Halobellum.Combat (BattleSimulation), Halobellum.Core (Team).

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Net
{
    public enum LanMatchState
    {
        /// <summary>Picking starting cards (WaitingForOpponent once the local pick is in).</summary>
        Draft,
        /// <summary>The local player takes or skips its card and deploys, then presses Ready.</summary>
        Deployment,
        /// <summary>The local Ready is sent; the other player's is not here yet.</summary>
        WaitingForOpponent,
        /// <summary>Both lists are applied; waiting for the other game's state hash.</summary>
        CheckingSync,
        /// <summary>Both games agree: the battle may start (CanStartBattle).</summary>
        ReadyToFight,
        /// <summary>The battle is playing on this machine.</summary>
        Battle,
        /// <summary>Guest: its battle is over; waiting for the host's result.</summary>
        WaitingForHostResult,
        /// <summary>The round's result is in; ContinueAfterResult starts the next round.</summary>
        Result,
        /// <summary>A base fell.</summary>
        MatchOver,
        /// <summary>The two games no longer agree (LastError says how). Final.</summary>
        Desync,
        /// <summary>The other player left (LastError says why). Final.</summary>
        OpponentLeft,
        /// <summary>The connection was lost (LastError says why). Final.</summary>
        ConnectionLost,
        /// <summary>This player left (Leave). Final.</summary>
        Left,
    }

    /// <summary>What the other player is doing, as far as this game knows from the other game's messages (for the HUD).</summary>
    public enum LanOpponentStep
    {
        /// <summary>Picking its starting card.</summary>
        Picking,
        /// <summary>Deploying (its Ready for its current round is not here).</summary>
        Deploying,
        /// <summary>Its Ready is here; this game has not agreed on that round's state yet.</summary>
        Ready,
        /// <summary>Both games agreed: its battle is playing (or about to).</summary>
        InBattle,
        /// <summary>Its battle is over: it sees the round's result (the guest may still wait for the host's).</summary>
        BattleOver,
        /// <summary>The match is over.</summary>
        MatchOver,
        /// <summary>It left after the match was over.</summary>
        Left,
        /// <summary>The match has ended (a final state).</summary>
        Gone,
    }

    public sealed class LanMatchCoordinator
    {
        private sealed class HostResult
        {
            public RoundOutcome Outcome;
            public ulong Hash;
            public ulong State;
        }

        private readonly Action<string> log;
        private string remoteDraft;                                                  // the other side's starting card (command text), until applied
        private readonly Dictionary<int, string> remoteReady = new Dictionary<int, string>();        // by round: the other side's command text
        private readonly Dictionary<int, ulong> remoteHashes = new Dictionary<int, ulong>();         // by round: the other side's state hash
        private readonly Dictionary<int, HostResult> hostResults = new Dictionary<int, HostResult>(); // by round (guest): the host's result
        private readonly List<int> hostResultRounds = new List<int>();
        private bool draftSent;
        private int readyRound = -1;       // the round whose Ready this side has sent
        private int appliedRound = -1;     // the round whose remote list has been applied here
        private int agreedRound = -1;      // the round whose two state hashes agree
        private ulong appliedHash;         // this machine's state hash once the round's remote list was applied (the one sent)
        private int lastRemoteReadyRound;  // the last round a remote Ready came for (each must be later than the one before)
        private bool finishWanted;         // guest: the local battle is over and waits for the host's result
        private LanMatchState? final;
        private LanMatchState? pendingEnd; // the other side left or the line dropped, but a buffered host result still finishes the round
        private string pendingReason = "";
        private bool channelEnded;         // the closed channel has been seen (it is acted on once)
        private bool leftAfterMatch;       // Leave was called after the match was over: nothing more is read
        private int remoteBattleOverRound; // the last round the other game said its battle was over (Status)
        private int remoteContinuedRound;  // the last round the other game said it left the result of (Status)

        /// <summary>
        /// A coordinator for a two-humans match in its draft, with this machine playing <paramref name="local"/> (the host Team A,
        /// the guest Team B). Usually made by LanMatchStart.BeginAsHost or LanGuestStart.
        /// </summary>
        public LanMatchCoordinator(Match match, Team local, ILanChannel channel, Action<string> log = null)
        {
            Match = match ?? throw new ArgumentNullException(nameof(match));
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            if (!match.IsTwoHumans) throw new ArgumentException("A LAN match needs a two-humans Match (MatchSeats.TwoHumans).", nameof(match));
            if (match.Phase != MatchPhase.Draft) throw new ArgumentException("A LAN match starts at the draft.", nameof(match));
            LocalTeam = local;
            this.log = log;
        }

        public Match Match { get; }
        public ILanChannel Channel { get; }
        public Team LocalTeam { get; }
        public Team RemoteTeam => LocalTeam == Team.A ? Team.B : Team.A;
        /// <summary>The host plays Team A, and its battle result is final.</summary>
        public bool IsHost => LocalTeam == Team.A;

        /// <summary>Raised when this machine finishes a round's battle (host: in FinishBattle; guest: when the host's result is applied).</summary>
        public event Action<RoundSummary> BattleFinished;

        // ------------------------------------------------------------------ status (read-only, for the HUD)

        public LanMatchState State
        {
            get
            {
                if (final.HasValue) return final.Value;
                switch (Match.Phase)
                {
                    case MatchPhase.Draft: return LanMatchState.Draft;
                    case MatchPhase.Battle: return finishWanted ? LanMatchState.WaitingForHostResult : LanMatchState.Battle;
                    case MatchPhase.Result: return LanMatchState.Result;
                    case MatchPhase.MatchOver: return LanMatchState.MatchOver;
                    default:
                        if (readyRound != Match.Round) return LanMatchState.Deployment;
                        if (appliedRound != Match.Round) return LanMatchState.WaitingForOpponent;
                        if (agreedRound != Match.Round) return LanMatchState.CheckingSync;
                        return LanMatchState.ReadyToFight;
                }
            }
        }

        /// <summary>True once the match cannot go on (Desync, OpponentLeft, ConnectionLost, Left).</summary>
        public bool IsFinished => final.HasValue;

        /// <summary>True while the local player may act on the Match for its team (take or skip its card, deploy, undo).</summary>
        public bool LocalCanAct => State == LanMatchState.Deployment;

        public bool LocalPickedDraft => draftSent || Match.Phase != MatchPhase.Draft;

        public bool OpponentPickedDraft => remoteDraft != null || Match.HasChosenDraft(RemoteTeam);

        /// <summary>The local Ready for this round is sent.</summary>
        public bool LocalReady => Match.Phase == MatchPhase.Deployment && readyRound == Match.Round;

        /// <summary>The other player pressed Ready this round (its list is here, applied or not).</summary>
        public bool OpponentReady => Match.Phase == MatchPhase.Deployment && (remoteReady.ContainsKey(Match.Round) || appliedRound == Match.Round);

        /// <summary>This machine waits for the other one: its starting card, its Ready, its state hash, or (guest) the host's result.</summary>
        public bool WaitingForOpponent
        {
            get
            {
                switch (State)
                {
                    case LanMatchState.Draft: return LocalPickedDraft && !OpponentPickedDraft;
                    case LanMatchState.WaitingForOpponent:
                    case LanMatchState.CheckingSync:
                    case LanMatchState.WaitingForHostResult:
                        return true;
                    default: return false;
                }
            }
        }

        /// <summary>Both games agree on the state before this round's battle: StartBattle may be called.</summary>
        public bool CanStartBattle => State == LanMatchState.ReadyToFight && Match.CanStartBattle;

        /// <summary>The last problem in words (a desync, why the other player left, why the connection ended); "" if none.</summary>
        public string LastError { get; private set; } = "";

        /// <summary>
        /// The last state hashes compared (hex), here and from the other game: before each battle, and (guest) after the host's result
        /// when that comparison failed. "" before the first. After a desync they differ only if the desync was a state difference:
        /// then they are the two hashes that differed (otherwise they are an earlier agreed pair, or one is "").
        /// </summary>
        public string LocalHash { get; private set; } = "";
        public string RemoteHash { get; private set; } = "";

        /// <summary>Guest: true once its own battle ended differently from the host's in some round (the host's result was used).</summary>
        public bool HostResultUsed => hostResultRounds.Count > 0;

        /// <summary>Guest: the rounds whose battle ended differently here, in order.</summary>
        public IReadOnlyList<int> HostResultRounds => hostResultRounds;

        /// <summary>The other side left or the connection ended after the match was over (not an error then).</summary>
        public bool OpponentGone { get; private set; }

        /// <summary>The hash of the last round result applied here (RoundOutcome.Hash: the host's own, or on the guest the host's); 0 before the first.</summary>
        public ulong LastOutcomeHash { get; private set; }

        /// <summary>
        /// The hash of this game's own battle result in the last finished round (RoundOutcome.Hash); 0 before the first. On the host it
        /// is LastOutcomeHash. On the guest it differs from LastOutcomeHash when its own battle ended differently (the host's was used).
        /// </summary>
        public ulong LastOwnOutcomeHash { get; private set; }

        /// <summary>Guest: true on the result of a round (Match.Round) whose battle ended differently here, so the host's result was used.</summary>
        public bool HostResultUsedThisRound => hostResultRounds.Count > 0 && hostResultRounds[hostResultRounds.Count - 1] == Match.Round
            && (Match.Phase == MatchPhase.Result || Match.Phase == MatchPhase.MatchOver);

        /// <summary>
        /// What the other player is doing, from what its messages said: its starting card, its Ready for a round, its Status (battle
        /// over, result left). Never taken from this game's own phase alone, which may be a round ahead or behind the other one.
        /// </summary>
        public LanOpponentStep OpponentStep
        {
            get
            {
                if (final.HasValue) return LanOpponentStep.Gone;
                if (Match.Phase == MatchPhase.Draft) return OpponentPickedDraft ? LanOpponentStep.Ready : LanOpponentStep.Picking;
                if (Match.Phase == MatchPhase.MatchOver) return OpponentGone ? LanOpponentStep.Left : LanOpponentStep.MatchOver;
                int ready = Math.Max(lastRemoteReadyRound, appliedRound); // the last round the other player pressed Ready in
                if (ready <= 0 || remoteContinuedRound >= ready) return LanOpponentStep.Deploying;
                if (remoteBattleOverRound >= ready) return LanOpponentStep.BattleOver;
                // Its Ready is here. Once this game agrees on that round's state (it sent its own hash first), both start the battle.
                bool started = agreedRound >= ready || Match.Round > ready || (Match.Round == ready && Match.Phase != MatchPhase.Deployment);
                return started ? LanOpponentStep.InBattle : LanOpponentStep.Ready;
            }
        }

        /// <summary>A short line for the HUD.</summary>
        public string StatusText
        {
            get
            {
                switch (State)
                {
                    case LanMatchState.Draft: return !LocalPickedDraft ? "Pick your starting card." : "Waiting for the other player's starting card.";
                    case LanMatchState.Deployment: return OpponentReady ? "The other player is ready." : "Deploy, then press Ready.";
                    case LanMatchState.WaitingForOpponent: return "Waiting for the other player to press Ready.";
                    case LanMatchState.CheckingSync: return "Checking that both games agree...";
                    case LanMatchState.ReadyToFight: return "Both players are ready.";
                    case LanMatchState.Battle: return "Battle.";
                    case LanMatchState.WaitingForHostResult: return "Waiting for the host's result.";
                    case LanMatchState.Result: return "Round result.";
                    case LanMatchState.MatchOver: return "Match over.";
                    case LanMatchState.Desync: return "Out of sync: " + LastError;
                    case LanMatchState.OpponentLeft: return "The other player left. " + LastError;
                    case LanMatchState.ConnectionLost: return "Connection lost. " + LastError;
                    case LanMatchState.Left: return "You left the match.";
                    default: return "";
                }
            }
        }

        // ------------------------------------------------------------------ the local player's steps

        /// <summary>The local player's starting card: picked on the Match and sent.</summary>
        public ActionResult ChooseDraft(int cardIndex)
        {
            if (final.HasValue) return ActionResult.Fail("The match has ended.");
            if (Match.Phase != MatchPhase.Draft) return ActionResult.Fail("The draft is over.");
            if (draftSent) return ActionResult.Fail("Your starting card is already chosen.");
            ActionResult result = Match.ChooseDraft(LocalTeam, cardIndex);
            if (!result.Ok) return result;
            List<MatchCommand> commands = Match.TakeCommands(LocalTeam);
            draftSent = true;
            Channel.Send(new NetMessage(LanMessageTypes.DraftPick).Set(LanFields.Round, 0).Set(LanFields.Index, cardIndex)
                .Set(LanFields.Commands, MatchCommand.ListToText(commands)));
            NetLog.Write(log, "Starting card " + cardIndex + " sent." + CommandLines(commands));
            Advance();
            return result;
        }

        /// <summary>The local player is done for this round: its whole command list goes to the other game.</summary>
        public ActionResult Ready()
        {
            if (final.HasValue) return ActionResult.Fail("The match has ended.");
            if (State != LanMatchState.Deployment) return ActionResult.Fail("Not in deployment.");
            if (Match.InCards(LocalTeam)) return ActionResult.Fail("Take or skip your card first.");
            List<MatchCommand> commands = Match.TakeCommands(LocalTeam);
            readyRound = Match.Round;
            Channel.Send(new NetMessage(LanMessageTypes.Ready).Set(LanFields.Round, Match.Round).Set(LanFields.Commands, MatchCommand.ListToText(commands)));
            NetLog.Write(log, "Round " + Match.Round + ": Ready sent with " + commands.Count + " command(s)." + CommandLines(commands));
            Advance();
            return ActionResult.Success();
        }

        /// <summary>
        /// Starts this round's battle on this machine once both games agree. Null if it may not start yet. If this game acted on
        /// the match after its hash was made (so its state is no longer the agreed one), it is a desync and the battle does not start.
        /// </summary>
        public BattleSimulation StartBattle()
        {
            if (!CanStartBattle) return null;
            if (Match.PendingCommands(LocalTeam) > 0 || MatchStateHash.Compute(Match) != appliedHash)
            {
                Desync("this game acted on the match after pressing Ready.");
                return null;
            }
            return Match.StartBattle();
        }

        /// <summary>
        /// Call when this machine's battle is over. The host finishes at once and sends its result. The guest finishes with the host's
        /// result, at once if it is here, else in a later Update (State WaitingForHostResult until then). True if the round is
        /// finished now. BattleFinished is raised either way when it finishes.
        /// </summary>
        public bool FinishBattle()
        {
            if (final.HasValue) return false;
            if (Match.Phase != MatchPhase.Battle || Match.CurrentBattle == null || !Match.CurrentBattle.IsFinished)
                throw new InvalidOperationException("Finish the battle first.");
            if (IsHost)
            {
                RoundOutcome outcome = Match.OutcomeOfCurrentBattle();
                RoundSummary summary = Match.FinishBattle(outcome);
                LastOutcomeHash = outcome.Hash();
                LastOwnOutcomeHash = LastOutcomeHash;
                ulong state = MatchStateHash.Compute(Match);
                Channel.Send(new NetMessage(LanMessageTypes.BattleResult).Set(LanFields.Round, outcome.Round).Set(LanFields.Outcome, outcome.ToText())
                    .Set(LanFields.Hash, outcome.Hash()).Set(LanFields.State, state));
                SendStatus(outcome.Round, LanStatusSteps.BattleOver);
                NetLog.Write(log, "Round " + outcome.Round + ": result sent (" + outcome + ").\n" + outcome.ToText());
                BattleFinished?.Invoke(summary);
                return true;
            }
            if (!finishWanted) SendStatus(Match.Round, LanStatusSteps.BattleOver);
            finishWanted = true;
            Advance();
            return !finishWanted;
        }

        /// <summary>Leaves the result for the next round (as Match.ContinueAfterResult).</summary>
        public void ContinueAfterResult()
        {
            if (final.HasValue) return;
            int round = Match.Round;
            bool onResult = Match.Phase == MatchPhase.Result;
            Match.ContinueAfterResult();
            if (onResult && Match.Phase != MatchPhase.Result) SendStatus(round, LanStatusSteps.Continued);
            Advance();
        }

        /// <summary>
        /// The local player leaves: the other game is told (Leave {reason}) and the channel closes. During a match the state becomes
        /// Left. After a final state (Desync, OpponentLeft, ConnectionLost) or once the match is over, the state and LastError stay
        /// as they are and only the channel closes (after MatchOver the other game is still told). Call it whenever the scene
        /// leaves the match screen.
        /// </summary>
        public void Leave(string reason = "The other player left the match.")
        {
            if (final.HasValue)
            {
                if (final.Value != LanMatchState.Left) Channel.Close(); // the other game already stopped, or was told why
                return;
            }
            if (leftAfterMatch) return;
            if (Channel.IsOpen) Channel.Send(new NetMessage(LanMessageTypes.Leave).Set(LanFields.Reason, reason ?? ""));
            Channel.Close();
            if (Match.Phase == MatchPhase.MatchOver)
            {
                leftAfterMatch = true;
                NetLog.Write(log, "Left after the match.");
                return;
            }
            final = LanMatchState.Left;
            NetLog.Write(log, "Left the match.");
        }

        // ------------------------------------------------------------------ every frame

        /// <summary>
        /// How many rounds past this machine's a message from the other game may be. The other game is at most one round ahead (it
        /// cannot press Ready for the round after next before both agreed on the next one); 2 leaves room. Later is a Desync.
        /// </summary>
        public const int MaxRoundsAhead = 2;

        /// <summary>Reads the other game's messages, applies what can be applied, and notices a closed channel. Call every frame.</summary>
        public void Update()
        {
            if (final.HasValue || leftAfterMatch) return;
            bool open = Channel.IsOpen; // read before the inbox: a message that came just before a close is still read
            int handled = 0;
            while (!final.HasValue && handled < LanLimits.MaxMessagesPerUpdate && Channel.TryReceive(out NetMessage message))
            {
                handled++;
                Handle(message);
            }
            // With the frame's share used up, more may wait: the close is noticed in a later Update, after them.
            if (!final.HasValue && !open && !channelEnded && handled < LanLimits.MaxMessagesPerUpdate)
            {
                channelEnded = true;
                OtherSideEnded(LanMatchState.ConnectionLost, Describe(Channel.LostReason));
            }
            Advance();
        }

        private void Handle(NetMessage m)
        {
            try
            {
                switch (m.Type)
                {
                    case LanMessageTypes.DraftPick:
                        if (Match.Phase != MatchPhase.Draft || remoteDraft != null || Match.HasChosenDraft(RemoteTeam)) { Protocol("a second starting card"); return; }
                        remoteDraft = m.GetString(LanFields.Commands, "");
                        break;
                    case LanMessageTypes.Ready:
                    {
                        int round = m.GetInt(LanFields.Round);
                        if (TooFarAhead(round, "a Ready")) return;
                        if (round < 1 || round <= lastRemoteReadyRound || round < Match.Round) { Protocol("a Ready for round " + round); return; }
                        lastRemoteReadyRound = round;
                        remoteReady[round] = m.GetString(LanFields.Commands, "");
                        break;
                    }
                    case LanMessageTypes.StateHash:
                    {
                        int round = m.GetInt(LanFields.Round);
                        if (TooFarAhead(round, "a state hash")) return;
                        if (remoteHashes.ContainsKey(round) || round < Match.Round) { Protocol("a second state hash for round " + round); return; }
                        remoteHashes[round] = m.GetULong(LanFields.Hash);
                        break;
                    }
                    case LanMessageTypes.BattleResult:
                    {
                        int round = m.GetInt(LanFields.Round);
                        if (IsHost) { Protocol("a battle result (only the host sends those)"); return; }
                        if (TooFarAhead(round, "a battle result")) return;
                        if (hostResults.ContainsKey(round) || round < Match.Round) { Protocol("a second result for round " + round); return; }
                        var result = new HostResult
                        {
                            Outcome = RoundOutcome.Parse(m.GetString(LanFields.Outcome, "")),
                            Hash = m.GetULong(LanFields.Hash),
                            State = m.GetULong(LanFields.State),
                        };
                        if (result.Outcome.Round != round) { Protocol("a result for round " + result.Outcome.Round + " sent as round " + round); return; }
                        hostResults[round] = result;
                        break;
                    }
                    case LanMessageTypes.Desync:
                        End(LanMatchState.Desync, "The other game found the match out of sync in round " + m.GetInt(LanFields.Round) + ": "
                            + LanLimits.Clip(m.GetString(LanFields.Reason, "")));
                        break;
                    case LanMessageTypes.Status:
                    {
                        // Only for the HUD: a Status that makes no sense is ignored, never a desync.
                        int round = m.GetInt(LanFields.Round);
                        string step = m.GetString(LanFields.Step, "");
                        if (round < 1 || round > lastRemoteReadyRound) NetLog.Write(log, "Ignored a Status for round " + round + ".");
                        else if (step == LanStatusSteps.BattleOver) remoteBattleOverRound = Math.Max(remoteBattleOverRound, round);
                        else if (step == LanStatusSteps.Continued) remoteContinuedRound = Math.Max(remoteContinuedRound, round);
                        break;
                    }
                    case LanMessageTypes.Leave:
                    {
                        string reason = LanLimits.Clip(m.GetString(LanFields.Reason, ""));
                        if (m.GetBool(LanFields.Refused)) reason = "The other game refused the match: " + reason;
                        OtherSideEnded(LanMatchState.OpponentLeft, reason.Length > 0 ? reason : "The other player left the match.");
                        break;
                    }
                    default:
                        NetLog.Write(log, "Ignored a " + LanLimits.Clip(m.Type, LanLimits.MaxNameChars) + " message.");
                        break;
                }
            }
            catch (Exception e)
            {
                // Any exception, not only FormatException: the text readers can also throw IndexOutOfRange, ArgumentOutOfRange or
                // Overflow on cut-off or odd text. Whatever it is, the message is broken and the match cannot go on.
                Protocol("a broken " + LanLimits.Clip(m.Type, LanLimits.MaxNameChars) + " message (" + e.GetType().Name + ": "
                    + LanLimits.Clip(e.Message, LanLimits.MaxNameChars * 2) + ")");
            }
        }

        /// <summary>Applies everything that can be applied now, in order.</summary>
        private void Advance()
        {
            if (final.HasValue) return;

            // The other side's starting card, once this side's is in (so the local player never picks knowing the other's card).
            if (remoteDraft != null && draftSent && Match.Phase == MatchPhase.Draft)
            {
                string text = remoteDraft;
                remoteDraft = null;
                NetLog.Write(log, "The other player's starting card." + CommandLines(text));
                if (!ApplyRemote(text, "starting card")) return;
            }

            if (Match.Phase == MatchPhase.Deployment)
            {
                int round = Match.Round;
                // Both Readys in: apply the other side's list, then send this machine's state hash.
                if (readyRound == round && appliedRound != round && remoteReady.TryGetValue(round, out string text))
                {
                    remoteReady.Remove(round);
                    if (Match.PendingCommands(LocalTeam) > 0)
                    {
                        Desync("this game acted on the match after pressing Ready.");
                        return;
                    }
                    NetLog.Write(log, "Round " + round + ": the other player's list." + CommandLines(text));
                    if (!ApplyRemote(text, "list")) return;
                    if (!Match.CanStartBattle)
                    {
                        Desync("the other player's card is still open after its Ready.");
                        return;
                    }
                    appliedRound = round;
                    ulong hash = MatchStateHash.Compute(Match);
                    appliedHash = hash;
                    LocalHash = MatchStateHash.Hex(hash);
                    RemoteHash = "";
                    Channel.Send(new NetMessage(LanMessageTypes.StateHash).Set(LanFields.Round, round).Set(LanFields.Hash, hash));
                }
                // Both hashes in: compare.
                if (appliedRound == round && agreedRound != round && remoteHashes.TryGetValue(round, out ulong theirs))
                {
                    remoteHashes.Remove(round);
                    RemoteHash = MatchStateHash.Hex(theirs);
                    if (Match.PendingCommands(LocalTeam) > 0)
                    {
                        Desync("this game acted on the match after pressing Ready.");
                        return;
                    }
                    if (RemoteHash != LocalHash)
                    {
                        Desync("the state before the battle differs (hash " + LocalHash + " here, " + RemoteHash + " there).");
                        return;
                    }
                    agreedRound = round;
                    NetLog.Write(log, "Round " + round + ": both games agree (" + LocalHash + ").");
                }
            }

            // Guest: the local battle is over and the host's result is here.
            if (finishWanted && Match.Phase == MatchPhase.Battle && hostResults.TryGetValue(Match.Round, out HostResult result))
            {
                hostResults.Remove(Match.Round);
                ApplyHostResult(result);
                if (final.HasValue) return;
            }

            // The other side left (or the line dropped): end now, or once a buffered host result has finished this round.
            if (pendingEnd.HasValue && !CanStillFinishRound())
            {
                LanMatchState state = pendingEnd.Value;
                pendingEnd = null;
                if (Match.Phase == MatchPhase.MatchOver)
                {
                    // The host's last result finished the match: not an error, but say why the other player is gone.
                    OpponentGone = true;
                    LastError = pendingReason;
                    NetLog.Write(log, "After the match: " + pendingReason);
                }
                else End(state, pendingReason);
            }
        }

        private void ApplyHostResult(HostResult result)
        {
            int round = Match.Round;
            if (result.Outcome.Hash() != result.Hash)
            {
                Desync("the host's result does not match its own hash.");
                return;
            }
            bool differed;
            RoundSummary summary;
            RoundOutcome own;
            try
            {
                own = Match.OutcomeOfCurrentBattle();
                differed = own.Hash() != result.Hash;
                summary = Match.FinishBattle(result.Outcome);
            }
            catch (Exception e)
            {
                Desync("the host's result could not be applied here (" + e.GetType().Name + ": " + e.Message + ").");
                return;
            }
            finishWanted = false;
            LastOutcomeHash = result.Hash;
            LastOwnOutcomeHash = own.Hash();
            if (differed)
            {
                hostResultRounds.Add(round);
                LastError = "Round " + round + ": this game's battle ended differently; the host's result is used.";
                NetLog.Write(log, LastError + "\nhost: " + result.Outcome + "\nhere: " + own + "\nhost: " + result.Outcome.ToText() + "\nhere: " + own.ToText());
            }
            ulong state = MatchStateHash.Compute(Match);
            if (state != result.State)
            {
                // The end screen shows the pair that differed, not the round's agreed pair from before the battle.
                LocalHash = MatchStateHash.Hex(state);
                RemoteHash = MatchStateHash.Hex(result.State);
                Desync("the state after the host's result differs (hash " + LocalHash + " here, " + RemoteHash + " there).");
                return;
            }
            BattleFinished?.Invoke(summary);
        }

        /// <summary>Applies one of the other side's command lists; a list that cannot be read or a command that fails is a desync.</summary>
        private bool ApplyRemote(string text, string what)
        {
            List<MatchCommand> commands;
            try
            {
                commands = MatchCommand.ParseList(text ?? "");
            }
            catch (Exception e)
            {
                // Any exception: on cut-off text the JSON reader can throw IndexOutOfRange and others, not only FormatException.
                Desync("the other player's " + what + " could not be read (" + e.GetType().Name + ": " + e.Message + ").");
                return false;
            }
            var failures = new List<string>();
            bool applied;
            try
            {
                applied = MatchCommands.ApplyAll(Match, RemoteTeam, commands, failures);
            }
            catch (Exception e)
            {
                Desync("the other player's " + what + " broke here (" + e.GetType().Name + ": " + e.Message + ").");
                return false;
            }
            if (applied) return true;
            Desync("the other player's " + what + " did not work here: " + (failures.Count > 0 ? failures[0] : "unknown") + ".");
            return false;
        }

        /// <summary>Guest: the host's result for this round is here and this machine can still play the battle to the end and apply it.</summary>
        private bool CanStillFinishRound() =>
            !IsHost && hostResults.ContainsKey(Match.Round)
            && (Match.Phase == MatchPhase.Battle || (Match.Phase == MatchPhase.Deployment && agreedRound == Match.Round));

        /// <summary>The other side left or the connection ended: final now, unless a buffered result still finishes this round.</summary>
        private void OtherSideEnded(LanMatchState state, string reason)
        {
            if (Match.Phase == MatchPhase.MatchOver)
            {
                if (OpponentGone) return; // already told (a Leave, then the close that follows it)
                OpponentGone = true;
                LastError = reason;
                NetLog.Write(log, "After the match: " + reason);
                return;
            }
            // Decided in Advance, once the messages that came before this one are applied: a buffered host result may still
            // finish the round first.
            if (pendingEnd.HasValue) return;
            pendingEnd = state;
            pendingReason = reason;
        }

        /// <summary>A message for a round more than MaxRoundsAhead past this machine's: Desync (true). Nothing is buffered for it.</summary>
        private bool TooFarAhead(int round, string what)
        {
            if (round <= Match.Round + MaxRoundsAhead) return false;
            Protocol(what + " for round " + round + ", more than " + MaxRoundsAhead + " rounds past this game's round " + Match.Round);
            return true;
        }

        private void Desync(string what)
        {
            what = LanLimits.Clip(what); // it may hold text from the other game; the Desync sent back must stay small
            string reason = "Round " + Match.Round + ": " + what;
            Channel.Send(new NetMessage(LanMessageTypes.Desync).Set(LanFields.Round, Match.Round).Set(LanFields.Reason, what));
            End(LanMatchState.Desync, reason);
        }

        private void Protocol(string what) => Desync("the other game sent " + what + ".");

        private void SendStatus(int round, string step) => Channel.Send(new NetMessage(LanMessageTypes.Status).Set(LanFields.Round, round).Set(LanFields.Step, step));

        /// <summary>A command list for the log: its lines after a line break, or "" if it is empty.</summary>
        private static string CommandLines(string text)
        {
            text = (text ?? "").TrimEnd('\n', '\r');
            return text.Length == 0 ? "" : "\n" + text;
        }

        private static string CommandLines(List<MatchCommand> commands) => CommandLines(MatchCommand.ListToText(commands));

        private void End(LanMatchState state, string reason)
        {
            final = state;
            LastError = reason ?? "";
            pendingEnd = null;
            NetLog.Write(log, "LAN match " + state + ": " + LastError);
        }

        private static string Describe(string lost) => string.IsNullOrEmpty(lost) ? "The connection to the other game ended." : lost;
    }
}
