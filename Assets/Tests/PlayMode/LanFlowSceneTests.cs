// LanFlowSceneTests.cs
// M11 Stage 4 in the real Match scene, over loopback TCP (127.0.0.1, ports the system picks, so no firewall prompt):
//   The host: its lobby takes a guest (an engine-free LanSession in this process, with this game's own handshake), GameFlow starts the
//   LAN match, and the Match scene builds a two-humans match as Team A. The in-process guest builds its own match from the host's
//   MatchStart (LanGuestStart, with its own copy of the content): the same seed. Both pick their starting card (the host's through the
//   controller, which goes through the coordinator and waits for the guest's), both press Ready (the host is then locked out of acting),
//   and once both games agree on the state the battle starts in the scene with the agreed hash. The pause menu does not stop time in a
//   LAN match, and leaving forfeits it until it ended. The guest leaving gives the host's end screen.
//   A start that fails with an exception ends the host's match with its end screen, and the other game is told.
//   The guest: an in-process host (lobby, then LanMatchStart.BeginAsHost) starts, the guest's lobby sees the MatchStart and GameFlow
//   starts the LAN match as Team B: the scene reads the host's MatchStart put back for it, builds the same match, and shows it turned.
// Depends on: Halobellum.Presentation (GameFlow, LanLaunch, LanMatch, MatchController, BattleController, BattleCameraController, GamePause,
//             LanGameIdentity), Halobellum.UI (PauseMenu), Halobellum.Net, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using Halobellum.Presentation;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Halobellum.Tests
{
    public class LanFlowSceneTests
    {
        private LanLobby _hostLobby, _guestLobby;
        private LanSession _guestSession;
        private LanMatchCoordinator _remote;

        private static LanLobbyOptions Loopback() => new LanLobbyOptions { GamePort = 0, DiscoveryPort = 0, BindAddress = IPAddress.Loopback, Broadcast = false };

        [TearDown]
        public void TearDown()
        {
            _remote?.Leave();
            _guestSession?.Close();
            _hostLobby?.Close();
            _guestLobby?.Close();
            if (LanMatch.Current != null) LanMatch.Current.Leave();
            GameFlow.TakeLanMatch()?.Channel?.Close();
            GamePause.Resume();
        }

        /// <summary>Waits frame by frame until the condition holds (doing a step each frame first); fails after the time limit.</summary>
        private static IEnumerator Until(Func<bool> condition, string what, Action step = null, float seconds = 20f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (true)
            {
                step?.Invoke();
                if (condition()) yield break;
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out: " + what);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheHostsMatch_AndAnInProcessGuest_ReadyARound_AndTheBattleStarts()
        {
            _hostLobby = new LanLobby(Loopback());
            _hostLobby.Host(LanGameIdentity.Hello("Hosty"));
            Assert.AreEqual(LanLobbyState.HostWaiting, _hostLobby.State, _hostLobby.Status);
            _guestSession = LanSession.Join("127.0.0.1", _hostLobby.HostPort, LanGameIdentity.Hello("Guesty"));
            yield return Until(() => _hostLobby.CanStart && _guestSession.State == LanSessionState.Connected, "the guest joins the host's lobby", () =>
            {
                _hostLobby.Update();
                _guestSession.Poll();
            });

            var settings = new LanMatchSettings { Seed = 4242, Mode = MatchMode.Faction, BaseHealth = 0, Cards = CardFilter.AllCards };
            GameFlow.StartLanMatch(new LanLaunch { Channel = _hostLobby.TakeChannel(), LocalTeam = Team.A, Settings = settings, OpponentName = "Guesty" });
            yield return Until(() => LanMatch.Current != null && LanMatch.Current.Coordinator != null, "the Match scene builds the host's match");
            LanMatch lan = LanMatch.Current;
            MatchController host = Object.FindAnyObjectByType<MatchController>();
            BattleController battle = Object.FindAnyObjectByType<BattleController>();
            Assert.AreSame(lan, host.Lan);
            Assert.IsTrue(host.Match.IsTwoHumans, "A two-humans match: no AI.");
            Assert.AreSame(lan.Coordinator.Match, host.Match);
            Assert.AreEqual(Team.A, host.LocalTeam);
            Assert.AreEqual(MatchPhase.Draft, host.Match.Phase);
            Assert.AreEqual(4242, host.Match.Seed);
            Assert.AreEqual("Guesty: picking", lan.OpponentLine);

            // The guest, in this process: its own copy of the scene's content, as the other PC would have.
            var start = new LanGuestStart(_guestSession, host.FreshRules(), host.CombatRules, host.BuildFactions());
            yield return Until(() => start.State != LanGuestStartState.Waiting, "the guest reads the host's MatchStart", start.Poll);
            Assert.AreEqual(LanGuestStartState.Started, start.State, start.Reason);
            _remote = start.Coordinator;
            Assert.AreEqual(host.Match.Seed, start.Match.Seed, "The guest's match has the host's seed.");
            Assert.AreEqual(Team.B, _remote.LocalTeam);

            // The starting cards: the host's through the controller (and its coordinator), which waits for the guest's.
            Assert.IsTrue(host.ChooseDraft(0).Ok, host.Message);
            Assert.AreEqual(MatchPhase.Draft, host.Match.Phase, "The round waits for the other player's card.");
            Assert.IsTrue(lan.WaitingAtDraft);
            Assert.IsTrue(lan.WaitingForOpponent);
            Assert.IsFalse(host.ChooseDraft(1).Ok, "One starting card.");
            yield return Until(() => _remote.OpponentPickedDraft, "the host's card reaches the guest", _remote.Update);
            Assert.IsTrue(_remote.ChooseDraft(0).Ok);
            yield return Until(() => host.Match.Phase == MatchPhase.Deployment, "the guest's card ends the host's draft", _remote.Update);
            Assert.AreEqual(1, host.Match.Round);
            Assert.IsTrue(host.IsDeploying);
            Assert.IsFalse(host.ActionsLocked);
            Assert.AreEqual("Guesty: deploying", lan.OpponentLine);

            // The host buys a squad, then presses Ready: its decisions go to the guest, and it can do nothing more this round.
            UnitStats type = host.Match.Player.Unlocked.Where(u => u.Cost <= host.Match.Player.Credits).OrderBy(u => u.Cost).FirstOrDefault();
            if (type != null) Assert.IsTrue(host.Buy(type).Ok, host.Message);
            host.Ready();
            Assert.AreEqual(MatchPhase.Deployment, host.Match.Phase, "No battle before the other player is ready.");
            Assert.IsTrue(lan.Coordinator.LocalReady);
            Assert.IsTrue(host.ActionsLocked);
            Assert.IsFalse(host.IsDeploying, "Nothing on the field takes a click after Ready.");
            if (type != null) Assert.IsFalse(host.Buy(type).Ok, "No action after Ready.");
            Assert.IsTrue(lan.WaitingForOpponent);

            // The guest plays its side on its own match and presses Ready through its coordinator.
            Assert.IsTrue(_remote.Ready().Ok);
            yield return Until(() => host.Match.Phase == MatchPhase.Battle && battle.Simulation == host.Match.CurrentBattle, "the battle starts in the scene", () =>
            {
                _remote.Update();
                if (_remote.CanStartBattle && _remote.Match.Phase == MatchPhase.Deployment) Assert.IsNotNull(_remote.StartBattle());
            });
            Assert.AreEqual(lan.Coordinator.LocalHash, lan.Coordinator.RemoteHash, "Both games agreed on the state before the battle.");
            Assert.AreEqual(_remote.LocalHash, lan.Coordinator.LocalHash);
            Assert.AreEqual(MatchPhase.Battle, _remote.Match.Phase);
            Assert.IsFalse(lan.Ended);

            // The battle bar's Pause does not stop a LAN match either: the host's result must reach the other game.
            Assert.IsFalse(battle.PauseAllowed);
            float speed = battle.Speed;
            battle.Speed = 0f;
            Assert.AreEqual(speed, battle.Speed, "A pause is ignored in a LAN match.");
            Assert.IsFalse(battle.Clock.IsPaused);
            Assert.AreEqual("Guesty: in battle", lan.OpponentLine);

            // The pause menu does not stop a LAN match.
            PauseMenu pause = PauseMenu.Current;
            Assert.IsNotNull(pause);
            pause.Open();
            Assert.IsTrue(pause.IsOpen);
            Assert.IsFalse(GamePause.IsPaused, "Time goes on: the other player's game does.");
            Assert.AreEqual(1f, Time.timeScale);
            pause.Close();
            Assert.IsFalse(pause.IsOpen);
            Assert.IsTrue(PauseMenu.LeavingForfeits, "Leaving now forfeits the match.");
            Assert.AreEqual(PauseMenu.LeaveMatchLabel, PauseMenu.ThirdButtonLabel);

            // The round ends: the guest's own battle is over first and waits for the host's result; the host's scene plays its
            // battle and round ending, finishes through its coordinator and sends the result; both show the same result.
            battle.Speed = 16f;
            _remote.Match.CurrentBattle.Run();
            Assert.IsFalse(_remote.FinishBattle(), "The guest waits for the host's result.");
            Assert.AreEqual(LanMatchState.WaitingForHostResult, _remote.State);
            yield return Until(() => host.Match.Phase == MatchPhase.Result && _remote.State == LanMatchState.Result, "the round's result on both games", _remote.Update, 180f);
            Assert.AreEqual(MatchStateHash.Compute(host.Match), MatchStateHash.Compute(_remote.Match), "The same state after the host's result.");
            Assert.AreEqual(lan.Coordinator.LastOutcomeHash, _remote.LastOutcomeHash);
            Assert.AreNotEqual(0UL, lan.Coordinator.LastOutcomeHash);
            Assert.IsFalse(_remote.HostResultUsed, "Both played the same battle.");
            Assert.IsFalse(lan.HostResultUsedThisRound);
            Assert.AreEqual(lan.Coordinator.LastOutcomeHash, lan.Coordinator.LastOwnOutcomeHash, "The host's own result is the one used.");
            Assert.AreEqual("Guesty: battle over", lan.OpponentLine, "The guest's Status said its battle is over.");
            host.Continue();
            _remote.ContinueAfterResult();
            Assert.AreEqual(2, host.Match.Round);
            Assert.AreEqual(MatchPhase.Deployment, host.Match.Phase);
            Assert.AreEqual(LanMatchState.Deployment, lan.Coordinator.State, "Round 2: the host may act again.");
            Assert.IsFalse(host.ActionsLocked);
            yield return Until(() => lan.OpponentLine == "Guesty: deploying", "the guest's Continue reaches the host");

            // The guest leaves: the host's end screen says so.
            _remote.Leave("The other player left the match.");
            yield return Until(() => lan.Ended, "the host sees the guest leave");
            Assert.AreEqual(LanMatchState.OpponentLeft, lan.Coordinator.State);
            Assert.AreEqual("The other player left", lan.EndTitle);
            StringAssert.Contains("left", lan.EndText);
            Assert.IsFalse(PauseMenu.LeavingForfeits, "Nothing left to forfeit.");
            Assert.AreEqual("Quit to Main Menu", PauseMenu.ThirdButtonLabel);
        }

        [UnityTest]
        public IEnumerator AStartThatFails_EndsTheMatchOnBothGames_InsteadOfWaitingForEver()
        {
            _hostLobby = new LanLobby(Loopback());
            _hostLobby.Host(LanGameIdentity.Hello("Hosty"));
            _guestSession = LanSession.Join("127.0.0.1", _hostLobby.HostPort, LanGameIdentity.Hello("Guesty"));
            yield return Until(() => _hostLobby.CanStart && _guestSession.State == LanSessionState.Connected, "the guest joins the host's lobby", () =>
            {
                _hostLobby.Update();
                _guestSession.Poll();
            });
            var settings = new LanMatchSettings { Seed = 99, Mode = MatchMode.Faction, BaseHealth = 0, Cards = CardFilter.AllCards };
            GameFlow.StartLanMatch(new LanLaunch { Channel = _hostLobby.TakeChannel(), LocalTeam = Team.A, Settings = settings, OpponentName = "Guesty" });
            yield return Until(() => LanMatch.Current != null && LanMatch.Current.Coordinator != null, "the Match scene builds the host's match");
            LanMatch lan = LanMatch.Current;
            MatchController host = Object.FindAnyObjectByType<MatchController>();

            // Setting up fails with an exception (as building or showing the match might).
            LogAssert.Expect(LogType.Exception, new Regex("A test failure"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[LAN\] ended StartFailed The match could not be set up in this game\. A test failure\.$"));
            lan.FailStart(new InvalidOperationException("A test failure."));
            Assert.IsTrue(lan.Ended);
            Assert.IsNull(lan.Coordinator, "The half-built match is not driven any further.");
            Assert.AreEqual("The match could not start", lan.EndTitle);
            StringAssert.Contains("A test failure.", lan.EndText);
            Assert.IsFalse(PauseMenu.LeavingForfeits);
            yield return null;
            Assert.IsTrue(lan.Ended, "It stays ended.");

            // The other game is told and stops too: here it got the MatchStart first, then the Leave.
            var start = new LanGuestStart(_guestSession, host.FreshRules(), host.CombatRules, host.BuildFactions());
            yield return Until(() => start.State != LanGuestStartState.Waiting, "the guest reads the host's messages", start.Poll);
            if (start.State == LanGuestStartState.Started)
            {
                _remote = start.Coordinator;
                yield return Until(() => _remote.IsFinished, "the guest sees the host leave", _remote.Update);
                Assert.AreEqual(LanMatchState.OpponentLeft, _remote.State);
                StringAssert.Contains("could not set up the match", _remote.LastError);
            }
            else Assert.AreEqual(LanGuestStartState.Lost, start.State, start.Reason);
        }

        [UnityTest]
        public IEnumerator TheGuest_FollowsAnInProcessHost_IntoTheSameMatch_AsTeamB()
        {
            // The scene's content, for the in-process host's match.
            yield return SceneManager.LoadSceneAsync(GameFlow.MatchScene, LoadSceneMode.Single);
            yield return null;
            MatchController first = Object.FindAnyObjectByType<MatchController>();
            Assert.IsNull(first.Lan, "An ordinary load of the scene is single-player.");
            Assert.AreEqual(Team.A, first.LocalTeam);
            MatchRules rules = first.FreshRules();
            CombatRules combat = first.CombatRules;
            var factions = first.BuildFactions();

            _hostLobby = new LanLobby(Loopback());
            _hostLobby.Host(LanGameIdentity.Hello("Hosty"));
            _guestLobby = new LanLobby(Loopback());
            _guestLobby.Join("127.0.0.1", _hostLobby.HostPort, LanGameIdentity.Hello("Guesty"));
            yield return Until(() => _hostLobby.CanStart && _guestLobby.State == LanLobbyState.JoinWaiting, "the guest's lobby joins", () =>
            {
                _hostLobby.Update();
                _guestLobby.Update();
            });

            var settings = new LanMatchSettings { Seed = 777, Mode = MatchMode.Faction, BaseHealth = 0, Cards = CardFilter.AllCards };
            _remote = LanMatchStart.BeginAsHost(_hostLobby.TakeChannel(), rules, combat, factions, settings);
            Assert.IsTrue(_remote.ChooseDraft(0).Ok, "The host picks at once; its card waits in the line for the guest.");
            yield return Until(() => _guestLobby.State == LanLobbyState.GuestStarting, "the guest's lobby sees the host's start", _guestLobby.Update);
            string hostName = _guestLobby.PeerName;
            Assert.AreEqual("Hosty", hostName);
            GameFlow.StartLanMatch(new LanLaunch { Channel = _guestLobby.TakeChannel(), LocalTeam = Team.B, OpponentName = hostName });

            yield return Until(() => LanMatch.Current != null && LanMatch.Current.Coordinator != null, "the guest's scene builds the host's match", _remote.Update);
            LanMatch lan = LanMatch.Current;
            MatchController guest = Object.FindAnyObjectByType<MatchController>();
            Assert.AreEqual(Team.B, guest.LocalTeam);
            Assert.IsFalse(lan.IsHost);
            Assert.AreEqual(777, guest.Match.Seed, "The host's seed.");
            Assert.AreEqual(180f, Object.FindAnyObjectByType<BattleCameraController>().Yaw, 0.01f, "The guest sees the field turned.");

            yield return Until(() => lan.Coordinator.OpponentPickedDraft, "the host's early card reaches the guest's scene", _remote.Update);
            Assert.IsTrue(guest.ChooseDraft(0).Ok, guest.Message);
            Assert.AreEqual(MatchPhase.Deployment, guest.Match.Phase, "Both cards in: round 1.");
            yield return Until(() => _remote.Match.Phase == MatchPhase.Deployment, "the guest's card reaches the host", _remote.Update);
            Assert.AreEqual(MatchStateHash.Compute(_remote.Match), MatchStateHash.Compute(guest.Match), "The same match on both sides.");
            Assert.IsTrue(guest.IsDeploying);
        }
    }
}
