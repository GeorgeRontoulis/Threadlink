namespace Threadforge.Threadlink
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;

    /// <summary>Explicit serialization adapter. Implementations use an existing serializer or engineering mapping.</summary>
    public interface ISnapshotCodec<T>
    {
        byte[] Encode(T snapshot);
        T Decode(ReadOnlyMemory<byte> payload);
    }

    /// <summary>Machine-readable unsupported schema or missing migration. Malformed bytes are separate corruption errors.</summary>
    public sealed class SnapshotSchemaException : InvalidOperationException
    {
        public PersistenceError Error { get; }
        public int SourceVersion { get; }

        public SnapshotSchemaException(
            PersistenceError error,
            string message,
            int sourceVersion = 0
        )
            : base(message)
        {
            Error = error;
            SourceVersion = sourceVersion;
        }
    }

    /// <summary>Typed TLSP envelope compatible with the existing 28-byte schema header; no implicit legacy fallback.</summary>
    public sealed class SnapshotSchema<T>
    {
        private const int HeaderLength = 28;
        private readonly ISnapshotCodec<T> codec;
        private readonly Action<T> validate;
        private readonly Dictionary<int, Func<ReadOnlyMemory<byte>, T>> migrations =
            new Dictionary<int, Func<ReadOnlyMemory<byte>, T>>();
        private Func<ReadOnlyMemory<byte>, T> legacy;
        public Guid Identity { get; }
        public int Version { get; }
        public int MaximumBytes { get; }

        /// <summary>Validation runs before serialization and after every current, migrated or legacy read.</summary>
        public SnapshotSchema(
            Guid identity,
            int version,
            ISnapshotCodec<T> codec,
            Action<T> validate,
            int maximumBytes = 16 * 1024 * 1024
        )
        {
            if (identity == Guid.Empty || version < 1 || maximumBytes <= HeaderLength)
                throw new ArgumentException("Invalid snapshot schema.");
            Identity = identity;
            Version = version;
            MaximumBytes = maximumBytes;
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.validate = validate ?? throw new ArgumentNullException(nameof(validate));
        }

        /// <summary>Registers a declared old-version payload reader that produces the current snapshot.</summary>
        public void RegisterMigration(int sourceVersion, Func<ReadOnlyMemory<byte>, T> reader)
        {
            if (
                sourceVersion < 1
                || sourceVersion >= Version
                || reader == null
                || migrations.ContainsKey(sourceVersion)
            )
                throw new ArgumentException("Invalid migration registration.");
            migrations.Add(sourceVersion, reader);
        }

        /// <summary>Opts into a declared reader for bytes with no TLSP magic; never used for an unknown TLSP version.</summary>
        public void RegisterLegacyReader(Func<ReadOnlyMemory<byte>, T> reader)
        {
            if (legacy != null || reader == null)
                throw new ArgumentException("Invalid legacy reader.");
            legacy = reader;
        }

        public byte[] Write(T snapshot)
        {
            validate(snapshot);
            byte[] payload =
                codec.Encode(snapshot)
                ?? throw new InvalidOperationException("Codec returned no bytes.");
            if (payload.Length < 1 || payload.Length > MaximumBytes - HeaderLength)
                throw new InvalidOperationException("Snapshot size exceeds the schema limit.");
            var bytes = new byte[payload.Length + HeaderLength];
            bytes[0] = (byte)'T';
            bytes[1] = (byte)'L';
            bytes[2] = (byte)'S';
            bytes[3] = (byte)'P';
            Identity.TryWriteBytes(bytes.AsSpan(4, 16));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), Version);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), payload.Length);
            payload.CopyTo(bytes, HeaderLength);
            return bytes;
        }

        public T Read(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBytes)
                throw new InvalidOperationException("Invalid snapshot size.");
            var span = bytes.Span;
            bool magic =
                span.Length >= 4
                && span[0] == 'T'
                && span[1] == 'L'
                && span[2] == 'S'
                && span[3] == 'P';
            T snapshot;
            if (!magic)
                snapshot =
                    legacy != null
                        ? legacy(bytes)
                        : throw new SnapshotSchemaException(
                            PersistenceError.Unsupported,
                            "An explicit legacy reader is required."
                        );
            else
            {
                if (bytes.Length < HeaderLength)
                    throw new InvalidOperationException("Truncated snapshot header.");
                if (new Guid(span.Slice(4, 16)) != Identity)
                    throw new SnapshotSchemaException(
                        PersistenceError.Unsupported,
                        "Schema identity does not match."
                    );
                int version = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(20));
                int length = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(24));
                if (length < 1 || length != bytes.Length - HeaderLength)
                    throw new InvalidOperationException("Invalid snapshot payload length.");
                var payload = bytes.Slice(HeaderLength);
                snapshot =
                    version == Version ? codec.Decode(payload)
                    : migrations.TryGetValue(version, out var reader) ? reader(payload)
                    : throw new SnapshotSchemaException(
                        PersistenceError.Unsupported,
                        "No reader for snapshot version " + version,
                        version
                    );
            }
            validate(snapshot);
            return snapshot;
        }
    }
}
