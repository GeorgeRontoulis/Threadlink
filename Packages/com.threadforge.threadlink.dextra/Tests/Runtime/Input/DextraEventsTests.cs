namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Users;

    public sealed class DextraEventsTests
    {
        [ManualRegistration]
        public sealed class Watcher : ThreadlinkSubsystem<Watcher>
        {
            public static readonly List<PlayerJoined> Joined = new List<PlayerJoined>();
            private SubscriptionToken token;

            protected override void OnSubsystemBoot() => token = Iris.Subscribe<PlayerJoined>(Joined.Add);

            protected override void OnSubsystemShutdown() => token.Dispose();
        }

        private sealed class View : IScreenView
        {
            public UniTask PrepareAsync(CancellationToken token) => UniTask.CompletedTask;

            public void SetVisible(bool visible) { }

            public void SetFocused(bool focused) { }

            public void Dispose() { }
        }

        private sealed class Factory : IScreenFactory
        {
            public UniTask<IScreenView> CreateAsync(Guid id, CancellationToken token) => UniTask.FromResult<IScreenView>(new View());
        }

        private readonly List<InputDevice> devices = new List<InputDevice>();
        private InputActionAsset source;

        [SetUp]
        public void SetUp()
        {
            source = ScriptableObject.CreateInstance<InputActionAsset>();
            source.AddActionMap("Gameplay").AddAction("Jump", binding: "<Gamepad>/buttonSouth");
            source.AddActionMap("UI").AddAction("Submit", binding: "<Keyboard>/enter");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var device in devices)
                if (device.added)
                    InputSystem.RemoveDevice(device);
            devices.Clear();
            UnityEngine.Object.DestroyImmediate(source);
        }

        private T Add<T>()
            where T : InputDevice
        {
            var device = InputSystem.AddDevice<T>();
            devices.Add(device);
            return device;
        }

        [Test]
        public void PlayersAtStartupAreAnnouncedToBootTimeSubscribers()
        {
            Watcher.Joined.Clear();
            using var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Dextra), typeof(Watcher));
            Assert.That(Watcher.Joined.Count, Is.EqualTo(Dextra.Players.Count), "Each startup player is announced once, during initialization.");
            for (int i = 0; i < Watcher.Joined.Count; i++)
                Assert.That(Watcher.Joined[i].Player, Is.SameAs(Dextra.Players[i]));
        }

        [Test]
        public void JoiningLeavingAndLosingDevicesAreAnnounced()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Dextra));
            var heard = new List<string>();
            Iris.Subscribe<PlayerJoined>(e => heard.Add("joined " + e.Index));
            Iris.Subscribe<PlayerLeft>(e => heard.Add("left " + e.Index));
            Iris.Subscribe<DeviceLost>(e => heard.Add("lost " + e.Device.layout));
            Iris.Subscribe<DeviceRegained>(e => heard.Add("regained " + e.Device.layout));
            var gamepad = Add<Gamepad>();
            var player = Dextra.AddPlayer(source, gamepad);
            int index = Dextra.Players.Count - 1;
            InputSystem.RemoveDevice(gamepad);
            Assert.That(player.IsConnected, Is.False);
            InputSystem.AddDevice(gamepad);
            Assert.That(player.IsConnected, Is.True, "The Input System returns a reconnected device to its player.");
            Dextra.RemovePlayer(player);
            Assert.That(heard, Is.EqualTo(new[] { "joined " + index, "lost Gamepad", "regained Gamepad", "left " + index }));
        }

        [Test]
        public void SwitchingToAnotherKindOfDeviceChangesTheControls()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Dextra));
            var changed = new List<InputDevice>();
            Iris.Subscribe<ControlsChanged>(e => changed.Add(e.Device));
            var keyboard = Add<Keyboard>();
            var mouse = Add<Mouse>();
            var gamepad = Add<Gamepad>();
            var player = Dextra.AddPlayer(source, keyboard, mouse, gamepad);
            player.RecordUse(mouse);
            Assert.That(changed, Is.Empty, "Keyboard and mouse are one kind of controls.");
            player.RecordUse(gamepad);
            player.RecordUse(gamepad);
            player.RecordUse(keyboard);
            Assert.That(changed, Is.EqualTo(new InputDevice[] { gamepad, keyboard }));
        }

        [Test]
        public void ChangedBindingsAreAnnounced()
        {
            // Keep the project's saved bindings untouched.
            var settings = DextraSettings.Current;
            bool saving = settings.SaveBindings;
            settings.SaveBindings = false;
            try
            {
                using var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Dextra));
                int announced = 0;
                Iris.Subscribe<BindingsChanged>(_ => announced++);
                var player = Dextra.AddPlayer(source, Add<Gamepad>());
                player.ImportBindings(player.ExportBindings());
                player.ResetBindings();
                Assert.That(announced, Is.EqualTo(1), "Importing saved bindings is not a change; resetting them is.");
            }
            finally
            {
                settings.SaveBindings = saving;
            }
        }

        [Test]
        public void DevicesPlayerZeroUsesImplicitlyGoToAPlayerAssignedThem()
        {
            var gamepad = Add<Gamepad>();
            // Take the device from whoever borrowed it, then free it.
            using (new PlayerInputContext(source, gamepad)) { }
            Assert.That(InputUser.FindUserPairedToDevice(gamepad).HasValue, Is.False);
            using var zero = new PlayerInputContext(source);
            zero.Lend(gamepad);
            Assert.That(InputUser.FindUserPairedToDevice(gamepad)?.id, Is.EqualTo(zero.UserId));
            using var assigned = new PlayerInputContext(source, gamepad);
            Assert.That(InputUser.FindUserPairedToDevice(gamepad)?.id, Is.EqualTo(assigned.UserId));
            zero.Lend(gamepad);
            Assert.That(InputUser.FindUserPairedToDevice(gamepad)?.id, Is.EqualTo(assigned.UserId), "A device assigned explicitly is not lent back.");
            Assert.Throws<InvalidOperationException>(() => zero.ReassignDevices(gamepad), "An explicit assignment is not taken over.");
        }

        [Test]
        public void ScreensAndBlockedGameplayInputAreReported()
        {
            var player = new PlayerInputContext(source, Add<Keyboard>());
            var blocker = new InputBlocker(() => player, () => "UI");
            var blocks = new List<bool>();
            blocker.BlockChanged = blocks.Add;
            var opened = new List<ScreenHandle>();
            var closed = new List<ScreenHandle>();
            var host = new ScreenHost(new Factory(), blocker) { Opened = opened.Add, Closed = closed.Add };
            try
            {
                var menu = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Menu, true, true).GetAwaiter().GetResult();
                var modal = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Modal, true, true).GetAwaiter().GetResult();
                host.CloseAsync(modal).GetAwaiter().GetResult();
                Assert.That(blocks, Is.EqualTo(new[] { true }), "Blocking starts with the first screen and holds while one is open.");
                host.Dispose();
                Assert.That(opened, Is.EqualTo(new[] { menu, modal }));
                Assert.That(closed, Is.EqualTo(new[] { modal, menu }), "Disposing the host closes the remaining screens.");
                Assert.That(blocks, Is.EqualTo(new[] { true, false }));
            }
            finally
            {
                host.Dispose();
                player.Dispose();
            }
        }
    }
}
