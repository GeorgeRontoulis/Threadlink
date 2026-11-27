namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;

    internal interface IAsyncSubscription
    {
        void Release(long generation);
    }

    /// <summary>Generation-safe async listener registration. Copies and repeated disposal never remove a replacement.</summary>
    public readonly struct AsyncSubscription : IDisposable
    {
        private readonly IAsyncSubscription registration;
        private readonly long generation;

        internal AsyncSubscription(IAsyncSubscription registration, long generation)
        {
            this.registration = registration;
            this.generation = generation;
        }

        /// <summary>Removes this listener. Call on the bus's owning thread.</summary>
        public void Dispose() => registration?.Release(generation);
    }

    /// <summary>Raised when a request has no registered responder.</summary>
    public sealed class NoResponderException : InvalidOperationException
    {
        /// <summary>Creates the exception for a request/response pair.</summary>
        public NoResponderException(Type request, Type response)
            : base($"No responder is registered for {request.Name} -> {response.Name}.") { }
    }

    /// <summary>Typed awaited broadcasts and single-responder request endpoints, separate from synchronous Iris buses.</summary>
    /// <remarks>
    /// Create, subscribe, publish and dispose on one owning thread. Sequential publication awaits listeners in
    /// registration order. A publication delivers to the listeners registered when it starts; listeners removed
    /// before their turn are skipped. Listener failures propagate to the publisher and stop the remaining sequence.
    /// Cancellation is observed before each listener and is passed to every listener; it never cancels other
    /// publications. Listeners must complete on the owning thread (Unity's main thread).
    /// No host, bootstrap scene or frame adapter is required.
    /// </remarks>
    internal sealed class AsyncEventBus : IDisposable
    {
        private interface IChannel
        {
            void Clear();
        }

        private sealed class Registration<T>
        {
            internal long Generation;
            internal Func<T, CancellationToken, UniTask> Handler;
        }

        private sealed class Channel<T> : IChannel, IAsyncSubscription
        {
            private readonly AsyncEventBus bus;
            internal readonly List<Registration<T>> Items = new List<Registration<T>>();
            private readonly Stack<Registration<T>> available = new Stack<Registration<T>>();
            private long generation;
            internal int Active;
            internal int Publishing;

            // Snapshot buffers owned by the channel. Unlike ArrayPool<T>.Shared, which trims its cache when the garbage
            // collector runs, retained buffers keep warmed publications allocation-free. Nested publications take
            // further buffers, so the stacks hold at most the deepest nesting seen.
            private readonly Stack<Registration<T>[]> freeSnapshots = new Stack<Registration<T>[]>();
            private readonly Stack<long[]> freeGenerations = new Stack<long[]>();

            internal void Rent(int count, out Registration<T>[] snapshot, out long[] generations)
            {
                snapshot = freeSnapshots.Count != 0 && freeSnapshots.Peek().Length >= count ? freeSnapshots.Pop() : new Registration<T>[Math.Max(4, count * 2)];
                generations = freeGenerations.Count != 0 && freeGenerations.Peek().Length >= count ? freeGenerations.Pop() : new long[snapshot.Length];
            }

            internal void Return(Registration<T>[] snapshot, long[] generations)
            {
                freeSnapshots.Push(snapshot);
                freeGenerations.Push(generations);
            }

            internal Channel(AsyncEventBus bus) => this.bus = bus;

            internal AsyncSubscription Add(Func<T, CancellationToken, UniTask> handler)
            {
                long next = checked(generation + 1);
                // Reuse removed records only while no publication can observe them.
                var item = Publishing == 0 && available.Count != 0 ? available.Pop() : new Registration<T>();
                item.Generation = generation = next;
                item.Handler = handler;
                Items.Add(item);
                Active++;
                return new AsyncSubscription(this, next);
            }

            public void Release(long tokenGeneration)
            {
                if (bus.disposed)
                    return;
                bus.CheckThread();
                for (int i = 0; i < Items.Count; i++)
                    if (Items[i].Generation == tokenGeneration && Items[i].Handler != null)
                    {
                        Items[i].Handler = null;
                        Active--;
                        break;
                    }
                Compact();
            }

            public void Clear()
            {
                foreach (var item in Items)
                    item.Handler = null;
                Active = 0;
                Compact();
            }

            internal void Compact()
            {
                if (Publishing != 0)
                    return;
                int remaining = 0;
                for (int i = 0; i < Items.Count; i++)
                    if (Items[i].Handler != null)
                        Items[remaining++] = Items[i];
                    else
                        available.Push(Items[i]);
                if (remaining < Items.Count)
                    Items.RemoveRange(remaining, Items.Count - remaining);
            }
        }

        private sealed class Responder<TRequest, TResponse> : IDisposable
        {
            private AsyncEventBus bus;
            internal readonly Func<TRequest, CancellationToken, UniTask<TResponse>> Handler;

            internal Responder(AsyncEventBus bus, Func<TRequest, CancellationToken, UniTask<TResponse>> handler)
            {
                this.bus = bus;
                Handler = handler;
            }

            public void Dispose()
            {
                if (bus == null || bus.disposed)
                    return;
                bus.CheckThread();
                var key = (typeof(TRequest), typeof(TResponse));
                if (bus.responders.TryGetValue(key, out var current) && ReferenceEquals(current, this))
                    bus.responders.Remove(key);
                bus = null;
            }
        }

        private readonly Dictionary<Type, IChannel> channels = new Dictionary<Type, IChannel>();
        private readonly Dictionary<(Type, Type), object> responders = new Dictionary<(Type, Type), object>();
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        // Lists of started listeners for concurrent publications, kept so warmed publications allocate nothing.
        private readonly Stack<List<UniTask>> startedPool = new Stack<List<UniTask>>();
        private bool disposed;

        /// <summary>Cancelled when the bus is disposed; in-flight publications stop before their next listener.</summary>
        public CancellationToken Lifetime => lifetime.Token;

        /// <summary>Registers an awaited listener for <typeparamref name="T"/>.</summary>
        public AsyncSubscription Subscribe<T>(Func<T, CancellationToken, UniTask> handler)
        {
            CheckThread();
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            return GetChannel<T>(true).Add(handler);
        }

        /// <summary>Number of current listeners for <typeparamref name="T"/>.</summary>
        public int ListenerCount<T>()
        {
            CheckThread();
            return GetChannel<T>(false)?.Active ?? 0;
        }

        /// <summary>Awaits each listener in registration order.</summary>
        /// <remarks>Warmed publications allocate nothing, including listeners written as <c>async UniTask</c>.</remarks>
        public async UniTask PublishAsync<T>(T value, CancellationToken token = default)
        {
            CheckThread();
            token.ThrowIfCancellationRequested();
            var channel = GetChannel<T>(false);
            if (channel == null || channel.Active == 0)
                return;
            int count = channel.Items.Count;
            channel.Rent(count, out var snapshot, out var generations);
            channel.Items.CopyTo(0, snapshot, 0, count);
            for (int i = 0; i < count; i++)
                generations[i] = snapshot[i].Generation;
            channel.Publishing++;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (disposed)
                        throw new OperationCanceledException(lifetime.Token);
                    // Skip listeners removed (or recycled) after this publication started.
                    var item = snapshot[i];
                    var handler = item.Generation == generations[i] ? item.Handler : null;
                    if (handler == null)
                        continue;
                    await handler(value, token);
                }
            }
            finally
            {
                Finish(channel, snapshot, generations, count);
            }
        }

        private void Finish<T>(Channel<T> channel, Registration<T>[] snapshot, long[] generations, int count)
        {
            channel.Publishing--;
            Array.Clear(snapshot, 0, count);
            channel.Return(snapshot, generations);
            if (!disposed)
                channel.Compact();
        }

        /// <summary>Starts every current listener and awaits all of them. One failure is rethrown; several are aggregated.</summary>
        public async UniTask PublishConcurrentAsync<T>(T value, CancellationToken token = default)
        {
            CheckThread();
            token.ThrowIfCancellationRequested();
            var channel = GetChannel<T>(false);
            if (channel == null || channel.Active == 0)
                return;
            var started = startedPool.Count != 0 ? startedPool.Pop() : new List<UniTask>(channel.Active);
            List<Exception> errors = null;
            channel.Publishing++;
            try
            {
                foreach (var item in channel.Items)
                    if (item.Handler != null)
                    {
                        try
                        {
                            started.Add(item.Handler(value, token));
                        }
                        catch (Exception error)
                        {
                            (errors ??= new List<Exception>()).Add(error);
                        }
                    }
            }
            finally
            {
                channel.Publishing--;
                channel.Compact();
            }
            try
            {
                foreach (var pending in started)
                {
                    try
                    {
                        await pending;
                    }
                    catch (Exception error)
                    {
                        (errors ??= new List<Exception>()).Add(error);
                    }
                }
            }
            finally
            {
                started.Clear();
                startedPool.Push(started);
            }
            if (errors != null)
                throw errors.Count == 1 ? errors[0] : new AggregateException(errors);
        }

        /// <summary>Registers the single responder for a request type pair.</summary>
        /// <exception cref="InvalidOperationException">A responder is already registered.</exception>
        public IDisposable Respond<TRequest, TResponse>(Func<TRequest, CancellationToken, UniTask<TResponse>> responder)
        {
            CheckThread();
            if (responder == null)
                throw new ArgumentNullException(nameof(responder));
            var key = (typeof(TRequest), typeof(TResponse));
            if (responders.ContainsKey(key))
                throw new InvalidOperationException(
                    $"A responder is already registered for {typeof(TRequest).Name} -> {typeof(TResponse).Name}."
                );
            var registration = new Responder<TRequest, TResponse>(this, responder);
            responders.Add(key, registration);
            return registration;
        }

        /// <summary>Whether a responder is currently registered.</summary>
        public bool HasResponder<TRequest, TResponse>()
        {
            CheckThread();
            return responders.ContainsKey((typeof(TRequest), typeof(TResponse)));
        }

        /// <summary>Awaits the registered responder.</summary>
        /// <exception cref="NoResponderException">No responder is registered. Required work must not silently succeed.</exception>
        public async UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken token = default)
        {
            CheckThread();
            token.ThrowIfCancellationRequested();
            if (!responders.TryGetValue((typeof(TRequest), typeof(TResponse)), out var value))
                throw new NoResponderException(typeof(TRequest), typeof(TResponse));
            return await ((Responder<TRequest, TResponse>)value).Handler(request, token);
        }

        /// <summary>Removes all listeners and responders and stops in-flight sequential publications before their next listener.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            CheckThread();
            disposed = true;
            lifetime.Cancel();
            foreach (var channel in channels.Values)
                channel.Clear();
            channels.Clear();
            responders.Clear();
        }

        private Channel<T> GetChannel<T>(bool create)
        {
            if (channels.TryGetValue(typeof(T), out var existing))
                return (Channel<T>)existing;
            if (!create)
                return null;
            var channel = new Channel<T>(this);
            channels.Add(typeof(T), channel);
            return channel;
        }

        private void CheckThread()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AsyncEventBus));
            if (Thread.CurrentThread.ManagedThreadId != owner)
                throw new InvalidOperationException("AsyncEventBus must be used on its owning thread.");
        }
    }
}
