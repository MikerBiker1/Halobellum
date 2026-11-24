// ILanChannel.cs
// The pipe the lockstep coordinator talks through: send a message, take the next one, and know whether the pipe is still open
// (and if not, why). A real LAN game uses LanSession (the TCP connection after the handshake); the tests use LanMemoryChannel,
// a pair of channels that hand messages to each other in memory, through their JSON text as the real wire does.
// Everything is meant for one thread (the game's main thread), but LanMemoryChannel locks, so a test may use it from two.
// Plain C# 9 without the engine.
// Depends on: NetMessage, NetJson (JsonObject).

using System.Collections.Generic;

namespace Halobellum.Net
{
    /// <summary>A two-way message pipe between this game and the other one.</summary>
    public interface ILanChannel
    {
        /// <summary>Sends a message to the other game. False once the channel is no longer open.</summary>
        bool Send(JsonObject message);

        /// <summary>
        /// The next message from the other game, if any. Messages that arrived before the channel closed can still be read after it
        /// closed, so the last message before a drop (a Leave, a final BattleResult) is never lost.
        /// </summary>
        bool TryReceive(out NetMessage message);

        /// <summary>True while messages can go both ways.</summary>
        bool IsOpen { get; }

        /// <summary>Why the channel is no longer open, in words for the player; "" while it is open.</summary>
        string LostReason { get; }

        /// <summary>Closes the channel here (the other side sees it closed). Safe to call more than once.</summary>
        void Close();
    }

    /// <summary>
    /// One end of an in-memory channel pair (tests). Each message goes across as its JSON text and is read back, as on the wire.
    /// Close on one end closes both: this end says "Closed by this game.", the other "The other game closed the connection.".
    /// Drop(reason) closes both ends with the same reason, as a pulled cable would.
    /// </summary>
    public sealed class LanMemoryChannel : ILanChannel
    {
        public const string ClosedHere = "Closed by this game.";
        public const string ClosedThere = "The other game closed the connection.";

        private readonly object gate;
        private readonly Queue<string> inbox = new Queue<string>();
        private LanMemoryChannel other;
        private bool closed;
        private string reason = "";

        private LanMemoryChannel(object gate)
        {
            this.gate = gate;
        }

        /// <summary>Two connected ends: what one sends, the other receives.</summary>
        public static void CreatePair(out LanMemoryChannel a, out LanMemoryChannel b)
        {
            var gate = new object();
            a = new LanMemoryChannel(gate);
            b = new LanMemoryChannel(gate);
            a.other = b;
            b.other = a;
        }

        /// <summary>How many messages this end has sent (tests).</summary>
        public int SentCount { get; private set; }

        /// <summary>Messages waiting to be read at this end.</summary>
        public int Pending
        {
            get { lock (gate) return inbox.Count; }
        }

        public bool IsOpen
        {
            get { lock (gate) return !closed; }
        }

        public string LostReason
        {
            get { lock (gate) return closed ? reason : ""; }
        }

        public bool Send(JsonObject message)
        {
            if (message == null) return false;
            string text = message.ToJson();
            lock (gate)
            {
                if (closed) return false;
                other.inbox.Enqueue(text);
                SentCount++;
                return true;
            }
        }

        public bool TryReceive(out NetMessage message)
        {
            string text;
            lock (gate)
            {
                if (inbox.Count == 0)
                {
                    message = null;
                    return false;
                }
                text = inbox.Dequeue();
            }
            message = NetMessage.Parse(text);
            return true;
        }

        public void Close()
        {
            lock (gate)
            {
                if (closed) return;
                End(ClosedHere, ClosedThere);
            }
        }

        /// <summary>Closes both ends at once with the same reason (a pulled cable, a crashed game).</summary>
        public void Drop(string why = "The connection dropped.")
        {
            lock (gate)
            {
                if (closed) return;
                End(why, why);
            }
        }

        private void End(string here, string there)
        {
            closed = true;
            reason = here;
            if (!other.closed)
            {
                other.closed = true;
                other.reason = there;
            }
        }
    }
}
