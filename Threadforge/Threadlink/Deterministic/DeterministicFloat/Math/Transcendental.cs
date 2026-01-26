namespace Threadlink.Deterministic
{
    public static partial class DFPMath
    {
        private static DFP ScalBN(DFP x, int n)
        {
            DFP x1p127 = DFP.FromRaw(0x7f000000); // 0x1p127f === 2 ^ 127
            DFP x1p_126 = DFP.FromRaw(0x800000); // 0x1p-126f === 2 ^ -126
            DFP x1p24 = DFP.FromRaw(0x4b800000); // 0x1p24f === 2 ^ 24

            if (n > 127)
            {
                x *= x1p127;
                n -= 127;
                if (n > 127)
                {
                    x *= x1p127;
                    n -= 127;
                    if (n > 127)
                    {
                        n = 127;
                    }
                }
            }
            else if (n < -126)
            {
                x *= x1p_126 * x1p24;
                n += 126 - 24;
                if (n < -126)
                {
                    x *= x1p_126 * x1p24;
                    n += 126 - 24;
                    if (n < -126)
                    {
                        n = -126;
                    }
                }
            }

            return x * DFP.FromRaw(((uint)(0x7f + n)) << 23);
        }

        /// <summary>
        /// Returns e raised to the power x (e ~= 2.71828182845904523536)
        /// </summary>
        public static DFP Exponent(DFP x)
        {
            const uint LN2_HI_U32 = 0x3f317200; // 6.9314575195e-01
            const uint LN2_LO_U32 = 0x35bfbe8e; // 1.4286067653e-06
            const uint INV_LN2_U32 = 0x3fb8aa3b; // 1.4426950216e+00

            const uint P1_U32 = 0x3e2aaa8f; // 1.6666625440e-1 /*  0xaaaa8f.0p-26 */
            const uint P2_U32 = 0xbb355215; // -2.7667332906e-3 /* -0xb55215.0p-32 */

            DFP x1p127 = DFP.FromRaw(0x7f000000); // 0x1p127f === 2 ^ 127
            DFP x1p_126 = DFP.FromRaw(0x800000); // 0x1p-126f === 2 ^ -126  /*original 0x1p-149f    ??????????? */
            uint hx = x.RawValue;
            int sign = (int)(hx >> 31); /* sign bit of x */
            bool signb = sign != 0;
            hx &= 0x7fffffff; /* high word of |x| */

            /* special cases */
            if (hx >= 0x42aeac50)
            {
                /* if |x| >= -87.33655f or NaN */
                if (hx > 0x7f800000)
                {
                    /* NaN */
                    return x;
                }

                if (hx >= 0x42b17218 && !signb)
                {
                    /* x >= 88.722839f */
                    /* overflow */
                    x *= x1p127;
                    return x;
                }

                if (signb)
                {
                    /* underflow */
                    //force_eval!(-x1p_126 / x);

                    if (hx >= 0x42cff1b5)
                    {
                        /* x <= -103.972084f */
                        return DFP.Zero;
                    }
                }
            }

            /* argument reduction */
            int k;
            DFP hi;
            DFP lo;
            if (hx > 0x3eb17218)
            {
                /* if |x| > 0.5 ln2 */
                if (hx > 0x3f851592)
                {
                    /* if |x| > 1.5 ln2 */
                    k = (int)(DFP.FromRaw(INV_LN2_U32) * x + (signb ? (DFP)0.5f : (DFP)(-0.5f)));
                }
                else
                {
                    k = 1 - sign - sign;
                }

                DFP kf = (DFP)k;
                hi = x - kf * DFP.FromRaw(LN2_HI_U32); /* k*ln2hi is exact here */
                lo = kf * DFP.FromRaw(LN2_LO_U32);
                x = hi - lo;
            }
            else if (hx > 0x39000000)
            {
                /* |x| > 2**-14 */
                k = 0;
                hi = x;
                lo = DFP.Zero;
            }
            else
            {
                /* raise inexact */
                //force_eval!(x1p127 + x);
                return DFP.One + x;
            }

            /* x is now in primary range */
            DFP xx = x * x;
            DFP c = x - xx * (DFP.FromRaw(P1_U32) + xx * DFP.FromRaw(P2_U32));
            DFP y = DFP.One + (x * c / ((DFP)2.0f - c) - lo + hi);
            return k == 0 ? y : ScalBN(y, k);
        }

        /// <summary>
        /// Returns the natural logarithm (base e) of x
        /// </summary>
        public static DFP LogE(DFP x)
        {
            const uint LN2_HI_U32 = 0x3f317180; // 6.9313812256e-01
            const uint LN2_LO_U32 = 0x3717f7d1; // 9.0580006145e-06

            /* |(log(1+s)-log(1-s))/s - Lg(s)| < 2**-34.24 (~[-4.95e-11, 4.97e-11]). */
            const uint LG1_U32 = 0x3f2aaaaa; // 0.66666662693 /*  0xaaaaaa.0p-24*/
            const uint LG2_U32 = 0x3eccce13; // 0.40000972152 /*  0xccce13.0p-25 */
            const uint LG3_U32 = 0x3e91e9ee; // 0.28498786688 /*  0x91e9ee.0p-25 */
            const uint LG4_U32 = 0x3e789e26; // 0.24279078841 /*  0xf89e26.0p-26 */

            uint ix = x.RawValue;
            int k = 0;

            if ((ix < 0x00800000) || ((ix >> 31) != 0))
            {
                /* x < 2**-126  */
                if (ix << 1 == 0)
                {
                    //return -1. / (x * x); /* log(+-0)=-inf */
                    return DFP.NegativeInfinity;
                }

                if ((ix >> 31) != 0)
                {
                    //return (x - x) / 0.; /* log(-#) = NaN */
                    return DFP.NaN;
                }

                /* subnormal number, scale up x */
                DFP x1p25 = DFP.FromRaw(0x4c000000); // 0x1p25f === 2 ^ 25
                k -= 25;
                x *= x1p25;
                ix = x.RawValue;
            }
            else if (ix >= 0x7f800000)
            {
                return x;
            }
            else if (ix == 0x3f800000)
            {
                return DFP.Zero;
            }

            /* reduce x into [sqrt(2)/2, sqrt(2)] */
            ix += 0x3f800000 - 0x3f3504f3;
            k += ((int)(ix >> 23)) - 0x7f;
            ix = (ix & 0x007fffff) + 0x3f3504f3;
            x = DFP.FromRaw(ix);

            DFP f = x - DFP.One;
            DFP s = f / ((DFP)2.0f + f);
            DFP z = s * s;
            DFP w = z * z;
            DFP t1 = w * (DFP.FromRaw(LG2_U32) + w * DFP.FromRaw(LG4_U32));
            DFP t2 = z * (DFP.FromRaw(LG1_U32) + w * DFP.FromRaw(LG3_U32));
            DFP r = t2 + t1;
            DFP hfsq = (DFP)0.5f * f * f;
            DFP dk = (DFP)k;

            return s * (hfsq + r) + dk * DFP.FromRaw(LN2_LO_U32) - hfsq + f + dk * DFP.FromRaw(LN2_HI_U32);
        }

        /// <summary>
        /// Returns the base 2 logarithm of x
        /// </summary>
        public static DFP Log2(DFP x)
        {
            const uint IVLN2HI_U32 = 0x3fb8b000; // 1.4428710938e+00
            const uint IVLN2LO_U32 = 0xb9389ad4; // -1.7605285393e-04

            /* |(log(1+s)-log(1-s))/s - Lg(s)| < 2**-34.24 (~[-4.95e-11, 4.97e-11]). */
            const uint LG1_U32 = 0x3f2aaaaa; // 0.66666662693 /*  0xaaaaaa.0p-24*/
            const uint LG2_U32 = 0x3eccce13; // 0.40000972152 /*  0xccce13.0p-25 */
            const uint LG3_U32 = 0x3e91e9ee; // 0.28498786688 /*  0x91e9ee.0p-25 */
            const uint LG4_U32 = 0x3e789e26; // 0.24279078841 /*  0xf89e26.0p-26 */

            DFP x1p25f = DFP.FromRaw(0x4c000000); // 0x1p25f === 2 ^ 25

            uint ui = x.RawValue;
            DFP hfsq;
            DFP f;
            DFP s;
            DFP z;
            DFP r;
            DFP w;
            DFP t1;
            DFP t2;
            DFP hi;
            DFP lo;
            uint ix;
            int k;

            ix = ui;
            k = 0;
            if (ix < 0x00800000 || (ix >> 31) > 0)
            {
                /* x < 2**-126  */
                if (ix << 1 == 0)
                {
                    //return -1. / (x * x); /* log(+-0)=-inf */
                    return DFP.NegativeInfinity;
                }

                if ((ix >> 31) > 0)
                {
                    //return (x - x) / 0.0; /* log(-#) = NaN */
                    return DFP.NaN;
                }

                /* subnormal number, scale up x */
                k -= 25;
                x *= x1p25f;
                ui = x.RawValue;
                ix = ui;
            }
            else if (ix >= 0x7f800000)
            {
                return x;
            }
            else if (ix == 0x3f800000)
            {
                return DFP.Zero;
            }

            /* reduce x into [sqrt(2)/2, sqrt(2)] */
            ix += 0x3f800000 - 0x3f3504f3;
            k += (int)(ix >> 23) - 0x7f;
            ix = (ix & 0x007fffff) + 0x3f3504f3;
            ui = ix;
            x = DFP.FromRaw(ui);

            f = x - DFP.One;
            s = f / ((DFP)2.0f + f);
            z = s * s;
            w = z * z;
            t1 = w * (DFP.FromRaw(LG2_U32) + w * DFP.FromRaw(LG4_U32));
            t2 = z * (DFP.FromRaw(LG1_U32) + w * DFP.FromRaw(LG3_U32));
            r = t2 + t1;
            hfsq = (DFP)0.5f * f * f;

            hi = f - hfsq;
            ui = hi.RawValue;
            ui &= 0xfffff000;
            hi = DFP.FromRaw(ui);
            lo = f - hi - hfsq + s * (hfsq + r);
            return (lo + hi) * DFP.FromRaw(IVLN2LO_U32) + lo * DFP.FromRaw(IVLN2HI_U32) + hi * DFP.FromRaw(IVLN2HI_U32) + (DFP)k;
        }

        /// <summary>
        /// Returns x raised to the power y
        /// </summary>
        public static DFP Power(DFP x, DFP y)
        {
            const uint BP_0_U32 = 0x3f800000; /* 1.0 */
            const uint BP_1_U32 = 0x3fc00000; /* 1.5 */
            const uint DP_H_0_U32 = 0x00000000; /* 0.0 */
            const uint DP_H_1_U32 = 0x3f15c000; /* 5.84960938e-01 */
            const uint DP_L_0_U32 = 0x00000000; /* 0.0 */
            const uint DP_L_1_U32 = 0x35d1cfdc; /* 1.56322085e-06 */
            const uint TWO24_U32 = 0x4b800000; /* 16777216.0 */
            const uint HUGE_U32 = 0x7149f2ca; /* 1.0e30 */
            const uint TINY_U32 = 0x0da24260; /* 1.0e-30 */
            const uint L1_U32 = 0x3f19999a; /* 6.0000002384e-01 */
            const uint L2_U32 = 0x3edb6db7; /* 4.2857143283e-01 */
            const uint L3_U32 = 0x3eaaaaab; /* 3.3333334327e-01 */
            const uint L4_U32 = 0x3e8ba305; /* 2.7272811532e-01 */
            const uint L5_U32 = 0x3e6c3255; /* 2.3066075146e-01 */
            const uint L6_U32 = 0x3e53f142; /* 2.0697501302e-01 */
            const uint P1_U32 = 0x3e2aaaab; /* 1.6666667163e-01 */
            const uint P2_U32 = 0xbb360b61; /* -2.7777778450e-03 */
            const uint P3_U32 = 0x388ab355; /* 6.6137559770e-05 */
            const uint P4_U32 = 0xb5ddea0e; /* -1.6533901999e-06 */
            const uint P5_U32 = 0x3331bb4c; /* 4.1381369442e-08 */
            const uint LG2_U32 = 0x3f317218; /* 6.9314718246e-01 */
            const uint LG2_H_U32 = 0x3f317200; /* 6.93145752e-01 */
            const uint LG2_L_U32 = 0x35bfbe8c; /* 1.42860654e-06 */
            const uint OVT_U32 = 0x3338aa3c; /* 4.2995665694e-08 =-(128-log2(ovfl+.5ulp)) */
            const uint CP_U32 = 0x3f76384f; /* 9.6179670095e-01 =2/(3ln2) */
            const uint CP_H_U32 = 0x3f764000; /* 9.6191406250e-01 =12b cp */
            const uint CP_L_U32 = 0xb8f623c6; /* -1.1736857402e-04 =tail of cp_h */
            const uint IVLN2_U32 = 0x3fb8aa3b; /* 1.4426950216e+00 */
            const uint IVLN2_H_U32 = 0x3fb8aa00; /* 1.4426879883e+00 */
            const uint IVLN2_L_U32 = 0x36eca570; /* 7.0526075433e-06 */

            DFP z;
            DFP ax;
            DFP z_h;
            DFP z_l;
            DFP p_h;
            DFP p_l;
            DFP y1;
            DFP t1;
            DFP t2;
            DFP r;
            DFP s;
            DFP sn;
            DFP t;
            DFP u;
            DFP v;
            DFP w;
            int i;
            int j;
            int k;
            int yisint;
            int n;
            int hx;
            int hy;
            int ix;
            int iy;
            int iS;

            hx = (int)x.RawValue;
            hy = (int)y.RawValue;

            ix = hx & 0x7fffffff;
            iy = hy & 0x7fffffff;

            /* x**0 = 1, even if x is NaN */
            if (iy == 0)
            {
                return DFP.One;
            }

            /* 1**y = 1, even if y is NaN */
            if (hx == 0x3f800000)
            {
                return DFP.One;
            }

            /* NaN if either arg is NaN */
            if (ix > 0x7f800000 || iy > 0x7f800000)
            {
                return DFP.NaN;
            }

            /* determine if y is an odd int when x < 0
             * yisint = 0       ... y is not an integer
             * yisint = 1       ... y is an odd int
             * yisint = 2       ... y is an even int
             */
            yisint = 0;
            if (hx < 0)
            {
                if (iy >= 0x4b800000)
                {
                    yisint = 2; /* even integer y */
                }
                else if (iy >= 0x3f800000)
                {
                    k = (iy >> 23) - 0x7f; /* exponent */
                    j = iy >> (23 - k);
                    if ((j << (23 - k)) == iy)
                    {
                        yisint = 2 - (j & 1);
                    }
                }
            }

            /* special value of y */
            if (iy == 0x7f800000)
            {
                /* y is +-inf */
                if (ix == 0x3f800000)
                {
                    /* (-1)**+-inf is 1 */
                    return DFP.One;
                }
                else if (ix > 0x3f800000)
                {
                    /* (|x|>1)**+-inf = inf,0 */
                    return hy >= 0 ? y : DFP.Zero;
                }
                else
                {
                    /* (|x|<1)**+-inf = 0,inf */
                    return hy >= 0 ? DFP.Zero : -y;
                }
            }

            if (iy == 0x3f800000)
            {
                /* y is +-1 */
                return hy >= 0 ? x : DFP.One / x;
            }

            if (hy == 0x40000000)
            {
                /* y is 2 */
                return x * x;
            }

            if (hy == 0x3f000000
               /* y is  0.5 */
               && hx >= 0)
            {
                /* x >= +0 */
                return Sqrt(x);
            }

            ax = Abs(x);
            /* special value of x */
            if (ix == 0x7f800000 || ix == 0 || ix == 0x3f800000)
            {
                /* x is +-0,+-inf,+-1 */
                z = ax;
                if (hy < 0)
                {
                    /* z = (1/|x|) */
                    z = DFP.One / z;
                }

                if (hx < 0)
                {
                    if (((ix - 0x3f800000) | yisint) == 0)
                    {
                        z = (z - z) / (z - z); /* (-1)**non-int is NaN */
                    }
                    else if (yisint == 1)
                    {
                        z = -z; /* (x<0)**odd = -(|x|**odd) */
                    }
                }

                return z;
            }

            sn = DFP.One; /* sign of result */
            if (hx < 0)
            {
                if (yisint == 0)
                {
                    /* (x<0)**(non-int) is NaN */
                    //return (x - x) / (x - x);
                    return DFP.NaN;
                }

                if (yisint == 1)
                {
                    /* (x<0)**(odd int) */
                    sn = -DFP.One;
                }
            }

            /* |y| is HUGE */
            if (iy > 0x4d000000)
            {
                /* if |y| > 2**27 */
                /* over/underflow if x is not close to one */
                if (ix < 0x3f7ffff8)
                {
                    return hy < 0
                        ? sn * DFP.FromRaw(HUGE_U32) * DFP.FromRaw(HUGE_U32)
                        : sn * DFP.FromRaw(TINY_U32) * DFP.FromRaw(TINY_U32);
                }

                if (ix > 0x3f800007)
                {
                    return hy > 0
                        ? sn * DFP.FromRaw(HUGE_U32) * DFP.FromRaw(HUGE_U32)
                        : sn * DFP.FromRaw(TINY_U32) * DFP.FromRaw(TINY_U32);
                }

                /* now |1-x| is TINY <= 2**-20, suffice to compute
                log(x) by x-x^2/2+x^3/3-x^4/4 */
                t = ax - DFP.One; /* t has 20 trailing zeros */
                w = (t * t) * (DFP.FromRaw(0x3f000000) - t * (DFP.FromRaw(0x3eaaaaab) - t * DFP.FromRaw(0x3e800000)));
                u = DFP.FromRaw(IVLN2_H_U32) * t; /* IVLN2_H has 16 sig. bits */
                v = t * DFP.FromRaw(IVLN2_L_U32) - w * DFP.FromRaw(IVLN2_U32);
                t1 = u + v;
                iS = (int)t1.RawValue;
                t1 = DFP.FromRaw((uint)iS & 0xfffff000);
                t2 = v - (t1 - u);
            }
            else
            {
                DFP s2;
                DFP s_h;
                DFP s_l;
                DFP t_h;
                DFP t_l;

                n = 0;
                /* take care subnormal number */
                if (ix < 0x00800000)
                {
                    ax *= DFP.FromRaw(TWO24_U32);
                    n -= 24;
                    ix = (int)ax.RawValue;
                }

                n += ((ix) >> 23) - 0x7f;
                j = ix & 0x007fffff;
                /* determine interval */
                ix = j | 0x3f800000; /* normalize ix */
                if (j <= 0x1cc471)
                {
                    /* |x|<sqrt(3/2) */
                    k = 0;
                }
                else if (j < 0x5db3d7)
                {
                    /* |x|<sqrt(3)   */
                    k = 1;
                }
                else
                {
                    k = 0;
                    n += 1;
                    ix -= 0x00800000;
                }

                ax = DFP.FromRaw((uint)ix);

                /* compute s = s_h+s_l = (x-1)/(x+1) or (x-1.5)/(x+1.5) */
                u = ax - DFP.FromRaw(k == 0 ? BP_0_U32 : BP_1_U32); /* bp[0]=1.0, bp[1]=1.5 */
                v = DFP.One / (ax + DFP.FromRaw(k == 0 ? BP_0_U32 : BP_1_U32));
                s = u * v;
                s_h = s;
                iS = (int)s_h.RawValue;
                s_h = DFP.FromRaw((uint)iS & 0xfffff000);

                /* t_h=ax+bp[k] High */
                iS = (int)((((uint)ix >> 1) & 0xfffff000) | 0x20000000);
                t_h = DFP.FromRaw((uint)iS + 0x00400000 + (((uint)k) << 21));
                t_l = ax - (t_h - DFP.FromRaw(k == 0 ? BP_0_U32 : BP_1_U32));
                s_l = v * ((u - s_h * t_h) - s_h * t_l);

                /* compute log(ax) */
                s2 = s * s;
                r = s2 * s2 * (DFP.FromRaw(L1_U32) + s2 * (DFP.FromRaw(L2_U32) + s2 * (DFP.FromRaw(L3_U32) + s2 * (DFP.FromRaw(L4_U32) + s2 * (DFP.FromRaw(L5_U32) + s2 * DFP.FromRaw(L6_U32))))));
                r += s_l * (s_h + s);
                s2 = s_h * s_h;
                t_h = DFP.FromRaw(0x40400000) + s2 + r;
                iS = (int)t_h.RawValue;
                t_h = DFP.FromRaw((uint)iS & 0xfffff000);
                t_l = r - ((t_h - DFP.FromRaw(0x40400000)) - s2);

                /* u+v = s*(1+...) */
                u = s_h * t_h;
                v = s_l * t_h + t_l * s;

                /* 2/(3log2)*(s+...) */
                p_h = u + v;
                iS = (int)p_h.RawValue;
                p_h = DFP.FromRaw((uint)iS & 0xfffff000);
                p_l = v - (p_h - u);
                z_h = DFP.FromRaw(CP_H_U32) * p_h; /* cp_h+cp_l = 2/(3*log2) */
                z_l = DFP.FromRaw(CP_L_U32) * p_h + p_l * DFP.FromRaw(CP_U32) + DFP.FromRaw(k == 0 ? DP_L_0_U32 : DP_L_1_U32);

                /* log2(ax) = (s+..)*2/(3*log2) = n + dp_h + z_h + z_l */
                t = (DFP)n;
                t1 = ((z_h + z_l) + DFP.FromRaw(k == 0 ? DP_H_0_U32 : DP_H_1_U32)) + t;
                iS = (int)t1.RawValue;
                t1 = DFP.FromRaw((uint)iS & 0xfffff000);
                t2 = z_l - (((t1 - t) - DFP.FromRaw(k == 0 ? DP_H_0_U32 : DP_H_1_U32)) - z_h);
            }
            ;

            /* split up y into y1+y2 and compute (y1+y2)*(t1+t2) */
            iS = (int)y.RawValue;
            y1 = DFP.FromRaw((uint)iS & 0xfffff000);
            p_l = (y - y1) * t1 + y * t2;
            p_h = y1 * t1;
            z = p_l + p_h;
            j = (int)z.RawValue;
            if (j > 0x43000000)
            {
                /* if z > 128 */
                return sn * DFP.FromRaw(HUGE_U32) * DFP.FromRaw(HUGE_U32); /* overflow */
            }
            else if (j == 0x43000000)
            {
                /* if z == 128 */
                if (p_l + DFP.FromRaw(OVT_U32) > z - p_h)
                {
                    return sn * DFP.FromRaw(HUGE_U32) * DFP.FromRaw(HUGE_U32); /* overflow */
                }
            }
            else if ((j & 0x7fffffff) > 0x43160000)
            {
                /* z < -150 */
                // FIXME: check should be  (uint32_t)j > 0xc3160000
                return sn * DFP.FromRaw(TINY_U32) * DFP.FromRaw(TINY_U32); /* underflow */
            }
            else if ((uint)j == 0xc3160000
                    /* z == -150 */
                    && p_l <= z - p_h)
            {
                return sn * DFP.FromRaw(TINY_U32) * DFP.FromRaw(TINY_U32); /* underflow */
            }

            /*
             * compute 2**(p_h+p_l)
             */
            i = j & 0x7fffffff;
            k = (i >> 23) - 0x7f;
            n = 0;
            if (i > 0x3f000000)
            {
                /* if |z| > 0.5, set n = [z+0.5] */
                n = j + (0x00800000 >> (k + 1));
                k = ((n & 0x7fffffff) >> 23) - 0x7f; /* new k for n */
                t = DFP.FromRaw((uint)n & ~(0x007fffffu >> k));
                n = ((n & 0x007fffff) | 0x00800000) >> (23 - k);
                if (j < 0)
                {
                    n = -n;
                }
                p_h -= t;
            }

            t = p_l + p_h;
            iS = (int)t.RawValue;
            t = DFP.FromRaw((uint)iS & 0xffff8000);
            u = t * DFP.FromRaw(LG2_H_U32);
            v = (p_l - (t - p_h)) * DFP.FromRaw(LG2_U32) + t * DFP.FromRaw(LG2_L_U32);
            z = u + v;
            w = v - (z - u);
            t = z * z;
            t1 = z - t * (DFP.FromRaw(P1_U32) + t * (DFP.FromRaw(P2_U32) + t * (DFP.FromRaw(P3_U32) + t * (DFP.FromRaw(P4_U32) + t * DFP.FromRaw(P5_U32)))));
            r = (z * t1) / (t1 - DFP.FromRaw(0x40000000)) - (w + z * w);
            z = DFP.One - (r - z);
            j = (int)z.RawValue;
            j += n << 23;
            if ((j >> 23) <= 0)
            {
                /* subnormal output */
                z = ScalBN(z, n);
            }
            else
            {
                z = DFP.FromRaw((uint)j);
            }

            return sn * z;
        }
    }
}