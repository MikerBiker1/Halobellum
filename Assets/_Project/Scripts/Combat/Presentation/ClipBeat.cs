// ClipBeat.cs
// The arithmetic of a baked clip that keeps time with the battle (D90): how fast it plays, and when it must have started, for its moment
// (a blow landing, a shot leaving, a birth) to come with the battle's next beat (a weapon's strike, a spawn), and with every beat after.
// UnitViewManager uses it for the Stalker Form's lunges, the Pure Form Ranged's thrust and the Spawner's heaving belly.
// Depends on: UnityEngine (Mathf).

using UnityEngine;

namespace Halobellum.Presentation
{
    public static class ClipBeat
    {
        /// <summary>
        /// A clip seconds long that keeps time with beats beat seconds apart, the next of them untilNext seconds from now. As many blows as fit
        /// in the clip land one a beat, so it plays at pace (times its own speed); it started at start for its moment (a share of it) to come then.
        /// </summary>
        public static void Time(float seconds, float beat, float moment, float now, float untilNext, out float pace, out float start)
        {
            int beats = Mathf.Max(1, Mathf.RoundToInt(seconds / beat));
            pace = seconds / (beats * beat);
            start = now + untilNext - moment * seconds / pace;
        }

        /// <summary>Of the starts a whole beat apart, each as good as the others, the one nearest the start of what is playing.</summary>
        public static float Nearest(float start, float playing, float beat) => start + Mathf.Round((playing - start) / beat) * beat;
    }
}
