// SimRandom.cs
// Seeded random number generator for the battle simulation. The same seed always produces the
// same sequence of numbers, so a battle with the same armies and seed plays out the same way
// (Bible Section 4, rule 3). Used for chance rolls such as shield block chances.
// Depends on: nothing (plain C#). Do not use System.Random or UnityEngine.Random in the simulation.

namespace Halobellum.Core
{
    public sealed class SimRandom
    {
        private ulong _state;

        public SimRandom(int seed)
        {
            // SplitMix64 turns any seed (including 0 and small numbers) into a well-mixed start state.
            ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            _state = z ^ (z >> 31);
            if (_state == 0) _state = 1;
        }

        /// <summary>Next raw 64-bit value (xorshift64*).</summary>
        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 2685821657736338717UL;
        }

        /// <summary>A float from 0 (inclusive) to 1 (exclusive).</summary>
        public float NextFloat()
        {
            return (NextULong() >> 40) * (1f / 16777216f);
        }

        /// <summary>A float from min (inclusive) to max (exclusive).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>True with the given probability (0 = never, 1 = always).</summary>
        public bool Chance(float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            return NextFloat() < probability;
        }
    }
}
