namespace Threadlink.Memory
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A dictionary of at most <see cref="Capacity"/> entries inside another value's memory, keyed by the key's raw bytes
    /// (see <see cref="InlineTable"/>). A slot is an 8-byte state, the key and the value, each on an 8-byte boundary. See
    /// <see cref="InlineArray{T}"/> for why it is a <see langword="ref struct"/>, and <see cref="InlineLayout"/>.
    /// </summary>
    public readonly unsafe ref struct InlineDictionary<TKey, TValue>
        where TKey : unmanaged
        where TValue : unmanaged
    {
        private static readonly int SlotBytes = InlineLayout.DictionarySlotBytes(sizeof(TKey), sizeof(TValue));
        private static readonly int ValueOffset = InlineLayout.HEADER_BYTES + InlineLayout.Align(sizeof(TKey));

        private readonly byte* memory;

        /// <summary>The table's size: the most entries it holds.</summary>
        public int Capacity { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InlineDictionary(byte* memory, int capacity)
        {
            this.memory = memory;
            Capacity = capacity;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *(int*)memory;
        }

        public bool ContainsKey(in TKey key) => Find(in key) >= 0;

        public bool TryGetValue(in TKey key, out TValue value)
        {
            int index = Find(in key);

            value = index >= 0 ? *ValueAt(index) : default;
            return index >= 0;
        }

        /// <summary>The value stored for <paramref name="key"/>, by reference. Throws if the key is absent.</summary>
        public ref TValue this[in TKey key]
        {
            get
            {
                int index = Find(in key);

                if (index < 0)
                    throw new KeyNotFoundException("The key is not in this InlineDictionary<" + typeof(TKey).Name + ", " + typeof(TValue).Name + ">.");

                return ref *ValueAt(index);
            }
        }

        /// <summary>Adds an entry; false if the key was already there.</summary>
        public bool TryAdd(in TKey key, in TValue value)
        {
            fixed (TKey* keyBytes = &key)
            {
                if (InlineTable.Find(memory, Capacity, SlotBytes, (byte*)keyBytes, sizeof(TKey)) >= 0)
                    return false;

                byte* slot = InlineTable.Claim(memory, Capacity, SlotBytes, (byte*)keyBytes, sizeof(TKey),
                    "InlineDictionary<" + typeof(TKey).Name + ", " + typeof(TValue).Name + ">");

                *(TKey*)(slot + InlineLayout.HEADER_BYTES) = key;
                *(TValue*)(slot + ValueOffset) = value;
                return true;
            }
        }

        /// <summary>Removes the entry for <paramref name="key"/>; false if it wasn't there.</summary>
        public bool Remove(in TKey key)
        {
            int index = Find(in key);

            if (index < 0)
                return false;

            InlineTable.Remove(memory, index, SlotBytes);
            return true;
        }

        public void Clear() => InlineTable.Clear(memory, Capacity, SlotBytes);

        private int Find(in TKey key)
        {
            fixed (TKey* keyBytes = &key)
                return InlineTable.Find(memory, Capacity, SlotBytes, (byte*)keyBytes, sizeof(TKey));
        }

        private TValue* ValueAt(int index) => (TValue*)(InlineTable.Slot(memory, index, SlotBytes) + ValueOffset);

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

            public (TKey Key, TValue Value) Current
            {
                get
                {
                    byte* slot = InlineTable.Slot(memory, index, SlotBytes);
                    return (*(TKey*)(slot + InlineLayout.HEADER_BYTES), *(TValue*)(slot + ValueOffset));
                }
            }

            public bool MoveNext() => (index = InlineTable.NextOccupied(memory, capacity, SlotBytes, index)) < capacity;
        }
    }
}
