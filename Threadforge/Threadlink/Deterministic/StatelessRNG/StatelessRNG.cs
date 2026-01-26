namespace Threadlink.Deterministic
{
    /// <summary>
    /// Deterministic RNG designed to act as the singular entry point for all randomization.
    /// <para></para>
    /// Given a single global seed and the same inputs, any module using this RNG can have
    /// its entire timeline deterministically reconstructed.
    /// <para></para>
    /// This makes it especially suitable for replay systems or other cases where reproducibility
    /// is important.
    /// </summary>
    public static partial class StatelessRNG
    {
        /// <summary>
        /// The global seed of the RNG.
        /// </summary>
        public static ulong Seed { get; private set; }

        private static bool Initialized { get; set; } = false;

        /// <summary>
        /// Initialize the RNG with the specified seed.
        /// </summary>
        public static void Boot(ulong seed)
        {
            if (Initialized) return;

            Seed = seed;
        }
    }
}
