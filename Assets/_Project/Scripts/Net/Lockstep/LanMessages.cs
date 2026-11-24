// LanMessages.cs
// The names and fields of the messages a LAN match sends once the handshake is done (Milestone 11, lockstep of decisions).
//   MatchStart   host -> guest, once: {proto, seed, mode, baseHealth, cards, content, state}   (LanMatchStart)
//   DraftPick    both, once:          {round: 0, index, commands}    the side's starting card, as its recorded command list
//   Ready        both, every round:   {round, commands}              the side's whole command list for the round
//   StateHash    both, every round:   {round, hash}                  MatchStateHash once both lists are applied
//   BattleResult host -> guest:       {round, outcome, hash, state}  RoundOutcome text, its Hash(), MatchStateHash after it
//   Desync       either:              {round, reason}                this game found the match out of sync; both stop
//   Leave        either:              {reason, refused}              the player left (or the guest refused the MatchStart)
// Command lists are MatchCommand.ListToText (one JSON command per line, floats as int bits), so positions go across exactly.
// Hashes are whole unsigned 64-bit numbers (JsonObject.Set(key, ulong), read with GetULong).
// Plain C# 9 without the engine.
// Depends on: NetMessage.

namespace Halobellum.Net
{
    public static class LanMessageTypes
    {
        public const string MatchStart = "MatchStart";
        public const string DraftPick = "DraftPick";
        public const string Ready = "Ready";
        public const string StateHash = "StateHash";
        public const string BattleResult = "BattleResult";
        public const string Desync = "Desync";
        public const string Leave = "Leave";
    }

    /// <summary>The field names the LAN match messages use.</summary>
    public static class LanFields
    {
        public const string Round = "round";
        public const string Index = "index";
        public const string Commands = "commands";
        public const string Hash = "hash";
        public const string Outcome = "outcome";
        public const string State = "state";
        public const string Reason = "reason";
        public const string Refused = "refused";
    }
}
