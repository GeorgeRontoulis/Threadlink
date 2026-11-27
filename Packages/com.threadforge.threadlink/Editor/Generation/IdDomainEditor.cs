namespace Threadforge.Threadlink.Editor
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>The ID domain Inspector: who owns it, what it generates, its entries and a usage preview.</summary>
    [CustomEditor(typeof(IdDomain))]
    internal sealed class IdDomainEditor : UnityEditor.Editor
    {
        private const string OwnEnumeration = "Its own enumeration";

        public override VisualElement CreateInspectorGUI()
        {
            var domain = (IdDomain)target;
            var root = new VisualElement();
            var intro = new HelpBox(string.Empty, HelpBoxMessageType.None);
            var placement = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            var move = new Button(() => MoveToOwnerFolder(domain)) { text = "Move to its owner's folder" };
            placement.Add(move);
            root.Add(intro);
            root.Add(new PropertyField(serializedObject.FindProperty("owner")));
            root.Add(placement);

            var extends = serializedObject.FindProperty("extends");
            var choices = new List<string> { OwnEnumeration };
            choices.AddRange(IdDomainTargets.All.Select(target => target.DisplayName));
            var current = IdDomainTargets.Find(extends.stringValue);
            if (current == null && !string.IsNullOrEmpty(extends.stringValue))
                choices.Add(extends.stringValue + " (not installed)");
            int index = current != null ? choices.IndexOf(current.DisplayName) : string.IsNullOrEmpty(extends.stringValue) ? 0 : choices.Count - 1;
            var generates = new PopupField<string>("Generates", choices, index)
            {
                tooltip = extends.tooltip,
            };
            generates.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            generates.RegisterValueChangedCallback(change =>
            {
                var chosen = IdDomainTargets.All.FirstOrDefault(target => target.DisplayName == change.newValue);
                if (chosen == null && change.newValue != OwnEnumeration)
                    return;
                extends.stringValue = chosen?.Id ?? string.Empty;
                serializedObject.ApplyModifiedProperties();
            });
            root.Add(generates);
            root.Add(new PropertyField(serializedObject.FindProperty("summary")));
            root.Add(new PropertyField(serializedObject.FindProperty("entries"), "Entries"));

            var problems = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            root.Add(problems);
            var usage = new Label { enableRichText = true };
            usage.style.marginTop = 6;
            usage.style.whiteSpace = WhiteSpace.Normal;
            root.Add(usage);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            buttons.Add(new Button(() => GeneratedIds.Run(new IdDomainGenerator())) { text = "Generate now" });
            buttons.Add(new Button(() => WorkspaceWindow.Open().Show(typeof(CodeGenerationPage))) { text = "Open Code Generation" });
            root.Add(buttons);

            void Refresh()
            {
                if (domain == null)
                    return;
                var target = domain.Target;
                intro.text = target != null
                    ? $"Adds these IDs to {target.ClassName} ({target.DisplayName}). Values stay the same when an entry is renamed; a removed entry stays as an obsolete tombstone."
                    : $"Generates the enumeration {domain.TypeName} (None = 0, then one member per entry). Values stay the same when an entry is renamed; a removed entry stays as an obsolete tombstone.";
                string folder = domain.Owner == IdDomainOwner.Design ? IdDomain.DesignFolder : IdDomain.EngineeringFolder;
                bool placed = AssetDatabase.GetAssetPath(domain).StartsWith(folder + "/", System.StringComparison.Ordinal);
                placement.text = $"{domain.Owner} domains live in {folder}.";
                placement.style.display = placed ? DisplayStyle.None : DisplayStyle.Flex;
                var found = domain.Validate();
                problems.text = string.Join("\n", found);
                problems.style.display = found.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                var first = domain.Entries.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.Name));
                string member = first != null ? GeneratedIds.Identifier(first.Name) : "Entry";
                usage.text = target != null
                    ? $"In code: <b>{target.ClassName}.{member}</b>"
                    : $"In code: <b>{domain.TypeName}.{member}</b>, or a serialized <b>{domain.TypeName}</b> field for a dropdown in the Inspector.";
            }

            Refresh();
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            return root;
        }

        private static void MoveToOwnerFolder(IdDomain domain)
        {
            string folder = domain.Owner == IdDomainOwner.Design ? IdDomain.DesignFolder : IdDomain.EngineeringFolder;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string path = AssetDatabase.GetAssetPath(domain);
            string error = AssetDatabase.MoveAsset(path, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(path)));
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning("Threadlink: " + error, domain);
        }
    }

    /// <summary>One entry: its name and description, with the stable value shown but not editable.</summary>
    [CustomPropertyDrawer(typeof(IdDomain.Entry))]
    internal sealed class IdDomainEntryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var name = new TextField { tooltip = property.FindPropertyRelative(nameof(IdDomain.Entry.Name)).tooltip };
            name.BindProperty(property.FindPropertyRelative(nameof(IdDomain.Entry.Name)));
            name.style.flexGrow = 1;
            name.style.flexBasis = 0;
            var summary = new TextField { tooltip = property.FindPropertyRelative(nameof(IdDomain.Entry.Summary)).tooltip };
            summary.BindProperty(property.FindPropertyRelative(nameof(IdDomain.Entry.Summary)));
            summary.style.flexGrow = 2;
            summary.style.flexBasis = 0;
            var value = new Label { tooltip = property.FindPropertyRelative(nameof(IdDomain.Entry.Value)).tooltip };
            value.style.minWidth = 90;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            value.style.opacity = 0.6f;
            var valueProperty = property.FindPropertyRelative(nameof(IdDomain.Entry.Value));
            void Show(SerializedProperty changed) => value.text = changed.intValue == 0 ? "new" : "= " + changed.intValue;
            Show(valueProperty);
            value.TrackPropertyValue(valueProperty, Show);
            row.Add(name);
            row.Add(summary);
            row.Add(value);
            return row;
        }
    }
}
