namespace Threadforge.Threadlink.Showcase
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Threadforge.Threadlink.Showcase.Demos;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>The Showcase gallery: a list of demos, the selected demo's controls and a shared log.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShowcaseApp : MonoBehaviour
    {
        /// <summary>Command-line switch that makes a player run every demo's smoke check, write the result and quit.</summary>
        public const string SmokeArgument = "-showcaseSmoke";

        internal static readonly Color Canvas = new Color(0.018f, 0.018f, 0.022f);
        internal static readonly Color Panel = new Color(0.05f, 0.05f, 0.058f);
        internal static readonly Color Ink = new Color(0.95f, 0.95f, 0.95f);
        internal static readonly Color InkDim = new Color(1f, 1f, 1f, 0.58f);
        internal static readonly Color Accent = new Color(0.35f, 0.62f, 1f);
        internal static readonly Color Good = new Color(0.38f, 0.85f, 0.56f);
        internal static readonly Color Caution = new Color(0.98f, 0.73f, 0.3f);
        internal static readonly Color Bad = new Color(1f, 0.4f, 0.4f);

        private ShowcaseContext context;
        private ShowcaseDemo current;
        private VisualElement panel;
        private Label title,
            packages,
            summary;
        private ScrollView log;
        private readonly List<Label> entries = new List<Label>();

        /// <summary>Every demo, in gallery order.</summary>
        public static IReadOnlyList<Func<ShowcaseDemo>> Demos { get; } = new Func<ShowcaseDemo>[]
        {
            () => new EventsDemo(),
            () => new AwaitedEventsDemo(),
            () => new SubsystemsDemo(),
            () => new ContentDemo(),
            () => new ScenesDemo(),
            () => new AudioDemo(),
            () => new InputDemo(),
            () => new ScreensDemo(),
            () => new InteractionDemo(),
            () => new VaultDemo(),
            () => new SavingDemo(),
            () => new PlatformDemo(),
            () => new DeterministicDemo(),
            () => new MemoryDemo(),
            () => new OperationsDemo(),
        };

        private void OnEnable()
        {
            context = new ShowcaseContext(gameObject);
            context.Logged += Append;
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexGrow = 1;
            root.style.backgroundColor = Canvas;

            var list = new ScrollView();
            list.style.width = 230;
            list.style.backgroundColor = Panel;
            list.style.paddingTop = 14;
            var heading = new Label("THREADLINK SHOWCASE");
            heading.style.color = Ink;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.letterSpacing = 1;
            heading.style.marginLeft = 16;
            heading.style.marginBottom = 12;
            list.Add(heading);
            for (int i = 0; i < Demos.Count; i++)
            {
                var create = Demos[i];
                string name;
                using (var demo = create())
                    name = demo.Title;
                var entry = new Label(name);
                entry.style.color = InkDim;
                entry.style.paddingLeft = 16;
                entry.style.height = 28;
                entry.style.unityTextAlign = TextAnchor.MiddleLeft;
                entry.style.borderLeftWidth = 2;
                entry.style.borderLeftColor = Color.clear;
                int index = i;
                entry.RegisterCallback<ClickEvent>(_ => Select(index));
                entries.Add(entry);
                list.Add(entry);
            }
            root.Add(list);

            var right = new VisualElement();
            right.style.flexGrow = 1;
            right.style.paddingLeft = right.style.paddingRight = 20;
            right.style.paddingTop = 16;
            title = new Label();
            title.style.fontSize = 22;
            title.style.color = Ink;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            packages = new Label();
            packages.style.color = Accent;
            packages.style.marginBottom = 6;
            summary = new Label();
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.color = InkDim;
            summary.style.marginBottom = 12;
            panel = new VisualElement();
            panel.style.flexGrow = 1;
            log = new ScrollView();
            log.style.height = 170;
            log.style.backgroundColor = Panel;
            log.style.paddingLeft = 10;
            log.style.paddingTop = 6;
            log.style.marginBottom = 16;
            var logTitle = new Label("LOG");
            logTitle.style.color = InkDim;
            logTitle.style.fontSize = 10;
            logTitle.style.letterSpacing = 1.5f;
            logTitle.style.marginBottom = 4;
            right.Add(title);
            right.Add(packages);
            right.Add(summary);
            right.Add(panel);
            right.Add(logTitle);
            right.Add(log);
            root.Add(right);
            Select(0);
        }

        private async void Start()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), SmokeArgument) < 0)
                return;
            string result;
            try
            {
                result = await SmokeAllAsync(context, CancellationToken.None);
            }
            catch (Exception error)
            {
                result = "FAIL: " + error;
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "player-result.txt"), result);
            Application.Quit(result.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1);
        }

        private void OnDisable()
        {
            current?.Dispose();
            current = null;
            context?.Dispose();
        }

        private void Select(int index)
        {
            current?.Dispose();
            panel.Clear();
            current = Demos[index]();
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].style.color = i == index ? Ink : InkDim;
                entries[i].style.borderLeftColor = i == index ? Accent : Color.clear;
                entries[i].style.backgroundColor = i == index ? new Color(Accent.r, Accent.g, Accent.b, 0.18f) : Color.clear;
            }
            title.text = current.Title;
            packages.text = "Packages: " + string.Join(", ", current.Packages);
            summary.text = current.Summary;
            try
            {
                current.Build(panel, context);
            }
            catch (Exception error)
            {
                context.Log(current.Title + " could not start: " + error.Message);
            }
        }

        private void Append(string line)
        {
            if (log == null)
                return;
            var label = new Label(line);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = line.Contains("failed") ? Bad : Ink;
            log.Add(label);
            while (log.childCount > 200)
                log.RemoveAt(0);
            log.scrollOffset = new Vector2(0, float.MaxValue);
        }

        internal static void StyleButton(Button button)
        {
            button.style.backgroundColor = new Color(0.09f, 0.09f, 0.1f);
            button.style.color = Ink;
            button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = new Color(1, 1, 1, 0.12f);
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 4;
            button.style.marginRight = 6;
            button.style.paddingLeft = button.style.paddingRight = 10;
            button.style.minHeight = 26;
        }

        /// <summary>Runs every demo's smoke check in order; returns a PASS report or throws on the first failure.</summary>
        public static async UniTask<string> SmokeAllAsync(ShowcaseContext context, CancellationToken token)
        {
            var report = new StringBuilder();
            int frames = Time.frameCount;
            foreach (var create in Demos)
            {
                using var demo = create();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    await demo.SmokeAsync(context, token);
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException(demo.Title + ": " + error.Message, error);
                }
                report.AppendLine($"ok: {demo.Title} ({watch.ElapsedMilliseconds} ms)");
            }
            return $"PASS: {Demos.Count} demos, frames {frames}->{Time.frameCount}\n" + report;
        }
    }
}
