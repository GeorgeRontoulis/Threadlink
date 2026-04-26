namespace Threadlink.Deterministic
{
    using Shared;
    using System;
    using System.Runtime.CompilerServices;

    public static partial class StatelessRNG
    {
        /// <summary>
        /// <see href="https://rosettacode.org/wiki/Pseudo-random_numbers/Splitmix64">SplitMix64</see> for numbers.
        /// <para></para>
        /// <see cref="System.IO.Hashing.XxHash64"/> for <see langword="string"/>s.
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

            /// <summary>
            /// Intended for entropy composition inside the domains themselves.
            /// Hashes the given <paramref name="identityComponent"/> without applying <see cref="Seed"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong ForIdentity(string identityComponent)
            {
                return HashFunctions.ToXxHash64(identityComponent, Convert.ToInt64(Seed));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong Mix(ulong input, bool seeded)
            {
                return seeded ? HashFunctions.ToSplitMix64(input, Seed) : HashFunctions.ToSplitMix64(input);
            }
        }
    }
}
