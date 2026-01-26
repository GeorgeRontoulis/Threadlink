namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;

    public static partial class StatelessRNG
    {
        /// <summary>
        /// <see href="https://rosettacode.org/wiki/Pseudo-random_numbers/Splitmix64">SplitMix64</see>
        /// </summary>
        public static class Hash
        {
            /// <summary>
            /// Hashes the given <paramref name="identity"/> under <see cref="Seed"/>.
            /// This is the only valid path for domain sampling.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static ulong ForSampling(ulong identity) => Mix(identity, true);

            /// <summary>
            /// Intended for entropy composition inside the domains themselves.
            /// Hashes the given <paramref name="identityComponent"/> without applying <see cref="Seed"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong ForIdentity(ulong identityComponent) => Mix(identityComponent, false);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong Mix(ulong input, bool seeded)
            {
                ulong x = seeded ? input ^ Seed : input;

                x ^= x >> 30;
                x *= 0xbf58476d1ce4e5b9UL;
                x ^= x >> 27;
                x *= 0x94d049bb133111ebUL;

                return x ^ (x >> 31);
            }
        }
    }
}
