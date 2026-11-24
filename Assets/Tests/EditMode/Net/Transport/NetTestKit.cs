// NetTestKit.cs
// Helpers for the LAN transport tests: short timings, waiting for a condition with a time limit (no test can hang), and a host
// on the loopback address with a port the system picks (no firewall prompt).
// Depends on: Halobellum.Net (transport), NUnit.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    internal static class NetTestKit
    {
        /// <summary>Every wait in these tests gives up after this long.</summary>
        public const int WaitMs = 5000;

        /// <summary>Short times, but a timeout long enough that a busy test machine does not drop a healthy connection.</summary>
        public static LanTimings Fast()
        {
            return new LanTimings
            {
                PingIntervalMs = 50,
                TimeoutMs = 3000,
                ConnectTimeoutMs = 3000,
                HandshakeTimeoutMs = 3000,
                CloseFlushMs = 500,
                CloseLingerMs = 300,
                SendTimeoutMs = 3000,
            };
        }

        /// <summary>Waits until the condition holds; false if it still does not after timeoutMs.</summary>
        public static bool Until(Func<bool> condition, int timeoutMs = WaitMs)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                Thread.Sleep(5);
            }
            return condition();
        }

        /// <summary>The next message on a connection; fails the test after the time limit.</summary>
        public static NetMessage Receive(LanConnection connection, int timeoutMs = WaitMs)
        {
            NetMessage message = null;
            Assert.IsTrue(Until(() => connection.TryReceive(out message), timeoutMs), "No message arrived in time on " + connection.Label
                + " (state " + connection.State + ", " + connection.CloseDetail + ").");
            return message;
        }

        /// <summary>The next application message of a session (polled); fails the test after the time limit.</summary>
        public static NetMessage Receive(LanSession session, int timeoutMs = WaitMs)
        {
            NetMessage message = null;
            Assert.IsTrue(Until(() => session.TryReceive(out message), timeoutMs), "No message arrived in time (session " + session.State + ").");
            return message;
        }

        /// <summary>A started host on 127.0.0.1 with a free port.</summary>
        public static LanHost StartHost(LanTimings timings = null)
        {
            var host = new LanHost(0, IPAddress.Loopback, timings ?? Fast());
            Assert.IsTrue(host.TryStart(out string error), error);
            Assert.Greater(host.Port, 0);
            return host;
        }

        /// <summary>A guest connection to the host, and the host's side of it.</summary>
        public static LanConnection Connect(LanHost host, out LanConnection hostSide, LanTimings timings = null)
        {
            LanConnection client = LanClient.Connect("127.0.0.1", host.Port, timings ?? Fast());
            LanConnection taken = null;
            Assert.IsTrue(Until(() => host.TryAcceptGuest(out taken)), "The host took no guest.");
            Assert.IsTrue(Until(() => client.State == LanConnectionState.Connected), "The client did not connect: " + client.CloseDetail);
            hostSide = taken;
            return client;
        }

        public static LanHello Hello(string name, string version = "0.416.2", string content = "units=aa;cards=bb;rules=cc")
        {
            return new LanHello { Name = name, Version = version, Code = "code1234abcd", Content = content };
        }
    }
}
