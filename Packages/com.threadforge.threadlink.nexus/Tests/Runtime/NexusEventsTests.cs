namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    public sealed class NexusEventsTests
    {
        [UnityTest]
        public IEnumerator AFailedTransitionIsAnnouncedAndKeepsTheCurrentScene()
        {
            var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Nexus));
            try
            {
                var heard = new List<string>();
                SceneTransitionFailed failure = default;
                Iris.Subscribe<SceneTransitionStarted>(e => heard.Add("started"));
                Iris.Subscribe<SceneChanged>(e => heard.Add("changed"));
                Iris.Subscribe<SceneTransitionFailed>(e =>
                {
                    heard.Add("failed");
                    failure = e;
                });
                var before = Nexus.CurrentScene;
                // An ID that no Content mapping holds: the transition fails before anything loads.
                var transition = Nexus.LoadAsync(new ContentSceneId(int.MaxValue - 7));
                var awaiter = transition.GetAwaiter();
                while (!awaiter.IsCompleted)
                    yield return null;
                Assert.That(transition.Status, Is.EqualTo(UniTaskStatus.Faulted));
                // Observe the failure, so it is not reported later as unobserved.
                Assert.That(() => awaiter.GetResult(), Throws.Exception);
                Assert.That(heard, Is.EqualTo(new[] { "started", "failed" }));
                Assert.That(failure.From, Is.EqualTo(before));
                Assert.That(failure.Error, Is.Not.Null.And.Not.InstanceOf<OperationCanceledException>());
                Assert.That(Nexus.CurrentScene, Is.EqualTo(before));
                Assert.That(Nexus.IsTransitioning, Is.False);
            }
            finally
            {
                runtime.Dispose();
            }
        }
    }
}
