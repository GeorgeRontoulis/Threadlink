namespace Threadlink.Shared
{
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Mathematics;

    [Serializable]
#pragma warning disable IDE1006 // Naming Styles
    public struct ushort2 : IEquatable<ushort2>
#pragma warning restore IDE1006 // Naming Styles
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.HorizontalGroup]
        [Sirenix.OdinInspector.HideLabel]
#endif
        public ushort x;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.HorizontalGroup]
        [Sirenix.OdinInspector.HideLabel]
#endif
        public ushort y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort2(ushort x, ushort y)
        {
            this.x = x;
            this.y = y;
        }

        public static readonly ushort2 zero = new(0, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(ushort2 other) => x == other.x && y == other.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object obj) => obj is ushort2 other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode() => (x << 16) | y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ushort2 a, ushort2 b) => a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ushort2 a, ushort2 b) => !a.Equals(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int2(ushort2 v) => new(v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator uint2(ushort2 v) => new(v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator ushort2(int2 v) => new((ushort)v.x, (ushort)v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator ushort2(uint2 v) => new((ushort)v.x, (ushort)v.y);

        public override readonly string ToString() => $"ushort2({x}, {y})";
    }
}