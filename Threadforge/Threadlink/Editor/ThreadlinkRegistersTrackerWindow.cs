#if UNITY_EDITOR
namespace Threadlink.Editor
{
    using Core;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityObject = UnityEngine.Object;

    public sealed class ThreadlinkRegistersTrackerWindow : EditorWindow
    {
        private static readonly Type RegisterOpenType = typeof(Register<,>);
        private static readonly Dictionary<Type, PropertyInfo> AccessorCache = new();

        private const string AccessorName = "EditorOnly_Registry";
        private const double RefreshInterval = 0.25;

        private readonly Dictionary<int, bool> foldouts = new();
        private Vector2 scrollPosition;
        private double lastRefreshTime;

        [MenuItem("Threadlink/Registers Tracker")]
        private static void Open()
        {
            var window = GetWindow<ThreadlinkRegistersTrackerWindow>("Registers Tracker");
            window.minSize = new Vector2(480, 320);
        }

        private void OnEnable() => EditorApplication.update += OnEditorUpdate;
        private void OnDisable() => EditorApplication.update -= OnEditorUpdate;

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - lastRefreshTime < RefreshInterval)
                return;

            lastRefreshTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect Threadlink's Registers.", MessageType.Info);
                return;
            }

            if (!Threadlink.TryGetSingleton(out var core) || !TryGetRegistryEntries(core, out var subsystems))
            {
                EditorGUILayout.HelpBox("Threadlink has not been deployed yet.", MessageType.Info);
                return;
            }

            var registerSubsystems = new List<KeyValuePair<int, IIdentifiable>>();

            foreach (var entry in subsystems)
            {
                if (IsRegisterType(entry.Value.GetType()))
                    registerSubsystems.Add(entry);
            }

            if (registerSubsystems.Count == 0)
            {
                EditorGUILayout.HelpBox("No Register-type subsystems found among Threadlink's woven subsystems.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var entry in registerSubsystems)
                DrawRegisterFoldout(entry.Key, entry.Value);

            EditorGUILayout.EndScrollView();
        }

        private void DrawRegisterFoldout(int subsystemID, IIdentifiable subsystem)
        {
            if (!TryGetRegistryEntries(subsystem, out var entries))
                return;

            var subsystemType = subsystem.GetType();
            var isExpanded = foldouts.TryGetValue(subsystemID, out var expanded) && expanded;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            isExpanded = EditorGUILayout.Foldout(isExpanded,
            $"{subsystemType.Name}   [0x{subsystemID:X8}]   —   {entries.Count} entries", true);

            foldouts[subsystemID] = isExpanded;

            if (isExpanded)
                DrawEntriesTable(entries);

            EditorGUILayout.EndVertical();
        }

        private static void DrawEntriesTable(List<KeyValuePair<int, IIdentifiable>> entries)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID", EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField("Name", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField("Reference", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            var count = entries.Count;

            for (var i = 0; i < count; i++)
            {
                var value = entries[i].Value;
                var valueType = value.GetType();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"0x{entries[i].Key:X8}", GUILayout.Width(90));
                EditorGUILayout.LabelField(value is INamable namable ? namable.Name : "—", GUILayout.Width(200));
                EditorGUILayout.LabelField(valueType.Name, GUILayout.Width(200));

                if (value is UnityObject unityObject)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(unityObject, valueType, true);
                }
                else
                {
                    EditorGUILayout.LabelField(value.ToString());
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private static bool IsRegisterType(Type type)
        {
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == RegisterOpenType)
                    return true;

                type = type.BaseType;
            }

            return false;
        }

        private static bool TryGetRegistryEntries(object instance, out List<KeyValuePair<int, IIdentifiable>> entries)
        {
            var type = instance.GetType();

            if (!AccessorCache.TryGetValue(type, out var property))
            {
                property = type.GetProperty(AccessorName, BindingFlags.Public | BindingFlags.Instance);
                AccessorCache[type] = property;
            }

            if (property?.GetValue(instance) is IEnumerable<KeyValuePair<int, IIdentifiable>> raw)
            {
                entries = new List<KeyValuePair<int, IIdentifiable>>(raw);
                return true;
            }

            entries = null;
            return false;
        }
    }
}
#endif
