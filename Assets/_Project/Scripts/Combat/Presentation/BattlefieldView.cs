// BattlefieldView.cs
// Draws the arena floor from the Combat Rules: Team A's deployment zone (bottom), the strip of no
// man's land, and Team B's zone (top), with a faint checkerboard showing the deployment squares
// (decision D4), on top of a larger dark ground. Placeholder art; built once when the battle starts.
// Depends on: BattleController (Rules), PlaceholderArt, UnityEngine.

using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class BattlefieldView : MonoBehaviour
    {
        [SerializeField] private Color groundColor = new Color(0.16f, 0.17f, 0.16f);
        [SerializeField] private Color teamAZoneColor = new Color(0.28f, 0.32f, 0.38f);
        [SerializeField] private Color teamBZoneColor = new Color(0.38f, 0.3f, 0.29f);
        [SerializeField] private Color noMansLandColor = new Color(0.22f, 0.22f, 0.2f);
        [Tooltip("How much lighter the alternate squares of the checkerboard are.")]
        [SerializeField] private float checkerContrast = 0.06f;

        private void Start()
        {
            var battle = GetComponent<BattleController>();
            if (battle.Rules == null) return;

            var floor = new GameObject("Battlefield").transform;
            float width = battle.Rules.ArenaWidth;
            float length = battle.Rules.ArenaLength;
            float square = battle.Rules.SquareSize;
            float zoneDepth = battle.Rules.DeploymentDepthSquares * square;
            float gap = battle.Rules.NoMansLandSquares * square;

            CreateFloor("Ground", floor, new Vector2(0f, 0f), new Vector2(width * 3f, length * 3f), -0.05f, MaterialPlain(groundColor));
            CreateFloor("Team A Zone", floor, new Vector2(0f, -(gap + zoneDepth) * 0.5f), new Vector2(width, zoneDepth), 0f,
                MaterialChecker(teamAZoneColor, battle.Rules.ArenaWidthSquares, battle.Rules.DeploymentDepthSquares));
            CreateFloor("Team B Zone", floor, new Vector2(0f, (gap + zoneDepth) * 0.5f), new Vector2(width, zoneDepth), 0f,
                MaterialChecker(teamBZoneColor, battle.Rules.ArenaWidthSquares, battle.Rules.DeploymentDepthSquares));
            if (gap > 0f) CreateFloor("No Man's Land", floor, Vector2.zero, new Vector2(width, gap), 0f, MaterialPlain(noMansLandColor));
        }

        private static void CreateFloor(string name, Transform parent, Vector2 centre, Vector2 size, float height, Material material)
        {
            Transform quad = PlaceholderArt.CreatePart(name, parent, PlaceholderArt.Quad, material);
            quad.SetPositionAndRotation(new Vector3(centre.x, height, centre.y), Quaternion.Euler(90f, 0f, 0f));
            quad.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<MeshRenderer>().receiveShadows = true;
        }

        private static Material MaterialPlain(Color color) => new Material(PlaceholderArt.MaterialFor(color));

        /// <summary>A material with a 2 x 2 checker texture tiled so each cell is one deployment square.</summary>
        private Material MaterialChecker(Color color, int squaresWide, int squaresDeep)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            Color light = Color.white;
            Color dark = new Color(1f - checkerContrast, 1f - checkerContrast, 1f - checkerContrast, 1f);
            texture.SetPixels(new[] { light, dark, dark, light });
            texture.Apply();

            var material = new Material(PlaceholderArt.MaterialFor(color)) { mainTexture = texture };
            material.mainTextureScale = new Vector2(squaresWide * 0.5f, squaresDeep * 0.5f);
            return material;
        }
    }
}
