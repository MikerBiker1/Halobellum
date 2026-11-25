// LanBufferedChannel.cs
// A channel with messages put back in front of it. The guest's lobby reads the host's MatchStart in the menu (that is how it knows the
// host pressed Start), then hands the session to the Match scene with that message put back, so LanGuestStart reads it there as if it
// had never been taken. After a match, the guest's coordinator does the same with the host's next MatchStart (New game).
// Plain C# 9 without the engine.
// Depends on: ILanChannel, NetMessage.

using System;
using System.Collections.Generic;

namespace Halobellum.Net
{
    public sealed class LanBufferedChannel : ILanChannel
    {
        private readonly Queue<NetMessage> front = new Queue<NetMessage>();

        public LanBufferedChannel(ILanChannel inner, IEnumerable<NetMessage> putBack = null)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (putBack != null)
                foreach (NetMessage m in putBack) front.Enqueue(m);
        }

        /// <summary>
        /// <paramref name="channel"/> with these messages put back in front. Buffered channels with nothing left to give back are
        /// unwrapped first, so a connection used for match after match (New game) does not grow a chain of wrappers.
        /// </summary>
        public static LanBufferedChannel PutBackInFront(ILanChannel channel, params NetMessage[] putBack)
        {
            while (channel is LanBufferedChannel buffered && buffered.PutBack == 0) channel = buffered.Inner;
            return new LanBufferedChannel(channel, putBack);
        }

        /// <summary>The channel underneath (a LanSession in the game).</summary>
        public ILanChannel Inner { get; }

        /// <summary>Messages put back and not read yet.</summary>
        public int PutBack => front.Count;

        public bool Send(JsonObject message) => Inner.Send(message);

        /// <summary>The messages put back first, in order, then the inner channel's.</summary>
        public bool TryReceive(out NetMessage message)
        {
            if (front.Count > 0)
            {
                message = front.Dequeue();
                return true;
            }
            return Inner.TryReceive(out message);
        }

        public bool IsOpen => Inner.IsOpen;

        public string LostReason => Inner.LostReason;

        public void Close() => Inner.Close();
    }
}
