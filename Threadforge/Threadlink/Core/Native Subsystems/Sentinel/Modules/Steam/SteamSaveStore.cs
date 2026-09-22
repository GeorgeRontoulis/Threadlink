namespace Threadlink.SentinelModules.Steam
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Core.NativeSubsystems.Sentinel;
    using Steamworks;

    internal sealed class SteamSaveStore : IDisposable
    {
        private const int MAX_STEAM_FILE_BYTES = 100 * 1024 * 1024;

        private int RetainedGenerations { get; }
        private Dictionary<int, SemaphoreSlim> Gates { get; set; } = new();

        internal SteamSaveStore(int retainedGenerations)
        {
            RetainedGenerations = retainedGenerations < 1 ? 1 : retainedGenerations;
        }

        internal SemaphoreSlim GetGate(int saveID)
        {
            lock (Gates)
            {
                if (!Gates.TryGetValue(saveID, out var gate))
                {
                    gate = new SemaphoreSlim(1, 1);
                    Gates.Add(saveID, gate);
                }

                return gate;
            }
        }

        internal SentinelResult<SteamSaveSnapshot> LoadLatest(int saveID)
        {
            if (saveID == 0)
            {
                return SentinelResult<SteamSaveSnapshot>.Failure(
                    SentinelError.InvalidArgument,
                    "Save ID 0 is reserved."
                );
            }

            var prefix = GetPrefix(saveID);
            var count = SteamRemoteStorage.GetFileCount();
            var latest = default(SteamSaveSnapshot);
            var generationTie = false;
            var sawCandidate = false;

            for (var i = 0; i < count; i++)
            {
                var name = SteamRemoteStorage.GetFileNameAndSize(i, out var size);

                if (
                    string.IsNullOrEmpty(name)
                    || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                sawCandidate = true;

                if (size <= 0 || size > MAX_STEAM_FILE_BYTES)
                    continue;

                var data = new byte[size];
                var read = SteamRemoteStorage.FileRead(name, data, size);

                if (read != size)
                    continue;

                var parsed = SteamSaveEnvelope.Deserialize(name, data);

                if (!parsed.Succeeded || parsed.Value.SaveID != saveID)
                    continue;

                var snapshot = parsed.Value;

                if (latest == null || snapshot.Generation > latest.Generation)
                {
                    latest = snapshot;
                    generationTie = false;
                }
                else if (
                    snapshot.Generation == latest.Generation
                    && !string.Equals(
                        snapshot.RemoteFileName,
                        latest.RemoteFileName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    generationTie = true;
                }
            }

            if (generationTie)
            {
                return SentinelResult<SteamSaveSnapshot>.Failure(
                    SentinelError.Conflict,
                    $"Steam Cloud contains divergent generation {latest.Generation} for save {saveID}."
                );
            }

            if (latest == null && sawCandidate)
            {
                return SentinelResult<SteamSaveSnapshot>.Failure(
                    SentinelError.CorruptData,
                    $"Steam Remote Storage contains Sentinel save data for {saveID}, but no generation passed integrity validation."
                );
            }

            return SentinelResult<SteamSaveSnapshot>.Success(
                latest ?? SteamSaveSnapshot.Missing(saveID)
            );
        }

        internal SentinelResult Commit(
            SteamSaveSnapshot expectedBase,
            Dictionary<int, byte[]> files,
            bool deleted
        )
        {
            var saveID = expectedBase.SaveID;
            var latest = LoadLatest(saveID);

            if (!latest.Succeeded)
                return latest.Untyped();

            if (!SameGeneration(latest.Value, expectedBase))
            {
                return SentinelResult.Failure(
                    SentinelError.Conflict,
                    $"Steam save {saveID} changed after this transaction began."
                );
            }

            var generation = expectedBase.Generation + 1;
            var serialized = SteamSaveEnvelope.Serialize(saveID, generation, deleted, files);

            if (!serialized.Succeeded)
                return serialized.Untyped();

            var bytes = serialized.Value;

            if (bytes.Length > MAX_STEAM_FILE_BYTES)
            {
                return SentinelResult.Failure(
                    SentinelError.QuotaExceeded,
                    "Serialized Steam save generation exceeds Steam Remote Storage's 100 MB FileWrite limit."
                );
            }

            if (
                SteamRemoteStorage.GetQuota(out _, out var available)
                && (ulong)bytes.Length > available
            )
            {
                return SentinelResult.Failure(
                    SentinelError.QuotaExceeded,
                    $"Steam Cloud reports only {available} bytes available; the save needs {bytes.Length}."
                );
            }

            var remoteFile = BuildGenerationFileName(saveID, generation);
            var beganBatch = SteamRemoteStorage.BeginFileWriteBatch();
            var written = false;

            try
            {
                written = SteamRemoteStorage.FileWrite(remoteFile, bytes, bytes.Length);
            }
            finally
            {
                if (beganBatch)
                    SteamRemoteStorage.EndFileWriteBatch();
            }

            if (!written)
            {
                return SentinelResult.Failure(
                    SentinelError.NativeFailure,
                    $"SteamRemoteStorage.FileWrite failed for generation '{remoteFile}'."
                );
            }

            // Commit is complete. Retention cleanup cannot invalidate it.
            CleanupOldGenerations(saveID);
            return SentinelResult.Success();
        }

        public void Dispose()
        {
            if (Gates == null)
                return;

            lock (Gates)
            {
                foreach (var gate in Gates.Values)
                    gate.Dispose();

                Gates.Clear();
            }

            Gates = null;
        }

        private void CleanupOldGenerations(int saveID)
        {
            try
            {
                var generations = EnumerateValidGenerations(saveID)
                    .OrderByDescending(x => x.Generation)
                    .ThenByDescending(x => x.RemoteFileName)
                    .ToList();

                for (var i = RetainedGenerations; i < generations.Count; i++)
                    SteamRemoteStorage.FileDelete(generations[i].RemoteFileName);
            }
            catch
            {
                // The commit has already succeeded; cleanup is deliberately non-fatal.
            }
        }

        private IEnumerable<SteamSaveSnapshot> EnumerateValidGenerations(int saveID)
        {
            var prefix = GetPrefix(saveID);
            var count = SteamRemoteStorage.GetFileCount();

            for (var i = 0; i < count; i++)
            {
                var name = SteamRemoteStorage.GetFileNameAndSize(i, out var size);

                if (
                    string.IsNullOrEmpty(name)
                    || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    || size <= 0
                    || size > MAX_STEAM_FILE_BYTES
                )
                {
                    continue;
                }

                var data = new byte[size];

                if (SteamRemoteStorage.FileRead(name, data, size) != size)
                    continue;

                var parsed = SteamSaveEnvelope.Deserialize(name, data);

                if (parsed.Succeeded && parsed.Value.SaveID == saveID)
                    yield return parsed.Value;
            }
        }

        private static bool SameGeneration(SteamSaveSnapshot left, SteamSaveSnapshot right)
        {
            if (left.Generation != right.Generation)
                return false;

            return string.Equals(
                left.RemoteFileName,
                right.RemoteFileName,
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static string GetPrefix(int saveID) => $"tl2_s{saveID}_g";

        private static string BuildGenerationFileName(int saveID, long generation) =>
            $"{GetPrefix(saveID)}{generation:D20}_{Guid.NewGuid():N}.sent";
    }
}
