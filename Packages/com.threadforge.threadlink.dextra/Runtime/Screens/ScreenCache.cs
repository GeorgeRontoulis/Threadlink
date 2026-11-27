namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>Explicit optional screen caching/prewarming policy. Retained views remain owned until eviction or disposal.</summary>
    internal sealed class ScreenCache : IScreenFactory, IDisposable
    {
        private sealed class Entry
        {
            internal Guid Id;
            internal IScreenView View;
            internal bool Leased,
                Invalid,
                Preparing;
        }

        private sealed class Lease : IScreenView, IScreenObject
        {
            private ScreenCache owner;
            private readonly Entry entry;

            internal Lease(ScreenCache owner, Entry entry)
            {
                this.owner = owner;
                this.entry = entry;
            }

            public async UniTask PrepareAsync(CancellationToken token)
            {
                entry.Preparing = true;
                try
                {
                    await (
                        entry.View ?? throw new ObjectDisposedException(nameof(ScreenCache))
                    ).PrepareAsync(token);
                }
                catch
                {
                    entry.Invalid = true;
                    throw;
                }
                finally
                {
                    entry.Preparing = false;
                }
            }

            public UnityEngine.GameObject Instance => (entry.View as IScreenObject)?.Instance;

            public void SetVisible(bool visible) => entry.View?.SetVisible(visible);

            public void SetFocused(bool focused) => entry.View?.SetFocused(focused);

            public void Dispose()
            {
                var release = owner;
                owner = null;
                release?.Return(entry);
            }
        }

        private readonly IScreenFactory source;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly int capacity;
        private bool disposed;
        public int RetainedViews => entries.Count;
        public int ActiveLeases
        {
            get
            {
                int count = 0;
                foreach (var entry in entries)
                    if (entry.Leased)
                        count++;
                return count;
            }
        }

        /// <summary>Limits inactive cached views; active views are separately reported and never shared between instances.</summary>
        public ScreenCache(IScreenFactory source, int capacity)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public async UniTask<IScreenView> CreateAsync(Guid screen, CancellationToken token)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ScreenCache));
            token.ThrowIfCancellationRequested();
            foreach (var entry in entries)
                if (!entry.Leased && entry.Id == screen)
                {
                    entry.Leased = true;
                    return new Lease(this, entry);
                }
            var view =
                await source.CreateAsync(screen, token)
                ?? throw new InvalidOperationException("Cached screen factory returned no view.");
            if (disposed || token.IsCancellationRequested)
            {
                view.Dispose();
                token.ThrowIfCancellationRequested();
                throw new ObjectDisposedException(nameof(ScreenCache));
            }
            var created = new Entry
            {
                Id = screen,
                View = view,
                Leased = true,
            };
            entries.Add(created);
            return new Lease(this, created);
        }

        public async UniTask PrewarmAsync(Guid screen, CancellationToken token = default)
        {
            using var view = await CreateAsync(screen, token);
            view.SetVisible(false);
            await view.PrepareAsync(token);
        }

        private void Return(Entry entry)
        {
            if (!entry.Leased)
                return;
            entry.Leased = false;
            if (entry.Preparing)
                entry.Invalid = true;
            if (entry.View == null)
                return;
            try
            {
                entry.View.SetFocused(false);
                entry.View.SetVisible(false);
            }
            catch
            {
                entry.Invalid = true;
                throw;
            }
            finally
            {
                if (disposed || entry.Invalid || RetainedViews - ActiveLeases > capacity)
                {
                    entries.Remove(entry);
                    var release = entry.View;
                    entry.View = null;
                    release.Dispose();
                }
            }
        }

        /// <summary>Releases all retained views. Dispose dependent hosts first; active leases are invalidated.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            var errors = new List<Exception>();
            foreach (var entry in entries)
            {
                var release = entry.View;
                entry.View = null;
                try
                {
                    release?.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            entries.Clear();
            if (errors.Count > 0)
                throw new AggregateException(errors);
        }
    }
}
