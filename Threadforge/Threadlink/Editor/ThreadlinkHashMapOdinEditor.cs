namespace Threadlink.Editor
{
    using Sirenix.OdinInspector;
    using Sirenix.OdinInspector.Editor;
    using Sirenix.Utilities.Editor;
    using System;
    using System.Collections.Generic;
    using Threadlink.Collections;
    using Threadlink.Utilities.Attributes;
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

    /// <summary>
    /// Routes fields marked <see cref="HashMapDrawerMode.Native"/> to Threadlink's own
    /// <see cref="PropertyDrawer"/> by injecting Odin's own opt-out attribute.
    /// </summary>
    public sealed class HashMapDrawerModeProcessor : OdinAttributeProcessor<object>
    {
        public override bool CanProcessSelfAttributes(InspectorProperty property)
        {
            return property.Info.GetAttribute<HashMapDrawerAttribute>() is HashMapDrawerAttribute attribute
            && attribute.Mode is HashMapDrawerMode.Native;
        }

        public override void ProcessSelfAttributes(InspectorProperty property, List<Attribute> attributes)
        {
            attributes.Add(new DrawWithUnityAttribute());
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
        private bool readOnly = false;

        protected override bool CanDrawValueProperty(InspectorProperty property)
        {
            var attribute = property.Info.GetAttribute<HashMapDrawerAttribute>();

            return attribute == null || attribute.Mode is not HashMapDrawerMode.Native;
        }

        protected override void Initialize()
        {
            readOnly = Property.Info.GetAttribute<Utilities.Attributes.ReadOnlyAttribute>() != null
            || Property.Info.GetAttribute<Sirenix.OdinInspector.ReadOnlyAttribute>() != null;

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

                if (!isSearching && !readOnly)
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

                            // Reordering shifts every key between the drag source and drop
                            // target by one position - the bucket chains still point at the
                            // old positions, so without a rebuild a lookup could resolve to
                            // a neighboring key's value from this point on.
                            this.ValueEntry.SmartValue.OnAfterDeserialize();

                            dragIndex = i;
                            GUI.changed = true;
                            Event.current.Use();

                            this.Property.Tree.DelayActionUntilRepaint(() => this.Property.Tree.UpdateTree());
                        }
                    }
                }

                GUILayout.BeginVertical(GUILayout.Width(140));

                if (readOnly)
                {
                    GUILayout.Label(keyChild.ValueEntry?.WeakSmartValue?.ToString() ?? string.Empty);
                }
                else
                {
                    GUIHelper.PushHierarchyMode(false);
                    keyChild.Draw(GUIContent.none);
                    GUIHelper.PopHierarchyMode();
                }

                GUILayout.EndVertical();

                SirenixEditorGUI.VerticalLineSeparator();

                GUILayout.BeginVertical();
                GUIHelper.PushLabelWidth(120);
                EditorGUI.BeginDisabledGroup(readOnly);

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

                EditorGUI.EndDisabledGroup();
                GUIHelper.PopLabelWidth();
                GUILayout.EndVertical();

                if (readOnly is false)
                {
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
                }

                GUILayout.EndHorizontal();
                SirenixEditorGUI.EndListItem();
            }

            SirenixEditorGUI.EndVerticalList();

            if (Event.current.rawType == EventType.MouseUp)
            {
                isDragging = false;
                dragIndex = -1;
            }

            if (readOnly is false)
            {
                GUILayout.Space(6f);

                SirenixEditorGUI.BeginToolbarBoxHeader();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (SirenixEditorGUI.IconButton(EditorIcons.Plus))
                    AddRow(uKeys, uValues, uCount, so);
                GUILayout.EndHorizontal();
                SirenixEditorGUI.EndToolbarBoxHeader();
            }

            SirenixEditorGUI.EndBox();
        }

        private void AddRow(SerializedProperty uKeys, SerializedProperty uValues, SerializedProperty uCount, SerializedObject so)
        {
            uCount.intValue++;
            uKeys.arraySize = uCount.intValue;
            uValues.arraySize = uCount.intValue;

            int newIndex = uCount.intValue - 1;

            DetachManagedReferenceAliases(uKeys.GetArrayElementAtIndex(newIndex));
            DetachManagedReferenceAliases(uValues.GetArrayElementAtIndex(newIndex));

            so.ApplyModifiedProperties();

            // Rebuild the hash lookup tables against the current, post-add state immediately -
            // ValueEntry.SmartValue is the same live TMap instance ApplyModifiedProperties just
            // wrote the grown arrays into, so this doesn't wait on Unity's next incidental
            // deserialize pass to make the new row findable via TryGetValue/ContainsKey.
            this.ValueEntry.SmartValue.OnAfterDeserialize();

            this.Property.Tree.DelayActionUntilRepaint(() => this.Property.Tree.UpdateTree());
        }

        /// <summary>
        /// Growing an array via SerializedProperty.arraySize duplicates the previous last
        /// element's serialized data into the new slot. For plain fields that's just a value
        /// copy - harmless, the new row can be edited independently afterwards. But for any
        /// [SerializeReference] field caught up in that duplication - whether it's the array's
        /// own element type (as in RefHashMap) or nested arbitrarily deep inside a plain value's
        /// own fields (as in FieldHashMap, e.g. a RefList/RefArray tucked inside a serializable
        /// class like a settings Section holding a Widgets collection) - the "copy" is actually
        /// an alias: both slots end up pointing at the exact same managed object, so editing the
        /// new row's nested collection silently edits the old row's too.
        /// This walks the newly added element's entire property subtree and gives every managed
        /// reference found a fresh, independent instance, severing any alias left by the duplication.
        /// </summary>
        private static void DetachManagedReferenceAliases(SerializedProperty element)
        {
            if (element == null) return;

            DetachIfManagedReference(element);

            var iterator = element.Copy();
            var end = element.GetEndProperty();

            while (iterator.NextVisible(true) && !SerializedProperty.EqualContents(iterator, end))
                DetachIfManagedReference(iterator);
        }

        private static void DetachIfManagedReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference) return;
            if (property.managedReferenceValue == null) return;

            var concreteType = property.managedReferenceValue.GetType();

            try
            {
                property.managedReferenceValue = Activator.CreateInstance(concreteType);
            }
            catch (MissingMethodException)
            {
                Debug.LogWarning($"[ThreadlinkHashMap] '{concreteType.Name}' has no parameterless constructor, " +
                                  "so a newly added row could not be safely detached from the previous row's " +
                                  "reference. Please assign this field manually.");
            }
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

            // Every key at an index after the deleted one just shifted down by one, which
            // invalidates the bucket chains built from the old positions - rebuild them
            // against the current, post-delete state immediately.
            this.ValueEntry.SmartValue.OnAfterDeserialize();

            this.Property.Tree.DelayActionUntilRepaint(() => this.Property.Tree.UpdateTree());
        }
    }
}