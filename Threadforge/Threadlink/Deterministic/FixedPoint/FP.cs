namespace Threadlink.Deterministic
{
    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A Q32.32 fixed-point number: 32 integer and 32 fractional bits in one <see cref="long"/>, for a range of about
    /// ±2.1 billion at a uniform resolution of 2^-32 (about 2.3e-10). Every operation is integer arithmetic, so the same
    /// inputs give the same bits on every machine, runtime and compiler: networked simulation, replays, lockstep and
    /// procedural generation.
    /// <list type="bullet">
    /// <item>Results round to nearest, ties away from zero, so they are symmetric in sign: <c>(-a) * b == -(a * b)</c>.</item>
    /// <item>Arithmetic saturates at <see cref="MinValue"/> and <see cref="MaxValue"/> instead of wrapping around.</item>
    /// <item>Nothing throws: dividing by zero saturates, and each function says what it returns outside its domain.</item>
    /// </list>
    /// Write values exactly: <c>3</c> (integers convert implicitly), <c>FP.FromFraction(3, 20)</c>, or a literal
    /// converted once, <c>(FP)0.15</c>. Converting a literal or an authored value is deterministic; a float computed at
    /// runtime is not, since the float itself may differ between machines, so it must never flow into simulated state.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct FP : IEquatable<FP>, IComparable<FP>, IComparable, IFormattable
    {
        public const int FRACTIONAL_BITS = 32;

        internal const long ONE = 1L << FRACTIONAL_BITS;
        internal const long HALF = ONE >> 1;
        internal const long FRACTION_MASK = ONE - 1;

        /// <summary>
        /// The underlying integer: the value times 2^32. Serialize, hash and compare this, never a converted float.
        /// </summary>
        public readonly long RawValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FP(long raw) => RawValue = raw;

        #region Constants:
        public static FP Zero
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => default;
        }

        public static FP One
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(ONE);
        }

        public static FP MinusOne
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(-ONE);
        }

        public static FP Half
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(HALF);
        }

        public static FP Two
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(2L * ONE);
        }

        /// <summary>The smallest positive value, 2^-32.</summary>
        public static FP Epsilon
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(1L);
        }

        /// <summary>Just under 2^31: 2147483647.9999999998.</summary>
        public static FP MaxValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(long.MaxValue);
        }

        /// <summary>-2^31.</summary>
        public static FP MinValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(long.MinValue);
        }
        #endregion

        #region Construction and conversion:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromRaw(long raw) => new(raw);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromInt(int value) => new((long)value << FRACTIONAL_BITS);

        /// <summary>
        /// <paramref name="numerator"/> / <paramref name="denominator"/>, rounded to the nearest representable value:
        /// <c>FP.FromFraction(3, 20)</c> is 0.15 as closely as Q32.32 holds it.
        /// </summary>
        public static FP FromFraction(int numerator, int denominator) => FromInt(numerator) / FromInt(denominator);

        /// <summary>Lossless: every <see cref="int"/> is representable.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FP(int value) => FromInt(value);

        /// <summary>Saturates outside ±2^31.</summary>
        public static explicit operator FP(long value)
        {
            if (value > int.MaxValue)
                return MaxValue;

            if (value < int.MinValue)
                return MinValue;

            return new(value << FRACTIONAL_BITS);
        }

        /// <summary>
        /// The nearest representable value, saturating outside the range; NaN is zero. Deterministic for literals and
        /// authored data. Never convert a float computed at runtime into simulated state.
        /// </summary>
        public static explicit operator FP(double value)
        {
            if (double.IsNaN(value))
                return Zero;

            // Scaling by a power of two is exact, and rounding to an integer is exact in IEEE 754.
            double scaled = value * ONE;

            if (scaled >= 9223372036854775807d)
                return MaxValue;

            if (scaled <= -9223372036854775808d)
                return MinValue;

            return new((long)Math.Round(scaled, MidpointRounding.AwayFromZero));
        }

        /// <inheritdoc cref="op_Explicit(double)"/>
        public static explicit operator FP(float value) => (FP)(double)value;

        /// <summary>The integer part, rounded toward negative infinity: <c>(int)(FP)(-1.5)</c> is -2.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator int(FP value) => (int)(value.RawValue >> FRACTIONAL_BITS);

        /// <summary>For presentation only: a float must never flow back into simulated state.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator float(FP value) => (float)(value.RawValue * (1d / ONE));

        /// <summary>For presentation only: a double must never flow back into simulated state.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator double(FP value) => value.RawValue * (1d / ONE);
        #endregion

        #region Arithmetic:
        public static FP operator +(FP a, FP b)
        {
            long sum = unchecked(a.RawValue + b.RawValue);

            // Overflow when both operands have a sign the sum doesn't.
            if (((a.RawValue ^ sum) & (b.RawValue ^ sum)) < 0)
                return a.RawValue < 0 ? MinValue : MaxValue;

            return new(sum);
        }

        public static FP operator -(FP a, FP b)
        {
            long difference = unchecked(a.RawValue - b.RawValue);

            // Overflow when the operands' signs differ and the difference doesn't have a's.
            if (((a.RawValue ^ b.RawValue) & (a.RawValue ^ difference)) < 0)
                return a.RawValue < 0 ? MinValue : MaxValue;

            return new(difference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP value) => value.RawValue == long.MinValue ? MaxValue : new(-value.RawValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator +(FP value) => value;

        /// <summary>The exact 128-bit product, rounded to nearest and saturated.</summary>
        public static FP operator *(FP a, FP b) => new(Multiply(a.RawValue, b.RawValue));

        /// <summary>
        /// The exact 128-bit product truncated toward zero, saturated like <c>*</c>: never larger in magnitude than the exact
        /// product. Conservative geometry needs it, such as advancing a body along its path, where rounding to nearest
        /// could carry it one raw unit past the point it may reach.
        /// </summary>
        public static FP MultiplyTowardZero(FP a, FP b) => new(MultiplyTruncated(a.RawValue, b.RawValue));

        /// <summary>
        /// Rounded to nearest and saturated. Dividing by zero saturates toward the dividend's sign; zero divided by
        /// zero is zero.
        /// </summary>
        public static FP operator /(FP a, FP b) => new(Divide(a.RawValue, b.RawValue));

        /// <summary>The remainder of truncated division, with the dividend's sign, as <c>%</c> on integers. Zero for a zero divisor.</summary>
        public static FP operator %(FP a, FP b)
        {
            long divisor = b.RawValue;

            // |divisor| of one raw unit divides everything, and long.MinValue % -1 would overflow.
            if (divisor == 0L || divisor == 1L || divisor == -1L)
                return Zero;

            return new(a.RawValue % divisor);
        }

        internal static long Multiply(long a, long b)
        {
            bool negative = (a ^ b) < 0;
            ulong high = MultiplyHigh(Magnitude(a), Magnitude(b), out ulong low);

            // Round at bit 32, carrying into the high word.
            ulong rounded = unchecked(low + (1UL << (FRACTIONAL_BITS - 1)));

            if (rounded < low)
                high++;

            // The result is bits 32 to 95 of the product: anything above bit 94 is overflow.
            if (high >= 1UL << 31)
                return negative ? long.MinValue : long.MaxValue;

            long magnitude = (long)((high << 32) | (rounded >> 32));
            return negative ? -magnitude : magnitude;
        }

        /// <summary>Bits 32 to 95 of the exact product, truncated toward zero and saturated.</summary>
        internal static long MultiplyTruncated(long a, long b)
        {
            bool negative = (a ^ b) < 0;
            ulong high = MultiplyHigh(Magnitude(a), Magnitude(b), out ulong low);

            if (high >= 1UL << 31)
                return negative ? long.MinValue : long.MaxValue;

            long magnitude = (long)((high << 32) | (low >> 32));
            return negative ? -magnitude : magnitude;
        }

        /// <summary>|a| × 2^32 / |b| as an exact 128-by-64-bit division, rounded half away from zero.</summary>
        internal static long Divide(long a, long b)
        {
            if (b == 0L)
                return a > 0L ? long.MaxValue : a < 0L ? long.MinValue : 0L;

            bool negative = (a ^ b) < 0;
            ulong dividend = Magnitude(a);
            ulong divisor = Magnitude(b);
            ulong high = dividend >> FRACTIONAL_BITS;

            // A quotient of 2^64 or more.
            if (high >= divisor)
                return negative ? long.MinValue : long.MaxValue;

            ulong quotient = DivRem128(high, dividend << FRACTIONAL_BITS, divisor, out ulong remainder);

            // 2^63 is exactly long.MinValue when negative; anything else past long.MaxValue saturates.
            if (quotient > long.MaxValue)
                return negative ? long.MinValue : long.MaxValue;

            // Past half the divisor, or exactly half: away from zero.
            if (remainder >= divisor - remainder && ++quotient > long.MaxValue)
                return negative ? long.MinValue : long.MaxValue;

            return negative ? -(long)quotient : (long)quotient;
        }
        #endregion

        #region Rounding:
        /// <summary>The largest integer at or below <paramref name="value"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Floor(FP value) => new(value.RawValue & ~FRACTION_MASK);

        /// <summary>The smallest integer at or above <paramref name="value"/>, saturating at <see cref="MaxValue"/>.</summary>
        public static FP Ceiling(FP value)
        {
            long raw = value.RawValue;

            if ((raw & FRACTION_MASK) == 0L)
                return value;

            return raw > long.MaxValue - FRACTION_MASK ? MaxValue : new((raw + FRACTION_MASK) & ~FRACTION_MASK);
        }

        /// <summary>The nearest integer, halves away from zero, saturating at <see cref="MaxValue"/>.</summary>
        public static FP Round(FP value)
        {
            long raw = value.RawValue;

            if (raw >= 0L)
                return raw > long.MaxValue - HALF ? MaxValue : new((raw + HALF) & ~FRACTION_MASK);

            // -(|raw| rounded), so halves go away from zero on this side too.
            ulong magnitude = (Magnitude(raw) + HALF) & ~(ulong)FRACTION_MASK;
            return magnitude >= 1UL << 63 ? MinValue : new(-(long)magnitude);
        }

        /// <summary>The integer part, toward zero.</summary>
        public static FP Truncate(FP value)
        {
            long raw = value.RawValue;

            // A negative value with a fraction truncates to the integer above its floor.
            return raw >= 0L || (raw & FRACTION_MASK) == 0L ? Floor(value) : new((raw & ~FRACTION_MASK) + ONE);
        }

        /// <summary><paramref name="value"/> minus its <see cref="Floor"/>: always in [0, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Frac(FP value) => new(value.RawValue & FRACTION_MASK);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorToInt(FP value) => (int)(value.RawValue >> FRACTIONAL_BITS);

        /// <summary>The smallest integer at or above <paramref name="value"/>; above <see cref="int.MaxValue"/> it saturates.</summary>
        public static int CeilToInt(FP value)
        {
            long ceiling = (value.RawValue >> FRACTIONAL_BITS) + ((value.RawValue & FRACTION_MASK) != 0L ? 1L : 0L);
            return ceiling > int.MaxValue ? int.MaxValue : (int)ceiling;
        }

        /// <summary>The nearest integer, halves away from zero. <see cref="MaxValue"/> rounds to 2^31, which saturates to <see cref="int.MaxValue"/>.</summary>
        public static int RoundToInt(FP value)
        {
            ulong rounded = (Magnitude(value.RawValue) + HALF) >> FRACTIONAL_BITS;

            if (value.RawValue >= 0L)
                return rounded > int.MaxValue ? int.MaxValue : (int)rounded;

            return rounded > 1UL << 31 ? int.MinValue : -(int)(long)rounded;
        }
        #endregion

        #region Comparison and helpers:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Abs(FP value) => value.RawValue >= 0L ? value : -value;

        /// <summary>-1, 0 or 1.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(FP value) => value.RawValue > 0L ? 1 : value.RawValue < 0L ? -1 : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Min(FP a, FP b) => a.RawValue <= b.RawValue ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Max(FP a, FP b) => a.RawValue >= b.RawValue ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp(FP value, FP min, FP max) => value.RawValue < min.RawValue ? min : value.RawValue > max.RawValue ? max : value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp01(FP value) => Clamp(value, Zero, One);

        /// <summary><paramref name="a"/> + (<paramref name="b"/> - <paramref name="a"/>) × <paramref name="t"/>, with <paramref name="t"/> unclamped.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP LerpUnclamped(FP a, FP b, FP t) => a + (b - a) * t;

        /// <summary>From <paramref name="a"/> at <paramref name="t"/> = 0 to <paramref name="b"/> at 1, <paramref name="t"/> clamped to [0, 1].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Lerp(FP a, FP b, FP t) => LerpUnclamped(a, b, Clamp01(t));

        /// <summary>Where <paramref name="value"/> lies from <paramref name="a"/> (0) to <paramref name="b"/> (1), clamped; 0 when they are equal.</summary>
        public static FP InverseLerp(FP a, FP b, FP value) => a == b ? Zero : Clamp01((value - a) / (b - a));

        /// <summary><paramref name="current"/> moved toward <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
        public static FP MoveTowards(FP current, FP target, FP maxDelta)
        {
            FP delta = target - current;
            return Abs(delta) <= maxDelta ? target : current + (delta.RawValue > 0L ? maxDelta : -maxDelta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FP a, FP b) => a.RawValue == b.RawValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FP a, FP b) => a.RawValue != b.RawValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(FP a, FP b) => a.RawValue < b.RawValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(FP a, FP b) => a.RawValue > b.RawValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(FP a, FP b) => a.RawValue <= b.RawValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(FP a, FP b) => a.RawValue >= b.RawValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(FP other) => RawValue == other.RawValue;

        public override bool Equals(object obj) => obj is FP other && RawValue == other.RawValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(FP other) => RawValue.CompareTo(other.RawValue);

        public int CompareTo(object obj) => obj is FP other ? RawValue.CompareTo(other.RawValue) : 1;

        /// <summary>Spelled out rather than <see cref="long.GetHashCode"/>, so no runtime can change it.</summary>
        public override int GetHashCode() => unchecked((int)RawValue ^ (int)(RawValue >> 32));

        public override string ToString() => ((double)this).ToString("0.##########", CultureInfo.InvariantCulture);

        public string ToString(string format, IFormatProvider provider) => ((double)this).ToString(format, provider ?? CultureInfo.InvariantCulture);
        #endregion

        #region Integer helpers:
        /// <summary>|<paramref name="value"/>| as an unsigned integer: 2^63 for <see cref="long.MinValue"/>, which has no positive counterpart.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong Magnitude(long value) => value >= 0L ? (ulong)value : unchecked((ulong)(-(value + 1L)) + 1UL);

        /// <summary>The high 64 bits of <paramref name="a"/> × <paramref name="b"/>, with the low 64 in <paramref name="low"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong MultiplyHigh(ulong a, ulong b, out ulong low)
        {
            ulong aLow = (uint)a, aHigh = a >> 32;
            ulong bLow = (uint)b, bHigh = b >> 32;

            ulong lowLow = aLow * bLow;
            ulong lowHigh = aLow * bHigh;
            ulong highLow = aHigh * bLow;
            ulong highHigh = aHigh * bHigh;

            // At most 3 × (2^32 - 1): no overflow.
            ulong middle = (lowLow >> 32) + (uint)lowHigh + (uint)highLow;

            low = (middle << 32) | (uint)lowLow;
            return highHigh + (lowHigh >> 32) + (highLow >> 32) + (middle >> 32);
        }

        /// <summary>
        /// (<paramref name="high"/> × 2^64 + <paramref name="low"/>) / <paramref name="divisor"/>, for a nonzero divisor
        /// above <paramref name="high"/>, so the quotient fits in 64 bits: Knuth's algorithm D with two 32-bit digits
        /// (Hacker's Delight, <c>divlu</c>), using only 64-bit hardware division.
        /// </summary>
        internal static ulong DivRem128(ulong high, ulong low, ulong divisor, out ulong remainder)
        {
            const ulong BASE = 1UL << 32;

            // Normalize: the divisor's top bit set, which keeps each digit estimate at most two above the true digit.
            int shift = LeadingZeroCount(divisor);

            divisor <<= shift;

            ulong divisorHigh = divisor >> 32;
            ulong divisorLow = (uint)divisor;
            ulong top = shift == 0 ? high : (high << shift) | (low >> (64 - shift));
            ulong bottom = low << shift;
            ulong digit1 = bottom >> 32;
            ulong digit0 = (uint)bottom;

            ulong quotientHigh = top / divisorHigh;
            ulong rest = top - quotientHigh * divisorHigh;

            while (quotientHigh >= BASE || quotientHigh * divisorLow > (rest << 32) + digit1)
            {
                quotientHigh--;
                rest += divisorHigh;

                if (rest >= BASE)
                    break;
            }

            // What is left is below the divisor, so the wrapped arithmetic is exact.
            ulong middle = unchecked((top << 32) + digit1 - quotientHigh * divisor);
            ulong quotientLow = middle / divisorHigh;

            rest = middle - quotientLow * divisorHigh;

            while (quotientLow >= BASE || quotientLow * divisorLow > (rest << 32) + digit0)
            {
                quotientLow--;
                rest += divisorHigh;

                if (rest >= BASE)
                    break;
            }

            remainder = unchecked((middle << 32) + digit0 - quotientLow * divisor) >> shift;
            return (quotientHigh << 32) + quotientLow;
        }

        /// <summary>Leading zero bits of <paramref name="value"/>, 64 for zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int LeadingZeroCount(ulong value) => Bits.LeadingZeroCount(value);

        /// <summary>
        /// round(√(<paramref name="high"/> × 2^64 + <paramref name="low"/>)): the root of a 128-bit integer, rounded to nearest.
        /// A double's square root only seeds the search: the result is then made exact with integer arithmetic, so it is
        /// the same on every machine, whatever the seed.
        /// </summary>
        internal static ulong SqrtRounded(ulong high, ulong low)
        {
            if (high == 0UL && low == 0UL)
                return 0UL;

            ulong root;

            if (high < 1UL << 40)
            {
                // Below 2^104 a double places the root within one unit.
                root = (ulong)Math.Sqrt(high * 18446744073709551616d + low);
            }
            else
            {
                // Above, seed from the root of the number shifted down by an even count, then take one Newton step, which
                // squares the seed's error: from a few thousand units to well under one.
                int shift = (64 - LeadingZeroCount(high) - 40 + 1) >> 1;
                ulong shiftedHigh = high >> (2 * shift);
                ulong shiftedLow = (low >> (2 * shift)) | (high << (64 - 2 * shift));
                ulong top = (ulong)Math.Sqrt(shiftedHigh * 18446744073709551616d + shiftedLow);
                ulong seed = top >= 1UL << (64 - shift) ? ulong.MaxValue : top << shift;

                if (seed <= high)
                {
                    // Only a root at the very top of the range: 2^64 − 1, which the steps below confirm.
                    root = ulong.MaxValue;
                }
                else
                {
                    ulong quotient = DivRem128(high, low, seed, out _);
                    root = (seed >> 1) + (quotient >> 1) + (seed & quotient & 1UL);
                }
            }

            // Exact floor: step until root² ≤ n < (root + 1)².
            while (IsAbove(root, high, low))
                root--;

            while (root != ulong.MaxValue && !IsAbove(root + 1UL, high, low))
                root++;

            // Round to nearest: n − root² > root puts n past (root + ½)².
            ulong squareHigh = MultiplyHigh(root, root, out ulong squareLow);
            ulong restLow = unchecked(low - squareLow);
            ulong restHigh = unchecked(high - squareHigh - (low < squareLow ? 1UL : 0UL));

            if ((restHigh != 0UL || restLow > root) && root != ulong.MaxValue)
                root++;

            return root;
        }

        /// <summary>Whether <paramref name="root"/>² exceeds <paramref name="high"/> × 2^64 + <paramref name="low"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAbove(ulong root, ulong high, ulong low)
        {
            ulong squareHigh = MultiplyHigh(root, root, out ulong squareLow);
            return squareHigh > high || (squareHigh == high && squareLow > low);
        }
        #endregion
    }
}
