// LanTimings.cs
// The times, ports and limits (LanLimits) of the LAN layer in one place, and the logging hook. The defaults are the plan's (a
// ping every second, 10 seconds of silence = lost); tests pass much shorter times so they run in a fraction of a second.
// NetLog writes a line to the game's log hook (an Action<string>) and never lets a failing hook break the network threads.
// Plain C# 9 without the engine.
// Depends on: nothing.

using System;
using System.Diagnostics;

namespace Halobellum.Net
{
    /// <summary>Times of the connection, all in milliseconds. A fresh object with the defaults is made by new LanTimings().</summary>
    public sealed class LanTimings
    {
        /// <summary>How often each side sends a Ping. 0 or less sends none (only for tests).</summary>
        public int PingIntervalMs = 1000;
        /// <summary>Silence (nothing at all received) after which the connection counts as lost.</summary>
        public int TimeoutMs = 10000;
        /// <summary>How long a guest tries to connect before giving up.</summary>
        public int ConnectTimeoutMs = 5000;
        /// <summary>How long each side waits for the other's part of the handshake (Hello, then Welcome or Reject).</summary>
        public int HandshakeTimeoutMs = 10000;
        /// <summary>
        /// Host: how long a guest that connected may take to send its Hello. A real guest sends it the moment it is connected, so
        /// this is short: a connection that sends nothing must not hold the only guest slot for long.
        /// </summary>
        public int HelloTimeoutMs = 3000;
        /// <summary>On a local Close: how long the messages still waiting may take to go out.</summary>
        public int CloseFlushMs = 1000;
        /// <summary>On a local Close: how long to wait for the other side to close in turn, so the last messages are not cut off.</summary>
        public int CloseLingerMs = 1000;
        /// <summary>A single blocking send gives up (and the connection fails) after this long.</summary>
        public int SendTimeoutMs = 10000;

        public LanTimings Copy() => (LanTimings)MemberwiseClone();
    }

    /// <summary>The LAN layer's fixed numbers.</summary>
    public static class LanDefaults
    {
        /// <summary>TCP port the host listens on.</summary>
        public const int GamePort = 47810;
        /// <summary>UDP port of the announcements.</summary>
        public const int DiscoveryPort = 47811;
        /// <summary>Version of the LAN messages; both games must use the same one.</summary>
        public const int Protocol = 1;
        /// <summary>The game name in announcements, so other programs' packets on the same port are ignored.</summary>
        public const string GameName = "Halobellum";
    }

    /// <summary>
    /// The sizes and counts the LAN layer accepts from the other game (and from itself), so a broken or hostile game on the network
    /// can neither make this one use a lot of memory nor freeze its main thread. Real messages are far below every limit: the
    /// largest one measured in whole AI matches is a Ready of about 8.6 KB (a 16-round match at 2000 base health; a MatchStart is
    /// about 150 bytes), and a Ready with 3001 actions in one round is about 770 KB, some 255 bytes an action, so 1 MB holds about
    /// 4100 actions in a round (LanMessageSizeTests). See net_transport.md, "Limits".
    /// </summary>
    public static class LanLimits
    {
        /// <summary>The longest frame (one message's UTF-8 JSON), in bytes. Longer ones are refused when their header arrives.</summary>
        public const int MaxFrameBytes = 1024 * 1024;
        /// <summary>The most JSON values (objects, arrays, strings, numbers, true/false/null) one message may hold.</summary>
        public const int MaxJsonValues = 10000;
        /// <summary>The longest JSON text NetJson reads, and the longest string in it, in characters.</summary>
        public const int MaxJsonChars = MaxFrameBytes;
        /// <summary>A Ping's "ms" is answered only when it is a whole number of at most this many characters.</summary>
        public const int MaxPingDigits = 20;
        /// <summary>Bytes waiting to be sent on one connection; past it the connection closes (the other game does not read).</summary>
        public const int MaxOutboxBytes = 4 * MaxFrameBytes;
        /// <summary>Messages waiting to be sent on one connection; past it the connection closes.</summary>
        public const int MaxOutboxMessages = 4096;
        /// <summary>Messages received and not yet read by the game on one connection; past it the connection closes.</summary>
        public const int MaxInboxMessages = 4096;
        /// <summary>Bytes received and not yet read by the game on one connection; past it the connection closes.</summary>
        public const int MaxInboxBytes = 8 * MaxFrameBytes;
        /// <summary>The most messages one main-thread drain (a coordinator Update, a lobby Update, a Poll) handles; the rest wait a frame.</summary>
        public const int MaxMessagesPerUpdate = 256;
        /// <summary>The longest text from the other game that is echoed back (a Reject or Desync reason) or kept in a reason.</summary>
        public const int MaxReasonChars = 300;
        /// <summary>The longest name, version or message type from the other game that goes into a reason or a log line.</summary>
        public const int MaxNameChars = 64;
        /// <summary>The most games the join list keeps.</summary>
        public const int MaxGamesListed = 32;
        /// <summary>The most refused connections the host lets linger at once (to send their Reject); more are closed at once.</summary>
        public const int MaxRefusing = 8;

        /// <summary>The text, cut to at most max characters (ending "..." when cut); "" for null. Never splits a surrogate pair.</summary>
        public static string Clip(string text, int max = MaxReasonChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= max) return text;
            if (max <= 3) return text.Substring(0, Math.Max(0, max));
            int cut = max - 3;
            if (char.IsHighSurrogate(text[cut - 1])) cut--;
            return text.Substring(0, cut) + "...";
        }
    }

    /// <summary>The logging hook, and a clock in milliseconds.</summary>
    public static class NetLog
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        /// <summary>Milliseconds since the program started, never going back.</summary>
        public static long NowMs => Watch.ElapsedMilliseconds;

        /// <summary>
        /// Writes one line to the hook, prefixed "[LAN] ". It may be called from the network's background threads, so the hook
        /// must be thread-safe (Unity's Debug.Log is). A hook that throws is ignored.
        /// </summary>
        public static void Write(Action<string> log, string text)
        {
            if (log == null) return;
            try { log("[LAN] " + text); }
            catch { /* the log must never break the network */ }
        }
    }
}
