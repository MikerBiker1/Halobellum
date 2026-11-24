// LockstepTestKit.cs
// Helpers for the lockstep coordinator tests: the test content (two factions, cards of every general kind, towers and the
// Command Center's oil; new objects on every call, as a second machine would build them), the host's settings, a host and a
// guest started over any channel (LanMatchStart.BeginAsHost and LanGuestStart), two LanSessions connected over loopback TCP,
// and an AiSeat: one machine whose own side is played by the hardest RuleBasedAi through the coordinator, step by step, as the
// Match scene would drive it, recording the agreed state hash before every battle and the match's state hash after every result
// (BattleFinished), and counting the rounds in which messages came in the less usual orders. PlayOut steps two seats to the end,
// in the fixed host-then-guest order or in a seeded random order per frame.
// Depends on: Halobellum.Net (LanMatchCoordinator, LanMatchStart, LanGuestStart, LanSession, LanHost, LanContent),
//             Halobellum.Economy, Halobellum.Core, NUnit, TestMatch, TestRules, NetTestKit.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    internal static class LockstepTestKit
    {
        /// <summary>Smaller bases than the game's, so that a whole match with the Insane AI on both sides takes seconds.</summary>
        public const int WholeMatchBaseHealth = 400;

        /// <summary>Standard UNSC and Covenant (the test roster) with a tech on the Tank and cards of every general kind.</summary>
        public static List<FactionInfo> Factions(float tankDamage = 10f)
        {
            List<FactionInfo> factions = TestMatch.StandardFactions(out _, out _, out UnitStats tank, out _);
            tank.Weapons[0].DamagePerShot = tankDamage;
            tank.TechUpgrades = new[] { new TechUpgrade { Name = "Plating", Cost = 100, HealthMultiplier = 1.2f } };
            var strike = new SpellStats { Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Cost = 50, CooldownRounds = 1, MaxOwnedCopies = 2, Radius = 5f, DelaySeconds = 1f, DamagePerTick = 200f, DamageType = DamageType.Explosive };
            var cards = new List<CardStats>
            {
                CardStats.FromSpell(strike),
                new CardStats { Name = "Heavy Armor", Type = CardType.Item, Cost = 50, MaxOwnedCopies = 99, Item = new ItemStats { Name = "Heavy Armor", HealthMultiplier = 1.7f } },
                new CardStats { Name = "Supply Enhancement", Type = CardType.Economy, Cost = 100, BonusIncome = 50 },
                new CardStats { Name = "Intensive Training", Type = CardType.Training, Cost = 50, CooldownRounds = 1 },
            };
            foreach (FactionInfo faction in factions) faction.Cards.AddRange(cards);
            return factions;
        }

        /// <summary>The game's rules with cards, towers and the Command Center's oil: a fresh copy every call.</summary>
        public static MatchRules Rules()
        {
            MatchRules rules = TestMatch.RulesWithCards();
            rules.TowerBaseHealth = 100f;
            rules.OilZone = new SpellStats { Name = "Oil Zone", Offensive = true, Effect = SpellEffect.Oil, Cost = 150, AreaWidth = 15f, AreaDepth = 4f, DurationSeconds = 0f, SlowShare = 0.4f, FireDamageMultiplier = 1.5f, CooldownRounds = 2 };
            return rules;
        }

        public static LanMatchSettings Settings(int seed, MatchMode mode = MatchMode.Faction, int baseHealth = WholeMatchBaseHealth)
        {
            return new LanMatchSettings { Seed = seed, Mode = mode, BaseHealth = baseHealth, Cards = CardFilter.AllCards };
        }

        /// <summary>The host's coordinator (it sends MatchStart); then the guest's, which reads it. Fails the test if the guest refuses.</summary>
        public static void Start(ILanChannel hostChannel, ILanChannel guestChannel, LanMatchSettings settings, out LanMatchCoordinator host, out LanMatchCoordinator guest,
            int timeoutMs = NetTestKit.WaitMs)
        {
            host = LanMatchStart.BeginAsHost(hostChannel, Rules(), TestRules.Default(), Factions(), settings);
            var start = new LanGuestStart(guestChannel, Rules(), TestRules.Default(), Factions());
            Assert.IsTrue(NetTestKit.Until(() =>
            {
                start.Poll();
                return start.State != LanGuestStartState.Waiting;
            }, timeoutMs), "The guest got no MatchStart.");
            Assert.AreEqual(LanGuestStartState.Started, start.State, start.Reason);
            guest = start.Coordinator;
            Assert.AreEqual(Team.A, host.LocalTeam);
            Assert.AreEqual(Team.B, guest.LocalTeam);
        }

        /// <summary>A coordinator pair over the in-memory channel.</summary>
        public static void StartInMemory(LanMatchSettings settings, out LanMatchCoordinator host, out LanMatchCoordinator guest,
            out LanMemoryChannel hostChannel, out LanMemoryChannel guestChannel)
        {
            LanMemoryChannel.CreatePair(out hostChannel, out guestChannel);
            Start(hostChannel, guestChannel, settings, out host, out guest);
        }

        /// <summary>Two sessions connected over loopback TCP (host on 127.0.0.1, a port the system picks), handshake done.</summary>
        public static void ConnectSessions(out LanSession host, out LanSession guest, string hostContent = null, string guestContent = null)
        {
            var lanHost = new LanHost(0, IPAddress.Loopback, NetTestKit.Fast());
            string content = LanContent.Describe(Factions(), Rules(), TestRules.Default());
            host = LanSession.Host(lanHost, NetTestKit.Hello("Hosty", content: hostContent ?? content));
            guest = LanSession.Join("127.0.0.1", lanHost.Port, NetTestKit.Hello("Guesty", content: guestContent ?? content), NetTestKit.Fast());
            LanSession h = host, g = guest;
            NetTestKit.Until(() =>
            {
                h.Poll();
                g.Poll();
                return (h.State == LanSessionState.Connected && g.State == LanSessionState.Connected) || g.IsFinished;
            });
        }

        /// <summary>
        /// Both coordinators step until both matches are over or one stops; fails the test after the time limit. Without
        /// <paramref name="order"/>, every frame steps the host, then the guest. With it, each frame picks at random (from that
        /// seeded Random) one of: host then guest, guest then host, only the host, only the guest. A side that does not step in a
        /// frame reads its messages later, as over a slow line, so the messages arrive in many different orders.
        /// </summary>
        public static void PlayOut(AiSeat host, AiSeat guest, int timeoutMs = 120000, bool pause = false, Random order = null)
        {
            var watch = Stopwatch.StartNew();
            while (!(host.Done && guest.Done))
            {
                switch (order == null ? 0 : order.Next(4))
                {
                    case 0:
                        host.Step();
                        guest.Step();
                        break;
                    case 1:
                        guest.Step();
                        host.Step();
                        break;
                    case 2:
                        host.Step();
                        break;
                    default:
                        guest.Step();
                        break;
                }
                if (host.Coordinator.IsFinished || guest.Coordinator.IsFinished) break;
                Assert.Less(watch.ElapsedMilliseconds, timeoutMs, $"The match did not end in time (host {host.Coordinator.State}, guest {guest.Coordinator.State}).");
                if (pause) Thread.Sleep(1);
            }
        }

        public static string HashOf(Match match) => MatchStateHash.Hex(MatchStateHash.Compute(match));

        /// <summary>One side's card and deployment on its own machine, as a person would take them (the AI decides).</summary>
        public static void PlaySide(Match match, Team team, RuleBasedAi ai)
        {
            var view = new AiView(match, team);
            if (match.InCards(team))
            {
                int choice = ai.ChooseCard(view, match.Get(team).Offer);
                if (choice < 0 || !match.ChooseCard(team, choice).Ok) match.SkipCards(team);
            }
            ai.Deploy(view);
        }
    }

    /// <summary>One machine: its coordinator, and the AI that plays its own side, one step per call (as the Match scene would, per frame).</summary>
    internal sealed class AiSeat
    {
        public readonly LanMatchCoordinator Coordinator;
        public readonly RuleBasedAi Ai;
        /// <summary>The agreed state hash before each round's battle.</summary>
        public readonly Dictionary<int, string> BattleHashes = new Dictionary<int, string>();
        /// <summary>The match's state hash once each round's result is in.</summary>
        public readonly Dictionary<int, string> ResultHashes = new Dictionary<int, string>();
        /// <summary>The most squads the other side had on this machine's match before a battle (its decisions arrived).</summary>
        public int MostRemoteSquads;
        /// <summary>The rounds in which this machine's battle was over before the host's result was here (guest only).</summary>
        public int WaitedForHostResult;
        /// <summary>The rounds in which the other side's Ready was here before this machine pressed its own.</summary>
        public int RemoteReadyFirst;

        public AiSeat(LanMatchCoordinator coordinator, int aiSeed, AiDifficulty difficulty = AiDifficulty.Insane)
        {
            Coordinator = coordinator;
            Ai = new RuleBasedAi(difficulty, aiSeed);
            coordinator.BattleFinished += summary =>
            {
                Assert.AreEqual(coordinator.Match.Round, summary.Round);
                ResultHashes[summary.Round] = LockstepTestKit.HashOf(coordinator.Match);
            };
        }

        public Match Match => Coordinator.Match;

        public bool Done => Coordinator.IsFinished || Coordinator.State == LanMatchState.MatchOver;

        public void Step()
        {
            Coordinator.Update();
            Match match = Coordinator.Match;
            Team team = Coordinator.LocalTeam;
            switch (Coordinator.State)
            {
                case LanMatchState.Draft:
                    if (!Coordinator.LocalPickedDraft)
                    {
                        int pick = Ai.ChooseDraftCard(new AiView(match, team), team == Team.A ? match.PlayerDraft : match.EnemyDraft);
                        Assert.IsTrue(Coordinator.ChooseDraft(pick).Ok);
                    }
                    break;
                case LanMatchState.Deployment:
                    if (Coordinator.OpponentReady) RemoteReadyFirst++;
                    LockstepTestKit.PlaySide(match, team, Ai);
                    Assert.IsTrue(Coordinator.Ready().Ok, "Ready works once the card is decided.");
                    break;
                case LanMatchState.ReadyToFight:
                    Assert.IsTrue(Coordinator.CanStartBattle);
                    Assert.AreEqual(Coordinator.LocalHash, Coordinator.RemoteHash);
                    BattleHashes[match.Round] = Coordinator.LocalHash;
                    MostRemoteSquads = Math.Max(MostRemoteSquads, match.Get(Coordinator.RemoteTeam).Squads.Count);
                    Coordinator.StartBattle().Run();
                    if (!Coordinator.FinishBattle()) WaitedForHostResult++; // finished in a later Update, once the host's result is here
                    break;
                case LanMatchState.Result:
                    Coordinator.ContinueAfterResult();
                    break;
            }
        }
    }
}
