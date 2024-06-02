namespace Threadlink.Utilities.Editor.Attributes
{
#if UNITY_EDITOR
	using UnityEditor;
	using UnityEngine;

	[CustomPropertyDrawer(typeof(SpritePreviewAttribute))]
	public class SpritePreviewDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var spritePreviewAttribute = attribute as SpritePreviewAttribute;
			float previewSize = spritePreviewAttribute.PreviewHeight;
			float fieldWidth = position.width - previewSize - 4; // 4 is a small padding between the field and the preview

			// Draw the label and sprite field
			// Adjust the field position to be vertically centered relative to the preview
			float fieldHeight = EditorGUIUtility.singleLineHeight;
			float verticalOffset = (previewSize - fieldHeight) * 0.5f; // Centering offset
			var fieldPosition = new Rect(position.x, position.y + verticalOffset, fieldWidth, fieldHeight);
			EditorGUI.PropertyField(fieldPosition, property, label, true);

			if (property.objectReferenceValue is Sprite sprite && sprite.texture != null)
			{
				// Calculate the position for the sprite preview, to the right of the field
				var previewPosition = new Rect(position.xMax - previewSize, position.y, previewSize, previewSize);

				// Draw the sprite preview
				GUI.DrawTexture(previewPosition, sprite.texture, ScaleMode.ScaleToFit, true);
			}
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// Adjust the property height to accommodate the sprite preview
			return Mathf.Max(EditorGUIUtility.singleLineHeight, (attribute as SpritePreviewAttribute).PreviewHeight);
		}
	}

#endif
}