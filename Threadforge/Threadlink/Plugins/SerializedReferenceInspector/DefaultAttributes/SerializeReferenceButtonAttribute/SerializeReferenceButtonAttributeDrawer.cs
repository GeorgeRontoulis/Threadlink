#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SerializeReferenceButtonAttribute))]
public class SerializeReferenceButtonAttributeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Dynamically check if the caller requested no label (e.g. GUIContent.none)
        bool hasLabel = label != null && label != GUIContent.none && !string.IsNullOrEmpty(label.text);

        var buttonPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        if (hasLabel)
        {
            var labelPosition = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelPosition, label);

            // Only offset the button if a label is actually being drawn
            buttonPosition.x += EditorGUIUtility.labelWidth + EditorGUIUtility.standardVerticalSpacing;
            buttonPosition.width -= EditorGUIUtility.labelWidth + EditorGUIUtility.standardVerticalSpacing;
        }

        var typeRestrictions = SerializedReferenceUIDefaultTypeRestrictions.GetAllBuiltInTypeRestrictions(fieldInfo);

        // Pass the dynamically calculated buttonPosition, not the raw position
        property.DrawSelectionButtonForManagedReference(buttonPosition, typeRestrictions);

        EditorGUI.PropertyField(position, property, GUIContent.none, true);

        EditorGUI.EndProperty();
    }
}
#endif