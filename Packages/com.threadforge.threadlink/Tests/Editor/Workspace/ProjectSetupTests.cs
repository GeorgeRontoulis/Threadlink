namespace Threadforge.Threadlink.Editor.Tests
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public class ProjectSetupTests
    {
        [Serializable]
        private sealed class Definition
        {
            public string name;
            public string[] references;
            public bool autoReferenced;
        }

        [Test]
        public void NewAssemblyDefinitionIsAnAutoReferencedDefinitionWithItsReferences()
        {
            var data = JsonUtility.FromJson<Definition>(ProjectSetup.NewAssemblyDefinition("Game.Generated", new[] { "GUID:a", "GUID:b", "GUID:a", null }));
            Assert.That(data.name, Is.EqualTo("Game.Generated"));
            Assert.That(data.references, Is.EqualTo(new[] { "GUID:a", "GUID:b" }));
            Assert.That(data.autoReferenced, Is.True);
            Assert.That(JsonUtility.FromJson<Definition>(ProjectSetup.NewAssemblyDefinition("Empty", Array.Empty<string>())).references, Is.Empty);
        }

        [Test]
        public void MergeAddsOnlyMissingReferencesAndKeepsTheRest()
        {
            string json = "{\n  \"name\": \"Game\",\n  \"references\": [\n    \"GUID:a\",\n    \"Unity.Mathematics\"\n  ],\n  \"autoReferenced\": true\n}\n";
            string merged = ProjectSetup.MergeReferences(json, new[] { ("GUID:a", "A"), ("GUID:m", "Unity.Mathematics"), ("GUID:c", "C") });
            var data = JsonUtility.FromJson<Definition>(merged);
            Assert.That(data.references, Is.EqualTo(new[] { "GUID:a", "Unity.Mathematics", "GUID:c" }), "present by GUID or by name counts");
            Assert.That(data.name, Is.EqualTo("Game"));
            Assert.That(merged, Does.Contain("\n    \"GUID:c\""), "the file's indentation is kept");
            Assert.That(ProjectSetup.MergeReferences(merged, new[] { ("GUID:c", "C") }), Is.SameAs(merged));
        }

        [Test]
        public void MergeFillsAnEmptyOrMissingReferenceList()
        {
            Assert.That(JsonUtility.FromJson<Definition>(ProjectSetup.MergeReferences("{\n    \"name\": \"A\",\n    \"references\": [],\n    \"autoReferenced\": true\n}", new[] { ("GUID:x", "X") })).references, Is.EqualTo(new[] { "GUID:x" }));
            var data = JsonUtility.FromJson<Definition>(ProjectSetup.MergeReferences("{\n    \"name\": \"A\"\n}", new[] { ("GUID:x", "X") }));
            Assert.That(data.references, Is.EqualTo(new[] { "GUID:x" }));
            Assert.That(data.name, Is.EqualTo("A"));
        }

        [Test]
        public void RuntimeAssembliesAreThePackagesMainRuntimeAssembliesWithoutSamples()
        {
            Assert.That(ProjectSetup.IsPackageRuntime("Packages/com.threadforge.threadlink.aura/Runtime/Threadforge.Threadlink.Aura.asmdef"), Is.True);
            Assert.That(ProjectSetup.IsPackageRuntime("Packages/com.threadforge.threadlink.aura/Nexus/Runtime/Threadforge.Threadlink.Aura.Nexus.asmdef"), Is.False);
            Assert.That(ProjectSetup.IsPackageRuntime("Packages/com.threadforge.threadlink/Editor/Threadforge.Threadlink.Editor.asmdef"), Is.False);
            Assert.That(ProjectSetup.IsPackageRuntime("Packages/com.threadforge.threadlink.starter/Runtime/Threadforge.Threadlink.Starter.asmdef"), Is.False);
            Assert.That(ProjectSetup.IsPackageRuntime(null), Is.False);

            var assemblies = ProjectSetup.RuntimeAssemblies();
            Assert.That(assemblies.Select(assembly => assembly.name), Does.Contain("Threadforge.Threadlink"));
            Assert.That(assemblies.Select(assembly => assembly.name), Has.None.Contains("Showcase").And.None.Contains("Starter").And.None.Contains("Editor"));
            Assert.That(assemblies.Select(assembly => assembly.reference), Has.All.StartsWith("GUID:"));
            string core = AssetDatabase.AssetPathToGUID("Packages/com.threadforge.threadlink/Runtime/Threadforge.Threadlink.asmdef");
            Assert.That(assemblies, Does.Contain(("GUID:" + core, "Threadforge.Threadlink")));
        }

        [Test]
        public void FoldersOutsideAssetsHaveNoOwner()
        {
            Assert.That(ProjectSetup.OwnerOf("Packages/com.threadforge.threadlink/Runtime"), Is.Null);
            Assert.That(ProjectSetup.OwnAssemblyDefinition("Assets/Threadlink Folder That Does Not Exist"), Is.Null);
            Assert.That(ProjectSetup.Folders, Does.Contain(GeneratedIds.Folder).And.Contain(ProjectSetup.CodebaseFolder));
        }
    }
}
