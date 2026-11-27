namespace Threadforge.Threadlink.Tests
{
    using System;
    using global::MessagePack.Resolvers;
    using NUnit.Framework;

    /// <summary>
    /// Recorded snapshot envelopes. Saves written earlier must keep
    /// reading, and new saves must stay byte-compatible with them.
    /// </summary>
    public sealed class SaveEnvelopeGoldenTests
    {
        private static readonly Guid Identity = new Guid("c15c72b2-8cff-4532-9998-d4d12a3ec5ad");

            [TestCase(-2147483648, "544C5350B2725CC1FF8C32459998D4D12A3EC5AD0100000005000000D280000000")]
            [TestCase(-1, "544C5350B2725CC1FF8C32459998D4D12A3EC5AD0100000001000000FF")]
            [TestCase(0, "544C5350B2725CC1FF8C32459998D4D12A3EC5AD010000000100000000")]
            [TestCase(127, "544C5350B2725CC1FF8C32459998D4D12A3EC5AD01000000010000007F")]
            [TestCase(128, "544C5350B2725CC1FF8C32459998D4D12A3EC5AD0100000002000000CC80")]
            [TestCase(2147483647, "544C5350B2725CC1FF8C32459998D4D12A3EC5AD0100000005000000CE7FFFFFFF")]
        public void IntegerSchemaReadsAndWritesRecordedEnvelopes(int value, string hex)
        {
            var schema = new SnapshotSchema<int>(Identity, 1, new MessagePackSnapshotCodec<int>(BuiltinResolver.Instance), _ => { });
            byte[] recorded = FromHex(hex);
            Assert.That(schema.Read(recorded), Is.EqualTo(value));
            CollectionAssert.AreEqual(recorded, schema.Write(value));
        }

        private static byte[] FromHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
