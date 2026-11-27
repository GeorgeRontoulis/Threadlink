namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.PackageManager;
    using UnityEditor.PackageManager.Requests;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary>The project's render pipeline: packages, the default and per-quality-level pipeline assets, and their folders.</summary>
    public sealed class RenderingPage : WorkspacePage
    {
        private static AddRequest installing;

        /// <inheritdoc />
        public override string Title => "Rendering";

        /// <inheritdoc />
        public override string Group => "Project";

        /// <inheritdoc />
        public override int Order => 25;

        /// <inheritdoc />
        public override VisualElement CreateContent(WorkspaceContext context)
        {
            var root = new VisualElement();
            Action changed = context.Refresh;
            var current = GraphicsSettings.defaultRenderPipeline;
            root.Add(
                WorkspaceUI.Text(
                    "The render pipeline the project draws with. New pipeline assets go to the Render Pipeline folder below. Quality levels can use their own pipeline asset; levels without one use the default."
                )
            );
            root.Add(
                WorkspaceUI.Buttons(
                    ("Graphics Settings", () => context.OpenProjectSettings("Project/Graphics")),
                    ("Quality Settings", () => context.OpenProjectSettings("Project/Quality"))
                )
            );

            root.Add(WorkspaceUI.Heading("Pipeline"));
            var kind = RenderPipelineKind.Of(current);
            root.Add(
                WorkspaceUI.Row(
                    "Active",
                    current == null
                        ? "Built-in Render Pipeline"
                        : (kind?.Name ?? current.GetType().Name) + " · " + current.name
                )
            );
            var field = new ObjectField("Default pipeline asset")
            {
                objectType = typeof(RenderPipelineAsset),
                allowSceneObjects = false,
                value = current,
            };
            field.RegisterValueChangedCallback(change =>
            {
                GraphicsSettings.defaultRenderPipeline = change.newValue as RenderPipelineAsset;
                AssetDatabase.SaveAssets();
                changed();
            });
            root.Add(field);

            root.Add(WorkspaceUI.Heading("Quality levels"));
            var quality = new SerializedObject(
                AssetDatabase
                    .LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")
                    .FirstOrDefault()
            );
            var levels = quality.FindProperty("m_QualitySettings");
            for (int i = 0; levels != null && i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var pipeline = level.FindPropertyRelative("customRenderPipeline");
                if (pipeline == null)
                    continue;
                var levelField = new ObjectField(
                    level.FindPropertyRelative("name")?.stringValue ?? "Level " + i
                )
                {
                    objectType = typeof(RenderPipelineAsset),
                    allowSceneObjects = false,
                    tooltip = "Empty: the level uses the default pipeline asset.",
                };
                levelField.BindProperty(pipeline);
                root.Add(levelField);
            }

            root.Add(WorkspaceUI.Heading("Packages"));
            foreach (var pipelineKind in RenderPipelineKind.All)
            {
                var target = pipelineKind;
                bool busy = installing != null && !installing.IsCompleted;
                root.Add(
                    pipelineKind.IsInstalled
                        ? WorkspaceUI.Row(
                            pipelineKind.Name,
                            "Installed",
                            "New asset",
                            () => CreatePipeline(context, target)
                        )
                        : WorkspaceUI.Row(
                            pipelineKind.Name,
                            busy ? "Installing…" : "Not installed (" + pipelineKind.Package + ")",
                            busy ? null : "Install",
                            () => Install(target, changed)
                        )
                );
            }

            root.Add(WorkspaceUI.Heading("Configuration assets"));
            root.Add(ConfigUI.Category(context, "Rendering", changed));

            var pipelines = FindAll<RenderPipelineAsset>();
            root.Add(WorkspaceUI.Heading($"Pipeline assets ({pipelines.Count})"));
            if (pipelines.Count == 0)
                root.Add(WorkspaceUI.Text("The project has no render pipeline assets."));
            foreach (var asset in pipelines)
                root.Add(
                    AssetRow(
                        context,
                        asset,
                        asset == current ? "default" : RenderPipelineKind.Of(asset)?.Name,
                        asset == current
                            ? null
                            : (
                                "Use as default",
                                () =>
                                {
                                    GraphicsSettings.defaultRenderPipeline = asset;
                                    AssetDatabase.SaveAssets();
                                }
                            ),
                        changed
                    )
                );

            var renderers = FindAll<ScriptableObject>("t:ScriptableRendererData");
            if (renderers.Count > 0)
            {
                root.Add(WorkspaceUI.Heading($"Renderers ({renderers.Count})"));
                foreach (var renderer in renderers)
                    root.Add(AssetRow(context, renderer, null, null, changed));
            }
            return root;
        }

        private static VisualElement AssetRow(
            WorkspaceContext context,
            Object asset,
            string note,
            (string label, Action action)? extra,
            Action changed
        )
        {
            string path = AssetDatabase.GetAssetPath(asset);
            string folder = RenderingConfigSource.PipelineLocation.Folder;
            var row = WorkspaceUI.Row(
                asset.name,
                path + (note != null ? " · " + note : string.Empty),
                "Select",
                () => context.Select(asset)
            );
            if (extra.HasValue)
            {
                var (label, action) = extra.Value;
                row.Add(
                    new Button(() =>
                    {
                        action();
                        changed();
                    })
                    {
                        text = label,
                    }
                );
            }
            if (
                path.StartsWith("Assets/", StringComparison.Ordinal)
                && !AssetLocations.IsIn(path, folder)
            )
                row.Add(
                    new Button(() =>
                    {
                        string target = AssetDatabase.GenerateUniqueAssetPath(
                            AssetLocations.EnsureFolder(folder)
                                + "/"
                                + System.IO.Path.GetFileName(path)
                        );
                        string error = AssetDatabase.MoveAsset(path, target);
                        if (!string.IsNullOrEmpty(error))
                            EditorUtility.DisplayDialog(
                                "Could not move " + asset.name,
                                error,
                                "OK"
                            );
                        changed();
                    })
                    {
                        text = "Move to folder",
                        tooltip = "Move it to " + folder + ".",
                    }
                );
            return row;
        }

        private static System.Collections.Generic.List<T> FindAll<T>(string filter = null)
            where T : Object =>
            AssetDatabase
                .FindAssets(filter ?? "t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();

        private static void CreatePipeline(WorkspaceContext context, RenderPipelineKind kind)
        {
            try
            {
                var asset = kind.Create(RenderingConfigSource.PipelineLocation.Folder);
                context.Select(asset);
            }
            catch (Exception error)
            {
                EditorUtility.DisplayDialog(
                    "Could not create a " + kind.Name + " asset",
                    error.Message,
                    "OK"
                );
            }
            context.Refresh();
        }

        private static void Install(RenderPipelineKind kind, Action changed)
        {
            if (installing != null && !installing.IsCompleted)
                return;
            if (
                !EditorUtility.DisplayDialog(
                    "Install " + kind.Name,
                    "Add " + kind.Package + " to the project? It ships with the Unity Editor.",
                    "Install",
                    "Cancel"
                )
            )
                return;
            installing = Client.Add(kind.Package);
            void Poll()
            {
                if (!installing.IsCompleted)
                    return;
                EditorApplication.update -= Poll;
                if (installing.Status == StatusCode.Failure)
                    Debug.LogError(
                        "Installing " + kind.Package + " failed: " + installing.Error?.message
                    );
                changed();
            }
            EditorApplication.update += Poll;
            changed();
        }
    }
}
