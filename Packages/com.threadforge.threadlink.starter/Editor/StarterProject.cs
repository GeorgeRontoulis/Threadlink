namespace Threadforge.Threadlink.Starter.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Threadforge.Threadlink.Editor;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Starter project setup. Nothing runs automatically on import.</summary>
    public static class StarterProject
    {
        /// <summary>Starter scene names, bootstrap first.</summary>
        public static readonly string[] SceneNames = { "Starter Bootstrap", "Starter Menu", "Starter Forest" };

        /// <summary>Finds imported starter scenes under Assets, keyed by scene name.</summary>
        public static Dictionary<string, string> FindScenes()
        {
            var found = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!SceneNames.Contains(name))
                    continue;
                if (found.ContainsKey(name))
                    throw new InvalidOperationException($"More than one '{name}' scene exists: {found[name]} and {path}.");
                found.Add(name, path);
            }
            return found;
        }

        /// <summary>The build scene list the setup command applies: starter scenes first, others kept in order.</summary>
        public static EditorBuildSettingsScene[] PreviewBuildScenes()
        {
            var scenes = FindScenes();
            var missing = SceneNames.Where(name => !scenes.ContainsKey(name)).ToArray();
            if (missing.Length != 0)
                throw new InvalidOperationException("Import the Starter sample first. Missing: " + string.Join(", ", missing));
            var starter = SceneNames.Select(name => new EditorBuildSettingsScene(scenes[name], true)).ToList();
            var paths = new HashSet<string>(starter.Select(scene => scene.path));
            starter.AddRange(EditorBuildSettings.scenes.Where(scene => !paths.Contains(scene.path)));
            return starter.ToArray();
        }

        /// <summary>Puts the starter scenes first in the active build scene list, keeping other scenes.</summary>
        [MenuItem("Threadlink/Starter/Add Starter Scenes To Build Profile")]
        public static void ConfigureBuildScenes()
        {
            var scenes = PreviewBuildScenes();
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Starter build scenes", "Set the build scene list to:\n\n" + string.Join("\n", scenes.Select(scene => scene.path)), "Apply", "Cancel"))
                return;
            EditorBuildSettings.scenes = scenes;
            Debug.Log("Starter scenes added to the build scene list. Open 'Starter Bootstrap' and enter Play mode.");
        }

        /// <summary>Opens the bootstrap scene.</summary>
        [MenuItem("Threadlink/Starter/Open Bootstrap Scene")]
        public static void OpenBootstrap()
        {
            if (!FindScenes().TryGetValue(SceneNames[0], out var path))
                throw new InvalidOperationException("Import the Starter sample first.");
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
        }

        internal static List<StarterContent> FindContent() =>
            AssetDatabase
                .FindAssets("t:" + nameof(StarterContent))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StarterContent>)
                .Where(content => content != null)
                .ToList();
    }

    /// <summary>Checks the starter content, its level definition and the build scene order.</summary>
    public sealed class StarterValidator : IWorkspaceValidator
    {
        /// <inheritdoc />
        public string Name => "Starter";

        /// <inheritdoc />
        public void Validate(List<ValidationIssue> issues)
        {
            var contents = StarterProject.FindContent();
            if (contents.Count == 0)
                return;
            if (!LocalizationSetup.IsReady)
                issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, "Unity Localization is not set up for the starter's string table. Open Threadlink ▸ Workspace ▸ Localization and select Set up."));
            if (Physics.GetIgnoreLayerCollision(0, 0))
                issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, "Default-layer collisions are disabled in Physics settings; the starter player falls through the ground."));
            foreach (var content in contents)
            {
                string problem = content.Validate();
                if (problem != null)
                {
                    issues.Add(new ValidationIssue(Name, IssueSeverity.Error, content.name + ": " + problem, content));
                    continue;
                }
                try
                {
                    content.FirstLevel.ValidateDefinition();
                }
                catch (Exception error)
                {
                    issues.Add(new ValidationIssue(Name, IssueSeverity.Error, content.FirstLevel.name + ": " + error.Message, content.FirstLevel));
                }
            }
            Dictionary<string, string> scenes;
            try
            {
                scenes = StarterProject.FindScenes();
            }
            catch (Exception error)
            {
                issues.Add(new ValidationIssue(Name, IssueSeverity.Error, error.Message));
                return;
            }
            var build = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToList();
            for (int i = 0; i < StarterProject.SceneNames.Length; i++)
            {
                string name = StarterProject.SceneNames[i];
                if (!scenes.TryGetValue(name, out var path) || build.IndexOf(path) < 0)
                {
                    issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, $"'{name}' is not in the build scene list. Run Threadlink ▸ Starter ▸ Add Starter Scenes To Build Profile."));
                    return;
                }
            }
            if (build.Count > 0 && scenes.TryGetValue(StarterProject.SceneNames[0], out var bootstrap) && build[0] != bootstrap)
                issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, "'Starter Bootstrap' is not the first build scene."));
        }
    }

    /// <summary>Workspace page for the Starter sample.</summary>
    public sealed class StarterPage : WorkspacePage
    {
        /// <inheritdoc />
        public override string Title => "Starter";

        /// <inheritdoc />
        public override string Group => "Project";

        /// <inheritdoc />
        public override int Order => 30;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            root.Add(WorkspaceUI.Text("A small complete game on Threadlink. StarterGame is one subsystem; scenes, audio, input, screens and saves are already running when the bootstrap scene starts. Designers edit the Starter Content asset; nothing in it selects code."));
            root.Add(WorkspaceUI.Buttons(("Add scenes to Build Profile", StarterProject.ConfigureBuildScenes), ("Open bootstrap scene", StarterProject.OpenBootstrap), ("Localization", () => context.OpenPage<LocalizationPage>())));
            root.Add(WorkspaceUI.Heading("Content"));
            var contents = StarterProject.FindContent();
            if (contents.Count == 0)
                root.Add(WorkspaceUI.Text("Import the Starter sample from the Hub or the Package Manager."));
            foreach (var content in contents)
            {
                var target = content;
                string problem = content.Validate();
                root.Add(WorkspaceUI.Row(AssetDatabase.GetAssetPath(content), problem ?? "Complete", "Select", () => context.Select(target), problem == null ? IssueSeverity.Info : IssueSeverity.Error));
            }
            root.Add(WorkspaceUI.Heading("Validation"));
            var issues = new List<ValidationIssue>();
            new StarterValidator().Validate(issues);
            if (issues.Count == 0)
                root.Add(WorkspaceUI.Text("No problems found."));
            foreach (var issue in issues)
                root.Add(WorkspaceUI.Row(issue.Severity.ToString(), issue.Message, severity: issue.Severity));
            return root;
        }
    }
}
