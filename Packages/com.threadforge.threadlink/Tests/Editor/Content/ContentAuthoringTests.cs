namespace Threadforge.Threadlink.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;
    using UnityEditor.AddressableAssets.Settings.GroupSchemas;
    using UnityEngine;

    public class ContentAuthoringTests
    {
        private string folder;
        private AddressableAssetSettings addressables;
        private AddressableAssetGroup characters;
        private ContentSettings settings;
        private AddressableAssetSettings project;
        private List<AddressableAssetGroup> projectGroups;
        private string text,
            prefab,
            scene;

        [SetUp]
        public void SetUp()
        {
            // Addressables registers every imported group asset with the project's settings, including the
            // temporary ones below; TearDown restores the project's group list.
            project = AddressableAssetSettingsDefaultObject.Settings;
            projectGroups = project != null ? new List<AddressableAssetGroup>(project.groups) : null;
            folder = "Assets/ContentTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            File.WriteAllText(folder + "/Notes.txt", "notes");
            AssetDatabase.ImportAsset(folder + "/Notes.txt");
            var source = new GameObject("Probe", typeof(BoxCollider));
            PrefabUtility.SaveAsPrefabAsset(source, folder + "/Probe.prefab");
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", folder + "/Level.unity");
            text = AssetDatabase.AssetPathToGUID(folder + "/Notes.txt");
            prefab = AssetDatabase.AssetPathToGUID(folder + "/Probe.prefab");
            scene = AssetDatabase.AssetPathToGUID(folder + "/Level.unity");
            addressables = AddressableAssetSettings.Create(folder + "/Addressables", "Settings", true, true);
            characters = addressables.CreateGroup("Characters", false, false, false, null, typeof(BundledAssetGroupSchema));
            addressables.CreateOrMoveEntry(text, addressables.DefaultGroup);
            addressables.CreateOrMoveEntry(prefab, characters);
            addressables.CreateOrMoveEntry(scene, addressables.DefaultGroup);
            settings = ScriptableObject.CreateInstance<ContentSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(settings);
            AssetDatabase.DeleteAsset(folder);
            if (project != null && project.groups.RemoveAll(group => group == null || !projectGroups.Contains(group)) > 0)
            {
                EditorUtility.SetDirty(project);
                AssetDatabase.SaveAssetIfDirty(project);
            }
        }

        [Test]
        public void EntriesAreListedByGroupWithTheirLoadStrategy()
        {
            var rows = ContentMapping.Load(addressables, settings);
            Assert.That(rows.Single(row => row.Guid == text).Kind, Is.EqualTo(ContentKind.Asset));
            Assert.That(rows.Single(row => row.Guid == prefab).Kind, Is.EqualTo(ContentKind.Prefab));
            Assert.That(rows.Single(row => row.Guid == scene).Kind, Is.EqualTo(ContentKind.Scene));
            Assert.That(rows.Single(row => row.Guid == prefab).Group, Is.EqualTo("Characters"));
            Assert.That(rows.All(row => !row.Mapped), Is.True, "Nothing is mapped until it is ticked.");
        }

        [Test]
        public void ApplyingFillsOneReadOnlyDictionaryPerLoadStrategyAndKeepsValues()
        {
            var rows = ContentMapping.Load(addressables, settings);
            foreach (var row in rows)
                row.Mapped = true;
            ContentMapping.Apply(rows, settings);
            Assert.That(settings.Assets.Values.Single().AssetGUID, Is.EqualTo(text));
            Assert.That(settings.Prefabs.Values.Single().AssetGUID, Is.EqualTo(prefab));
            Assert.That(settings.Scenes.Values.Single().AssetGUID, Is.EqualTo(scene));
            int value = settings.Prefabs.Keys.Single().Value;
            Assert.That(value, Is.Not.Zero);
            AssetDatabase.MoveAsset(folder + "/Probe.prefab", folder + "/Renamed.prefab");
            rows = ContentMapping.Load(addressables, settings);
            Assert.That(rows.All(row => row.Mapped), Is.True);
            rows.Single(row => row.Guid == text).Mapped = false;
            ContentMapping.Apply(rows, settings);
            Assert.That(settings.Prefabs.Keys.Single().Value, Is.EqualTo(value), "A renamed asset keeps its ID's value.");
            Assert.That(settings.Assets, Is.Empty, "Unticking removes the entry.");
        }

        [Test]
        public void GeneratedNamesNestByGroupAndStayUnique()
        {
            var rows = ContentMapping.Load(addressables, settings);
            foreach (var row in rows)
                row.Mapped = true;
            ContentMapping.Apply(rows, settings);
            var prefabs = ContentMapping.Names(ContentKind.Prefab, settings, addressables);
            Assert.That(prefabs.Values.Single().group, Is.EqualTo("Characters"));
            Assert.That(prefabs.Values.Single().name, Is.EqualTo("Probe"));
            Assert.That(ContentMapping.Names(ContentKind.Scene, settings, addressables).Values.Single().name, Is.EqualTo("Level"));
        }

        [Test]
        public void TheMappingIsCheckedAgainstAddressables()
        {
            var rows = ContentMapping.Load(addressables, settings);
            foreach (var row in rows)
                row.Mapped = true;
            ContentMapping.Apply(rows, settings);
            Assert.That(ContentValidator.Check(settings, addressables), Is.Empty);
            characters.GetSchema<BundledAssetGroupSchema>().IncludeGUIDInCatalog = false;
            Assert.That(ContentValidator.Check(settings, addressables).Select(issue => issue.Message), Has.Some.Contains("excludes GUIDs"));
            addressables.RemoveAssetEntry(text);
            Assert.That(ContentValidator.Check(settings, addressables).Select(issue => issue.Message), Has.Some.Contains("no longer addressable"));
        }
    }
}
