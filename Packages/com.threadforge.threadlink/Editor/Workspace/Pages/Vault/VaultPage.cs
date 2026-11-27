namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine.UIElements;

    /// <summary>Typed definition assets, schema versions and pending authoring migrations.</summary>
    public sealed class VaultPage : WorkspacePage
    {
        /// <inheritdoc />
        public override string Title => "Vault";

        /// <inheritdoc />
        public override int Order => 30;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            root.Add(
                WorkspaceUI.Text(
                    "Vault assets are validated with the same preparation used at runtime. Pending authoring migrations are applied explicitly from each asset's Inspector."
                )
            );
            var definitions = VaultValidator.FindDefinitions();
            root.Add(WorkspaceUI.Heading($"Vault assets ({definitions.Count})"));
            foreach (var group in definitions.GroupBy(d => d.GetType().Name).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                root.Add(WorkspaceUI.Heading(group.Key));
                foreach (var definition in group)
                {
                    var target = definition;
                    string state;
                    try
                    {
                        state = $"schema v{definition.SchemaVersion}, authored v{definition.AuthoredVersion}" + (definition.AuthoredVersion != definition.SchemaVersion ? " — migration required" : string.Empty);
                    }
                    catch (Exception error)
                    {
                        state = error.Message;
                    }
                    root.Add(WorkspaceUI.Row(definition.name, state, "Select", () => context.Select(target)));
                }
            }
            return root;
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
            foreach (var definition in FindDefinitions())
            {
                try
                {
                    var identity = definition.Identity;
                    if (identities.TryGetValue(identity, out var other))
                        issues.Add(new ValidationIssue(Name, IssueSeverity.Error, $"{definition.name} shares identity {identity} with {other.name}.", definition));
                    else
                        identities.Add(identity, definition);
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
