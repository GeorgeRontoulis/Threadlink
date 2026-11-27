namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.Rendering;
    using UnityEngine;
    using UnityEngine.Rendering;
    using Object = UnityEngine.Object;

    /// <summary>A scriptable render pipeline package the Workspace can install and create assets for.</summary>
    public sealed class RenderPipelineKind
    {
        private readonly string assetTypeName;
        private readonly Func<RenderPipelineKind, string, RenderPipelineAsset> create;

        private RenderPipelineKind(
            string name,
            string package,
            string assetTypeName,
            Func<RenderPipelineKind, string, RenderPipelineAsset> create
        )
        {
            Name = name;
            Package = package;
            this.assetTypeName = assetTypeName;
            this.create = create;
        }

        /// <summary>The Universal Render Pipeline.</summary>
        public static readonly RenderPipelineKind Universal = new RenderPipelineKind(
            "Universal Render Pipeline",
            "com.unity.render-pipelines.universal",
            "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime",
            CreateUniversal
        );

        /// <summary>The High Definition Render Pipeline.</summary>
        public static readonly RenderPipelineKind HighDefinition = new RenderPipelineKind(
            "High Definition Render Pipeline",
            "com.unity.render-pipelines.high-definition",
            "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime",
            (kind, folder) =>
                Save(
                    (RenderPipelineAsset)ScriptableObject.CreateInstance(kind.AssetType),
                    folder,
                    kind.Name + ".asset"
                )
        );

        /// <summary>Every supported pipeline.</summary>
        public static IReadOnlyList<RenderPipelineKind> All { get; } =
            new[] { Universal, HighDefinition };

        /// <summary>The pipelines whose packages are installed.</summary>
        public static IReadOnlyList<RenderPipelineKind> Installed =>
            All.Where(kind => kind.IsInstalled).ToList();

        /// <summary>Display name.</summary>
        public string Name { get; }

        /// <summary>Package name.</summary>
        public string Package { get; }

        /// <summary>The pipeline asset type, or null when the package is not installed.</summary>
        public Type AssetType => Type.GetType(assetTypeName);

        /// <summary>Whether the package is installed and compiled.</summary>
        public bool IsInstalled => AssetType != null;

        /// <summary>Whether <paramref name="asset"/> belongs to this pipeline.</summary>
        public bool Owns(RenderPipelineAsset asset) =>
            asset != null && AssetType != null && AssetType.IsInstanceOfType(asset);

        /// <summary>The pipeline of <paramref name="asset"/>, or null.</summary>
        public static RenderPipelineKind Of(RenderPipelineAsset asset) =>
            All.FirstOrDefault(kind => kind.Owns(asset));

        /// <summary>
        /// Creates a pipeline asset, with its renderer where the pipeline has one, in <paramref name="folder"/>. It does
        /// not become the default; see <see cref="GraphicsSettings.defaultRenderPipeline"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The package is not installed.</exception>
        public RenderPipelineAsset Create(string folder)
        {
            if (!IsInstalled)
                throw new InvalidOperationException(Name + " is not installed.");
            var asset = create(this, AssetLocations.EnsureFolder(folder));
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static RenderPipelineAsset CreateUniversal(RenderPipelineKind kind, string folder)
        {
            // Universal's public factory: a renderer data asset, then the pipeline asset that uses it.
            var rendererType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime",
                true
            );
            var rendererBase = Type.GetType(
                "UnityEngine.Rendering.Universal.ScriptableRendererData, Unity.RenderPipelines.Universal.Runtime",
                true
            );
            var factory =
                kind.AssetType.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { rendererBase },
                    null
                ) ?? throw new MissingMethodException(kind.AssetType.Name, "Create");
            var rendererData = ScriptableObject.CreateInstance(rendererType);
            // As Universal's own menu does: the package's post-processing shaders and textures.
            var postProcess = rendererType.GetField(
                "postProcessData",
                BindingFlags.Public | BindingFlags.Instance
            );
            if (postProcess != null)
                postProcess.SetValue(
                    rendererData,
                    AssetDatabase.LoadAssetAtPath(
                        "Packages/" + kind.Package + "/Runtime/Data/PostProcessData.asset",
                        postProcess.FieldType
                    )
                );
            var renderer = Save(rendererData, folder, kind.Name + " Renderer.asset");
            var asset = (RenderPipelineAsset)factory.Invoke(null, new object[] { renderer });
            return Save(asset, folder, kind.Name + ".asset");
        }

        private static T Save<T>(T asset, string folder, string fileName)
            where T : Object
        {
            AssetDatabase.CreateAsset(
                asset,
                AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName)
            );
            return asset;
        }
    }

    /// <summary>The render pipeline assets the project uses: the default pipeline and its global settings.</summary>
    public sealed class RenderingConfigSource : ConfigSource
    {
        /// <summary>Where new pipeline and renderer assets are created.</summary>
        public static readonly AssetLocation PipelineLocation = new AssetLocation(
            "unity.rendering.pipeline",
            "Render Pipeline",
            "Rendering",
            ConfigRoot.Engineering
        );

        /// <summary>Where the pipeline's global settings belong.</summary>
        public static readonly AssetLocation GlobalSettingsLocation = new AssetLocation(
            "unity.rendering.global-settings",
            "Pipeline Global Settings",
            "Rendering",
            ConfigRoot.Engineering
        );

        /// <inheritdoc />
        public override IEnumerable<ConfigAsset> Assets =>
            new ConfigAsset[] { new DefaultPipeline(), new GlobalSettings() };

        /// <summary>The global settings of the default pipeline, or null.</summary>
        public static RenderPipelineGlobalSettings CurrentGlobalSettings()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            var type = pipeline != null ? pipeline.pipelineType : null;
            return type != null
                ? EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset(type)
                : null;
        }

        private sealed class DefaultPipeline : ConfigAsset
        {
            public DefaultPipeline()
                : base(PipelineLocation) { }

            public override string Description =>
                "The project's default render pipeline asset. Without one the project uses the Built-in Render Pipeline.";

            public override Object Find() => GraphicsSettings.defaultRenderPipeline;

            protected override bool MovesCompanions => true;

            public override string CreateUnavailableReason =>
                RenderPipelineKind.Installed.Count == 0
                    ? "No render pipeline package is installed. Install one on the Rendering page; until then the project uses the Built-in Render Pipeline."
                    : null;

            public override string CreateConfirmation =>
                "This creates a "
                + RenderPipelineKind.Installed.First().Name
                + " asset and makes it the project's default render pipeline. Materials made for the Built-in Render Pipeline need converting.";

            protected override Object CreateIn(string folder)
            {
                var asset = RenderPipelineKind.Installed.First().Create(folder);
                GraphicsSettings.defaultRenderPipeline = asset;
                return asset;
            }
        }

        private sealed class GlobalSettings : ConfigAsset
        {
            public GlobalSettings()
                : base(GlobalSettingsLocation) { }

            public override string Description =>
                "Settings the active pipeline shares across all its assets. The pipeline creates them when it becomes the default.";

            public override Object Find() => CurrentGlobalSettings();

            protected override bool MovesCompanions => true;

            public override string CreateUnavailableReason =>
                GraphicsSettings.defaultRenderPipeline == null
                    ? "The project uses the Built-in Render Pipeline, which has none."
                    : "The render pipeline creates them; open Project Settings ▸ Graphics.";

            protected override Object CreateIn(string folder) =>
                throw new InvalidOperationException(CreateUnavailableReason);
        }
    }
}
