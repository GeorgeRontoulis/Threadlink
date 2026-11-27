namespace Threadforge.Threadlink.Editor.Tests
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;

    public class VaultPageTests
    {
        private const string Folder = "Assets/ThreadlinkVaultPageTests";

        [TearDown]
        public void Clean()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void FieldSetsAreCreatableFromTheVaultPage()
        {
            var types = VaultPage.CreatableTypes();
            Assert.That(types, Does.Contain((typeof(VaultFieldSetAsset), "Threadlink/Vault/Field Set")));
            Assert.That(types.Select(entry => entry.type), Has.All.Matches<Type>(type => typeof(VaultAuthoring).IsAssignableFrom(type) && !type.IsAbstract));
        }

        [Test]
        public void CreatedAndDuplicatedDefinitionsHaveTheirOwnIdentity()
        {
            var created = VaultPage.Create(typeof(VaultFieldSetAsset), Folder);
            Assert.That(AssetDatabase.GetAssetPath(created), Does.StartWith(Folder + "/New Vault Field Set Asset"));
            Assert.That(created.Identity, Is.Not.EqualTo(Guid.Empty));
            Assert.That(created.AuthoredVersion, Is.EqualTo(created.SchemaVersion));
            Assert.That(VaultPage.Selected, Is.SameAs(created));

            var copy = VaultPage.Duplicate(created);
            Assert.That(copy, Is.Not.Null.And.Not.SameAs(created));
            Assert.That(copy.Identity, Is.Not.EqualTo(created.Identity));
            Assert.That(VaultPage.Selected, Is.SameAs(copy));
            Assert.That(VaultValidator.FindDefinitions(), Has.Member(created).And.Member(copy));
        }
    }
}
