namespace Threadlink.Editor.CodeGen
{
    using System;
    using System.Collections.Generic;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using UnityEngine;

    /// <summary>
    /// The Mapping Window (Threadlink > Addressables > Mapping Window) as code, for Editor tools: a tool that creates
    /// Addressable content maps it here, so its generated ID (<c>ThreadlinkIDs.Addressables.*</c>) exists without a manual step.
    /// </summary>
    public static class ThreadlinkAddressablesMapping
    {
        /// <summary>Reapply the current mappings without changing identifiers or clearing manifest history.</summary>
        public static bool Apply()
        {
            var mapping = new AddressableMapping();
            mapping.Reload();
            return mapping.Apply();
        }

        /// <summary>
        /// Whether the Addressable entry at <paramref name="assetPath"/> is mapped to a generated ID.
        /// </summary>
        /// <param name="assetPath">A project-relative path, for example <c>Assets/Database/Scenes/Farm/Farm.unity</c>.</param>
        public static bool IsMapped(string assetPath)
        {
            var mapping = new AddressableMapping();

            mapping.Reload();
            return mapping.TryFindRow(assetPath, out var row) && row.Mapped;
        }

        /// <summary>
        /// Return the paths that are not mapped, in input order. Like <see cref="IsMapped"/>, a missing entry or an entry
        /// in a read-only group is unmapped. Reloads the groups once and indexes their mapped paths, so querying many
        /// assets costs O(entries + paths) instead of reloading and scanning the groups for every path.
        /// </summary>
        /// <param name="assetPaths">Project-relative asset paths. Duplicate paths are preserved.</param>
        public static List<string> FindUnmapped(IEnumerable<string> assetPaths)
        {
            var mapping = new AddressableMapping();
            mapping.Reload();

            var mappedPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in mapping.Groups)
                foreach (var row in group.Rows)
                    if (row.Mapped)
                        mappedPaths.Add(row.AssetPath);

            var unmapped = new List<string>();

            foreach (string path in assetPaths)
                if (!mappedPaths.Contains(path))
                    unmapped.Add(path);

            return unmapped;
        }

        /// <summary>
        /// Map the Addressable entries at <paramref name="assetPaths"/>, keeping every existing mapping: exactly what ticking
        /// their rows in the Mapping Window and pressing Apply does. Applying regenerates the Addressables domains and
        /// recompiles scripts, so code that needs the new IDs runs after the domain reload.
        /// <para/>
        /// Nothing is applied if a path is not an entry of an editable Addressable group, or if every entry is mapped already.
        /// </summary>
        /// <param name="assetPaths">Project-relative paths, for example <c>Assets/Database/Scenes/Farm/Farm.unity</c>.</param>
        /// <returns>Whether every entry is mapped afterwards.</returns>
        public static bool Map(params string[] assetPaths) => Map((IEnumerable<string>)assetPaths);

        /// <inheritdoc cref="Map(string[])"/>
        public static bool Map(IEnumerable<string> assetPaths)
        {
            var mapping = new AddressableMapping();
            bool changed = false;

            mapping.Reload();

            foreach (var assetPath in assetPaths)
            {
                if (mapping.TryFindRow(assetPath, out var row) is false)
                {
                    Scribe.Send<AddressableMapping>("'", assetPath, "' is not an entry of an editable Addressable group, so it ",
                    "cannot be mapped. Nothing was applied.").ToUnityConsole(DebugType.Error);

                    return false;
                }

                changed |= row.Mapped is false;
                row.Mapped = true;
            }

            if (changed is false)
                return true;

            bool applied = mapping.Apply();

            // An open Mapping Window shows the new rows ticked.
            foreach (var window in Resources.FindObjectsOfTypeAll<AddressableMappingWindow>())
                window.Reload();

            return applied;
        }
    }
}
