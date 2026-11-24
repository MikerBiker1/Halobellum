// LanMatchLog.cs
// The lines every LAN match writes to the game's log, one per round and one at the end, so the logs of the two games can be
// compared line by line (M11 Stage 4, Stage 5's two-copy check):
//   [LAN] round <n> hash <state hash before the battle> result <hash of the round's result> baseA <x> baseB <y>
//   [LAN] match over winner <A|B>
//   [LAN] ended <state> <reason>
// Hashes are 16 hex digits (MatchStateHash.Hex). The bases are after the round's result.
// Plain C# 9 without the engine.
// Depends on: LanMatchCoordinator (LanMatchState), Halobellum.Economy (MatchStateHash), Halobellum.Core (Team).

using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Net
{
    public static class LanMatchLog
    {
        public static string Round(int round, string stateHash, ulong outcomeHash, int baseA, int baseB) =>
            "[LAN] round " + round + " hash " + (string.IsNullOrEmpty(stateHash) ? "none" : stateHash) + " result " + MatchStateHash.Hex(outcomeHash)
            + " baseA " + baseA + " baseB " + baseB;

        public static string MatchOver(Team? winner) => "[LAN] match over winner " + (winner.HasValue ? winner.Value.ToString() : "none");

        public static string Ended(LanMatchState state, string reason) => Ended(state.ToString(), reason);

        /// <summary>The same for an end that is not a coordinator state (the guest's start refused or lost: "StartRefused", "StartLost").</summary>
        public static string Ended(string state, string reason) => ("[LAN] ended " + state + " " + OneLine(reason)).TrimEnd();

        private static string OneLine(string text) => (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
