namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Bit counts and rotations of 64-bit words, the same on every runtime: Mono, IL2CPP and CoreCLR alike. Masks,
    /// hashes and fixed-point arithmetic use them.
    /// </summary>
    public static class Bits
    {
        private const ulong DE_BRUIJN = 0x03F79D71B4CB0A89UL;

        private static readonly byte[] DeBruijnIndex =
        {
             0,  1, 48,  2, 57, 49, 28,  3, 61, 58, 50, 42, 38, 29, 17,  4,
            62, 55, 59, 36, 53, 51, 43, 22, 45, 39, 33, 30, 24, 18, 12,  5,
            63, 47, 56, 27, 60, 41, 37, 16, 54, 35, 52, 21, 44, 32, 23, 11,
            46, 26, 40, 15, 34, 20, 31, 10, 25, 14, 19,  9, 13,  8,  7,  6,
        };

        /// <summary>The index of the lowest set bit, or 64 for zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(ulong value)
        {
            return value == 0UL ? 64 : DeBruijnIndex[unchecked((value & (ulong)-(long)value) * DE_BRUIJN) >> 58];
        }

        /// <summary>Zero bits above the highest set bit, or 64 for zero.</summary>
        public static int LeadingZeroCount(ulong value)
        {
            if (value == 0UL)
                return 64;

            int count = 0;

            if (value >> 32 == 0UL) { count += 32; value <<= 32; }
            if (value >> 48 == 0UL) { count += 16; value <<= 16; }
            if (value >> 56 == 0UL) { count += 8; value <<= 8; }
            if (value >> 60 == 0UL) { count += 4; value <<= 4; }
            if (value >> 62 == 0UL) { count += 2; value <<= 2; }
            if (value >> 63 == 0UL) { count += 1; }

            return count;
        }

        /// <summary>How many bits are set.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;

            return (int)(unchecked(value * 0x0101010101010101UL) >> 56);
        }

        /// <summary><paramref name="value"/> rotated left by <paramref name="offset"/> bits (taken modulo 64).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateLeft(ulong value, int offset) => (value << offset) | (value >> (64 - offset));
    }
}
