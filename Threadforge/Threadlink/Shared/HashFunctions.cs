namespace Threadlink.Shared
{
    using System;
    using System.IO.Hashing;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;

    public static class HashFunctions
    {
        public static ulong ToXxHash64(string input, long seed)
        {
            input = input.Trim().ToLowerInvariant();

            int byteCount = Encoding.UTF8.GetByteCount(input);
            Span<byte> buffer = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

            Encoding.UTF8.GetBytes(input, buffer);

            return XxHash64.HashToUInt64(buffer, seed);
        }

        public static ulong ToXxHash64(string input)
        {
            input = input.Trim().ToLowerInvariant();

            int byteCount = Encoding.UTF8.GetByteCount(input);
            Span<byte> buffer = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

            Encoding.UTF8.GetBytes(input, buffer);

            return XxHash64.HashToUInt64(buffer);
        }

        public static int ToXxHash32(string input, int seed)
        {
            input = input.Trim().ToLowerInvariant();

            int byteCount = Encoding.UTF8.GetByteCount(input);
            Span<byte> buffer = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

            Encoding.UTF8.GetBytes(input, buffer);

            Span<byte> result = stackalloc byte[sizeof(int)];
            XxHash32.Hash(buffer, result, seed);

            return MemoryMarshal.Read<int>(result);
        }

        public static int ToXxHash32(string input)
        {
            input = input.Trim().ToLowerInvariant();

            int byteCount = Encoding.UTF8.GetByteCount(input);
            Span<byte> buffer = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

            Encoding.UTF8.GetBytes(input, buffer);

            Span<byte> result = stackalloc byte[sizeof(int)];
            XxHash32.Hash(buffer, result);

            return MemoryMarshal.Read<int>(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToSplitMix64(ulong input, ulong seed)
        {
            ulong x = input ^ seed;

            x ^= x >> 30;
            x *= 0xbf58476d1ce4e5b9UL;
            x ^= x >> 27;
            x *= 0x94d049bb133111ebUL;

            return x ^ (x >> 31);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToSplitMix64(ulong input)
        {
            ulong x = input;

            x ^= x >> 30;
            x *= 0xbf58476d1ce4e5b9UL;
            x ^= x >> 27;
            x *= 0x94d049bb133111ebUL;

            return x ^ (x >> 31);
        }
    }
}
