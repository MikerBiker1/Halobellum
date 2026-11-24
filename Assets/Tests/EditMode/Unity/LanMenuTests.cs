// LanMenuTests.cs
// M11 Stage 4 without a scene: the main menu's LAN Game page and GameFlow's LAN hand-over. The page opens from the main page, hosts on
// the loopback address (a port the system picks, no firewall prompt), sees a guest join (an engine-free LanSession with this game's
// own handshake), and Start leaves for the match with the host's team, the guest's name and the host's settings (the LAN match's own
// base health and cards; the Play page's are not touched); the host panel hosts only on Host, and Cancel keeps it so the name can
// change; Esc and Back leave the page and stop hosting; the join panel lists games, and Close after a failed join closes it. GameFlow keeps one LAN launch for the Match scene, which takes it once; a
// -lanDemo run is a test run (the saved settings are not touched) and -windowSlot sets the window from the command line.
// Depends on: Halobellum.UI (MainMenuHud), Halobellum.Presentation (GameFlow, LanLaunch, LanGameIdentity, GameSettings),
//             Halobellum.Net (LanLobby, LanSession, LanMemoryChannel), Halobellum.Economy, Halobellum.Core, NUnit, UnityEngine.

using System.Diagnostics;
using System.Net;
using System.Threading;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using Halobellum.Presentation;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class LanMenuTests
    {
        private GameObject _host;
        private MainMenuHud _menu;
        private LanSession _guest;

        [SetUp]
        public void SetUp()
        {
            GameSettings.HoldInMemory(startEmpty: true);
            MainMenuHud.LobbyOptions = new LanLobbyOptions { GamePort = 0, DiscoveryPort = 0, BindAddress = IPAddress.Loopback, Broadcast = false };
            _host = new GameObject("LAN menu test");
            _menu = _host.AddComponent<MainMenuHud>();
        }

        [TearDown]
        public void TearDown()
        {
            _guest?.Close();
            _menu.LeavingForLan?.Channel?.Close();
            _menu.Lobby?.Close();
            Object.DestroyImmediate(_host);
            MainMenuHud.LobbyOptions = null;
            GameFlow.TakeLanMatch()?.Channel?.Close();
            GameSettings.ReleaseHold();
        }

        private static bool Until(System.Func<bool> condition, int timeoutMs = 5000)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                Thread.Sleep(5);
            }
            return condition();
        }

        [Test]
        public void TheLanPage_Hosts_SeesTheGuest_AndStartLeavesForTheMatchAsTeamA()
        {
            Assert.AreEqual(MainMenuHud.Page.Main, _menu.CurrentPage);
            _menu.OpenLanPage();
            Assert.AreEqual(MainMenuHud.Page.Lan, _menu.CurrentPage);
            Assert.AreEqual(MainMenuHud.LanPanel.None, _menu.CurrentLanPanel);
            Assert.AreEqual(LanLobbyState.Idle, _menu.Lobby.State);

            _menu.OpenHostPanel();
            Assert.AreEqual(MainMenuHud.LanPanel.Host, _menu.CurrentLanPanel);
            Assert.AreEqual(LanLobbyState.Idle, _menu.Lobby.State, "Nothing is hosted before Host is pressed.");
            Assert.IsFalse(_menu.Lobby.Busy, "So the name can still be changed.");
            Assert.AreEqual(MainMenuHud.PressHostText, _menu.LanStatusText);

            _menu.LanName = "  Hosty  ";
            _menu.HostLanGame();
            Assert.AreEqual(MainMenuHud.LanPanel.Host, _menu.CurrentLanPanel);
            Assert.AreEqual(LanLobbyState.HostWaiting, _menu.Lobby.State, _menu.Lobby.Status);
            Assert.AreEqual("Waiting for a player...", _menu.Lobby.Status);
            Assert.AreEqual("Hosty", _menu.LanName, "The name is cleaned.");
            Assert.AreEqual("Hosty", GameSettings.LanPlayerName, "And remembered.");
            Assert.AreEqual("Hosty", _menu.Lobby.Mine.Name);
            Assert.AreEqual(Application.version, _menu.Lobby.Mine.Version);

            _menu.StartLanGame();
            Assert.IsFalse(_menu.IsLeaving, "Start does nothing before a player joined.");

            _guest = LanSession.Join("127.0.0.1", _menu.Lobby.HostPort, LanGameIdentity.Hello("Guesty"));
            Assert.IsTrue(Until(() =>
            {
                _menu.UpdateLan();
                _guest.Poll();
                return _menu.Lobby.CanStart && _guest.State == LanSessionState.Connected;
            }), _menu.Lobby.Status);
            Assert.AreEqual("Guesty joined. Press Start to begin.", _menu.Lobby.Status);

            _menu.LanMode = MatchMode.UnlockSkirmish;
            _menu.LanCards = CardFilter.NoReinforcements;
            _menu.LanBaseHealth = 1500;
            _menu.StartLanGame();
            Assert.IsTrue(_menu.IsLeaving, "Start fades out to the match.");
            LanLaunch launch = _menu.LeavingForLan;
            Assert.IsNotNull(launch);
            Assert.AreEqual(Team.A, launch.LocalTeam);
            Assert.IsTrue(launch.IsHost);
            Assert.AreEqual("Guesty", launch.OpponentName);
            Assert.IsFalse(launch.Demo);
            Assert.IsNotNull(launch.Channel);
            Assert.IsTrue(launch.Channel.IsOpen, "The connection goes with it.");
            Assert.AreEqual(MatchMode.UnlockSkirmish, launch.Settings.Mode);
            Assert.AreEqual(1500, launch.Settings.BaseHealth);
            Assert.AreEqual(CardFilter.NoReinforcements, launch.Settings.Cards);
            Assert.AreEqual(LanLobbyState.Started, _menu.Lobby.State);
            Assert.AreEqual(GameSettings.DefaultSkirmishBaseHealth, GameSettings.SkirmishBaseHealth, "The Play page's saved base health is not touched.");
            Assert.AreEqual(CardFilter.AllCards, _menu.Cards, "Nor its cards.");
        }

        [Test]
        public void CancelOnTheHostPanel_StopsHosting_AndKeepsThePanel_SoTheNameCanChange()
        {
            _menu.OpenLanPage();
            _menu.OpenHostPanel();
            _menu.LanName = "First";
            _menu.HostLanGame();
            Assert.IsTrue(_menu.Lobby.IsHosting);
            Assert.IsTrue(_menu.Lobby.Busy, "The name is fixed while hosting.");
            Assert.AreEqual(LanLobby.WaitingText, _menu.LanStatusText);

            _menu.CancelLan();
            Assert.AreEqual(LanLobbyState.Idle, _menu.Lobby.State, "Hosting stopped.");
            Assert.AreEqual(MainMenuHud.LanPanel.Host, _menu.CurrentLanPanel, "The panel stays, with Host again.");
            Assert.IsFalse(_menu.Lobby.Busy);
            _menu.LanName = "Second";
            _menu.HostLanGame();
            Assert.AreEqual("Second", _menu.Lobby.Mine.Name);
            Assert.IsTrue(_menu.Lobby.IsHosting);

            _menu.OpenJoinPanel();
            Assert.IsFalse(_menu.Lobby.IsHosting, "The join panel stops hosting.");
            Assert.AreEqual(LanLobbyState.Browsing, _menu.Lobby.State);
        }

        [Test]
        public void TheHostsSettings_FollowTheModeChosen()
        {
            _menu.OpenLanPage();
            _menu.LanMode = MatchMode.Faction;
            LanMatchSettings faction = _menu.LanSettingsForHost();
            Assert.AreEqual(MatchMode.Faction, faction.Mode);
            Assert.AreEqual(0, faction.BaseHealth, "Faction mode plays the rules as they are, as against the AI.");
            Assert.GreaterOrEqual(faction.Seed, 0);
        }

        [Test]
        public void EscAndBack_LeaveTheLanPage_AndStopHosting()
        {
            _menu.OpenLanPage();
            _menu.HostLanGame();
            Assert.IsTrue(_menu.Lobby.IsHosting);
            Assert.IsTrue(_menu.HandleEscape());
            Assert.AreEqual(MainMenuHud.Page.Main, _menu.CurrentPage);
            Assert.AreEqual(LanLobbyState.Idle, _menu.Lobby.State, "Hosting stops with the page.");
            Assert.IsFalse(_menu.HandleEscape(), "Nothing further back.");

            _menu.OpenLanPage();
            _menu.OpenJoinPanel();
            Assert.AreEqual(MainMenuHud.LanPanel.Join, _menu.CurrentLanPanel);
            Assert.AreEqual(LanLobbyState.Browsing, _menu.Lobby.State);
            Assert.Greater(_menu.Lobby.BrowsePort, 0, "The join panel listens for games.");
            _menu.CancelLan();
            Assert.AreEqual(MainMenuHud.LanPanel.None, _menu.CurrentLanPanel);
            _menu.Back();
            Assert.AreEqual(MainMenuHud.Page.Main, _menu.CurrentPage);
        }

        [Test]
        public void JoiningWhereNoGameHosts_ShowsTheFirewallHint()
        {
            var free = new LanHost(0, IPAddress.Loopback);
            Assert.IsTrue(free.TryStart(out string error), error);
            int port = free.Port;
            free.Stop();
            _menu.OpenLanPage();
            _menu.JoinLanGame("127.0.0.1", port);
            Assert.AreEqual("127.0.0.1", GameSettings.LanAddress, "The address is remembered.");
            Assert.IsTrue(Until(() =>
            {
                _menu.UpdateLan();
                return _menu.Lobby.State == LanLobbyState.JoinFailed;
            }), _menu.Lobby.Status);
            StringAssert.Contains("Windows Firewall", _menu.Lobby.Status);
            Assert.AreEqual(MainMenuHud.LanPanel.Join, _menu.CurrentLanPanel);

            // The button says Close after a failed join, and one press closes the panel.
            _menu.CancelLan();
            Assert.AreEqual(MainMenuHud.LanPanel.None, _menu.CurrentLanPanel);
            Assert.AreEqual(LanLobbyState.Idle, _menu.Lobby.State);
        }

        [Test]
        public void GameFlow_KeepsOneLanLaunch_ForTheMatchScene()
        {
            Assert.IsNull(GameFlow.TakeLanMatch());
            Assert.Throws<System.ArgumentException>(() => GameFlow.SetLanLaunch(null));
            Assert.Throws<System.ArgumentException>(() => GameFlow.SetLanLaunch(new LanLaunch()));

            LanMemoryChannel.CreatePair(out LanMemoryChannel first, out LanMemoryChannel firstOther);
            LanMemoryChannel.CreatePair(out LanMemoryChannel second, out LanMemoryChannel secondOther);
            var settings = new LanMatchSettings { Seed = 9, Mode = MatchMode.Faction };
            GameFlow.SetLanLaunch(new LanLaunch { Channel = first, LocalTeam = Team.A, Settings = settings });
            var guest = new LanLaunch { Channel = second, LocalTeam = Team.B, OpponentName = "Hosty" };
            GameFlow.SetLanLaunch(guest);
            Assert.IsFalse(first.IsOpen, "A launch nobody took is closed when another replaces it.");
            Assert.IsNull(GameFlow.TakeMatchSetup(), "A LAN launch is not a skirmish.");
            Assert.IsFalse(GameFlow.TakeTutorial());

            LanLaunch taken = GameFlow.TakeLanMatch();
            Assert.AreSame(guest, taken);
            Assert.IsFalse(taken.IsHost);
            Assert.IsTrue(second.IsOpen, "Taken, it stays open for the match.");
            Assert.IsNull(GameFlow.TakeLanMatch(), "Only once.");
            second.Close();
            firstOther.Close();
            secondOther.Close();
        }

        [Test]
        public void ALanDemo_IsATestRun_AndAWindowSlotSetsTheWindow()
        {
            string[] demo = { "Halobellum.exe", "-lanDemo", "host", "-windowSlot", "0" };
            Assert.IsTrue(GameFlow.IsTestRun(demo), "The saved settings stay untouched, so two copies don't overwrite each other's.");
            Assert.IsNull(GameFlow.DirectSceneFor(demo), "The menu still shows: it hosts or joins.");
            Assert.IsTrue(GameFlow.CommandLineSetsWindow(new[] { "-windowSlot", "1" }));
            Assert.IsFalse(GameFlow.IsTestRun(new[] { "-windowSlot", "1" }), "A window slot alone is an ordinary run.");
            Assert.AreEqual(GameFlow.MainMenuScene, GameFlow.MenuSceneFor(true, demo), "A test run always has the cinematic menu.");
        }

        [Test]
        public void ThisGamesHello_HasItsVersion_TheCode_AndNoChangedStats()
        {
            LanHello hello = LanGameIdentity.Hello("Mike");
            Assert.AreEqual("Mike", hello.Name);
            Assert.AreEqual(Application.version, hello.Version);
            Assert.AreEqual("modded=no", hello.Content, "The editor plays the assets.");
            Assert.AreEqual(LanIdentity.CodeFingerprint(), hello.Code);
            Assert.IsNull(LanHello.Mismatch(hello, LanGameIdentity.Hello("Other")));
            Assert.AreEqual("Leave match", PauseMenu.LeaveMatchLabel);
            Assert.AreEqual("Leaving forfeits the match.", PauseMenu.ForfeitWarning);
            Assert.AreEqual("Waiting for opponent...", MatchHud.WaitingForOpponentText);
        }
    }
}
