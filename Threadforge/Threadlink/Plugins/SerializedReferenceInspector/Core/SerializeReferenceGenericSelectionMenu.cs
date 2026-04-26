#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SerializeReferenceGenericSelectionMenu
{
    public static void ShowContextMenuForManagedReference(this SerializedProperty property, Rect position, IEnumerable<Func<Type, bool>> filters = null)
    {
        var context = new GenericMenu();
        FillContextMenu(filters, context, property);
        context.DropDown(position);
    }

    public static void ShowContextMenuForManagedReference(this SerializedProperty property, IEnumerable<Func<Type, bool>> filters = null)
    {
        var context = new GenericMenu();
        FillContextMenu(filters, context, property);
        context.ShowAsContext();
    }

    private static void FillContextMenu(IEnumerable<Func<Type, bool>> enumerableFilters, GenericMenu contextMenu, SerializedProperty property)
    {
        var filters = enumerableFilters.ToList();

        contextMenu.AddItem(new GUIContent("Null"), false, property.SetManagedReferenceToNull);

        var appropriateTypes = property.GetAppropriateTypesForAssigningToManagedReference(filters);

        foreach (var appropriateType in appropriateTypes)
            AddItemToContextMenu(appropriateType, contextMenu, property);
    }

    private static void AddItemToContextMenu(Type type, GenericMenu genericMenuContext, SerializedProperty property)
    {
        var assemblyName = type.Assembly.GetName().Name;
        // Utilize the new formatting logic
        var formattedName = ManagedReferenceUtility.GetTypeName(type);

        // Use standard dot notation for sub-menus
        var entryName = formattedName.Replace('+', '.') + "  ( " + assemblyName + " )";

        genericMenuContext.AddItem(new GUIContent(entryName), false, AssignNewInstanceCommand, new GenericMenuParameterForAssignInstanceCommand(type, property));
    }

    private static void AssignNewInstanceCommand(object objectGenericMenuParameter)
    {
        var parameter = (GenericMenuParameterForAssignInstanceCommand)objectGenericMenuParameter;
        var type = parameter.Type;
        var property = parameter.Property;
        property.AssignNewInstanceOfTypeToManagedReference(type);
    }

    private readonly struct GenericMenuParameterForAssignInstanceCommand
    {
        public GenericMenuParameterForAssignInstanceCommand(Type type, SerializedProperty property)
        {
            Type = type;
            Property = property;
        }

        public readonly SerializedProperty Property;
        public readonly Type Type;
    }
}

#endif