// LanTimings.cs
// The times and ports of the LAN layer in one place, and the logging hook. The defaults are the plan's (a ping every second,
// 10 seconds of silence = lost); tests pass much shorter times so they run in a fraction of a second.
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
