namespace Threadlink.Deterministic
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Shared;

    public static partial class StatelessRNG
    {
        public partial struct Scope : IDisposable
        {
            private readonly ulong Identity;

            private ulong sample;
            private uint state;

            public readonly void Dispose() { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Scope(ulong identity)
            {
                Identity = identity;
                state = 0;
                sample = Hash.ForSampling(Identity ^ Hash.ForIdentity(state));
            }

            /// <summary>
            /// Advances to the next deterministic state.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Scope Advance()
            {
                ++state;
                sample = Hash.ForSampling(Identity ^ Hash.ForIdentity(state));
                return this;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Scope CreateScope(ThreadlinkIDs.StatelessRNG.Domains domain)
        {
            return new(Hash.ForIdentity((byte)domain));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Scope CreateScope<C>(ThreadlinkIDs.StatelessRNG.Domains domain, in C context) where C : unmanaged, IContext
        {
            return new(Hash.ForIdentity((byte)domain) ^ context.Identity);
        }
    }
}
