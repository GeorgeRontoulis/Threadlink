namespace Threadlink.Tests.ECS
{
    using NUnit.Framework;
    using Threadlink.ECS;

    [TestFixture]
    internal sealed class ECSFilterTests
    {
        private ECSWorld world;

        [OneTimeSetUp]
        public void Setup()
        {
            world = new ECSWorld();
            world.Boot(); // Runs ComponentRegistry.Hydrate — assigns BitIndices
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            world.Discard();
            world = null;
        }

        [Test]
        public void DefaultFilter_MatchesEmptyMask()
        {
            Assert.IsTrue(default(ECSFilter).Matches(default));
        }

        [Test]
        public void DefaultFilter_MatchesAnyPopulatedMask()
        {
            var mask = default(ComponentMask);
            mask.Set(5);
            mask.Set(42);
            Assert.IsTrue(default(ECSFilter).Matches(mask));
        }

        [Test]
        public void With_SetsIncludeBit()
        {
            var filter = new ECSFilter().With<TestCompA>();
            Assert.IsTrue(filter.Include.Has(ComponentType.Of<TestCompA>.BitIndex));
        }

        [Test]
        public void With_LeavesExcludeEmpty()
        {
            var filter = new ECSFilter().With<TestCompA>();
            Assert.IsTrue(filter.Exclude.IsEmpty);
        }

        [Test]
        public void ChainedWith_SetsBothIncludeBits()
        {
            var filter = new ECSFilter().With<TestCompA>().With<TestCompB>();
            Assert.IsTrue(filter.Include.Has(ComponentType.Of<TestCompA>.BitIndex));
            Assert.IsTrue(filter.Include.Has(ComponentType.Of<TestCompB>.BitIndex));
        }

        [Test]
        public void With_IsImmutable_OriginalUnchanged()
        {
            var original = default(ECSFilter);
            var extended = original.With<TestCompA>();
            Assert.IsTrue(original.Include.IsEmpty);
            Assert.IsFalse(extended.Include.IsEmpty);
        }

        [Test]
        public void Without_SetsExcludeBit()
        {
            var filter = new ECSFilter().Without<TestCompB>();
            Assert.IsTrue(filter.Exclude.Has(ComponentType.Of<TestCompB>.BitIndex));
        }

        [Test]
        public void Without_LeavesIncludeEmpty()
        {
            var filter = new ECSFilter().Without<TestCompB>();
            Assert.IsTrue(filter.Include.IsEmpty);
        }

        [Test]
        public void Without_IsImmutable_OriginalUnchanged()
        {
            var original = default(ECSFilter);
            var extended = original.Without<TestCompA>();
            Assert.IsTrue(original.Exclude.IsEmpty);
            Assert.IsFalse(extended.Exclude.IsEmpty);
        }

        [Test]
        public void Matches_TrueWhenEntityHasAllIncludedComponents()
        {
            var filter = new ECSFilter().With<TestCompA>().With<TestCompB>();

            var mask = default(ComponentMask);
            mask.Set(ComponentType.Of<TestCompA>.BitIndex);
            mask.Set(ComponentType.Of<TestCompB>.BitIndex);

            Assert.IsTrue(filter.Matches(mask));
        }

        [Test]
        public void Matches_TrueWhenEntityHasMoreThanRequired()
        {
            var filter = new ECSFilter().With<TestCompA>();

            var mask = default(ComponentMask);
            mask.Set(ComponentType.Of<TestCompA>.BitIndex);
            mask.Set(ComponentType.Of<TestCompB>.BitIndex); // Extra component — should still match

            Assert.IsTrue(filter.Matches(mask));
        }

        [Test]
        public void Matches_FalseWhenEntityMissingIncludedComponent()
        {
            var filter = new ECSFilter().With<TestCompA>().With<TestCompB>();

            var mask = default(ComponentMask);
            mask.Set(ComponentType.Of<TestCompA>.BitIndex); // Missing B

            Assert.IsFalse(filter.Matches(mask));
        }

        [Test]
        public void Matches_FalseForEmptyMaskWithNonEmptyInclude()
        {
            var filter = new ECSFilter().With<TestCompA>();
            Assert.IsFalse(filter.Matches(default(ComponentMask)));
        }

        [Test]
        public void Matches_FalseWhenEntityHasExcludedComponent()
        {
            var filter = new ECSFilter().With<TestCompA>().Without<TestCompB>();

            var mask = default(ComponentMask);
            mask.Set(ComponentType.Of<TestCompA>.BitIndex);
            mask.Set(ComponentType.Of<TestCompB>.BitIndex); // Excluded

            Assert.IsFalse(filter.Matches(mask));
        }

        [Test]
        public void Matches_TrueWhenNoExcludedComponentIsPresent()
        {
            var filter = new ECSFilter().With<TestCompA>().Without<TestCompB>();

            var mask = default(ComponentMask);
            mask.Set(ComponentType.Of<TestCompA>.BitIndex); // B absent

            Assert.IsTrue(filter.Matches(mask));
        }

        [Test]
        public void Matches_FalseWhenExcludeOnlyAndEntityHasExcluded()
        {
            var filter = new ECSFilter().Without<TestCompC>();

            var mask = default(ComponentMask);
            mask.Set(ComponentType.Of<TestCompC>.BitIndex);

            Assert.IsFalse(filter.Matches(mask));
        }

        [Test]
        public void Matches_TrueWhenExcludeOnlyAndEntityLacksExcluded()
        {
            var filter = new ECSFilter().Without<TestCompC>();

            var mask = default(ComponentMask);
            mask.Set(ComponentType.Of<TestCompA>.BitIndex);

            Assert.IsTrue(filter.Matches(mask));
        }
    }
}
