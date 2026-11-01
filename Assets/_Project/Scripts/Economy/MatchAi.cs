// MatchAi.cs
// The AI opponent's decisions. The AI plays under the same rules as the player (Bible Section 11)
// and only sees and acts through an AiView (no access to the opponent's current deployment).
// RuleBasedAi is the real opponent (Milestone 5); the do-nothing AI picks the first draft card,
// keeps its 2 free starting squads and never acts (used by tests).
// Depends on: AiView, DraftCard.

using System.Collections.Generic;

namespace Halobellum.Economy
{
    public interface IMatchAi
    {
        /// <summary>Index of the draft card to pick.</summary>
        int ChooseDraftCard(AiView view, IReadOnlyList<DraftCard> cards);

        /// <summary>
        /// The card to take from this round's offer (Milestone 7), or -1 to skip it. Called at the start
        /// of the round, before Deploy.
        /// </summary>
        int ChooseCard(AiView view, CardOffer offer);

        /// <summary>Spend credits, place squads and place spells for this round's deployment phase.</summary>
        void Deploy(AiView view);

        /// <summary>A short name for the HUD, e.g. "Normal AI".</summary>
        string Label { get; }
    }

    public sealed class DoNothingAi : IMatchAi
    {
        public int ChooseDraftCard(AiView view, IReadOnlyList<DraftCard> cards) => 0;

        public int ChooseCard(AiView view, CardOffer offer) => -1;

        public void Deploy(AiView view)
        {
        }

        public string Label => "Passive AI";
    }
}
