// GameCulture.cs
// Decimals always use a point (M11). The game shows numbers with plain string formatting, which follows the PC's language, so on a French or German Windows
// 1.5 comes out as 1,5. This puts the whole game on the invariant culture before anything is shown, on this thread and on any thread started later.
// The game hooks in here when it starts; the editor hooks in through EditorCulture. It lives here rather than in Core, which may not use the engine.
// Once the first scene has loaded it writes one line in the log with a decimal in it. No test in the editor can tell whether the hook works in a real build, because
// EditorCulture has already done the same thing there, so this line in the player's log is the check for a build.
// Depends on: UnityEngine.

using System.Globalization;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class GameCulture
    {
        private static string _languageAtStart;

        /// <summary>Sets the culture of this thread and the one new threads start with to the invariant culture.</summary>
        public static void Apply()
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = invariant;
            CultureInfo.CurrentUICulture = invariant;
            CultureInfo.DefaultThreadCurrentCulture = invariant;
            CultureInfo.DefaultThreadCurrentUICulture = invariant;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ApplyOnStart()
        {
            _languageAtStart = CultureInfo.CurrentCulture.Name;
            Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LogOnceStarted() => Debug.Log($"GameCulture: the language at start was \"{_languageAtStart}\", and 1.5 is written {1.5f:0.0}.");
    }
}
