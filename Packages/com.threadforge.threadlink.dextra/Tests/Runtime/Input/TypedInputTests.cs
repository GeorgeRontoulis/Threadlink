namespace Threadforge.Threadlink.Tests
{
    using System;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.LowLevel;

    public sealed class TypedInputTests
    {
        private InputActionAsset source;
        private Keyboard keyboard;

        [SetUp]
        public void SetUp()
        {
            source = ScriptableObject.CreateInstance<InputActionAsset>();
            source.AddActionMap("Gameplay").AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            source.AddActionMap("UI").AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            UnityEngine.Object.Destroy(source);
        }

        [Test]
        public void TypedIdsReadClaimAndPromptThroughTheActionId()
        {
            var jump = InputId<float>.Of(source.FindAction("Gameplay/Jump"));
            var gameplay = InputMapId.Of(source.FindActionMap("Gameplay"));
            var ui = InputMapId.Of(source.FindActionMap("UI"));
            using var player = new PlayerInputContext(source, keyboard);
            using (player.Claim(0, false, gameplay))
            {
                Assert.That(player.Action(jump), Is.Not.SameAs(source.FindAction("Gameplay/Jump")), "Each player reads its own copy of the action.");
                Assert.That(player.Action(jump).id, Is.EqualTo(jump.Value));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                InputSystem.Update();
                Assert.That(player.IsPressed(jump), Is.True);
                Assert.That(player.Read(jump), Is.EqualTo(1f));
                Assert.That(player.Prompt(jump), Is.Not.Empty);
                using (player.Claim(10, true, ui))
                    Assert.That(player.Actions.FindActionMap(gameplay.Value).enabled, Is.False);
                Assert.That(player.Actions.FindActionMap(gameplay.Value).enabled, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
            }
        }

        [Test]
        public void IdsFromAnotherAssetResolveByPath()
        {
            var other = ScriptableObject.CreateInstance<InputActionAsset>();
            var jump = InputId<float>.Of(other.AddActionMap("Gameplay").AddAction("Jump"));
            var ui = InputMapId.Of(other.AddActionMap("UI"));
            using var player = new PlayerInputContext(source, keyboard);
            Assert.That(player.Action(jump).name, Is.EqualTo("Jump"), "An ID authored in another asset finds the action with the same path.");
            using (player.Claim(10, true, ui))
                Assert.That(player.Actions.FindActionMap("UI").enabled, Is.True);
            UnityEngine.Object.Destroy(other);
        }

        [Test]
        public void UnknownIdsNameWhatIsMissing()
        {
            using var player = new PlayerInputContext(source, keyboard);
            var error = Assert.Throws<ArgumentException>(() => player.Action(new InputId(Guid.NewGuid(), "Gameplay/Missing")));
            StringAssert.Contains("Gameplay/Missing", error.Message);
            Assert.Throws<ArgumentException>(() => player.Claim(0, false, new InputMapId(Guid.NewGuid(), "Missing")));
        }
    }
}
