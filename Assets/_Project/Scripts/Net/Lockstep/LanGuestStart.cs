// LanGuestStart.cs
// The guest's side of the match start: it waits for the host's MatchStart, builds the same two-humans Match from the host's
// settings with its own rules and factions (LanMatchFactory.Build), and checks that the content fingerprint and the fresh state
// hash agree with the host's. If they do, it hands over a LanMatchCoordinator for Team B. If not (or the match cannot be built here at
// all: an exception while building it), it tells the host why with
// Leave {refused: true, reason}, closes the channel and stays Refused with the same reason.
// It reads one message at a time and stops at the MatchStart, so anything the host sends after it (its starting card, say) is
// left in the channel for the coordinator.
// Call Poll every frame on the main thread until State is no longer Waiting. One Poll reads at most LanLimits.MaxMessagesPerUpdate
// messages (the rest wait a frame), and text from the host is cut short (LanLimits.Clip) before it goes into a reason or the log.
// Plain C# 9 without the engine.
// Depends on: LanMatchStart (LanMatchStart, LanMatchSettings, LanMatchFactory), LanMatchCoordinator, ILanChannel, LanMessages, LanLimits,
//             Halobellum.Economy (Match, MatchRules, MatchStateHash), Halobellum.Core (CombatRules).

using System;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Net
{
    public enum LanGuestStartState
    {
        /// <summary>No MatchStart yet.</summary>
        Waiting,
        /// <summary>The match is built and agrees with the host's: Coordinator is ready.</summary>
        Started,
        /// <summary>The host's match could not be built the same here (Reason says why); the host was told. Final.</summary>
        Refused,
        /// <summary>The connection ended, or the host left, before the match started (Reason says why). Final.</summary>
        Lost,
    }

    public sealed class LanGuestStart
    {
        private readonly ILanChannel channel;
        private readonly MatchRules freshRules;
        private readonly CombatRules combatRules;
        private readonly IReadOnlyList<FactionInfo> factions;
        private readonly Action<string> log;

        /// <summary>
        /// The guest's start over a connected channel. <paramref name="freshRules"/> is used once (the host's setup is written into
        /// it), so pass a new copy; <paramref name="factions"/> are the real factions, as on the host.
        /// </summary>
        public LanGuestStart(ILanChannel channel, MatchRules freshRules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions, Action<string> log = null)
        {
            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
            this.freshRules = freshRules ?? throw new ArgumentNullException(nameof(freshRules));
            this.combatRules = combatRules ?? throw new ArgumentNullException(nameof(combatRules));
            this.factions = factions ?? throw new ArgumentNullException(nameof(factions));
            this.log = log;
        }

        public LanGuestStartState State { get; private set; } = LanGuestStartState.Waiting;

        /// <summary>Why the start was Refused or Lost, in words for the player; "" otherwise.</summary>
        public string Reason { get; private set; } = "";

        /// <summary>The host's settings, once its MatchStart arrived.</summary>
        public LanMatchSettings Settings { get; private set; }

        /// <summary>The guest's match (Team B is this side), once Started.</summary>
        public Match Match { get; private set; }

        /// <summary>The coordinator for this match, once Started.</summary>
        public LanMatchCoordinator Coordinator { get; private set; }

        /// <summary>Reads messages up to the host's MatchStart and acts on it; notices a closed channel.</summary>
        public void Poll()
        {
            if (State != LanGuestStartState.Waiting) return;
            bool open = channel.IsOpen; // read before the inbox, so a message that came just before a close is still read
            int handled = 0;
            while (handled < LanLimits.MaxMessagesPerUpdate && channel.TryReceive(out NetMessage m))
            {
                handled++;
                if (m.Type == LanMessageTypes.MatchStart)
                {
                    Begin(m);
                    return;
                }
                if (m.Type == LanMessageTypes.Leave)
                {
                    End(LanGuestStartState.Lost, "The host left before the match started. " + LanLimits.Clip(m.GetString(LanFields.Reason, "")));
                    channel.Close();
                    return;
                }
                NetLog.Write(log, "Before the match start, ignored a " + LanLimits.Clip(m.Type, LanLimits.MaxNameChars) + " message.");
            }
            // With the frame's share used up, more may wait: the close is noticed in a later Poll, after them.
            if (!open && handled < LanLimits.MaxMessagesPerUpdate) End(LanGuestStartState.Lost, Describe(channel.LostReason));
        }

        private void Begin(NetMessage m)
        {
            LanMatchStart start;
            try
            {
                start = LanMatchStart.FromMessage(m);
            }
            catch (FormatException e)
            {
                Refuse("The host's match start could not be read: " + e.Message);
                return;
            }
            Settings = start.Settings;
            if (start.Proto != LanDefaults.Protocol)
            {
                Refuse(LanMatchStart.Check(start, 0, 0));
                return;
            }
            Match match;
            string reason;
            try
            {
                match = LanMatchFactory.Build(freshRules, combatRules, factions, start.Settings, out ulong content);
                reason = LanMatchStart.Check(start, content, MatchStateHash.Compute(match));
            }
            catch (Exception e)
            {
                // The MatchStart is read already: without this the guest would wait for it for ever, and the host at the draft.
                NetLog.Write(log, "Building the host's match failed: " + e);
                Refuse("The host's match could not be built here: " + e.Message);
                return;
            }
            if (reason != null)
            {
                Refuse(reason);
                return;
            }
            Match = match;
            Coordinator = new LanMatchCoordinator(match, Team.B, channel, log);
            State = LanGuestStartState.Started;
            NetLog.Write(log, "Match started as guest: " + start.Settings + ".");
        }

        private void Refuse(string reason)
        {
            reason = LanLimits.Clip(reason); // it may hold the host's own text (an exception's message): the Leave must stay small
            channel.Send(new NetMessage(LanMessageTypes.Leave).Set(LanFields.Reason, reason).Set(LanFields.Refused, true));
            channel.Close();
            End(LanGuestStartState.Refused, reason);
        }

        private void End(LanGuestStartState state, string reason)
        {
            State = state;
            Reason = reason ?? "";
            NetLog.Write(log, "Match start " + state + ": " + Reason);
        }

        private static string Describe(string lost) => "The connection to the host ended before the match started." + (string.IsNullOrEmpty(lost) ? "" : " " + lost);
    }
}
