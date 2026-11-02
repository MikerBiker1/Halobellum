// HudLine.cs
// One line of HUD text made from numbers. OnGUI runs at least twice a frame, so a string built there is garbage at every frame rate; this keeps
// the string and builds a new one only when one of its numbers has changed:
//     if (_line.Changed(alive, tenths)) _line.Text = $"...";
//     GUI.Label(rect, _line.Text, style);
// Pass the numbers as they are shown (rounded the way the text rounds them), or it will rebuild for changes nobody can see.
// Depends on: nothing.

namespace Halobellum.UI
{
    public sealed class HudLine
    {
        private bool _built;
        private long _a, _b, _c, _d;

        /// <summary>The line as last built.</summary>
        public string Text { get; set; }

        /// <summary>True if these numbers differ from the ones the text was last built from. Remembers them.</summary>
        public bool Changed(long a, long b = 0L, long c = 0L, long d = 0L)
        {
            if (_built && a == _a && b == _b && c == _c && d == _d) return false;
            _built = true;
            _a = a;
            _b = b;
            _c = c;
            _d = d;
            return true;
        }

        /// <summary>A number as a key for a text that shows it with no decimals.</summary>
        public static long Whole(float value) => (long)System.Math.Round(value, System.MidpointRounding.AwayFromZero);

        /// <summary>A number as a key for a text that shows it with one decimal.</summary>
        public static long Tenths(float value) => Whole(value * 10f);
    }
}
