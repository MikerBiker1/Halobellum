// TerrainNoise.cs
// The random numbers and noise behind the faction terrain (M10): its own small generator, so that a map looks the same for a seed and nothing touches
// Unity's global random state or the simulation's, and smooth value noise from a fixed table, safe to read from several threads.
// Looks only. Depends on: nothing but System.

using System;

namespace Halobellum.Presentation
{
    /// <summary>A small repeatable random number generator (xorshift).</summary>
    public sealed class TerrainRandom
    {
        private uint _state;

        public TerrainRandom(int seed, int salt = 0)
        {
            _state = Mix((uint)seed * 2654435761u ^ ((uint)salt + 0x9E3779B9u));
            if (_state == 0u) _state = 0x6D2B79F5u;
        }

        public static uint Mix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }

        private uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>From 0 up to, but not including, 1.</summary>
        public float Value() => (Next() >> 8) / 16777216f;

        public float Range(float low, float high) => low + (high - low) * Value();

        /// <summary>From 0 up to, but not including, count.</summary>
        public int Int(int count) => count <= 0 ? 0 : (int)(Next() % (uint)count);

        public bool Chance(float probability) => Value() < probability;

        public float Sign() => Value() < 0.5f ? -1f : 1f;
    }

    /// <summary>Smooth noise of about one wave per unit, from 0 to 1.</summary>
    public static class TerrainNoise
    {
        private const int Size = 256;
        private const int Mask = Size - 1;
        private static readonly float[] Lattice = BuildLattice();

        private static float[] BuildLattice()
        {
            var random = new TerrainRandom(90210, 7);
            var lattice = new float[Size * Size];
            for (int i = 0; i < lattice.Length; i++) lattice[i] = random.Value();
            return lattice;
        }

        public static float Value(float x, float y)
        {
            int ix = (int)Math.Floor(x);
            int iy = (int)Math.Floor(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = ix & Mask, x1 = (ix + 1) & Mask;
            int y0 = (iy & Mask) * Size, y1 = ((iy + 1) & Mask) * Size;
            float a = Lattice[y0 + x0], b = Lattice[y0 + x1], c = Lattice[y1 + x0], d = Lattice[y1 + x1];
            float top = a + (b - a) * fx;
            float bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        /// <summary>Several sizes of noise added together: broad shapes with detail on them.</summary>
        public static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, weight = 0.5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y) * weight;
                total += weight;
                weight *= 0.5f;
                float nx = x * 1.62f + y * 1.18f + 17.3f;
                y = y * 1.62f - x * 1.18f + 4.9f;
                x = nx;
            }
            return sum / total;
        }

        /// <summary>Sharp crests where the noise crosses its middle: mountain ridges and rock fins.</summary>
        public static float Ridged(float x, float y, int octaves)
        {
            float sum = 0f, weight = 0.5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Math.Abs(Value(x, y) * 2f - 1f);
                sum += n * n * weight;
                total += weight;
                weight *= 0.5f;
                float nx = x * 1.71f + y * 1.09f + 31.7f;
                y = y * 1.71f - x * 1.09f + 11.1f;
                x = nx;
            }
            return sum / total;
        }

        /// <summary>Round swollen mounds with sharp creases between them.</summary>
        public static float Billow(float x, float y, int octaves)
        {
            float sum = 0f, weight = 0.5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Math.Abs(Value(x, y) * 2f - 1f) * weight;
                total += weight;
                weight *= 0.5f;
                float nx = x * 1.8f + y * 0.9f + 3.1f;
                y = y * 1.8f - x * 0.9f + 8.7f;
                x = nx;
            }
            return sum / total;
        }

        public static float Smooth(float low, float high, float value)
        {
            float t = (value - low) / (high - low);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }
    }
}
