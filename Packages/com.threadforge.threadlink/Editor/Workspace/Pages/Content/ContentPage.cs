namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEngine.UIElements;

    /// <summary>
    /// The Content mapping: tick the Addressables entries game code loads by ID, then Apply. Addressables owns groups,
    /// addresses, labels and delivery; this page only decides which entries get generated IDs.
    /// </summary>
    public sealed class ContentPage : WorkspacePage
    {
        private static string filter = string.Empty;
        private static readonly HashSet<string> collapsed = new HashSet<string>(StringComparer.Ordinal);

        private List<ContentMappingRow> rows;
        private Dictionary<string, bool> applied;
        private Label pending;
        private Button apply,
            revert;
        private VisualElement groups;

        /// <inheritdoc />
        public override string Title => "Content";

        /// <inheritdoc />
        public override int Order => 20;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            root.Add(
                WorkspaceUI.Text(
                    "Tick the Addressables entries game code loads by ID, then Apply. Each becomes ContentAssets, ContentPrefabs or ContentScenes by how it loads, nested by group. An ID keeps its value when the asset is renamed, moved or regrouped."
                )
            );
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            if (addressables == null)
            {
                root.Add(WorkspaceUI.Row("Addressables", "Not set up. Create its settings in the Groups window first.", "Open Groups", () => context.OpenMenu("Window/Asset Management/Addressables/Groups"), IssueSeverity.Warning));
                return root;
            }
            rows = ContentMapping.Load();
            applied = rows.ToDictionary(row => row.Guid, row => row.Mapped, StringComparer.Ordinal);

            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            var search = new TextField { value = filter, tooltip = "Show entries whose name contains this text." };
            search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(change =>
            {
                filter = change.newValue ?? string.Empty;
                Build();
            });
            toolbar.Add(search);
            toolbar.Add(new Button(() => context.OpenMenu("Window/Asset Management/Addressables/Groups")) { text = "Addressables Groups", tooltip = "Make assets addressable, and organize groups and labels." });
            toolbar.Add(new Button(context.Refresh) { text = "Reload", tooltip = "Read the Addressables groups again." });
            root.Add(toolbar);

            groups = new VisualElement();
            root.Add(groups);

            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 8 } };
            pending = new Label();
            pending.AddToClassList("tl-text");
            pending.style.flexGrow = 1;
            revert = new Button(() =>
            {
                foreach (var row in rows)
                    row.Mapped = applied[row.Guid];
                Build();
            })
            {
                text = "Revert",
                tooltip = "Discard the changes since the last Apply.",
            };
            apply = new Button(() =>
            {
                ContentMapping.Apply(rows);
                context.Refresh();
            })
            {
                text = "Apply",
                tooltip = "Write the mapping and regenerate the content IDs. Scripts recompile afterwards.",
            };
            footer.Add(pending);
            footer.Add(revert);
            footer.Add(apply);
            root.Add(footer);

            var labels = addressables.GetLabels();
            root.Add(WorkspaceUI.Heading("Labels"));
            root.Add(WorkspaceUI.Row("ContentLabels", labels.Count == 0 ? "None" : string.Join(", ", labels), "Open Groups", () => context.OpenMenu("Window/Asset Management/Addressables/Groups")));

            var issues = ContentValidator.Check(ContentMapping.Settings(false), addressables);
            if (issues.Count > 0)
            {
                root.Add(WorkspaceUI.Heading($"Problems ({issues.Count})"));
                foreach (var issue in issues)
                {
                    var target = issue.Context;
                    root.Add(WorkspaceUI.Row(issue.Severity.ToString(), issue.Message, target != null ? "Select" : null, target != null ? () => context.Select(target) : null, issue.Severity));
                }
            }
            Build();
            return root;
        }

        private void Build()
        {
            groups.Clear();
            foreach (var group in rows.GroupBy(row => row.Group))
            {
                var visible = group.Where(row => filter.Length == 0 || row.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (visible.Count == 0)
                    continue;
                var foldout = new Foldout { text = $"{group.Key}  ({group.Count(row => row.Mapped)}/{group.Count()} mapped)", value = !collapsed.Contains(group.Key) };
                string key = group.Key;
                foldout.RegisterValueChangedCallback(change =>
                {
                    if (change.target != foldout)
                        return;
                    if (change.newValue)
                        collapsed.Remove(key);
                    else
                        collapsed.Add(key);
                });
                var header = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
                header.Add(new Button(() => Set(visible, true)) { text = "All", tooltip = "Map every shown entry of this group." });
                header.Add(new Button(() => Set(visible, false)) { text = "None", tooltip = "Unmap every shown entry of this group." });
                foldout.Add(header);
                foreach (var row in visible.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase))
                    foldout.Add(Row(row));
                groups.Add(foldout);
            }
            if (groups.childCount == 0)
                groups.Add(WorkspaceUI.Text(rows.Count == 0 ? "No Addressables entries yet. Make assets addressable in the Groups window." : "No entry matches the filter."));
            Update();
        }

        private VisualElement Row(ContentMappingRow row)
        {
            var line = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var toggle = new Toggle { value = row.Mapped, tooltip = "Map this entry to a generated ID." };
            toggle.RegisterValueChangedCallback(change =>
            {
                row.Mapped = change.newValue;
                Update();
            });
            var name = new Label(row.Name) { tooltip = row.Path };
            name.style.flexGrow = 1;
            var kind = new Label(row.Kind.ToString()) { tooltip = row.Kind == ContentKind.Prefab ? "Loads or instantiates as a GameObject or a root component." : row.Kind == ContentKind.Scene ? "Loads additively as a SceneInstance." : "Loads as its type or a base type." };
            kind.AddToClassList("tl-value--muted");
            kind.style.width = 60;
            line.Add(toggle);
            line.Add(name);
            line.Add(kind);
            line.Add(new Button(() => EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(row.Path))) { text = "Ping", tooltip = "Show the asset in the Project window." });
            return line;
        }

        private void Set(IEnumerable<ContentMappingRow> visible, bool mapped)
        {
            foreach (var row in visible)
                row.Mapped = mapped;
            Build();
        }

        private void Update()
        {
            int changes = rows.Count(row => row.Mapped != applied[row.Guid]);
            int mapped = rows.Count(row => row.Mapped);
            pending.text = $"{mapped} of {rows.Count} entries mapped" + (changes == 0 ? "." : $", {changes} change(s) not applied.");
            apply.SetEnabled(changes > 0);
            revert.SetEnabled(changes > 0);
        }
    }
}
