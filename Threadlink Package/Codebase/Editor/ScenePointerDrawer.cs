namespace Threadlink.Editor
{
	using Addressables;
	using UnityEditor;
	using UnityEngine;

	[CustomPropertyDrawer(typeof(ScenePointer))]
	internal sealed class ScenePointerDrawer : PropertyDrawer
	{
		private string[] sceneNames = new string[] { "None" };

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			Initialize();

			EditorGUI.BeginProperty(position, label, property);

			// Access the 'indexInDatabase' property
			SerializedProperty indexProp = property.FindPropertyRelative("indexInDatabase");
			int currentIndexInDatabase = indexProp.intValue;

			// Map indexInDatabase to dropdown index
			int dropdownIndex = currentIndexInDatabase >= 0 ? currentIndexInDatabase + 1 : 0;

			// Determine the validity of the selected scene
			Object editorScene = GetEditorSceneForIndex(currentIndexInDatabase);

			// Store the original GUI color
			Color originalColor = GUI.color;

			GUI.color = dropdownIndex > 0 && editorScene != null ? Color.green : Color.red;

			// Draw the scene dropdown
			EditorGUI.BeginChangeCheck();
			int selectedDropdownIndex = EditorGUI.Popup(position, label.text, dropdownIndex, sceneNames);
			if (EditorGUI.EndChangeCheck())
			{
				// Map dropdown index back to indexInDatabase
				if (selectedDropdownIndex == 0)
				{
					// "None" selected
					indexProp.intValue = -1;
				}
				else
				{
					// Scene selected
					indexProp.intValue = selectedDropdownIndex - 1;
				}
			}

			// Reset GUI color to original
			GUI.color = originalColor;

			EditorGUI.EndProperty();
		}

		/// <summary>
		/// Initializes the sceneNames array by populating it with scene names from ThreadlinkPreferences.
		/// The first entry is always "None".
		/// </summary>
		private void Initialize()
		{
			var preferences = ThreadlinkPreferencesUtility.Preferences;

			if (preferences != null)
			{
				var scenes = preferences.sceneDatabase;
				sceneNames = new string[scenes.Length + 1];
				sceneNames[0] = "None";

				for (int i = 0; i < scenes.Length; i++)
				{
					var asset = scenes[i].editorAsset;

					string sceneName = asset != null ? asset.name : $"Scene {i + 1}";
					sceneNames[i + 1] = sceneName;
				}
			}
			else
			{
				sceneNames = new string[] { "None" };
			}
		}

		/// <summary>
		/// Retrieves the editor scene asset for a given index in the database.
		/// Returns null if the index is -1 or out of range.
		/// </summary>
		/// <param name="indexInDatabase">The index in the database (-1 for "None").</param>
		/// <returns>The corresponding editor scene asset if valid; otherwise, null.</returns>
		private Object GetEditorSceneForIndex(int indexInDatabase)
		{
			if (indexInDatabase < 0)
				return null; // "None" is represented by -1

			var preferences = ThreadlinkPreferencesUtility.Preferences;

			if (preferences == null)
				return null;

			// Check sceneDatabase
			if (indexInDatabase < preferences.sceneDatabase.Length)
			{
				return preferences.sceneDatabase[indexInDatabase].editorAsset;
			}

			return null;
		}
	}
}
