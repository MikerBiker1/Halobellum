// UnitViewManager.cs
// Shows every unit of the battle with a health bar: the prefab from its unit data asset (tinted with
// its side's color), or a placeholder primitive if none is set. One view per simulation
// body; nothing here changes the simulation. Positions come from the simulation after each tick
// (Ticked event) and are blended between the last two ticks each frame, so movement looks smooth
// at any frame rate. Dead units disappear (UnitDied event). Health bars face the camera and show
// only when a unit is damaged or its squad is selected. Selected squads get a ring under each body.
// A squad can be dropped in (DropSquad): it starts high above, out of sight, and falls to its spot
// (used for newly bought squads during deployment; looks only, the simulation is unchanged).
// Units added during the battle (a Pelican's Marines) get their views when they appear, and fall
// from their carrier's flying height; squads landed by drop-pod spells fall from high above.
// Units of the GhostTeam are drawn grey: during deployment, the enemy's army as it was at the last
// battle start (decision D25). Squads for which LevelUpReady says yes get a small yellow arrow above
// them that pulses bright, dim, bright (decision D26).
// The simulation's 2D position (x, y) becomes the world position (x, height, y).
// Depends on: BattleController, SquadSelection, PlaceholderArt, Halobellum.Combat, UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class UnitViewManager : MonoBehaviour
    {
        [Header("Colors")]
        [SerializeField] private Color teamAColor = new Color(0.25f, 0.45f, 0.95f);
        [SerializeField] private Color teamBColor = new Color(0.9f, 0.3f, 0.22f);
        [SerializeField] private Color selectionColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color healthBackColor = new Color(0.08f, 0.08f, 0.08f);
        [SerializeField] private Color healthHighColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color healthMidColor = new Color(0.95f, 0.8f, 0.2f);
        [SerializeField] private Color healthLowColor = new Color(0.95f, 0.25f, 0.2f);

        [Header("Layout (metres)")]
        [Tooltip("Height at which air units fly.")]
        [SerializeField] private float airHeight = 8f;
        [SerializeField] private float healthBarHeight = 0.25f;
        [SerializeField] private float healthBarMinWidth = 1.2f;
        [SerializeField] private float healthBarGap = 0.6f;

        [Header("Drop-in of newly bought squads")]
        [Tooltip("Height above its spot from which a dropped squad starts falling (metres; above the camera).")]
        [SerializeField] private float dropHeight = 300f;
        [Tooltip("Seconds a dropped squad takes to land.")]
        [SerializeField] private float dropDuration = 0.6f;

        [Header("Ghosts and level-up arrows")]
        [Tooltip("Color of the enemy's last-battle army shown during deployment (decision D25).")]
        [SerializeField] private Color ghostColor = new Color(0.56f, 0.58f, 0.62f);
        [Tooltip("The arrow pulses between these two colours (values above 1 make it brighter under the scene's lighting).")]
        [SerializeField] private Color arrowBrightColor = new Color(1.5f, 1.35f, 0.25f);
        [SerializeField] private Color arrowDimColor = new Color(0.8f, 0.66f, 0.1f);
        [SerializeField] private Color arrowOutlineColor = new Color(0.08f, 0.06f, 0.01f);
        [Tooltip("Seconds for one bright-dim-bright pulse of the level-up arrow.")]
        [SerializeField] private float arrowPulseSeconds = 1.2f;
        [Tooltip("Height of the level-up arrow (metres).")]
        [SerializeField] private float arrowSize = 2.6f;
        [Tooltip("Gap between the top of the squad's tallest body and the arrow (metres).")]
        [SerializeField] private float arrowGap = 1.4f;

        private sealed class View
        {
            public SimUnit Unit;
            public GameObject Root;
            public Transform Bar;
            public Transform Fill;
            public MeshRenderer FillRenderer;
            public GameObject Ring;
            public float BarWidth;
            public float ShownFraction = -1f;
            public Vector3 PreviousPosition, CurrentPosition;
            public Quaternion PreviousRotation, CurrentRotation;
            /// <summary>Time.unscaledTime at which the drop-in started, or NaN when not dropping.</summary>
            public float DropStart = float.NaN;
            public float DropHeight;
        }

        private BattleController _battle;
        private SquadSelection _selection;
        private readonly List<View> _views = new List<View>();
        private Material _fillHigh, _fillMid, _fillLow;
        private Camera _camera;
        private Transform _arrowContainer;
        private Material _arrowMaterial;
        private Mesh _arrowMesh;
        /// <summary>Level-up arrows by simulation squad id.</summary>
        private readonly Dictionary<int, Transform> _arrows = new Dictionary<int, Transform>();

        /// <summary>Units of this side are drawn as grey ghosts (set before the battle is shown), or null.</summary>
        public Team? GhostTeam { get; set; }
        /// <summary>Which simulation squads show the level-up arrow (null = none). Asked every frame.</summary>
        public Func<int, bool> LevelUpReady { get; set; }
        /// <summary>The ghost color (for tests).</summary>
        public Color GhostColor => ghostColor;

        /// <summary>Simulation squads currently showing a level-up arrow (for tests).</summary>
        public List<int> SquadsWithArrows()
        {
            var squads = new List<int>();
            foreach (var pair in _arrows) if (pair.Value.gameObject.activeSelf) squads.Add(pair.Key);
            return squads;
        }

        public float AirHeight => airHeight;
        /// <summary>Seconds a dropped-in squad takes to land.</summary>
        public float DropDuration => dropDuration;

        /// <summary>
        /// Makes every body of this simulation squad fall from high above to its spot. startTime is the
        /// Time.unscaledTime the fall began (so a squad keeps falling if the views are rebuilt meanwhile).
        /// </summary>
        public void DropSquad(int squadId, float startTime) => DropSquad(squadId, startTime, dropHeight);

        private void DropSquad(int squadId, float startTime, float height)
        {
            foreach (View view in _views)
            {
                if (view.Unit.SquadId != squadId) continue;
                view.DropStart = startTime;
                view.DropHeight = height;
                view.Root.transform.position = view.CurrentPosition + Vector3.up * DropOffset(view);
            }
        }

        /// <summary>Height above its spot of a falling body (0 once landed). Falls like under gravity: slow, then fast.</summary>
        private float DropOffset(View view)
        {
            if (float.IsNaN(view.DropStart)) return 0f;
            float progress = dropDuration > 0f ? (Time.unscaledTime - view.DropStart) / dropDuration : 1f;
            if (progress >= 1f)
            {
                view.DropStart = float.NaN;
                return 0f;
            }
            progress = Mathf.Max(0f, progress);
            return view.DropHeight * (1f - progress * progress);
        }

        /// <summary>World position of a unit's base as of the latest tick (air units at flying height).</summary>
        public Vector3 WorldPosition(SimUnit unit) => ToWorld(unit);

        private BattleSimulation _shown;
        private Transform _container;

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _selection = GetComponent<SquadSelection>();
            _fillHigh = PlaceholderArt.MaterialFor(healthHighColor);
            _fillMid = PlaceholderArt.MaterialFor(healthMidColor);
            _fillLow = PlaceholderArt.MaterialFor(healthLowColor);

            _battle.BattleChanged += Rebuild;
            _selection.SelectionChanged += OnSelectionChanged;
            Rebuild(_battle.Simulation);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_arrowMaterial != null) Destroy(_arrowMaterial);
            if (_arrowMesh != null) Destroy(_arrowMesh);
            if (_battle != null) _battle.BattleChanged -= Rebuild;
            if (_selection != null) _selection.SelectionChanged -= OnSelectionChanged;
        }

        /// <summary>Throws away the old views and makes one view per body of the newly shown battle.</summary>
        private void Rebuild(BattleSimulation simulation)
        {
            Unsubscribe();
            _views.Clear();
            if (_container != null) Destroy(_container.gameObject);
            _shown = simulation;
            if (simulation == null) return;

            if (_arrowContainer == null) _arrowContainer = new GameObject("Level-up Arrows").transform;
            foreach (Transform arrow in _arrows.Values) Destroy(arrow.gameObject);
            _arrows.Clear();
            _container = new GameObject("Units").transform;
            foreach (SimUnit unit in simulation.Units) _views.Add(CreateView(unit, _container));
            simulation.Events.Ticked += OnTicked;
            simulation.Events.UnitDied += OnUnitDied;
            simulation.Events.UnitSpawned += OnUnitSpawned;
            simulation.Events.ReinforcementDropped += OnReinforcementDropped;
            simulation.Events.SpellTriggered += OnSpellTriggered;
            OnSelectionChanged(_selection.SelectedSquadId);
        }

        private void Unsubscribe()
        {
            if (_shown == null) return;
            _shown.Events.Ticked -= OnTicked;
            _shown.Events.UnitDied -= OnUnitDied;
            _shown.Events.UnitSpawned -= OnUnitSpawned;
            _shown.Events.ReinforcementDropped -= OnReinforcementDropped;
            _shown.Events.SpellTriggered -= OnSpellTriggered;
            _shown = null;
        }

        private View CreateView(SimUnit unit, Transform container)
        {
            UnitStats stats = unit.Stats;
            var view = new View { Unit = unit };
            view.Root = new GameObject($"{stats.Name} #{unit.Id} ({unit.Team})");
            view.Root.transform.SetParent(container, false);

            Color color = GhostTeam == unit.Team ? ghostColor : unit.Team == Team.A ? teamAColor : teamBColor;
            Material teamMaterial = PlaceholderArt.MaterialFor(color);
            GameObject prefab = _battle.PrefabFor(stats);
            if (prefab != null)
            {
                // The unit's prefab (from its data asset), tinted with the side's color.
                GameObject body = Instantiate(prefab, view.Root.transform, false);
                body.name = "Body";
                foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = teamMaterial;
            }
            else
            {
                Transform body = PlaceholderArt.CreatePart("Body", view.Root.transform, PlaceholderArt.BodyMesh(stats), teamMaterial);
                body.localScale = PlaceholderArt.BodyScale(stats);
                body.localPosition = new Vector3(0f, stats.IsAir ? 0f : PlaceholderArt.BodyHeight(stats) * 0.5f, 0f);
            }

            // Health bar: a dark background quad and a colored fill quad that shrinks from the right.
            view.BarWidth = Mathf.Max(healthBarMinWidth, stats.CollisionRadius * 2f);
            float barY = (stats.IsAir ? PlaceholderArt.BodyHeight(stats) * 0.5f : PlaceholderArt.BodyHeight(stats)) + healthBarGap;
            view.Bar = new GameObject("Health Bar").transform;
            view.Bar.SetParent(view.Root.transform, false);
            view.Bar.localPosition = new Vector3(0f, barY, 0f);
            Transform back = PlaceholderArt.CreatePart("Back", view.Bar, PlaceholderArt.Quad, PlaceholderArt.MaterialFor(healthBackColor));
            back.localScale = new Vector3(view.BarWidth + 0.08f, healthBarHeight + 0.08f, 1f);
            view.Fill = PlaceholderArt.CreatePart("Fill", view.Bar, PlaceholderArt.Quad, _fillHigh);
            view.FillRenderer = view.Fill.GetComponent<MeshRenderer>();
            view.Fill.localPosition = new Vector3(0f, 0f, -0.01f);
            view.Bar.gameObject.SetActive(false);

            // Selection ring: a thin flat cylinder under the body.
            Transform ring = PlaceholderArt.CreatePart("Selection Ring", view.Root.transform, PlaceholderArt.Cylinder, PlaceholderArt.MaterialFor(selectionColor));
            float ringSize = stats.CollisionRadius * 2.4f;
            ring.localScale = new Vector3(ringSize, 0.02f, ringSize);
            ring.localPosition = new Vector3(0f, stats.IsAir ? -airHeight + 0.03f : 0.03f, 0f);
            view.Ring = ring.gameObject;
            view.Ring.SetActive(false);

            view.CurrentPosition = view.PreviousPosition = ToWorld(unit);
            view.CurrentRotation = view.PreviousRotation = ToRotation(unit);
            view.Root.transform.SetPositionAndRotation(view.CurrentPosition, view.CurrentRotation);
            return view;
        }

        private void OnTicked(int tick)
        {
            foreach (View view in _views)
            {
                if (!view.Unit.IsAlive) continue;
                view.PreviousPosition = view.CurrentPosition;
                view.PreviousRotation = view.CurrentRotation;
                view.CurrentPosition = ToWorld(view.Unit);
                view.CurrentRotation = ToRotation(view.Unit);
            }
        }

        /// <summary>A unit added during the battle: give it a view (unit ids are their index, so views stay in step).</summary>
        private void OnUnitSpawned(SimUnit unit)
        {
            while (_views.Count <= unit.Id && _views.Count < _shown.Units.Count)
            {
                _views.Add(CreateView(_shown.Units[_views.Count], _container));
            }
        }

        private void OnReinforcementDropped(SimUnit carrier, int squadId)
        {
            if (carrier.IsAir) DropSquad(squadId, Time.unscaledTime, airHeight);
        }

        /// <summary>Drop-pod spells: the landed squads fall from high above, like newly bought squads.</summary>
        private void OnSpellTriggered(SimSpell spell)
        {
            if (spell.Stats.Effect != SpellEffect.DropSquad || spell.DroppedSquadId < 0) return;
            for (int n = 0; n < spell.Stats.DropSquads; n++) DropSquad(spell.DroppedSquadId + n, Time.unscaledTime, dropHeight);
        }

        private void OnUnitDied(SimUnit unit, SimUnit killer)
        {
            // Unit ids are their index in the simulation's unit list.
            if (unit.Id >= 0 && unit.Id < _views.Count) _views[unit.Id].Root.SetActive(false);
        }

        private void OnSelectionChanged(int squadId)
        {
            foreach (View view in _views) view.Ring.SetActive(squadId >= 0 && view.Unit.SquadId == squadId);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_shown == null || _battle.Clock == null) return;
            Quaternion barRotation = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            float alpha = _battle.IsPlaying ? _battle.Clock.Alpha : 1f;
            int selected = _selection.SelectedSquadId;

            foreach (View view in _views)
            {
                SimUnit unit = view.Unit;
                if (!unit.IsAlive) continue;

                Transform root = view.Root.transform;
                root.SetPositionAndRotation(
                    Vector3.LerpUnclamped(view.PreviousPosition, view.CurrentPosition, alpha) + Vector3.up * DropOffset(view),
                    Quaternion.SlerpUnclamped(view.PreviousRotation, view.CurrentRotation, alpha));

                float fraction = Mathf.Clamp01(unit.Health / unit.Stats.MaxHealth);
                bool showBar = fraction < 0.999f || unit.SquadId == selected;
                if (view.Bar.gameObject.activeSelf != showBar) view.Bar.gameObject.SetActive(showBar);
                if (!showBar) continue;

                view.Bar.rotation = barRotation;
                if (!Mathf.Approximately(fraction, view.ShownFraction))
                {
                    view.ShownFraction = fraction;
                    float width = view.BarWidth * fraction;
                    view.Fill.localScale = new Vector3(width, healthBarHeight, 1f);
                    view.Fill.localPosition = new Vector3((width - view.BarWidth) * 0.5f, 0f, -0.01f);
                    view.FillRenderer.sharedMaterial = fraction > 0.5f ? _fillHigh : fraction > 0.25f ? _fillMid : _fillLow;
                }
            }
            UpdateArrows(barRotation);
        }

        /// <summary>Shows a pulsing arrow above every squad that is ready to level up, hides the others.</summary>
        private void UpdateArrows(Quaternion rotation)
        {
            foreach (Transform arrow in _arrows.Values) arrow.gameObject.SetActive(false);
            if (LevelUpReady == null) return;

            float phase = arrowPulseSeconds > 0f ? Time.unscaledTime / arrowPulseSeconds : 0f;
            float bob = Mathf.Sin(phase * 2f * Mathf.PI) * 0.15f;
            foreach (SimSquad squad in _shown.Squads)
            {
                if (!LevelUpReady(squad.Id)) continue;
                // Above the middle of the squad's living bodies, over the tallest one.
                Vector3 sum = Vector3.zero;
                float top = float.MinValue, widest = 0f;
                int alive = 0;
                foreach (SimUnit member in squad.Members)
                {
                    if (!member.IsAlive || member.Id < 0 || member.Id >= _views.Count) continue;
                    Vector3 position = _views[member.Id].Root.transform.position;
                    sum += position;
                    top = Mathf.Max(top, position.y + PlaceholderArt.BodyHeight(member.Stats) * (member.Stats.IsAir ? 0.5f : 1f));
                    widest = Mathf.Max(widest, member.Stats.CollisionRadius);
                    alive++;
                }
                if (alive == 0) continue;

                if (!_arrows.TryGetValue(squad.Id, out Transform arrow))
                {
                    arrow = PlaceholderArt.CreatePart($"Level-up Arrow (squad {squad.Id})", _arrowContainer, ArrowMesh(), ArrowMaterial());
                    // A dark outline just behind it (the arrow faces the camera), so it reads over any squad.
                    Transform outline = PlaceholderArt.CreatePart("Outline", arrow, ArrowMesh(), PlaceholderArt.MaterialFor(arrowOutlineColor));
                    outline.localPosition = new Vector3(0f, 0f, 0.03f);
                    outline.localScale = Vector3.one * 1.22f;
                    _arrows[squad.Id] = arrow;
                }
                float size = arrowSize * Mathf.Max(1f, widest * 0.6f);
                Vector3 centre = sum / alive;
                arrow.gameObject.SetActive(true);
                arrow.SetPositionAndRotation(new Vector3(centre.x, top + arrowGap + size * 0.5f + bob, centre.z), rotation);
                arrow.localScale = Vector3.one * size;
            }
            // Bright, dim, bright: one full cycle per arrowPulseSeconds.
            if (_arrowMaterial != null) _arrowMaterial.color = Color.Lerp(arrowDimColor, arrowBrightColor, 0.5f + 0.5f * Mathf.Cos(phase * 2f * Mathf.PI));
        }

        private Material ArrowMaterial()
        {
            if (_arrowMaterial == null) _arrowMaterial = new Material(PlaceholderArt.MaterialFor(arrowBrightColor)) { name = "Level-up Arrow" };
            return _arrowMaterial;
        }

        /// <summary>A flat arrow pointing up (1 unit tall, centred), visible from both sides.</summary>
        private Mesh ArrowMesh()
        {
            if (_arrowMesh != null) return _arrowMesh;
            var outline = new[]
            {
                new Vector3(-0.17f, -0.5f), new Vector3(0.17f, -0.5f), new Vector3(0.17f, 0.02f),
                new Vector3(0.42f, 0.02f), new Vector3(0f, 0.5f), new Vector3(-0.42f, 0.02f), new Vector3(-0.17f, 0.02f),
            };
            int[] front = { 0, 2, 1, 0, 6, 2, 6, 4, 2, 5, 4, 6, 2, 4, 3 };
            var vertices = new List<Vector3>(outline);
            vertices.AddRange(outline);
            var triangles = new List<int>(front);
            for (int i = 0; i < front.Length; i += 3)
            {
                // The back side: same triangles, opposite winding.
                triangles.Add(front[i] + outline.Length);
                triangles.Add(front[i + 2] + outline.Length);
                triangles.Add(front[i + 1] + outline.Length);
            }
            _arrowMesh = new Mesh { name = "Level-up Arrow" };
            _arrowMesh.SetVertices(vertices);
            _arrowMesh.SetTriangles(triangles, 0);
            _arrowMesh.RecalculateNormals();
            _arrowMesh.RecalculateBounds();
            return _arrowMesh;
        }

        private Vector3 ToWorld(SimUnit unit)
        {
            return new Vector3(unit.Position.X, unit.IsAir ? airHeight : 0f, unit.Position.Y);
        }

        private static Quaternion ToRotation(SimUnit unit)
        {
            var forward = new Vector3(unit.Facing.X, 0f, unit.Facing.Y);
            return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
        }
    }
}
