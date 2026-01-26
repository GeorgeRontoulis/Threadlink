namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;

    public static partial class StatelessRNG
    {
        public partial struct IdentitySource
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Range(int min, int max)
            {
                return min + (int)(Next() % (uint)(max - min));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Index(int count)
            {
                return (int)(Next() % (uint)count);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Boolean()
            {
                // 0.5 threshold, branchless
                return (Next() & 1UL) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Probability(DFP probability)
            {
                return Float01() < probability;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DFP Float01()
            {
                uint mantissa = (uint)(Next() >> 41);

                uint raw = 0x3F800000u | mantissa;
                return DFP.FromRaw(raw) - DFP.One;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DFP Range(DFP min, DFP max)
            {
                return min + (max - min) * Float01();
            }
        }
    }
}
