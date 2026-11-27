namespace Threadforge.Threadlink.Showcase
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// One interactive demo. <see cref="Build"/> adds controls to the gallery; <see cref="SmokeAsync"/> exercises the same
    /// features without input and throws if anything misbehaves, so the gallery can verify itself.
    /// </summary>
    public abstract class ShowcaseDemo : IDisposable
    {
        /// <summary>Gallery title.</summary>
        public abstract string Title { get; }

        /// <summary>What the demo shows, in one or two sentences.</summary>
        public abstract string Summary { get; }

        /// <summary>Package name suffixes after <c>com.threadforge.threadlink</c> that the demo uses ("core" for the core).</summary>
        public abstract string[] Packages { get; }

        /// <summary>Adds the demo's controls. Called once when the demo is selected.</summary>
        public abstract void Build(VisualElement panel, ShowcaseContext context);

        /// <summary>Runs the demo's features end to end and throws on any unexpected result.</summary>
        public abstract UniTask SmokeAsync(ShowcaseContext context, CancellationToken token);

        /// <summary>Releases everything the demo created.</summary>
        public virtual void Dispose() { }

        /// <summary>Checks a condition and throws with a description when it is false.</summary>
        protected static void Check(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException("Showcase check failed: " + description);
        }

        /// <summary>A gallery button that runs its action as a recorded operation.</summary>
        protected static Button Action(ShowcaseContext context, string text, Func<UniTask> action)
        {
            var button = new Button(() => context.Run(text, action)) { text = text };
            button.AddToClassList("showcase-action");
            ShowcaseApp.StyleButton(button);
            return button;
        }

        /// <summary>A gallery button for synchronous work.</summary>
        protected static Button Action(ShowcaseContext context, string text, Action action) =>
            Action(context, text, () =>
            {
                action();
                return UniTask.CompletedTask;
            });

        /// <summary>A horizontal row of controls.</summary>
        protected static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.AddToClassList("showcase-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 6;
            foreach (var child in children)
                row.Add(child);
            return row;
        }

        /// <summary>A text line that a demo updates.</summary>
        protected static Label Value(string text = "")
        {
            var label = new Label(text);
            label.AddToClassList("showcase-value");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            label.style.color = ShowcaseApp.Ink;
            return label;
        }

        /// <summary>A small coloured box used to visualise state.</summary>
        protected static Label Box(string text, Color color)
        {
            var box = new Label(text);
            box.AddToClassList("showcase-box");
            box.style.backgroundColor = color;
            box.style.color = Color.white;
            box.style.paddingLeft = box.style.paddingRight = 8;
            box.style.paddingTop = box.style.paddingBottom = 4;
            box.style.marginRight = box.style.marginBottom = 4;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 4;
            box.style.unityTextAlign = TextAnchor.MiddleCenter;
            return box;
        }
    }
}
