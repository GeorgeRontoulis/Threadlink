namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    // The runtime type, the definition's BuildRuntime and the variant's overrides are generated.
    public sealed partial class GeneratedEnemy { }

    public sealed partial class GeneratedEnemyDefinition : VaultAsset<GeneratedEnemy>
    {
        public string DisplayName = "Slime";

        [SerializeField, Tooltip("Hit points.")]
        private int health = 30;

        public List<int> Drops = new List<int> { 1, 2 };

        public void SetHealth(int value) => health = value;
    }

    public sealed partial class GeneratedEnemyVariant : VaultVariant<GeneratedEnemy> { }

    // A hand-written runtime type: the generated BuildRuntime calls its constructor by parameter name.
    public sealed class Blade
    {
        public Blade(string name, int damage)
        {
            Name = name;
            Damage = damage;
        }

        public string Name { get; }
        public int Damage { get; }
    }

    public sealed partial class BladeDefinition : VaultAsset<Blade>
    {
        public int Damage = 4;
        public string Name = "Dagger";
    }

    public class VaultAuthoringTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Destroy()
        {
            foreach (var item in created)
                Object.DestroyImmediate(item);
            created.Clear();
        }

        private T Create<T>(string json = null)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            JsonUtility.FromJsonOverwrite("{\"identity\":\"" + Guid.NewGuid().ToString("D") + "\",\"authoredVersion\":1}", asset);
            if (json != null)
                JsonUtility.FromJsonOverwrite(json, asset);
            return asset;
        }

        [Test]
        public void GeneratedRuntimeTypeCopiesTheAuthoredFields()
        {
            var definition = Create<GeneratedEnemyDefinition>();
            definition.SetHealth(45);
            var enemy = new VaultContext().Prepare(definition).Value;
            definition.Drops.Add(3);
            definition.DisplayName = "Changed";

            Assert.That(enemy.DisplayName, Is.EqualTo("Slime"));
            Assert.That(enemy.Health, Is.EqualTo(45));
            Assert.That(enemy.Drops, Is.EqualTo(new[] { 1, 2 }), "collections are copied when prepared");
            Assert.Throws<NotSupportedException>(() => ((IList<int>)enemy.Drops).Add(9));
        }

        [Test]
        public void GeneratedVariantInheritsAndAppliesEnabledOverrides()
        {
            var basis = Create<GeneratedEnemyDefinition>();
            var variant = Create<GeneratedEnemyVariant>("{\"health\":{\"Enabled\":true,\"Value\":99},\"displayName\":{\"Enabled\":false,\"Value\":\"Ignored\"}}");
            variant.Parent = basis;

            var enemy = new VaultContext().Prepare(variant).Value;

            Assert.That(enemy.Health, Is.EqualTo(99));
            Assert.That(enemy.DisplayName, Is.EqualTo("Slime"));
            Assert.That(enemy.Drops, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void GeneratedVariantWithoutParentUsesItsOverrideValues()
        {
            var variant = Create<GeneratedEnemyVariant>("{\"health\":{\"Enabled\":false,\"Value\":12},\"displayName\":{\"Enabled\":false,\"Value\":\"Bat\"}}");

            var enemy = new VaultContext().Prepare(variant).Value;

            Assert.That(enemy.Health, Is.EqualTo(12));
            Assert.That(enemy.DisplayName, Is.EqualTo("Bat"));
            Assert.That(enemy.Drops, Is.Empty);
        }

        [Test]
        public void GeneratedBuildRuntimeCallsAHandWrittenConstructor()
        {
            var blade = new VaultContext().Prepare(Create<BladeDefinition>()).Value;
            Assert.That(blade.Name, Is.EqualTo("Dagger"));
            Assert.That(blade.Damage, Is.EqualTo(4));
        }

        [Test]
        public void DefaultSchemaIsStableAndSharedByDefinitionsOfOneRuntimeType()
        {
            var definition = Create<GeneratedEnemyDefinition>();
            var variant = Create<GeneratedEnemyVariant>();
            // RFC 4122 version 5 of the runtime type's name; the value never depends on assembly versions.
            Assert.That(definition.Schema, Is.EqualTo(new Guid("07857da5-b5df-5f20-9f55-1840d37ac0a0")));
            Assert.That(variant.Schema, Is.EqualTo(definition.Schema));
            Assert.That(definition.SchemaVersion, Is.EqualTo(1));
            Assert.That(VaultSchema.StableName(typeof(List<int>)), Is.EqualTo("System.Collections.Generic.List<System.Int32>"));
            Assert.That(VaultSchema.Of(typeof(List<int>)), Is.EqualTo(new Guid("4e1af7d9-a8c9-5024-8500-52ad56836cb9")));
        }

        [Test]
        public void FieldSetReadsTypedValuesAndInheritsFromItsParent()
        {
            var damage = new VaultField("Damage");
            var speed = new VaultField("Speed");
            var spawn = new VaultField("Spawn");
            var facing = new VaultField("Facing");
            var basis = Create<VaultFieldSetAsset>();
            basis.Set(damage, 10);
            basis.Set(speed, 2.5f);
            basis.Set(spawn, new Vector3(1, 2, 3));
            basis.Set(facing, Quaternion.Euler(0, 90, 0));
            var child = Create<VaultFieldSetAsset>();
            child.Parent = basis;
            child.Set(damage, 25);

            var fields = new VaultContext().Prepare(child).Value;
            basis.Set(speed, 9f);

            Assert.That(fields.Get<int>(damage), Is.EqualTo(25));
            Assert.That(fields.Get<float>(speed), Is.EqualTo(2.5f), "a prepared set does not follow later edits");
            Assert.That(fields.Get<Vector3>(spawn), Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(Quaternion.Angle(fields.Get<Quaternion>(facing), Quaternion.Euler(0, 90, 0)), Is.LessThan(0.01f));
            Assert.That(fields.Count, Is.EqualTo(4));
            Assert.That(fields.Contains(new VaultField("Missing")), Is.False);
            Assert.That(fields.TryGet(damage, out string _), Is.False);
            Assert.That(child.Fields, Has.Count.EqualTo(1));
            Assert.That(child.Schema, Is.EqualTo(basis.Schema));
        }

        [Test]
        public void FieldSetRejectsRepeatedAndUnnamedFields()
        {
            var set = Create<VaultFieldSetAsset>("{\"fields\":[{\"field\":\"Damage\",\"kind\":0,\"whole\":1},{\"field\":\"Damage\",\"kind\":0,\"whole\":2}]}");
            Assert.That(() => set.ValidateDefinition(), Throws.InvalidOperationException.With.Message.Contains("more than once"));

            var unnamed = Create<VaultFieldSetAsset>("{\"fields\":[{\"field\":\"\",\"kind\":2,\"flag\":true}]}");
            Assert.That(() => unnamed.ValidateDefinition(), Throws.InvalidOperationException.With.Message.Contains("no ID"));

            var cyclic = Create<VaultFieldSetAsset>();
            cyclic.Parent = cyclic;
            Assert.That(() => cyclic.ValidateDefinition(), Throws.InvalidOperationException);
        }

        [Test]
        public void FieldSetEntriesKeepEveryKindOfValue()
        {
            var set = Create<VaultFieldSetAsset>();
            var values = new object[] { 7, 8L, 1.5f, 2.25, true, "text", new Vector2(1, 2), new Vector4(1, 2, 3, 4), new Vector2Int(3, 4), new Vector3Int(5, 6, 7), Color.red, set };
            for (int i = 0; i < values.Length; i++)
                set.Set(new VaultField("F" + i), values[i]);
            var prepared = new VaultContext().Prepare(set).Value;
            for (int i = 0; i < values.Length; i++)
                Assert.That(prepared.Get<object>(new VaultField("F" + i)), Is.EqualTo(values[i]), values[i].GetType().Name);
            Assert.Throws<ArgumentException>(() => set.Set(new VaultField("Bad"), new object()));
        }
    }
}
