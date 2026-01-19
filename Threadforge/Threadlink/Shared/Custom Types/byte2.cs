namespace Threadlink.Shared
{
    using System;
    using System.Runtime.CompilerServices;

    [Serializable]
#pragma warning disable IDE1006 // Naming Styles
    public struct byte2 : IEquatable<byte2>
#pragma warning restore IDE1006 // Naming Styles
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.HorizontalGroup]
        [Sirenix.OdinInspector.HideLabel]
#endif
        public byte x;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.HorizontalGroup]
        [Sirenix.OdinInspector.HideLabel]
#endif
        public byte y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte2(byte x, byte y)
        {
            this.x = x;
            this.y = y;
        }

        public static readonly byte2 zero = new(0, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(byte2 other) => x == other.x && y == other.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object obj) => obj is byte2 other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode() => (x << 16) | y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(byte2 a, byte2 b) => a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(byte2 a, byte2 b) => !a.Equals(b);

        public override readonly string ToString() => $"byte2({x}, {y})";
    }
}