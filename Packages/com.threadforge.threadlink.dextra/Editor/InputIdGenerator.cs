namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Layouts;
    using UnityEngine.UIElements;

    /// <summary>
    /// Generates <c>GameInput</c>: an <see cref="InputMapId"/> per action map under <c>GameInput.Maps</c>, and a typed
    /// <see cref="InputId{T}"/> per action under <c>GameInput.MapName</c>, from the input actions in Project Settings ▸
    /// Threadlink ▸ Dextra. IDs are the actions' stable IDs, so renaming an action keeps working code.
    /// </summary>
    public sealed class InputIdGenerator : IIdGenerator
    {
        /// <inheritdoc />
        public string Title => "Input actions";

        /// <inheritdoc />
        public string Source => "Input actions in Project Settings ▸ Threadlink ▸ Dextra";

        /// <inheritdoc />
        public System.Collections.Generic.IEnumerable<string> Outputs => new[] { "GameInput" };

        /// <inheritdoc />
        public void OpenSource() => UnityEditor.SettingsService.OpenProjectSettings("Project/Threadlink/Dextra");

        /// <inheritdoc />
        public bool Generate()
        {
            var actions = DextraSettings.Current.Actions;
            if (actions == null)
                return false;
            return Write("GameInput", actions);
        }

        /// <summary>Generates an ID class for any input actions asset, for example a sample's own actions.</summary>
        public static bool Write(string className, InputActionAsset actions)
        {
            var members = new List<GeneratedIds.Member>();
            foreach (var map in actions.actionMaps)
            {
                members.Add(
                    new GeneratedIds.Member
                    {
                        Group = "Maps",
                        Name = map.name,
                        Key = map.id.ToString("D"),
                        Type = "global::Threadforge.Threadlink.InputMapId",
                        Value = "new global::Threadforge.Threadlink.InputMapId(new global::System.Guid(\"" + map.id.ToString("D") + "\"), " + GeneratedIds.Quote(map.name) + ")",
                        Summary = "The " + map.name + " action map.",
                    }
                );
                foreach (var action in map.actions)
                {
                    var valueType = ValueType(action);
                    string type = valueType == null ? "global::Threadforge.Threadlink.InputId" : "global::Threadforge.Threadlink.InputId<" + TypeName(valueType) + ">";
                    members.Add(
                        new GeneratedIds.Member
                        {
                            Group = GeneratedIds.Identifier(map.name),
                            Name = action.name,
                            Key = action.id.ToString("D"),
                            Type = type,
                            Value = "new " + type + "(new global::System.Guid(\"" + action.id.ToString("D") + "\"), " + GeneratedIds.Quote(map.name + "/" + action.name) + ")",
                            Summary = action.type + (string.IsNullOrEmpty(action.expectedControlType) ? string.Empty : " (" + action.expectedControlType + ")") + " action " + map.name + "/" + action.name + ".",
                        }
                    );
                }
            }
            return GeneratedIds.Write(className, "Input action IDs generated from " + actions.name + ".", members);
        }

        private static string TypeName(Type type) =>
            type == typeof(float) ? "float"
            : type == typeof(int) ? "int"
            : type == typeof(bool) ? "bool"
            : type == typeof(double) ? "double"
            : "global::" + type.FullName.Replace('+', '.');

        /// <summary>The value type an action reads, from its expected control layout; null when it has none.</summary>
        private static Type ValueType(InputAction action)
        {
            string layout = action.expectedControlType;
            if (string.IsNullOrEmpty(layout))
                return action.type == InputActionType.Button ? typeof(float) : null;
            InputControlLayout loaded;
            try
            {
                loaded = InputSystem.LoadLayout(layout);
            }
            catch (Exception)
            {
                return null;
            }
            for (var type = loaded?.type; type != null; type = type.BaseType)
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(InputControl<>))
                {
                    var value = type.GetGenericArguments()[0];
                    return value.IsValueType && value.IsPublic ? value : null;
                }
            return null;
        }
    }

    /// <summary>A dropdown of the project's actions ("Map/Action") for fields marked <see cref="InputActionPickerAttribute"/>; stores the action ID.</summary>
    [CustomPropertyDrawer(typeof(InputActionPickerAttribute))]
    internal sealed class InputActionPickerDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var actions = DextraSettings.Current.Actions;
            var all = actions == null ? new List<UnityEngine.InputSystem.InputAction>() : actions.ToList();
            if (all.Count == 0)
            {
                var text = new TextField(property.displayName) { tooltip = (property.tooltip + " Assign input actions to choose from them.").Trim() };
                text.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                text.BindProperty(property);
                return text;
            }
            var choices = new List<string> { "(None)" };
            choices.AddRange(all.Select(action => action.actionMap.name + "/" + action.name));
            int index = all.FindIndex(action => action.id.ToString("D") == property.stringValue || action.actionMap.name + "/" + action.name == property.stringValue) + 1;
            var popup = new PopupField<string>(property.displayName, choices, Math.Max(0, index)) { tooltip = property.tooltip };
            popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            popup.RegisterValueChangedCallback(change =>
            {
                int chosen = choices.IndexOf(change.newValue) - 1;
                property.stringValue = chosen < 0 ? string.Empty : all[chosen].id.ToString("D");
                property.serializedObject.ApplyModifiedProperties();
            });
            return popup;
        }
    }

    /// <summary>A dropdown of the project's action maps for fields marked <see cref="InputMapReferenceAttribute"/>; stores the map ID.</summary>
    [CustomPropertyDrawer(typeof(InputMapReferenceAttribute))]
    internal sealed class InputMapReferenceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var actions = DextraSettings.Current.Actions;
            if (actions == null || actions.actionMaps.Count == 0)
                return PlainText(property, (property.tooltip + " Assign input actions to choose from their maps.").Trim());
            var maps = actions.actionMaps.ToList();
            var choices = new List<string> { "(None)" };
            choices.AddRange(maps.Select(map => map.name));
            int index = maps.FindIndex(map => map.id.ToString("D") == property.stringValue || map.name == property.stringValue) + 1;
            var popup = new PopupField<string>(property.displayName, choices, Math.Max(0, index)) { tooltip = property.tooltip };
            popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            popup.RegisterValueChangedCallback(change =>
            {
                int chosen = choices.IndexOf(change.newValue) - 1;
                property.stringValue = chosen < 0 ? string.Empty : maps[chosen].id.ToString("D");
                property.serializedObject.ApplyModifiedProperties();
            });
            return popup;
        }

        // A bound text field for when there is nothing to choose from yet. A PropertyField here would draw this drawer again.
        private static VisualElement PlainText(SerializedProperty property, string tooltip)
        {
            var text = new TextField(property.displayName) { tooltip = tooltip };
            text.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            text.BindProperty(property);
            return text;
        }
    }
}
