namespace Threadlink.Editor.CodeGen
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEngine;

    /// <summary>
    /// Threadlink > Addressables > Mapping Window: tick the Addressable entries to map to generated IDs, then Apply.
    /// Editor tools map through <see cref="ThreadlinkAddressablesMapping"/> instead.
    /// </summary>
    internal sealed class AddressableMappingWindow : EditorWindow
    {
        private readonly AddressableMapping mapping = new();
        private readonly Dictionary<string, bool> foldouts = new(4, StringComparer.Ordinal);

        private Vector2 scroll;
        private string filter = string.Empty;

        [MenuItem("Threadlink/Addressables/Mapping Window")]
        private static void Open()
        {
            var window = GetWindow<AddressableMappingWindow>(false, "Threadlink Addressables", true);

            window.minSize = new Vector2(560f, 400f);
            window.Reload();
            window.Show();
        }

        private void OnEnable() => Reload();

        internal void Reload() => mapping.Reload();

        private void OnGUI()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                EditorGUILayout.HelpBox("No Addressable Asset Settings found in this project.", MessageType.Error);
                return;
            }

            DrawToolbar();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            var groups = mapping.Groups;
            int count = groups.Count;

            for (int i = 0; i < count; i++)
                DrawGroup(groups[i]);

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                Reload();

            GUILayout.Space(6f);

            filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120f));

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField($"Mapped: {mapping.CountMapped()}", EditorStyles.miniLabel, GUILayout.Width(90f));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGroup(AddressableMappingGroup group)
        {
            foldouts.TryAdd(group.Scope, true);

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            var header = string.Equals(group.DisplayName, group.Scope, StringComparison.Ordinal)
            ? group.DisplayName
            : $"{group.DisplayName}  ({group.Scope})";

            foldouts[group.Scope] = EditorGUILayout.Foldout(foldouts[group.Scope], header, true, EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(38f)))
                SetAll(group.Rows, true);

            if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(44f)))
                SetAll(group.Rows, false);

            EditorGUILayout.EndHorizontal();

            if (foldouts[group.Scope])
            {
                int count = group.Rows.Count;

                for (int i = 0; i < count; i++)
                    DrawRow(group.Rows[i]);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRow(AddressableMappingRow row)
        {
            if (string.IsNullOrEmpty(filter) is false
            && row.EntryName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();

            row.Mapped = EditorGUILayout.Toggle(row.Mapped, GUILayout.Width(18f));

            EditorGUILayout.LabelField(row.EntryName, GUILayout.MinWidth(160f));
            EditorGUILayout.LabelField(row.Kind.ToString(), EditorStyles.miniLabel, GUILayout.Width(56f));

            if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(42f)))
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(row.AssetPath));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox("Apply writes one injector file per group, regenerates the three Addressables domains, "
            + "then rebuilds the reference maps on the Threadlink User Config. Enum values are hashes of group scope and "
            + "asset name, so moving an asset between groups changes its ID.", MessageType.Info);

            if (GUILayout.Button("Apply", GUILayout.Height(28f)))
                mapping.Apply();

            if (GUILayout.Button("Reset Addressables manifests and apply")
            && EditorUtility.DisplayDialog("Reset Addressables manifests?",
                "Rebuild the scene, prefab and asset manifests from the selected mappings, removing all tombstones. "
                + "Collision-resolved IDs can change, and removed IDs may be reused. Existing saves and serialized references "
                + "may no longer match. Review the changes in version control before keeping them.", "Reset and apply", "Cancel"))
            {
                mapping.Apply(resetManifests: true);
            }
        }

        private static void SetAll(List<AddressableMappingRow> rows, bool mapped)
        {
            int count = rows.Count;

            for (int i = 0; i < count; i++)
                rows[i].Mapped = mapped;
        }
    }
}
