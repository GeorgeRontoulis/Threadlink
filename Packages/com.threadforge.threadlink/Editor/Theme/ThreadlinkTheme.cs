namespace Threadforge.Threadlink.Editor
{
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>The Threadlink Editor theme. Apply it to the root of a window, settings page or inspector.</summary>
    public static class ThreadlinkTheme
    {
        private const string Path = "Packages/com.threadforge.threadlink/Editor/Theme/Threadlink.uss";
        private static StyleSheet sheet;

        /// <summary>Adds the theme's style sheet and skin class to <paramref name="root"/>.</summary>
        public static void Apply(VisualElement root)
        {
            if (sheet == null)
                sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Path);
            if (sheet != null && !root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);
            root.AddToClassList("threadlink");
            root.EnableInClassList("threadlink--light", !EditorGUIUtility.isProSkin);
        }

        /// <summary>The theme color for a monitor tone.</summary>
        public static Color Tone(MonitorTone tone, bool light) =>
            tone switch
            {
                MonitorTone.Good => light ? new Color32(31, 157, 85, 255) : new Color32(97, 217, 143, 255),
                MonitorTone.Caution => light ? new Color32(194, 124, 14, 255) : new Color32(250, 186, 77, 255),
                MonitorTone.Bad => light ? new Color32(209, 59, 59, 255) : new Color32(255, 102, 102, 255),
                MonitorTone.Muted => light ? new Color(0, 0, 0, 0.38f) : new Color(1, 1, 1, 0.32f),
                _ => light ? new Color32(26, 26, 28, 255) : new Color32(242, 242, 242, 255),
            };

        /// <summary>Whether the Editor uses the light skin.</summary>
        public static bool Light => !EditorGUIUtility.isProSkin;
    }
}
