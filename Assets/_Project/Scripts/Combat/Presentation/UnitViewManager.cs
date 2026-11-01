// UnitViewManager.cs
// Shows every unit of the battle with a health bar: the prefab from its unit data asset (tinted with
// its side's color), or a placeholder primitive if none is set. One view per simulation
// body; nothing here changes the simulation. Positions come from the simulation after each tick
// (Ticked event) and are blended between the last two ticks each frame, so movement looks smooth
// at any frame rate. Dead units disappear (UnitDied event). Health bars face the camera and show
// only when a unit is damaged or its squad is selected. Selected squads get a ring under each body.
// A squad can be dropped in (DropSquad): it starts high above, out of sight, and falls to its spot
// (used for newly bought squads during deployment; looks only, the simulation is unchanged).
// The simulation's 2D position (x, y) becomes the world position (x, height, y).
// Depends on: BattleController, SquadSelection, PlaceholderArt, Halobellum.Combat, UnityEngine.

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
        }

        private BattleController _battle;
        private SquadSelection _selection;
        private readonly List<View> _views = new List<View>();
        private Material _fillHigh, _fillMid, _fillLow;
        private Camera _camera;

        public float AirHeight => airHeight;
        /// <summary>Seconds a dropped-in squad takes to land.</summary>
        public float DropDuration => dropDuration;

        /// <summary>
        /// Makes every body of this simulation squad fall from high above to its spot. startTime is the
        /// Time.unscaledTime the fall began (so a squad keeps falling if the views are rebuilt meanwhile).
        /// </summary>
        public void DropSquad(int squadId, float startTime)
        {
            foreach (View view in _views)
            {
                if (view.Unit.SquadId != squadId) continue;
                view.DropStart = startTime;
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
            return dropHeight * (1f - progress * progress);
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

            _container = new GameObject("Units").transform;
            foreach (SimUnit unit in simulation.Units) _views.Add(CreateView(unit, _container));
            simulation.Events.Ticked += OnTicked;
            simulation.Events.UnitDied += OnUnitDied;
            OnSelectionChanged(_selection.SelectedSquadId);
        }

        private void Unsubscribe()
        {
            if (_shown == null) return;
            _shown.Events.Ticked -= OnTicked;
            _shown.Events.UnitDied -= OnUnitDied;
            _shown = null;
        }

        private View CreateView(SimUnit unit, Transform container)
        {
            UnitStats stats = unit.Stats;
            var view = new View { Unit = unit };
            view.Root = new GameObject($"{stats.Name} #{unit.Id} ({unit.Team})");
            view.Root.transform.SetParent(container, false);

            Color color = unit.Team == Team.A ? teamAColor : teamBColor;
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
