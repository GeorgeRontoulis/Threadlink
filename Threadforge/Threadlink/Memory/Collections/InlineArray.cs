namespace Threadlink.Memory
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A fixed number of <typeparamref name="T"/> slots inside another value's memory, every one always present. A
    /// <see langword="ref struct"/> because it points into memory that can be copied or moved (a simulation frame rolled
    /// back): it can't be stored, boxed or captured, so no pointer outlives the call that made it. See
    /// <see cref="InlineLayout"/>.
    /// </summary>
    public readonly unsafe ref struct InlineArray<T> where T : unmanaged
    {
        private readonly T* data;

        /// <summary>How many slots there are.</summary>
        public int Length { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InlineArray(byte* memory, int length)
        {
            data = (T*)memory;
            Length = length;
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)Length)
                    throw new IndexOutOfRangeException("Index " + index + " is outside an InlineArray<" + typeof(T).Name + "> of " + Length + ".");

                return ref data[index];
            }
        }

        /// <summary>Sets every slot to its default.</summary>
        public void Clear() => NativeAllocator.Clear((byte*)data, (long)Length * sizeof(T));

        public Enumerator GetEnumerator() => new(data, Length);

        public ref struct Enumerator
        {
            private readonly T* data;
            private readonly int length;
            private int index;

            internal Enumerator(T* data, int length)
            {
                this.data = data;
                this.length = length;
                index = -1;
            }

            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref data[index];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++index < length;
        }
    }
}
