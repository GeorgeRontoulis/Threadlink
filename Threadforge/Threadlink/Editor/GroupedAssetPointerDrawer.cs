namespace Threadlink.Editor
{
    using Addressables;
    using Collections.Extensions;
    using Core;
    using Shared;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Utilities.Collections;

    [CustomPropertyDrawer(typeof(GroupedAssetPointer))]
    internal sealed class GroupedAssetPointerDrawer : PropertyDrawer
    {
        // Cache to store foldout states based on property paths
        private static readonly Dictionary<string, bool> foldoutStates = new(1);
        private static readonly List<string> groupsBuffer = new(1);

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

                if (groupProp == null)
                {
                    EditorGUI.LabelField(position, label.text, "Missing 'group' field");
                    EditorGUI.EndProperty();
                    return;
                }

                AssetGroups currentGroup;
                switch (groupProp.propertyType)
                {
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.Integer:
                        currentGroup = (AssetGroups)groupProp.intValue;
                        break;

                    default:
                        EditorGUI.LabelField(position, label.text, $"Unsupported 'group' type: {groupProp.propertyType}");
                        EditorGUI.EndProperty();
                        return;
                }

                // Access the 'indexInDatabase' property
                var indexPropAsset = property.FindPropertyRelative("indexInDatabase");
                int currentIndexInDatabase = indexPropAsset.intValue;

                // Map indexInDatabase to dropdown index
                int dropdownIndex = currentIndexInDatabase >= 0 ? currentIndexInDatabase + 1 : 0;
                float xPos = position.x + 15;

                // Define Rects for group and asset dropdowns
                var groupLabelRect = new Rect(xPos, yOffset, 60, lineHeight); // Indent label by 15
                var groupFieldRect = new Rect(xPos + 65, yOffset, position.width - 15 - 65 - Spacing, lineHeight);

                yOffset += lineHeight + EditorGUIUtility.standardVerticalSpacing;

                var assetLabelRect = new Rect(xPos, yOffset, 60, lineHeight);
                var assetFieldRect = new Rect(xPos + 65, yOffset, position.width - 15 - 65 - Spacing, lineHeight);

                // Draw the 'Group' label and dropdown
                EditorGUI.LabelField(groupLabelRect, "Group");
                EditorGUI.BeginChangeCheck();

                var selectedGroup = (AssetGroups)EditorGUI.EnumPopup(groupFieldRect, GUIContent.none, currentGroup);
                if (EditorGUI.EndChangeCheck())
                {
                    groupProp.intValue = (int)selectedGroup;
                    indexPropAsset.intValue = -1;
                }

                // Draw the 'Asset' label
                EditorGUI.LabelField(assetLabelRect, "Asset");

                // Store the original GUI color
                var originalColor = GUI.color;

                // Set GUI.color based on asset validity
                if (dropdownIndex == 0)
                {
                    // "None" selected - Invalid selection
                    GUI.color = Color.red;
                }
                else if (TryGetResource(currentGroup, currentIndexInDatabase, out _))
                {
                    // Valid asset selected
                    GUI.color = Color.green;
                }
                else
                {
                    // Invalid asset selection
                    GUI.color = Color.red;
                }

                // Retrieve asset names based on the selected group, including "None"
                var assetNames = GetAssetNamesForGroup(currentGroup);

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
        private string[] GetAssetNamesForGroup(AssetGroups group)
        {
            groupsBuffer.Clear();
            groupsBuffer.Add("None");

            if (ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkUserConfig userData))
            {
                // Check assetDatabase first
                if (userData.Assets.TryGetValue(group, out var assetRefs) && assetRefs != null)
                {
                    foreach (var assetRef in assetRefs)
                    {
                        if (assetRef != null && assetRef.editorAsset != null)
                            groupsBuffer.Add(assetRef.editorAsset.name);
                    }
                }

                // Check prefabDatabase
                if (userData.Prefabs.TryGetValue(group, out var prefabRefs) && prefabRefs != null)
                {
                    foreach (var prefabRef in prefabRefs)
                    {
                        if (prefabRef == null && prefabRef.editorAsset != null)
                            groupsBuffer.Add(prefabRef.editorAsset.name);
                    }
                }
            }

            return groupsBuffer.ToArray();
        }

        /// <summary>
        /// Retrieves the editor asset for a given group and index in the database.
        /// </summary>
        /// <param name="group">The addressable group.</param>
        /// <param name="index">The selected index in the database.</param>
        /// <returns>The corresponding editor asset if valid; otherwise, null.</returns>
        private bool TryGetResource(AssetGroups group, int index, out Object result)
        {
            result = null;

            if (index < 0 || !ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkUserConfig userData))
                return false;

            // Check assetDatabase first
            if (userData.Assets.TryGetValue(group, out var assetRefs) && assetRefs != null)
            {
                if (index.IsWithinBoundsOf(assetRefs))
                {
                    result = assetRefs[index].editorAsset;
                    return result != null;
                }

                index -= assetRefs.Length;
            }

            // Check prefabDatabase
            if (userData.Prefabs.TryGetValue(group, out var prefabRefs) && prefabRefs != null)
            {
                if (index.IsWithinBoundsOf(prefabRefs))
                {
                    result = prefabRefs[index].editorAsset;
                    return result != null;
                }
            }

            return false;
        }
    }
}
