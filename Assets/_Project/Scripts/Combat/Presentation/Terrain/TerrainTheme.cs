// TerrainTheme.cs
// What each faction's land is like (M10): its three ground textures, how they are tiled, the shape of its hills, and, for the player's faction, the sky, sun, ambient
// light and haze of the whole map. Pure data plus the hill function. Looks only: the simulation knows nothing of any of this.
// Depends on: TerrainNoise, UnityEngine (colours), BattleFactions (the faction names).

using UnityEngine;

namespace Halobellum.Presentation
{
    public enum TerrainHillStyle
    {
        /// <summary>Soft rolling hills with far mountains (UNSC colonies).</summary>
        Rolling,
        /// <summary>Terraced rock with sharp crests (Covenant worlds).</summary>
        Terraced,
        /// <summary>Swollen mounds with creases between them (Flood).</summary>
        Swollen,
    }

    /// <summary>One faction's land and light. Copy with Clone to adjust it for one layout.</summary>
    public sealed class TerrainTheme
    {
        public string Faction;

        /// <summary>Texture names under Resources/Terrain: the main ground, then two others mixed in.</summary>
        public string[] Layers;
        /// <summary>Tiles per metre of each layer.</summary>
        public Vector3 Tiles = new Vector3(1f / 24f, 1f / 24f, 1f / 24f);
        public Color GroundTint = Color.white;

        public TerrainHillStyle Hills;
        public float HillHeight = 18f;
        public float HillSize = 130f;
        public float FarHeight = 150f;

        // Sky.
        public Color SkyTop, SkyHorizon, Cloud;
        public float CloudAmount = 0.5f;
        public Color Moon;
        public float MoonAmount;
        public Vector3 MoonDirection = new Vector3(0.5f, 0.5f, 1f);
        public float MoonSize = 0.1f;

        // Light.
        public Color SunColor;
        public float SunIntensity = 1.2f;
        public Vector2 SunAngles = new Vector2(50f, -30f); // Pitch and yaw of the light.
        public Color AmbientSky, AmbientEquator, AmbientGround;

        // Air.
        public Color FogColor;
        public float FogStart = 170f, FogEnd = 1100f, FogAmount = 1f;
        public Color Haze;
        public float HazeAmount = 0.2f;
        public Color MoteColor;
        public int MoteCount = 60;

        public TerrainTheme Clone() => (TerrainTheme)MemberwiseClone();

        /// <summary>This theme's hills above flat ground at a point, before they are pressed flat around the arena.</summary>
        public float HillAt(float x, float z, float distanceOut, float seedX, float seedZ)
        {
            float px = (x + seedX) / HillSize;
            float pz = (z + seedZ) / HillSize;
            float far = TerrainNoise.Smooth(240f, 800f, distanceOut);
            switch (Hills)
            {
                case TerrainHillStyle.Terraced:
                {
                    float n = TerrainNoise.Ridged(px * 0.7f, pz * 0.7f, 3);
                    float steps = 5f;
                    float stepped = Mathf.Floor(n * steps);
                    float within = n * steps - stepped;
                    float terraced = (stepped + TerrainNoise.Smooth(0.72f, 0.98f, within)) / steps;
                    float mountains = TerrainNoise.Ridged(px * 0.22f + 40f, pz * 0.22f, 4);
                    return terraced * HillHeight * 1.7f + mountains * mountains * FarHeight * far * 1.5f;
                }
                case TerrainHillStyle.Swollen:
                {
                    float n = TerrainNoise.Billow(px * 0.8f, pz * 0.8f, 3);
                    float mountains = TerrainNoise.Billow(px * 0.2f + 13f, pz * 0.2f, 3);
                    return n * HillHeight * 2.2f + mountains * FarHeight * far * 1.3f;
                }
                default:
                {
                    float n = TerrainNoise.Fbm(px, pz, 4);
                    float mountains = TerrainNoise.Ridged(px * 0.24f + 70f, pz * 0.24f, 4);
                    return Mathf.Max(0f, n - 0.28f) * HillHeight * 2.2f + mountains * FarHeight * far;
                }
            }
        }
    }

    public static class TerrainThemes
    {
        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        private static readonly TerrainTheme Unsc = new TerrainTheme
        {
            Faction = BattleFactions.Unsc,
            Layers = new[] { "GroundUnscDirt", "GroundUnscGrass", "GroundUnscRock" },
            Tiles = new Vector3(1f / 26f, 1f / 19f, 1f / 31f),
            Hills = TerrainHillStyle.Rolling,
            HillHeight = 26f,
            HillSize = 150f,
            FarHeight = 190f,
            SkyTop = Rgb(70, 118, 186),
            SkyHorizon = Rgb(196, 208, 212),
            Cloud = Rgb(244, 244, 240),
            CloudAmount = 0.75f,
            SunColor = Rgb(255, 244, 222),
            SunIntensity = 1.25f,
            SunAngles = new Vector2(52f, -32f),
            AmbientSky = Rgb(150, 170, 196),
            AmbientEquator = Rgb(128, 128, 118),
            AmbientGround = Rgb(82, 72, 58),
            FogColor = Rgb(190, 204, 210),
            FogStart = 190f,
            FogEnd = 1150f,
            FogAmount = 0.95f,
            Haze = Rgb(214, 208, 190),
            HazeAmount = 0.12f,
            MoteColor = Rgb(255, 240, 200),
            MoteCount = 30,
        };

        private static readonly TerrainTheme Covenant = new TerrainTheme
        {
            Faction = BattleFactions.Covenant,
            Layers = new[] { "GroundCovenantRock", "GroundCovenantPlate", "GroundCovenantSand" },
            Tiles = new Vector3(1f / 30f, 1f / 34f, 1f / 16f),
            Hills = TerrainHillStyle.Terraced,
            HillHeight = 24f,
            HillSize = 120f,
            FarHeight = 210f,
            SkyTop = Rgb(38, 26, 84),
            SkyHorizon = Rgb(170, 120, 186),
            Cloud = Rgb(214, 150, 204),
            CloudAmount = 0.5f,
            Moon = Rgb(216, 226, 244),
            MoonAmount = 0.9f,
            MoonDirection = new Vector3(0.45f, 0.5f, 1f),
            MoonSize = 0.14f,
            SunColor = Rgb(236, 216, 255),
            SunIntensity = 1.2f,
            SunAngles = new Vector2(42f, -48f),
            AmbientSky = Rgb(128, 118, 188),
            AmbientEquator = Rgb(112, 92, 136),
            AmbientGround = Rgb(66, 52, 86),
            FogColor = Rgb(150, 108, 168),
            FogStart = 170f,
            FogEnd = 1000f,
            FogAmount = 0.95f,
            Haze = Rgb(186, 140, 214),
            HazeAmount = 0.2f,
            MoteColor = Rgb(120, 230, 255),
            MoteCount = 70,
        };

        private static readonly TerrainTheme Flood = new TerrainTheme
        {
            Faction = BattleFactions.Flood,
            Layers = new[] { "GroundFloodMass", "GroundFloodRot", "GroundFloodSinew" },
            Tiles = new Vector3(1f / 22f, 1f / 24f, 1f / 20f),
            Hills = TerrainHillStyle.Swollen,
            HillHeight = 22f,
            HillSize = 110f,
            FarHeight = 170f,
            SkyTop = Rgb(26, 30, 22),
            SkyHorizon = Rgb(96, 104, 60),
            Cloud = Rgb(58, 56, 40),
            CloudAmount = 0.85f,
            SunColor = Rgb(226, 236, 180),
            SunIntensity = 1.15f,
            SunAngles = new Vector2(58f, 26f),
            AmbientSky = Rgb(112, 124, 88),
            AmbientEquator = Rgb(92, 92, 62),
            AmbientGround = Rgb(62, 50, 34),
            FogColor = Rgb(84, 92, 54),
            FogStart = 150f,
            FogEnd = 850f,
            FogAmount = 1f,
            Haze = Rgb(146, 150, 82),
            HazeAmount = 0.3f,
            MoteColor = Rgb(216, 224, 110),
            MoteCount = 140,
        };

        /// <summary>The theme of a faction. Anything unknown looks like the UNSC.</summary>
        public static TerrainTheme For(string faction)
        {
            if (faction == BattleFactions.Covenant) return Covenant;
            if (faction == BattleFactions.Flood) return Flood;
            return Unsc;
        }
    }
}
