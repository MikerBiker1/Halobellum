// BootLoader.cs
// The Boot scene, the first a build loads: it reads the unit stats files (UnitStatsMods) from the sources it holds, then opens the main menu.
// The sources reach every unit's art; held by this scene instead of Resources, that art goes into this scene's own build file, and the
// build's resources file stays under GitHub's 100 MB a file (0.413.4). Opening the menu unloads the scene, and what only it used.
// Depends on: UnitStatsMods, UnitStatsSources, GameFlow, UnityEngine.

using Halobellum.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halobellum.Presentation
{
    public sealed class BootLoader : MonoBehaviour
    {
        public UnitStatsSources unitStatsSources;

        private void Awake()
        {
            UnitStatsMods.LoadAtLaunch(unitStatsSources);
            unitStatsSources = null;
            SceneManager.LoadScene(GameFlow.MainMenuScene);
        }
    }
}
