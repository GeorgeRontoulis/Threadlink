namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using NUnit.Framework;

    public class InteractionTests
    {
        private sealed class Target : IInteractable
        {
            private static readonly UnityEngine.Localization.LocalizedString Use = new UnityEngine.Localization.LocalizedString("Tests", "use");

            public Guid Identity { get; } = Guid.NewGuid();
            public bool Available = true;
            public int Executions;

            public InteractionAvailability Evaluate(object actor) =>
                new InteractionAvailability(Available, Use);

            public UniTask ExecuteAsync(object actor, CancellationToken token)
            {
                Executions++;
                return UniTask.CompletedTask;
            }
        }

        [Test]
        public void SelectionRevalidatesAvailabilityAndRejectsStalePrompts()
        {
            var target = new Target();
            var other = new Target();
            var interactor = new Interactor();
            interactor.Select(
                new[] { new InteractionCandidate(other, 10), new InteractionCandidate(target, 1) },
                null
            );
            Assert.That(interactor.Selected, Is.SameAs(target));
            Assert.Throws<InvalidOperationException>(() => interactor.ExecuteAsync(other.Identity, null).GetAwaiter().GetResult());
            target.Available = false;
            Assert.Throws<InvalidOperationException>(() => interactor.ExecuteAsync(target.Identity, null).GetAwaiter().GetResult());
            target.Available = true;
            interactor.ExecuteAsync(target.Identity, null).GetAwaiter().GetResult();
            Assert.That(target.Executions, Is.EqualTo(1));
        }
    }
}
