namespace Threadlink.Tests.ECS
{
    using NUnit.Framework;
    using Threadlink.ECS;

    internal struct TestCompA : IComponent { public int Value; public readonly void Dispose() { } }
    internal struct TestCompB : IComponent { public float Value; public readonly void Dispose() { } }
    internal struct TestCompC : IComponent { public bool Flag; public readonly void Dispose() { } }

    [TestFixture]
    internal sealed unsafe class ECSWorldTests
    {
        private ECSWorld world;

        // Static accumulator for function-pointer callbacks (no closures allowed)
        private static int s_callCount;
        private static readonly System.Collections.Generic.List<Entity> s_entities = new();

        [OneTimeSetUp]
        public void Setup()
        {
            world = new ECSWorld();
            world.Boot(); // Runs ComponentRegistry.Hydrate + allocates native lists
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            world.Discard();
            world = null;
        }

        [SetUp]
        public void PerTestSetup()
        {
            s_callCount = 0;
            s_entities.Clear();
        }

        [Test]
        public void CreateNewEntity_IsValid()
        {
            var e = world.CreateNewEntity();
            Assert.IsTrue(world.IsValid(e));
            world.Destroy(e);
        }

        [Test]
        public void Destroy_InvalidatesEntity()
        {
            var e = world.CreateNewEntity();
            world.Destroy(e);
            Assert.IsFalse(world.IsValid(e));
        }

        [Test]
        public void Destroyed_ID_IsReused_With_NewGeneration()
        {
            var e1 = world.CreateNewEntity();
            int id = e1.ID;
            world.Destroy(e1);

            var e2 = world.CreateNewEntity();
            Assert.AreEqual(id, e2.ID);
            Assert.AreNotEqual(e1.Generation, e2.Generation);
        }

        [Test]
        public void Add_And_Has_Component()
        {
            var e = world.CreateNewEntity();
            world.Add<TestCompA>(e);
            Assert.IsTrue(world.Has<TestCompA>(e));
            world.Destroy(e);
        }

        [Test]
        public void TryGetPointer_ReturnsPointerAfterAdd()
        {
            var e = world.CreateNewEntity();
            var ptr = world.Add<TestCompA>(e);
            ptr->Value = 99;

            bool found = world.TryGetPointer<TestCompA>(e, out var readPtr);
            Assert.IsTrue(found);
            Assert.AreEqual(99, readPtr->Value);
            world.Destroy(e);
        }

        [Test]
        public void Has_ReturnsFalse_For_UnattachedComponent()
        {
            var e = world.CreateNewEntity();
            world.Add<TestCompA>(e);
            Assert.IsFalse(world.Has<TestCompB>(e));
            world.Destroy(e);
        }

        [Test]
        public void Destroy_Removes_All_Components()
        {
            var e = world.CreateNewEntity();
            world.Add<TestCompA>(e);
            world.Add<TestCompB>(e);
            world.Destroy(e);

            // Entity is no longer valid, so Contains must return false
            Assert.IsFalse(world.Contains(e));
        }

        [Test]
        public void ForEach_T1_VisitsAllEntitiesWithComponent()
        {
            var e1 = world.CreateNewEntity();
            var e2 = world.CreateNewEntity();
            var e3 = world.CreateNewEntity(); // No TestCompA

            world.Add<TestCompA>(e1);
            world.Add<TestCompA>(e2);
            world.Add<TestCompB>(e3);

            world.ForEach<TestCompA>(&CollectEntity_A);

            Assert.GreaterOrEqual(s_callCount, 2);
            Assert.IsTrue(s_entities.Contains(e1));
            Assert.IsTrue(s_entities.Contains(e2));
            Assert.IsFalse(s_entities.Contains(e3));

            world.Destroy(e1);
            world.Destroy(e2);
            world.Destroy(e3);
        }

        [Test]
        public void ForEach_T1_T2_OnlyVisitsEntitiesWithBothComponents()
        {
            var e1 = world.CreateNewEntity();
            var e2 = world.CreateNewEntity(); // Missing B

            world.Add<TestCompA>(e1);
            world.Add<TestCompB>(e1);
            world.Add<TestCompA>(e2);

            world.ForEach<TestCompA, TestCompB>(&CollectEntity_AB);

            Assert.AreEqual(1, s_callCount);
            Assert.IsTrue(s_entities.Contains(e1));
            Assert.IsFalse(s_entities.Contains(e2));

            world.Destroy(e1);
            world.Destroy(e2);
        }

        [Test]
        public void FilteredForEach_ExcludesMaskedEntities()
        {
            var included = world.CreateNewEntity();
            var excluded = world.CreateNewEntity();

            world.Add<TestCompA>(included);             // has A, no B
            world.Add<TestCompA>(excluded);
            world.Add<TestCompB>(excluded);             // has A and B

            var filter = new ECSFilter().With<TestCompA>().Without<TestCompB>();

            world.ForEach(in filter, &CollectEntity_Untyped);

            Assert.AreEqual(1, s_callCount);
            Assert.IsTrue(s_entities.Contains(included));
            Assert.IsFalse(s_entities.Contains(excluded));

            world.Destroy(included);
            world.Destroy(excluded);
        }

        [Test]
        public void FilteredForEach_T1_RequiresIncludeComponent()
        {
            var e1 = world.CreateNewEntity();
            var e2 = world.CreateNewEntity(); // A only, no C

            world.Add<TestCompA>(e1);
            world.Add<TestCompC>(e1);
            world.Add<TestCompA>(e2);

            var filter = new ECSFilter().With<TestCompA>().With<TestCompC>();

            world.ForEach<TestCompA>(in filter, &CollectEntity_A);

            Assert.AreEqual(1, s_callCount);
            Assert.IsTrue(s_entities.Contains(e1));
            Assert.IsFalse(s_entities.Contains(e2));

            world.Destroy(e1);
            world.Destroy(e2);
        }

        [Test]
        public void UntypedFilteredForEach_MatchesAllIncludeComponents()
        {
            var e1 = world.CreateNewEntity();
            var e2 = world.CreateNewEntity();

            world.Add<TestCompA>(e1);
            world.Add<TestCompB>(e1);
            world.Add<TestCompC>(e1);
            world.Add<TestCompA>(e2); // Missing B and C

            var filter = new ECSFilter().With<TestCompA>().With<TestCompB>().With<TestCompC>();

            world.ForEach(in filter, &CollectEntity_Untyped);

            Assert.AreEqual(1, s_callCount);
            Assert.IsTrue(s_entities.Contains(e1));

            world.Destroy(e1);
            world.Destroy(e2);
        }

        private static void CollectEntity_Untyped(in Entity entity)
        {
            s_callCount++;
            s_entities.Add(entity);
        }

        private static void CollectEntity_A(in Entity entity, TestCompA* _)
        {
            s_callCount++;
            s_entities.Add(entity);
        }

        private static void CollectEntity_AB(in Entity entity, TestCompA* _, TestCompB* __)
        {
            s_callCount++;
            s_entities.Add(entity);
        }
    }
}
