namespace Threadlink.Memory
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A set of at most <see cref="Capacity"/> elements inside another value's memory, hashed and compared as raw bytes
    /// (see <see cref="InlineTable"/>): an element's padding must be zeroed, as generated layouts guarantee. Enumeration
    /// visits slots in table order, which depends only on the operations made. See <see cref="InlineArray{T}"/> for why it
    /// is a <see langword="ref struct"/>, and <see cref="InlineLayout"/>.
    /// </summary>
    public readonly unsafe ref struct InlineHashSet<T> where T : unmanaged
    {
        private static readonly int SlotBytes = InlineLayout.SetSlotBytes(sizeof(T));

        private readonly byte* memory;

        /// <summary>The table's size: the most elements it holds.</summary>
        public int Capacity { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InlineHashSet(byte* memory, int capacity)
        {
            this.memory = memory;
            Capacity = capacity;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *(int*)memory;
        }

        public bool Contains(in T value)
        {
            fixed (T* key = &value)
                return InlineTable.Find(memory, Capacity, SlotBytes, (byte*)key, sizeof(T)) >= 0;
        }

        /// <summary>Adds <paramref name="value"/>; false if it was already there.</summary>
        public bool Add(in T value)
        {
            fixed (T* key = &value)
            {
                if (InlineTable.Find(memory, Capacity, SlotBytes, (byte*)key, sizeof(T)) >= 0)
                    return false;

                byte* slot = InlineTable.Claim(memory, Capacity, SlotBytes, (byte*)key, sizeof(T), "InlineHashSet<" + typeof(T).Name + ">");

                *(T*)(slot + InlineLayout.HEADER_BYTES) = value;
                return true;
            }
        }

        /// <summary>Removes <paramref name="value"/>; false if it wasn't there.</summary>
        public bool Remove(in T value)
        {
            fixed (T* key = &value)
            {
                int index = InlineTable.Find(memory, Capacity, SlotBytes, (byte*)key, sizeof(T));

                if (index < 0)
                    return false;

                InlineTable.Remove(memory, index, SlotBytes);
                return true;
            }
        }

        public void Clear() => InlineTable.Clear(memory, Capacity, SlotBytes);

        public Enumerator GetEnumerator() => new(memory, Capacity);

        public ref struct Enumerator
        {
            private readonly byte* memory;
            private readonly int capacity;
            private int index;

            internal Enumerator(byte* memory, int capacity)
            {
                this.memory = memory;
                this.capacity = capacity;
                index = -1;
            }

            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref *(T*)(InlineTable.Slot(memory, index, SlotBytes) + InlineLayout.HEADER_BYTES);
            }

            public bool MoveNext() => (index = InlineTable.NextOccupied(memory, capacity, SlotBytes, index)) < capacity;
        }
    }
}
