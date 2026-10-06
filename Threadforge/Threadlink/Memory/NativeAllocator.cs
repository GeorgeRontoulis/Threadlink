namespace Threadlink.Memory
{
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// Long-lived native memory for state that is copied, hashed and saved as raw bytes, such as a simulation's frames.
    /// Every block is zeroed and aligned, and allocated through Unity's tracked allocator, so the Editor's leak detection
    /// reports a block that is never freed.
    /// </summary>
    public static unsafe class NativeAllocator
    {
        /// <summary>A cache line: enough for any struct, and keeps a block from sharing a line with another.</summary>
        public const int DEFAULT_ALIGNMENT = 64;

        /// <summary>
        /// <paramref name="bytes"/> of zeroed memory aligned to <paramref name="alignment"/>, a power of two. Zeroing is not
        /// optional: a block that is hashed whole must not contain whatever the allocator left there.
        /// </summary>
        public static byte* AllocateZeroed(long bytes, int alignment = DEFAULT_ALIGNMENT)
        {
            if (bytes <= 0L)
                throw new ArgumentOutOfRangeException(nameof(bytes), "Allocate at least one byte.");

            if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(alignment), "The alignment must be a positive power of two.");

            // Refuse a size the allocator's alignment padding would overflow, before asking it for anything.
            if (bytes > long.MaxValue - alignment - IntPtr.Size)
                throw new ArgumentOutOfRangeException(nameof(bytes), "No allocation of " + bytes + " bytes can be aligned to " + alignment + ".");

            var block = (byte*)UnsafeUtility.MallocTracked(bytes, alignment, Allocator.Persistent, 0);

            if (block == null)
                throw new OutOfMemoryException("Could not allocate " + bytes + " bytes of native memory.");

            UnsafeUtility.MemClear(block, bytes);
            return block;
        }

        /// <summary>Frees a block from <see cref="AllocateZeroed"/>. A null block is ignored.</summary>
        public static void Free(byte* block)
        {
            if (block != null)
                UnsafeUtility.FreeTracked(block, Allocator.Persistent);
        }

        /// <summary>Copies <paramref name="bytes"/> from <paramref name="source"/>; the two ranges must not overlap.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy(byte* destination, byte* source, long bytes) => UnsafeUtility.MemCpy(destination, source, bytes);

        /// <summary>Sets <paramref name="bytes"/> bytes to zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clear(byte* block, long bytes) => UnsafeUtility.MemClear(block, bytes);

        /// <summary>Whether two ranges hold the same bytes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equal(byte* a, byte* b, long bytes) => UnsafeUtility.MemCmp(a, b, bytes) == 0;
    }
}
