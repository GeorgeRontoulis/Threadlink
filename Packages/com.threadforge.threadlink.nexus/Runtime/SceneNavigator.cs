namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>One loaded scene with explicit presentation and native release contracts.</summary>
    internal interface ISceneResidency
    {
        /// <summary>The Unity scene.</summary>
        UnityEngine.SceneManagement.Scene Scene { get; }

        /// <summary>Whether the backend still has a usable loaded scene.</summary>
        bool IsLoaded { get; }

        /// <summary>Changes presentation synchronously; does not initialize arbitrary scene components.</summary>
        void SetPresented(bool presented);

        /// <summary>Releases this backend acquisition and awaits teardown.</summary>
        UniTask ReleaseAsync();
    }

    /// <summary>Explicit scene-loading backend; required work is awaited directly.</summary>
    internal interface ISceneBackend
    {
        /// <summary>Loads one acquisition. Failure must clean partial backend work.</summary>
        UniTask<ISceneResidency> LoadAsync(Guid scene, CancellationToken token);
    }

    /// <summary>Committed transition outcome. Cleanup errors never imply that the transition rolled back.</summary>
    internal sealed class TransitionOutcome
    {
        /// <summary>Identity of the committed presentation.</summary>
        public Guid Scene { get; }

        /// <summary>Failure releasing the previous presentation after commitment, if any.</summary>
        public Exception CleanupError { get; }

        internal TransitionOutcome(Guid scene, Exception cleanup)
        {
            Scene = scene;
            CleanupError = cleanup;
        }
    }

    /// <summary>Independent shared scene-residency claims and serialized transactional presentation.</summary>
    /// <remarks>Use on a single thread/synchronization context. Required readiness/preparation is explicit.
    /// Scene claims retain one ordinary backend acquisition; they do not replace Addressables resource accounting.</remarks>
    internal sealed class SceneNavigator
    {
        private sealed class Entry
        {
            internal Guid Id;
            internal int Claims;
            internal UniTask<ISceneResidency> Load;
            internal UniTask? Release;
        }

        /// <summary>Independent residency claim. Repeated release shares cleanup completion.</summary>
        public sealed class SceneClaim
        {
            private SceneNavigator owner;
            private readonly Entry entry;
            private UniTask? released;

            internal SceneClaim(SceneNavigator owner, object entry)
            {
                this.owner = owner;
                this.entry = (Entry)entry;
            }

            internal ISceneResidency LoadedScene => entry.Load.GetAwaiter().GetResult();

            /// <summary>Loaded scene. Access fails after this claim releases.</summary>
            public ISceneResidency Scene =>
                owner != null && !owner.closed
                    ? entry.Load.GetAwaiter().GetResult()
                    : throw new ObjectDisposedException(nameof(SceneClaim));

            /// <summary>Releases this claim once, unloading when no other claims retain the scene.</summary>
            public UniTask ReleaseAsync()
            {
                if (released.HasValue)
                    return released.Value;
                var completion = new UniTaskCompletionSource<bool>();
                released = completion.Task;
                var current = owner;
                owner = null;
                CompleteAsync(current, completion).Forget();
                return completion.Task;
            }

            private async UniTaskVoid CompleteAsync(
                SceneNavigator current,
                UniTaskCompletionSource<bool> completion
            )
            {
                try
                {
                    await current.ReleaseAsync(entry);
                    completion.TrySetResult(true);
                }
                catch (Exception error)
                {
                    completion.TrySetException(error);
                }
            }
        }

        private readonly ISceneBackend backend;
        private readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
        private readonly SemaphoreSlim navigation = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly List<Entry> releasing = new List<Entry>();
        private SceneClaim current;
        private UniTask? shutdown;
        private bool closed;

        /// <summary>Current committed scene identity, or empty before the first presentation.</summary>
        public Guid PresentedScene { get; private set; }

        internal ISceneResidency PresentedResidency => current?.LoadedScene;

        /// <summary>Number of backend scene acquisitions retained by claims.</summary>
        public int ResidentScenes => entries.Count;
        public int PendingCleanup => releasing.Count;

        /// <summary>Creates an isolated navigator using the selected backend.</summary>
        public SceneNavigator(ISceneBackend backend) =>
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));

        /// <summary>Acquires shared residency without presenting the scene. Caller cancellation affects only this claim.</summary>
        public async UniTask<SceneClaim> HoldAsync(Guid id, CancellationToken token = default)
        {
            if (closed)
                throw new ObjectDisposedException(nameof(SceneNavigator));
            token.ThrowIfCancellationRequested();
            if (id == Guid.Empty)
                throw new ArgumentException("Scene identity is empty.");
            if (!entries.TryGetValue(id, out var entry))
            {
                entry = new Entry { Id = id };
                entries.Add(id, entry);
                try
                {
                    // Preserved: every claim on the scene awaits the same load.
                    entry.Load = backend.LoadAsync(id, lifetime.Token).Preserve();
                }
                catch
                {
                    entries.Remove(id);
                    throw;
                }
            }
            entry.Claims++;
            try
            {
                // Cancelling this claim stops waiting without cancelling the shared load.
                var scene = await entry.Load.AttachExternalCancellation(token);
                token.ThrowIfCancellationRequested();
                if (closed || !scene.IsLoaded)
                    throw new InvalidOperationException("Scene residency is unavailable.");
                return new SceneClaim(this, entry);
            }
            catch
            {
                ReleaseAsync(entry).Forget(_ => { });
                throw;
            }
        }

        private UniTask ReleaseAsync(Entry entry)
        {
            if (entry.Claims > 0)
                entry.Claims--;
            if (entry.Claims != 0)
                return UniTask.CompletedTask;
            if (entry.Release.HasValue)
                return entry.Release.Value;
            var completion = new UniTaskCompletionSource<bool>();
            entry.Release = completion.Task;
            releasing.Add(entry);
            entries.Remove(entry.Id);
            CompleteReleaseAsync(entry, completion).Forget();
            return completion.Task;
        }

        private async UniTaskVoid CompleteReleaseAsync(Entry entry, UniTaskCompletionSource<bool> completion)
        {
            try
            {
                await ReleaseEntryAsync(entry);
                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
            finally
            {
                releasing.Remove(entry);
            }
        }

        private static async UniTask ReleaseEntryAsync(Entry entry)
        {
            ISceneResidency scene;
            try
            {
                scene = await entry.Load;
            }
            catch
            {
                return;
            }
            await scene.ReleaseAsync();
        }

        /// <summary>Awaits readiness and preparation before committing presentation. Precommit failure preserves the previous scene.</summary>
        public async UniTask<TransitionOutcome> TransitionAsync(
            Guid id,
            Func<CancellationToken, UniTask> readiness = null,
            Func<ISceneResidency, CancellationToken, UniTask> prepare = null,
            CancellationToken token = default
        )
        {
            if (closed)
                throw new ObjectDisposedException(nameof(SceneNavigator));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                lifetime.Token
            );
            await navigation.WaitAsync(linked.Token);
            SceneClaim next = null;
            Exception failure = null;
            try
            {
                if (closed)
                    throw new ObjectDisposedException(nameof(SceneNavigator));
                if (readiness != null)
                    await readiness(linked.Token);
                next = await HoldAsync(id, linked.Token);
                if (prepare != null)
                    await prepare(next.Scene, linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                var previous = current;
                try
                {
                    next.Scene.SetPresented(true);
                    if (previous != null && previous.Scene != next.Scene && previous.Scene.IsLoaded)
                        previous.Scene.SetPresented(false);
                }
                catch
                {
                    try
                    {
                        if (previous != null && previous.Scene.IsLoaded)
                            previous.Scene.SetPresented(true);
                    }
                    finally
                    {
                        if (next.Scene != previous?.Scene)
                            next.Scene.SetPresented(false);
                    }
                    throw;
                }
                current = next;
                next = null;
                PresentedScene = id;
                Exception cleanupError = null;
                if (previous != null)
                    try
                    {
                        await previous.ReleaseAsync();
                    }
                    catch (Exception error)
                    {
                        cleanupError = error;
                    }
                return new TransitionOutcome(id, cleanupError);
            }
            catch (Exception error)
            {
                failure = error;
                throw;
            }
            finally
            {
                try
                {
                    if (next != null)
                        await next.ReleaseAsync();
                }
                catch (Exception cleanup)
                {
                    if (failure != null)
                        throw new AggregateException(failure, cleanup);
                    throw;
                }
                finally
                {
                    navigation.Release();
                }
            }
        }

        /// <summary>Closes admission, cancels queued work and releases all session-owned residency, including remaining claims.</summary>
        public UniTask ShutdownAsync()
        {
            if (shutdown.HasValue)
                return shutdown.Value;
            var completion = new UniTaskCompletionSource<bool>();
            shutdown = completion.Task;
            closed = true;
            Exception cancellationError = null;
            try
            {
                lifetime.Cancel();
            }
            catch (Exception error)
            {
                cancellationError = error;
            }
            CompleteShutdownAsync(completion, cancellationError).Forget();
            return completion.Task;
        }

        private async UniTaskVoid CompleteShutdownAsync(
            UniTaskCompletionSource<bool> completion,
            Exception cancellationError
        )
        {
            try
            {
                await ShutdownCoreAsync(cancellationError);
                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }

        private async UniTask ShutdownCoreAsync(Exception cancellationError)
        {
            await navigation.WaitAsync();
            var errors = new List<Exception>();
            if (cancellationError != null)
                errors.Add(cancellationError);
            try
            {
                if (current != null)
                    try
                    {
                        var scene = current.LoadedScene;
                        if (scene.IsLoaded)
                            scene.SetPresented(false);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                current = null;
                PresentedScene = Guid.Empty;
                var remaining = new List<Entry>(entries.Values);
                foreach (var entry in remaining)
                {
                    entry.Claims = 1;
                    try
                    {
                        await ReleaseAsync(entry);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
                foreach (var pending in new List<Entry>(releasing))
                    try
                    {
                        await pending.Release.Value;
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
            }
            finally
            {
                navigation.Release();
            }
            if (errors.Count > 0)
                throw new AggregateException(errors);
        }
    }
}
