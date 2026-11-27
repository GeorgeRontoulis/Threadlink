namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;

    /// <summary>A scene change in progress or just committed.</summary>
    public readonly struct SceneTransition
    {
        /// <summary>The scene being left, or default before the first transition.</summary>
        public readonly Scene From;

        /// <summary>The scene being entered. During preparation it is loaded but not yet shown.</summary>
        public readonly Scene To;

        /// <summary>Identity of the entered scene: the Addressables content id, or derived from the Build Profile scene name.</summary>
        public readonly Guid Id;

        internal SceneTransition(Scene from, Scene to, Guid id)
        {
            From = from;
            To = to;
            Id = id;
        }
    }

    /// <summary>Published on Iris after Nexus presents a scene.</summary>
    public readonly struct SceneChanged
    {
        /// <summary>The committed transition.</summary>
        public readonly SceneTransition Transition;

        internal SceneChanged(SceneTransition transition) => Transition = transition;
    }

    /// <summary>Announced on Iris when a transition starts, before the next scene loads: show a loading screen, lock input.</summary>
    public readonly struct SceneTransitionStarted
    {
        /// <summary>The scene on screen, which stays until the next one is presented.</summary>
        public readonly Scene From;

        /// <summary>Identity of the scene being loaded, as in <see cref="SceneTransition.Id"/>.</summary>
        public readonly Guid Target;

        /// <summary>Creates the event.</summary>
        public SceneTransitionStarted(Scene from, Guid target)
        {
            From = from;
            Target = target;
        }
    }

    /// <summary>
    /// Announced on Iris when a transition fails or is cancelled before presentation. The current scene stays on screen;
    /// <see cref="Error"/> is an <see cref="OperationCanceledException"/> for a cancelled transition.
    /// </summary>
    public readonly struct SceneTransitionFailed
    {
        /// <summary>The scene that stays on screen.</summary>
        public readonly Scene From;

        /// <summary>Identity of the scene that did not load.</summary>
        public readonly Guid Target;

        /// <summary>Why the transition failed.</summary>
        public readonly Exception Error;

        /// <summary>Creates the event.</summary>
        public SceneTransitionFailed(Scene from, Guid target, Exception error)
        {
            From = from;
            Target = target;
            Error = error;
        }
    }

    /// <summary>A scene kept loaded without being presented. Release it to allow unloading.</summary>
    public sealed class SceneHold
    {
        private readonly SceneNavigator.SceneClaim claim;

        internal SceneHold(SceneNavigator.SceneClaim claim) => this.claim = claim;

        /// <summary>The loaded scene.</summary>
        public Scene Scene => claim.Scene.Scene;

        /// <summary>Releases the hold; the scene unloads when nothing else holds or presents it.</summary>
        public UniTask ReleaseAsync() => claim.ReleaseAsync();
    }

    /// <summary>
    /// Nexus: scene transitions for the session. The next scene loads and prepares while the current one stays on
    /// screen; if anything fails before presentation, the current scene stays. Works with Build Profile scenes by name
    /// and with Addressables scenes by content id.
    /// </summary>
    public sealed class Nexus : ThreadlinkSubsystem<Nexus>
    {
        private readonly List<(Func<SceneTransition, CancellationToken, UniTask> prepare, Action<SceneTransition> committed)> steps =
            new List<(Func<SceneTransition, CancellationToken, UniTask>, Action<SceneTransition>)>();
        private readonly NexusBackend backend = new NexusBackend();
        private SceneNavigator navigator;
        private int transitions;
        private int failures;

        /// <summary>The scene Nexus presents, or the active scene before the first transition.</summary>
        public static Scene CurrentScene =>
            Instance.navigator.PresentedResidency is { } residency ? residency.Scene : SceneManager.GetActiveScene();

        /// <summary>Whether a transition is running.</summary>
        public static bool IsTransitioning => Instance.transitionDepth > 0;

        private int transitionDepth;

        /// <inheritdoc />
        protected override void OnSubsystemBoot()
        {
            // Content is looked up when a scene loads, so booting needs no other subsystem.
            backend.Content = new ContentSceneBackend(() => ContentLibrary.Instance.Provider);
            navigator = new SceneNavigator(backend);
        }

        // Unloads scenes while the content they came from is still available.
        /// <inheritdoc />
        protected override void OnSubsystemDeinit() => navigator.ShutdownAsync().Forget(Debug.LogException);

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            var scene = navigator.PresentedResidency?.Scene;
            report.Summary(scene.HasValue ? scene.Value.name : "No transition yet", failures > 0);
            if (!report.Detailed)
                return;
            report.Section("Scenes");
            report.Field("Presented", scene.HasValue ? scene.Value.name : "—");
            report.Field("Resident", navigator.ResidentScenes);
            report.Field("Unloading", navigator.PendingCleanup, navigator.PendingCleanup > 0 ? MonitorTone.Caution : MonitorTone.Muted);
            report.Section("Transitions");
            report.Field("Completed", transitions);
            report.Field("Failed", failures, failures > 0 ? MonitorTone.Bad : MonitorTone.Muted);
            report.Field("Transition steps", steps.Count);
        }

        /// <summary>Records Build Profile scenes and their current paths. Generated <c>GameScenes</c> calls this at registration.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void AddScenes(IEnumerable<SceneId> scenes) => BuildSceneBackend.AddKnown(scenes);

        /// <summary>Loads a Build Profile scene, prepares it, then presents it and unloads the previous one.</summary>
        public static UniTask<SceneTransition> LoadAsync(SceneId scene, CancellationToken token = default) =>
            Instance.TransitionAsync(Instance.backend.Build.Register(scene), token);

        /// <summary>Loads a mapped Addressables scene (a ContentScenes ID), prepares it, then presents it and unloads the previous one.</summary>
        public static UniTask<SceneTransition> LoadAsync(ContentSceneId scene, CancellationToken token = default) =>
            scene.IsAssigned
                ? Instance.TransitionAsync(scene.Identity, token)
                : UniTask.FromException<SceneTransition>(new ArgumentException("The scene ID is empty.", nameof(scene)));

        /// <summary>Keeps a Build Profile scene loaded but hidden, for instant transitions later.</summary>
        public static async UniTask<SceneHold> HoldAsync(SceneId scene, CancellationToken token = default) =>
            new SceneHold(await Instance.navigator.HoldAsync(Instance.backend.Build.Register(scene), token));

        /// <summary>Keeps a mapped Addressables scene loaded but hidden, for instant transitions later.</summary>
        public static async UniTask<SceneHold> HoldAsync(ContentSceneId scene, CancellationToken token = default) =>
            new SceneHold(await Instance.navigator.HoldAsync(scene.IsAssigned ? scene.Identity : throw new ArgumentException("The scene ID is empty.", nameof(scene)), token));

        /// <summary>
        /// Joins every transition: <paramref name="prepare"/> runs after the next scene loads and before it is presented
        /// (failing it keeps the current scene); <paramref name="committed"/> runs after presentation. Dispose to leave.
        /// </summary>
        public static IDisposable AddTransitionStep(Func<SceneTransition, CancellationToken, UniTask> prepare, Action<SceneTransition> committed = null)
        {
            var nexus = Instance;
            var step = (prepare, committed);
            nexus.steps.Add(step);
            return new Step(() => nexus.steps.Remove(step));
        }

        private async UniTask<SceneTransition> TransitionAsync(Guid id, CancellationToken token)
        {
            var from = navigator.PresentedResidency?.Scene ?? SceneManager.GetActiveScene();
            SceneTransition prepared = default;
            transitionDepth++;
            Iris.TryPublish(new SceneTransitionStarted(from, id));
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, Lifetime);
                var snapshot = steps.ToArray();
                await navigator.TransitionAsync(
                    id,
                    null,
                    async (residency, stepToken) =>
                    {
                        prepared = new SceneTransition(from, residency.Scene, id);
                        foreach (var step in snapshot)
                            if (step.prepare != null)
                                await step.prepare(prepared, stepToken);
                    },
                    linked.Token
                );
                transitions++;
                foreach (var step in snapshot)
                {
                    try
                    {
                        step.committed?.Invoke(prepared);
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error);
                    }
                }
                Iris.TryPublish(new SceneChanged(prepared));
                return prepared;
            }
            catch (Exception error)
            {
                failures++;
                Iris.TryPublish(new SceneTransitionFailed(from, id, error));
                throw;
            }
            finally
            {
                transitionDepth--;
            }
        }

        private sealed class Step : IDisposable
        {
            private Action remove;

            internal Step(Action remove) => this.remove = remove;

            public void Dispose()
            {
                remove?.Invoke();
                remove = null;
            }
        }
    }
}
