// Development tool: authors the Showcase sample and writes it into the Showcase package's Samples~ folder.
//   Unity -batchmode -quit -projectPath . -executeMethod Threadforge.Threadlink.Development.ShowcaseSampleBuilder.BatchBuild
namespace Threadforge.Threadlink.Development
{
    using System;
    using System.IO;
    using Threadforge.Threadlink.Showcase;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.UIElements;

    public static class ShowcaseSampleBuilder
    {
        private const string Build = "Assets/ShowcaseSampleBuild";
        private const string Root = Build + "/Showcase";
        private const string Destination = "Packages/com.threadforge.threadlink.showcase/Samples~/Showcase";

        [MenuItem("Threadlink/Development/Rebuild Showcase Sample")]
        public static void BuildSample()
        {
            if (AssetDatabase.IsValidFolder(Build))
                AssetDatabase.DeleteAsset(Build);
            Directory.CreateDirectory(Root + "/UI");
            Directory.CreateDirectory(Root + "/Scenes");
            File.WriteAllText(Root + "/UI/ShowcaseTheme.tss", "@import url(\"unity-theme://default\");\n");
            File.WriteAllText(
                Root + "/README.md",
                "# Threadlink Showcase\n\nOpen `Scenes/Showcase.unity` and enter Play mode. Select a demo on the left; its controls, the packages it uses and a live log appear on the right. Each demo's code is one file under `Runtime/Demos` in the Threadlink Showcase package.\n\n"
                    + "Importing the sample sets up Unity Localization for its English string table, `Localization/Threadlink Showcase` (**Threadlink ▸ Workspace ▸ Localization ▸ Set up** does the same). Add languages to that table to translate the demos' text.\n"
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/ShowcaseTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.match = 0.5f;
            AssetDatabase.CreateAsset(panel, Root + "/UI/ShowcasePanel.asset");

            // Additive, so the scene open in the Editor stays loaded and unchanged.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 1, -6);
            var light = new GameObject("Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var app = new GameObject("Showcase", typeof(UIDocument));
            app.GetComponent<UIDocument>().panelSettings = panel;
            app.AddComponent<ShowcaseApp>();
            foreach (var item in new[] { camera, light, app })
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item, scene);
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Showcase.unity");
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();

            // The sample's string table is authored with Unity Localization, not by this builder: keep it.
            string localization = Path.Combine(Destination, "Localization");
            string kept = Path.Combine(Path.GetTempPath(), "Threadlink Showcase Localization");
            if (Directory.Exists(kept))
                Directory.Delete(kept, true);
            if (Directory.Exists(localization))
            {
                CopyWithMeta(localization, kept);
                File.Copy(localization + ".meta", kept + ".meta", true);
            }
            if (Directory.Exists(Destination))
                Directory.Delete(Destination, true);
            Directory.CreateDirectory(Path.GetDirectoryName(Destination));
            CopyWithMeta(Root, Destination);
            if (Directory.Exists(kept))
            {
                CopyWithMeta(kept, localization);
                File.Copy(kept + ".meta", localization + ".meta", true);
                Directory.Delete(kept, true);
                File.Delete(kept + ".meta");
            }
            AssetDatabase.DeleteAsset(Build);
            Debug.Log("Showcase sample written to " + Destination);
        }

        /// <summary>Batch-mode entry point.</summary>
        public static void BatchBuild()
        {
            try
            {
                BuildSample();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void CopyWithMeta(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (var folder in Directory.GetDirectories(source))
                CopyWithMeta(folder, Path.Combine(destination, Path.GetFileName(folder)));
        }
    }
}
