namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEngine.UIElements;

    /// <summary>
    /// Every configuration asset installed packages manage, grouped by category, and the folders new ones are created
    /// in.
    /// </summary>
    public sealed class ConfigurationPage : WorkspacePage
    {
        /// <inheritdoc />
        public override string Title => "Configuration";

        /// <inheritdoc />
        public override string Group => "Project";

        /// <inheritdoc />
        public override int Order => 15;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            Action changed = context.Refresh;
            root.Add(
                WorkspaceUI.Text(
                    "Configuration assets of Threadlink and the Unity packages it uses. New ones are created in their folder: the engineering or design folder below, unless an asset has its own. Changing a folder affects assets created afterwards; Move to folder relocates an existing one and keeps its references."
                )
            );
            root.Add(WorkspaceUI.Heading("Default folders"));
            root.Add(ConfigUI.RootRow(ConfigRoot.Engineering, changed));
            root.Add(ConfigUI.RootRow(ConfigRoot.Design, changed));

            var assets = ConfigAssets.All();
            var outside = assets.Where(asset => asset.CanMove && asset.IsOutsideFolder).ToList();
            if (outside.Count > 0)
                root.Add(
                    WorkspaceUI.Buttons(
                        (
                            $"Move all to their folders ({outside.Count})",
                            () =>
                            {
                                if (
                                    !EditorUtility.DisplayDialog(
                                        "Move configuration assets",
                                        string.Join(
                                            "\n",
                                            outside.Select(asset =>
                                                asset.Title
                                                + ": "
                                                + asset.AssetPath
                                                + " → "
                                                + asset.Location.Folder
                                            )
                                        ),
                                        "Move",
                                        "Cancel"
                                    )
                                )
                                    return;
                                var errors = outside
                                    .Select(asset => asset.MoveToFolder())
                                    .Where(error => error != null)
                                    .ToList();
                                if (errors.Count > 0)
                                    EditorUtility.DisplayDialog(
                                        "Some assets did not move",
                                        string.Join("\n", errors),
                                        "OK"
                                    );
                                changed();
                            }
                        )
                    )
                );

            foreach (
                string category in ConfigAssets
                    .Locations()
                    .Select(location => location.Category)
                    .Distinct()
            )
            {
                root.Add(WorkspaceUI.Heading(category));
                root.Add(ConfigUI.Category(context, category, changed));
            }
            return root;
        }
    }
}
