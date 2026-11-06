// TutorialAi.cs
// The tutorial's enemy (D77): the same small army every time, bought round by round from a fixed list, so that every player sees the
// same match and a player following the steps wins. It never takes a card. Units it can't afford or place this round are left for later.
// Depends on: IMatchAi, AiView, Halobellum.Core.

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed class TutorialAi : IMatchAi
    {
        private readonly Dictionary<int, string[]> _buys;

        /// <param name="buys">For each round, the units to buy (by name), in order.</param>
        public TutorialAi(Dictionary<int, string[]> buys)
        {
            _buys = buys ?? new Dictionary<int, string[]>();
        }

        public string Label => "Tutorial AI";

        /// <summary>The tutorial gives it one card.</summary>
        public int ChooseDraftCard(AiView view, IReadOnlyList<DraftCard> cards) => 0;

        public int ChooseCard(AiView view, CardOffer offer) => -1;

        public void Deploy(AiView view)
        {
            if (!_buys.TryGetValue(view.Round, out string[] names)) return;
            foreach (string name in names)
            {
                UnitStats type = view.Self.Roster.Find(u => u.Name == name);
                if (type == null) continue;
                if (!view.Self.Unlocked.Contains(type) && !view.Unlock(type).Ok) continue;
                view.Buy(type);
            }
        }
    }
}
