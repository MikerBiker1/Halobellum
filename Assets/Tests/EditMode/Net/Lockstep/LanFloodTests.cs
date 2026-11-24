// LanFloodTests.cs
// Tests that the match layer stays responsive against a broken or hostile game: over a channel that never runs dry, one coordinator
// Update and one LanGuestStart Poll each handle at most LanLimits.MaxMessagesPerUpdate messages and return (the rest wait a frame);
// and a Ready, state hash or battle result for a round more than MaxRoundsAhead past this game's is a Desync with a clear reason,
// never a message kept for later (a round or two ahead still waits its turn). Text from the other game in a Desync is cut short.
// Depends on: Halobellum.Net (LanMatchCoordinator, LanMatchStart, LanGuestStart, LanMemoryChannel, LanLimits), Halobellum.Core,
//             NUnit, LockstepTestKit, TestRules.

using Halobellum.Core;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanFloodTests
    {
        /// <summary>A channel that always has one more message (made by the given function) and counts how many were taken.</summary>
        private sealed class EndlessChannel : ILanChannel
        {
            private readonly System.Func<int, NetMessage> next;

            public EndlessChannel(System.Func<int, NetMessage> next) { this.next = next; }

            public int Taken { get; private set; }
            public bool Send(JsonObject message) => true;

            public bool TryReceive(out NetMessage message)
            {
                message = next(Taken++);
                return true;
            }

            public bool IsOpen => true;
            public string LostReason => "";
            public void Close() { }
        }

        [Test]
        public void OneUpdate_HandlesAFramesShare_OfAChannelThatNeverRunsDry()
        {
            var flood = new EndlessChannel(i => (NetMessage)new NetMessage(i % 2 == 0 ? "Junk" : LanMessageTypes.Status).Set(LanFields.Round, 99));
            LanMatchCoordinator host = LanMatchStart.BeginAsHost(flood, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(),
                LockstepTestKit.Settings(7));
            host.Update();
            Assert.AreEqual(LanLimits.MaxMessagesPerUpdate, flood.Taken, "One Update stops after its share.");
            host.Update();
            Assert.AreEqual(2 * LanLimits.MaxMessagesPerUpdate, flood.Taken, "The next Update takes the next share.");
            Assert.IsFalse(host.IsFinished, "Ignored messages are not an error: " + host.LastError);

            var junk = new EndlessChannel(i => new NetMessage("Junk"));
            var start = new LanGuestStart(junk, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions());
            start.Poll();
            Assert.AreEqual(LanLimits.MaxMessagesPerUpdate, junk.Taken, "One Poll of the guest's start stops after its share.");
            Assert.AreEqual(LanGuestStartState.Waiting, start.State);
        }

        private static void Draft(LanMatchCoordinator host, LanMatchCoordinator guest)
        {
            Assert.IsTrue(host.ChooseDraft(0).Ok);
            Assert.IsTrue(guest.ChooseDraft(0).Ok);
            host.Update();
            guest.Update();
            Assert.AreEqual(1, host.Match.Round);
            Assert.AreEqual(LanMatchState.Deployment, host.State);
            Assert.AreEqual(LanMatchState.Deployment, guest.State);
        }

        [Test]
        public void AReadyForARoundTooFarAhead_IsADesync_ButARoundOrTwoAheadWaits()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(71), out LanMatchCoordinator host, out LanMatchCoordinator guest,
                out LanMemoryChannel hostChannel, out LanMemoryChannel guestChannel);
            Draft(host, guest);
            int limit = host.Match.Round + LanMatchCoordinator.MaxRoundsAhead;

            guestChannel.Send(new NetMessage(LanMessageTypes.Ready).Set(LanFields.Round, limit).Set(LanFields.Commands, ""));
            host.Update();
            Assert.IsFalse(host.IsFinished, "A Ready " + LanMatchCoordinator.MaxRoundsAhead + " rounds ahead waits: " + host.LastError);

            guestChannel.Send(new NetMessage(LanMessageTypes.Ready).Set(LanFields.Round, limit + 1).Set(LanFields.Commands, ""));
            host.Update();
            Assert.AreEqual(LanMatchState.Desync, host.State);
            StringAssert.Contains("a Ready for round " + (limit + 1), host.LastError);
            StringAssert.Contains("more than " + LanMatchCoordinator.MaxRoundsAhead + " rounds past", host.LastError);
            Assert.IsTrue(NetTestKit.Until(() =>
            {
                guest.Update();
                return guest.State == LanMatchState.Desync;
            }), "The other game is told.");
        }

        [Test]
        public void AStateHashOrResultForARoundTooFarAhead_IsADesync()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(72), out LanMatchCoordinator host, out LanMatchCoordinator guest,
                out _, out LanMemoryChannel guestChannel);
            Draft(host, guest);
            guestChannel.Send(new NetMessage(LanMessageTypes.StateHash).Set(LanFields.Round, 1000000).Set(LanFields.Hash, 5UL));
            host.Update();
            Assert.AreEqual(LanMatchState.Desync, host.State);
            StringAssert.Contains("a state hash for round 1000000", host.LastError);

            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(73), out host, out guest, out LanMemoryChannel hostChannel, out _);
            Draft(host, guest);
            hostChannel.Send(new NetMessage(LanMessageTypes.BattleResult).Set(LanFields.Round, 9).Set(LanFields.Outcome, "x"));
            guest.Update();
            Assert.AreEqual(LanMatchState.Desync, guest.State);
            StringAssert.Contains("a battle result for round 9", guest.LastError);
        }

        [Test]
        public void TextFromTheOtherGame_IsCutShort_InADesync()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(74), out LanMatchCoordinator host, out LanMatchCoordinator guest,
                out _, out LanMemoryChannel guestChannel);
            Draft(host, guest);
            // The other game's Desync with a huge reason: the reason kept (and shown) is cut short.
            guestChannel.Send(new NetMessage(LanMessageTypes.Desync).Set(LanFields.Round, 1).Set(LanFields.Reason, new string('r', 500000)));
            host.Update();
            Assert.AreEqual(LanMatchState.Desync, host.State);
            Assert.Less(host.LastError.Length, LanLimits.MaxReasonChars + 100, "The other game's reason is cut short.");
        }
    }
}
