// EditorCulture.cs
// Decimals use a point in the editor too (M11): after every script reload the editor goes on the invariant culture through GameCulture, so that edit mode tests
// and editor tools format numbers the same way the game does, whatever language the PC is set to.
// Depends on: Halobellum.Presentation, UnityEditor.

using Halobellum.Presentation;
using UnityEditor;

namespace Halobellum.EditorTools
{
    public static class EditorCulture
    {
        [InitializeOnLoadMethod]
        private static void ApplyOnLoad() => GameCulture.Apply();
    }
}
