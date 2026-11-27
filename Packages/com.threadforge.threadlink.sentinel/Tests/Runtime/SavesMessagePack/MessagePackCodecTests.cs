namespace Threadforge.Threadlink.Tests
{
    using System;
    using global::MessagePack;
    using global::MessagePack.Resolvers;
    using NUnit.Framework;

    [GeneratedMessagePackResolver]
    internal partial class CodecTestResolver { }

    [MessagePackObject]
    public sealed partial class Snapshot
    {
        [Key(0)]
        public int Score { get; set; }

        [Key(1)]
        public string[] Inventory { get; set; }
    }

    public sealed class Undeclared
    {
        public int Value;
    }

    public class MessagePackCodecTests
    {
        private static IFormatterResolver Resolver =>
            CompositeResolver.Create(CodecTestResolver.Instance, BuiltinResolver.Instance);

        [Test]
        public void GeneratedTypedDtoAndCompatibleTlspEnvelopeRoundTrip()
        {
            var codec = new MessagePackSnapshotCodec<Snapshot>(Resolver);
            var schema = new SnapshotSchema<Snapshot>(
                new Guid("c15c72b2-8cff-4532-9998-d4d12a3ec5ad"),
                1,
                codec,
                value =>
                {
                    if (value.Score < 0)
                        throw new ArgumentException();
                }
            );
            var source = new Snapshot { Score = 12, Inventory = new[] { "Rope", "Lens" } };
            byte[] bytes = schema.Write(source);
            var read = schema.Read(bytes);
            Assert.That(read.Score, Is.EqualTo(12));
            Assert.That(read.Inventory, Is.EqualTo(source.Inventory));
            Assert.That(bytes[0], Is.EqualTo((byte)'T'));
        }

        [Test]
        public void TrailingBytesLimitsAndUndeclaredTypesFailWithoutDynamicFallback()
        {
            var codec = new MessagePackSnapshotCodec<Snapshot>(Resolver);
            byte[] bytes = codec.Encode(
                new Snapshot { Score = 1, Inventory = Array.Empty<string>() }
            );
            Array.Resize(ref bytes, bytes.Length + 1);
            Assert.Throws<MessagePackSerializationException>(() => codec.Decode(bytes));
            Assert.Throws<MessagePackSerializationException>(
                () => new MessagePackSnapshotCodec<Snapshot>(Resolver, 1).Decode(bytes)
            );
            Assert.Throws<MessagePackSerializationException>(
                () => new MessagePackSnapshotCodec<Undeclared>(Resolver).Encode(new Undeclared())
            );
        }
    }
}
