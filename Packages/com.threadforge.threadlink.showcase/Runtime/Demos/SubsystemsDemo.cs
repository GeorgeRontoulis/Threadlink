namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>A project subsystem: registered, initialized before Awake, reached from anywhere.</summary>
    public sealed class SubsystemsDemo : ShowcaseDemo
    {
        /// <summary>A score kept for the whole session. In a game it would register automatically; the demo adds it by hand.</summary>
        [ManualRegistration]
        public sealed class ShowcaseScore : ThreadlinkSubsystem<ShowcaseScore>
        {
            /// <summary>The score.</summary>
            public int Points { get; private set; }

            /// <summary>Adds points and announces the new score.</summary>
            public static void Add(int points)
            {
                Instance.Points += points;
                Iris.Publish(Instance.Points);
            }

            protected override void OnInitialize() => Require<Iris>();

            public override void Report(MonitorReport report) => report.Summary(Points + " points");
        }

        public override string Title => "Subsystems";

        public override string Summary =>
            "Derive from ThreadlinkSubsystem<T> and Threadlink creates it before the first scene's Awake, in dependency order, and shuts it down last. Static methods on the subsystem are its API; TryGetSingleton checks that it exists and is linked to the core.";

        public override string[] Packages => new[] { "core" };

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            Threadlink.Instance.Get<ShowcaseScore>();
            var label = Value();
            void Show() => label.text = ShowcaseScore.TryGetSingleton(out var score) ? "Points: " + score.Points + " (linked to the core)" : "Not linked.";
            Show();
            panel.Add(label);
            panel.Add(Row(
                Action(context, "Add 10 points", () =>
                {
                    ShowcaseScore.Add(10);
                    Show();
                }),
                Action(context, "List the session's subsystems", () =>
                {
                    foreach (var subsystem in Threadlink.Instance.Subsystems)
                        context.Log(subsystem.Name + ": " + subsystem.State);
                })));
            panel.Add(Value("Open Threadlink ▸ Runtime Monitor to see every subsystem, its dependencies and its report."));
        }

        public override UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            Check(Threadlink.IsDeployed, "Threadlink deployed before the scene");
            Check(Iris.TryGetSingleton(out var iris) && iris.State == SubsystemState.Ready, "Iris is linked to the core");
            var score = Threadlink.Instance.Get<ShowcaseScore>();
            int before = score.Points;
            ShowcaseScore.Add(1);
            Check(ShowcaseScore.Instance.Points == before + 1, "static API reaches the linked instance");
            Check(System.Linq.Enumerable.Contains(score.Dependencies, typeof(Iris)), "the required subsystem is recorded");
            return UniTask.CompletedTask;
        }
    }
}
