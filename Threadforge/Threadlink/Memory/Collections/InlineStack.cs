namespace Threadlink.Memory
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A last-in, first-out stack of at most <see cref="Capacity"/> elements inside another value's memory, laid out as an
    /// <see cref="InlineList{T}"/>. See <see cref="InlineArray{T}"/> for why it is a <see langword="ref struct"/>, and
    /// <see cref="InlineLayout"/>.
    /// </summary>
    public readonly unsafe ref struct InlineStack<T> where T : unmanaged
    {
        private readonly byte* memory;

        /// <summary>The most elements it holds.</summary>
        public int Capacity { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InlineStack(byte* memory, int capacity)
        {
            this.memory = memory;
            Capacity = capacity;
        }

        private int* CountPointer => (int*)memory;
        private T* Data => (T*)(memory + InlineLayout.HEADER_BYTES);

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *CountPointer;
        }

        public void Push(in T value)
        {
            int* count = CountPointer;

            if (*count >= Capacity)
                throw new InvalidOperationException("An InlineStack<" + typeof(T).Name + "> holds at most " + Capacity + " elements. Raise its declared capacity.");

            Data[*count] = value;
            (*count)++;
        }

        public T Pop()
        {
            int* count = CountPointer;

            if (*count <= 0)
                throw new InvalidOperationException("The InlineStack<" + typeof(T).Name + "> is empty.");

            return Data[--(*count)];
        }

        public T Peek()
        {
            int count = *CountPointer;

            if (count <= 0)
                throw new InvalidOperationException("The InlineStack<" + typeof(T).Name + "> is empty.");

            return Data[count - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => *CountPointer = 0;
    }
}
