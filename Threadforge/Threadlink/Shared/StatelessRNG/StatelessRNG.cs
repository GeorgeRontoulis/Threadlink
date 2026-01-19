namespace Threadlink.Shared
{
    using System.Runtime.CompilerServices;

    public readonly partial struct StatelessRNG
    {
        internal readonly uint RootSeed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StatelessRNG(uint rootSeed) => RootSeed = rootSeed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint UInt(in Key key) => Hash5(RootSeed, key.A, key.B, key.C, key.D);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Float01(in Key key) => (UInt(key) & 0x00FFFFFF) * (1f / 16777216f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Range(in Key key, int min, int max) => min + (int)(UInt(key) % (uint)(max - min));
    }
}
