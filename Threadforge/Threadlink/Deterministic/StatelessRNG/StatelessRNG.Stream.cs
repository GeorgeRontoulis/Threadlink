namespace Threadlink.Deterministic
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public static partial class StatelessRNG
    {
        /// <summary>
        /// The numbers of one identity, in order. Each draw advances this copy only: a copy made earlier replays the same
        /// numbers, and two streams created from the same identity give the same ones. Create one per purpose, entity and
        /// tick rather than keeping one across ticks, so a result never depends on how many draws came before it.
        /// <para/>
        /// Every sampler is unbiased and makes the same draws whatever its arguments' values, so a probability of zero
        /// advances the stream as much as any other.
        /// </summary>
        public struct Stream
        {
            private readonly ulong key;
            private ulong position;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Stream(ulong key)
            {
                this.key = key;
                position = 0UL;
            }

            /// <summary>How many 64-bit draws this stream has made.</summary>
            public readonly ulong Position => position;

            /// <summary>
            /// A stream for a finer identity, independent of this one and of its draws: an item within an entity's loot,
            /// one enemy within a wave.
            /// </summary>
            public readonly Stream Fork(ulong part) => new(Fold(key, part));

            #region Raw draws:
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ulong NextUInt64()
            {
                position++;
                return Mix(unchecked(key + position * GAMMA));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint NextUInt32() => (uint)(NextUInt64() >> 32);
            #endregion

            #region Integers:
            /// <summary>
            /// An integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>), every value equally likely.
            /// An empty range returns <paramref name="minInclusive"/>.
            /// </summary>
            public int Range(int minInclusive, int maxExclusive)
            {
                // The span as unsigned, so even int.MinValue to int.MaxValue fits.
                uint span = unchecked((uint)(maxExclusive - minInclusive));

                if (maxExclusive <= minInclusive)
                {
                    NextUInt64();
                    return minInclusive;
                }

                return unchecked(minInclusive + (int)Below(span));
            }

            /// <summary>An index into <paramref name="count"/> items, every one equally likely; 0 when there are none.</summary>
            public int Index(int count) => Range(0, count);

            /// <summary>Heads or tails.</summary>
            public bool Bool() => (long)NextUInt64() < 0L;

            /// <summary>True in <paramref name="numerator"/> of every <paramref name="denominator"/> draws, exactly: <c>Chance(1, 20)</c>.</summary>
            public bool Chance(int numerator, int denominator) => Range(0, denominator) < numerator;
            #endregion

            #region Fixed point:
            /// <summary>
            /// True with <paramref name="probability"/>: never at 0 or below, always at 1 or above. Each of the 2^32
            /// values in [0, 1) is equally likely to be drawn, so the probability is exact to the last raw unit.
            /// </summary>
            public bool Chance(FP probability) => (long)(NextUInt64() >> 32) < probability.RawValue;

            /// <summary>A value in [0, 1): each of its 2^32 fixed-point values equally likely.</summary>
            public FP Value01() => FP.FromRaw((long)(NextUInt64() >> 32));

            /// <summary>
            /// A value in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>), every raw value equally
            /// likely. An empty range returns <paramref name="minInclusive"/>.
            /// </summary>
            public FP Range(FP minInclusive, FP maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                {
                    NextUInt64();
                    return minInclusive;
                }

                ulong span = unchecked((ulong)(maxExclusive.RawValue - minInclusive.RawValue));
                return FP.FromRaw(unchecked(minInclusive.RawValue + (long)Below(span)));
            }

            /// <summary>An angle in [0, 2π).</summary>
            public FP Angle() => Range(FP.Zero, FP.TwoPi);
            #endregion

            #region Geometry:
            /// <summary>A direction: a point on the unit circle, all equally likely.</summary>
            public FPVector2 OnUnitCircle() => FPVector2.FromAngle(Angle());

            /// <summary>A point in the unit disc, spread evenly over its area.</summary>
            public FPVector2 InsideUnitCircle()
            {
                FPVector2 direction = OnUnitCircle();
                return direction * FP.Sqrt(Value01());
            }

            /// <summary>A direction in 3D: a point on the unit sphere, all equally likely.</summary>
            public FPVector3 OnUnitSphere()
            {
                // Archimedes: height uniform in [−1, 1], angle uniform around it.
                FP z = Range(FP.MinusOne, FP.One);
                FP radius = FP.Sqrt((FP.One - z) * (FP.One + z));

                FP.SinCos(Angle(), out FP sin, out FP cos);
                return new FPVector3(radius * cos, radius * sin, z);
            }

            /// <summary>A point in the unit ball, spread evenly over its volume.</summary>
            public FPVector3 InsideUnitSphere()
            {
                // Points of the cube until one lands inside the ball: about two tries on average.
                while (true)
                {
                    var point = new FPVector3(Range(FP.MinusOne, FP.One), Range(FP.MinusOne, FP.One), Range(FP.MinusOne, FP.One));

                    if (point.SqrMagnitude <= FP.One)
                        return point;
                }
            }

            /// <summary>A rotation, all equally likely (Shoemake's method).</summary>
            public FPQuaternion Rotation()
            {
                FP u = Value01();
                FP a = FP.Sqrt(FP.One - u);
                FP b = FP.Sqrt(u);

                FP.SinCos(Angle(), out FP sin1, out FP cos1);
                FP.SinCos(Angle(), out FP sin2, out FP cos2);

                return new FPQuaternion(a * sin1, a * cos1, b * sin2, b * cos2);
            }
            #endregion

            #region Collections:
            /// <summary>One of <paramref name="items"/>, every one equally likely; default when empty.</summary>
            public T Pick<T>(ReadOnlySpan<T> items)
            {
                int index = Index(items.Length);
                return items.Length == 0 ? default : items[index];
            }

            /// <summary>One of <paramref name="items"/>, every one equally likely; default when empty.</summary>
            public T Pick<T>(T[] items) => Pick((ReadOnlySpan<T>)items);

            /// <summary>One of <paramref name="items"/>, every one equally likely; default when empty.</summary>
            public T Pick<T>(IReadOnlyList<T> items)
            {
                int index = Index(items.Count);
                return items.Count == 0 ? default : items[index];
            }

            /// <summary>Put <paramref name="items"/> in a random order, every order equally likely (Fisher–Yates).</summary>
            public void Shuffle<T>(Span<T> items)
            {
                for (int i = items.Length - 1; i > 0; i--)
                {
                    int j = Range(0, i + 1);
                    (items[i], items[j]) = (items[j], items[i]);
                }
            }

            /// <summary>Put <paramref name="items"/> in a random order, every order equally likely (Fisher–Yates).</summary>
            public void Shuffle<T>(T[] items) => Shuffle(items.AsSpan());

            /// <summary>Put <paramref name="items"/> in a random order, every order equally likely (Fisher–Yates).</summary>
            public void Shuffle<T>(IList<T> items)
            {
                for (int i = items.Count - 1; i > 0; i--)
                {
                    int j = Range(0, i + 1);
                    (items[i], items[j]) = (items[j], items[i]);
                }
            }

            /// <summary>
            /// An index chosen in proportion to <paramref name="weights"/>: a weight of 3 is picked three times as often as a
            /// weight of 1. Zero and negative weights are never picked; −1 when no weight is positive.
            /// </summary>
            public int WeightedIndex(ReadOnlySpan<int> weights)
            {
                ulong total = 0UL;

                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] > 0)
                        total += (ulong)weights[i];
                }

                return PickWeighted(weights, total);
            }

            /// <summary>
            /// An index chosen in proportion to <paramref name="weights"/>, exact to the raw unit. Zero and negative weights
            /// are never picked; −1 when no weight is positive. The positive weights must sum below 2^63 raw.
            /// </summary>
            public int WeightedIndex(ReadOnlySpan<FP> weights)
            {
                ulong total = 0UL;

                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i].RawValue > 0L)
                        total += (ulong)weights[i].RawValue;
                }

                if (total == 0UL)
                {
                    NextUInt64();
                    return -1;
                }

                ulong target = Below(total);

                for (int i = 0; i < weights.Length; i++)
                {
                    long weight = weights[i].RawValue;

                    if (weight <= 0L)
                        continue;

                    if (target < (ulong)weight)
                        return i;

                    target -= (ulong)weight;
                }

                return -1;
            }

            private int PickWeighted(ReadOnlySpan<int> weights, ulong total)
            {
                if (total == 0UL)
                {
                    NextUInt64();
                    return -1;
                }

                ulong target = Below(total);

                for (int i = 0; i < weights.Length; i++)
                {
                    int weight = weights[i];

                    if (weight <= 0)
                        continue;

                    if (target < (ulong)weight)
                        return i;

                    target -= (ulong)weight;
                }

                return -1;
            }
            #endregion

            #region Unbiased bounds (Lemire):
            /// <summary>
            /// A value in [0, <paramref name="bound"/>) for a nonzero bound, every value equally likely: the high word of a
            /// draw times the bound, redrawn in the rare case the low word falls in the few values that would favour some
            /// results.
            /// </summary>
            private uint Below(uint bound)
            {
                ulong product = (ulong)NextUInt32() * bound;

                if ((uint)product < bound)
                {
                    uint threshold = unchecked(0U - bound) % bound;

                    while ((uint)product < threshold)
                        product = (ulong)NextUInt32() * bound;
                }

                return (uint)(product >> 32);
            }

            /// <inheritdoc cref="Below(uint)"/>
            private ulong Below(ulong bound)
            {
                ulong high = FP.MultiplyHigh(NextUInt64(), bound, out ulong low);

                if (low < bound)
                {
                    ulong threshold = unchecked(0UL - bound) % bound;

                    while (low < threshold)
                        high = FP.MultiplyHigh(NextUInt64(), bound, out low);
                }

                return high;
            }
            #endregion
        }
    }
}
