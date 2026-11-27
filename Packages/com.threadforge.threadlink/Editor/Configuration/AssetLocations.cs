namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>Which default folder an asset kind is created in.</summary>
    public enum ConfigRoot
    {
        /// <summary>Engineering configuration: settings, pipelines, input and localization settings.</summary>
        Engineering,

        /// <summary>Designer-owned configuration: locales and string tables.</summary>
        Design,
    }

    /// <summary>
    /// A kind of asset the Workspace creates, such as Threadlink's Dextra settings or Unity's Localization settings. New
    /// assets of the kind are created in <see cref="Folder"/>.
    /// </summary>
    public sealed class AssetLocation
    {
        /// <summary>Creates a location.</summary>
        /// <param name="id">Stable identifier, stored with the project's folder choices.</param>
        /// <param name="title">Display name.</param>
        /// <param name="category">Workspace category, such as "Input" or "Rendering".</param>
        /// <param name="root">The default folder used until the project chooses another.</param>
        public AssetLocation(string id, string title, string category, ConfigRoot root)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("An asset location needs an identifier.", nameof(id));
            Id = id;
            Title = title ?? id;
            Category = category ?? "Other";
            Root = root;
        }

        /// <summary>Stable identifier.</summary>
        public string Id { get; }

        /// <summary>Display name.</summary>
        public string Title { get; }

        /// <summary>Workspace category.</summary>
        public string Category { get; }

        /// <summary>The default folder used until the project chooses another.</summary>
        public ConfigRoot Root { get; }

        /// <summary>The folder new assets of this kind are created in.</summary>
        public string Folder => AssetLocations.GetFolder(this);

        /// <summary>Whether the project chose a folder for this kind rather than its root.</summary>
        public bool IsCustom => AssetLocations.IsCustom(this);
    }

    /// <summary>
    /// The folders new configuration assets are created in. The project's choices are stored in
    /// <c>ProjectSettings/ThreadlinkAssetLocations.asset</c>. Changing a folder affects assets created afterwards; the
    /// Workspace moves existing ones on request.
    /// </summary>
    public static class AssetLocations
    {
        /// <summary>The default engineering configuration folder.</summary>
        public const string DefaultEngineeringFolder =
            "Assets/Threadforge/Threadlink User/Engineering/Configs";

        /// <summary>The default designer configuration folder.</summary>
        public const string DefaultDesignFolder =
            "Assets/Threadforge/Threadlink User/Design/Configs";

        /// <summary>Raised after any folder choice changes.</summary>
        public static event Action Changed;

        internal static void RaiseChanged() => Changed?.Invoke();

        /// <summary>The engineering root folder.</summary>
        public static string EngineeringFolder
        {
            get => AssetLocationStore.instance.Engineering;
            set => AssetLocationStore.instance.SetRoot(ConfigRoot.Engineering, value);
        }

        /// <summary>The designer root folder.</summary>
        public static string DesignFolder
        {
            get => AssetLocationStore.instance.Design;
            set => AssetLocationStore.instance.SetRoot(ConfigRoot.Design, value);
        }

        /// <summary>The folder of a root.</summary>
        public static string GetRootFolder(ConfigRoot root) =>
            root == ConfigRoot.Design ? DesignFolder : EngineeringFolder;

        /// <summary>Sets the folder of a root; null or empty restores its default.</summary>
        public static void SetRootFolder(ConfigRoot root, string folder) =>
            AssetLocationStore.instance.SetRoot(root, folder);

        /// <summary>The folder new assets of <paramref name="location"/> are created in.</summary>
        public static string GetFolder(AssetLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));
            return AssetLocationStore.instance.Override(location.Id)
                ?? GetRootFolder(location.Root);
        }

        /// <summary>Whether <paramref name="location"/> has its own folder.</summary>
        public static bool IsCustom(AssetLocation location) =>
            location != null && AssetLocationStore.instance.Override(location.Id) != null;

        /// <summary>Gives <paramref name="location"/> its own folder; null or empty returns it to its root.</summary>
        public static void SetFolder(AssetLocation location, string folder)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));
            AssetLocationStore.instance.SetOverride(
                location.Id,
                string.IsNullOrWhiteSpace(folder) ? null : Normalize(folder)
            );
        }

        /// <summary>Returns <paramref name="location"/> to its root folder.</summary>
        public static void ResetFolder(AssetLocation location) => SetFolder(location, null);

        /// <summary>
        /// A project-relative folder path under Assets with forward slashes. Accepts absolute paths inside the
        /// project's Assets folder.
        /// </summary>
        /// <exception cref="ArgumentException">The folder is not inside Assets.</exception>
        public static string Normalize(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("A folder is required.", nameof(folder));
            string path = folder.Replace('\\', '/').Trim().TrimEnd('/');
            string project =
                Path.GetFullPath(Directory.GetCurrentDirectory()).Replace('\\', '/').TrimEnd('/')
                + "/";
            if (Path.IsPathRooted(path))
            {
                string full = Path.GetFullPath(path).Replace('\\', '/');
                if (!full.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        $"'{folder}' is outside this project.",
                        nameof(folder)
                    );
                path = full.Substring(project.Length);
            }
            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"'{folder}' is not inside the Assets folder.",
                    nameof(folder)
                );
            if (path.Contains("/../") || path.EndsWith("/..", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"'{folder}' is not a plain folder path.",
                    nameof(folder)
                );
            return path;
        }

        /// <summary>Creates <paramref name="folder"/> and its parents in the asset database if needed, and returns it.</summary>
        public static string EnsureFolder(string folder)
        {
            string path = Normalize(folder);
            if (AssetDatabase.IsValidFolder(path))
                return path;
            string parent = "Assets";
            foreach (
                string part in path.Substring("Assets".Length)
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            )
            {
                string next = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, part);
                parent = next;
            }
            return path;
        }

        /// <summary>A free asset path for <paramref name="fileName"/> in <paramref name="location"/>'s folder, which is created if needed.</summary>
        public static string NewAssetPath(AssetLocation location, string fileName) =>
            AssetDatabase.GenerateUniqueAssetPath(
                EnsureFolder(GetFolder(location)) + "/" + fileName
            );

        /// <summary>Whether <paramref name="assetPath"/> is directly inside <paramref name="folder"/>.</summary>
        public static bool IsIn(string assetPath, string folder) =>
            !string.IsNullOrEmpty(assetPath)
            && string.Equals(
                Path.GetDirectoryName(assetPath)?.Replace('\\', '/'),
                folder?.TrimEnd('/'),
                StringComparison.Ordinal
            );
    }
}
