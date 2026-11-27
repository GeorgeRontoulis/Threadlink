namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    /// <summary>Generates <c>AudioCategories</c> from the categories in Project Settings ▸ Threadlink ▸ Aura.</summary>
    public sealed class AudioCategoryGenerator : IIdGenerator
    {
        /// <inheritdoc />
        public string Title => "Audio categories";

        /// <inheritdoc />
        public string Source => "Categories in Project Settings ▸ Threadlink ▸ Aura";

        /// <inheritdoc />
        public System.Collections.Generic.IEnumerable<string> Outputs => new[] { "AudioCategories" };

        /// <inheritdoc />
        public void OpenSource() => UnityEditor.SettingsService.OpenProjectSettings("Project/Threadlink/Aura");

        /// <inheritdoc />
        public bool Generate()
        {
            var members = AuraSettings
                .Current.Categories.Where(category => category != null && !string.IsNullOrWhiteSpace(category.Name))
                .Select(category => new GeneratedIds.Member
                {
                    Name = category.Name,
                    Key = category.Name,
                    Type = "global::Threadforge.Threadlink.AudioCategory",
                    Value = "new global::Threadforge.Threadlink.AudioCategory(" + GeneratedIds.Quote(category.Name) + ")",
                    Summary = "The " + category.Name + " category (" + category.VoiceLimit + " voices).",
                })
                .ToList();
            return GeneratedIds.Write("AudioCategories", "Audio categories from Project Settings ▸ Threadlink ▸ Aura.", members);
        }
    }

    /// <summary>A dropdown of the project's audio categories for fields marked <see cref="AudioCategoryNameAttribute"/>.</summary>
    [CustomPropertyDrawer(typeof(AudioCategoryNameAttribute))]
    internal sealed class AudioCategoryNameDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var choices = AuraSettings.Current.Categories.Where(category => category != null && !string.IsNullOrWhiteSpace(category.Name)).Select(category => category.Name).ToList();
            if (!string.IsNullOrEmpty(property.stringValue) && !choices.Contains(property.stringValue))
                choices.Add(property.stringValue + " (unknown)");
            if (choices.Count == 0)
                return PlainText(property, (property.tooltip + " Add categories in Project Settings ▸ Threadlink ▸ Aura.").Trim());
            int index = Math.Max(0, choices.FindIndex(choice => choice == property.stringValue || choice == property.stringValue + " (unknown)"));
            var popup = new PopupField<string>(property.displayName, choices, index)
            {
                tooltip = string.IsNullOrEmpty(property.tooltip) ? "Categories are defined in Project Settings ▸ Threadlink ▸ Aura." : property.tooltip,
            };
            popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            popup.RegisterValueChangedCallback(change =>
            {
                property.stringValue = change.newValue.EndsWith(" (unknown)", StringComparison.Ordinal) ? change.newValue.Substring(0, change.newValue.Length - 10) : change.newValue;
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
