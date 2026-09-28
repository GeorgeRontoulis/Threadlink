namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;

    public static partial class StatelessRNG
    {
        /// <summary>
        /// Identity parts that belong together, added in one place and always in the same order: an attack's attacker,
        /// target and tick. Keep contexts <see langword="readonly"/> structs made for the call.
        /// <code>
        /// public readonly struct AttackRoll : StatelessRNG.IContext
        /// {
        ///     public readonly uint Attacker, Target, Tick;
        ///
        ///     public void Compose(ref StatelessRNG.Identity identity)
        ///     {
        ///         identity.Add(Attacker);
        ///         identity.Add(Target);
        ///         identity.Add(Tick);
        ///     }
        /// }
        /// </code>
        /// </summary>
        public interface IContext
        {
            void Compose(ref Identity identity);
        }

        /// <summary>The identity a stream is made from, built one part at a time. Order matters: (a, b) and (b, a) differ.</summary>
        public struct Identity
        {
            internal ulong Key { get; private set; }

            internal Identity(ulong key) => Key = key;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(ulong part) => Key = Fold(Key, part);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(long part) => Add(unchecked((ulong)part));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(uint part) => Add((ulong)part);

            /// <summary>Sign-extended, so −1 and <see cref="uint.MaxValue"/> are different parts.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(int part) => Add(unchecked((ulong)(long)part));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(FP part) => Add(part.RawValue);

            /// <summary>A string's hash (<see cref="Part"/>): hash a fixed string once and add the part instead where it repeats.</summary>
            public void Add(string part) => Add(Part(part));
        }
    }
}
