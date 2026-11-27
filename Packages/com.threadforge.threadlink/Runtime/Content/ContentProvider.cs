namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;

    /// <summary>Catalog resolution and per-acquisition ownership, independent of services.
    /// Use from one thread and synchronization context; backend completion resumes on that context.</summary>
    internal sealed class ContentProvider
    {
        private sealed class Record
        {
            internal long Id;
            internal Guid Content;
            internal AcquisitionKind Kind;
            internal RequestState State;
            internal object Handle;
            internal Exception Error,
                CleanupError;
            internal bool ReleaseAttempted;
        }

        private readonly ContentCatalog catalog;
        private readonly IContentBackend backend;
        private readonly Dictionary<long, Record> active = new Dictionary<long, Record>();
        private readonly Queue<RequestStatus> history = new Queue<RequestStatus>();
        private readonly int historyCapacity;
        private long nextId;

        /// <summary>Creates a provider with bounded completed-request history and no load cache.</summary>
        public ContentProvider(
            ContentCatalog catalog,
            IContentBackend backend,
            int historyCapacity = 128
        )
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (historyCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(historyCapacity));
            this.historyCapacity = historyCapacity;
        }

        /// <summary>Number of admitted requests whose release has not been attempted.</summary>
        public int OutstandingRequests => active.Count;

        /// <summary>Loads one typed asset. Cancelled or failed acquisitions release their own backend operation.</summary>
        public UniTask<ResourceRequest<T>> LoadAsync<T>(
            ResourceId<T> id,
            CancellationToken token = default
        ) => AcquireAsync(id, AcquisitionKind.Asset, token);

        /// <summary>
        /// Acquires an asset, instance or scene under the backend's corresponding release contract. A component type
        /// acquires the prefab (or its instance) and returns that component of its root.
        /// </summary>
        // Async so that cancellation and catalog errors surface as a failed task, never as a synchronous throw.
        public async UniTask<ResourceRequest<T>> AcquireAsync<T>(
            ResourceId<T> id,
            AcquisitionKind kind,
            CancellationToken token = default
        )
        {
            token.ThrowIfCancellationRequested();
            var entry = catalog.Resolve(id);
            if (IsComponent(typeof(T)))
                return await AcquireComponentAsync<T>(id.Value, kind, () => backend.Acquire<GameObject>(entry.Location, kind), token);
            return await AcquireCore(id.Value, kind, () => backend.Acquire<T>(entry.Location, kind), token);
        }

        /// <summary>
        /// Loads every asset with an Addressables label as one request; releasing it releases them all. A component type
        /// loads the labelled prefabs and returns that component of each root.
        /// </summary>
        public async UniTask<ResourceRequest<IList<T>>> LoadLabelAsync<T>(ContentLabel label, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(label.Name))
                throw new ArgumentException("The label is empty.", nameof(label));
            if (!IsComponent(typeof(T)))
                return await AcquireCore(label.Identity, AcquisitionKind.Asset, () => backend.AcquireLabel<T>(label.Name), token);
            return await LoadLabelledComponentsAsync<T>(label, token);
        }

        private async UniTask<ResourceRequest<IList<T>>> LoadLabelledComponentsAsync<T>(ContentLabel label, CancellationToken token)
        {
            var prefabs = await AcquireCore(label.Identity, AcquisitionKind.Asset, () => backend.AcquireLabel<GameObject>(label.Name), token);
            var components = new List<T>(prefabs.Asset.Count);
            foreach (var prefab in prefabs.Asset)
            {
                var component = prefab != null ? prefab.GetComponent(typeof(T)) : null;
                if (component == null)
                {
                    prefabs.Release();
                    throw new InvalidOperationException($"{(prefab != null ? prefab.name : "A labelled asset")} has no {typeof(T).Name} component.");
                }
                components.Add((T)(object)component);
            }
            return new ResourceRequest<IList<T>>(components, prefabs.ReleaseAsync);
        }

        // A prefab acquired for one of its root components: the request returns the component and releases the prefab.
        private async UniTask<ResourceRequest<T>> AcquireComponentAsync<T>(Guid identity, AcquisitionKind kind, Func<IResourceOperation<GameObject>> start, CancellationToken token)
        {
            var prefab = await AcquireCore(identity, kind, start, token);
            var component = prefab.Asset.GetComponent(typeof(T));
            if (component == null)
            {
                string name = prefab.Asset.name;
                prefab.Release();
                throw new InvalidOperationException($"{name} has no {typeof(T).Name} component on its root.");
            }
            return new ResourceRequest<T>((T)(object)component, prefab.ReleaseAsync);
        }

        private static bool IsComponent(Type type) => typeof(Component).IsAssignableFrom(type);

        private async UniTask<ResourceRequest<T>> AcquireCore<T>(
            Guid identity,
            AcquisitionKind kind,
            Func<IResourceOperation<T>> start,
            CancellationToken token
        )
        {
            token.ThrowIfCancellationRequested();
            var record = new Record
            {
                Id = checked(++nextId),
                Content = identity,
                Kind = kind,
                State = RequestState.Loading,
            };
            active.Add(record.Id, record);
            IResourceOperation<T> operation = null;
            try
            {
                operation =
                    start()
                    ?? throw new InvalidOperationException("Backend returned no acquisition.");
                record.Handle = operation.Handle;
                // Preserved so the late outcome can still be observed after a cancelled wait.
                var completion = operation.Completion.Preserve();
                Observe(completion);
                T result = token.CanBeCanceled ? await completion.AttachExternalCancellation(token) : await completion;
                token.ThrowIfCancellationRequested();
                if (ReferenceEquals(result, null))
                    throw new InvalidOperationException("Backend returned no content.");
                record.State = RequestState.Ready;
                var acquired = operation;
                return new ResourceRequest<T>(
                    result,
                    () => Release(record, acquired, RequestState.Released)
                );
            }
            catch (Exception error)
            {
                record.Error = error;
                var outcome =
                    error is OperationCanceledException && token.IsCancellationRequested
                        ? RequestState.Cancelled
                        : RequestState.Failed;
                if (outcome == RequestState.Failed)
                    Iris.TryNotify(new ContentLoadFailed(identity, kind, error));
                if (operation == null)
                    Finish(record, outcome);
                else
                {
                    try
                    {
                        _ = Release(record, operation, outcome);
                    }
                    catch (Exception cleanup)
                    {
                        throw new AggregateException(
                            "Acquisition and cleanup failed.",
                            error,
                            cleanup
                        );
                    }
                }
                throw;
            }
        }

        /// <summary>Loads a batch; failure releases all already completed members. Retaining the returned requests preloads content.</summary>
        public async UniTask<ResourceRequest<T>[]> LoadBatchAsync<T>(
            IEnumerable<ResourceId<T>> ids,
            CancellationToken token = default
        )
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));
            var requests = new List<ResourceRequest<T>>();
            try
            {
                foreach (var id in ids)
                    requests.Add(await LoadAsync(id, token));
                return requests.ToArray();
            }
            catch (Exception error)
            {
                var failures = new List<Exception> { error };
                for (int i = requests.Count - 1; i >= 0; i--)
                    try
                    {
                        requests[i].Release();
                    }
                    catch (Exception cleanup)
                    {
                        failures.Add(cleanup);
                    }
                if (failures.Count > 1)
                    throw new AggregateException(failures);
                throw;
            }
        }

        private UniTask Release<T>(
            Record record,
            IResourceOperation<T> operation,
            RequestState outcome
        )
        {
            if (record.ReleaseAttempted)
                return UniTask.CompletedTask;
            record.ReleaseAttempted = true;
            try
            {
                operation.Release();
                if (operation is IAsyncResourceCleanup asynchronous)
                {
                    record.State = RequestState.Releasing;
                    var pending = CompleteReleaseAsync(record, asynchronous.Cleanup, outcome).Preserve();
                    Observe(pending);
                    return pending;
                }
                Finish(record, outcome);
                return UniTask.CompletedTask;
            }
            catch (Exception error)
            {
                record.CleanupError = error;
                Finish(record, RequestState.CleanupFailed);
                throw;
            }
        }

        // Failures are recorded on the request; observing them here keeps a discarded outcome from being reported twice.
        private static void Observe(UniTask pending) => Swallow(pending).Forget();

        private static void Observe<T>(UniTask<T> pending) => Swallow(pending.AsUniTask()).Forget();

        private static async UniTaskVoid Swallow(UniTask pending)
        {
            try
            {
                await pending;
            }
            catch (Exception)
            {
                // Recorded on the request.
            }
        }

        private async UniTask CompleteReleaseAsync(Record record, UniTask cleanup, RequestState outcome)
        {
            try
            {
                await cleanup;
                Finish(record, outcome);
            }
            catch (Exception error)
            {
                record.CleanupError = error;
                Finish(record, RequestState.CleanupFailed);
                throw;
            }
        }

        private static RequestStatus Snapshot(Record record) =>
            new RequestStatus(
                record.Id,
                record.Content,
                record.Kind,
                record.State,
                record.Handle,
                record.Error,
                record.CleanupError
            );

        private void Finish(Record record, RequestState state)
        {
            record.State = state;
            active.Remove(record.Id);
            if (historyCapacity == 0)
                return;
            if (history.Count == historyCapacity)
                history.Dequeue();
            history.Enqueue(Snapshot(record));
        }

        /// <summary>Copies outstanding request statuses. These counts are not Addressables reference counts.</summary>
        public RequestStatus[] GetOutstanding()
        {
            var result = new RequestStatus[active.Count];
            int i = 0;
            foreach (var record in active.Values)
                result[i++] = Snapshot(record);
            return result;
        }

        /// <summary>Returns bounded completed-request history, oldest first.</summary>
        public RequestStatus[] GetHistory() => history.ToArray();
    }
}
