// MatchAi.cs
// The AI opponent's decisions. The AI plays under the same rules as the player (Bible Section 11)
// and only acts through the same Match actions. Milestone 3 uses the do-nothing AI: it picks the
// first draft card, keeps its 2 free starting squads where the match places them, and never buys,
// unlocks or sells anything. The rule-based AI comes in Milestone 5.
// Depends on: Match, PlayerState, DraftCard.

using System.Collections.Generic;

namespace Halobellum.Economy
{
    public interface IMatchAi
    {
        /// <summary>Index of the draft card to pick.</summary>
        int ChooseDraftCard(Match match, PlayerState self, IReadOnlyList<DraftCard> cards);

        /// <summary>Spend credits and place squads for this round's deployment phase.</summary>
        void Deploy(Match match, PlayerState self);
    }

    public sealed class DoNothingAi : IMatchAi
    {
        public int ChooseDraftCard(Match match, PlayerState self, IReadOnlyList<DraftCard> cards) => 0;

        public void Deploy(Match match, PlayerState self)
        {
        }
    }
}
