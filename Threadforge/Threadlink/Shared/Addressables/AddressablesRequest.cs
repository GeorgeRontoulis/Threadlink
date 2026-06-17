namespace Threadlink.Shared
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AddressablesRequest<T> : IDisposable
    where T : unmanaged, Enum
    {
        public ref T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref buffer.ElementAt(i);
        }

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer.Length;
        }

        private UnsafeList<T> buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddressablesRequest(int count, Allocator allocator) => buffer = new(count, allocator);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T resourceID) => buffer.AddNoResize(resourceID);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => buffer.Dispose();
    }
}
