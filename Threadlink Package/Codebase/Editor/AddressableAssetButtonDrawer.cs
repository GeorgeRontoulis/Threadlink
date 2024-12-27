namespace Threadlink.Editor.Attributes
{
	using UnityEngine;
	using UnityEditor;

	[CustomPropertyDrawer(typeof(AddressableAssetButtonAttribute))]
	internal sealed class AddressableAssetButtonDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			string assetPath = property.stringValue;
			bool pathIsValid = string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)) == false;

			string assetName = pathIsValid ? System.IO.Path.GetFileName(assetPath) : "Please select a valid asset";

			GUI.backgroundColor = pathIsValid ? Color.green : Color.red;

			var buttonContent = new GUIContent(assetName, assetPath);

			if (GUI.Button(position, buttonContent))
			{
				AddressableSelectorWindow.ShowWindow(ref property, (selectedAsset) =>
				{
					if (property == null) return;

					property.stringValue = selectedAsset;
					property.serializedObject.ApplyModifiedProperties();
				});
			}

			GUI.backgroundColor = Color.white;

			EditorGUI.EndProperty();
		}
	}
}