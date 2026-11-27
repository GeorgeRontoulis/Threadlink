namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>UI Toolkit workspace hosting module-contributed pages.</summary>
    public sealed class WorkspaceWindow : EditorWindow
    {
        private const string SelectedKey = "Threadforge.Threadlink.Page";
        private readonly List<WorkspacePage> pages = new List<WorkspacePage>();
        private ScrollView content;
        private Label pageTitle;
        private WorkspacePage current;
        private IVisualElementScheduledItem live;

        /// <summary>Opens the workspace.</summary>
        [MenuItem("Threadlink/Workspace", priority = 0)]
        public static WorkspaceWindow Open()
        {
            var window = GetWindow<WorkspaceWindow>();
            window.titleContent = new GUIContent("Threadlink");
            window.minSize = new Vector2(640, 360);
            return window;
        }

        /// <summary>Discovers pages with public parameterless constructors, ordered by group and order.</summary>
        public static List<WorkspacePage> DiscoverPages()
        {
            var result = new List<WorkspacePage>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<WorkspacePage>())
                if (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                    result.Add((WorkspacePage)Activator.CreateInstance(type));
            return result
                .OrderBy(page => GroupRank(page.Group))
                .ThenBy(page => page.Group, StringComparer.Ordinal)
                .ThenBy(page => page.Order)
                .ThenBy(page => page.Title, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Pages shown by this window.</summary>
        public IReadOnlyList<WorkspacePage> Pages => pages;

        /// <summary>The selected page.</summary>
        public WorkspacePage CurrentPage => current;

        /// <summary>Selects the first page of the given type, if present.</summary>
        public bool Show(Type pageType)
        {
            var page = pages.FirstOrDefault(candidate => pageType.IsInstanceOfType(candidate));
            if (page == null)
                return false;
            Select(page);
            return true;
        }

        /// <summary>Rebuilds the current page of every open workspace, after a change it shows.</summary>
        public static void Refresh()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<WorkspaceWindow>())
                window.Rebuild();
        }

        private static int GroupRank(string group) =>
            group == "Project" ? 0
            : group == "Runtime" ? 1
            : 2;

        /// <summary>Opens the workspace on the Runtime page.</summary>
        [MenuItem("Threadlink/Runtime Monitor", priority = 1)]
        public static void OpenRuntime() => Open().Show(typeof(RuntimePage));

        private readonly Dictionary<WorkspacePage, VisualElement> items = new Dictionary<WorkspacePage, VisualElement>();
        private Label state;
        private int ticks;

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            pages.Clear();
            items.Clear();
            pages.AddRange(DiscoverPages());
            ThreadlinkTheme.Apply(rootVisualElement);

            var shell = new VisualElement();
            shell.AddToClassList("tl-shell");
            rootVisualElement.Add(shell);

            var sidebar = new ScrollView();
            sidebar.AddToClassList("tl-sidebar");
            var brand = new VisualElement();
            brand.AddToClassList("tl-brand");
            var mark = new VisualElement();
            mark.AddToClassList("tl-brand__mark");
            brand.Add(mark);
            var name = new Label("THREADLINK");
            name.AddToClassList("tl-brand__name");
            brand.Add(name);
            state = new Label();
            state.AddToClassList("tl-brand__state");
            brand.Add(state);
            sidebar.Add(brand);
            string group = null;
            foreach (var page in pages)
            {
                if (page.Group != group)
                {
                    group = page.Group;
                    var header = new Label(group.ToUpperInvariant());
                    header.AddToClassList("tl-group");
                    sidebar.Add(header);
                }
                var selected = page;
                var item = new VisualElement();
                item.AddToClassList("tl-nav");
                item.Add(new Label(page.Title));
                item.RegisterCallback<ClickEvent>(_ => Select(selected));
                items.Add(page, item);
                sidebar.Add(item);
            }
            shell.Add(sidebar);

            var main = new VisualElement();
            main.AddToClassList("tl-main");
            var bar = new VisualElement();
            bar.AddToClassList("tl-header");
            pageTitle = new Label();
            pageTitle.AddToClassList("tl-title");
            bar.Add(pageTitle);
            bar.Add(new Button(Rebuild) { text = "Refresh", tooltip = "Rebuild the current page." });
            main.Add(bar);
            content = new ScrollView();
            content.AddToClassList("tl-content");
            main.Add(content);
            shell.Add(main);

            string remembered = EditorPrefs.GetString(SelectedKey, string.Empty);
            var initial = EditorApplication.isPlaying ? pages.FirstOrDefault(page => page is RuntimePage) : null;
            initial ??= pages.FirstOrDefault(page => page.GetType().FullName == remembered) ?? pages.FirstOrDefault();
            if (initial != null)
                Select(initial);
            else
                content.Add(new HelpBox("No workspace pages are installed.", HelpBoxMessageType.Info));

            live = rootVisualElement.schedule.Execute(RefreshLive).Every(100);
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        private void PlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && !(current is RuntimePage))
                Show(typeof(RuntimePage));
            else if (change == PlayModeStateChange.EnteredEditMode && current is RuntimePage)
                Rebuild();
        }

        private void OnDisable()
        {
            live?.Pause();
            EditorApplication.playModeStateChanged -= PlayModeChanged;
        }

        private void Select(WorkspacePage page)
        {
            current = page;
            EditorPrefs.SetString(SelectedKey, page.GetType().FullName);
            foreach (var pair in items)
                pair.Value.EnableInClassList("tl-nav--selected", pair.Key == page);
            Rebuild();
        }

        private void Rebuild()
        {
            if (current == null || content == null)
                return;
            pageTitle.text = current.Title;
            content.Clear();
            bool fill = current is IFillsWorkspace;
            content.verticalScrollerVisibility = fill ? ScrollerVisibility.Hidden : ScrollerVisibility.Auto;
            content.contentContainer.style.height = StyleKeyword.Auto;
            try
            {
                var built = current.CreateContent(new WorkspaceContext(type => Show(type), Rebuild));
                if (fill)
                    built.style.flexGrow = 1;
                content.Add(built);
                current.Refresh();
            }
            catch (Exception error)
            {
                content.Add(new HelpBox($"{current.Title} failed to build: {error.Message}", HelpBoxMessageType.Error));
                Debug.LogException(error);
            }
        }

        private void RefreshLive()
        {
            if (state != null)
            {
                bool deployed = Threadlink.IsDeployed;
                state.text = deployed ? "LIVE" : EditorApplication.isPlaying ? "DEPLOYING" : "EDIT";
                state.EnableInClassList("tl-brand__state--live", deployed);
            }
            if (current is IFillsWorkspace && content != null && content.contentViewport.layout.height > 0)
                content.contentContainer.style.height = content.contentViewport.layout.height;
            if (current == null || !current.RefreshesInPlayMode || !EditorApplication.isPlaying)
                return;
            // Ordinary pages refresh twice a second; filling pages (the Runtime monitor) every tick.
            if (!(current is IFillsWorkspace) && ++ticks % 5 != 0)
                return;
            try
            {
                current.Refresh();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }

    /// <summary>A page that fills the workspace instead of scrolling, such as the Runtime monitor.</summary>
    public interface IFillsWorkspace { }

    /// <summary>Small UI helpers shared by built-in and module pages. They use the Threadlink theme.</summary>
    public static class WorkspaceUI
    {
        /// <summary>A section heading.</summary>
        public static Label Heading(string text)
        {
            var label = new Label(text.ToUpperInvariant());
            label.AddToClassList("tl-heading");
            return label;
        }

        /// <summary>Wrapped explanatory text.</summary>
        public static Label Text(string text)
        {
            var label = new Label(text);
            label.AddToClassList("tl-text");
            return label;
        }

        /// <summary>A key/value row with optional action button.</summary>
        public static VisualElement Row(string key, string value, string action = null, Action onAction = null, IssueSeverity severity = IssueSeverity.Info)
        {
            var row = new VisualElement();
            row.AddToClassList("tl-row");
            var name = new Label(key);
            name.AddToClassList("tl-key");
            var text = new Label(value);
            text.AddToClassList("tl-value");
            text.EnableInClassList("tl-value--error", severity == IssueSeverity.Error);
            text.EnableInClassList("tl-value--warning", severity == IssueSeverity.Warning);
            row.Add(name);
            row.Add(text);
            if (action != null && onAction != null)
                row.Add(new Button(onAction) { text = action });
            return row;
        }

        /// <summary>A horizontal row of buttons.</summary>
        public static VisualElement Buttons(params (string label, Action action)[] actions)
        {
            var row = new VisualElement();
            row.AddToClassList("tl-buttons");
            foreach (var (label, action) in actions)
                row.Add(new Button(action) { text = label });
            return row;
        }
    }
}
