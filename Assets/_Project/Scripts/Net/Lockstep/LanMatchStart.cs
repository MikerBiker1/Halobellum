// LanMatchStart.cs
// How both games build the same two-humans Match. The host picks a seed and its skirmish settings (mode, base health, card filter:
// LanMatchSettings) and sends them in the MatchStart message, with a fingerprint of the content after those settings and the
// state hash of the fresh match. Both sides call the same LanMatchFactory.Build with their own fresh rules, combat rules and
// factions, so both get the same match; the guest checks the fingerprint and the hash before it plays (LanGuestStart).
// LanContent.Describe gives the content text for the handshake's Hello (one section per faction plus the rules), so a refusal
// can name what differs.
// Plain C# 9 without the engine.
// Depends on: Halobellum.Economy (Match, MatchRules, MatchSetup, SkirmishFaction, ContentFingerprint, MatchStateHash),
//             Halobellum.Core (CombatRules), NetMessage, LanMessages, LanTimings (LanDefaults), LanMatchCoordinator, ILanChannel.

using System;
using System.Collections.Generic;
using System.Text;
using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Net
{
    /// <summary>What the host decides for a LAN match: the seed and the parts of MatchSetup that change the match.</summary>
    public sealed class LanMatchSettings
    {
        public int Seed;
        public MatchMode Mode;
        /// <summary>Each base's health before the starting card moves it; 0 = the rules' own (as in MatchSetup).</summary>
        public int BaseHealth;
        public CardFilter Cards;

        public LanMatchSettings Copy() => (LanMatchSettings)MemberwiseClone();

        /// <summary>The settings for a new game on the same connection (New game): the same mode, base health and cards, this seed.</summary>
        public LanMatchSettings WithSeed(int seed)
        {
            LanMatchSettings next = Copy();
            next.Seed = seed;
            return next;
        }

        /// <summary>
        /// The seed of the host's next game after <paramref name="lastSeed"/>: with a demo seed (-lanDemoSeed) it is that seed plus
        /// <paramref name="rematch"/> (1 for the first new game, 2 for the next...), else <paramref name="fresh"/>. Never the last seed.
        /// </summary>
        public static int NextSeed(int lastSeed, int? demoSeed, int rematch, int fresh)
        {
            int seed = demoSeed.HasValue ? unchecked(demoSeed.Value + rematch) : fresh;
            return seed == lastSeed ? unchecked(seed + 1) : seed;
        }

        /// <summary>The settings from a seed and a skirmish setup (null = Faction mode with the rules as they are).</summary>
        public static LanMatchSettings From(int seed, MatchSetup setup)
        {
            return new LanMatchSettings
            {
                Seed = seed,
                Mode = setup?.Mode ?? MatchMode.Faction,
                BaseHealth = setup?.BaseHealth ?? 0,
                Cards = setup?.Cards ?? CardFilter.AllCards,
            };
        }

        /// <summary>The MatchSetup these settings stand for (what MatchController keeps for its match).</summary>
        public MatchSetup ToSetup() => new MatchSetup { Mode = Mode, BaseHealth = BaseHealth, Cards = Cards };

        public override string ToString() => $"seed {Seed}, {Mode}, base health {(BaseHealth == 0 ? "default" : BaseHealth.ToString())}, {Cards}";
    }

    /// <summary>Builds the two-humans Match the same way on both machines.</summary>
    public static class LanMatchFactory
    {
        /// <summary>
        /// The two-humans match for these settings: the setup is written into <paramref name="freshRules"/> (so pass a new copy each
        /// time, as MatchController does with its Match Rules asset), Unlock Skirmish plays the one faction built from all of them, and
        /// the match is new Match(rules, combatRules, offered factions, seed, MatchSeats.TwoHumans). <paramref name="content"/> is the
        /// fingerprint of the offered factions with the rules after the setup: the same on both machines when their content agrees.
        /// <paramref name="factions"/> are the real factions, as MatchController builds them from its faction assets.
        /// </summary>
        public static Match Build(MatchRules freshRules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions, LanMatchSettings settings, out ulong content)
        {
            if (freshRules == null) throw new ArgumentNullException(nameof(freshRules));
            if (combatRules == null) throw new ArgumentNullException(nameof(combatRules));
            if (factions == null) throw new ArgumentNullException(nameof(factions));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ToSetup().ApplyTo(freshRules);
            IReadOnlyList<FactionInfo> offered = factions;
            if (freshRules.Mode == MatchMode.UnlockSkirmish) offered = new[] { SkirmishFaction.Build(factions) };
            content = ContentFingerprint.Compute(offered, freshRules, combatRules);
            return new Match(freshRules, combatRules, offered, settings.Seed, MatchSeats.TwoHumans);
        }

        public static Match Build(MatchRules freshRules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions, LanMatchSettings settings)
        {
            return Build(freshRules, combatRules, factions, settings, out _);
        }
    }

    /// <summary>The host's MatchStart message: the settings, and what the guest's match must come out as.</summary>
    public sealed class LanMatchStart
    {
        public int Proto = LanDefaults.Protocol;
        public LanMatchSettings Settings = new LanMatchSettings();
        /// <summary>LanMatchFactory.Build's content fingerprint on the host.</summary>
        public ulong Content;
        /// <summary>MatchStateHash of the host's fresh match.</summary>
        public ulong State;

        public NetMessage ToMessage()
        {
            var m = new NetMessage(LanMessageTypes.MatchStart);
            m.Set("proto", Proto).Set("seed", Settings.Seed).Set("mode", Settings.Mode.ToString()).Set("baseHealth", Settings.BaseHealth)
                .Set("cards", Settings.Cards.ToString()).Set("content", Content).Set(LanFields.State, State);
            return m;
        }

        /// <summary>Reads a MatchStart message. FormatException when it is not one, or names a mode or card filter this game does not know.</summary>
        public static LanMatchStart FromMessage(JsonObject m)
        {
            if (m == null || m.GetString(NetMessage.TypeKey) != LanMessageTypes.MatchStart) throw new FormatException("Not a MatchStart message.");
            foreach (string key in new[] { "proto", "seed", "mode", "baseHealth", "cards", "content", LanFields.State })
                if (!m.Has(key)) throw new FormatException("The MatchStart message has no \"" + key + "\".");
            return new LanMatchStart
            {
                Proto = m.GetInt("proto"),
                Settings = new LanMatchSettings
                {
                    Seed = m.GetInt("seed"),
                    Mode = ParseEnum<MatchMode>(m.GetString("mode", ""), "mode"),
                    BaseHealth = m.GetInt("baseHealth"),
                    Cards = ParseEnum<CardFilter>(m.GetString("cards", ""), "card filter"),
                },
                Content = m.GetULong("content"),
                State = m.GetULong(LanFields.State),
            };
        }

        /// <summary>
        /// Null when a match built here matches the host's (same protocol, content and fresh state), else the difference in words
        /// that both players can read (it names the host and the guest, not "here" and "there").
        /// </summary>
        public static string Check(LanMatchStart host, ulong guestContent, ulong guestState)
        {
            if (host == null) return "The host did not say how to start the match.";
            if (host.Proto != LanDefaults.Protocol)
                return "The host uses LAN protocol " + host.Proto + "; the guest uses " + LanDefaults.Protocol + ". Both need the same version of the game.";
            if (host.Content != guestContent)
                return "The two games build different matches from the host's settings (content " + MatchStateHash.Hex(host.Content) + " on the host, "
                    + MatchStateHash.Hex(guestContent) + " on the guest). Both games need the same unit stats files.";
            if (host.State != guestState)
                return "The two games start the match differently (state " + MatchStateHash.Hex(host.State) + " on the host, "
                    + MatchStateHash.Hex(guestState) + " on the guest). Both need the same build.";
            return null;
        }

        /// <summary>
        /// The host's side of the start: builds the match (LanMatchFactory.Build), sends MatchStart and returns the coordinator for
        /// Team A. The guest answers with nothing if it agrees, or with Leave {refused: true, reason}, which the coordinator shows as
        /// OpponentLeft with that reason.
        /// </summary>
        public static LanMatchCoordinator BeginAsHost(ILanChannel channel, MatchRules freshRules, CombatRules combatRules, IReadOnlyList<FactionInfo> factions,
            LanMatchSettings settings, Action<string> log = null)
        {
            if (channel == null) throw new ArgumentNullException(nameof(channel));
            Match match = LanMatchFactory.Build(freshRules, combatRules, factions, settings, out ulong content);
            var start = new LanMatchStart { Settings = settings.Copy(), Content = content, State = MatchStateHash.Compute(match) };
            channel.Send(start.ToMessage());
            NetLog.Write(log, "Match started as host: " + settings + ".");
            return new LanMatchCoordinator(match, Team.A, channel, log);
        }

        private static T ParseEnum<T>(string text, string what) where T : struct
        {
            if (text.Length > 0 && !char.IsDigit(text[0]) && text[0] != '-' && Enum.TryParse(text, false, out T value) && Enum.IsDefined(typeof(T), value)) return value;
            throw new FormatException("Unknown " + what + " \"" + text + "\" in the MatchStart message.");
        }
    }

    /// <summary>The content text for the handshake (LanHello.Content).</summary>
    public static class LanContent
    {
        /// <summary>
        /// "UNSC=1a2b...;Covenant=3c4d...;rules=5e6f...": one fingerprint per faction (by its name) and one for the match and combat
        /// rules, so that LanHello.Mismatch can say which differs. Use the rules as they are before any skirmish setup.
        /// </summary>
        public static string Describe(IReadOnlyList<FactionInfo> factions, MatchRules rules, CombatRules combatRules)
        {
            var text = new StringBuilder();
            var used = new HashSet<string>();
            if (factions != null)
            {
                for (int i = 0; i < factions.Count; i++)
                {
                    string name = SectionName(factions[i]?.Name, i);
                    while (!used.Add(name)) name += "+";
                    text.Append(name).Append('=').Append(MatchStateHash.Hex(ContentFingerprint.Of(factions[i]))).Append(';');
                }
            }
            var hasher = new StableHasher().ULong(ContentFingerprint.Of(rules)).ULong(ContentFingerprint.Of(combatRules));
            text.Append("rules=").Append(MatchStateHash.Hex(hasher.Value));
            return text.ToString();
        }

        private static string SectionName(string name, int index)
        {
            if (string.IsNullOrWhiteSpace(name)) return "faction" + (index + 1);
            var clean = new StringBuilder();
            foreach (char c in name.Trim())
                if (c != ';' && c != '=') clean.Append(c);
            return clean.Length > 0 ? clean.ToString() : "faction" + (index + 1);
        }
    }
}
