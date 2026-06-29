namespace Threadlink.ECS
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Utilities.Collections;

    [StructLayout(LayoutKind.Sequential)]
    public struct RingBuffer<T> : IDisposable where T : unmanaged
    {
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => nativeBuffer.IsCreated;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => nativeBuffer.Length;
        }

        private NativeArray<T> nativeBuffer;
        private int head;
        private int tail;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            nativeBuffer.DisposeSafely();
            head = tail = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RingBuffer(int length, Allocator allocator)
        {
            nativeBuffer = new(length, allocator);
            head = tail = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Cycle(int value) => (value + 1) % nativeBuffer.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(in T input)
        {
            if (!nativeBuffer.IsCreated)
                return;

            nativeBuffer[tail] = input;
            tail = Cycle(tail);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out T result)
        {
            if (head == tail || !nativeBuffer.IsCreated)
            {
                result = default;
                return false;
            }

            result = nativeBuffer[head];
            head = Cycle(head);
            return true;
        }
    }
}
