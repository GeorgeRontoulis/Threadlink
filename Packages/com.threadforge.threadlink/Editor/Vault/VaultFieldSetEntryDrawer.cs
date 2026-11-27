namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary>The Vault field names the project defines: the entries of ID domains that extend Vault fields.</summary>
    internal static class VaultFieldNames
    {
        public const string Target = "Vault.Fields";

        /// <summary>Generated member names (the keys a field set stores), sorted.</summary>
        public static List<string> Known() =>
            IdDomain
                .FindAll()
                .Where(domain => domain.Extends == Target)
                .SelectMany(domain => domain.Entries)
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry => GeneratedIds.Identifier(entry.Name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
    }

    /// <summary>One field set entry on one line: the field, its kind and a value editor for that kind.</summary>
    [CustomPropertyDrawer(typeof(VaultFieldSetAsset.Entry))]
    public sealed class VaultFieldSetEntryDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var field = property.FindPropertyRelative("field");
            var kind = property.FindPropertyRelative("kind");

            var names = VaultFieldNames.Known();
            VisualElement picker;
            if (names.Count == 0)
            {
                var text = new TextField { tooltip = "No ID domain extends Vault fields yet. Create one (Create ▸ Threadlink ▸ ID Domain, Extends: Vault fields) to pick fields from a list." };
                text.BindProperty(field);
                picker = text;
            }
            else
            {
                string current = field.stringValue;
                var choices = new List<string>(names);
                if (!string.IsNullOrEmpty(current) && !choices.Contains(current))
                    choices.Insert(0, current);
                var dropdown = new DropdownField(choices, string.IsNullOrEmpty(current) ? -1 : choices.IndexOf(current))
                {
                    tooltip = "The field: an entry of an ID domain that extends Vault fields.",
                };
                if (string.IsNullOrEmpty(current))
                    dropdown.SetValueWithoutNotify("Choose a field");
                else if (!names.Contains(current))
                    dropdown.tooltip = current + " is not defined by any Vault fields domain.";
                dropdown.RegisterValueChangedCallback(change =>
                {
                    field.stringValue = change.newValue;
                    field.serializedObject.ApplyModifiedProperties();
                });
                picker = dropdown;
            }
            picker.style.width = new Length(38, LengthUnit.Percent);
            row.Add(picker);

            var kindField = new EnumField((VaultValueKind)kind.enumValueIndex) { tooltip = "The kind of value." };
            kindField.style.width = 96;
            row.Add(kindField);

            var value = new VisualElement { style = { flexGrow = 1 } };
            row.Add(value);
            kindField.RegisterValueChangedCallback(change =>
            {
                kind.enumValueIndex = Convert.ToInt32(change.newValue);
                kind.serializedObject.ApplyModifiedProperties();
                Fill(value, property, (VaultValueKind)kind.enumValueIndex);
            });
            Fill(value, property, (VaultValueKind)kind.enumValueIndex);
            return row;
        }

        private static void Fill(VisualElement container, SerializedProperty entry, VaultValueKind kind)
        {
            container.Clear();
            var whole = entry.FindPropertyRelative("whole");
            var number = entry.FindPropertyRelative("number");
            var vector = entry.FindPropertyRelative("vector");
            var wholes = entry.FindPropertyRelative("wholes");
            VisualElement editor = kind switch
            {
                VaultValueKind.Integer => Field(new IntegerField(), whole, p => (int)p.longValue, (p, v) => p.longValue = v),
                VaultValueKind.Long => Bound(new LongField(), whole),
                VaultValueKind.Float => Field(new FloatField(), number, p => (float)p.doubleValue, (p, v) => p.doubleValue = v),
                VaultValueKind.Double => Bound(new DoubleField(), number),
                VaultValueKind.Boolean => Bound(new Toggle(), entry.FindPropertyRelative("flag")),
                VaultValueKind.Text => Bound(new TextField { multiline = true }, entry.FindPropertyRelative("text")),
                VaultValueKind.Vector2 => Field(new Vector2Field(), vector, p => (Vector2)p.vector4Value, (p, v) => p.vector4Value = v),
                VaultValueKind.Vector3 => Field(new Vector3Field(), vector, p => (Vector3)p.vector4Value, (p, v) => p.vector4Value = v),
                VaultValueKind.Vector4 => Bound(new Vector4Field(), vector),
                VaultValueKind.Color => Field(new ColorField(), vector, p => (Color)p.vector4Value, (p, v) => p.vector4Value = v),
                VaultValueKind.Rotation => Field(new Vector3Field { tooltip = "Euler angles in degrees." }, vector, p => (Vector3)p.vector4Value, (p, v) => p.vector4Value = v),
                VaultValueKind.Vector2Int => Field(new Vector2IntField(), wholes, p => (Vector2Int)p.vector3IntValue, (p, v) => p.vector3IntValue = new Vector3Int(v.x, v.y, 0)),
                VaultValueKind.Vector3Int => Bound(new Vector3IntField(), wholes),
                VaultValueKind.Object => Bound(new ObjectField { objectType = typeof(Object), allowSceneObjects = false }, entry.FindPropertyRelative("reference")),
                _ => new Label("Unknown kind."),
            };
            editor.style.flexGrow = 1;
            container.Add(editor);
        }

        private static VisualElement Bound<T>(BaseField<T> field, SerializedProperty property)
        {
            field.BindProperty(property);
            return field;
        }

        // Fields whose storage differs from the shown type (an int in a long, a Vector2 in a Vector4) are copied by hand.
        private static VisualElement Field<T>(BaseField<T> field, SerializedProperty property, Func<SerializedProperty, T> read, Action<SerializedProperty, T> write)
        {
            field.SetValueWithoutNotify(read(property));
            field.RegisterValueChangedCallback(change =>
            {
                write(property, change.newValue);
                property.serializedObject.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(property, changed => field.SetValueWithoutNotify(read(changed)));
            return field;
        }
    }
}
