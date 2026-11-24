// LanHost.cs
// The host's side of the connection: listens on a TCP port (47810 unless told otherwise) and takes one guest at a time.
// Anyone else who connects while a guest is there, or after Lock (the match has started), gets a polite Reject message with
// a reason and is disconnected: "already has a player" after Lock, "another player is joining" while the guest's handshake is
// still going (the session drops a guest that sends no Hello within LanTimings.HelloTimeoutMs, so that never lasts long).
// A refused connection costs no thread of its own: the Reject is written on the accept thread, and a short task on the thread
// pool waits (up to CloseLingerMs) for the other side to close before the socket is released. At most LanLimits.MaxRefusing
// wait at once; more are closed at once. When the guest's connection closes before Lock (a refused handshake, a guest who left), the
// next guest is taken. The game picks up a newly taken guest with TryAcceptGuest (polled, never blocks).
// Stop closes the listener and the guest's connection.
// An accept that fails for a passing reason (a guest that connected and reset at once, a port scan, no buffers or sockets for a
// moment) is logged and the host goes on accepting, after a short back-off. Any other failure ends the accept thread for good:
// Failed is then true (FailReason says why), IsListening false, and the lobby starts a fresh host.
// Plain C# 9 without the engine; System.Net.Sockets only.
// Depends on: LanConnection, Framing, NetMessage, LanTimings (LanLimits).

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Halobellum.Net
{
    public sealed class LanHost
    {
        /// <summary>The Reject reason given to a second guest once the host has its player (after Lock).</summary>
        public const string BusyReason = "This game already has a player.";

        /// <summary>The Reject reason given to a guest that connects while another one is still in its handshake.</summary>
        public const string JoiningReason = "Another player is joining this game right now. Try again in a few seconds.";

        private readonly object gate = new object();
        private readonly int requestedPort;
        private readonly IPAddress bindAddress;
        private readonly LanTimings timings;
        private readonly Action<string> log;
        private readonly ConcurrentQueue<LanConnection> taken = new ConcurrentQueue<LanConnection>();
        private Socket listener;
        private Thread acceptThread;
        private LanConnection current;
        private volatile bool locked;
        private volatile bool stopped;
        private volatile bool failed;
        private volatile string failReason = "";
        private int refusing; // refused connections still waiting for the other side's close (LanLimits.MaxRefusing)

        /// <summary>The longest back-off between two accepts that failed for a passing reason, in milliseconds.</summary>
        public const int MaxAcceptBackOffMs = 1000;

        /// <param name="port">TCP port; 0 lets the system pick a free one (tests), read it back from Port after TryStart.</param>
        /// <param name="bindAddress">Address to listen on; null = every network card. Tests use IPAddress.Loopback (no firewall prompt).</param>
        public LanHost(int port = LanDefaults.GamePort, IPAddress bindAddress = null, LanTimings timings = null, Action<string> log = null)
        {
            requestedPort = port;
            this.bindAddress = bindAddress ?? IPAddress.Any;
            this.timings = (timings ?? new LanTimings()).Copy();
            this.log = log;
        }

        /// <summary>The port actually listened on (after TryStart), else the one asked for.</summary>
        public int Port { get; private set; }

        public bool IsListening => listener != null && !stopped && !failed;

        /// <summary>True once the accept thread ended by itself (not by Stop): no new guest can come. FailReason says why.</summary>
        public bool Failed => failed;

        /// <summary>Why the accept thread ended by itself; "" while it runs.</summary>
        public string FailReason => failReason;

        /// <summary>The accept errors that pass by themselves: accepting goes on after a short back-off.</summary>
        public static bool IsPassingAcceptError(SocketError error) =>
            error == SocketError.ConnectionReset || error == SocketError.ConnectionAborted || error == SocketError.NoBufferSpaceAvailable
            || error == SocketError.TooManyOpenSockets || error == SocketError.Interrupted || error == SocketError.TryAgain
            || error == SocketError.WouldBlock || error == SocketError.NetworkReset;

        /// <summary>Refused connections still waiting for the other side to close (tests).</summary>
        public int Refusing => Volatile.Read(ref refusing);

        /// <summary>The number of accepts that failed for a passing reason so far (tests).</summary>
        public int PassingAcceptErrors => Volatile.Read(ref passingAcceptErrors);

        private int passingAcceptErrors;

        /// <summary>Tests only: called on the accept thread before each Accept; it may throw to act out an accept error. Set it before TryStart.</summary>
        public Action BeforeAccept { get; set; }

        /// <summary>True once Lock was called: every new guest is refused.</summary>
        public bool IsLocked => locked;

        /// <summary>Timings given to the guests' connections.</summary>
        public LanTimings Timings => timings;

        /// <summary>The log hook this host was made with.</summary>
        public Action<string> Log => log;

        /// <summary>Starts listening. False with a reason for the player when the port cannot be used (another game hosting...).</summary>
        public bool TryStart(out string error)
        {
            error = null;
            lock (gate)
            {
                if (stopped) { error = "This host was stopped."; return false; }
                if (listener != null) return true;
                Socket socket = null;
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Bind(new IPEndPoint(bindAddress, requestedPort));
                    socket.Listen(4);
                    Port = ((IPEndPoint)socket.LocalEndPoint).Port;
                }
                catch (Exception e)
                {
                    try { socket?.Close(); } catch { }
                    error = "Could not listen on port " + requestedPort + ": " + e.Message;
                    NetLog.Write(log, error);
                    return false;
                }
                listener = socket;
                acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "LAN accept" };
                acceptThread.Start();
            }
            NetLog.Write(log, "Hosting on port " + Port + ".");
            return true;
        }

        /// <summary>A guest taken since the last call, if any. The connection is already Connected (or closed again).</summary>
        public bool TryAcceptGuest(out LanConnection guest) => taken.TryDequeue(out guest);

        /// <summary>The guest being served now (null before the first one).</summary>
        public LanConnection CurrentGuest
        {
            get { lock (gate) return current; }
        }

        /// <summary>Refuses every new guest from now on (call once the match has its player).</summary>
        public void Lock() => locked = true;

        /// <summary>Stops listening and closes the guest's connection. Safe to call more than once.</summary>
        public void Stop()
        {
            Socket socket;
            LanConnection guest;
            lock (gate)
            {
                if (stopped) return;
                stopped = true;
                socket = listener;
                guest = current;
            }
            try { socket?.Close(); } catch { }
            guest?.Close();
            while (taken.TryDequeue(out LanConnection waiting)) waiting.Close();
            NetLog.Write(log, "Stopped hosting.");
        }

        private void AcceptLoop()
        {
            int backOffMs = 0;
            while (!stopped)
            {
                Socket incoming;
                try
                {
                    BeforeAccept?.Invoke();
                    incoming = listener.Accept();
                    backOffMs = 0;
                }
                catch (SocketException e) when (!stopped && IsPassingAcceptError(e.SocketErrorCode))
                {
                    Interlocked.Increment(ref passingAcceptErrors);
                    backOffMs = Math.Min(MaxAcceptBackOffMs, backOffMs == 0 ? 50 : backOffMs * 2);
                    NetLog.Write(log, "An incoming connection failed (" + e.SocketErrorCode + "); still accepting guests.");
                    Thread.Sleep(backOffMs);
                    continue;
                }
                catch (Exception e)
                {
                    if (!stopped)
                    {
                        failReason = e.Message;
                        failed = true;
                        NetLog.Write(log, "Stopped accepting guests: " + e.Message);
                    }
                    return;
                }
                try
                {
                    Serve(incoming);
                }
                catch (Exception e)
                {
                    // One guest's connection could not be set up (no thread for it...): drop it and keep accepting.
                    NetLog.Write(log, "Could not take an incoming connection: " + e.Message);
                    try { incoming.Close(); } catch { }
                }
            }
        }

        /// <summary>Takes the new connection as the guest, or refuses it politely when there is one already (or after Lock).</summary>
        private void Serve(Socket incoming)
        {
            string from = SafeRemote(incoming);
            LanConnection guest = null;
            string reason = null;
            lock (gate)
            {
                if (stopped || locked) reason = BusyReason;
                else if (current != null && current.State != LanConnectionState.Closed) reason = JoiningReason;
                else
                {
                    guest = new LanConnection(incoming, timings, log, "guest " + from);
                    current = guest;
                }
            }
            if (guest != null)
            {
                taken.Enqueue(guest);
                return;
            }
            NetLog.Write(log, "Refused a second guest from " + from + ": " + reason);
            Refuse(incoming, reason);
        }

        /// <summary>
        /// Sends a Reject on a connection that is not taken, then lets it close cleanly: the sending side is shut at once, and a task
        /// on the thread pool reads (and drops) what the other side still sends until it closes in turn, or CloseLingerMs passes,
        /// so the Reject is not cut off by a reset. No thread of its own; with MaxRefusing waiting already, the socket closes at once.
        /// </summary>
        private void Refuse(Socket incoming, string reason)
        {
            // Only the accept thread adds to the count, so reading it first never lets it go past the limit.
            if (Volatile.Read(ref refusing) >= LanLimits.MaxRefusing)
            {
                try { incoming.Close(); } catch { }
                return;
            }
            Interlocked.Increment(ref refusing);
            bool lingering = false;
            try
            {
                byte[] frame = Framing.Encode(new NetMessage(NetMessageTypes.Reject).Set("reason", reason));
                incoming.SendTimeout = Math.Max(1, Math.Min(1000, timings.SendTimeoutMs)); // a few bytes into a fresh socket's buffer
                int sent = 0;
                while (sent < frame.Length) sent += incoming.Send(frame, sent, frame.Length - sent, SocketFlags.None);
                incoming.Shutdown(SocketShutdown.Send);
                lingering = ThreadPool.QueueUserWorkItem(_ => Linger(incoming));
            }
            catch { /* the other side is gone already */ }
            finally
            {
                if (!lingering)
                {
                    try { incoming.Close(); } catch { }
                    Interlocked.Decrement(ref refusing);
                }
            }
        }

        /// <summary>Thread pool: waits for a refused connection's other side to close (dropping what it sends), then releases it.</summary>
        private void Linger(Socket socket)
        {
            try
            {
                var scratch = new byte[1024];
                long deadline = NetLog.NowMs + Math.Max(0, timings.CloseLingerMs);
                while (!stopped)
                {
                    long left = deadline - NetLog.NowMs;
                    if (left <= 0) break;
                    if (!socket.Poll((int)Math.Min(left, 100) * 1000, SelectMode.SelectRead)) continue;
                    if (socket.Receive(scratch) == 0) break; // the other side closed
                }
            }
            catch { /* reset or shut: done either way */ }
            finally
            {
                try { socket.Close(); } catch { }
                Interlocked.Decrement(ref refusing);
            }
        }

        private static string SafeRemote(Socket socket)
        {
            try { return socket.RemoteEndPoint?.ToString() ?? "?"; }
            catch { return "?"; }
        }
    }
}
