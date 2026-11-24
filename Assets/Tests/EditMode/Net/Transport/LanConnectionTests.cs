// LanConnectionTests.cs
// Tests the TCP connection over real loopback sockets: host and guest connect and exchange messages both ways (a burst of small
// ones and a half-megabyte one that the stream splits across many reads), a message sent while still connecting goes out, a second
// guest is refused with a Reject ("another player is joining" before Lock, "already has a player" after it), a close on the other side is noticed (and the last message before it still arrives),
// silence is noticed by the heartbeat, pings keep a quiet connection alive, a frame over the limit closes the connection,
// Close can be called any number of times, and a Close while still connecting stops the attempt before WaitClosed returns.
// An accept that fails for a passing reason (a connection reset before it was taken) keeps the host accepting; one that fails for
// good says so (Failed).
// Depends on: Halobellum.Net (LanHost, LanClient, LanConnection), NetTestKit, NUnit.

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanConnectionTests
    {
        [Test]
        public void HostAndClient_ExchangeMessages_BothWays()
        {
            LanHost host = NetTestKit.StartHost();
            try
            {
                LanConnection client = NetTestKit.Connect(host, out LanConnection hostSide);
                Assert.AreEqual(LanConnectionState.Connected, hostSide.State);
                Assert.That(client.RemoteAddress, Does.StartWith("127.0.0.1:"));

                client.Send(new NetMessage("Hi").Set("from", "guest").Set("n", 1));
                NetMessage atHost = NetTestKit.Receive(hostSide);
                Assert.AreEqual("Hi", atHost.Type);
                Assert.AreEqual("guest", atHost.GetString("from"));

                hostSide.Send(new NetMessage("Hi").Set("from", "host"));
                Assert.AreEqual("host", NetTestKit.Receive(client).GetString("from"));

                // A burst of small messages arrives complete and in order (many share one read).
                for (int i = 0; i < 500; i++) hostSide.Send(new NetMessage("N").Set("i", i));
                for (int i = 0; i < 500; i++) Assert.AreEqual(i, NetTestKit.Receive(client).GetInt("i", -1));

                // A half-megabyte message (the frame limit is 1 MB) is split by the stream into many reads and joined again.
                string big = new string('q', 512 * 1024) + "é";
                client.Send(new NetMessage("Big").Set("s", big));
                Assert.AreEqual(big, NetTestKit.Receive(hostSide).GetString("s"));

                // Pings and Pongs never reach the inbox, but give a round trip time.
                Assert.IsTrue(NetTestKit.Until(() => client.RoundTripMs >= 0 && hostSide.RoundTripMs >= 0), "No Pong came back.");
                Assert.IsFalse(client.TryReceive(out _));

                client.Close();
            }
            finally { host.Stop(); }
        }

        [Test]
        public void AnAcceptThatFailsForAPassingReason_KeepsTheHostAccepting()
        {
            // As Windows does when a guest connects and resets before the accept (WSAECONNRESET), or for a moment without buffers.
            int failures = 2;
            var host = new LanHost(0, IPAddress.Loopback, NetTestKit.Fast())
            {
                BeforeAccept = () => { if (Interlocked.Decrement(ref failures) >= 0) throw new SocketException((int)SocketError.ConnectionReset); },
            };
            Assert.IsTrue(host.TryStart(out string error), error);
            try
            {
                LanConnection client = NetTestKit.Connect(host, out LanConnection hostSide);
                Assert.AreEqual(2, host.PassingAcceptErrors);
                Assert.IsFalse(host.Failed);
                Assert.IsTrue(host.IsListening);
                client.Close();
                hostSide.Close();
            }
            finally { host.Stop(); }
            Assert.IsFalse(host.Failed, "Stop is not a failure.");
        }

        [Test]
        public void AnAcceptThatFailsForGood_SaysSo()
        {
            var host = new LanHost(0, IPAddress.Loopback, NetTestKit.Fast()) { BeforeAccept = () => throw new SocketException((int)SocketError.AccessDenied) };
            Assert.IsTrue(host.TryStart(out string error), error);
            try
            {
                Assert.IsTrue(NetTestKit.Until(() => host.Failed), "The host says it stopped accepting.");
                Assert.IsFalse(host.IsListening);
                Assert.Greater(host.FailReason.Length, 0);
                Assert.IsTrue(LanHost.IsPassingAcceptError(SocketError.ConnectionReset));
                Assert.IsFalse(LanHost.IsPassingAcceptError(SocketError.AccessDenied));
            }
            finally { host.Stop(); }
        }

        [Test]
        public void MessageSentWhileConnecting_GoesOutOnceConnected()
        {
            LanHost host = NetTestKit.StartHost();
            try
            {
                LanConnection client = LanClient.Connect("127.0.0.1", host.Port, NetTestKit.Fast());
                Assert.IsTrue(client.Send(new NetMessage("Early")));
                LanConnection hostSide = null;
                Assert.IsTrue(NetTestKit.Until(() => host.TryAcceptGuest(out hostSide)));
                Assert.AreEqual("Early", NetTestKit.Receive(hostSide).Type);
                client.Close();
            }
            finally { host.Stop(); }
        }

        [Test]
        public void SecondGuest_IsRefusedPolitely()
        {
            LanHost host = NetTestKit.StartHost();
            try
            {
                LanConnection first = NetTestKit.Connect(host, out LanConnection firstAtHost);

                LanConnection second = LanClient.Connect("127.0.0.1", host.Port, NetTestKit.Fast());
                NetMessage reject = NetTestKit.Receive(second);
                Assert.AreEqual(NetMessageTypes.Reject, reject.Type);
                Assert.AreEqual(LanHost.JoiningReason, reject.GetString("reason"), "The first guest has not finished a handshake (no Lock yet).");
                Assert.IsTrue(NetTestKit.Until(() => second.State == LanConnectionState.Closed), "The refused guest stayed connected.");
                Assert.AreEqual(LanCloseReason.RemoteClosed, second.CloseReason);
                Assert.IsFalse(host.TryAcceptGuest(out _), "The host took the second guest.");

                // The first guest is unharmed.
                first.Send(new NetMessage("StillHere"));
                Assert.AreEqual("StillHere", NetTestKit.Receive(firstAtHost).Type);

                // Once the first guest has left (and the host is not locked), the next one is taken.
                first.Close();
                Assert.IsTrue(NetTestKit.Until(() => firstAtHost.State == LanConnectionState.Closed));
                LanConnection third = NetTestKit.Connect(host, out LanConnection thirdAtHost);
                Assert.AreEqual(LanConnectionState.Connected, thirdAtHost.State);

                // After Lock everyone new is refused, even with the slot free.
                host.Lock();
                third.Close();
                Assert.IsTrue(NetTestKit.Until(() => thirdAtHost.State == LanConnectionState.Closed));
                LanConnection fourth = LanClient.Connect("127.0.0.1", host.Port, NetTestKit.Fast());
                NetMessage locked = NetTestKit.Receive(fourth);
                Assert.AreEqual(NetMessageTypes.Reject, locked.Type);
                Assert.AreEqual(LanHost.BusyReason, locked.GetString("reason"));
            }
            finally { host.Stop(); }
        }

        [Test]
        public void RemoteClose_IsNoticed_AndTheLastMessageStillArrives()
        {
            LanHost host = NetTestKit.StartHost();
            try
            {
                LanConnection client = NetTestKit.Connect(host, out LanConnection hostSide);
                hostSide.Send(new NetMessage("Bye").Set("why", "done"));
                hostSide.Close();

                Assert.IsTrue(NetTestKit.Until(() => client.State == LanConnectionState.Closed), "The close was not noticed.");
                Assert.AreEqual(LanCloseReason.RemoteClosed, client.CloseReason);
                Assert.IsNotEmpty(client.CloseDetail);
                NetMessage last = NetTestKit.Receive(client); // still readable after the close
                Assert.AreEqual("Bye", last.Type);
                Assert.AreEqual(LanCloseReason.LocalClose, hostSide.CloseReason);
                Assert.IsTrue(hostSide.WaitClosed(NetTestKit.WaitMs));
                Assert.IsTrue(client.WaitClosed(NetTestKit.WaitMs));
                Assert.IsFalse(client.Send(new NetMessage("Late")), "Send after the close should say false.");
            }
            finally { host.Stop(); }
        }

        [Test]
        public void Silence_IsNoticed_ByTheHeartbeat()
        {
            // A bare socket that accepts and then never sends anything (not even Pongs).
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket silent = null;
            try
            {
                listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                listener.Listen(1);
                int port = ((IPEndPoint)listener.LocalEndPoint).Port;

                LanTimings timings = NetTestKit.Fast();
                timings.PingIntervalMs = 30;
                timings.TimeoutMs = 300;
                LanConnection client = LanClient.Connect("127.0.0.1", port, timings);
                silent = listener.Accept();
                Assert.IsTrue(NetTestKit.Until(() => client.State == LanConnectionState.Connected));

                Assert.IsTrue(NetTestKit.Until(() => client.State == LanConnectionState.Closed, 3000), "Silence was not noticed.");
                Assert.AreEqual(LanCloseReason.Timeout, client.CloseReason);
                StringAssert.Contains("Nothing heard", client.CloseDetail);
                Assert.IsTrue(client.WaitClosed(NetTestKit.WaitMs));
            }
            finally
            {
                try { silent?.Close(); } catch { }
                listener.Close();
            }
        }

        [Test]
        public void Pings_KeepAQuietConnectionAlive()
        {
            // The timeout is in seconds, so a busy machine (a balance tournament on every core) cannot starve the other side's
            // writer thread long enough to drop the connection; the quiet time is still well past one timeout.
            LanTimings timings = NetTestKit.Fast();
            timings.PingIntervalMs = 50;
            timings.TimeoutMs = 1500;
            LanHost host = NetTestKit.StartHost(timings);
            try
            {
                LanConnection client = NetTestKit.Connect(host, out LanConnection hostSide, timings);
                Thread.Sleep(2500); // more than a timeout's worth of silence from the game, but the pings flow
                Assert.AreEqual(LanConnectionState.Connected, client.State, client.CloseDetail);
                Assert.AreEqual(LanConnectionState.Connected, hostSide.State, hostSide.CloseDetail);
                Assert.GreaterOrEqual(client.RoundTripMs, 0, "No Pong came back to the guest.");
                Assert.GreaterOrEqual(hostSide.RoundTripMs, 0, "No Pong came back to the host.");
                client.Close();
            }
            finally { host.Stop(); }
        }

        [Test]
        public void OversizeFrame_FromTheOtherSide_ClosesWithAnError()
        {
            LanHost host = NetTestKit.StartHost();
            var raw = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                raw.Connect(new IPEndPoint(IPAddress.Loopback, host.Port));
                LanConnection hostSide = null;
                Assert.IsTrue(NetTestKit.Until(() => host.TryAcceptGuest(out hostSide)));
                raw.Send(new byte[] { 0x7f, 0xff, 0xff, 0xff, (byte)'{' });
                Assert.IsTrue(NetTestKit.Until(() => hostSide.State == LanConnectionState.Closed), "The oversize frame was not refused.");
                Assert.AreEqual(LanCloseReason.Error, hostSide.CloseReason);
                StringAssert.Contains("over the limit", hostSide.CloseDetail);

                // Bad JSON also closes with an error.
                var raw2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    raw2.Connect(new IPEndPoint(IPAddress.Loopback, host.Port));
                    LanConnection second = null;
                    Assert.IsTrue(NetTestKit.Until(() => host.TryAcceptGuest(out second)));
                    raw2.Send(new byte[] { 0, 0, 0, 3, (byte)'{', (byte)'x', (byte)'}' });
                    Assert.IsTrue(NetTestKit.Until(() => second.State == LanConnectionState.Closed));
                    Assert.AreEqual(LanCloseReason.Error, second.CloseReason);
                    StringAssert.Contains("Bad message", second.CloseDetail);
                }
                finally { raw2.Close(); }
            }
            finally
            {
                raw.Close();
                host.Stop();
            }
        }

        [Test]
        public void Close_CanBeCalledAnyNumberOfTimes()
        {
            LanHost host = NetTestKit.StartHost();
            try
            {
                LanConnection client = NetTestKit.Connect(host, out LanConnection hostSide);
                var threads = new Thread[4];
                for (int i = 0; i < threads.Length; i++)
                {
                    threads[i] = new Thread(client.Close);
                    threads[i].Start();
                }
                client.Close();
                foreach (Thread t in threads) Assert.IsTrue(t.Join(NetTestKit.WaitMs));
                client.Close();
                Assert.AreEqual(LanConnectionState.Closed, client.State);
                Assert.AreEqual(LanCloseReason.LocalClose, client.CloseReason);
                Assert.IsTrue(client.WaitClosed(NetTestKit.WaitMs));
                Assert.IsTrue(NetTestKit.Until(() => hostSide.State == LanConnectionState.Closed));
                Assert.AreEqual(LanCloseReason.RemoteClosed, hostSide.CloseReason);

                // A connection closed while still connecting stays closed.
                LanConnection early = LanClient.Connect("127.0.0.1", host.Port, NetTestKit.Fast());
                early.Close();
                early.Close();
                Assert.AreEqual(LanConnectionState.Closed, early.State);
                Assert.IsTrue(early.WaitClosed(NetTestKit.WaitMs));
            }
            finally
            {
                host.Stop();
                host.Stop();
            }
            Assert.IsFalse(host.IsListening);
        }

        [Test]
        public void CloseWhileConnecting_StopsTheAttempt_BeforeWaitClosedReturns()
        {
            // A port nobody listens on: Windows takes about 2 s to give up on a refused loopback connect, so the attempt is
            // still under way when Close comes.
            var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)probe.LocalEndPoint).Port;
            probe.Close();

            var lines = new List<string>();
            LanTimings timings = NetTestKit.Fast();
            timings.ConnectTimeoutMs = 5000;
            LanConnection early = LanClient.Connect("127.0.0.1", port, timings, line => { lock (lines) lines.Add(line); });
            Thread.Sleep(100);
            if (early.State != LanConnectionState.Connecting) Assert.Inconclusive("This system refused the connect at once: " + early.CloseDetail);

            early.Close();
            Assert.AreEqual(LanConnectionState.Closed, early.State);
            Assert.AreEqual(LanCloseReason.LocalClose, early.CloseReason);
            // WaitClosed returns only once the connect thread has stopped and shut its socket (long before the 2 s refusal).
            Assert.IsTrue(early.WaitClosed(1000), "The connect attempt went on after Close.");
            lock (lines) Assert.IsTrue(lines.Exists(l => l.Contains("stopped connecting")), "WaitClosed returned before the connect thread ended.");
        }
    }
}
