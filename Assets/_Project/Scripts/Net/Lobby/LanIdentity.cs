// LanIdentity.cs
// What this game says about itself in the handshake (LanHello), apart from the version, which the engine knows:
//   Code      a fingerprint of the game's code: the build ids of the assemblies the match runs on (Core, Combat, Economy, Net). Two
//             copies of the same build agree; two different builds do not.
//   Content   the unit stats files in force, one section per changed unit, so a refusal can name the units that differ:
//             "modded=no" with the game's own values, else "modded=yes;Marine=1a2b...;Scorpion=3c4d...". The whole content (every
//             unit, card and rule after the setup) is checked again when the match starts (LanGuestStart).
// Also the player's name as it goes on the network: trimmed, at most NameLength letters, never empty; and CleanText, which makes a
// text from the other game (a name, a version, a reason) safe to show on one line.
// Plain C# 9 without the engine.
// Depends on: LanHandshake (LanHello), Halobellum.Economy (Match, StableHasher, MatchStateHash), Halobellum.Combat, Halobellum.Core.

using System;
using System.Collections.Generic;
using System.Text;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Net
{
    public static class LanIdentity
    {
        /// <summary>The longest name a player can use on the network.</summary>
        public const int NameLength = 24;

        /// <summary>The name when the player left the field empty.</summary>
        public const string DefaultName = "Player";

        /// <summary>The build ids of the assemblies the match runs on, as one hex fingerprint.</summary>
        public static string CodeFingerprint()
        {
            var hasher = new StableHasher();
            foreach (Type type in new[] { typeof(Team), typeof(BattleSimulation), typeof(Match), typeof(LanSession) })
                hasher.Text(type.Assembly.ManifestModule.ModuleVersionId.ToString("N"));
            return MatchStateHash.Hex(hasher.Value);
        }

        /// <summary>
        /// The content text for the Hello from the unit stats files' changes (UnitStatOverrides.Changes, one line per changed value, each
        /// starting with the unit's name: "Marine: cost 100 -> 150", "Marine, weapon 1 (...): damagePerShot 4 -> 5"). Null or none: "modded=no".
        /// </summary>
        public static string Content(IReadOnlyList<string> changes)
        {
            if (changes == null || changes.Count == 0) return "modded=no";
            var byUnit = new SortedDictionary<string, StableHasher>(StringComparer.Ordinal);
            foreach (string change in changes)
            {
                string line = change ?? "";
                string unit = UnitOf(line);
                if (!byUnit.TryGetValue(unit, out StableHasher hasher)) byUnit[unit] = hasher = new StableHasher();
                hasher.Text(line);
            }
            var text = new StringBuilder("modded=yes");
            foreach (KeyValuePair<string, StableHasher> pair in byUnit) text.Append(';').Append(pair.Key).Append('=').Append(MatchStateHash.Hex(pair.Value.Value));
            return text.ToString();
        }

        /// <summary>The unit a change line is about: the text before its first ':' or ',' (without ';' and '=', which the sections use).</summary>
        public static string UnitOf(string change)
        {
            string text = change ?? "";
            int end = text.IndexOfAny(new[] { ':', ',' });
            if (end >= 0) text = text.Substring(0, end);
            var clean = new StringBuilder();
            foreach (char c in text.Trim())
                if (c != ';' && c != '=') clean.Append(c);
            return clean.Length > 0 ? clean.ToString() : "unit";
        }

        /// <summary>The name as it goes on the network: trimmed, no line breaks, at most NameLength letters, DefaultName if empty.</summary>
        public static string CleanName(string name)
        {
            string result = CleanText(name, NameLength);
            return result.Length > 0 ? result : DefaultName;
        }

        /// <summary>A text from the other game, made safe to show on one line: no line breaks or other control letters, at most maxLength letters, "" if empty.</summary>
        public static string CleanText(string text, int maxLength)
        {
            var clean = new StringBuilder();
            foreach (char c in (text ?? "").Trim())
            {
                if (clean.Length >= maxLength) break;
                if (char.IsControl(c))
                {
                    if (clean.Length > 0 && clean[clean.Length - 1] != ' ') clean.Append(' '); // a line break becomes a space
                    continue;
                }
                clean.Append(c);
            }
            return clean.ToString().Trim();
        }

        /// <summary>This game's description for the handshake.</summary>
        public static LanHello Hello(string name, string version, IReadOnlyList<string> statChanges)
        {
            return new LanHello { Name = CleanName(name), Version = version ?? "", Code = CodeFingerprint(), Content = Content(statChanges) };
        }
    }
}
