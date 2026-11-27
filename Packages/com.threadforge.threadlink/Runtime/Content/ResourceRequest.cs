namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;

    /// <summary>The release contract requested from a backend.</summary>
    public enum AcquisitionKind
    {
        Asset,
        Instance,
        Scene,
    }

    /// <summary>Observable outcome of one admitted acquisition.</summary>
    public enum RequestState
    {
        Loading,
        Ready,
        Releasing,
        Released,
        Cancelled,
        Failed,
        CleanupFailed,
    }

    /// <summary>Optional backend cleanup completion, for operations such as asynchronous scene unloading.</summary>
    public interface IAsyncResourceCleanup
    {
        /// <summary>Completion of the cleanup requested by Release, including late native completion.</summary>
        UniTask Cleanup { get; }
    }

    /// <summary>One backend acquisition, with its corresponding release obligation.</summary>
    public interface IResourceOperation<T>
    {
        /// <summary>Completion of this acquisition, without consumer cancellation.</summary>
        UniTask<T> Completion { get; }

        /// <summary>The native backend handle for diagnostics.</summary>
        object Handle { get; }

        /// <summary>Releases this acquisition. Must support release before completion.</summary>
        void Release();
    }

    /// <summary>Backend acquisition contract. Every returned operation transfers one release obligation.</summary>
    public interface IContentBackend
    {
        /// <summary>Acquires an independent backend operation, or throws before transferring any obligation.</summary>
        IResourceOperation<T> Acquire<T>(string location, AcquisitionKind kind);

        /// <summary>Loads every asset with a label as one operation. Backends without labels throw <see cref="NotSupportedException"/>.</summary>
        IResourceOperation<System.Collections.Generic.IList<T>> AcquireLabel<T>(string label) =>
            throw new NotSupportedException(GetType().Name + " does not load by label.");
    }

    /// <summary>Snapshot of one request. Counts describe requests, never backend reference counts or memory.</summary>
    public sealed class RequestStatus
    {
        /// <summary>Unique request identity.</summary>
        public long RequestId { get; }

        /// <summary>Persisted content identity.</summary>
        public Guid ContentId { get; }

        /// <summary>Acquisition release contract.</summary>
        public AcquisitionKind Kind { get; }

        /// <summary>Current request outcome.</summary>
        public RequestState State { get; }

        /// <summary>Backend handle, where acquired.</summary>
        public object BackendHandle { get; }

        /// <summary>Acquisition failure, if any.</summary>
        public Exception Error { get; }

        /// <summary>Release failure, if any.</summary>
        public Exception CleanupError { get; }

        internal RequestStatus(
            long id,
            Guid content,
            AcquisitionKind kind,
            RequestState state,
            object handle,
            Exception error,
            Exception cleanup
        )
        {
            RequestId = id;
            ContentId = content;
            Kind = kind;
            State = state;
            BackendHandle = handle;
            Error = error;
            CleanupError = cleanup;
        }
    }

    /// <summary>One independently owned acquisition. No shared-load cache or extra reference counter is used.</summary>
    public sealed class ResourceRequest<T> : IDisposable
    {
        private Func<UniTask> release;
        private UniTask cleanup = UniTask.CompletedTask;
        private Exception releaseError;
        private T asset;
        private bool released;

        internal ResourceRequest(T asset, Func<UniTask> release)
        {
            this.asset = asset;
            this.release = release;
        }

        /// <summary>The acquired result. Access after release throws.</summary>
        public T Asset =>
            !released ? asset : throw new ObjectDisposedException(nameof(ResourceRequest<T>));

        /// <summary>Whether this request has released its obligation.</summary>
        public bool IsReleased => released;

        /// <summary>Releases once. Repeated calls do nothing, including after a release failure.</summary>
        public void Release()
        {
            if (released)
                return;
            released = true;
            asset = default;
            var releaseAction = release;
            release = null;
            try
            {
                cleanup = releaseAction();
            }
            catch (Exception error)
            {
                // Kept as the error, not a faulted task: Release already throws it, and a task nobody awaits would be
                // reported again as unobserved when it is collected.
                releaseError = error;
                throw;
            }
        }

        /// <summary>Requests release and awaits backend cleanup. Repeated calls share completion.</summary>
        public UniTask ReleaseAsync()
        {
            try
            {
                Release();
            }
            catch (Exception)
            {
                // Reported through the returned task below.
            }
            return releaseError != null ? UniTask.FromException(releaseError) : cleanup;
        }

        /// <summary>Releases this request.</summary>
        public void Dispose() => Release();
    }
}
