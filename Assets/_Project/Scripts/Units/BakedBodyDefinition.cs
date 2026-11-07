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
        /// <summary>A blow with its close-in weapon (the Hunters' shield bash, D87), for a body that has one.</summary>
        Melee = 4,
        /// <summary>A charge along the ground at its target (the Hunters' leap, D87), for a body that has one.</summary>
        Charge = 5,
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
        [Tooltip("Folder in Art/Imported/Models holding its FBX and texture, if not named after this asset. Two units can share a body.")]
        public string modelFolder;
        [Tooltip("The rigged FBX with its animations.")]
        public GameObject sourceModel;
        public Texture2D albedo;
        [Tooltip("Which PNG in the model's folder is its albedo, when bodies share a model in different colours. Empty for the first one.")]
        public string albedoFile;
        [Tooltip("Clip played while moving.")]
        public string movingClip;
        [Tooltip("Clip played while shooting.")]
        public string shootingClip;
        [Tooltip("Clip played once on death.")]
        public string deathClip;
        [Tooltip("Clip that the held standing frame is taken from (the moment its feet are closest together).")]
        public string stillClip;
        [Tooltip("Clip played while it strikes with a close-in weapon (a melee one, used one at a time with its gun: the Hunters' shield bash). Empty: it shoots.")]
        public string meleeClip;
        [Tooltip("Clip played while its leap is a charge along the ground (the Hunters). Empty: it moves.")]
        public string chargeClip;
        [Tooltip("Folder in Art/Imported/Models of a prepared weapon (Tools/Art), merged into the right hand and aimed along the shooting pose. Empty for none.")]
        public string weaponName;
        [Tooltip("Length of the weapon in the game, in metres.")]
        public float weaponLength = 0.8f;
        [Tooltip("Hold it in the hand reaching forward instead of the one at the chest, where a small weapon would be hidden by the arm.")]
        public bool weaponInForwardHand;
        [Tooltip("Hold it in the right hand, in line with the forearm, like a blade.")]
        public bool weaponAlongForearm;
        [Tooltip("With no weapon of its own: shots start from the right hand, where the model has its gun.")]
        public bool muzzleInRightHand;
        [Tooltip("With no weapon of its own: shots start from the point of the body furthest forward in the shooting pose, the end of a gun that is part of its model.")]
        public bool muzzleAtFront;
        [Tooltip("Folder in Art/Imported/Models of a prepared shield (Tools/Art), fixed to the left hand, facing forward in the shooting pose, and drawn see-through. Empty for none.")]
        public string shieldName;
        [Tooltip("How wide the shield is in the game, in metres.")]
        public float shieldDiameter = 1.1f;
        [Tooltip("Where the middle of the shield sits from the left hand, in the body's own axes, in the shooting pose.")]
        public Vector3 shieldOffset;

        [Header("Playing")]
        [Tooltip("Battle seconds a cross-fade from one animation to the next takes.")]
        public float transitionSeconds = 0.2f;
        [Tooltip("Battle seconds a body stays on the field from the moment it dies, with its fall.")]
        public float corpseSeconds = 14f;

        [Header("Baked (filled in by the Animation Baker)")]
        public int bakedFps = 30;
        public Mesh mesh;
        public Material material;
        [Tooltip("What the see-through part of the mesh is drawn with: its second sub-mesh. Null if it has none.")]
        public Material glassMaterial;
        public List<BakedClip> clips = new List<BakedClip>();
        [Tooltip("Where shots start: the tip of the weapon in the shooting pose, in the body's own space.")]
        public Vector3 muzzle;
        public bool hasMuzzle;
        [Tooltip("How tall the body is at its rest pose, in metres, before the unit's scale.")]
        public float bodyHeight;

        public bool IsReady => mesh != null && material != null && clips.Count > 0;

        /// <summary>The materials a renderer of this body's mesh needs.</summary>
        public Material[] Materials => glassMaterial != null ? new[] { material, glassMaterial } : new[] { material };

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
