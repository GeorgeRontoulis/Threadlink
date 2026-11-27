namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Buffers.Binary;
    using System.Text;
    using System.Threading;

    /// <summary>Steam Cloud (Remote Storage) slots with compare-and-publish over an explicit version envelope.</summary>
    /// <remarks>
    /// Steam Cloud keeps one copy per file, so <see cref="ExposesDivergentVersions"/> is false: the current remote version
    /// is returned and stale publications fail with <see cref="PlatformError.Conflict"/>. Steam's own launch-time
    /// conflict dialog remains Steam's responsibility. Files are named <c>{CloudFilePrefix}{slot}.tlc</c>.
    /// </remarks>
    public sealed class SteamCloudStorage : ICloudStorage
    {
        private const int HeaderBytes = 4 + 16 + 16 + 4;
        private static readonly byte[] Magic = { (byte)'T', (byte)'L', (byte)'S', (byte)'C' };
        private readonly SteamProvider provider;

        internal SteamCloudStorage(SteamProvider provider) => this.provider = provider;

        /// <inheritdoc />
        public bool ExposesDivergentVersions => false;

        /// <summary>Steam Cloud file name for a slot.</summary>
        public string FileName(int slot) => provider.Options.CloudFilePrefix + slot + ".tlc";

        /// <inheritdoc />
        public UniTask<PlatformResult<CloudSaveVersion[]>> ReadVersionsAsync(CloudAddress address, CancellationToken token)
        {
            if (!Check(address, token, out PlatformResult<CloudSaveVersion[]> failure))
                return UniTask.FromResult(failure);
            var current = Read(address.Slot, out var error);
            if (error != null)
                return UniTask.FromResult(PlatformResult<CloudSaveVersion[]>.Failure(PlatformError.InvalidData, error));
            return UniTask.FromResult(
                PlatformResult<CloudSaveVersion[]>.Success(current == null ? Array.Empty<CloudSaveVersion>() : new[] { current })
            );
        }

        /// <inheritdoc />
        public UniTask<PlatformResult<CloudSaveVersion>> PublishAsync(
            CloudAddress address,
            CloudSaveVersion value,
            string expectedVersion,
            CancellationToken token
        )
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (!Check(address, token, out PlatformResult<CloudSaveVersion> failure))
                return UniTask.FromResult(failure);
            var current = Read(address.Slot, out var error);
            if (error != null)
                return UniTask.FromResult(PlatformResult<CloudSaveVersion>.Failure(PlatformError.InvalidData, error));
            if (current?.Version != expectedVersion)
                return UniTask.FromResult(PlatformResult<CloudSaveVersion>.Failure(PlatformError.Conflict, "Steam Cloud slot changed."));
            var published = new CloudSaveVersion(Guid.NewGuid().ToString("N"), value.Export(), value.LocalRevision, value.Title);
            if (!provider.Backend.RemoteFileWrite(FileName(address.Slot), Encode(published)))
                return UniTask.FromResult(PlatformResult<CloudSaveVersion>.Failure(PlatformError.QuotaExceeded, "Steam Cloud rejected the write."));
            return UniTask.FromResult(PlatformResult<CloudSaveVersion>.Success(published));
        }

        private bool Check<T>(CloudAddress address, CancellationToken token, out PlatformResult<T> failure)
        {
            if (!provider.Ready(Capability.CloudStorage, token, out failure))
                return false;
            if (provider.Current != address.Account)
            {
                failure = PlatformResult<T>.Failure(PlatformError.AccountChanged, "Steam Cloud belongs to the signed-in Steam account only.");
                return false;
            }
            return true;
        }

        private CloudSaveVersion Read(int slot, out string error)
        {
            error = null;
            string name = FileName(slot);
            if (!provider.Backend.RemoteFileExists(name))
                return null;
            var bytes = provider.Backend.RemoteFileRead(name);
            if (bytes == null || bytes.Length < HeaderBytes || !bytes.AsSpan(0, 4).SequenceEqual(Magic))
            {
                error = "Steam Cloud file " + name + " is not a Threadlink slot.";
                return null;
            }
            try
            {
                var version = new Guid(bytes.AsSpan(4, 16)).ToString("N");
                var revision = new Guid(bytes.AsSpan(20, 16));
                int titleLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(36));
                if (titleLength < 0 || titleLength > bytes.Length - HeaderBytes)
                    throw new FormatException("Invalid title length.");
                string title = new UTF8Encoding(false, true).GetString(bytes, HeaderBytes, titleLength);
                var payload = bytes.AsSpan(HeaderBytes + titleLength).ToArray();
                return new CloudSaveVersion(version, payload, revision, title);
            }
            catch (Exception exception)
            {
                error = "Steam Cloud file " + name + " is damaged: " + exception.Message;
                return null;
            }
        }

        private static byte[] Encode(CloudSaveVersion value)
        {
            byte[] title = Encoding.UTF8.GetBytes(value.Title);
            byte[] payload = value.Export();
            var bytes = new byte[HeaderBytes + title.Length + payload.Length];
            Magic.CopyTo(bytes, 0);
            Guid.ParseExact(value.Version, "N").TryWriteBytes(bytes.AsSpan(4, 16));
            value.LocalRevision.TryWriteBytes(bytes.AsSpan(20, 16));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), title.Length);
            title.CopyTo(bytes, HeaderBytes);
            payload.CopyTo(bytes, HeaderBytes + title.Length);
            return bytes;
        }
    }
}
