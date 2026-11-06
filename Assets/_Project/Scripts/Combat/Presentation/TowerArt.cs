// TowerArt.cs
// The towers' looks (D55). Each is the owner's Meshy model, a prefab in Resources/Towers named after its kind (RealModelBuilder makes
// them from Tools/Art/units/CommandCenter.py and ResearchCenter.py): the Command Center a domed hall with a radar dish, the Research
// Center a striped sphere beside a dome. The Command Center's trim and the Research Center's lights are a mask in the model's glow,
// lit here in the colour of the faction that owns the tower (FactionColors), so the rest of the texture is untouched. A thin slab in
// the side's colour lies under each, so that two towers of one faction still show whose they are. If a prefab is missing, the tower
// is put together from plain shapes as before.
// Depends on: PlaceholderArt, Halobellum.Core (TowerKind), UnityEngine.

using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class TowerArt
    {
        public const string ResourceFolder = "Towers";

        private static readonly Color Steel = new Color(0.34f, 0.36f, 0.4f), DarkSteel = new Color(0.17f, 0.18f, 0.21f);
        private static readonly Color Glow = new Color(0.55f, 0.85f, 1f);
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>How brightly each tower's mask glows in its faction's colour: the Command Center's trim is paint that catches the light, the Research Center's are lights.</summary>
        public const float TrimGlow = 0.9f, LightGlow = 1.6f;
        private const float SlabHeight = 0.06f;

        /// <summary>The prefab of a tower of this kind, or null if it hasn't been built.</summary>
        public static GameObject PrefabFor(TowerKind kind) => Resources.Load<GameObject>(ResourceFolder + "/" + kind);

        /// <summary>The colour a tower's mask glows in, for its faction's colour.</summary>
        public static Color GlowFor(TowerKind kind, Color faction)
        {
            float strength = kind == TowerKind.CommandCenter ? TrimGlow : LightGlow;
            return new Color(faction.r * strength, faction.g * strength, faction.b * strength, 1f);
        }

        /// <summary>Builds the tower under this root, for a body of this radius, with its side's colour and its faction's. Returns how tall it is.</summary>
        public static float Build(Transform root, TowerKind kind, Color team, Color faction, float radius)
        {
            GameObject prefab = PrefabFor(kind);
            if (prefab == null) return BuildFromShapes(root, kind, team, radius);

            Part(root, PlaceholderArt.Cube, team, new Vector3(0f, SlabHeight * 0.5f, 0f), new Vector3(radius * 2f, SlabHeight, radius * 2f));
            GameObject body = Object.Instantiate(prefab, root, false);
            body.name = "Body";
            var block = new MaterialPropertyBlock();
            Color glow = GlowFor(kind, faction);
            float top = 0f;
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, glow);
                renderer.SetPropertyBlock(block);
                top = Mathf.Max(top, renderer.bounds.max.y - root.position.y);
            }
            return top;
        }

        // ------------------------------------------------------------------ plain shapes, if the models are missing

        private static float BuildFromShapes(Transform root, TowerKind kind, Color team, float radius)
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
