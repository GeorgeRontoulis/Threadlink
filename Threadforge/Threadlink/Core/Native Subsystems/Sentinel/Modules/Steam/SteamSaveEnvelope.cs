namespace Threadlink.SentinelModules.Steam
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using Core.NativeSubsystems.Sentinel;

    internal sealed class SteamSaveSnapshot
    {
        internal int SaveID { get; }
        internal long Generation { get; }
        internal string RemoteFileName { get; }
        internal bool IsDeleted { get; }
        internal Dictionary<int, byte[]> Files { get; }

        internal SteamSaveSnapshot(
            int saveID,
            long generation,
            string remoteFileName,
            bool isDeleted,
            Dictionary<int, byte[]> files
        )
        {
            SaveID = saveID;
            Generation = generation;
            RemoteFileName = remoteFileName;
            IsDeleted = isDeleted;
            Files = files ?? new Dictionary<int, byte[]>();
        }

        internal static SteamSaveSnapshot Missing(int saveID) =>
            new(saveID, 0, null, true, new Dictionary<int, byte[]>());
    }

    internal static class SteamSaveEnvelope
    {
        private const int MAGIC = 0x32534C54; // TLS2
        private const int VERSION = 1;
        private const int HASH_BYTES = 32;
        private const int MAX_FILES = 4096;
        private const int MAX_FILE_BYTES = 100 * 1024 * 1024;

        internal static SentinelResult<byte[]> Serialize(
            int saveID,
            long generation,
            bool deleted,
            Dictionary<int, byte[]> files
        )
        {
            try
            {
                using var payloadStream = new MemoryStream();
                using var writer = new BinaryWriter(payloadStream);

                writer.Write(MAGIC);
                writer.Write(VERSION);
                writer.Write(saveID);
                writer.Write(generation);
                writer.Write(deleted);

                var count = deleted || files == null ? 0 : files.Count;

                if (count > MAX_FILES)
                {
                    return SentinelResult<byte[]>.Failure(
                        SentinelError.QuotaExceeded,
                        $"Save contains {count} files; Sentinel Steam supports at most {MAX_FILES}."
                    );
                }

                writer.Write(count);

                if (!deleted && files != null)
                {
                    foreach (var pair in files.OrderBy(x => x.Key))
                    {
                        if (pair.Key == 0 || pair.Value == null)
                        {
                            return SentinelResult<byte[]>.Failure(
                                SentinelError.InvalidArgument,
                                "Save contains an invalid file ID or null payload."
                            );
                        }

                        if (pair.Value.Length > MAX_FILE_BYTES)
                        {
                            return SentinelResult<byte[]>.Failure(
                                SentinelError.QuotaExceeded,
                                $"Save file {pair.Key} exceeds Steam Remote Storage's 100 MB per-file limit."
                            );
                        }

                        writer.Write(pair.Key);
                        writer.Write(pair.Value.Length);
                        writer.Write(pair.Value);
                    }
                }

                writer.Flush();

                var payload = payloadStream.ToArray();

                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(payload);

                using var result = new MemoryStream(payload.Length + HASH_BYTES);
                result.Write(payload, 0, payload.Length);
                result.Write(hash, 0, hash.Length);

                return SentinelResult<byte[]>.Success(result.ToArray());
            }
            catch (Exception exception)
            {
                return SentinelResult<byte[]>.Failure(
                    SentinelError.NativeFailure,
                    exception.Message
                );
            }
        }

        internal static SentinelResult<SteamSaveSnapshot> Deserialize(
            string remoteFileName,
            byte[] data
        )
        {
            if (data == null || data.Length <= HASH_BYTES)
            {
                return SentinelResult<SteamSaveSnapshot>.Failure(
                    SentinelError.CorruptData,
                    $"Steam save generation '{remoteFileName}' is too small."
                );
            }

            var payloadLength = data.Length - HASH_BYTES;
            var payload = new byte[payloadLength];
            var expectedHash = new byte[HASH_BYTES];

            Buffer.BlockCopy(data, 0, payload, 0, payloadLength);
            Buffer.BlockCopy(data, payloadLength, expectedHash, 0, HASH_BYTES);

            using var sha = SHA256.Create();
            var actualHash = sha.ComputeHash(payload);

            if (!HashesEqual(actualHash, expectedHash))
            {
                return SentinelResult<SteamSaveSnapshot>.Failure(
                    SentinelError.CorruptData,
                    $"Steam save generation '{remoteFileName}' failed its SHA-256 integrity check."
                );
            }

            try
            {
                using var stream = new MemoryStream(payload, false);
                using var reader = new BinaryReader(stream);

                if (reader.ReadInt32() != MAGIC)
                {
                    return SentinelResult<SteamSaveSnapshot>.Failure(
                        SentinelError.CorruptData,
                        $"Steam save generation '{remoteFileName}' has an invalid signature."
                    );
                }

                if (reader.ReadInt32() != VERSION)
                {
                    return SentinelResult<SteamSaveSnapshot>.Failure(
                        SentinelError.CorruptData,
                        $"Steam save generation '{remoteFileName}' uses an unsupported format version."
                    );
                }

                var saveID = reader.ReadInt32();
                var generation = reader.ReadInt64();
                var deleted = reader.ReadBoolean();
                var count = reader.ReadInt32();

                if (saveID == 0 || generation <= 0 || count < 0 || count > MAX_FILES)
                {
                    return SentinelResult<SteamSaveSnapshot>.Failure(
                        SentinelError.CorruptData,
                        $"Steam save generation '{remoteFileName}' contains invalid metadata."
                    );
                }

                var files = new Dictionary<int, byte[]>(count);

                for (var i = 0; i < count; i++)
                {
                    var fileID = reader.ReadInt32();
                    var length = reader.ReadInt32();

                    if (
                        fileID == 0
                        || length < 0
                        || length > MAX_FILE_BYTES
                        || stream.Position + length > stream.Length
                    )
                    {
                        return SentinelResult<SteamSaveSnapshot>.Failure(
                            SentinelError.CorruptData,
                            $"Steam save generation '{remoteFileName}' contains invalid file data."
                        );
                    }

                    var bytes = reader.ReadBytes(length);

                    if (bytes.Length != length || !files.TryAdd(fileID, bytes))
                    {
                        return SentinelResult<SteamSaveSnapshot>.Failure(
                            SentinelError.CorruptData,
                            $"Steam save generation '{remoteFileName}' contains duplicate or truncated file data."
                        );
                    }
                }

                if (stream.Position != stream.Length)
                {
                    return SentinelResult<SteamSaveSnapshot>.Failure(
                        SentinelError.CorruptData,
                        $"Steam save generation '{remoteFileName}' contains trailing payload data."
                    );
                }

                return SentinelResult<SteamSaveSnapshot>.Success(
                    new SteamSaveSnapshot(saveID, generation, remoteFileName, deleted, files)
                );
            }
            catch (Exception exception)
            {
                return SentinelResult<SteamSaveSnapshot>.Failure(
                    SentinelError.CorruptData,
                    $"Steam save generation '{remoteFileName}' could not be decoded: {exception.Message}"
                );
            }
        }

        private static bool HashesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            var difference = 0;

            for (var i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];

            return difference == 0;
        }
    }
}
