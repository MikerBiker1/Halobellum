// LanLobbyTests.cs
// Tests the LAN Game page's engine-free work (M11 Stage 4) over real loopback sockets with ports the system picks (no firewall
// prompt): a host is found by the join page's list, joined, and starts the match, and the guest builds the same match from the
// host's MatchStart (put back for it by the lobby) without losing the host's first message after it; a guest whose version or
// unit stats differ is refused in plain words and the host keeps waiting; a guest who leaves puts the host back to waiting; the
// host stopping tells the guest; joining where no game hosts says so with the Windows Firewall hint; a port in use is tried again;
// a listener that stops accepting by itself is started again; the host's "join at" line, a typed "address:port", and the join
// list's hint when nothing is heard.
// Also the handshake's content text and name, the demo flags and window slots, the log lines of a LAN match and the buffered channel.
// Depends on: Halobellum.Net (LanLobby, LanIdentity, LanDemoArgs, LanMatchLog, LanBufferedChannel, LanMatchStart, LanGuestStart),
//             Halobellum.Economy, Halobellum.Core, NUnit, NetTestKit, LockstepTestKit, TestRules.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanLobbyTests
    {
        private readonly List<LanLobby> _lobbies = new List<LanLobby>();

        [TearDown]
        public void CloseEverything()
        {
            foreach (LanLobby lobby in _lobbies) lobby.Close();
            _lobbies.Clear();
        }

        private static string Content => LanContent.Describe(LockstepTestKit.Factions(), LockstepTestKit.Rules(), TestRules.Default());

        private static LanHello Hello(string name, string version = "0.416.2", string content = null) => NetTestKit.Hello(name, version, content ?? Content);

        private LanLobby Lobby(int announceTo = 0)
        {
            var options = new LanLobbyOptions { GamePort = 0, DiscoveryPort = 0, BindAddress = IPAddress.Loopback, Broadcast = false, Timings = NetTestKit.Fast() };
            if (announceTo > 0) options.AnnounceTargets.Add(new IPEndPoint(IPAddress.Loopback, announceTo));
            var lobby = new LanLobby(options);
            _lobbies.Add(lobby);
            return lobby;
        }

        private static bool Pump(System.Func<bool> done, params LanLobby[] lobbies) => NetTestKit.Until(() =>
        {
            foreach (LanLobby lobby in lobbies) lobby.Update();
            return done();
        });

        [Test]
        public void AHostIsListedJoinedAndStarted_AndTheGuestBuildsTheSameMatch()
        {
            LanLobby guest = Lobby();
            guest.Browse(Hello("Guesty"));
            Assert.AreEqual(LanLobbyState.Browsing, guest.State);
            Assert.Greater(guest.BrowsePort, 0, "The join page listens for games.");

            LanLobby host = Lobby(guest.BrowsePort);
            host.Host(Hello("Hosty"));
            Assert.AreEqual(LanLobbyState.HostWaiting, host.State, host.Status);
            Assert.AreEqual(LanLobby.WaitingText, host.Status);
            Assert.IsFalse(host.CanStart);

            Assert.IsTrue(Pump(() => guest.Games.Any(g => g.Name == "Hosty"), host, guest), "The join page lists the host.");
            LanGameInfo game = guest.Games.First(g => g.Name == "Hosty");
            Assert.AreEqual(host.HostPort, game.Port);
            Assert.AreEqual("127.0.0.1", game.Address);

            guest.Join(game);
            Assert.AreEqual(LanLobbyState.JoinConnecting, guest.State);
            Assert.IsTrue(Pump(() => host.CanStart && guest.State == LanLobbyState.JoinWaiting, host, guest), $"host {host.State} '{host.Status}', guest {guest.State} '{guest.Status}'");
            Assert.AreEqual("Guesty", host.PeerName);
            StringAssert.Contains("Guesty joined", host.Status);
            StringAssert.Contains("Joined Hosty", guest.Status);
            Assert.IsTrue(guest.Busy && host.Busy);

            // The host starts: its session goes to the match, which sends MatchStart and then the host's starting card.
            ILanChannel hostChannel = host.TakeChannel();
            Assert.IsNotNull(hostChannel);
            Assert.AreEqual(LanLobbyState.Started, host.State);
            Assert.IsNull(host.TakeChannel(), "Handed over once.");
            LanMatchSettings settings = LockstepTestKit.Settings(41);
            LanMatchCoordinator hostCoordinator = LanMatchStart.BeginAsHost(hostChannel, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), settings);
            Assert.IsTrue(hostCoordinator.ChooseDraft(0).Ok);

            Assert.IsTrue(Pump(() => guest.State == LanLobbyState.GuestStarting, guest), "The guest sees the host's start: " + guest.Status);
            ILanChannel guestChannel = guest.TakeChannel();
            Assert.IsInstanceOf<LanBufferedChannel>(guestChannel);
            Assert.AreEqual(LanLobbyState.Started, guest.State);

            var start = new LanGuestStart(guestChannel, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions());
            Assert.IsTrue(NetTestKit.Until(() =>
            {
                start.Poll();
                return start.State != LanGuestStartState.Waiting;
            }));
            Assert.AreEqual(LanGuestStartState.Started, start.State, start.Reason);
            Assert.AreEqual(41, start.Match.Seed);
            Assert.AreEqual(Team.B, start.Coordinator.LocalTeam);
            Assert.AreEqual(settings.ToString(), start.Settings.ToString(), "The host's settings; Started means the content and the fresh state hash agreed.");

            // The host's starting card, sent after MatchStart, was left for the coordinator.
            LanMatchCoordinator guestCoordinator = start.Coordinator;
            Assert.IsTrue(NetTestKit.Until(() =>
            {
                guestCoordinator.Update();
                return guestCoordinator.OpponentPickedDraft;
            }), "The host's pick reaches the guest's coordinator.");
            Assert.IsTrue(guestCoordinator.ChooseDraft(0).Ok);
            Assert.AreEqual(MatchPhase.Deployment, start.Match.Phase);
            Assert.IsTrue(NetTestKit.Until(() =>
            {
                hostCoordinator.Update();
                return hostCoordinator.Match.Phase == MatchPhase.Deployment;
            }));
            Assert.AreEqual(LockstepTestKit.HashOf(hostCoordinator.Match), LockstepTestKit.HashOf(start.Match), "Round 1 is the same on both.");
            hostCoordinator.Leave();
            guestCoordinator.Leave();
        }

        [Test]
        public void AGuestOfAnotherVersion_IsRefusedInPlainWords_AndTheHostKeepsWaiting()
        {
            LanLobby host = Lobby();
            host.Host(Hello("Hosty", "0.416.2"));
            LanLobby old = Lobby();
            old.Join("127.0.0.1", host.HostPort, Hello("Oldy", "0.416.1"));
            Assert.IsTrue(Pump(() => old.State == LanLobbyState.JoinFailed, host, old), old.Status);
            Assert.AreEqual("The host refused: The other game is version 0.416.2; this one is 0.416.1.", old.Status);
            Assert.IsTrue(Pump(() => host.LastRefusal.Length > 0, host));
            Assert.AreEqual(LanLobbyState.HostWaiting, host.State);
            Assert.AreEqual("A player could not join: The other game is version 0.416.1; this one is 0.416.2.\n" + LanLobby.WaitingText, host.Status);

            // A game that matches can still join.
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1", host.HostPort, Hello("Guesty"));
            Assert.IsTrue(Pump(() => host.CanStart && guest.State == LanLobbyState.JoinWaiting, host, guest), guest.Status);
        }

        [Test]
        public void AGuestWithOtherUnitStats_IsToldWhichUnitsDiffer()
        {
            string mine = LanIdentity.Content(new[] { "Marine: cost 100 -> 150" });
            string theirs = LanIdentity.Content(null);
            LanLobby host = Lobby();
            host.Host(Hello("Hosty", content: theirs));
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1", host.HostPort, Hello("Guesty", content: mine));
            Assert.IsTrue(Pump(() => guest.State == LanLobbyState.JoinFailed, host, guest), guest.Status);
            Assert.AreEqual("The host refused: Unit stats differ: modded, Marine. Both games need the same unit stats files.", guest.Status);
        }

        [Test]
        public void AGuestWhoLeaves_PutsTheHostBackToWaiting_AndTheNextOneCanJoin()
        {
            LanLobby host = Lobby();
            host.Host(Hello("Hosty"));
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1", host.HostPort, Hello("Guesty"));
            Assert.IsTrue(Pump(() => host.CanStart && guest.State == LanLobbyState.JoinWaiting, host, guest));

            guest.Cancel();
            Assert.AreEqual(LanLobbyState.Browsing, guest.State, "The guest goes back to its list.");
            Assert.IsTrue(Pump(() => host.State == LanLobbyState.HostWaiting, host), host.Status);
            Assert.AreEqual("Guesty left.\n" + LanLobby.WaitingText, host.Status);
            Assert.Greater(host.HostPort, 0, "Listening again.");

            LanLobby next = Lobby();
            next.Join("127.0.0.1", host.HostPort, Hello("Nexty"));
            Assert.IsTrue(Pump(() => host.CanStart, host, next), host.Status);
            Assert.AreEqual("Nexty", host.PeerName);
        }

        [Test]
        public void TheHostStopping_TellsTheGuest()
        {
            LanLobby host = Lobby();
            host.Host(Hello("Hosty"));
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1", host.HostPort, Hello("Guesty"));
            Assert.IsTrue(Pump(() => host.CanStart && guest.State == LanLobbyState.JoinWaiting, host, guest));

            host.Cancel();
            Assert.AreEqual(LanLobbyState.Idle, host.State);
            Assert.IsTrue(Pump(() => guest.State == LanLobbyState.JoinFailed, guest), guest.Status);
            StringAssert.StartsWith("The host left.", guest.Status);
        }

        [Test]
        public void JoiningWhereNoGameHosts_SaysSo_WithTheFirewallHint()
        {
            LanHost free = NetTestKit.StartHost();
            int port = free.Port;
            free.Stop();
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1", port, Hello("Guesty"));
            Assert.IsTrue(Pump(() => guest.State == LanLobbyState.JoinFailed, guest), guest.Status);
            StringAssert.StartsWith("Could not join.", guest.Status);
            StringAssert.Contains("Windows Firewall", guest.Status);

            guest.Join("  ", 0, null);
            Assert.AreEqual(LanLobbyState.JoinFailed, guest.State);
            Assert.AreEqual("Type the host's address first.", guest.Status);

            Assert.AreEqual("No answer. " + LanLobby.FirewallHint, LanLobby.WithFirewallHint("No answer."));
            string already = "No answer from x within 5 s. Is the game hosting there, and does Windows Firewall on that PC allow Halobellum?";
            Assert.AreEqual(already, LanLobby.WithFirewallHint(already), "Said once.");
        }

        [Test]
        public void AGuestRefusedAgain_IsShownAgain_AfterAnotherPlayerLeft()
        {
            LanLobby host = Lobby();
            host.Host(Hello("Hosty", "0.416.2"));
            string refused = "A player could not join: The other game is version 0.416.1; this one is 0.416.2.\n" + LanLobby.WaitingText;
            LanLobby old = Lobby();
            old.Join("127.0.0.1", host.HostPort, Hello("Oldy", "0.416.1"));
            Assert.IsTrue(Pump(() => old.State == LanLobbyState.JoinFailed && host.Status == refused, host, old), host.Status);

            // Another player joins and leaves: the status says so, on a fresh listener.
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1", host.HostPort, Hello("Guesty"));
            Assert.IsTrue(Pump(() => host.CanStart && guest.State == LanLobbyState.JoinWaiting, host, guest), host.Status);
            guest.Cancel();
            Assert.IsTrue(Pump(() => host.State == LanLobbyState.HostWaiting, host), host.Status);
            Assert.AreEqual("Guesty left.\n" + LanLobby.WaitingText, host.Status);

            // The old game tries again, refused for the same reason: the host is told again.
            old.Join("127.0.0.1", host.HostPort, Hello("Oldy", "0.416.1"));
            Assert.IsTrue(Pump(() => old.State == LanLobbyState.JoinFailed && host.Status == refused, host, old), "The refusal shows again: " + host.Status);
        }

        [Test]
        public void AFullHost_StopsAnnouncing_AndAnnouncesAgainWhenThePlayerLeaves()
        {
            LanLobby host = Lobby();
            host.Host(Hello("Hosty"));
            Assert.IsTrue(host.Announcing, "Waiting for a player: in the join lists.");
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1", host.HostPort, Hello("Guesty"));
            Assert.IsTrue(Pump(() => host.CanStart && guest.State == LanLobbyState.JoinWaiting, host, guest), host.Status);
            Assert.IsFalse(host.Announcing, "Full: no longer in the join lists.");

            guest.Cancel();
            Assert.IsTrue(Pump(() => host.State == LanLobbyState.HostWaiting, host), host.Status);
            Assert.IsTrue(host.Announcing, "Waiting again: back in the join lists.");
        }

        [Test]
        public void NamesFromTheNetwork_AreCleaned_BeforeTheyShow()
        {
            LanLobby guest = Lobby();
            guest.Browse(Hello("Guesty"));
            LanLobby host = Lobby(guest.BrowsePort);
            host.Host(Hello("Ho\nsty " + new string('h', 80)));
            Assert.IsTrue(Pump(() => guest.Games.Count > 0, host, guest), "The join page lists the host.");
            LanGameInfo game = guest.Games[0];
            StringAssert.StartsWith("Ho sty ", game.Name);
            Assert.AreEqual(LanIdentity.NameLength, game.Name.Length, "One line, not too long.");

            // A guest whose game sends a name with line breaks and hundreds of letters (a modified game).
            LanSession raw = LanSession.Join("127.0.0.1", host.HostPort, Hello("Bad\r\n\r\nname" + new string('x', 300)), NetTestKit.Fast());
            try
            {
                Assert.IsTrue(Pump(() =>
                {
                    raw.Poll();
                    return host.CanStart;
                }, host), host.Status);
                Assert.AreEqual(LanIdentity.CleanName("Bad name" + new string('x', 300)), host.PeerName);
                Assert.AreEqual(LanIdentity.NameLength, host.PeerName.Length);
                Assert.IsFalse(host.Status.Contains("\r"));
                Assert.AreEqual(host.PeerName + " joined. Press Start to begin.", host.Status);
            }
            finally
            {
                raw.Close();
            }
            Assert.AreEqual("a b c", LanIdentity.CleanText(" a\nb\r\n\tc ", 50));
            Assert.AreEqual("", LanIdentity.CleanText(null, 10));
            Assert.AreEqual("abc", LanIdentity.CleanText("abcdef", 3));
        }

        [Test]
        public void TheHostStoppingDuringTheHandshake_GivesNoFirewallHint()
        {
            LanLobby host = Lobby();
            host.Host(Hello("Hosty"));
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1", host.HostPort, Hello("Guesty"));
            // The guest's connection is made (the system takes it for the listening host), but the host never answers its Hello.
            Assert.IsTrue(Pump(() => guest.HostReached, guest), guest.Status);
            Assert.AreEqual(LanLobbyState.JoinConnecting, guest.State);

            host.Cancel();
            Assert.IsTrue(Pump(() => guest.State == LanLobbyState.JoinFailed, guest), guest.Status);
            StringAssert.StartsWith("Could not join.", guest.Status);
            StringAssert.DoesNotContain("Firewall", guest.Status, "The connection worked: the host only stopped.");
        }

        [Test]
        public void APortInUse_IsTriedAgain()
        {
            LanHost busy = NetTestKit.StartHost();
            long now = 0;
            var options = new LanLobbyOptions { GamePort = busy.Port, DiscoveryPort = 0, BindAddress = IPAddress.Loopback, Broadcast = false, Timings = NetTestKit.Fast() };
            var host = new LanLobby(options, () => now);
            _lobbies.Add(host);
            host.Host(Hello("Hosty"));
            Assert.AreEqual(LanLobbyState.HostFailed, host.State);
            StringAssert.StartsWith("Could not host:", host.Status);
            Assert.IsTrue(host.IsHosting);

            busy.Stop();
            host.Update();
            Assert.AreEqual(LanLobbyState.HostFailed, host.State, "Not before the wait.");
            now += LanLobby.RetryMs;
            host.Update();
            Assert.AreEqual(LanLobbyState.HostWaiting, host.State, host.Status);
        }

        [Test]
        public void AListenerThatStopsAcceptingByItself_IsStartedAgain_AndAGuestCanJoin()
        {
            long now = 0;
            int failures = 1;
            var options = new LanLobbyOptions
            {
                GamePort = 0, DiscoveryPort = 0, BindAddress = IPAddress.Loopback, Broadcast = false, Timings = NetTestKit.Fast(),
                BeforeAccept = () => { if (System.Threading.Interlocked.Decrement(ref failures) >= 0) throw new System.InvalidOperationException("The network card went away."); },
            };
            var host = new LanLobby(options, () => now);
            _lobbies.Add(host);
            host.Host(Hello("Hosty"));
            Assert.IsTrue(Pump(() => host.State == LanLobbyState.HostFailed, host), host.State + " " + host.Status);
            StringAssert.StartsWith("Hosting stopped: The network card went away.", host.Status);
            Assert.IsTrue(host.IsHosting);
            Assert.AreEqual("", host.JoinAt);

            now += LanLobby.RetryMs;
            host.Update();
            Assert.AreEqual(LanLobbyState.HostWaiting, host.State, host.Status);
            LanLobby guest = Lobby();
            guest.Join("127.0.0.1:" + host.HostPort, 0, Hello("Guesty")); // the form people copy: the port is read from it
            Assert.IsTrue(Pump(() => host.CanStart && guest.State == LanLobbyState.JoinWaiting, host, guest), $"host {host.State} '{host.Status}', guest {guest.State} '{guest.Status}'");
        }

        [Test]
        public void TheHostShowsWhereToJoin_AndTheJoinListGivesAHintWhenNothingIsHeard()
        {
            Assert.AreEqual("Others can join at 192.168.1.5 (PC-NAME).", LanAddresses.JoinAtText(new[] { "192.168.1.5" }, "PC-NAME"));
            Assert.AreEqual("Others can join at 192.168.1.5 or 10.0.0.2.", LanAddresses.JoinAtText(new[] { "192.168.1.5", "10.0.0.2" }, ""));
            Assert.AreEqual("Others can join at PC-NAME.", LanAddresses.JoinAtText(new string[0], "PC-NAME"));
            Assert.AreEqual("", LanAddresses.JoinAtText(null, null));
            Assert.IsFalse(LanAddresses.Shown(IPAddress.Loopback));
            Assert.IsFalse(LanAddresses.Shown(IPAddress.Parse("169.254.3.4")), "Self-assigned: no network behind it.");
            Assert.IsFalse(LanAddresses.Shown(IPAddress.IPv6Loopback));
            Assert.IsTrue(LanAddresses.Shown(IPAddress.Parse("192.168.1.5")));
            foreach (string address in LanAddresses.LocalIPv4()) Assert.IsTrue(LanAddresses.Shown(IPAddress.Parse(address)), address);

            Assert.AreEqual("192.168.1.5", LanAddresses.SplitPort(" 192.168.1.5:47810 ", out int port));
            Assert.AreEqual(47810, port);
            Assert.AreEqual("PC-NAME", LanAddresses.SplitPort("PC-NAME", out port));
            Assert.AreEqual(0, port);
            Assert.AreEqual("PC-NAME:abc", LanAddresses.SplitPort("PC-NAME:abc", out port), "Not a port: left for the name lookup to refuse.");
            Assert.AreEqual(0, port);
            Assert.AreEqual("fe80::1", LanAddresses.SplitPort("fe80::1", out port));
            Assert.AreEqual(0, port);

            // A host listening on loopback only (a demo, the tests) says so; one on every card lists this PC's addresses, with a port that is not the usual one.
            Assert.AreEqual("Only this PC can join, at 127.0.0.1:5000.", LanLobby.JoinAtFor(IPAddress.Loopback, 5000));
            Assert.AreEqual("Only this PC can join, at 127.0.0.1.", LanLobby.JoinAtFor(IPAddress.Loopback, LanDefaults.GamePort));
            string everyCard = LanLobby.JoinAtFor(null, LanDefaults.GamePort);
            List<string> mine = LanAddresses.LocalIPv4();
            if (mine.Count > 0) StringAssert.Contains(mine[0], everyCard);
            StringAssert.DoesNotContain(":" + LanDefaults.GamePort, everyCard);

            LanLobby host = Lobby();
            host.Host(Hello("Hosty"));
            StringAssert.StartsWith("Only this PC can join, at 127.0.0.1:" + host.HostPort, host.JoinAt);
            host.Cancel();
            Assert.AreEqual("", host.JoinAt);

            long now = 0;
            var options = new LanLobbyOptions { GamePort = 0, DiscoveryPort = 0, BindAddress = IPAddress.Loopback, Broadcast = false, Timings = NetTestKit.Fast() };
            var guest = new LanLobby(options, () => now);
            _lobbies.Add(guest);
            guest.Browse(Hello("Guesty"));
            guest.Update();
            Assert.AreEqual(LanLobby.LookingText, guest.NoGamesText);
            now += LanLobby.NoGamesHintMs;
            guest.Update();
            Assert.AreEqual(LanLobby.NoGamesHint, guest.NoGamesText, "Nothing heard for a while: this PC's firewall, or type the address.");
            StringAssert.Contains("Firewall on this PC", guest.NoGamesText);
        }

        // ------------------------------------------------------------------ the handshake's identity

        [Test]
        public void TheContentText_NamesTheChangedUnits_AndTheNameIsCleaned()
        {
            Assert.AreEqual("modded=no", LanIdentity.Content(null));
            Assert.AreEqual("modded=no", LanIdentity.Content(new string[0]));
            string modded = LanIdentity.Content(new[] { "Scorpion: cost 600 -> 650", "Marine, weapon 1 (MA5): damagePerShot 4 -> 5", "Marine: cost 100 -> 150" });
            StringAssert.StartsWith("modded=yes;Marine=", modded);
            StringAssert.Contains(";Scorpion=", modded);
            Assert.AreEqual(modded, LanIdentity.Content(new[] { "Scorpion: cost 600 -> 650", "Marine, weapon 1 (MA5): damagePerShot 4 -> 5", "Marine: cost 100 -> 150" }), "The same files give the same text.");
            string other = LanIdentity.Content(new[] { "Scorpion: cost 600 -> 650", "Marine, weapon 1 (MA5): damagePerShot 4 -> 6", "Marine: cost 100 -> 150" });
            var a = new LanHello { Content = modded };
            var b = new LanHello { Content = other };
            Assert.AreEqual("Unit stats differ: Marine. Both games need the same unit stats files.", LanHello.Mismatch(a, b));

            Assert.AreEqual("Mike", LanIdentity.CleanName("  Mike \n"));
            Assert.AreEqual(LanIdentity.DefaultName, LanIdentity.CleanName("   "));
            Assert.AreEqual(LanIdentity.NameLength, LanIdentity.CleanName(new string('x', 60)).Length);
            Assert.AreEqual(LanIdentity.CodeFingerprint(), LanIdentity.CodeFingerprint());
            Assert.AreEqual(16, LanIdentity.CodeFingerprint().Length);
            LanHello hello = LanIdentity.Hello(" Mike ", "0.416.2", null);
            Assert.AreEqual("Mike", hello.Name);
            Assert.IsNull(LanHello.Mismatch(hello, LanIdentity.Hello("Other", "0.416.2", null)), "Two copies of one build agree.");
        }

        // ------------------------------------------------------------------ the test run's flags and log lines

        [Test]
        public void TheDemoFlags_AreRead()
        {
            LanDemoArgs none = LanDemoArgs.Parse(new[] { "game.exe" });
            Assert.IsFalse(none.IsDemo);
            Assert.AreEqual(-1, none.WindowSlot);
            Assert.IsNull(none.Seed);

            LanDemoArgs host = LanDemoArgs.Parse(new[] { "game.exe", "-lanDemo", "host", "-lanDemoSeed", "123", "-windowSlot", "0" });
            Assert.AreEqual(LanDemoRole.Host, host.Role);
            Assert.AreEqual(123, host.Seed);
            Assert.AreEqual(0, host.WindowSlot);

            LanDemoArgs join = LanDemoArgs.Parse(new[] { "-lanDemo", "JOIN", "-windowSlot", "1" });
            Assert.AreEqual(LanDemoRole.Join, join.Role);
            Assert.AreEqual("127.0.0.1", join.Address);
            Assert.AreEqual(1, join.WindowSlot);
            Assert.AreEqual(-1, LanDemoArgs.Parse(new[] { "-windowSlot", "2" }).WindowSlot);
            Assert.AreEqual(LanDemoRole.None, LanDemoArgs.Parse(new[] { "-lanDemo" }).Role);
            Assert.IsFalse(host.ListenOnNetwork, "a demo host listens on 127.0.0.1 only by default (no firewall prompt)");
            Assert.IsTrue(LanDemoArgs.Parse(new[] { "-lanDemo", "host", "-lanDemoNetwork" }).ListenOnNetwork);

            LanDemoArgs.SlotRect(0, 1920, 1080, out int x0, out int y0, out int w0, out int h0);
            LanDemoArgs.SlotRect(1, 1920, 1080, out int x1, out int y1, out int w1, out int h1);
            Assert.AreEqual(new[] { 0, 960, 960 }, new[] { x0, w0, x1 });
            Assert.AreEqual(w0, w1);
            Assert.AreEqual(y0, y1);
            Assert.Less(y0 + h0, 1080, "Room for the title bar and the taskbar.");
        }

        [Test]
        public void TheLogLines_HaveTheAgreedShape()
        {
            Assert.AreEqual("[LAN] round 3 hash 00000000000000ff result 000000000000abcd own 000000000000abce baseA 1500 baseB 800", LanMatchLog.Round(3, "00000000000000ff", 0xabcd, 0xabce, 1500, 800));
            Assert.AreEqual("[LAN] match over winner A", LanMatchLog.MatchOver(Team.A));
            Assert.AreEqual("[LAN] match over winner none", LanMatchLog.MatchOver(null));
            Assert.AreEqual("[LAN] ended OpponentLeft The other player left.", LanMatchLog.Ended(LanMatchState.OpponentLeft, "The other player left.\n"));
        }

        [Test]
        public void TheBufferedChannel_GivesWhatWasPutBackFirst()
        {
            LanMemoryChannel.CreatePair(out LanMemoryChannel a, out LanMemoryChannel b);
            a.Send(new NetMessage("Second"));
            var buffered = new LanBufferedChannel(b, new[] { new NetMessage("First") });
            Assert.AreEqual(1, buffered.PutBack);
            Assert.IsTrue(buffered.TryReceive(out NetMessage m));
            Assert.AreEqual("First", m.Type);
            Assert.IsTrue(buffered.TryReceive(out m));
            Assert.AreEqual("Second", m.Type);
            Assert.IsFalse(buffered.TryReceive(out m));
            Assert.IsTrue(buffered.IsOpen);
            buffered.Close();
            Assert.IsFalse(a.IsOpen);
        }

        [Test]
        public void AGuestThatFloodsTheHost_IsReadAFramesShareAtATime_AndItsLeaveStillCounts()
        {
            const int Junk = 1000;
            int ignored = 0;
            var options = new LanLobbyOptions
            {
                GamePort = 0, DiscoveryPort = 0, BindAddress = IPAddress.Loopback, Broadcast = false, Timings = NetTestKit.Fast(),
                Log = line => { if (line.Contains("ignored a Junk")) System.Threading.Interlocked.Increment(ref ignored); },
            };
            var host = new LanLobby(options);
            _lobbies.Add(host);
            host.Host(Hello("Hosty"));
            LanSession flooder = LanSession.Join("127.0.0.1", host.HostPort, Hello("Floody"), NetTestKit.Fast());
            try
            {
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    host.Update();
                    flooder.Poll();
                    return host.CanStart && flooder.State == LanSessionState.Connected;
                }), host.Status);
                for (int i = 0; i < Junk; i++) Assert.IsTrue(flooder.Send(new NetMessage("Junk").Set("i", i)));
                Assert.IsTrue(flooder.Send(new NetMessage(LanMessageTypes.Leave).Set(LanFields.Reason, "bye")));

                int updates = 0;
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    int before = System.Threading.Volatile.Read(ref ignored);
                    host.Update();
                    updates++;
                    Assert.LessOrEqual(System.Threading.Volatile.Read(ref ignored) - before, LanLimits.MaxMessagesPerUpdate, "One Update read more than its share.");
                    return host.State == LanLobbyState.HostWaiting;
                }), "The host never read the guest's Leave: " + host.State + " " + host.Status);
                Assert.AreEqual(Junk, ignored, "Every message before the Leave was read.");
                Assert.GreaterOrEqual(updates, Junk / LanLimits.MaxMessagesPerUpdate, "The flood took several frames, not one.");
                Assert.AreEqual("Floody left.\n" + LanLobby.WaitingText, host.Status);
            }
            finally { flooder.Close(); }
        }
    }
}
