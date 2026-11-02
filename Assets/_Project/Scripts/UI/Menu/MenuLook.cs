// MenuLook.cs
// The fonts of the main menu, settings and pause screens (M10): an asset at Resources/MenuLook, made by MainMenuSceneBuilder, so that the
// pause menu, which is added to a scene at run time, can find them too.
// Depends on: UnityEngine.

using UnityEngine;

namespace Halobellum.UI
{
    public sealed class MenuLook : ScriptableObject
    {
        public const string ResourcePath = "MenuLook";

        [Tooltip("The big HALOBELLUM title.")]
        public Font titleFont;
        [Tooltip("Buttons and headings.")]
        public Font headingFont;
        [Tooltip("Everything else.")]
        public Font textFont;
    }
}
