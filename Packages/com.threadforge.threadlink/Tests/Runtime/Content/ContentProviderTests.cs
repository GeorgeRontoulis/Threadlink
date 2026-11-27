namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    public class ContentProviderTests
    {
        private sealed class Operation : IResourceOperation<string>, IAsyncResourceCleanup
        {
            internal UniTask<string> Pending = UniTask.FromResult("asset");
            internal int Releases;
            internal bool FailRelease;
            public UniTask<string> Completion => Pending;
            public object Handle => this;
            public UniTask Cleanup { get; set; } = UniTask.CompletedTask;

            public void Release()
            {
                Releases++;
                if (FailRelease)
                    throw new Exception("release failed");
            }
        }

        private sealed class Backend : IContentBackend
        {
            internal readonly List<Operation> Acquired = new List<Operation>();
            internal UniTask<string>? Completion;
            internal bool FailAdmission;

            public IResourceOperation<T> Acquire<T>(string location, AcquisitionKind kind)
            {
                if (FailAdmission)
                    throw new NotSupportedException("unsupported capability");
                var operation = new Operation { Pending = Completion ?? UniTask.FromResult("asset") };
                Acquired.Add(operation);
                return (IResourceOperation<T>)(object)operation;
            }
        }

        private readonly ResourceId<string> id = new ResourceId<string>(
            new Guid("d376890c-41c2-4365-8600-42ac7ab998f6")
        );

        private ContentProvider Provider(Backend backend, int capacity = 128) =>
            new ContentProvider(
                new ContentCatalog(new[] { ContentEntry.Create(id, "location") }),
                backend,
                capacity
            );

        [Test]
        public void ConcurrentConsumersOwnIndependentAcquisitionsAndIdempotentRelease()
        {
            var backend = new Backend();
            var provider = Provider(backend);
            var first = provider.LoadAsync(id).GetAwaiter().GetResult();
            var second = provider.LoadAsync(id).GetAwaiter().GetResult();
            Assert.That(provider.OutstandingRequests, Is.EqualTo(2));
            first.Release();
            first.Release();
            Assert.That(second.Asset, Is.EqualTo("asset"));
            Assert.Throws<ObjectDisposedException>(() =>
            {
                var ignored = first.Asset;
            });
            second.Dispose();
            Assert.That(backend.Acquired.Count, Is.EqualTo(2));
            Assert.That(backend.Acquired[0].Releases, Is.EqualTo(1));
            Assert.That(backend.Acquired[1].Releases, Is.EqualTo(1));
            Assert.That(provider.OutstandingRequests, Is.Zero);
        }

        [Test]
        public void TypeMismatchAndUnknownIdDoNotAcquire()
        {
            var backend = new Backend();
            var provider = Provider(backend);
            Assert.That(TaskAssert.Faulted(provider.LoadAsync(new ResourceId<int>(id.Value))), Is.True);
            Assert.That(
                TaskAssert.Faulted(provider.LoadAsync(new ResourceId<string>(Guid.NewGuid()))),
                Is.True
            );
            Assert.That(backend.Acquired, Is.Empty);
        }

        [Test]
        public void FailedLoadsAreAnnouncedAndCancelledOnesAreNot()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(Iris));
            var failed = new List<ContentLoadFailed>();
            Iris.Subscribe<ContentLoadFailed>(failed.Add);
            var provider = Provider(new Backend { FailAdmission = true });
            Assert.That(TaskAssert.Faulted(provider.LoadAsync(id)), Is.True);
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Assert.That(Provider(new Backend()).LoadAsync(id, cancel.Token).Status.IsCanceled(), Is.True);
            }
            Assert.That(failed, Has.Count.EqualTo(1));
            Assert.That(failed[0].Content, Is.EqualTo(id.Value));
            Assert.That(failed[0].Kind, Is.EqualTo(AcquisitionKind.Asset));
            Assert.That(failed[0].Error, Is.TypeOf<NotSupportedException>());
        }

        [Test]
        public void CancellationBeforeAdmissionDoesNotAcquire()
        {
            var backend = new Backend();
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            Assert.That(Provider(backend).LoadAsync(id, cancel.Token).Status.IsCanceled(), Is.True);
            Assert.That(backend.Acquired, Is.Empty);
        }

        [UnityTest]
        public IEnumerator CancellationDuringCompletionReleasesOnlyCancelledConsumer()
        {
            var gate = new UniTaskCompletionSource<string>();
            var backend = new Backend { Completion = gate.Task };
            var provider = Provider(backend);
            using var cancel = new CancellationTokenSource();
            var cancelled = provider.LoadAsync(id, cancel.Token);
            var surviving = provider.LoadAsync(id);
            cancel.Cancel();
            for (int i = 0; i < 120 && !cancelled.Status.IsCompleted(); i++)
                yield return null;
            Assert.That(cancelled.Status.IsCanceled(), Is.True);
            Assert.That(backend.Acquired[0].Releases, Is.EqualTo(1));
            Assert.That(backend.Acquired[1].Releases, Is.Zero);
            gate.TrySetResult("late asset");
            for (int i = 0; i < 120 && !surviving.Status.IsCompleted(); i++)
                yield return null;
            var request = surviving.GetAwaiter().GetResult();
            Assert.That(request.Asset, Is.EqualTo("late asset"));
            request.Release();
            Assert.That(backend.Acquired[0].Releases, Is.EqualTo(1));
            Assert.That(provider.OutstandingRequests, Is.Zero);
        }

        [Test]
        public void FailedAcquisitionReleasesItsHandleAndKeepsFailureStatus()
        {
            var backend = new Backend
            {
                Completion = UniTask.FromException<string>(new Exception("failed")),
            };
            var provider = Provider(backend);
            Assert.That(TaskAssert.Faulted(provider.LoadAsync(id)), Is.True);
            Assert.That(backend.Acquired[0].Releases, Is.EqualTo(1));
            Assert.That(provider.GetHistory()[0].State, Is.EqualTo(RequestState.Failed));
        }

        [Test]
        public void UnsupportedAdmissionHasNoReleaseObligation()
        {
            var backend = new Backend { FailAdmission = true };
            var provider = Provider(backend);
            Assert.That(TaskAssert.Faulted(provider.LoadAsync(id)), Is.True);
            Assert.That(provider.GetHistory()[0].Error, Is.TypeOf<NotSupportedException>());
            Assert.That(provider.OutstandingRequests, Is.Zero);
        }

        [Test]
        public void CleanupFailureDoesNotRepeatNativeRelease()
        {
            var backend = new Backend();
            var provider = Provider(backend);
            var request = provider.LoadAsync(id).GetAwaiter().GetResult();
            backend.Acquired[0].FailRelease = true;
            Assert.Throws<Exception>(() => request.Release());
            request.Release();
            Assert.That(provider.GetHistory()[0].State, Is.EqualTo(RequestState.CleanupFailed));
            Assert.That(backend.Acquired[0].Releases, Is.EqualTo(1));
        }

        [Test]
        public void BatchRollbackReleasesPriorPreloads()
        {
            var backend = new Backend();
            var provider = Provider(backend);
            Assert.That(
                TaskAssert.Faulted(provider
                    .LoadBatchAsync(new[] { id, new ResourceId<string>(Guid.NewGuid()) })),
                Is.True
            );
            Assert.That(backend.Acquired[0].Releases, Is.EqualTo(1));
            Assert.That(provider.OutstandingRequests, Is.Zero);
        }

        [Test]
        public void HistoryIsBoundedAndRequestsAreNeverReused()
        {
            var backend = new Backend();
            var provider = Provider(backend, 2);
            for (int i = 0; i < 5; i++)
                provider.LoadAsync(id).GetAwaiter().GetResult().Release();
            Assert.That(provider.GetHistory().Length, Is.EqualTo(2));
            Assert.That(provider.GetHistory()[1].RequestId, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator AsynchronousReleaseRemainsObservableUntilNativeCompletion()
        {
            var backend = new Backend();
            var provider = Provider(backend);
            var request = provider.LoadAsync(id).GetAwaiter().GetResult();
            var gate = new UniTaskCompletionSource<bool>();
            backend.Acquired[0].Cleanup = gate.Task;
            var release = request.ReleaseAsync();
            Assert.That(request.ReleaseAsync(), Is.EqualTo(release), "Repeated release shares one completion.");
            Assert.That(provider.GetOutstanding()[0].State, Is.EqualTo(RequestState.Releasing));
            gate.TrySetResult(true);
            for (int i = 0; i < 120 && !release.Status.IsCompleted(); i++)
                yield return null;
            release.GetAwaiter().GetResult();
            Assert.That(provider.OutstandingRequests, Is.Zero);
            Assert.That(provider.GetHistory()[0].State, Is.EqualTo(RequestState.Released));
        }
    }
}
