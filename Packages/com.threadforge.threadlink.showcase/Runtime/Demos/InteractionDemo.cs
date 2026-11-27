namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.Localization;
    using UnityEngine.UIElements;

    /// <summary>Interactables found by an InteractionSensor.</summary>
    public sealed class InteractionDemo : ShowcaseDemo
    {
        /// <summary>A chest: an Interactable with a prompt and an awaited interaction.</summary>
        public sealed class ShowcaseChest : Interactable
        {
            /// <summary>Whether the chest is open.</summary>
            public bool Opened { get; private set; }

            // Entries of the Showcase string table, registered by the Workspace's Localization setup.
            private static readonly LocalizedString Closed = new LocalizedString(ShowcaseText.Table, "open-chest");
            private static readonly LocalizedString Empty = new LocalizedString(ShowcaseText.Table, "empty-chest");

            public override LocalizedString Prompt => Opened ? Empty : Closed;

            public override bool CanInteract(GameObject actor) => base.CanInteract(actor) && !Opened;

            protected override async UniTask InteractAsync(GameObject actor, CancellationToken token)
            {
                await UniTask.NextFrame(token);
                Opened = true;
            }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private InteractionSensor sensor;
        private ShowcaseChest chest;

        public override string Title => "Interaction";

        public override string Summary =>
            "Derive from Interactable for anything the player can use; an InteractionSensor on the player finds the closest available one through physics, exposes its prompt, and runs one interaction at a time.";

        public override string[] Packages => new[] { "dextra" };

        private void Prepare()
        {
            var player = new GameObject("Showcase Player");
            sensor = player.AddComponent<InteractionSensor>();
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Showcase Chest";
            target.transform.position = new Vector3(1, 0, 0);
            chest = target.AddComponent<ShowcaseChest>();
            created.Add(player);
            created.Add(target);
            Physics.SyncTransforms();
        }

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            Prepare();
            var prompt = Value("Prompt: …");
            panel.Add(prompt);
            panel.Add(Row(
                Action(context, "Read the prompt", () => prompt.text = "Prompt: " + (sensor.Target != null ? sensor.Prompt.GetLocalizedString() : "nothing in range")),
                Action(context, "Interact", async () =>
                {
                    bool done = await sensor.InteractAsync();
                    context.Log(done ? "Interacted: " + chest.Prompt.GetLocalizedString() : "Nothing to interact with.");
                })));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            Prepare();
            await UniTask.NextFrame(token);
            await UniTask.NextFrame(token);
            Check(sensor.Target != null, "the sensor selects the chest in range");
            Check(ReferenceEquals(sensor.Prompt, chest.Prompt) && sensor.Prompt.TableEntryReference.Key == "open-chest", "the prompt comes from the target");
            Check(await sensor.InteractAsync(), "the interaction runs");
            Check(chest.Opened, "the chest opened");
            await UniTask.NextFrame(token);
            Check(sensor.Target == null, "an unavailable target is no longer selected");
        }

        public override void Dispose()
        {
            foreach (var item in created)
                if (item != null)
                    Object.Destroy(item);
            created.Clear();
        }
    }
}
