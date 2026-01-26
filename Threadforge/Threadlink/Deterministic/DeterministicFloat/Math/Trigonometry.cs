namespace Threadlink.Deterministic
{
    using System.Runtime.CompilerServices;

    public static partial class DFPMath
    {
        const uint PI = 0x40490fdb; // 3.1415926535897932384626433832795
        const uint HALF_PI = 0x3fc90fdb; // 1.5707963267948966192313216916398
        const uint TAU = 0x40c90fdb; // 6.283185307179586476925286766559
        const uint PI_OVER_4 = 0x3f490fdb; // 0.78539816339744830961566084581988
        const uint PI_TIMES_3_OVER_4 = 0x4016cbe4; // 2.3561944901923449288469825374596

        /// <summary>
        /// Returns the sine of x
        /// </summary>
        public static DFP Sin(DFP x)
        {
            const uint pi_squared_times_five = 0x42456460; // 49.348022005446793094172454999381

            ///<see href="https://en.wikipedia.org/wiki/Bhaskara_I%27s_sine_approximation_formula"/> 
            // sin(x) ~= (16x * (pi - x)) / (5pi^2 - 4x * (pi - x)) if 0 <= x <= pi

            // move x into range
            x %= DFP.FromRaw(TAU);
            if (x.IsNegative())
            {
                x += DFP.FromRaw(TAU);
            }

            bool negate;
            if (x > DFP.FromRaw(PI))
            {
                // pi < x <= 2pi, we need to move x to the 0 <= x <= pi range
                // also, we need to negate the result before returning it
                x = DFP.FromRaw(TAU) - x;
                negate = true;
            }
            else
            {
                negate = false;
            }

            DFP piMinusX = DFP.FromRaw(PI) - x;
            DFP result = ((DFP)16.0f * x * piMinusX) / (DFP.FromRaw(pi_squared_times_five) - (DFP)4.0f * x * piMinusX);
            return negate ? -result : result;
        }

        /// <summary>
        /// Returns the cosine of x
        /// </summary>
        public static DFP Cos(DFP x) => Sin(x + DFP.FromRaw(HALF_PI));

        /// <summary>
        /// Returns the tangent of x
        /// </summary>
        public static DFP Tan(DFP x) => Sin(x) / Cos(x);

        /// <summary>
        /// Returns the square root of (x*x + y*y)
        /// </summary>
        public static DFP Hypot(DFP x, DFP y)
        {
            DFP w;

            int ha = (int)x.RawValue;
            ha &= 0x7fffffff;

            int hb = (int)y.RawValue;
            hb &= 0x7fffffff;

            if (hb > ha)
            {
                (hb, ha) = (ha, hb);
            }

            DFP a = DFP.FromRaw((uint)ha); /* a <- |a| */
            DFP b = DFP.FromRaw((uint)hb); /* b <- |b| */

            if (ha - hb > 0xf000000)
            {
                return a + b;
            } /* x/y > 2**30 */

            uint k = 0;
            if (ha > 0x58800000)
            {
                /* a>2**50 */
                if (ha >= 0x7f800000)
                {
                    /* Inf or NaN */
                    w = a + b; /* for sNaN */
                    if (ha == 0x7f800000)
                    {
                        w = a;
                    }

                    if (hb == 0x7f800000)
                    {
                        w = b;
                    }

                    return w;
                }

                /* scale a and b by 2**-60 */
                ha -= 0x5d800000;
                hb -= 0x5d800000;
                k += 60;
                a = DFP.FromRaw((uint)ha);
                b = DFP.FromRaw((uint)hb);
            }

            if (hb < 0x26800000)
            {
                /* b < 2**-50 */
                if (hb <= 0x007fffff)
                {
                    /* subnormal b or 0 */
                    if (hb == 0)
                    {
                        return a;
                    }

                    DFP t1 = DFP.FromRaw(0x3f000000); /* t1=2^126 */
                    b *= t1;
                    a *= t1;
                    k -= 126;
                }
                else
                {
                    /* scale a and b by 2^60 */
                    ha += 0x5d800000; /* a *= 2^60 */
                    hb += 0x5d800000; /* b *= 2^60 */
                    k -= 60;
                    a = DFP.FromRaw((uint)ha);
                    b = DFP.FromRaw((uint)hb);
                }
            }

            /* medium size a and b */
            w = a - b;
            if (w > b)
            {
                DFP t1 = DFP.FromRaw(((uint)ha) & 0xfffff000);
                DFP t2 = a - t1;
                w = Sqrt(t1 * t1 - (b * (-b) - t2 * (a + t1)));
            }
            else
            {
                a += a;
                DFP y1 = DFP.FromRaw(((uint)hb) & 0xfffff000);
                DFP y2 = b - y1;
                DFP t1 = DFP.FromRaw(((uint)ha) + 0x00800000);
                DFP t2 = a - t1;
                w = Sqrt(t1 * y1 - (w * (-w) - (t1 * y2 + t2 * b)));
            }

            if (k != 0)
            {
                DFP t1 = DFP.FromRaw(0x3f800000 + (k << 23));
                return t1 * w;
            }
            else
            {
                return w;
            }
        }

        private static readonly uint[] ATAN_HI = new uint[4]
        {
            0x3eed6338, // 4.6364760399e-01, /* atan(0.5)hi */
            0x3f490fda, // 7.8539812565e-01, /* atan(1.0)hi */
            0x3f7b985e, // 9.8279368877e-01, /* atan(1.5)hi */
            0x3fc90fda, // 1.5707962513e+00, /* atan(inf)hi */
        };

        private static readonly uint[] ATAN_LO = new uint[4]
        {
            0x31ac3769, // 5.0121582440e-09, /* atan(0.5)lo */
            0x33222168, // 3.7748947079e-08, /* atan(1.0)lo */
            0x33140fb4, // 3.4473217170e-08, /* atan(1.5)lo */
            0x33a22168, // 7.5497894159e-08, /* atan(inf)lo */
        };

        private static readonly uint[] A_T = new uint[5]
        {
            0x3eaaaaa9, // 3.3333328366e-01
            0xbe4cca98, // -1.9999158382e-01
            0x3e11f50d, // 1.4253635705e-01
            0xbdda1247, // -1.0648017377e-01
            0x3d7cac25  // 6.1687607318e-02
        };

        /// <summary>
        /// Returns the arctangent of x
        /// </summary>
        public unsafe static DFP Atan(DFP x)
        {
            DFP z;

            uint ix = x.RawValue;
            bool sign = (ix >> 31) != 0;
            ix &= 0x7fffffff;

            if (ix >= 0x4c800000)
            {
                /* if |x| >= 2**26 */
                if (x.IsNaN())
                {
                    return x;
                }

                DFP x1p_120 = DFP.FromRaw(0x03800000); // 0x1p-120 === 2 ^ (-120)
                z = DFP.FromRaw(ATAN_HI[3]) + x1p_120;
                return sign ? -z : z;
            }

            int id;
            if (ix < 0x3ee00000)
            {
                /* |x| < 0.4375 */
                if (ix < 0x39800000)
                {
                    /* |x| < 2**-12 */
                    //if (ix < 0x00800000)
                    //{
                    //    /* raise underflow for subnormal x */
                    //    force_eval!(x * x);
                    //}
                    return x;
                }
                id = -1;
            }
            else
            {
                x = Abs(x);
                if (ix < 0x3f980000)
                {
                    /* |x| < 1.1875 */
                    if (ix < 0x3f300000)
                    {
                        /*  7/16 <= |x| < 11/16 */
                        x = ((DFP)2.0f * x - (DFP)1.0f) / ((DFP)2.0f + x);
                        id = 0;
                    }
                    else
                    {
                        /* 11/16 <= |x| < 19/16 */
                        x = (x - (DFP)1.0f) / (x + (DFP)1.0f);
                        id = 1;
                    }
                }
                else if (ix < 0x401c0000)
                {
                    /* |x| < 2.4375 */
                    x = (x - (DFP)1.5f) / ((DFP)1.0f + (DFP)1.5f * x);
                    id = 2;
                }
                else
                {
                    /* 2.4375 <= |x| < 2**26 */
                    x = (DFP)(-1.0f) / x;
                    id = 3;
                }
            }
            ;

            /* end of argument reduction */
            z = x * x;
            DFP w = z * z;

            /* break sum from i=0 to 10 aT[i]z**(i+1) into odd and even poly */
            DFP s1 = z * (DFP.FromRaw(A_T[0]) + w * (DFP.FromRaw(A_T[2]) + w * DFP.FromRaw(A_T[4])));
            DFP s2 = w * (DFP.FromRaw(A_T[1]) + w * DFP.FromRaw(A_T[3]));
            if (id < 0)
            {
                return x - x * (s1 + s2);
            }

            z = DFP.FromRaw(ATAN_HI[id]) - ((x * (s1 + s2) - DFP.FromRaw(ATAN_LO[id])) - x);
            return sign ? -z : z;
        }

        /// <summary>
        /// Returns the signed angle between the positive x axis, and the direction (x, y)
        /// </summary>
        public static DFP Atan2(DFP y, DFP x)
        {
            if (x.IsNaN() || y.IsNaN())
            {
                return x + y;
            }

            uint ix = x.RawValue;
            uint iy = y.RawValue;

            if (ix == 0x3f800000)
            {
                /* x=1.0 */
                return Atan(y);
            }

            uint m = ((iy >> 31) & 1) | ((ix >> 30) & 2); /* 2*sign(x)+sign(y) */
            ix &= 0x7fffffff;
            iy &= 0x7fffffff;

            const uint PI_LO_U32 = 0xb3bbbd2e; // -8.7422776573e-08

            /* when y = 0 */
            if (iy == 0)
            {
                return m switch
                {
                    0 or 1 => y,/* atan(+-0,+anything)=+-0 */
                    2 => DFP.FromRaw(PI),/* atan(+0,-anything) = pi */
                    _ => -DFP.FromRaw(PI),/* atan(-0,-anything) =-pi */
                };
            }

            /* when x = 0 */
            if (ix == 0)
            {
                return (m & 1) != 0 ? -DFP.FromRaw(HALF_PI) : DFP.FromRaw(HALF_PI);
            }

            /* when x is INF */
            if (ix == 0x7f800000)
            {
                if (iy == 0x7f800000)
                {
                    return m switch
                    {
                        0 => DFP.FromRaw(PI_OVER_4),/* atan(+INF,+INF) */
                        1 => -DFP.FromRaw(PI_OVER_4),/* atan(-INF,+INF) */
                        2 => DFP.FromRaw(PI_TIMES_3_OVER_4),/* atan(+INF,-INF)*/
                        _ => -DFP.FromRaw(PI_TIMES_3_OVER_4),/* atan(-INF,-INF)*/
                    };
                }
                else
                {
                    return m switch
                    {
                        0 => DFP.Zero,/* atan(+...,+INF) */
                        1 => -DFP.Zero,/* atan(-...,+INF) */
                        2 => DFP.FromRaw(PI),/* atan(+...,-INF) */
                        _ => -DFP.FromRaw(PI),/* atan(-...,-INF) */
                    };
                }
            }

            /* |y/x| > 0x1p26 */
            if (ix + (26 << 23) < iy || iy == 0x7f800000)
            {
                return (m & 1) != 0 ? -DFP.FromRaw(HALF_PI) : DFP.FromRaw(HALF_PI);
            }

            /* z = atan(|y/x|) with correct underflow */
            DFP z = (m & 2) != 0 && iy + (26 << 23) < ix
                ? DFP.Zero /*|y/x| < 0x1p-26, x < 0 */
                : Atan(Abs(y / x));

            return m switch
            {
                0 => z,/* atan(+,+) */
                1 => -z,/* atan(-,+) */
                2 => DFP.FromRaw(PI) - (z - DFP.FromRaw(PI_LO_U32)),/* atan(+,-) */
                _ => (z - DFP.FromRaw(PI_LO_U32)) - DFP.FromRaw(PI),/* atan(-,-) */
            };
        }

        /// <summary>
        /// Returns the arccosine of x
        /// </summary>
        public static DFP Acos(DFP x)
        {
            const uint PIO2_HI_U32 = 0x3fc90fda; // 1.5707962513e+00
            const uint PIO2_LO_U32 = 0x33a22168; // 7.5497894159e-08
            const uint P_S0_U32 = 0x3e2aaa75; // 1.6666586697e-01
            const uint P_S1_U32 = 0xbd2f13ba; // -4.2743422091e-02
            const uint P_S2_U32 = 0xbc0dd36b; // -8.6563630030e-03
            const uint Q_S1_U32 = 0xbf34e5ae; // - 7.0662963390e-01

            static DFP r(DFP z)
            {
                DFP p = z * (DFP.FromRaw(P_S0_U32) + z * (DFP.FromRaw(P_S1_U32) + z * DFP.FromRaw(P_S2_U32)));
                DFP q = (DFP)1.0f + z * DFP.FromRaw(Q_S1_U32);
                return p / q;
            }

            DFP x1p_120 = DFP.FromRaw(0x03800000); // 0x1p-120 === 2 ^ (-120)

            DFP z;
            DFP w;
            DFP s;

            uint hx = x.RawValue;
            uint ix = hx & 0x7fffffff;

            /* |x| >= 1 or nan */
            if (ix >= 0x3f800000)
            {
                if (ix == 0x3f800000)
                {
                    if ((hx >> 31) != 0)
                    {
                        return (DFP)2.0f * DFP.FromRaw(PIO2_HI_U32) + x1p_120;
                    }

                    return DFP.Zero;
                }

                return DFP.NaN;
            }

            /* |x| < 0.5 */
            if (ix < 0x3f000000)
            {
                if (ix <= 0x32800000)
                {
                    /* |x| < 2**-26 */
                    return DFP.FromRaw(PIO2_HI_U32) + x1p_120;
                }

                return DFP.FromRaw(PIO2_HI_U32) - (x - (DFP.FromRaw(PIO2_LO_U32) - x * r(x * x)));
            }

            /* x < -0.5 */
            if ((hx >> 31) != 0)
            {
                z = ((DFP)1.0f + x) * (DFP)0.5f;
                s = Sqrt(z);
                w = r(z) * s - DFP.FromRaw(PIO2_LO_U32);
                return (DFP)2.0 * (DFP.FromRaw(PIO2_HI_U32) - (s + w));
            }

            /* x > 0.5 */
            z = ((DFP)1.0f - x) * (DFP)0.5f;
            s = Sqrt(z);
            hx = s.RawValue;
            DFP df = DFP.FromRaw(hx & 0xfffff000);
            DFP c = (z - df * df) / (s + df);
            w = r(z) * s + c;
            return (DFP)2.0f * (df + w);
        }

        /// <summary>
        /// Returns the arcsine of x
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DFP Asin(DFP x) => DFP.FromRaw(HALF_PI) - Acos(x);
    }
}