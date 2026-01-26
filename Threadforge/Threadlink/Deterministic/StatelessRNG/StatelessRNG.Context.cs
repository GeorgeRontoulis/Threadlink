namespace Threadlink.Deterministic
{
    public static partial class StatelessRNG
    {
        /// <summary>
        /// A local set of abstract identity components.
        /// <para></para>
        /// The components must be hashed using <see cref="Hash.ForIdentity(ulong)"/>
        /// inside the <see cref="Identity"/> getter.
        /// <para></para>
        /// The generated hashes must then be XOR-ed to produce a final identity value.
        /// </summary>
        public interface IContext
        {
            /// <summary>
            /// Compose a 64-bit sequence containing the identity
            /// produced by this <see cref="IContext"/>'s components.
            /// <para></para>
            /// Contexts must be <see langword="readonly"/> <see langword="struct"/>s,
            /// as their lifetime is ephemeral and only tied to the current frame.
            /// </summary>
            public ulong Identity { get; }
        }
    }
}
