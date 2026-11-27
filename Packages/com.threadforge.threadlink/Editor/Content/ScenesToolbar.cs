namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;
    using UnityEditor.SceneManagement;
    using UnityEditor.Toolbars;
    using UnityEngine;

    /// <summary>
    /// The <b>Scenes</b> dropdown in the main toolbar: every scene the game can load, from the active Build Profile and
    /// from Addressables groups, opened with one click.
    /// </summary>
    public static class ScenesToolbar
    {
        private const string ElementId = "Threadlink/Scenes";

        /// <summary>One scene the game can load.</summary>
        public readonly struct Entry
        {
            /// <summary>The scene's asset path.</summary>
            public readonly string Path;

            /// <summary>Whether it loads through Addressables rather than the Build Profile.</summary>
            public readonly bool IsAddressable;

            internal Entry(string path, bool addressable)
            {
                Path = path;
                IsAddressable = addressable;
            }

            /// <summary>The scene's name.</summary>
            public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        }

        [InitializeOnLoadMethod]
        private static void Watch()
        {
            EditorBuildSettings.sceneListChanged += Refresh;
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => Refresh();
        }

        private static void Refresh() => MainToolbar.Refresh(ElementId);

        /// <summary>
        /// Every scene the game can load: the enabled scenes of the active Build Profile in their order, then the
        /// Addressables scenes by name. A scene in both is listed once, as a Build Profile scene.
        /// </summary>
        public static List<Entry> Collect()
        {
            var result = new List<Entry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && !string.IsNullOrEmpty(scene.path) && File.Exists(scene.path) && seen.Add(scene.path))
                    result.Add(new Entry(scene.path, false));
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return result;
            var addressable = new List<string>();
            var gathered = new List<AddressableAssetEntry>();
            foreach (var group in settings.groups.Where(group => group != null))
                foreach (var entry in group.entries)
                {
                    gathered.Clear();
                    entry.GatherAllAssets(gathered, true, true, false);
                    foreach (var asset in gathered)
                        if (asset.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && seen.Add(asset.AssetPath))
                            addressable.Add(asset.AssetPath);
                }
            result.AddRange(addressable.OrderBy(path => System.IO.Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase).Select(path => new Entry(path, true)));
            return result;
        }

        [MainToolbarElement(ElementId, defaultDockPosition = MainToolbarDockPosition.Middle)]
        private static MainToolbarElement Create() =>
            new MainToolbarDropdown(new MainToolbarContent(Label(), "Open a scene the game can load: Build Profile and Addressables scenes."), Show)
            {
                populateContextMenu = menu =>
                {
                    menu.AppendAction("Refresh", _ => Refresh());
                    menu.AppendAction("Build Profiles…", _ => EditorApplication.ExecuteMenuItem("File/Build Profiles"));
                    menu.AppendAction("Addressables Groups…", _ => EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups"));
                },
            };

        private static string Label()
        {
            string active = EditorSceneManager.GetActiveScene().path;
            return Collect().Any(entry => entry.Path == active) ? Path.GetFileNameWithoutExtension(active) : "Scenes";
        }

        private static void Show(Rect area)
        {
            var menu = new GenericMenu();
            var entries = Collect();
            string active = EditorSceneManager.GetActiveScene().path;
            if (entries.Count == 0)
                menu.AddDisabledItem(new GUIContent("No Build Profile or Addressables scenes"));
            bool separated = false;
            foreach (var entry in entries)
            {
                if (entry.IsAddressable && !separated)
                {
                    if (entries[0].Path != entry.Path)
                        menu.AddSeparator(string.Empty);
                    separated = true;
                }
                var chosen = entry;
                menu.AddItem(new GUIContent(entry.IsAddressable ? entry.Name + "  (Addressables)" : entry.Name), entry.Path == active, () => Open(chosen.Path));
            }
            menu.DropDown(area);
        }

        private static void Open(string path)
        {
            if (EditorSceneManager.GetActiveScene().path == path || EditorApplication.isPlaying)
                return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
