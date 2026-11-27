namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    internal interface ISubscription
    {
        void Release(long generation);
    }

    /// <summary>Generation-safe registration. Copies and repeated disposal do not remove replacement listeners.</summary>
    public readonly struct SubscriptionToken : IDisposable
    {
        private readonly ISubscription registration;
        private readonly long generation;

        internal SubscriptionToken(ISubscription registration, long generation)
        {
            this.registration = registration;
            this.generation = generation;
        }

        /// <summary>Removes this listener on its bus's owning thread.</summary>
        public void Dispose() => registration?.Release(generation);
    }

    /// <summary>Typed synchronous events on the constructing thread; workers may use bounded TryPost.</summary>
    /// <remarks>Dispatch preserves registration order. Removal is immediate. Additions affect nested and
    /// subsequent publications, but not the currently executing publication. No object registration or host is required.</remarks>
    public sealed class EventBus : IDisposable
    {
        private interface IChannel
        {
            void Clear();
        }

        private interface IPosted
        {
            void Deliver(EventBus bus);
        }

        private sealed class Posted<T> : IPosted
        {
            internal T Value;

            public void Deliver(EventBus bus) => bus.PublishIsolated(Value);
        }

        private sealed class Channel<T> : IChannel, ISubscription
        {
            private sealed class Registration
            {
                internal long Generation;
                internal Action<T> Handler;
            }

            private readonly EventBus bus;
            private readonly List<Registration> items = new List<Registration>();
            private readonly Stack<Registration> available = new Stack<Registration>();
            private long generation;
            private int depth;
            internal int Count;

            internal Channel(EventBus bus) => this.bus = bus;

            internal SubscriptionToken Add(Action<T> handler)
            {
                // Never wrap a generation into the identity of an old token.
                long next = checked(generation + 1);
                var item = available.Count == 0 ? new Registration() : available.Pop();
                item.Generation = generation = next;
                item.Handler = handler;
                items.Add(item);
                Count++;
                return new SubscriptionToken(this, next);
            }

            public void Release(long tokenGeneration)
            {
                bus.CheckThread();
                for (int i = 0; i < items.Count; i++)
                    if (items[i].Generation == tokenGeneration && items[i].Handler != null)
                    {
                        items[i].Handler = null;
                        Count--;
                        break;
                    }
                Compact();
            }

            public void Clear()
            {
                for (int i = 0; i < items.Count; i++)
                    items[i].Handler = null;
                Count = 0;
                Compact();
            }

            private void Compact()
            {
                if (depth != 0)
                    return;
                int remaining = 0;
                for (int i = 0; i < items.Count; i++)
                    if (items[i].Handler != null)
                        items[remaining++] = items[i];
                    else
                        available.Push(items[i]);
                if (remaining < items.Count)
                    items.RemoveRange(remaining, items.Count - remaining);
            }

            internal void Deliver(T value, bool isolated)
            {
                int count = items.Count;
                depth++;
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        var handler = items[i].Handler;
                        if (handler == null)
                            continue;
                        if (!isolated)
                            handler(value);
                        else
                        {
                            try
                            {
                                handler(value);
                            }
                            catch (Exception error)
                            {
                                bus.Report(error);
                            }
                        }
                    }
                }
                finally
                {
                    depth--;
                    Compact();
                }
            }
        }

        private readonly int ownerThread = Environment.CurrentManagedThreadId;
        private readonly Dictionary<Type, IChannel> channels = new Dictionary<Type, IChannel>();
        private readonly object ingressLock = new object();
        private readonly Queue<IPosted> ingress = new Queue<IPosted>();
        private readonly int capacity;
        private bool disposed;
        private bool draining;
        private long rejected;
        private long observerFailures;

        /// <summary>Creates an isolated bus with a bounded worker queue. No frame pipeline is installed.</summary>
        public EventBus(int ingressCapacity = 1024)
        {
            if (ingressCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(ingressCapacity));
            capacity = ingressCapacity;
        }

        /// <summary>Optional failure observer. Observer failures are counted and cannot interrupt isolated publication.</summary>
        /// <remarks>Set on the owning thread. Use one reporting callback to fan out diagnostics if necessary.</remarks>
        public Action<Exception> ObserverFailed { get; set; }

        /// <summary>Number of callbacks or failure reporters that have thrown during isolated delivery.</summary>
        public long ObserverFailures => Interlocked.Read(ref observerFailures);

        /// <summary>Number of worker posts rejected due to capacity or disposal.</summary>
        public long RejectedPosts => Interlocked.Read(ref rejected);

        /// <summary>Current queued request count.</summary>
        public int QueueDepth
        {
            get
            {
                lock (ingressLock)
                    return ingress.Count;
            }
        }

        private void CheckThread()
        {
            if (Environment.CurrentManagedThreadId != ownerThread)
                throw new InvalidOperationException(
                    "Use TryPost from workers; other operations require the bus's owning thread."
                );
        }

        private void CheckOpen()
        {
            CheckThread();
            if (disposed)
                throw new ObjectDisposedException(nameof(EventBus));
        }

        private Channel<T> Get<T>()
        {
            if (!channels.TryGetValue(typeof(T), out var channel))
                channels.Add(typeof(T), channel = new Channel<T>(this));
            return (Channel<T>)channel;
        }

        /// <summary>Registers a callback independently of object lifetime. Dispose the returned token to stop delivery.</summary>
        public SubscriptionToken Subscribe<T>(Action<T> callback)
        {
            CheckOpen();
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            return Get<T>().Add(callback);
        }

        /// <summary>Publishes synchronously; callback failures propagate after restoring dispatcher invariants.</summary>
        public void Publish<T>(T value)
        {
            CheckOpen();
            if (channels.TryGetValue(typeof(T), out var channel))
                ((Channel<T>)channel).Deliver(value, false);
        }

        /// <summary>Publishes observational notifications, continuing after reported callback failures.</summary>
        public void PublishIsolated<T>(T value)
        {
            CheckOpen();
            if (channels.TryGetValue(typeof(T), out var channel))
                ((Channel<T>)channel).Deliver(value, true);
        }

        private void Report(Exception error)
        {
            Interlocked.Increment(ref observerFailures);
            try
            {
                ObserverFailed?.Invoke(error);
            }
            catch
            {
                Interlocked.Increment(ref observerFailures);
            }
        }

        /// <summary>Returns active registrations for this event type without creating a channel.</summary>
        public int ListenerCount<T>()
        {
            CheckOpen();
            return channels.TryGetValue(typeof(T), out var channel)
                ? ((Channel<T>)channel).Count
                : 0;
        }

        /// <summary>Removes current listeners of this type, including those in an active dispatch.</summary>
        public void Clear<T>()
        {
            CheckOpen();
            if (channels.TryGetValue(typeof(T), out var channel))
                channel.Clear();
        }

        /// <summary>Queues one value from any thread. Returns false after closure or when capacity is reached.</summary>
        /// <remarks>Accepted posts allocate a queue record. Mutable reference payloads must not be modified after posting.</remarks>
        public bool TryPost<T>(T value)
        {
            lock (ingressLock)
            {
                if (disposed || ingress.Count >= capacity)
                {
                    Interlocked.Increment(ref rejected);
                    return false;
                }
                ingress.Enqueue(new Posted<T> { Value = value });
                return true;
            }
        }

        /// <summary>Drains at most the initial queue snapshot on the owning thread. Nested drains do nothing.</summary>
        public int DrainPosted(int limit = 1024)
        {
            CheckOpen();
            if (limit < 1)
                throw new ArgumentOutOfRangeException(nameof(limit));
            if (draining)
                return 0;
            int count;
            lock (ingressLock)
                count = Math.Min(limit, ingress.Count);
            draining = true;
            int delivered = 0;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    IPosted item;
                    lock (ingressLock)
                    {
                        if (disposed)
                            break;
                        item = ingress.Dequeue();
                    }
                    item.Deliver(this);
                    delivered++;
                }
                return delivered;
            }
            finally
            {
                draining = false;
            }
        }

        /// <summary>Closes worker ingress, drops queued events, and removes all subscriptions. Tokens remain safely disposable.</summary>
        public void Dispose()
        {
            CheckThread();
            lock (ingressLock)
            {
                if (disposed)
                    return;
                disposed = true;
                ingress.Clear();
            }
            foreach (var channel in channels.Values)
                channel.Clear();
            channels.Clear();
            ObserverFailed = null;
        }
    }
}
