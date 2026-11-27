namespace Threadforge.Threadlink.Tests
{
    using System;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class NativeDefinition : VaultVariant<int>
    {
        public VaultOverride<int> Amount;
        public override Guid Schema => new Guid("e23683f8-0c6e-412e-b5bf-b1fb53db114a");
        public override int SchemaVersion => 1;

        protected override int BuildDefault(VaultContext context) => 10;

        protected override int ApplyOverrides(int inherited, VaultContext context) =>
            Amount.Resolve(inherited);
    }

    public class VaultNativeTests
    {
        [Test]
        public void SerializedTypedVariantsPrepareIndependentlyFromAuthoringMutation()
        {
            var basis = ScriptableObject.CreateInstance<NativeDefinition>();
            var variant = ScriptableObject.CreateInstance<NativeDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite(
                    "{\"identity\":\"0ab504d5-91d0-47b3-8054-a2d0835b71d5\",\"authoredVersion\":1}",
                    basis
                );
                JsonUtility.FromJsonOverwrite(
                    "{\"identity\":\"20abe0ce-d3a7-4bea-9466-2a14c8aa0c5a\",\"authoredVersion\":1}",
                    variant
                );
                variant.Parent = basis;
                variant.Amount = new VaultOverride<int> { Enabled = true, Value = 22 };
                var prepared = new VaultContext().Prepare(variant);
                variant.Amount.Value = 99;
                Assert.That(prepared.Value, Is.EqualTo(22));
                Assert.That(variant.InheritedOrigin, Is.EqualTo(basis.Identity));
                variant.Parent = variant;
                Assert.Throws<InvalidOperationException>(() => variant.ValidateDefinition());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(basis);
                UnityEngine.Object.DestroyImmediate(variant);
            }
        }

        [Test]
        public void UnsupportedAuthoringVersionRequiresExplicitMigration()
        {
            var source = ScriptableObject.CreateInstance<NativeDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite(
                    "{\"identity\":\"0ab504d5-91d0-47b3-8054-a2d0835b71d5\",\"authoredVersion\":2}",
                    source
                );
                Assert.Throws<InvalidOperationException>(() => source.ValidateDefinition());
                Assert.Catch(() => source.ApplyAuthoringMigration());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
