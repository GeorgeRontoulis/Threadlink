namespace Threadlink.ECS
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Mathematics;

    [StructLayout(LayoutKind.Sequential)]
    public struct ComponentMask
    {
        public ulong a, b, c, d;

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (a | b | c | d) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int bit)
        {
            int seg = bit >> 6;
            ulong mask = 1UL << (bit & 63);

            switch (seg)
            {
                case 0: a |= mask; break;
                case 1: b |= mask; break;
                case 2: c |= mask; break;
                case 3: d |= mask; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(int bit)
        {
            int seg = bit >> 6;
            ulong mask = ~(1UL << (bit & 63));

            switch (seg)
            {
                case 0: a &= mask; break;
                case 1: b &= mask; break;
                case 2: c &= mask; break;
                case 3: d &= mask; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Has(int bit)
        {
            int seg = bit >> 6;
            ulong mask = 1UL << (bit & 63);

            return seg switch
            {
                0 => (a & mask) != 0,
                1 => (b & mask) != 0,
                2 => (c & mask) != 0,
                3 => (d & mask) != 0,
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Matches(ComponentMask filter)
        {
            return (a & filter.a) == filter.a
            && (b & filter.b) == filter.b
            && (c & filter.c) == filter.c
            && (d & filter.d) == filter.d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool HasAnyFrom(ComponentMask mask)
        {
            return (a & mask.a) != 0 || (b & mask.b) != 0 || (c & mask.c) != 0 || (d & mask.d) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Enumerator GetEnumerator() => new(in this);

        public struct Enumerator
        {
            public int Current { get; private set; }

            // Storing the copies guarantees strict memory safety.
            private readonly ulong _a, _b, _c, _d;
            private ulong currentBlock;
            private int blockIndex;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(in ComponentMask mask)
            {
                _a = mask.a;
                _b = mask.b;
                _c = mask.c;
                _d = mask.d;

                currentBlock = _a;
                blockIndex = 0;
                Current = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                while (blockIndex < 4)
                {
                    if (currentBlock != 0)
                    {
                        // math.tzcnt counts the trailing zeros to find the exact bit index
                        int bit = math.tzcnt(currentBlock);

                        // Clears the lowest set bit instantly without branching
                        currentBlock &= currentBlock - 1;

                        Current = (blockIndex << 6) + bit;
                        return true;
                    }

                    // Move to the next block safely using a jump table
                    blockIndex++;
                    currentBlock = blockIndex switch
                    {
                        1 => _b,
                        2 => _c,
                        3 => _d,
                        _ => 0
                    };
                }

                return false;
            }
        }
    }
}