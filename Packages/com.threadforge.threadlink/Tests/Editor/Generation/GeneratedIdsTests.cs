namespace Threadforge.Threadlink.Editor.Tests
{
    using System.IO;
    using NUnit.Framework;

    public sealed class GeneratedIdsTests
    {
        private const string Folder = "Temp/ThreadlinkGeneratedIdsTests";

        [SetUp]
        public void Clean()
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }

        private static GeneratedIds.Member Member(string name, string key, string group = "") =>
            new GeneratedIds.Member { Group = group, Name = name, Key = key, Type = "int", Value = "1" };

        [Test]
        public void IdentifiersAreValidCSharp()
        {
            Assert.That(GeneratedIds.Identifier("first-chest"), Is.EqualTo("FirstChest"));
            Assert.That(GeneratedIds.Identifier("2D view"), Is.EqualTo("_2DView"));
            Assert.That(GeneratedIds.Identifier("  "), Is.EqualTo("Unnamed"));
            Assert.That(GeneratedIds.Quote("a\"b\\c"), Is.EqualTo("\"a\\\"b\\\\c\""));
        }

        [Test]
        public void RemovedIdsBecomeObsoleteTombstonesAndRenamesKeepTheirKey()
        {
            Assert.That(GeneratedIds.WriteTo(Folder, "TestIds", "Test.", new[] { Member("Jump", "a"), Member("Fire", "b") }), Is.True);
            Assert.That(GeneratedIds.WriteTo(Folder, "TestIds", "Test.", new[] { Member("Jump", "a"), Member("Fire", "b") }), Is.False, "Unchanged sources leave the file alone.");
            GeneratedIds.WriteTo(Folder, "TestIds", "Test.", new[] { Member("Leap", "a") });
            string code = File.ReadAllText(Folder + "/TestIds.cs");
            StringAssert.Contains("int Leap = 1;", code);
            StringAssert.Contains("[global::System.Obsolete(\"This ID no longer exists in its source.\")]\n    public static readonly int Fire", code);
            StringAssert.DoesNotContain(" Jump ", code, "A renamed member keeps its key, so the old name is not a tombstone.");
        }

        [Test]
        public void GroupsBecomeNestedClasses()
        {
            GeneratedIds.WriteTo(Folder, "TestInput", "Test.", new[] { Member("Move", "m", "Gameplay"), Member("Gameplay", "g", "Maps") });
            string code = File.ReadAllText(Folder + "/TestInput.cs");
            StringAssert.Contains("public static class Gameplay", code);
            StringAssert.Contains("public static class Maps", code);
        }
    }
}
