// LanHardeningTests.cs
// Tests the transport's limits against a broken or hostile game on the network (LanLimits), over real loopback sockets and raw
// sockets that break the rules on purpose: the JSON reader refuses too many values or too long a text, quickly; a Ping is answered
// only when its "ms" is a short whole number; a connection whose other side does not read closes once too much waits to be sent;
// a connection that is sent more than the game reads closes; this game's own message over the frame limit closes the connection
// instead of throwing (on a connection and on a session); a Hello with a huge version is refused with a short reason and Poll never
// throws; a session moves at most a frame's share of messages per Poll and notices a drop only after every message before it; a
// connection that sends nothing loses the guest slot after HelloTimeoutMs, and a real guest who comes meanwhile is told another
// player is joining, then can join; refused connections get their Reject without a thread of their own and are released; and the
// join list keeps at most MaxGamesListed games, with long names cut short.
// Depends on: Halobellum.Net (Framing, NetJson, LanConnection, LanHost, LanSession, LanHello, LanBrowser, LanLimits), NetTestKit,
//             LockstepTestKit (ConnectSessions), NUnit.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanHardeningTests
    {
        // ------------------------------------------------------------------ raw sockets that break the rules

        private static Socket RawConnect(int port, int receiveBuffer = 0)
        {
            var raw = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (receiveBuffer > 0) raw.ReceiveBufferSize = receiveBuffer;
            raw.Connect(new IPEndPoint(IPAddress.Loopback, port));
            return raw;
        }

        private static void SendJson(Socket raw, string json)
        {
            byte[] frame = Framing.Encode(json);
            int sent = 0;
            while (sent < frame.Length) sent += raw.Send(frame, sent, frame.Length - sent, SocketFlags.None);
        }

        /// <summary>The next message of the given type on a raw socket (others skipped); null after the time limit or a close.</summary>
        private static NetMessage ReadRaw(Socket raw, FrameReader frames, string type, int timeoutMs = NetTestKit.WaitMs)
        {
            var buffer = new byte[64 * 1024];
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                while (frames.TryReadText(out string json))
                {
                    NetMessage m = NetMessage.Parse(json);
                    if (m.Type == type) return m;
                }
                if (!raw.Poll(20 * 1000, SelectMode.SelectRead)) continue;
                int count = raw.Receive(buffer);
                if (count == 0) return null;
                frames.Push(buffer, 0, count);
            }
            return null;
        }

        /// <summary>True once the other side closed the raw socket (reads give 0), dropping whatever comes first.</summary>
        private static bool ClosedByTheOtherSide(Socket raw, int timeoutMs = NetTestKit.WaitMs)
        {
            var buffer = new byte[4096];
            var watch = Stopwatch.StartNew();
            try
            {
                while (watch.ElapsedMilliseconds < timeoutMs)
                {
                    if (!raw.Poll(20 * 1000, SelectMode.SelectRead)) continue;
                    if (raw.Receive(buffer) == 0) return true;
                }
            }
            catch (SocketException) { return true; } // reset: closed too
            return false;
        }

        // ------------------------------------------------------------------ JSON and frames

        [Test]
        public void TheJsonReader_RefusesTooManyValues_AndTooLongAText_Quickly()
        {
            Assert.AreEqual(1024 * 1024, Framing.MaxFrameBytes, "The frame limit is 1 MB (real messages are a few KB).");

            // Half a megabyte of empty objects would be over 100 MB of objects in memory: refused after MaxValues.
            var many = new StringBuilder("{\"t\":\"X\",\"a\":[");
            for (int i = 0; i < 170000; i++) many.Append(i == 0 ? "{}" : ",{}");
            many.Append("]}");
            Assert.Less(many.Length, Framing.MaxFrameBytes);
            var watch = Stopwatch.StartNew();
            var e = Assert.Throws<FormatException>(() => NetMessage.Parse(many.ToString()));
            StringAssert.Contains("values", e.Message);
            Assert.Less(watch.ElapsedMilliseconds, 1000, "Refused at once, not after building every object.");

            // Fewer values than the limit read as usual.
            var fine = new StringBuilder("{\"t\":\"X\",\"a\":[");
            for (int i = 0; i < NetJson.MaxValues - 10; i++) fine.Append(i == 0 ? "0" : ",0");
            fine.Append("]}");
            Assert.AreEqual(NetJson.MaxValues - 10, NetMessage.Parse(fine.ToString()).GetArray("a").Count);

            // A text longer than the limit is refused before it is read.
            string longText = "{\"t\":\"X\",\"s\":\"" + new string('x', NetJson.MaxChars) + "\"}";
            StringAssert.Contains("over the limit", Assert.Throws<FormatException>(() => NetJson.ParseObject(longText)).Message);
        }

        [Test]
        public void APing_IsAnsweredOnlyWhenItsTimeIsAShortWholeNumber()
        {
            LanHost host = NetTestKit.StartHost();
            Socket raw = RawConnect(host.Port);
            try
            {
                LanConnection atHost = null;
                Assert.IsTrue(NetTestKit.Until(() => host.TryAcceptGuest(out atHost)));
                SendJson(raw, "{\"t\":\"Ping\",\"ms\":\"" + new string('1', 300000) + "\"}");   // a string: not echoed
                SendJson(raw, "{\"t\":\"Ping\",\"ms\":" + new string('7', 100) + "}");          // too many digits
                SendJson(raw, "{\"t\":\"Ping\",\"ms\":{\"x\":[1,2,3]}}");                         // an object
                SendJson(raw, "{\"t\":\"Ping\",\"ms\":12345}");                                    // the real thing
                NetMessage pong = ReadRaw(raw, new FrameReader(), NetMessageTypes.Pong);
                Assert.IsNotNull(pong, "The good Ping got no Pong: " + atHost.CloseDetail);
                Assert.AreEqual(12345, pong.GetLong("ms"), "Only the Ping with a short number was answered, and first.");
                Assert.AreEqual(LanConnectionState.Connected, atHost.State, "Odd Pings are ignored, not an error.");
            }
            finally
            {
                raw.Close();
                host.Stop();
            }
        }

        [Test]
        public void AConnectionWhoseOtherSideDoesNotRead_ClosesOnceTooMuchWaitsToBeSent()
        {
            LanHost host = NetTestKit.StartHost();
            Socket raw = RawConnect(host.Port, receiveBuffer: 4096); // connects and never reads
            try
            {
                LanConnection atHost = null;
                Assert.IsTrue(NetTestKit.Until(() => host.TryAcceptGuest(out atHost)));
                string big = new string('b', 900 * 1024);
                int accepted = 0;
                for (int i = 0; i < 60 && atHost.Send(new NetMessage("Big").Set("s", big)); i++) accepted++;
                Assert.Less(accepted, 60, "Send said false once too much waited.");
                Assert.AreEqual(LanConnectionState.Closed, atHost.State);
                Assert.AreEqual(LanCloseReason.Error, atHost.CloseReason);
                StringAssert.Contains("not reading", atHost.CloseDetail);
                Assert.IsTrue(atHost.WaitClosed(NetTestKit.WaitMs));
            }
            finally
            {
                raw.Close();
                host.Stop();
            }
        }

        [Test]
        public void AConnectionSentMoreThanTheGameReads_Closes()
        {
            LanHost host = NetTestKit.StartHost();
            try
            {
                LanConnection client = NetTestKit.Connect(host, out LanConnection atHost);
                // In batches, so the sender's own outbox (also bounded) drains between them.
                for (int i = 0; i < LanLimits.MaxInboxMessages + 1000 && client.State == LanConnectionState.Connected; i++)
                {
                    client.Send(new NetMessage("N").Set("i", i));
                    if (i % 500 == 499) System.Threading.Thread.Sleep(30);
                }
                Assert.IsTrue(NetTestKit.Until(() => atHost.State == LanConnectionState.Closed), "The flood did not close the connection.");
                Assert.AreEqual(LanCloseReason.Error, atHost.CloseReason);
                StringAssert.Contains("more than this one can take", atHost.CloseDetail);
                Assert.LessOrEqual(atHost.Pending, LanLimits.MaxInboxMessages);
                Assert.AreEqual(0, atHost.ReceiveAll()[0].GetInt("i", -1), "What was taken before the close can still be read, in order.");
                client.Close();
            }
            finally { host.Stop(); }
        }

        [Test]
        public void ThisGamesOwnMessage_OverTheLimit_ClosesTheConnection_InsteadOfThrowing()
        {
            var tooBig = new NetMessage("Huge").Set("s", new string('x', Framing.MaxFrameBytes));

            LanHost host = NetTestKit.StartHost();
            try
            {
                LanConnection client = NetTestKit.Connect(host, out LanConnection atHost);
                bool sent = true;
                Assert.DoesNotThrow(() => sent = client.Send(tooBig));
                Assert.IsFalse(sent);
                Assert.AreEqual(LanConnectionState.Closed, client.State);
                Assert.AreEqual(LanCloseReason.Error, client.CloseReason);
                StringAssert.Contains("too long", client.CloseDetail);
                Assert.IsTrue(NetTestKit.Until(() => atHost.State == LanConnectionState.Closed), "The other side sees the close.");
            }
            finally { host.Stop(); }

            // On a session: Send says false, and Poll and TryReceive never throw; the session is Lost with the reason.
            LockstepTestKit.ConnectSessions(out LanSession atHostSession, out LanSession atGuest);
            try
            {
                Assert.AreEqual(LanSessionState.Connected, atGuest.State, atGuest.Reason);
                Assert.IsFalse(atGuest.Send(tooBig));
                Assert.DoesNotThrow(() =>
                {
                    atGuest.Poll();
                    atGuest.TryReceive(out _);
                });
                Assert.AreEqual(LanSessionState.Lost, atGuest.State);
                StringAssert.Contains("too long", atGuest.Reason);
            }
            finally
            {
                atGuest.Close();
                atHostSession.Close();
            }
        }

        // ------------------------------------------------------------------ the handshake

        [Test]
        public void AHelloWithAHugeVersion_IsRefusedWithAShortReason_AndPollNeverThrows()
        {
            var huge = new LanHello { Version = new string('9', 200000), Code = "code1234abcd", Content = "units=aa;cards=bb;rules=cc" };
            Assert.LessOrEqual(LanHello.Mismatch(NetTestKit.Hello("Hosty"), huge).Length, LanLimits.MaxReasonChars);
            Assert.LessOrEqual(LanHello.Mismatch(huge, NetTestKit.Hello("Hosty")).Length, LanLimits.MaxReasonChars);

            var lanHost = new LanHost(0, IPAddress.Loopback, NetTestKit.Fast());
            LanSession session = LanSession.Host(lanHost, NetTestKit.Hello("Hosty"));
            Socket raw = RawConnect(lanHost.Port);
            try
            {
                // Just under the frame limit: the reason would echo the version back and go over it.
                string head = "{\"t\":\"Hello\",\"proto\":1,\"version\":\"", tail = "\",\"code\":\"x\",\"content\":\"y\",\"name\":\"z\"}";
                SendJson(raw, head + new string('9', Framing.MaxFrameBytes - head.Length - tail.Length) + tail);
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    Assert.DoesNotThrow(session.Poll);
                    return session.LastRefusal.Length > 0;
                }), "The guest was not refused (" + session.State + ").");
                Assert.LessOrEqual(session.LastRefusal.Length, LanLimits.MaxReasonChars);
                StringAssert.Contains("version", session.LastRefusal);
                Assert.AreEqual(LanSessionState.Waiting, session.State, "The host keeps waiting for the next guest.");

                NetMessage reject = ReadRaw(raw, new FrameReader(), NetMessageTypes.Reject);
                Assert.IsNotNull(reject, "The guest got no Reject.");
                Assert.LessOrEqual(reject.GetString("reason").Length, LanLimits.MaxReasonChars);
                StringAssert.Contains("version", reject.GetString("reason"));
            }
            finally
            {
                raw.Close();
                session.Close();
            }
        }

        [Test]
        public void AConnectionThatSendsNothing_LosesTheGuestSlotSoon_AndARealGuestIsToldWhyMeanwhile()
        {
            LanTimings timings = NetTestKit.Fast();
            timings.HelloTimeoutMs = 600;
            Assert.AreEqual(3000, new LanTimings().HelloTimeoutMs, "The game's own wait for a Hello is short.");
            var lanHost = new LanHost(0, IPAddress.Loopback, timings);
            LanSession session = LanSession.Host(lanHost, NetTestKit.Hello("Hosty"));
            Socket silent = RawConnect(lanHost.Port);
            LanSession meanwhile = null, later = null;
            try
            {
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    session.Poll();
                    return session.State == LanSessionState.Handshaking;
                }), "The silent connection was not taken.");

                // A real guest while the silent one holds the slot: refused with a reason that says what is going on.
                meanwhile = LanSession.Join("127.0.0.1", lanHost.Port, NetTestKit.Hello("Early"), NetTestKit.Fast());
                LanSession m = meanwhile;
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    session.Poll();
                    m.Poll();
                    return m.IsFinished;
                }), "The early guest got no answer.");
                Assert.AreEqual(LanSessionState.Refused, meanwhile.State, meanwhile.Reason);
                Assert.AreEqual(LanHost.JoiningReason, meanwhile.Reason);

                // The silent connection is dropped after HelloTimeoutMs (not the 3 s handshake time), and told why.
                var watch = Stopwatch.StartNew();
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    session.Poll();
                    return session.State == LanSessionState.Waiting;
                }), "The silent connection kept the slot.");
                Assert.Less(watch.ElapsedMilliseconds, 2500);
                StringAssert.Contains("no Hello", session.LastRefusal);
                Assert.IsNotNull(ReadRaw(silent, new FrameReader(), NetMessageTypes.Reject), "The silent connection was told.");

                // Now a real guest joins.
                later = LanSession.Join("127.0.0.1", lanHost.Port, NetTestKit.Hello("Later"), NetTestKit.Fast());
                LanSession l = later;
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    session.Poll();
                    l.Poll();
                    return session.State == LanSessionState.Connected && l.State == LanSessionState.Connected;
                }), "The next guest could not join: " + later.State + " " + later.Reason);
                Assert.AreEqual("Later", session.PeerName);
            }
            finally
            {
                silent.Close();
                meanwhile?.Close();
                later?.Close();
                session.Close();
            }
        }

        [Test]
        public void RefusedConnections_GetTheirReject_AndAreReleased_AFewAtATime()
        {
            LanHost host = NetTestKit.StartHost();
            var raws = new List<Socket>();
            try
            {
                host.Lock();
                // Each refused connection gets the Reject and is closed by the host.
                for (int i = 0; i < 3; i++)
                {
                    Socket raw = RawConnect(host.Port);
                    raws.Add(raw);
                    NetMessage reject = ReadRaw(raw, new FrameReader(), NetMessageTypes.Reject);
                    Assert.IsNotNull(reject, "Refused connection " + i + " got no Reject.");
                    Assert.AreEqual(LanHost.BusyReason, reject.GetString("reason"));
                    raw.Shutdown(SocketShutdown.Send); // closes in turn, as LanConnection does
                    Assert.IsTrue(ClosedByTheOtherSide(raw), "The host did not release refused connection " + i + ".");
                }

                // Many at once that never close: at most MaxRefusing linger, and all are released after the linger time.
                int most = 0;
                for (int i = 0; i < LanLimits.MaxRefusing + 6; i++)
                {
                    raws.Add(RawConnect(host.Port));
                    most = Math.Max(most, host.Refusing);
                }
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    most = Math.Max(most, host.Refusing);
                    return host.Refusing == 0;
                }), "Refused connections were never released (" + host.Refusing + " left).");
                Assert.LessOrEqual(most, LanLimits.MaxRefusing);
                Assert.IsTrue(host.IsListening, "Still hosting.");
            }
            finally
            {
                foreach (Socket raw in raws) raw.Close();
                host.Stop();
            }
        }

        // ------------------------------------------------------------------ the session's share per frame

        [Test]
        public void OnePoll_MovesAtMostAFramesShare_AndADropComesAfterEveryMessageBeforeIt()
        {
            const int Count = 1000;
            LockstepTestKit.ConnectSessions(out LanSession atHost, out LanSession atGuest);
            try
            {
                Assert.AreEqual(LanSessionState.Connected, atGuest.State, atGuest.Reason);
                for (int i = 0; i < Count; i++) Assert.IsTrue(atGuest.Send(new NetMessage("N").Set("i", i)));
                atGuest.Close();
                LanConnection connection = atHost.Connection;
                Assert.IsTrue(NetTestKit.Until(() => connection.State == LanConnectionState.Closed), "The guest's close did not arrive.");
                Assert.AreEqual(Count, connection.Pending, "Every message came before the close.");

                atHost.Poll();
                Assert.AreEqual(Count - LanLimits.MaxMessagesPerUpdate, connection.Pending, "One Poll takes a frame's share only.");
                Assert.AreEqual(LanSessionState.Connected, atHost.State, "Not Lost while messages from before the close wait.");

                var got = new List<int>();
                while (atHost.TryReceive(out NetMessage m)) got.Add(m.GetInt("i", -1));
                Assert.AreEqual(Count, got.Count);
                for (int i = 0; i < Count; i++) Assert.AreEqual(i, got[i]);
                Assert.AreEqual(LanSessionState.Lost, atHost.State, atHost.Reason);
            }
            finally
            {
                atGuest.Close();
                atHost.Close();
            }
        }

        // ------------------------------------------------------------------ discovery

        [Test]
        public void TheJoinList_KeepsAtMostMaxGames_AndDropsTheStaleOnesForNewOnes()
        {
            long now = 0;
            var browser = new LanBrowser(0, IPAddress.Loopback, null, () => now); // not started: games are added by hand
            LanGameInfo Game(int i) => new LanGameInfo { Name = "Game " + i, Version = "0.416.2", Proto = 1, Address = "192.168.1.20", Port = 40000 + i };

            for (int i = 0; i < LanBrowser.MaxGames + 10; i++) Assert.AreEqual(i < LanBrowser.MaxGames, browser.Add(Game(i), now));
            Assert.AreEqual(LanBrowser.MaxGames, browser.Count);
            Assert.IsTrue(browser.Add(Game(0), now), "A game already listed is still heard.");
            Assert.AreEqual(LanBrowser.MaxGames, browser.Games().Count);

            now = browser.StaleMs + 1000;
            Assert.IsTrue(browser.Add(Game(500), now), "Once the others are stale, a new game takes a place.");
            Assert.AreEqual(1, browser.Count);
            Assert.AreEqual("Game 500", browser.Games()[0].Name);

            byte[] packet = Encoding.UTF8.GetBytes(LanAnnouncer.BuildAnnouncement(new string('N', 1000), new string('V', 900), 47810).ToJson());
            LanGameInfo heard = LanBrowser.ParseAnnouncement(packet, packet.Length, "192.168.1.30");
            Assert.LessOrEqual(heard.Name.Length, LanLimits.MaxNameChars);
            Assert.LessOrEqual(heard.Version.Length, LanLimits.MaxNameChars);
            browser.Close();
        }
    }
}
