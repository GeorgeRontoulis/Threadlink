namespace Threadlink.Memory
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Open addressing with linear probing over a fixed slot table, shared by <see cref="InlineHashSet{T}"/> and
    /// <see cref="InlineDictionary{TKey, TValue}"/>. Keys are hashed and compared as raw bytes, never through
    /// <c>GetHashCode</c> or <c>Equals</c>, so a table holds the same bytes on every machine. A slot is an 8-byte state,
    /// then the key at offset 8. Removal leaves a marker that a later insertion reuses, so churn never exhausts the table.
    /// The capacity is the table's size: declare headroom above the most entries expected, since a nearly full table
    /// probes further.
    /// </summary>
    internal static unsafe class InlineTable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte* Slot(byte* memory, int index, int slotBytes) => memory + InlineLayout.HEADER_BYTES + (long)index * slotBytes;

        /// <summary>The slot holding <paramref name="key"/>, or -1.</summary>
        internal static int Find(byte* memory, int capacity, int slotBytes, byte* key, int keySize)
        {
            if (capacity <= 0)
                return -1;

            int index = (int)(MemoryHash.XxHash32(key, keySize) % (uint)capacity);

            for (int probe = 0; probe < capacity; probe++)
            {
                byte* slot = Slot(memory, index, slotBytes);

                if (*slot == InlineLayout.EMPTY)
                    return -1;

                if (*slot == InlineLayout.OCCUPIED && NativeAllocator.Equal(slot + InlineLayout.HEADER_BYTES, key, keySize))
                    return index;

                index = (index + 1) % capacity;
            }

            return -1;
        }

        /// <summary>Claims a slot for a key not in the table and counts it: the first removed slot on its probe path, else the empty one that ends it.</summary>
        internal static byte* Claim(byte* memory, int capacity, int slotBytes, byte* key, int keySize, string owner)
        {
            if (capacity > 0)
            {
                int index = (int)(MemoryHash.XxHash32(key, keySize) % (uint)capacity);
                int reusable = -1;

                for (int probe = 0; probe < capacity; probe++)
                {
                    byte* slot = Slot(memory, index, slotBytes);

                    if (*slot == InlineLayout.EMPTY)
                        return Occupy(memory, reusable >= 0 ? Slot(memory, reusable, slotBytes) : slot);

                    if (*slot == InlineLayout.REMOVED && reusable < 0)
                        reusable = index;

                    index = (index + 1) % capacity;
                }

                if (reusable >= 0)
                    return Occupy(memory, Slot(memory, reusable, slotBytes));
            }

            throw new InvalidOperationException("An " + owner + " holds at most " + capacity + " entries. Raise its declared capacity.");
        }

        internal static void Remove(byte* memory, int index, int slotBytes)
        {
            *Slot(memory, index, slotBytes) = InlineLayout.REMOVED;
            (*(int*)memory)--;
        }

        internal static void Clear(byte* memory, int capacity, int slotBytes)
        {
            for (int i = 0; i < capacity; i++)
                *Slot(memory, i, slotBytes) = InlineLayout.EMPTY;

            *(int*)memory = 0;
        }

        /// <summary>The next occupied slot after <paramref name="index"/>, or <paramref name="capacity"/>.</summary>
        internal static int NextOccupied(byte* memory, int capacity, int slotBytes, int index)
        {
            while (++index < capacity)
            {
                if (*Slot(memory, index, slotBytes) == InlineLayout.OCCUPIED)
                    return index;
            }

            return capacity;
        }

        private static byte* Occupy(byte* memory, byte* slot)
        {
            *slot = InlineLayout.OCCUPIED;
            (*(int*)memory)++;
            return slot;
        }
    }
}
