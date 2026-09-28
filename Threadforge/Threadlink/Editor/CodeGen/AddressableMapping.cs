namespace Threadlink.Editor.CodeGen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using Threadlink.Shared;
    using Threadlink.Utilities.Strings;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using Threadlink.Generated;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    internal enum AddressableReferenceKind : byte
    {
        Asset,
        Prefab,
        Scene
    }

    internal sealed class AddressableMappingRow
    {
        internal string AssetPath { get; set; }
        internal string Guid { get; set; }
        internal string EntryName { get; set; }
        internal AddressableReferenceKind Kind { get; set; }
        internal bool Mapped { get; set; }
    }

    internal sealed class AddressableMappingGroup
    {
        internal string DisplayName { get; set; }
        internal string Scope { get; set; }
        internal List<AddressableMappingRow> Rows { get; } = new(8);
    }

    /// <summary>
    /// The Addressables mapping: which entries of the project's Addressable groups are mapped to generated IDs, and
    /// applying a change. The Mapping Window edits one; <see cref="ThreadlinkAddressablesMapping"/> drives one for tools.
    /// </summary>
    internal sealed class AddressableMapping
    {
        private const string SCENES_DOMAIN = ThreadlinkDomainRegistry.SCENES_DOMAIN;
        private const string ASSETS_DOMAIN = ThreadlinkDomainRegistry.ASSETS_DOMAIN;
        private const string PREFABS_DOMAIN = ThreadlinkDomainRegistry.PREFABS_DOMAIN;

        internal List<AddressableMappingGroup> Groups { get; } = new(4);

        private readonly List<ThreadlinkDomainDescriptor> descriptors = new(12);

        /// <summary>
        /// The row of the Addressable entry at <paramref name="assetPath"/>, if it is an entry of an editable group.
        /// </summary>
        internal bool TryFindRow(string assetPath, out AddressableMappingRow row)
        {
            int groupCount = Groups.Count;

            for (int g = 0; g < groupCount; g++)
            {
                var rows = Groups[g].Rows;
                int count = rows.Count;

                for (int i = 0; i < count; i++)
                {
                    if (string.Equals(rows[i].AssetPath, assetPath, StringComparison.Ordinal))
                    {
                        row = rows[i];
                        return true;
                    }
                }
            }

            row = null;
            return false;
        }

        internal void Reload()
        {
            Groups.Clear();

            var settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
                return;

            var mappedKeys = LoadMappedKeys();
            var claimedScopes = new Dictionary<string, string>(4, StringComparer.Ordinal);

            var settingsGroups = settings.groups;
            int groupCount = settingsGroups.Count;

            for (int i = 0; i < groupCount; i++)
            {
                var settingsGroup = settingsGroups[i];

                if (settingsGroup == null || settingsGroup.ReadOnly)
                    continue;

                var scope = EnumCodeGen.SanitizeEnumName(settingsGroup.Name);

                if (claimedScopes.TryGetValue(scope, out var owner))
                {
                    this.Send("Addressable groups '", owner, "' and '", settingsGroup.Name, "' both reduce to the scope '",
                    scope, "'. Entries sharing a name across them would collide. Rename one of the groups.")
                    .ToUnityConsole(DebugType.Warning);
                }
                else claimedScopes[scope] = settingsGroup.Name;

                var group = new AddressableMappingGroup
                {
                    DisplayName = settingsGroup.Name,
                    Scope = scope
                };

                foreach (var entry in settingsGroup.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.AssetPath))
                        continue;

                    if (AssetDatabase.IsValidFolder(entry.AssetPath))
                        continue;

                    var entryName = Path.GetFileNameWithoutExtension(entry.AssetPath);

                    group.Rows.Add(new AddressableMappingRow
                    {
                        AssetPath = entry.AssetPath,
                        Guid = entry.guid,
                        EntryName = entryName,
                        Kind = ResolveKind(entry.AssetPath),
                        Mapped = mappedKeys.Contains(ThreadlinkDomainSources.ComposeKey(scope, entryName))
                    });
                }

                if (group.Rows.Count > 0)
                    Groups.Add(group);
            }
        }

        /// <summary>
        /// Write the injector files for the mapped rows, regenerate the three Addressables domains, and rebuild the reference
        /// maps on the Threadlink User Config. Scripts recompile afterwards.
        /// </summary>
        internal bool Apply(bool resetManifests = false)
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig)
            || !ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkUserConfig userConfig))
            {
                this.Send("Threadlink configs not found. Apply aborted.").ToUnityConsole(DebugType.Error);
                return false;
            }

            var injectorsFolder = editorConfig.AddressablesInjectorsFolder == null
            ? string.Empty
            : AssetDatabase.GetAssetPath(editorConfig.AddressablesInjectorsFolder);

            if (ThreadlinkAssetIO.IsInsideAssets(injectorsFolder) is false
            || AssetDatabase.IsValidFolder(injectorsFolder) is false)
            {
                this.Send("The Addressables Injectors Folder is not assigned on the Threadlink Editor Config. Apply aborted.")
                .ToUnityConsole(DebugType.Error);
                return false;
            }

            if (ThreadlinkDomainRegistry.TryBuildAll(editorConfig, descriptors) is false)
            {
                this.Send("Domain descriptors could not be built. Apply aborted before regeneration.").ToUnityConsole(DebugType.Error);
                descriptors.Clear();
                return false;
            }

            // Imports must never observe the gap between removing an old group and writing its replacement.
            AssetDatabase.StartAssetEditing();
            try
            {
                WriteInjectorFiles(injectorsFolder);

                if (resetManifests)
                {
                    foreach (var descriptor in descriptors)
                    {
                        if (IsAddressableDomain(descriptor.DomainName))
                            new ThreadlinkDomainManifest().Save(descriptor);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // The descriptor's sources include the injector files that now exist.
            if (!ThreadlinkDomainRegistry.TryBuildAll(editorConfig, descriptors))
                return false;

            if (TryRegenerateAddressableDomains() is false)
            {
                descriptors.Clear();
                return false;
            }

            RebuildReferenceMaps(userConfig);

            descriptors.Clear();

            EditorUtility.SetDirty(userConfig);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            this.Send("Addressables mapping applied.").ToUnityConsole();
            return true;
        }

        private void WriteInjectorFiles(string injectorsFolder)
        {
            var expected = new HashSet<string>(StringComparer.Ordinal);

            int groupCount = Groups.Count;

            for (int g = 0; g < groupCount; g++)
            {
                var group = Groups[g];
                var rows = group.Rows;
                int count = rows.Count;

                var scenes = new List<string>(count);
                var prefabs = new List<string>(count);
                var assets = new List<string>(count);

                for (int i = 0; i < count; i++)
                {
                    var row = rows[i];

                    if (row.Mapped is false)
                        continue;

                    switch (row.Kind)
                    {
                        case AddressableReferenceKind.Scene: scenes.Add(row.EntryName); break;
                        case AddressableReferenceKind.Prefab: prefabs.Add(row.EntryName); break;
                        default: assets.Add(row.EntryName); break;
                    }
                }

                WriteInjectorFile(injectorsFolder, SCENES_DOMAIN, group.Scope, scenes, expected);
                WriteInjectorFile(injectorsFolder, PREFABS_DOMAIN, group.Scope, prefabs, expected);
                WriteInjectorFile(injectorsFolder, ASSETS_DOMAIN, group.Scope, assets, expected);
            }

            // Delete only obsolete injectors, after all replacements exist. Existing files retain their GUIDs.
            var obsolete = new List<string>();
            ThreadlinkAssetIO.CollectAssetsInFolder(injectorsFolder, $"{SCENES_DOMAIN}.*.txt", obsolete);
            ThreadlinkAssetIO.CollectAssetsInFolder(injectorsFolder, $"{PREFABS_DOMAIN}.*.txt", obsolete);
            ThreadlinkAssetIO.CollectAssetsInFolder(injectorsFolder, $"{ASSETS_DOMAIN}.*.txt", obsolete);
            obsolete.RemoveAll(expected.Contains);
            ThreadlinkAssetIO.DeleteAssets(obsolete);
        }

        private static void WriteInjectorFile(string folder, string domainName, string scope, List<string> entries, HashSet<string> expected)
        {
            if (entries.Count <= 0)
                return;

            entries.Sort(StringComparer.Ordinal);

            string path = $"{folder}/{domainName}.{scope}.txt";
            expected.Add(path);
            ThreadlinkAssetIO.WriteText(path, string.Join(Environment.NewLine, entries) + Environment.NewLine);
        }

        private bool TryRegenerateAddressableDomains()
        {
            bool success = true;
            int count = descriptors.Count;

            for (int i = 0; i < count; i++)
            {
                var descriptor = descriptors[i];

                if (IsAddressableDomain(descriptor.DomainName) is false)
                    continue;

                if (ThreadlinkDomainCodeGen.TryGenerate(descriptor, out var diff) is false)
                    success = false;

                if (diff.HasChanges())
                    diff.Report();
            }

            return success;
        }

        private void RebuildReferenceMaps(ThreadlinkUserConfig userConfig)
        {
            userConfig.EditorOnly_ClearSceneReferences();
            userConfig.EditorOnly_ClearAssetReferences();
            userConfig.EditorOnly_ClearPrefabReferences();

            var sceneValues = LoadManifestValues(SCENES_DOMAIN);
            var prefabValues = LoadManifestValues(PREFABS_DOMAIN);
            var assetValues = LoadManifestValues(ASSETS_DOMAIN);

            int groupCount = Groups.Count;

            for (int g = 0; g < groupCount; g++)
            {
                var group = Groups[g];
                var rows = group.Rows;
                int count = rows.Count;

                for (int i = 0; i < count; i++)
                {
                    var row = rows[i];

                    if (row.Mapped is false)
                        continue;

                    var key = ThreadlinkDomainSources.ComposeKey(group.Scope, row.EntryName);

                    switch (row.Kind)
                    {
                        case AddressableReferenceKind.Scene:
                            if (sceneValues.TryGetValue(key, out int sceneValue))
                                userConfig.EditorOnly_TryAddSceneReference((ThreadlinkIDs.Addressables.Scenes)sceneValue, new SceneAssetReference(row.Guid));
                            else
                                ReportMissingManifestEntry(SCENES_DOMAIN, key);
                            break;

                        case AddressableReferenceKind.Prefab:
                            if (prefabValues.TryGetValue(key, out int prefabValue))
                                userConfig.EditorOnly_TryAddPrefabReference((ThreadlinkIDs.Addressables.Prefabs)prefabValue, new AssetReferenceGameObject(row.Guid));
                            else
                                ReportMissingManifestEntry(PREFABS_DOMAIN, key);
                            break;

                        default:
                            if (assetValues.TryGetValue(key, out int assetValue))
                                userConfig.EditorOnly_TryAddAssetReference((ThreadlinkIDs.Addressables.Assets)assetValue, new AssetReference(row.Guid));
                            else
                                ReportMissingManifestEntry(ASSETS_DOMAIN, key);
                            break;
                    }
                }
            }
        }

        private Dictionary<string, int> LoadManifestValues(string domainName)
        {
            var result = new Dictionary<string, int>(16, StringComparer.Ordinal);

            int count = descriptors.Count;

            for (int i = 0; i < count; i++)
            {
                var descriptor = descriptors[i];

                if (string.Equals(descriptor.DomainName, domainName, StringComparison.Ordinal) is false)
                    continue;

                var manifest = ThreadlinkDomainManifest.LoadOrCreate(descriptor);
                int entryCount = manifest.entries.Count;

                for (int j = 0; j < entryCount; j++)
                {
                    var entry = manifest.entries[j];

                    if (entry.tombstoned is false)
                        result[entry.key] = entry.value;
                }

                break;
            }

            return result;
        }

        private HashSet<string> LoadMappedKeys()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig)
            || editorConfig.AddressablesInjectorsFolder == null)
            {
                return result;
            }

            var folder = AssetDatabase.GetAssetPath(editorConfig.AddressablesInjectorsFolder);

            if (ThreadlinkAssetIO.IsInsideAssets(folder) is false || AssetDatabase.IsValidFolder(folder) is false)
                return result;

            folder = folder.ToAbsolutePath();

            CollectMappedKeys(folder, SCENES_DOMAIN, result);
            CollectMappedKeys(folder, PREFABS_DOMAIN, result);
            CollectMappedKeys(folder, ASSETS_DOMAIN, result);

            return result;
        }

        private static void CollectMappedKeys(string folder, string domainName, HashSet<string> buffer)
        {
            var files = Directory.GetFiles(folder, $"{domainName}.*.txt", SearchOption.TopDirectoryOnly);
            int length = files.Length;

            for (int i = 0; i < length; i++)
            {
                var fileName = Path.GetFileNameWithoutExtension(files[i]);

                if (fileName.Length <= domainName.Length + 1 || fileName[domainName.Length] is not '.')
                    continue;

                var scope = fileName[(domainName.Length + 1)..];

                if (scope.IndexOf('.') >= 0)
                    continue;

                var lines = File.ReadAllLines(files[i]);
                int lineCount = lines.Length;

                for (int j = 0; j < lineCount; j++)
                {
                    var line = lines[j].Trim();

                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    buffer.Add(ThreadlinkDomainSources.ComposeKey(scope, line));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReportMissingManifestEntry(string domainName, string key)
        {
            Scribe.Send<AddressableMapping>("No manifest entry for '", key, "' in domain '", domainName,
            "'. The reference was not mapped.").ToUnityConsole(DebugType.Error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAddressableDomain(string domainName)
        {
            return string.Equals(domainName, SCENES_DOMAIN, StringComparison.Ordinal)
            || string.Equals(domainName, PREFABS_DOMAIN, StringComparison.Ordinal)
            || string.Equals(domainName, ASSETS_DOMAIN, StringComparison.Ordinal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AddressableReferenceKind ResolveKind(string assetPath)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

            if (type == typeof(SceneAsset))
                return AddressableReferenceKind.Scene;

            if (type == typeof(GameObject))
                return AddressableReferenceKind.Prefab;

            return AddressableReferenceKind.Asset;
        }

        internal int CountMapped()
        {
            int total = 0;
            int groupCount = Groups.Count;

            for (int g = 0; g < groupCount; g++)
            {
                var rows = Groups[g].Rows;
                int count = rows.Count;

                for (int i = 0; i < count; i++)
                {
                    if (rows[i].Mapped)
                        total++;
                }
            }

            return total;
        }
    }
}
