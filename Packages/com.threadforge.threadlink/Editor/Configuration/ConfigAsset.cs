namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// A configuration asset the project uses, such as an engineering settings asset, the Input System's settings or
    /// the active render pipeline. The Workspace finds, creates and moves it; new ones go to its location's folder.
    /// </summary>
    public abstract class ConfigAsset
    {
        /// <summary>Creates a configuration asset kind stored at <paramref name="location"/>.</summary>
        protected ConfigAsset(AssetLocation location) =>
            Location = location ?? throw new ArgumentNullException(nameof(location));

        /// <summary>Where new assets of this kind are created.</summary>
        public AssetLocation Location { get; }

        /// <summary>Display name.</summary>
        public string Title => Location.Title;

        /// <summary>One sentence on what the asset configures, shown in the Workspace.</summary>
        public virtual string Description => null;

        /// <summary>The asset the project uses, or null when there is none.</summary>
        public abstract Object Find();

        /// <summary>Why the Workspace cannot create one, or null when it can.</summary>
        public virtual string CreateUnavailableReason => null;

        /// <summary>A question to confirm before creating, for assets whose creation changes project behavior.</summary>
        public virtual string CreateConfirmation => null;

        /// <summary>Whether the Workspace may move the asset into its folder.</summary>
        public virtual bool CanMove => true;

        /// <summary>The asset's path, or null.</summary>
        public string AssetPath
        {
            get
            {
                var asset = Find();
                return asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            }
        }

        /// <summary>Whether the asset exists and sits outside its location's folder.</summary>
        public bool IsOutsideFolder
        {
            get
            {
                string path = AssetPath;
                return !string.IsNullOrEmpty(path)
                    && path.StartsWith("Assets/", StringComparison.Ordinal)
                    && !AssetLocations.IsIn(MovedPath(path), Location.Folder);
            }
        }

        /// <summary>
        /// Returns the existing asset, or creates one in the location's folder and makes the project use it.
        /// </summary>
        /// <exception cref="InvalidOperationException">The asset cannot be created here; see <see cref="CreateUnavailableReason"/>.</exception>
        public Object Create()
        {
            var existing = Find();
            if (existing != null)
                return existing;
            string reason = CreateUnavailableReason;
            if (reason != null)
                throw new InvalidOperationException(Title + ": " + reason);
            string folder = AssetLocations.EnsureFolder(Location.Folder);
            var created = CreateIn(folder);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>Moves the asset into its location's folder. Returns null on success, or Unity's error.</summary>
        public string MoveToFolder()
        {
            if (!CanMove)
                return Title + " cannot be moved from the Workspace.";
            string path = AssetPath;
            if (string.IsNullOrEmpty(path))
                return Title + " does not exist.";
            string source = MovedPath(path);
            string folder = AssetLocations.EnsureFolder(Location.Folder);
            if (AssetLocations.IsIn(source, folder))
                return null;
            // Companions are the asset's direct dependencies beside it, such as a pipeline's renderer.
            string from = Path.GetDirectoryName(source)?.Replace('\\', '/');
            var companions = MovesCompanions
                ? AssetDatabase
                    .GetDependencies(source, false)
                    .Where(path =>
                        path != source
                        && path.StartsWith("Assets/", StringComparison.Ordinal)
                        && !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        && AssetLocations.IsIn(path, from)
                    )
                    .ToList()
                : new List<string>();
            string error = AssetDatabase.MoveAsset(
                source,
                AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(source))
            );
            if (!string.IsNullOrEmpty(error))
                return error;
            foreach (string companion in companions)
            {
                error = AssetDatabase.MoveAsset(
                    companion,
                    AssetDatabase.GenerateUniqueAssetPath(
                        folder + "/" + Path.GetFileName(companion)
                    )
                );
                if (!string.IsNullOrEmpty(error))
                    return error;
            }
            AssetDatabase.SaveAssets();
            return null;
        }

        /// <summary>
        /// Whether moving the asset also moves the assets it references from its own folder, such as a render
        /// pipeline's renderer or a panel's theme.
        /// </summary>
        protected virtual bool MovesCompanions => false;

        /// <summary>Creates the asset in <paramref name="folder"/>, which exists, and makes the project use it.</summary>
        protected abstract Object CreateIn(string folder);

        /// <summary>
        /// The path moved into the folder: the asset's own path by default, or a containing folder for assets that
        /// live in one, such as Addressables' data folder.
        /// </summary>
        protected virtual string MovedPath(string assetPath) => assetPath;

        /// <summary>Creates <paramref name="asset"/> at a free path for <paramref name="fileName"/> in <paramref name="folder"/>.</summary>
        protected static T Save<T>(T asset, string folder, string fileName)
            where T : Object
        {
            AssetDatabase.CreateAsset(
                asset,
                AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName)
            );
            return asset;
        }
    }

    /// <summary>
    /// Contributes configuration assets and folder-only locations to the Workspace. Sources are discovered in the Editor
    /// and need a public parameterless constructor.
    /// </summary>
    public abstract class ConfigSource
    {
        /// <summary>The configuration assets this source manages.</summary>
        public abstract IEnumerable<ConfigAsset> Assets { get; }

        /// <summary>Locations without a single asset, such as the folder new string tables are created in.</summary>
        public virtual IEnumerable<AssetLocation> Locations => Array.Empty<AssetLocation>();
    }

    /// <summary>Every configuration asset and location contributed by installed packages.</summary>
    public static class ConfigAssets
    {
        /// <summary>Category order in the Workspace; unlisted categories follow alphabetically.</summary>
        public static readonly string[] CategoryOrder =
        {
            "Threadlink",
            "Content",
            "Input",
            "Localization",
            "Rendering",
        };

        /// <summary>Discovers every source and returns its assets, ordered by category.</summary>
        public static List<ConfigAsset> All() =>
            Sources()
                .SelectMany(source => Safely(() => source.Assets))
                .OrderBy(asset => Rank(asset.Location.Category))
                .ThenBy(asset => asset.Location.Category, StringComparer.Ordinal)
                .ToList();

        /// <summary>The assets of one category.</summary>
        public static List<ConfigAsset> InCategory(string category) =>
            All().Where(asset => asset.Location.Category == category).ToList();

        /// <summary>Every location: those of the assets, then folder-only ones.</summary>
        public static List<AssetLocation> Locations()
        {
            var sources = Sources();
            return sources
                .SelectMany(source => Safely(() => source.Assets).Select(asset => asset.Location))
                .Concat(sources.SelectMany(source => Safely(() => source.Locations)))
                .GroupBy(location => location.Id)
                .Select(group => group.First())
                .OrderBy(location => Rank(location.Category))
                .ThenBy(location => location.Category, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>A location by identifier, or null.</summary>
        public static AssetLocation Location(string id) =>
            Locations().FirstOrDefault(location => location.Id == id);

        private static List<ConfigSource> Sources()
        {
            var result = new List<ConfigSource>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ConfigSource>())
                if (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                    result.Add((ConfigSource)Activator.CreateInstance(type));
            return result
                .OrderBy(source => source.GetType().FullName, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<T> Safely<T>(Func<IEnumerable<T>> read)
        {
            try
            {
                return read()?.ToList() ?? new List<T>();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return new List<T>();
            }
        }

        private static int Rank(string category)
        {
            int index = Array.IndexOf(CategoryOrder, category);
            return index < 0 ? CategoryOrder.Length : index;
        }
    }
}
