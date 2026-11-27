namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Threading;

    public enum SynchronizationState
    {
        Uploaded,
        Conflict,
        Failed,
        Cancelled,
    }

    /// <summary>Remote outcome separate from local commitment. Conflicts retain the provider's distinct immutable versions.</summary>
    public sealed class SynchronizationOutcome
    {
        public Guid LocalRevision { get; }
        public SynchronizationState State { get; }
        public string Detail { get; }
        public bool MayHaveCommittedRemotely { get; }
        public IReadOnlyList<CloudSaveVersion> Conflicts { get; }

        internal SynchronizationOutcome(
            Guid revision,
            SynchronizationState state,
            string detail = null,
            bool mayHaveCommitted = false,
            CloudSaveVersion[] conflicts = null
        )
        {
            LocalRevision = revision;
            State = state;
            Detail = detail;
            MayHaveCommittedRemotely = mayHaveCommitted;
            Conflicts = Array.AsReadOnly(conflicts ?? Array.Empty<CloudSaveVersion>());
        }
    }

    /// <summary>Explicit account-bound cloud publication and game-selected conflict resolution. No automatic retry, merge or timestamp winner.</summary>
    public sealed class CloudSynchronization<T>
    {
        private readonly AccountSnapshot<T> local;
        private readonly ICloudStorage remote;
        private readonly int maximumVersions;

        public CloudSynchronization(
            AccountSnapshot<T> local,
            ICloudStorage remote,
            int maximumVersions = 64
        )
        {
            this.local = local ?? throw new ArgumentNullException(nameof(local));
            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
            if (maximumVersions < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumVersions));
            this.maximumVersions = maximumVersions;
        }

        public async UniTask<PlatformResult<CloudSaveVersion[]>> FetchAsync(
            int slot,
            CancellationToken token = default
        )
        {
            if (!local.Account.IsCurrent)
                return PlatformResult<CloudSaveVersion[]>.Failure(PlatformError.AccountChanged);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                local.Account.Token
            );
            try
            {
                var versions = await remote.ReadVersionsAsync(
                    new CloudAddress(local.Account.Account, slot),
                    linked.Token
                );
                if (!local.Account.IsCurrent)
                    return PlatformResult<CloudSaveVersion[]>.Failure(PlatformError.AccountChanged);
                if (!versions.Succeeded)
                    return versions;
                if (versions.Value == null || versions.Value.Length > maximumVersions)
                    return PlatformResult<CloudSaveVersion[]>.Failure(
                        PlatformError.InvalidData,
                        "Invalid cloud version count."
                    );
                foreach (var version in versions.Value)
                    if (version == null || version.ByteCount > local.Store.Schema.MaximumBytes + 40)
                        return PlatformResult<CloudSaveVersion[]>.Failure(
                            PlatformError.InvalidData,
                            "Cloud version exceeds schema limit."
                        );
                return PlatformResult<CloudSaveVersion[]>.Success(
                    (CloudSaveVersion[])versions.Value.Clone()
                );
            }
            catch (OperationCanceledException)
            {
                return PlatformResult<CloudSaveVersion[]>.Failure(
                    local.Account.IsCurrent ? PlatformError.Cancelled : PlatformError.AccountChanged
                );
            }
            catch (Exception error)
            {
                return PlatformResult<CloudSaveVersion[]>.Failure(
                    PlatformError.NativeFailure,
                    error.Message
                );
            }
        }

        public async UniTask<SynchronizationOutcome> UploadAsync(
            int slot,
            string expectedRemoteVersion,
            CancellationToken token = default
        )
        {
            Guid revision = Guid.Empty;
            bool submitted = false;
            if (!local.Account.IsCurrent)
                return new SynchronizationOutcome(
                    revision,
                    SynchronizationState.Cancelled,
                    "Account changed."
                );
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                local.Account.Token
            );
            try
            {
                var read = await local.Store.ReadAsync(slot, linked.Token);
                if (!read.Succeeded)
                    return new SynchronizationOutcome(
                        revision,
                        SynchronizationState.Failed,
                        read.Error + ": " + read.Message
                    );
                revision = read.Value.Summary.Revision;
                local.Account.ThrowIfInvalid();
                linked.Token.ThrowIfCancellationRequested();
                var candidate = new CloudSaveVersion(
                    string.Empty,
                    Encode(read.Value.Export()),
                    revision,
                    read.Value.Summary.Title
                );
                submitted = true;
                var publication = await remote.PublishAsync(
                    new CloudAddress(local.Account.Account, slot),
                    candidate,
                    expectedRemoteVersion,
                    linked.Token
                );
                if (!local.Account.IsCurrent)
                    return new SynchronizationOutcome(
                        revision,
                        SynchronizationState.Cancelled,
                        "Account changed; reconcile the previous account's remote publication before retry.",
                        true
                    );
                if (publication.Succeeded)
                    return new SynchronizationOutcome(revision, SynchronizationState.Uploaded);
                if (publication.Error == PlatformError.Conflict)
                {
                    var conflicts = await FetchAsync(slot, linked.Token);
                    return new SynchronizationOutcome(
                        revision,
                        SynchronizationState.Conflict,
                        conflicts.Succeeded ? publication.Detail : conflicts.Detail,
                        publication.MayHaveCommitted,
                        conflicts.Succeeded ? conflicts.Value : null
                    );
                }
                return new SynchronizationOutcome(
                    revision,
                    publication.Error == PlatformError.Cancelled
                    || publication.Error == PlatformError.AccountChanged
                        ? SynchronizationState.Cancelled
                        : SynchronizationState.Failed,
                    publication.Detail,
                    publication.MayHaveCommitted
                );
            }
            catch (OperationCanceledException)
            {
                return new SynchronizationOutcome(
                    revision,
                    SynchronizationState.Cancelled,
                    local.Account.IsCurrent ? "Synchronization cancelled." : "Account changed.",
                    submitted
                );
            }
            catch (Exception error)
            {
                return new SynchronizationOutcome(
                    revision,
                    SynchronizationState.Failed,
                    error.Message,
                    submitted
                );
            }
        }

        /// <summary>Decodes a selected remote candidate without applying it to local storage or game state.</summary>
        public PersistenceResult<T> Preview(CloudSaveVersion version)
        {
            if (!local.Account.IsCurrent)
                return PersistenceResult<T>.Failure(PersistenceError.AccountUnavailable);
            try
            {
                return PersistenceResult<T>.Success(local.Store.Schema.Read(Decode(version)));
            }
            catch (SnapshotSchemaException error)
            {
                return PersistenceResult<T>.Failure(error.Error, error.Message);
            }
            catch (Exception error)
            {
                return PersistenceResult<T>.Failure(PersistenceError.CorruptData, error.Message);
            }
        }

        /// <summary>Explicitly accepts one remote version using a local compare-and-save. The caller separately chooses remote publication.</summary>
        public UniTask<PersistenceResult<SlotSummary>> AcceptRemoteAsync(
            int slot,
            CloudSaveVersion selected,
            Guid expectedLocalRevision,
            CancellationToken token = default
        )
        {
            var preview = Preview(selected);
            if (!preview.Succeeded)
                return UniTask.FromResult(
                    PersistenceResult<SlotSummary>.Failure(preview.Error, preview.Message)
                );
            local.Account.ThrowIfInvalid();
            return local.Store.SaveAsync(
                slot,
                preview.Value,
                selected.Title,
                expectedLocalRevision,
                token
            );
        }

        private static byte[] Encode(byte[] bytes)
        {
            var output = new byte[bytes.Length + 40];
            output[0] = (byte)'T';
            output[1] = (byte)'L';
            output[2] = (byte)'C';
            output[3] = (byte)'P';
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(4), bytes.Length);
            using var sha = SHA256.Create();
            sha.ComputeHash(bytes).CopyTo(output, 8);
            bytes.CopyTo(output, 40);
            return output;
        }

        private byte[] Decode(CloudSaveVersion version)
        {
            if (
                version == null
                || version.ByteCount < 41
                || version.ByteCount > local.Store.Schema.MaximumBytes + 40
            )
                throw new InvalidOperationException("Invalid cloud snapshot length.");
            byte[] bytes = version.Export();
            if (
                bytes[0] != 'T'
                || bytes[1] != 'L'
                || bytes[2] != 'C'
                || bytes[3] != 'P'
                || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)) != bytes.Length - 40
            )
                throw new InvalidOperationException("Invalid cloud snapshot frame.");
            var payload = bytes.AsSpan(40).ToArray();
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(payload);
            int different = 0;
            for (int i = 0; i < hash.Length; i++)
                different |= bytes[8 + i] ^ hash[i];
            if (different != 0)
                throw new InvalidOperationException("Cloud snapshot checksum failed.");
            return payload;
        }
    }
}
