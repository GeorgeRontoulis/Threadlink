#if ODIN_INSPECTOR
namespace Threadlink.Editor
{
    using Sirenix.OdinInspector;
    using Sirenix.OdinInspector.Editor;
    using Sirenix.Utilities.Editor;
    using System;
    using System.Collections.Generic;
    using Threadlink.Collections;
    using UnityEditor;
    using UnityEngine;

    // --- 1. THE RECURSIVE LABEL PROCESSOR ---
    // This runs automatically when Odin builds the inspector tree.
    public class SerializeReferenceHideLabelProcessor : OdinAttributeProcessor<object>
    {
        public override bool CanProcessSelfAttributes(InspectorProperty property)
        {
            // Only intercept fields that actually have the [SerializeReference] attribute attached
            return property.Info.GetAttribute<SerializeReference>() != null;
        }

        public override void ProcessSelfAttributes(InspectorProperty property, List<Attribute> attributes)
        {
            InspectorProperty parent = property.Parent;
            bool isInsideDictionary = false;

            // Walk up the tree to see if this field lives anywhere inside our ThreadlinkHashMap
            while (parent != null)
            {
                var type = parent.ValueEntry?.TypeOfValue;
                if (type != null)
                {
                    Type baseType = type;
                    while (baseType != null && baseType != typeof(object))
                    {
                        if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ThreadlinkHashMap<,>))
                        {
                            isInsideDictionary = true;
                            break;
                        }
                        baseType = baseType.BaseType;
                    }
                }

                if (isInsideDictionary) break;
                parent = parent.Parent;
            }

            // If it's inside the dictionary, dynamically inject [HideLabel] 
            // so Odin natively strips the label at ANY nested depth!
            if (isInsideDictionary)
            {
                attributes.Add(new HideLabelAttribute());
            }
        }
    }

    // --- 2. THE DICTIONARY DRAWER ---
    [DrawerPriority(0.0, 0.0, 1.0)]
    public class ThreadlinkHashMapOdinDrawer<TMap, TKey, TValue> : OdinValueDrawer<TMap>
        where TMap : ThreadlinkHashMap<TKey, TValue>
    {
        private InspectorProperty keysProp;
        private InspectorProperty valuesProp;
        private InspectorProperty countProp;
        private string searchString = string.Empty;

        // --- Drag & Drop State ---
        private int dragIndex = -1;
        private bool isDragging = false;

        protected override void Initialize()
        {
            keysProp = this.Property.Children.Get("keys");
            valuesProp = this.Property.Children.Get("values");
            countProp = this.Property.Children.Get("count");
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (keysProp == null || valuesProp == null || countProp == null)
            {
                SirenixEditorGUI.ErrorMessageBox($"Odin Dictionary Binding Failed.");
                CallNextDrawer(label);
                return;
            }

            var so = this.Property.Tree.UnitySerializedObject;
            string basePath = this.Property.UnityPropertyPath;

            SerializedProperty uCount = so?.FindProperty(basePath + ".count");
            SerializedProperty uKeys = so?.FindProperty(basePath + ".keys");
            SerializedProperty uValues = so?.FindProperty(basePath + ".values");

            if (uCount == null || uKeys == null || uValues == null)
            {
                SirenixEditorGUI.ErrorMessageBox("Failed to resolve underlying Unity arrays. Ensure the object is serialized properly.");
                CallNextDrawer(label);
                return;
            }

            int count = countProp.ValueEntry.WeakSmartValue is int c ? c : 0;

            SirenixEditorGUI.BeginBox();

            // --- TOOLBAR HEADER ---
            SirenixEditorGUI.BeginToolbarBoxHeader();
            GUILayout.BeginHorizontal();

            GUILayout.Label(label ?? new GUIContent(this.Property.NiceName), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            searchString = SirenixEditorGUI.ToolbarSearchField(searchString);

            GUILayout.EndHorizontal();
            SirenixEditorGUI.EndToolbarBoxHeader();

            string searchLower = searchString.ToLowerInvariant();
            bool isSearching = !string.IsNullOrEmpty(searchString);

            SirenixEditorGUI.BeginVerticalList();

            // --- DICTIONARY ROWS ---
            for (int i = 0; i < count; i++)
            {
                if (i >= keysProp.Children.Count || i >= valuesProp.Children.Count) break;

                InspectorProperty keyChild = keysProp.Children[i];
                InspectorProperty valueChild = valuesProp.Children[i];

                if (isSearching)
                {
                    string keyStr = keyChild.ValueEntry?.WeakSmartValue?.ToString() ?? "";
                    if (!keyStr.ToLowerInvariant().Contains(searchLower)) continue;
                }

                SirenixEditorGUI.BeginListItem();
                Rect rowRect = EditorGUILayout.BeginHorizontal();

                // --- DRAG HANDLE LOGIC ---
                if (!isSearching)
                {
                    Rect dragRect = GUILayoutUtility.GetRect(20, 22, GUILayout.ExpandHeight(false));
                    dragRect.y += 2;
                    GUI.Label(dragRect, "\u2630", EditorStyles.centeredGreyMiniLabel);

                    if (Event.current.type == EventType.Repaint && isDragging && dragIndex == i)
                    {
                        EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.5f, 1f, 0.2f));
                    }

                    if (Event.current.type == EventType.MouseDown && dragRect.Contains(Event.current.mousePosition))
                    {
                        dragIndex = i;
                        isDragging = true;
                        Event.current.Use();
                    }

                    if (isDragging && Event.current.type == EventType.MouseDrag)
                    {
                        if (rowRect.Contains(Event.current.mousePosition) && dragIndex != i && dragIndex != -1)
                        {
                            uKeys.MoveArrayElement(dragIndex, i);
                            uValues.MoveArrayElement(dragIndex, i);
                            so.ApplyModifiedProperties();

                            dragIndex = i;
                            GUI.changed = true;
                            Event.current.Use();

                            this.Property.Tree.DelayActionUntilRepaint(() => this.Property.Tree.UpdateTree());
                        }
                    }
                }

                // 1. KEY COLUMN
                GUILayout.BeginVertical(GUILayout.Width(140));
                GUIHelper.PushHierarchyMode(false);
                keyChild.Draw(GUIContent.none);
                GUIHelper.PopHierarchyMode();
                GUILayout.EndVertical();

                SirenixEditorGUI.VerticalLineSeparator();

                // 2. VALUE COLUMN
                GUILayout.BeginVertical();
                GUIHelper.PushLabelWidth(120);

                bool shouldUnpack = valueChild.Children.Count > 0;
                Type valType = valueChild.ValueEntry?.TypeOfValue;

                if (shouldUnpack && valType != null)
                {
                    // 1. Prevent destructive unpacking of standard collections, strings, and Unity Objects
                    if (valType == typeof(string) ||
                        typeof(System.Collections.IEnumerable).IsAssignableFrom(valType) ||
                        typeof(UnityEngine.Object).IsAssignableFrom(valType))
                    {
                        shouldUnpack = false;
                    }
                    else
                    {
                        // 2. Prevent destructive unpacking of our nested dictionaries
                        Type t = valType;
                        while (t != null && t != typeof(object))
                        {
                            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ThreadlinkHashMap<,>))
                            {
                                shouldUnpack = false;
                                break;
                            }
                            t = t.BaseType;
                        }
                    }
                }

                if (shouldUnpack)
                {
                    for (int j = 0; j < valueChild.Children.Count; j++)
                        valueChild.Children[j].Draw();
                }
                else valueChild.Draw(GUIContent.none);

                GUIHelper.PopLabelWidth();
                GUILayout.EndVertical();

                // 3. REMOVE BUTTON
                GUILayout.Space(4);
                GUILayout.BeginVertical(GUILayout.Width(22));
                if (SirenixEditorGUI.IconButton(EditorIcons.X))
                {
                    DeleteRow(i, uKeys, uValues, uCount, so);
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                    SirenixEditorGUI.EndListItem();
                    break;
                }
                GUILayout.EndVertical();
                GUILayout.Space(2);

                GUILayout.EndHorizontal();
                SirenixEditorGUI.EndListItem();
            }

            SirenixEditorGUI.EndVerticalList();

            if (Event.current.rawType == EventType.MouseUp)
            {
                isDragging = false;
                dragIndex = -1;
            }

            GUILayout.Space(6f);

            // --- ADD NEW BUTTON FOOTER ---
            SirenixEditorGUI.BeginToolbarBoxHeader();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (SirenixEditorGUI.IconButton(EditorIcons.Plus))
            {
                AddRow(uKeys, uValues, uCount, so);
            }

            GUILayout.EndHorizontal();
            SirenixEditorGUI.EndToolbarBoxHeader();

            SirenixEditorGUI.EndBox();
        }

        private void AddRow(SerializedProperty uKeys, SerializedProperty uValues, SerializedProperty uCount, SerializedObject so)
        {
            uCount.intValue++;

            if (uKeys != null && uValues != null)
            {
                uKeys.arraySize = uCount.intValue;
                uValues.arraySize = uCount.intValue;
                so.ApplyModifiedProperties();
            }

            this.Property.Tree.DelayActionUntilRepaint(() => this.Property.Tree.UpdateTree());
        }

        private void DeleteRow(int index, SerializedProperty uKeys, SerializedProperty uValues, SerializedProperty uCount, SerializedObject so)
        {
            if (uKeys != null && uValues != null && uCount != null)
            {
                int originalSize = uKeys.arraySize;
                uKeys.DeleteArrayElementAtIndex(index);
                if (uKeys.arraySize == originalSize) uKeys.DeleteArrayElementAtIndex(index);

                originalSize = uValues.arraySize;
                uValues.DeleteArrayElementAtIndex(index);
                if (uValues.arraySize == originalSize) uValues.DeleteArrayElementAtIndex(index);

                uCount.intValue--;
                so.ApplyModifiedProperties();
            }

            this.Property.Tree.DelayActionUntilRepaint(() => this.Property.Tree.UpdateTree());
        }
    }
}
#endif