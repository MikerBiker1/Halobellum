// GamePause.cs
// The game's one pause switch (M10): paused means Time.timeScale 0, so BattleClock gets no time, no tick runs, and animation, effects
// and particles all stand still. Nothing that reads the simulation has to know about it. Only the menus use unscaled time.
// Anything that has to react (sound, say) listens to Changed. Every scene starts un-paused (GameFlow).
// Battle speed (BattleHud's Pause, 1x, 2x, 4x) is a separate thing and is kept: the battle resumes at the speed it had.
// Depends on: UnityEngine.

using System;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        /// <summary>Raised with the new state when the game pauses or resumes.</summary>
        public static event Action<bool> Changed;

        public static void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            Changed?.Invoke(true);
        }

        public static void Resume()
        {
            Time.timeScale = 1f;
            if (!IsPaused) return;
            IsPaused = false;
            Changed?.Invoke(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnStart()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
