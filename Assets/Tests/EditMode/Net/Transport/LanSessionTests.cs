// LanSessionTests.cs
// Tests the handshake and the session over loopback: matching games connect and exchange messages both ways; a guest of another
// version or with other unit stats is refused with a reason from its own point of view while the host keeps waiting (and then
// takes a matching guest); a third game is refused once a match has its player; a dropped host is reported as Lost; a guest
// that left right after its Hello does not lock the host; the last message before the other side closes (a Leave, a Reject)
// always reaches the session; Close is safe to repeat. Also the pure comparison of two Hellos and its words.
// Depends on: Halobellum.Net (LanSession, LanHost, LanHello, LanClient), NetTestKit, NUnit.

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanSessionTests
    {
        private static void PollUntil(LanSession a, LanSession b, System.Func<bool> condition, string what)
        {
            Assert.IsTrue(NetTestKit.Until(() =>
            {
                a.Poll();
                b?.Poll();
                return condition();
            }), what + " (host " + a.State + " " + a.Reason + ", guest " + b?.State + " " + b?.Reason + ")");
        }

        [Test]
        public void MatchingGames_Connect_AndExchangeMessages()
        {
            var lines = new List<string>();
            LanHost host = new LanHost(0, System.Net.IPAddress.Loopback, NetTestKit.Fast());
            LanSession atHost = LanSession.Host(host, NetTestKit.Hello("Hosty"), line => { lock (lines) lines.Add(line); });
            LanSession atGuest = LanSession.Join("127.0.0.1", host.Port, NetTestKit.Hello("Guesty"), NetTestKit.Fast());
            try
            {
                Assert.IsTrue(atHost.IsHost);
                Assert.IsFalse(atGuest.IsHost);
                PollUntil(atHost, atGuest, () => atHost.State == LanSessionState.Connected && atGuest.State == LanSessionState.Connected, "Not connected");
                Assert.AreEqual("Guesty", atHost.PeerName);
                Assert.AreEqual("Hosty", atGuest.PeerName);
                Assert.AreEqual("0.416.2", atGuest.Peer.Version);
                Assert.IsTrue(host.IsLocked, "The host should refuse others once the match has its player.");

                Assert.IsTrue(atGuest.Send(new NetMessage("Ready").Set("round", 1)));
                NetMessage ready = NetTestKit.Receive(atHost);
                Assert.AreEqual("Ready", ready.Type);
                Assert.AreEqual(1, ready.GetInt("round"));

                Assert.IsTrue(atHost.Send(new NetMessage("MatchStart").Set("seed", 42)));
                Assert.AreEqual(42, NetTestKit.Receive(atGuest).GetInt("seed"));

                // A third game is refused politely.
                LanConnection third = LanClient.Connect("127.0.0.1", host.Port, NetTestKit.Fast());
                NetMessage reject = NetTestKit.Receive(third);
                Assert.AreEqual(NetMessageTypes.Reject, reject.Type);
                Assert.AreEqual(LanHost.BusyReason, reject.GetString("reason"));
                Assert.AreEqual(LanSessionState.Connected, atHost.State);

                lock (lines) Assert.IsTrue(lines.Exists(l => l.StartsWith("[LAN] ") && l.Contains("Handshake done")), "The log hook got no handshake line.");
            }
            finally
            {
                atGuest.Close();
                atHost.Close();
            }
        }

        [Test]
        public void OtherVersion_IsRefused_WithItsReason_AndTheHostKeepsWaiting()
        {
            LanHost host = new LanHost(0, System.Net.IPAddress.Loopback, NetTestKit.Fast());
            LanSession atHost = LanSession.Host(host, NetTestKit.Hello("Hosty", "0.416.2"));
            LanSession old = LanSession.Join("127.0.0.1", host.Port, NetTestKit.Hello("Old", "0.416.1"), NetTestKit.Fast());
            LanSession good = null;
            try
            {
                PollUntil(atHost, old, () => old.State == LanSessionState.Refused, "The old version was not refused");
                Assert.AreEqual("The other game is version 0.416.2; this one is 0.416.1.", old.Reason);
                Assert.AreEqual("The other game is version 0.416.1; this one is 0.416.2.", atHost.LastRefusal);
                Assert.AreEqual(LanSessionState.Waiting, atHost.State);
                Assert.IsFalse(old.Send(new NetMessage("Nope")));

                // The host takes the next, matching guest.
                good = LanSession.Join("127.0.0.1", host.Port, NetTestKit.Hello("Good"), NetTestKit.Fast());
                PollUntil(atHost, good, () => atHost.State == LanSessionState.Connected && good.State == LanSessionState.Connected, "The matching guest was not taken");
                Assert.AreEqual("Good", atHost.PeerName);
            }
            finally
            {
                old.Close();
                good?.Close();
                atHost.Close();
            }
        }

        [Test]
        public void OtherUnitStats_AreRefused_NamingWhatDiffers()
        {
            LanHost host = new LanHost(0, System.Net.IPAddress.Loopback, NetTestKit.Fast());
            LanSession atHost = LanSession.Host(host, NetTestKit.Hello("Hosty", content: "units=aa;cards=bb;rules=cc"));
            LanSession modded = LanSession.Join("127.0.0.1", host.Port, NetTestKit.Hello("Modded", content: "units=a2;cards=bb;rules=cc"), NetTestKit.Fast());
            try
            {
                PollUntil(atHost, modded, () => modded.State == LanSessionState.Refused, "Other unit stats were not refused");
                Assert.AreEqual("Unit stats differ: units. Both games need the same unit stats files.", modded.Reason);
                Assert.AreEqual(LanSessionState.Waiting, atHost.State);
            }
            finally
            {
                modded.Close();
                atHost.Close();
            }
        }

        [Test]
        public void DroppedHost_IsReportedAsLost()
        {
            LanHost host = new LanHost(0, System.Net.IPAddress.Loopback, NetTestKit.Fast());
            LanSession atHost = LanSession.Host(host, NetTestKit.Hello("Hosty"));
            LanSession atGuest = LanSession.Join("127.0.0.1", host.Port, NetTestKit.Hello("Guesty"), NetTestKit.Fast());
            try
            {
                PollUntil(atHost, atGuest, () => atHost.State == LanSessionState.Connected && atGuest.State == LanSessionState.Connected, "Not connected");
                atHost.Send(new NetMessage("Last"));
                atHost.Close();
                atHost.Close();
                Assert.AreEqual(LanSessionState.Closed, atHost.State);
                Assert.IsFalse(host.IsListening);

                PollUntil(atGuest, null, () => atGuest.State == LanSessionState.Lost, "The drop was not noticed");
                Assert.IsNotEmpty(atGuest.Reason);
                Assert.IsTrue(atGuest.TryReceive(out NetMessage last), "The message sent before the drop is lost.");
                Assert.AreEqual("Last", last.Type);
            }
            finally
            {
                atGuest.Close();
                atHost.Close();
            }
        }

        [Test]
        public void GuestThatLeftAfterItsHello_IsNotTaken_AndTheHostKeepsWaiting()
        {
            LanHost host = new LanHost(0, IPAddress.Loopback, NetTestKit.Fast());
            LanSession atHost = LanSession.Host(host, NetTestKit.Hello("Hosty"));
            var raw = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            LanSession good = null;
            try
            {
                // A guest sends a good Hello and leaves before the host's next Poll.
                raw.Connect(new IPEndPoint(IPAddress.Loopback, host.Port));
                raw.Send(Framing.Encode(NetTestKit.Hello("Gone").ToMessage()));
                raw.Shutdown(SocketShutdown.Send);
                Assert.IsTrue(NetTestKit.Until(() => host.CurrentGuest != null && host.CurrentGuest.State == LanConnectionState.Closed),
                    "The host did not see the guest leave.");
                Assert.AreEqual(1, host.CurrentGuest.Pending, "The Hello should still wait in the inbox.");

                atHost.Poll();
                Assert.AreEqual(LanSessionState.Waiting, atHost.State, atHost.Reason);
                Assert.IsFalse(host.IsLocked, "The host locked itself for a guest who had left.");

                // The next guest is taken as usual.
                good = LanSession.Join("127.0.0.1", host.Port, NetTestKit.Hello("Good"), NetTestKit.Fast());
                PollUntil(atHost, good, () => atHost.State == LanSessionState.Connected && good.State == LanSessionState.Connected, "The next guest was not taken");
                Assert.AreEqual("Good", atHost.PeerName);
            }
            finally
            {
                raw.Close();
                good?.Close();
                atHost.Close();
            }
        }

        [Test]
        public void LastMessageBeforeTheOtherSideCloses_IsNeverLost()
        {
            // A bare-socket host answers the Hello, sends one more message and closes at once, while the guest's session is
            // polled in a tight loop. Whenever the close is noticed, the message before it must still come out: a Leave after
            // the handshake, a Reject during it. Repeated, because the close can land at any point of a Poll.
            for (int round = 0; round < 40; round++)
            {
                bool reject = round % 2 == 1;
                var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Socket peer = null;
                LanSession guest = null;
                try
                {
                    listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                    listener.Listen(1);
                    guest = LanSession.Join("127.0.0.1", ((IPEndPoint)listener.LocalEndPoint).Port, NetTestKit.Hello("Guesty"), NetTestKit.Fast());
                    peer = listener.Accept();
                    Assert.AreEqual(NetMessageTypes.Hello, ReadOne(peer).Type);

                    if (reject) peer.Send(Framing.Encode(new NetMessage(NetMessageTypes.Reject).Set("reason", "No room.")));
                    else
                    {
                        peer.Send(Framing.Encode(NetTestKit.Hello("Hosty").ToMessage(NetMessageTypes.Welcome)));
                        peer.Send(Framing.Encode(new NetMessage("Leave").Set("round", round)));
                    }
                    peer.Shutdown(SocketShutdown.Send);

                    var got = new List<NetMessage>();
                    LanSession polled = guest;
                    Assert.IsTrue(NetTestKit.Until(() =>
                    {
                        while (polled.TryReceive(out NetMessage m)) got.Add(m);
                        return polled.IsFinished;
                    }), "Round " + round + ": the close was not noticed (" + guest.State + ").");
                    while (guest.TryReceive(out NetMessage m)) got.Add(m);

                    if (reject)
                    {
                        Assert.AreEqual(LanSessionState.Refused, guest.State, "Round " + round + ": " + guest.Reason);
                        Assert.AreEqual("No room.", guest.Reason);
                    }
                    else
                    {
                        Assert.AreEqual(LanSessionState.Lost, guest.State, "Round " + round + ": " + guest.Reason);
                        Assert.AreEqual(1, got.Count, "Round " + round + ": the Leave before the close was lost.");
                        Assert.AreEqual("Leave", got[0].Type);
                        Assert.AreEqual(round, got[0].GetInt("round", -1));
                    }
                }
                finally
                {
                    guest?.Close();
                    try { peer?.Close(); } catch { }
                    listener.Close();
                }
            }
        }

        /// <summary>Reads one whole message from a bare socket.</summary>
        private static NetMessage ReadOne(Socket socket)
        {
            var frames = new FrameReader();
            var buffer = new byte[4096];
            socket.ReceiveTimeout = NetTestKit.WaitMs;
            string json;
            while (!frames.TryReadText(out json))
            {
                int count = socket.Receive(buffer);
                Assert.Greater(count, 0, "The connection closed before a whole message came.");
                frames.Push(buffer, 0, count);
            }
            return NetMessage.Parse(json);
        }

        [Test]
        public void HostThatCannotListen_IsLostAtOnce()
        {
            LanHost first = NetTestKit.StartHost();
            try
            {
                var second = new LanHost(first.Port, System.Net.IPAddress.Loopback, NetTestKit.Fast());
                LanSession session = LanSession.Host(second, NetTestKit.Hello("Late"));
                Assert.AreEqual(LanSessionState.Lost, session.State);
                StringAssert.Contains("Could not listen on port", session.Reason);
                session.Close();
            }
            finally { first.Stop(); }
        }

        [Test]
        public void Mismatch_ComparesProtocolVersionCodeAndContent()
        {
            LanHello a = NetTestKit.Hello("A");
            LanHello b = NetTestKit.Hello("B");
            Assert.IsNull(LanHello.Mismatch(a, b), "Names are not compared.");

            LanHello proto = b.Copy();
            proto.Proto = 2;
            StringAssert.StartsWith("The other game uses LAN protocol 2; this one uses 1.", LanHello.Mismatch(a, proto));

            LanHello version = b.Copy();
            version.Version = "0.416.1";
            Assert.AreEqual("The other game is version 0.416.1; this one is 0.416.2.", LanHello.Mismatch(a, version));

            LanHello code = b.Copy();
            code.Code = "otherbuild999";
            string codeReason = LanHello.Mismatch(a, code);
            StringAssert.Contains("different builds of version 0.416.2", codeReason);
            StringAssert.Contains("otherbui", codeReason);

            LanHello sections = b.Copy();
            sections.Content = "units=aa;cards=XX;rules=YY";
            Assert.AreEqual("Unit stats differ: cards, rules. Both games need the same unit stats files.", LanHello.Mismatch(a, sections));

            LanHello extra = b.Copy();
            extra.Content = "units=aa;cards=bb;rules=cc;spells=dd";
            Assert.AreEqual("Unit stats differ: spells. Both games need the same unit stats files.", LanHello.Mismatch(a, extra));

            LanHello plainA = a.Copy(), plainB = b.Copy();
            plainA.Content = "0123456789abcdef";
            plainB.Content = "fedcba9876543210";
            Assert.AreEqual("Unit stats differ: fingerprint fedcba98 there, 01234567 here. Both games need the same unit stats files.", LanHello.Mismatch(plainA, plainB));

            // A Hello survives its message.
            LanHello back = LanHello.FromMessage(NetMessage.Parse(a.ToMessage().ToJson()));
            Assert.IsNull(LanHello.Mismatch(a, back));
            Assert.AreEqual("A", back.Name);
            Assert.AreEqual(NetMessageTypes.Hello, a.ToMessage().Type);
        }
    }
}
