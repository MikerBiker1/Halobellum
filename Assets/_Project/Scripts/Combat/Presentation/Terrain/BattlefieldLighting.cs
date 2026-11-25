// BattlefieldLighting.cs
// The light and grade of each battlefield look (the lighting pass, before 1.0). Every look has a post-processing profile of its own, made in code from its theme
// (TerrainTheme), which BattlefieldView puts on its "Battlefield Mood" volume when the look changes:
//   - Tonemapping (Neutral, as the project's default profile already had), a colour grade (contrast, saturation, filter, exposure), white balance and split toning:
//     UNSC cool and clean, Covenant lavender and indigo, Flood murky and warm-sick with darker edges. All of it is baked into one colour lookup, so it costs nothing per pixel.
//   - A vignette.
//   - The bloom: a threshold above white, so only lights and effects glow, never units, the ground or the HUD (which is drawn after post-processing). The bright cores
//     of strips, seams, slits, pods and fungus go past white for it (TerrainTheme.GlowCore, PropGlow).
// What follows the quality level: on High the bloom filters at high quality and the polished floors mirror what stands around them (TerrainMirror); on Low the bloom is
// lighter and cheaper and there is no mirror. The soft contact shadows (SSAO) are a renderer feature of the battle's own renderer on High (Assets/Settings/PC_BattleRenderer.asset,
// the second renderer of PC_RPAsset, which the battle's camera picks: UseBattleRenderer). Low's pipeline asset has no such renderer, so Low has no SSAO. The menu keeps PC_Renderer.
// The outdoor maps get a profile too ("Outdoor"), neutral apart from the tonemapping and bloom. Looks only: nothing here touches the simulation.
// Depends on: TerrainTheme, URP (Volume overrides), UnityEngine.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Halobellum.Presentation
{
    public sealed class BattlefieldLighting
    {
        /// <summary>The profile's name on an outdoor map; an interior look's profile is named after its Id.</summary>
        public const string OutdoorId = "Outdoor";
        /// <summary>The bloom's threshold, in URP's gamma terms (about 1.36 in linear light). Lit units and ground stay under it.</summary>
        public const float BloomThreshold = 1.15f;
        /// <summary>The name of the quality level the settings show as Low (EffectKit, SettingsScreen).</summary>
        public const string LowQualityName = "Mobile";

        private readonly Dictionary<string, VolumeProfile> _profiles = new Dictionary<string, VolumeProfile>();

        /// <summary>True on any quality level but Low: the bloom's high-quality filtering and the floors' mirror are on.</summary>
        public static bool IsHighQuality(int level)
        {
            string[] names = QualitySettings.names;
            return level < 0 || level >= names.Length || names[level] != LowQualityName;
        }

        public static bool HighQualityNow => IsHighQuality(QualitySettings.GetQualityLevel());

        /// <summary>
        /// The renderer of the battle on the current quality level's pipeline asset: High's second renderer, PC_BattleRenderer, which is PC_Renderer with the soft contact
        /// shadows (SSAO after the opaque pass, from depth), or -1 (the default renderer) where there is none, as on Low. The menu keeps the default renderer and its look.
        /// </summary>
        public static int BattleRendererIndex()
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (asset == null) return -1;
            ReadOnlySpan<ScriptableRendererData> renderers = asset.rendererDataList;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].name == BattleRendererName) return i;
            return -1;
        }

        public const string BattleRendererName = "PC_BattleRenderer";

        /// <summary>Puts the camera on the battle's renderer for the current quality level.</summary>
        public static void UseBattleRenderer(Camera camera)
        {
            if (camera == null) return;
            int index = BattleRendererIndex();
            camera.GetUniversalAdditionalCameraData().SetRenderer(index);
            Debug.Log($"Battlefield lighting: quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, renderer {(index >= 0 ? BattleRendererName : "default")}.");
        }

        /// <summary>The profile of this look (an interior look's Id, or OutdoorId), made the first time and kept; set to this theme and quality level.</summary>
        public VolumeProfile ProfileFor(string id, TerrainTheme theme, bool high)
        {
            if (!_profiles.TryGetValue(id, out VolumeProfile profile) || profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = id;
                profile.hideFlags = HideFlags.DontSave;
                profile.Add<Tonemapping>(true);
                profile.Add<ColorAdjustments>(true);
                profile.Add<WhiteBalance>(true);
                profile.Add<SplitToning>(true);
                profile.Add<Vignette>(true);
                profile.Add<Bloom>(true);
                _profiles[id] = profile;
            }
            Configure(profile, theme, high);
            return profile;
        }

        /// <summary>Sets every override of a profile from a theme. Cheap: it only sets values, and the colour lookup is rebuilt by URP when they change.</summary>
        public static void Configure(VolumeProfile profile, TerrainTheme air, bool high)
        {
            if (profile.TryGet(out Tonemapping tone))
            {
                tone.mode.Override(TonemappingMode.Neutral);
                tone.active = true;
            }
            if (profile.TryGet(out ColorAdjustments grade))
            {
                grade.colorFilter.Override(air.ColorFilter);
                grade.contrast.Override(air.Contrast);
                grade.saturation.Override(air.Saturation);
                grade.postExposure.Override(air.Exposure);
                grade.active = air.ColorFilter != Color.white || air.Contrast != 0f || air.Saturation != 0f || air.Exposure != 0f;
            }
            if (profile.TryGet(out WhiteBalance balance))
            {
                balance.temperature.Override(air.Temperature);
                balance.tint.Override(air.Tint);
                balance.active = air.Temperature != 0f || air.Tint != 0f;
            }
            if (profile.TryGet(out SplitToning split))
            {
                split.shadows.Override(air.ShadowTone);
                split.highlights.Override(air.HighlightTone);
                split.balance.Override(air.ToneBalance);
                split.active = air.ShadowTone != Color.grey || air.HighlightTone != Color.grey;
            }
            if (profile.TryGet(out Vignette vignette))
            {
                // Always overridden, so the project's default vignette doesn't come through on a look that wants none.
                vignette.intensity.Override(air.Vignette);
                vignette.color.Override(air.VignetteColor);
                vignette.smoothness.Override(0.45f);
                vignette.active = true;
            }
            if (profile.TryGet(out Bloom bloom))
            {
                bloom.threshold.Override(BloomThreshold);
                // Low gets a lighter, cheaper bloom: quarter size, a faster filter, fewer steps.
                bloom.intensity.Override(air.BloomIntensity * (high ? 1f : 0.75f));
                bloom.scatter.Override(0.68f);
                bloom.tint.Override(air.BloomTint);
                bloom.highQualityFiltering.Override(high);
                bloom.filter.Override(high ? BloomFilterMode.Gaussian : BloomFilterMode.Dual);
                bloom.downscale.Override(high ? BloomDownscaleMode.Half : BloomDownscaleMode.Quarter);
                bloom.maxIterations.Override(high ? 6 : 4);
                bloom.active = true;
            }
        }

        public void Destroy()
        {
            foreach (VolumeProfile profile in _profiles.Values)
                if (profile != null) Object.Destroy(profile);
            _profiles.Clear();
        }
    }
}
