namespace Threadlink.Editor
{
	using Addressables;
	using Core;
	using System.Collections.Generic;
	using UnityEditor;
	using UnityEngine;

	[CustomPropertyDrawer(typeof(GroupedAssetPointer))]
	internal sealed class GroupedAssetPointerDrawer : PropertyDrawer
	{
		// Cache to store foldout states based on property paths
		private static readonly Dictionary<string, bool> foldoutStates = new();

		// Define spacing between fields
		private const float Spacing = 8f; // Spacing between fields

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// Begin the property
			EditorGUI.BeginProperty(position, label, property);

			// Retrieve or initialize the foldout state for this property
			if (!foldoutStates.ContainsKey(property.propertyPath))
				foldoutStates[property.propertyPath] = false;

			// Calculate the position for the foldout
			var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

			// Draw the foldout and update the state
			foldoutStates[property.propertyPath] = EditorGUI.Foldout(
				foldoutRect,
				foldoutStates[property.propertyPath],
				label,
				true // toggleOnLabelClick: allows clicking the label to toggle
			);

			// If the foldout is expanded, draw the asset dropdowns
			if (foldoutStates[property.propertyPath])
			{
				// Increase the indent level for nested fields
				EditorGUI.indentLevel++;

				// Calculate the height needed for the additional fields
				float lineHeight = EditorGUIUtility.singleLineHeight;

				// Define the starting y position
				float yOffset = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

				// Access the 'group' property
				var groupProp = property.FindPropertyRelative("group");
				var currentGroup = (ThreadlinkAddressableGroup)groupProp.enumValueIndex;

				// Retrieve asset names based on the selected group, including "None"
				var assetNames = GetAssetNamesForGroup(currentGroup);

				// Access the 'indexInDatabase' property
				var indexPropAsset = property.FindPropertyRelative("indexInDatabase");
				int currentIndexInDatabase = indexPropAsset.intValue;

				// Map indexInDatabase to dropdown index
				int dropdownIndex = currentIndexInDatabase >= 0 ? currentIndexInDatabase + 1 : 0;

				// Define Rects for group and asset dropdowns
				var groupLabelRect = new Rect(position.x + 15, yOffset, 60, lineHeight); // Indent label by 15
				var groupFieldRect = new Rect(position.x + 15 + 65, yOffset, position.width - 15 - 65 - Spacing, lineHeight);

				yOffset += lineHeight + EditorGUIUtility.standardVerticalSpacing;

				var assetLabelRect = new Rect(position.x + 15, yOffset, 60, lineHeight);
				var assetFieldRect = new Rect(position.x + 15 + 65, yOffset, position.width - 15 - 65 - Spacing, lineHeight);

				// Draw the 'Group' label and dropdown
				EditorGUI.LabelField(groupLabelRect, "Group");
				EditorGUI.BeginChangeCheck();

				var selectedGroup = (ThreadlinkAddressableGroup)EditorGUI.EnumPopup(groupFieldRect, GUIContent.none, currentGroup);

				if (EditorGUI.EndChangeCheck())
				{
					groupProp.enumValueIndex = (int)selectedGroup;

					// Reset indexInDatabase when group changes
					indexPropAsset.intValue = -1; // Default to "None"

					// Optionally, clear the cached asset names for the previous group to ensure data consistency
					// Uncomment the following line if you want to refresh the cache when the group changes
					// groupAssetNamesCache.Remove(currentGroup);
				}

				// Draw the 'Asset' label
				EditorGUI.LabelField(assetLabelRect, "Asset");

				// Determine the validity of the selected asset
				var editorAsset = GetEditorAssetForGroupAndIndex(currentGroup, currentIndexInDatabase);

				// Store the original GUI color
				var originalColor = GUI.color;

				// Set GUI.color based on asset validity
				if (dropdownIndex == 0)
				{
					// "None" selected - Invalid selection
					GUI.color = Color.red;
				}
				else if (editorAsset != null)
				{
					// Valid asset selected
					GUI.color = Color.green;
				}
				else
				{
					// Invalid asset selection
					GUI.color = Color.red;
				}

				// Draw the asset dropdown
				EditorGUI.BeginChangeCheck();
				int selectedDropdownIndex = EditorGUI.Popup(assetFieldRect, dropdownIndex, assetNames);
				if (EditorGUI.EndChangeCheck())
				{
					// Map dropdown index back to indexInDatabase
					if (selectedDropdownIndex == 0)
					{
						// "None" selected
						indexPropAsset.intValue = -1;
					}
					else
					{
						// Asset selected
						indexPropAsset.intValue = selectedDropdownIndex - 1;
					}
				}

				// Reset GUI color to original
				GUI.color = originalColor;

				// Restore the indent level
				EditorGUI.indentLevel--;
			}

			EditorGUI.EndProperty();
		}

		/// <summary>
		/// Calculates the height required for the property drawer.
		/// Ensures enough space for the foldout and its contents.
		/// </summary>
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float height = EditorGUIUtility.singleLineHeight;

			if (foldoutStates.ContainsKey(property.propertyPath) && foldoutStates[property.propertyPath])
			{
				// Two additional lines: Group and Asset
				height += 2 * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
			}

			return height;
		}

		/// <summary>
		/// Retrieves asset names for a given group from ThreadlinkPreferences.
		/// Includes "None" as the first option.
		/// Caches the results to improve performance.
		/// </summary>
		/// <param name="group">The addressable group.</param>
		/// <returns>An array of asset names with "None" as the first option.</returns>
		private string[] GetAssetNamesForGroup(ThreadlinkAddressableGroup group)
		{
			// Initialize the list with "None" as the first option
			var names = new List<string> { "None" };

			var preferences = ThreadlinkPreferencesUtility.Preferences;

			if (preferences != null)
			{
				// Check assetDatabase first
				if (preferences.assetDatabase.TryGetValue(group, out var assetRefs))
				{
					foreach (var assetRef in assetRefs)
					{
						string assetName = assetRef.editorAsset != null ? assetRef.editorAsset.name : "Invalid Asset Entry";
						names.Add(assetName);
					}
				}

				// Check prefabDatabase
				if (preferences.prefabDatabase.TryGetValue(group, out var prefabRefs))
				{
					foreach (var prefabRef in prefabRefs)
					{
						string prefabName = prefabRef.editorAsset != null ? prefabRef.editorAsset.name : "Invalid Prefab Entry";
						names.Add(prefabName);
					}
				}
			}

			return names.ToArray();
		}

		/// <summary>
		/// Retrieves the editor asset for a given group and index in the database.
		/// </summary>
		/// <param name="group">The addressable group.</param>
		/// <param name="index">The selected index in the database.</param>
		/// <returns>The corresponding editor asset if valid; otherwise, null.</returns>
		private Object GetEditorAssetForGroupAndIndex(ThreadlinkAddressableGroup group, int index)
		{
			if (index < 0)
				return null; // "None" is represented by -1

			var preferences = ThreadlinkPreferencesUtility.Preferences;

			if (preferences == null)
				return null;

			// Check assetDatabase first
			if (preferences.assetDatabase.TryGetValue(group, out var assetRefs))
			{
				if (index < assetRefs.Length)
					return assetRefs[index].editorAsset;

				index -= assetRefs.Length;
			}

			// Check prefabDatabase
			if (preferences.prefabDatabase.TryGetValue(group, out var prefabRefs))
			{
				if (index < prefabRefs.Length)
					return prefabRefs[index].editorAsset;
			}

			return null;
		}
	}
}
