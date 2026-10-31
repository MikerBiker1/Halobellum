// Vec2.cs
// A small 2D vector used by the battle simulation for positions, directions and velocities.
// The battlefield is flat and seen from above: X runs left to right, Y runs from Team A's side
// toward Team B's side.
// Depends on: nothing (plain C#, no Unity types), so the simulation runs headless and in tests.

using System;

namespace Halobellum.Core
{
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 Up = new Vec2(0f, 1f);
        public static readonly Vec2 Down = new Vec2(0f, -1f);
        public static readonly Vec2 Right = new Vec2(1f, 0f);

        public float LengthSquared => X * X + Y * Y;
        public float Length => MathF.Sqrt(LengthSquared);

        /// <summary>Returns this vector scaled to length 1, or zero if it has no length.</summary>
        public Vec2 Normalized
        {
            get
            {
                float length = Length;
                return length > 1e-6f ? new Vec2(X / length, Y / length) : Zero;
            }
        }

        /// <summary>The vector turned 90 degrees counter-clockwise.</summary>
        public Vec2 Perpendicular => new Vec2(-Y, X);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>2D cross product: positive when b is counter-clockwise from a.</summary>
        public static float Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static float DistanceSquared(Vec2 a, Vec2 b) => (a - b).LengthSquared;

        /// <summary>Returns this vector shortened to maxLength if it is longer.</summary>
        public Vec2 ClampLength(float maxLength)
        {
            float lengthSquared = LengthSquared;
            if (lengthSquared <= maxLength * maxLength) return this;
            return Normalized * maxLength;
        }

        /// <summary>Angle between two directions in degrees, from 0 to 180.</summary>
        public static float AngleDegrees(Vec2 a, Vec2 b)
        {
            Vec2 na = a.Normalized;
            Vec2 nb = b.Normalized;
            if (na.LengthSquared < 0.5f || nb.LengthSquared < 0.5f) return 0f;
            float dot = Math.Clamp(Dot(na, nb), -1f, 1f);
            return MathF.Acos(dot) * (180f / MathF.PI);
        }

        /// <summary>
        /// Turns the direction 'from' toward 'to' by at most maxDegrees and returns the new direction
        /// (length 1). When the two point exactly opposite ways it turns counter-clockwise.
        /// </summary>
        public static Vec2 RotateTowards(Vec2 from, Vec2 to, float maxDegrees)
        {
            Vec2 target = to.Normalized;
            if (target.LengthSquared < 0.5f) return from.Normalized;
            Vec2 start = from.Normalized;
            if (start.LengthSquared < 0.5f) return target;

            float angle = AngleDegrees(start, target);
            if (angle <= maxDegrees) return target;
            float direction = Cross(start, target) >= 0f ? 1f : -1f;
            return start.Rotated(direction * maxDegrees).Normalized;
        }

        /// <summary>The vector rotated counter-clockwise by the given angle in degrees.</summary>
        public Vec2 Rotated(float degrees)
        {
            float radians = degrees * (MathF.PI / 180f);
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            return new Vec2(X * cos - Y * sin, X * sin + Y * cos);
        }

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }
}
