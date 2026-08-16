namespace Threadlink.Editor
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEditor.Toolbars;
    using UnityEngine;

    internal static class SceneDropdownMainToolbar
    {
        private const string ElementId = "Threadlink/Scene Dropdown";
        private const string DefaultLabel = "Scenes";

        static SceneDropdownMainToolbar()
        {
            EditorBuildSettings.sceneListChanged += Refresh;
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => Refresh();
        }

        private static void Refresh() => MainToolbar.Refresh(ElementId);

        [MainToolbarElement(ElementId, defaultDockPosition = MainToolbarDockPosition.Middle)]
        private static IEnumerable<MainToolbarElement> CreateSceneDropdown()
        {
            yield return new MainToolbarDropdown
            (
                new MainToolbarContent(GetCurrentLabel(),
                "Open a scene that will be included in the build (Build Settings or Addressables)."),
                ShowDropdownMenu
            )
            { populateContextMenu = PopulateContextMenu, };
        }

        private static void ShowDropdownMenu(Rect dropDownRect)
        {
            var menu = new GenericMenu();
            var entries = BuildSceneCollector.CollectValidBuildScenes();
            string currentPath = EditorSceneManager.GetActiveScene().path;

            if (entries.Count == 0)
                menu.AddDisabledItem(new GUIContent("No valid build scenes found"));
            else
            {
                var buildSettingsEntries = entries.Where(e => !e.IsAddressable).ToList();
                var addressableEntries = entries.Where(e => e.IsAddressable).ToList();

                foreach (var entry in buildSettingsEntries)
                    AppendMenuItem(menu, entry, currentPath);

                if (buildSettingsEntries.Count > 0 && addressableEntries.Count > 0)
                    menu.AddSeparator(string.Empty);

                foreach (var entry in addressableEntries)
                    AppendMenuItem(menu, entry, currentPath);
            }

            menu.DropDown(dropDownRect);
        }

        private static void AppendMenuItem(GenericMenu menu, BuildSceneCollector.Entry entry, string currentPath)
        {
            string label = entry.IsAddressable ? $"{entry.DisplayName}  (Addressable)" : entry.DisplayName;

            menu.AddItem(new GUIContent(label), entry.Path == currentPath, () => OpenScene(entry.Path));
        }

        private static void PopulateContextMenu(UnityEngine.UIElements.DropdownMenu menu)
        {
            menu.AppendAction("Refresh", _ => Refresh());
            menu.AppendAction("Open Build Settings...", _ => GetWindow("UnityEditor.BuildPlayerWindow"));
        }

        private static void GetWindow(string typeName)
        {
            var type = typeof(Editor).Assembly.GetType(typeName);

            if (type != null)
                EditorWindow.GetWindow(type);
        }

        private static string GetCurrentLabel()
        {
            string currentPath = EditorSceneManager.GetActiveScene().path;
            var entries = BuildSceneCollector.CollectValidBuildScenes();
            var match = entries.FirstOrDefault(e => e.Path.Equals(currentPath));

            return match.Path != null ? match.DisplayName : DefaultLabel;
        }

        private static void OpenScene(string path)
        {
            if (EditorSceneManager.GetActiveScene().path.Equals(path)) return;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}