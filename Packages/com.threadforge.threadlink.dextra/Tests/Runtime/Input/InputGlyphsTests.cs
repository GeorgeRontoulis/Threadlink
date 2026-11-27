namespace Threadforge.Threadlink.Tests
{
    using System;
    using Cysharp.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.InputSystem;

    public class InputGlyphsTests
    {
        private InputGlyphs glyphs;
        private Sprite south,
            cross,
            space;
        private Keyboard keyboard;
        private Gamepad pad;
        private Gamepad playStation;

        [SetUp]
        public void SetUp()
        {
            glyphs = ScriptableObject.CreateInstance<InputGlyphs>();
            var texture = Texture2D.whiteTexture;
            south = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            cross = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            space = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            glyphs.Add(InputDeviceFamily.Gamepad, "<Gamepad>/buttonSouth", south);
            glyphs.Add(InputDeviceFamily.PlayStation, "<Gamepad>/buttonSouth", cross);
            glyphs.Add(InputDeviceFamily.Any, "<Keyboard>/space", space);
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
            // By layout name, so the test compiles on platforms without the PlayStation layouts.
            playStation = (Gamepad)InputSystem.AddDevice("DualShock4GamepadHID");
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(pad);
            InputSystem.RemoveDevice(playStation);
            UnityEngine.Object.Destroy(glyphs);
            UnityEngine.Object.Destroy(south);
            UnityEngine.Object.Destroy(cross);
            UnityEngine.Object.Destroy(space);
        }

        [Test]
        public void TheDevicesOwnFamilyWinsThenAnyGamepadThenAnyDevice()
        {
            Assert.That(InputGlyphs.FamilyOf(keyboard), Is.EqualTo(InputDeviceFamily.KeyboardMouse));
            Assert.That(InputGlyphs.FamilyOf(pad), Is.EqualTo(InputDeviceFamily.Gamepad));
            Assert.That(InputGlyphs.FamilyOf(playStation), Is.EqualTo(InputDeviceFamily.PlayStation));

            Assert.That(glyphs.Find(playStation.buttonSouth), Is.SameAs(cross));
            Assert.That(glyphs.Find(pad.buttonSouth), Is.SameAs(south));
            Assert.That(glyphs.Find(keyboard.spaceKey), Is.SameAs(space));
            Assert.That(glyphs.Find(pad.buttonNorth), Is.Null);
            Assert.That(glyphs.Find(null), Is.Null);
        }

        [Test]
        public void PlayersShowTheGlyphOfTheDeviceTheyUsedLast()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var jump = actions.AddActionMap("Gameplay").AddAction("Jump", InputActionType.Button);
            jump.AddBinding("<Gamepad>/buttonSouth");
            jump.AddBinding("<Keyboard>/space");
            var player = new PlayerInputContext(actions, keyboard, playStation);
            try
            {
                var id = InputId.Of(player.Actions.FindAction("Gameplay/Jump"));
                player.RecordUse(playStation);
                Assert.That(player.Glyph(id, glyphs), Is.SameAs(cross));
                player.RecordUse(keyboard);
                Assert.That(player.Glyph(id, glyphs), Is.SameAs(space));
                Assert.That(player.Glyph(id, ScriptableObject.CreateInstance<InputGlyphs>()), Is.Null, "no glyph: show Prompt instead");
            }
            finally
            {
                player.Dispose();
                UnityEngine.Object.Destroy(actions);
            }
        }

        [Test]
        public void APromptForAnUnknownActionShowsItsPath()
        {
            var prompt = new InputPrompt { Action = "Nowhere/Nothing" };
            Assert.That(prompt.ShowsGlyph, Is.False);
            Assert.That(prompt.Text, Is.EqualTo("Nowhere/Nothing"));
        }
    }

    public class ScreenBackTests
    {
        private sealed class View : IScreenView
        {
            public UniTask PrepareAsync(System.Threading.CancellationToken token) => UniTask.CompletedTask;

            public void SetVisible(bool visible) { }

            public void SetFocused(bool focused) { }

            public void Dispose() { }
        }

        private sealed class Factory : IScreenFactory
        {
            public UniTask<IScreenView> CreateAsync(Guid screen, System.Threading.CancellationToken token) => UniTask.FromResult<IScreenView>(new View());
        }

        private sealed class SubPanel : MonoBehaviour, IScreenBackHandler
        {
            internal bool Open = true;

            public bool OnBack()
            {
                bool handled = Open;
                Open = false;
                return handled;
            }
        }

        [Test]
        public void TheFocusedScreenIsTheLatestOnTheHighestLayer()
        {
            using var host = new ScreenHost(new Factory());
            var menu = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Menu).GetAwaiter().GetResult();
            var hud = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Hud).GetAwaiter().GetResult();
            Assert.That(host.Focused, Is.SameAs(menu));
            Assert.That(menu.ClosesOnBack, Is.False, "closing on Back is opt-in");
            var modal = host.OpenAsync(Guid.NewGuid(), ScreenLayer.Modal).GetAwaiter().GetResult();
            Assert.That(host.Focused, Is.SameAs(modal));
            host.CloseAsync(modal).GetAwaiter().GetResult();
            Assert.That(host.Focused, Is.SameAs(menu));
            host.CloseAsync(menu).GetAwaiter().GetResult();
            Assert.That(host.Focused, Is.SameAs(hud));
        }

        [Test]
        public void BackHandlersOnTheScreenTakeBackFirst()
        {
            var screen = new GameObject("Screen with sub-panel");
            try
            {
                var panel = screen.AddComponent<SubPanel>();
                Assert.That(ScreenListeners.Back(screen), Is.True, "the open sub-panel handles Back");
                Assert.That(panel.Open, Is.False);
                Assert.That(ScreenListeners.Back(screen), Is.False, "then the screen itself goes back");
                Assert.That(ScreenListeners.Back(null), Is.False);
            }
            finally
            {
                UnityEngine.Object.Destroy(screen);
            }
        }
    }
}
