namespace Threadlink.Tests.ECS
{
    using NUnit.Framework;
    using Threadlink.ECS;

    [TestFixture]
    internal sealed class ComponentMaskTests
    {
        [Test]
        public void Set_And_Has_Segment0()
        {
            var mask = default(ComponentMask);
            mask.Set(0);
            mask.Set(63);
            Assert.IsTrue(mask.Has(0));
            Assert.IsTrue(mask.Has(63));
            Assert.IsFalse(mask.Has(1));
        }

        [Test]
        public void Set_And_Has_Segment1()
        {
            var mask = default(ComponentMask);
            mask.Set(64);
            mask.Set(127);
            Assert.IsTrue(mask.Has(64));
            Assert.IsTrue(mask.Has(127));
            Assert.IsFalse(mask.Has(63));
            Assert.IsFalse(mask.Has(128));
        }

        [Test]
        public void Set_And_Has_Segment2()
        {
            var mask = default(ComponentMask);
            mask.Set(128);
            Assert.IsTrue(mask.Has(128));
            Assert.IsFalse(mask.Has(127));
        }

        [Test]
        public void Set_And_Has_Segment3()
        {
            var mask = default(ComponentMask);
            mask.Set(192);
            mask.Set(255);
            Assert.IsTrue(mask.Has(192));
            Assert.IsTrue(mask.Has(255));
            Assert.IsFalse(mask.Has(191));
        }

        [Test]
        public void Clear_RemovesBit()
        {
            var mask = default(ComponentMask);
            mask.Set(5);
            mask.Set(70);
            mask.Clear(5);
            Assert.IsFalse(mask.Has(5));
            Assert.IsTrue(mask.Has(70));
        }

        [Test]
        public void IsEmpty_TrueForDefault()
        {
            Assert.IsTrue(default(ComponentMask).IsEmpty);
        }

        [Test]
        public void IsEmpty_FalseAfterSet()
        {
            var mask = default(ComponentMask);
            mask.Set(0);
            Assert.IsFalse(mask.IsEmpty);
        }

        [Test]
        public void Matches_TrueWhenAllBitsPresent()
        {
            var entity = default(ComponentMask);
            entity.Set(1);
            entity.Set(2);
            entity.Set(3);

            var filter = default(ComponentMask);
            filter.Set(1);
            filter.Set(2);

            Assert.IsTrue(entity.Matches(filter));
        }

        [Test]
        public void Matches_FalseWhenBitMissing()
        {
            var entity = default(ComponentMask);
            entity.Set(1);

            var filter = default(ComponentMask);
            filter.Set(1);
            filter.Set(2);

            Assert.IsFalse(entity.Matches(filter));
        }

        [Test]
        public void Matches_TrueForEmptyFilter()
        {
            var entity = default(ComponentMask);
            entity.Set(7);
            Assert.IsTrue(entity.Matches(default(ComponentMask)));
        }

        [Test]
        public void HasAnyFrom_TrueWhenOneOverlaps()
        {
            var entity = default(ComponentMask);
            entity.Set(5);

            var exclude = default(ComponentMask);
            exclude.Set(5);
            exclude.Set(10);

            Assert.IsTrue(entity.HasAnyFrom(exclude));
        }

        [Test]
        public void HasAnyFrom_FalseWhenNoOverlap()
        {
            var entity = default(ComponentMask);
            entity.Set(1);
            entity.Set(2);

            var exclude = default(ComponentMask);
            exclude.Set(3);
            exclude.Set(4);

            Assert.IsFalse(entity.HasAnyFrom(exclude));
        }

        [Test]
        public void HasAnyFrom_FalseForEmptyMask()
        {
            var entity = default(ComponentMask);
            entity.Set(0);
            Assert.IsFalse(entity.HasAnyFrom(default(ComponentMask)));
        }

        [Test]
        public void Enumerator_YieldsAllSetBits()
        {
            var mask = default(ComponentMask);
            int[] expected = { 0, 7, 63, 64, 200, 255 };

            foreach (var b in expected) mask.Set(b);

            var found = new System.Collections.Generic.List<int>();
            foreach (var b in mask) found.Add(b);

            Assert.AreEqual(expected.Length, found.Count);

            foreach (var b in expected)
                Assert.IsTrue(found.Contains(b), $"Bit {b} was not yielded by enumerator");
        }

        [Test]
        public void Enumerator_EmptyMaskYieldsNothing()
        {
            int count = 0;
            foreach (var _ in default(ComponentMask)) count++;
            Assert.AreEqual(0, count);
        }
    }
}
