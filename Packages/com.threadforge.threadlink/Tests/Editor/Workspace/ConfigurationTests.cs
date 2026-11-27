namespace Threadforge.Threadlink.Editor.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed class ConfigurationTests
    {
        private const string Root = "Assets/ThreadlinkConfigurationTests";

        // Not a ConfigSource, so the Workspace never lists it.
        private sealed class NoteConfig : ConfigAsset
        {
            public Object Asset;

            public NoteConfig(AssetLocation location)
                : base(location) { }

            public override Object Find() => Asset;

            protected override Object CreateIn(string folder) =>
                Asset = Save(
                    new Material(Shader.Find("Hidden/InternalErrorShader")),
                    folder,
                    "Note.mat"
                );
        }

        private AssetLocation location;

        [SetUp]
        public void SetUp() =>
            location = new AssetLocation(
                "tests.configuration." + Guid.NewGuid().ToString("N"),
                "Test Note",
                "Tests",
                ConfigRoot.Engineering
            );

        [TearDown]
        public void TearDown()
        {
            AssetLocations.ResetFolder(location);
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void FoldersAreNormalizedInsideAssets()
        {
            Assert.That(
                AssetLocations.Normalize("Assets\\Configs\\"),
                Is.EqualTo("Assets/Configs")
            );
            Assert.That(
                AssetLocations.Normalize(Path.GetFullPath("Assets/Configs")),
                Is.EqualTo("Assets/Configs")
            );
            Assert.Throws<ArgumentException>(
                () => AssetLocations.Normalize("Packages/com.threadforge.threadlink")
            );
            Assert.Throws<ArgumentException>(
                () => AssetLocations.Normalize("Assets/../ProjectSettings")
            );
            Assert.Throws<ArgumentException>(() => AssetLocations.Normalize(Path.GetTempPath()));
        }

        [Test]
        public void LocationsUseTheirRootUntilGivenAFolder()
        {
            Assert.That(location.Folder, Is.EqualTo(AssetLocations.EngineeringFolder));
            Assert.That(location.IsCustom, Is.False);
            AssetLocations.SetFolder(location, Root + "/Custom/");
            Assert.That(location.Folder, Is.EqualTo(Root + "/Custom"));
            Assert.That(location.IsCustom, Is.True);
            AssetLocations.ResetFolder(location);
            Assert.That(location.Folder, Is.EqualTo(AssetLocations.EngineeringFolder));
        }

        [Test]
        public void RootFolderChangesApplyToLocationsWithoutTheirOwn()
        {
            string previous = AssetLocations.DesignFolder;
            var design = new AssetLocation(
                location.Id + ".design",
                "Design note",
                "Tests",
                ConfigRoot.Design
            );
            try
            {
                AssetLocations.DesignFolder = Root + "/Design";
                Assert.That(design.Folder, Is.EqualTo(Root + "/Design"));
                AssetLocations.SetRootFolder(ConfigRoot.Design, null);
                Assert.That(design.Folder, Is.EqualTo(AssetLocations.DefaultDesignFolder));
            }
            finally
            {
                AssetLocations.DesignFolder = previous;
            }
        }

        [Test]
        public void CreateUsesTheFolderAndMoveKeepsTheAsset()
        {
            AssetLocations.SetFolder(location, Root + "/First");
            var config = new NoteConfig(location);
            var created = config.Create();
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(created));
            Assert.That(AssetDatabase.GetAssetPath(created), Is.EqualTo(Root + "/First/Note.mat"));
            Assert.That(
                config.Create(),
                Is.SameAs(created),
                "an existing asset is returned, not duplicated"
            );
            Assert.That(config.IsOutsideFolder, Is.False);

            AssetLocations.SetFolder(location, Root + "/Second");
            Assert.That(config.IsOutsideFolder, Is.True);
            Assert.That(config.MoveToFolder(), Is.Null);
            Assert.That(AssetDatabase.GetAssetPath(created), Is.EqualTo(Root + "/Second/Note.mat"));
            Assert.That(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(created)),
                Is.EqualTo(guid)
            );
            Assert.That(config.IsOutsideFolder, Is.False);
        }

        [Test]
        public void EverySettingsTypeAndUnityConfigurationIsListed()
        {
            var assets = ConfigAssets.All();
            foreach (var type in ThreadlinkSettingsAssets.Types)
                Assert.That(
                    assets.Any(asset => asset.Location.Id == "threadlink.settings." + type.Name),
                    type.Name
                );
            Assert.That(assets.Any(asset => asset.Location.Id == "unity.addressables.settings"));
            Assert.That(
                assets.Any(asset => asset.Location.Id == RenderingConfigSource.PipelineLocation.Id)
            );
            Assert.That(
                assets
                    .Where(asset => asset.Location.Category == "Threadlink")
                    .All(asset => asset.Location.Root == ConfigRoot.Engineering)
            );
            var ids = ConfigAssets.Locations().Select(found => found.Id).ToList();
            Assert.That(ids, Is.Unique);
            var categories = ConfigAssets
                .Locations()
                .Select(found => found.Category)
                .Distinct()
                .ToList();
            Assert.That(
                categories.IndexOf("Threadlink"),
                Is.LessThan(categories.IndexOf("Rendering"))
            );
        }

        [Test]
        public void ExistingSettingsAreFoundWhereverTheyAre()
        {
            foreach (var type in ThreadlinkSettingsAssets.Types)
            {
                var settings = ThreadlinkSettingsAssets.Get(type, false);
                if (settings == null)
                    continue;
                Assert.That(settings.GetType(), Is.EqualTo(type));
                Assert.That(
                    ThreadlinkSettingsAssets.IsSettingsPath(AssetDatabase.GetAssetPath(settings)),
                    Is.True,
                    type.Name
                );
            }
            Assert.That(
                ThreadlinkSettingsAssets.IsSettingsPath("Assets/Scenes/SampleScene.unity"),
                Is.False
            );
        }

        [Test]
        public void RenderPipelinesAreReportedByPackage()
        {
            Assert.That(
                RenderPipelineKind.All.Select(kind => kind.Package),
                Is.EquivalentTo(
                    new[]
                    {
                        "com.unity.render-pipelines.universal",
                        "com.unity.render-pipelines.high-definition",
                    }
                )
            );
            foreach (var kind in RenderPipelineKind.All)
                Assert.That(kind.IsInstalled, Is.EqualTo(kind.AssetType != null));
            var pipeline = new RenderingConfigSource().Assets.First();
            if (
                RenderPipelineKind.Installed.Count == 0
                && UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null
            )
            {
                Assert.That(pipeline.Find(), Is.Null);
                Assert.That(pipeline.CreateUnavailableReason, Does.Contain("Built-in"));
                Assert.Throws<InvalidOperationException>(() => pipeline.Create());
            }
        }
    }
}
