#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ManagedReferenceUtility
{
    public static object AssignNewInstanceOfTypeToManagedReference(this SerializedProperty serializedProperty, Type type)
    {
        var instance = Activator.CreateInstance(type);

        serializedProperty.serializedObject.Update();
        serializedProperty.managedReferenceValue = instance;
        serializedProperty.serializedObject.ApplyModifiedProperties();

        return instance;
    }

    public static void SetManagedReferenceToNull(this SerializedProperty serializedProperty)
    {
        serializedProperty.serializedObject.Update();
        serializedProperty.managedReferenceValue = null;
        serializedProperty.serializedObject.ApplyModifiedProperties();
    }

    public static IEnumerable<Type> GetAppropriateTypesForAssigningToManagedReference(this SerializedProperty property, List<Func<Type, bool>> filters = null)
    {
        var fieldType = property.GetManagedReferenceFieldType();
        return GetAppropriateTypesForAssigningToManagedReference(fieldType, filters);
    }

    public static IEnumerable<Type> GetAppropriateTypesForAssigningToManagedReference(Type fieldType, List<Func<Type, bool>> filters = null)
    {
        var appropriateTypes = new List<Type>();
        var derivedTypes = new List<Type>();

        // 1. Get standard derived types
        derivedTypes.AddRange(TypeCache.GetTypesDerivedFrom(fieldType));

        // 2. If the field is generic, we must also grab types that derive from the open generic definition
        // (e.g., catching 'ConcreteRef<T> : GenericRef<T>' when the field is GenericRef<int>)
        if (fieldType != null && fieldType.IsGenericType)
        {
            var openBaseType = fieldType.GetGenericTypeDefinition();
            var openDerivedTypes = TypeCache.GetTypesDerivedFrom(openBaseType);
            foreach (var openDerived in openDerivedTypes)
            {
                if (!derivedTypes.Contains(openDerived))
                    derivedTypes.Add(openDerived);
            }
        }

        foreach (var type in derivedTypes)
        {
            if (type.IsSubclassOf(typeof(Object))) continue;
            if (type.IsAbstract) continue;

            Type typeToInstantiate = type;

            // 3. Dynamically close open generic types
            if (type.ContainsGenericParameters)
            {
                if (fieldType != null && fieldType.IsGenericType)
                {
                    try
                    {
                        typeToInstantiate = type.MakeGenericType(fieldType.GetGenericArguments());

                        // Ensure the resulting closed type actually matches the field type
                        if (!fieldType.IsAssignableFrom(typeToInstantiate))
                            continue;
                    }
                    catch
                    {
                        continue; // Failsafe if generic constraints don't match
                    }
                }
                else
                {
                    continue; // Can't close an open generic without a generic base to infer from
                }
            }

            if (typeToInstantiate.IsClass && typeToInstantiate.GetConstructor(Type.EmptyTypes) == null)
                continue;

            if (filters != null && filters.All(f => f == null || f.Invoke(typeToInstantiate)) == false)
                continue;

            if (!appropriateTypes.Contains(typeToInstantiate))
                appropriateTypes.Add(typeToInstantiate);
        }

        return appropriateTypes;
    }

    public static Type GetManagedReferenceFieldType(this SerializedProperty property)
    {
        var realPropertyType = GetRealTypeFromTypename(property.managedReferenceFieldTypename);
        if (realPropertyType != null)
            return realPropertyType;

        Debug.LogError($"Can not get field type of managed reference : {property.managedReferenceFieldTypename}");
        return null;
    }

    public static Type GetRealTypeFromTypename(string stringType)
    {
        var (AssemblyName, ClassName) = GetSplitNamesFromTypename(stringType);
        if (string.IsNullOrEmpty(AssemblyName) || string.IsNullOrEmpty(ClassName)) return null;

        var realType = Type.GetType($"{ClassName}, {AssemblyName}");
        if (realType != null)
            return realType;

        // Fallback for tricky assembly resolutions
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == AssemblyName)
            {
                realType = assembly.GetType(ClassName);
                if (realType != null)
                    return realType;
            }
        }

        return null;
    }

    public static (string AssemblyName, string ClassName) GetSplitNamesFromTypename(string typename)
    {
        if (string.IsNullOrEmpty(typename))
            return (string.Empty, string.Empty);

        // Safely split by the FIRST space to avoid breaking on spaces inside generic brackets
        int spaceIndex = typename.IndexOf(' ');
        if (spaceIndex < 0)
            return (string.Empty, string.Empty);

        var typeAssemblyName = typename.Substring(0, spaceIndex);
        var typeClassName = typename.Substring(spaceIndex + 1);

        // Type.GetType demands '+' for nested classes, while Unity's string uses '/'
        typeClassName = typeClassName.Replace('/', '+');

        return (typeAssemblyName, typeClassName);
    }

    /// Recursively formats generic types for beautiful UI labels
    public static string GetTypeName(Type type)
    {
        if (type == null) return "Null";
        if (!type.IsGenericType) return type.Name.Replace('+', '.');

        var typeName = type.Name.Split('`')[0];
        var genericArgs = type.GetGenericArguments();
        var argNames = new string[genericArgs.Length];
        for (int i = 0; i < genericArgs.Length; i++)
        {
            argNames[i] = GetTypeName(genericArgs[i]);
        }

        return $"{typeName}<{string.Join(", ", argNames)}>";
    }
}
#endif