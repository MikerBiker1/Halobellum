// MenuCinematic.cs
// Makes the cinematic main menu move (D94): a battle of the three sides seen in a loop of camera shots, each a slow move from one place to
// another, with a fade through black between them, drawn under the menu's own screen so the buttons never flicker. In the picture: a
// Scarab striding across the field on planted feet (WalkerLegs, as in battle), gathering energy into its mouth and sweeping the human line
// with its beam, drawn as in battle (BeamDrawing, through an EffectKit of its own, D98), its mouth glowing green as it does; armies whose
// guns fire on their own (particle systems MenuCinematicBuilder made); timed blasts that flash and throw fire and smoke at changing spots;
// lightning in the storm; fires flickering; aircraft crossing; troops marching; and soldiers and Flood forms playing their baked clips
// (D41) on this component's clock. Everything runs from one clock, allocating nothing while it plays. MenuCinematicBuilder makes the scene
// and fills in the fields here; Advance can also be stepped by hand, so the builder can take pictures of any moment.
// Shadows have to reach the far side of the field, so it widens the shadow distance while the menu is open and puts it back afterwards.
// Depends on: WalkerLegs (Halobellum.Presentation), UnityEngine, URP (the shadow distance).

using System;
using System.Collections.Generic;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Halobellum.UI
{
    public sealed class MenuCinematic : MonoBehaviour
    {
        /// <summary>One camera shot: a slow move from one place to another, looking from one point to another, over its seconds.</summary>
        [Serializable]
        public sealed class Shot
        {
            public string name;
            public Vector3 from, fromLook, to, toLook;
            public float seconds = 10f;
            public float fieldOfView = 36f, fieldOfViewTo = 36f;
        }

        /// <summary>A baked figure playing one looping clip on this clock.</summary>
        [Serializable]
        public sealed class Figure
        {
            public MeshRenderer renderer;
            public float firstRow, frames, start, pace = 1f;
            [Tooltip("rgb the colour, alpha how strongly: a unit whose body is shared with another (as in battle).")]
            public Color tint = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>Something walking along a line again and again, facing the way it goes (troops charging).</summary>
        [Serializable]
        public sealed class Marcher
        {
            public Transform body;
            public Vector3 from, to;
            public float speed = 2f, offset;
            [Tooltip("Its scale; it grows from nothing at the start of its line and shrinks away at the end, so it never jumps back in sight.")]
            public Vector3 size = Vector3.one;
            [Tooltip("The ground's height at even steps along its line, so it rises and falls with it. Empty: a straight line.")]
            public float[] heights = Array.Empty<float>();
        }

        /// <summary>An aircraft crossing the sky from one point to another, again and again.</summary>
        [Serializable]
        public sealed class Flyer
        {
            public Transform body;
            public Vector3 from, to;
            public float period = 40f, start, duration = 20f, bank, bobPhase;
        }

        /// <summary>A fire's light, flickering.</summary>
        [Serializable]
        public sealed class Flicker
        {
            public Light light;
            public float intensity = 2f, amount = 0.4f, speed = 9f, phase;
        }

        /// <summary>A blast that goes off every period at the next of its spots: its particle systems move there and burst, and its light flashes.</summary>
        [Serializable]
        public sealed class Blast
        {
            public ParticleSystem[] parts = Array.Empty<ParticleSystem>();
            public int[] counts = Array.Empty<int>();
            public Light light;
            public float intensity = 40f, lightSeconds = 0.6f;
            public Vector3[] spots = Array.Empty<Vector3>();
            public float period = 5f, offset;
        }

        [Header("Camera")]
        public Camera view;
        public Shot[] shots = Array.Empty<Shot>();
        [Tooltip("Seconds of the fade through black at each cut.")]
        public float fadeSeconds = 0.9f;

        [Header("Things that move")]
        public Figure[] figures = Array.Empty<Figure>();
        public Marcher[] marchers = Array.Empty<Marcher>();
        public Flyer[] flyers = Array.Empty<Flyer>();
        public Flicker[] flickers = Array.Empty<Flicker>();
        public Blast[] blasts = Array.Empty<Blast>();
        [Tooltip("Small glow cards that always face the camera (engines).")]
        public Transform[] glows = Array.Empty<Transform>();
        [Tooltip("Hovering vehicles: each bobs and sways a little where it is.")]
        public Transform[] hovers = Array.Empty<Transform>();

        [Header("The walker")]
        [Tooltip("What moves across the field; the model is its child.")]
        public Transform walkerRoot;
        public Transform walkerBody;
        public Vector3 walkerFrom, walkerTo;

        [Header("The walker's beam")]
        public Transform beamFrom;
        [Tooltip("Its end sweeps along the ground from the one to the other while it fires.")]
        public Vector3 beamFromPoint, beamToPoint;
        public Light beamLight;
        [Tooltip("Seconds between firings, of the firing, when the first firing starts, and of the charge before each.")]
        public float beamPeriod = 13f, beamSeconds = 4.5f, beamStart = 3f, beamChargeSeconds = 2f;
        [Tooltip("The beam's width and its burning spot's radius as in battle, and how much bigger it is all drawn here, the walker being bigger.")]
        public float beamWidth = 1.6f, beamRadius = 2f, beamScale = 2.5f, beamLightIntensity = 90f;
        public Color beamColor = new Color(0.42f, 0.78f, 1f);
        [Tooltip("The walker's glowing panels at rest: this share of their material's colour, and of its glow; and the share of its glow as the beam peaks.")]
        public float panelColorShare = 0.12f, panelGlowAtRest = 0.03f, panelGlowFiring = 0.5f;

        [Header("The supercarrier's glassing beam")]
        [Tooltip("The beam's lines (a white core and a wide glow, D104), each flickering about its own width.")]
        public LineRenderer[] glassing = Array.Empty<LineRenderer>();
        public float[] glassingWidths = Array.Empty<float>();

        [Header("The storm")]
        public Light lightning;
        public float lightningIntensity = 3f;
        public float[] lightningTimes = Array.Empty<float>();
        [Tooltip("Where each stroke of lightning is, as a direction from the field; the sky lights its clouds there and draws its bolt.")]
        public Vector3[] lightningDirections = Array.Empty<Vector3>();

        [Header("Rendering")]
        public Color ambientAbove = new Color(0.36f, 0.36f, 0.5f);
        public Color ambientMiddle = new Color(0.4f, 0.3f, 0.3f);
        public Color ambientBelow = new Color(0.1f, 0.08f, 0.09f);
        public float shadowDistance = 500f;

        private static readonly int BattleTimeId = Shader.PropertyToID("_BattleTime");
        private static readonly int FlashId = Shader.PropertyToID("_MenuFlash");
        private static readonly int FlashDirectionId = Shader.PropertyToID("_MenuFlashDirection");
        private static readonly int AnimNowId = Shader.PropertyToID("_AnimNow");
        private static readonly int AnimOldId = Shader.PropertyToID("_AnimOld");
        private static readonly int AnimBlendId = Shader.PropertyToID("_AnimBlend");
        private static readonly int TintId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>One glowing material on one of the walker's renderers.</summary>
        private struct GlowingPanel
        {
            public Renderer renderer;
            public int materialIndex;
            public Color color, glow;
        }

        private EffectKit _kit;
        private float _beamEmit;
        private Vector3 _lastScorch;
        private Light _mouthLight;
        private Transform[] _mandibles;
        private readonly Vector3[] _mandiblePoints = new Vector3[2];
        private WalkerLegs _legs;
        private float _time, _lastWalkerShare;
        private float[] _blastAt;
        private UniversalRenderPipelineAsset _pipeline;
        private float _oldShadowDistance;
        private float _clockStart;
        private Texture2D _black;
        private Vector3[] _hoverRest = Array.Empty<Vector3>();
        private Quaternion[] _hoverTurn = Array.Empty<Quaternion>();
        private GlowingPanel[] _panels;
        private MaterialPropertyBlock _panelBlock;
        private float _panelStrength = -1f;

        /// <summary>Seconds of the whole loop of shots.</summary>
        public float LoopSeconds
        {
            get
            {
                float total = 0f;
                foreach (Shot shot in shots) total += shot.seconds;
                return Mathf.Max(1f, total);
            }
        }

        /// <summary>The clock: seconds since the menu opened.</summary>
        public float Clock => _time;

        /// <summary>The walker's legs (for tests), once made.</summary>
        public WalkerLegs Walker => _legs;

        /// <summary>How far the walker's panels have risen toward their firing glow, 0 at rest to 1. For tests.</summary>
        public float PanelStrength => _panelStrength;

        /// <summary>True while the walker's beam fires; how far its charge has come, 0 to 1, while it charges (0 otherwise).</summary>
        public bool BeamFiring { get; private set; }
        public float BeamCharge { get; private set; }

        /// <summary>The light inside the walker's mouth (its model's MouthGlow), or null.</summary>
        public Light MouthLight => _mouthLight;

        private void OnEnable()
        {
            RefreshLight();
            _pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_pipeline == null) return;
            _oldShadowDistance = _pipeline.shadowDistance;
            _pipeline.shadowDistance = shadowDistance;
        }

        private void OnDisable()
        {
            if (_pipeline != null) _pipeline.shadowDistance = _oldShadowDistance;
            _pipeline = null;
            Shader.SetGlobalFloat(FlashId, 0f);
        }

        private void Start()
        {
            _clockStart = Time.unscaledTime;
            Prepare();
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime - _clockStart;
            Advance(now - _time);
        }

        /// <summary>Sets the scene's ambient light from these colours: a scene made by code has no baked lighting data to get it from.</summary>
        public void RefreshLight()
        {
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambientMiddle);
            probe.AddDirectionalLight(Vector3.up, ambientAbove - ambientMiddle, 1f);
            probe.AddDirectionalLight(Vector3.down, ambientBelow - ambientMiddle, 1f);
            RenderSettings.ambientProbe = probe;
        }

        /// <summary>Makes the walker's legs and tells each figure what to play. Called by Start, or by the builder before stepping by hand.</summary>
        public void Prepare()
        {
            _time = 0f;
            _blastAt = new float[blasts.Length];
            for (int i = 0; i < _blastAt.Length; i++) _blastAt[i] = -100f;
            if (walkerRoot != null && walkerBody != null && _legs == null)
            {
                PlaceWalker(0f);
                var hips = new List<Transform>();
                var feet = new Dictionary<int, Transform>();
                foreach (Transform node in walkerBody.GetComponentsInChildren<Transform>(true))
                {
                    if (node.name.StartsWith("Leg_")) hips.Add(node);
                    else if (node.name.StartsWith("Foot_") && int.TryParse(node.name.Substring("Foot_".Length), out int number)) feet[number] = node;
                }
                if (hips.Count > 0 && feet.Count > 0) _legs = WalkerLegs.Create(walkerRoot, walkerBody, hips, feet, 0.13f);
            }
            _hoverRest = new Vector3[hovers.Length];
            _hoverTurn = new Quaternion[hovers.Length];
            for (int i = 0; i < hovers.Length; i++)
            {
                if (hovers[i] == null) continue;
                _hoverRest[i] = hovers[i].position;
                _hoverTurn[i] = hovers[i].rotation;
            }
            FindMouth();
            if (_kit == null && beamFrom != null)
            {
                _kit = new EffectKit(transform);
                _kit.TestDepth(); // So the walker's own body hides its mouth's glow when the camera is behind it (D104).
            }
            var block = new MaterialPropertyBlock();
            foreach (Figure figure in figures)
            {
                if (figure.renderer == null) continue;
                var clip = new Vector4(figure.firstRow, figure.frames, figure.start, figure.pace);
                block.Clear();
                block.SetColor(TintId, figure.tint);
                block.SetVector(AnimNowId, clip);
                block.SetVector(AnimOldId, clip);
                block.SetVector(AnimBlendId, Vector4.zero);
                figure.renderer.SetPropertyBlock(block);
            }
            ApplyCamera(0f);
        }

        /// <summary>Moves the clock on by this many seconds and shows that moment.</summary>
        public void Advance(float delta)
        {
            float before = _time;
            _time += Mathf.Max(0f, delta);
            float time = _time;
            Shader.SetGlobalFloat(BattleTimeId, time);
            ApplyCamera(time);
            foreach (Marcher marcher in marchers) March(marcher, time);
            foreach (Flyer flyer in flyers) Fly(flyer, time);
            foreach (Flicker flicker in flickers)
            {
                if (flicker.light == null) continue;
                float wobble = Mathf.PerlinNoise(time * flicker.speed, flicker.phase) - 0.5f;
                flicker.light.intensity = flicker.intensity * (1f + wobble * 2f * flicker.amount);
            }
            for (int i = 0; i < blasts.Length; i++) Detonate(i, before, time);
            for (int i = 0; i < hovers.Length && i < _hoverRest.Length; i++)
            {
                if (hovers[i] == null) continue;
                float phase = i * 1.93f;
                hovers[i].SetPositionAndRotation(_hoverRest[i] + Vector3.up * (Mathf.Sin(time * 1.1f + phase) * 0.18f),
                    _hoverTurn[i] * Quaternion.Euler(Mathf.Sin(time * 0.8f + phase) * 1.2f, 0f, Mathf.Sin(time * 0.67f + phase * 1.3f) * 1.6f));
            }
            Walk(time, delta);
            FireBeam(time, Mathf.Max(0f, delta));
            float glassingFlicker = 0.86f + 0.14f * Mathf.Sin(time * 29f) * Mathf.Sin(time * 7.3f + 1f);
            for (int i = 0; i < glassing.Length && i < glassingWidths.Length; i++)
            {
                if (glassing[i] != null) glassing[i].widthMultiplier = glassingWidths[i] * glassingFlicker;
            }
            Storm(time);
            if (view != null)
            {
                Quaternion facing = view.transform.rotation;
                foreach (Transform glow in glows)
                {
                    if (glow != null) glow.rotation = facing;
                }
            }
        }

        // ------------------------------------------------------------------ the camera

        /// <summary>Which shot is on at this moment, and how far into it (0 to 1).</summary>
        public int ShotAt(float time, out float share)
        {
            share = 0f;
            if (shots.Length == 0) return -1;
            float t = Mathf.Repeat(time, LoopSeconds);
            for (int i = 0; i < shots.Length; i++)
            {
                if (t < shots[i].seconds || i == shots.Length - 1)
                {
                    share = Mathf.Clamp01(t / Mathf.Max(0.1f, shots[i].seconds));
                    return i;
                }
                t -= shots[i].seconds;
            }
            return shots.Length - 1;
        }

        /// <summary>How dark the fade through black is at this moment: 1 at a cut, 0 through the middle of a shot.</summary>
        public float FadeAt(float time)
        {
            int index = ShotAt(time, out float share);
            if (index < 0) return 0f;
            float seconds = shots[index].seconds;
            float into = share * seconds, left = seconds - into;
            float half = fadeSeconds * 0.5f;
            return Mathf.Max(1f - Mathf.Clamp01(into / half), 1f - Mathf.Clamp01(left / half));
        }

        private void ApplyCamera(float time)
        {
            if (view == null) return;
            int index = ShotAt(time, out float share);
            if (index < 0) return;
            Shot shot = shots[index];
            float eased = share * share * (3f - 2f * share) * 0.6f + share * 0.4f; // Mostly steady, easing a little at the ends.
            Vector3 position = Vector3.Lerp(shot.from, shot.to, eased);
            Vector3 look = Vector3.Lerp(shot.fromLook, shot.toLook, eased);
            // A hand-held breath, very small.
            position += new Vector3(Mathf.Sin(time * 0.37f), Mathf.Sin(time * 0.53f + 1f) * 0.6f, Mathf.Sin(time * 0.29f + 2f)) * 0.12f;
            view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
            view.fieldOfView = Mathf.Lerp(shot.fieldOfView, shot.fieldOfViewTo, eased);
        }

        private void OnGUI()
        {
            float fade = FadeAt(_time);
            if (fade <= 0.001f) return;
            if (_black == null)
            {
                _black = new Texture2D(1, 1);
                _black.SetPixel(0, 0, Color.black);
                _black.Apply();
            }
            GUI.depth = 10; // Behind the menu's own screen, which draws at the default depth.
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);
            GUI.color = old;
        }

        private void OnDestroy()
        {
            if (_black != null) Destroy(_black);
            _kit?.Destroy();
        }

        // ------------------------------------------------------------------ what moves

        private static void March(Marcher marcher, float time)
        {
            if (marcher.body == null) return;
            Vector3 path = marcher.to - marcher.from;
            float length = Mathf.Max(0.1f, path.magnitude);
            float along = Mathf.Repeat(time * marcher.speed + marcher.offset, length);
            Vector3 position = marcher.from + path * (along / length);
            float[] heights = marcher.heights;
            if (heights != null && heights.Length > 1)
            {
                float step = along / length * (heights.Length - 1);
                int below = Mathf.Min(Mathf.FloorToInt(step), heights.Length - 2);
                position.y = Mathf.Lerp(heights[below], heights[below + 1], step - below);
            }
            marcher.body.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(path.x, 0f, path.z)));
            float ends = Mathf.Max(0.5f, length * 0.04f);
            marcher.body.localScale = marcher.size * Mathf.Clamp01(Mathf.Min(along, length - along) / ends);
        }

        private static void Fly(Flyer flyer, float time)
        {
            if (flyer.body == null) return;
            float cycle = Mathf.Repeat(time + flyer.start, Mathf.Max(1f, flyer.period));
            bool flying = cycle < flyer.duration;
            GameObject body = flyer.body.gameObject;
            if (body.activeSelf != flying) body.SetActive(flying);
            if (!flying) return;
            float progress = cycle / Mathf.Max(0.1f, flyer.duration);
            Vector3 path = flyer.to - flyer.from;
            Vector3 position = flyer.from + path * progress;
            position.y += Mathf.Sin(time * 0.9f + flyer.bobPhase) * 0.6f;
            float lean = Mathf.Sin(progress * Mathf.PI) * flyer.bank + Mathf.Sin(time * 0.6f + flyer.bobPhase) * 1.5f;
            flyer.body.SetPositionAndRotation(position, Quaternion.LookRotation(path) * Quaternion.Euler(0f, 0f, lean));
        }

        /// <summary>Sets off a blast if one of its moments falls between these two clock readings; fades its light.</summary>
        private void Detonate(int index, float before, float time)
        {
            Blast blast = blasts[index];
            if (blast.period <= 0f || blast.spots.Length == 0) return;
            int was = Mathf.FloorToInt((before - blast.offset) / blast.period);
            int now = Mathf.FloorToInt((time - blast.offset) / blast.period);
            if (now > was && time >= blast.offset)
            {
                Vector3 spot = blast.spots[((now % blast.spots.Length) + blast.spots.Length) % blast.spots.Length];
                for (int p = 0; p < blast.parts.Length; p++)
                {
                    if (blast.parts[p] == null) continue;
                    blast.parts[p].transform.position = spot;
                    blast.parts[p].Emit(p < blast.counts.Length ? blast.counts[p] : 10);
                }
                if (blast.light != null) blast.light.transform.position = spot + Vector3.up * 5f;
                _blastAt[index] = time;
            }
            if (blast.light != null)
            {
                float since = time - _blastAt[index];
                float strength = since < 0f ? 0f : Mathf.Exp(-since / Mathf.Max(0.05f, blast.lightSeconds * 0.4f)) * (since < blast.lightSeconds * 2.5f ? 1f : 0f);
                blast.light.enabled = strength > 0.01f;
                blast.light.intensity = blast.intensity * strength;
            }
        }

        // ------------------------------------------------------------------ the walker and its beam

        private void PlaceWalker(float share)
        {
            Vector3 path = walkerTo - walkerFrom;
            walkerRoot.SetPositionAndRotation(walkerFrom + path * share, Quaternion.LookRotation(new Vector3(path.x, 0f, path.z)));
        }

        private void Walk(float time, float delta)
        {
            if (walkerRoot == null) return;
            // One crossing a loop of shots: it starts again during the first fade, where nobody sees it.
            float share = Mathf.Repeat(time, LoopSeconds) / LoopSeconds;
            PlaceWalker(share);
            if (_legs == null) return;
            if (share < _lastWalkerShare) _legs.Hold(time); // Back at the start: its feet go straight to their places.
            else _legs.Animate(delta, time);
            _lastWalkerShare = share;
        }

        /// <summary>The walker's mouth glow: its model's MouthGlow light, reaching as far as the model is big, and its Mandible_ places.</summary>
        private void FindMouth()
        {
            if (walkerBody == null || _mouthLight != null) return;
            Transform left = null, right = null;
            foreach (Transform node in walkerBody.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == "MouthGlow") _mouthLight = node.GetComponent<Light>();
                else if (node.name == "Mandible_0") left = node;
                else if (node.name == "Mandible_1") right = node;
            }
            if (_mouthLight != null) _mouthLight.range *= walkerBody.lossyScale.x;
            if (left != null && right != null) _mandibles = new[] { left, right };
        }

        /// <summary>
        /// The beam, once each period: a charge (energy swirling into the walker's mouth, which glows green), then the beam itself, its end
        /// sweeping along the human line, its light on the ground there. Drawn as in battle, bigger as the walker is.
        /// </summary>
        private void FireBeam(float time, float delta)
        {
            if (beamFrom == null)
            {
                ShowPanels(0f);
                return;
            }
            float cycle = Mathf.Repeat(time - beamStart + beamChargeSeconds, Mathf.Max(1f, beamPeriod));
            bool begun = time >= beamStart - beamChargeSeconds;
            BeamCharge = begun && cycle < beamChargeSeconds ? cycle / beamChargeSeconds : 0f;
            BeamFiring = begun && cycle >= beamChargeSeconds && cycle < beamChargeSeconds + beamSeconds;
            float into = cycle - beamChargeSeconds;
            Vector3 from = beamFrom.position;
            Vector3 to = Vector3.Lerp(beamFromPoint, beamToPoint, BeamFiring ? into / beamSeconds : 0f);
            float strength = BeamFiring ? Mathf.Clamp01(into / 0.3f) * Mathf.Clamp01((beamSeconds - into) / 0.6f) : 0f;
            float flicker = 0.85f + 0.15f * Mathf.Sin(time * 47f);

            // The mouth glows green from the start of the charge, burning as the beam fires.
            float mouth = BeamFiring ? 0.9f + 0.1f * Mathf.Sin(time * 31f) : BeamCharge > 0f ? 0.25f + 0.75f * BeamCharge : 0f;
            Vector3[] mandibles = null;
            if (_mouthLight != null)
            {
                _mouthLight.enabled = mouth > 0.01f;
                _mouthLight.intensity = mouth * 9f;
            }
            if (_mandibles != null)
            {
                _mandiblePoints[0] = _mandibles[0].position;
                _mandiblePoints[1] = _mandibles[1].position;
                mandibles = _mandiblePoints;
            }

            if (_kit != null)
            {
                _kit.BeginFrame(1f, delta, delta);
                if (BeamFiring) BeamDrawing.Fire(_kit, from, to, beamRadius * beamScale, beamWidth, beamColor, beamScale, time, delta, ref _beamEmit, ref _lastScorch, mandibles);
                else if (BeamCharge > 0f) BeamDrawing.Charge(_kit, from, to, BeamCharge, beamRadius * beamScale, beamColor, beamScale, time, delta, ref _beamEmit, mandibles);
                _kit.EndFrame();
            }
            ShowPanels(BeamFiring ? strength : BeamCharge * 0.4f);
            if (beamLight != null)
            {
                beamLight.enabled = BeamFiring;
                beamLight.transform.position = to + Vector3.up * 8f;
                beamLight.intensity = beamLightIntensity * strength * flicker;
            }
        }

        /// <summary>Dims the walker's glowing panels, or raises them by this much (0 to 1) as the beam fires. Allocates nothing once they are found.</summary>
        private void ShowPanels(float strength)
        {
            if (_panels == null) FindPanels();
            if (strength == _panelStrength) return;
            _panelStrength = strength;
            float glow = Mathf.Lerp(panelGlowAtRest, panelGlowFiring, strength);
            for (int i = 0; i < _panels.Length; i++)
            {
                _panelBlock.Clear();
                _panelBlock.SetColor(TintId, _panels[i].color * panelColorShare);
                _panelBlock.SetColor(EmissionId, _panels[i].glow * glow);
                _panels[i].renderer.SetPropertyBlock(_panelBlock, _panels[i].materialIndex);
            }
        }

        private void FindPanels()
        {
            _panelBlock = new MaterialPropertyBlock();
            var found = new List<GlowingPanel>();
            if (walkerBody != null)
            {
                foreach (Renderer renderer in walkerBody.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null || !materials[i].HasProperty(EmissionId) || materials[i].GetColor(EmissionId).maxColorComponent <= 0f) continue;
                        found.Add(new GlowingPanel { renderer = renderer, materialIndex = i, color = materials[i].GetColor(TintId), glow = materials[i].GetColor(EmissionId) });
                    }
                }
            }
            _panels = found.ToArray();
        }

        private void Storm(float time)
        {
            float flash = 0f;
            float t = Mathf.Repeat(time, LoopSeconds);
            for (int i = 0; i < lightningTimes.Length; i++)
            {
                float since = t - lightningTimes[i];
                if (since < 0f || since > 0.6f) continue;
                // Two quick strokes and a flicker.
                float stroke = Mathf.Exp(-since * 14f) + 0.6f * Mathf.Exp(-Mathf.Abs(since - 0.18f) * 30f);
                if (stroke <= flash) continue;
                flash = stroke;
                if (i < lightningDirections.Length) Shader.SetGlobalVector(FlashDirectionId, lightningDirections[i].normalized);
            }
            Shader.SetGlobalFloat(FlashId, flash);
            if (lightning != null)
            {
                lightning.enabled = flash > 0.01f;
                lightning.intensity = lightningIntensity * flash;
            }
        }
    }
}
