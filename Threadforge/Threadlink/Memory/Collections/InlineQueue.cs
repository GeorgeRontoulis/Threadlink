namespace Threadlink.Memory
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A first-in, first-out ring of at most <see cref="Capacity"/> elements inside another value's memory: an 8-byte
    /// header holding the head and the count, then the slots. See <see cref="InlineArray{T}"/> for why it is a
    /// <see langword="ref struct"/>, and <see cref="InlineLayout"/>.
    /// </summary>
    public readonly unsafe ref struct InlineQueue<T> where T : unmanaged
    {
        private readonly byte* memory;

        /// <summary>The most elements it holds.</summary>
        public int Capacity { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InlineQueue(byte* memory, int capacity)
        {
            this.memory = memory;
            Capacity = capacity;
        }

        private int* HeadPointer => (int*)memory;
        private int* CountPointer => (int*)(memory + 4);
        private T* Data => (T*)(memory + InlineLayout.HEADER_BYTES);

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *CountPointer;
        }

        public void Enqueue(in T value)
        {
            int* count = CountPointer;

            if (*count >= Capacity)
                throw new InvalidOperationException("An InlineQueue<" + typeof(T).Name + "> holds at most " + Capacity + " elements. Raise its declared capacity.");

            Data[(*HeadPointer + *count) % Capacity] = value;
            (*count)++;
        }

        public T Dequeue()
        {
            int* count = CountPointer;

            if (*count <= 0)
                throw new InvalidOperationException("The InlineQueue<" + typeof(T).Name + "> is empty.");

            int* head = HeadPointer;
            T value = Data[*head];

            *head = (*head + 1) % Capacity;
            (*count)--;
            return value;
        }

        public T Peek()
        {
            if (*CountPointer <= 0)
                throw new InvalidOperationException("The InlineQueue<" + typeof(T).Name + "> is empty.");

            return Data[*HeadPointer];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            *HeadPointer = 0;
            *CountPointer = 0;
        }
    }
}
