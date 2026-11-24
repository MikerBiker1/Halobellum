// LanMatchCoordinatorTests.cs
// Tests the lockstep coordinator (Milestone 11): two machines, each with its own Match and its own side played by the Insane AI,
// play whole matches through two coordinators, over the in-memory channel pair for several seeds (and an Unlock Skirmish, each in
// a seeded random stepping order so the messages arrive in many orders, plus one in the fixed order) and over a real loopback TCP
// LanSession pair: no desync, the same winner, and the same state hash before every battle and after every result. Also: the guest builds the host's match from MatchStart (and the host's first message after it is not lost); the
// handshake and the match start are refused on a content mismatch; a guest whose own battle ended differently goes on with the
// host's result and says so; a connection dropped mid-round gives ConnectionLost on the other side (in memory and over TCP); a
// Leave gives OpponentLeft, but the host's last result still finishes the guest's round; messages that arrive early (the other
// side's Ready before the local one, the next round's Ready during this round's battle) wait their turn; a forged command or a
// state that differs gives Desync on both sides; a local action after the lists are applied gives Desync before the battle;
// broken command lists, starting cards and battle results (cut-off JSON and the like) give Desync, never an exception; Leave
// after a final state or MatchOver keeps that state and closes the channel; the host leaving after its last result gives the
// guest OpponentGone with the host's reason; and the MatchStart message reads back exactly.
// Depends on: Halobellum.Net (LanMatchCoordinator, LanMatchStart, LanGuestStart, LanMemoryChannel, LanSession, LanContent),
//             Halobellum.Economy, Halobellum.Core, NUnit, LockstepTestKit (AiSeat), NetTestKit, TestRules.

using System;
using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class LanMatchCoordinatorTests
    {
        // ------------------------------------------------------------------ whole matches

        private static void AssertWholeMatch(AiSeat host, AiSeat guest, string where)
        {
            LanMatchCoordinator h = host.Coordinator, g = guest.Coordinator;
            Assert.IsFalse(h.IsFinished, where + ": the host stopped: " + h.State + " " + h.LastError);
            Assert.IsFalse(g.IsFinished, where + ": the guest stopped: " + g.State + " " + g.LastError);
            Assert.AreEqual(LanMatchState.MatchOver, h.State);
            Assert.AreEqual(LanMatchState.MatchOver, g.State);
            Assert.IsTrue(h.Match.Winner.HasValue);
            Assert.AreEqual(h.Match.Winner, g.Match.Winner, "The same winner on both machines.");
            Assert.AreEqual(h.Match.Round, g.Match.Round);
            Assert.IsFalse(g.HostResultUsed, "The same battle on both machines, so the guest's own result agreed every round.");
            Assert.AreEqual(LockstepTestKit.HashOf(h.Match), LockstepTestKit.HashOf(g.Match), "The same final state.");
            Assert.Greater(host.ResultHashes.Count, 0);
            CollectionAssert.AreEquivalent(host.BattleHashes, guest.BattleHashes, "The same state before every battle.");
            CollectionAssert.AreEquivalent(host.ResultHashes, guest.ResultHashes, "The same state after every result.");
            Assert.AreEqual(h.Match.Round, host.ResultHashes.Count, "A result for every round.");
        }

        [TestCase(23, MatchMode.Faction)]
        [TestCase(31, MatchMode.Faction)]
        [TestCase(17, MatchMode.Faction)]
        [TestCase(5, MatchMode.UnlockSkirmish)]
        public void WholeMatches_OverTheMemoryPair_StayInStep(int seed, MatchMode mode)
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(seed, mode), out LanMatchCoordinator h, out LanMatchCoordinator g, out _, out _);
            Assert.AreEqual(LockstepTestKit.HashOf(h.Match), LockstepTestKit.HashOf(g.Match), "The same match on both machines.");
            var host = new AiSeat(h, seed + 1);
            var guest = new AiSeat(g, seed + 2);
            // Each frame steps the two machines in a random order (or only one of them), so the messages come in many orders.
            LockstepTestKit.PlayOut(host, guest, order: new Random(seed));
            AssertWholeMatch(host, guest, "Seed " + seed);
            // The decisions went across: each machine saw the other side field more than its two starting squads.
            Assert.Greater(host.MostRemoteSquads, 2);
            Assert.Greater(guest.MostRemoteSquads, 2);
            // The orders that the fixed host-then-guest stepping never gives came up during the match.
            Assert.Greater(host.RemoteReadyFirst, 0, "The guest's Ready reached the host before the host's own in some round.");
            Assert.Greater(guest.RemoteReadyFirst, 0, "The host's Ready reached the guest before the guest's own in some round.");
            Assert.Greater(guest.WaitedForHostResult, 0, "The guest finished its battle and waited for the host's result.");
            TestContext.WriteLine($"Seed {seed} ({mode}): {h.Match.Round} rounds, winner {h.Match.Winner}; remote Ready first {host.RemoteReadyFirst} (host), "
                + $"{guest.RemoteReadyFirst} (guest); guest waited in {guest.WaitedForHostResult} round(s) for the host's result.");
        }

        [Test]
        public void WholeMatch_InTheFixedOrder_StaysInStep()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(23), out LanMatchCoordinator h, out LanMatchCoordinator g, out _, out _);
            var host = new AiSeat(h, 24);
            var guest = new AiSeat(g, 25);
            LockstepTestKit.PlayOut(host, guest);
            AssertWholeMatch(host, guest, "Fixed order");
        }

        [Test]
        public void AWholeMatch_OverLoopbackTcp_StaysInStep()
        {
            LockstepTestKit.ConnectSessions(out LanSession atHost, out LanSession atGuest);
            try
            {
                Assert.AreEqual(LanSessionState.Connected, atGuest.State, atGuest.Reason);
                LockstepTestKit.Start(atHost, atGuest, LockstepTestKit.Settings(23), out LanMatchCoordinator h, out LanMatchCoordinator g);
                var host = new AiSeat(h, 24);
                var guest = new AiSeat(g, 25);
                LockstepTestKit.PlayOut(host, guest, pause: true);
                AssertWholeMatch(host, guest, "Over TCP");
            }
            finally
            {
                atGuest.Close();
                atHost.Close();
            }
        }

        // ------------------------------------------------------------------ the start

        [Test]
        public void TheGuest_BuildsTheHostsMatch_AndTheHostsFirstPickIsNotLost()
        {
            LanMemoryChannel.CreatePair(out LanMemoryChannel hostEnd, out LanMemoryChannel guestEnd);
            LanMatchSettings settings = LockstepTestKit.Settings(77, baseHealth: 700);
            settings.Cards = CardFilter.NoReinforcements;
            LanMatchCoordinator host = LanMatchStart.BeginAsHost(hostEnd, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), settings);
            Assert.IsTrue(host.ChooseDraft(1).Ok, "The host picks before the guest has even read the MatchStart.");
            Assert.IsTrue(host.WaitingForOpponent);

            var start = new LanGuestStart(guestEnd, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions());
            start.Poll();
            Assert.AreEqual(LanGuestStartState.Started, start.State, start.Reason);
            Assert.AreEqual(77, start.Settings.Seed);
            Assert.AreEqual(700, start.Settings.BaseHealth);
            Assert.AreEqual(CardFilter.NoReinforcements, start.Settings.Cards);
            Assert.AreEqual(700, start.Match.Rules.BaseHealth, "The host's base health is used.");
            Assert.AreEqual(CardFilter.NoReinforcements, start.Match.Rules.Cards);
            Assert.AreEqual(1, guestEnd.Pending, "The host's starting card is left for the coordinator.");

            LanMatchCoordinator guest = start.Coordinator;
            Assert.AreSame(start.Match, guest.Match);
            Assert.IsTrue(guest.Match.IsTwoHumans);
            guest.Update();
            Assert.IsTrue(guest.OpponentPickedDraft);
            Assert.AreEqual(MatchPhase.Draft, guest.Match.Phase, "The host's card waits until the guest has picked its own.");
            Assert.IsFalse(guest.Match.HasChosenDraft(Team.A), "Not applied before the guest's own pick.");
            Assert.IsTrue(guest.ChooseDraft(0).Ok);
            Assert.AreEqual(LanMatchState.Deployment, guest.State);
            host.Update();
            Assert.AreEqual(LanMatchState.Deployment, host.State);
            Assert.AreEqual(1, host.Match.Round);
            Assert.AreEqual(LockstepTestKit.HashOf(host.Match), LockstepTestKit.HashOf(guest.Match));
            Assert.IsFalse(host.ChooseDraft(0).Ok, "The draft is over.");
        }

        [Test]
        public void TheHandshake_IsRefused_OnAContentMismatch_AndNamesTheFaction()
        {
            List<FactionInfo> theirs = LockstepTestKit.Factions();
            theirs[1].Cards.Add(new CardStats { Name = "Extra", Type = CardType.Economy, Cost = 10, BonusIncome = 5 });
            string mine = LanContent.Describe(LockstepTestKit.Factions(), LockstepTestKit.Rules(), TestRules.Default());
            string other = LanContent.Describe(theirs, LockstepTestKit.Rules(), TestRules.Default());
            StringAssert.StartsWith("UNSC=", mine);
            Assert.AreEqual(mine, LanContent.Describe(LockstepTestKit.Factions(), LockstepTestKit.Rules(), TestRules.Default()), "The same content gives the same text.");

            LockstepTestKit.ConnectSessions(out LanSession atHost, out LanSession atGuest, hostContent: mine, guestContent: other);
            try
            {
                Assert.AreEqual(LanSessionState.Refused, atGuest.State);
                StringAssert.Contains("Unit stats differ: Covenant.", atGuest.Reason);
                Assert.IsFalse(atGuest.IsOpen);
                Assert.AreEqual(atGuest.Reason, atGuest.LostReason);
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    atHost.Poll();
                    return atHost.LastRefusal.Length > 0;
                }));
                Assert.AreEqual(LanSessionState.Waiting, atHost.State, "The host keeps waiting for a matching guest.");
            }
            finally
            {
                atGuest.Close();
                atHost.Close();
            }
        }

        [Test]
        public void TheMatchStart_IsRefused_OnAContentMismatch_AndTheHostIsTold()
        {
            LanMemoryChannel.CreatePair(out LanMemoryChannel hostEnd, out LanMemoryChannel guestEnd);
            LanMatchCoordinator host = LanMatchStart.BeginAsHost(hostEnd, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(tankDamage: 11f),
                LockstepTestKit.Settings(9));
            var start = new LanGuestStart(guestEnd, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions());
            start.Poll();
            Assert.AreEqual(LanGuestStartState.Refused, start.State);
            StringAssert.Contains("different matches", start.Reason);
            Assert.IsNull(start.Coordinator);
            Assert.IsFalse(guestEnd.IsOpen, "The guest closes the channel after it refused.");

            host.Update();
            Assert.AreEqual(LanMatchState.OpponentLeft, host.State);
            StringAssert.StartsWith("The other game refused the match: ", host.LastError);
            StringAssert.Contains("unit stats files", host.LastError);
            Assert.IsTrue(host.IsFinished);
            Assert.IsFalse(host.ChooseDraft(0).Ok);
        }

        [Test]
        public void AMatchStartTheGuestCannotBuild_IsRefused_InsteadOfWaitingForEver()
        {
            LanMemoryChannel.CreatePair(out LanMemoryChannel hostEnd, out LanMemoryChannel guestEnd);
            LanMatchCoordinator host = LanMatchStart.BeginAsHost(hostEnd, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), LockstepTestKit.Settings(9));
            // Content the guest's factory cannot handle: building the match throws.
            var start = new LanGuestStart(guestEnd, LockstepTestKit.Rules(), TestRules.Default(), new List<FactionInfo> { null });
            start.Poll();
            Assert.AreEqual(LanGuestStartState.Refused, start.State, start.Reason);
            StringAssert.StartsWith("The host's match could not be built here: ", start.Reason);
            Assert.IsNull(start.Coordinator);
            Assert.IsFalse(guestEnd.IsOpen);

            host.Update();
            Assert.AreEqual(LanMatchState.OpponentLeft, host.State, "The host is told and does not wait at the draft.");
            StringAssert.Contains("could not be built here", host.LastError);
        }

        [Test]
        public void TheMatchStart_ReadsBack_AndAnUnknownModeIsAFormatError()
        {
            var start = new LanMatchStart { Settings = LockstepTestKit.Settings(-5, MatchMode.UnlockSkirmish, 1300), Content = ulong.MaxValue - 3, State = 1UL << 63 };
            start.Settings.Cards = CardFilter.OnlyReinforcements;
            LanMatchStart back = LanMatchStart.FromMessage(NetMessage.Parse(start.ToMessage().ToJson()));
            Assert.AreEqual(-5, back.Settings.Seed);
            Assert.AreEqual(MatchMode.UnlockSkirmish, back.Settings.Mode);
            Assert.AreEqual(1300, back.Settings.BaseHealth);
            Assert.AreEqual(CardFilter.OnlyReinforcements, back.Settings.Cards);
            Assert.AreEqual(start.Content, back.Content);
            Assert.AreEqual(start.State, back.State);
            Assert.AreEqual(LanDefaults.Protocol, back.Proto);

            NetMessage bad = start.ToMessage();
            bad.Set("mode", "Chess");
            Assert.Throws<FormatException>(() => LanMatchStart.FromMessage(bad));
            bad.Set("mode", "1");
            Assert.Throws<FormatException>(() => LanMatchStart.FromMessage(bad), "A number is not a mode name.");
            NetMessage missing = start.ToMessage();
            missing.Remove("seed");
            Assert.Throws<FormatException>(() => LanMatchStart.FromMessage(missing));

            // Another protocol is refused with words for both players.
            start.Proto = LanDefaults.Protocol + 1;
            StringAssert.Contains("LAN protocol", LanMatchStart.Check(start, start.Content, start.State));
        }

        // ------------------------------------------------------------------ helpers for one round

        /// <summary>Both sides pick their first starting card: round 1's deployment on both.</summary>
        private static void Draft(LanMatchCoordinator host, LanMatchCoordinator guest)
        {
            Assert.IsTrue(host.ChooseDraft(0).Ok);
            Assert.IsTrue(guest.ChooseDraft(0).Ok);
            host.Update();
            guest.Update();
            Assert.AreEqual(LanMatchState.Deployment, host.State);
            Assert.AreEqual(LanMatchState.Deployment, guest.State);
        }

        /// <summary>Both press Ready (host first) and exchange hashes: both may fight.</summary>
        private static void BothReady(LanMatchCoordinator host, LanMatchCoordinator guest)
        {
            Assert.IsTrue(host.Ready().Ok);
            Assert.IsTrue(guest.Ready().Ok);
            Frames(host, guest);
            Assert.AreEqual(LanMatchState.ReadyToFight, host.State, host.LastError);
            Assert.AreEqual(LanMatchState.ReadyToFight, guest.State, guest.LastError);
        }

        /// <summary>A few frames on both machines (each message needs one Update at the other end).</summary>
        private static void Frames(LanMatchCoordinator host, LanMatchCoordinator guest, int frames = 3)
        {
            for (int i = 0; i < frames; i++)
            {
                host.Update();
                guest.Update();
            }
        }

        private static UnitStats Infantry(Match match, Team team) => match.Get(team).Roster.Single(u => u.Name == "Infantry");

        // ------------------------------------------------------------------ the host's result

        [Test]
        public void AGuestWhoseBattleEndedDifferently_GoesOnWithTheHostsResult_AndSaysSo()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(41), out LanMatchCoordinator host, out LanMatchCoordinator guest, out _, out _);
            var finishedOnGuest = new List<RoundSummary>();
            guest.BattleFinished += finishedOnGuest.Add;
            Draft(host, guest);
            BothReady(host, guest);

            guest.StartBattle().Run();
            // Tamper with the guest's own battle: as if its floating point had come out differently.
            guest.Match.CurrentBattle.Result.TeamA.SurvivorBaseDamage += 300f;
            guest.Match.CurrentBattle.Result.TeamB.SurvivorBaseDamage += 300f;
            Assert.IsFalse(guest.FinishBattle(), "The guest waits for the host's result.");
            Assert.AreEqual(LanMatchState.WaitingForHostResult, guest.State);
            Assert.IsTrue(guest.WaitingForOpponent);
            Assert.AreEqual(0, finishedOnGuest.Count);

            host.StartBattle().Run();
            Assert.IsTrue(host.FinishBattle(), "The host's result is final at once.");
            Assert.AreEqual(LanMatchState.Result, host.State);

            guest.Update();
            Assert.AreEqual(LanMatchState.Result, guest.State, guest.LastError);
            Assert.IsTrue(guest.HostResultUsed, "The guest says its own battle ended differently.");
            CollectionAssert.AreEqual(new[] { 1 }, guest.HostResultRounds);
            StringAssert.Contains("host's result is used", guest.LastError);
            Assert.AreEqual(1, finishedOnGuest.Count);
            Assert.AreEqual(host.Match.Player.BaseHealth, guest.Match.Player.BaseHealth, "The host's base damage, not the guest's.");
            Assert.AreEqual(host.Match.Enemy.BaseHealth, guest.Match.Enemy.BaseHealth);
            Assert.AreEqual(LockstepTestKit.HashOf(host.Match), LockstepTestKit.HashOf(guest.Match), "The guest ends with the host's state.");
            Assert.IsFalse(host.HostResultUsed);

            // The match goes on.
            host.ContinueAfterResult();
            guest.ContinueAfterResult();
            host.Update();
            guest.Update();
            Assert.AreEqual(2, guest.Match.Round);
            Assert.AreEqual(LanMatchState.Deployment, guest.State);
        }

        [Test]
        public void TheHostsLastResult_StillFinishesTheGuestsRound_AfterTheHostLeft()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(43), out LanMatchCoordinator host, out LanMatchCoordinator guest, out _, out LanMemoryChannel guestEnd);
            Draft(host, guest);
            BothReady(host, guest);
            host.StartBattle().Run();
            Assert.IsTrue(host.FinishBattle());
            host.Leave();
            Assert.AreEqual(LanMatchState.Left, host.State);

            guest.Update();
            Assert.AreEqual(LanMatchState.ReadyToFight, guest.State, "The guest can still play the battle the host's result is for.");
            guest.StartBattle().Run();
            guest.Update();
            Assert.AreEqual(LanMatchState.Battle, guest.State);
            Assert.IsTrue(guest.FinishBattle());
            Assert.AreEqual(MatchPhase.Result, guest.Match.Phase, "The round finished with the host's result.");
            Assert.AreEqual(LockstepTestKit.HashOf(host.Match), LockstepTestKit.HashOf(guest.Match));
            Assert.AreEqual(LanMatchState.OpponentLeft, guest.State, "Then the other player is gone.");
            Assert.IsFalse(guestEnd.IsOpen);
        }

        [Test]
        public void TheHostLeaving_AfterItsLastResult_GivesOpponentGone_WithTheReason_AndLeaveKeepsMatchOver()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(23), out LanMatchCoordinator h, out LanMatchCoordinator g, out LanMemoryChannel hostEnd, out LanMemoryChannel guestEnd);
            var host = new AiSeat(h, 24);
            var guest = new AiSeat(g, 25);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // Step until the host's match is over; the guest does not step in the frame the host finished its last battle.
            while (!host.Done)
            {
                host.Step();
                if (!host.Done) guest.Step();
                Assert.Less(watch.ElapsedMilliseconds, 120000, "The match did not end in time.");
            }
            Assert.AreEqual(LanMatchState.MatchOver, h.State, h.LastError);
            Assert.AreNotEqual(MatchPhase.MatchOver, g.Match.Phase, "The guest has not read the host's last result yet.");

            h.Leave("The host went home.");
            Assert.AreEqual(LanMatchState.MatchOver, h.State, "Leaving after the match keeps MatchOver.");
            Assert.AreEqual("Match over.", h.StatusText);
            Assert.IsFalse(hostEnd.IsOpen, "The channel is closed.");

            for (int i = 0; i < 5 && g.State != LanMatchState.MatchOver; i++) guest.Step();
            Assert.AreEqual(LanMatchState.MatchOver, g.State, g.LastError);
            Assert.IsTrue(g.OpponentGone);
            Assert.AreEqual("The host went home.", g.LastError, "The reason the host gave, not an old message.");
            Assert.AreEqual(LockstepTestKit.HashOf(h.Match), LockstepTestKit.HashOf(g.Match));
            g.Leave();
            Assert.AreEqual(LanMatchState.MatchOver, g.State);
            Assert.AreEqual("The host went home.", g.LastError);
            Assert.IsFalse(guestEnd.IsOpen);
        }

        // ------------------------------------------------------------------ leaving and lost connections

        [Test]
        public void AConnectionDroppedMidRound_GivesConnectionLost_OnTheOtherSide()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(47), out LanMatchCoordinator host, out LanMatchCoordinator guest, out LanMemoryChannel hostEnd, out _);
            Draft(host, guest);
            Assert.IsTrue(host.Ready().Ok);
            hostEnd.Drop("The cable was pulled.");
            guest.Update();
            Assert.AreEqual(LanMatchState.ConnectionLost, guest.State);
            Assert.AreEqual("The cable was pulled.", guest.LastError);
            StringAssert.StartsWith("Connection lost.", guest.StatusText);
            Assert.IsTrue(guest.IsFinished);
            Assert.IsFalse(guest.Ready().Ok, "Nothing more to do once the connection is lost.");
            host.Update();
            Assert.AreEqual(LanMatchState.ConnectionLost, host.State);
        }

        [Test]
        public void AConnectionDroppedMidRound_OverTcp_GivesConnectionLost()
        {
            LockstepTestKit.ConnectSessions(out LanSession atHost, out LanSession atGuest);
            try
            {
                LockstepTestKit.Start(atHost, atGuest, LockstepTestKit.Settings(53), out LanMatchCoordinator host, out LanMatchCoordinator guest);
                Assert.IsTrue(host.ChooseDraft(0).Ok);
                Assert.IsTrue(guest.ChooseDraft(0).Ok);
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    host.Update();
                    guest.Update();
                    return host.State == LanMatchState.Deployment && guest.State == LanMatchState.Deployment;
                }), "Both reach round 1.");
                Assert.IsTrue(guest.Ready().Ok);
                atHost.Close(); // the host's game goes away without a Leave
                Assert.IsTrue(NetTestKit.Until(() =>
                {
                    guest.Update();
                    return guest.IsFinished;
                }), "The guest notices.");
                Assert.AreEqual(LanMatchState.ConnectionLost, guest.State, guest.LastError);
                Assert.IsNotEmpty(guest.LastError);
            }
            finally
            {
                atGuest.Close();
                atHost.Close();
            }
        }

        [Test]
        public void ALeave_GivesOpponentLeft_WithItsReason()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(59), out LanMatchCoordinator host, out LanMatchCoordinator guest, out _, out _);
            Draft(host, guest);
            guest.Leave("Mike went home.");
            Assert.AreEqual(LanMatchState.Left, guest.State);
            host.Update();
            Assert.AreEqual(LanMatchState.OpponentLeft, host.State);
            Assert.AreEqual("Mike went home.", host.LastError);
            host.Leave();
            Assert.AreEqual(LanMatchState.OpponentLeft, host.State, "Leaving after a final state keeps it.");
            Assert.AreEqual("Mike went home.", host.LastError);
        }

        // ------------------------------------------------------------------ messages that come early

        [Test]
        public void TheOtherSidesReady_BeforeTheLocalOne_Waits_AndSoDoesTheNextRoundsDuringTheBattle()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(61), out LanMatchCoordinator host, out LanMatchCoordinator guest, out _, out _);
            Draft(host, guest);
            Assert.IsTrue(guest.Match.Buy(Team.B, Infantry(guest.Match, Team.B)).Ok, "The guest deploys live.");
            Assert.IsTrue(host.Match.Buy(Team.A, Infantry(host.Match, Team.A)).Ok);
            int hostSquadsOnGuest = guest.Match.Player.Squads.Count;
            Assert.IsTrue(host.Ready().Ok);
            Assert.AreEqual(LanMatchState.WaitingForOpponent, host.State);
            Assert.IsTrue(host.WaitingForOpponent);
            Assert.IsFalse(host.LocalCanAct);

            for (int i = 0; i < 3; i++) guest.Update();
            Assert.AreEqual(LanMatchState.Deployment, guest.State, "The host's list waits for the guest's own Ready.");
            Assert.IsTrue(guest.OpponentReady);
            Assert.IsTrue(guest.LocalCanAct);
            Assert.AreEqual(hostSquadsOnGuest, guest.Match.Player.Squads.Count, "The host's new squad is not on the guest's match yet.");
            Assert.AreEqual("The other player is ready.", guest.StatusText);

            Assert.IsTrue(guest.Ready().Ok);
            Assert.AreEqual(LanMatchState.CheckingSync, guest.State, "The guest applied the host's list and sent its hash.");
            Assert.AreEqual(host.Match.Player.Squads.Count, guest.Match.Player.Squads.Count);
            host.Update();
            Assert.AreEqual(LanMatchState.ReadyToFight, host.State, host.LastError);
            guest.Update();
            Assert.AreEqual(LanMatchState.ReadyToFight, guest.State, guest.LastError);
            Assert.AreEqual(host.LocalHash, guest.LocalHash);
            Assert.AreEqual(guest.LocalHash, guest.RemoteHash);

            // The host fights, finishes and readies round 2 while the guest's battle is still on.
            host.StartBattle().Run();
            guest.StartBattle();
            Assert.IsTrue(host.FinishBattle());
            if (host.State == LanMatchState.MatchOver) Assert.Inconclusive("The match ended in round 1.");
            host.ContinueAfterResult();
            if (host.Match.InCards(Team.A)) Assert.IsTrue(host.Match.SkipCards(Team.A).Ok);
            Assert.IsTrue(host.Ready().Ok);
            guest.Update();
            Assert.AreEqual(LanMatchState.Battle, guest.State);
            Assert.IsFalse(guest.OpponentReady, "Not counted in the battle.");

            guest.Match.CurrentBattle.Run();
            Assert.IsTrue(guest.FinishBattle());
            guest.ContinueAfterResult();
            Assert.AreEqual(2, guest.Match.Round);
            Assert.IsTrue(guest.OpponentReady, "The host's round 2 Ready waited in the buffer.");
        }

        // ------------------------------------------------------------------ desyncs

        [Test]
        public void AForgedCommand_GivesDesync_AndTheOtherGameIsTold()
        {
            // The host is a real coordinator; the "guest" is a raw channel that sends what it likes.
            LanMemoryChannel.CreatePair(out LanMemoryChannel hostEnd, out LanMemoryChannel rawGuest);
            LanMatchCoordinator host = LanMatchStart.BeginAsHost(hostEnd, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), LockstepTestKit.Settings(67));
            Match guestMatch = LanMatchFactory.Build(LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), LockstepTestKit.Settings(67));
            Assert.IsTrue(rawGuest.TryReceive(out NetMessage start));
            Assert.AreEqual(LanMessageTypes.MatchStart, start.Type);
            Assert.IsTrue(guestMatch.ChooseDraft(Team.B, 0).Ok);
            rawGuest.Send(new NetMessage(LanMessageTypes.DraftPick).Set(LanFields.Round, 0).Set(LanFields.Commands, MatchCommand.ListToText(guestMatch.TakeCommands(Team.B))));
            Assert.IsTrue(host.ChooseDraft(0).Ok);
            host.Update();
            Assert.AreEqual(LanMatchState.Deployment, host.State);
            Assert.IsTrue(host.Ready().Ok);

            // A list that tries to sell one of the host's squads.
            var forged = new MatchCommand { Team = Team.A, Round = 1, Seq = 2, Action = MatchCommand.SellAction, SquadId = 1 };
            rawGuest.Send(new NetMessage(LanMessageTypes.Ready).Set(LanFields.Round, 1).Set(LanFields.Commands, MatchCommand.ListToText(new[] { forged })));
            int squads = host.Match.Player.Squads.Count;
            host.Update();
            Assert.AreEqual(LanMatchState.Desync, host.State);
            StringAssert.Contains("did not work here", host.LastError);
            Assert.AreEqual(squads, host.Match.Player.Squads.Count, "The forged command did nothing.");

            NetMessage told = null;
            while (rawGuest.TryReceive(out NetMessage m)) told = m;
            Assert.IsNotNull(told);
            Assert.AreEqual(LanMessageTypes.Desync, told.Type, "The other game is told.");
            Assert.AreEqual(1, told.GetInt(LanFields.Round));
        }

        [Test]
        public void StatesThatDiffer_GiveDesync_OnBothSides_WithBothHashes()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(71), out LanMatchCoordinator host, out LanMatchCoordinator guest, out _, out LanMemoryChannel guestEnd);
            Draft(host, guest);
            guest.Match.Enemy.Credits += 1; // a change that no command made
            Assert.IsTrue(host.Ready().Ok);
            Assert.IsTrue(guest.Ready().Ok);
            Frames(host, guest);
            Assert.AreEqual(LanMatchState.Desync, host.State);
            Assert.AreEqual(LanMatchState.Desync, guest.State);
            Assert.AreNotEqual(host.LocalHash, host.RemoteHash);
            Assert.AreEqual(host.LocalHash, guest.RemoteHash);
            StringAssert.Contains(host.LocalHash, host.LastError);
            StringAssert.Contains(host.RemoteHash, host.LastError);
            Assert.IsFalse(host.CanStartBattle);
            Assert.IsNull(host.StartBattle());
            StringAssert.StartsWith("Out of sync: ", guest.StatusText);

            // A final state keeps the channel open (the Desync message must not be cut off); Leave closes it and keeps the state.
            Assert.IsTrue(guestEnd.IsOpen);
            string error = guest.LastError;
            guest.Leave();
            Assert.AreEqual(LanMatchState.Desync, guest.State);
            Assert.AreEqual(error, guest.LastError);
            StringAssert.StartsWith("Out of sync: ", guest.StatusText);
            Assert.IsFalse(guestEnd.IsOpen);
        }

        [Test]
        public void ALocalActionAfterTheListsAreApplied_GivesDesync_BeforeTheBattle()
        {
            // In ReadyToFight: a squad bought after both games agreed. The battle does not start.
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(79), out LanMatchCoordinator host, out LanMatchCoordinator guest, out _, out _);
            Draft(host, guest);
            BothReady(host, guest);
            Assert.IsFalse(host.LocalCanAct);
            Assert.IsTrue(host.Match.Buy(Team.A, Infantry(host.Match, Team.A)).Ok, "The Match itself does not stop it.");
            Assert.IsNull(host.StartBattle(), "No battle with an army the other game did not agree to.");
            Assert.AreEqual(LanMatchState.Desync, host.State);
            StringAssert.Contains("acted on the match after pressing Ready", host.LastError);
            Assert.AreEqual(MatchPhase.Deployment, host.Match.Phase);
            guest.Update();
            Assert.AreEqual(LanMatchState.Desync, guest.State, "The other game is told.");

            // In ReadyToFight: a change that no command made (nothing pending, but the state is not the agreed one).
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(83), out host, out guest, out _, out _);
            Draft(host, guest);
            BothReady(host, guest);
            guest.Match.Enemy.Credits += 5;
            Assert.IsNull(guest.StartBattle());
            Assert.AreEqual(LanMatchState.Desync, guest.State);
            host.Update();
            Assert.AreEqual(LanMatchState.Desync, host.State);

            // In CheckingSync: the guest has applied the host's list and sent its hash, then buys.
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(89), out host, out guest, out _, out _);
            Draft(host, guest);
            Assert.IsTrue(host.Ready().Ok);
            guest.Update();
            Assert.IsTrue(guest.Ready().Ok);
            Assert.AreEqual(LanMatchState.CheckingSync, guest.State);
            Assert.IsTrue(guest.Match.Buy(Team.B, Infantry(guest.Match, Team.B)).Ok);
            host.Update();
            Assert.AreEqual(LanMatchState.ReadyToFight, host.State, host.LastError);
            guest.Update();
            Assert.AreEqual(LanMatchState.Desync, guest.State, "Found when the hashes are compared.");
            StringAssert.Contains("acted on the match after pressing Ready", guest.LastError);
            host.Update();
            Assert.AreEqual(LanMatchState.Desync, host.State);
            Assert.IsNull(host.StartBattle());
        }

        // ------------------------------------------------------------------ broken messages

        /// <summary>A host coordinator in round 1's deployment, with a raw "guest" channel that sends what a test likes.</summary>
        private static LanMatchCoordinator HostWithRawGuest(int seed, out LanMemoryChannel rawGuest, out Match guestMatch)
        {
            LanMemoryChannel.CreatePair(out LanMemoryChannel hostEnd, out rawGuest);
            LanMatchCoordinator host = LanMatchStart.BeginAsHost(hostEnd, LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), LockstepTestKit.Settings(seed));
            guestMatch = LanMatchFactory.Build(LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), LockstepTestKit.Settings(seed));
            Assert.IsTrue(rawGuest.TryReceive(out NetMessage start));
            Assert.AreEqual(LanMessageTypes.MatchStart, start.Type);
            return host;
        }

        private static void AssertToldDesync(LanMemoryChannel raw)
        {
            NetMessage told = null;
            while (raw.TryReceive(out NetMessage m)) told = m;
            Assert.IsNotNull(told);
            Assert.AreEqual(LanMessageTypes.Desync, told.Type, "The other game is told.");
        }

        // Cut-off or odd text: the JSON reader under these throws IndexOutOfRange, ArgumentOutOfRange and others, not only FormatException.
        private static readonly string[] BrokenLists =
        {
            "{\"team\":\"B\"",
            "{",
            "[",
            "{\"team\":\"\\u12\"}",
            "{\"team\":\"B\",\"round\":1e999}",
            "not json",
        };

        [TestCaseSource(nameof(BrokenLists))]
        public void ABrokenCommandList_InAReady_GivesDesync_NotAnException(string text)
        {
            LanMatchCoordinator host = HostWithRawGuest(97, out LanMemoryChannel rawGuest, out Match guestMatch);
            Assert.IsTrue(guestMatch.ChooseDraft(Team.B, 0).Ok);
            rawGuest.Send(new NetMessage(LanMessageTypes.DraftPick).Set(LanFields.Round, 0).Set(LanFields.Commands, MatchCommand.ListToText(guestMatch.TakeCommands(Team.B))));
            Assert.IsTrue(host.ChooseDraft(0).Ok);
            host.Update();
            Assert.AreEqual(LanMatchState.Deployment, host.State);
            Assert.IsTrue(host.Ready().Ok);

            rawGuest.Send(new NetMessage(LanMessageTypes.Ready).Set(LanFields.Round, 1).Set(LanFields.Commands, text));
            Assert.DoesNotThrow(() => host.Update());
            Assert.AreEqual(LanMatchState.Desync, host.State, host.LastError);
            Assert.IsTrue(host.IsFinished);
            AssertToldDesync(rawGuest);
        }

        [TestCaseSource(nameof(BrokenLists))]
        public void ABrokenStartingCard_GivesDesync_NotAnException(string text)
        {
            LanMatchCoordinator host = HostWithRawGuest(101, out LanMemoryChannel rawGuest, out _);
            rawGuest.Send(new NetMessage(LanMessageTypes.DraftPick).Set(LanFields.Round, 0).Set(LanFields.Commands, text));
            host.Update();
            Assert.DoesNotThrow(() => host.ChooseDraft(0), "The other side's card is applied once the local one is in.");
            Assert.AreEqual(LanMatchState.Desync, host.State, host.LastError);
            AssertToldDesync(rawGuest);
        }

        [Test]
        public void ABrokenReady_ThatArrivesFirst_GivesDesync_WhenTheLocalReadyIsPressed()
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(103), out LanMatchCoordinator host, out LanMatchCoordinator guest, out LanMemoryChannel hostEnd, out _);
            Draft(host, guest);
            // The host's end sends a cut-off list (the host's own coordinator is not used from here on).
            hostEnd.Send(new NetMessage(LanMessageTypes.Ready).Set(LanFields.Round, 1).Set(LanFields.Commands, "{"));
            guest.Update();
            Assert.AreEqual(LanMatchState.Deployment, guest.State, "The list waits for the guest's own Ready.");
            ActionResult ready = default;
            Assert.DoesNotThrow(() => ready = guest.Ready());
            Assert.IsTrue(ready.Ok);
            Assert.AreEqual(LanMatchState.Desync, guest.State, guest.LastError);
            StringAssert.Contains("could not be read", guest.LastError);
            NetMessage told = null;
            while (hostEnd.TryReceive(out NetMessage m)) told = m;
            Assert.AreEqual(LanMessageTypes.Desync, told?.Type);
        }

        [TestCase("[")]
        [TestCase("{")]
        [TestCase("not an outcome")]
        public void ABrokenBattleResult_GivesDesync_NotAnException(string outcome)
        {
            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(107), out LanMatchCoordinator host, out LanMatchCoordinator guest, out LanMemoryChannel hostEnd, out _);
            Draft(host, guest);
            BothReady(host, guest);
            guest.StartBattle().Run();
            Assert.IsFalse(guest.FinishBattle());
            hostEnd.Send(new NetMessage(LanMessageTypes.BattleResult).Set(LanFields.Round, 1).Set(LanFields.Outcome, outcome)
                .Set(LanFields.Hash, 0UL).Set(LanFields.State, 0UL));
            Assert.DoesNotThrow(() => guest.Update());
            Assert.AreEqual(LanMatchState.Desync, guest.State, guest.LastError);
            host.Update();
            Assert.AreEqual(LanMatchState.Desync, host.State, "The host is told.");
        }

        [Test]
        public void Ready_WaitsForTheCard_AndTheMatchMustBeTwoHumans()
        {
            Assert.Throws<ArgumentException>(() => new LanMatchCoordinator(
                new Match(LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), 1, MatchSeats.HumanVsAi), Team.A, new NullChannel()));

            LockstepTestKit.StartInMemory(LockstepTestKit.Settings(73), out LanMatchCoordinator host, out LanMatchCoordinator guest, out _, out _);
            Assert.IsFalse(host.Ready().Ok, "Not in the draft.");
            Draft(host, guest);
            BothReady(host, guest);
            host.StartBattle().Run();
            guest.StartBattle().Run();
            Assert.IsTrue(host.FinishBattle());
            Assert.IsFalse(guest.FinishBattle(), "The host's result has not been read yet.");
            guest.Update();
            Assert.AreEqual(MatchPhase.Result == host.Match.Phase ? LanMatchState.Result : LanMatchState.MatchOver, guest.State);
            if (host.State == LanMatchState.MatchOver) Assert.Inconclusive("The match ended in round 1.");
            host.ContinueAfterResult();
            Assert.IsTrue(host.Match.InCards(Team.A), "Round 2 offers a card.");
            Assert.IsFalse(host.Ready().Ok, "Take or skip the card first.");
            Assert.IsTrue(host.Match.SkipCards(Team.A).Ok);
            Assert.IsTrue(host.Ready().Ok);
        }

        private sealed class NullChannel : ILanChannel
        {
            public bool Send(JsonObject message) => true;
            public bool TryReceive(out NetMessage message)
            {
                message = null;
                return false;
            }
            public bool IsOpen => true;
            public string LostReason => "";
            public void Close() { }
        }
    }
}
