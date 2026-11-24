// LanSession.cs
// The link between the two games once found: it runs the handshake on top of a LanConnection, then passes the game's own
// messages both ways. One side hosts (LanSession.Host, over a started LanHost), the other joins (LanSession.Join).
//   Host:  Waiting (for a guest) -> Handshaking (waiting for its Hello) -> Connected. A guest whose game does not match is sent
//          Reject {reason} and dropped; the host keeps waiting for the next one (LastRefusal says why). Once Connected the
//          host is locked: anyone else is refused politely.
//   Guest: Waiting (connecting) -> Handshaking (Hello sent, waiting) -> Connected, or Refused (Reject received, or the host's
//          Welcome does not match this game), or Lost (could not connect, no answer, connection dropped).
// A Connected session becomes Lost when the connection drops (Reason says why), or Closed after Close here.
// Every step reads "is the connection closed?" BEFORE it empties the connection's inbox: the reader thread puts a message in
// the inbox before it marks the close, so a close seen first means every message that came before it is already there. That
// way the last message before a drop (Leave, a final BattleResult, a Reject) is never left behind.
// Everything runs on the caller's thread: call Poll (TryReceive calls it too) every frame. Nothing here knows about the Match;
// the lockstep coordinator sits on top and uses Send / TryReceive, through ILanChannel (IsOpen: Connected; LostReason: Reason).
// Plain C# 9 without the engine.
// Depends on: LanConnection (LanClient), LanHost, LanHandshake (LanHello), NetMessage, LanTimings, ILanChannel.

using System;
using System.Collections.Generic;

namespace Halobellum.Net
{
    public enum LanSessionState
    {
        /// <summary>Host: no guest yet. Guest: still connecting.</summary>
        Waiting,
        /// <summary>Connected; the Hello / Welcome exchange is under way.</summary>
        Handshaking,
        /// <summary>Both games agreed: application messages flow.</summary>
        Connected,
        /// <summary>Guest only: the host refused (or does not match); Reason says why. Final.</summary>
        Refused,
        /// <summary>The connection could not be made or dropped; Reason says why. Final.</summary>
        Lost,
        /// <summary>Close was called here. Final.</summary>
        Closed,
    }

    public sealed class LanSession : ILanChannel
    {
        private readonly LanHost host;            // host side only
        private readonly LanHello mine;
        private readonly LanTimings timings;
        private readonly Action<string> log;
        private readonly Queue<NetMessage> received = new Queue<NetMessage>();
        private LanConnection connection;
        private long handshakeStartMs = -1;
        private bool wasConnected;                // the session reached Connected at least once

        private LanSession(bool isHost, LanHost host, LanConnection connection, LanHello mine, LanTimings timings, Action<string> log)
        {
            IsHost = isHost;
            this.host = host;
            this.connection = connection;
            this.mine = (mine ?? new LanHello()).Copy();
            this.timings = (timings ?? new LanTimings()).Copy();
            this.log = log;
        }

        /// <summary>
        /// The host's session over a LanHost (started with TryStart, or started here). The session owns the host: Close stops it.
        /// If the host cannot start, the session is Lost at once with the reason.
        /// </summary>
        public static LanSession Host(LanHost host, LanHello mine, Action<string> log = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var session = new LanSession(true, host, null, mine, host.Timings, log ?? host.Log);
            if (!host.TryStart(out string error)) session.End(LanSessionState.Lost, error);
            return session;
        }

        /// <summary>The guest's session: connects to address:port and sends Hello as soon as it is connected.</summary>
        public static LanSession Join(string address, int port, LanHello mine, LanTimings timings = null, Action<string> log = null)
        {
            return Join(LanClient.Connect(address, port, timings, log), mine, timings, log);
        }

        /// <summary>The guest's session over a connection already made or being made.</summary>
        public static LanSession Join(LanConnection connection, LanHello mine, LanTimings timings = null, Action<string> log = null)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            var session = new LanSession(false, null, connection, mine, timings, log);
            connection.Send(session.mine.ToMessage(NetMessageTypes.Hello)); // waits in the queue until connected
            return session;
        }

        public bool IsHost { get; }

        public LanSessionState State { get; private set; } = LanSessionState.Waiting;

        /// <summary>Why the session is Refused, Lost or Closed, in words for the player; "" otherwise.</summary>
        public string Reason { get; private set; } = "";

        /// <summary>Host: why the last guest was refused (for the log or the status line); "" if none was.</summary>
        public string LastRefusal { get; private set; } = "";

        /// <summary>The other game's description once Connected (the guest's Hello, or the host's Welcome).</summary>
        public LanHello Peer { get; private set; }

        public string PeerName => Peer?.Name ?? "";

        /// <summary>The connection in use (null while the host waits for a guest).</summary>
        public LanConnection Connection => connection;

        public bool IsFinished => State == LanSessionState.Refused || State == LanSessionState.Lost || State == LanSessionState.Closed;

        /// <summary>ILanChannel: true while Connected (as of the last Poll; TryReceive polls).</summary>
        public bool IsOpen => State == LanSessionState.Connected;

        /// <summary>ILanChannel: why the session ended (Reason once Refused, Lost or Closed), "" before that.</summary>
        public string LostReason => IsFinished ? Reason : "";

        /// <summary>Sends an application message. False unless Connected.</summary>
        public bool Send(JsonObject message)
        {
            if (State != LanSessionState.Connected || connection == null) return false;
            return connection.Send(message);
        }

        /// <summary>The next application message, if any (runs Poll first). Messages that came before a drop can still be read.</summary>
        public bool TryReceive(out NetMessage message)
        {
            Poll();
            // After the session ended, anything that still reached the connection's inbox is handed on too.
            if (received.Count == 0 && IsFinished && wasConnected && connection != null)
                while (connection.TryReceive(out NetMessage late)) received.Enqueue(late);
            if (received.Count > 0)
            {
                message = received.Dequeue();
                return true;
            }
            message = null;
            return false;
        }

        /// <summary>Moves the session on: takes a guest, answers the handshake, collects messages, notices a drop.</summary>
        public void Poll()
        {
            if (IsFinished) return;
            if (IsHost) PollHost();
            else PollGuest();
        }

        /// <summary>Ends the session here: closes the connection (and stops the host). Safe to call more than once.</summary>
        public void Close()
        {
            if (!IsFinished) End(LanSessionState.Closed, "Closed by this game.");
            connection?.Close();
            host?.Stop();
        }

        private void PollHost()
        {
            if (State == LanSessionState.Waiting)
            {
                if (!host.TryAcceptGuest(out LanConnection guest)) return;
                connection = guest;
                handshakeStartMs = NetLog.NowMs;
                State = LanSessionState.Handshaking;
                NetLog.Write(log, "A guest connected from " + guest.RemoteAddress + "; waiting for its Hello.");
            }

            if (State == LanSessionState.Handshaking)
            {
                // A guest that already left is not taken, even with its Hello still in the inbox: taking it would lock the host
                // for a player who is gone.
                if (connection.State == LanConnectionState.Closed)
                {
                    GuestLeft("The guest left before the handshake ended: ");
                    return;
                }
                while (State == LanSessionState.Handshaking && connection.TryReceive(out NetMessage m))
                {
                    if (m.Type != NetMessageTypes.Hello)
                    {
                        DropGuest("it sent " + m.Type + " before its Hello", "The host expected a Hello first.");
                        return;
                    }
                    LanHello theirs = LanHello.FromMessage(m);
                    string hereReason = LanHello.Mismatch(mine, theirs);
                    if (hereReason != null)
                    {
                        // The guest reads the reason from its own point of view ("The other game is version ..." = the host).
                        DropGuest(hereReason, LanHello.Mismatch(theirs, mine));
                        return;
                    }
                    if (!connection.Send(mine.ToMessage(NetMessageTypes.Welcome)) || connection.State == LanConnectionState.Closed)
                    {
                        GuestLeft("The guest left right after its Hello: ");
                        return;
                    }
                    Peer = theirs;
                    host.Lock();
                    State = LanSessionState.Connected;
                    wasConnected = true;
                    NetLog.Write(log, "Handshake done: " + PeerName + " joined (version " + theirs.Version + ").");
                }
                if (State == LanSessionState.Handshaking)
                {
                    if (NetLog.NowMs - handshakeStartMs > timings.HandshakeTimeoutMs)
                    {
                        DropGuest("no Hello within " + timings.HandshakeTimeoutMs + " ms", "The host got no Hello in time.");
                    }
                    return;
                }
            }

            if (State == LanSessionState.Connected) PumpConnected();
        }

        private void PollGuest()
        {
            if (State == LanSessionState.Waiting)
            {
                if (connection.State == LanConnectionState.Connecting) return;
                if (connection.State == LanConnectionState.Closed && connection.Pending == 0)
                {
                    End(LanSessionState.Lost, connection.CloseDetail);
                    return;
                }
                handshakeStartMs = NetLog.NowMs;
                State = LanSessionState.Handshaking;
            }

            if (State == LanSessionState.Handshaking)
            {
                bool closed = connection.State == LanConnectionState.Closed; // read before the inbox (see the top of the file)
                while (State == LanSessionState.Handshaking && connection.TryReceive(out NetMessage m))
                {
                    if (m.Type == NetMessageTypes.Reject)
                    {
                        string reason = m.GetString("reason", "The host refused the connection.");
                        End(LanSessionState.Refused, reason);
                        connection.Close();
                        return;
                    }
                    if (m.Type != NetMessageTypes.Welcome)
                    {
                        End(LanSessionState.Lost, "The host sent " + m.Type + " instead of an answer to the Hello.");
                        connection.Close();
                        return;
                    }
                    LanHello theirs = LanHello.FromMessage(m);
                    // The host checked already; a Welcome that carries the host's description is checked here too.
                    string reasonHere = m.Has("proto") ? LanHello.Mismatch(mine, theirs) : null;
                    if (reasonHere != null)
                    {
                        End(LanSessionState.Refused, reasonHere);
                        connection.Close();
                        return;
                    }
                    Peer = theirs;
                    State = LanSessionState.Connected;
                    wasConnected = true;
                    NetLog.Write(log, "Handshake done: joined " + PeerName + " (version " + theirs.Version + ").");
                }
                if (State == LanSessionState.Handshaking)
                {
                    if (closed)
                        End(LanSessionState.Lost, "The host closed the connection before answering. " + connection.CloseDetail);
                    else if (NetLog.NowMs - handshakeStartMs > timings.HandshakeTimeoutMs)
                    {
                        End(LanSessionState.Lost, "The host did not answer.");
                        connection.Close();
                    }
                    return;
                }
            }

            if (State == LanSessionState.Connected) PumpConnected();
        }

        /// <summary>Connected: hands the messages on, then notices a closed connection.</summary>
        private void PumpConnected()
        {
            bool closed = connection.State == LanConnectionState.Closed; // read before the inbox (see the top of the file)
            while (connection.TryReceive(out NetMessage m)) received.Enqueue(m);
            if (closed)
            {
                if (connection.CloseReason == LanCloseReason.LocalClose) End(LanSessionState.Closed, "Closed by this game.");
                else End(LanSessionState.Lost, connection.CloseDetail);
            }
        }

        /// <summary>Host: the guest's connection closed during the handshake; wait for the next guest without locking.</summary>
        private void GuestLeft(string what)
        {
            NetLog.Write(log, what + connection.CloseDetail);
            connection.Close();
            connection = null;
            Peer = null;
            State = LanSessionState.Waiting;
        }

        /// <summary>Host: refuses the guest in the handshake, then waits for the next one.</summary>
        private void DropGuest(string hereReason, string theirReason)
        {
            LastRefusal = hereReason;
            NetLog.Write(log, "Refused the guest: " + hereReason);
            connection.Send(new NetMessage(NetMessageTypes.Reject).Set("reason", theirReason ?? hereReason));
            connection.Close();
            connection = null;
            State = LanSessionState.Waiting;
        }

        private void End(LanSessionState state, string reason)
        {
            State = state;
            Reason = reason ?? "";
            NetLog.Write(log, "Session " + state + (Reason.Length > 0 ? ": " + Reason : "."));
        }
    }
}
