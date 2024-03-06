namespace Threadlink.Utilities.Editor.Attributes
{
	using UnityEngine;

#if UNITY_EDITOR
	using UnityEditor;
#endif

#if UNITY_EDITOR
	// Custom property drawer for the ReadOnly attribute
	[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
	public class ReadOnlyDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUI.GetPropertyHeight(property, label, true);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			GUI.enabled = false; // Disable editing
			EditorGUI.PropertyField(position, property, label, true);
			GUI.enabled = true; // Re-enable editing
		}
	}
#endif
}