namespace Threadlink.Editor
{
    using Addressables;
    using Core;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Utilities.Collections;

    [CustomPropertyDrawer(typeof(ScenePointer))]
    internal sealed class ScenePointerDrawer : PropertyDrawer
    {
        private static readonly List<string> mapNamesBuffer = new(1);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Initialize();

            EditorGUI.BeginProperty(position, label, property);

            // Access the 'indexInDatabase' property
            var indexProp = property.FindPropertyRelative("indexInDatabase");
            int currentIndexInDatabase = indexProp.intValue;

            // Map indexInDatabase to dropdown index
            int dropdownIndex = currentIndexInDatabase >= 0 ? currentIndexInDatabase + 1 : 0;

            // Store the original GUI color
            var originalColor = GUI.color;

            GUI.color = dropdownIndex > 0 && TryGetSceneAssetAt(currentIndexInDatabase, out _) ? Color.green : Color.red;

            // Draw the scene dropdown
            EditorGUI.BeginChangeCheck();
            int selectedDropdownIndex = EditorGUI.Popup(position, label.text, dropdownIndex, mapNamesBuffer.ToArray());

            if (EditorGUI.EndChangeCheck())
                indexProp.intValue = selectedDropdownIndex == 0 ? -1 : selectedDropdownIndex - 1;

            // Reset GUI color to original
            GUI.color = originalColor;

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Initializes the sceneNames array by populating it with scene names from the database.
        /// The first entry is always "None".
        /// </summary>
        private void Initialize()
        {
            mapNamesBuffer.Clear();
            mapNamesBuffer.Add("None");

            if (ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkUserConfig database))
            {
                var scenes = database.Scenes;
                int length = scenes.Length;

                for (int i = 0; i < length; i++)
                    mapNamesBuffer.Add(scenes[i].Asset.name);
            }
        }

        private bool TryGetSceneAssetAt(int indexInDatabase, out SceneAsset result)
        {
            result = null;

            if (indexInDatabase < 0 || !ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkUserConfig database))
                return false;

            var scenes = database.Scenes;

            if (indexInDatabase.IsWithinBoundsOf(scenes))
            {
                var scene = scenes[indexInDatabase].editorAsset as SceneAsset;

                result = scene;
                return result != null;
            }

            return false;
        }
    }
}
