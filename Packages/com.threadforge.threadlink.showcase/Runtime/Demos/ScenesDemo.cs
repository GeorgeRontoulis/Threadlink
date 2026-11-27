namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Nexus transitions: preparation before presentation, failure keeps the current scene.</summary>
    public sealed class ScenesDemo : ShowcaseDemo
    {
        // A project uses its generated GameScenes; this ID names a scene no Build Profile contains.
        private static readonly SceneId Missing = new SceneId(new Guid("0d1e2f30-4152-4637-8899-aabbccddeeff"), "Showcase Scene That Does Not Exist");

        private IDisposable step;

        public override string Title => "Scenes";

        public override string Summary =>
            "Nexus.LoadAsync loads the next scene (by Build Profile name or Addressables ID) and runs every transition step before showing it. If loading or a step fails, the current scene stays on screen. Aura uses a step to start scene music on time.";

        public override string[] Packages => new[] { "nexus", "aura" };

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            var status = Value("Current scene: " + Nexus.CurrentScene.name);
            panel.Add(status);
            step = Nexus.AddTransitionStep(
                async (transition, token) =>
                {
                    context.Log("Preparing " + transition.To.name + " while " + transition.From.name + " stays visible…");
                    await UniTask.WaitForSeconds(0.25f, cancellationToken: token);
                },
                transition => context.Log("Presented " + transition.To.name + ".")
            );
            panel.Add(Row(Action(context, "Load a scene that is not in the build", async () =>
            {
                try
                {
                    await Nexus.LoadAsync(Missing);
                }
                catch (Exception error)
                {
                    context.Log("Transition failed, nothing changed: " + error.Message);
                }
                status.text = "Current scene: " + Nexus.CurrentScene.name;
            })));
            panel.Add(Value("Add scenes to your Build Profile and call Nexus.LoadAsync(GameScenes.Name) to see a full transition."));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            var before = Nexus.CurrentScene;
            int prepared = 0;
            using (Nexus.AddTransitionStep((transition, t) =>
            {
                prepared++;
                return UniTask.CompletedTask;
            }))
            {
                bool failed = false;
                try
                {
                    await Nexus.LoadAsync(Missing, token);
                }
                catch (InvalidOperationException)
                {
                    failed = true;
                }
                Check(failed, "a missing scene fails the transition");
                Check(prepared == 0, "steps never run for a scene that did not load");
                Check(Nexus.CurrentScene == before, "the current scene stays after a failed transition");
                Check(!Nexus.IsTransitioning, "no transition is left running");
            }
        }

        public override void Dispose() => step?.Dispose();
    }
}
