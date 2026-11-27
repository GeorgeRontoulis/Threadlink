namespace Threadforge.Threadlink.Editor
{
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>A range slider with editable ends for fields marked <see cref="MinMaxRangeAttribute"/>.</summary>
    [CustomPropertyDrawer(typeof(MinMaxRangeAttribute))]
    internal sealed class MinMaxRangeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var range = (MinMaxRangeAttribute)attribute;
            bool whole = property.propertyType == SerializedPropertyType.Vector2Int;
            if (property.propertyType != SerializedPropertyType.Vector2 && !whole)
                return new HelpBox(property.displayName + ": [MinMaxRange] needs a Vector2 or Vector2Int field.", HelpBoxMessageType.Warning);

            Vector2 Read() => whole ? (Vector2)property.vector2IntValue : property.vector2Value;
            void Write(Vector2 value)
            {
                float low = Mathf.Clamp(Mathf.Min(value.x, value.y), range.Min, range.Max);
                float high = Mathf.Clamp(Mathf.Max(value.x, value.y), range.Min, range.Max);
                if (whole)
                    property.vector2IntValue = new Vector2Int(Mathf.RoundToInt(low), Mathf.RoundToInt(high));
                else
                    property.vector2Value = new Vector2(low, high);
                property.serializedObject.ApplyModifiedProperties();
            }

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var slider = new MinMaxSlider(property.displayName, Read().x, Read().y, range.Min, range.Max) { tooltip = property.tooltip };
            slider.AddToClassList(BaseField<Vector2>.alignedFieldUssClassName);
            slider.style.flexGrow = 1;
            var low = new FloatField { value = Read().x, style = { width = 52 } };
            var high = new FloatField { value = Read().y, style = { width = 52 } };
            slider.RegisterValueChangedCallback(change => Write(change.newValue));
            low.RegisterValueChangedCallback(change => Write(new Vector2(change.newValue, Read().y)));
            high.RegisterValueChangedCallback(change => Write(new Vector2(Read().x, change.newValue)));
            row.TrackPropertyValue(property, _ =>
            {
                var value = Read();
                slider.SetValueWithoutNotify(value);
                low.SetValueWithoutNotify(value.x);
                high.SetValueWithoutNotify(value.y);
            });
            row.Add(slider);
            row.Add(low);
            row.Add(high);
            return row;
        }
    }

    /// <summary>An <see cref="AnimatorHash"/> as one name field, showing its hash.</summary>
    [CustomPropertyDrawer(typeof(AnimatorHash))]
    internal sealed class AnimatorHashDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var name = property.FindPropertyRelative("name");
            var field = new TextField(property.displayName) { tooltip = "The parameter or state name, as written in the Animator Controller." };
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            field.BindProperty(name);
            void Hint() => field.tooltip = "Animator hash " + Animator.StringToHash(name.stringValue ?? string.Empty) + ". The name, as written in the Animator Controller.";
            Hint();
            field.TrackPropertyValue(name, _ => Hint());
            return field;
        }
    }
}
