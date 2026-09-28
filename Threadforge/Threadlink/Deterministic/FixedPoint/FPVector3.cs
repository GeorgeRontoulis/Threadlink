namespace Threadlink.Deterministic
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A deterministic 3D vector of <see cref="FP"/>, with exact lengths like <see cref="FPVector2"/>. Axes follow Unity:
    /// y up, z forward.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FPVector3 : IEquatable<FPVector3>
    {
        public FP X;
        public FP Y;
        public FP Z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3(FP x, FP y, FP z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static FPVector3 Zero => default;
        public static FPVector3 One => new(FP.One, FP.One, FP.One);
        public static FPVector3 Up => new(FP.Zero, FP.One, FP.Zero);
        public static FPVector3 Down => new(FP.Zero, FP.MinusOne, FP.Zero);
        public static FPVector3 Left => new(FP.MinusOne, FP.Zero, FP.Zero);
        public static FPVector3 Right => new(FP.One, FP.Zero, FP.Zero);
        public static FPVector3 Forward => new(FP.Zero, FP.Zero, FP.One);
        public static FPVector3 Back => new(FP.Zero, FP.Zero, FP.MinusOne);

        #region Operators:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator +(FPVector3 a, FPVector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator -(FPVector3 a, FPVector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator -(FPVector3 value) => new(-value.X, -value.Y, -value.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator *(FPVector3 value, FP scale) => new(value.X * scale, value.Y * scale, value.Z * scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator *(FP scale, FPVector3 value) => new(value.X * scale, value.Y * scale, value.Z * scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator /(FPVector3 value, FP divisor) => new(value.X / divisor, value.Y / divisor, value.Z / divisor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FPVector3 a, FPVector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FPVector3 a, FPVector3 b) => !(a == b);
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
        public readonly FPVector3 Normalized
        {
            get
            {
                ulong largest = Math.Max(FP.Magnitude(X.RawValue), Math.Max(FP.Magnitude(Y.RawValue), FP.Magnitude(Z.RawValue)));

                if (largest == 0UL)
                    return Zero;

                int shift = FPVectors.NormalizingShift(largest);
                var scaled = new FPVector3(FPVectors.Scale(X, shift), FPVectors.Scale(Y, shift), FPVectors.Scale(Z, shift));
                FP length = scaled.Magnitude;

                return new(scaled.X / length, scaled.Y / length, scaled.Z / length);
            }
        }

        private readonly void SumOfSquares(out ulong high, out ulong low)
        {
            high = 0UL;
            low = 0UL;

            // Three squares of at most 2^126 each: no overflow.
            FPVectors.AddSquare(X, ref high, ref low);
            FPVectors.AddSquare(Y, ref high, ref low);
            FPVectors.AddSquare(Z, ref high, ref low);
        }
        #endregion

        #region Functions:
        public static FP Dot(FPVector3 a, FPVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static FPVector3 Cross(FPVector3 a, FPVector3 b) => new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        public static FP Distance(FPVector3 a, FPVector3 b) => (a - b).Magnitude;

        public static FP SqrDistance(FPVector3 a, FPVector3 b) => (a - b).SqrMagnitude;

        /// <summary>From <paramref name="a"/> at <paramref name="t"/> = 0 to <paramref name="b"/> at 1, <paramref name="t"/> clamped to [0, 1].</summary>
        public static FPVector3 Lerp(FPVector3 a, FPVector3 b, FP t) => LerpUnclamped(a, b, FP.Clamp01(t));

        public static FPVector3 LerpUnclamped(FPVector3 a, FPVector3 b, FP t) => new(
            FP.LerpUnclamped(a.X, b.X, t),
            FP.LerpUnclamped(a.Y, b.Y, t),
            FP.LerpUnclamped(a.Z, b.Z, t));

        /// <summary><paramref name="current"/> moved in a straight line toward <paramref name="target"/> by at most <paramref name="maxDistance"/>.</summary>
        public static FPVector3 MoveTowards(FPVector3 current, FPVector3 target, FP maxDistance)
        {
            FPVector3 delta = target - current;
            FP distance = delta.Magnitude;

            if (distance <= maxDistance || distance.RawValue == 0L)
                return target;

            // The direction first, which is at most 1, so nothing saturates on the way.
            return current + new FPVector3(delta.X / distance * maxDistance, delta.Y / distance * maxDistance, delta.Z / distance * maxDistance);
        }

        /// <summary><paramref name="value"/> shortened to <paramref name="maxLength"/> if longer.</summary>
        public static FPVector3 ClampMagnitude(FPVector3 value, FP maxLength)
        {
            FP length = value.Magnitude;

            return length <= maxLength
            ? value
            : new(value.X / length * maxLength, value.Y / length * maxLength, value.Z / length * maxLength);
        }

        /// <summary>The unsigned angle between <paramref name="a"/> and <paramref name="b"/>, in [0, π].</summary>
        public static FP Angle(FPVector3 a, FPVector3 b) => FP.Atan2(Cross(a, b).Magnitude, Dot(a, b));

        /// <summary><paramref name="value"/>'s projection onto <paramref name="onNormal"/>; zero for a zero normal.</summary>
        public static FPVector3 Project(FPVector3 value, FPVector3 onNormal)
        {
            FPVector3 unit = onNormal.Normalized;
            return unit * Dot(value, unit);
        }

        /// <summary><paramref name="value"/> with its component along <paramref name="planeNormal"/> removed.</summary>
        public static FPVector3 ProjectOnPlane(FPVector3 value, FPVector3 planeNormal) => value - Project(value, planeNormal);
        #endregion

        public readonly bool Equals(FPVector3 other) => this == other;

        public override readonly bool Equals(object obj) => obj is FPVector3 other && this == other;

        public override readonly int GetHashCode() => unchecked((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode());

        public override readonly string ToString() => $"({X}, {Y}, {Z})";
    }
}
