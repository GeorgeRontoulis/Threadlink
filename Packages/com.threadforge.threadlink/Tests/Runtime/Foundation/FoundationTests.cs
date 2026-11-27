namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using NUnit.Framework;

    public class FoundationTests
    {
        private sealed class Cleanup : IDisposable
        {
            private readonly Action action;

            public Cleanup(Action action) => this.action = action;

            public void Dispose() => action();
        }

        [Test]
        public void CleanupIsReverseOrderedAndReentrantSafe()
        {
            var calls = new List<int>();
            var owner = new ResourceOwner();
            owner.Own(new Cleanup(() => calls.Add(1)));
            owner.Own(
                new Cleanup(() =>
                {
                    owner.Dispose();
                    calls.Add(2);
                })
            );
            owner.Dispose();
            owner.Dispose();
            CollectionAssert.AreEqual(new[] { 2, 1 }, calls);
        }

        [Test]
        public void CleanupFailuresDoNotLeakRemainingResources()
        {
            var owner = new ResourceOwner();
            int disposed = 0;
            owner.Own(new Cleanup(() => disposed++));
            owner.Own(new Cleanup(() => throw new InvalidOperationException("expected")));
            var error = Assert.Throws<AggregateException>(() => owner.Dispose());
            Assert.That(error.InnerExceptions.Count, Is.EqualTo(1));
            Assert.That(disposed, Is.EqualTo(1));
            Assert.That(owner.IsDisposed, Is.True);
        }

        [Test]
        public void ClosedAdmissionReleasesResource()
        {
            var owner = new ResourceOwner();
            owner.Dispose();
            int disposed = 0;
            Assert.Throws<ObjectDisposedException>(() => owner.Own(new Cleanup(() => disposed++)));
            Assert.That(disposed, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentAdmissionAndClosureReleaseEveryResourceOnce()
        {
            var owner = new ResourceOwner();
            int disposed = 0;
            var worker = new Thread(() =>
            {
                for (int i = 0; i < 1000; i++)
                    try
                    {
                        owner.Own(new Cleanup(() => Interlocked.Increment(ref disposed)));
                    }
                    catch (ObjectDisposedException) { }
            });
            worker.Start();
            owner.Dispose();
            Assert.That(worker.Join(5000), Is.True);
            Assert.That(disposed, Is.EqualTo(1000));
        }

        [Test]
        public void IdentityRoundTripsWithoutRenamingOrRegeneration()
        {
            const string text = "c15c72b2-8cff-4532-9998-d4d12a3ec5ad";
            Assert.That(StableId.TryParse(text, out var id), Is.True);
            Assert.That(id.ToString(), Is.EqualTo(text));
            Assert.That(id, Is.EqualTo(new StableId(new Guid(text))));
            Assert.That(StableId.TryParse(Guid.Empty.ToString("D"), out _), Is.False);
            Assert.That(StableId.TryParse("display name", out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => default(StableId).ToString());
        }
    }
}
