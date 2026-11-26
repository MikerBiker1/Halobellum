// BattlefieldLightingTests.cs
// The lighting pass: every interior look puts a post-processing profile of its own on the mood volume, made from its theme, and the outdoor maps theirs; the bloom stays
// above white; the polished floor's mirror and the soft contact shadows (SSAO) are on High only, and Low has neither; a test run can pick its quality level.
// The shadows pass: on High the sun casts hard shadows (softened in the game's shaders), from the side, and the blob under a unit is lighter; on Low there are none.
// The effects pass: the smoke, fire, flame and explosion flipbooks are in the build.
// Depends on: Halobellum.Presentation, URP, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class BattlefieldLightingTests
    {
        private static int Level(string name)
        {
            int level = System.Array.IndexOf(QualitySettings.names, name);
            Assert.GreaterOrEqual(level, 0, $"No quality level {name}.");
            return level;
        }

        /// <summary>The SSAO renderer features of a quality level's pipeline asset that are on, by renderer name.</summary>
        private static List<string> RenderersWithSsao(int level)
        {
            var found = new List<string>();
            var asset = QualitySettings.GetRenderPipelineAssetAt(level) as UniversalRenderPipelineAsset;
            Assert.IsNotNull(asset, QualitySettings.names[level] + " has no URP asset.");
            foreach (ScriptableRendererData renderer in asset.rendererDataList)
            {
                if (renderer == null) continue;
                foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                    if (feature is ScreenSpaceAmbientOcclusion && feature.isActive) found.Add(renderer.name);
            }
            return found;
        }

        [Test]
        public void LowQuality_HasNoSsao_HighHasItOnTheBattleRenderer()
        {
            CollectionAssert.IsEmpty(RenderersWithSsao(Level(BattlefieldLighting.LowQualityName)), "Low must stay cheap: no soft contact shadows.");
            CollectionAssert.Contains(RenderersWithSsao(Level("PC")), BattlefieldLighting.BattleRendererName, "High's battle renderer has them.");
            Assert.IsFalse(BattlefieldLighting.IsHighQuality(Level(BattlefieldLighting.LowQualityName)));
            Assert.IsTrue(BattlefieldLighting.IsHighQuality(Level("PC")));
        }

        [TestCase(new[] { "-terrainGallery", "x", "-quality", "Low" }, "Mobile")]
        [TestCase(new[] { "-benchmark", "-quality", "high" }, "PC")]
        [TestCase(new[] { "-benchmark", "-quality", "PC" }, "PC")]
        [TestCase(new[] { "-benchmark", "-quality", "0" }, "Mobile")]
        public void ATestRun_CanPickItsQualityLevel(string[] args, string level) =>
            Assert.AreEqual(System.Array.IndexOf(new[] { "Mobile", "PC" }, level), GameSettings.QualityFromCommandLine(args, new[] { "Mobile", "PC" }));

        [TestCase(new[] { "-benchmark" }, -1)]
        [TestCase(new[] { "-benchmark", "-quality" }, -1)]
        [TestCase(new[] { "-benchmark", "-quality", "Ultra" }, -1)]
        [TestCase(new[] { "-benchmark", "-quality", "7" }, -1)]
        public void ATestRun_KeepsItsQualityLevel_UnlessOneThatExistsIsAskedFor(string[] args, int expected) =>
            Assert.AreEqual(expected, GameSettings.QualityFromCommandLine(args, new[] { "Mobile", "PC" }));

        private static Light Sun()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return light;
            Assert.Fail("No sun.");
            return null;
        }

        [Test]
        public void TheEffectFlipbooks_AreInTheBuild()
        {
            foreach (EffectAssets.Sheet sheet in new[] { EffectAssets.SmokeSheet, EffectAssets.FireSheet, EffectAssets.FireGreySheet, EffectAssets.FlameSheet, EffectAssets.BlastSheet })
            {
                Assert.IsTrue(sheet.Animated, sheet.Texture != null ? sheet.Texture.name : "missing");
                Assert.Greater(sheet.TilesX * sheet.TilesY, 4);
            }
            Assert.AreEqual(0.5f, EffectAssets.FlameSheet.Aspect, 1e-4f, "The flame's frames are twice as tall as wide.");
        }

        [UnityTest]
        public IEnumerator EveryLook_SwitchesTheMoodToItsOwnProfile_AndLowDropsTheMirrorAndSsao()
        {
            int startLevel = QualitySettings.GetQualityLevel();
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            var field = Object.FindAnyObjectByType<BattlefieldView>();
            Assert.IsNotNull(field);
            try
            {
                QualitySettings.SetQualityLevel(Level("PC"), false);
                var seen = new HashSet<VolumeProfile>();
                foreach (TerrainLook look in TerrainLooks.All)
                {
                    field.ShowLook(look, BattleFactions.Unsc, BattleFactions.Covenant, 12345);
                    yield return null;
                    yield return null;
                    VolumeProfile profile = field.MoodProfile;
                    Assert.IsNotNull(profile, look.Id);
                    Assert.AreEqual(look.Id, profile.name, "The look's own profile is on the volume.");
                    Assert.IsTrue(seen.Add(profile), look.Id + " shares its profile with another look.");
                    TerrainTheme theme = look.Theme;
                    Assert.IsTrue(profile.TryGet(out Tonemapping tone) && tone.mode.value == TonemappingMode.Neutral, look.Id);
                    Assert.IsTrue(profile.TryGet(out WhiteBalance balance), look.Id);
                    Assert.AreEqual(theme.Temperature, balance.temperature.value, 1e-4f, look.Id);
                    Assert.IsTrue(profile.TryGet(out ColorAdjustments grade), look.Id);
                    Assert.AreEqual(theme.Saturation, grade.saturation.value, 1e-4f, look.Id);
                    Assert.IsTrue(profile.TryGet(out Vignette vignette), look.Id);
                    Assert.AreEqual(theme.Vignette, vignette.intensity.value, 1e-4f, look.Id);
                    Assert.IsTrue(profile.TryGet(out Bloom bloom), look.Id);
                    Assert.GreaterOrEqual(bloom.threshold.value, 1.1f, look.Id + ": the bloom must stay above white, so units and the ground don't glow.");
                    Assert.IsTrue(bloom.highQualityFiltering.value, look.Id + " on High.");
                    Assert.AreEqual(theme.Mirror > 0f, field.MirrorShown, look.Id + ": the mirror on High.");
                    Assert.GreaterOrEqual(BattlefieldLighting.BattleRendererIndex(), 0, "High has the battle's renderer, with SSAO.");
                    Light sun = Sun();
                    Assert.AreEqual(LightShadows.Hard, sun.shadows, look.Id + ": hard shadows on High (URP's soft ones crash on exit on D3D12; ours are softened in the shaders).");
                    Assert.AreEqual(theme.SunShadow, sun.shadowStrength, 1e-4f, look.Id);
                    Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(theme.SunAngles.y, 0f)), 55f, look.Id + ": the light comes from the side, or every shadow hides behind what casts it.");
                }
                var blobs = Object.FindAnyObjectByType<UnitBlobShadows>();
                if (blobs != null) Assert.AreEqual(BattlefieldView.HighQualityBlobStrength, blobs.Strength, 1e-4f, "Lighter blobs where units cast real shadows.");
                // The looks differ in their grade, not only in name.
                Assert.AreNotEqual(TerrainLooks.UnscInstallation.Theme.Temperature, TerrainLooks.FloodOnUnsc.Theme.Temperature);
                Assert.AreNotEqual(TerrainLooks.UnscInstallation.Theme.Saturation, TerrainLooks.CovenantWarship.Theme.Saturation);

                field.ShowLook(TerrainLooks.CovenantWarship, BattleFactions.Covenant, BattleFactions.Unsc, 12345);
                yield return null;
                Assert.IsTrue(field.MirrorShown, "The Covenant warship's polished deck mirrors on High.");
                QualitySettings.SetQualityLevel(Level(BattlefieldLighting.LowQualityName), false);
                yield return null;
                yield return null;
                Assert.IsFalse(field.MirrorShown, "No mirror on Low.");
                Assert.AreEqual(LightShadows.None, Sun().shadows, "No real shadows on Low.");
                if (blobs != null) Assert.AreEqual(1f, blobs.Strength, 1e-4f, "Full blobs on Low.");
                Assert.AreEqual(-1, BattlefieldLighting.BattleRendererIndex(), "Low's pipeline asset has no renderer with SSAO.");
                Assert.IsTrue(field.MoodProfile.TryGet(out Bloom lowBloom));
                Assert.IsFalse(lowBloom.highQualityFiltering.value, "A cheaper bloom on Low.");
                Assert.AreEqual("CovenantWarship", field.MoodProfile.name);

                field.ShowOutdoor(BattleFactions.Unsc, BattleFactions.Covenant, 12345, 0, 0);
                yield return null;
                Assert.AreEqual(BattlefieldLighting.OutdoorId, field.MoodProfile.name, "The outdoor maps have their own.");
                Assert.IsFalse(field.MirrorShown);
            }
            finally
            {
                field.Follow();
                QualitySettings.SetQualityLevel(startLevel, false);
            }
        }
    }
}
