namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;

    /// <summary>
    /// Iris: typed events for the session. Synchronous events, awaited events, single-responder requests and frame
    /// callbacks, with no MonoBehaviour or host required.
    /// </summary>
    /// <remarks>
    /// Use Iris from the main thread; workers queue events with <see cref="TryPost{T}"/>, delivered at the next Update.
    /// Passing an owner ties a subscription to that object: it ends when the object is destroyed.
    /// </remarks>
    public sealed class Iris : ThreadlinkSubsystem<Iris>
    {
        private EventBus bus;
        private AsyncEventBus awaited;
        private bool ownsFrames;

        internal EventBus Bus => bus;

        internal AsyncEventBus AwaitedBus => awaited;

        /// <inheritdoc />
        protected override void OnSubsystemBoot()
        {
            bus = new EventBus();
            awaited = new AsyncEventBus();
            bus.ObserverFailed = Debug.LogException;
            // One frame pump serves whichever session is current; a test session inside Play mode reuses it.
            ownsFrames = Application.isPlaying && !IrisFrames.Installed;
            if (ownsFrames)
                IrisFrames.Install();
        }

        /// <inheritdoc />
        protected override void OnSubsystemShutdown()
        {
            if (ownsFrames && IrisFrames.Installed)
                IrisFrames.Uninstall();
            awaited?.Dispose();
            bus?.Dispose();
        }

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            long failures = bus.ObserverFailures;
            report.Summary(failures > 0 ? failures + " listener failures" : "Ready", failures > 0);
            if (!report.Detailed)
                return;
            report.Section("Delivery");
            report.Field("Queued posts", bus.QueueDepth);
            report.Field("Rejected posts", bus.RejectedPosts, bus.RejectedPosts > 0 ? MonitorTone.Caution : MonitorTone.Muted);
            report.Field("Listener failures", failures, failures > 0 ? MonitorTone.Bad : MonitorTone.Muted);
            report.Field("Frame callbacks", IrisFrames.Installed ? "Installed" : "Not installed", IrisFrames.Installed ? MonitorTone.Good : MonitorTone.Muted);
        }

        /// <summary>Calls <paramref name="callback"/> for every <typeparamref name="T"/> published, until the token is disposed.</summary>
        public static SubscriptionToken Subscribe<T>(Action<T> callback) => Instance.bus.Subscribe(callback);

        /// <summary>Calls <paramref name="callback"/> for every <typeparamref name="T"/> published, until <paramref name="owner"/> is destroyed or the token is disposed.</summary>
        public static SubscriptionToken Subscribe<T>(MonoBehaviour owner, Action<T> callback) => Own(owner, Instance.bus.Subscribe(callback));

        /// <summary>Delivers <paramref name="value"/> to every listener now, in subscription order. A listener that throws is
        /// logged and the others still run.</summary>
        public static void Publish<T>(T value) => Instance.bus.PublishIsolated(value);

        /// <summary>
        /// Publishes when Iris is available, as <see cref="Publish{T}"/> does; false when it is not, such as in a test session
        /// without Iris. Framework events use it.
        /// </summary>
        public static bool TryPublish<T>(T value)
        {
            if (!TryGetSingleton(out var iris))
                return false;
            iris.bus.PublishIsolated(value);
            return true;
        }

        /// <summary>Queues an event from any thread for delivery at the next Update. False when the queue is full or Iris is not linked.</summary>
        public static bool TryPost<T>(T value) => TryGetSingleton(out var iris) && iris.bus.TryPost(value);

        /// <summary>Number of synchronous listeners for <typeparamref name="T"/>.</summary>
        public static int ListenerCount<T>() => Instance.bus.ListenerCount<T>();

        /// <summary>Registers an awaited listener for <typeparamref name="T"/>.</summary>
        public static AsyncSubscription SubscribeAsync<T>(Func<T, CancellationToken, UniTask> listener) => Instance.awaited.Subscribe(listener);

        /// <summary>Registers an awaited listener for <typeparamref name="T"/> until <paramref name="owner"/> is destroyed.</summary>
        public static AsyncSubscription SubscribeAsync<T>(MonoBehaviour owner, Func<T, CancellationToken, UniTask> listener)
        {
            var subscription = Instance.awaited.Subscribe(listener);
            Own(owner, subscription);
            return subscription;
        }

        /// <summary>Awaits every awaited listener of <typeparamref name="T"/>, one after another in subscription order.</summary>
        public static UniTask PublishAsync<T>(T value, CancellationToken token = default) => Instance.awaited.PublishAsync(value, token);

        /// <summary>Starts every awaited listener of <typeparamref name="T"/> at once and awaits them all.</summary>
        public static UniTask PublishConcurrentAsync<T>(T value, CancellationToken token = default) => Instance.awaited.PublishConcurrentAsync(value, token);

        /// <summary>Answers requests of <typeparamref name="TRequest"/>. Only one responder may exist per request and response pair.</summary>
        public static IDisposable Respond<TRequest, TResponse>(Func<TRequest, CancellationToken, UniTask<TResponse>> responder) =>
            Instance.awaited.Respond(responder);

        /// <summary>Answers requests of <typeparamref name="TRequest"/> until <paramref name="owner"/> is destroyed.</summary>
        public static IDisposable Respond<TRequest, TResponse>(MonoBehaviour owner, Func<TRequest, CancellationToken, UniTask<TResponse>> responder) =>
            Own(owner, Instance.awaited.Respond(responder));

        /// <summary>Awaits the responder of <typeparamref name="TRequest"/>. Throws <see cref="NoResponderException"/> when there is none.</summary>
        public static UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken token = default) =>
            Instance.awaited.RequestAsync<TRequest, TResponse>(request, token);

        /// <summary>Calls <paramref name="callback"/> with the frame's delta time after every MonoBehaviour Update.</summary>
        public static SubscriptionToken OnUpdate(MonoBehaviour owner, Action<float> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            return Own(owner, Instance.bus.Subscribe<UpdateFrame>(frame => callback(frame.DeltaTime)));
        }

        /// <summary>Calls <paramref name="callback"/> with the fixed step after every MonoBehaviour FixedUpdate.</summary>
        public static SubscriptionToken OnFixedUpdate(MonoBehaviour owner, Action<float> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            return Own(owner, Instance.bus.Subscribe<FixedFrame>(frame => callback(frame.DeltaTime)));
        }

        /// <summary>Calls <paramref name="callback"/> with the frame's delta time after every MonoBehaviour LateUpdate.</summary>
        public static SubscriptionToken OnLateUpdate(MonoBehaviour owner, Action<float> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            return Own(owner, Instance.bus.Subscribe<LateFrame>(frame => callback(frame.DeltaTime)));
        }

        private static TDisposable Own<TDisposable>(MonoBehaviour owner, TDisposable subscription)
            where TDisposable : IDisposable
        {
            if (owner == null)
            {
                subscription.Dispose();
                throw new ArgumentNullException(nameof(owner), "The owner is missing or destroyed.");
            }
            owner.destroyCancellationToken.Register(static state => ((IDisposable)state).Dispose(), subscription);
            return subscription;
        }
    }
}
