namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;

    /// <summary>Dextra players, claims, action queries and prompts.</summary>
    public sealed class InputDemo : ShowcaseDemo
    {
        private InputActionAsset actions;
        private Keyboard keyboard;
        private PlayerInputContext player;
        private IDisposable menu;
        private InputId<float> jump;
        private InputMapId gameplayMap,
            uiMap;

        public override string Title => "Input";

        public override string Summary =>
            "Dextra gives each player a copy of the input actions paired with their devices. Player 0 is ready before Awake with the actions from Project Settings ▸ Threadlink ▸ Dextra. Claims switch action maps by priority; rebinds are saved automatically; prompts follow the last used device.";

        public override string[] Packages => new[] { "dextra" };

        private void Prepare()
        {
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplay = actions.AddActionMap("Gameplay");
            jump = InputId<float>.Of(gameplay.AddAction("Jump", InputActionType.Button, "<Keyboard>/space"));
            var ui = actions.AddActionMap("UI");
            ui.AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
            // A project uses its generated GameInput IDs; actions created at runtime get theirs with Of.
            gameplayMap = InputMapId.Of(gameplay);
            uiMap = InputMapId.Of(ui);
            keyboard = InputSystem.AddDevice<Keyboard>("Showcase Keyboard");
            player = Dextra.AddPlayer(actions, keyboard);
        }

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            Prepare();
            var status = Value();
            void Show() => status.text = "Gameplay map: " + (player.Actions.FindActionMap("Gameplay").enabled ? "on" : "off") + "   UI map: " + (player.Actions.FindActionMap("UI").enabled ? "on" : "off") + "   Jump prompt: " + player.Prompt(jump);
            Show();
            panel.Add(status);
            panel.Add(Row(
                Action(context, "Open a menu (claim UI)", () =>
                {
                    menu ??= player.Claim(100, true, uiMap);
                    Show();
                }),
                Action(context, "Close the menu", () =>
                {
                    menu?.Dispose();
                    menu = null;
                    Show();
                })));
            panel.Add(Value("Players this session: " + Dextra.Players.Count));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            Prepare();
            Check(player.Actions.FindActionMap("Gameplay").enabled, "the gameplay map is enabled for a new player");
            using (player.Claim(100, true, uiMap))
            {
                Check(!player.Actions.FindActionMap("Gameplay").enabled && player.Actions.FindActionMap("UI").enabled, "a blocking claim suspends gameplay");
            }
            Check(player.Actions.FindActionMap("Gameplay").enabled, "releasing the claim restores gameplay");
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Space));
            InputSystem.Update();
            Check(player.IsPressed(jump), "action queries read the player's own device");
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            InputSystem.Update();
            Check(!string.IsNullOrEmpty(player.Prompt(jump)), "prompts describe the binding");
            await UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            menu?.Dispose();
            if (player != null && Dextra.TryGetSingleton(out _))
                Dextra.RemovePlayer(player);
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            if (actions != null)
                UnityEngine.Object.Destroy(actions);
        }
    }
}
