namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Synchronous Iris events, notifications, worker posts and frame callbacks.</summary>
    public sealed class EventsDemo : ShowcaseDemo
    {
        /// <summary>A coin pickup.</summary>
        public readonly struct CoinCollected
        {
            /// <summary>Coins in the pickup.</summary>
            public readonly int Value;

            /// <summary>Creates the event.</summary>
            public CoinCollected(int value) => Value = value;
        }

        private SubscriptionToken coins;
        private int total;

        public override string Title => "Events";

        public override string Summary =>
            "Iris delivers typed events to every listener immediately. Subscribe with an owner and the subscription ends when the owner is destroyed; workers queue events with TryPost for the next frame.";

        public override string[] Packages => new[] { "core" };

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            var label = Value("Coins: 0");
            coins = Iris.Subscribe<CoinCollected>(coin =>
            {
                total += coin.Value;
                label.text = "Coins: " + total;
            });
            panel.Add(label);
            panel.Add(Row(
                Action(context, "Publish a coin", () => Iris.Publish(new CoinCollected(1))),
                Action(context, "Post from a worker", () =>
                {
                    var worker = new Thread(() => Iris.TryPost(new CoinCollected(5)));
                    worker.Start();
                    worker.Join();
                    context.Log("Posted from a worker thread; it arrives at the next Update.");
                }),
                Action(context, "Count listeners", () => context.Log("Listeners for CoinCollected: " + Iris.ListenerCount<CoinCollected>()))));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            int received = 0;
            using (Iris.Subscribe<CoinCollected>(coin => received += coin.Value))
            {
                Iris.Publish(new CoinCollected(2));
                Check(received == 2, "a published event reaches its listener");
                Check(Iris.TryPost(new CoinCollected(3)), "a post is accepted");
                await UniTask.NextFrame(token);
                await UniTask.NextFrame(token);
                Check(received == 5, "a posted event arrives at the next frame");
            }
            Iris.Publish(new CoinCollected(10));
            Check(received == 5, "a disposed subscription receives nothing");
            int frames = 0;
            var updates = Iris.Subscribe<UpdateFrame>(_ => frames++);
            await UniTask.NextFrame(token);
            await UniTask.NextFrame(token);
            updates.Dispose();
            Check(frames > 0, "frame callbacks arrive every Update");
        }

        public override void Dispose() => coins.Dispose();
    }
}
