namespace Threadlink.Deterministic
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Generated;
    using Threadlink.Shared;

    /// <summary>
    /// The framework's random numbers: the one source of randomness for gameplay, simulation, procedural generation and
    /// presentation alike. A <see cref="Stream"/> is a pure function of the world's <see cref="Seed"/>, a domain (what
    /// the numbers are for) and identity parts (whose, and when: an entity, a tick, a map cell), folded in order. The
    /// same inputs give the same numbers on every machine, in any order and on any thread, however many draws happen
    /// elsewhere: nothing is shared or advanced globally.
    /// <code>
    /// var loot = StatelessRNG.CreateStream(ThreadlinkIDs.StatelessRNG.Domains.Loot, chestID, day);
    ///
    /// int gold = loot.Range(10, 50);
    /// bool rare = loot.Chance(FP.FromFraction(1, 20));
    /// </code>
    /// Not for secrets or uniqueness: session tokens and nonces come from
    /// <c>System.Security.Cryptography.RandomNumberGenerator</c>, and unique ids from <see cref="Guid"/>.
    /// </summary>
    public static partial class StatelessRNG
    {
        /// <summary>
        /// Every stream starts from it. Choose it once per world and keep it with the world's save; every machine
        /// simulating the world must use the same one.
        /// </summary>
        public static ulong Seed { get; private set; }

        public static void SetSeed(ulong seed) => Seed = seed;

        #region Streams:
        /// <summary>The stream for <paramref name="domain"/> alone.</summary>
        public static Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain) => new(Start(domain));

        /// <summary>The stream for <paramref name="domain"/> and one identity part, such as an entity's id.</summary>
        public static Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain, ulong a) => new(Fold(Start(domain), a));

        /// <summary>The stream for <paramref name="domain"/> and two identity parts, in this order: an entity and a tick.</summary>
        public static Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain, ulong a, ulong b) => new(Fold(Fold(Start(domain), a), b));

        /// <summary>The stream for <paramref name="domain"/> and three identity parts, in this order: a cell's x, y and a day.</summary>
        public static Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain, ulong a, ulong b, ulong c)
        {
            return new(Fold(Fold(Fold(Start(domain), a), b), c));
        }

        /// <summary>The stream for <paramref name="domain"/> and any number of identity parts, in order: <c>stackalloc ulong[] { … }</c> allocates nothing.</summary>
        public static Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain, ReadOnlySpan<ulong> parts)
        {
            ulong key = Start(domain);

            for (int i = 0; i < parts.Length; i++)
                key = Fold(key, parts[i]);

            return new(key);
        }

        /// <summary>The stream for <paramref name="domain"/> and a context's parts, which it adds in its own order.</summary>
        public static Stream CreateStream<TContext>(ThreadlinkIDs.StatelessRNG.Domains domain, in TContext context) where TContext : struct, IContext
        {
            var identity = new Identity(Start(domain));

            context.Compose(ref identity);
            return new(identity.Key);
        }

        /// <summary>
        /// A string as an identity part: its 64-bit hash, as Threadlink hashes names (trimmed, and case-insensitive). Hash
        /// a fixed string once and keep the part, rather than hashing it for every stream.
        /// </summary>
        public static ulong Part(string text) => HashFunctions.ToXxHash64(text ?? string.Empty);
        #endregion

        #region Mixing:
        // SplitMix64's increment (the golden ratio × 2^64) and finalizer (Stafford's variant 13), which carries every input
        // bit into every output bit. A stream is SplitMix64 started at its identity's key, which passes BigCrush.
        private const ulong GAMMA = 0x9E3779B97F4A7C15UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong Mix(ulong z)
        {
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }

        /// <summary>
        /// One identity part folded into a key. The part is mixed on its own first, so structured parts (0, 1, 2, …)
        /// spread over all 64 bits and a zero part still changes the key; mixing again after each part makes the order
        /// matter and keeps equal parts from cancelling each other.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong Fold(ulong key, ulong part) => Mix(key ^ Mix(unchecked(part + GAMMA)));

        /// <summary>The seed, then the domain's full 32-bit id.</summary>
        private static ulong Start(ThreadlinkIDs.StatelessRNG.Domains domain) => Fold(Mix(unchecked(Seed + GAMMA)), unchecked((uint)domain));
        #endregion
    }
}
