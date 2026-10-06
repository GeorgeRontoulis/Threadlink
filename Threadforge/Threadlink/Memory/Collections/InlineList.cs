namespace Threadlink.Memory
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A list of at most <see cref="Capacity"/> elements inside another value's memory: an 8-byte header holding the
    /// count, then the slots. Adding past capacity throws. Removal swaps the last element in, so element order depends
    /// only on the operations made, as everything hashed or compared byte for byte must. See <see cref="InlineArray{T}"/>
    /// for why it is a <see langword="ref struct"/>, and <see cref="InlineLayout"/>.
    /// </summary>
    public readonly unsafe ref struct InlineList<T> where T : unmanaged
    {
        private readonly byte* memory;

        /// <summary>The most elements it holds.</summary>
        public int Capacity { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InlineList(byte* memory, int capacity)
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

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)*CountPointer)
                    throw new IndexOutOfRangeException("Index " + index + " is outside an InlineList<" + typeof(T).Name + "> of " + *CountPointer + ".");

                return ref Data[index];
            }
        }

        public void Add(in T value)
        {
            int* count = CountPointer;

            if (*count >= Capacity)
                throw new InvalidOperationException("An InlineList<" + typeof(T).Name + "> holds at most " + Capacity + " elements. Raise its declared capacity.");

            Data[*count] = value;
            (*count)++;
        }

        /// <summary>Removes the element at <paramref name="index"/>, moving the last element into its place.</summary>
        public void RemoveAt(int index)
        {
            int* count = CountPointer;

            if ((uint)index >= (uint)*count)
                throw new IndexOutOfRangeException("Index " + index + " is outside an InlineList<" + typeof(T).Name + "> of " + *count + ".");

            int last = --(*count);

            if (index != last)
                Data[index] = Data[last];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => *CountPointer = 0;

        public Enumerator GetEnumerator() => new(Data, *CountPointer);

        public ref struct Enumerator
        {
            private readonly T* data;
            private readonly int count;
            private int index;

            internal Enumerator(T* data, int count)
            {
                this.data = data;
                this.count = count;
                index = -1;
            }

            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref data[index];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++index < count;
        }
    }
}
