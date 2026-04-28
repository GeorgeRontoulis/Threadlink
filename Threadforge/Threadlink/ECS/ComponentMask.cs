namespace Threadlink.ECS
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Mathematics;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ComponentMask
    {
        public ulong a, b, c, d;

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
        public Enumerator GetEnumerator()
        {
            fixed (ulong* ptr = &a)
                return new(ptr);
        }

        public unsafe struct Enumerator
        {
            public int Current { get; private set; }

            private readonly ulong* Blocks; // pointer to a,b,c,d
            private ulong currentBlock;
            private int blockIndex;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(ulong* blocks)
            {
                Blocks = blocks;
                blockIndex = 0;
                currentBlock = blocks[0];
                Current = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                while (blockIndex < 4)
                {
                    if (currentBlock != 0)
                    {
                        int bit = math.tzcnt(currentBlock);
                        currentBlock &= currentBlock - 1;
                        Current = (blockIndex << 6) + bit; // baseOffset = blockIndex * 64
                        return true;
                    }

                    currentBlock = Blocks[++blockIndex]; // advance to next block
                }

                return false;
            }
        }
    }
}