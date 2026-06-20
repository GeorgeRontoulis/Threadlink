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

    public class SerializeReferenceHideLabelProcessor : OdinAttributeProcessor<object>
    {
        public override bool CanProcessSelfAttributes(InspectorProperty property)
        {
            return property.Info.GetAttribute<SerializeReference>() != null;
        }

        public override void ProcessSelfAttributes(InspectorProperty property, List<Attribute> attributes)
        {
            InspectorProperty parent = property.Parent;
            bool isInsideDictionary = false;

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

            if (isInsideDictionary)
                attributes.Add(new HideLabelAttribute());
        }
    }

    [DrawerPriority(0.0, 0.0, 1.0)]
    public class ThreadlinkHashMapOdinDrawer<TMap, TKey, TValue> : OdinValueDrawer<TMap>
        where TMap : ThreadlinkHashMap<TKey, TValue>
    {
        private InspectorProperty keysProp;
        private InspectorProperty valuesProp;
        private InspectorProperty countProp;
        private bool _valuesAreUnityObjects;
        private string searchString = string.Empty;

        private int dragIndex = -1;
        private bool isDragging = false;

        protected override void Initialize()
        {
            keysProp = this.Property.Children.Get("keys");
            countProp = this.Property.Children.Get("count");
            _valuesAreUnityObjects = typeof(UnityEngine.Object).IsAssignableFrom(typeof(TValue));

            if (!_valuesAreUnityObjects)
                valuesProp = this.Property.Children.Get("values");
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (keysProp == null || countProp == null)
            {
                SirenixEditorGUI.ErrorMessageBox("Odin Dictionary Binding Failed.");
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
                SirenixEditorGUI.ErrorMessageBox("Failed to resolve underlying Unity arrays.");
                CallNextDrawer(label);
                return;
            }

            int count = countProp.ValueEntry.WeakSmartValue is int c ? c : 0;

            SirenixEditorGUI.BeginBox();

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

            for (int i = 0; i < count; i++)
            {
                if (i >= keysProp.Children.Count || i >= uValues.arraySize) break;

                var keyChild = keysProp.Children[i];
                var valueChild = valuesProp?.Children[i];

                if (isSearching)
                {
                    string keyStr = keyChild.ValueEntry?.WeakSmartValue?.ToString() ?? string.Empty;
                    if (!keyStr.ToLowerInvariant().Contains(searchLower)) continue;
                }

                SirenixEditorGUI.BeginListItem();
                var rowRect = EditorGUILayout.BeginHorizontal();

                if (!isSearching)
                {
                    var dragRect = GUILayoutUtility.GetRect(20, 22, GUILayout.ExpandHeight(false));
                    dragRect.y += 2;
                    GUI.Label(dragRect, "\u2630", EditorStyles.centeredGreyMiniLabel);

                    if (Event.current.type == EventType.Repaint && isDragging && dragIndex == i)
                        EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.5f, 1f, 0.2f));

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

                GUILayout.BeginVertical(GUILayout.Width(140));
                GUIHelper.PushHierarchyMode(false);
                keyChild.Draw(GUIContent.none);
                GUIHelper.PopHierarchyMode();
                GUILayout.EndVertical();

                SirenixEditorGUI.VerticalLineSeparator();

                GUILayout.BeginVertical();
                GUIHelper.PushLabelWidth(120);

                if (_valuesAreUnityObjects)
                {
                    SerializedProperty valueProp = uValues.GetArrayElementAtIndex(i);
                    EditorGUI.BeginChangeCheck();
                    var updated = EditorGUILayout.ObjectField(
                        valueProp.objectReferenceValue, typeof(TValue), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        valueProp.objectReferenceValue = updated as UnityEngine.Object;
                        so.ApplyModifiedProperties();
                    }
                }
                else if (valueChild != null)
                {
                    bool shouldUnpack = valueChild.Children.Count > 0;
                    Type valType = valueChild.ValueEntry?.TypeOfValue;

                    if (shouldUnpack && valType != null)
                    {
                        if (valType == typeof(string)
                        || typeof(System.Collections.IEnumerable).IsAssignableFrom(valType)
                        || typeof(UnityEngine.Object).IsAssignableFrom(valType))
                        {
                            shouldUnpack = false;
                        }
                        else
                        {
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
                    else
                    {
                        valueChild.Draw(GUIContent.none);
                    }
                }

                GUIHelper.PopLabelWidth();
                GUILayout.EndVertical();

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

            SirenixEditorGUI.BeginToolbarBoxHeader();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (SirenixEditorGUI.IconButton(EditorIcons.Plus))
                AddRow(uKeys, uValues, uCount, so);
            GUILayout.EndHorizontal();
            SirenixEditorGUI.EndToolbarBoxHeader();

            SirenixEditorGUI.EndBox();
        }

        private void AddRow(SerializedProperty uKeys, SerializedProperty uValues, SerializedProperty uCount, SerializedObject so)
        {
            uCount.intValue++;
            uKeys.arraySize = uCount.intValue;
            uValues.arraySize = uCount.intValue;
            so.ApplyModifiedProperties();
            this.Property.Tree.DelayActionUntilRepaint(() => this.Property.Tree.UpdateTree());
        }

        private void DeleteRow(int index, SerializedProperty uKeys, SerializedProperty uValues,
                               SerializedProperty uCount, SerializedObject so)
        {
            int originalSize = uKeys.arraySize;
            uKeys.DeleteArrayElementAtIndex(index);
            if (uKeys.arraySize == originalSize) uKeys.DeleteArrayElementAtIndex(index);

            originalSize = uValues.arraySize;
            uValues.DeleteArrayElementAtIndex(index);
            if (uValues.arraySize == originalSize) uValues.DeleteArrayElementAtIndex(index);

            uCount.intValue--;
            so.ApplyModifiedProperties();
            this.Property.Tree.DelayActionUntilRepaint(() => this.Property.Tree.UpdateTree());
        }
    }
}
#endif