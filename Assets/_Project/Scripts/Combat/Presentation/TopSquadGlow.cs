// TopSquadGlow.cs
// A soft glow on the ground under the squad of each side that has dealt the most damage this battle (asked by the owner): blue under the
// player's (MatchController.LocalTeam: Team A, also in autoplay and replays; Team B for a LAN guest), red under the enemy's. Damage is counted as the Battle stats "Dealt" column counts it
// (SquadDamageLeaders), again whenever the battle has moved on. Only the leader's own bodies glow; a leader that is wiped out stays the
// leader until another squad passes it. The glows stay through the result, the card screen and the next deployment (on the player's
// squad and on the enemy's grey ghost, D25), and go the moment the next battle starts. The Sandbox has none (it has no MatchController).
// All glows share one mesh, rewritten each frame after UnitViewManager has moved the bodies, so it is one draw call and allocates
// nothing during battle.
// Looks only: it reads the simulation, the Match and the views, and never changes them.
// Depends on: BattleController, UnitViewManager, MatchController, EffectAssets, Halobellum.Economy (Match, SquadDamageLeaders,
//             SeenSquad), Halobellum.Combat, UnityEngine.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;
using UnityEngine.Rendering;

namespace Halobellum.Presentation
{
    [DefaultExecutionOrder(50)] // After UnitViewManager has moved the bodies (as CombatEffectsView).
    public sealed class TopSquadGlow : MonoBehaviour
    {
        /// <summary>Above the terrain's glows and decals and the blob shadows, below the selection ring.</summary>
        private const float Height = -0.01f;
        private const int Capacity = 512;
        public static readonly Color Blue = new Color(0.2f, 0.55f, 1f, 0.8f);
        public static readonly Color Red = new Color(1f, 0.2f, 0.12f, 0.8f);

        private BattleController _battle;
        private UnitViewManager _views;
        private MatchController _match;
        private readonly SquadDamageLeaders _leaders = new SquadDamageLeaders();
        private Match _forMatch;
        private BattleSimulation _forBattle;
        private int _countedTick = -1;
        /// <summary>What stays through the result, the card screen and the next deployment.</summary>
        private int _bluePlacedId = -1;
        private SeenSquad _redGhost;

        private GameObject _part;
        private Mesh _mesh;
        private Material _material;
        private MeshRenderer _renderer;
        private Vector3[] _positions;
        private Vector2[] _uvs;
        private Color[] _colors;
        private int _shownLast;

        /// <summary>The simulation squad glowing blue in the shown simulation, or -1.</summary>
        public int BlueSquadId { get; private set; } = -1;
        /// <summary>The simulation squad glowing red in the shown simulation, or -1.</summary>
        public int RedSquadId { get; private set; } = -1;
        /// <summary>The player's placed squad that led the last battle (or leads this one), or -1.</summary>
        public int BluePlacedId => _bluePlacedId;
        /// <summary>The enemy's squad, as seen at the battle start, that led the last battle (or leads this one), or null.</summary>
        public SeenSquad RedGhost => _redGhost;
        /// <summary>Glows drawn last frame (for tests).</summary>
        public int Shown => _shownLast;

        private void Awake()
        {
            _battle = GetComponent<BattleController>();
            _views = GetComponent<UnitViewManager>();
            _match = GetComponent<MatchController>();

            _positions = new Vector3[Capacity * 4];
            _uvs = new Vector2[Capacity * 4];
            _colors = new Color[Capacity * 4];
            var triangles = new int[Capacity * 6];
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                _uvs[v] = new Vector2(0f, 0f);
                _uvs[v + 1] = new Vector2(0f, 1f);
                _uvs[v + 2] = new Vector2(1f, 1f);
                _uvs[v + 3] = new Vector2(1f, 0f);
            }
            _mesh = new Mesh { name = "Squad Glow" };
            _mesh.MarkDynamic();
            _mesh.vertices = _positions;
            _mesh.SetUVs(0, _uvs);
            _mesh.colors = _colors;
            _mesh.triangles = triangles;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 100f, 4000f));

            // After the scorch marks and the effects' ground glows (2960-2985), so a burn mark does not swallow it; before the smoke (3000).
            _material = EffectAssets.ParticleMaterial("Squad Glow", EffectAssets.Soft, 1.4f, 0.25f, 0f, 2990);

            // Not under "Units": the scene tests count that container's children.
            _part = new GameObject("Squad Glow");
            _part.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _part.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (_mesh == null || _match == null || _battle == null) return;
            Match m = _match.Match;
            BattleSimulation sim = _battle.Simulation;
            if (m != _forMatch)
            {
                _forMatch = m; // A new match, the tutorial, "New match".
                Forget();
            }
            BlueSquadId = RedSquadId = -1;
            if (m != null && sim != null)
            {
                if (m.CurrentBattle != null && sim == m.CurrentBattle) // The battle, the round's ending, the result, the match's end.
                {
                    if (sim != _forBattle)
                    {
                        Forget(); // A new combat starts: last battle's glows go.
                        _forBattle = sim;
                    }
                    if (sim.Tick != _countedTick)
                    {
                        _countedTick = sim.Tick;
                        _leaders.Count(sim, m.Player.Squads.Count + m.Enemy.Squads.Count);
                        Remember(m);
                    }
                    bool localA = _match.LocalTeam == Team.A;
                    int blue = localA ? _leaders.TopA : _leaders.TopB, red = localA ? _leaders.TopB : _leaders.TopA;
                    BlueSquadId = blue >= 0 && blue < sim.Squads.Count ? sim.Squads[blue].Id : -1;
                    RedSquadId = red >= 0 && red < sim.Squads.Count ? sim.Squads[red].Id : -1;
                }
                else if (m.Phase == MatchPhase.Deployment || m.Phase == MatchPhase.Cards) // The preview: last battle's leaders.
                {
                    BlueSquadId = _match.PreviewSquadOfPlaced(_bluePlacedId);
                    RedSquadId = _match.PreviewSquadOfGhost(_redGhost);
                }
            }
            Draw(sim);
        }

        /// <summary>Who leads, by what lasts past the battle: the local side's placed squad and the other side's squad as seen at the battle start.</summary>
        private void Remember(Match m)
        {
            // Simulation squad i is placed squad i, Team A's first; SeenArmy(side) was copied from that side's squads, in order, at this battle's start.
            int squadsA = m.Player.Squads.Count;
            Team local = _match.LocalTeam;
            int own = local == Team.A ? _leaders.TopA : _leaders.TopB - squadsA;
            int other = local == Team.A ? _leaders.TopB - squadsA : _leaders.TopA;
            bool ownLeads = local == Team.A ? _leaders.TopA >= 0 : _leaders.TopB >= 0;
            bool otherLeads = local == Team.A ? _leaders.TopB >= 0 : _leaders.TopA >= 0;
            List<PlacedSquad> mine = m.Get(local).Squads;
            _bluePlacedId = ownLeads && own >= 0 && own < mine.Count ? mine[own].Id : -1;
            IReadOnlyList<SeenSquad> seen = m.SeenArmy(local.Opponent());
            _redGhost = otherLeads && other >= 0 && other < seen.Count ? seen[other] : null;
        }

        private void Forget()
        {
            _leaders.Reset();
            _forBattle = null;
            _countedTick = -1;
            _bluePlacedId = -1;
            _redGhost = null;
        }

        private void Draw(BattleSimulation sim)
        {
            int shown = 0;
            if (sim != null && (BlueSquadId >= 0 || RedSquadId >= 0))
            {
                IReadOnlyList<SimUnit> units = sim.Units;
                for (int i = 0; i < units.Count && shown < Capacity; i++)
                {
                    SimUnit unit = units[i];
                    // Risen bodies have no squad (-1), so a body raised for the other side never picks up a glow.
                    if (!unit.IsAlive || unit.SquadId < 0 || (unit.SquadId != BlueSquadId && unit.SquadId != RedSquadId)) continue;
                    if (!_views.IsShown(unit) || !_views.TryBodyPosition(unit, out Vector3 at)) continue;
                    // Always on the ground: straight under a flyer, under where a squad dropping in or being moved is drawn.
                    float radius = Mathf.Max(0.6f, unit.Radius) * 2.4f;
                    Color colour = unit.SquadId == BlueSquadId ? Blue : Red;
                    int v = shown * 4;
                    float x0 = at.x - radius, x1 = at.x + radius, z0 = at.z - radius, z1 = at.z + radius;
                    _positions[v] = new Vector3(x0, Height, z0);
                    _positions[v + 1] = new Vector3(x0, Height, z1);
                    _positions[v + 2] = new Vector3(x1, Height, z1);
                    _positions[v + 3] = new Vector3(x1, Height, z0);
                    _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = colour;
                    shown++;
                }
            }
            if (shown == 0 && _shownLast == 0) return; // Nothing drawn, nothing to fold away.
            // Fold away the glows no longer used.
            for (int i = shown; i < _shownLast; i++)
            {
                int v = i * 4;
                _positions[v] = _positions[v + 1] = _positions[v + 2] = _positions[v + 3] = Vector3.zero;
            }
            _shownLast = shown;
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
            _mesh.SetVertices(_positions, 0, _positions.Length, flags);
            _mesh.SetColors(_colors, 0, _colors.Length, flags);
            _renderer.enabled = shown > 0;
        }

        private void OnDestroy()
        {
            if (_part != null) Destroy(_part);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
