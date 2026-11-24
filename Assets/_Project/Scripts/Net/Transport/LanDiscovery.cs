// LanDiscovery.cs
// Finding a host on the local network without typing its address.
//   - LanAnnouncer (on the host) sends a small UDP packet every second to port 47811: to 255.255.255.255, to the broadcast
//     address of each IPv4 network card (some networks drop the first) and to any extra address given with AddTarget.
//     The packet is JSON: {"game":"Halobellum","proto":1,"version":"0.416.2","name":"Mike","port":47810}.
//   - LanBrowser (on the Join page) listens on port 47811 (shared with other programs, ReuseAddress) and keeps the games
//     heard in the last 4 seconds, each with the address the packet came from. It keeps at most LanLimits.MaxGamesListed games
//     (a game not heard for StaleMs is dropped first; when the list is still full, a new game waits until a place frees up), and
//     a name or version longer than LanLimits.MaxNameChars is cut short.
// Broadcasts do not reach a socket bound to the loopback address, so the tests send a unicast announcement (AddTarget) to a
// browser bound to 127.0.0.1; the game itself uses the broadcasts. Both close cleanly; ports and addresses are injectable.
// Plain C# 9 without the engine; System.Net.Sockets only.
// Depends on: NetJson, LanTimings (LanDefaults, LanLimits, NetLog).

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Halobellum.Net
{
    /// <summary>A game heard on the network.</summary>
    public sealed class LanGameInfo
    {
        /// <summary>The host player's name.</summary>
        public string Name = "";
        public string Version = "";
        public int Proto;
        /// <summary>The host's address, as the packet came from it ("192.168.1.20").</summary>
        public string Address = "";
        /// <summary>TCP port to connect to.</summary>
        public int Port;
        /// <summary>When the last announcement arrived, on the browser's clock (ms).</summary>
        public long LastHeardMs;

        public LanGameInfo Copy() => (LanGameInfo)MemberwiseClone();

        public override string ToString() => Name + " (" + Version + ") at " + Address + ":" + Port;
    }

    /// <summary>Sends the host's announcement every second.</summary>
    public sealed class LanAnnouncer
    {
        private readonly object gate = new object();
        private readonly int discoveryPort;
        private readonly IPAddress bindAddress;
        private readonly Action<string> log;
        private readonly List<IPEndPoint> extraTargets = new List<IPEndPoint>();
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private readonly byte[] payload;
        private Socket socket;
        private Thread thread;
        private volatile bool closed;
        private int sentCount;
        private bool warned;

        /// <param name="bindAddress">Address to send from; null = any. Tests use IPAddress.Loopback.</param>
        public LanAnnouncer(string hostName, string version, int gamePort, int discoveryPort = LanDefaults.DiscoveryPort,
            IPAddress bindAddress = null, Action<string> log = null)
        {
            this.discoveryPort = discoveryPort;
            this.bindAddress = bindAddress ?? IPAddress.Any;
            this.log = log;
            payload = Encoding.UTF8.GetBytes(BuildAnnouncement(hostName, version, gamePort).ToJson());
        }

        /// <summary>Time between announcements.</summary>
        public int IntervalMs = 1000;

        /// <summary>False sends only to the AddTarget addresses (tests: no broadcast packets on the real network).</summary>
        public bool Broadcast = true;

        /// <summary>Announcements sent so far (one per address per round).</summary>
        public int SentCount => Volatile.Read(ref sentCount);

        /// <summary>Also sends every announcement to this address (a unicast target, e.g. 127.0.0.1 in tests).</summary>
        public void AddTarget(IPEndPoint target)
        {
            lock (gate) extraTargets.Add(target);
        }

        /// <summary>The announcement's JSON.</summary>
        public static JsonObject BuildAnnouncement(string hostName, string version, int gamePort)
        {
            return new JsonObject()
                .Set("game", LanDefaults.GameName)
                .Set("proto", LanDefaults.Protocol)
                .Set("version", version ?? "")
                .Set("name", hostName ?? "")
                .Set("port", gamePort);
        }

        /// <summary>Starts sending. False with a reason when no UDP socket could be made.</summary>
        public bool TryStart(out string error)
        {
            error = null;
            lock (gate)
            {
                if (closed) { error = "This announcer was closed."; return false; }
                if (socket != null) return true;
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.EnableBroadcast = true;
                    socket.Bind(new IPEndPoint(bindAddress, 0));
                }
                catch (Exception e)
                {
                    try { socket?.Close(); } catch { }
                    socket = null;
                    error = "Could not announce the game: " + e.Message;
                    NetLog.Write(log, error);
                    return false;
                }
                thread = new Thread(Loop) { IsBackground = true, Name = "LAN announcer" };
                thread.Start();
            }
            return true;
        }

        public void Close()
        {
            Socket s;
            lock (gate)
            {
                if (closed) return;
                closed = true;
                s = socket;
            }
            stop.Set();
            thread?.Join(1000);
            try { s?.Close(); } catch { }
        }

        /// <summary>255.255.255.255 and the broadcast address of every IPv4 network card that is up (loopback left out).</summary>
        public static List<IPAddress> BroadcastAddresses()
        {
            var result = new List<IPAddress> { IPAddress.Broadcast };
            try
            {
                foreach (NetworkInterface card in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (card.OperationalStatus != OperationalStatus.Up || card.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation info in card.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        IPAddress mask;
                        try { mask = info.IPv4Mask; } catch { continue; } // not every platform knows the mask
                        IPAddress broadcast = BroadcastAddress(info.Address, mask);
                        if (broadcast != null && !result.Contains(broadcast)) result.Add(broadcast);
                    }
                }
            }
            catch { /* without the card list, 255.255.255.255 still works on most networks */ }
            return result;
        }

        /// <summary>The broadcast address of a network: the address with every bit outside the mask set. Null if unknown.</summary>
        public static IPAddress BroadcastAddress(IPAddress address, IPAddress mask)
        {
            if (address == null || mask == null) return null;
            byte[] a = address.GetAddressBytes();
            byte[] m = mask.GetAddressBytes();
            if (a.Length != 4 || m.Length != 4) return null;
            if (m[0] == 0 && m[1] == 0 && m[2] == 0 && m[3] == 0) return null;
            var b = new byte[4];
            for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
            return new IPAddress(b);
        }

        private void Loop()
        {
            List<IPAddress> broadcasts = null;
            long refreshed = long.MinValue;
            do
            {
                var targets = new List<IPEndPoint>();
                if (Broadcast)
                {
                    long now = NetLog.NowMs;
                    if (broadcasts == null || now - refreshed > 10000) { broadcasts = BroadcastAddresses(); refreshed = now; }
                    foreach (IPAddress a in broadcasts) targets.Add(new IPEndPoint(a, discoveryPort));
                }
                lock (gate) targets.AddRange(extraTargets);
                foreach (IPEndPoint target in targets)
                {
                    if (closed) return;
                    try
                    {
                        socket.SendTo(payload, target);
                        Interlocked.Increment(ref sentCount);
                    }
                    catch (Exception e)
                    {
                        if (closed) return;
                        if (!warned) NetLog.Write(log, "Could not announce to " + target + ": " + e.Message);
                        warned = true;
                    }
                }
            } while (!stop.WaitOne(Math.Max(10, IntervalMs)));
        }
    }

    /// <summary>Listens for announcements and keeps the games heard lately.</summary>
    public sealed class LanBrowser
    {
        private readonly object gate = new object();
        private readonly int discoveryPort;
        private readonly IPAddress bindAddress;
        private readonly Action<string> log;
        private readonly Func<long> clockMs;
        private readonly Dictionary<string, LanGameInfo> games = new Dictionary<string, LanGameInfo>();
        private Socket socket;
        private Thread thread;
        private volatile bool closed;

        /// <param name="bindAddress">Address to listen on; null = every network card. Tests use IPAddress.Loopback.</param>
        /// <param name="clockMs">Clock in milliseconds (tests); null = the real one.</param>
        public LanBrowser(int discoveryPort = LanDefaults.DiscoveryPort, IPAddress bindAddress = null, Action<string> log = null, Func<long> clockMs = null)
        {
            this.discoveryPort = discoveryPort;
            this.bindAddress = bindAddress ?? IPAddress.Any;
            this.log = log;
            this.clockMs = clockMs ?? (() => NetLog.NowMs);
        }

        /// <summary>A game not heard for this long drops off the list.</summary>
        public int StaleMs = 4000;

        /// <summary>The most games kept at once (LanLimits.MaxGamesListed).</summary>
        public const int MaxGames = LanLimits.MaxGamesListed;

        /// <summary>Games kept now, stale ones included until the next Games() or the next new game (tests).</summary>
        public int Count
        {
            get { lock (gate) return games.Count; }
        }

        /// <summary>The port actually listened on (after TryStart; useful with port 0).</summary>
        public int Port { get; private set; }

        /// <summary>Starts listening. False with a reason when the port cannot be used.</summary>
        public bool TryStart(out string error)
        {
            error = null;
            lock (gate)
            {
                if (closed) { error = "This browser was closed."; return false; }
                if (socket != null) return true;
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.Bind(new IPEndPoint(bindAddress, discoveryPort));
                    Port = ((IPEndPoint)socket.LocalEndPoint).Port;
                }
                catch (Exception e)
                {
                    try { socket?.Close(); } catch { }
                    socket = null;
                    error = "Could not listen for games on port " + discoveryPort + ": " + e.Message;
                    NetLog.Write(log, error);
                    return false;
                }
                thread = new Thread(Loop) { IsBackground = true, Name = "LAN browser" };
                thread.Start();
            }
            return true;
        }

        /// <summary>The games heard in the last StaleMs, by name then address. Copies: safe to keep.</summary>
        public List<LanGameInfo> Games()
        {
            long now = clockMs();
            var result = new List<LanGameInfo>();
            lock (gate)
            {
                var stale = new List<string>();
                foreach (KeyValuePair<string, LanGameInfo> pair in games)
                {
                    if (now - pair.Value.LastHeardMs > StaleMs) stale.Add(pair.Key);
                    else result.Add(pair.Value.Copy());
                }
                foreach (string key in stale) games.Remove(key);
            }
            result.Sort((x, y) =>
            {
                int byName = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : string.CompareOrdinal(x.Address + ":" + x.Port, y.Address + ":" + y.Port);
            });
            return result;
        }

        public void Close()
        {
            Socket s;
            lock (gate)
            {
                if (closed) return;
                closed = true;
                s = socket;
            }
            try { s?.Close(); } catch { }
            thread?.Join(1000);
        }

        /// <summary>Reads one announcement; null when it is not a Halobellum game's.</summary>
        public static LanGameInfo ParseAnnouncement(byte[] data, int count, string senderAddress)
        {
            try
            {
                JsonObject o = NetJson.ParseObject(Encoding.UTF8.GetString(data, 0, count));
                if (o.GetString("game") != LanDefaults.GameName) return null;
                int port = o.GetInt("port");
                if (port <= 0 || port > 65535) return null;
                return new LanGameInfo
                {
                    Name = LanLimits.Clip(o.GetString("name", ""), LanLimits.MaxNameChars),
                    Version = LanLimits.Clip(o.GetString("version", ""), LanLimits.MaxNameChars),
                    Proto = o.GetInt("proto"),
                    Address = senderAddress ?? "",
                    Port = port,
                };
            }
            catch (FormatException) { return null; }
        }

        private void Loop()
        {
            var buffer = new byte[2048];
            while (!closed)
            {
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                int count;
                try { count = socket.ReceiveFrom(buffer, ref from); }
                catch (SocketException e)
                {
                    if (closed) return;
                    if (e.SocketErrorCode == SocketError.ConnectionReset || e.SocketErrorCode == SocketError.MessageSize) continue;
                    NetLog.Write(log, "Stopped listening for games: " + e.Message);
                    return;
                }
                catch (ObjectDisposedException) { return; }
                LanGameInfo game = ParseAnnouncement(buffer, count, (from as IPEndPoint)?.Address.ToString());
                if (game != null) Add(game, clockMs());
            }
        }

        /// <summary>
        /// Notes a game heard at this time (the receive loop calls it; tests may too). False when the list is full of games heard
        /// lately (MaxGames): the new one is not kept.
        /// </summary>
        public bool Add(LanGameInfo game, long nowMs)
        {
            game.LastHeardMs = nowMs;
            string key = game.Address + ":" + game.Port;
            lock (gate)
            {
                if (!games.ContainsKey(key))
                {
                    if (games.Count >= MaxGames) RemoveStale(nowMs);
                    if (games.Count >= MaxGames) return false; // full of games heard lately: this one waits for a free place
                    NetLog.Write(log, "Found a game: " + game + ".");
                }
                games[key] = game;
                return true;
            }
        }

        /// <summary>Drops the games not heard for StaleMs. Call inside the lock.</summary>
        private void RemoveStale(long nowMs)
        {
            var stale = new List<string>();
            foreach (KeyValuePair<string, LanGameInfo> pair in games)
                if (nowMs - pair.Value.LastHeardMs > StaleMs) stale.Add(pair.Key);
            foreach (string key in stale) games.Remove(key);
        }
    }
}
