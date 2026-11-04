// GameCulture.cs
// Decimals always use a point (M11). The game shows numbers with plain string formatting, which follows the PC's language, so on a French or German Windows
// 1.5 comes out as 1,5. This puts the whole game on the invariant culture before anything is shown, on this thread and on any thread started later.
// The game hooks in here when it starts; the editor hooks in through EditorCulture. It lives here rather than in Core, which may not use the engine.
// Depends on: UnityEngine.

using System.Globalization;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class GameCulture
    {
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
        private static void ApplyOnStart() => Apply();
    }
}
