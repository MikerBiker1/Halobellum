// LanHost.cs
// The host's side of the connection: listens on a TCP port (47810 unless told otherwise) and takes one guest at a time.
// Anyone else who connects while a guest is there, or after Lock (the match has started), gets a polite Reject message with
// a reason and is disconnected. When the guest's connection closes before Lock (a refused handshake, a guest who left), the
// next guest is taken. The game picks up a newly taken guest with TryAcceptGuest (polled, never blocks).
// Stop closes the listener and the guest's connection.
// Plain C# 9 without the engine; System.Net.Sockets only.
// Depends on: LanConnection, NetMessage, LanTimings.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Halobellum.Net
{
    public sealed class LanHost
    {
        /// <summary>The Reject reason given to a second guest.</summary>
        public const string BusyReason = "This game already has a player.";

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

        public bool IsListening => listener != null && !stopped;

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
            while (!stopped)
            {
                Socket incoming;
                try { incoming = listener.Accept(); }
                catch (Exception e)
                {
                    if (!stopped) NetLog.Write(log, "Stopped accepting guests: " + e.Message);
                    return;
                }
                string from = SafeRemote(incoming);
                LanConnection guest = null;
                lock (gate)
                {
                    bool busy = stopped || locked || (current != null && current.State != LanConnectionState.Closed);
                    if (!busy)
                    {
                        guest = new LanConnection(incoming, timings, log, "guest " + from);
                        current = guest;
                    }
                }
                if (guest != null)
                {
                    taken.Enqueue(guest);
                    continue;
                }
                NetLog.Write(log, "Refused a second guest from " + from + ".");
                // A short-lived connection sends the Reject, then closes cleanly so the message is not cut off.
                var refused = new LanConnection(incoming, timings, null, "refused " + from);
                refused.Send(new NetMessage(NetMessageTypes.Reject).Set("reason", BusyReason));
                refused.Close();
            }
        }

        private static string SafeRemote(Socket socket)
        {
            try { return socket.RemoteEndPoint?.ToString() ?? "?"; }
            catch { return "?"; }
        }
    }
}
