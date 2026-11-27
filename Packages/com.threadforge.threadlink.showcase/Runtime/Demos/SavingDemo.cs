namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using MessagePack;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>The save data: a stable name and version, serialized with MessagePack.</summary>
    [Save("showcase", 1)]
    [MessagePackObject]
    public sealed partial class ShowcaseSave
    {
        /// <summary>Collected coins.</summary>
        [Key(0)]
        public int Coins { get; set; }

        /// <summary>The level the player is on.</summary>
        [Key(1)]
        public string Level { get; set; } = "Forest";
    }

    /// <summary>Sentinel saves: per account, transactional, versioned and recoverable.</summary>
    public sealed class SavingDemo : ShowcaseDemo
    {
        private const int Slot = 900;
        private readonly ShowcaseSave game = new ShowcaseSave();

        public override string Title => "Saving";

        public override string Summary =>
            "Declare save data once with [Save]. Sentinel.Saves writes slots atomically for the current account, refuses to overwrite a slot that changed elsewhere, migrates older versions, and restores the previous version of a damaged slot.";

        public override string[] Packages => new[] { "sentinel" };

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            var status = Value("Coins: 0");
            panel.Add(status);
            panel.Add(Row(
                Action(context, "Collect a coin", () => status.text = "Coins: " + ++game.Coins),
                Action(context, "Save", async () =>
                {
                    var saved = await Sentinel.Saves.SaveAsync(Slot, game, "Showcase, " + game.Coins + " coins");
                    context.Log(saved.Succeeded ? "Saved at " + saved.Value.SavedAt.ToLocalTime() : "Save failed: " + saved.Error);
                }),
                Action(context, "Load", async () =>
                {
                    var loaded = await Sentinel.Saves.LoadAsync<ShowcaseSave>(Slot);
                    if (loaded.Succeeded)
                        game.Coins = loaded.Value.Coins;
                    status.text = "Coins: " + game.Coins;
                    context.Log(loaded.Succeeded ? "Loaded." : "Nothing loaded: " + loaded.Error);
                }),
                Action(context, "Delete", async () => context.Log("Deleted: " + (await Sentinel.Saves.DeleteAsync<ShowcaseSave>(Slot)).Succeeded))));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            await Sentinel.Saves.DeleteAsync<ShowcaseSave>(Slot, token);
            var empty = await Sentinel.Saves.LoadAsync<ShowcaseSave>(Slot, token);
            Check(!empty.Succeeded && empty.Error == PersistenceError.NotFound, "an empty slot reports NotFound");
            var saved = await Sentinel.Saves.SaveAsync(Slot, new ShowcaseSave { Coins = 12 }, "Smoke", token);
            Check(saved.Succeeded, "a save succeeds: " + saved.Error + " " + saved.Message);
            var loaded = await Sentinel.Saves.LoadAsync<ShowcaseSave>(Slot, token);
            Check(loaded.Succeeded && loaded.Value.Coins == 12 && loaded.Value.Level == "Forest", "the save loads back");
            var again = await Sentinel.Saves.SaveAsync(Slot, new ShowcaseSave { Coins = 13 }, "Smoke", token);
            Check(again.Succeeded, "saving again after a load succeeds");
            Check(System.Array.IndexOf(Sentinel.Saves.List<ShowcaseSave>(), Slot) >= 0, "the slot is listed");
            var restored = await Sentinel.Saves.RestorePreviousAsync<ShowcaseSave>(Slot, token);
            Check(restored.Succeeded, "the previous version can be restored");
            var previous = await Sentinel.Saves.LoadAsync<ShowcaseSave>(Slot, token);
            Check(previous.Succeeded && previous.Value.Coins == 12, "restoring brings back the earlier version");
            await Sentinel.Saves.DeleteAsync<ShowcaseSave>(Slot, token);
        }
    }
}
