﻿#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SerializeReferenceInspectorButton
{
	public static readonly Color SerializedReferenceMenuBackgroundColor = new Color(0.1f, 0.55f, 0.9f, 1f);

	/// Must be drawn before DefaultProperty in order to receive input
	public static void DrawSelectionButtonForManagedReference(this SerializedProperty property, Rect position, IEnumerable<Func<Type, bool>> filters = null) =>
		property.DrawSelectionButtonForManagedReference(position, SerializedReferenceMenuBackgroundColor, filters);

	/// Must be drawn before DefaultProperty in order to receive input
	public static void DrawSelectionButtonForManagedReference(this SerializedProperty property,
	Rect position, Color color, IEnumerable<Func<Type, bool>> filters = null)
	{
		var backgroundColor = color;

		var buttonPosition = position;
		buttonPosition.x += EditorGUIUtility.labelWidth + 1 * EditorGUIUtility.standardVerticalSpacing;
		buttonPosition.width = position.width - EditorGUIUtility.labelWidth - 1 * EditorGUIUtility.standardVerticalSpacing;
		buttonPosition.height = EditorGUIUtility.singleLineHeight;

		var storedIndent = EditorGUI.indentLevel;
		EditorGUI.indentLevel = 0;
		var storedColor = GUI.backgroundColor;
		GUI.backgroundColor = backgroundColor;


		var (AssemblyName, ClassName) = ManagedReferenceUtility.GetSplitNamesFromTypename(property.managedReferenceFullTypename);
		var className = string.IsNullOrEmpty(ClassName) ? "Null (Assign)" : ClassName;
		var assemblyName = AssemblyName;

		int i = className.LastIndexOf('.');
		string last = i < 0 ? className : className[(i + 1)..];
		if (GUI.Button(buttonPosition, new GUIContent(last, className + "  ( " + assemblyName + " )")))
			property.ShowContextMenuForManagedReference(buttonPosition, filters);

		GUI.backgroundColor = storedColor;
		EditorGUI.indentLevel = storedIndent;
	}
}

#endif