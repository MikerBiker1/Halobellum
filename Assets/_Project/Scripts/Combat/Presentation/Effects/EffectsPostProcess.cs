// EffectsPostProcess.cs
// A bloom on the game camera (M10), so that what is brighter than white glows: effects draw above 1 on purpose, and nothing in the
// lit scene does, so units and the ground are untouched. The volume is made in code; its priority is low, so any other volume that
// sets bloom wins. Turning on the camera's post-processing is needed for it to run at all.
// Depends on: URP (UniversalAdditionalCameraData, Bloom), UnityEngine.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Halobellum.Presentation
{
    public static class EffectsPostProcess
    {
        private const string VolumeName = "Effects Bloom";

        /// <summary>Call once a scene with a battle is up. It does nothing if it has been done in this scene.</summary>
        public static void Apply(Transform parent)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            if (GameObject.Find(VolumeName) != null) return;
            var body = new GameObject(VolumeName);
            body.transform.SetParent(parent, false);
            Volume volume = body.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = -10f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = VolumeName;
            profile.hideFlags = HideFlags.DontSave;
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.68f);
            bloom.highQualityFiltering.Override(false);
            volume.sharedProfile = profile;
        }
    }
}
