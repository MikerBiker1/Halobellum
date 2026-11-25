// LanDemoArgs.cs
// The command line of a LAN test run (M11 Stage 4):
//   -lanDemo host|join   host: hosts at once with the default settings and starts as soon as a guest joins; join: joins 127.0.0.1
//                        (or -lanDemoAddress A). Both sides are played by the Insane AI, battles run at the fastest speed, and the
//                        game quits after the match.
//   -lanDemoSeed N       the match's seed (the host's choice; the guest takes the host's). The k-th new game uses N + k.
//   -lanDemoMatches N    play N matches back to back on the same connection (New game, pressed by both sides), then quit
//                        (default 1). Give both copies the same N.
//   -windowSlot 0|1      a window half the screen wide: slot 0 on the left, slot 1 on the right (two copies side by side).
// Plain C# 9 without the engine.
// Depends on: nothing.

using System;
using System.Globalization;

namespace Halobellum.Net
{
    public enum LanDemoRole { None, Host, Join }

    public sealed class LanDemoArgs
    {
        public const string DemoFlag = "-lanDemo";
        public const string SeedFlag = "-lanDemoSeed";
        public const string MatchesFlag = "-lanDemoMatches";
        /// <summary>The most matches -lanDemoMatches takes.</summary>
        public const int MostMatches = 1000;
        public const string AddressFlag = "-lanDemoAddress";
        public const string WindowSlotFlag = "-windowSlot";
        /// <summary>A demo host listens on 127.0.0.1 only (no Windows Firewall prompt) unless this flag is given, for a demo across two PCs.</summary>
        public const string NetworkFlag = "-lanDemoNetwork";
        public const string DefaultAddress = "127.0.0.1";

        public LanDemoRole Role = LanDemoRole.None;
        /// <summary>The seed the host uses, or null for a new one.</summary>
        public int? Seed;
        /// <summary>How many matches to play back to back through New game before quitting (-lanDemoMatches, 1 to MostMatches).</summary>
        public int Matches = 1;
        /// <summary>Where the guest joins.</summary>
        public string Address = DefaultAddress;
        /// <summary>0 (left half) or 1 (right half), or -1 for the window as usual.</summary>
        public int WindowSlot = -1;

        /// <summary>-lanDemoNetwork: listen on every network card instead of 127.0.0.1 only.</summary>
        public bool ListenOnNetwork;

        public bool IsDemo => Role != LanDemoRole.None;

        /// <summary>Reads the flags; anything missing or unreadable stays at its default.</summary>
        public static LanDemoArgs Parse(string[] args)
        {
            var result = new LanDemoArgs();
            if (args == null) return result;
            string role = Value(args, DemoFlag);
            if (string.Equals(role, "host", StringComparison.OrdinalIgnoreCase)) result.Role = LanDemoRole.Host;
            else if (string.Equals(role, "join", StringComparison.OrdinalIgnoreCase)) result.Role = LanDemoRole.Join;
            if (int.TryParse(Value(args, SeedFlag), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)) result.Seed = seed;
            if (int.TryParse(Value(args, MatchesFlag), NumberStyles.Integer, CultureInfo.InvariantCulture, out int matches) && matches >= 1) result.Matches = Math.Min(matches, MostMatches);
            string address = Value(args, AddressFlag);
            if (!string.IsNullOrWhiteSpace(address) && !address.StartsWith("-")) result.Address = address.Trim();
            foreach (string arg in args) if (string.Equals(arg, NetworkFlag, StringComparison.OrdinalIgnoreCase)) result.ListenOnNetwork = true;
            if (int.TryParse(Value(args, WindowSlotFlag), NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot) && (slot == 0 || slot == 1)) result.WindowSlot = slot;
            return result;
        }

        /// <summary>
        /// The window of a slot on a screen of this size, as x, y, width and height in pixels from the screen's top left: half the width,
        /// slot 0 on the left and slot 1 on the right, a little lower than the top and shorter than the screen (room for the title bar and the taskbar).
        /// </summary>
        public static void SlotRect(int slot, int screenWidth, int screenHeight, out int x, out int y, out int width, out int height)
        {
            width = Math.Max(320, screenWidth / 2);
            height = Math.Max(240, screenHeight - 120);
            x = slot == 1 ? screenWidth - width : 0;
            y = 30;
        }

        private static string Value(string[] args, string flag)
        {
            int at = Array.IndexOf(args, flag);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}
