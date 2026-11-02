// CardLayout.cs
// Where the cards of a card screen go (M10): a row of 1 to 5 cards, centred, that fits any screen. Plain arithmetic,
// no drawing, so that a test can check every card count at every screen size. All sizes are in the HUD's virtual units
// (HudScale: the screen is always 1080 units tall, or more on a very small window, and as wide as its shape makes it).
// Depends on: UnityEngine (Rect, Mathf).

using UnityEngine;

namespace Halobellum.UI
{
    /// <summary>A row of equal cards.</summary>
    public readonly struct CardRow
    {
        public readonly float CardWidth, CardHeight, Gap, Left, Top;
        public readonly int Count;

        public CardRow(int count, float cardWidth, float cardHeight, float gap, float left, float top)
        {
            Count = count;
            CardWidth = cardWidth;
            CardHeight = cardHeight;
            Gap = gap;
            Left = left;
            Top = top;
        }

        public Rect CardAt(int index) => new Rect(Left + index * (CardWidth + Gap), Top, CardWidth, CardHeight);
    }

    public static class CardLayout
    {
        /// <summary>The widest a card gets, however few of them there are.</summary>
        public const float MostWidth = 420f;
        /// <summary>The tallest a card gets, however much room there is.</summary>
        public const float MostHeight = 760f;
        /// <summary>Room kept free at the left and right of the screen.</summary>
        public const float SideMargin = 14f;
        public const float LeastGap = 8f, MostGap = 26f;

        /// <summary>Fits `count` cards across the screen, between the heights `top` and `bottom`.</summary>
        public static CardRow Arrange(int count, float screenWidth, float top, float bottom)
        {
            int n = Mathf.Max(1, count);
            float room = Mathf.Max(1f, screenWidth - 2f * SideMargin);
            float gap = Mathf.Clamp(room * 0.012f, LeastGap, MostGap);
            float width = Mathf.Min(MostWidth, (room - (n - 1) * gap) / n);
            float height = Mathf.Min(MostHeight, Mathf.Max(1f, bottom - top));
            float total = n * width + (n - 1) * gap;
            return new CardRow(n, width, height, gap, (screenWidth - total) * 0.5f, top + (bottom - top - height) * 0.5f);
        }
    }
}
