namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    public sealed class AsyncEventBusTests
    {
        private struct Saved
        {
            public int Slot;
        }

        private struct Load
        {
            public int Slot;
        }

        [UnityTest]
        public IEnumerator SequentialPublicationAwaitsListenersInOrder() =>
            AwaitableTest.Run(async () =>
            {
                using var bus = new AsyncEventBus();
                var order = new List<string>();
                var gate = new UniTaskCompletionSource();
                bus.Subscribe<Saved>(async (value, token) =>
                {
                    order.Add("first-start");
                    await gate.Task;
                    order.Add("first-end");
                });
                bus.Subscribe<Saved>((value, token) =>
                {
                    order.Add("second:" + value.Slot);
                    return UniTask.CompletedTask;
                });
                var publication = bus.PublishAsync(new Saved { Slot = 3 });
                Assert.That(order, Is.EqualTo(new[] { "first-start" }));
                gate.TrySetResult();
                await publication;
                Assert.That(order, Is.EqualTo(new[] { "first-start", "first-end", "second:3" }));
            });

        [UnityTest]
        public IEnumerator RemovalDuringPublicationSkipsPendingListenerAndAdditionsWaitForNextPublication() =>
            AwaitableTest.Run(async () =>
            {
                using var bus = new AsyncEventBus();
                var calls = new List<string>();
                AsyncSubscription second = default;
                bus.Subscribe<Saved>((value, token) =>
                {
                    calls.Add("first");
                    second.Dispose();
                    bus.Subscribe<Saved>((v, t) =>
                    {
                        calls.Add("added");
                        return UniTask.CompletedTask;
                    });
                    return UniTask.CompletedTask;
                });
                second = bus.Subscribe<Saved>((value, token) =>
                {
                    calls.Add("second");
                    return UniTask.CompletedTask;
                });
                await bus.PublishAsync(new Saved());
                Assert.That(calls, Is.EqualTo(new[] { "first" }));
                calls.Clear();
                second.Dispose();
                await bus.PublishAsync(new Saved());
                Assert.That(calls, Is.EqualTo(new[] { "first", "added" }));
            });

        [UnityTest]
        public IEnumerator CopiedTokensDoNotRemoveReplacementListeners() =>
            AwaitableTest.Run(async () =>
            {
                using var bus = new AsyncEventBus();
                int calls = 0;
                var original = bus.Subscribe<Saved>((v, t) => UniTask.CompletedTask);
                var copy = original;
                original.Dispose();
                bus.Subscribe<Saved>((v, t) =>
                {
                    calls++;
                    return UniTask.CompletedTask;
                });
                copy.Dispose();
                original.Dispose();
                await bus.PublishAsync(new Saved());
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(bus.ListenerCount<Saved>(), Is.EqualTo(1));
            });

        [UnityTest]
        public IEnumerator CancellationStopsBeforeNextListenerAndFailuresPropagate() =>
            AwaitableTest.Run(async () =>
            {
                using var bus = new AsyncEventBus();
                using var cancellation = new CancellationTokenSource();
                bool secondCalled = false;
                bus.Subscribe<Saved>((value, token) =>
                {
                    cancellation.Cancel();
                    return UniTask.CompletedTask;
                });
                bus.Subscribe<Saved>((value, token) =>
                {
                    secondCalled = true;
                    return UniTask.CompletedTask;
                });
                bool cancelled = false;
                try
                {
                    await bus.PublishAsync(new Saved(), cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                Assert.That(cancelled, Is.True);
                Assert.That(secondCalled, Is.False);

                using var failing = new AsyncEventBus();
                failing.Subscribe<Saved>((v, t) => throw new InvalidOperationException("listener"));
                Exception caught = null;
                try
                {
                    await failing.PublishAsync(new Saved());
                }
                catch (Exception error)
                {
                    caught = error;
                }
                Assert.That(caught, Is.TypeOf<InvalidOperationException>());
            });

        [UnityTest]
        public IEnumerator ConcurrentPublicationAwaitsAllListeners() =>
            AwaitableTest.Run(async () =>
            {
                using var bus = new AsyncEventBus();
                var first = new UniTaskCompletionSource();
                var second = new UniTaskCompletionSource();
                int started = 0;
                bus.Subscribe<Saved>(async (v, t) =>
                {
                    started++;
                    await first.Task;
                });
                bus.Subscribe<Saved>(async (v, t) =>
                {
                    started++;
                    await second.Task;
                });
                var publication = bus.PublishConcurrentAsync(new Saved());
                Assert.That(started, Is.EqualTo(2));
                first.TrySetResult();
                Assert.That(publication.Status.IsCompleted(), Is.False);
                second.TrySetResult();
                await publication;
            });

        [UnityTest]
        public IEnumerator RequestsRequireExactlyOneResponder() =>
            AwaitableTest.Run(async () =>
            {
                using var bus = new AsyncEventBus();
                Exception missing = null;
                try
                {
                    await bus.RequestAsync<Load, string>(new Load { Slot = 1 });
                }
                catch (Exception error)
                {
                    missing = error;
                }
                Assert.That(missing, Is.TypeOf<NoResponderException>());
                var responder = bus.Respond<Load, string>(async (request, token) =>
                {
                    await UniTask.NextFrame();
                    return "slot " + request.Slot;
                });
                Assert.Throws<InvalidOperationException>(() =>
                    bus.Respond<Load, string>((r, t) => UniTask.FromResult("duplicate"))
                );
                Assert.That(await bus.RequestAsync<Load, string>(new Load { Slot = 2 }), Is.EqualTo("slot 2"));
                responder.Dispose();
                responder.Dispose();
                Assert.That(bus.HasResponder<Load, string>(), Is.False);
            });

        [UnityTest]
        public IEnumerator DisposalStopsInFlightPublicationAndLateTokenDisposalIsHarmless() =>
            AwaitableTest.Run(async () =>
            {
                var bus = new AsyncEventBus();
                var gate = new UniTaskCompletionSource();
                bool secondCalled = false;
                var token = bus.Subscribe<Saved>(async (v, t) => await gate.Task);
                bus.Subscribe<Saved>((v, t) =>
                {
                    secondCalled = true;
                    return UniTask.CompletedTask;
                });
                var publication = bus.PublishAsync(new Saved());
                bus.Dispose();
                gate.TrySetResult();
                bool cancelled = false;
                try
                {
                    await publication;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                Assert.That(cancelled, Is.True);
                Assert.That(secondCalled, Is.False);
                token.Dispose();
                Assert.Throws<ObjectDisposedException>(() => bus.Subscribe<Saved>((v, t) => UniTask.CompletedTask));
            });

#pragma warning disable CS1998 // Completes synchronously by design: the allocation baseline for one async method call.
        private static async UniTask CompletedAsync() { }
#pragma warning restore CS1998

        [Test]
        public void WarmedSynchronousPublicationAddsNothingBeyondItsAsyncCall()
        {
            // In release builds an async UniTask method that completes synchronously allocates nothing. The Editor
            // compiles in Debug, where every async method allocates its state machine once per call. Publication
            // itself (snapshots, delivery, bookkeeping) must add nothing beyond that one call.
            using var bus = new AsyncEventBus();
            int sum = 0;
            bus.Subscribe<Saved>((value, token) =>
            {
                sum += value.Slot;
                return UniTask.CompletedTask;
            });
            for (int i = 0; i < 100; i++)
            {
                bus.PublishAsync(new Saved { Slot = 1 }).GetAwaiter().GetResult();
                CompletedAsync().GetAwaiter().GetResult();
            }
            long baseline;
            using (var measurement = AllocationMeasurement.Start())
            {
                for (int i = 0; i < 10000; i++)
                    CompletedAsync().GetAwaiter().GetResult();
                baseline = measurement.Finish();
            }
            long published;
            using (var measurement = AllocationMeasurement.Start())
            {
                for (int i = 0; i < 10000; i++)
                    bus.PublishAsync(new Saved { Slot = 1 }).GetAwaiter().GetResult();
                published = measurement.Finish();
            }
            Assert.That(published, Is.LessThanOrEqualTo(baseline));
            Assert.That(sum, Is.EqualTo(10100));
        }

        [Test]
        public void UseFromAnotherThreadIsRejected()
        {
            using var bus = new AsyncEventBus();
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    bus.Subscribe<Saved>((v, t) => UniTask.CompletedTask);
                }
                catch (Exception failure)
                {
                    error = failure;
                }
            });
            thread.Start();
            thread.Join();
            Assert.That(error, Is.TypeOf<InvalidOperationException>());
        }
    }
}
