namespace Threadforge.Threadlink.Editor.Tests
{
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class IdDomainTests
    {
        private const string Folder = "Temp/ThreadlinkIdDomainTests";
        private IdDomain domain;

        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
            domain = ScriptableObject.CreateInstance<IdDomain>();
            domain.name = "Validation Test Kind";
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(domain);

        [Test]
        public void EntriesGetStableUniqueValuesThatSurviveRenames()
        {
            var slime = domain.Add("Slime");
            var copy = domain.Add("Slime");
            var goblin = domain.Add("Goblin");
            Assert.That(slime.Value, Is.EqualTo(IdDomain.Hash("Slime")), "A new entry starts from its name's hash.");
            Assert.That(new[] { slime.Value, copy.Value, goblin.Value }.Distinct().Count(), Is.EqualTo(3));
            Assert.That(new[] { slime.Value, copy.Value, goblin.Value }, Has.None.EqualTo(0), "Zero is reserved for None.");
            int before = goblin.Value;
            goblin.Name = "Hobgoblin";
            domain.Add("Wraith");
            Assert.That(goblin.Value, Is.EqualTo(before), "Renaming keeps the value.");
            Assert.That(domain.TypeName, Is.EqualTo("ValidationTestKind"));
        }

        [Test]
        public void ProblemsBlockGeneration()
        {
            domain.Add("Slime");
            domain.Add("slime");
            domain.Add("None");
            domain.Add(" ");
            var problems = domain.Validate();
            Assert.That(problems, Has.Some.Contains("both named Slime"));
            Assert.That(problems, Has.Some.Contains("'None' is reserved"));
            Assert.That(problems, Has.Some.Contains("no name"));
        }

        [Test]
        public void EnumerationsKeepNoneAndTombstoneRemovedValues()
        {
            GeneratedIds.Member Member(string name, int value) =>
                new GeneratedIds.Member { Name = name, Key = value.ToString(), Value = value.ToString() };
            GeneratedIds.WriteEnumTo(Folder, "TestKind", "Test.", new[] { Member("Slime", 7), Member("Goblin", 9) });
            GeneratedIds.WriteEnumTo(Folder, "TestKind", "Test.", new[] { Member("Ooze", 7) });
            string code = File.ReadAllText(Folder + "/TestKind.cs");
            StringAssert.Contains("public enum TestKind : int", code);
            StringAssert.Contains("None = 0,", code);
            StringAssert.Contains("Ooze = 7,", code);
            StringAssert.Contains("[global::System.Obsolete(\"This ID no longer exists in its source.\")]\n    Goblin = 9,", code);
            StringAssert.DoesNotContain("Slime", code, "A renamed entry keeps its value under the new name.");
            Assert.That(GeneratedIds.RecordedKeys(Folder, "TestKind"), Is.EquivalentTo(new[] { "7", "9" }), "Removed values stay reserved.");
        }

        [Test]
        public void NativeDomainsAreDiscovered()
        {
            var vault = IdDomainTargets.Find("Vault.Fields");
            Assert.That(vault, Is.Not.Null);
            Assert.That(vault.ClassName, Is.EqualTo("VaultFields"));
            Assert.That(IdDomainTargets.Find(string.Empty), Is.Null);
            Assert.That(IdDomainTargets.Find("Missing.Domain"), Is.Null);
        }
    }
}
