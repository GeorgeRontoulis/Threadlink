namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using NUnit.Framework;

    public class EventBusTests
    {
        [Test]
        public void NestedMutationRetainsPublicationBoundary()
        {
            using var bus = new EventBus();
            var calls = new List<int>();
            SubscriptionToken second = default;
            bool nested = false;
            bus.Subscribe<int>(_ =>
            {
                calls.Add(1);
                second.Dispose();
                if (!nested)
                {
                    nested = true;
                    bus.Subscribe<int>(v => calls.Add(3));
                    bus.Publish(0);
                }
            });
            second = bus.Subscribe<int>(_ => calls.Add(2));
            bus.Publish(0);
            CollectionAssert.AreEqual(new[] { 1, 1, 3 }, calls);
        }

        [Test]
        public void CopiedStaleTokenCannotRemoveReusedRegistration()
        {
            using var bus = new EventBus();
            var old = bus.Subscribe<int>(_ => { });
            var copy = old;
            old.Dispose();
            int called = 0;
            using var current = bus.Subscribe<int>(_ => called++);
            copy.Dispose();
            bus.Publish(1);
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void IsolatedBusDoesNotReceiveOtherBusEvents()
        {
            using var first = new EventBus();
            using var second = new EventBus();
            int called = 0;
            second.Subscribe<int>(_ => called++);
            first.Publish(1);
            Assert.That(called, Is.Zero);
        }

        [Test]
        public void OrdinaryFailurePropagatesAndIsolatedReportingCannotInterrupt()
        {
            using var bus = new EventBus();
            var bad = bus.Subscribe<int>(_ => throw new InvalidOperationException());
            int called = 0;
            bus.Subscribe<int>(_ => called++);
            Assert.Throws<InvalidOperationException>(() => bus.Publish(0));
            bus.ObserverFailed = _ => throw new ArgumentException();
            bus.PublishIsolated(0);
            Assert.That(called, Is.EqualTo(1));
            Assert.That(bus.ObserverFailures, Is.EqualTo(2));
            bad.Dispose();
            bus.Publish(0);
            Assert.That(called, Is.EqualTo(2));
        }

        [Test]
        public void ClearDuringPublicationSkipsOldAndDefersNewListeners()
        {
            using var bus = new EventBus();
            int called = 0;
            bus.Subscribe<int>(_ =>
            {
                bus.Clear<int>();
                bus.Subscribe<int>(v => called += 10);
            });
            bus.Subscribe<int>(_ => called++);
            bus.Publish(0);
            Assert.That(called, Is.Zero);
            bus.Publish(0);
            Assert.That(called, Is.EqualTo(10));
        }

        [Test]
        public void DisposeDuringDeliveryClosesAdmissionAndLeavesTokensSafe()
        {
            var bus = new EventBus();
            bus.Subscribe<int>(_ => bus.Dispose());
            var token = bus.Subscribe<int>(_ => Assert.Fail("Disposed listener ran"));
            bus.Publish(0);
            token.Dispose();
            bus.Dispose();
            Assert.That(bus.TryPost(0), Is.False);
            Assert.Throws<ObjectDisposedException>(() => bus.Subscribe<int>(_ => { }));
        }

        [Test]
        public void WorkerIngressIsBoundedAndDefersPostsMadeDuringDrain()
        {
            using var bus = new EventBus(2);
            Exception wrongThread = null;
            var worker = new Thread(() =>
            {
                try
                {
                    bus.Publish(0);
                }
                catch (Exception e)
                {
                    wrongThread = e;
                }
                bus.TryPost(1);
                bus.TryPost(2);
                bus.TryPost(3);
            });
            worker.Start();
            Assert.That(worker.Join(5000), Is.True);
            Assert.That(wrongThread, Is.TypeOf<InvalidOperationException>());
            Assert.That(bus.RejectedPosts, Is.EqualTo(1));
            var calls = new List<int>();
            bus.Subscribe<int>(value =>
            {
                calls.Add(value);
                if (value == 1)
                {
                    bus.TryPost(4);
                    Assert.That(bus.DrainPosted(), Is.Zero);
                }
            });
            Assert.That(bus.DrainPosted(), Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 1, 2 }, calls);
            Assert.That(bus.DrainPosted(), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, calls);
        }

        [Test]
        public void WarmDispatchDoesNotAllocateManagedMemory()
        {
            using var bus = new EventBus();
            int sum = 0;
            using var listener = bus.Subscribe<int>(value => sum += value);
            for (int i = 0; i < 100; i++)
                bus.Publish(1);
            using var measurement = AllocationMeasurement.Start();
            for (int i = 0; i < 10000; i++)
                bus.Publish(1);
            measurement.AssertNoAllocations();
            Assert.That(sum, Is.EqualTo(10100));
        }
    }
}
