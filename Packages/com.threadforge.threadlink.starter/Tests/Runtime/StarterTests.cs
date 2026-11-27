namespace Threadforge.Threadlink.Starter.Tests
{
    using Cysharp.Threading.Tasks;
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.TestTools;

    public sealed class StarterTests
    {
        [Test]
        public void TheGameIsASessionSubsystem()
        {
            Assert.That(StarterGame.TryGetSingleton(out var game), Is.True, "StarterGame registers automatically and deploys with the session.");
            Assert.That(game.State, Is.EqualTo(SubsystemState.Ready));
            Assert.That(Threadlink.Instance.Subsystems, Has.Member(game));
        }

        [Test]
        public void IncompleteContentIsReported()
        {
            var content = ScriptableObject.CreateInstance<StarterContent>();
            StringAssert.Contains("Input", content.Validate());
            content.Input = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplay = content.Input.AddActionMap("Gameplay");
            gameplay.AddAction("Move");
            gameplay.AddAction("Interact");
            gameplay.AddAction("Pause");
            content.Input.AddActionMap("UI");
            StringAssert.Contains("Panel", content.Validate());
            Object.Destroy(content.Input);
            Object.Destroy(content);
        }

        [UnityTest]
        public IEnumerator SavesRoundTripThroughSentinel()
        {
            var save = new StarterSave { Level = "Forest", Position = new[] { 1f, 2f, 3f }, PlaySeconds = 4 };
            save.Collected.Add("lantern-a");
            var saved = Sentinel.Saves.SaveAsync(950, save);
            while (!saved.GetAwaiter().IsCompleted)
                yield return null;
            Assert.That(saved.GetAwaiter().GetResult().Succeeded, Is.True);
            var loaded = Sentinel.Saves.LoadAsync<StarterSave>(950);
            while (!loaded.GetAwaiter().IsCompleted)
                yield return null;
            var result = loaded.GetAwaiter().GetResult();
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.Collected, Is.EqualTo(new[] { "lantern-a" }));
            Assert.That(result.Value.Position, Is.EqualTo(new[] { 1f, 2f, 3f }));
            var deleted = Sentinel.Saves.DeleteAsync<StarterSave>(950);
            while (!deleted.GetAwaiter().IsCompleted)
                yield return null;
        }
    }
}
