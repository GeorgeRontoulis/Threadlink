namespace Threadlink.Editor.Attributes
{
	using UnityEditor;
	using UnityEngine;

	[CustomPropertyDrawer(typeof(SpritePreviewAttribute))]
	internal sealed class SpritePreviewDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var spritePreviewAttribute = attribute as SpritePreviewAttribute;
			float previewSize = spritePreviewAttribute.PreviewHeight;
			float fieldWidth = position.width - previewSize - 4;

			float fieldHeight = EditorGUIUtility.singleLineHeight;
			float verticalOffset = (previewSize - fieldHeight) * 0.5f;
			var fieldPosition = new Rect(position.x, position.y + verticalOffset, fieldWidth, fieldHeight);
			EditorGUI.PropertyField(fieldPosition, property, label, true);

			if (property.objectReferenceValue is Sprite sprite && sprite.texture != null)
			{
				var previewPosition = new Rect(position.xMax - previewSize, position.y, previewSize, previewSize);
				GUI.DrawTexture(previewPosition, sprite.texture, ScaleMode.ScaleToFit, true);
			}
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return Mathf.Max(EditorGUIUtility.singleLineHeight, (attribute as SpritePreviewAttribute).PreviewHeight);
		}
	}
}