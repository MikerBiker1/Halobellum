// StableHasher.cs
// A 64-bit FNV-1a hash that gives the same number for the same data on every machine and in every run (Milestone 11: two LAN machines
// compare their match state and their game content with it). Numbers go in as their bytes, little-endian; floats as their exact bits;
// strings as their UTF-8 length and bytes (ordinal, no culture). Never feed it string.GetHashCode, System.HashCode or Vec2.GetHashCode:
// those change from one run to the next.
// Depends on: nothing (plain C#).

using System.Runtime.InteropServices;
using System.Text;

namespace Halobellum.Economy
{
    public sealed class StableHasher
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash = OffsetBasis;

        /// <summary>The hash of everything added so far.</summary>
        public ulong Value => _hash;

        public StableHasher Byte(byte value)
        {
            _hash ^= value;
            _hash *= Prime;
            return this;
        }

        public StableHasher Int(int value)
        {
            uint v = unchecked((uint)value);
            Byte((byte)v);
            Byte((byte)(v >> 8));
            Byte((byte)(v >> 16));
            return Byte((byte)(v >> 24));
        }

        public StableHasher Long(long value)
        {
            ulong v = unchecked((ulong)value);
            for (int i = 0; i < 8; i++) Byte((byte)(v >> (8 * i)));
            return this;
        }

        public StableHasher ULong(ulong value) => Long(unchecked((long)value));

        public StableHasher Bool(bool value) => Byte(value ? (byte)1 : (byte)0);

        /// <summary>A float as its exact bits (so 0.1f and 0.1f + one ulp differ, and -0 differs from 0).</summary>
        public StableHasher Float(float value) => Int(FloatToBits(value));

        public StableHasher Double(double value) => Long(DoubleToBits(value));

        /// <summary>A string as its UTF-8 byte count and bytes; null is told apart from "".</summary>
        public StableHasher Text(string value)
        {
            if (value == null) return Int(-1);
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Int(bytes.Length);
            foreach (byte b in bytes) Byte(b);
            return this;
        }

        /// <summary>A hash as 16 hex digits, for messages and logs.</summary>
        public static string Hex(ulong hash) => hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ exact float bits (no unsafe code, works on every API level)

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public int Int;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DoubleBits
        {
            [FieldOffset(0)] public double Double;
            [FieldOffset(0)] public long Long;
        }

        /// <summary>The float's 32 bits as an int (the same as BitConverter.SingleToInt32Bits).</summary>
        public static int FloatToBits(float value) => new FloatBits { Float = value }.Int;

        /// <summary>The float whose 32 bits are these.</summary>
        public static float BitsToFloat(int bits) => new FloatBits { Int = bits }.Float;

        public static long DoubleToBits(double value) => new DoubleBits { Double = value }.Long;
    }
}
