// LanMessageSizeTests.cs
// Measures the real LAN match messages against the frame limit (LanLimits.MaxFrameBytes): whole matches with the Insane AI on both
// sides (the in-memory pair, several seeds, one at the game's own base health), and a worst-case round in which one side does
// thousands of actions (a squad moved again and again) before its Ready. Every message must stay far below the limit, so the
// limit can be small without ever cutting a real match short, and the long Ready must still go across and apply on the other
// machine. The sizes are printed for the docs (net_transport.md, "Limits").
// Depends on: Halobellum.Net (LanMatchCoordinator, LanMemoryChannel, Framing, LanLimits), Halobellum.Economy, NUnit,
//             LockstepTestKit (AiSeat).

using System;
using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanMessageSizeTests
    {
        /// <summary>A channel that passes everything on and notes the biggest message sent through it, per type.</summary>
        private sealed class SizeRecorder : ILanChannel
        {
            private readonly ILanChannel inner;
            public readonly Dictionary<string, int> LargestByType = new Dictionary<string, int>();

            public SizeRecorder(ILanChannel inner) { this.inner = inner; }

            public int Largest => LargestByType.Count == 0 ? 0 : LargestByType.Values.Max();

            public bool Send(JsonObject message)
            {
                int bytes = Framing.Encode(message).Length - Framing.HeaderBytes;
                string type = message.GetString(NetMessage.TypeKey, "?");
                if (!LargestByType.TryGetValue(type, out int was) || bytes > was) LargestByType[type] = bytes;
                return inner.Send(message);
            }

            public bool TryReceive(out NetMessage message) => inner.TryReceive(out message);
            public bool IsOpen => inner.IsOpen;
            public string LostReason => inner.LostReason;
            public void Close() => inner.Close();

            public string Describe() => string.Join(", ", LargestByType.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value));
        }

        [TestCase(23, MatchMode.Faction, LockstepTestKit.WholeMatchBaseHealth)]
        [TestCase(31, MatchMode.Faction, LockstepTestKit.WholeMatchBaseHealth)]
        [TestCase(5, MatchMode.UnlockSkirmish, LockstepTestKit.WholeMatchBaseHealth)]
        [TestCase(17, MatchMode.Faction, 2000)]
        public void TheMessagesOfWholeMatches_StayFarBelowTheFrameLimit(int seed, MatchMode mode, int baseHealth)
        {
            LanMemoryChannel.CreatePair(out LanMemoryChannel a, out LanMemoryChannel b);
            var hostSide = new SizeRecorder(a);
            var guestSide = new SizeRecorder(b);
            LockstepTestKit.Start(hostSide, guestSide, LockstepTestKit.Settings(seed, mode, baseHealth), out LanMatchCoordinator h, out LanMatchCoordinator g);
            var host = new AiSeat(h, seed + 1);
            var guest = new AiSeat(g, seed + 2);
            LockstepTestKit.PlayOut(host, guest, order: new Random(seed));
            Assert.AreEqual(LanMatchState.MatchOver, h.State, h.LastError);
            Assert.AreEqual(LanMatchState.MatchOver, g.State, g.LastError);

            int largest = Math.Max(hostSide.Largest, guestSide.Largest);
            TestContext.Progress.WriteLine($"Seed {seed} ({mode}, base {baseHealth}, {h.Match.Round} rounds): largest message {largest} bytes. "
                + $"Host: {hostSide.Describe()}. Guest: {guestSide.Describe()}.");
            Assert.Less(largest, LanLimits.MaxFrameBytes / 32, "A real message is far below the frame limit.");
        }

        [Test]
        public void AReadyWithThousandsOfActions_FitsInAFrame_AndAppliesOnTheOtherMachine()
        {
            const int Moves = 3000;
            LanMemoryChannel.CreatePair(out LanMemoryChannel a, out LanMemoryChannel b);
            var hostSide = new SizeRecorder(a);
            LockstepTestKit.Start(hostSide, b, LockstepTestKit.Settings(61), out LanMatchCoordinator host, out LanMatchCoordinator guest);
            Assert.IsTrue(host.ChooseDraft(0).Ok);
            Assert.IsTrue(guest.ChooseDraft(0).Ok);
            host.Update();
            guest.Update();

            // A player who drags one squad around for the whole deployment: every drop is a command.
            Match match = host.Match;
            UnitStats infantry = match.Get(Team.A).Roster.Single(u => u.Name == "Infantry");
            Assert.IsTrue(match.Buy(Team.A, infantry).Ok);
            PlacedSquad squad = match.Get(Team.A).Squads.Last();
            for (int i = 0; i < Moves; i++)
                Assert.IsTrue(match.Move(Team.A, squad.Id, squad.Column, squad.Row, squad.Rotated90).Ok, "Move " + i);
            Assert.IsTrue(host.Ready().Ok);
            int ready = hostSide.LargestByType[LanMessageTypes.Ready];
            int perAction = ready / (Moves + 1);
            TestContext.Progress.WriteLine($"A Ready with {Moves + 1} commands: {ready} bytes (about {perAction} bytes a command); "
                + $"the frame limit of {LanLimits.MaxFrameBytes} bytes holds about {LanLimits.MaxFrameBytes / Math.Max(1, perAction)} commands.");
            Assert.Less(ready, LanLimits.MaxFrameBytes, "Thousands of actions in one round still fit in a frame.");

            Assert.IsTrue(guest.Ready().Ok);
            for (int i = 0; i < 3; i++)
            {
                host.Update();
                guest.Update();
            }
            Assert.AreEqual(LanMatchState.ReadyToFight, guest.State, guest.LastError);
            Assert.AreEqual(LanMatchState.ReadyToFight, host.State, host.LastError);
            Assert.AreEqual(host.LocalHash, guest.LocalHash);
        }
    }
}
