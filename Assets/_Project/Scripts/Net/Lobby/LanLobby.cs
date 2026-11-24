// LanLobby.cs
// The LAN Game page's work before a match (M11 Stage 4), without the engine, so the menu is only a view over it:
//   Host   listens for a guest (LanHost, LanSession.Host; TCP 47810) and announces the game (LanAnnouncer; UDP 47811).
//          HostWaiting ("Waiting for a player...") -> HostReady once a guest's handshake passed (its name in PeerName). A guest whose
//          game does not match is refused and the host keeps waiting (the reason shows, each time). A guest who leaves before the
//          start, even in the same moment its handshake passed, puts the host back to waiting on a fresh listener. A full host
//          (HostReady) stops announcing itself, so join lists show only games that can be joined. Start hands the session over
//          (TakeChannel).
//          HostFailed when the port cannot be used; it tries again every RetryMs.
//   Join   listens for announcements (LanBrowser) while the page shows: Games is the list. Join(address) connects and runs the
//          handshake: JoinConnecting -> JoinWaiting ("Joined X. Waiting for the host to start...") -> GuestStarting once the
//          host's MatchStart arrives (TakeChannel gives the session with that message put back, for LanGuestStart in the Match
//          scene). JoinFailed with the reason in plain words: refused (version, build or unit stats differ), no connection (with a
//          hint about Windows Firewall, only when no connection to the host was made), or the host left.
// Names and texts that come from the network (the other player's name, the games heard, a refusal) are cleaned before they show:
// one line, and not too long (LanIdentity.CleanName, CleanText).
// Cancel leaves either side (the other game is told with Leave) and goes back to Idle (Join: back to browsing).
// Everything runs on the caller's thread: call Update every frame. Close ends everything (leaving the menu).
// Plain C# 9 without the engine.
// Depends on: LanHost, LanSession, LanAnnouncer, LanBrowser, LanHello, LanTimings, NetMessage, LanMessages, LanBufferedChannel.

using System;
using System.Collections.Generic;
using System.Net;

namespace Halobellum.Net
{
    public enum LanLobbyState
    {
        Idle,
        /// <summary>Hosting: no player yet.</summary>
        HostWaiting,
        /// <summary>Hosting: a player joined (PeerName); Start may be pressed.</summary>
        HostReady,
        /// <summary>Hosting could not start (Status says why); tried again every RetryMs.</summary>
        HostFailed,
        /// <summary>The join page is open: listening for games, nothing joined.</summary>
        Browsing,
        /// <summary>Connecting to a host and shaking hands.</summary>
        JoinConnecting,
        /// <summary>Joined; waiting for the host to start.</summary>
        JoinWaiting,
        /// <summary>The join failed or ended (Status says why); the page still browses.</summary>
        JoinFailed,
        /// <summary>The guest got the host's MatchStart: TakeChannel and go to the match.</summary>
        GuestStarting,
        /// <summary>The channel was handed over to the match. Final for this lobby.</summary>
        Started,
    }

    /// <summary>Ports and addresses of a lobby. The defaults are the game's; tests use the loopback address and ports the system picks.</summary>
    public sealed class LanLobbyOptions
    {
        public int GamePort = LanDefaults.GamePort;
        public int DiscoveryPort = LanDefaults.DiscoveryPort;
        /// <summary>The address to listen on; null = every network card. Tests use IPAddress.Loopback (no firewall prompt).</summary>
        public IPAddress BindAddress;
        /// <summary>False: the host announces only to AnnounceTargets (tests).</summary>
        public bool Broadcast = true;
        public List<IPEndPoint> AnnounceTargets = new List<IPEndPoint>();
        /// <summary>False: the join page does not listen for announcements.</summary>
        public bool Browse = true;
        public LanTimings Timings = new LanTimings();
        public Action<string> Log;

        public LanLobbyOptions Copy()
        {
            var copy = (LanLobbyOptions)MemberwiseClone();
            copy.AnnounceTargets = new List<IPEndPoint>(AnnounceTargets);
            copy.Timings = (Timings ?? new LanTimings()).Copy();
            return copy;
        }
    }

    public sealed class LanLobby
    {
        /// <summary>How long the host waits before it tries to listen again after a failure.</summary>
        public const int RetryMs = 2000;

        public const string WaitingText = "Waiting for a player...";
        public const string FirewallHint = "If the game is hosting there, Windows Firewall on that PC may be blocking it: allow Halobellum there, or check the address.";

        private readonly LanLobbyOptions options;
        private readonly Func<long> clockMs;
        private LanHello mine;
        private LanSession session;
        private LanAnnouncer announcer;
        private LanBrowser browser;
        private NetMessage matchStart;
        private string lastRefusal = "";
        private string shownRefusal = "";          // the refusal the status shows, for this listener
        private long retryAtMs;
        private List<LanGameInfo> games = new List<LanGameInfo>();

        public LanLobby(LanLobbyOptions options = null, Func<long> clockMs = null)
        {
            this.options = (options ?? new LanLobbyOptions()).Copy();
            this.clockMs = clockMs ?? (() => NetLog.NowMs);
        }

        public LanLobbyState State { get; private set; } = LanLobbyState.Idle;

        /// <summary>What the page says now, in plain words.</summary>
        public string Status { get; private set; } = "";

        /// <summary>True while this lobby hosts (or tries to).</summary>
        public bool IsHosting => State == LanLobbyState.HostWaiting || State == LanLobbyState.HostReady || State == LanLobbyState.HostFailed;

        /// <summary>True on the join side (browsing, joining, joined).</summary>
        public bool IsJoinSide => State == LanLobbyState.Browsing || State == LanLobbyState.JoinConnecting || State == LanLobbyState.JoinWaiting
            || State == LanLobbyState.JoinFailed || State == LanLobbyState.GuestStarting;

        /// <summary>True while a connection to the other game is open or being made (the name can't change then).</summary>
        public bool Busy => IsHosting || State == LanLobbyState.JoinConnecting || State == LanLobbyState.JoinWaiting || State == LanLobbyState.GuestStarting;

        /// <summary>Host: a player has joined and the match can start.</summary>
        public bool CanStart => State == LanLobbyState.HostReady;

        /// <summary>The other player's name once the handshake passed; "" before.</summary>
        public string PeerName => session != null && session.State == LanSessionState.Connected ? LanIdentity.CleanName(session.PeerName) : "";

        /// <summary>Host: why the last player was refused; "" if none was.</summary>
        public string LastRefusal => lastRefusal;

        /// <summary>Join: true once the TCP connection to the host was made (the handshake may still be going on).</summary>
        public bool HostReached => session != null && !session.IsHost && ReachedHost(session);

        /// <summary>True while the host announces itself on the network (waiting for a player; not once one joined).</summary>
        public bool Announcing => announcer != null;

        /// <summary>The TCP port in use while hosting (useful with port 0 in tests); 0 otherwise.</summary>
        public int HostPort { get; private set; }

        /// <summary>The UDP port the join page listens on (useful with port 0 in tests); 0 when it does not listen.</summary>
        public int BrowsePort => browser?.Port ?? 0;

        /// <summary>The games heard on the network lately (join page), as of the last Update.</summary>
        public IReadOnlyList<LanGameInfo> Games => games;

        /// <summary>Why the join page cannot list games ("" when it can): the address field still works.</summary>
        public string BrowseError { get; private set; } = "";

        /// <summary>This game's description as sent.</summary>
        public LanHello Mine => mine;

        // ------------------------------------------------------------------ host

        /// <summary>Starts hosting with this description (its name is the player's). Leaves whatever this lobby did before.</summary>
        public void Host(LanHello hello)
        {
            CloseAll(null);
            mine = (hello ?? new LanHello()).Copy();
            lastRefusal = "";
            StartListening(WaitingText);
        }

        private void StartListening(string status)
        {
            var host = new LanHost(options.GamePort, options.BindAddress, options.Timings, options.Log);
            session = LanSession.Host(host, mine, options.Log);
            if (session.State == LanSessionState.Lost)
            {
                string reason = session.Reason;
                session.Close();
                session = null;
                HostPort = 0;
                State = LanLobbyState.HostFailed;
                Status = "Could not host: " + reason + " Is another copy of the game hosting on this PC? Trying again...";
                retryAtMs = clockMs() + RetryMs;
                return;
            }
            HostPort = host.Port;
            shownRefusal = "";
            announcer = new LanAnnouncer(mine.Name, mine.Version, host.Port, options.DiscoveryPort, options.BindAddress, options.Log) { Broadcast = options.Broadcast };
            foreach (IPEndPoint target in options.AnnounceTargets) announcer.AddTarget(target);
            if (!announcer.TryStart(out string error)) NetLog.Write(options.Log, "No announcements: " + error);
            State = LanLobbyState.HostWaiting;
            Status = status;
        }

        private void UpdateHost()
        {
            if (State == LanLobbyState.HostFailed)
            {
                if (clockMs() >= retryAtMs) StartListening(WaitingText);
                return;
            }
            session.Poll();
            if (session.LastRefusal.Length > 0 && session.LastRefusal != shownRefusal)
            {
                // Each fresh listener starts with none shown, so a guest refused again for the same reason is shown again.
                shownRefusal = session.LastRefusal;
                lastRefusal = LanIdentity.CleanText(session.LastRefusal, TextLength);
                if (State == LanLobbyState.HostWaiting) Status = "A player could not join: " + lastRefusal + "\n" + WaitingText;
            }
            if (State == LanLobbyState.HostWaiting && session.State == LanSessionState.Connected)
            {
                State = LanLobbyState.HostReady;
                Status = PeerName + " joined. Press Start to begin.";
                // Full: the game no longer shows in join lists. A fresh listener announces again if this player leaves.
                announcer?.Close();
                announcer = null;
                return;
            }
            if (State == LanLobbyState.HostWaiting && session.IsFinished)
            {
                // The guest's handshake passed and its connection dropped in the same poll: the session is over (and its LanHost
                // locked), so listen again as if the guest had left.
                string gone = session.PeerName.Length > 0 ? LanIdentity.CleanName(session.PeerName) : "The player";
                NetLog.Write(options.Log, "The guest left as it joined: " + session.Reason);
                CloseConnection(null);
                StartListening(gone + " left.\n" + WaitingText);
                return;
            }
            if (State != LanLobbyState.HostReady) return;

            string left = null;
            while (session.TryReceive(out NetMessage m))
            {
                if (m.Type == LanMessageTypes.Leave) left = m.GetString(LanFields.Reason, "");
                else NetLog.Write(options.Log, "Before the start, ignored a " + m.Type + " message.");
            }
            if (left == null && session.State == LanSessionState.Connected) return;
            string name = session.PeerName.Length > 0 ? LanIdentity.CleanName(session.PeerName) : "The player";
            NetLog.Write(options.Log, "The guest left before the start: " + (left ?? session.Reason));
            CloseConnection(null);
            StartListening(name + " left.\n" + WaitingText);
        }

        /// <summary>Host: hands the connected session over for the match (LanMatchStart.BeginAsHost). Null unless CanStart.</summary>
        public ILanChannel TakeChannel()
        {
            if (State == LanLobbyState.HostReady)
            {
                LanSession taken = session;
                session = null;
                CloseAll(null);
                State = LanLobbyState.Started;
                Status = "Starting...";
                return taken;
            }
            if (State == LanLobbyState.GuestStarting)
            {
                var taken = new LanBufferedChannel(session, new[] { matchStart });
                session = null;
                matchStart = null;
                CloseAll(null);
                State = LanLobbyState.Started;
                Status = "Starting...";
                return taken;
            }
            return null;
        }

        // ------------------------------------------------------------------ join

        /// <summary>Opens the join side: listens for games (unless the options say not to). Leaves whatever this lobby did before.</summary>
        public void Browse(LanHello hello)
        {
            CloseAll(null);
            mine = (hello ?? new LanHello()).Copy();
            StartBrowser();
            State = LanLobbyState.Browsing;
            Status = "Pick a game, or type the host's address.";
        }

        private void StartBrowser()
        {
            BrowseError = "";
            games = new List<LanGameInfo>();
            if (!options.Browse || browser != null) return;
            browser = new LanBrowser(options.DiscoveryPort, options.BindAddress, options.Log);
            if (!browser.TryStart(out string error))
            {
                browser = null;
                BrowseError = "Games on the network can't be listed (" + error + "). Type the host's address instead.";
            }
        }

        /// <summary>Joins the game at this address (an IPv4 address or a computer name). With hello, the name is updated first.</summary>
        public void Join(string address, int port = 0, LanHello hello = null)
        {
            if (hello != null) mine = hello.Copy();
            if (mine == null) mine = new LanHello();
            CloseConnection("The other player left.");
            if (browser == null && !IsJoinSide) StartBrowser();
            string where = (address ?? "").Trim();
            if (where.Length == 0)
            {
                State = LanLobbyState.JoinFailed;
                Status = "Type the host's address first.";
                return;
            }
            session = LanSession.Join(where, port > 0 ? port : options.GamePort, mine, options.Timings, options.Log);
            State = LanLobbyState.JoinConnecting;
            Status = "Connecting to " + where + "...";
        }

        /// <summary>Joins a game from the list.</summary>
        public void Join(LanGameInfo game, LanHello hello = null)
        {
            if (game == null) return;
            Join(game.Address, game.Port, hello);
        }

        private void UpdateJoin()
        {
            if (browser != null) games = CleanGames(browser.Games());
            if (session == null) return;
            if (State == LanLobbyState.JoinConnecting)
            {
                session.Poll();
                switch (session.State)
                {
                    case LanSessionState.Connected:
                        State = LanLobbyState.JoinWaiting;
                        Status = "Joined " + PeerName + ". Waiting for the host to start...";
                        break;
                    case LanSessionState.Refused:
                        Fail("The host refused: " + LanIdentity.CleanText(session.Reason, TextLength));
                        return;
                    case LanSessionState.Lost:
                    case LanSessionState.Closed:
                        // The firewall hint only when no connection to the host was made: once one was, the host is reachable and
                        // ended it itself (it stopped hosting, or quit).
                        string why = "Could not join. " + LanIdentity.CleanText(session.Reason, TextLength);
                        Fail(ReachedHost(session) ? why : WithFirewallHint(why));
                        return;
                }
            }
            if (State != LanLobbyState.JoinWaiting) return;
            while (session.TryReceive(out NetMessage m))
            {
                if (m.Type == LanMessageTypes.MatchStart)
                {
                    matchStart = m; // the rest stays in the session, for the coordinator
                    State = LanLobbyState.GuestStarting;
                    Status = "The host started the match.";
                    return;
                }
                if (m.Type == LanMessageTypes.Leave)
                {
                    string reason = LanIdentity.CleanText(m.GetString(LanFields.Reason, ""), TextLength);
                    Fail("The host left." + (reason.Length > 0 && reason != "The other player left." ? " " + reason : ""));
                    return;
                }
                NetLog.Write(options.Log, "Before the start, ignored a " + m.Type + " message.");
            }
            if (session.State != LanSessionState.Connected) Fail("The connection to the host ended. " + session.Reason);
        }

        /// <summary>The longest text from the network (a refusal, a reason to leave) that shows on the page.</summary>
        public const int TextLength = 200;

        /// <summary>True once the guest's TCP connection to the host was made (the host's address is known then).</summary>
        private static bool ReachedHost(LanSession joining) => joining.Connection != null && joining.Connection.RemoteAddress.Length > 0;

        /// <summary>The games heard, with their name and version cleaned for the list (they come from the network).</summary>
        private static List<LanGameInfo> CleanGames(IEnumerable<LanGameInfo> heard)
        {
            var clean = new List<LanGameInfo>();
            foreach (LanGameInfo game in heard)
            {
                LanGameInfo copy = game.Copy();
                copy.Name = LanIdentity.CleanName(game.Name);
                copy.Version = LanIdentity.CleanText(game.Version, LanIdentity.NameLength);
                clean.Add(copy);
            }
            return clean;
        }

        private void Fail(string status)
        {
            session?.Close();
            session = null;
            State = LanLobbyState.JoinFailed;
            Status = status;
        }

        /// <summary>Adds the Windows Firewall hint to a reason that does not already mention it.</summary>
        public static string WithFirewallHint(string reason)
        {
            string text = (reason ?? "").Trim();
            if (text.IndexOf("Firewall", StringComparison.OrdinalIgnoreCase) >= 0) return text;
            return (text.Length > 0 ? text + " " : "") + FirewallHint;
        }

        // ------------------------------------------------------------------ both

        /// <summary>Moves things on: takes a player, answers the handshake, lists games, notices a refusal, a start or a drop.</summary>
        public void Update()
        {
            if (IsHosting) UpdateHost();
            else if (IsJoinSide) UpdateJoin();
        }

        /// <summary>
        /// Leaves what this side is doing: the host stops hosting (a joined player is told) and goes Idle; the guest leaves the host it
        /// joined (the host is told) and goes back to browsing, or Idle if it was only browsing.
        /// </summary>
        public void Cancel()
        {
            if (IsHosting)
            {
                CloseAll("The host stopped hosting.");
                State = LanLobbyState.Idle;
                Status = "";
                return;
            }
            if (State == LanLobbyState.JoinConnecting || State == LanLobbyState.JoinWaiting || State == LanLobbyState.GuestStarting || State == LanLobbyState.JoinFailed)
            {
                CloseConnection("The other player left.");
                matchStart = null;
                State = LanLobbyState.Browsing;
                Status = "Pick a game, or type the host's address.";
                return;
            }
            CloseAll(null);
            State = LanLobbyState.Idle;
            Status = "";
        }

        /// <summary>Ends everything (leaving the page or the menu). The other game, if any, is told. Safe to call more than once.</summary>
        public void Close()
        {
            if (State == LanLobbyState.Started) return;
            CloseAll("The other player left.");
            State = LanLobbyState.Idle;
            Status = "";
        }

        private void CloseConnection(string leaveReason)
        {
            if (session == null) return;
            if (leaveReason != null && session.State == LanSessionState.Connected) session.Send(new NetMessage(LanMessageTypes.Leave).Set(LanFields.Reason, leaveReason));
            session.Close();
            session = null;
            announcer?.Close();
            announcer = null;
            HostPort = 0;
        }

        private void CloseAll(string leaveReason)
        {
            CloseConnection(leaveReason);
            announcer?.Close();
            announcer = null;
            browser?.Close();
            browser = null;
            games = new List<LanGameInfo>();
        }
    }
}
