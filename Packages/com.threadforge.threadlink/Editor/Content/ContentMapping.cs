namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;
    using UnityEditor.AddressableAssets.Settings.GroupSchemas;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.UIElements;

    /// <summary>One Addressables entry on the Content page: whether it is mapped to a generated ID, and its load strategy.</summary>
    public sealed class ContentMappingRow
    {
        /// <summary>The Addressables group's name.</summary>
        public string Group { get; internal set; }

        /// <summary>The entry's asset GUID.</summary>
        public string Guid { get; internal set; }

        /// <summary>The asset's path.</summary>
        public string Path { get; internal set; }

        /// <summary>The asset's file name, which names the generated ID.</summary>
        public string Name { get; internal set; }

        /// <summary>How the entry loads: asset, prefab or scene.</summary>
        public ContentKind Kind { get; internal set; }

        /// <summary>Whether the entry is mapped. Changing it takes effect on <see cref="ContentMapping.Apply"/>.</summary>
        public bool Mapped { get; set; }
    }

    /// <summary>
    /// The Content mapping, as the Content page edits it and as code for tools: which Addressables entries are mapped to
    /// generated IDs. Applying writes the three read-only dictionaries in <see cref="ContentSettings"/> and regenerates
    /// <c>ContentAssets</c>, <c>ContentPrefabs</c> and <c>ContentScenes</c>.
    /// </summary>
    public static class ContentMapping
    {
        /// <summary>Every entry of the project's editable Addressables groups, by group, with its current mapping.</summary>
        public static List<ContentMappingRow> Load() => Load(AddressableAssetSettingsDefaultObject.Settings, Settings(false));

        internal static List<ContentMappingRow> Load(AddressableAssetSettings addressables, ContentSettings settings)
        {
            var rows = new List<ContentMappingRow>();
            if (addressables == null)
                return rows;
            var mapped = MappedGuids(settings);
            foreach (var group in addressables.groups)
            {
                if (group == null || group.ReadOnly)
                    continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.AssetPath) || AssetDatabase.IsValidFolder(entry.AssetPath))
                        continue;
                    rows.Add(new ContentMappingRow
                    {
                        Group = group.Name,
                        Guid = entry.guid,
                        Path = entry.AssetPath,
                        Name = System.IO.Path.GetFileNameWithoutExtension(entry.AssetPath),
                        Kind = KindOf(entry.AssetPath),
                        Mapped = mapped.Contains(entry.guid),
                    });
                }
            }
            return rows;
        }

        /// <summary>The load strategy of an asset: scenes and prefabs have their own; everything else is an asset.</summary>
        public static ContentKind KindOf(string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type == typeof(SceneAsset) ? ContentKind.Scene : type == typeof(GameObject) ? ContentKind.Prefab : ContentKind.Asset;
        }

        /// <summary>Writes the mapping of <paramref name="rows"/> and regenerates the IDs. A mapped entry keeps its value.</summary>
        public static bool Apply(IEnumerable<ContentMappingRow> rows)
        {
            var settings = Settings(true);
            Apply(rows, settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            ThreadlinkSettingsAssets.Preload();
            GeneratedIds.Run(new ContentIdGenerator());
            return true;
        }

        internal static void Apply(IEnumerable<ContentMappingRow> rows, ContentSettings settings)
        {
            var previous = new Dictionary<(ContentKind, string), int>();
            foreach (var pair in settings.Assets)
                previous[(ContentKind.Asset, pair.Value.AssetGUID)] = pair.Key.Value;
            foreach (var pair in settings.Prefabs)
                previous[(ContentKind.Prefab, pair.Value.AssetGUID)] = pair.Key.Value;
            foreach (var pair in settings.Scenes)
                previous[(ContentKind.Scene, pair.Value.AssetGUID)] = pair.Key.Value;
            var taken = new Dictionary<ContentKind, HashSet<int>>();
            foreach (ContentKind kind in Enum.GetValues(typeof(ContentKind)))
            {
                taken[kind] = new HashSet<int>(previous.Where(pair => pair.Key.Item1 == kind).Select(pair => pair.Value));
                foreach (string key in GeneratedIds.RecordedKeys(ClassOf(kind)))
                    if (int.TryParse(key, out int recorded))
                        taken[kind].Add(recorded);
            }
            var assets = new Dictionary<ContentAssetId, AssetReference>();
            var prefabs = new Dictionary<ContentPrefabId, AssetReferenceGameObject>();
            var scenes = new Dictionary<ContentSceneId, AssetReference>();
            var types = new Dictionary<ContentAssetId, string>();
            foreach (var row in rows.Where(row => row.Mapped).OrderBy(row => row.Path, StringComparer.Ordinal))
            {
                if (!previous.TryGetValue((row.Kind, row.Guid), out int value))
                {
                    // A new entry: a value from its GUID, never one in use or recorded as removed.
                    value = IdDomain.Hash(row.Guid);
                    while (value == 0 || taken[row.Kind].Contains(value))
                        value = unchecked(value + 1) & 0x7FFFFFFF;
                    taken[row.Kind].Add(value);
                }
                switch (row.Kind)
                {
                    case ContentKind.Scene:
                        scenes[new ContentSceneId(value)] = new AssetReference(row.Guid);
                        break;
                    case ContentKind.Prefab:
                        prefabs[new ContentPrefabId(value)] = new AssetReferenceGameObject(row.Guid);
                        break;
                    default:
                        assets[new ContentAssetId(value)] = new AssetReference(row.Guid);
                        var type = AssetDatabase.GetMainAssetTypeAtPath(row.Path);
                        if (type != null)
                            types[new ContentAssetId(value)] = type.FullName + ", " + type.Assembly.GetName().Name;
                        break;
                }
            }
            Undo.RecordObject(settings, "Apply Content Mapping");
            settings.ReplaceMapping(assets, prefabs, scenes, types);
        }

        /// <summary>Maps the Addressables entries at <paramref name="paths"/>, keeping every existing mapping. False if a path is not an entry of an editable group.</summary>
        public static bool Map(IEnumerable<string> paths) => Change(paths, true);

        /// <summary>Unmaps the entries at <paramref name="paths"/>; their IDs stay as obsolete tombstones.</summary>
        public static bool Unmap(IEnumerable<string> paths) => Change(paths, false);

        /// <summary>Whether the Addressables entry at <paramref name="path"/> is mapped.</summary>
        public static bool IsMapped(string path) => MappedGuids(Settings(false)).Contains(AssetDatabase.AssetPathToGUID(path));

        /// <summary>The paths that are not mapped, in input order.</summary>
        public static List<string> FindUnmapped(IEnumerable<string> paths)
        {
            var mapped = MappedGuids(Settings(false));
            return paths.Where(path => !mapped.Contains(AssetDatabase.AssetPathToGUID(path))).ToList();
        }

        private static bool Change(IEnumerable<string> paths, bool mapped)
        {
            var rows = Load();
            var byPath = rows.ToDictionary(row => row.Path, StringComparer.Ordinal);
            bool changed = false;
            foreach (string path in paths)
            {
                if (!byPath.TryGetValue(path, out var row))
                {
                    Debug.LogError("Threadlink: " + path + " is not an entry of an editable Addressables group, so its mapping did not change.");
                    return false;
                }
                changed |= row.Mapped != mapped;
                row.Mapped = mapped;
            }
            return !changed || Apply(rows);
        }

        internal static ContentSettings Settings(bool create) => (ContentSettings)ThreadlinkSettingsAssets.Get(typeof(ContentSettings), create);

        internal static string ClassOf(ContentKind kind) => kind == ContentKind.Scene ? "ContentScenes" : kind == ContentKind.Prefab ? "ContentPrefabs" : "ContentAssets";

        private static HashSet<string> MappedGuids(ContentSettings settings)
        {
            var guids = new HashSet<string>(StringComparer.Ordinal);
            if (settings == null)
                return guids;
            foreach (var reference in settings.Assets.Values)
                guids.Add(reference.AssetGUID);
            foreach (var reference in settings.Prefabs.Values)
                guids.Add(reference.AssetGUID);
            foreach (var reference in settings.Scenes.Values)
                guids.Add(reference.AssetGUID);
            return guids;
        }

        /// <summary>The generated names of every mapped entry of a kind, as "Group.Name", by value.</summary>
        internal static Dictionary<int, (string group, string name, string guid)> Names(ContentKind kind) =>
            Names(kind, Settings(false), AddressableAssetSettingsDefaultObject.Settings);

        internal static Dictionary<int, (string group, string name, string guid)> Names(ContentKind kind, ContentSettings settings, AddressableAssetSettings addressables)
        {
            var result = new Dictionary<int, (string, string, string)>();
            if (settings == null)
                return result;
            IEnumerable<(int value, string guid)> mapped = kind == ContentKind.Scene ? settings.Scenes.Select(pair => (pair.Key.Value, pair.Value.AssetGUID))
                : kind == ContentKind.Prefab ? settings.Prefabs.Select(pair => (pair.Key.Value, pair.Value.AssetGUID))
                : settings.Assets.Select(pair => (pair.Key.Value, pair.Value.AssetGUID));
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (value, guid) in mapped.OrderBy(item => AssetDatabase.GUIDToAssetPath(item.guid), StringComparer.Ordinal))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var entry = addressables != null ? addressables.FindAssetEntry(guid) : null;
                if (string.IsNullOrEmpty(path) || entry == null || entry.parentGroup == null)
                    continue;
                string group = GeneratedIds.Identifier(entry.parentGroup.Name);
                string name = GeneratedIds.Identifier(System.IO.Path.GetFileNameWithoutExtension(path));
                // Two assets of one group and kind with the same name: later ones get a numeric suffix.
                string unique = name;
                for (int suffix = 2; !used.Add(group + "." + unique); suffix++)
                    unique = name + suffix;
                result[value] = (group, unique, guid);
            }
            return result;
        }
    }

    /// <summary>Generates <c>ContentAssets</c>, <c>ContentPrefabs</c> and <c>ContentScenes</c> from the Content mapping, nested by Addressables group.</summary>
    public sealed class ContentIdGenerator : IIdGenerator
    {
        /// <inheritdoc />
        public string Title => "Content";

        /// <inheritdoc />
        public string Source => "Addressables entries mapped on the Workspace's Content page";

        /// <inheritdoc />
        public IEnumerable<string> Outputs => new[] { "ContentAssets", "ContentPrefabs", "ContentScenes" };

        /// <inheritdoc />
        public void OpenSource() => WorkspaceWindow.Open().Show(typeof(ContentPage));

        /// <inheritdoc />
        public bool Generate()
        {
            if (ContentMapping.Settings(false) == null)
                return false;
            bool changed = false;
            foreach (var (kind, type, summary) in new[]
            {
                (ContentKind.Asset, "ContentAssetId", "Mapped Addressables assets. Load with Content.LoadAsync<T>(id)."),
                (ContentKind.Prefab, "ContentPrefabId", "Mapped Addressables prefabs. Load or instantiate with Content.LoadAsync / InstantiateAsync, as a GameObject or a root component."),
                (ContentKind.Scene, "ContentSceneId", "Mapped Addressables scenes. Transition with Nexus.LoadAsync(id) or load with Content.LoadSceneAsync(id)."),
            })
            {
                string className = ContentMapping.ClassOf(kind);
                var members = ContentMapping
                    .Names(kind)
                    .Select(pair => new GeneratedIds.Member
                    {
                        Group = pair.Value.group,
                        Name = pair.Value.name,
                        Key = pair.Key.ToString(),
                        Type = "global::Threadforge.Threadlink." + type,
                        Value = "new global::Threadforge.Threadlink." + type + "(" + pair.Key + ")",
                        Summary = AssetDatabase.GUIDToAssetPath(pair.Value.guid) + ".",
                    })
                    .ToList();
                // An ID whose entry is no longer addressable stays, obsolete, through the ledger.
                if (members.Count > 0 || File.Exists(GeneratedIds.PathOf(className)))
                    changed |= GeneratedIds.Write(className, summary, members);
            }
            return changed;
        }
    }

    /// <summary>Generates <c>ContentLabels</c> from the labels in the Addressables settings.</summary>
    public sealed class ContentLabelGenerator : IIdGenerator
    {
        /// <inheritdoc />
        public string Title => "Content labels";

        /// <inheritdoc />
        public string Source => "Labels in the Addressables settings";

        /// <inheritdoc />
        public IEnumerable<string> Outputs => new[] { "ContentLabels" };

        /// <inheritdoc />
        public void OpenSource() => EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");

        /// <inheritdoc />
        public bool Generate()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return false;
            var members = settings
                .GetLabels()
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => new GeneratedIds.Member
                {
                    Name = label,
                    Key = label,
                    Type = "global::Threadforge.Threadlink.ContentLabel",
                    Value = "new global::Threadforge.Threadlink.ContentLabel(" + GeneratedIds.Quote(label) + ")",
                    Summary = "Addressables label " + label + ".",
                })
                .ToList();
            return GeneratedIds.Write("ContentLabels", "Addressables labels, for Content.LoadAllAsync<T>(label).", members);
        }
    }

    /// <summary>Checks the Content mapping against Addressables. Read-only.</summary>
    public sealed class ContentValidator : IWorkspaceValidator
    {
        /// <inheritdoc />
        public string Name => "Content";

        /// <inheritdoc />
        public void Validate(List<ValidationIssue> issues) => issues.AddRange(Check(ContentMapping.Settings(false), AddressableAssetSettingsDefaultObject.Settings));

        /// <summary>The problems of a mapping against <paramref name="addressables"/>.</summary>
        public static List<ValidationIssue> Check(ContentSettings settings, AddressableAssetSettings addressables)
        {
            var issues = new List<ValidationIssue>();
            if (settings == null)
                return issues;
            var mapped = settings.Assets.Values.Select(reference => (reference.AssetGUID, ContentKind.Asset))
                .Concat(settings.Prefabs.Values.Select(reference => (reference.AssetGUID, ContentKind.Prefab)))
                .Concat(settings.Scenes.Values.Select(reference => (reference.AssetGUID, ContentKind.Scene)))
                .ToList();
            int untyped = settings.Assets.Keys.Count(id => !settings.AssetTypes.ContainsKey(id));
            if (untyped > 0)
                issues.Add(new ValidationIssue("Content", IssueSeverity.Info, $"{untyped} mapped asset(s) were mapped before asset types were recorded; Content.AssetsOf<T>() omits them until the mapping is applied again.", settings));
            if (mapped.Count > 0 && addressables == null)
            {
                issues.Add(new ValidationIssue("Content", IssueSeverity.Error, "Addressables is not set up, so mapped content cannot load.", settings));
                return issues;
            }
            foreach (var (guid, kind) in mapped)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var entry = addressables.FindAssetEntry(guid);
                if (string.IsNullOrEmpty(path))
                    issues.Add(new ValidationIssue("Content", IssueSeverity.Error, $"A mapped asset ({guid}) was deleted. Apply the mapping to remove it.", settings));
                else if (entry == null)
                    issues.Add(new ValidationIssue("Content", IssueSeverity.Error, $"{path} is mapped but no longer addressable. Apply the mapping to remove it.", AssetDatabase.LoadMainAssetAtPath(path)));
                else if (ContentMapping.KindOf(path) != kind)
                    issues.Add(new ValidationIssue("Content", IssueSeverity.Warning, $"{path} is mapped as a {kind.ToString().ToLowerInvariant()} but is now a {ContentMapping.KindOf(path).ToString().ToLowerInvariant()}. Apply the mapping to move it.", AssetDatabase.LoadMainAssetAtPath(path)));
                else if (entry.parentGroup != null && entry.parentGroup.GetSchema<BundledAssetGroupSchema>() is { IncludeGUIDInCatalog: false })
                    issues.Add(new ValidationIssue("Content", IssueSeverity.Error, $"{path}: group {entry.parentGroup.Name} excludes GUIDs from the catalog, so it cannot load by ID. Enable Include GUIDs in Catalog.", entry.parentGroup));
            }
            return issues;
        }
    }

    /// <summary>Menus for the Content mapping, and regeneration when Addressables changes.</summary>
    internal static class ContentMappingMenus
    {
        [InitializeOnLoadMethod]
        private static void Watch()
        {
            AddressableAssetSettings.OnModificationGlobal -= Changed;
            AddressableAssetSettings.OnModificationGlobal += Changed;
        }

        private static void Changed(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent change, object data) => GeneratedIds.Request();

        [MenuItem("Threadlink/Content Mapping", priority = 21)]
        private static void Open() => WorkspaceWindow.Open().Show(typeof(ContentPage));

        [MenuItem("Assets/Threadlink/Map for Loading by ID", true)]
        private static bool CanMap() => Selection.assetGUIDs.Length > 0;

        [MenuItem("Assets/Threadlink/Map for Loading by ID", priority = 40)]
        private static void Map() => ContentMapping.Map(Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(path => !AssetDatabase.IsValidFolder(path)));
    }

    /// <summary>A dropdown of the mapped entries of one kind for content ID fields; shows the generated name.</summary>
    internal abstract class ContentIdDrawer : PropertyDrawer
    {
        protected abstract ContentKind Kind { get; }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var value = property.FindPropertyRelative("value");
            var names = ContentMapping.Names(Kind);
            var values = new List<int> { 0 };
            var choices = new List<string> { "(None)" };
            foreach (var pair in names.OrderBy(pair => pair.Value.group + "." + pair.Value.name, StringComparer.Ordinal))
            {
                values.Add(pair.Key);
                choices.Add(pair.Value.group + "/" + pair.Value.name);
            }
            if (!values.Contains(value.intValue))
            {
                values.Add(value.intValue);
                choices.Add("(Unmapped " + value.intValue + ")");
            }
            var popup = new PopupField<string>(preferredLabel, choices, values.IndexOf(value.intValue))
            {
                tooltip = string.IsNullOrEmpty(property.tooltip) ? "Mapped on the Workspace's Content page." : property.tooltip,
            };
            popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            popup.RegisterValueChangedCallback(change =>
            {
                value.intValue = values[choices.IndexOf(change.newValue)];
                property.serializedObject.ApplyModifiedProperties();
            });
            return popup;
        }
    }

    [CustomPropertyDrawer(typeof(ContentAssetId))]
    internal sealed class ContentAssetIdDrawer : ContentIdDrawer
    {
        protected override ContentKind Kind => ContentKind.Asset;
    }

    [CustomPropertyDrawer(typeof(ContentPrefabId))]
    internal sealed class ContentPrefabIdDrawer : ContentIdDrawer
    {
        protected override ContentKind Kind => ContentKind.Prefab;
    }

    [CustomPropertyDrawer(typeof(ContentSceneId))]
    internal sealed class ContentSceneIdDrawer : ContentIdDrawer
    {
        protected override ContentKind Kind => ContentKind.Scene;
    }
}
