namespace Threadlink.Deterministic
{
    using System;
    using System.IO.Hashing;
    using System.Runtime.CompilerServices;
    using System.Text;

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
            public static ulong ForIdentity(string identityComponent)
            {
                identityComponent = identityComponent.Trim().ToLowerInvariant();

                int byteCount = Encoding.UTF8.GetByteCount(identityComponent);
                Span<byte> buffer = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

                Encoding.UTF8.GetBytes(identityComponent, buffer);

                return XxHash64.HashToUInt64(buffer, unchecked((long)Seed));
            }


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
