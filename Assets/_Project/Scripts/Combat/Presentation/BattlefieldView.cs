// BattlefieldView.cs
// Shows the ground the battle is fought on (M10): the player's side looks like the player's faction's land, the enemy's side like the enemy's, with no man's land blending between them, real land out to the horizon, and a sky, sun and
// air set by the player's faction. A map is built by TerrainBuilder from the two factions and the match seed; it rebuilds when those change. In the Sandbox it follows the factions of the units in play.
// Deployment squares (decision D4) are an overlay: clear during deployment and fading out when a battle runs.
// The fighting floor stays flat and clear: props stand outside it, and paint on it can't hide a unit. Looks only: it reads the battle and the factions and changes neither.
// Depends on: BattleController (Rules), MatchController, SandboxController, BattleFactions, TerrainBuilder, UnitBlobShadows, UnityEngine.

using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;
using UnityEngine.Rendering;

namespace Halobellum.Presentation
{
    public sealed class BattlefieldView : MonoBehaviour
    {
        private static readonly int FogColorId = Shader.PropertyToID("_HBFogColor");
        private static readonly int FogRangeId = Shader.PropertyToID("_HBFogRange");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        [Tooltip("Seconds for the deployment squares to fade away when a battle starts, or back when it ends.")]
        [SerializeField] private float squaresFadeSeconds = 0.8f;
        [SerializeField] private Color teamASquaresColor = new Color(0.3f, 0.62f, 1f);
        [SerializeField] private Color teamBSquaresColor = new Color(1f, 0.36f, 0.26f);

        private BattleController _battle;
        private MatchController _match;
        private SandboxController _sandbox;
        private UnitBlobShadows _blobs;

        private Transform _root;
        private TerrainMap _map;
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Material> _overlays = new List<Material>();
        private float _squares = 1f;

        private string _shownA, _shownB;
        private int _shownSeed;
        private int _forcedA = -1, _forcedB = -1;
        private bool _forced;
        private string _wantedA, _wantedB;
        private float _wantedSince;

        // For tests and the terrain gallery.
        public TerrainMap Map => _map;
        public string ShownFactionA => _shownA;
        public string ShownFactionB => _shownB;
        /// <summary>How clear the deployment squares are, 0 to 1.</summary>
        public float SquaresShown => _squares;

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _match = GetComponent<MatchController>();
            _sandbox = GetComponent<SandboxController>();
            if (_battle.Rules == null) return;
            _blobs = gameObject.AddComponent<UnitBlobShadows>();
            Wanted(out string a, out string b, out int seed);
            Show(a, b, seed);
        }

        /// <summary>Shows this exact map and stops following the battle (the terrain gallery uses it). layout -1 lets the seed pick.</summary>
        public void ShowExactly(string factionA, string factionB, int seed, int layoutA, int layoutB)
        {
            _forced = true;
            _forcedA = layoutA;
            _forcedB = layoutB;
            Show(factionA, factionB, seed);
        }

        /// <summary>Goes back to following the battle.</summary>
        public void Follow()
        {
            _forced = false;
            _forcedA = _forcedB = -1;
        }

        private void LateUpdate()
        {
            if (_battle == null || _battle.Rules == null) return;
            UpdateSquares();
            if (_forced) return;
            Wanted(out string a, out string b, out int seed);
            if (a == _shownA && b == _shownB && seed == _shownSeed)
            {
                _wantedA = null;
                return;
            }
            // Never during a running battle, and in the Sandbox only once the armies have stopped changing, since a change of map costs a moment.
            if (_battle.IsPlaying) return;
            if (_sandbox != null)
            {
                if (a != _wantedA || b != _wantedB)
                {
                    _wantedA = a;
                    _wantedB = b;
                    _wantedSince = Time.unscaledTime;
                    return;
                }
                if (Time.unscaledTime - _wantedSince < 0.4f) return;
            }
            Show(a, b, seed);
        }

        /// <summary>The factions and seed the map ought to show now.</summary>
        private void Wanted(out string a, out string b, out int seed)
        {
            a = BattleFactions.TeamA;
            b = BattleFactions.TeamB;
            seed = 1;
            if (_sandbox != null)
            {
                seed = _sandbox.Seed;
                a = MostCommonFaction(Team.A) ?? BattleFactions.Unsc;
                b = MostCommonFaction(Team.B) ?? BattleFactions.Covenant;
                return;
            }
            if (_match != null && _match.Match != null)
            {
                // At the draft the factions aren't chosen yet, and what's shown is only a backdrop: keep it.
                if (_match.Match.Phase == MatchPhase.Draft && _shownA != null)
                {
                    a = _shownA;
                    b = _shownB;
                    seed = _shownSeed;
                    return;
                }
                seed = _match.Match.Seed;
            }
        }

        private readonly Dictionary<string, string> _factionOfUnit = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _counting = new Dictionary<string, int>();

        /// <summary>The faction most of this side's squads belong to in the Sandbox, or null if it has none.</summary>
        private string MostCommonFaction(Team team)
        {
            if (_factionOfUnit.Count == 0)
            {
                foreach (SandboxController.SandboxUnit unit in _sandbox.Units)
                {
                    if (unit.Faction != SandboxController.ExtraUnitsLabel) _factionOfUnit[unit.Stats.Name] = unit.Faction;
                }
            }
            _counting.Clear();
            string best = null;
            int bestCount = 0;
            foreach (PlacedSquad squad in _sandbox.Army(team).Squads)
            {
                if (!_factionOfUnit.TryGetValue(squad.Type.Name, out string faction)) continue;
                _counting.TryGetValue(faction, out int count);
                count++;
                _counting[faction] = count;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = faction;
                }
            }
            return best;
        }

        private void Show(string factionA, string factionB, int seed)
        {
            _shownA = factionA;
            _shownB = factionB;
            _shownSeed = seed;
            _wantedA = null;

            Clear();
            CombatRules rules = _battle.Rules;
            _map = TerrainBuilder.Build(new TerrainRequest
            {
                FactionA = factionA,
                FactionB = factionB,
                Seed = seed,
                LayoutA = _forcedA,
                LayoutB = _forcedB,
                HalfWidth = rules.ArenaWidth * 0.5f,
                ZoneDepth = rules.DeploymentDepthSquares * rules.SquareSize,
                Gap = rules.NoMansLandSquares * rules.SquareSize,
            });
            _root = new GameObject("Battlefield").transform;

            Texture2D grain = Tile("GroundGrain");
            Texture2D macro = Tile("GroundMacro");

            Material ground = Make("Terrain/TerrainGround");
            ground.SetTexture("_L0", Tile(_map.ThemeA.Layers[0]));
            ground.SetTexture("_L1", Tile(_map.ThemeA.Layers[1]));
            ground.SetTexture("_L2", Tile(_map.ThemeA.Layers[2]));
            ground.SetTexture("_L3", Tile(_map.ThemeB.Layers[0]));
            ground.SetTexture("_L4", Tile(_map.ThemeB.Layers[1]));
            ground.SetTexture("_L5", Tile(_map.ThemeB.Layers[2]));
            ground.SetTexture("_Control", _map.Control);
            ground.SetTexture("_Macro", macro);
            ground.SetVector("_TileA", _map.ThemeA.Tiles);
            ground.SetVector("_TileB", _map.ThemeB.Tiles);
            ground.SetColor("_TintA", _map.ThemeA.GroundTint);
            ground.SetColor("_TintB", _map.ThemeB.GroundTint);
            Part("Ground", _map.Ground, ground);

            Material decals = Make("Terrain/TerrainDecal");
            decals.SetTexture("_Grain", grain);
            Part("Decals", _map.Decals, decals);

            Material solid = Make("Terrain/TerrainProp");
            solid.SetTexture("_Grain", grain);
            Part("Solid", _map.Solid, solid);

            Part("Glow", _map.Glow, Make("Terrain/TerrainGlow"));

            TerrainTheme air = _map.Atmosphere;
            Material haze = Make("Terrain/TerrainHaze");
            haze.SetTexture("_Macro", macro);
            Color hazeColor = air.Haze;
            hazeColor.a = air.HazeAmount;
            haze.SetColor("_Color", hazeColor);
            Part("Haze", _map.Haze, haze);

            Material sky = Make("Terrain/TerrainSky");
            sky.SetTexture("_Macro", macro);
            sky.SetColor("_Top", air.SkyTop);
            sky.SetColor("_Horizon", air.SkyHorizon);
            Color cloud = air.Cloud;
            cloud.a = air.CloudAmount;
            sky.SetColor("_Cloud", cloud);
            sky.SetColor("_SunColor", air.SunColor);
            Vector3 toSun = -(Quaternion.Euler(air.SunAngles.x, air.SunAngles.y, 0f) * Vector3.forward);
            sky.SetVector("_SunDirection", toSun);
            Color moon = air.Moon;
            moon.a = air.MoonAmount;
            sky.SetColor("_Moon", moon);
            Vector3 toMoon = air.MoonDirection.normalized;
            sky.SetVector("_MoonDirection", new Vector4(toMoon.x, toMoon.y, toMoon.z, air.MoonSize));
            Part("Sky", _map.Sky, sky);

            SquaresOver("Team A Squares", rules, Team.A, teamASquaresColor);
            SquaresOver("Team B Squares", rules, Team.B, teamBSquaresColor);

            ApplyAir(air);
            if (_blobs != null)
            {
                Vector3 forward = Quaternion.Euler(air.SunAngles.x, air.SunAngles.y, 0f) * Vector3.forward;
                float flatten = Mathf.Max(0.4f, -forward.y);
                _blobs.ThrowPerMetre = new Vector2(forward.x, forward.z) / flatten;
            }
            Debug.Log($"Terrain: {factionA} {_map.LayoutA.Name} against {factionB} {_map.LayoutB.Name}, seed {seed}: {_map.TriangleCount} triangles, built in {_map.BuildMilliseconds:0} ms.");
        }

        private void SquaresOver(string name, CombatRules rules, Team team, Color color)
        {
            float width = rules.ArenaWidth, depth = rules.DeploymentDepthSquares * rules.SquareSize, gap = rules.NoMansLandSquares * rules.SquareSize;
            float z0 = team == Team.A ? -gap * 0.5f - depth : gap * 0.5f;
            float z1 = z0 + depth;

            Material material = Make("Terrain/TerrainDeployment");
            material.SetColor("_Team", color);
            material.SetVector("_Zone", new Vector4(-width * 0.5f, z0, width * 0.5f, z1));
            material.SetFloat("_Square", rules.SquareSize);
            material.SetFloat(FadeId, _squares);
            _overlays.Add(material);

            Transform quad = PlaceholderArt.CreatePart(name, _root, PlaceholderArt.Quad, material);
            quad.SetPositionAndRotation(new Vector3(0f, TerrainCanvas.DecalHeight + 0.01f, (z0 + z1) * 0.5f), Quaternion.Euler(90f, 0f, 0f));
            quad.localScale = new Vector3(width, depth, 1f);
        }

        /// <summary>The squares show while placing, and fade out while a battle runs or its result is up.</summary>
        private void UpdateSquares()
        {
            bool show = !_battle.IsPlaying && (_battle.Simulation == null || !_battle.Simulation.IsFinished);
            float target = show ? 1f : 0f;
            if (Mathf.Approximately(_squares, target)) return;
            _squares = Mathf.MoveTowards(_squares, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, squaresFadeSeconds));
            foreach (Material overlay in _overlays) overlay.SetFloat(FadeId, _squares);
        }

        /// <summary>The sky colour behind everything, the scene's sun, ambient light, and the haze the terrain's shaders use.</summary>
        private void ApplyAir(TerrainTheme air)
        {
            Camera view = Camera.main;
            if (view != null) view.backgroundColor = air.SkyHorizon;

            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = air.SunColor;
                light.intensity = air.SunIntensity;
                light.transform.rotation = Quaternion.Euler(air.SunAngles.x, air.SunAngles.y, 0f);
                break;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = air.AmbientSky;
            RenderSettings.ambientEquatorColor = air.AmbientEquator;
            RenderSettings.ambientGroundColor = air.AmbientGround;

            Color fog = air.FogColor.linear;
            Shader.SetGlobalVector(FogColorId, new Vector4(fog.r, fog.g, fog.b, 1f));
            Shader.SetGlobalVector(FogRangeId, new Vector4(air.FogStart, air.FogEnd, air.FogAmount, 0f));
        }

        private void Part(string name, Mesh mesh, Material material) =>
            PlaceholderArt.CreatePart(name, _root, mesh, material);

        private Material Make(string shaderName)
        {
            var material = new Material(Resources.Load<Shader>(shaderName));
            _materials.Add(material);
            return material;
        }

        private static readonly Dictionary<string, Texture2D> Tiles = new Dictionary<string, Texture2D>();

        /// <summary>A tiled ground texture, loaded once. Sharp at a slant, since the camera always looks at the ground at a slant.</summary>
        private static Texture2D Tile(string name)
        {
            if (Tiles.TryGetValue(name, out Texture2D texture) && texture != null) return texture;
            texture = Resources.Load<Texture2D>("Terrain/" + name);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 8;
            }
            Tiles[name] = texture;
            return texture;
        }

        private void Clear()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            foreach (Material material in _materials) Destroy(material);
            _materials.Clear();
            _overlays.Clear();
            _map?.Destroy();
            _map = null;
        }

        private void OnDestroy() => Clear();
    }
}
