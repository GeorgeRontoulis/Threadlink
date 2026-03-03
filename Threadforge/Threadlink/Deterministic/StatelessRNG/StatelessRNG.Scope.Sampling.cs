namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;

    public static partial class StatelessRNG
    {
        public partial struct Scope
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int Range(int min, int max)
            {
                return min + (int)(sample % (uint)(max - min));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int Index(int count)
            {
                return (int)(sample % (uint)count);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool Boolean()
            {
                // 0.5 threshold, branchless
                return (sample & 1UL) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool Probability(DFP probability)
            {
                return Float01() < probability;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly DFP Float01()
            {
                uint mantissa = (uint)(sample >> 41);

                uint raw = 0x3F800000u | mantissa;
                return DFP.FromRaw(raw) - DFP.One;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly DFP Range(DFP min, DFP max)
            {
                return min + (max - min) * Float01();
            }
        }
    }
}
