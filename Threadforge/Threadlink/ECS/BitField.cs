namespace Threadlink.ECS
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Utilities.ECS;
    using Unity.Collections;

    /// <summary>
    /// A zero-allocation, tightly packed array of bits. 
    /// 8x more memory efficient than bool[].
    /// </summary>
    public struct BitField : IDisposable
    {
        private NativeArray<ulong> bits;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => bits.DisposeSafely();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitField(int capacity, Allocator allocator)
        {
            // capacity + 63 ensures we round up to the nearest 64
            bits = new((capacity + 63) / 64, allocator, NativeArrayOptions.ClearMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int index)
        {
            int arrayIndex = index >> 6; // Fast divide by 64
            int bitIndex = index & 63;   // Fast modulo 64

            return (bits[arrayIndex] & (1UL << bitIndex)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index)
        {
            int arrayIndex = index >> 6;
            int bitIndex = index & 63;

            bits[arrayIndex] |= (1UL << bitIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(int index)
        {
            int arrayIndex = index >> 6;
            int bitIndex = index & 63;

            bits[arrayIndex] &= ~(1UL << bitIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            int length = bits.Length;
            for (int i = 0; i < length; i++)
                bits[i] = 0;
        }
    }
}