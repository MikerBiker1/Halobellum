// MushroomCloud.cs
// The shape of the HAVOK Nuke's cloud (M10): a stem standing on a skirt of dust, a wide cap on top that rolls over itself. It starts as a boiling ball of
// fire; then the fire dies down, and the cap's smoke goes on glowing for a while in its folds and underneath, in patches.
// The whole shape is worked out from the cloud's age for every puff, each frame: flying particles can't hold a stem and a cap together, and this way
// it freezes and speeds up with the battle like everything else. Puffs that flow (up the stem, out over the cap's crown) fade at both ends of their
// run, so nothing pops. Plain numbers: no random numbers (each puff's differences come from its index and the cloud's seed), nothing allocated.
// The camera looks down at it at a slant, so a round cap would cover its own stem. The cap is narrower along the way the camera faces at the blast than across it (CapDepthShare)
// to leave the stem in view. It keeps that shape if the camera turns afterwards: a cloud that turned with the camera would look wrong.
// Looks only. Depends on: UnityEngine.

using UnityEngine;

namespace Halobellum.Presentation
{
    public static class MushroomCloud
    {
        public struct Puff
        {
            /// <summary>From the middle of the blast on the ground, in metres.</summary>
            public Vector3 Offset;
            public float Size;
            public Color Color;
            public float Degrees;
            public uint Seed;
        }

        /// <summary>How long the cloud lasts, in seconds of battle time.</summary>
        public const float Seconds = 8.6f;

        public const int StemPuffs = 30;
        public const int SkirtPuffs = 22;
        public const int RimAround = 13;
        public const int RimAcross = 5;
        public const int CrownPuffs = 20;
        public const int SmokePuffs = StemPuffs + SkirtPuffs + RimAround * RimAcross + CrownPuffs;

        public const int StemFires = 14;
        public const int BallFires = 10;
        public const int RimFires = 34;
        public const int FirePuffs = StemFires + BallFires + RimFires;

        public const float CapDepthShare = 0.68f;

        /// <summary>Facing north, as the camera does when the game starts.</summary>
        public static readonly Vector2 North = new Vector2(0f, 1f);

        private const float TwoPi = Mathf.PI * 2f;

        /// <summary>The way a camera faces along the ground, for a blast seen from it: north if it looks straight down.</summary>
        public static Vector2 FacingOf(Vector3 cameraForward)
        {
            var facing = new Vector2(cameraForward.x, cameraForward.z);
            return facing.sqrMagnitude > 1e-4f ? facing.normalized : North;
        }

        /// <summary>Squeezes a part of the cap along the way the camera faces.</summary>
        private static Vector3 Narrowed(Vector3 offset, Vector2 facing)
        {
            float along = (offset.x * facing.x + offset.z * facing.y) * (1f - CapDepthShare);
            offset.x -= facing.x * along;
            offset.z -= facing.y * along;
            return offset;
        }

        /// <summary>The height of the cap's middle above the ground.</summary>
        public static float CapHeight(float age, float radius) => radius * (0.6f + 2.9f * Rise(age) + 0.05f * age);

        /// <summary>The distance from the cloud's axis to the middle of the cap's rolling rim, east to west.</summary>
        public static float RimRadius(float age, float radius) => radius * (0.2f + 0.8f * Spread(age));

        /// <summary>The thickness of the cap's rim, from its middle to its edge.</summary>
        public static float RimThickness(float age, float radius) => radius * (0.32f + 0.3f * Spread(age));

        /// <summary>The stem's radius at this share of the way from the ground to the cap, 0 to 1: wide at its foot, narrow, opening again into the cap.</summary>
        public static float StemRadius(float along, float radius)
        {
            float foot = 1f - along, foot2 = foot * foot, top = along * along;
            return radius * (0.2f + 0.42f * foot2 * foot2 + 0.24f * top * top * top);
        }

        private static float Rise(float age) => 1f - Mathf.Exp(-age / 0.95f);
        private static float Spread(float age) => 1f - Mathf.Exp(-age / 1.3f);

        /// <summary>How much of the smoke is still there: it comes up as the flash fades, stays, and thins away at the end.</summary>
        private static float SmokeLeft(float age, float goesFrom, float goneBy) => Mathf.SmoothStep(0f, 1f, age / 0.45f) * (1f - Mathf.SmoothStep(0f, 1f, (age - goesFrom) / (goneBy - goesFrom)));

        /// <summary>One puff of smoke, by index from 0 to SmokePuffs.</summary>
        public static Puff Smoke(int index, float age, float radius, int seed) => Smoke(index, age, radius, seed, North);

        /// <summary>One puff of smoke, by index from 0 to SmokePuffs, of a cloud seen from a camera that faced this way at the blast.</summary>
        public static Puff Smoke(int index, float age, float radius, int seed, Vector2 facing)
        {
            uint id = (uint)(index + seed * 131);
            float a = Hash(id), b = Hash(id + 1000u), c = Hash(id + 2000u);
            if (index < StemPuffs) return StemSmoke(index, id, a, b, c, age, radius);
            index -= StemPuffs;
            if (index < SkirtPuffs) return SkirtSmoke(index, id, a, b, c, age, radius);
            index -= SkirtPuffs;
            if (index < RimAround * RimAcross) return RimSmoke(index, id, a, b, c, age, radius, facing);
            return CrownSmoke(index - RimAround * RimAcross, id, a, b, c, age, radius, facing);
        }

        /// <summary>One lick of fire, by index from 0 to FirePuffs.</summary>
        public static Puff Fire(int index, float age, float radius, int seed) => Fire(index, age, radius, seed, North);

        /// <summary>One lick of fire, by index from 0 to FirePuffs, of a cloud seen from a camera that faced this way at the blast.</summary>
        public static Puff Fire(int index, float age, float radius, int seed, Vector2 facing)
        {
            uint id = (uint)(index + seed * 137 + 5000);
            float a = Hash(id), b = Hash(id + 1000u), c = Hash(id + 2000u);
            if (index < StemFires) return StemFire(index, id, a, b, c, age, radius);
            index -= StemFires;
            if (index < BallFires) return BallFire(id, a, b, c, age, radius);
            return RimFire(index - BallFires, id, a, b, c, age, radius, facing);
        }

        // ------------------------------------------------------------------ smoke

        private static Puff StemSmoke(int index, uint id, float a, float b, float c, float age, float radius)
        {
            float along = Mathf.Repeat((index + a * 0.6f) / StemPuffs + age * 0.13f, 1f);
            float top = StemTop(age, radius);
            float around = a * TwoPi + age * (b < 0.5f ? 1.1f : -0.9f);
            float reach = StemRadius(along, radius) * Mathf.Lerp(0.55f, 1f, c);
            float size = radius * (0.6f + 0.42f * (1f - along) * (1f - along) * (1f - along) + 0.2f * along) * Mathf.Lerp(0.85f, 1.1f, b);

            float heat = HeatOf(age);
            // Lit from below at first, and sooty once the fire has gone; brighter on the sunny east side.
            float shade = (0.72f + 0.34f * Mathf.Cos(around)) * Mathf.Lerp(0.85f, 1.1f, c);
            Color color = Color.Lerp(new Color(0.26f, 0.22f, 0.19f), new Color(0.52f, 0.3f, 0.16f), heat) * shade;
            color.a = 0.86f * Flowing(along) * SmokeLeft(age, 4.6f, 7f);
            return new Puff { Offset = new Vector3(Mathf.Cos(around) * reach, along * top, Mathf.Sin(around) * reach), Size = size, Color = color, Degrees = a * 360f + age * (c - 0.5f) * 50f, Seed = id };
        }

        private static Puff SkirtSmoke(int index, uint id, float a, float b, float c, float age, float radius)
        {
            float around = (index + a * 0.8f) / SkirtPuffs * TwoPi;
            float reach = radius * (0.5f + 1.25f * Spread(age) + 0.05f * age);
            var offset = new Vector3(Mathf.Cos(around) * reach, radius * (0.14f + 0.14f * b), Mathf.Sin(around) * reach * 0.85f);
            Color color = new Color(0.52f, 0.45f, 0.36f) * Mathf.Lerp(0.8f, 1.1f, c);
            color.a = 0.42f * SmokeLeft(age, 3.4f, 6.4f);
            return new Puff { Offset = offset, Size = radius * Mathf.Lerp(0.95f, 1.4f, b), Color = color, Degrees = b * 360f + age * 9f, Seed = id };
        }

        private static Puff RimSmoke(int index, uint id, float a, float b, float c, float age, float radius, Vector2 facing)
        {
            int ring = index / RimAcross, across = index % RimAcross;
            float around = (ring + (across % 2) * 0.5f + a * 0.3f) / RimAround * TwoPi + age * 0.05f;
            // Rolling: what rises in the middle goes out over the top, down the outside and back in underneath.
            float roll = (across + b * 0.5f) / RimAcross * TwoPi - Rolled(age);
            float cosRoll = Mathf.Cos(roll), sinRoll = Mathf.Sin(roll);
            float thick = RimThickness(age, radius);
            float reach = RimRadius(age, radius) + thick * cosRoll;
            var offset = Narrowed(new Vector3(Mathf.Cos(around) * reach, CapHeight(age, radius) + thick * 0.85f * sinRoll, Mathf.Sin(around) * reach), facing);
            float size = radius * (0.62f + 0.42f * Spread(age)) * Mathf.Lerp(0.88f, 1.12f, c);
            return CapPuff(offset, size, sinRoll, around, a, b, c, age, id);
        }

        private static Puff CrownSmoke(int index, uint id, float a, float b, float c, float age, float radius, Vector2 facing)
        {
            // Out from the middle of the top, to where the rim takes over.
            float flow = Mathf.Repeat((index + a * 0.7f) / CrownPuffs + age * 0.11f, 1f);
            float outward = Mathf.Sqrt(flow);
            float around = b * TwoPi + age * 0.1f;
            float reach = RimRadius(age, radius) * outward;
            float thick = RimThickness(age, radius);
            var offset = Narrowed(new Vector3(Mathf.Cos(around) * reach, CapHeight(age, radius) + thick * (1.25f - 0.4f * flow), Mathf.Sin(around) * reach), facing);
            float size = radius * (0.58f + 0.36f * Spread(age)) * Mathf.Lerp(0.88f, 1.12f, c);
            Puff puff = CapPuff(offset, size, 1f, around, a, b, c, age, id);
            puff.Color.a *= Flowing(flow);
            return puff;
        }

        /// <summary>Colour of a puff of the cap: sunlit where it faces up, dark underneath, and glowing while it is hot. The top cools first, the folds and the underside last, each puff in its own time.</summary>
        private static Puff CapPuff(Vector3 offset, float size, float up, float around, float a, float b, float c, float age, uint id)
        {
            float facing = up * 0.5f + 0.5f;
            float coolsAt = Mathf.Lerp(3.9f, 2f, facing) * Mathf.Lerp(0.85f, 1.2f, b);
            float heat = 1f - Mathf.SmoothStep(0f, 1f, (age - coolsAt + 1.1f) / 1.9f);
            float light = Mathf.Lerp(0.5f, 1.18f, facing) * (0.92f + 0.14f * Mathf.Cos(around)) * Mathf.Lerp(0.82f, 1.14f, c);
            Color cool = new Color(0.48f, 0.43f, 0.38f) * light;
            Color hot = Color.Lerp(new Color(0.95f, 0.46f, 0.14f), new Color(0.62f, 0.31f, 0.15f), facing) * Mathf.Lerp(0.85f, 1.1f, c);
            Color color = Color.Lerp(cool, hot, heat);
            color.a = 0.88f * SmokeLeft(age, 5.4f, 8.4f);
            float swell = 1f + 0.05f * Mathf.Max(0f, age - 5f);
            return new Puff { Offset = offset, Size = size * swell, Color = color, Degrees = a * 360f + age * (b - 0.5f) * 40f, Seed = id };
        }

        /// <summary>How far the rim has rolled by now, in radians: fast at first, then steady.</summary>
        private static float Rolled(float age) => 2f * (1f - Mathf.Exp(-age / 2f)) + 0.32f * age;

        /// <summary>From 1 just after the blast to 0 when the cloud has cooled.</summary>
        private static float HeatOf(float age) => 1f - Mathf.SmoothStep(0f, 1f, (age - 1f) / 2.8f);

        private static float StemTop(float age, float radius) => CapHeight(age, radius) - RimThickness(age, radius) * 0.3f;

        /// <summary>Fades something that flows along a run from 0 to 1 in at the start and out at the end, so that it doesn't pop when it starts again.</summary>
        private static float Flowing(float along) => Mathf.Clamp01(along * 12f) * Mathf.Clamp01((1f - along) * 8f);

        // ------------------------------------------------------------------ fire

        private static Puff StemFire(int index, uint id, float a, float b, float c, float age, float radius)
        {
            float along = Mathf.Repeat((index + a * 0.6f) / StemFires + age * 0.42f, 1f);
            float around = a * TwoPi + age * 1.6f;
            float reach = StemRadius(along, radius) * 0.5f * c;
            var offset = new Vector3(Mathf.Cos(around) * reach, along * StemTop(age, radius), Mathf.Sin(around) * reach);
            float strength = Flowing(along) * FireLeft(age, 1.2f, 3f);
            return FirePuff(offset, radius * Mathf.Lerp(0.42f, 0.66f, b), strength, age * 0.25f + along * 0.2f, a, b, age, id);
        }

        private static Puff BallFire(uint id, float a, float b, float c, float age, float radius)
        {
            // The first ball of fire, on the ground, gone as the stem takes over.
            float around = a * TwoPi;
            float up = b * 0.9f + 0.1f;
            float grow = radius * (0.35f + 0.45f * Mathf.Clamp01(age / 0.8f));
            float across = Mathf.Sqrt(1f - up * up);
            var offset = new Vector3(Mathf.Cos(around) * across * grow, up * grow + radius * 0.25f * age, Mathf.Sin(around) * across * grow);
            float strength = FireLeft(age, 0.5f, 1.6f);
            return FirePuff(offset, radius * Mathf.Lerp(0.8f, 1.2f, c), strength, age * 0.3f + 0.1f, a, b, age, id);
        }

        private static Puff RimFire(int index, uint id, float a, float b, float c, float age, float radius, Vector2 facing)
        {
            float around = (index + a * 0.9f) / RimFires * TwoPi + age * 0.07f;
            float roll = (index * 2.4f + b) - Rolled(age);
            float cosRoll = Mathf.Cos(roll), sinRoll = Mathf.Sin(roll);
            float thick = RimThickness(age, radius);
            float reach = RimRadius(age, radius) + thick * cosRoll * 0.85f;
            var offset = Narrowed(new Vector3(Mathf.Cos(around) * reach, CapHeight(age, radius) + thick * 0.8f * sinRoll, Mathf.Sin(around) * reach), facing);
            // Fire goes out on the top first. By the time it has gone everywhere, CapPuff's smoke has taken over the glow.
            float up = sinRoll * 0.5f + 0.5f;
            float strength = FireLeft(age, Mathf.Lerp(1.3f, 1f, up), Mathf.Lerp(2.7f, 2f, up));
            return FirePuff(offset, radius * Mathf.Lerp(0.55f, 0.82f, c), strength, age * 0.25f + up * 0.15f, a, b, age, id);
        }

        private static float FireLeft(float age, float goesFrom, float goneBy) => Mathf.Clamp01(age / 0.25f) * (1f - Mathf.SmoothStep(0f, 1f, (age - goesFrom) / (goneBy - goesFrom)));

        /// <summary>White-yellow through orange to a deep orange as cool goes from 0 to 1. It is out before it could look like a red ball.</summary>
        private static Puff FirePuff(Vector3 offset, float size, float strength, float cool, float a, float b, float age, uint id)
        {
            Color color = cool < 0.5f
                ? Color.Lerp(new Color(1f, 0.86f, 0.55f), new Color(1f, 0.5f, 0.13f), cool * 2f)
                : Color.Lerp(new Color(1f, 0.5f, 0.13f), new Color(0.8f, 0.24f, 0.05f), Mathf.Clamp01(cool * 2f - 1f));
            color.a = strength * 0.8f;
            return new Puff { Offset = offset, Size = size, Color = color, Degrees = a * 360f + age * (b - 0.5f) * 90f, Seed = id };
        }

        /// <summary>A number from 0 to 1 that only depends on n.</summary>
        private static float Hash(uint n)
        {
            n = (n ^ 61u) ^ (n >> 16);
            n *= 9u;
            n ^= n >> 4;
            n *= 0x27d4eb2du;
            n ^= n >> 15;
            return (n & 0xFFFFFF) / 16777216f;
        }
    }
}
