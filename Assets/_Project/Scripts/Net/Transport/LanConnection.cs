// LanConnection.cs
// One TCP connection to the other game. Two background threads do the network work:
//   - the reader receives bytes, cuts them into frames (FrameReader) and puts each message into the inbox;
//   - the writer sends what Send queued, sends a Ping every second and notices 10 seconds of silence (times in LanTimings).
// The game drains the inbox on its own thread (TryReceive, every frame), so it never touches a socket or waits.
// Ping and Pong are handled here and never reach the inbox. State goes Connecting -> Connected -> Closed, and once Closed it
// stays so; CloseReason says why (closed here, closed by the other side, silence, an error) and CloseDetail says it in words.
// Messages already in the inbox can still be read after the connection closed (a Reject arrives just before the close).
// A Close while still connecting shuts the connecting socket, so the attempt stops at once instead of after the connect timeout.
// A local Close sends the messages still queued, then waits a moment for the other side to close in turn, so the last
// message is not cut off (closing a socket with unread data would reset the connection).
// Plain C# 9 without the engine; System.Net.Sockets only.
// Depends on: Framing, NetMessage, LanTimings (NetLog).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Halobellum.Net
{
    public enum LanConnectionState
    {
        Connecting,
        Connected,
        Closed,
    }

    public enum LanCloseReason
    {
        /// <summary>Not closed.</summary>
        None,
        /// <summary>This game called Close.</summary>
        LocalClose,
        /// <summary>The other game closed the connection (or it was cut on its side).</summary>
        RemoteClosed,
        /// <summary>Nothing received for LanTimings.TimeoutMs.</summary>
        Timeout,
        /// <summary>A network error, a failed connect or a broken message; CloseDetail has the words.</summary>
        Error,
    }

    public sealed class LanConnection
    {
        private readonly object gate = new object();
        private readonly LanTimings timings;
        private readonly Action<string> log;
        private readonly ConcurrentQueue<NetMessage> inbox = new ConcurrentQueue<NetMessage>();
        private readonly ConcurrentQueue<byte[]> outbox = new ConcurrentQueue<byte[]>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly ManualResetEvent finished = new ManualResetEvent(false);
        private Socket socket;
        private Socket connectingSocket;          // LanClient: the socket still connecting (Close shuts it to stop the attempt)
        private bool connectWorkerRunning;        // LanClient: the connect thread has not ended yet
        private Thread reader;
        private Thread writer;
        private volatile LanConnectionState state = LanConnectionState.Connecting;
        private volatile LanCloseReason closeReason = LanCloseReason.None;
        private volatile string closeDetail = "";
        private long lastHeardMs;
        private int roundTripMs = -1;

        /// <summary>Wraps a socket that is already connected and starts its threads.</summary>
        public LanConnection(Socket connected, LanTimings timings = null, Action<string> log = null, string label = null)
            : this(timings, log, label)
        {
            if (connected == null) throw new ArgumentNullException(nameof(connected));
            Attach(connected);
        }

        /// <summary>A connection still being made (LanClient); Attach or FailConnect follows.</summary>
        internal LanConnection(LanTimings timings, Action<string> log, string label)
        {
            this.timings = (timings ?? new LanTimings()).Copy();
            this.log = log;
            Label = label ?? "connection";
        }

        /// <summary>A name for the log ("guest 192.168.1.20:51234").</summary>
        public string Label { get; private set; }

        /// <summary>The other side's address and port, once connected ("192.168.1.20:47810"), else "".</summary>
        public string RemoteAddress { get; private set; } = "";

        public LanConnectionState State => state;
        public LanCloseReason CloseReason => closeReason;
        /// <summary>Why the connection closed, in words for the player and the log; "" while open.</summary>
        public string CloseDetail => closeDetail;

        /// <summary>The last Ping's round trip in milliseconds, or -1 before the first Pong.</summary>
        public int RoundTripMs => Volatile.Read(ref roundTripMs);

        /// <summary>Messages waiting in the inbox.</summary>
        public int Pending => inbox.Count;

        /// <summary>
        /// Queues a message to send. Thread-safe. Returns false when the connection is closed (the message is dropped).
        /// While Connecting the message waits and goes out once connected. Throws FrameException when it is over 4 MB.
        /// </summary>
        public bool Send(JsonObject message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            byte[] frame = Framing.Encode(message);
            if (state == LanConnectionState.Closed) return false;
            outbox.Enqueue(frame);
            wake.Set();
            return true;
        }

        /// <summary>The next received message, if any. Works after the close too, until the inbox is empty.</summary>
        public bool TryReceive(out NetMessage message) => inbox.TryDequeue(out message);

        /// <summary>Every message waiting in the inbox, oldest first.</summary>
        public List<NetMessage> ReceiveAll()
        {
            var result = new List<NetMessage>();
            while (inbox.TryDequeue(out NetMessage m)) result.Add(m);
            return result;
        }

        /// <summary>Closes the connection. Safe to call any number of times and from any thread; it does not wait.</summary>
        public void Close() => CloseWith(LanCloseReason.LocalClose, "Closed by this game.");

        /// <summary>
        /// Waits until the socket is fully shut and the threads have ended (for a connection closed while still connecting: until
        /// the connect attempt has stopped and its socket is shut). True if that happened within the time.
        /// </summary>
        public bool WaitClosed(int timeoutMs) => finished.WaitOne(timeoutMs);

        /// <summary>LanClient: its connect thread starts. WaitClosed waits for ConnectWorkerEnded from now on.</summary>
        internal void ConnectWorkerStarted()
        {
            lock (gate) connectWorkerRunning = true;
        }

        /// <summary>
        /// LanClient: the socket that is about to connect. False when the connection was closed already (the caller shuts the
        /// socket and gives up). While it is kept here, Close shuts it, which stops the attempt at once.
        /// </summary>
        internal bool TrackConnecting(Socket connecting)
        {
            lock (gate)
            {
                if (state == LanConnectionState.Closed) return false;
                connectingSocket = connecting;
                return true;
            }
        }

        /// <summary>LanClient: the connect thread is ending (connected, failed or given up).</summary>
        internal void ConnectWorkerEnded()
        {
            bool done;
            lock (gate)
            {
                connectWorkerRunning = false;
                connectingSocket = null;
                done = state == LanConnectionState.Closed && writer == null;
            }
            if (done)
            {
                if (closeReason == LanCloseReason.LocalClose) NetLog.Write(log, Label + ": stopped connecting.");
                finished.Set(); // never connected: nothing else to tear down
            }
        }

        /// <summary>Gives a connected socket to a connection made by LanClient and starts the threads.</summary>
        internal void Attach(Socket connected)
        {
            lock (gate)
            {
                connectingSocket = null;
                if (state == LanConnectionState.Closed)
                {
                    // Closed while still connecting: nobody wants this socket any more (ConnectWorkerEnded sets finished).
                    try { connected.Close(); } catch { }
                    return;
                }
                socket = connected;
                try { socket.NoDelay = true; } catch { }
                try { socket.SendTimeout = timings.SendTimeoutMs; } catch { }
                try { RemoteAddress = socket.RemoteEndPoint is IPEndPoint ep ? ep.Address + ":" + ep.Port : ""; } catch { }
                Interlocked.Exchange(ref lastHeardMs, NetLog.NowMs);
                state = LanConnectionState.Connected;
                reader = new Thread(ReadLoop) { IsBackground = true, Name = "LAN reader" };
                writer = new Thread(WriteLoop) { IsBackground = true, Name = "LAN writer" };
                reader.Start();
                writer.Start();
            }
            NetLog.Write(log, Label + ": connected to " + RemoteAddress + ".");
        }

        /// <summary>Ends a connection that could not be made.</summary>
        internal void FailConnect(string detail) => CloseWith(LanCloseReason.Error, detail);

        /// <summary>Moves to Closed with this reason, unless already closed. True if this call closed it.</summary>
        private bool CloseWith(LanCloseReason reason, string detail)
        {
            bool hasWriter, connecting;
            Socket stillConnecting;
            lock (gate)
            {
                if (state == LanConnectionState.Closed) return false;
                closeDetail = detail ?? "";
                closeReason = reason;
                state = LanConnectionState.Closed;
                hasWriter = writer != null;
                connecting = connectWorkerRunning;
                stillConnecting = connectingSocket;
                connectingSocket = null;
            }
            NetLog.Write(log, Label + ": closed (" + reason + "). " + detail);
            wake.Set();
            // Closed while connecting: shutting the socket ends the attempt now, instead of after the connect timeout.
            if (stillConnecting != null) { try { stillConnecting.Close(); } catch { } }
            // Never connected and no connect thread left: nothing to tear down. Otherwise the writer (TearDown) or the connect
            // thread (ConnectWorkerEnded) sets finished when it ends.
            if (!hasWriter && !connecting) finished.Set();
            return true;
        }

        private void ReadLoop()
        {
            var buffer = new byte[64 * 1024];
            var frames = new FrameReader();
            try
            {
                while (true)
                {
                    int count = socket.Receive(buffer);
                    if (count == 0)
                    {
                        CloseWith(LanCloseReason.RemoteClosed, "The other game closed the connection.");
                        return;
                    }
                    if (state == LanConnectionState.Closed) continue; // closing here: drop what still comes, wait for their close
                    Interlocked.Exchange(ref lastHeardMs, NetLog.NowMs);
                    frames.Push(buffer, 0, count);
                    while (frames.TryReadText(out string json)) Handle(NetMessage.Parse(json));
                }
            }
            catch (FrameException e) { CloseWith(LanCloseReason.Error, "Bad message from the other game: " + e.Message); }
            catch (FormatException e) { CloseWith(LanCloseReason.Error, "Bad message from the other game: " + e.Message); }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionReset || e.SocketErrorCode == SocketError.ConnectionAborted)
                    CloseWith(LanCloseReason.RemoteClosed, "The connection was cut on the other side.");
                else CloseWith(LanCloseReason.Error, "Network error: " + e.Message);
            }
            catch (ObjectDisposedException) { CloseWith(LanCloseReason.Error, "The connection was shut."); }
            catch (Exception e) { CloseWith(LanCloseReason.Error, "Network error: " + e.Message); }
        }

        private void Handle(NetMessage message)
        {
            string type = message.Type;
            if (type == NetMessageTypes.Ping)
            {
                var pong = new NetMessage(NetMessageTypes.Pong);
                pong.SetRaw("ms", message.Get("ms"));
                outbox.Enqueue(Framing.Encode(pong));
                wake.Set();
                return;
            }
            if (type == NetMessageTypes.Pong)
            {
                long sent = message.GetLong("ms", -1);
                if (sent >= 0) Volatile.Write(ref roundTripMs, (int)Math.Max(0, Math.Min(int.MaxValue, NetLog.NowMs - sent)));
                return;
            }
            inbox.Enqueue(message);
        }

        private void WriteLoop()
        {
            int ping = timings.PingIntervalMs;
            int tick = Math.Max(5, Math.Min(100, Math.Min(ping > 0 ? ping : 1000, Math.Max(1, timings.TimeoutMs)) / 4));
            long lastPing = NetLog.NowMs;
            try
            {
                while (state != LanConnectionState.Closed)
                {
                    wake.WaitOne(tick);
                    if (state == LanConnectionState.Closed) break;
                    long now = NetLog.NowMs;
                    if (now - Interlocked.Read(ref lastHeardMs) >= timings.TimeoutMs)
                    {
                        CloseWith(LanCloseReason.Timeout, "Nothing heard from the other game for " + (timings.TimeoutMs / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s.");
                        break;
                    }
                    if (ping > 0 && now - lastPing >= ping)
                    {
                        lastPing = now;
                        SendAll(Framing.Encode(new NetMessage(NetMessageTypes.Ping).Set("ms", now)));
                    }
                    while (state != LanConnectionState.Closed && outbox.TryDequeue(out byte[] frame)) SendAll(frame);
                }
            }
            catch (SocketException e) { CloseWith(LanCloseReason.Error, "Network error while sending: " + e.Message); }
            catch (ObjectDisposedException) { CloseWith(LanCloseReason.Error, "The connection was shut."); }
            catch (Exception e) { CloseWith(LanCloseReason.Error, "Network error while sending: " + e.Message); }
            TearDown();
        }

        private void SendAll(byte[] frame)
        {
            int sent = 0;
            while (sent < frame.Length) sent += socket.Send(frame, sent, frame.Length - sent, SocketFlags.None);
        }

        /// <summary>Runs once on the writer thread after the close: flush (local close only), shut, wait for the reader, release.</summary>
        private void TearDown()
        {
            bool local = closeReason == LanCloseReason.LocalClose;
            if (local)
            {
                long deadline = NetLog.NowMs + timings.CloseFlushMs;
                try
                {
                    while (NetLog.NowMs < deadline && outbox.TryDequeue(out byte[] frame)) SendAll(frame);
                }
                catch { /* the other side is gone: nothing more to send */ }
            }
            try { socket.Shutdown(SocketShutdown.Send); } catch { }
            // On a local close the reader keeps reading until the other side closes in turn (or the linger time ends).
            if (reader != null) reader.Join(local ? Math.Max(0, timings.CloseLingerMs) : 100);
            try { socket.Close(); } catch { }
            if (reader != null) reader.Join(1000);
            finished.Set();
        }
    }

    /// <summary>Opens a connection to a host.</summary>
    public static class LanClient
    {
        /// <summary>
        /// Starts connecting to address:port (an IPv4 address or a computer name) and returns at once with a connection in the
        /// Connecting state. It becomes Connected, or Closed with reason Error and a detail for the player after
        /// LanTimings.ConnectTimeoutMs. Messages sent meanwhile go out once connected.
        /// </summary>
        public static LanConnection Connect(string address, int port, LanTimings timings = null, Action<string> log = null)
        {
            timings = (timings ?? new LanTimings()).Copy();
            var connection = new LanConnection(timings, log, "host " + address + ":" + port);
            connection.ConnectWorkerStarted();
            var thread = new Thread(() => ConnectWorker(connection, address, port, timings, log)) { IsBackground = true, Name = "LAN connect" };
            thread.Start();
            return connection;
        }

        private static void ConnectWorker(LanConnection connection, string address, int port, LanTimings timings, Action<string> log)
        {
            try { TryConnect(connection, address, port, timings, log); }
            finally { connection.ConnectWorkerEnded(); }
        }

        private static void TryConnect(LanConnection connection, string address, int port, LanTimings timings, Action<string> log)
        {
            string where = address + ":" + port;
            IPAddress ip = Resolve(address);
            if (ip == null)
            {
                connection.FailConnect("Could not find the computer \"" + address + "\" on the network.");
                return;
            }
            NetLog.Write(log, "Connecting to " + ip + ":" + port + "...");
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                if (!connection.TrackConnecting(socket))
                {
                    try { socket.Close(); } catch { }
                    return; // closed here before the attempt began
                }
                IAsyncResult pending = socket.BeginConnect(ip, port, null, null);
                if (!pending.AsyncWaitHandle.WaitOne(Math.Max(1, timings.ConnectTimeoutMs)))
                {
                    try { socket.Close(); } catch { }
                    connection.FailConnect("No answer from " + where + " within " + (timings.ConnectTimeoutMs / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                        + " s. Is the game hosting there, and does Windows Firewall on that PC allow Halobellum?");
                    return;
                }
                socket.EndConnect(pending);
                connection.Attach(socket);
            }
            catch (Exception e)
            {
                try { socket?.Close(); } catch { }
                connection.FailConnect("Could not connect to " + where + ": " + e.Message);
            }
        }

        private static IPAddress Resolve(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            address = address.Trim();
            if (IPAddress.TryParse(address, out IPAddress ip)) return ip.AddressFamily == AddressFamily.InterNetwork ? ip : null;
            try
            {
                foreach (IPAddress candidate in Dns.GetHostAddresses(address))
                    if (candidate.AddressFamily == AddressFamily.InterNetwork) return candidate;
            }
            catch { }
            return null;
        }
    }
}
