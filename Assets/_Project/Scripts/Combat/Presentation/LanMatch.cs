// LanMatch.cs
// The LAN match driver (M11 Stage 4): on the Match scene's object only in a LAN match (MatchController adds it when GameFlow holds a
// LanLaunch). It links the coordinator to the scene, following lockstep.md's rules for the Match scene:
//   Start    the host builds the match and sends MatchStart (LanMatchStart.BeginAsHost); the guest reads the host's MatchStart
//            (LanGuestStart) and builds the same match. Both show it with MatchController.UseMatch for their own team.
//   Update   every frame, also in battle and on the result screen: coordinator.Update(); when the other side's starting card ends
//            the draft, the field is set up (OnRemoteCommandsApplied); when both games agree, the battle starts (StartBattle).
//   Battle   MatchController hands the end of the local battle here (FinishLocalBattle): the host finishes at once, the guest when
//            the host's result has come. The round's result shows once the coordinator says so (BattleFinished).
// The starting card and Ready go through the coordinator (MatchController.ChooseDraft and Ready ask it), never straight to the
// Match. No AI plays in a LAN match. Battle speed stays each player's own.
// Every round writes one line to the log, and the end one more (LanMatchLog), so two games' logs can be compared.
// Leaving the scene, by any way, tells the other game (Leave). A final state (Desync, OpponentLeft, ConnectionLost) shows its
// screen (MatchHud) with a way back to the menu. So does a start that failed with an exception (FailStart: the other game is told,
// and the log says "[LAN] ended StartFailed ...").
// A -lanDemo run (LanLaunch.Demo): the Insane AI plays this side through the same steps as a player (the starting card through
// the coordinator, its card and deployment on the Match for its own team, then Ready), battles run at the fastest speed a player
// can pick, and the game quits a few seconds after the match ends.
// Depends on: MatchController, BattleController, GameFlow (LanLaunch), LanGameIdentity, Halobellum.Net (LanMatchCoordinator,
//             LanMatchStart, LanGuestStart, LanMatchLog), Halobellum.Economy, Halobellum.Combat, Halobellum.Core, UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using UnityEngine;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(MatchController))]
    public sealed class LanMatch : MonoBehaviour
    {
        /// <summary>What Leave tells the other game.</summary>
        public const string LeftReason = "The other player left the match.";

        private MatchController _controller;
        private BattleController _battle;
        private LanGuestStart _guestStart;
        private bool _endLogged;
        private RuleBasedAi _ai;
        private float _demoWaitUntil;
        private float _quitAt = -1f;

        /// <summary>The LAN match of the loaded scene, or null.</summary>
        public static LanMatch Current { get; private set; }

        /// <summary>What the menu handed over (set by MatchController before Start).</summary>
        public LanLaunch Launch { get; internal set; }

        /// <summary>The coordinator, once the match is built (at once on the host; on the guest once the host's MatchStart is read).</summary>
        public LanMatchCoordinator Coordinator { get; private set; }

        /// <summary>Why the match could not start (guest: refused or connection lost; either: setting it up failed); "" otherwise.</summary>
        public string StartError { get; private set; } = "";

        public bool IsHost => Launch == null || Launch.IsHost;

        public bool IsDemo => Launch != null && Launch.Demo;

        /// <summary>True once the match cannot go on: it could not start, or the coordinator is in a final state.</summary>
        public bool Ended => StartError.Length > 0 || (Coordinator != null && Coordinator.IsFinished);

        /// <summary>True while leaving would forfeit the match: it has not ended and is not over.</summary>
        public bool LeavingForfeits => !Ended && (Coordinator == null || Coordinator.State != LanMatchState.MatchOver);

        /// <summary>The other player's name, or "Other player".</summary>
        public string OpponentName => Launch != null && !string.IsNullOrEmpty(Launch.OpponentName) ? Launch.OpponentName : "Other player";

        /// <summary>What the other player is doing, as the top bar says it: picking, deploying, ready.</summary>
        public string OpponentStatus
        {
            get
            {
                if (Coordinator == null) return Ended ? "gone" : "connecting";
                switch (Coordinator.State)
                {
                    case LanMatchState.Draft: return Coordinator.OpponentPickedDraft ? "ready" : "picking";
                    case LanMatchState.Deployment:
                    case LanMatchState.WaitingForOpponent: return Coordinator.OpponentReady ? "ready" : "deploying";
                    case LanMatchState.CheckingSync:
                    case LanMatchState.ReadyToFight: return "ready";
                    case LanMatchState.Battle:
                    case LanMatchState.WaitingForHostResult: return "in battle";
                    case LanMatchState.Result: return "seeing the result";
                    case LanMatchState.MatchOver: return Coordinator.OpponentGone ? "left" : "match over";
                    default: return "gone";
                }
            }
        }

        /// <summary>The top bar's line about the other player.</summary>
        public string OpponentLine => OpponentName + ": " + OpponentStatus;

        /// <summary>True while this game waits for the other one: its starting card, its Ready, the check of both states, or (guest) the host's result.</summary>
        public bool WaitingForOpponent => Coordinator != null && !Coordinator.IsFinished && Coordinator.WaitingForOpponent;

        /// <summary>True while the local pick is in and the other one is not.</summary>
        public bool WaitingAtDraft => Coordinator != null && Coordinator.State == LanMatchState.Draft && Coordinator.LocalPickedDraft;

        /// <summary>Guest: its battle is over and it waits for the host's result.</summary>
        public bool WaitingForHostResult => Coordinator != null && Coordinator.State == LanMatchState.WaitingForHostResult;

        /// <summary>The end screen's title.</summary>
        public string EndTitle
        {
            get
            {
                if (StartError.Length > 0) return "The match could not start";
                switch (Coordinator?.State)
                {
                    case LanMatchState.Desync: return "Out of sync";
                    case LanMatchState.OpponentLeft: return "The other player left";
                    case LanMatchState.ConnectionLost: return "Connection lost";
                    case LanMatchState.Left: return "You left the match";
                    default: return "";
                }
            }
        }

        /// <summary>The end screen's text: why it ended, in plain words.</summary>
        public string EndText
        {
            get
            {
                if (StartError.Length > 0) return StartError;
                if (Coordinator == null) return "";
                string error = Coordinator.LastError ?? "";
                switch (Coordinator.State)
                {
                    case LanMatchState.Desync: return "The two games no longer agree, so the match stopped. " + error;
                    case LanMatchState.OpponentLeft: return error.Length > 0 ? error : LeftReason;
                    case LanMatchState.ConnectionLost: return "The connection to the other game was lost. " + error;
                    default: return error;
                }
            }
        }

        /// <summary>Desync: both games' last state hashes, for the logs; "" otherwise.</summary>
        public string EndHashes
        {
            get
            {
                if (Coordinator == null || Coordinator.State != LanMatchState.Desync) return "";
                return "State hash in this game: " + Show(Coordinator.LocalHash) + ". In the other game: " + Show(Coordinator.RemoteHash) + ".";
            }
        }

        private static string Show(string hash) => string.IsNullOrEmpty(hash) ? "none yet" : hash;

        private void Awake() => Current = this;

        private void Start()
        {
            _controller = GetComponent<MatchController>();
            _battle = GetComponent<BattleController>();
            _controller.Lan = this;
            Application.runInBackground = true; // Two windows on one PC: the one without focus keeps playing.
            if (Launch == null || Launch.Channel == null)
            {
                StartError = "There is no connection to the other game.";
                return;
            }
            try
            {
                List<FactionInfo> factions = _controller.PrepareFactions(); // The scene's own unit and card objects, which the views know.
                if (Launch.IsHost)
                {
                    LanMatchSettings settings = Launch.Settings ?? LanMatchSettings.From(Environment.TickCount & 0x7FFFFFFF, null);
                    Coordinator = LanMatchStart.BeginAsHost(Launch.Channel, _controller.FreshRules(), _controller.CombatRules, factions, settings, LanGameIdentity.Log);
                    Begin();
                }
                else
                {
                    _guestStart = new LanGuestStart(Launch.Channel, _controller.FreshRules(), _controller.CombatRules, factions, LanGameIdentity.Log);
                    PollGuestStart();
                }
            }
            catch (Exception e)
            {
                FailStart(e);
            }
        }

        /// <summary>
        /// Setting up the match failed with an exception (building it, or showing it): the other game is told (Leave), the connection
        /// closes, the log says "[LAN] ended StartFailed ..." and the end screen shows why, instead of waiting for ever.
        /// </summary>
        public void FailStart(Exception e)
        {
            Debug.LogException(e);
            StartError = "The match could not be set up in this game. " + e.Message;
            const string told = "The other game could not set up the match.";
            LanMatchCoordinator coordinator = Coordinator;
            if (coordinator != null)
            {
                coordinator.BattleFinished -= OnRoundFinished;
                coordinator.Leave(told);
            }
            else if (Launch?.Channel != null && Launch.Channel.IsOpen)
            {
                Launch.Channel.Send(new NetMessage(LanMessageTypes.Leave).Set(LanFields.Reason, told));
            }
            Coordinator = null; // A half-built match is not driven any further.
            _guestStart = null;
            Launch?.Channel?.Close();
            if (!_endLogged)
            {
                _endLogged = true;
                Debug.Log(LanMatchLog.Ended("StartFailed", StartError));
            }
        }

        private void PollGuestStart()
        {
            _guestStart.Poll();
            switch (_guestStart.State)
            {
                case LanGuestStartState.Started:
                    Coordinator = _guestStart.Coordinator;
                    Begin();
                    break;
                case LanGuestStartState.Refused:
                case LanGuestStartState.Lost:
                    StartError = _guestStart.Reason;
                    Debug.Log(LanMatchLog.Ended("Start" + _guestStart.State, _guestStart.Reason));
                    _endLogged = true;
                    break;
            }
        }

        private void Begin()
        {
            Coordinator.BattleFinished += OnRoundFinished;
            _controller.UseMatch(Coordinator.Match, Coordinator.LocalTeam);
            if (IsDemo) _ai = new RuleBasedAi(AiDifficulty.Insane, Coordinator.Match.Seed * 31 + (Coordinator.IsHost ? 1 : 2), _controller.Matchups);
        }

        private void Update()
        {
            if (Coordinator == null && _guestStart != null && StartError.Length == 0)
            {
                try
                {
                    PollGuestStart();
                }
                catch (Exception e)
                {
                    FailStart(e);
                }
            }
            if (Coordinator != null)
            {
                Match match = Coordinator.Match;
                bool wasDraft = match.Phase == MatchPhase.Draft;
                Coordinator.Update();
                if (wasDraft && match.Phase != MatchPhase.Draft) _controller.OnRemoteCommandsApplied(); // The other side's starting card ended the draft.
                if (Coordinator.CanStartBattle && match.Phase == MatchPhase.Deployment)
                {
                    BattleSimulation battle = Coordinator.StartBattle(); // Null (and a desync) if this game acted after Ready.
                    if (battle != null)
                    {
                        _controller.ShowStartedBattle(battle);
                        if (IsDemo) _battle.Speed = MatchController.FastestPlayerSpeed;
                    }
                }
                if (Coordinator.IsFinished && !_endLogged) LogEnd();
            }
            if (IsDemo) DemoStep();
        }

        /// <summary>MatchController: the local battle is over (its round ending has played). The host finishes now; the guest when the host's result is here.</summary>
        public void FinishLocalBattle()
        {
            if (Coordinator == null || Coordinator.IsFinished) return;
            Match match = Coordinator.Match;
            if (match.Phase != MatchPhase.Battle || match.CurrentBattle == null || !match.CurrentBattle.IsFinished) return;
            Coordinator.FinishBattle();
        }

        private void OnRoundFinished(RoundSummary summary)
        {
            Match match = Coordinator.Match;
            Debug.Log(LanMatchLog.Round(summary.Round, Coordinator.LocalHash, Coordinator.LastOutcomeHash, match.Player.BaseHealth, match.Enemy.BaseHealth));
            if (match.Phase == MatchPhase.MatchOver) Debug.Log(LanMatchLog.MatchOver(match.Winner));
            _demoWaitUntil = Time.unscaledTime + 1.5f;
            _controller.OnLanRoundFinished(summary);
        }

        /// <summary>Tells the other game this player leaves (a forfeit during the match) and closes the connection. Safe to call more than once.</summary>
        public void Leave(string reason = LeftReason)
        {
            Coordinator?.Leave(reason);
            Launch?.Channel?.Close();
            if (Coordinator != null && Coordinator.IsFinished && !_endLogged) LogEnd();
        }

        private void LogEnd()
        {
            _endLogged = true;
            string reason = Coordinator.LastError;
            if (string.IsNullOrEmpty(reason) && Coordinator.State == LanMatchState.Left) reason = "This player left the match.";
            Debug.Log(LanMatchLog.Ended(Coordinator.State, reason));
        }

        /// <summary>Leaves the match (the other game is told) and goes back to the main menu.</summary>
        public void LeaveToMenu()
        {
            Leave();
            GameFlow.OpenMainMenu();
        }

        private void OnDestroy()
        {
            if (Coordinator != null) Coordinator.BattleFinished -= OnRoundFinished;
            Leave();
            if (Current == this) Current = null;
        }

        private void OnApplicationQuit() => Leave();

        // ------------------------------------------------------------------ -lanDemo

        private void DemoStep()
        {
            bool over = Ended || (Coordinator != null && Coordinator.State == LanMatchState.MatchOver);
            if (over)
            {
                if (_quitAt < 0f) _quitAt = Time.unscaledTime + 3f;
                else if (Time.unscaledTime >= _quitAt)
                {
                    _quitAt = float.MaxValue;
                    Leave();
                    GameFlow.QuitGame();
                }
                return;
            }
            if (Coordinator == null || _ai == null || Time.unscaledTime < _demoWaitUntil) return;
            Match match = Coordinator.Match;
            Team team = Coordinator.LocalTeam;
            switch (Coordinator.State)
            {
                case LanMatchState.Draft:
                    if (Coordinator.LocalPickedDraft) return;
                    _controller.ChooseDraft(_ai.ChooseDraftCard(new AiView(match, team), _controller.MyDraft));
                    _demoWaitUntil = Time.unscaledTime + 0.5f;
                    break;
                case LanMatchState.Deployment:
                    PlaySide(match, team, _ai);
                    _controller.RefreshView();
                    _controller.Ready();
                    _demoWaitUntil = Time.unscaledTime + 0.5f;
                    break;
                case LanMatchState.Battle:
                    if (_battle.Speed < MatchController.FastestPlayerSpeed) _battle.Speed = MatchController.FastestPlayerSpeed;
                    break;
                case LanMatchState.Result:
                    _controller.Continue();
                    _demoWaitUntil = Time.unscaledTime + 0.5f;
                    break;
            }
        }

        /// <summary>The AI's card and deployment for its own side, on the Match (which records them for the Ready), as a player would take them.</summary>
        public static void PlaySide(Match match, Team team, RuleBasedAi ai)
        {
            var view = new AiView(match, team);
            if (match.InCards(team))
            {
                int choice = ai.ChooseCard(view, match.Get(team).Offer);
                if (choice < 0 || !match.ChooseCard(team, choice).Ok) match.SkipCards(team);
            }
            ai.Deploy(view);
        }
    }
}
