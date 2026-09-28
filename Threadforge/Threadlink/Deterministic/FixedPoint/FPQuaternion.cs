namespace Threadlink.Deterministic
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A deterministic rotation. Rotations are unit quaternions: <see cref="FromAxisAngle"/>, <see cref="Normalized"/>,
    /// <see cref="Lerp"/> and <see cref="Slerp"/> return unit length, and <see cref="Rotate"/> and composition assume it.
    /// Renormalize a rotation composed many times over, since each product rounds.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FPQuaternion : IEquatable<FPQuaternion>
    {
        public FP X;
        public FP Y;
        public FP Z;
        public FP W;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPQuaternion(FP x, FP y, FP z, FP w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static FPQuaternion Identity => new(FP.Zero, FP.Zero, FP.Zero, FP.One);

        /// <summary>
        /// Past this dot product the inputs are nearly the same rotation: <see cref="Slerp"/> blends linearly, since
        /// sin θ is too small to divide by.
        /// </summary>
        private static FP NearlyParallel => FP.FromRaw(4292819812L); // round(0.9995 × 2^32)

        /// <summary>A turn of <paramref name="radians"/> around <paramref name="axis"/>, which needn't be unit length. A zero axis is no turn.</summary>
        public static FPQuaternion FromAxisAngle(FPVector3 axis, FP radians)
        {
            FPVector3 unit = axis.Normalized;

            if (unit == FPVector3.Zero)
                return Identity;

            FP.SinCos(radians * FP.Half, out FP sin, out FP cos);
            return new FPQuaternion(unit.X * sin, unit.Y * sin, unit.Z * sin, cos);
        }

        #region Composition and rotation:
        /// <summary>The Hamilton product: <c>a * b</c> rotates by <paramref name="b"/>, then by <paramref name="a"/>.</summary>
        public static FPQuaternion operator *(FPQuaternion a, FPQuaternion b) => new(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator *(FPQuaternion rotation, FPVector3 value) => Rotate(rotation, value);

        /// <summary><paramref name="value"/> rotated by the unit quaternion <paramref name="rotation"/>, without building intermediate quaternions.</summary>
        public static FPVector3 Rotate(FPQuaternion rotation, FPVector3 value)
        {
            var axis = new FPVector3(rotation.X, rotation.Y, rotation.Z);
            FPVector3 twice = FPVector3.Cross(axis, value) * FP.Two;

            return value + twice * rotation.W + FPVector3.Cross(axis, twice);
        }

        /// <summary>The opposite rotation of a unit quaternion.</summary>
        public readonly FPQuaternion Conjugate => new(-X, -Y, -Z, W);

        /// <summary>The opposite rotation of any nonzero quaternion; <see cref="Identity"/> for zero.</summary>
        public readonly FPQuaternion Inverse
        {
            get
            {
                FP squared = SqrMagnitude;
                return squared.RawValue == 0L ? Identity : new(-X / squared, -Y / squared, -Z / squared, W / squared);
            }
        }
        #endregion

        #region Length:
        public static FP Dot(FPQuaternion a, FPQuaternion b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

        /// <summary>The squared length, rounded once and saturated.</summary>
        public readonly FP SqrMagnitude => SumOfSquares(out ulong high, out ulong low) ? FPVectors.Shift128(high, low) : FP.MaxValue;

        /// <summary>The length: the exact root of the squared raw components, rounded to nearest.</summary>
        public readonly FP Magnitude => SumOfSquares(out ulong high, out ulong low) ? FPVectors.Root(high, low) : FP.MaxValue;

        /// <summary>This rotation at unit length; <see cref="Identity"/> for zero.</summary>
        public readonly FPQuaternion Normalized
        {
            get
            {
                ulong largest = Math.Max(Math.Max(FP.Magnitude(X.RawValue), FP.Magnitude(Y.RawValue)), Math.Max(FP.Magnitude(Z.RawValue), FP.Magnitude(W.RawValue)));

                if (largest == 0UL)
                    return Identity;

                int shift = FPVectors.NormalizingShift(largest);
                var scaled = new FPQuaternion(FPVectors.Scale(X, shift), FPVectors.Scale(Y, shift), FPVectors.Scale(Z, shift), FPVectors.Scale(W, shift));
                FP length = scaled.Magnitude;

                return new(scaled.X / length, scaled.Y / length, scaled.Z / length, scaled.W / length);
            }
        }

        /// <summary>False if the 128-bit sum overflowed, which only components all near the range limit can do.</summary>
        private readonly bool SumOfSquares(out ulong high, out ulong low)
        {
            high = 0UL;
            low = 0UL;

            return FPVectors.AddSquare(X, ref high, ref low)
            & FPVectors.AddSquare(Y, ref high, ref low)
            & FPVectors.AddSquare(Z, ref high, ref low)
            & FPVectors.AddSquare(W, ref high, ref low);
        }
        #endregion

        #region Interpolation:
        /// <summary>A linear blend along the shorter way, renormalized ("nlerp"): cheaper than <see cref="Slerp"/>, not constant speed. <paramref name="t"/> is clamped to [0, 1].</summary>
        public static FPQuaternion Lerp(FPQuaternion a, FPQuaternion b, FP t)
        {
            t = FP.Clamp01(t);

            if (Dot(a, b) < FP.Zero)
                b = new(-b.X, -b.Y, -b.Z, -b.W);

            return new FPQuaternion(
                FP.LerpUnclamped(a.X, b.X, t),
                FP.LerpUnclamped(a.Y, b.Y, t),
                FP.LerpUnclamped(a.Z, b.Z, t),
                FP.LerpUnclamped(a.W, b.W, t)).Normalized;
        }

        /// <summary>Spherical interpolation along the shorter way, at constant angular speed. <paramref name="t"/> is clamped to [0, 1].</summary>
        public static FPQuaternion Slerp(FPQuaternion a, FPQuaternion b, FP t)
        {
            t = FP.Clamp01(t);

            FP dot = Dot(a, b);

            if (dot < FP.Zero)
            {
                b = new(-b.X, -b.Y, -b.Z, -b.W);
                dot = -dot;
            }

            if (dot > NearlyParallel)
                return Lerp(a, b, t);

            FP theta = FP.Acos(dot);
            FP sinTheta = FP.Sin(theta);
            FP weightA = FP.Sin((FP.One - t) * theta) / sinTheta;
            FP weightB = FP.Sin(t * theta) / sinTheta;

            return new FPQuaternion(
                a.X * weightA + b.X * weightB,
                a.Y * weightA + b.Y * weightB,
                a.Z * weightA + b.Z * weightB,
                a.W * weightA + b.W * weightB).Normalized;
        }

        /// <summary>The angle of the rotation from <paramref name="a"/> to <paramref name="b"/>, unit quaternions, in [0, π].</summary>
        public static FP Angle(FPQuaternion a, FPQuaternion b) => FP.Acos(FP.Min(FP.Abs(Dot(a, b)), FP.One)) * FP.Two;
        #endregion

        public readonly bool Equals(FPQuaternion other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;

        public override readonly bool Equals(object obj) => obj is FPQuaternion other && Equals(other);

        public static bool operator ==(FPQuaternion a, FPQuaternion b) => a.Equals(b);

        public static bool operator !=(FPQuaternion a, FPQuaternion b) => !a.Equals(b);

        public override readonly int GetHashCode() => unchecked(((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode()) * 397 ^ W.GetHashCode());

        public override readonly string ToString() => $"({X}, {Y}, {Z}, {W})";
    }
}
