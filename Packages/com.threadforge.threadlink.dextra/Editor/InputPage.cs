namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;

    /// <summary>Input action assets and navigation to the Input System editors.</summary>
    public sealed class InputPage : WorkspacePage
    {
        /// <inheritdoc />
        public override string Title => "Input";

        /// <inheritdoc />
        public override int Order => 40;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            root.Add(
                WorkspaceUI.Text(
                    "Threadlink input contexts clone these assets per player. Edit maps, bindings and control schemes with the Input System's own editor."
                )
            );
            root.Add(WorkspaceUI.Buttons(("Input System Settings", () => context.OpenProjectSettings("Project/Input System Package"))));
            var assets = InputValidator.FindAssets();
            root.Add(WorkspaceUI.Heading($"Action assets ({assets.Count})"));
            foreach (var asset in assets)
            {
                var target = asset;
                root.Add(
                    WorkspaceUI.Row(
                        AssetDatabase.GetAssetPath(asset),
                        $"{asset.actionMaps.Count} map(s), {asset.controlSchemes.Count} scheme(s)",
                        "Open",
                        () => AssetDatabase.OpenAsset(target)
                    )
                );
            }
            return root;
        }
    }

    /// <summary>Reports action assets whose bindings reference undefined control schemes.</summary>
    public sealed class InputValidator : IWorkspaceValidator
    {
        /// <inheritdoc />
        public string Name => "Input";

        internal static List<InputActionAsset> FindAssets() =>
            AssetDatabase
                .FindAssets("t:" + nameof(InputActionAsset))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<InputActionAsset>)
                .Where(asset => asset != null)
                .ToList();

        /// <inheritdoc />
        public void Validate(List<ValidationIssue> issues)
        {
            foreach (var asset in FindAssets())
            {
                var schemes = new HashSet<string>(asset.controlSchemes.Select(s => s.bindingGroup), StringComparer.OrdinalIgnoreCase);
                foreach (var map in asset.actionMaps)
                    foreach (var binding in map.bindings)
                    {
                        if (string.IsNullOrEmpty(binding.groups) || binding.isComposite)
                            continue;
                        foreach (var group in binding.groups.Split(InputBinding.Separator))
                            if (group.Length != 0 && !schemes.Contains(group))
                                issues.Add(
                                    new ValidationIssue(
                                        Name,
                                        IssueSeverity.Warning,
                                        $"{asset.name}/{map.name}: binding {binding.path} uses undefined control scheme '{group}'.",
                                        asset
                                    )
                                );
                    }
            }
        }
    }
}
