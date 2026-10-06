namespace Threadlink.Memory
{
    using System;

    /// <summary>
    /// The byte layout of the inline collections: views over a fixed number of slots inside another value's memory,
    /// such as a simulation component, so a collection is copied, hashed and saved with the bytes that hold it.
    /// <list type="bullet">
    /// <item>Every header, slot and value starts on an 8-byte boundary. The most a generated field needs is 8, and a
    /// value's alignment can't be recovered from its size, so everything rounds up to 8 instead.</item>
    /// <item>A list, queue, stack, set or dictionary starts with an 8-byte header holding its count (a queue adds its
    /// head); an array has no header.</item>
    /// <item>A set or dictionary slot is an 8-byte state (empty, occupied or removed), then the key, then the value.</item>
    /// </list>
    /// A layout generator must produce exactly these sizes; <see cref="ArrayBytes"/> and its siblings are the reference.
    /// </summary>
    public static class InlineLayout
    {
        /// <summary>The header before a counted collection's slots.</summary>
        public const int HEADER_BYTES = 8;

        internal const byte EMPTY = 0;
        internal const byte OCCUPIED = 1;
        internal const byte REMOVED = 2;

        /// <summary><paramref name="size"/> rounded up to a multiple of 8.</summary>
        public static int Align(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            return checked(size + 7) & ~7;
        }

        /// <summary>Bytes of an array of <paramref name="capacity"/> elements.</summary>
        public static int ArrayBytes(int capacity, int elementSize) => Bytes(0, capacity, elementSize);

        /// <summary>Bytes of a list, queue or stack of <paramref name="capacity"/> elements.</summary>
        public static int SequenceBytes(int capacity, int elementSize) => Bytes(HEADER_BYTES, capacity, elementSize);

        /// <summary>Bytes of one hash set slot.</summary>
        public static int SetSlotBytes(int elementSize) => checked(HEADER_BYTES + Align(elementSize));

        /// <summary>Bytes of a hash set of <paramref name="capacity"/> slots.</summary>
        public static int SetBytes(int capacity, int elementSize) => Bytes(HEADER_BYTES, capacity, SetSlotBytes(elementSize));

        /// <summary>Bytes of one dictionary slot.</summary>
        public static int DictionarySlotBytes(int keySize, int valueSize) => checked(HEADER_BYTES + Align(keySize) + Align(valueSize));

        /// <summary>Bytes of a dictionary of <paramref name="capacity"/> slots.</summary>
        public static int DictionaryBytes(int capacity, int keySize, int valueSize) => Bytes(HEADER_BYTES, capacity, DictionarySlotBytes(keySize, valueSize));

        private static int Bytes(int header, int capacity, int slotSize)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            if (slotSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotSize));

            return checked(header + capacity * slotSize);
        }
    }
}
