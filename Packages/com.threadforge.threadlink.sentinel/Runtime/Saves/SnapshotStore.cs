namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    /// <summary>Published slot metadata. Revision is an optimistic concurrency token, independent of remote versions.</summary>
    public sealed class SlotSummary
    {
        public Guid Revision { get; }
        public DateTimeOffset SavedAt { get; }
        public string Title { get; }
        public int SnapshotBytes { get; }
        internal byte[] Hash { get; }

        internal SlotSummary(
            Guid revision,
            DateTimeOffset savedAt,
            string title,
            int bytes,
            byte[] hash
        )
        {
            Revision = revision;
            SavedAt = savedAt;
            Title = title;
            SnapshotBytes = bytes;
            Hash = hash;
        }
    }

    /// <summary>Validated data; restoring it into a live game remains a separate game operation.</summary>
    public sealed class StoredSnapshot<T>
    {
        public T Value { get; }
        public SlotSummary Summary { get; }
        private readonly byte[] bytes;

        internal StoredSnapshot(T value, SlotSummary summary, byte[] bytes)
        {
            Value = value;
            Summary = summary;
            this.bytes = bytes;
        }

        public byte[] Export() => (byte[])bytes.Clone();
    }

    /// <summary>Local typed snapshots and summaries over ordinary atomic byte transactions. No platform/cloud dependency.</summary>
    public sealed class SnapshotStore<T>
    {
        public const int SnapshotFile = 1;
        public const int SummaryFile = 2;
        private readonly ISaveService storage;
        public SnapshotSchema<T> Schema { get; }

        public SnapshotStore(ISaveService storage, SnapshotSchema<T> schema)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public PersistenceResult<int[]> ListSlots() =>
            storage is ISaveCatalog catalog
                ? catalog.ListSlots()
                : PersistenceResult<int[]>.Failure(PersistenceError.Unsupported);

        /// <summary>Explicitly deletes a logical slot through atomic storage publication, retaining recovery history.</summary>
        public UniTask<PersistenceResult> DeleteAsync(int slot, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return slot > 0
                ? storage.DeleteSaveAsync(slot)
                : UniTask.FromResult(PersistenceResult.Failure(PersistenceError.InvalidArgument));
        }

        /// <summary>Reads both files from one immutable generation. Cancellation never mutates saved data.</summary>
        public async UniTask<PersistenceResult<StoredSnapshot<T>>> ReadAsync(
            int slot,
            CancellationToken token = default
        )
        {
            if (slot < 1)
                return PersistenceResult<StoredSnapshot<T>>.Failure(
                    PersistenceError.InvalidArgument
                );
            token.ThrowIfCancellationRequested();
            var begun = await storage.BeginTransactionAsync(slot);
            if (!begun.Succeeded)
                return PersistenceResult<StoredSnapshot<T>>.Failure(begun.Error, begun.Message);
            using var transaction = begun.Value;
            return await ReadAsync(transaction, token);
        }

        private async UniTask<PersistenceResult<StoredSnapshot<T>>> ReadAsync(
            ISaveReader reader,
            CancellationToken token
        )
        {
            token.ThrowIfCancellationRequested();
            var bytes = await reader.ReadAsync(SnapshotFile);
            if (!bytes.Succeeded)
                return PersistenceResult<StoredSnapshot<T>>.Failure(
                    bytes.Error == PersistenceError.NotFound
                    && (!(reader is ISaveTransaction transaction) || transaction.HasPublishedState)
                        ? PersistenceError.CorruptData
                        : bytes.Error,
                    bytes.Message
                );
            var metadata = await reader.ReadAsync(SummaryFile);
            if (!metadata.Succeeded)
                return PersistenceResult<StoredSnapshot<T>>.Failure(
                    metadata.Error == PersistenceError.NotFound
                        ? PersistenceError.CorruptData
                        : metadata.Error,
                    metadata.Message
                );
            token.ThrowIfCancellationRequested();
            try
            {
                var summary = DecodeSummary(metadata.Value);
                using var sha = SHA256.Create();
                if (
                    summary.SnapshotBytes != bytes.Value.Length
                    || !Equal(summary.Hash, sha.ComputeHash(bytes.Value))
                )
                    throw new InvalidDataException("Snapshot integrity check failed.");
                return PersistenceResult<StoredSnapshot<T>>.Success(
                    new StoredSnapshot<T>(Schema.Read(bytes.Value), summary, bytes.Value)
                );
            }
            catch (SnapshotSchemaException error)
            {
                return PersistenceResult<StoredSnapshot<T>>.Failure(error.Error, error.Message);
            }
            catch (Exception error)
            {
                return PersistenceResult<StoredSnapshot<T>>.Failure(
                    PersistenceError.CorruptData,
                    error.Message
                );
            }
        }

        /// <summary>Captures at a caller-selected consistency boundary. Supply Guid.Empty to require an unused slot, null for explicit replacement, or the revision read earlier for compare-and-save.</summary>
        public async UniTask<PersistenceResult<SlotSummary>> SaveAsync(
            int slot,
            T snapshot,
            string title,
            Guid? expectedRevision = null,
            CancellationToken token = default
        )
        {
            if (slot < 1 || title == null || Encoding.UTF8.GetByteCount(title) > 2048)
                return PersistenceResult<SlotSummary>.Failure(PersistenceError.InvalidArgument);
            token.ThrowIfCancellationRequested();
            byte[] bytes;
            try
            {
                bytes = Schema.Write(snapshot);
            }
            catch (Exception error)
            {
                return PersistenceResult<SlotSummary>.Failure(
                    PersistenceError.InvalidArgument,
                    error.Message
                );
            }
            var begun = await storage.BeginTransactionAsync(slot);
            if (!begun.Succeeded)
                return PersistenceResult<SlotSummary>.Failure(begun.Error, begun.Message);
            using var transaction = begun.Value;
            var existing = await transaction.ReadAsync(SnapshotFile);
            if (!existing.Succeeded && existing.Error != PersistenceError.NotFound)
                return PersistenceResult<SlotSummary>.Failure(existing.Error, existing.Message);
            if (!existing.Succeeded && transaction.HasPublishedState)
                return PersistenceResult<SlotSummary>.Failure(
                    PersistenceError.CorruptData,
                    "Published slot is missing its snapshot file."
                );
            Guid revision = Guid.Empty;
            if (existing.Succeeded)
            {
                var current = await ReadAsync(transaction, token);
                if (!current.Succeeded)
                    return PersistenceResult<SlotSummary>.Failure(current.Error, current.Message);
                revision = current.Value.Summary.Revision;
            }
            if (expectedRevision.HasValue && expectedRevision.Value != revision)
                return PersistenceResult<SlotSummary>.Failure(
                    PersistenceError.Conflict,
                    "Local slot changed."
                );
            using var sha = SHA256.Create();
            var summary = new SlotSummary(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                title,
                bytes.Length,
                sha.ComputeHash(bytes)
            );
            var write = await transaction.WriteAsync(SnapshotFile, bytes);
            if (!write.Succeeded)
                return PersistenceResult<SlotSummary>.Failure(write.Error, write.Message);
            write = await transaction.WriteAsync(SummaryFile, EncodeSummary(summary));
            if (!write.Succeeded)
                return PersistenceResult<SlotSummary>.Failure(write.Error, write.Message);
            token.ThrowIfCancellationRequested();
            var commit = await transaction.CommitAsync();
            // Never turn an already committed local write into a cancellation result.
            return commit.Succeeded
                ? PersistenceResult<SlotSummary>.Success(summary)
                : PersistenceResult<SlotSummary>.Failure(commit.Error, commit.Message);
        }

        /// <summary>Lists retained immutable generations for explicit game-selected recovery.</summary>
        public PersistenceResult<SaveVersion[]> ListRecoveryVersions(int slot) =>
            storage is IRecoverableSaveService recovery
                ? recovery.ListVersions(slot)
                : PersistenceResult<SaveVersion[]>.Failure(PersistenceError.Unsupported);

        /// <summary>Validates metadata, checksum, codec, schema and game invariants before restoring a selected generation.</summary>
        public UniTask<PersistenceResult> RecoverAsync(
            int slot,
            string generation,
            CancellationToken token = default
        ) =>
            storage is IRecoverableSaveService recovery
                ? recovery.RestoreVersionAsync(
                    slot,
                    generation,
                    async (reader, cancellation) =>
                        (await ReadAsync(reader, cancellation)).Untyped(),
                    token
                )
                : UniTask.FromResult(PersistenceResult.Failure(PersistenceError.Unsupported));

        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            int difference = 0;
            for (int i = 0; i < a.Length; i++)
                difference |= a[i] ^ b[i];
            return difference == 0;
        }

        private static byte[] EncodeSummary(SlotSummary value)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(0x31534c54);
            writer.Write(value.Revision.ToByteArray());
            writer.Write(value.SavedAt.UtcDateTime.Ticks);
            writer.Write(value.SnapshotBytes);
            writer.Write(value.Hash);
            byte[] title = Encoding.UTF8.GetBytes(value.Title);
            writer.Write(title.Length);
            writer.Write(title);
            return stream.ToArray();
        }

        private static SlotSummary DecodeSummary(byte[] data)
        {
            if (data == null || data.Length < 68 || data.Length > 2116)
                throw new InvalidDataException("Invalid slot summary size.");
            using var stream = new MemoryStream(data, false);
            using var reader = new BinaryReader(stream, new UTF8Encoding(false, true));
            if (reader.ReadInt32() != 0x31534c54)
                throw new InvalidDataException("Invalid slot summary format.");
            var revision = new Guid(reader.ReadBytes(16));
            var at = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
            int length = reader.ReadInt32();
            byte[] hash = reader.ReadBytes(32);
            int titleLength = reader.ReadInt32();
            if (
                revision == Guid.Empty
                || length < 1
                || titleLength < 0
                || titleLength > 2048
                || titleLength != stream.Length - stream.Position
            )
                throw new InvalidDataException("Invalid slot summary.");
            return new SlotSummary(
                revision,
                at,
                new UTF8Encoding(false, true).GetString(reader.ReadBytes(titleLength)),
                length,
                hash
            );
        }
    }
}
