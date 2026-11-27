namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    public class SceneNavigatorTests
    {
        private sealed class Scene : ISceneResidency
        {
            UnityEngine.SceneManagement.Scene ISceneResidency.Scene => default;

            public bool IsLoaded { get; set; } = true;
            public bool Presented;
            public int Released;
            public bool FailPresent,
                FailCleanup;

            public void SetPresented(bool presented)
            {
                if (presented && FailPresent)
                    throw new Exception("present");
                Presented = presented;
            }

            public UniTask ReleaseAsync()
            {
                Released++;
                IsLoaded = false;
                return FailCleanup
                    ? UniTask.FromException(new Exception("cleanup"))
                    : UniTask.CompletedTask;
            }
        }

        private sealed class Backend : ISceneBackend
        {
            internal readonly Dictionary<Guid, Scene> Scenes = new Dictionary<Guid, Scene>();
            internal UniTask<ISceneResidency>? Pending;
            internal int Loads;

            public UniTask<ISceneResidency> LoadAsync(Guid id, CancellationToken token)
            {
                Loads++;
                return Pending ?? UniTask.FromResult<ISceneResidency>(Scenes[id]);
            }
        }

        [Test]
        public void PreparationFailurePreservesPreviousPresentationAndReleasesNext()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var backend = new Backend();
            backend.Scenes[a] = new Scene();
            backend.Scenes[b] = new Scene();
            var navigator = new SceneNavigator(backend);
            navigator.TransitionAsync(a).GetAwaiter().GetResult();
            var failed = navigator.TransitionAsync(
                b,
                prepare: (scene, token) => throw new Exception("prepare")
            );
            Assert.That(TaskAssert.Faulted(failed), Is.True);
            Assert.That(navigator.PresentedScene, Is.EqualTo(a));
            Assert.That(backend.Scenes[a].Presented, Is.True);
            Assert.That(backend.Scenes[b].Released, Is.EqualTo(1));
            navigator.ShutdownAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void SharedResidencyIsIndependentFromPresentation()
        {
            var id = Guid.NewGuid();
            var backend = new Backend();
            backend.Scenes[id] = new Scene();
            var navigator = new SceneNavigator(backend);
            var first = navigator.HoldAsync(id).GetAwaiter().GetResult();
            var second = navigator.HoldAsync(id).GetAwaiter().GetResult();
            Assert.That(backend.Loads, Is.EqualTo(1));
            Assert.That(backend.Scenes[id].Presented, Is.False);
            first.ReleaseAsync().GetAwaiter().GetResult();
            Assert.That(backend.Scenes[id].Released, Is.Zero);
            second.ReleaseAsync().GetAwaiter().GetResult();
            second.ReleaseAsync().GetAwaiter().GetResult();
            Assert.That(backend.Scenes[id].Released, Is.EqualTo(1));
            navigator.ShutdownAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void PresentationFailureRollsBackAndCleanupAfterCommitIsExplicit()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var backend = new Backend();
            backend.Scenes[a] = new Scene();
            backend.Scenes[b] = new Scene { FailPresent = true };
            var navigator = new SceneNavigator(backend);
            navigator.TransitionAsync(a).GetAwaiter().GetResult();
            Assert.That(TaskAssert.Faulted(navigator.TransitionAsync(b)), Is.True);
            Assert.That(backend.Scenes[a].Presented, Is.True);
            backend.Scenes[b] = new Scene();
            backend.Scenes[a].FailCleanup = true;
            var committed = navigator.TransitionAsync(b).GetAwaiter().GetResult();
            Assert.That(committed.Scene, Is.EqualTo(b));
            Assert.That(committed.CleanupError, Is.Not.Null);
            navigator.ShutdownAsync().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator CancelledClaimDoesNotCancelAnotherWaiterOrLeakLateScene()
        {
            var gate = new UniTaskCompletionSource<ISceneResidency>();
            var backend = new Backend { Pending = gate.Task };
            var navigator = new SceneNavigator(backend);
            using var cancel = new CancellationTokenSource();
            var id = Guid.NewGuid();
            var abandoned = navigator.HoldAsync(id, cancel.Token);
            var surviving = navigator.HoldAsync(id);
            cancel.Cancel();
            for (int i = 0; i < 120 && !abandoned.Status.IsCompleted(); i++)
                yield return null;
            Assert.That(abandoned.Status.IsCanceled(), Is.True);
            var scene = new Scene();
            gate.TrySetResult(scene);
            for (int i = 0; i < 120 && !surviving.Status.IsCompleted(); i++)
                yield return null;
            var claim = surviving.GetAwaiter().GetResult();
            Assert.That(scene.Released, Is.Zero);
            var cleanup = claim.ReleaseAsync();
            for (int i = 0; i < 120 && !cleanup.Status.IsCompleted(); i++)
                yield return null;
            cleanup.GetAwaiter().GetResult();
            Assert.That(scene.Released, Is.EqualTo(1));
            navigator.ShutdownAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void ExternalUnloadDoesNotPreventRecoveryTransition()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var backend = new Backend();
            backend.Scenes[a] = new Scene();
            backend.Scenes[b] = new Scene();
            var navigator = new SceneNavigator(backend);
            navigator.TransitionAsync(a).GetAwaiter().GetResult();
            backend.Scenes[a].IsLoaded = false;
            navigator.TransitionAsync(b).GetAwaiter().GetResult();
            Assert.That(navigator.PresentedScene, Is.EqualTo(b));
            navigator.ShutdownAsync().GetAwaiter().GetResult();
        }
    }
}
