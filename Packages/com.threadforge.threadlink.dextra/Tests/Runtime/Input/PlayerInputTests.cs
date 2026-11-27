namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.LowLevel;
    using UnityEngine.TestTools;

    public class PlayerInputTests
    {
        private InputActionAsset source;
        private Keyboard keyboard;
        private Keyboard otherKeyboard;
        private PlayerInputContext player;

        [SetUp]
        public void SetUp()
        {
            source = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplay = source.AddActionMap("Gameplay");
            gameplay.AddAction("Use", InputActionType.Button, "<Keyboard>/e");
            gameplay.AddAction("Other", InputActionType.Button, "<Keyboard>/f");
            var ui = source.AddActionMap("UI");
            ui.AddAction("Accept", InputActionType.Button, "<Keyboard>/enter");
            keyboard = InputSystem.AddDevice<Keyboard>();
            otherKeyboard = InputSystem.AddDevice<Keyboard>();
            player = new PlayerInputContext(source, keyboard);
        }

        [TearDown]
        public void TearDown()
        {
            player.Dispose();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(otherKeyboard);
            UnityEngine.Object.Destroy(source);
        }

        [Test]
        public void ContextClaimsComposeAndClosingOneDoesNotEnableSuppressedGameplay()
        {
            using var gameplay = player.ClaimByName(0, false, "Gameplay");
            var first = player.ClaimByName(10, true, "UI");
            var second = player.ClaimByName(20, true, "UI");
            first.Dispose();
            Assert.That(player.Actions.FindActionMap("Gameplay").enabled, Is.False);
            second.Dispose();
            Assert.That(player.Actions.FindActionMap("Gameplay").enabled, Is.True);
            Assert.That(source.FindActionMap("Gameplay").enabled, Is.False);
        }

        [Test]
        public void OverridesAndConflictResolutionArePlayerIsolated()
        {
            using var second = new PlayerInputContext(source, otherKeyboard);
            player.Actions.FindAction("Gameplay/Use").ApplyBindingOverride(0, "<Keyboard>/f");
            var conflicts = player.GetConflictsByName("Gameplay/Use", 0);
            Assert.That(conflicts.Length, Is.EqualTo(1));
            player.ClearConflictingBinding(new Guid(conflicts[0]));
            Assert.That(
                player.Actions.FindAction("Gameplay/Other").bindings[0].effectivePath,
                Is.Empty
            );
            string saved = player.ExportBindings();
            player.ResetBindings();
            player.ImportBindings(saved);
            Assert.That(
                player.Actions.FindAction("Gameplay/Use").bindings[0].effectivePath,
                Is.EqualTo("<Keyboard>/f")
            );
            Assert.That(
                second.Actions.FindAction("Gameplay/Use").bindings[0].effectivePath,
                Is.EqualTo("<Keyboard>/e")
            );
        }

        [UnityTest]
        public IEnumerator RebindingAcceptsOnlyPairedDevicesAndCancellationRestoresClaims()
        {
            using var claim = player.ClaimByName(0, false, "Gameplay");
            using var cancel = new CancellationTokenSource();
            var rebinding = player.Rebind("Gameplay/Use", 0, cancel.Token);
            InputSystem.QueueStateEvent(otherKeyboard, new KeyboardState(Key.F));
            InputSystem.Update();
            yield return null;
            Assert.That(rebinding.Status.IsCompleted(), Is.False);
            cancel.Cancel();
            for (float end = Time.realtimeSinceStartup + 5f; Time.realtimeSinceStartup < end && !rebinding.Status.IsCompleted();)
                yield return null;
            Assert.That(rebinding.Status.IsCanceled(), Is.True);
            Assert.That(player.Actions.FindAction("Gameplay/Use").enabled, Is.True);
            Assert.That(
                player.Actions.FindAction("Gameplay/Use").bindings[0].effectivePath,
                Is.EqualTo("<Keyboard>/e")
            );
        }

        [Test]
        public void ExplicitReassignmentDoesNotStealAnotherPlayersDevice()
        {
            using var second = new PlayerInputContext(source, otherKeyboard);
            Assert.Throws<InvalidOperationException>(() => player.ReassignDevices(otherKeyboard));
            Assert.That(player.IsConnected, Is.True);
            player.ReassignDevices();
            Assert.That(player.IsConnected, Is.False);
            player.ReassignDevices(keyboard);
            Assert.That(player.IsConnected, Is.True);
        }

        [UnityTest]
        public IEnumerator RebindingCompletionAppliesActualPairedControl()
        {
            using var claim = player.ClaimByName(0, false, "Gameplay");
            var task = player.Rebind("Gameplay/Use", 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
            InputSystem.Update();
            for (float end = Time.realtimeSinceStartup + 5f; Time.realtimeSinceStartup < end && !task.Status.IsCompleted();)
                yield return null;
            if (!task.Status.IsCompleted())
                player.CancelRebind();
            Assert.That(task.Status.IsCompletedSuccessfully(), Is.True);
            Assert.That(
                player.Actions.FindAction("Gameplay/Use").bindings[0].overridePath,
                Does.Contain("g")
            );
            Assert.That(player.PromptByName("Gameplay/Use"), Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator CompositeCancellationRestoresEarlierCompletedParts()
        {
            var move = player
                .Actions.FindActionMap("Gameplay")
                .AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            using var claim = player.ClaimByName(0, false, "Gameplay");
            string original = player.ExportBindings();
            var task = player.Rebind("Gameplay/Move", 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            InputSystem.Update();
            for (float end = Time.realtimeSinceStartup + 5f; Time.realtimeSinceStartup < end && string.IsNullOrEmpty(move.bindings[1].overridePath);)
                yield return null;
            Assert.That(move.bindings[1].overridePath, Is.Not.Empty);
            Assert.That(task.Status.IsCompleted(), Is.False);
            player.CancelRebind();
            for (float end = Time.realtimeSinceStartup + 5f; Time.realtimeSinceStartup < end && !task.Status.IsCompleted();)
                yield return null;
            Assert.That(task.Status.IsCanceled(), Is.True);
            Assert.That(player.ExportBindings(), Is.EqualTo(original));
        }
    }
}
