namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Numerics;
    using NUnit.Framework;

    public class FixedPointTests
    {
        private static long Clamp(BigInteger value) =>
            value > long.MaxValue ? long.MaxValue
            : value < long.MinValue ? long.MinValue
            : (long)value;

        private static BigInteger Round(BigInteger value, BigInteger denominator)
        {
            int sign = value.Sign;
            BigInteger result = BigInteger.DivRem(
                BigInteger.Abs(value),
                denominator,
                out var remainder
            );
            if (remainder * 2 >= denominator)
                result++;
            return sign * result;
        }

        [TestCase(-1500000000, 1500000000)]
        [TestCase(int.MinValue, int.MaxValue)]
        [TestCase(1500000000, -1500000000)]
        [TestCase(7, 7)]
        public void InterpolationPreservesEndpoints(int a, int b)
        {
            Assert.That(FP.Lerp(a, b, FP.Zero), Is.EqualTo((FP)a));
            Assert.That(FP.Lerp(a, b, FP.One), Is.EqualTo((FP)b));
            Assert.That(FP.Lerp(a, b, -1), Is.EqualTo((FP)a));
            Assert.That(FP.Lerp(a, b, 2), Is.EqualTo((FP)b));
        }

        [Test]
        public void ArithmeticAndInterpolationMatchIndependentIntegerOracle()
        {
            var random = new Random(431);
            var bytes = new byte[8];
            long Next()
            {
                random.NextBytes(bytes);
                return BitConverter.ToInt64(bytes, 0);
            }
            for (int i = 0; i < 4000; i++)
            {
                long a = Next(),
                    b = Next(),
                    t = Next();
                var x = FP.FromRaw(a);
                var y = FP.FromRaw(b);
                Assert.That((x + y).RawValue, Is.EqualTo(Clamp((BigInteger)a + b)));
                Assert.That((x - y).RawValue, Is.EqualTo(Clamp((BigInteger)a - b)));
                Assert.That(
                    (x * y).RawValue,
                    Is.EqualTo(Clamp(Round((BigInteger)a * b, BigInteger.One << 32)))
                );
                if (b != 0)
                    Assert.That(
                        (x / y).RawValue,
                        Is.EqualTo(
                            Clamp(Round((BigInteger)a * Math.Sign(b) << 32, BigInteger.Abs(b)))
                        )
                    );
                var exact = ((BigInteger)a << 32) + ((BigInteger)b - a) * t;
                Assert.That(
                    FP.LerpUnclamped(x, y, FP.FromRaw(t)).RawValue,
                    Is.EqualTo(Clamp(Round(exact, BigInteger.One << 32)))
                );
            }
        }

        [Test]
        public void InterpolationBoundaryAndTieCasesMatchOracle()
        {
            long[] values =
            {
                long.MinValue,
                long.MaxValue,
                -4294967296L,
                -1,
                0,
                1,
                2147483648L,
                4294967296L,
            };
            foreach (long a in values)
            foreach (long b in values)
            foreach (long t in values)
            {
                var exact = ((BigInteger)a << 32) + ((BigInteger)b - a) * t;
                Assert.That(
                    FP.LerpUnclamped(FP.FromRaw(a), FP.FromRaw(b), FP.FromRaw(t)).RawValue,
                    Is.EqualTo(Clamp(Round(exact, BigInteger.One << 32)))
                );
            }
        }

        [Test]
        public void VectorsInheritCorrectInterpolationAndNormalize()
        {
            var a = new FPVector3(-1500000000, 3, 4);
            var b = new FPVector3(1500000000, 3, 4);
            Assert.That(FPVector3.Lerp(a, b, FP.One), Is.EqualTo(b));
            Assert.That(FPVector3.Lerp(a, b, FP.Half).X, Is.EqualTo(FP.Zero));
            var unit = new FPVector3(0, 3, 4).Normalized;
            Assert.That(Math.Abs((double)unit.Magnitude - 1), Is.LessThan(1e-8));
            Assert.That(FPQuaternion.Identity * b, Is.EqualTo(b));
            Assert.That(new FPQuaternion().Normalized, Is.EqualTo(FPQuaternion.Identity));
        }

        [TestCase(-3)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(100)]
        public void SqrtAndZeroDivisionHaveDocumentedResults(int value)
        {
            Assert.That(
                (double)FP.Sqrt(value),
                Is.EqualTo(Math.Sqrt(Math.Max(0, value))).Within(1e-8)
            );
            Assert.That(
                (FP)value / FP.Zero,
                Is.EqualTo(
                    value == 0 ? FP.Zero
                    : value < 0 ? FP.MinValue
                    : FP.MaxValue
                )
            );
        }
    }
}
