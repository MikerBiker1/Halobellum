// TowerArt.cs
// The towers' looks (D55), put together from plain shapes until real models are found (Assets_to_Download.md). The Command Center
// is a block with a mast and a radar dish, the Research Center a lab with a glowing dome. Each carries a band in its side's colour.
// Depends on: PlaceholderArt, Halobellum.Core (TowerKind), UnityEngine.

using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class TowerArt
    {
        private static readonly Color Steel = new Color(0.34f, 0.36f, 0.4f), DarkSteel = new Color(0.17f, 0.18f, 0.21f);
        private static readonly Color Glow = new Color(0.55f, 0.85f, 1f);

        /// <summary>Builds the tower under this root, for a body of this radius. Returns how tall it is.</summary>
        public static float Build(Transform root, TowerKind kind, Color team, float radius)
        {
            float width = radius * 2f;
            Part(root, PlaceholderArt.Cube, DarkSteel, new Vector3(0f, 0.4f, 0f), new Vector3(width, 0.8f, width));
            Part(root, PlaceholderArt.Cube, team, new Vector3(0f, 1f, 0f), new Vector3(width * 0.86f, 0.4f, width * 0.86f));
            return kind == TowerKind.CommandCenter ? CommandCenter(root, width) : ResearchCenter(root, width);
        }

        private static float CommandCenter(Transform root, float width)
        {
            Part(root, PlaceholderArt.Cube, Steel, new Vector3(0f, 3.2f, 0f), new Vector3(width * 0.7f, 4f, width * 0.7f));
            Part(root, PlaceholderArt.Cube, Glow, new Vector3(0f, 4.4f, -width * 0.36f), new Vector3(width * 0.5f, 0.5f, 0.1f));
            Part(root, PlaceholderArt.Cylinder, DarkSteel, new Vector3(0f, 7f, 0f), new Vector3(0.35f, 1.8f, 0.35f));
            Transform dish = Part(root, PlaceholderArt.Cylinder, Steel, new Vector3(0f, 8.4f, 0f), new Vector3(width * 0.6f, 0.08f, width * 0.6f));
            dish.localRotation = Quaternion.Euler(30f, 0f, 0f);
            return 9.5f;
        }

        private static float ResearchCenter(Transform root, float width)
        {
            Part(root, PlaceholderArt.Cylinder, Steel, new Vector3(0f, 3f, 0f), new Vector3(width * 0.72f, 1.8f, width * 0.72f));
            Part(root, PlaceholderArt.Cylinder, DarkSteel, new Vector3(0f, 5f, 0f), new Vector3(width * 0.82f, 0.2f, width * 0.82f));
            Part(root, PlaceholderArt.Sphere, Glow, new Vector3(0f, 5.6f, 0f), new Vector3(width * 0.62f, width * 0.62f, width * 0.62f));
            Part(root, PlaceholderArt.Cylinder, DarkSteel, new Vector3(0f, 8f, 0f), new Vector3(0.2f, 0.9f, 0.2f));
            return 9f;
        }

        private static Transform Part(Transform root, Mesh mesh, Color color, Vector3 position, Vector3 scale)
        {
            Transform part = PlaceholderArt.CreatePart("Tower Part", root, mesh, PlaceholderArt.MaterialFor(color));
            part.localPosition = position;
            part.localScale = scale;
            return part;
        }
    }
}
