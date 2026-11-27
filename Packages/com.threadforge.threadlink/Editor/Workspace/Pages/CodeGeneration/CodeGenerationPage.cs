namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    /// <summary>
    /// Every code generator in one place: generated IDs and where they come from, the project's ID domains, content
    /// catalogs and subsystem registration.
    /// </summary>
    public sealed class CodeGenerationPage : WorkspacePage
    {
        private const string OwnEnumeration = "Its own enumeration";
        private static IdDomain editing;

        /// <inheritdoc />
        public override string Title => "Code Generation";

        /// <inheritdoc />
        public override string Group => "Project";

        /// <inheritdoc />
        public override int Order => 20;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            root.Add(
                WorkspaceUI.Text(
                    "Threadlink turns project data into typed code, so scenes, input, categories and your own IDs are checked by the compiler instead of spelled as strings. Generation runs automatically when a source changes."
                )
            );
            root.Add(
                WorkspaceUI.Buttons(
                    ("Generate all", () =>
                    {
                        GeneratedIds.GenerateAll();
                        context.Refresh();
                    }),
                    ("Show output folder", () => context.Select(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GeneratedIds.Folder)))
                )
            );
            root.Add(WorkspaceUI.Row("Output", GeneratedIds.Folder));

            AddGenerators(root, context);
            AddDomains(root, context);
            AddCatalogs(root, context);
            AddSubsystems(root);
            return root;
        }

        private static void AddGenerators(VisualElement root, WorkspaceContext context)
        {
            var generators = GeneratedIds.Generators();
            root.Add(WorkspaceUI.Heading($"Generated IDs ({generators.Count})"));
            foreach (var generator in generators)
            {
                var outputs = generator.Outputs.ToList();
                var counts = outputs.Select(GeneratedIds.Count).ToList();
                int live = counts.Sum(count => count.live),
                    tombstones = counts.Sum(count => count.tombstones);
                string detail = generator.Source
                    + (outputs.Count == 0 ? string.Empty : " → " + string.Join(", ", outputs))
                    + $" · {live} ID(s)"
                    + (tombstones == 0 ? string.Empty : $", {tombstones} tombstone(s)");
                var current = generator;
                root.Add(WorkspaceUI.Row(generator.Title, detail, "Open source", () => current.OpenSource()));
                root.Add(WorkspaceUI.Buttons(("Generate " + generator.Title.ToLowerInvariant(), () =>
                {
                    GeneratedIds.Run(current);
                    context.Refresh();
                })));
            }
        }

        private static void AddDomains(VisualElement root, WorkspaceContext context)
        {
            var domains = IdDomain.FindAll();
            root.Add(WorkspaceUI.Heading($"ID domains ({domains.Count})"));
            root.Add(
                WorkspaceUI.Text(
                    "An ID domain is a list of names your game refers to, such as enemy kinds or spawn points. It generates an enumeration that is type-safe in code and a dropdown in the Inspector; values never change when an entry is renamed. A domain can instead add entries to a native domain, such as StatelessRNG random domains or Vault fields."
                )
            );
            root.Add(CreateForm(context));
            foreach (var domain in domains)
            {
                var problems = domain.Validate();
                string generates = domain.Target != null ? "adds to " + domain.Target.ClassName : "enum " + domain.TypeName;
                if (!string.IsNullOrEmpty(domain.Extends) && domain.Target == null)
                    generates = "extends " + domain.Extends + " (not installed)";
                string detail = $"{domain.Owner} · {generates} · {domain.Entries.Count} entr{(domain.Entries.Count == 1 ? "y" : "ies")}"
                    + (problems.Count == 0 ? string.Empty : " · " + problems[0]);
                var current = domain;
                root.Add(WorkspaceUI.Row(domain.name, detail, editing == domain ? "Close" : "Edit", () =>
                {
                    editing = editing == current ? null : current;
                    context.Refresh();
                }, problems.Count == 0 ? IssueSeverity.Info : IssueSeverity.Error));
                if (editing == domain)
                {
                    var inspector = new InspectorElement(domain);
                    inspector.AddToClassList("tl-panel");
                    inspector.style.marginBottom = 8;
                    root.Add(inspector);
                }
            }
        }

        private static VisualElement CreateForm(WorkspaceContext context)
        {
            var form = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, alignItems = Align.Center, marginBottom = 6 } };
            var name = new TextField { value = string.Empty, tooltip = "Becomes the enumeration's name, for example EnemyKind." };
            name.style.minWidth = 160;
            name.style.flexGrow = 1;
            var owner = new EnumField(IdDomainOwner.Design) { tooltip = "Design domains live under Design/Identifiers, engineering domains under Engineering/Identifiers." };
            owner.style.minWidth = 110;
            var choices = new List<string> { OwnEnumeration };
            choices.AddRange(IdDomainTargets.All.Select(target => target.DisplayName));
            var generates = new PopupField<string>(choices, 0) { tooltip = "Generate its own enumeration, or add to a native domain." };
            generates.style.minWidth = 180;
            var create = new Button(() =>
            {
                if (string.IsNullOrWhiteSpace(name.value))
                    return;
                var target = IdDomainTargets.All.FirstOrDefault(each => each.DisplayName == generates.value);
                editing = IdDomain.Create(name.value.Trim(), (IdDomainOwner)owner.value, target?.Id ?? string.Empty);
                context.Refresh();
            })
            {
                text = "New domain",
            };
            form.Add(name);
            form.Add(owner);
            form.Add(generates);
            form.Add(create);
            return form;
        }

        private static void AddCatalogs(VisualElement root, WorkspaceContext context)
        {
            var rows = ContentMapping.Load();
            root.Add(WorkspaceUI.Heading("Content mapping"));
            root.Add(WorkspaceUI.Text("Addressables entries mapped to ContentAssets, ContentPrefabs and ContentScenes. Addressables groups stay the source of truth."));
            root.Add(WorkspaceUI.Row("Mapped", $"{rows.Count(row => row.Mapped)} of {rows.Count} Addressables entries", "Open Content page", () => context.OpenPage<ContentPage>()));
        }

        private static void AddSubsystems(VisualElement root)
        {
            root.Add(WorkspaceUI.Heading("Subsystem registration"));
            root.Add(
                WorkspaceUI.Text(
                    "Classes deriving from ThreadlinkSubsystem<T> are registered by the bundled source generator while scripts compile. There is nothing to configure."
                )
            );
            int count = TypeCache.GetTypesDerivedFrom<ThreadlinkSubsystem>().Count(type => !type.IsAbstract && !type.IsGenericTypeDefinition);
            root.Add(WorkspaceUI.Row("Subsystem types", count.ToString()));
        }
    }
}
