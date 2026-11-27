namespace Threadforge.Threadlink
{
    using System;
    using System.Buffers;
    using global::MessagePack;

    /// <summary>Explicit generated/declared MessagePack formatters, bounded input/depth, no typeless or dynamic fallback.</summary>
    public sealed class MessagePackSnapshotCodec<T> : ISnapshotCodec<T>
    {
        public MessagePackSerializerOptions Options { get; }
        public int MaximumBytes { get; }

        public MessagePackSnapshotCodec(
            IFormatterResolver resolver,
            int maximumBytes = 16 * 1024 * 1024,
            int maximumDepth = 64
        )
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));
            if (maximumBytes < 1 || maximumDepth < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            MaximumBytes = maximumBytes;
            Options = MessagePackSerializerOptions
                .Standard.WithResolver(resolver)
                .WithSecurity(
                    MessagePackSecurity
                        .UntrustedData.WithMaximumObjectGraphDepth(maximumDepth)
                        .WithMaximumDecompressedSize(maximumBytes)
                );
        }

        public byte[] Encode(T snapshot)
        {
            var bytes = MessagePackSerializer.Serialize(snapshot, Options);
            if (bytes.Length > MaximumBytes)
                throw new MessagePackSerializationException("Snapshot exceeds byte limit.");
            return bytes;
        }

        public T Decode(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBytes)
                throw new MessagePackSerializationException("Invalid snapshot byte count.");
            var reader = new MessagePackReader(new ReadOnlySequence<byte>(bytes));
            var value = Options
                .Resolver.GetFormatterWithVerify<T>()
                .Deserialize(ref reader, Options);
            if (!reader.End)
                throw new MessagePackSerializationException("Trailing snapshot bytes.");
            return value;
        }
    }
}
