namespace Threadlink.Memory
{
    using System;
    using System.IO.Hashing;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Hashes of raw memory: the same bytes give the same hash on every machine and runtime, which a type's own
    /// <c>GetHashCode</c> does not promise. <see cref="XxHash64(byte*, long, long)"/> fingerprints whole blocks (a
    /// simulation frame, to detect divergence); <see cref="XxHash32(byte*, int)"/> places keys in hash tables.
    /// </summary>
    public static unsafe class MemoryHash
    {
        private const ulong PRIME1 = 11400714785074694791UL;
        private const ulong PRIME2 = 14029467366897019727UL;
        private const ulong PRIME3 = 1609587929392839161UL;
        private const ulong PRIME4 = 9650029242287828579UL;
        private const ulong PRIME5 = 2870177450012600261UL;

        /// <summary>
        /// The 64-bit xxHash (XXH64) of <paramref name="length"/> bytes, of any size: the value
        /// <see cref="System.IO.Hashing.XxHash64"/> gives, computed over raw memory. Frames are hashed whole on every
        /// snapshot and save, and the library's span-based reads cost Mono (the Editor) many times what these do.
        /// </summary>
        public static ulong XxHash64(byte* data, long length, long seed = 0L)
        {
            if (length < 0L)
                throw new ArgumentOutOfRangeException(nameof(length));

            // The reads below are little-endian, as XXH64 is defined; a big-endian runtime takes the library's way.
            if (BitConverter.IsLittleEndian is false)
                return LibraryXxHash64(data, length, seed);

            ulong s = (ulong)seed;
            byte* end = data + length;
            ulong hash;

            if (length >= 32L)
            {
                byte* limit = end - 32;
                ulong v1 = s + PRIME1 + PRIME2, v2 = s + PRIME2, v3 = s, v4 = s - PRIME1;

                // The bulk of a frame: rounds written out, since the Editor compiles in Debug and calls aren't inlined there.
                // Aligned blocks (frames always are) read directly; others read safely on every architecture.
                if (((ulong)data & 7UL) == 0UL)
                {
                    do
                    {
                        v1 += *(ulong*)data * PRIME2; v1 = ((v1 << 31) | (v1 >> 33)) * PRIME1;
                        v2 += *(ulong*)(data + 8) * PRIME2; v2 = ((v2 << 31) | (v2 >> 33)) * PRIME1;
                        v3 += *(ulong*)(data + 16) * PRIME2; v3 = ((v3 << 31) | (v3 >> 33)) * PRIME1;
                        v4 += *(ulong*)(data + 24) * PRIME2; v4 = ((v4 << 31) | (v4 >> 33)) * PRIME1;
                        data += 32;
                    }
                    while (data <= limit);
                }
                else
                {
                    do
                    {
                        v1 = Round(v1, Unsafe.ReadUnaligned<ulong>(data));
                        v2 = Round(v2, Unsafe.ReadUnaligned<ulong>(data + 8));
                        v3 = Round(v3, Unsafe.ReadUnaligned<ulong>(data + 16));
                        v4 = Round(v4, Unsafe.ReadUnaligned<ulong>(data + 24));
                        data += 32;
                    }
                    while (data <= limit);
                }

                hash = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
                hash = MergeRound(hash, v1);
                hash = MergeRound(hash, v2);
                hash = MergeRound(hash, v3);
                hash = MergeRound(hash, v4);
            }
            else hash = s + PRIME5;

            hash += (ulong)length;

            while (data + 8 <= end)
            {
                hash ^= Round(0UL, Unsafe.ReadUnaligned<ulong>(data));
                hash = RotateLeft(hash, 27) * PRIME1 + PRIME4;
                data += 8;
            }

            if (data + 4 <= end)
            {
                hash ^= Unsafe.ReadUnaligned<uint>(data) * PRIME1;
                hash = RotateLeft(hash, 23) * PRIME2 + PRIME3;
                data += 4;
            }

            while (data < end)
            {
                hash ^= *data * PRIME5;
                hash = RotateLeft(hash, 11) * PRIME1;
                data++;
            }

            hash ^= hash >> 33;
            hash *= PRIME2;
            hash ^= hash >> 29;
            hash *= PRIME3;
            hash ^= hash >> 32;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Round(ulong accumulator, ulong input) => RotateLeft(accumulator + input * PRIME2, 31) * PRIME1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MergeRound(ulong hash, ulong accumulator) => (hash ^ Round(0UL, accumulator)) * PRIME1 + PRIME4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft(ulong value, int bits) => (value << bits) | (value >> (64 - bits));

        private static ulong LibraryXxHash64(byte* data, long length, long seed)
        {
            if (length <= int.MaxValue)
                return System.IO.Hashing.XxHash64.HashToUInt64(new ReadOnlySpan<byte>(data, (int)length), seed);

            // Larger than one span: the incremental hash gives the same result, at the cost of one allocation.
            var hash = new System.IO.Hashing.XxHash64(seed);

            while (length > 0L)
            {
                int chunk = length > int.MaxValue ? int.MaxValue : (int)length;

                hash.Append(new ReadOnlySpan<byte>(data, chunk));
                data += chunk;
                length -= chunk;
            }

            return hash.GetCurrentHashAsUInt64();
        }

        /// <summary>The 32-bit xxHash of <paramref name="length"/> bytes: a key's bucket in an open-addressing table.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint XxHash32(byte* data, int length) => System.IO.Hashing.XxHash32.HashToUInt32(new ReadOnlySpan<byte>(data, length));

        /// <summary>The 32-bit xxHash of a value's bytes. Its padding must be zeroed, as generated layouts guarantee.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint XxHash32<T>(in T value) where T : unmanaged
        {
            fixed (T* pointer = &value)
                return XxHash32((byte*)pointer, sizeof(T));
        }
    }
}
