// LanDiscoveryTests.cs
// Tests finding games: an announcer and a browser on the loopback address see each other, the game drops off the list after
// 4 seconds without news (on an injected clock), packets of other programs are ignored, and the broadcast address of a network
// is worked out right. Broadcast packets do not reach a socket bound to 127.0.0.1 (and binding every card would bring up a
// firewall prompt), so the test sends the announcement to the browser directly (AddTarget, unicast): the same packet and the
// same reading as a broadcast.
// Depends on: Halobellum.Net (LanAnnouncer, LanBrowser), NetTestKit, NUnit.

using System.Collections.Generic;
using System.Net;
using System.Text;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanDiscoveryTests
    {
        [Test]
        public void AnnouncerAndBrowser_SeeEachOther_OnLoopback()
        {
            long fakeNow = 1000;
            var browser = new LanBrowser(0, IPAddress.Loopback, null, () => fakeNow);
            Assert.IsTrue(browser.TryStart(out string error), error);
            var announcer = new LanAnnouncer("Mike", "0.416.2", 47810, browser.Port, IPAddress.Loopback) { IntervalMs = 50, Broadcast = false };
            announcer.AddTarget(new IPEndPoint(IPAddress.Loopback, browser.Port));
            try
            {
                Assert.IsTrue(announcer.TryStart(out error), error);
                List<LanGameInfo> games = null;
                Assert.IsTrue(NetTestKit.Until(() => (games = browser.Games()).Count > 0), "The browser heard nothing.");
                Assert.AreEqual(1, games.Count);
                LanGameInfo game = games[0];
                Assert.AreEqual("Mike", game.Name);
                Assert.AreEqual("0.416.2", game.Version);
                Assert.AreEqual(LanDefaults.Protocol, game.Proto);
                Assert.AreEqual(47810, game.Port);
                Assert.AreEqual("127.0.0.1", game.Address);
                Assert.Greater(announcer.SentCount, 0);

                // Heard again and again: still one entry.
                Assert.IsTrue(NetTestKit.Until(() => announcer.SentCount >= 3));
                Assert.AreEqual(1, browser.Games().Count);

                // Once the announcements stop, the game drops off after 4 s without news.
                announcer.Close();
                announcer.Close();
                System.Threading.Thread.Sleep(200); // let a packet still on its way land before the clock moves
                fakeNow += 3999;
                Assert.AreEqual(1, browser.Games().Count, "Dropped too early.");
                fakeNow += 2;
                Assert.AreEqual(0, browser.Games().Count, "Still listed after 4 s of silence.");
            }
            finally
            {
                announcer.Close();
                browser.Close();
                browser.Close();
            }
        }

        [Test]
        public void Announcement_OfAnotherProgram_IsIgnored()
        {
            byte[] ours = Encoding.UTF8.GetBytes(LanAnnouncer.BuildAnnouncement("Ann", "1.0.0", 5000).ToJson());
            LanGameInfo game = LanBrowser.ParseAnnouncement(ours, ours.Length, "10.0.0.7");
            Assert.IsNotNull(game);
            Assert.AreEqual("Ann", game.Name);
            Assert.AreEqual("10.0.0.7", game.Address);
            Assert.AreEqual(5000, game.Port);

            foreach (string other in new[] { "{\"game\":\"Other\",\"port\":5000}", "{\"game\":\"Halobellum\",\"port\":0}", "not json", "" })
            {
                byte[] bytes = Encoding.UTF8.GetBytes(other);
                Assert.IsNull(LanBrowser.ParseAnnouncement(bytes, bytes.Length, "10.0.0.7"), other);
            }
        }

        [Test]
        public void BroadcastAddress_SetsTheBitsOutsideTheMask()
        {
            Assert.AreEqual(IPAddress.Parse("192.168.1.255"), LanAnnouncer.BroadcastAddress(IPAddress.Parse("192.168.1.20"), IPAddress.Parse("255.255.255.0")));
            Assert.AreEqual(IPAddress.Parse("10.0.255.255"), LanAnnouncer.BroadcastAddress(IPAddress.Parse("10.0.3.4"), IPAddress.Parse("255.255.0.0")));
            Assert.IsNull(LanAnnouncer.BroadcastAddress(IPAddress.Parse("10.0.3.4"), IPAddress.Parse("0.0.0.0")));
            List<IPAddress> all = LanAnnouncer.BroadcastAddresses();
            Assert.AreEqual(IPAddress.Broadcast, all[0]);
        }
    }
}
