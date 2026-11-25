// BattlefieldView.cs
// Shows the ground the battle is fought on. Since D80 the whole field is one interior look, picked from the two factions and the match seed by TerrainLooks.Pick: a UNSC installation,
// a Covenant warship, or a ship the Flood has taken, with its own ground, light, ceiling, surroundings and a mild mood in a post-processing volume. The outdoor maps of M10 (the player's side
// the player's faction's land, the enemy's side the enemy's, real land to the horizon, sky, sun and air from the player's faction) are still here for the terrain gallery (ShowOutdoor). A map is built by TerrainBuilder from the two factions and the match seed; it rebuilds when those change. In the Sandbox it follows the factions of the units in play, or the map its Map setting names (SandboxMaps, D82).
// Deployment squares (decision D4) are an overlay: clear during deployment and fading out when a battle runs. The local side's squares and flank zones
// are blue and the other side's red (LocalTeam, M11: Team A except for a LAN guest).
// The fighting floor stays flat and clear: props stand outside it, paint on it can't hide a unit, and from wherever the camera has turned the terrain's shaders leave out what would stand in front of it. Looks only: it reads the battle and the factions and changes neither.
// Depends on: BattleController (Rules), MatchController, SandboxController, BattleFactions, TerrainBuilder, UnitBlobShadows, UnityEngine.

using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Halobellum.Presentation
{
    public sealed class BattlefieldView : MonoBehaviour
    {
        private static readonly int FogColorId = Shader.PropertyToID("_HBFogColor");
        private static readonly int FogRangeId = Shader.PropertyToID("_HBFogRange");
        private static readonly int FloorId = Shader.PropertyToID("_HBFloor");
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
        private Material _squaresA, _squaresB;
        private Team _localTeam = Team.A;

        /// <summary>The side whose squares and flank zones are blue (the own colour); the other side's are red. Team A except for a LAN guest.</summary>
        public Team LocalTeam
        {
            get => _localTeam;
            set
            {
                if (value == _localTeam) return;
                _localTeam = value;
                if (_squaresA != null) _squaresA.SetColor("_Team", SquaresColor(Team.A));
                if (_squaresB != null) _squaresB.SetColor("_Team", SquaresColor(Team.B));
                if (_flankTeam.HasValue)
                {
                    Team? shown = _flankTeam;
                    _flankTeam = null;
                    ShowFlankZones(shown);
                }
            }
        }

        /// <summary>A side's colour for its squares and flank zones: the own colour for the local side.</summary>
        public Color SquaresColor(Team team) => team == _localTeam ? teamASquaresColor : teamBSquaresColor;

        private string _shownA, _shownB;
        private int _shownSeed, _shownMap;
        private int _forcedA = -1, _forcedB = -1;
        private bool _forced;
        private TerrainLook _forcedLook;
        private bool _forcedOutdoor;
        private Volume _mood;
        private readonly BattlefieldLighting _lighting = new BattlefieldLighting();
        private readonly List<MeshRenderer> _mirrors = new List<MeshRenderer>();
        private int _qualityShown = -1;
        private Light _sun;
        private UniversalRenderPipelineAsset _shadowAsset;
        private float _assetShadowDistance;
        private Vector3 _assetCascades;
        private int _assetCascadeCount;
        private string _wantedA, _wantedB;
        private float _wantedSince;

        // For tests and the terrain gallery.
        public TerrainMap Map => _map;
        public string ShownFactionA => _shownA;
        public string ShownFactionB => _shownB;
        /// <summary>How clear the deployment squares are, 0 to 1.</summary>
        public float SquaresShown => _squares;
        /// <summary>The look's own post-processing profile on the mood volume (BattlefieldLighting): named after the look's Id, or "Outdoor".</summary>
        public VolumeProfile MoodProfile => _mood != null ? _mood.sharedProfile : null;
        /// <summary>True while the polished floor's mirror is drawn (a look with one, on the High quality level).</summary>
        public bool MirrorShown
        {
            get
            {
                foreach (MeshRenderer mirror in _mirrors)
                    if (mirror != null && mirror.enabled) return true;
                return false;
            }
        }

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
        public void ShowExactly(string factionA, string factionB, int seed, int layoutA, int layoutB) => ShowOutdoor(factionA, factionB, seed, layoutA, layoutB);

        /// <summary>Shows the outdoor map of M10 with these layouts and stops following the battle (the terrain gallery's -terrainOutdoor). layout -1 lets the seed pick.</summary>
        public void ShowOutdoor(string factionA, string factionB, int seed, int layoutA, int layoutB)
        {
            _forced = true;
            _forcedOutdoor = true;
            _forcedLook = null;
            _forcedA = layoutA;
            _forcedB = layoutB;
            Show(factionA, factionB, seed);
        }

        /// <summary>Shows this interior look with these factions and stops following the battle (the terrain gallery).</summary>
        public void ShowLook(TerrainLook look, string factionA, string factionB, int seed)
        {
            _forced = true;
            _forcedOutdoor = false;
            _forcedLook = look;
            _forcedA = _forcedB = -1;
            Show(factionA, factionB, seed);
        }

        /// <summary>Goes back to following the battle.</summary>
        public void Follow()
        {
            _forced = false;
            _forcedOutdoor = false;
            _forcedLook = null;
            _forcedA = _forcedB = -1;
        }

        private void LateUpdate()
        {
            if (_battle == null || _battle.Rules == null) return;
            if (QualitySettings.GetQualityLevel() != _qualityShown && _map != null) ApplyQuality();
            UpdateShadowRange();
            UpdateSquares();
            UpdateFlankPulse();
            if (_forced) return;
            Wanted(out string a, out string b, out int seed);
            if (a == _shownA && b == _shownB && seed == _shownSeed && SandboxMapChoice == _shownMap)
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

        /// <summary>The Sandbox's Map setting (SandboxMaps), 0 for "as in a match" and outside the Sandbox.</summary>
        private int SandboxMapChoice => _sandbox != null ? _sandbox.MapChoice : 0;

        /// <summary>The factions and seed the map ought to show now.</summary>
        private void Wanted(out string a, out string b, out int seed)
        {
            a = BattleFactions.TeamA;
            b = BattleFactions.TeamB;
            seed = 1;
            if (_sandbox != null)
            {
                seed = _sandbox.Seed; // SandboxController sets BattleFactions from its armies.
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

        private void Show(string factionA, string factionB, int seed)
        {
            _shownA = factionA;
            _shownB = factionB;
            _shownSeed = seed;
            _shownMap = SandboxMapChoice;
            _wantedA = null;
            // The Sandbox can be set to any map (D82); otherwise a match's interior look, or what the terrain gallery forced.
            SandboxMap chosen = _forced ? null : SandboxMaps.At(_shownMap);
            bool outdoor = _forcedOutdoor || (chosen != null && chosen.Outdoor);
            bool oneLand = chosen != null && chosen.Outdoor && chosen.Land != null;

            Clear();
            CombatRules rules = _battle.Rules;
            Shader.SetGlobalVector(FloorId, new Vector4(rules.ArenaWidth * 0.5f, rules.DeploymentDepthSquares * rules.SquareSize + rules.NoMansLandSquares * rules.SquareSize * 0.5f, 0f, 0f));
            _map = TerrainBuilder.Build(new TerrainRequest
            {
                FactionA = oneLand ? chosen.Land : factionA,
                FactionB = oneLand ? chosen.Land : factionB,
                Seed = seed,
                LayoutA = oneLand ? chosen.Layout : _forcedA,
                LayoutB = oneLand ? chosen.Layout : _forcedB,
                Look = outdoor ? null : _forcedLook ?? chosen?.Look ?? TerrainLooks.Pick(factionA, factionB, seed),
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
            ground.SetVector("_GlossA", _map.ThemeA.Gloss);
            ground.SetVector("_GlossB", _map.ThemeB.Gloss);
            ground.SetColor("_GlossColor", _map.Atmosphere.GlossColor);
            ground.SetVector("_GlossDir", -(Quaternion.Euler(_map.Atmosphere.GlossLightAngles.x, _map.Atmosphere.GlossLightAngles.y, 0f) * Vector3.forward));
            Part("Ground", _map.Ground, ground);

            Material decals = Make("Terrain/TerrainDecal");
            decals.SetTexture("_Grain", grain);
            Part("Decals", _map.Decals, decals);

            TerrainTheme air = _map.Atmosphere;
            Material solid = Make("Terrain/TerrainProp");
            solid.SetTexture("_Grain", grain);
            solid.SetFloat("_GlowBoost", air.PropGlow);
            // What stands casts the sun's shadow (High quality, the shadows pass); the floor, its decals and glows only receive it.
            PlaceholderArt.CreatePart("Solid", _root, _map.Solid, solid).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

            Material glow = Make("Terrain/TerrainGlow");
            glow.SetFloat("_Core", air.GlowCore);
            Part("Glow", _map.Glow, glow);
            if (air.Mirror > 0f)
            {
                _mirrors.Add(Mirror("Mirror Solid", _map.Solid, air, false));
                _mirrors.Add(Mirror("Mirror Glow", _map.Glow, air, true));
            }

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

            _squaresA = SquaresOver("Team A Squares", rules, Team.A, SquaresColor(Team.A));
            _squaresB = SquaresOver("Team B Squares", rules, Team.B, SquaresColor(Team.B));

            ApplyAir(air);
            ApplyQuality();
            if (_blobs != null)
            {
                Vector3 forward = Quaternion.Euler(air.SunAngles.x, air.SunAngles.y, 0f) * Vector3.forward;
                float flatten = Mathf.Max(0.4f, -forward.y);
                _blobs.ThrowPerMetre = new Vector2(forward.x, forward.z) / flatten;
            }
            Debug.Log($"Terrain: {factionA} against {factionB}, seed {seed}: {_map.Describe()}, {_map.TriangleCount} triangles, built in {_map.BuildMilliseconds:0} ms.");
        }

        private Transform _flankLeft, _flankRight;
        private Team? _flankTeam;
        private bool _flankPulsed;
        /// <summary>The flank zones glow on and off (the tutorial's flank zone step).</summary>
        public bool PulseFlankZones { get; set; }

        /// <summary>Shows the flank zones of this team, beside the enemy's area, or none (D61).</summary>
        public void ShowFlankZones(Team? team)
        {
            if (_root == null || team == _flankTeam && _flankLeft != null == team.HasValue) return;
            if (_flankLeft != null) Destroy(_flankLeft.gameObject);
            if (_flankRight != null) Destroy(_flankRight.gameObject);
            _flankLeft = _flankRight = null;
            _flankTeam = team;
            if (!team.HasValue) return;

            CombatRules rules = _battle.Rules;
            Color color = SquaresColor(team.Value);
            float near = rules.ArenaWidth * 0.5f + rules.FlankGapColumns * rules.SquareSize, far = near + rules.FlankZoneColumns * rules.SquareSize;
            _flankLeft = FlankZone(rules, team.Value, -far, -near, color);
            _flankRight = FlankZone(rules, team.Value, near, far, color);
        }

        private Transform FlankZone(CombatRules rules, Team team, float x0, float x1, Color color)
        {
            float depth = rules.DeploymentDepthSquares * rules.SquareSize, gap = rules.NoMansLandSquares * rules.SquareSize;
            float z0 = team == Team.A ? gap * 0.5f : -gap * 0.5f - depth; // Over the enemy's area.
            float z1 = z0 + depth;
            Material material = Make("Terrain/TerrainDeployment");
            material.SetColor("_Team", color);
            material.SetVector("_Zone", new Vector4(x0, z0, x1, z1));
            material.SetFloat("_Square", rules.SquareSize);
            material.SetFloat(FadeId, _squares);
            _overlays.Add(material);
            Transform quad = PlaceholderArt.CreatePart("Flank Zone", _root, PlaceholderArt.Quad, material);
            quad.SetPositionAndRotation(new Vector3((x0 + x1) * 0.5f, TerrainCanvas.DecalHeight + 0.01f, (z0 + z1) * 0.5f), Quaternion.Euler(90f, 0f, 0f));
            quad.localScale = new Vector3(x1 - x0, depth, 1f);
            return quad;
        }

        private Material SquaresOver(string name, CombatRules rules, Team team, Color color)
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
            return material;
        }

        /// <summary>Runs draw with the deployment squares out of sight, for a picture of the field (D69); they come back as they were.</summary>
        public void WithoutSquares(System.Action draw)
        {
            foreach (Material overlay in _overlays) overlay.SetFloat(FadeId, 0f);
            try
            {
                draw();
            }
            finally
            {
                foreach (Material overlay in _overlays) overlay.SetFloat(FadeId, _squares);
            }
        }

        /// <summary>The flank zones' glow, on and off while pulsing, and back to the squares' fade after.</summary>
        private void UpdateFlankPulse()
        {
            if (!PulseFlankZones && !_flankPulsed) return;
            _flankPulsed = PulseFlankZones;
            float fade = PulseFlankZones ? _squares * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)) : _squares;
            foreach (Transform zone in new[] { _flankLeft, _flankRight })
                if (zone != null) zone.GetComponent<MeshRenderer>().sharedMaterial.SetFloat(FadeId, fade);
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
                _sun = light;
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
            ApplyMood(air);
        }

        /// <summary>
        /// The map's mood (D80) and light (the lighting pass): the look's own profile (BattlefieldLighting) on a global volume above the effects' bloom: its tonemapping,
        /// colour grade, white balance, split toning, vignette and bloom. Each look has its own profile, named after its Id; the outdoor maps share a nearly neutral one.
        /// </summary>
        private void ApplyMood(TerrainTheme air)
        {
            if (_mood == null)
            {
                var body = new GameObject("Battlefield Mood");
                body.transform.SetParent(transform, false);
                _mood = body.AddComponent<Volume>();
                _mood.isGlobal = true;
                _mood.priority = -5f;
            }
            _mood.sharedProfile = _lighting.ProfileFor(_map.Look != null ? _map.Look.Id : BattlefieldLighting.OutdoorId, air, BattlefieldLighting.HighQualityNow);
            Camera view = Camera.main;
            if (view != null) view.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        /// <summary>What follows the quality level, which can change at any moment: the bloom's quality, the floor's mirror and the renderer with the soft contact shadows (High only).</summary>
        private void ApplyQuality()
        {
            _qualityShown = QualitySettings.GetQualityLevel();
            bool high = BattlefieldLighting.IsHighQuality(_qualityShown);
            foreach (MeshRenderer mirror in _mirrors)
                if (mirror != null) mirror.enabled = high;
            if (_mood != null && _mood.sharedProfile != null && _map != null) BattlefieldLighting.Configure(_mood.sharedProfile, _map.Atmosphere, high);
            BattlefieldLighting.UseBattleRenderer(Camera.main);
            if (_sun != null && _map != null)
            {
                _sun.shadows = high ? LightShadows.Soft : LightShadows.None;
                _sun.shadowStrength = _map.Atmosphere.SunShadow;
            }
            if (_blobs != null) _blobs.Strength = high ? HighQualityBlobStrength : 1f;
        }

        /// <summary>How dark the blob under a unit is at High quality, where the unit also casts a real shadow: only the soft contact with the floor is left.</summary>
        public const float HighQualityBlobStrength = 0.55f;

        /// <summary>
        /// The sun's shadows reach exactly as far as the fighting floor seen from where the camera is (the shadows pass): from 150 m away, a fixed short range would
        /// leave the field unshadowed and a long one would spread the shadow map thin. The four cascades split what lies between the nearest and farthest corner of
        /// the floor (the air in front of it needs none). The pipeline asset's own values come back when the battlefield goes, so the editor's asset never keeps them.
        /// </summary>
        private void UpdateShadowRange()
        {
            if (_sun == null || _sun.shadows == LightShadows.None || _battle.Rules == null) return;
            Camera view = Camera.main;
            var asset = UniversalRenderPipeline.asset;
            if (view == null || asset == null) return;
            if (asset != _shadowAsset)
            {
                RestoreShadowAsset();
                _shadowAsset = asset;
                _assetShadowDistance = asset.shadowDistance;
                _assetCascades = asset.cascade4Split;
                _assetCascadeCount = asset.shadowCascadeCount;
            }
            CombatRules rules = _battle.Rules;
            float halfWidth = rules.ArenaWidth * 0.5f + 6f, halfLength = rules.ArenaLength * 0.5f + 6f;
            Vector3 eye = view.transform.position;
            float near = float.MaxValue, far = 0f;
            for (int corner = 0; corner < 4; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? -halfWidth : halfWidth, 0f, (corner & 2) == 0 ? -halfLength : halfLength);
                float metres = Vector3.Distance(eye, point);
                near = Mathf.Min(near, metres);
                far = Mathf.Max(far, metres);
            }
            // A camera right over the floor is nearest to the point under it, not to a corner.
            near = Mathf.Min(near, Mathf.Max(0f, eye.y - 2f));
            float range = Mathf.Clamp(far + 10f, 40f, 500f);
            float start = Mathf.Clamp01(near / range);
            asset.shadowDistance = range;
            asset.shadowCascadeCount = 4;
            asset.cascade4Split = new Vector3(Mathf.Lerp(start, 1f, 0.22f), Mathf.Lerp(start, 1f, 0.46f), Mathf.Lerp(start, 1f, 0.72f));
        }

        private void RestoreShadowAsset()
        {
            if (_shadowAsset == null) return;
            _shadowAsset.shadowDistance = _assetShadowDistance;
            _shadowAsset.cascade4Split = _assetCascades;
            _shadowAsset.shadowCascadeCount = _assetCascadeCount;
            _shadowAsset = null;
        }

        /// <summary>A polished floor's mirror of a mesh that stands around the field (TerrainMirror).</summary>
        private MeshRenderer Mirror(string name, Mesh mesh, TerrainTheme air, bool glow)
        {
            Material material = Make("Terrain/TerrainMirror");
            material.SetFloat("_Strength", air.Mirror);
            material.SetFloat("_FadeHeight", 12f);
            material.SetFloat("_GlowBoost", air.PropGlow);
            material.SetFloat("_Core", air.GlowCore);
            material.SetFloat("_IsGlow", glow ? 1f : 0f);
            // Turned upside down, the solid mesh's outsides wind the other way. The glow's halos are turned to the camera after the flip, so they keep theirs.
            material.SetFloat("_Cull", glow ? (float)CullMode.Off : (float)CullMode.Front);
            return PlaceholderArt.CreatePart(name, _root, mesh, material).GetComponent<MeshRenderer>();
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
            _flankLeft = _flankRight = null;
            _flankTeam = null;
            _mirrors.Clear();
            foreach (Material material in _materials) Destroy(material);
            _materials.Clear();
            _overlays.Clear();
            _map?.Destroy();
            _map = null;
        }

        private void OnDestroy()
        {
            RestoreShadowAsset();
            Clear();
            _lighting.Destroy();
        }
    }
}
