// EffectBudget.cs
// Decides whether an effect may still be drawn, so that a huge battle keeps its frame rate (M10). Particles are split into
// importance levels. As a layer of particles fills up, or as frames get slow, the small ones are dropped first; a huge one
// (a nuke, the Infected Pelican's crash) is only ever stopped by the layer being completely full. Plain C#, so it can be tested.
// Depends on: nothing.

namespace Halobellum.Presentation
{
    public enum EffectPriority
    {
        /// <summary>Muzzle flashes, bullet sparks, dust, trail puffs: many, and nobody misses a few.</summary>
        Small = 0,
        /// <summary>Plasma splashes, trails of big shots, an infantry death.</summary>
        Medium = 1,
        /// <summary>A blast, a vehicle's death.</summary>
        Big = 2,
        /// <summary>Spells and the crash of the Infected Pelican.</summary>
        Huge = 3,
    }

    public sealed class EffectBudget
    {
        /// <summary>Above this frame time (seconds) small effects start being dropped.</summary>
        public const float RelaxedFrameSeconds = 1f / 75f;
        /// <summary>At this frame time (seconds) everything that may be dropped is dropped as far as it goes.</summary>
        public const float StrainedFrameSeconds = 1f / 45f;

        private const float SmallFill = 0.6f, SmallFillStrained = 0.12f;
        private const float MediumFill = 0.85f, MediumFillStrained = 0.4f;
        private const float BigFill = 0.97f;
        /// <summary>The most small particles started in one frame, unless the kit has room for more.</summary>
        public const int SmallPerFrame = 260;

        private int _smallThisFrame;

        /// <summary>The most small particles started in one frame with this budget: High quality's extra dust needs more than Low's.</summary>
        public int SmallLimit { get; set; } = SmallPerFrame;

        /// <summary>From 0 (frames are fast) to 1 (frames are slow).</summary>
        public float Pressure { get; private set; }

        /// <summary>Everything turned down since the battle was shown, by importance (for reports and tests).</summary>
        public readonly int[] Dropped = new int[4];

        /// <summary>Call once per frame with the time the frame took.</summary>
        public void BeginFrame(float frameSeconds)
        {
            _smallThisFrame = 0;
            float wanted = Clamp01((frameSeconds - RelaxedFrameSeconds) / (StrainedFrameSeconds - RelaxedFrameSeconds));
            // Quick to back off, slow to come back, so that one long frame only thins things out for a moment.
            Pressure += (wanted - Pressure) * (wanted > Pressure ? 0.25f : 0.03f);
        }

        public void Reset()
        {
            Pressure = 0f;
            _smallThisFrame = 0;
            for (int i = 0; i < Dropped.Length; i++) Dropped[i] = 0;
        }

        /// <summary>The share of a particle layer this importance may fill right now.</summary>
        public float FillLimit(EffectPriority priority)
        {
            switch (priority)
            {
                case EffectPriority.Small: return SmallFill + (SmallFillStrained - SmallFill) * Pressure;
                case EffectPriority.Medium: return MediumFill + (MediumFillStrained - MediumFill) * Pressure;
                case EffectPriority.Big: return BigFill;
                default: return 1f;
            }
        }

        /// <summary>True if a particle of this importance may start in a layer that is this full (0 to 1).</summary>
        public bool Allow(EffectPriority priority, float fill)
        {
            if (fill >= FillLimit(priority) || (priority == EffectPriority.Small && _smallThisFrame >= SmallLimit))
            {
                Dropped[(int)priority]++;
                return false;
            }
            if (priority == EffectPriority.Small) _smallThisFrame++;
            return true;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
