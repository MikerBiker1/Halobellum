// LanHandshake.cs
// What the two games tell each other before a match, and the check that they can play together.
// The guest sends Hello {proto, version, code, content, name}; the host answers Welcome {name, and its own proto, version,
// code and content so the guest can check too} or Reject {reason}.
//   proto    the LAN message version (LanDefaults.Protocol);
//   version  the game version ("0.416.2");
//   code     a fingerprint of the game's code (same build);
//   content  a fingerprint of every unit, card, spell, item and rule after the player's unit stats files are applied. It is
//            either one hash, or sections "units=1a2b;cards=3c4d;..." so a mismatch can name what differs.
// LanHello.Mismatch compares two of them and gives null, or a reason in words for the player.
// Plain C# 9 without the engine.
// Depends on: NetMessage, LanTimings (LanDefaults).

using System;
using System.Collections.Generic;

namespace Halobellum.Net
{
    /// <summary>One game's description for the handshake.</summary>
    public sealed class LanHello
    {
        public int Proto = LanDefaults.Protocol;
        public string Version = "";
        public string Code = "";
        public string Content = "";
        /// <summary>The player's name, shown to the other side; not compared.</summary>
        public string Name = "";

        public LanHello Copy() => (LanHello)MemberwiseClone();

        /// <summary>This description as a message of the given type (Hello, or Welcome for the host's answer).</summary>
        public NetMessage ToMessage(string type = NetMessageTypes.Hello)
        {
            var m = new NetMessage(type);
            m.Set("proto", Proto).Set("version", Version ?? "").Set("code", Code ?? "").Set("content", Content ?? "").Set("name", Name ?? "");
            return m;
        }

        /// <summary>Reads a description from a Hello or Welcome message (missing fields are empty, proto 0).</summary>
        public static LanHello FromMessage(JsonObject m)
        {
            return new LanHello
            {
                Proto = m.GetInt("proto"),
                Version = m.GetString("version", ""),
                Code = m.GetString("code", ""),
                Content = m.GetString("content", ""),
                Name = m.GetString("name", ""),
            };
        }

        /// <summary>
        /// Null when the two games can play together, else the first difference in words, from this game's point of view
        /// ("The other game is version 0.416.1; this one is 0.416.2."). Checked in this order: protocol, version, code, content.
        /// </summary>
        public static string Mismatch(LanHello mine, LanHello theirs)
        {
            if (mine == null || theirs == null) return "The other game did not say what it is.";
            if (mine.Proto != theirs.Proto)
                return "The other game uses LAN protocol " + theirs.Proto + "; this one uses " + mine.Proto + ". Both need the same version of the game.";
            if (!string.Equals(mine.Version ?? "", theirs.Version ?? "", StringComparison.Ordinal))
                return "The other game is version " + Show(theirs.Version) + "; this one is " + Show(mine.Version) + ".";
            if (!string.Equals(mine.Code ?? "", theirs.Code ?? "", StringComparison.Ordinal))
                return "The two games are different builds of version " + Show(mine.Version) + " (code " + Short(theirs.Code) + " there, "
                    + Short(mine.Code) + " here). Both need the same build.";
            if (!string.Equals(mine.Content ?? "", theirs.Content ?? "", StringComparison.Ordinal))
                return "Unit stats differ: " + ContentDifference(mine.Content ?? "", theirs.Content ?? "")
                    + ". Both games need the same unit stats files.";
            return null;
        }

        /// <summary>
        /// What differs between two content fingerprints: the names of the sections that differ ("units, spells") when both are
        /// written as sections, else the two fingerprints ("fingerprint 1a2b3c4d there, 5e6f7a8b here").
        /// </summary>
        public static string ContentDifference(string mine, string theirs)
        {
            Dictionary<string, string> a = Sections(mine), b = Sections(theirs);
            if (a != null && b != null)
            {
                var names = new List<string>();
                foreach (KeyValuePair<string, string> pair in a)
                    if (!b.TryGetValue(pair.Key, out string other) || other != pair.Value) names.Add(pair.Key);
                foreach (string key in b.Keys)
                    if (!a.ContainsKey(key)) names.Add(key);
                if (names.Count > 0) return string.Join(", ", names);
            }
            return "fingerprint " + Short(theirs) + " there, " + Short(mine) + " here";
        }

        /// <summary>"units=1a;cards=2b" as a dictionary, in order; null when the text is not written as sections.</summary>
        private static Dictionary<string, string> Sections(string content)
        {
            if (content.IndexOf('=') < 0) return null;
            var result = new Dictionary<string, string>();
            foreach (string part in content.Split(';'))
            {
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) return null;
                result[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
            }
            return result;
        }

        private static string Show(string s) => string.IsNullOrEmpty(s) ? "(unknown)" : s;

        private static string Short(string s) => string.IsNullOrEmpty(s) ? "(none)" : s.Length <= 8 ? s : s.Substring(0, 8);
    }
}
