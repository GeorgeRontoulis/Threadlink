namespace Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;

    /// <summary>
    /// Enumerates every scene that will actually ship in the build, from both
    /// sources Unity treats completely differently at build time:
    /// <list type="bullet">
    ///   <item><b>Build Settings</b> — only <i>enabled</i> entries in
    ///   <see cref="EditorBuildSettings.scenes"/> ship. Disabled entries are
    ///   excluded, matching what "Scenes in Build" actually means.</item>
    ///   <item><b>Addressables</b> — scenes added as entries to any
    ///   <see cref="AddressableAssetGroup"/>. These are never part of
    ///   "Scenes in Build" — they're loaded at runtime via
    ///   <c>Addressables.LoadSceneAsync</c> and ship as separately built
    ///   content bundles, assuming an Addressables content build has been
    ///   run. Being listed here reflects "is currently marked addressable,"
    ///   not "has definitely been bundled."</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Requires the Addressables package ("com.unity.addressables") to be
    /// installed — this references its Editor API directly rather than via
    /// reflection, consistent with how other package dependencies are
    /// handled elsewhere in this project. If Addressables isn't installed,
    /// remove the <see cref="CollectAddressableScenes"/> call and its using
    /// directive.
    /// </remarks>
    internal static class BuildSceneCollector
    {
        internal readonly struct Entry
        {
            public readonly string Path;
            public readonly string DisplayName;
            public readonly bool IsAddressable;

            public Entry(string path, string displayName, bool isAddressable)
            {
                Path = path;
                DisplayName = displayName;
                IsAddressable = isAddressable;
            }
        }

        /// <summary>
        /// Returns every scene that will make it into the build, deduplicated
        /// by path and sorted alphabetically by display name.
        /// </summary>
        internal static List<Entry> CollectValidBuildScenes()
        {
            var result = new List<Entry>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;               // disabled entries don't ship
                if (string.IsNullOrEmpty(scene.path)) continue; // deleted/missing reference
                if (!seenPaths.Add(scene.path)) continue;

                result.Add(new(scene.path, Path.GetFileNameWithoutExtension(scene.path), false));
            }

            foreach (var entry in CollectAddressableScenes())
            {
                if (!seenPaths.Add(entry.Path)) continue; // skip if already listed via Build Settings
                result.Add(entry);
            }

            result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        /// <remarks>
        /// KNOWN LIMITATION: this checks each entry's own AssetPath directly.
        /// If a scene is only addressable indirectly — via an entire folder
        /// added as a single addressable entry — it won't be found here,
        /// since the folder entry's AssetPath is the folder, not the scene
        /// file. Expand folder entries via
        /// <c>AddressableAssetEntry.GatherAllAssets</c> if that pattern is
        /// in use for scenes specifically.
        /// </remarks>
        private static IEnumerable<Entry> CollectAddressableScenes()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) yield break; // package installed but not configured yet

            foreach (var group in settings.groups)
            {
                if (group == null) continue;

                foreach (var entry in group.entries)
                {
                    if (entry?.AssetPath == null) continue;
                    if (!entry.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;

                    yield return new(entry.AssetPath, Path.GetFileNameWithoutExtension(entry.AssetPath), true);
                }
            }
        }
    }
}
