namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Awaited events and requests on Unity Awaitable.</summary>
    public sealed class AwaitedEventsDemo : ShowcaseDemo
    {
        /// <summary>A finished level.</summary>
        public readonly struct LevelCompleted
        {
            /// <summary>The level number.</summary>
            public readonly int Level;

            /// <summary>Creates the event.</summary>
            public LevelCompleted(int level) => Level = level;
        }

        /// <summary>Asks which level follows another.</summary>
        public readonly struct NextLevel
        {
            /// <summary>The finished level.</summary>
            public readonly int After;

            /// <summary>Creates the request.</summary>
            public NextLevel(int after) => After = after;
        }

        private readonly List<IDisposable> owned = new List<IDisposable>();

        public override string Title => "Awaited Events";

        public override string Summary =>
            "PublishAsync awaits every listener in subscription order, so a level end can wait for its fade, save and analytics. A request has exactly one responder and returns its answer.";

        public override string[] Packages => new[] { "core" };

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            var status = Value("Ready.");
            panel.Add(status);
            owned.Add(Iris.SubscribeAsync<LevelCompleted>(async (done, token) =>
            {
                status.text = "Fading out…";
                await UniTask.WaitForSeconds(0.5f, cancellationToken: token);
            }));
            owned.Add(Iris.SubscribeAsync<LevelCompleted>(async (done, token) =>
            {
                status.text = "Saving…";
                await UniTask.WaitForSeconds(0.5f, cancellationToken: token);
            }));
            owned.Add(Iris.Respond<NextLevel, int>((request, token) => UniTask.FromResult(request.After + 1)));
            panel.Add(Row(Action(context, "Complete level 3", async () =>
            {
                await Iris.PublishAsync(new LevelCompleted(3));
                int next = await Iris.RequestAsync<NextLevel, int>(new NextLevel(3));
                status.text = "Done. Next level: " + next;
                context.Log("Every listener finished, in order; the responder answered " + next + ".");
            })));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            var order = new List<string>();
            using (Iris.SubscribeAsync<LevelCompleted>(async (done, t) =>
            {
                await UniTask.NextFrame(t);
                order.Add("fade");
            }))
            using (Iris.SubscribeAsync<LevelCompleted>((done, t) =>
            {
                order.Add("save");
                return UniTask.CompletedTask;
            }))
            using (Iris.Respond<NextLevel, int>((request, t) => UniTask.FromResult(request.After + 1)))
            {
                await Iris.PublishAsync(new LevelCompleted(1), token);
                Check(order.Count == 2 && order[0] == "fade" && order[1] == "save", "listeners finish in subscription order");
                Check(await Iris.RequestAsync<NextLevel, int>(new NextLevel(1), token) == 2, "the responder answers");
            }
            bool refused = false;
            try
            {
                await Iris.RequestAsync<NextLevel, int>(new NextLevel(1), token);
            }
            catch (NoResponderException)
            {
                refused = true;
            }
            Check(refused, "a request without a responder fails instead of silently succeeding");
        }

        public override void Dispose()
        {
            foreach (var item in owned)
                item.Dispose();
            owned.Clear();
        }
    }
}
