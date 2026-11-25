// LanRematchTests.cs
// Tests New game after a LAN match (the coordinator's rematch step), over the in-memory channel pair and over loopback TCP:
//   two whole AI matches back to back on one channel through New game, no desync, the second with another seed (the guest pressing
//   first, then the host; and the other way round), the old coordinators reading nothing more and their Leave leaving the channel open;
//   one side going back to the menu instead gives the other "went back to the menu" and no new game; the host's next MatchStart that
//   arrives before the guest pressed New game is kept (put back in front) with what follows it, and the guest then sends no Rematch of
//   its own; the host's Rematch that arrives during the guest's last battle is kept; the connection dropping while one side waits ends
//   the wait (OpponentGone) without an error; a Rematch or MatchStart never breaks the match it reaches.
//   Also the helpers: the next seed (-lanDemoSeed plus k, or a fresh one, never the last), the settings kept but the seed,
//   -lanDemoMatches, the buffered channel unwrapped when put back again, and the "[LAN] rematch k" line.
// The short matches clear the guest's squads and nearly empty its base on both machines in the same way after the draft (so the
// states still agree), and both sides then press Ready with no decisions: the host's starting army ends it in a round or a few.
// Depends on: Halobellum.Net (LanMatchCoordinator, LanMatchStart, LanGuestStart, LanMemoryChannel, LanSession, LanBufferedChannel,
//             LanDemoArgs, LanMatchLog), Halobellum.Economy, Halobellum.Core, NUnit, LockstepTestKit (AiSeat), NetTestKit, TestRules.

using System;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanRematchTests
    {
        private const string Memory = "memory", Tcp = "tcp";

        /// <summary>The two ends of one connection: the in-memory pair or two loopback TCP sessions.</summary>
        private sealed class Link : IDisposable
        {
            public ILanChannel Host, Guest;
            public LanMemoryChannel MemoryHost, MemoryGuest;
            public LanSession SessionHost, SessionGuest;

            public bool IsTcp => SessionHost != null;

            public static Link Open(string transport)
            {
                var link = new Link();
                if (transport == Tcp)
                {
                    LockstepTestKit.ConnectSessions(out link.SessionHost, out link.SessionGuest);
                    Assert.AreEqual(LanSessionState.Connected, link.SessionGuest.State, link.SessionGuest.Reason);
                    link.Host = link.SessionHost;
                    link.Guest = link.SessionGuest;
                }
                else
                {
                    LanMemoryChannel.CreatePair(out link.MemoryHost, out link.MemoryGuest);
                    link.Host = link.MemoryHost;
                    link.Guest = link.MemoryGuest;
                }
                return link;
            }

            /// <summary>The guest's game goes away without a Leave (a pulled cable, a crash).</summary>
            public void DropGuest()
            {
                if (IsTcp) SessionGuest.Close();
                else MemoryGuest.Drop("The cable was pulled.");
            }

            public void Dispose()
            {
                SessionGuest?.Close();
                SessionHost?.Close();
            }
        }

        /// <summary>Updates the coordinators until the condition holds; fails the test after the time limit.</summary>
        private static void Step(Func<bool> condition, string what, params LanMatchCoordinator[] coordinators)
        {
            Assert.IsTrue(NetTestKit.Until(() =>
            {
                foreach (LanMatchCoordinator c in coordinators) c.Update();
                return condition();
            }, 20000), "Timed out: " + what);
        }

        private static LanGuestStart GuestStart(ILanChannel channel) => new LanGuestStart(channel, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions());

        private static LanMatchCoordinator BeginAsHost(ILanChannel channel, LanMatchSettings settings) =>
            LanMatchStart.BeginAsHost(channel, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), settings);

        private static LanMatchCoordinator StartGuest(ILanChannel channel)
        {
            LanGuestStart start = GuestStart(channel);
            Assert.IsTrue(NetTestKit.Until(() =>
            {
                start.Poll();
                return start.State != LanGuestStartState.Waiting;
            }, 20000), "The guest got no MatchStart.");
            Assert.AreEqual(LanGuestStartState.Started, start.State, start.Reason);
            return start.Coordinator;
        }

        /// <summary>A whole match with the Insane AI on both sides: no desync, the same winner and the same state on both machines.</summary>
        private static void PlayWholeMatch(LanMatchCoordinator h, LanMatchCoordinator g, int aiSeed, bool tcp, string where)
        {
            var host = new AiSeat(h, aiSeed);
            var guest = new AiSeat(g, aiSeed + 1);
            LockstepTestKit.PlayOut(host, guest, pause: tcp, order: tcp ? null : new Random(aiSeed));
            Assert.IsFalse(h.IsFinished, where + ": the host stopped: " + h.State + " " + h.LastError);
            Assert.IsFalse(g.IsFinished, where + ": the guest stopped: " + g.State + " " + g.LastError);
            Assert.AreEqual(LanMatchState.MatchOver, h.State, where);
            Assert.AreEqual(LanMatchState.MatchOver, g.State, where);
            Assert.AreEqual(h.Match.Winner, g.Match.Winner, where + ": the same winner.");
            Assert.AreEqual(LockstepTestKit.HashOf(h.Match), LockstepTestKit.HashOf(g.Match), where + ": the same final state.");
            CollectionAssert.AreEquivalent(host.BattleHashes, guest.BattleHashes, where + ": the same state before every battle.");
            CollectionAssert.AreEquivalent(host.ResultHashes, guest.ResultHashes, where + ": the same state after every result.");
        }

        /// <summary>
        /// A short match to its end (see the file's header). With <paramref name="hostAsksEarly"/>, the host presses New game as soon as
        /// its last round is finished, and its Rematch reaches the guest while the guest's last battle is still on.
        /// </summary>
        private static void PlayShortMatch(LanMatchCoordinator h, LanMatchCoordinator g, bool hostAsksEarly = false)
        {
            Assert.IsTrue(h.ChooseDraft(0).Ok);
            Assert.IsTrue(g.ChooseDraft(0).Ok);
            Step(() => h.State == LanMatchState.Deployment && g.State == LanMatchState.Deployment, "both reach round 1", h, g);
            foreach (Match m in new[] { h.Match, g.Match })
            {
                m.Enemy.Squads.Clear();
                m.Enemy.BaseHealth = 1;
            }
            for (int round = 0; round < 20 && h.Match.Phase != MatchPhase.MatchOver; round++)
            {
                foreach (LanMatchCoordinator c in new[] { h, g })
                {
                    if (c.Match.InCards(c.LocalTeam)) c.Match.SkipCards(c.LocalTeam);
                    Assert.IsTrue(c.Ready().Ok, c.StatusText);
                }
                Step(() => h.CanStartBattle && g.CanStartBattle, "both agree on round " + h.Match.Round, h, g);
                h.StartBattle().Run();
                g.StartBattle().Run();
                Assert.IsTrue(h.FinishBattle());
                if (hostAsksEarly && h.Match.Phase == MatchPhase.MatchOver)
                {
                    Assert.IsTrue(h.RequestRematch(), "The host's match is over: it may ask.");
                    Step(() => g.OpponentWantsRematch, "the host's Rematch reaches the guest during its last battle", g);
                    Assert.AreEqual(MatchPhase.Battle, g.Match.Phase, "The guest's last battle is still on.");
                    Assert.IsFalse(g.CanRematch, "Not before its own match is over.");
                    Assert.IsFalse(g.IsFinished);
                }
                g.FinishBattle();
                Step(() => g.Match.Phase != MatchPhase.Battle, "the host's result finishes the guest's round", g);
                if (h.Match.Phase == MatchPhase.Result)
                {
                    h.ContinueAfterResult();
                    g.ContinueAfterResult();
                }
            }
            Assert.IsFalse(h.IsFinished, h.LastError);
            Assert.IsFalse(g.IsFinished, g.LastError);
            Assert.AreEqual(LanMatchState.MatchOver, h.State);
            Assert.AreEqual(LanMatchState.MatchOver, g.State);
            Assert.AreEqual(Team.A, h.Match.Winner);
            Assert.AreEqual(h.Match.Winner, g.Match.Winner);
        }

        /// <summary>
        /// New game on both sides, the first one first, then the next match: the host starts it on the channel it is handed, the guest
        /// reads its MatchStart. The old coordinators read nothing more, and their Leave (the old scene going away) keeps the channel open.
        /// </summary>
        private static void Rematch(Link link, LanMatchCoordinator h, LanMatchCoordinator g, LanMatchSettings next, bool guestFirst,
            out LanMatchCoordinator h2, out LanMatchCoordinator g2)
        {
            LanMatchCoordinator first = guestFirst ? g : h, second = guestFirst ? h : g;
            string firstName = guestFirst ? "Guesty" : "Hosty";
            Assert.IsTrue(first.CanRematch);
            Assert.AreEqual("", first.RematchStatus("Other"));
            Assert.IsTrue(first.RequestRematch());
            Assert.IsTrue(first.LocalWantsRematch);
            Assert.IsFalse(first.RematchAgreed);
            Assert.IsNull(first.TakeRematchChannel(), "No channel before both pressed.");
            Assert.AreEqual("Waiting for Other...", first.RematchStatus("Other"));
            Step(() => second.OpponentWantsRematch, "the first New game reaches the other game", second);
            Assert.AreEqual(firstName + " wants a new game.", second.RematchStatus(firstName));
            Assert.IsFalse(second.RematchAgreed);
            Assert.IsTrue(second.RequestRematch());

            Step(() => h.RematchAgreed, "the host sees both want a new game", h);
            ILanChannel hostNext = h.TakeRematchChannel();
            Assert.AreSame(link.Host, hostNext, "The host goes on with the same channel.");
            Assert.IsTrue(h.HandedOver);
            Assert.IsNull(h.TakeRematchChannel(), "Handed over once.");
            h2 = BeginAsHost(hostNext, next);

            Step(() => g.RematchAgreed, "the guest sees both want a new game", g);
            if (guestFirst && !link.IsTcp)
                Assert.AreEqual(1, link.MemoryGuest.Pending, "The host's Rematch is read; its MatchStart right behind it is left in the channel.");
            ILanChannel guestNext = g.TakeRematchChannel();
            Assert.AreSame(link.Guest, guestNext, "Nothing held: the guest goes on with the same channel.");

            // The old scene goes away: its coordinator's Leave neither sends nor closes, and its Update reads nothing.
            h.Leave();
            g.Leave();
            h.Update();
            g.Update();
            Assert.IsTrue(link.Host.IsOpen && link.Guest.IsOpen, "The channel stays open for the next match.");
            Assert.AreEqual(LanMatchState.MatchOver, h.State);
            Assert.AreEqual(LanMatchState.MatchOver, g.State);

            g2 = StartGuest(guestNext);
            Assert.AreEqual(Team.A, h2.LocalTeam);
            Assert.AreEqual(Team.B, g2.LocalTeam);
            Assert.AreEqual(next.Seed, g2.Match.Seed, "The host's new seed.");
            Assert.AreEqual(LockstepTestKit.HashOf(h2.Match), LockstepTestKit.HashOf(g2.Match), "The same new match on both machines.");
        }

        // ------------------------------------------------------------------ whole matches back to back

        [TestCase(Memory, true)]
        [TestCase(Memory, false)]
        [TestCase(Tcp, true)]
        public void TwoWholeMatches_BackToBack_ThroughNewGame_OnOneChannel(string transport, bool guestFirst)
        {
            using Link link = Link.Open(transport);
            LanMatchSettings settings = LockstepTestKit.Settings(23);
            LockstepTestKit.Start(link.Host, link.Guest, settings, out LanMatchCoordinator h, out LanMatchCoordinator g);
            string firstFresh = LockstepTestKit.HashOf(h.Match);
            PlayWholeMatch(h, g, 24, link.IsTcp, "Match 1");

            LanMatchSettings next = settings.WithSeed(LanMatchSettings.NextSeed(settings.Seed, null, 1, Environment.TickCount & 0x7FFFFFFF));
            Assert.AreNotEqual(23, next.Seed, "Another seed.");
            Rematch(link, h, g, next, guestFirst, out LanMatchCoordinator h2, out LanMatchCoordinator g2);
            Assert.AreEqual(settings.Mode, h2.Match.Rules.Mode);
            Assert.AreEqual(LockstepTestKit.WholeMatchBaseHealth, h2.Match.Rules.BaseHealth, "The same base health.");
            Assert.AreEqual(LockstepTestKit.WholeMatchBaseHealth, g2.Match.Rules.BaseHealth);
            Assert.AreNotEqual(firstFresh, LockstepTestKit.HashOf(h2.Match), "A new match, not the old one again.");
            Assert.AreEqual(MatchPhase.Draft, g2.Match.Phase);

            PlayWholeMatch(h2, g2, 34, link.IsTcp, "Match 2");
            Assert.IsTrue(link.Host.IsOpen && link.Guest.IsOpen);
            TestContext.WriteLine($"{transport}: match 1 seed 23, winner {h.Match.Winner} in {h.Match.Round} rounds; match 2 seed {next.Seed}, winner {h2.Match.Winner} in {h2.Match.Round} rounds.");
        }

        // ------------------------------------------------------------------ one side goes back to the menu

        [TestCase(Memory, true)]
        [TestCase(Memory, false)]
        [TestCase(Tcp, true)]
        public void OneSideGoingBackToTheMenu_TellsTheOther_AndNoNewGameStarts(string transport, bool guestLeaves)
        {
            using Link link = Link.Open(transport);
            LockstepTestKit.Start(link.Host, link.Guest, LockstepTestKit.Settings(41), out LanMatchCoordinator h, out LanMatchCoordinator g);
            PlayShortMatch(h, g);
            LanMatchCoordinator stays = guestLeaves ? h : g, leaves = guestLeaves ? g : h;
            Assert.IsTrue(stays.RequestRematch());
            Step(() => leaves.OpponentWantsRematch, "the New game reaches the other game", leaves);
            leaves.Leave(); // Main Menu
            Assert.IsFalse(leaves.CanRematch);
            Assert.IsFalse(leaves.RequestRematch(), "It left.");

            Step(() => stays.OpponentGone, "the other game's Leave arrives", stays);
            Assert.AreEqual(LanMatchState.MatchOver, stays.State, "Not an error: the match was over.");
            Assert.IsFalse(stays.IsFinished);
            Assert.AreEqual(LanOpponentStep.Left, stays.OpponentStep);
            Assert.IsFalse(stays.CanRematch);
            Assert.IsFalse(stays.RematchAgreed);
            Assert.IsNull(stays.TakeRematchChannel());
            Assert.IsFalse(stays.RequestRematch());
            Assert.AreEqual("Pat went back to the menu.", stays.RematchStatus("Pat"));
            stays.Leave();
            Assert.IsFalse(stays.Channel.IsOpen, "Main Menu closes the channel.");
        }

        [Test]
        public void GoingBackToTheMenu_BeforeTheOtherAsked_StillTellsIt()
        {
            using Link link = Link.Open(Memory);
            LockstepTestKit.Start(link.Host, link.Guest, LockstepTestKit.Settings(43), out LanMatchCoordinator h, out LanMatchCoordinator g);
            PlayShortMatch(h, g);
            g.Leave();
            h.Update();
            Assert.IsTrue(h.OpponentGone);
            Assert.AreEqual("Guesty went back to the menu.", h.RematchStatus("Guesty"));
            Assert.IsFalse(h.RequestRematch(), "New game is greyed out.");
            Assert.AreEqual(0, link.MemoryGuest.Pending, "Nothing sent.");
        }

        // ------------------------------------------------------------------ messages that come early

        [Test]
        public void TheHostsNextMatchStart_BeforeTheGuestPressedNewGame_IsKept_WithWhatFollows()
        {
            using Link link = Link.Open(Memory);
            LockstepTestKit.Start(link.Host, link.Guest, LockstepTestKit.Settings(47), out LanMatchCoordinator h, out LanMatchCoordinator g);
            PlayShortMatch(h, g);
            Assert.IsTrue(h.RequestRematch());
            // A host that does not wait for the guest's New game: its next MatchStart, then its starting card, go out at once.
            LanMatchCoordinator h2 = BeginAsHost(link.Host, LockstepTestKit.Settings(48));
            Assert.IsTrue(h2.ChooseDraft(0).Ok);
            Assert.AreEqual(3, link.MemoryGuest.Pending, "Rematch, MatchStart, DraftPick.");

            g.Update();
            g.Update();
            Assert.IsTrue(g.OpponentWantsRematch);
            Assert.IsFalse(g.LocalWantsRematch);
            Assert.IsFalse(g.RematchAgreed);
            Assert.IsFalse(g.IsFinished, "Not a protocol error: " + g.LastError);
            Assert.AreEqual(LanMatchState.MatchOver, g.State);
            Assert.AreEqual(1, link.MemoryGuest.Pending, "The old match stops reading at the MatchStart: the starting card behind it waits.");
            Assert.AreEqual("Hosty wants a new game.", g.RematchStatus("Hosty"));

            int sent = link.MemoryGuest.SentCount;
            Assert.IsTrue(g.RequestRematch());
            Assert.AreEqual(sent, link.MemoryGuest.SentCount, "No Rematch of its own: it would reach the next match.");
            Assert.IsTrue(g.RematchAgreed);
            ILanChannel next = g.TakeRematchChannel();
            Assert.IsInstanceOf<LanBufferedChannel>(next);
            Assert.AreEqual(1, ((LanBufferedChannel)next).PutBack, "The MatchStart is put back in front.");
            Assert.AreSame(link.Guest, ((LanBufferedChannel)next).Inner);

            LanMatchCoordinator g2 = StartGuest(next);
            Assert.AreEqual(48, g2.Match.Seed);
            g2.Update();
            Assert.IsTrue(g2.OpponentPickedDraft, "The host's starting card was not lost.");
            Assert.IsTrue(g2.ChooseDraft(0).Ok);
            Step(() => h2.State == LanMatchState.Deployment, "the guest's card reaches the host", h2);
            Assert.AreEqual(LanMatchState.Deployment, g2.State);
            Assert.AreEqual(LockstepTestKit.HashOf(h2.Match), LockstepTestKit.HashOf(g2.Match));
            Assert.AreEqual(1, h2.Match.Round, "Round 1 of the new match.");
            h2.Update();
            Assert.IsFalse(h2.OpponentWantsRematch, "No Rematch from the old match reached the new one.");
        }

        [Test]
        public void TheHostsRematch_DuringTheGuestsLastBattle_IsKept()
        {
            using Link link = Link.Open(Memory);
            LockstepTestKit.Start(link.Host, link.Guest, LockstepTestKit.Settings(53), out LanMatchCoordinator h, out LanMatchCoordinator g);
            PlayShortMatch(h, g, hostAsksEarly: true);
            Assert.IsTrue(g.OpponentWantsRematch);
            Assert.AreEqual("Hosty wants a new game.", g.RematchStatus("Hosty"));
            Assert.IsTrue(g.RequestRematch());
            Assert.IsTrue(g.RematchAgreed, "Both asked.");
            Step(() => h.RematchAgreed, "the guest's New game reaches the host", h);
        }

        [Test]
        public void ARematchOrMatchStart_DuringTheMatch_DoesNotStartAnything()
        {
            using Link link = Link.Open(Memory);
            LockstepTestKit.Start(link.Host, link.Guest, LockstepTestKit.Settings(59), out LanMatchCoordinator h, out LanMatchCoordinator g);
            link.Guest.Send(new NetMessage(LanMessageTypes.Rematch));
            h.Update();
            Assert.IsFalse(h.IsFinished, "A Rematch is only noted.");
            Assert.IsFalse(h.CanRematch, "The match is not over.");
            Assert.IsFalse(h.RequestRematch());
            Assert.AreEqual("", h.RematchStatus("Guesty"));

            link.Host.Send(new LanMatchStart { Settings = LockstepTestKit.Settings(60) }.ToMessage());
            g.Update();
            Assert.AreEqual(LanMatchState.Desync, g.State, "A MatchStart during the match breaks the protocol.");
            StringAssert.Contains("a match start during the match", g.LastError);
        }

        // ------------------------------------------------------------------ the connection drops while waiting

        [TestCase(Memory)]
        [TestCase(Tcp)]
        public void TheConnectionDropping_WhileWaitingForANewGame_EndsTheWait(string transport)
        {
            using Link link = Link.Open(transport);
            LockstepTestKit.Start(link.Host, link.Guest, LockstepTestKit.Settings(61), out LanMatchCoordinator h, out LanMatchCoordinator g);
            PlayShortMatch(h, g);
            Assert.IsTrue(h.RequestRematch());
            Assert.AreEqual("Waiting for Guesty...", h.RematchStatus("Guesty"));
            link.DropGuest();
            Step(() => h.OpponentGone, "the host notices the connection is gone", h);
            Assert.AreEqual(LanMatchState.MatchOver, h.State, "Not an error: the match was over.");
            Assert.IsFalse(h.IsFinished);
            Assert.IsNotEmpty(h.LastError);
            Assert.IsFalse(h.CanRematch);
            Assert.IsFalse(h.RematchAgreed);
            Assert.IsNull(h.TakeRematchChannel());
            Assert.AreEqual("Guesty went back to the menu.", h.RematchStatus("Guesty"));
            if (!link.IsTcp)
            {
                g.Update();
                Assert.IsTrue(g.OpponentGone);
                Assert.IsFalse(g.RequestRematch());
                Assert.AreEqual("Hosty went back to the menu.", g.RematchStatus("Hosty"));
            }
        }

        [Test]
        public void TheConnectionDropping_AfterTheMatchStartWasKept_EndsTheWaitToo()
        {
            using Link link = Link.Open(Memory);
            LockstepTestKit.Start(link.Host, link.Guest, LockstepTestKit.Settings(67), out LanMatchCoordinator h, out LanMatchCoordinator g);
            PlayShortMatch(h, g);
            BeginAsHost(link.Host, LockstepTestKit.Settings(68));
            g.Update();
            Assert.IsTrue(g.OpponentWantsRematch);
            link.MemoryHost.Drop("The cable was pulled.");
            g.Update();
            Assert.IsTrue(g.OpponentGone, "A closed channel is still noticed while the MatchStart is kept.");
            Assert.IsFalse(g.RequestRematch());
            Assert.AreEqual("Hosty went back to the menu.", g.RematchStatus("Hosty"));
        }

        // ------------------------------------------------------------------ helpers

        [Test]
        public void TheNextSeed_IsTheDemoSeedPlusK_OrAFreshOne_NeverTheLast()
        {
            Assert.AreEqual(24, LanMatchSettings.NextSeed(23, 23, 1, 999));
            Assert.AreEqual(25, LanMatchSettings.NextSeed(24, 23, 2, 999));
            Assert.AreEqual(999, LanMatchSettings.NextSeed(23, null, 1, 999));
            Assert.AreEqual(1000, LanMatchSettings.NextSeed(999, null, 1, 999), "Never the same seed again.");
            var settings = new LanMatchSettings { Seed = 5, Mode = MatchMode.UnlockSkirmish, BaseHealth = 1500, Cards = CardFilter.NoReinforcements };
            LanMatchSettings next = settings.WithSeed(6);
            Assert.AreEqual(6, next.Seed);
            Assert.AreEqual(MatchMode.UnlockSkirmish, next.Mode);
            Assert.AreEqual(1500, next.BaseHealth);
            Assert.AreEqual(CardFilter.NoReinforcements, next.Cards);
            Assert.AreEqual(5, settings.Seed, "The last match's settings are not changed.");
        }

        [Test]
        public void LanDemoMatches_IsReadAndKeptInBounds()
        {
            Assert.AreEqual(1, LanDemoArgs.Parse(new[] { "-lanDemo", "host" }).Matches);
            Assert.AreEqual(3, LanDemoArgs.Parse(new[] { "-lanDemo", "host", "-lanDemoMatches", "3" }).Matches);
            Assert.AreEqual(1, LanDemoArgs.Parse(new[] { "-lanDemo", "join", "-lanDemoMatches", "0" }).Matches);
            Assert.AreEqual(1, LanDemoArgs.Parse(new[] { "-lanDemoMatches", "lots" }).Matches);
            Assert.AreEqual(LanDemoArgs.MostMatches, LanDemoArgs.Parse(new[] { "-lanDemoMatches", "99999" }).Matches);
            Assert.AreEqual("[LAN] rematch 2", LanMatchLog.Rematch(2));
        }

        [Test]
        public void PuttingBackAgain_UnwrapsAnEmptyBufferedChannel()
        {
            LanMemoryChannel.CreatePair(out LanMemoryChannel a, out LanMemoryChannel b);
            var first = new LanBufferedChannel(b, new[] { new NetMessage("One") });
            LanBufferedChannel kept = LanBufferedChannel.PutBackInFront(first, new NetMessage("Two"));
            Assert.AreSame(first, kept.Inner, "Something is still put back in the first one: it stays.");
            Assert.IsTrue(kept.TryReceive(out NetMessage m));
            Assert.AreEqual("Two", m.Type);
            Assert.IsTrue(kept.TryReceive(out m));
            Assert.AreEqual("One", m.Type);
            LanBufferedChannel again = LanBufferedChannel.PutBackInFront(kept, new NetMessage("Three"));
            Assert.AreSame(b, again.Inner, "Emptied wrappers are dropped, all of them.");
            LanBufferedChannel third = LanBufferedChannel.PutBackInFront(new LanBufferedChannel(b), new NetMessage("Four"));
            Assert.AreSame(b, third.Inner);
            a.Send(new NetMessage("Five"));
            Assert.IsTrue(third.TryReceive(out m));
            Assert.AreEqual("Four", m.Type);
            Assert.IsTrue(third.TryReceive(out m));
            Assert.AreEqual("Five", m.Type);
        }
    }
}
