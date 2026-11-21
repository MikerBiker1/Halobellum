// TestBodies.cs
// The baked bodies by name, for tests: Art/BakedBodies/<name>.asset. They left Resources so that the build's resources file stays under GitHub's
// 100 MB a file; the game reaches them through each unit's own reference.
// Depends on: BakedBodyDefinition, UnityEditor.

using Halobellum.Units;
using UnityEditor;

namespace Halobellum.Tests
{
    internal static class TestBodies
    {
        public const string Folder = "Assets/_Project/Art/BakedBodies/";

        public static BakedBodyDefinition Load(string name) => AssetDatabase.LoadAssetAtPath<BakedBodyDefinition>(Folder + name + ".asset");
    }
}
