namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine.UIElements;

    /// <summary>Workspace rows for configuration assets and their folders.</summary>
    public static class ConfigUI
    {
        /// <summary>
        /// A row for <paramref name="asset"/>: where it is, and Select, Create, Move to folder and Folder actions.
        /// <paramref name="changed"/> runs after an action changes the project, usually to rebuild the page.
        /// </summary>
        public static VisualElement AssetRow(
            WorkspaceContext context,
            ConfigAsset asset,
            Action changed
        )
        {
            var found = asset.Find();
            string path = found != null ? AssetDatabase.GetAssetPath(found) : null;
            string folder = asset.Location.Folder;
            var row = Row(
                asset.Title,
                found != null ? path : "Not created · new assets go to " + folder,
                found == null ? "tl-value--muted" : null
            );
            row.tooltip = asset.Description ?? string.Empty;
            if (found != null)
                row.Add(new Button(() => context.Select(found)) { text = "Select" });
            else
            {
                string reason = asset.CreateUnavailableReason;
                var create = new Button(() => Create(asset, changed)) { text = "Create" };
                if (reason != null)
                {
                    create.SetEnabled(false);
                    create.tooltip = reason;
                    row.tooltip = reason;
                }
                row.Add(create);
            }
            if (found != null && asset.CanMove && asset.IsOutsideFolder)
                row.Add(
                    new Button(() => Move(asset, changed))
                    {
                        text = "Move to folder",
                        tooltip = "Move it to " + folder + ".",
                    }
                );
            row.Add(
                new Button(() => Choose(asset.Location, changed))
                {
                    text = "Folder…",
                    tooltip =
                        "Choose the folder new "
                        + asset.Title
                        + " assets go to. Now: "
                        + folder
                        + ".",
                }
            );
            if (asset.Location.IsCustom)
                row.Add(
                    new Button(() => Reset(asset.Location, changed))
                    {
                        text = "Default folder",
                        tooltip = "Use " + AssetLocations.GetRootFolder(asset.Location.Root) + ".",
                    }
                );
            return row;
        }

        /// <summary>A row for a folder-only location, with Change and Default folder actions.</summary>
        public static VisualElement LocationRow(AssetLocation location, Action changed)
        {
            var row = Row(
                location.Title,
                location.Folder + (location.IsCustom ? string.Empty : " (default)")
            );
            row.Add(new Button(() => Choose(location, changed)) { text = "Folder…" });
            if (location.IsCustom)
                row.Add(new Button(() => Reset(location, changed)) { text = "Default folder" });
            return row;
        }

        /// <summary>A row for a root folder, with Change and Reset actions.</summary>
        public static VisualElement RootRow(ConfigRoot root, Action changed)
        {
            string current = AssetLocations.GetRootFolder(root);
            string fallback =
                root == ConfigRoot.Design
                    ? AssetLocations.DefaultDesignFolder
                    : AssetLocations.DefaultEngineeringFolder;
            var row = Row(root + " configs", current);
            row.Add(
                new Button(() =>
                {
                    string chosen = Pick(root + " configuration folder", current);
                    if (chosen == null)
                        return;
                    AssetLocations.SetRootFolder(root, chosen);
                    changed?.Invoke();
                })
                {
                    text = "Folder…",
                }
            );
            if (current != fallback)
                row.Add(
                    new Button(() =>
                    {
                        AssetLocations.SetRootFolder(root, null);
                        changed?.Invoke();
                    })
                    {
                        text = "Default folder",
                        tooltip = fallback,
                    }
                );
            return row;
        }

        /// <summary>Every asset of a category, followed by its folder-only locations.</summary>
        public static VisualElement Category(
            WorkspaceContext context,
            string category,
            Action changed
        )
        {
            var root = new VisualElement();
            foreach (var asset in ConfigAssets.InCategory(category))
                root.Add(AssetRow(context, asset, changed));
            var assetIds = ConfigAssets
                .InCategory(category)
                .Select(asset => asset.Location.Id)
                .ToHashSet();
            foreach (
                var location in ConfigAssets
                    .Locations()
                    .Where(location =>
                        location.Category == category && !assetIds.Contains(location.Id)
                    )
            )
                root.Add(LocationRow(location, changed));
            return root;
        }

        /// <summary>Asks for a folder inside Assets, starting at <paramref name="current"/>. Returns null when cancelled or invalid.</summary>
        public static string Pick(string title, string current)
        {
            string start = current;
            while (!string.IsNullOrEmpty(start) && !AssetDatabase.IsValidFolder(start))
                start = Path.GetDirectoryName(start)?.Replace('\\', '/');
            string chosen = EditorUtility.OpenFolderPanel(
                title,
                Path.GetFullPath(string.IsNullOrEmpty(start) ? "Assets" : start),
                string.Empty
            );
            if (string.IsNullOrEmpty(chosen))
                return null;
            try
            {
                return AssetLocations.Normalize(chosen);
            }
            catch (ArgumentException error)
            {
                EditorUtility.DisplayDialog("Choose a folder inside Assets", error.Message, "OK");
                return null;
            }
        }

        private static void Create(ConfigAsset asset, Action changed)
        {
            string question = asset.CreateConfirmation;
            if (
                question != null
                && !EditorUtility.DisplayDialog(
                    "Create " + asset.Title,
                    question,
                    "Create",
                    "Cancel"
                )
            )
                return;
            try
            {
                asset.Create();
            }
            catch (Exception error)
            {
                EditorUtility.DisplayDialog("Could not create " + asset.Title, error.Message, "OK");
            }
            changed?.Invoke();
        }

        private static void Move(ConfigAsset asset, Action changed)
        {
            string error = asset.MoveToFolder();
            if (error != null)
                EditorUtility.DisplayDialog("Could not move " + asset.Title, error, "OK");
            changed?.Invoke();
        }

        private static void Choose(AssetLocation location, Action changed)
        {
            string chosen = Pick(location.Title + " folder", location.Folder);
            if (chosen == null)
                return;
            AssetLocations.SetFolder(location, chosen);
            changed?.Invoke();
        }

        private static void Reset(AssetLocation location, Action changed)
        {
            AssetLocations.ResetFolder(location);
            changed?.Invoke();
        }

        private static VisualElement Row(string key, string value, string valueClass = null)
        {
            var row = new VisualElement();
            row.AddToClassList("tl-row");
            var name = new Label(key);
            name.AddToClassList("tl-key");
            var text = new Label(value);
            text.AddToClassList("tl-value");
            if (valueClass != null)
                text.AddToClassList(valueClass);
            row.Add(name);
            row.Add(text);
            return row;
        }
    }
}
