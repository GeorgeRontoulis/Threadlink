namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Dextra screens from a UI Toolkit document.</summary>
    public sealed class ScreensDemo : ShowcaseDemo
    {
        private VisualTreeAsset document;
        private ScreenHandle open;

        public override string Title => "Screens";

        public override string Summary =>
            "Dextra.Screens opens prefab screens from the content catalog or UI Toolkit documents directly. A screen prepares before it shows, takes focus, blocks gameplay input while open, and gives focus and input back when it closes.";

        public override string[] Packages => new[] { "dextra" };

        private VisualTreeAsset Document()
        {
            if (document != null)
                return document;
            document = ScriptableObject.CreateInstance<VisualTreeAsset>();
            document.name = "Showcase Pause";
            return document;
        }

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            var status = Value("Open screens: " + Dextra.Screens.OpenCount);
            panel.Add(status);
            panel.Add(Row(
                Action(context, "Open a pause screen", async () =>
                {
                    if (open != null && !open.IsClosed)
                        return;
                    open = await Dextra.Screens.OpenAsync(Document(), ScreenLayer.Modal);
                    status.text = "Open screens: " + Dextra.Screens.OpenCount;
                }),
                Action(context, "Close it", async () =>
                {
                    if (open == null || open.IsClosed)
                        return;
                    await Dextra.Screens.CloseAsync(open);
                    status.text = "Open screens: " + Dextra.Screens.OpenCount;
                })));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            int before = Dextra.Screens.OpenCount;
            var screen = await Dextra.Screens.OpenAsync(Document(), ScreenLayer.Menu, token: token);
            Check(!screen.IsClosed && Dextra.Screens.OpenCount == before + 1, "a document opens as a screen");
            Check(screen.Layer == ScreenLayer.Menu, "the screen is on its layer");
            await Dextra.Screens.CloseAsync(screen, token);
            Check(screen.IsClosed && Dextra.Screens.OpenCount == before, "closing removes the screen");
        }

        public override void Dispose()
        {
            if (open != null && !open.IsClosed && Dextra.TryGetSingleton(out _))
                Dextra.Screens.CloseAsync(open).Forget();
            if (document != null)
                Object.Destroy(document);
        }
    }
}
