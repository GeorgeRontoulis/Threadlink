namespace Threadlink.Shared
{
    using System;
    using System.Runtime.CompilerServices;

    public readonly partial struct StatelessRNG
    {
        public readonly struct Key : IEquatable<Key>
        {
            public readonly uint A, B, C, D;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Key(uint a, uint b = 0, uint c = 0, uint d = 0)
            {
                A = a;
                B = b;
                C = c;
                D = d;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(Key other) => A == other.A && B == other.B && C == other.C && D == other.D;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Equals(object obj) => obj is Key k && Equals(k);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode() => HashCode.Combine(A, B, C, D);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(Key x, Key y) => x.Equals(y);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(Key x, Key y) => !x.Equals(y);
        }
    }
}