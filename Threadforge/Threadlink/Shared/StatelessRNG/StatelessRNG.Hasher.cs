namespace Threadlink.Shared
{
    using System.Runtime.CompilerServices;

    public readonly partial struct StatelessRNG
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash5(uint seed, uint a, uint b, uint c, uint d)
        {
            uint h = seed;
            h ^= Mix(a);
            h ^= Mix(b);
            h ^= Mix(c);
            h ^= Mix(d);
            return Mix(h);
        }
    }
}
