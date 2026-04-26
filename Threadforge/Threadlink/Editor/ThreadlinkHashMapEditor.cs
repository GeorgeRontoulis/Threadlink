namespace Threadlink.Editor
{
    using System.Collections.Generic;
    using Threadlink.Collections;
    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(ThreadlinkHashMap<,>), true)]
    public class ThreadlinkHashMapDrawer : PropertyDrawer
    {
        // Increased slightly to 0.45f to give complex struct keys room to breathe
        private const float KeyWidthPercentage = 0.45f;
        private const float RemoveButtonWidth = 25f;
        private const float DragHandleWidth = 20f;
        private const float Padding = 6f;
        private const float MaxScrollHeight = 300f;

        private class DrawerState
        {
            public string searchString = string.Empty;
            public Vector2 scrollPosition = Vector2.zero;
            public int dragIndex = -1;
            public bool isDragging = false;
        }

        private static readonly Dictionary<string, DrawerState> States = new(1);
        private static readonly Dictionary<string, int> KeyFrequencies = new(64);

        private DrawerState GetState(string propertyPath)
        {
            if (!States.TryGetValue(propertyPath, out DrawerState state))
            {
                state = new DrawerState();
                States[propertyPath] = state;
            }
            return state;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty countProp = property.FindPropertyRelative("count");
            SerializedProperty keysProp = property.FindPropertyRelative("keys");
            SerializedProperty valuesProp = property.FindPropertyRelative("values");

            DrawerState state = GetState(property.propertyPath);
            int count = countProp.intValue;

            float nonContentHeight = (Padding * 2)
                                   + (EditorGUIUtility.singleLineHeight * 3)
                                   + (EditorGUIUtility.standardVerticalSpacing * 4);

            if (valuesProp.arraySize < count) return nonContentHeight;

            float contentHeight = 0f;
            string searchLower = state.searchString.ToLowerInvariant();

            for (int i = 0; i < count; i++)
            {
                SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(i);
                if (IsSearchMatch(keyProp, searchLower))
                {
                    // Calculate height for BOTH key and value, as the Key might be a multi-line struct
                    float keyHeight = EditorGUI.GetPropertyHeight(keyProp, true);
                    float valueHeight = EditorGUI.GetPropertyHeight(valuesProp.GetArrayElementAtIndex(i), true);

                    float rowHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, Mathf.Max(keyHeight, valueHeight));
                    contentHeight += rowHeight + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            float visibleHeight = Mathf.Min(contentHeight, MaxScrollHeight);
            if (contentHeight > 0f) visibleHeight += 2f;

            return nonContentHeight + visibleHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty keysProp = property.FindPropertyRelative("keys");
            SerializedProperty valuesProp = property.FindPropertyRelative("values");
            SerializedProperty countProp = property.FindPropertyRelative("count");

            DrawerState state = GetState(property.propertyPath);
            int count = countProp.intValue;

            if (keysProp.arraySize < count) keysProp.arraySize = count;
            if (valuesProp.arraySize < count) valuesProp.arraySize = count;

            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            float currentY = position.y + Padding;
            float availableWidth = position.width - (Padding * 2);
            float innerX = position.x + Padding;

            var titleRect = new Rect(innerX, currentY, availableWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(titleRect, label, EditorStyles.boldLabel);
            currentY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            var searchRect = new Rect(innerX, currentY, availableWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            string newSearch = EditorGUI.TextField(searchRect, string.Empty, state.searchString, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                state.searchString = newSearch;
            }
            currentY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // --- DUPLICATE DETECTION PASS ---
            KeyFrequencies.Clear();
            for (int i = 0; i < count; i++)
            {
                string keyString = GetKeyString(keysProp.GetArrayElementAtIndex(i));
                if (!KeyFrequencies.TryAdd(keyString, 1))
                {
                    KeyFrequencies[keyString]++;
                }
            }

            float contentHeight = 0f;
            string searchLower = state.searchString.ToLowerInvariant();
            bool isSearching = !string.IsNullOrEmpty(state.searchString);

            for (int i = 0; i < count; i++)
            {
                SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(i);
                if (IsSearchMatch(keyProp, searchLower))
                {
                    float keyHeight = EditorGUI.GetPropertyHeight(keyProp, true);
                    float valueHeight = EditorGUI.GetPropertyHeight(valuesProp.GetArrayElementAtIndex(i), true);
                    contentHeight += Mathf.Max(EditorGUIUtility.singleLineHeight, Mathf.Max(keyHeight, valueHeight)) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            float visibleHeight = Mathf.Min(contentHeight, MaxScrollHeight);
            if (contentHeight > 0f) visibleHeight += 2f;

            var viewRect = new Rect(innerX, currentY, availableWidth, visibleHeight);
            float scrollContentWidth = contentHeight > visibleHeight ? availableWidth - 16f : availableWidth;
            var contentRect = new Rect(0, 0, scrollContentWidth, contentHeight);

            state.scrollPosition = GUI.BeginScrollView(viewRect, state.scrollPosition, contentRect);

            float listY = 0f;
            Event e = Event.current;

            for (int i = 0; i < count; i++)
            {
                SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(i);
                SerializedProperty valueProp = valuesProp.GetArrayElementAtIndex(i);

                if (!IsSearchMatch(keyProp, searchLower)) continue;

                float keyHeight = EditorGUI.GetPropertyHeight(keyProp, true);
                float valueHeight = EditorGUI.GetPropertyHeight(valueProp, true);
                float rowHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, Mathf.Max(keyHeight, valueHeight));

                var rowRect = new Rect(0, listY, scrollContentWidth, rowHeight);

                string currentKeyStr = GetKeyString(keyProp);
                if (KeyFrequencies.TryGetValue(currentKeyStr, out int occurrences) && occurrences > 1)
                {
                    EditorGUI.DrawRect(rowRect, Color.indianRed);
                }

                if (!isSearching)
                {
                    var dragRect = new Rect(rowRect.x, rowRect.y, DragHandleWidth, EditorGUIUtility.singleLineHeight);
                    GUI.Label(dragRect, "\u2630", EditorStyles.centeredGreyMiniLabel);

                    if (state.isDragging && state.dragIndex == i)
                    {
                        EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.5f, 1f, 0.2f));
                    }

                    if (e.type is EventType.MouseDown && dragRect.Contains(e.mousePosition))
                    {
                        state.dragIndex = i;
                        state.isDragging = true;
                        e.Use();
                    }

                    if (state.isDragging && e.type is EventType.MouseDrag)
                    {
                        if (rowRect.Contains(e.mousePosition) && state.dragIndex != i && state.dragIndex != -1)
                        {
                            keysProp.MoveArrayElement(state.dragIndex, i);
                            valuesProp.MoveArrayElement(state.dragIndex, i);
                            state.dragIndex = i;
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                }

                // 16f guarantees enough horizontal clearance for the native foldout arrow
                float innerSpacing = 16f;

                float contentSpace = rowRect.width - (isSearching ? 0 : DragHandleWidth) - RemoveButtonWidth - innerSpacing - Padding;
                float keyWidth = contentSpace * KeyWidthPercentage;
                float valueWidth = contentSpace - keyWidth;

                float startX = isSearching ? rowRect.x : rowRect.x + DragHandleWidth;

                var keyRect = new Rect(startX, rowRect.y, keyWidth, keyHeight);
                var valueRect = new Rect(keyRect.xMax + innerSpacing, rowRect.y, valueWidth, valueHeight);
                var removeRect = new Rect(valueRect.xMax + Padding, rowRect.y, RemoveButtonWidth, EditorGUIUtility.singleLineHeight);

                // Dynamically adjust label width so inner fields of structs don't get completely crushed visually
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = keyWidth * 0.4f;

                // includeChildren set to true for both fields
                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);
                EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, true);

                EditorGUIUtility.labelWidth = previousLabelWidth;

                if (GUI.Button(removeRect, "\u274C"))
                {
                    SafeDeleteArrayElement(keysProp, i);
                    SafeDeleteArrayElement(valuesProp, i);
                    countProp.intValue--;

                    if (state.dragIndex == i) state.isDragging = false;
                    break;
                }

                listY += rowHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            GUI.EndScrollView();

            if (e.rawType is EventType.MouseUp)
            {
                state.isDragging = false;
                state.dragIndex = -1;
            }

            currentY += visibleHeight + EditorGUIUtility.standardVerticalSpacing;

            float addButtonWidth = 35f;
            float clearButtonWidth = 35f;
            float totalButtonsWidth = addButtonWidth + clearButtonWidth;
            float buttonsStartX = innerX + availableWidth - totalButtonsWidth;

            var addButtonRect = new Rect(buttonsStartX, currentY, addButtonWidth, EditorGUIUtility.singleLineHeight);
            var clearButtonRect = new Rect(buttonsStartX + addButtonWidth, currentY, clearButtonWidth, EditorGUIUtility.singleLineHeight);

            if (GUI.Button(addButtonRect, "\uFF0B", EditorStyles.miniButtonLeft))
            {
                countProp.intValue++;
                keysProp.arraySize = countProp.intValue;
                valuesProp.arraySize = countProp.intValue;

                state.searchString = string.Empty;
                GUI.FocusControl(null);
                state.scrollPosition = new Vector2(0, float.MaxValue);
            }

            if (GUI.Button(clearButtonRect, "\uD83D\uDDD1", EditorStyles.miniButtonRight))
            {
                if (EditorUtility.DisplayDialog("Clear Map", "Clear this map?", "Clear", "Cancel"))
                {
                    countProp.intValue = 0;
                    keysProp.arraySize = 0;
                    valuesProp.arraySize = 0;

                    state.searchString = string.Empty;
                    GUI.FocusControl(null);
                }
            }

            EditorGUI.EndProperty();
        }

        private static readonly System.Text.StringBuilder SB = new();

        /// <summary>
        /// Recursively extracts primitive data to build a composite string representing the object.
        /// This allows structs to be searched and checked for duplicates accurately.
        /// </summary>
        private string GetKeyString(SerializedProperty prop)
        {
            if (prop.propertyType != SerializedPropertyType.Generic)
                return GetPrimitiveString(prop);

            SB.Clear();
            var iterator = prop.Copy();
            var endProperty = iterator.GetEndProperty();

            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (SerializedProperty.EqualContents(iterator, endProperty))
                    break;

                SB.Append(GetPrimitiveString(iterator)).Append("|");
                enterChildren = false; // Only iterate immediate children, not deep nested arrays
            }

            var result = SB.ToString();
            SB.Clear();
            return result;
        }

        private string GetPrimitiveString(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Enum => prop.enumNames.Length > 0 ? prop.enumNames[prop.enumValueIndex] : prop.intValue.ToString(),
                SerializedPropertyType.String => prop.stringValue,
                SerializedPropertyType.Integer => prop.intValue.ToString(),
                SerializedPropertyType.Float => prop.floatValue.ToString(),
                SerializedPropertyType.Boolean => prop.boolValue.ToString(),
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null ? prop.objectReferenceValue.name : string.Empty,
                SerializedPropertyType.Vector2 => prop.vector2Value.ToString(),
                SerializedPropertyType.Vector3 => prop.vector3Value.ToString(),
                SerializedPropertyType.Color => prop.colorValue.ToString(),
                _ => prop.propertyType.ToString()
            };
        }

        private bool IsSearchMatch(SerializedProperty enumProp, string searchString)
        {
            if (string.IsNullOrEmpty(searchString)) return true;
            return GetKeyString(enumProp).ToLowerInvariant().Contains(searchString);
        }

        private void SafeDeleteArrayElement(SerializedProperty arrayProp, int index)
        {
            int originalSize = arrayProp.arraySize;
            arrayProp.DeleteArrayElementAtIndex(index);

            if (arrayProp.arraySize == originalSize)
            {
                arrayProp.DeleteArrayElementAtIndex(index);
            }
        }
    }
}