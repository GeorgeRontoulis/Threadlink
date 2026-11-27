namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    public sealed class StatelessRngTests
    {
        private static readonly StatelessRNG.Domain Loot = new StatelessRNG.Domain(1);
        private static readonly StatelessRNG.Domain Weather = new StatelessRNG.Domain(2);

        private readonly struct Attack : StatelessRNG.IContext
        {
            public readonly uint Attacker, Target, Tick;

            public Attack(uint attacker, uint target, uint tick)
            {
                Attacker = attacker;
                Target = target;
                Tick = tick;
            }

            public void Compose(ref StatelessRNG.Identity identity)
            {
                identity.Add(Attacker);
                identity.Add(Target);
                identity.Add(Tick);
            }
        }

        [Test]
        public void SameInputsGiveSameNumbersRegardlessOfOtherDraws()
        {
            var rng = new StatelessRNG.Source(0xC0FFEEUL);
            var first = rng.CreateStream(Loot, 7, 3);
            var noise = rng.CreateStream(Weather, 7, 3);
            for (int i = 0; i < 100; i++)
                noise.NextUInt64();
            var second = new StatelessRNG.Source(0xC0FFEEUL).CreateStream(Loot, 7, 3);
            for (int i = 0; i < 64; i++)
                Assert.That(first.NextUInt64(), Is.EqualTo(second.NextUInt64()));
        }

        [Test]
        public void DomainsSeedsAndPartOrderSeparateStreams()
        {
            var rng = new StatelessRNG.Source(42);
            ulong a = rng.CreateStream(Loot, 1, 2).NextUInt64();
            Assert.That(rng.CreateStream(Loot, 2, 1).NextUInt64(), Is.Not.EqualTo(a));
            Assert.That(rng.CreateStream(Weather, 1, 2).NextUInt64(), Is.Not.EqualTo(a));
            Assert.That(new StatelessRNG.Source(43).CreateStream(Loot, 1, 2).NextUInt64(), Is.Not.EqualTo(a));
            Span<ulong> parts = stackalloc ulong[] { 1, 2 };
            Assert.That(rng.CreateStream(Loot, parts).NextUInt64(), Is.EqualTo(a));
            Assert.That(rng.CreateStream(Loot, new Attack(1, 2, 3)).NextUInt64(), Is.EqualTo(rng.CreateStream(Loot, 1, 2, 3).NextUInt64()));
        }

        [Test]
        public void ForksAreIndependentAndCopiesReplay()
        {
            var stream = new StatelessRNG.Source(9).CreateStream(Loot, 5);
            var copy = stream;
            ulong next = stream.NextUInt64();
            Assert.That(copy.NextUInt64(), Is.EqualTo(next));
            Assert.That(stream.Position, Is.EqualTo(1UL));
            var fork = stream.Fork(1);
            Assert.That(fork.NextUInt64(), Is.Not.EqualTo(stream.Fork(2).NextUInt64()));
        }

        [Test]
        public void RangesStayInBoundsAndCoverEveryValue()
        {
            var stream = new StatelessRNG.Source(1).CreateStream(Loot);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int value = stream.Range(-3, 4);
                Assert.That(value, Is.InRange(-3, 3));
                seen.Add(value);
            }
            Assert.That(seen, Has.Count.EqualTo(7));
            Assert.That(stream.Range(5, 5), Is.EqualTo(5));
            Assert.That(stream.Range(int.MinValue, int.MaxValue), Is.InRange(int.MinValue, int.MaxValue - 1));
        }

        [Test]
        public void NamedDomainsAndPartsAreStableAndCaseInsensitive()
        {
            Assert.That(StatelessRNG.Part("  Loot "), Is.EqualTo(StatelessRNG.Part("loot")));
            Assert.That(StatelessRNG.Domain.Named("Loot"), Is.EqualTo(StatelessRNG.Domain.Named("LOOT")));
            Assert.That(StatelessRNG.Domain.Named("Loot").Id, Is.EqualTo(unchecked((uint)StatelessRNG.Part("loot"))));
        }
    }
}
