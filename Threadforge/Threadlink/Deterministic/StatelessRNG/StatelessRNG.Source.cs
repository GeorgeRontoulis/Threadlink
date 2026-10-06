namespace Threadlink.Deterministic
{
    using System;
    using Threadlink.Generated;

    public static partial class StatelessRNG
    {
        /// <summary>
        /// The streams of one seed. Each world carries its own, so several worlds in one process never share randomness:
        /// a host's authoritative simulation and its prediction, two worlds in a test, a world and a preview of another.
        /// <code>
        /// var rng = new StatelessRNG.Source(worldSeed);
        /// var loot = rng.CreateStream(ThreadlinkIDs.StatelessRNG.Domains.Loot, chestID, day);
        /// </code>
        /// For the same seed it gives exactly the streams the static <see cref="StatelessRNG"/> methods give for
        /// <see cref="StatelessRNG.Seed"/>. A <see langword="default"/> source is the source of seed 0.
        /// </summary>
        public readonly struct Source : IEquatable<Source>
        {
            /// <summary>The seed every stream of this source starts from.</summary>
            public ulong Seed { get; }

            public Source(ulong seed) => Seed = seed;

            /// <summary>The stream for <paramref name="domain"/> alone.</summary>
            public Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain) => new(Start(domain));

            /// <summary>The stream for <paramref name="domain"/> and one identity part, such as an entity's id.</summary>
            public Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain, ulong a) => new(Fold(Start(domain), a));

            /// <summary>The stream for <paramref name="domain"/> and two identity parts, in this order: an entity and a tick.</summary>
            public Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain, ulong a, ulong b) => new(Fold(Fold(Start(domain), a), b));

            /// <summary>The stream for <paramref name="domain"/> and three identity parts, in order.</summary>
            public Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain, ulong a, ulong b, ulong c)
            {
                return new(Fold(Fold(Fold(Start(domain), a), b), c));
            }

            /// <summary>The stream for <paramref name="domain"/> and any number of identity parts, in order: <c>stackalloc ulong[] { … }</c> allocates nothing.</summary>
            public Stream CreateStream(ThreadlinkIDs.StatelessRNG.Domains domain, ReadOnlySpan<ulong> parts)
            {
                ulong key = Start(domain);

                for (int i = 0; i < parts.Length; i++)
                    key = Fold(key, parts[i]);

                return new(key);
            }

            /// <summary>The stream for <paramref name="domain"/> and a context's parts, which it adds in its own order.</summary>
            public Stream CreateStream<TContext>(ThreadlinkIDs.StatelessRNG.Domains domain, in TContext context) where TContext : struct, IContext
            {
                var identity = new Identity(Start(domain));

                context.Compose(ref identity);
                return new(identity.Key);
            }

            /// <summary>The seed, then the domain's full 32-bit id.</summary>
            private ulong Start(ThreadlinkIDs.StatelessRNG.Domains domain) => Fold(Mix(unchecked(Seed + GAMMA)), unchecked((uint)domain));

            public bool Equals(Source other) => Seed == other.Seed;

            public override bool Equals(object obj) => obj is Source other && Seed == other.Seed;

            public override int GetHashCode() => Seed.GetHashCode();

            public override string ToString() => "StatelessRNG.Source(" + Seed.ToString("X16") + ")";
        }
    }
}
