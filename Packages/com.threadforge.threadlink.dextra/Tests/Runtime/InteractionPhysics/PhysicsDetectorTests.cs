namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class NativeTarget : MonoBehaviour, IInteractable
    {
        public Guid Identity { get; } = Guid.NewGuid();

        public InteractionAvailability Evaluate(object actor) =>
            new InteractionAvailability(true, "Use");

        public UniTask ExecuteAsync(object actor, CancellationToken token) => UniTask.CompletedTask;
    }

    public class PhysicsDetectorTests
    {
        [UnityTest]
        public IEnumerator BothPhysicsAdaptersFindOrdinaryComponentTargets()
        {
            var target = new GameObject("Interaction physics test");
            target.transform.position = new Vector3(100, 100, 0);
            UnityEngine.Object.DontDestroyOnLoad(target);
            try
            {
                var interactable = target.AddComponent<NativeTarget>();
                target.AddComponent<BoxCollider>();
                var sensor = new GameObject("2D sensor");
                sensor.transform.SetParent(target.transform, false);
                var collider2D = sensor.AddComponent<BoxCollider2D>();
                yield return new WaitForFixedUpdate();
                UnityEngine.Physics.SyncTransforms();
                Physics2D.SyncTransforms();
                var candidates = new List<InteractionCandidate>();
                Assert.That(
                    PhysicsTargets.Collect3D(
                        target.scene.GetPhysicsScene(),
                        target.transform.position,
                        3,
                        1,
                        new Collider[8],
                        candidates
                    ),
                    Is.True
                );
                Assert.That(candidates.Exists(item => item.Target == interactable), Is.True);
                Assert.That(
                    PhysicsTargets.Collect2D(
                        sensor.scene.GetPhysicsScene2D(),
                        sensor.transform.position,
                        3,
                        1,
                        new Collider2D[8],
                        candidates
                    ),
                    Is.True
                );
                Assert.That(
                    candidates.Exists(item => item.Target == interactable),
                    Is.True,
                    "2D query scene="
                        + sensor.scene.name
                        + " enabled="
                        + collider2D.enabled
                        + " active="
                        + sensor.activeInHierarchy
                        + " simulation="
                        + Physics2D.simulationMode
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
