// UnitViewManager.cs
// Shows every unit of the battle with a health bar: the prefab from its unit data asset (tinted with
// its side's color), or a placeholder primitive if none is set. One view per simulation
// body; nothing here changes the simulation. Positions come from the simulation after each tick
// (Ticked event) and are blended between the last two ticks each frame, so movement looks smooth
// at any frame rate. Dead units disappear (UnitDied event). Health bars face the camera and show
// only when a unit is damaged or its squad is selected; a unit with an energy shield has a blue shield
// bar over its health bar (decision D35). Selected squads get a ring under each body.
// A squad can be dropped in (DropSquad): it starts high above, out of sight, and falls to its spot
// (used for newly bought squads during deployment; looks only, the simulation is unchanged).
// Units added during the battle (a Pelican's Marines) get their views when they appear, and fall
// from their carrier's flying height; squads landed by drop-pod spells fall from high above.
// Units of the GhostTeam are drawn grey: during deployment, the enemy's army as it was at the last
// battle start (decision D25). Squads for which LevelUpReady says yes get a small yellow arrow above
// them that pulses bright, dim, bright (decision D26). A side suffering a fallen tower's shockwave
// pulses with a tint over all its bodies for as long as it lasts (D68).
// A flier with a baked body drops to the ground as its death plays (the Seeder, D93). A baked body can keep time with the battle (D90): its shooting clip paced and timed so that each blow lands on one of its weapon's strikes
// (the Stalker Form's lunges, the Pure Form Ranged's thrust), or its moving and shooting clips so that it gives birth as it spawns (the Spawner).
// The simulation's 2D position (x, y) becomes the world position (x, height, y).
// Depends on: BattleController, SquadSelection, PlaceholderArt, Halobellum.Combat, UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Units;
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
        [Tooltip("Health bars tell the sides apart (D43): your units are green, the enemy's red.")]
        [SerializeField] private Color friendlyHealthColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color enemyHealthColor = new Color(0.95f, 0.25f, 0.2f);
        [SerializeField] private Color shieldColor = new Color(0.25f, 0.6f, 1f);
        [Tooltip("Mixed into the side's color for infected units, and the color of a corpse about to rise (D38).")]
        [SerializeField] private Color infectedColor = new Color(0.55f, 0.68f, 0.18f);
        [Tooltip("What a side's bodies pulse with while they suffer a fallen tower's shockwave (D68).")]
        [SerializeField] private Color shockwaveColor = new Color(0.45f, 0.92f, 1f);
        [Tooltip("Size of each tiny form in a pack, as a share of the pack's size (Infection Forms).")]
        [SerializeField] private float formScale = 0.42f;
        [Tooltip("How high a leap goes at its peak, as a share of its length.")]
        [SerializeField] private float leapHeightShare = 0.35f;

        [Header("Layout (metres)")]
        [Tooltip("Height at which air units fly.")]
        [SerializeField] private float airHeight = 13f;
        [Tooltip("Height of a carrier's middle when it has landed to drop its squad.")]
        [SerializeField] private float landedHeight = 2.2f;
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
            /// <summary>Its baked animated model and what that is playing (D41, D42), or null for a plain body.</summary>
            public BakedBodyDefinition Baked;
            public MeshRenderer BakedRenderer;
            public BakedAnimation Playing;
            public Vector4 AnimNow;
            public Color BakedTint;
            /// <summary>Height of a real model's top above its base, or 0 for a body sized from its unit's data.</summary>
            public float ModelTop;
            /// <summary>What its real model's nodes do, or null if it has none (D45).</summary>
            public ModelParts Parts;
            /// <summary>A Scarab's mouth (D98): the green light inside it and the two places its sides glow from, if its model has them.</summary>
            public Light MouthLight;
            public Transform[] Mandibles;
            /// <summary>The same for each tiny form of a pack.</summary>
            public ModelParts[] FormParts;
            /// <summary>Battle time at which a fallen body disappears.</summary>
            public float HideAt;
            /// <summary>The tiny forms of a pack, or null for a normal body.</summary>
            public GameObject[] Forms;
            public int FormsShown;
            public SimUnit Unit;
            public GameObject Root;
            public Transform Bar;
            public Transform Fill;
            public MeshRenderer FillRenderer;
            public GameObject Ring;
            public float BarWidth;
            public float ShownFraction = -1f;
            public GameObject ShieldBar;
            public Transform ShieldFill;
            /// <summary>The most shield this body has had in this battle (a bar's full width); 0 = no bar.</summary>
            public float ShieldPeak;
            public float ShownShieldFraction = -1f;
            public Vector3 PreviousPosition, CurrentPosition;
            public Quaternion PreviousRotation, CurrentRotation;
            /// <summary>Time.unscaledTime at which the drop-in started, or NaN when not dropping.</summary>
            public float DropStart = float.NaN;
            public float DropHeight;
            /// <summary>True while it shows a fallen tower's shockwave (D68).</summary>
            public bool ShockwaveShown;
            /// <summary>What of a body that isn't baked gets that tint, found the first time it is needed, and the colour each part had.</summary>
            public Renderer[] Tinted;
            public Color[] TintedFrom;
            public bool[] TintedHadBlock;
        }

        private BattleController _battle;
        private SquadSelection _selection;
        private readonly List<View> _views = new List<View>();
        private Material _fillFriendly, _fillEnemy;
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

        /// <summary>The fill of a unit's shield bar (0 to 1), or -1 if no shield bar is showing (for tests).</summary>
        public float ShieldBarFraction(SimUnit unit)
        {
            if (unit.Id < 0 || unit.Id >= _views.Count) return -1f;
            View view = _views[unit.Id];
            return view.Bar.gameObject.activeSelf && view.ShieldBar.activeSelf ? view.ShownShieldFraction : -1f;
        }

        /// <summary>World position just above the middle of a squad's living bodies (for icons over it); false if none is left.</summary>
        public bool SquadTop(int squadId, out Vector3 position)
        {
            position = Vector3.zero;
            SimSquad squad = _shown?.SquadById(squadId);
            if (squad == null) return false;
            Vector3 sum = Vector3.zero;
            float top = float.MinValue;
            int alive = 0;
            foreach (SimUnit member in squad.Members)
            {
                if (!member.IsAlive || member.Id < 0 || member.Id >= _views.Count) continue;
                Vector3 body = _views[member.Id].Root.transform.position;
                sum += body;
                top = Mathf.Max(top, body.y + BodyTop(_views[member.Id]));
                alive++;
            }
            if (alive == 0) return false;
            Vector3 centre = sum / alive;
            position = new Vector3(centre.x, top, centre.z);
            return true;
        }

        /// <summary>How far above its base the top of a body is.</summary>
        private static float BodyTop(View view)
        {
            UnitStats stats = view.Unit.Stats;
            if (view.ModelTop > 0f) return view.ModelTop;
            return PlaceholderArt.BodyHeight(stats) * (stats.IsAir ? 0.5f : 1f);
        }

        /// <summary>How far above its base the top of this body is.</summary>
        public float BodyTopOf(SimUnit unit) => unit.Id >= 0 && unit.Id < _views.Count ? BodyTop(_views[unit.Id]) : 0f;

        /// <summary>A walker's legs (the Scarab's, D84), or null. For the picture tools.</summary>
        public WalkerLegs WalkerOf(SimUnit unit) => unit.Id >= 0 && unit.Id < _views.Count ? _views[unit.Id].Parts?.Walker : null;

        /// <summary>A model's mouth glow, its nodes MouthGlow (with a Light) and Mandible_0 and _1 (RealModelBuilder, D98), if it has them.</summary>
        private static void FindMouth(View view, Transform body)
        {
            Transform left = null, right = null;
            foreach (Transform node in body.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == "MouthGlow") view.MouthLight = node.GetComponent<Light>();
                else if (node.name == "Mandible_0") left = node;
                else if (node.name == "Mandible_1") right = node;
            }
            if (left != null && right != null) view.Mandibles = new[] { left, right };
        }

        /// <summary>
        /// A Scarab's mouth (D98): the light inside it, and where its two sides glow, written into mandibles (two places). False if its model
        /// has no mouth glow.
        /// </summary>
        public bool TryMouth(SimUnit unit, out Light light, Vector3[] mandibles)
        {
            light = null;
            if (unit.Id < 0 || unit.Id >= _views.Count) return false;
            View view = _views[unit.Id];
            light = view.MouthLight;
            if (light == null) return false;
            if (view.Mandibles != null && mandibles != null && mandibles.Length >= 2)
            {
                mandibles[0] = view.Mandibles[0].position;
                mandibles[1] = view.Mandibles[1].position;
            }
            return true;
        }

        /// <summary>Where a weapon's given shot leaves a body, if its model says (D45). Otherwise the caller picks a point.</summary>
        public bool TryMuzzle(SimUnit unit, int weapon, int shot, out Vector3 position)
        {
            position = default;
            if (unit.Id < 0 || unit.Id >= _views.Count) return false;
            View view = _views[unit.Id];
            if (view.Parts != null && view.Parts.TryMuzzle(weapon, shot, out position)) return true;
            if (view.Baked == null) return false;
            if (weapon == 1 && view.Baked.hasSecondMuzzle)
            {
                position = view.BakedRenderer.transform.TransformPoint(view.Baked.secondMuzzle); // Its second weapon on another limb (the Mantis's rockets, D97).
                return true;
            }
            if (!view.Baked.hasMuzzle) return false;
            position = view.BakedRenderer.transform.TransformPoint(view.Baked.muzzle);
            return true;
        }

        /// <summary>The point of a body that shots aim at: the middle of what is drawn.</summary>
        public Vector3 AimPoint(SimUnit unit, float least)
        {
            Vector3 position = ToWorld(unit);
            if (!unit.IsAir && unit.Id >= 0 && unit.Id < _views.Count) position.y += Mathf.Max(least, BodyTop(_views[unit.Id]) * 0.5f);
            return position;
        }

        /// <summary>Where a body is drawn right now, even if it has just died.</summary>
        public Vector3 ShownPosition(SimUnit unit)
        {
            return unit.Id >= 0 && unit.Id < _views.Count ? _views[unit.Id].Root.transform.position : ToWorld(unit);
        }

        /// <summary>
        /// A dead body stays on the field a while, for DeathEffects to move and burn (M10): its root, or null if it plays its own
        /// death (a baked body). DeathEffects hides it again. Health bar and selection ring stay off.
        /// </summary>
        public Transform KeepCorpse(SimUnit unit)
        {
            if (unit.Id < 0 || unit.Id >= _views.Count) return null;
            View view = _views[unit.Id];
            if (view.Baked != null && view.Baked.Clip(BakedAnimation.Dead) != null) return null;
            view.Bar.gameObject.SetActive(false);
            view.Ring.SetActive(false);
            view.Root.SetActive(true);
            return view.Root.transform;
        }

        /// <summary>World position of a unit's base as of the latest tick (air units at flying height).</summary>
        public Vector3 WorldPosition(SimUnit unit) => ToWorld(unit);

        /// <summary>Where a body is drawn this frame (smoothed between ticks), for things that follow it on the ground; false if it has no view.</summary>
        public bool TryBodyPosition(SimUnit unit, out Vector3 position)
        {
            position = default;
            if (unit.Id < 0 || unit.Id >= _views.Count) return false;
            position = _views[unit.Id].Root.transform.position;
            return true;
        }

        private const float BodySinkSeconds = 1.6f, BodySinkDepth = 1.5f;
        /// <summary>Battle seconds a flier's baked body takes to fall from its flying height to the ground as it dies (D93).</summary>
        private const float AirFallSeconds = 1.3f;
        private BattleSimulation _shown;
        private Transform _container;
        private static readonly int BattleTimeId = Shader.PropertyToID("_BattleTime");
        private static readonly int TintId = Shader.PropertyToID("_BaseColor");
        private static readonly int AnimNowId = Shader.PropertyToID("_AnimNow");
        private static readonly int AnimOldId = Shader.PropertyToID("_AnimOld");
        private static readonly int AnimBlendId = Shader.PropertyToID("_AnimBlend");
        private MaterialPropertyBlock _block;
        private readonly List<View> _falling = new List<View>();
        /// <summary>The clock baked animations run on: battle time, so they pause and speed up with the battle.</summary>
        private float _animationTime;

        /// <summary>What a body's baked model is playing, or null if it has none (for tests).</summary>
        public BakedAnimation? PlayingAnimation(SimUnit unit)
        {
            if (unit.Id < 0 || unit.Id >= _views.Count || _views[unit.Id].Baked == null) return null;
            return _views[unit.Id].Playing;
        }

        /// <summary>
        /// What a baked body plays (first row, frames, start, pace), and the clock it plays on (for tests of clips keeping time, D90). The start
        /// and the clock are in battle seconds since the battle was shown.
        /// </summary>
        public Vector4 PlayingClip(SimUnit unit) => _views[unit.Id].AnimNow;

        public float AnimationClock => _animationTime;

        /// <summary>The colour a body is tinted with, whichever way it is drawn (for tests).</summary>
        public Color BodyColor(SimUnit unit)
        {
            View view = _views[unit.Id];
            if (view.Baked != null) return new Color(view.BakedTint.r, view.BakedTint.g, view.BakedTint.b, 1f);
            return view.Root.GetComponentInChildren<Renderer>().sharedMaterial.color;
        }

        /// <summary>How strongly a baked body is tinted (for tests).</summary>
        public float BakedTintStrength(SimUnit unit) => _views[unit.Id].BakedTint.a;

        /// <summary>The colour of a body's health bar (for tests).</summary>
        public Color HealthBarColor(SimUnit unit) => _views[unit.Id].FillRenderer.sharedMaterial.color;

        /// <summary>True if this body is still on the field (a fallen body stays a while; for tests).</summary>
        public bool IsShown(SimUnit unit) => unit.Id >= 0 && unit.Id < _views.Count && _views[unit.Id].Root.activeSelf;

        /// <summary>Starts an animation on a baked body, cross-fading from what it was playing unless told not to.</summary>
        private void Play(View view, BakedAnimation role, bool fade)
        {
            BakedClip clip = view.Baked.Clip(role);
            if (clip == null) return;
            // Bodies of a squad don't march in step: a loop starts somewhere different on each, unless it keeps time with the battle.
            float phase = clip.loops ? (view.Unit.Id * 0.37f % 1f) * clip.frames / view.Baked.bakedFps : 0f;
            Vector4 old = fade ? view.AnimNow : Vector4.zero;
            view.AnimNow = TryBeat(view, clip, out float pace, out float start, out _)
                ? new Vector4(clip.firstRow, clip.frames, start, pace)
                : new Vector4(clip.firstRow, clip.frames, _animationTime - phase, clip.loops ? 1f : 0f);
            view.Playing = role;
            _block.Clear();
            _block.SetColor(TintId, view.BakedTint);
            _block.SetVector(AnimNowId, view.AnimNow);
            _block.SetVector(AnimOldId, fade ? old : view.AnimNow);
            _block.SetVector(AnimBlendId, new Vector4(_animationTime, fade ? view.Baked.transitionSeconds : 0f, 0f, 0f));
            view.BakedRenderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// For a clip that keeps time with the battle (D90): the pace it plays at and when it started, so that its moment comes with the next
        /// strike of the body's main weapon (a shooting or close-in clip with a shot moment: the Combat Form's swipes, D92) or with its next spawn (a body with a spawn moment, in its moving
        /// and shooting clips), and how far apart those come (beat). A clip as long as several strikes lands one blow on each. False for a clip
        /// that simply loops, before the battle starts, or while the weapon is ready but holding fire.
        /// </summary>
        private bool TryBeat(View view, BakedClip clip, out float pace, out float start, out float beat)
        {
            pace = 1f;
            start = 0f;
            beat = 0f;
            BakedBodyDefinition body = view.Baked;
            SimUnit unit = view.Unit;
            if (!clip.loops || _shown == null || !_battle.IsPlaying) return false;
            float moment, untilNext;
            if (body.spawnMoment >= 0f && unit.Stats.SpawnIntervalSeconds > 0f && (clip.role == BakedAnimation.Moving || clip.role == BakedAnimation.Shooting))
            {
                moment = body.spawnMoment;
                beat = unit.Stats.SpawnIntervalSeconds;
                untilNext = unit.NextSpawnAt - _shown.ElapsedSeconds;
            }
            else if (body.shotMoment >= 0f && (clip.role == BakedAnimation.Shooting || clip.role == BakedAnimation.Melee) && unit.Weapons.Length > 0)
            {
                SimWeapon weapon = unit.Weapons[unit.MainWeaponIndex];
                if (weapon.Cooldown <= 0f) return false;
                float rate = Mathf.Max(0.05f, unit.ShockwaveFireRate);
                moment = body.shotMoment;
                beat = weapon.Stats.FireInterval / rate;
                untilNext = weapon.Cooldown / rate;
            }
            else
            {
                return false;
            }
            if (beat <= 0f) return false;
            // The battle has run on since its last tick: counted, the moment stays put between ticks.
            untilNext -= _battle.Clock.Alpha * _shown.TickSeconds;
            ClipBeat.Time(clip.frames / (float)body.bakedFps, beat, moment, _animationTime, untilNext, out pace, out start);
            return true;
        }

        /// <summary>
        /// Keeps a clip that keeps time in step (D90). Any start a whole beat apart lands on a strike just as well, so the one nearest what it
        /// plays now is taken; a little drift is mended at once, a big change (a new target, a slower reload) with a short fade.
        /// </summary>
        private void KeepTime(View view)
        {
            BakedClip clip = view.Baked.Clip(view.Playing);
            if (clip == null || !TryBeat(view, clip, out float pace, out float start, out float beat)) return;
            start = ClipBeat.Nearest(start, view.AnimNow.z, beat);
            float off = Mathf.Abs(start - view.AnimNow.z);
            if (off < BeatTolerance && Mathf.Abs(pace - view.AnimNow.w) < 0.005f) return;
            bool fade = off > BeatSnap;
            Vector4 old = view.AnimNow;
            view.AnimNow = new Vector4(clip.firstRow, clip.frames, start, pace);
            _block.Clear();
            _block.SetColor(TintId, view.BakedTint);
            _block.SetVector(AnimNowId, view.AnimNow);
            _block.SetVector(AnimOldId, fade ? old : view.AnimNow);
            _block.SetVector(AnimBlendId, new Vector4(_animationTime, fade ? view.Baked.transitionSeconds : 0f, 0f, 0f));
            view.BakedRenderer.SetPropertyBlock(_block);
        }

        /// <summary>Seconds a timed clip may be off its beat before it is mended, and beyond which it is mended with a fade (D90).</summary>
        private const float BeatTolerance = 0.03f, BeatSnap = 0.12f;

        /// <summary>
        /// What a living body should be playing right now. A body with a charge and a close-in blow (the Hunters, D87) plays the charge while
        /// its leap is a charge, and the blow while it fights with a melee weapon; without them, it moves and shoots as any other. A body that
        /// leaps through the air plays its charge clip as its leap, if it has one (the Juggernaut's pounce, D107).
        /// </summary>
        private BakedAnimation WantedAnimation(SimUnit unit, BakedBodyDefinition body)
        {
            if (!_battle.IsPlaying) return BakedAnimation.Still; // Time stands still in deployment.
            if (unit.IsLeaping && body.Clip(BakedAnimation.Charge) != null) return BakedAnimation.Charge;
            switch (unit.State)
            {
                case UnitState.Move: return BakedAnimation.Moving;
                case UnitState.Attack:
                    bool melee = unit.Weapons.Length > 0 && unit.Weapons[unit.MainWeaponIndex].Stats.DamageType == DamageType.Melee;
                    return melee && body.Clip(BakedAnimation.Melee) != null ? BakedAnimation.Melee : BakedAnimation.Shooting;
                default: return BakedAnimation.Still;
            }
        }

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _selection = GetComponent<SquadSelection>();
            _block = new MaterialPropertyBlock();
            _fillFriendly = PlaceholderArt.MaterialFor(friendlyHealthColor);
            _fillEnemy = PlaceholderArt.MaterialFor(enemyHealthColor);

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
            _falling.Clear();
            _animationTime = 0f;
            Shader.SetGlobalFloat(BattleTimeId, 0f);
            if (simulation == null) return;

            if (_arrowContainer == null) _arrowContainer = new GameObject("Level-up Arrows").transform;
            foreach (Transform arrow in _arrows.Values) Destroy(arrow.gameObject);
            _arrows.Clear();
            _container = new GameObject("Units").transform;
            foreach (SimUnit unit in simulation.Units) _views.Add(CreateView(unit, _container));
            foreach (View view in _views) if (view.Unit.NotArrived) view.Root.SetActive(false);
            simulation.Events.Ticked += OnTicked;
            simulation.Events.UnitDied += OnUnitDied;
            simulation.Events.UnitSpawned += OnUnitSpawned;
            simulation.Events.ReinforcementDropped += OnReinforcementDropped;
            simulation.Events.SquadArrived += OnSquadArrived;
            simulation.Events.SpellTriggered += OnSpellTriggered;
            simulation.Events.UnitInfected += OnUnitInfected;
            simulation.Events.UnitRose += OnUnitRose;
            OnSelectionChanged(_selection.SelectedSquadId);
        }

        private void Unsubscribe()
        {
            if (_shown == null) return;
            _shown.Events.Ticked -= OnTicked;
            _shown.Events.UnitDied -= OnUnitDied;
            _shown.Events.UnitSpawned -= OnUnitSpawned;
            _shown.Events.ReinforcementDropped -= OnReinforcementDropped;
            _shown.Events.SquadArrived -= OnSquadArrived;
            _shown.Events.SpellTriggered -= OnSpellTriggered;
            _shown.Events.UnitInfected -= OnUnitInfected;
            _shown.Events.UnitRose -= OnUnitRose;
            _shown = null;
        }

        private View CreateView(SimUnit unit, Transform container)
        {
            UnitStats stats = unit.Stats;
            var view = new View { Unit = unit };
            view.Root = new GameObject($"{stats.Name} #{unit.Id} ({unit.Team})");
            view.Root.transform.SetParent(container, false);

            // A tower is always there, so it is never a grey memory of the last battle.
            Color color = GhostTeam == unit.Team && !stats.IsTower ? ghostColor : unit.Team == Team.A ? teamAColor : teamBColor;
            if (stats.InfectedFrom != null) color = Color.Lerp(color, infectedColor, 0.55f);
            Material teamMaterial = PlaceholderArt.MaterialFor(color);
            GameObject prefab = _battle.PrefabFor(stats.InfectedFrom ?? stats);
            BakedBodyDefinition baked = _battle.BakedBodyFor(stats.InfectedFrom ?? stats);
            if (stats.IsTower)
            {
                view.ModelTop = TowerArt.Build(view.Root.transform, stats.Shockwave.Kind, color, FactionColors.OfTowers(unit.Team), stats.CollisionRadius);
            }
            else if (baked != null && baked.IsReady)
            {
                var body = new GameObject("Body");
                body.transform.SetParent(view.Root.transform, false);
                body.AddComponent<MeshFilter>().sharedMesh = baked.mesh;
                view.BakedRenderer = body.AddComponent<MeshRenderer>();
                view.BakedRenderer.sharedMaterials = baked.Materials;
                view.BakedRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                view.BakedRenderer.receiveShadows = false;
                view.Baked = baked;
                // A real model keeps its own colours (D43); only a ghost or an infected body is tinted, or a unit whose
                // data says so, because it shares its body with another unit (M10).
                float strength = GhostTeam == unit.Team ? 0.9f : stats.InfectedFrom != null ? 0.75f : 0f;
                UnitDefinition source = _battle.DefinitionFor(stats.InfectedFrom ?? stats);
                if (strength > 0f || source == null) view.BakedTint = new Color(color.r, color.g, color.b, strength);
                else view.BakedTint = source.bakedTint;
                if (source != null && source.bakedScale > 0f) body.transform.localScale = Vector3.one * source.bakedScale;
                view.ModelTop = baked.bodyHeight * body.transform.localScale.y;
                Play(view, WantedAnimation(unit, baked), false);
            }
            else if (stats.FormsPerBody > 1)
            {
                bool keepsPaint = _battle.KeepsOwnMaterials(stats.InfectedFrom ?? stats) && GhostTeam != unit.Team;
                CreateForms(view, stats, prefab, keepsPaint ? null : teamMaterial);
            }
            else if (prefab != null)
            {
                // The unit's prefab (from its data asset), tinted with the side's color.
                GameObject body = Instantiate(prefab, view.Root.transform, false);
                body.name = "Body";
                view.Parts = ModelParts.Find(view.Root.transform, body.transform, stats.IsAir, unit.Id * 0.73f);
                FindMouth(view, body.transform);
                // A real model keeps its own paint, except as a grey ghost.
                if (!_battle.KeepsOwnMaterials(stats.InfectedFrom ?? stats) || GhostTeam == unit.Team)
                {
                    foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>()) Repaint(renderer, teamMaterial);
                }
                else
                {
                    foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
                    {
                        view.ModelTop = Mathf.Max(view.ModelTop, renderer.bounds.max.y - view.Root.transform.position.y);
                        // A vehicle that rose infected keeps its model, in a sick colour (D43).
                        if (stats.InfectedFrom == null) continue;
                        _block.Clear();
                        _block.SetColor(TintId, Color.Lerp(Color.white, infectedColor, 0.7f));
                        renderer.SetPropertyBlock(_block);
                    }
                }
            }
            else
            {
                Transform body = PlaceholderArt.CreatePart("Body", view.Root.transform, PlaceholderArt.BodyMesh(stats), teamMaterial);
                body.localScale = PlaceholderArt.BodyScale(stats);
                body.localPosition = new Vector3(0f, stats.IsAir ? 0f : PlaceholderArt.BodyHeight(stats) * 0.5f, 0f);
            }

            // Health bar: a dark background quad and a colored fill quad that shrinks from the right.
            view.BarWidth = Mathf.Max(healthBarMinWidth, stats.CollisionRadius * 2f);
            float barY = BodyTop(view) + healthBarGap;
            view.Bar = new GameObject("Health Bar").transform;
            view.Bar.SetParent(view.Root.transform, false);
            view.Bar.localPosition = new Vector3(0f, barY, 0f);
            Transform back = PlaceholderArt.CreatePart("Back", view.Bar, PlaceholderArt.Quad, PlaceholderArt.MaterialFor(healthBackColor));
            back.localScale = new Vector3(view.BarWidth + 0.08f, healthBarHeight + 0.08f, 1f);
            view.Fill = PlaceholderArt.CreatePart("Fill", view.Bar, PlaceholderArt.Quad, unit.Team == Team.A ? _fillFriendly : _fillEnemy);
            view.FillRenderer = view.Fill.GetComponent<MeshRenderer>();
            view.Fill.localPosition = new Vector3(0f, 0f, -0.01f);
            view.Bar.gameObject.SetActive(false);

            // Shield bar: the same, in blue, just over the health bar.
            Transform shieldBar = new GameObject("Shield Bar").transform;
            shieldBar.SetParent(view.Bar, false);
            shieldBar.localPosition = new Vector3(0f, healthBarHeight + 0.1f, 0f);
            Transform shieldBack = PlaceholderArt.CreatePart("Back", shieldBar, PlaceholderArt.Quad, PlaceholderArt.MaterialFor(healthBackColor));
            shieldBack.localScale = new Vector3(view.BarWidth + 0.08f, healthBarHeight + 0.08f, 1f);
            view.ShieldFill = PlaceholderArt.CreatePart("Fill", shieldBar, PlaceholderArt.Quad, PlaceholderArt.MaterialFor(shieldColor));
            view.ShieldFill.localPosition = new Vector3(0f, 0f, -0.01f);
            view.ShieldBar = shieldBar.gameObject;
            view.ShieldBar.SetActive(false);

            // Selection ring: flat on the ground around the body, open in the middle so that it hides nothing that stands on it.
            Transform ring = PlaceholderArt.CreatePart("Selection Ring", view.Root.transform, PlaceholderArt.Ring, PlaceholderArt.MaterialFor(selectionColor));
            float ringSize = stats.CollisionRadius * 2.6f;
            ring.localScale = new Vector3(ringSize, 1f, ringSize);
            ring.localPosition = new Vector3(0f, stats.IsAir ? -airHeight + 0.05f : 0.05f, 0f);
            view.Ring = ring.gameObject;
            view.Ring.SetActive(false);

            view.CurrentPosition = view.PreviousPosition = ToWorld(unit);
            view.CurrentRotation = view.PreviousRotation = ToRotation(unit);
            view.Root.transform.SetPositionAndRotation(view.CurrentPosition, view.CurrentRotation);
            return view;
        }

        /// <summary>A pack shows each of its tiny forms: one in the middle, the others around it. A null material keeps a model's own.</summary>
        private void CreateForms(View view, UnitStats stats, GameObject prefab, Material material)
        {
            view.Forms = new GameObject[stats.FormsPerBody];
            view.FormParts = new ModelParts[stats.FormsPerBody];
            view.FormsShown = stats.FormsPerBody;
            for (int i = 0; i < stats.FormsPerBody; i++)
            {
                GameObject form;
                if (prefab != null)
                {
                    form = Instantiate(prefab, view.Root.transform, false);
                    if (material != null)
                    {
                        foreach (Renderer renderer in form.GetComponentsInChildren<Renderer>()) Repaint(renderer, material);
                    }
                }
                else
                {
                    Transform part = PlaceholderArt.CreatePart("Form", view.Root.transform, PlaceholderArt.BodyMesh(stats), material != null ? material : PlaceholderArt.MaterialFor(Color.white));
                    part.localScale = PlaceholderArt.BodyScale(stats);
                    part.localPosition = new Vector3(0f, stats.IsAir ? 0f : PlaceholderArt.BodyHeight(stats) * 0.5f, 0f);
                    form = part.gameObject;
                }
                form.name = $"Form {i + 1}";
                Transform t = form.transform;
                t.localScale *= formScale;
                t.localPosition *= formScale;
                if (i > 0)
                {
                    float angle = (i - 1) * Mathf.PI * 2f / (stats.FormsPerBody - 1);
                    t.localPosition += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (stats.CollisionRadius * (1f - formScale));
                }
                view.Forms[i] = form;
                view.FormParts[i] = ModelParts.Find(view.Root.transform, t, stats.IsAir, FormPhase(view.Unit.Id, i));
            }
        }

        private static float FormPhase(int id, int form) => id * 0.73f + form * 0.41f;

        /// <summary>One material over all of a part's slots (a part of a real model can have several).</summary>
        private static void Repaint(Renderer renderer, Material material)
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
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

        /// <summary>A squad that arrives late drops into its place from above the camera (D61).</summary>
        private void OnSquadArrived(int squadId)
        {
            foreach (View view in _views) if (view.Unit.SquadId == squadId) view.Root.SetActive(true);
            DropSquad(squadId, Time.unscaledTime, dropHeight);
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

        /// <summary>A body that will rise stays on the ground, turning sick green, until it does.</summary>
        private void OnUnitInfected(SimUnit corpse)
        {
            if (corpse.Id < 0 || corpse.Id >= _views.Count) return;
            View view = _views[corpse.Id];
            view.Root.SetActive(true);
            view.Bar.gameObject.SetActive(false);
            view.Ring.SetActive(false);
            if (view.Baked != null)
            {
                view.BakedTint = new Color(infectedColor.r, infectedColor.g, infectedColor.b, 0.8f);
                _block.Clear();
                view.BakedRenderer.GetPropertyBlock(_block);
                _block.SetColor(TintId, view.BakedTint);
                view.BakedRenderer.SetPropertyBlock(_block);
                return;
            }
            Material material = PlaceholderArt.MaterialFor(infectedColor);
            foreach (Renderer renderer in view.Root.GetComponentsInChildren<Renderer>()) Repaint(renderer, material);
        }

        private void OnUnitRose(SimUnit corpse, SimUnit risen)
        {
            if (corpse.Id < 0 || corpse.Id >= _views.Count) return;
            _views[corpse.Id].Root.SetActive(false);
            _falling.Remove(_views[corpse.Id]);
        }

        private void OnUnitDied(SimUnit unit, SimUnit killer)
        {
            // Unit ids are their index in the simulation's unit list.
            if (unit.Id < 0 || unit.Id >= _views.Count) return;
            View view = _views[unit.Id];
            if (view.Baked == null || view.Baked.Clip(BakedAnimation.Dead) == null)
            {
                view.Root.SetActive(false);
                return;
            }
            // It falls, stays down, then sinks away. Nothing bumps into it: the simulation only ever looks at living units.
            view.Bar.gameObject.SetActive(false);
            view.Ring.SetActive(false);
            Play(view, BakedAnimation.Dead, true);
            view.HideAt = _animationTime + view.Baked.corpseSeconds;
            _falling.Add(view);
        }

        // ------------------------------------------------------------------ a fallen tower's shockwave

        private const float ShockwavePulseSeconds = 0.8f, ShockwaveLeast = 0.3f, ShockwaveMost = 0.85f;

        /// <summary>How strongly this side's bodies are tinted right now: they pulse for as long as they suffer a shockwave. 0 if they don't.</summary>
        private float ShockwaveStrength(Team team)
        {
            if (_shown.ActiveShockwave(team) == null) return 0f;
            return Mathf.Lerp(ShockwaveLeast, ShockwaveMost, 0.5f + 0.5f * Mathf.Sin(_animationTime * Mathf.PI * 2f / ShockwavePulseSeconds));
        }

        /// <summary>True while this body shows the shockwave it suffers (for tests).</summary>
        public bool ShowsShockwave(SimUnit unit) => _views[unit.Id].ShockwaveShown;

        /// <summary>A tint over the whole body, whichever way it is drawn, so that no model needs anything of its own. A tower isn't part of the army, and doesn't suffer it.</summary>
        private void ShowShockwave(View view, float strength)
        {
            if (view.Unit.Stats.IsTower || (strength <= 0f && !view.ShockwaveShown)) return;
            view.ShockwaveShown = strength > 0f;
            if (view.Baked != null)
            {
                view.BakedRenderer.GetPropertyBlock(_block);
                _block.SetColor(TintId, strength > 0f ? new Color(shockwaveColor.r, shockwaveColor.g, shockwaveColor.b, Mathf.Max(strength, view.BakedTint.a)) : view.BakedTint);
                view.BakedRenderer.SetPropertyBlock(_block);
                return;
            }

            if (view.Tinted == null) FindTinted(view);
            // Over a real model's texture a tint can only multiply, so it goes above 1 to show on dark armour too.
            Color bright = new Color(shockwaveColor.r * 1.8f, shockwaveColor.g * 1.8f, shockwaveColor.b * 1.8f, 1f);
            for (int i = 0; i < view.Tinted.Length; i++)
            {
                Renderer renderer = view.Tinted[i];
                _block.Clear();
                if (strength > 0f) _block.SetColor(TintId, Color.LerpUnclamped(view.TintedFrom[i], bright, strength));
                else if (view.TintedHadBlock[i]) _block.SetColor(TintId, view.TintedFrom[i]);
                renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>Everything of a body that is drawn, without its bars and ring, and the colour each has now.</summary>
        private void FindTinted(View view)
        {
            var found = new List<Renderer>();
            foreach (Renderer renderer in view.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(view.Bar) || renderer.gameObject == view.Ring) continue;
                found.Add(renderer);
            }
            view.Tinted = found.ToArray();
            view.TintedFrom = new Color[found.Count];
            view.TintedHadBlock = new bool[found.Count];
            for (int i = 0; i < found.Count; i++)
            {
                Renderer renderer = found[i];
                Material material = renderer.sharedMaterial;
                view.TintedFrom[i] = material != null && material.HasProperty(TintId) ? material.GetColor(TintId) : Color.white;
                if (!renderer.HasPropertyBlock()) continue;
                renderer.GetPropertyBlock(_block);
                view.TintedFrom[i] = _block.GetColor(TintId);
                view.TintedHadBlock[i] = true;
            }
        }

        private void OnSelectionChanged(int squadId)
        {
            foreach (View view in _views) view.Ring.SetActive(squadId >= 0 && view.Unit.SquadId == squadId && view.Unit.IsAlive);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_shown == null || _battle.Clock == null) return;
            Quaternion barRotation = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            float alpha = _battle.IsPlaying ? _battle.Clock.Alpha : 1f;
            int selected = _selection.SelectedSquadId;

            _animationTime += Time.deltaTime * _battle.Speed;
            Shader.SetGlobalFloat(BattleTimeId, _animationTime);
            for (int i = _falling.Count - 1; i >= 0; i--)
            {
                float left = _falling[i].HideAt - _animationTime;
                if (left > 0f)
                {
                    // A flier (the Seeder, D93) drops from the sky as its death plays, faster and faster, and lies where it lands.
                    Vector3 at = _falling[i].CurrentPosition;
                    bool flier = _falling[i].Unit.IsAir;
                    if (flier)
                    {
                        float fallen = Mathf.Clamp01((_falling[i].Baked.corpseSeconds - left) / AirFallSeconds);
                        at.y *= 1f - fallen * fallen;
                    }
                    if (left < BodySinkSeconds) at += Vector3.down * (BodySinkDepth * (1f - left / BodySinkSeconds));
                    if (flier || left < BodySinkSeconds) _falling[i].Root.transform.position = at;
                    continue;
                }
                _falling[i].Root.SetActive(false);
                _falling.RemoveAt(i);
            }

            float shockwaveA = ShockwaveStrength(Team.A), shockwaveB = ShockwaveStrength(Team.B);
            foreach (View view in _views)
            {
                SimUnit unit = view.Unit;
                if (!unit.IsAlive) continue;

                Transform root = view.Root.transform;
                root.SetPositionAndRotation(
                    Vector3.LerpUnclamped(view.PreviousPosition, view.CurrentPosition, alpha) + Vector3.up * DropOffset(view),
                    Quaternion.SlerpUnclamped(view.PreviousRotation, view.CurrentRotation, alpha));
                if (unit.Stats.ReinforcementLandSeconds > 0f && view.Ring.activeSelf) view.Ring.transform.localPosition = new Vector3(0f, 0.05f - root.position.y, 0f); // It stays on the ground.

                if (view.Baked != null)
                {
                    BakedAnimation wanted = WantedAnimation(unit, view.Baked);
                    if (wanted != view.Playing) Play(view, wanted, true);
                    else KeepTime(view);
                }
                ShowShockwave(view, unit.Team == Team.A ? shockwaveA : shockwaveB);

                float motionDelta = _battle.IsPlaying ? Time.deltaTime * _battle.Speed : 0f;
                view.Parts?.Animate(unit, motionDelta, _animationTime);
                if (view.FormParts != null)
                {
                    foreach (ModelParts form in view.FormParts) form?.Animate(unit, motionDelta, _animationTime);
                }

                if (view.Forms != null && unit.FormsAlive != view.FormsShown)
                {
                    view.FormsShown = unit.FormsAlive;
                    for (int i = 0; i < view.Forms.Length; i++) view.Forms[i].SetActive(i < view.FormsShown);
                }

                float fraction = Mathf.Clamp01(unit.Health / unit.Stats.MaxHealth);
                view.ShieldPeak = Mathf.Max(view.ShieldPeak, Mathf.Max(unit.Stats.MaxShield, unit.Shield));
                float shieldFraction = view.ShieldPeak > 0f ? Mathf.Clamp01(unit.Shield / view.ShieldPeak) : 0f;
                bool hasShieldBar = view.ShieldPeak > 0f;
                // A pack's health shows in how many forms are left, so it only has a bar when selected.
                bool showBar = (fraction < 0.999f && view.Forms == null) || (selected >= 0 && unit.SquadId == selected) || (hasShieldBar && shieldFraction < 0.999f);
                if (view.Bar.gameObject.activeSelf != showBar) view.Bar.gameObject.SetActive(showBar);
                if (!showBar) continue;

                view.Bar.rotation = barRotation;
                if (view.ShieldBar.activeSelf != hasShieldBar) view.ShieldBar.SetActive(hasShieldBar);
                if (hasShieldBar && !Mathf.Approximately(shieldFraction, view.ShownShieldFraction))
                {
                    view.ShownShieldFraction = shieldFraction;
                    float shieldWidth = view.BarWidth * shieldFraction;
                    view.ShieldFill.localScale = new Vector3(shieldWidth, healthBarHeight, 1f);
                    view.ShieldFill.localPosition = new Vector3((shieldWidth - view.BarWidth) * 0.5f, 0f, -0.01f);
                }
                if (!Mathf.Approximately(fraction, view.ShownFraction))
                {
                    view.ShownFraction = fraction;
                    float width = view.BarWidth * fraction;
                    view.Fill.localScale = new Vector3(width, healthBarHeight, 1f);
                    view.Fill.localPosition = new Vector3((width - view.BarWidth) * 0.5f, 0f, -0.01f);
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
            IReadOnlyList<SimSquad> squads = _shown.Squads; // By index: this runs every frame, and foreach here would box an enumerator.
            for (int s = 0; s < squads.Count; s++)
            {
                SimSquad squad = squads[s];
                if (!LevelUpReady(squad.Id)) continue;
                // Above the middle of the squad's living bodies, over the tallest one.
                Vector3 sum = Vector3.zero;
                float top = float.MinValue;
                int alive = 0;
                foreach (SimUnit member in squad.Members)
                {
                    if (!member.IsAlive || member.Id < 0 || member.Id >= _views.Count) continue;
                    Vector3 position = _views[member.Id].Root.transform.position;
                    sum += position;
                    top = Mathf.Max(top, position.y + BodyTop(_views[member.Id]));
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
                float size = arrowSize; // The same for every squad, a Scarab's as an infantryman's (the owner, 2026-11-07).
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
            float height = unit.Stats.IsAir ? Mathf.Lerp(landedHeight, airHeight, unit.Altitude) : 0f;
            if (unit.IsLeaping)
            {
                float t = unit.LeapProgress;
                // A flier dives, falling faster and faster; anything else jumps in an arc.
                if (unit.IsAir) height *= 1f - t * t;
                else if (!unit.Stats.LeapCharges) height += 4f * leapHeightShare * Vec2.Distance(unit.LeapFrom, unit.LeapTo) * t * (1f - t); // A charge (the Hunters, D87) stays on the ground.
            }
            return new Vector3(unit.Position.X, height, unit.Position.Y);
        }

        private static Quaternion ToRotation(SimUnit unit)
        {
            var forward = new Vector3(unit.Facing.X, unit.IsAir && unit.IsLeaping ? -0.7f : 0f, unit.Facing.Y); // Nose down in a dive.
            return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
        }
    }
}
