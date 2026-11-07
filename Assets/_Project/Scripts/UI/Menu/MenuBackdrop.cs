// MenuBackdrop.cs
// Makes the main menu's picture move (M10): the camera's slow drift, Pelicans flying past, the Scarab's beam, (no figures since D81) baked figures. idle
// animation. The Scarab itself stands still, planted on all four legs: MainMenuSceneBuilder puts them on the ground and nothing here moves
// them. All of it runs from one clock with no objects made and nothing allocated while it plays. MainMenuSceneBuilder makes the scene and
// fills in the fields here. ApplyAt shows any moment, so the builder can take a preview picture.
// Shadows have to reach the far-off Scarab here, so it widens the shadow distance while the menu is open and puts it back afterwards.
// The Scarab's glowing panels are made for the battle's bright day and would burn flat white against this dusk, so here they stay dim
// and rise only as the beam fires. Only these renderers change (a property block): the battle's Scarab shares the materials.
// Depends on: UnityEngine, URP (the shadow distance).

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Halobellum.UI
{
    public sealed class MenuBackdrop : MonoBehaviour
    {
        /// <summary>An aircraft crossing the sky from one point to another, again and again.</summary>
        [Serializable]
        public sealed class Flyer
        {
            public Transform body;
            public Vector3 from, to;
            [Tooltip("Seconds between the starts of two passes.")]
            public float period = 60f;
            [Tooltip("Seconds into the clock of the first pass.")]
            public float start;
            [Tooltip("Seconds one pass takes.")]
            public float duration = 20f;
            [Tooltip("Degrees it leans into the middle of its path.")]
            public float bank;
            public float bobPhase;
        }

        /// <summary>A baked figure (D41), playing one looping clip on the shader's own clock.</summary>
        [Serializable]
        public sealed class Figure
        {
            public MeshRenderer renderer;
            public float firstRow, frames, startTime;
            public Color tint;
        }

        [Header("Camera")]
        public Camera view;
        public Vector3 cameraPosition;
        public Vector3 lookAt;
        [Tooltip("How far the camera wanders each way, in metres.")]
        public Vector3 cameraDrift = new Vector3(4f, 0.6f, 3f);
        [Tooltip("Seconds of the slowest part of its drift.")]
        public float driftSeconds = 70f;

        [Header("Things that move")]
        public Flyer[] flyers = Array.Empty<Flyer>();
        public Figure[] figures = Array.Empty<Figure>();
        [Tooltip("Small glow cards that always face the camera (engines).")]
        public Transform[] glows = Array.Empty<Transform>();

        [Header("The walker")]
        [Tooltip("The Scarab, standing still. Only its beam and its panels change.")]
        public Transform walker;

        [Header("The walker's beam")]
        public LineRenderer beam;
        public Transform beamFrom;
        public Vector3 beamTo;
        public Light beamLight;
        public float beamPeriod = 26f;
        public float beamSeconds = 4f;
        public float beamStart = 8f;
        public float beamWidth = 2.4f;
        public float beamLightIntensity = 60f;
        [Tooltip("The walker's glowing panels at rest: this share of their material's colour.")]
        public float panelColorShare = 0.12f;
        [Tooltip("The share of their material's glow at rest.")]
        public float panelGlowAtRest = 0.03f;
        [Tooltip("The share of their material's glow at the height of the beam.")]
        public float panelGlowFiring = 0.5f;

        [Header("Rendering")]
        public Color ambientAbove = new Color(0.5f, 0.47f, 0.72f);
        public Color ambientMiddle = new Color(0.62f, 0.42f, 0.38f);
        public Color ambientBelow = new Color(0.16f, 0.13f, 0.14f);
        public float shadowDistance = 420f;

        private static readonly int BattleTimeId = Shader.PropertyToID("_BattleTime");
        private static readonly int TintId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int AnimNowId = Shader.PropertyToID("_AnimNow");
        private static readonly int AnimOldId = Shader.PropertyToID("_AnimOld");
        private static readonly int AnimBlendId = Shader.PropertyToID("_AnimBlend");

        /// <summary>One glowing material on one of the walker's renderers.</summary>
        private struct GlowingPanel
        {
            public Renderer renderer;
            public int materialIndex;
            public Color color, glow;
        }

        private readonly Vector3[] _beamPoints = new Vector3[2];
        private GlowingPanel[] _panels;
        private MaterialPropertyBlock _panelBlock;
        private float _panelStrength = -1f;
        private UniversalRenderPipelineAsset _pipeline;
        private float _oldShadowDistance;
        private float _clockStart;

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
        }

        private void Start()
        {
            _clockStart = Time.unscaledTime;
            SetUpFigures();
        }

        private void LateUpdate() => ApplyAt(Time.unscaledTime - _clockStart);

        /// <summary>Sets the scene's ambient light from these colours: a scene made by code has no baked lighting data to get it from.</summary>
        public void RefreshLight()
        {
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambientMiddle);
            probe.AddDirectionalLight(Vector3.up, ambientAbove - ambientMiddle, 1f);
            probe.AddDirectionalLight(Vector3.down, ambientBelow - ambientMiddle, 1f);
            RenderSettings.ambientProbe = probe;
        }

        /// <summary>Tells each baked figure what to play. The clip itself runs on the shader's clock.</summary>
        public void SetUpFigures()
        {
            var block = new MaterialPropertyBlock();
            foreach (Figure figure in figures)
            {
                if (figure.renderer == null) continue;
                var clip = new Vector4(figure.firstRow, figure.frames, figure.startTime, 1f);
                block.Clear();
                block.SetColor(TintId, figure.tint);
                block.SetVector(AnimNowId, clip);
                block.SetVector(AnimOldId, clip);
                block.SetVector(AnimBlendId, Vector4.zero);
                figure.renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>Shows the picture as it is this many seconds after the menu opened.</summary>
        public void ApplyAt(float time)
        {
            Shader.SetGlobalFloat(BattleTimeId, time);
            MoveCamera(time);
            foreach (Flyer flyer in flyers) Fly(flyer, time);
            FireBeam(time);
            if (view != null)
            {
                Quaternion facing = view.transform.rotation;
                foreach (Transform glow in glows)
                {
                    if (glow != null) glow.rotation = facing;
                }
            }
        }

        private void MoveCamera(float time)
        {
            if (view == null) return;
            float slow = time * Mathf.PI * 2f / Mathf.Max(1f, driftSeconds);
            var drift = new Vector3(
                Mathf.Sin(slow) * cameraDrift.x,
                Mathf.Sin(slow * 0.73f + 1.1f) * cameraDrift.y,
                Mathf.Sin(slow * 0.51f + 2.3f) * cameraDrift.z);
            Vector3 position = cameraPosition + drift;
            // The look-at point moves a little less than the camera, so near things slide across far ones.
            Vector3 target = lookAt + drift * 0.35f + Vector3.up * (Mathf.Sin(slow * 1.3f) * 0.5f);
            view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
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
            position.y += Mathf.Sin(time * 0.9f + flyer.bobPhase) * 0.5f;
            float lean = Mathf.Sin(progress * Mathf.PI) * flyer.bank + Mathf.Sin(time * 0.6f + flyer.bobPhase) * 1.5f;
            flyer.body.SetPositionAndRotation(position, Quaternion.LookRotation(path) * Quaternion.Euler(0f, 0f, lean));
        }

        /// <summary>How far the walker's panels have risen toward their firing glow, 0 at rest to 1. For tests.</summary>
        public float PanelStrength => _panelStrength;

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
            if (walker != null)
            {
                foreach (Renderer renderer in walker.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null || materials[i].GetColor(EmissionId).maxColorComponent <= 0f) continue;
                        found.Add(new GlowingPanel { renderer = renderer, materialIndex = i, color = materials[i].GetColor(TintId), glow = materials[i].GetColor(EmissionId) });
                    }
                }
            }
            _panels = found.ToArray();
        }

        private void FireBeam(float time)
        {
            if (beam == null || beamFrom == null)
            {
                ShowPanels(0f);
                return;
            }
            float cycle = Mathf.Repeat(time - beamStart, Mathf.Max(1f, beamPeriod));
            bool firing = time >= beamStart && cycle < beamSeconds;
            if (beam.enabled != firing)
            {
                beam.enabled = firing;
                if (beamLight != null) beamLight.enabled = firing;
            }
            if (!firing)
            {
                ShowPanels(0f);
                return;
            }

            // Up quickly, a flicker while it burns, and down again; its end sweeps along the ground.
            float strength = Mathf.Clamp01(cycle / 0.25f) * Mathf.Clamp01((beamSeconds - cycle) / 0.5f);
            float flicker = 0.85f + 0.15f * Mathf.Sin(time * 47f);
            float width = beamWidth * strength * flicker;
            beam.startWidth = width;
            beam.endWidth = width * 1.5f;
            _beamPoints[0] = beamFrom.position;
            _beamPoints[1] = beamTo + new Vector3((cycle / beamSeconds - 0.5f) * 36f, 0f, 0f);
            beam.SetPositions(_beamPoints);
            ShowPanels(strength);
            if (beamLight != null)
            {
                beamLight.transform.position = _beamPoints[1] + Vector3.up * 6f;
                beamLight.intensity = beamLightIntensity * strength * flicker;
            }
        }
    }
}
