namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Roots, trigonometry, exponentials and logarithms. Trigonometry, <see cref="Exp"/> and <see cref="Log"/> run in
    /// Q3.61, 29 bits finer than the result, and round to Q32.32 once at the end, so each is within about one raw unit
    /// (2.3e-10) of the true value. <see cref="Sqrt"/> is exact: the true root, rounded to nearest.
    /// </summary>
    public readonly partial struct FP
    {
        #region Constants:
        // round(value × 2^32), from 80-digit references.
        private const long PI_RAW = 13493037705L;
        private const long TWO_PI_RAW = 26986075409L;
        private const long HALF_PI_RAW = 6746518852L;
        private const long QUARTER_PI_RAW = 3373259426L;
        private const long E_RAW = 11674931555L;
        private const long LN2_RAW = 2977044472L;
        private const long DEG2RAD_RAW = 74961321L;
        private const long RAD2DEG_RAW = 246083499208L;

        public static FP Pi
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(PI_RAW);
        }

        public static FP TwoPi
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(TWO_PI_RAW);
        }

        public static FP PiOver2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(HALF_PI_RAW);
        }

        public static FP PiOver4
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(QUARTER_PI_RAW);
        }

        public static FP E
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(E_RAW);
        }

        public static FP Ln2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(LN2_RAW);
        }

        /// <summary>Radians per degree: <c>degrees * FP.Deg2Rad</c>.</summary>
        public static FP Deg2Rad
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(DEG2RAD_RAW);
        }

        /// <summary>Degrees per radian: <c>radians * FP.Rad2Deg</c>.</summary>
        public static FP Rad2Deg
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(RAD2DEG_RAW);
        }
        #endregion

        #region Internal precision (Q3.61):
        private const int Q61_BITS = 61;
        private const int Q61_SHIFT = Q61_BITS - FRACTIONAL_BITS;
        private const long ONE_Q61 = 1L << Q61_BITS;

        // round(value × 2^61), from 100-digit references.
        private const long PI_Q61 = 7244019458077122842L;
        private const long HALF_PI_Q61 = 3622009729038561421L;
        private const long PI_OVER_6_Q61 = 1207336576346187140L;
        private const long SQRT3_Q61 = 3993837246235628775L;
        private const long TAN_PI_OVER_12_Q61 = 617848772191759129L;
        private const long SQRT2_Q61 = 3260954456333195553L;
        private const long INV_LN2_Q61 = 3326628274461080623L;

        // π/2 and ln 2 times 2^32: the integer part, and the fraction times 2^64. Range reduction removes a period's
        // fraction too, so it stays exact however many periods an argument spans.
        private const long HALF_PI_RAW_FLOOR = 6746518852L;
        private const ulong HALF_PI_FRACTION = 4814775065449907479UL;
        private const long LN2_RAW_FLOOR = 2977044471L;
        private const ulong LN2_FRACTION = 15118436252839555992UL;

        private const long INV_FACTORIAL_2 = 1152921504606846976L;
        private const long INV_FACTORIAL_3 = 384307168202282325L;
        private const long INV_FACTORIAL_4 = 96076792050570581L;
        private const long INV_FACTORIAL_5 = 19215358410114116L;
        private const long INV_FACTORIAL_6 = 3202559735019019L;
        private const long INV_FACTORIAL_7 = 457508533574146L;
        private const long INV_FACTORIAL_8 = 57188566696768L;
        private const long INV_FACTORIAL_9 = 6354285188530L;
        private const long INV_FACTORIAL_10 = 635428518853L;
        private const long INV_FACTORIAL_11 = 57766228987L;
        private const long INV_FACTORIAL_12 = 4813852416L;
        private const long INV_FACTORIAL_13 = 370296340L;

        private const long INV_3 = 768614336404564651L;
        private const long INV_5 = 461168601842738790L;
        private const long INV_7 = 329406144173384850L;
        private const long INV_9 = 256204778801521550L;
        private const long INV_11 = 209622091746699450L;
        private const long INV_13 = 177372539170284150L;
        private const long INV_15 = 153722867280912930L;
        private const long INV_17 = 135637824071393762L;

        /// <summary>round(<paramref name="a"/> × <paramref name="b"/> / 2^61), symmetric in sign, for a product below 4.</summary>
        private static long Mul61(long a, long b)
        {
            bool negative = (a ^ b) < 0;
            ulong high = MultiplyHigh(Magnitude(a), Magnitude(b), out ulong low);
            ulong rounded = unchecked(low + (1UL << (Q61_BITS - 1)));

            if (rounded < low)
                high++;

            long magnitude = (long)((high << (64 - Q61_BITS)) | (rounded >> Q61_BITS));
            return negative ? -magnitude : magnitude;
        }

        /// <summary>round(<paramref name="a"/> × 2^61 / <paramref name="b"/>), for a nonzero divisor and a quotient below 4.</summary>
        private static long Div61(long a, long b)
        {
            bool negative = (a ^ b) < 0;
            long quotient = Ratio61(Magnitude(a), Magnitude(b));
            return negative ? -quotient : quotient;
        }

        /// <summary>round(<paramref name="a"/> × 2^61 / <paramref name="b"/>) on magnitudes, for a quotient below 4.</summary>
        private static long Ratio61(ulong a, ulong b)
        {
            ulong quotient = DivRem128(a >> (64 - Q61_BITS), a << Q61_BITS, b, out ulong remainder);

            if (remainder >= b - remainder)
                quotient++;

            return (long)quotient;
        }

        /// <summary>A Q3.61 value rounded to Q32.32, halves away from zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long RoundQ61(long value)
        {
            long magnitude = (long)((Magnitude(value) + (1UL << (Q61_SHIFT - 1))) >> Q61_SHIFT);
            return value < 0L ? -magnitude : magnitude;
        }

        /// <summary>
        /// <paramref name="periods"/> × <paramref name="fraction"/> / 2^64 raw units, in Q3.61: what a floor constant
        /// leaves out, over that many periods.
        /// </summary>
        private static long FractionTimes(long periods, ulong fraction)
        {
            if (periods == 0L)
                return 0L;

            ulong high = MultiplyHigh(Magnitude(periods), fraction, out ulong low);
            ulong rounded = unchecked(low + (1UL << 34));

            if (rounded < low)
                high++;

            long correction = (long)((high << Q61_SHIFT) | (rounded >> 35));
            return periods < 0L ? -correction : correction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ClampUnit(long raw) => raw > ONE ? ONE : raw < -ONE ? -ONE : raw;
        #endregion

        #region Roots:
        /// <summary>√<paramref name="value"/>, the true root rounded to nearest. Zero for negative values, which are usually rounding residue.</summary>
        public static FP Sqrt(FP value)
        {
            long raw = value.RawValue;

            if (raw <= 0L)
                return Zero;

            // √(raw / 2^32) × 2^32 = √(raw × 2^32).
            return new((long)SqrtRounded((ulong)raw >> FRACTIONAL_BITS, (ulong)raw << FRACTIONAL_BITS));
        }
        #endregion

        #region Trigonometry:
        public static FP Sin(FP radians)
        {
            long r = ReduceQuarterTurns(radians.RawValue, out int quadrant);
            long value = (quadrant & 1) == 0 ? SinQ61(r) : CosQ61(r);

            // sin(r + kπ/2): sin r, cos r, −sin r, −cos r.
            return new(ClampUnit(RoundQ61((quadrant & 2) == 0 ? value : -value)));
        }

        public static FP Cos(FP radians)
        {
            long r = ReduceQuarterTurns(radians.RawValue, out int quadrant);
            long value = (quadrant & 1) == 0 ? CosQ61(r) : SinQ61(r);

            // cos(r + kπ/2): cos r, −sin r, −cos r, sin r.
            return new(ClampUnit(RoundQ61(quadrant == 1 || quadrant == 2 ? -value : value)));
        }

        /// <summary><see cref="Sin"/> and <see cref="Cos"/> together, reducing the angle once.</summary>
        public static void SinCos(FP radians, out FP sin, out FP cos)
        {
            long r = ReduceQuarterTurns(radians.RawValue, out int quadrant);
            long s = SinQ61(r);
            long c = CosQ61(r);

            switch (quadrant)
            {
                case 0:
                    sin = new(ClampUnit(RoundQ61(s)));
                    cos = new(ClampUnit(RoundQ61(c)));
                    break;

                case 1:
                    sin = new(ClampUnit(RoundQ61(c)));
                    cos = new(ClampUnit(RoundQ61(-s)));
                    break;

                case 2:
                    sin = new(ClampUnit(RoundQ61(-s)));
                    cos = new(ClampUnit(RoundQ61(-c)));
                    break;

                default:
                    sin = new(ClampUnit(RoundQ61(-c)));
                    cos = new(ClampUnit(RoundQ61(s)));
                    break;
            }
        }

        /// <summary>sin / cos, divided before either is rounded; saturates at odd multiples of π/2.</summary>
        public static FP Tan(FP radians)
        {
            long r = ReduceQuarterTurns(radians.RawValue, out int quadrant);
            long s = SinQ61(r);
            long c = CosQ61(r);

            // tan(r + kπ/2) is tan r for even k, −cot r for odd k. Both Q3.61, so Divide's 2^32 scale gives Q32.32.
            return new((quadrant & 1) == 0 ? Divide(s, c) : Divide(-c, s));
        }

        /// <summary>The angle whose tangent is <paramref name="value"/>, in (−π/2, π/2).</summary>
        public static FP Atan(FP value) => Atan2(value, One);

        /// <summary>
        /// The angle of the point (<paramref name="x"/>, <paramref name="y"/>) from the positive x axis, in (−π, π]. Zero
        /// for the origin.
        /// </summary>
        public static FP Atan2(FP y, FP x)
        {
            ulong ay = Magnitude(y.RawValue);
            ulong ax = Magnitude(x.RawValue);

            if (ay == 0UL && ax == 0UL)
                return Zero;

            // The smaller over the larger: in [0, 1], one division, and nothing to overflow.
            bool steep = ay > ax;
            long angle = AtanUnit(steep ? Ratio61(ax, ay) : Ratio61(ay, ax));

            if (steep)
                angle = HALF_PI_Q61 - angle;

            if (x.RawValue < 0L)
                angle = PI_Q61 - angle;

            return new(RoundQ61(y.RawValue < 0L ? -angle : angle));
        }

        /// <summary>The angle whose sine is <paramref name="value"/>, in [−π/2, π/2]. The value is clamped to [−1, 1].</summary>
        public static FP Asin(FP value)
        {
            value = Clamp(value, MinusOne, One);
            return Atan2(value, Cosine(value));
        }

        /// <summary>The angle whose cosine is <paramref name="value"/>, in [0, π]. The value is clamped to [−1, 1].</summary>
        public static FP Acos(FP value)
        {
            value = Clamp(value, MinusOne, One);
            return Atan2(Cosine(value), value);
        }

        /// <summary>
        /// √(1 − <paramref name="sine"/>²) for a sine in [−1, 1], from the exact product (1 − s)(1 + s): near ±1, where
        /// the result is tiny, rounding the product first would lose most of its digits.
        /// </summary>
        private static FP Cosine(FP sine)
        {
            ulong high = MultiplyHigh((ulong)(ONE - sine.RawValue), (ulong)(ONE + sine.RawValue), out ulong low);

            // The product is in units of 2^−64, so its root is in raw units (2^−32).
            return new((long)SqrtRounded(high, low));
        }

        /// <summary>
        /// <paramref name="raw"/> radians as r + k × π/2 with |r| near π/4 at most: r in Q3.61, and k mod 4 as the
        /// quadrant. Exact for any argument, since each quarter turn also removes the fraction of a raw unit that
        /// <see cref="HALF_PI_RAW_FLOOR"/> leaves out.
        /// </summary>
        private static long ReduceQuarterTurns(long raw, out int quadrant)
        {
            long turns = raw / HALF_PI_RAW_FLOOR;
            long rest = raw - turns * HALF_PI_RAW_FLOOR;

            // To the nearest quarter turn, so |rest| is at most an eighth of a turn.
            if (rest > HALF_PI_RAW_FLOOR / 2)
            {
                turns++;
                rest -= HALF_PI_RAW_FLOOR;
            }
            else if (rest < -(HALF_PI_RAW_FLOOR / 2))
            {
                turns--;
                rest += HALF_PI_RAW_FLOOR;
            }

            quadrant = (int)(turns & 3L);
            return (rest << Q61_SHIFT) - FractionTimes(turns, HALF_PI_FRACTION);
        }

        /// <summary>sin r for |r| up to about π/4 + 0.1, Taylor to r^13 (error below 1e-13).</summary>
        private static long SinQ61(long r)
        {
            long r2 = Mul61(r, r);
            long p = INV_FACTORIAL_13;

            p = Mul61(r2, p) - INV_FACTORIAL_11;
            p = Mul61(r2, p) + INV_FACTORIAL_9;
            p = Mul61(r2, p) - INV_FACTORIAL_7;
            p = Mul61(r2, p) + INV_FACTORIAL_5;
            p = Mul61(r2, p) - INV_FACTORIAL_3;
            p = Mul61(r2, p) + ONE_Q61;

            return Mul61(r, p);
        }

        /// <summary>cos r for |r| up to about π/4 + 0.1, Taylor to r^12 (error below 2e-12).</summary>
        private static long CosQ61(long r)
        {
            long r2 = Mul61(r, r);
            long p = INV_FACTORIAL_12;

            p = Mul61(r2, p) - INV_FACTORIAL_10;
            p = Mul61(r2, p) + INV_FACTORIAL_8;
            p = Mul61(r2, p) - INV_FACTORIAL_6;
            p = Mul61(r2, p) + INV_FACTORIAL_4;
            p = Mul61(r2, p) - INV_FACTORIAL_2;

            return Mul61(r2, p) + ONE_Q61;
        }

        /// <summary>atan t for t in [0, 1], in Q3.61.</summary>
        private static long AtanUnit(long t)
        {
            long offset = 0L;

            // Above tan(π/12), atan t = π/6 + atan((t√3 − 1) / (t + √3)), whose argument is back within ±tan(π/12).
            if (t > TAN_PI_OVER_12_Q61)
            {
                t = Div61(Mul61(t, SQRT3_Q61) - ONE_Q61, t + SQRT3_Q61);
                offset = PI_OVER_6_Q61;
            }

            // Taylor to t^17 on |t| ≤ 0.268: error below 1e-12.
            long t2 = Mul61(t, t);
            long p = INV_17;

            p = Mul61(t2, p) - INV_15;
            p = Mul61(t2, p) + INV_13;
            p = Mul61(t2, p) - INV_11;
            p = Mul61(t2, p) + INV_9;
            p = Mul61(t2, p) - INV_7;
            p = Mul61(t2, p) + INV_5;
            p = Mul61(t2, p) - INV_3;
            p = Mul61(t2, p) + ONE_Q61;

            return offset + Mul61(t, p);
        }
        #endregion

        #region Exponentials and logarithms:
        /// <summary>e^<paramref name="x"/>. Saturates at <see cref="MaxValue"/> (from x ≈ 21.49); zero below about −22.9.</summary>
        public static FP Exp(FP x)
        {
            long raw = x.RawValue;

            // e^22 is past MaxValue; e^−23 is under half a raw unit.
            if (raw >= 22L * ONE)
                return MaxValue;

            if (raw <= -23L * ONE)
                return Zero;

            // x = n ln 2 + r with |r| ≤ ln 2 / 2, reduced exactly as the trigonometry is.
            long n = raw / LN2_RAW_FLOOR;
            long rest = raw - n * LN2_RAW_FLOOR;

            if (rest > LN2_RAW_FLOOR / 2)
            {
                n++;
                rest -= LN2_RAW_FLOOR;
            }
            else if (rest < -(LN2_RAW_FLOOR / 2))
            {
                n--;
                rest += LN2_RAW_FLOOR;
            }

            long r = (rest << Q61_SHIFT) - FractionTimes(n, LN2_FRACTION);

            // e^r, Taylor to r^12 on |r| ≤ 0.35: relative error below 1e-14.
            long p = INV_FACTORIAL_12;

            p = Mul61(r, p) + INV_FACTORIAL_11;
            p = Mul61(r, p) + INV_FACTORIAL_10;
            p = Mul61(r, p) + INV_FACTORIAL_9;
            p = Mul61(r, p) + INV_FACTORIAL_8;
            p = Mul61(r, p) + INV_FACTORIAL_7;
            p = Mul61(r, p) + INV_FACTORIAL_6;
            p = Mul61(r, p) + INV_FACTORIAL_5;
            p = Mul61(r, p) + INV_FACTORIAL_4;
            p = Mul61(r, p) + INV_FACTORIAL_3;
            p = Mul61(r, p) + INV_FACTORIAL_2;
            p = Mul61(r, p) + ONE_Q61;
            p = Mul61(r, p) + ONE_Q61;

            // e^r × 2^n, from Q3.61 to Q32.32: a shift by n − 29, rounded when it is to the right.
            int shift = (int)n - Q61_SHIFT;

            if (shift >= 0)
                return p > long.MaxValue >> shift ? MaxValue : new(p << shift);

            int right = -shift;
            return new((long)(((ulong)p + (1UL << (right - 1))) >> right));
        }

        /// <summary>The natural logarithm. Zero and negative values return <see cref="MinValue"/>, the nearest to −∞.</summary>
        public static FP Log(FP x)
        {
            long raw = x.RawValue;

            if (raw <= 0L)
                return MinValue;

            long lnMantissa = LogMantissa(raw, out int exponent);

            // exponent × ln 2 + ln m: the integer part of ln 2 × 2^32 exactly, the rest rounded once with ln m.
            return new(exponent * LN2_RAW_FLOOR + RoundQ61(FractionTimes(exponent, LN2_FRACTION) + lnMantissa));
        }

        /// <summary>The base-2 logarithm. Zero and negative values return <see cref="MinValue"/>.</summary>
        public static FP Log2(FP x)
        {
            long raw = x.RawValue;

            if (raw <= 0L)
                return MinValue;

            long lnMantissa = LogMantissa(raw, out int exponent);
            return new(((long)exponent << FRACTIONAL_BITS) + RoundQ61(Mul61(lnMantissa, INV_LN2_Q61)));
        }

        /// <summary>
        /// <paramref name="x"/> to the power <paramref name="y"/>. A whole exponent multiplies by repeated squaring,
        /// for any base (zero to a negative power saturates). Otherwise it is e^(y ln x), which needs a positive base:
        /// a negative base returns zero.
        /// </summary>
        public static FP Pow(FP x, FP y)
        {
            if (y.RawValue == 0L)
                return One;

            if ((y.RawValue & FRACTION_MASK) == 0L)
                return PowInt(x, (int)(y.RawValue >> FRACTIONAL_BITS));

            if (x.RawValue <= 0L)
                return x.RawValue == 0L && y.RawValue < 0L ? MaxValue : Zero;

            return Exp(Log(x) * y);
        }

        private static FP PowInt(FP x, int exponent)
        {
            ulong remaining = exponent < 0 ? (ulong)-(long)exponent : (ulong)exponent;
            FP result = One;
            FP square = x;

            while (true)
            {
                if ((remaining & 1UL) != 0UL)
                    result *= square;

                remaining >>= 1;

                if (remaining == 0UL)
                    break;

                square *= square;
            }

            return exponent < 0 ? One / result : result;
        }

        /// <summary>
        /// ln m in Q3.61, for <paramref name="raw"/> = m × 2^(<paramref name="exponent"/> + 32) with m in [√½, √2):
        /// 2 atanh((m − 1) / (m + 1)), whose argument stays below 0.172.
        /// </summary>
        private static long LogMantissa(long raw, out int exponent)
        {
            int top = 63 - LeadingZeroCount((ulong)raw);

            exponent = top - FRACTIONAL_BITS;

            long m = top >= Q61_BITS ? raw >> (top - Q61_BITS) : raw << (Q61_BITS - top);

            if (m > SQRT2_Q61)
            {
                m >>= 1;
                exponent++;
            }

            long y = Div61(m - ONE_Q61, m + ONE_Q61);
            long y2 = Mul61(y, y);
            long p = INV_17;

            p = Mul61(y2, p) + INV_15;
            p = Mul61(y2, p) + INV_13;
            p = Mul61(y2, p) + INV_11;
            p = Mul61(y2, p) + INV_9;
            p = Mul61(y2, p) + INV_7;
            p = Mul61(y2, p) + INV_5;
            p = Mul61(y2, p) + INV_3;
            p = Mul61(y2, p) + ONE_Q61;

            return Mul61(y << 1, p);
        }
        #endregion
    }
}
