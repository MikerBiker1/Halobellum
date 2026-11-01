// BakedBodyDefinition.cs
// A unit's real animated model, baked for big armies (M9, decisions D41 and D42). The top half is filled in by
// hand: the source model and which of its clips to use. The Animation Baker (Halobellum > Art > Bake Animated
// Bodies) fills in the bottom half: a mesh, a texture holding every bone of every frame, and the clip table.
// UnitViewManager plays it through Resources/Crowd/BakedCrowd.shader.
// Depends on: UnityEngine.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Units
{
    /// <summary>What a body is doing, as far as its animation goes.</summary>
    public enum BakedAnimation
    {
        /// <summary>Standing still: a single held frame (deployment, or nothing to do).</summary>
        Still = 0,
        Moving = 1,
        Shooting = 2,
        Dead = 3,
    }

    /// <summary>Where one animation sits in the baked texture.</summary>
    [Serializable]
    public sealed class BakedClip
    {
        public BakedAnimation role;
        public int firstRow;
        public int frames;
        public bool loops;
    }

    [CreateAssetMenu(menuName = "Halobellum/Baked Body", fileName = "NewBakedBody")]
    public sealed class BakedBodyDefinition : ScriptableObject
    {
        [Header("Source (set by hand)")]
        [Tooltip("The rigged FBX with its animations.")]
        public GameObject sourceModel;
        public Texture2D albedo;
        [Tooltip("Clip played while moving.")]
        public string movingClip;
        [Tooltip("Clip played while shooting.")]
        public string shootingClip;
        [Tooltip("Clip played once on death.")]
        public string deathClip;
        [Tooltip("Clip that the held standing frame is taken from (the moment its feet are closest together).")]
        public string stillClip;
        [Tooltip("Merge a plain rifle made of boxes into the right hand, aimed along the shooting pose.")]
        public bool holdsRifle;

        [Header("Playing")]
        [Tooltip("Battle seconds a cross-fade from one animation to the next takes.")]
        public float transitionSeconds = 0.2f;
        [Tooltip("Battle seconds a body stays on the ground after its death animation ends.")]
        public float corpseSeconds = 1.5f;

        [Header("Baked (filled in by the Animation Baker)")]
        public int bakedFps = 30;
        public Mesh mesh;
        public Material material;
        public List<BakedClip> clips = new List<BakedClip>();

        public bool IsReady => mesh != null && material != null && clips.Count > 0;

        public BakedClip Clip(BakedAnimation role)
        {
            foreach (BakedClip clip in clips)
            {
                if (clip.role == role) return clip;
            }
            return null;
        }
    }
}
