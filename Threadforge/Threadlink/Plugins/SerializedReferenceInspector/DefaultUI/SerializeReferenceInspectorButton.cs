#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SerializeReferenceInspectorButton
{
    public static readonly Color ValidBackgroundColor = new(0.1f, 0.55f, 0.9f, 1f);
    public static readonly Color UnassignedBackgroundColor = new(1f, 0f, 0.1f, 1f);

    public static void DrawSelectionButtonForManagedReference(this SerializedProperty property,
    Rect position, IEnumerable<Func<Type, bool>> filters = null)
    {
        var assignedValue = property.managedReferenceValue;
        bool assigned = assignedValue != null;

        var backgroundColor = assigned ? ValidBackgroundColor : UnassignedBackgroundColor;

        var buttonPosition = position;

        buttonPosition.height = EditorGUIUtility.singleLineHeight;

        var storedIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        var storedColor = GUI.backgroundColor;
        GUI.backgroundColor = backgroundColor;



        var className = assigned ? ManagedReferenceUtility.GetTypeName(assignedValue.GetType()) : "Unassigned";
        var assemblyName = assigned ? assignedValue.GetType().Assembly.GetName().Name : string.Empty;

        var contentString = string.IsNullOrEmpty(assemblyName) ? className : $"{className} ({assemblyName})";

        if (GUI.Button(buttonPosition, new GUIContent(className, contentString)))
            property.ShowContextMenuForManagedReference(buttonPosition, filters);

        GUI.backgroundColor = storedColor;
        EditorGUI.indentLevel = storedIndent;
    }
}

#endif