namespace Threadlink.Deterministic
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A deterministic 2D vector of <see cref="FP"/>. Lengths are exact: <see cref="Magnitude"/> is the true length of
    /// the raw components, rounded once, whatever their scale, so tiny and huge vectors measure and normalize as well as
    /// ordinary ones.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FPVector2 : IEquatable<FPVector2>
    {
        public FP X;
        public FP Y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(FP x, FP y)
        {
            X = x;
            Y = y;
        }

        public static FPVector2 Zero => default;
        public static FPVector2 One => new(FP.One, FP.One);
        public static FPVector2 Up => new(FP.Zero, FP.One);
        public static FPVector2 Down => new(FP.Zero, FP.MinusOne);
        public static FPVector2 Left => new(FP.MinusOne, FP.Zero);
        public static FPVector2 Right => new(FP.One, FP.Zero);

        #region Operators:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator +(FPVector2 a, FPVector2 b) => new(a.X + b.X, a.Y + b.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator -(FPVector2 a, FPVector2 b) => new(a.X - b.X, a.Y - b.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator -(FPVector2 value) => new(-value.X, -value.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator *(FPVector2 value, FP scale) => new(value.X * scale, value.Y * scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator *(FP scale, FPVector2 value) => new(value.X * scale, value.Y * scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator /(FPVector2 value, FP divisor) => new(value.X / divisor, value.Y / divisor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FPVector2 a, FPVector2 b) => a.X == b.X && a.Y == b.Y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FPVector2 a, FPVector2 b) => a.X != b.X || a.Y != b.Y;
        #endregion

        #region Length and direction:
        /// <summary>The squared length, rounded once and saturated.</summary>
        public readonly FP SqrMagnitude
        {
            get
            {
                SumOfSquares(out ulong high, out ulong low);
                return FPVectors.Shift128(high, low);
            }
        }

        /// <summary>The length: the exact root of the squared raw components, rounded to nearest.</summary>
        public readonly FP Magnitude
        {
            get
            {
                SumOfSquares(out ulong high, out ulong low);
                return FPVectors.Root(high, low);
            }
        }

        /// <summary>The unit vector in this direction, each component rounded once, at any scale; zero for the zero vector.</summary>
        public readonly FPVector2 Normalized
        {
            get
            {
                ulong largest = Math.Max(FP.Magnitude(X.RawValue), FP.Magnitude(Y.RawValue));

                if (largest == 0UL)
                    return Zero;

                int shift = FPVectors.NormalizingShift(largest);
                var scaled = new FPVector2(FPVectors.Scale(X, shift), FPVectors.Scale(Y, shift));
                FP length = scaled.Magnitude;

                return new(scaled.X / length, scaled.Y / length);
            }
        }

        private readonly void SumOfSquares(out ulong high, out ulong low)
        {
            high = 0UL;
            low = 0UL;

            // Two squares of at most 2^126 each: no overflow.
            FPVectors.AddSquare(X, ref high, ref low);
            FPVectors.AddSquare(Y, ref high, ref low);
        }
        #endregion

        #region Functions:
        public static FP Dot(FPVector2 a, FPVector2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>The z of the 3D cross product: positive when <paramref name="b"/> is counterclockwise from <paramref name="a"/>.</summary>
        public static FP Cross(FPVector2 a, FPVector2 b) => a.X * b.Y - a.Y * b.X;

        public static FP Distance(FPVector2 a, FPVector2 b) => (a - b).Magnitude;

        public static FP SqrDistance(FPVector2 a, FPVector2 b) => (a - b).SqrMagnitude;

        /// <summary>From <paramref name="a"/> at <paramref name="t"/> = 0 to <paramref name="b"/> at 1, <paramref name="t"/> clamped to [0, 1].</summary>
        public static FPVector2 Lerp(FPVector2 a, FPVector2 b, FP t) => LerpUnclamped(a, b, FP.Clamp01(t));

        public static FPVector2 LerpUnclamped(FPVector2 a, FPVector2 b, FP t) => new(FP.LerpUnclamped(a.X, b.X, t), FP.LerpUnclamped(a.Y, b.Y, t));

        /// <summary><paramref name="current"/> moved in a straight line toward <paramref name="target"/> by at most <paramref name="maxDistance"/>.</summary>
        public static FPVector2 MoveTowards(FPVector2 current, FPVector2 target, FP maxDistance)
        {
            FPVector2 delta = target - current;
            FP distance = delta.Magnitude;

            if (distance <= maxDistance || distance.RawValue == 0L)
                return target;

            // The direction first, which is at most 1, so nothing saturates on the way.
            return current + new FPVector2(delta.X / distance * maxDistance, delta.Y / distance * maxDistance);
        }

        /// <summary><paramref name="value"/> shortened to <paramref name="maxLength"/> if longer.</summary>
        public static FPVector2 ClampMagnitude(FPVector2 value, FP maxLength)
        {
            FP length = value.Magnitude;
            return length <= maxLength ? value : new(value.X / length * maxLength, value.Y / length * maxLength);
        }

        /// <summary><paramref name="value"/> turned a quarter counterclockwise: (−y, x).</summary>
        public static FPVector2 Perpendicular(FPVector2 value) => new(-value.Y, value.X);

        /// <summary><paramref name="value"/> turned counterclockwise by <paramref name="radians"/>.</summary>
        public static FPVector2 Rotate(FPVector2 value, FP radians)
        {
            FP.SinCos(radians, out FP sin, out FP cos);
            return new(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
        }

        /// <summary>The unit vector at <paramref name="radians"/> counterclockwise from the positive x axis.</summary>
        public static FPVector2 FromAngle(FP radians)
        {
            FP.SinCos(radians, out FP sin, out FP cos);
            return new(cos, sin);
        }

        /// <summary>This vector's angle from the positive x axis, in (−π, π]; zero for the zero vector.</summary>
        public readonly FP Angle => FP.Atan2(Y, X);

        /// <summary>The counterclockwise angle from <paramref name="from"/> to <paramref name="to"/>, in (−π, π].</summary>
        public static FP SignedAngle(FPVector2 from, FPVector2 to) => FP.Atan2(Cross(from, to), Dot(from, to));
        #endregion

        public readonly bool Equals(FPVector2 other) => this == other;

        public override readonly bool Equals(object obj) => obj is FPVector2 other && this == other;

        public override readonly int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());

        public override readonly string ToString() => $"({X}, {Y})";
    }

    /// <summary>What the vector types share: exact lengths from 128-bit sums of squared raw components.</summary>
    internal static class FPVectors
    {
        /// <summary>Add <paramref name="value"/>'s squared raw value to a 128-bit sum; false if the sum overflows.</summary>
        public static bool AddSquare(FP value, ref ulong high, ref ulong low)
        {
            ulong magnitude = FP.Magnitude(value.RawValue);
            ulong squareHigh = FP.MultiplyHigh(magnitude, magnitude, out ulong squareLow);

            low = unchecked(low + squareLow);

            // A square's high word is at most 2^62, so the addition wraps exactly when the sum comes out smaller.
            ulong sum = unchecked(high + squareHigh + (low < squareLow ? 1UL : 0UL));
            bool fits = sum >= high;

            high = sum;
            return fits;
        }

        /// <summary>A sum of squared raw components as a squared length: shifted down 32 bits, rounded, saturated.</summary>
        public static FP Shift128(ulong high, ulong low)
        {
            ulong rounded = unchecked(low + (1UL << 31));

            if (rounded < low)
                high++;

            if (high >= 1UL << 31)
                return FP.MaxValue;

            return FP.FromRaw((long)((high << 32) | (rounded >> 32)));
        }

        /// <summary>The root of a sum of squared raw components: a length in raw units, rounded and saturated.</summary>
        public static FP Root(ulong high, ulong low)
        {
            ulong root = FP.SqrtRounded(high, low);
            return root > long.MaxValue ? FP.MaxValue : FP.FromRaw((long)root);
        }

        /// <summary>
        /// The shift that brings the largest component's top bit to bit 60: an exact scale-up for small vectors, whose
        /// length would otherwise round to few raw units, and room below the range limit for large ones. A direction
        /// survives it to the last bit, since the components and the length scale alike.
        /// </summary>
        public static int NormalizingShift(ulong largest) => 60 - (63 - FP.LeadingZeroCount(largest));

        /// <summary><paramref name="value"/> × 2^<paramref name="shift"/>: exact to the left, rounded to nearest to the right, symmetric in sign.</summary>
        public static FP Scale(FP value, int shift)
        {
            if (shift >= 0)
                return FP.FromRaw(value.RawValue << shift);

            int right = -shift;
            long magnitude = (long)((FP.Magnitude(value.RawValue) + (1UL << (right - 1))) >> right);

            return FP.FromRaw(value.RawValue < 0L ? -magnitude : magnitude);
        }
    }
}
