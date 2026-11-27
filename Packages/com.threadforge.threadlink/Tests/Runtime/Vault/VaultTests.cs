namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    public class VaultTests
    {
        private sealed class Source : IVaultSource<string>
        {
            public Guid Identity { get; set; } = Guid.NewGuid();
            public Guid Schema => new Guid("8abfe96c-44ac-43ea-83a7-eaf13b3b6732");
            public int SchemaVersion => 1;
            public Source Parent;

            public string Build(VaultContext context) =>
                Parent == null ? "immutable" : context.Prepare(Parent).Value;
        }

        [Test]
        public void ReferenceCyclesAndDuplicateIdentitiesAreRejected()
        {
            var a = new Source();
            var b = new Source { Parent = a };
            a.Parent = b;
            Assert.Throws<InvalidOperationException>(() => new VaultContext().Prepare(a));
            a.Parent = null;
            var context = new VaultContext();
            var prepared = context.Prepare(a);
            Assert.That(context.Prepare(a), Is.SameAs(prepared));
            Assert.Throws<InvalidOperationException>(
                () => context.Prepare(new Source { Identity = a.Identity })
            );
        }

        [Test]
        public void OverridesResetToInheritanceAndDynamicBagsRejectMutableValues()
        {
            var field = new VaultOverride<int> { Enabled = true, Value = 42 };
            Assert.That(field.Resolve(5), Is.EqualTo(42));
            field.Reset();
            Assert.That(field.Resolve(5), Is.EqualTo(5));
            Assert.Throws<ArgumentException>(
                () =>
                    new VaultFieldSet(
                        new[] { new KeyValuePair<string, object>("mutable", new List<int>()) }
                    )
            );
            var immutable = new VaultFieldSet(
                new[] { new KeyValuePair<string, object>("damage", 7) }
            );
            Assert.That(immutable.Get<int>("damage"), Is.EqualTo(7));
        }

        [Test]
        public void GeneratedFieldIdsReadFieldSets()
        {
            var damage = new VaultField("damage");
            var fields = new VaultFieldSet(new[] { new KeyValuePair<string, object>("damage", 7) });
            Assert.That(fields.Get<int>(damage), Is.EqualTo(7));
            Assert.That(fields.TryGet<int>(damage, out int value) && value == 7, Is.True);
            Assert.That(fields.TryGet<string>(damage, out _), Is.False, "A value of another type is not returned.");
            Assert.That(fields.TryGet<int>(new VaultField("speed"), out _), Is.False);
            Assert.That(damage, Is.EqualTo(new VaultField("damage")));
        }
    }
}
