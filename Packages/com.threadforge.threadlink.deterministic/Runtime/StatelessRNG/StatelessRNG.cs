namespace Threadforge.Threadlink
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Stateless random numbers for gameplay, simulation, procedural generation and presentation. A
    /// <see cref="Stream"/> is a pure function of a world's seed (carried by an explicit <see cref="Source"/>), a
    /// <see cref="Domain"/> (what the numbers are for) and identity parts (whose, and when), folded in order. The same
    /// inputs give the same numbers on every machine, in any order and on any thread; nothing is shared or advanced
    /// globally, and no project-generated identifiers are required.
    /// <code>
    /// static readonly StatelessRNG.Domain Loot = new StatelessRNG.Domain(1);
    /// var rng = new StatelessRNG.Source(worldSeed);
    /// var loot = rng.CreateStream(Loot, chestID, day);
    /// int gold = loot.Range(10, 50);
    /// </code>
    /// Not for secrets or uniqueness: use <c>System.Security.Cryptography.RandomNumberGenerator</c> and <see cref="Guid"/>.
    /// </summary>
    public static partial class StatelessRNG
    {
        /// <summary>
        /// What a stream's numbers are for. Domains with different ids never share numbers. Keep ids stable once a
        /// world depends on them; a project may generate named accessors that construct these values.
        /// </summary>
        public readonly struct Domain : IEquatable<Domain>
        {
            /// <summary>The domain's 32-bit id, folded into every stream of the domain.</summary>
            public uint Id { get; }

            /// <summary>Creates a domain from an explicit id.</summary>
            public Domain(uint id) => Id = id;

            /// <summary>A domain whose id is the low 32 bits of the name's <see cref="Part"/> hash.</summary>
            public static Domain Named(string name) => new Domain(unchecked((uint)Part(name)));

            /// <inheritdoc />
            public bool Equals(Domain other) => Id == other.Id;

            /// <inheritdoc />
            public override bool Equals(object obj) => obj is Domain other && Id == other.Id;

            /// <inheritdoc />
            public override int GetHashCode() => (int)Id;

            /// <inheritdoc />
            public override string ToString() => "StatelessRNG.Domain(" + Id + ")";
        }

        /// <summary>
        /// A string as an identity part: its 64-bit XxHash64, trimmed and case-insensitive. Hash a fixed string once and
        /// keep the part, rather than hashing it for every stream.
        /// </summary>
        public static ulong Part(string text)
        {
            string value = (text ?? string.Empty).Trim().ToLowerInvariant();
            int count = System.Text.Encoding.UTF8.GetByteCount(value);
            Span<byte> buffer = count <= 256 ? stackalloc byte[count] : new byte[count];
            System.Text.Encoding.UTF8.GetBytes(value, buffer);
            return System.IO.Hashing.XxHash64.HashToUInt64(buffer);
        }

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
        internal static ulong Fold(ulong key, ulong part) =>
            Mix(key ^ Mix(unchecked(part + GAMMA)));
        #endregion
    }
}
