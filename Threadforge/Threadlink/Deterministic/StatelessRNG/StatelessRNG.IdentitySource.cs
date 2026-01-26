namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;

    public static partial class StatelessRNG
    {
        /// <summary>
        /// Deterministic, local identity source.
        /// Produces a sequence of 64-bit values derived from abstract, fixed identity components and the <see cref="Seed"/>.
        /// <see cref="SourceFrom{C}(Domains, in C)"/> is a helper method encouraging the use of domains and local contexts
        /// as semantic constructs to ensure your call sites remain compact, human-readable and easy to debug.
        /// </summary>
        public partial struct IdentitySource
        {
            private readonly ulong Identity;
            private uint index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal IdentitySource(ulong identity)
            {
                Identity = identity;
                index = 0;
            }

            /// <summary>
            /// Returns the next deterministic identity value.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ulong Next()
            {
                var identity = Hash.ForSampling(Identity ^ Hash.ForIdentity(index));

                ++index;
                return identity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IdentitySource SourceFrom(Domains domain)
        {
            return new(Hash.ForIdentity((byte)domain));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IdentitySource SourceFrom<C>(Domains domain, in C context) where C : struct, IContext
        {
            return new(Hash.ForIdentity((byte)domain) ^ context.Identity);
        }
    }
}
