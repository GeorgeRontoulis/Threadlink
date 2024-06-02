namespace Threadlink.Utilities.Editor.Attributes
{
#if UNITY_EDITOR
	using UnityEngine;
	using UnityEditor;

	[CustomPropertyDrawer(typeof(LabelledSliderAttribute))]
	public sealed class LabelledSliderDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var sliderAttribute = attribute as LabelledSliderAttribute;

			if (property.propertyType.Equals(SerializedPropertyType.Float))
			{
				EditorGUI.BeginChangeCheck();

				float value = property.floatValue;

				EditorGUILayout.BeginVertical();
				EditorGUILayout.LabelField(sliderAttribute.propertyLabel);
				EditorGUILayout.EndVertical();
				value = EditorGUILayout.Slider(value, sliderAttribute.minValue, sliderAttribute.maxValue, GUILayout.ExpandWidth(true));

				if (EditorGUI.EndChangeCheck()) property.floatValue = value;
			}
			else EditorGUILayout.LabelField("LabelledSlider can only be used with a float.");
		}
	}
#endif
}