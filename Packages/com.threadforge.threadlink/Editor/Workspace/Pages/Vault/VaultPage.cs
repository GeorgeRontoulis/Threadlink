namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// The Vault browser: every definition asset in the project, searchable and grouped by type, with the selected one's
    /// Inspector beside the list. Create, duplicate, ping and delete definitions here.
    /// </summary>
    public sealed class VaultPage : WorkspacePage, IFillsWorkspace
    {
        /// <summary>Where new definitions go when none is selected.</summary>
        public const string DefinitionsFolder = ProjectSetup.Root + "/Design/Definitions";

        private static string filter = string.Empty;
        private static VaultAuthoring selected;

        private WorkspaceContext context;
        private List<VaultAuthoring> definitions;
        private VisualElement list;
        private VisualElement detail;
        private Label summary;

        /// <inheritdoc />
        public override string Title => "Vault";

        /// <inheritdoc />
        public override int Order => 30;

        /// <summary>The definition shown in the page.</summary>
        public static VaultAuthoring Selected => selected;

        /// <summary>Opens the Workspace on the Vault page with this definition selected.</summary>
        public static void Reveal(VaultAuthoring definition)
        {
            selected = definition;
            WorkspaceWindow.Open().Show(typeof(VaultPage));
        }

        [MenuItem("Assets/Threadlink/Open in Vault Page", priority = 31)]
        private static void RevealSelection() => Reveal((VaultAuthoring)Selection.activeObject);

        [MenuItem("Assets/Threadlink/Open in Vault Page", true)]
        private static bool CanRevealSelection() => Selection.activeObject is VaultAuthoring;

        [MenuItem("CONTEXT/VaultAuthoring/Open in Vault Page")]
        private static void RevealContext(MenuCommand command) => Reveal((VaultAuthoring)command.context);

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            this.context = context;
            definitions = VaultValidator.FindDefinitions();
            if (selected != null && !definitions.Contains(selected))
                selected = null;

            var root = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };

            var left = new VisualElement { style = { width = 280, minWidth = 200, flexShrink = 0, marginRight = 10 } };
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            var search = new TextField { value = filter, tooltip = "Show definitions whose name, type or folder contains this text." };
            search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(change =>
            {
                filter = change.newValue ?? string.Empty;
                Fill();
            });
            toolbar.Add(search);
            var create = new Button { text = "New", tooltip = "Create a definition of a type that has a creation menu entry." };
            create.clicked += () => CreateMenu().DropDown(create.worldBound);
            toolbar.Add(create);
            left.Add(toolbar);
            list = new ScrollView { style = { flexGrow = 1 } };
            left.Add(list);
            summary = new Label();
            summary.AddToClassList("tl-text");
            left.Add(summary);
            root.Add(left);

            detail = new ScrollView { style = { flexGrow = 1 } };
            root.Add(detail);

            Fill();
            Show();
            return root;
        }

        private void Fill()
        {
            list.Clear();
            int pending = 0;
            int shown = 0;
            foreach (var group in definitions.Where(Matches).GroupBy(d => ObjectNames.NicifyVariableName(d.GetType().Name)).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var header = new Label(group.Key.ToUpperInvariant());
                header.AddToClassList("tl-group");
                list.Add(header);
                foreach (var definition in group.OrderBy(d => d.name, StringComparer.OrdinalIgnoreCase))
                {
                    var target = definition;
                    bool migrate = NeedsMigration(definition);
                    pending += migrate ? 1 : 0;
                    shown++;
                    var item = new VisualElement { tooltip = AssetDatabase.GetAssetPath(definition) + (migrate ? "\nAn authoring migration is required." : string.Empty) };
                    item.AddToClassList("tl-nav");
                    item.EnableInClassList("tl-nav--selected", definition == selected);
                    item.Add(new Label(definition.name + (migrate ? "  ⚠" : string.Empty)));
                    item.RegisterCallback<ClickEvent>(_ =>
                    {
                        selected = target;
                        foreach (var other in list.Query(className: "tl-nav").ToList())
                            other.RemoveFromClassList("tl-nav--selected");
                        item.AddToClassList("tl-nav--selected");
                        Show();
                    });
                    item.AddManipulator(new ContextualMenuManipulator(menu =>
                    {
                        menu.menu.AppendAction("Ping", _ => context.Select(target));
                        menu.menu.AppendAction("Duplicate", _ => Duplicate(target));
                        menu.menu.AppendAction("Delete…", _ => Delete(target));
                    }));
                    list.Add(item);
                }
            }
            if (shown == 0)
                list.Add(WorkspaceUI.Text(definitions.Count == 0 ? "No definitions yet. Select New to create one." : "No definition matches the filter."));
            summary.text = $"{definitions.Count} definition(s)" + (pending > 0 ? $", {pending} need an authoring migration." : ".");
        }

        private static bool Matches(VaultAuthoring definition) =>
            filter.Length == 0
            || definition.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
            || definition.GetType().Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
            || AssetDatabase.GetAssetPath(definition).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool NeedsMigration(VaultAuthoring definition)
        {
            try
            {
                return definition.AuthoredVersion != definition.SchemaVersion;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Show()
        {
            detail.Clear();
            if (selected == null)
            {
                detail.Add(WorkspaceUI.Text("Select a definition to edit it here. Edits are saved to the asset, also in Play mode."));
                return;
            }
            var target = selected;
            detail.Add(WorkspaceUI.Heading(target.name));
            detail.Add(WorkspaceUI.Row(ObjectNames.NicifyVariableName(target.GetType().Name), AssetDatabase.GetAssetPath(target), "Ping", () => context.Select(target)));
            if (EditorApplication.isPlaying)
                detail.Add(WorkspaceUI.Row("Play mode", "Edits change the asset and stay after Play mode ends. The session keeps the value it already prepared.", severity: IssueSeverity.Warning));
            var inspector = new InspectorElement(target);
            inspector.style.marginTop = 6;
            detail.Add(inspector);
        }

        private GenericMenu CreateMenu()
        {
            var menu = new GenericMenu();
            var types = CreatableTypes();
            if (types.Count == 0)
                menu.AddDisabledItem(new GUIContent("No definition type has a creation menu entry"));
            foreach (var (type, path) in types)
            {
                var chosen = type;
                menu.AddItem(new GUIContent(path), false, () => Create(chosen));
            }
            return menu;
        }

        /// <summary>Definition types designers can create: concrete types with a CreateAssetMenu entry, by menu path.</summary>
        internal static List<(Type type, string path)> CreatableTypes() =>
            TypeCache
                .GetTypesWithAttribute<CreateAssetMenuAttribute>()
                .Where(type => typeof(VaultAuthoring).IsAssignableFrom(type) && !type.IsAbstract && !type.IsGenericTypeDefinition)
                .Select(type =>
                {
                    var attribute = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(type, typeof(CreateAssetMenuAttribute));
                    return (type, string.IsNullOrEmpty(attribute.menuName) ? ObjectNames.NicifyVariableName(type.Name) : attribute.menuName);
                })
                .OrderBy(entry => entry.Item2, StringComparer.Ordinal)
                .ToList();

        /// <summary>Creates a definition beside the selected one, or in <see cref="DefinitionsFolder"/>, and selects it.</summary>
        internal static VaultAuthoring Create(Type type, string folder = null)
        {
            folder ??= selected != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(selected))?.Replace('\\', '/') : DefinitionsFolder;
            AssetLocations.EnsureFolder(folder);
            var definition = (VaultAuthoring)ScriptableObject.CreateInstance(type);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/New " + ObjectNames.NicifyVariableName(type.Name) + ".asset");
            AssetDatabase.CreateAsset(definition, path);
            AssignIdentity(definition);
            selected = definition;
            WorkspaceWindow.Refresh();
            return definition;
        }

        /// <summary>Copies a definition with a new identity, so the copy is a separate definition.</summary>
        internal static VaultAuthoring Duplicate(VaultAuthoring source)
        {
            string from = AssetDatabase.GetAssetPath(source);
            string to = AssetDatabase.GenerateUniqueAssetPath(from);
            if (!AssetDatabase.CopyAsset(from, to))
                return null;
            var copy = AssetDatabase.LoadAssetAtPath<VaultAuthoring>(to);
            var data = new SerializedObject(copy);
            var identity = data.FindProperty("identity");
            if (identity != null)
            {
                identity.stringValue = Guid.NewGuid().ToString("D");
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(copy);
            }
            selected = copy;
            WorkspaceWindow.Refresh();
            return copy;
        }

        private static void Delete(VaultAuthoring definition)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            if (!EditorUtility.DisplayDialog("Delete definition", $"Move {path} to the trash? Anything that references it loses the reference.", "Delete", "Cancel"))
                return;
            if (selected == definition)
                selected = null;
            AssetDatabase.MoveAssetToTrash(path);
            WorkspaceWindow.Refresh();
        }

        private static void AssignIdentity(VaultAuthoring definition)
        {
            var data = new SerializedObject(definition);
            var identity = data.FindProperty("identity");
            if (identity == null || Guid.TryParse(identity.stringValue, out var known) && known != Guid.Empty)
                return;
            identity.stringValue = Guid.NewGuid().ToString("D");
            var version = data.FindProperty("authoredVersion");
            if (version != null)
                version.intValue = definition.SchemaVersion;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
        }
    }

    /// <summary>Runs definition preparation, detects duplicate identities and reports pending migrations.</summary>
    public sealed class VaultValidator : IWorkspaceValidator
    {
        /// <inheritdoc />
        public string Name => "Vault";

        internal static List<VaultAuthoring> FindDefinitions() =>
            AssetDatabase
                .FindAssets("t:" + nameof(VaultAuthoring))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<VaultAuthoring>()
                .ToList();

        /// <inheritdoc />
        public void Validate(List<ValidationIssue> issues)
        {
            var identities = new Dictionary<Guid, VaultAuthoring>();
            var fields = new HashSet<string>(VaultFieldNames.Known(), StringComparer.Ordinal);
            foreach (var definition in FindDefinitions())
            {
                try
                {
                    var identity = definition.Identity;
                    if (identities.TryGetValue(identity, out var other))
                        issues.Add(new ValidationIssue(Name, IssueSeverity.Error, $"{definition.name} shares identity {identity} with {other.name}.", definition));
                    else
                        identities.Add(identity, definition);
                    if (definition is VaultFieldSetAsset set)
                        foreach (var entry in set.Fields)
                            if (!string.IsNullOrEmpty(entry.Field.Name) && !fields.Contains(entry.Field.Name))
                                issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, $"{definition.name} sets {entry.Field.Name}, which no ID domain extending Vault fields defines; code cannot read it through VaultFields.", definition));
                    if (definition.AuthoredVersion != definition.SchemaVersion)
                    {
                        issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, $"{definition.name} is authored v{definition.AuthoredVersion}; schema is v{definition.SchemaVersion}. Apply the explicit migration.", definition));
                        continue;
                    }
                    definition.ValidateDefinition();
                }
                catch (Exception error)
                {
                    issues.Add(new ValidationIssue(Name, IssueSeverity.Error, $"{definition.name}: {error.Message}", definition));
                }
            }
        }
    }
}
